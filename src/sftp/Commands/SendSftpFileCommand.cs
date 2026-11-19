using System.Diagnostics;
using System.Management.Automation;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp.Commands;
/// <summary>Uploads local files to a directory on the server.</summary>
[Cmdlet(VerbsCommunications.Send, "SftpFile", SupportsShouldProcess = true, DefaultParameterSetName = PathSet)]
[OutputType(typeof(SftpTransferResult))]
public sealed class SendSftpFileCommand : SftpCommand {
    internal const string TemporarySuffix = ".filepart";

    /// <summary>Local files to upload. Wildcards are supported.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PathSet, ValueFromPipeline = true)]
    [SupportsWildcards]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = [];

    /// <summary>Local files to upload, used exactly as typed.</summary>
    [Parameter(Mandatory = true, ParameterSetName = LiteralPathSet, ValueFromPipelineByPropertyName = true)]
    [Alias("PSPath", "FullName", "LocalPath")]
    [ValidateNotNullOrEmpty]
    public string[] LiteralPath { get; set; } = [];

    /// <summary>The name of the session to use; filled in from the SessionName of piped files and results.</summary>
    [Parameter(ParameterSetName = LiteralPathSet, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string? SessionName { get; set; }

    /// <inheritdoc/>
    protected override string? PipedSessionName => SessionName;

    /// <summary>The remote directory to upload into. It must exist.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string Destination { get; set; } = "";

    /// <summary>Overwrite existing remote files.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <summary>Upload to name.filepart and rename when complete, so readers never see a partial file.</summary>
    [Parameter]
    public SwitchParameter UseTemporaryName { get; set; }

    /// <summary>Set the remote file's modification time to the local file's.</summary>
    [Parameter]
    public SwitchParameter PreserveTimestamp { get; set; }

    /// <inheritdoc/>
    protected override void ProcessItems() {
        bool literal = ParameterSetName == LiteralPathSet;
        foreach (string file in LocalFiles(literal ? LiteralPath : Path, literal)) {
            Upload(file);
        }
    }
    private void Upload(string source) {
        var info = new FileInfo(source);
        string destination = RemotePath.Combine(RemoteFullPath(Destination), info.Name);
        if (!info.Exists) {
            var error = System.IO.Directory.Exists(source) ?
                new IOException($"'{source}' is a directory; Send-SftpFile uploads files.") :
                (Exception)new FileNotFoundException($"File not found: {source}", source);
            WriteObject(Result("Upload", "Sftp", source, destination, 0, error.Message, TimeSpan.Zero));
            WriteItemError(error, "UploadFailed", source);

            return;
        }

        if (!ShouldProcess($"{source} -> {destination}", "Upload")) {
            return;
        }

        var timer = Stopwatch.StartNew();
        string target = UseTemporaryName ? destination + TemporarySuffix : destination;
        try {
            if (!Force && Client.Exists(destination)) {
                throw new IOException($"{destination} already exists. Use -Force to overwrite it.");
            }

            using (FileStream stream = info.OpenRead()) {
                var transfer = (SftpUploadAsyncResult)Client.BeginUploadFile(stream, target, true, null, null, null);
                WaitForTransfer(
                    transfer,
                    () => transfer.UploadedBytes,
                    () => transfer.IsUploadCanceled = true,
                    info.Length,
                    "Uploading",
                    $"{info.Name} -> {destination}");
                Client.EndUploadFile(transfer);
            }

            if (UseTemporaryName) {
                Rename(target, destination);
            }

            if (PreserveTimestamp) {
                Client.SetLastWriteTimeUtc(destination, info.LastWriteTimeUtc);
            }

            WriteObject(Result("Upload", "Sftp", source, destination, info.Length, null, timer.Elapsed));
        }
        catch (Exception error) when (error is SshException or IOException or OperationCanceledException or UnauthorizedAccessException) {
            if (UseTemporaryName) {
                TryDelete(target);
            }

            WriteObject(Result("Upload", "Sftp", source, destination, 0, error.Message, timer.Elapsed));
            if (error is OperationCanceledException) {
                throw new PipelineStoppedException();
            }

            WriteItemError(error, "UploadFailed", source);
        }
    }

    private void Rename(string from, string to) {
        try {
            // POSIX rename replaces an existing file atomically where the server supports it.
            Client.RenameFile(from, to, isPosix: true);
        }
        catch (Exception error) when (error is SshException or NotSupportedException) {
            if (Client.Exists(to)) {
                Client.DeleteFile(to);
            }

            Client.RenameFile(from, to, isPosix: false);
        }
    }

    private void TryDelete(string path) {
        try {
            if (Client.IsConnected && Client.Exists(path)) {
                Client.DeleteFile(path);
            }
        }
        catch (SshException) {
            // Best effort: the original error is what matters.
        }
    }
}
