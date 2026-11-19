using System.Diagnostics;
using System.Management.Automation;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace sftp.Commands;
/// <summary>Downloads remote files with SCP, for servers that do not offer SFTP.</summary>
[Cmdlet(VerbsCommunications.Receive, "ScpFile", SupportsShouldProcess = true)]
[OutputType(typeof(SftpTransferResult))]
public sealed class ReceiveScpFileCommand : SftpCommand {
    /// <summary>Remote files to download, used exactly as typed (SCP cannot expand wildcards). Accepts Get-SftpFile output.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("FullName", "RemotePath", "LiteralPath")]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = [];

    /// <summary>The name of the session to use; filled in from the SessionName of piped files and results.</summary>
    [Parameter(ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string? SessionName { get; set; }

    /// <inheritdoc/>
    protected override string? PipedSessionName => SessionName;

    /// <summary>The local directory to download into. It must exist.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string Destination { get; set; } = "";

    /// <summary>Overwrite existing local files.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <inheritdoc/>
    protected override void ProcessItems() {
        string directory = LocalPath(Destination);
        foreach (string path in Path) {
            var timer = Stopwatch.StartNew();
            string destination = System.IO.Path.Join(directory, RemotePath.GetName(path));
            string partial = destination + SendSftpFileCommand.TemporarySuffix;
            try {
                if (!System.IO.Directory.Exists(directory)) {
                    throw new DirectoryNotFoundException($"Destination directory not found: {directory}");
                }

                if (!ShouldProcess($"{path} -> {destination}", "Download (SCP)")) {
                    continue;
                }

                if (!Force && File.Exists(destination)) {
                    throw new IOException($"{destination} already exists. Use -Force to overwrite it.");
                }

                Current.GetScpClient().Download(path, new FileInfo(partial));
                File.Move(partial, destination, Force);
                long length = new FileInfo(destination).Length;
                WriteObject(Result("Download", "Scp", path, destination, length, null, timer.Elapsed));
            }
            catch (Exception error) when (error is SshException or IOException) {
                try {
                    File.Delete(partial);
                }
                catch (IOException) {
                    // Best effort.
                }

                WriteObject(Result("Download", "Scp", path, destination, 0, error.Message, timer.Elapsed));
                WriteItemError(error, "DownloadFailed", path);
            }
        }
    }
}
