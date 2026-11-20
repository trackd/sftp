namespace sftp;
/// <summary>The size and free space of the file system that holds a path on the server.</summary>
/// <param name="Path">The remote path that was queried.</param>
/// <param name="TotalBytes">The size of the file system.</param>
/// <param name="FreeBytes">Free space, including space reserved for the administrator.</param>
/// <param name="AvailableBytes">Free space you can use.</param>
/// <param name="IsReadOnly">True when the file system is mounted read-only.</param>
public sealed record SftpDiskSpace(string Path, ulong TotalBytes, ulong FreeBytes, ulong AvailableBytes, bool IsReadOnly) {
    /// <summary>Space in use.</summary>
    public ulong UsedBytes => TotalBytes - FreeBytes;

    /// <summary>The share of the file system in use, from 0 to 100.</summary>
    public double PercentUsed => TotalBytes == 0 ? 0 : Math.Round(100.0 * UsedBytes / TotalBytes, 1);

    /// <summary>The name of the session that was queried.</summary>
    public string SessionName { get; init; } = "";
}
