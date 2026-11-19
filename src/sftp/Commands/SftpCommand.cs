using System.Diagnostics;
using System.Management.Automation;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp.Commands;
/// <summary>Shared behaviour for cmdlets that work on an open session.</summary>
public abstract class SftpCommand : PSCmdlet {
    internal const string PathSet = "Path";
    internal const string LiteralPathSet = "LiteralPath";
    private int _progressId;

    /// <summary>
    /// The session to use, as a session object or its name. Defaults to the session of piped files and
    /// results, then to the current session (the most recent Connect-SftpServer).
    /// </summary>
    [Parameter]
    [SessionTransformation]
    public SftpSession? Session { get; set; }

    /// <summary>
    /// The session name carried by piped files and results. Commands declare their own -SessionName
    /// parameter in the parameter set that receives piped objects: a pipeline parameter shared by every
    /// set would keep the default -Path set alive and make PowerShell bind whole objects to -Path as text.
    /// </summary>
    protected virtual string? PipedSessionName => null;

    /// <summary>The session in use for the current pipeline object.</summary>
    internal SftpSession Current { get; private set; } = null!;

    /// <summary>The SFTP client of the session in use.</summary>
    internal SftpClient Client => Current.Client;

    /// <inheritdoc/>
    protected override void BeginProcessing() => _progressId = Math.Abs(GetHashCode() % 10000) + 1;

