using System.Diagnostics;
using System.Management.Automation;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp.Commands;
/// <summary>Makes a folder on the server match a local folder, or the other way round.</summary>
[Cmdlet(VerbsData.Sync, "SftpFolder", SupportsShouldProcess = true)]
[OutputType(typeof(SftpTransferResult))]
public sealed class SyncSftpFolderCommand : SftpCommand {
    /// <inheritdoc/>
    protected override IEnumerable<string> DrivePaths => [RemotePath];

    /// <summary>The folder on this computer.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string LocalPath { get; set; } = "";

    /// <summary>The folder on the server.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ArgumentCompleter(typeof(RemotePathCompleter))]
    [ValidateNotNullOrEmpty]
    public string RemotePath { get; set; } = "";

    /// <summary>Upload makes the server folder match the local one; Download makes the local folder match the server.</summary>
    [Parameter(Mandatory = true)]
    public SyncDirection Direction { get; set; }

    /// <summary>Also delete files and folders that are only in the destination. Use -WhatIf first to see what would go.</summary>
    [Parameter]
    public SwitchParameter Remove { get; set; }

    /// <inheritdoc/>
    protected override void ProcessItems() {
        string local;
        string remote;
        List<SyncAction> plan;
        try {
            local = ResolveLocalPath(LocalPath);
            remote = RemoteFullPath(RemotePath);
            bool upload = Direction == SyncDirection.Upload;
            if (upload && !Directory.Exists(local)) {
                throw new DirectoryNotFoundException($"Local folder not found: {local}");
            }

            if (!upload && !IsRemoteFolder(remote)) {
                throw new DirectoryNotFoundException($"Remote folder not found: {remote}");
            }

            List<SyncEntry> localEntries = Directory.Exists(local) ? [.. LocalEntries(local)] : [];
            List<SyncEntry> remoteEntries = IsRemoteFolder(remote) ? [.. RemoteEntries(remote, "")] : [];
            plan = upload ? SyncPlanner.Plan(localEntries, remoteEntries, Remove) : SyncPlanner.Plan(remoteEntries, localEntries, Remove);
            WriteVerbose($"{Direction} {local} <-> {Current.Name}:{remote}: {plan.Count(a => a.Kind == SyncActionKind.Copy)} to copy, " +
                $"{plan.Count(a => a.Kind == SyncActionKind.Remove)} to remove, {localEntries.Count + remoteEntries.Count} items compared");
            if (upload && !IsRemoteFolder(remote) && ShouldProcess(remote, "Create folder")) {
                EnsureRemoteFolder(remote);
            }
            else if (!upload && !Directory.Exists(local) && ShouldProcess(local, "Create folder")) {
                Directory.CreateDirectory(local);
            }
        }
        catch (Exception error) when (error is SshException or IOException or UnauthorizedAccessException or ArgumentException) {
            WriteItemError(error, "SyncFailed", Direction == SyncDirection.Upload ? LocalPath : RemotePath);

            return;
        }

        foreach (SyncAction action in plan) {
            if (Stopping) {
                break;
            }

            string localItem = Path.Join(local, action.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            string remoteItem = sftp.RemotePath.Combine(remote, action.RelativePath);
            switch (action.Kind) {
                case SyncActionKind.Remove:
                    RemoveItem(Direction == SyncDirection.Upload ? remoteItem : localItem, action);
                    break;
                case SyncActionKind.CreateFolder:
                    CreateFolder(Direction == SyncDirection.Upload ? remoteItem : localItem);
                    break;
                default:
                    if (Direction == SyncDirection.Upload) {
                        Upload(localItem, remoteItem, action.Reason);
                    }
                    else {
                        Download(remoteItem, localItem, action.Reason);
                    }

                    break;
            }
        }
    }

    private static IEnumerable<SyncEntry> LocalEntries(string root) {
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = true };
        foreach (FileSystemInfo item in new DirectoryInfo(root).EnumerateFileSystemInfos("*", options)) {
            string relative = Path.GetRelativePath(root, item.FullName).Replace('\\', '/');
            bool folder = item is DirectoryInfo;
            yield return new SyncEntry(relative, folder, folder ? 0 : ((FileInfo)item).Length, item.LastWriteTimeUtc);
        }
    }

