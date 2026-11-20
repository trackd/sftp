using Renci.SshNet;
using Renci.SshNet.Common;

namespace sftp;
/// <summary>An open, host-key-verified SFTP connection. Create it with Connect-SftpServer and close it with Disconnect-SftpServer.</summary>
public sealed class SftpSession : IDisposable {
    private readonly ConnectionInfo _connection;
    private readonly HostKeyVerifier _verifier;
    private ScpClient? _scp;
    private SshClient? _ssh;
    private SessionRegistry? _registry;
    private bool _disposed;

    internal SftpSession(ConnectionInfo connection, HostKeyVerifier verifier) {
        _connection = connection;
        _verifier = verifier;
        HostKeyVerifier.Restrict(connection.HostKeyAlgorithms, verifier.KeyTypes);
        VerboseCapture.EnsureInitialized();
        Client = new SftpClient(connection);
        Client.HostKeyReceived += OnHostKeyReceived;
    }

    /// <summary>The session's name, shown on the files and results it produces. Use it with -Session.</summary>
    public string Name { get; private set; } = "";

    /// <summary>The server name.</summary>
    public string HostName => _connection.Host;

    /// <summary>The server port.</summary>
    public int Port => _connection.Port;

    /// <summary>The user name.</summary>
    public string UserName => _connection.Username;

    /// <summary>How the host key was checked: Fingerprint, KnownHosts, KnownHostsAdded or Skipped.</summary>
    public string HostKeyVerification => _verifier.Mode;

    /// <summary>The server's host key, available once connected.</summary>
    public SshHostKey? HostKey { get; private set; }

    /// <summary>True while the SFTP connection is open.</summary>
    public bool IsConnected => !_disposed && Client.IsConnected;

    /// <summary>The remote working directory that relative paths resolve against.</summary>
    public string WorkingDirectory => IsConnected ? Client.WorkingDirectory : "";

    /// <summary>When the session was opened.</summary>
    public DateTime ConnectedAt { get; private set; }

    /// <summary>The user's home folder on the server: where the session started, and what ~ means.</summary>
    public string HomeDirectory { get; private set; } = "";

    /// <summary>True for the session commands use when -Session is omitted.</summary>
    public bool IsCurrent => _registry is not null && ReferenceEquals(_registry.Default, this);

    internal SftpClient Client { get; }

    /// <summary>
    /// Makes a remote path absolute: "~" and "~/..." start at the home folder, other relative paths at the
    /// working directory, and "." and ".." are resolved.
    /// </summary>
    internal string FullPath(string path) {
        string home = HomeDirectory.Length > 0 ? HomeDirectory : Client.WorkingDirectory;
        string full = path switch {
            "~" => home,
            _ when path.StartsWith("~/", StringComparison.Ordinal) => RemotePath.Combine(home, path[2..]),
            _ when path.StartsWith('/') => path,
            _ => RemotePath.Combine(Client.WorkingDirectory, path),
        };

        return RemotePath.Normalize(full);
    }

    /// <summary>The key the server presented when it did not match, for error reporting.</summary>
    internal SshHostKey? RejectedHostKey { get; private set; }

    internal HostKeyVerifier Verifier => _verifier;

    /// <summary>True for sessions owned by a drive or a one-shot transfer; they are never the default session.</summary>
    internal bool IsPrivate { get; private set; }

    internal void Register(SessionRegistry registry, string name, bool isPrivate) {
        _registry = registry;
        Name = name;
        IsPrivate = isPrivate;
    }

    private readonly List<SshTunnel> _tunnels = [];

    /// <summary>The tunnels opened on this session that are still open.</summary>
    internal IReadOnlyList<SshTunnel> Tunnels {
        get {
            lock (_tunnels) {
                return [.. _tunnels];
            }
        }
    }

    /// <summary>Starts local port forwarding on the session's SSH connection.</summary>
    internal SshTunnel OpenTunnel(string localAddress, uint localPort, string remoteHost, uint remotePort) {
        SshClient client = GetSshClient();
        var port = new ForwardedPortLocal(localAddress, localPort, remoteHost, remotePort);
        client.AddForwardedPort(port);
        try {
            port.Start();
        }
        catch {
            client.RemoveForwardedPort(port);
            port.Dispose();
            throw;
        }

        var tunnel = new SshTunnel(this, port);
        lock (_tunnels) {
            _tunnels.Add(tunnel);
        }

        return tunnel;
    }

    /// <summary>Stops a tunnel and forgets it.</summary>
    internal void CloseTunnel(SshTunnel tunnel) {
        lock (_tunnels) {
            if (!_tunnels.Remove(tunnel)) {
                return;
            }
        }

        tunnel.Stop(_ssh);
    }

    internal void Connect(CancellationToken cancellationToken) {
        Client.ConnectAsync(cancellationToken).GetAwaiter().GetResult();
        ConnectedAt = DateTime.Now;
        HomeDirectory = Client.WorkingDirectory;
    }

    /// <summary>Returns an SCP client on a second connection with the same credentials and host key check.</summary>
    internal ScpClient GetScpClient() {
        if (_scp is null) {
            // Shell-quote remote paths: SCP passes them to the server's shell, so unquoted names allow command injection.
            _scp = new ScpClient(_connection, RemotePathTransformation.ShellQuote);
            _scp.HostKeyReceived += OnHostKeyReceived;
        }

        if (!_scp.IsConnected) {
            _scp.Connect();
        }

        return _scp;
    }

    /// <summary>Returns an SSH client for running commands, on its own connection with the same credentials and host key check.</summary>
    internal SshClient GetSshClient() {
        if (_ssh is null) {
            _ssh = new SshClient(_connection);
            _ssh.HostKeyReceived += OnHostKeyReceived;
        }

        if (!_ssh.IsConnected) {
            _ssh.Connect();
        }

        return _ssh;
    }

    private void OnHostKeyReceived(object? sender, HostKeyEventArgs e) {
        SshHostKey key = HostKeyFingerprint.Describe(_connection.Host, _connection.Port, e);
        e.CanTrust = _verifier.Verify(key, e.HostKey);
        if (e.CanTrust) {
            HostKey = key;
        }
        else {
            RejectedHostKey = key;
        }
    }

    /// <inheritdoc/>
    public void Dispose() {
        _disposed = true;
        _registry?.Remove(this);
        foreach (SshTunnel tunnel in Tunnels) {
            CloseTunnel(tunnel);
        }

        _scp?.Dispose();
        _ssh?.Dispose();
        Client.Dispose();
    }

    /// <inheritdoc/>
    public override string ToString() => Name.Length > 0 ? Name : $"{UserName}@{HostName}:{Port}";
}
