namespace sftp;
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
