using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace sftp.TestServer;

/// <summary>
/// One SSH connection: transport (ecdh-sha2-nistp256, ecdsa-sha2-nistp256, aes256-gcm@openssh.com),
/// user authentication (none, password, keyboard-interactive, publickey) and session channels.
/// Deliberately small: only what the integration tests and SSH.NET need.
/// </summary>
internal sealed class SshConnection : IDisposable {
    private const string ServerVersion = "SSH-2.0-sftpTestServer_1.0";
    private const uint InitialWindow = 4 * 1024 * 1024;
    private const uint MaxPacket = 32 * 1024;

    private readonly Socket _socket;
    private readonly NetworkStream _stream;
    private readonly ServerOptions _options;
    private readonly Lock _sendLock = new();
    private readonly ConcurrentDictionary<uint, Channel> _channels = new();
    private byte[] _sessionId = [];
    private string _clientVersion = "";
    private byte[] _clientKexInit = [];
    private byte[] _serverKexInit = [];
    private AesGcm? _encrypt;
    private AesGcm? _decrypt;
    private byte[] _encryptNonce = [];
    private byte[] _decryptNonce = [];
    private uint _sendSequence;
    private uint _receiveSequence;
    private uint _nextChannel;
    private string _user = "";
    private bool _authenticated;

    public SshConnection(Socket socket, ServerOptions options) {
        _socket = socket;
        _stream = new NetworkStream(socket, ownsSocket: true);
        _options = options;
    }

    public void Run() {
        try {
            _stream.Write(Encoding.ASCII.GetBytes(ServerVersion + "\r\n"));
            _clientVersion = ReadVersionLine();
            SendKexInit();
            while (true) {
                byte[] payload = ReadPacket();
                if (!Handle(payload)) {
                    break;
                }
            }
        }
        catch (Exception error) when (error is IOException or SocketException or EndOfStreamException or CryptographicException or ObjectDisposedException) {
            // The client went away or sent garbage; either way this connection is over.
        }
        finally {
            foreach (Channel channel in _channels.Values) {
                channel.Input.Complete();
            }

            Dispose();
        }
    }

    public void Dispose() {
        _encrypt?.Dispose();
        _decrypt?.Dispose();
        _stream.Dispose();
    }

    private string ReadVersionLine() {
        var line = new StringBuilder();
        while (true) {
            int b = _stream.ReadByte();
            if (b < 0) {
                throw new EndOfStreamException();
            }

            if (b == '\n') {
                string text = line.ToString().TrimEnd('\r');
                if (text.StartsWith("SSH-", StringComparison.Ordinal)) {
                    return text;
                }

                line.Clear();
                continue;
            }

            line.Append((char)b);
        }
    }

    // ---------------------------------------------------------------- packets

    private void ReadExactly(Span<byte> buffer) {
        _stream.ReadExactly(buffer);
    }

    private byte[] ReadPacket() {
        byte[] lengthBytes = new byte[4];
        ReadExactly(lengthBytes);
        int length = (int)BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
        if (length is < 5 or > 256 * 1024) {
            throw new IOException("Bad packet length.");
        }

        byte[] body;
        if (_decrypt is null) {
            body = new byte[length];
            ReadExactly(body);
        }
        else {
            byte[] cipher = new byte[length];
            byte[] tag = new byte[16];
            ReadExactly(cipher);
            ReadExactly(tag);
            body = new byte[length];
            _decrypt.Decrypt(_decryptNonce, cipher, tag, body, lengthBytes);
            IncrementNonce(_decryptNonce);
        }

        _receiveSequence++;
        int padding = body[0];

        return body.AsSpan(1, length - 1 - padding).ToArray();
    }

