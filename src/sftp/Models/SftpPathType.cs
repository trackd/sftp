namespace sftp;
/// <summary>What Test-SftpPath counts: anything, folders only or files only.</summary>
public enum SftpPathType {
    /// <summary>Files and folders.</summary>
    Any,

    /// <summary>Folders only.</summary>
    Container,

    /// <summary>Files only.</summary>
    Leaf,
}
