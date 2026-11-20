using System.Management.Automation;
using Renci.SshNet.Common;

namespace sftp;
/// <summary>An Sftp drive: a folder on the server, reached through a session.</summary>
public sealed class SftpDriveInfo : PSDriveInfo {
    internal SftpDriveInfo(PSDriveInfo drive, string root, string displayRoot, SftpSession session, string remoteRoot, bool ownsSession)
        : base(drive.Name, drive.Provider, root, drive.Description, drive.Credential, displayRoot) {
        Session = session;
        RemoteRoot = remoteRoot;
        OwnsSession = ownsSession;
    }

    /// <summary>The session the drive uses.</summary>
    public SftpSession Session { get; }

    /// <summary>The folder on the server that the drive's root shows.</summary>
    public string RemoteRoot { get; }

    /// <summary>True when the drive opened the session itself; it is closed when the drive is removed.</summary>
    public bool OwnsSession { get; }

    internal AttributeCache Cache { get; } = new();

    private (SftpDiskSpace? Space, long Expires) _space;

    /// <summary>
    /// The disk space of the drive's root folder, or null when the server cannot report it (it needs the
    /// statvfs@openssh.com extension). Get-PSDrive shows it as Used and Free, like file system drives.
    /// </summary>
    public SftpDiskSpace? GetDiskSpace() {
        if (_space.Expires > Environment.TickCount64) {
            return _space.Space;
        }

        SftpDiskSpace? space = null;
        try {
            if (Session.IsConnected) {
                space = DiskSpaceQuery.Get(Session, RemoteRoot);
            }
        }
        catch (Exception error) when (error is SshException or NotSupportedException or IOException) {
            space = null;
        }

        _space = (space, Environment.TickCount64 + 5000);

        return space;
    }

    /// <summary>
    /// Turns a provider path into an absolute remote path. Provider paths are the drive's root (its name)
    /// followed by the path below the remote root, with \ or / separators.
    /// </summary>
    internal string ToRemote(string providerPath) {
        string path = providerPath.Replace('\\', '/').Trim('/');
        if (path.Equals(Root, StringComparison.OrdinalIgnoreCase)) {
            return RemoteRoot;
        }

        if (path.StartsWith(Root + "/", StringComparison.OrdinalIgnoreCase)) {
            path = path[(Root.Length + 1)..];
        }

        return RemotePath.Normalize(RemotePath.Combine(RemoteRoot, path));
    }

    /// <summary>Turns an absolute remote path into a provider path, using the platform's path separator.</summary>
    internal string ToProviderPath(string remotePath) {
        string prefix = RemoteRoot == "/" ? "/" : RemoteRoot + "/";
        if (remotePath == RemoteRoot) {
            return Root;
        }

        string relative = remotePath.StartsWith(prefix, StringComparison.Ordinal) ? remotePath[prefix.Length..] : remotePath.TrimStart('/');

        return Root + Path.DirectorySeparatorChar + relative.Replace('/', Path.DirectorySeparatorChar);
    }
}