    internal void SendPacket(byte[] payload) {
        lock (_sendLock) {
            int block = _encrypt is null ? 8 : 16;
            // Without encryption the length field counts toward block alignment; with AES-GCM it does not.
            int lengthCounted = _encrypt is null ? 4 : 0;
            int padding = block - ((lengthCounted + 1 + payload.Length) % block);
            if (padding < 4) {
                padding += block;
            }

            int length = 1 + payload.Length + padding;
            byte[] body = new byte[length];
            body[0] = (byte)padding;
            payload.CopyTo(body, 1);
            RandomNumberGenerator.Fill(body.AsSpan(1 + payload.Length));
            byte[] lengthBytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, (uint)length);
            _stream.Write(lengthBytes);
            if (_encrypt is null) {
                _stream.Write(body);
            }
            else {
                byte[] cipher = new byte[length];
                byte[] tag = new byte[16];
                _encrypt.Encrypt(_encryptNonce, body, cipher, tag, lengthBytes);
                IncrementNonce(_encryptNonce);
                _stream.Write(cipher);
                _stream.Write(tag);
            }

            _sendSequence++;
        }
    }

    private static void IncrementNonce(byte[] nonce) {
        // RFC 5647: the last 8 bytes are a big-endian invocation counter.
        ulong counter = BinaryPrimitives.ReadUInt64BigEndian(nonce.AsSpan(4));
        BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(4), counter + 1);
    }

    // ---------------------------------------------------------------- dispatch

    private bool Handle(byte[] payload) {
        var reader = new SshReader(payload, 1);
        switch (payload[0]) {
            case SshMessage.Disconnect:
                return false;
            case SshMessage.Ignore:
            case SshMessage.Debug:
            case SshMessage.Unimplemented:
                return true;
            case SshMessage.KexInit:
                _clientKexInit = payload;
                return true;
            case SshMessage.KexEcdhInit:
                KeyExchange(reader.ReadBinary());
                return true;
            case SshMessage.NewKeys:
                return true;
            case SshMessage.ServiceRequest:
                string service = reader.ReadString();
                SendPacket(new SshWriter().Byte(SshMessage.ServiceAccept).String(service).ToArray());
                return true;
            case SshMessage.UserAuthRequest:
                Authenticate(reader);
                return true;
            case SshMessage.UserAuthInfoResponse:
                KeyboardInteractiveResponse(reader);
                return true;
            case SshMessage.GlobalRequest:
                reader.ReadString();
                if (reader.ReadBoolean()) {
                    SendPacket([SshMessage.RequestFailure]);
                }

                return true;
            default:
                if (!_authenticated) {
                    return false;
                }

                return HandleChannelMessage(payload[0], reader);
        }
    }

    // ---------------------------------------------------------------- key exchange

    private void SendKexInit() {
        byte[] cookie = RandomNumberGenerator.GetBytes(16);
        _serverKexInit = new SshWriter()
            .Byte(SshMessage.KexInit)
            .Raw(cookie)
            .String("ecdh-sha2-nistp256")
            .String("ecdsa-sha2-nistp256")
            .String("aes256-gcm@openssh.com")
            .String("aes256-gcm@openssh.com")
            // AES-GCM authenticates packets itself; a MAC is still negotiated by name and then ignored.
            .String("hmac-sha2-256")
            .String("hmac-sha2-256")
            .String("none")
            .String("none")
            .String("")
            .String("")
            .Boolean(false)
            .UInt32(0)
            .ToArray();
        SendPacket(_serverKexInit);
    }

    private void KeyExchange(byte[] clientPublic) {
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var client = ECDiffieHellman.Create(new ECParameters {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = clientPublic[1..33], Y = clientPublic[33..65] },
        });
        byte[] secret = ephemeral.DeriveRawSecretAgreement(client.PublicKey);
        ECParameters parameters = ephemeral.ExportParameters(false);
        byte[] serverPublic = [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
        byte[] hostKey = _options.HostKeyBlob;
        byte[] sharedSecret = new SshWriter().MPInt(secret).ToArray();

        byte[] exchangeHash = SHA256.HashData(new SshWriter()
            .String(_clientVersion)
            .String(ServerVersion)
            .Binary(_clientKexInit)
            .Binary(_serverKexInit)
            .Binary(hostKey)
            .Binary(clientPublic)
            .Binary(serverPublic)
            .Raw(sharedSecret)
            .ToArray());
        if (_sessionId.Length == 0) {
            _sessionId = exchangeHash;
        }

        byte[] signature = _options.HostKey.SignData(exchangeHash, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        byte[] signatureBlob = new SshWriter()
            .String("ecdsa-sha2-nistp256")
            .Binary(new SshWriter().MPInt(signature.AsSpan(0, 32)).MPInt(signature.AsSpan(32, 32)).ToArray())
            .ToArray();
        SendPacket(new SshWriter()
            .Byte(SshMessage.KexEcdhReply)
            .Binary(hostKey)
            .Binary(serverPublic)
            .Binary(signatureBlob)
            .ToArray());
        SendPacket([SshMessage.NewKeys]);

        byte[] Derive(char letter) => SHA256.HashData([.. sharedSecret, .. exchangeHash, (byte)letter, .. _sessionId]);
        _decryptNonce = Derive('A')[..12];
        _encryptNonce = Derive('B')[..12];
        // The client switches keys after its own NEWKEYS, which arrives next; the first encrypted packet follows it.
        byte[] newKeys = ReadPacket();
        if (newKeys[0] != SshMessage.NewKeys) {
            throw new IOException("Expected NEWKEYS.");
        }

        _decrypt = new AesGcm(Derive('C'), 16);
        _encrypt = new AesGcm(Derive('D'), 16);
    }

    // ---------------------------------------------------------------- authentication

    private void Authenticate(SshReader reader) {
        string user = reader.ReadString();
        reader.ReadString();
        string method = reader.ReadString();
        bool userOk = user == _options.User;
        switch (method) {
            case "password" when userOk: {
                reader.ReadBoolean();
                string password = reader.ReadString();
                if (_options.Password is not null && password == _options.Password) {
                    Succeed(user);

                    return;
                }

                break;
            }
            case "keyboard-interactive" when userOk && _options.Password is not null:
                _user = user;
                SendPacket(new SshWriter()
                    .Byte(SshMessage.UserAuthInfoRequest)
                    .String("")
                    .String("")
                    .String("")
                    .UInt32(1)
                    .String("Password: ")
                    .Boolean(false)
                    .ToArray());

                return;
            case "publickey" when userOk: {
                bool hasSignature = reader.ReadBoolean();
                string algorithm = reader.ReadString();
                byte[] blob = reader.ReadBinary();
                bool authorized = algorithm == "ecdsa-sha2-nistp256" && _options.AuthorizedKeys.Any(key => key.AsSpan().SequenceEqual(blob));
                if (!authorized) {
                    break;
                }

                if (!hasSignature) {
                    SendPacket(new SshWriter().Byte(SshMessage.UserAuthPkOk).String(algorithm).Binary(blob).ToArray());

                    return;
                }

                byte[] signature = reader.ReadBinary();
                byte[] signed = new SshWriter()
                    .Binary(_sessionId)
                    .Byte(SshMessage.UserAuthRequest)
                    .String(user)
                    .String("ssh-connection")
                    .String("publickey")
                    .Boolean(true)
                    .String(algorithm)
                    .Binary(blob)
                    .ToArray();
                if (VerifyEcdsa(blob, signature, signed)) {
                    Succeed(user);

                    return;
                }

                break;
            }
        }

        Fail();
    }

    private void KeyboardInteractiveResponse(SshReader reader) {
        uint count = reader.ReadUInt32();
        string response = count > 0 ? reader.ReadString() : "";
        if (response == _options.Password) {
            Succeed(_user);
        }
        else {
            Fail();
        }
    }

    private void Succeed(string user) {
        _user = user;
        _authenticated = true;
        SendPacket([SshMessage.UserAuthSuccess]);
    }

    private void Fail() {
        string methods = _options.Password is null ? "publickey" : "publickey,password,keyboard-interactive";
        SendPacket(new SshWriter().Byte(SshMessage.UserAuthFailure).String(methods).Boolean(false).ToArray());
    }

    private static bool VerifyEcdsa(byte[] blob, byte[] signatureBlob, byte[] data) {
        var key = new SshReader(blob);
        key.ReadString();
        key.ReadString();
        byte[] point = key.ReadBinary();
        var signature = new SshReader(signatureBlob);
        if (signature.ReadString() != "ecdsa-sha2-nistp256") {
            return false;
        }

        var numbers = new SshReader(signature.ReadBinary());
        byte[] r = numbers.ReadMPInt();
        byte[] s = numbers.ReadMPInt();
        byte[] fixedSignature = new byte[64];
        r.CopyTo(fixedSignature, 32 - r.Length);
        s.CopyTo(fixedSignature, 64 - s.Length);
        using var ecdsa = ECDsa.Create(new ECParameters {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = point[1..33], Y = point[33..65] },
        });

        return ecdsa.VerifyData(data, fixedSignature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    // ---------------------------------------------------------------- channels

    /// <summary>Local port forwarding (ssh -L): connects to the requested host and port and relays the channel to it.</summary>
    private void OpenForward(SshReader reader, uint peer, uint window, uint maxPacket) {
        string host = reader.ReadString();
        uint port = reader.ReadUInt32();
        var tcp = new TcpClient();
        try {
            tcp.Connect(host, (int)port);
        }
        catch (SocketException error) {
            tcp.Dispose();
            SendPacket(new SshWriter().Byte(SshMessage.ChannelOpenFailure).UInt32(peer).UInt32(2).String(error.Message).String("").ToArray());

            return;
        }

        uint id = _nextChannel++;
        var channel = new Channel(this, id, peer, window, Math.Min(maxPacket, MaxPacket), InitialWindow);
        _channels[id] = channel;
        SendPacket(new SshWriter().Byte(SshMessage.ChannelOpenConfirmation).UInt32(peer).UInt32(id).UInt32(InitialWindow).UInt32(MaxPacket).ToArray());
        channel.Start(_ => Relay(channel, tcp));
    }

    private static void Relay(Channel channel, TcpClient tcp) {
        using (tcp) {
            NetworkStream network = tcp.GetStream();
            var toTarget = new Thread(() => {
                byte[] buffer = new byte[32 * 1024];
                try {
                    int read;
                    while ((read = channel.Input.Read(buffer)) > 0) {
                        network.Write(buffer, 0, read);
                    }

                    tcp.Client.Shutdown(SocketShutdown.Send);
                }
                catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException) {
                    // Either side went away.
                }
            }) { IsBackground = true, Name = "forward " + channel.Id };
            toTarget.Start();
            byte[] buffer = new byte[32 * 1024];
            try {
                int read;
                while ((read = network.Read(buffer)) > 0) {
                    channel.Write(buffer.AsSpan(0, read));
                }
            }
            catch (Exception error) when (error is IOException or SocketException) {
                // The target closed the connection.
            }

            channel.End();
            toTarget.Join(TimeSpan.FromSeconds(5));
        }
    }

    private bool HandleChannelMessage(byte message, SshReader reader) {
        switch (message) {
            case SshMessage.ChannelOpen: {
                string type = reader.ReadString();
                uint peer = reader.ReadUInt32();
                uint window = reader.ReadUInt32();
                uint maxPacket = reader.ReadUInt32();
                if (type == "direct-tcpip") {
                    OpenForward(reader, peer, window, maxPacket);

                    return true;
                }

                if (type != "session") {
                    SendPacket(new SshWriter().Byte(SshMessage.ChannelOpenFailure).UInt32(peer).UInt32(3).String("Only session and direct-tcpip channels.").String("").ToArray());

                    return true;
                }

                uint id = _nextChannel++;
                _channels[id] = new Channel(this, id, peer, window, Math.Min(maxPacket, MaxPacket), InitialWindow);
                SendPacket(new SshWriter().Byte(SshMessage.ChannelOpenConfirmation).UInt32(peer).UInt32(id).UInt32(InitialWindow).UInt32(MaxPacket).ToArray());

                return true;
            }
            case SshMessage.ChannelRequest: {
                Channel channel = _channels[reader.ReadUInt32()];
                string request = reader.ReadString();
                bool wantReply = reader.ReadBoolean();
                bool ok = request switch {
                    "subsystem" => channel.Start(reader.ReadString() == "sftp" ? channel => new SftpSubsystem(channel, _options).Run() : null),
                    "exec" => channel.Start(Scp.Create(reader.ReadString(), _options)),
                    _ => false,
                };
                if (wantReply) {
                    SendPacket(new SshWriter().Byte(ok ? SshMessage.ChannelSuccess : SshMessage.ChannelFailure).UInt32(channel.Peer).ToArray());
                }

                return true;
            }
            case SshMessage.ChannelData: {
                Channel channel = _channels[reader.ReadUInt32()];
                byte[] data = reader.ReadBinary();
                channel.Input.Write(data);
                channel.Consumed((uint)data.Length);

                return true;
            }
            case SshMessage.ChannelWindowAdjust:
                _channels[reader.ReadUInt32()].AddWindow(reader.ReadUInt32());

                return true;
            case SshMessage.ChannelEof:
                _channels[reader.ReadUInt32()].Input.Complete();

                return true;
            case SshMessage.ChannelClose: {
                uint id = reader.ReadUInt32();
                if (_channels.TryRemove(id, out Channel? channel)) {
                    channel.Input.Complete();
                    channel.Close();
                    channel.End(eof: false);
                }

                return true;
            }
            default:
                SendPacket(new SshWriter().Byte(SshMessage.Unimplemented).UInt32(_receiveSequence - 1).ToArray());

                return true;
        }
    }
}

