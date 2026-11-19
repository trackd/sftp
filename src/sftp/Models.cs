namespace sftp;
/// <summary>An SSH server host key and its fingerprints.</summary>
/// <param name="HostName">The server that presented the key.</param>
/// <param name="Port">The server port.</param>
/// <param name="Algorithm">The host key algorithm, for example ssh-ed25519.</param>
/// <param name="KeyLength">The key length in bits.</param>
/// <param name="Sha256">The unpadded Base64 SHA-256 fingerprint.</param>
/// <param name="Md5">The colon-separated hexadecimal MD5 fingerprint (legacy).</param>
/// <param name="KeyType">The key type, for example ssh-rsa; empty when unknown.</param>
public sealed record SshHostKey(string HostName, int Port, string Algorithm, int KeyLength, string Sha256, string Md5, string KeyType = "") {
    /// <summary>The fingerprint in "algorithm bits sha256" form, accepted by -HostKeyFingerprint.</summary>
    public string Fingerprint => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{(KeyType.Length > 0 ? KeyType : Algorithm)} {KeyLength} {Sha256}");

    /// <summary>The fingerprint in OpenSSH "SHA256:..." form.</summary>
    public string OpenSshFingerprint => "SHA256:" + Sha256;
}

/// <summary>A file or directory on an SFTP server.</summary>
/// <param name="Name">The item name.</param>
/// <param name="FullName">The absolute remote path.</param>
/// <param name="Length">The size in bytes.</param>
/// <param name="LastWriteTimeUtc">The last modification time in UTC.</param>
/// <param name="IsDirectory">True for directories.</param>
/// <param name="IsSymbolicLink">True for symbolic links.</param>
public sealed record SftpItem(string Name, string FullName, long Length, DateTime LastWriteTimeUtc, bool IsDirectory, bool IsSymbolicLink) {
    /// <summary>The last modification time in local time.</summary>
    public DateTime LastWriteTime => LastWriteTimeUtc.ToLocalTime();

    /// <summary>The parent directory path.</summary>
    public string DirectoryName => sftp.RemotePath.GetDirectory(FullName);

    /// <summary>The path on the server; lets the item be piped to commands that take -RemotePath.</summary>
    public string RemotePath => FullName;

    /// <summary>The name of the session the item came from, so piped commands use the same server.</summary>
    public string SessionName { get; init; } = "";
}

/// <summary>The outcome of transferring one file.</summary>
/// <param name="Operation">Upload or Download.</param>
/// <param name="Protocol">Sftp or Scp.</param>
/// <param name="Source">The source path.</param>
/// <param name="Destination">The destination path.</param>
/// <param name="Length">The number of bytes transferred.</param>
/// <param name="Success">True when the file was transferred completely.</param>
/// <param name="Error">The failure message, if any.</param>
/// <param name="Duration">How long the transfer took.</param>
public sealed record SftpTransferResult(
    string Operation, string Protocol, string Source, string Destination, long Length, bool Success, string? Error, TimeSpan Duration) {
    /// <summary>The file's path on the server: the source of a download, the destination of an upload.</summary>
    public string RemotePath => Operation == "Download" ? Source : Destination;

    /// <summary>The file's path on this computer: the destination of a download, the source of an upload.</summary>
    public string LocalPath => Operation == "Download" ? Destination : Source;

    /// <summary>The name of the session that made the transfer, so piped commands use the same server.</summary>
    public string SessionName { get; init; } = "";
}
