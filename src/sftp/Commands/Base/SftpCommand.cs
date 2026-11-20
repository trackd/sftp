using System.Diagnostics;
using System.Management.Automation;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp.Commands;
/// <summary>Shared behaviour for cmdlets that work on an open session.</summary>
public abstract class SftpCommand : PSCmdlet, IDisposable {
    internal const string PathSet = "Path";
    internal const string LiteralPathSet = "LiteralPath";
    private static readonly System.Buffers.SearchValues<char> s_driveNameCharacters =
        System.Buffers.SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-.");
    private int _progressId;
    private VerboseCapture? _log;
    private SftpSession? _ownedSession;

    /// <summary>
    /// The session to use, as a session object or its name. Defaults to the session of piped files and
    /// results, then to the session of an Sftp drive in the path or current location, then to the current session.
    /// </summary>
    [Parameter]
    [SessionTransformation]
    [ArgumentCompleter(typeof(SessionNameCompleter))]
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
    protected override void BeginProcessing() {
        _progressId = Math.Abs(GetHashCode() % 10000) + 1;
        _log = VerboseCapture.Start(this);
    }

    /// <inheritdoc/>
    protected override void EndProcessing() => FlushVerbose();

    /// <summary>Writes SSH.NET log messages collected for -Verbose. Call only on the pipeline thread.</summary>
    protected void FlushVerbose() => _log?.Flush();

    /// <summary>
    /// Paths that may name an Sftp drive (drive:\path). When -Session is not given and one of them is on an
    /// Sftp drive, the drive's session is used.
    /// </summary>
    protected virtual IEnumerable<string> DrivePaths => [];

    /// <summary>
    /// Uses a session this command opened itself (a one-shot transfer). It is closed when the command ends,
    /// on success, error or Ctrl+C.
    /// </summary>
    internal void UseOwnedSession(SftpSession session) {
        _ownedSession = session;
        Session = session;
    }

    /// <summary>
    /// Resolves a path on an Sftp drive to the drive and the remote path: drive:\path, or, when
    /// <paramref name="relativeToLocation"/> is set and the current location is on an Sftp drive, a relative
    /// path (one that does not start with / or ~). Other paths return false.
    /// </summary>
    internal bool TryDrivePath(
        string path, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SftpDriveInfo? drive, out string remotePath, bool relativeToLocation = false) {
        drive = null;
        remotePath = path;
        int colon = path.IndexOf(':');
        if (colon < 1 || path.AsSpan(0, colon).ContainsAnyExcept(s_driveNameCharacters)) {
            if (!relativeToLocation || path.Length == 0 || path[0] is '/' or '~' || LocationDrive is not { } here) {
                return false;
            }

            drive = here;
        }
        else {
            try {
                drive = SessionState.Drive.Get(path[..colon]) as SftpDriveInfo;
            }
            catch (System.Management.Automation.DriveNotFoundException) {
                return false;
            }

            if (drive is null) {
                return false;
            }
        }

        remotePath = drive.ToRemote(SessionState.Path.GetUnresolvedProviderPathFromPSPath(path));

        return true;
    }

    /// <summary>The Sftp drive of the current location (after Set-Location prod:\), if any.</summary>
    internal SftpDriveInfo? LocationDrive => SessionState.Path.CurrentLocation.Drive as SftpDriveInfo;

    /// <summary>
    /// The remote path for a path that may be on an Sftp drive of the session in use. Relative paths follow the
    /// current location when it is on a drive of this session.
    /// </summary>
    internal string RemoteOrDrivePath(string path) {
        bool relative = LocationDrive is { } here && ReferenceEquals(here.Session, Current);
        if (!TryDrivePath(path, out SftpDriveInfo? drive, out string remote, relative)) {
            return path;
        }

        if (!ReferenceEquals(drive.Session, Current)) {
            throw new ArgumentException($"'{path}' is on drive {drive.Name}:, which uses session '{drive.Session.Name}', not '{Current.Name}'.");
        }

        // Keep a trailing separator: it means "into this folder".
        return path.EndsWith('\\') || path.EndsWith('/') ? remote.TrimEnd('/') + "/" : remote;
    }