/// <summary>A session or direct-tcpip channel with flow control in both directions.</summary>
internal sealed class Channel(SshConnection connection, uint id, uint peer, uint window, uint maxPacket, uint localWindow) {
    // Monitor rather than System.Threading.Lock: writers wait for window adjustments.
    private readonly object _windowLock = new();
    private uint _remoteWindow = window;
    private uint _consumed;
    private bool _closed;
    private int _closeSent;

    public uint Id { get; } = id;

    public uint Peer { get; } = peer;

    public BlockingStream Input { get; } = new();

    /// <summary>Runs the channel's program on its own thread so the connection can keep reading window adjustments.</summary>
    public bool Start(Action<Channel>? program) {
        if (program is null) {
            return false;
        }

        var thread = new Thread(() => {
            try {
                program(this);
            }
            catch (Exception error) when (error is IOException or EndOfStreamException or ObjectDisposedException or SocketException) {
                // Connection dropped mid-command.
            }
        }) { IsBackground = true, Name = "channel " + Id };
        thread.Start();

        return true;
    }

    public void Write(ReadOnlySpan<byte> data) => Write(data, extended: false);

    public void WriteError(string text) => Write(Encoding.UTF8.GetBytes(text), extended: true);

    private void Write(ReadOnlySpan<byte> data, bool extended) {
        while (data.Length > 0) {
            int chunk;
            lock (_windowLock) {
                while (_remoteWindow == 0 && !_closed) {
                    Monitor.Wait(_windowLock);
                }

                if (_closed) {
                    throw new IOException("Channel closed.");
                }

                chunk = (int)Math.Min(Math.Min(_remoteWindow, maxPacket), (uint)data.Length);
                _remoteWindow -= (uint)chunk;
            }

            var writer = new SshWriter().Byte(extended ? SshMessage.ChannelExtendedData : SshMessage.ChannelData).UInt32(Peer);
            if (extended) {
                writer.UInt32(1);
            }

            connection.SendPacket(writer.Binary(data[..chunk]).ToArray());
            data = data[chunk..];
        }
    }

