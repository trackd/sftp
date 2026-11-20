namespace sftp;
/// <summary>Which way Sync-SftpFolder copies.</summary>
public enum SyncDirection {
    /// <summary>Make the server folder match the local folder.</summary>
    Upload,

    /// <summary>Make the local folder match the server folder.</summary>
    Download,
}
