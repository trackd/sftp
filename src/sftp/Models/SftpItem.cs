using System.Management.Automation;
using Renci.SshNet.Sftp;

namespace sftp;
/// <summary>
/// A file or folder on an SFTP server: the shared base of <see cref="SftpFileInfo"/> and
/// <see cref="SftpDirectoryInfo"/>, like FileSystemInfo for local files. Properties follow FileSystemInfo where
/// SFTP has the information, so scripts written for local files mostly work unchanged.
/// </summary>
public abstract class SftpItem {
    private protected SftpItem(string fullName, SftpFileAttributes attributes, string sessionName) {
        FullName = fullName;
        Name = sftp.RemotePath.GetName(fullName);
        LastWriteTimeUtc = attributes.LastWriteTimeUtc;
        LastAccessTimeUtc = attributes.LastAccessTimeUtc;
        IsSymbolicLink = attributes.IsSymbolicLink;
        Permissions = PermissionMode.From(attributes);
        UserId = attributes.UserId;
        GroupId = attributes.GroupId;
        SessionName = sessionName;
    }

    /// <summary>Creates a <see cref="SftpFileInfo"/> or a <see cref="SftpDirectoryInfo"/>.</summary>
    internal static SftpItem Create(string fullName, SftpFileAttributes attributes, string sessionName) =>
        attributes.IsDirectory ? new SftpDirectoryInfo(fullName, attributes, sessionName) : new SftpFileInfo(fullName, attributes, sessionName);

    /// <summary>The item name, for example report.csv.</summary>
    public string Name { get; }

    /// <summary>The absolute path on the server.</summary>
    public string FullName { get; }

    /// <summary>The extension including the dot, for example .csv; empty when the name has none.</summary>
    public string Extension => Path.GetExtension(Name);

    /// <summary>Always true: the item was found on the server.</summary>
    public bool Exists { get; } = true;

    /// <summary>True for folders.</summary>
    public abstract bool IsDirectory { get; }

    /// <summary>True for symbolic links.</summary>
    public bool IsSymbolicLink { get; }

    /// <summary>SymbolicLink for symbolic links, otherwise empty, like LinkType on local files.</summary>
    public string? LinkType => IsSymbolicLink ? "SymbolicLink" : null;

    /// <summary>The last modification time in UTC.</summary>
    public DateTime LastWriteTimeUtc { get; }

    /// <summary>The last modification time in local time.</summary>
    public DateTime LastWriteTime => LastWriteTimeUtc.ToLocalTime();

    /// <summary>The last access time in UTC.</summary>
    public DateTime LastAccessTimeUtc { get; }

    /// <summary>The last access time in local time.</summary>
    public DateTime LastAccessTime => LastAccessTimeUtc.ToLocalTime();

    /// <summary>
    /// SFTP servers do not report when a file was created, so this is the modification time, as .NET does on
    /// file systems that do not record creation times.
    /// </summary>
    public DateTime CreationTimeUtc => LastWriteTimeUtc;

    /// <summary>The creation time in local time; see CreationTimeUtc.</summary>
    public DateTime CreationTime => CreationTimeUtc.ToLocalTime();

    /// <summary>Directory, ReadOnly (the owner cannot write), Hidden (the name starts with a dot), ReparsePoint (a symbolic link), or Normal.</summary>
    public FileAttributes Attributes {
        get {
            FileAttributes attributes = 0;
            attributes |= IsDirectory ? FileAttributes.Directory : 0;
            attributes |= (Permissions & 0x80) == 0 ? FileAttributes.ReadOnly : 0;
            attributes |= Name.StartsWith('.') ? FileAttributes.Hidden : 0;
            attributes |= IsSymbolicLink ? FileAttributes.ReparsePoint : 0;

            return attributes == 0 ? FileAttributes.Normal : attributes;
        }
    }

    /// <summary>The attributes in PowerShell's Mode form (darhsl), as Get-ChildItem shows for local files.</summary>
    public string Mode => string.Create(6, Attributes, static (chars, attributes) => {
        chars[0] = attributes.HasFlag(FileAttributes.Directory) ? 'd' : '-';
        chars[1] = '-';
        chars[2] = attributes.HasFlag(FileAttributes.ReadOnly) ? 'r' : '-';
        chars[3] = attributes.HasFlag(FileAttributes.Hidden) ? 'h' : '-';
        chars[4] = '-';
        chars[5] = attributes.HasFlag(FileAttributes.ReparsePoint) ? 'l' : '-';
    });

    /// <summary>The permissions in ls -l form, for example -rw-r--r--, as UnixMode shows for local files on Linux and macOS.</summary>
    public string UnixMode => (IsSymbolicLink ? "l" : "") + PermissionMode.ToSymbolic(Permissions, IsDirectory)[(IsSymbolicLink ? 1 : 0)..];

    /// <summary>The permissions as a UnixFileMode, like UnixFileMode on local files.</summary>
    public UnixFileMode UnixFileMode => (UnixFileMode)Permissions;

    /// <summary>The permission bits as a number; see also Get-SftpPermission.</summary>
    public int Permissions { get; }

    /// <summary>The numeric owner.</summary>
    public int UserId { get; }

    /// <summary>The numeric group.</summary>
    public int GroupId { get; }

    /// <summary>The path on the server; lets the item be piped to commands that take -RemotePath.</summary>
    public string RemotePath => FullName;

    /// <summary>The name of the session the item came from, so piped commands use the same server.</summary>
    public string SessionName { get; }

    /// <summary>The open session the item came from.</summary>
    private protected SftpSession OpenSession() {
        SftpSession? session = SessionRegistry.Current.Find(SessionName);

        return session is { IsConnected: true } ? session :
            throw new PSInvalidOperationException($"The session '{SessionName}' is no longer open. Connect again with Connect-SftpServer.");
    }

    /// <inheritdoc/>
    public override string ToString() => FullName;
}