    public void AddWindow(uint bytes) {
        lock (_windowLock) {
            _remoteWindow += bytes;
            Monitor.PulseAll(_windowLock);
        }
    }

    /// <summary>Returns window to the client once a good share of it has been used.</summary>
    public void Consumed(uint bytes) {
        _consumed += bytes;
        if (_consumed >= localWindow / 2) {
            connection.SendPacket(new SshWriter().Byte(SshMessage.ChannelWindowAdjust).UInt32(Peer).UInt32(_consumed).ToArray());
            _consumed = 0;
        }
    }

    /// <summary>Sends the exit status and closes the channel from the server side.</summary>
    public void Exit(uint status) {
        if (Volatile.Read(ref _closeSent) != 0) {
            return;
        }

        connection.SendPacket(new SshWriter().Byte(SshMessage.ChannelRequest).UInt32(Peer).String("exit-status").Boolean(false).UInt32(status).ToArray());
        End();
    }

    /// <summary>Closes the channel from the server side, once.</summary>
    public void End(bool eof = true) {
        if (Interlocked.Exchange(ref _closeSent, 1) != 0) {
            return;
        }

        if (eof) {
            connection.SendPacket(new SshWriter().Byte(SshMessage.ChannelEof).UInt32(Peer).ToArray());
        }

        connection.SendPacket(new SshWriter().Byte(SshMessage.ChannelClose).UInt32(Peer).ToArray());
    }

