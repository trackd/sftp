namespace sftp;
/// <summary>The permissions of a file or folder on the server.</summary>
/// <param name="FullName">The absolute remote path.</param>
/// <param name="Mode">The permissions in ls -l form, for example -rw-r--r--.</param>
/// <param name="Octal">The permissions in octal, for example 0644.</param>
/// <param name="Permissions">The permission bits as a number.</param>
/// <param name="UserId">The numeric owner.</param>
/// <param name="GroupId">The numeric group.</param>
/// <param name="IsDirectory">True for folders.</param>
public sealed record SftpPermissionInfo(string FullName, string Mode, string Octal, int Permissions, int UserId, int GroupId, bool IsDirectory) {
    /// <summary>The item name.</summary>
    public string Name => sftp.RemotePath.GetName(FullName);

    /// <summary>The path on the server; lets the item be piped to commands that take -RemotePath.</summary>
    public string RemotePath => FullName;

    /// <summary>The name of the session the item came from, so piped commands use the same server.</summary>
    public string SessionName { get; init; } = "";
}
