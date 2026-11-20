namespace sftp;
/// <summary>Reads disk space with the statvfs@openssh.com extension.</summary>
internal static class DiskSpaceQuery {
    /// <summary>The space of the file system that holds the path; throws NotSupportedException when the server lacks the extension.</summary>
    public static SftpDiskSpace Get(SftpSession session, string fullPath) {
        Renci.SshNet.Sftp.SftpFileSystemInformation info = session.Client.GetStatus(fullPath);
        ulong blockSize = info.BlockSize > 0 ? info.BlockSize : info.FileSystemBlockSize;

        return new SftpDiskSpace(fullPath, info.TotalBlocks * blockSize, info.FreeBlocks * blockSize, info.AvailableBlocks * blockSize, info.IsReadOnly) {
            SessionName = session.Name,
        };
    }
}