    public void Close() {
        lock (_windowLock) {
            _closed = true;
            Monitor.PulseAll(_windowLock);
        }
    }
}

/// <summary>A blocking in-memory pipe from the connection thread to a channel program.</summary>
internal sealed class BlockingStream {
    private readonly BlockingCollection<byte[]> _chunks = [];
    private byte[] _current = [];
    private int _offset;

    public void Write(byte[] data) {
        if (!_chunks.IsAddingCompleted) {
            _chunks.Add(data);
        }
    }

    public void Complete() => _chunks.CompleteAdding();

    /// <summary>Reads up to buffer.Length bytes; returns 0 at end of input.</summary>
    public int Read(Span<byte> buffer) {
        while (_offset >= _current.Length) {
            if (!_chunks.TryTake(out byte[]? next, Timeout.Infinite)) {
                return 0;
            }

            _current = next;
            _offset = 0;
        }

        int count = Math.Min(buffer.Length, _current.Length - _offset);
        _current.AsSpan(_offset, count).CopyTo(buffer);
        _offset += count;

        return count;
    }

    public void ReadExactly(Span<byte> buffer) {
        while (buffer.Length > 0) {
            int read = Read(buffer);
            if (read == 0) {
                throw new EndOfStreamException();
            }

            buffer = buffer[read..];
        }
    }

