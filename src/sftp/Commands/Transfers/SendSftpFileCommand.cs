using System.Diagnostics;
using System.Management.Automation;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp.Commands;
/// <summary>Uploads local files to a directory on the server.</summary>
[Cmdlet(VerbsCommunications.Send, "SftpFile", SupportsShouldProcess = true, DefaultParameterSetName = PathSet)]
[OutputType(typeof(SftpTransferResult))]
public sealed class SendSftpFileCommand : SftpTransferCommand {
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

    /// <summary>
    /// Where to upload: an existing folder (the files keep their names), or a new file name for a single
    /// file. A path ending with / is always a folder. Defaults to the current folder on an Sftp drive of the same session, otherwise the home folder; ~ means the home folder.
    /// </summary>
    [Parameter(Position = 1)]
    [ArgumentCompleter(typeof(RemotePathCompleter))]
    [ValidateNotNullOrEmpty]
    public string? Destination { get; set; }

    /// <summary>Overwrite existing remote files.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <summary>Upload to name.filepart and rename when complete, so readers never see a partial file.</summary>
    [Parameter]
    public SwitchParameter UseTemporaryName { get; set; }

    /// <summary>Set the remote file's modification time to the local file's.</summary>
    [Parameter]
    public SwitchParameter PreserveTimestamp { get; set; }

    /// <summary>Upload folders and everything in them, keeping the folder structure.</summary>
    [Parameter]
    public SwitchParameter Recurse { get; set; }

    private string? _destination;

    /// <inheritdoc/>
    protected override IEnumerable<string> DrivePaths => Destination is null ? [] : [Destination];

    /// <inheritdoc/>
    protected override void ProcessItems() {
        try {
            _destination = Destination is null ? null : RemoteOrDrivePath(Destination);
        }
        catch (ArgumentException error) {
            ThrowTerminatingError(new ErrorRecord(error, "DestinationOnOtherSession", ErrorCategory.InvalidArgument, Destination));
        }

        bool literal = ParameterSetName == LiteralPathSet;
        foreach (string file in LocalFiles(literal ? LiteralPath : Path, literal)) {
            if (Recurse && System.IO.Directory.Exists(file)) {
                UploadFolder(file);
            }
            else {
                Upload(file, RemoteTarget(_destination, System.IO.Path.GetFileName(file)));
            }
        }
    }

    /// <summary>Uploads a folder tree. Like Copy-Item -Recurse: into -Destination when it is a folder, otherwise as -Destination.</summary>
    private void UploadFolder(string folder) {
        string name = System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(folder));
        string remoteRoot = RemoteTarget(_destination, name);
        try {
            if (ShouldProcess(remoteRoot, "Create folder")) {
                EnsureRemoteFolder(remoteRoot);
            }

            foreach (string directory in System.IO.Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories)) {
                string remoteFolder = RemotePath.Combine(remoteRoot, System.IO.Path.GetRelativePath(folder, directory).Replace('\\', '/'));
                if (ShouldProcess(remoteFolder, "Create folder")) {
                    EnsureRemoteFolder(remoteFolder);
                }
            }
        }
        catch (Exception error) when (error is SshException or IOException or UnauthorizedAccessException) {
            WriteItemError(error, "UploadFailed", folder);

            return;
        }

        foreach (string file in System.IO.Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)) {
            Upload(file, RemotePath.Combine(remoteRoot, System.IO.Path.GetRelativePath(folder, file).Replace('\\', '/')));
        }
    }

    private void Upload(string source, string destination) {
        var info = new FileInfo(source);
        if (!info.Exists) {
            var error = System.IO.Directory.Exists(source) ?
                new IOException($"'{source}' is a folder; add -Recurse to upload it with its contents.") :
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
        WriteVerbose($"Uploading {source} to {Current.Name}:{target} ({info.Length} bytes)");
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

            WriteVerbose($"Uploaded {destination} in {timer.Elapsed.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} s");
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
