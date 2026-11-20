using Renci.SshNet.Sftp;

namespace sftp;
/// <summary>A file on an SFTP server, like FileInfo for local files.</summary>
public sealed class SftpFileInfo : SftpItem {
    internal SftpFileInfo(string fullName, SftpFileAttributes attributes, string sessionName) : base(fullName, attributes, sessionName) =>
        Length = attributes.Size;

    /// <inheritdoc/>
    public override bool IsDirectory => false;

    /// <summary>The size in bytes.</summary>
    public long Length { get; }

    /// <summary>The name without its extension, like BaseName on local files.</summary>
    public string BaseName => Path.GetFileNameWithoutExtension(Name);

    /// <summary>The path of the folder that contains the file.</summary>
    public string DirectoryName => sftp.RemotePath.GetDirectory(FullName);

    /// <summary>True when the owner cannot write to the file.</summary>
    public bool IsReadOnly => Attributes.HasFlag(FileAttributes.ReadOnly);

    /// <summary>The folder that contains the file, read from the server.</summary>
    public SftpDirectoryInfo GetDirectory() {
        SftpSession session = OpenSession();
        string directory = DirectoryName;

        return new SftpDirectoryInfo(directory, session.Client.GetAttributes(directory), SessionName);
    }
}
