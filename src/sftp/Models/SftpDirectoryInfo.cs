using System.Management.Automation;
using Renci.SshNet.Sftp;

namespace sftp;
/// <summary>A folder on an SFTP server, like DirectoryInfo for local folders.</summary>
public sealed class SftpDirectoryInfo : SftpItem {
    internal SftpDirectoryInfo(string fullName, SftpFileAttributes attributes, string sessionName) : base(fullName, attributes, sessionName) {
    }

    /// <inheritdoc/>
    public override bool IsDirectory => true;

    /// <summary>The folder name, like BaseName on local folders.</summary>
    public string BaseName => Name;

    /// <summary>The path of the parent folder; empty for the root folder.</summary>
    public string? Parent => FullName == "/" ? null : sftp.RemotePath.GetDirectory(FullName);

    /// <summary>The root folder of the server: /.</summary>
    public string Root { get; } = "/";

    /// <summary>The files in the folder whose names match the pattern (wildcards allowed), read from the server.</summary>
    public SftpFileInfo[] GetFiles(string searchPattern = "*") => [.. List(searchPattern).OfType<SftpFileInfo>()];

    /// <summary>The folders in the folder whose names match the pattern (wildcards allowed), read from the server.</summary>
    public SftpDirectoryInfo[] GetDirectories(string searchPattern = "*") => [.. List(searchPattern).OfType<SftpDirectoryInfo>()];

    /// <summary>The files and folders in the folder whose names match the pattern (wildcards allowed), read from the server.</summary>
    public SftpItem[] GetFileSystemInfos(string searchPattern = "*") => [.. List(searchPattern)];

    /// <summary>The parent folder, read from the server; null for the root folder.</summary>
    public SftpDirectoryInfo? GetParent() {
        if (Parent is not { } parent) {
            return null;
        }

        return new SftpDirectoryInfo(parent, OpenSession().Client.GetAttributes(parent), SessionName);
    }

    private IEnumerable<SftpItem> List(string searchPattern) {
        var pattern = new WildcardPattern(searchPattern, WildcardOptions.IgnoreCase);
        foreach (ISftpFile item in OpenSession().Client.ListDirectory(FullName)) {
            if (item.Name is not "." and not ".." && pattern.IsMatch(item.Name)) {
                yield return Create(item.FullName, item.Attributes, SessionName);
            }
        }
    }
}
