using System.Buffers.Binary;
using System.Globalization;

namespace sftp.TestServer;

/// <summary>SFTP protocol version 3 (draft-ietf-secsh-filexfer-02) over a channel, rooted in one directory.</summary>
internal sealed class SftpSubsystem(Channel channel, ServerOptions options) {
    private const byte Init = 1, Version = 2, Open = 3, Close = 4, Read = 5, Write = 6, LStat = 7, FStat = 8, SetStat = 9, FSetStat = 10,
        OpenDir = 11, ReadDir = 12, Remove = 13, MkDir = 14, RmDir = 15, RealPath = 16, Stat = 17, Rename = 18, ReadLink = 19,
        Extended = 200, ExtendedReply = 201, Status = 101, Handle = 102, Data = 103, Name = 104, Attrs = 105;
    private const uint Ok = 0, Eof = 1, NoSuchFile = 2, PermissionDenied = 3, Failure = 4, OpUnsupported = 8;
    private const uint FlagRead = 1, FlagWrite = 2, FlagAppend = 4, FlagCreate = 8, FlagTruncate = 0x10, FlagExclusive = 0x20;
    private const uint AttrSize = 1, AttrUidGid = 2, AttrPermissions = 4, AttrTimes = 8, AttrExtended = 0x80000000;

    private readonly Dictionary<string, object> _handles = [];
    private int _nextHandle;

    public void Run() {
        byte[] lengthBytes = new byte[4];
        while (true) {
            int read = channel.Input.Read(lengthBytes.AsSpan(0, 1));
            if (read == 0) {
                break;
            }

            channel.Input.ReadExactly(lengthBytes.AsSpan(1));
            byte[] packet = new byte[BinaryPrimitives.ReadUInt32BigEndian(lengthBytes)];
            channel.Input.ReadExactly(packet);
            Dispatch(packet);
        }

        foreach (object handle in _handles.Values) {
            (handle as FileStream)?.Dispose();
        }

        channel.Exit(0);
    }

    private void Dispatch(byte[] packet) {
        var reader = new SshReader(packet, 1);
        if (packet[0] == Init) {
            var reply = new SshWriter().Byte(Version).UInt32(3);
            if (options.PosixRename) {
                reply.String("posix-rename@openssh.com").String("1");
            }

            if (options.StatVfs) {
                reply.String("statvfs@openssh.com").String("2");
            }

            Send(reply);

            return;
        }

        uint id = reader.ReadUInt32();
        try {
            switch (packet[0]) {
                case Open: OpenFile(id, reader); break;
                case Close: CloseHandle(id, reader.ReadString()); break;
                case Read: ReadFile(id, reader); break;
                case Write: WriteFile(id, reader); break;
                case LStat or Stat: SendAttributes(id, Map(reader.ReadString())); break;
                case FStat: SendAttributes(id, HandleStream(reader.ReadString()).Name); break;
                case SetStat: ApplyAttributes(id, Map(reader.ReadString()), reader); break;
                case FSetStat: ApplyAttributes(id, HandleStream(reader.ReadString()).Name, reader); break;
                case OpenDir: OpenDirectory(id, reader.ReadString()); break;
                case ReadDir: ReadDirectory(id, reader.ReadString()); break;
                case Remove: RemoveFile(id, Map(reader.ReadString())); break;
                case MkDir: MakeDirectory(id, Map(reader.ReadString())); break;
                case RmDir: RemoveDirectory(id, Map(reader.ReadString())); break;
                case RealPath: Canonicalize(id, reader.ReadString()); break;
                case Rename: RenameItem(id, reader.ReadString(), reader.ReadString(), overwrite: false); break;
                case ReadLink: SendStatus(id, OpUnsupported, "Symbolic links are not supported."); break;
                case Extended: ExtendedRequest(id, reader); break;
                default: SendStatus(id, OpUnsupported, "Unsupported request."); break;
            }
        }
        catch (FileNotFoundException error) {
            SendStatus(id, NoSuchFile, error.Message);
        }
        catch (DirectoryNotFoundException error) {
            SendStatus(id, NoSuchFile, error.Message);
        }
        catch (UnauthorizedAccessException error) {
            SendStatus(id, PermissionDenied, error.Message);
        }
        catch (IOException error) {
            SendStatus(id, Failure, error.Message);
        }
    }

