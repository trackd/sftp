using System.Diagnostics;
using System.Management.Automation;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp.Commands;
/// <summary>Downloads remote files into a local directory.</summary>
[Cmdlet(VerbsCommunications.Receive, "SftpFile", SupportsShouldProcess = true, DefaultParameterSetName = PathSet)]
[OutputType(typeof(SftpTransferResult))]
public sealed class ReceiveSftpFileCommand : SftpTransferCommand {
    /// <summary>Remote files to download. Wildcards are supported.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PathSet, ValueFromPipeline = true)]
    [SupportsWildcards]
    [ArgumentCompleter(typeof(RemotePathCompleter))]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = [];

    /// <summary>Remote files to download, used exactly as typed. Accepts Get-SftpFile output.</summary>
    [Parameter(Mandatory = true, ParameterSetName = LiteralPathSet, ValueFromPipelineByPropertyName = true)]
    [Alias("FullName", "RemotePath")]
    [ArgumentCompleter(typeof(RemotePathCompleter))]
    [ValidateNotNullOrEmpty]
    public string[] LiteralPath { get; set; } = [];

    /// <summary>The name of the session to use; filled in from the SessionName of piped files and results.</summary>
    [Parameter(ParameterSetName = LiteralPathSet, ValueFromPipelineByPropertyName = true)]
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

    /// <summary>Set the local file's modification time to the remote file's.</summary>
    [Parameter]
    public SwitchParameter PreserveTimestamp { get; set; }

    /// <summary>Download folders and everything in them, keeping the folder structure.</summary>
    [Parameter]
    public SwitchParameter Recurse { get; set; }

    /// <inheritdoc/>
    protected override IEnumerable<string> DrivePaths => ParameterSetName == LiteralPathSet ? LiteralPath : Path;

    /// <inheritdoc/>
    protected override void ProcessItems() {
        bool literal = ParameterSetName == LiteralPathSet;
        foreach (string path in literal ? LiteralPath : Path) {
            List<ISftpFile> items;
            try {
                items = [.. RemoteItems(path, literal)];
            }
            catch (Exception error) when (error is SshException or IOException or ItemNotFoundException or ArgumentException) {
                string destination = Destination ?? RemotePath.GetName(path);
                WriteObject(Result("Download", "Sftp", path, destination, 0, error.Message, TimeSpan.Zero));
                WriteItemError(error, "DownloadFailed", path);
                continue;
            }

            foreach (ISftpFile item in items) {
                if (Recurse && item.IsDirectory && !item.IsSymbolicLink) {
                    DownloadFolder(item);
                }
                else {
                    Download(item, null);
                }
            }
        }
    }

    /// <summary>Downloads a folder tree. Like Copy-Item -Recurse: into -Destination when it is a folder, otherwise as -Destination.</summary>
    private void DownloadFolder(ISftpFile folder) {
        string localRoot;
        try {
            if (string.IsNullOrEmpty(Destination)) {
                localRoot = System.IO.Path.Join(SessionState.Path.CurrentFileSystemLocation.ProviderPath, folder.Name);
            }
            else {
                string local = ResolveLocalPath(Destination);
                localRoot = System.IO.Directory.Exists(local) ? System.IO.Path.Join(local, folder.Name) : local;
            }

            if (ShouldProcess(localRoot, "Create folder")) {
                System.IO.Directory.CreateDirectory(localRoot);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) {
            WriteItemError(error, "DownloadFailed", folder.FullName);

            return;
        }

        DownloadTree(folder.FullName, localRoot);
    }

    private void DownloadTree(string remoteFolder, string localFolder) {
        List<ISftpFile> children;
        try {
            children = [.. List(remoteFolder)];
        }
        catch (Exception error) when (error is SshException or IOException) {
            WriteItemError(error, "DownloadFailed", remoteFolder);

            return;
        }

        foreach (ISftpFile child in children) {
            string target = System.IO.Path.Join(localFolder, child.Name);
            if (!child.IsDirectory) {
                Download(child, target);
                continue;
            }

            if (child.IsSymbolicLink) {
                continue;
            }

            if (ShouldProcess(target, "Create folder")) {
                System.IO.Directory.CreateDirectory(target);
            }

            DownloadTree(child.FullName, target);
        }
    }

    private void Download(ISftpFile remote, string? target) {
        var timer = Stopwatch.StartNew();
        string source = remote.FullName;
        string destination = target ?? Destination ?? remote.Name;
        string partial = "";
        try {
            destination = target ?? LocalTarget(Destination, remote.Name);
            // Download beside the target and rename at the end, so readers never see a partial file.
            partial = destination + SendSftpFileCommand.TemporarySuffix;
            if (remote.IsDirectory) {
                throw new IOException($"{source} is a folder; add -Recurse to download it with its contents.");
            }

            if (!ShouldProcess($"{source} -> {destination}", "Download")) {
                return;
            }

            if (!Force && File.Exists(destination)) {
                throw new IOException($"{destination} already exists. Use -Force to overwrite it.");
            }

            WriteVerbose($"Downloading {Current.Name}:{source} to {destination} ({remote.Length} bytes)");
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

            WriteVerbose($"Downloaded {source} in {timer.Elapsed.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} s");
            WriteObject(Result("Download", "Sftp", source, destination, remote.Length, null, timer.Elapsed));
        }
        catch (Exception error) when (error is SshException or IOException or OperationCanceledException or UnauthorizedAccessException) {
            if (partial.Length > 0) {
                TryDelete(partial);
            }

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
