using System.Management.Automation;
using Renci.SshNet.Common;

namespace sftp.Commands;
/// <summary>Shows the size and free space of the file system that holds a folder on the server.</summary>
[Cmdlet(VerbsCommon.Get, "SftpDiskSpace")]
[OutputType(typeof(SftpDiskSpace))]
public sealed class GetSftpDiskSpaceCommand : SftpCommand {
    /// <inheritdoc/>
    protected override IEnumerable<string> DrivePaths => Path;

    /// <summary>Remote paths to check. The default is the home folder.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("FullName", "RemotePath")]
    [ArgumentCompleter(typeof(RemotePathCompleter))]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = ["~"];

    /// <inheritdoc/>
    protected override void ProcessItems() {
        foreach (string path in Path) {
            string full = RemoteFullPath(path);
            try {
                WriteObject(DiskSpaceQuery.Get(Current, full));

            }
            catch (Exception error) when (error is NotSupportedException || (error is SshException && error is not SftpPathNotFoundException and not SshConnectionException &&
                error.Message.Contains("support", StringComparison.OrdinalIgnoreCase))) {
                WriteError(new ErrorRecord(
                    new NotSupportedException($"{Current.HostName} does not report disk space: the server does not support the statvfs@openssh.com extension.", error),
                    "DiskSpaceNotSupported",
                    ErrorCategory.NotImplemented,
                    full));
            }
            catch (Exception error) when (error is SshException or IOException) {
                WriteItemError(error, "GetDiskSpaceFailed", full);
            }
        }
    }
}