    // ---------------------------------------------------------------- paths

    /// <summary>Normalizes a client path to an absolute virtual path; ".." never climbs above the root.</summary>
    private static string Normalize(string path) {
        var parts = new List<string>();
        foreach (string part in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)) {
            if (part == ".") {
                continue;
            }

            if (part == "..") {
                if (parts.Count > 0) {
                    parts.RemoveAt(parts.Count - 1);
                }

                continue;
            }

            parts.Add(part);
        }

        return "/" + string.Join('/', parts);
    }

    private string Map(string path) {
        string normalized = Normalize(path);

        return normalized == "/" ? options.Root : Path.Join(options.Root, normalized[1..].Replace('/', Path.DirectorySeparatorChar));
    }

    private static bool Exists(string local) => File.Exists(local) || Directory.Exists(local);

    // ---------------------------------------------------------------- requests

    private void OpenFile(uint id, SshReader reader) {
        string local = Map(reader.ReadString());
        uint flags = reader.ReadUInt32();
        if (Directory.Exists(local)) {
            throw new IOException("Is a directory.");
        }

        FileMode mode;
        if ((flags & FlagCreate) != 0) {
            mode = (flags & FlagExclusive) != 0 ? FileMode.CreateNew : (flags & FlagTruncate) != 0 ? FileMode.Create : FileMode.OpenOrCreate;
        }
        else {
            mode = (flags & FlagTruncate) != 0 ? FileMode.Truncate : FileMode.Open;
        }

        FileAccess access = ((flags & FlagRead) != 0, (flags & (FlagWrite | FlagAppend)) != 0) switch {
            (true, true) => FileAccess.ReadWrite,
            (false, true) => FileAccess.Write,
            _ => FileAccess.Read,
        };
        if (mode == FileMode.Open && !File.Exists(local)) {
            throw new FileNotFoundException("No such file.");
        }

        var stream = new FileStream(local, mode, access, FileShare.ReadWrite | FileShare.Delete);
        if ((flags & FlagAppend) != 0) {
            stream.Seek(0, SeekOrigin.End);
        }

        SendHandle(id, stream);
    }

    private void ReadFile(uint id, SshReader reader) {
        FileStream stream = HandleStream(reader.ReadString());
        ulong offset = reader.ReadUInt64();
        int length = (int)Math.Min(reader.ReadUInt32(), 64 * 1024);
        stream.Position = (long)offset;
        byte[] buffer = new byte[length];
        int read = stream.Read(buffer);
        if (read == 0) {
            SendStatus(id, Eof, "End of file.");

            return;
        }

        Send(new SshWriter().Byte(Data).UInt32(id).Binary(buffer.AsSpan(0, read)));
    }

    private void WriteFile(uint id, SshReader reader) {
        FileStream stream = HandleStream(reader.ReadString());
        ulong offset = reader.ReadUInt64();
        byte[] data = reader.ReadBinary();
        stream.Position = (long)offset;
        stream.Write(data);
        SendStatus(id, Ok, "");
    }

    private void CloseHandle(uint id, string handle) {
        if (!_handles.Remove(handle, out object? value)) {
            SendStatus(id, Failure, "Invalid handle.");

            return;
        }

        (value as FileStream)?.Dispose();
        SendStatus(id, Ok, "");
    }

    private void OpenDirectory(uint id, string path) {
        string local = Map(path);
        if (!Directory.Exists(local)) {
            throw new DirectoryNotFoundException("No such directory.");
        }

        var entries = new Queue<string>(["." , "..", .. Directory.EnumerateFileSystemEntries(local).Select(Path.GetFileName).OfType<string>()]);
        string handle = (_nextHandle++).ToString(CultureInfo.InvariantCulture);
        _handles[handle] = new DirectoryListing(local, entries);
        Send(new SshWriter().Byte(Handle).UInt32(id).String(handle));
    }

    private void ReadDirectory(uint id, string handle) {
        if (!_handles.TryGetValue(handle, out object? value) || value is not DirectoryListing listing) {
            SendStatus(id, Failure, "Invalid handle.");

            return;
        }

        if (listing.Entries.Count == 0) {
            SendStatus(id, Eof, "End of directory.");

            return;
        }

        var batch = new List<string>();
        while (listing.Entries.Count > 0 && batch.Count < 100) {
            batch.Add(listing.Entries.Dequeue());
        }

        var writer = new SshWriter().Byte(Name).UInt32(id).UInt32((uint)batch.Count);
        foreach (string name in batch) {
            string local = name switch {
                "." => listing.Path,
                ".." => Path.GetDirectoryName(listing.Path) ?? listing.Path,
                _ => Path.Join(listing.Path, name),
            };
            writer.String(name).String(name);
            WriteAttributes(writer, local);
        }

        Send(writer);
    }

    private void RemoveFile(uint id, string local) {
        if (!File.Exists(local)) {
            throw new FileNotFoundException("No such file.");
        }

        File.Delete(local);
        SendStatus(id, Ok, "");
    }

    private void MakeDirectory(uint id, string local) {
        if (Exists(local)) {
            throw new IOException("Already exists.");
        }

        if (!Directory.Exists(Path.GetDirectoryName(local))) {
            throw new DirectoryNotFoundException("No such directory.");
        }

        Directory.CreateDirectory(local);
        SendStatus(id, Ok, "");
    }

    private void RemoveDirectory(uint id, string local) {
        if (!Directory.Exists(local)) {
            throw new DirectoryNotFoundException("No such directory.");
        }

        if (Directory.EnumerateFileSystemEntries(local).Any()) {
            throw new IOException("Directory not empty.");
        }

        Directory.Delete(local);
        SendStatus(id, Ok, "");
    }

    private void Canonicalize(uint id, string path) {
        string normalized = Normalize(path);
        if (!Exists(Map(normalized))) {
            throw new FileNotFoundException("No such file.");
        }

        var writer = new SshWriter().Byte(Name).UInt32(id).UInt32(1).String(normalized).String(normalized);
        WriteAttributes(writer, null);
        Send(writer);
    }

    private void RenameItem(uint id, string from, string to, bool overwrite) {
        string source = Map(from);
        string target = Map(to);
        if (!Exists(source)) {
            throw new FileNotFoundException("No such file.");
        }

        // Version 3 RENAME fails when the target exists; posix-rename replaces it.
        if (Exists(target) && !overwrite) {
            throw new IOException("Target exists.");
        }

        if (Directory.Exists(source)) {
            Directory.Move(source, target);
        }
        else {
            File.Move(source, target, overwrite);
        }

        SendStatus(id, Ok, "");
    }

    private void ExtendedRequest(uint id, SshReader reader) {
        string name = reader.ReadString();
        if (name == "posix-rename@openssh.com" && options.PosixRename) {
            RenameItem(id, reader.ReadString(), reader.ReadString(), overwrite: true);

            return;
        }

        if (name == "statvfs@openssh.com" && options.StatVfs) {
            string local = Map(reader.ReadString());
            if (!Exists(local)) {
                throw new FileNotFoundException("No such file.");
            }

            // Fixed numbers so tests can check the arithmetic: 4096-byte blocks, 1000 total, 250 free, 200 available.
            Send(new SshWriter().Byte(ExtendedReply).UInt32(id)
                .UInt64(4096).UInt64(4096).UInt64(1000).UInt64(250).UInt64(200)
                .UInt64(100).UInt64(50).UInt64(40).UInt64(1).UInt64(0).UInt64(255));

            return;
        }

        SendStatus(id, OpUnsupported, "Unsupported extension.");
    }

    // ---------------------------------------------------------------- attributes

    private void SendAttributes(uint id, string local) {
        if (!Exists(local)) {
            throw new FileNotFoundException("No such file.");
        }

        var writer = new SshWriter().Byte(Attrs).UInt32(id);
        WriteAttributes(writer, local);
        Send(writer);
    }

    private void WriteAttributes(SshWriter writer, string? local) {
        if (local is null) {
            writer.UInt32(0);

            return;
        }

        bool directory = Directory.Exists(local);
        long size = directory ? 0 : new FileInfo(local).Length;
        DateTime modified = directory ? Directory.GetLastWriteTimeUtc(local) : File.GetLastWriteTimeUtc(local);
        uint time = (uint)new DateTimeOffset(modified, TimeSpan.Zero).ToUnixTimeSeconds();
        // The host file system may not keep Unix permissions, so the server remembers what clients set.
        uint mode = options.Permissions.TryGetValue(local, out uint stored) ? stored : directory ? 0x1EDu : 0x1A4u;
        uint permissions = (directory ? 0x4000u : 0x8000u) | mode;
        writer.UInt32(AttrSize | AttrUidGid | AttrPermissions | AttrTimes)
            .UInt64((ulong)size)
            .UInt32(1000)
            .UInt32(1000)
            .UInt32(permissions)
            .UInt32(time)
            .UInt32(time);
    }

    private void ApplyAttributes(uint id, string local, SshReader reader) {
        if (!Exists(local)) {
            throw new FileNotFoundException("No such file.");
        }

        uint flags = reader.ReadUInt32();
        if ((flags & AttrSize) != 0) {
            ulong size = reader.ReadUInt64();
            if (File.Exists(local)) {
                using var stream = new FileStream(local, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                stream.SetLength((long)size);
            }
        }

        if ((flags & AttrUidGid) != 0) {
            reader.ReadUInt32();
            reader.ReadUInt32();
        }

        if ((flags & AttrPermissions) != 0) {
            options.Permissions[local] = reader.ReadUInt32() & 0xFFF;
        }

        if ((flags & AttrTimes) != 0) {
            reader.ReadUInt32();
            DateTime modified = DateTimeOffset.FromUnixTimeSeconds(reader.ReadUInt32()).UtcDateTime;
            if (Directory.Exists(local)) {
                Directory.SetLastWriteTimeUtc(local, modified);
            }
            else {
                File.SetLastWriteTimeUtc(local, modified);
            }
        }

        _ = flags & AttrExtended;
        SendStatus(id, Ok, "");
    }

    // ---------------------------------------------------------------- replies

    private FileStream HandleStream(string handle) =>
        _handles.TryGetValue(handle, out object? value) && value is FileStream stream ? stream : throw new IOException("Invalid handle.");

    private void SendHandle(uint id, object value) {
        string handle = (_nextHandle++).ToString(CultureInfo.InvariantCulture);
        _handles[handle] = value;
        Send(new SshWriter().Byte(Handle).UInt32(id).String(handle));
    }

    private void SendStatus(uint id, uint code, string message) =>
        Send(new SshWriter().Byte(Status).UInt32(id).UInt32(code).String(message).String(""));

    private void Send(SshWriter writer) {
        byte[] body = writer.ToArray();
        byte[] packet = new byte[4 + body.Length];
        BinaryPrimitives.WriteUInt32BigEndian(packet, (uint)body.Length);
        body.CopyTo(packet, 4);
        channel.Write(packet);
    }

    private sealed record DirectoryListing(string Path, Queue<string> Entries);
}