    /// <inheritdoc/>
    protected sealed override void ProcessRecord() {
        SftpSession? session = Session;
        string? sessionName = PipedSessionName;
        if (session is null && !string.IsNullOrEmpty(sessionName)) {
            session = SessionRegistry.Current.Find(sessionName);
            if (session is null) {
                WriteError(new ErrorRecord(
                    new InvalidOperationException($"No open session named '{sessionName}'. It may have been disconnected; see Get-SftpSession, or connect again with Connect-SftpServer."),
                    "SessionNotFound",
                    ErrorCategory.ConnectionError,
                    sessionName));

                return;
            }
        }

        session ??= SessionRegistry.Current.Default;
        if (session is null) {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Not connected. Run Connect-SftpServer first."),
                "NotConnected",
                ErrorCategory.ConnectionError,
                null));
        }

        if (!session.IsConnected) {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException($"The session '{session.Name}' is closed. Connect again with Connect-SftpServer."),
                "SessionClosed",
                ErrorCategory.ConnectionError,
                session));
        }

        Current = session;
        ProcessItems();
    }
    /// <summary>Handles the current pipeline object.</summary>
    protected abstract void ProcessItems();

    /// <summary>Maps SSH.NET failures to PowerShell error categories.</summary>
    internal static ErrorCategory CategoryOf(Exception error) => error switch {
        SftpPathNotFoundException or FileNotFoundException or DirectoryNotFoundException or ItemNotFoundException => ErrorCategory.ObjectNotFound,
        SftpPermissionDeniedException or UnauthorizedAccessException => ErrorCategory.PermissionDenied,
        SshConnectionException => ErrorCategory.ConnectionError,
        SshOperationTimeoutException or TimeoutException => ErrorCategory.OperationTimeout,
        OperationCanceledException => ErrorCategory.OperationStopped,
        IOException => ErrorCategory.WriteError,
        _ => ErrorCategory.InvalidOperation,
    };

    /// <summary>Writes a non-terminating error for one item.</summary>
    protected void WriteItemError(Exception error, string errorId, object? target) =>
        WriteError(new ErrorRecord(error, errorId, CategoryOf(error), target));

    /// <summary>Resolves a filesystem path without requiring it to exist.</summary>
    protected string LocalPath(string path) {
        string resolved = SessionState.Path.GetUnresolvedProviderPathFromPSPath(path, out ProviderInfo provider, out _);
        if (provider.Name != "FileSystem") {
            throw new ArgumentException($"'{path}' is not a FileSystem path.", nameof(path));
        }

        return resolved;
    }

    /// <summary>
    /// Resolves local file paths: wildcards are expanded for -Path, used as typed for -LiteralPath.
    /// Paths that match nothing are reported as errors.
    /// </summary>
    protected IEnumerable<string> LocalFiles(IEnumerable<string> paths, bool literal) {
        foreach (string path in paths) {
            if (literal) {
                yield return LocalPath(path);
                continue;
            }

            System.Collections.ObjectModel.Collection<string> resolved;
            try {
                resolved = GetResolvedProviderPathFromPSPath(path, out ProviderInfo provider);
                if (provider.Name != "FileSystem") {
                    throw new ArgumentException($"'{path}' is not a FileSystem path.");
                }
            }
            catch (Exception error) when (error is ItemNotFoundException or ArgumentException or System.Management.Automation.DriveNotFoundException) {
                WriteItemError(error, "PathNotFound", path);
                continue;
            }

            foreach (string item in resolved) {
                yield return item;
            }
        }
    }

    /// <summary>Resolves a remote path against the session's working directory.</summary>
    protected string RemoteFullPath(string path) =>
        path.StartsWith('/') ? path : RemotePath.Combine(Client.WorkingDirectory, path);

    /// <summary>
    /// Resolves remote items. For -Path, wildcards in the last segment match the names in that folder;
    /// for -LiteralPath the path is used as typed. Throws when nothing matches.
    /// </summary>
    protected IEnumerable<ISftpFile> RemoteItems(string path, bool literal) {
        string full = RemoteFullPath(path);
        string leaf = RemotePath.GetName(full);
        if (literal || !WildcardPattern.ContainsWildcardCharacters(leaf)) {
            return [Client.Get(full)];
        }

        var pattern = new WildcardPattern(leaf, WildcardOptions.IgnoreCase);
        List<ISftpFile> matches = [.. List(RemotePath.GetDirectory(full)).Where(item => pattern.IsMatch(item.Name))];

        return matches.Count > 0 ? matches : throw new ItemNotFoundException($"No items match '{full}'.");
    }

    /// <summary>Lists a remote folder without the "." and ".." entries.</summary>
    protected IEnumerable<ISftpFile> List(string directory) =>
        Client.ListDirectoryAsync(directory, PipelineStopToken).ToBlockingEnumerable(PipelineStopToken).Where(item => item.Name is not "." and not "..");

    /// <summary>Describes a remote item.</summary>
    internal SftpItem ToItem(ISftpFile item) =>
        new(item.Name, item.FullName, item.Length, item.LastWriteTimeUtc, item.IsDirectory, item.IsSymbolicLink) { SessionName = Current.Name };

    /// <summary>Builds a transfer result.</summary>
    internal SftpTransferResult Result(
        string operation, string protocol, string source, string destination, long length, string? error, TimeSpan duration) =>
        new(operation, protocol, source, destination, length, error is null, error, duration) { SessionName = Current.Name };

    /// <summary>
    /// Waits for an SSH.NET asynchronous transfer on the pipeline thread so progress and
    /// cancellation (Ctrl+C) work, then completes it.
    /// </summary>
    protected void WaitForTransfer(IAsyncResult transfer, Func<ulong> bytesDone, Action cancel, long total, string activity, string status) {
        var timer = Stopwatch.StartNew();
        bool cancelled = false;
        while (!transfer.AsyncWaitHandle.WaitOne(250)) {
            if (!cancelled && PipelineStopToken.IsCancellationRequested) {
                cancel();
                cancelled = true;
            }

            if (timer.ElapsedMilliseconds > 1000) {
                ulong done = bytesDone();
                int percent = total > 0 ? (int)Math.Min(100, done * 100 / (ulong)total) : -1;
                WriteProgress(new ProgressRecord(_progressId, activity, status) { PercentComplete = percent });
            }
        }

        if (timer.ElapsedMilliseconds > 1000) {
            WriteProgress(new ProgressRecord(_progressId, activity, status) { RecordType = ProgressRecordType.Completed });
        }

        PipelineStopToken.ThrowIfCancellationRequested();
    }
}