    /// <inheritdoc/>
    public void Dispose() {
        _log?.Dispose();
        _log = null;
        _ownedSession?.Dispose();
        _ownedSession = null;
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    protected sealed override void ProcessRecord() {
        SftpSession? session = Session;
        string? sessionName = PipedSessionName;
        if (session is null && string.IsNullOrEmpty(sessionName)) {
            // A drive path (prod:\upload) uses the drive's connection; so does any path while the current
            // location is on an Sftp drive. No new connection is opened.
            foreach (string path in DrivePaths) {
                if (TryDrivePath(path, out SftpDriveInfo? drive, out _)) {
                    session = drive.Session;
                    break;
                }
            }

            session ??= LocationDrive?.Session;
        }

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
        foreach (string path in DrivePaths) {
            if (TryDrivePath(path, out SftpDriveInfo? drive, out _) && !ReferenceEquals(drive.Session, Current)) {
                WriteError(new ErrorRecord(
                    new ArgumentException($"'{path}' is on drive {drive.Name}:, which uses session '{drive.Session.Name}', not '{Current.Name}'. Leave out -Session to use the drive's session."),
                    "DriveOnOtherSession",
                    ErrorCategory.InvalidArgument,
                    path));

                return;
            }
        }

        try {
            ProcessItems();
        }
        finally {
            FlushVerbose();
        }
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
    protected string ResolveLocalPath(string path) {
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
                yield return ResolveLocalPath(path);
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

    /// <summary>
    /// Makes a remote path absolute: "~" and "~/..." start at the home folder, other relative paths at the
    /// working directory, and "." and ".." are resolved.
    /// </summary>
    protected string RemoteFullPath(string path) => Current.FullPath(RemoteOrDrivePath(path));

    private readonly Dictionary<string, bool> _remoteFolders = new(StringComparer.Ordinal);

    /// <summary>Creates a remote folder and any missing parents; an existing folder is fine.</summary>
    protected void EnsureRemoteFolder(string fullPath) {
        string current = "";
        foreach (string segment in fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries)) {
            current += "/" + segment;
            if (!Client.Exists(current)) {
                Client.CreateDirectory(current);
            }
            else if (!Client.GetAttributes(current).IsDirectory) {
                throw new IOException($"{current} exists and is not a folder.");
            }

            _remoteFolders[current] = true;
        }
    }

    /// <summary>True when the remote path is an existing folder.</summary>
    protected bool IsRemoteFolder(string fullPath) {
        if (!_remoteFolders.TryGetValue(fullPath, out bool isFolder)) {
            try {
                isFolder = Client.GetAttributes(fullPath).IsDirectory;
            }
            catch (SftpPathNotFoundException) {
                isFolder = false;
            }

            _remoteFolders[fullPath] = isFolder;
        }

        return isFolder;
    }

    /// <summary>
    /// Where an uploaded file goes. Like Copy-Item: into -Destination when it is an existing folder (or
    /// ends with /), otherwise -Destination is the new file name. Without -Destination: the current folder
    /// when the location is on an Sftp drive of the session in use, otherwise the home folder.
    /// </summary>
    protected string RemoteTarget(string? destination, string fileName) {
        if (string.IsNullOrEmpty(destination)) {
            string folder = LocationDrive is { } here && ReferenceEquals(here.Session, Current) ?
                here.ToRemote(SessionState.Path.CurrentLocation.ProviderPath) :
                RemoteFullPath("~");

            return RemotePath.Combine(folder, fileName);
        }

        string full = RemoteFullPath(destination);
        bool intoFolder = destination.EndsWith('/') || destination.EndsWith('\\') || destination is "~" or "." or ".." || IsRemoteFolder(full);

        return intoFolder ? RemotePath.Combine(full, fileName) : full;
    }

    /// <summary>
    /// Where a downloaded file goes. Like Copy-Item: into -Destination when it is an existing folder (or
    /// ends with a slash), otherwise -Destination is the new file name. Without -Destination: the current folder.
    /// </summary>
    protected string LocalTarget(string? destination, string fileName) {
        if (string.IsNullOrEmpty(destination)) {
            return Path.Join(SessionState.Path.CurrentFileSystemLocation.ProviderPath, fileName);
        }

        string local = ResolveLocalPath(destination);
        if (Directory.Exists(local) || destination.EndsWith('/') || destination.EndsWith('\\')) {
            return Directory.Exists(local) ? Path.Join(local, fileName) : throw new DirectoryNotFoundException($"Destination folder not found: {local}");
        }

        string? parent = Path.GetDirectoryName(local);

        return parent is null || Directory.Exists(parent) ? local : throw new DirectoryNotFoundException($"Destination folder not found: {parent}");
    }
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
        SftpItem.Create(item.FullName, item.Attributes, Current.Name);

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
            FlushVerbose();
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
