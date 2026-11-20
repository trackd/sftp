using System.Diagnostics;
using System.Management.Automation;
using Renci.SshNet.Common;

namespace sftp.Commands;
/// <summary>Downloads remote files with SCP, for servers that do not offer SFTP.</summary>
[Cmdlet(VerbsCommunications.Receive, "ScpFile", SupportsShouldProcess = true)]
[OutputType(typeof(SftpTransferResult))]
public sealed class ReceiveScpFileCommand : SftpCommand {
    /// <inheritdoc/>
    protected override IEnumerable<string> DrivePaths => Path;

    /// <summary>Remote files to download, used exactly as typed (SCP cannot expand wildcards). Accepts Get-SftpFile output.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("FullName", "RemotePath", "LiteralPath")]
    [ArgumentCompleter(typeof(RemotePathCompleter))]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = [];

    /// <summary>The name of the session to use; filled in from the SessionName of piped files and results.</summary>
    [Parameter(ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string? SessionName { get; set; }

    /// <inheritdoc/>
    protected override string? PipedSessionName => SessionName;

    /// <summary>
    /// Where to download: an existing local folder (the files keep their names), or a new file name for a
    /// single file. Defaults to the current folder.
    /// </summary>
    [Parameter(Position = 1)]
    [ValidateNotNullOrEmpty]
    public string? Destination { get; set; }

    /// <summary>Overwrite existing local files.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <inheritdoc/>
    protected override void ProcessItems() {
        foreach (string path in Path) {
            var timer = Stopwatch.StartNew();
            string source = path;
            string destination = Destination ?? RemotePath.GetName(path);
            string partial = "";
            try {
                source = RemoteFullPath(path);
                destination = LocalTarget(Destination, RemotePath.GetName(source));
                partial = destination + SendSftpFileCommand.TemporarySuffix;
                if (!ShouldProcess($"{source} -> {destination}", "Download (SCP)")) {
                    continue;
                }

                if (!Force && File.Exists(destination)) {
                    throw new IOException($"{destination} already exists. Use -Force to overwrite it.");
                }

                Current.GetScpClient().Download(source, new FileInfo(partial));
                File.Move(partial, destination, Force);
                long length = new FileInfo(destination).Length;
                WriteObject(Result("Download", "Scp", source, destination, length, null, timer.Elapsed));
            }
            catch (Exception error) when (error is SshException or IOException) {
                try {
                    if (partial.Length > 0) {
                        File.Delete(partial);
                    }
                }
                catch (IOException) {
                    // Best effort.
                }

                WriteObject(Result("Download", "Scp", source, destination, 0, error.Message, timer.Elapsed));
                WriteItemError(error, "DownloadFailed", source);
            }
        }
    }
}
