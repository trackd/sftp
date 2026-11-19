using System.Diagnostics;
using System.Management.Automation;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp.Commands;
/// <summary>Downloads remote files into a local directory.</summary>
[Cmdlet(VerbsCommunications.Receive, "SftpFile", SupportsShouldProcess = true, DefaultParameterSetName = PathSet)]
[OutputType(typeof(SftpTransferResult))]
public sealed class ReceiveSftpFileCommand : SftpCommand {
    /// <summary>Remote files to download. Wildcards are supported.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PathSet, ValueFromPipeline = true)]
    [SupportsWildcards]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = [];

    /// <summary>Remote files to download, used exactly as typed. Accepts Get-SftpFile output.</summary>
    [Parameter(Mandatory = true, ParameterSetName = LiteralPathSet, ValueFromPipelineByPropertyName = true)]
    [Alias("FullName", "RemotePath")]
    [ValidateNotNullOrEmpty]
    public string[] LiteralPath { get; set; } = [];

    /// <summary>The name of the session to use; filled in from the SessionName of piped files and results.</summary>
    [Parameter(ParameterSetName = LiteralPathSet, ValueFromPipelineByPropertyName = true)]
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

    /// <summary>Set the local file's modification time to the remote file's.</summary>
    [Parameter]
    public SwitchParameter PreserveTimestamp { get; set; }

    private string _directory = "";

    /// <inheritdoc/>
    protected override void BeginProcessing() {
        base.BeginProcessing();
        _directory = LocalPath(Destination);
        if (!System.IO.Directory.Exists(_directory)) {
            ThrowTerminatingError(new ErrorRecord(
                new DirectoryNotFoundException($"Destination directory not found: {_directory}"),
                "DestinationNotFound",
                ErrorCategory.ObjectNotFound,
                Destination));
        }
    }

    /// <inheritdoc/>
    protected override void ProcessItems() {
        bool literal = ParameterSetName == LiteralPathSet;
        foreach (string path in literal ? LiteralPath : Path) {
            List<ISftpFile> items;
            try {
                items = [.. RemoteItems(path, literal)];
            }
            catch (Exception error) when (error is SshException or IOException or ItemNotFoundException) {
                string destination = System.IO.Path.Join(_directory, RemotePath.GetName(path));
                WriteObject(Result("Download", "Sftp", path, destination, 0, error.Message, TimeSpan.Zero));
                WriteItemError(error, "DownloadFailed", path);
                continue;
            }

            foreach (ISftpFile item in items) {
                Download(item);
            }
        }
    }

    private void Download(ISftpFile remote) {
        var timer = Stopwatch.StartNew();
        string source = remote.FullName;
        string destination = System.IO.Path.Join(_directory, remote.Name);
        // Download beside the target and rename at the end, so readers never see a partial file.
        string partial = destination + SendSftpFileCommand.TemporarySuffix;
        try {
            if (remote.IsDirectory) {
                throw new IOException($"{source} is a directory; Receive-SftpFile downloads files.");
            }

            if (!ShouldProcess($"{source} -> {destination}", "Download")) {
                return;
            }

            if (!Force && File.Exists(destination)) {
                throw new IOException($"{destination} already exists. Use -Force to overwrite it.");
            }

            using (var stream = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None)) {
                var transfer = (SftpDownloadAsyncResult)Client.BeginDownloadFile(source, stream, null, null, null);
                WaitForTransfer(
                    transfer,
                    () => transfer.DownloadedBytes,
                    () => transfer.IsDownloadCanceled = true,
                    remote.Length,
                    "Downloading",
                    $"{source} -> {destination}");
                Client.EndDownloadFile(transfer);
            }

            File.Move(partial, destination, Force);
            if (PreserveTimestamp) {
                File.SetLastWriteTimeUtc(destination, remote.LastWriteTimeUtc);
            }

            WriteObject(Result("Download", "Sftp", source, destination, remote.Length, null, timer.Elapsed));
        }
        catch (Exception error) when (error is SshException or IOException or OperationCanceledException or UnauthorizedAccessException) {
            TryDelete(partial);
            WriteObject(Result("Download", "Sftp", source, destination, 0, error.Message, timer.Elapsed));
            if (error is OperationCanceledException) {
                throw new PipelineStoppedException();
            }

            WriteItemError(error, "DownloadFailed", source);
        }
    }

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        }
        catch (IOException) {
            // Best effort: the original error is what matters.
        }
        catch (UnauthorizedAccessException) {
            // Same as above.
        }
    }
}