    private IEnumerable<SyncEntry> RemoteEntries(string folder, string relative) {
        foreach (ISftpFile item in List(folder).ToList()) {
            // Symbolic links are left alone: following them could leave the folder or loop.
            if (item.IsSymbolicLink) {
                continue;
            }

            string path = relative.Length == 0 ? item.Name : relative + "/" + item.Name;
            yield return new SyncEntry(path, item.IsDirectory, item.IsDirectory ? 0 : item.Length, item.LastWriteTimeUtc);
            if (item.IsDirectory) {
                foreach (SyncEntry child in RemoteEntries(item.FullName, path)) {
                    yield return child;
                }
            }
        }
    }

    private void RemoveItem(string path, SyncAction action) {
        if (!ShouldProcess(path, $"Remove ({action.Reason})")) {
            return;
        }

        string? error = null;
        try {
            if (Direction == SyncDirection.Upload) {
                if (action.IsDirectory) {
                    Client.DeleteDirectory(path);
                }
                else {
                    Client.DeleteFile(path);
                }
            }
            else if (action.IsDirectory) {
                Directory.Delete(path);
            }
            else {
                File.Delete(path);
            }

            WriteVerbose($"Removed {path} ({action.Reason})");
        }
        catch (Exception failure) when (failure is SshException or IOException or UnauthorizedAccessException) {
            error = failure.Message;
            WriteItemError(failure, "RemoveFailed", path);
        }

        WriteObject(Result("Remove", "Sftp", path, path, 0, error, TimeSpan.Zero));
    }

    private void CreateFolder(string path) {
        if (!ShouldProcess(path, "Create folder")) {
            return;
        }

        try {
            if (Direction == SyncDirection.Upload) {
                Client.CreateDirectory(path);
            }
            else {
                Directory.CreateDirectory(path);
            }
        }
        catch (Exception error) when (error is SshException or IOException or UnauthorizedAccessException) {
            WriteItemError(error, "CreateFolderFailed", path);
        }
    }

    private void Upload(string source, string destination, string reason) {
        if (!ShouldProcess($"{source} -> {destination} ({reason})", "Upload")) {
            return;
        }

        var timer = Stopwatch.StartNew();
        var info = new FileInfo(source);
        try {
            using (FileStream stream = info.OpenRead()) {
                var transfer = (SftpUploadAsyncResult)Client.BeginUploadFile(stream, destination, true, null, null, null);
                WaitForTransfer(transfer, () => transfer.UploadedBytes, () => transfer.IsUploadCanceled = true, info.Length, "Synchronizing", $"{info.Name} -> {destination}");
                Client.EndUploadFile(transfer);
            }

            // The same modification time on both sides is what makes the next run skip the file.
            Client.SetLastWriteTimeUtc(destination, info.LastWriteTimeUtc);
            WriteVerbose($"Uploaded {destination} ({reason})");
            WriteObject(Result("Upload", "Sftp", source, destination, info.Length, null, timer.Elapsed));
        }
        catch (Exception error) when (error is SshException or IOException or UnauthorizedAccessException or OperationCanceledException) {
            WriteObject(Result("Upload", "Sftp", source, destination, 0, error.Message, timer.Elapsed));
            if (error is OperationCanceledException) {
                throw new PipelineStoppedException();
            }

            WriteItemError(error, "UploadFailed", source);
        }
    }

    private void Download(string source, string destination, string reason) {
        if (!ShouldProcess($"{source} -> {destination} ({reason})", "Download")) {
            return;
        }

        var timer = Stopwatch.StartNew();
        string partial = destination + SendSftpFileCommand.TemporarySuffix;
        try {
            SftpFileAttributes attributes = Client.GetAttributes(source);
            using (var stream = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None)) {
                var transfer = (SftpDownloadAsyncResult)Client.BeginDownloadFile(source, stream, null, null, null);
                WaitForTransfer(transfer, () => transfer.DownloadedBytes, () => transfer.IsDownloadCanceled = true, attributes.Size, "Synchronizing", $"{source} -> {destination}");
                Client.EndDownloadFile(transfer);
            }

            File.Move(partial, destination, overwrite: true);
            File.SetLastWriteTimeUtc(destination, attributes.LastWriteTimeUtc);
            WriteVerbose($"Downloaded {source} ({reason})");
            WriteObject(Result("Download", "Sftp", source, destination, attributes.Size, null, timer.Elapsed));
        }
        catch (Exception error) when (error is SshException or IOException or UnauthorizedAccessException or OperationCanceledException) {
            try {
                File.Delete(partial);
            }
            catch (IOException) {
                // Best effort: the original error is what matters.
            }

            WriteObject(Result("Download", "Sftp", source, destination, 0, error.Message, timer.Elapsed));
            if (error is OperationCanceledException) {
                throw new PipelineStoppedException();
            }

            WriteItemError(error, "DownloadFailed", source);
        }
    }
}