    /// <summary>Reads one byte, or -1 at end of input.</summary>
    public int ReadByte() {
        Span<byte> one = stackalloc byte[1];

        return Read(one) == 0 ? -1 : one[0];
    }
}

internal static class SshMessage {
    public const byte Disconnect = 1;
    public const byte Ignore = 2;
    public const byte Unimplemented = 3;
    public const byte Debug = 4;
    public const byte ServiceRequest = 5;
    public const byte ServiceAccept = 6;
    public const byte KexInit = 20;
    public const byte NewKeys = 21;
    public const byte KexEcdhInit = 30;
    public const byte KexEcdhReply = 31;
    public const byte UserAuthRequest = 50;
    public const byte UserAuthFailure = 51;
    public const byte UserAuthSuccess = 52;
    public const byte UserAuthPkOk = 60;
    public const byte UserAuthInfoRequest = 60;
    public const byte UserAuthInfoResponse = 61;
    public const byte GlobalRequest = 80;
    public const byte RequestFailure = 82;
    public const byte ChannelOpen = 90;
    public const byte ChannelOpenConfirmation = 91;
    public const byte ChannelOpenFailure = 92;
    public const byte ChannelWindowAdjust = 93;
    public const byte ChannelData = 94;
    public const byte ChannelExtendedData = 95;
    public const byte ChannelEof = 96;
    public const byte ChannelClose = 97;
    public const byte ChannelRequest = 98;
    public const byte ChannelSuccess = 99;
    public const byte ChannelFailure = 100;
}
