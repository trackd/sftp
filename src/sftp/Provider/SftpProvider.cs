using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp;
/// <summary>
/// Browse and change files on an SFTP server with the standard item and content commands:
/// New-PSDrive -Name prod -PSProvider Sftp -Root / -Session prod, then dir prod:\, cd, Get-Content and so on.
/// Copying between drives of different providers is not possible; use Send-SftpFile and Receive-SftpFile.
/// </summary>
[CmdletProvider("Sftp", ProviderCapabilities.Filter | ProviderCapabilities.ShouldProcess | ProviderCapabilities.Credentials)]
[OutputType(typeof(SftpItem), ProviderCmdlet = ProviderCmdlet.GetItem)]
[OutputType(typeof(SftpItem), ProviderCmdlet = ProviderCmdlet.GetChildItem)]
public sealed class SftpProvider : NavigationCmdletProvider, IContentCmdletProvider {
    private static readonly string[] FolderTypes = ["directory", "container", "folder"];
    private static readonly string[] FileTypes = ["file", "leaf"];

    private SftpDriveInfo? _drive;

    /// <summary>The drive of the path being worked on; see <see cref="Use"/>.</summary>
    private SftpDriveInfo Drive => _drive ?? PSDriveInfo as SftpDriveInfo ??
        throw new PSInvalidOperationException("Sftp paths need an Sftp drive. Create one with New-PSDrive -PSProvider Sftp -Root / -Session <session>.");

    /// <summary>
    /// Finds the drive a provider path belongs to and returns the remote path. Provider paths start with the
    /// drive's root (its name), so piped items (whose PSPath carries no drive) still reach the right session.
    /// </summary>
    private string Use(string path) {
        _drive = DriveOf(path);

        return _drive.ToRemote(path);
    }

    private SftpDriveInfo DriveOf(string path) {
        string token = path.Replace('/', '\\').TrimStart('\\').Split('\\')[0];
        // The caller's session state sees drives created in its own scopes (scripts, tests), unlike ProviderInfo.Drives.
        foreach (PSDriveInfo drive in SessionState.Drive.GetAllForProvider(ProviderInfo.Name)) {
            if (drive is SftpDriveInfo sftpDrive && string.Equals(sftpDrive.Root, token, StringComparison.OrdinalIgnoreCase)) {
                return sftpDrive;
            }
        }

        return PSDriveInfo as SftpDriveInfo ??
            throw new PSInvalidOperationException("Sftp paths need an Sftp drive. Create one with New-PSDrive -PSProvider Sftp -Root / -Session <session>.");
    }

    private SftpClient Client => Drive.Session.Client;

    // ------------------------------------------------------------------ drives

    /// <inheritdoc/>
    protected override object NewDriveDynamicParameters() => new SftpDriveParameters();

    /// <inheritdoc/>
    protected override PSDriveInfo? NewDrive(PSDriveInfo drive) {
        if (drive is SftpDriveInfo) {
            return drive;
        }

        var parameters = DynamicParameters as SftpDriveParameters ?? new SftpDriveParameters();
        SftpSession session;
        bool owns = false;
        if (parameters.Session is not null) {
            if (parameters.HostName is not null) {
                ThrowTerminatingError(new ErrorRecord(
                    new PSArgumentException("Use either -Session or -HostName, not both."), "SessionAndHostName", ErrorCategory.InvalidArgument, drive.Name));
            }

            session = parameters.Session;
            if (!session.IsConnected) {
                ThrowTerminatingError(new ErrorRecord(
                    new PSInvalidOperationException($"The session '{session.Name}' is closed. Connect again with Connect-SftpServer."),
                    "SessionClosed",
                    ErrorCategory.ConnectionError,
                    session));
            }
        }
        else if (parameters.HostName is not null) {
            session = Connect(drive, parameters);
            owns = true;
        }
        else {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException("Give the drive a session with -Session, or a server to connect to with -HostName."),
                "SessionRequired",
                ErrorCategory.InvalidArgument,
                drive.Name));

            return null;
        }

        try {
            string root = string.IsNullOrEmpty(drive.Root) ? "/" : drive.Root.Replace('\\', '/');
            string remoteRoot = session.FullPath(root);
            if (!session.Client.GetAttributes(remoteRoot).IsDirectory) {
                throw new IOException($"{remoteRoot} is not a folder.");
            }

            WriteVerbose($"Drive {drive.Name}: is {session.Name}:{remoteRoot}");

            // The PowerShell root is the drive's name, like HKEY_LOCAL_MACHINE for HKLM:. A root of \ would make
            // PowerShell resolve paths that do not exist yet against the current file system drive.
            return new SftpDriveInfo(drive, drive.Name, $"{session.UserName}@{session.HostName}:{remoteRoot}", session, remoteRoot, owns);
        }
        catch (Exception error) when (error is SshException or IOException) {
            if (owns) {
                session.Dispose();
            }

            ThrowTerminatingError(new ErrorRecord(error, "DriveRootNotFound", ErrorCategory.ObjectNotFound, drive.Root));

            return null;
        }
    }

    private SftpSession Connect(PSDriveInfo drive, SftpDriveParameters parameters) {
        PSCredential? credential = drive.Credential is { } given && given != PSCredential.Empty && !string.IsNullOrEmpty(given.UserName) ? given : null;
        var options = new ConnectOptions {
            HostName = parameters.HostName!,
            Port = parameters.Port,
            Credential = credential,
            UserName = parameters.UserName,
            KeyFile = parameters.KeyFile,
            KeyPassphrase = parameters.KeyPassphrase,
            HostKeyFingerprint = parameters.HostKeyFingerprint,
            KnownHostsFile = parameters.KnownHostsFile,
            SkipHostKeyCheck = parameters.SkipHostKeyCheck,
        };
        SftpSession session;
        try {
            session = new SessionConnector(SessionState, WriteVerbose, WriteWarning).Connect(options, CancellationToken.None);
        }
        catch (SessionConnectException error) {
            ThrowTerminatingError(error.ErrorRecord);

            throw;
        }

        // Registered so items from the drive can be piped to other commands, but never as the current session.
        SessionRegistry.Current.Add(session, null, isPrivate: true);

        return session;
    }

    /// <inheritdoc/>
    protected override PSDriveInfo RemoveDrive(PSDriveInfo drive) {
        if (drive is SftpDriveInfo { OwnsSession: true } sftpDrive) {
            sftpDrive.Session.Dispose();
        }

        return drive;
    }

    // ------------------------------------------------------------------ items

    /// <inheritdoc/>
    protected override bool IsValidPath(string path) => !path.Contains('\0', StringComparison.Ordinal);

    /// <inheritdoc/>
    protected override bool ItemExists(string path) => Attributes(path) is not null;

    /// <inheritdoc/>
    protected override bool IsItemContainer(string path) => Attributes(path)?.IsDirectory == true;

    /// <inheritdoc/>
    protected override void GetItem(string path) {
        string remote = Use(path);
        Run(path, "GetItemFailed", () => {
            SftpFileAttributes attributes = Attributes(path) ?? throw new ItemNotFoundException($"Cannot find path '{remote}' because it does not exist.");
            WriteItem(remote, attributes);
        });
    }

    /// <inheritdoc/>
    protected override bool HasChildItems(string path) {
        string remote = Use(path);

        return Attributes(path)?.IsDirectory == true && List(remote).Any();
    }

    /// <inheritdoc/>
    protected override void GetChildItems(string path, bool recurse) => GetChildItems(path, recurse, uint.MaxValue);

    /// <inheritdoc/>
    protected override void GetChildItems(string path, bool recurse, uint depth) {
        string remote = Use(path);
        Run(path, "GetChildItemFailed", () => {
            SftpFileAttributes attributes = Attributes(path) ?? throw new ItemNotFoundException($"Cannot find path '{remote}' because it does not exist.");
            if (!attributes.IsDirectory) {
                WriteItem(remote, attributes);

                return;
            }

            WildcardPattern? filter = string.IsNullOrEmpty(Filter) ? null : new WildcardPattern(Filter, WildcardOptions.IgnoreCase);
            Walk(remote, recurse, depth, filter);
        });
    }

    private void Walk(string remote, bool recurse, uint depth, WildcardPattern? filter) {
        foreach (ISftpFile item in List(remote)) {
            if (Stopping) {
                return;
            }

            if (filter is null || filter.IsMatch(item.Name)) {
                WriteItem(item.FullName, item.Attributes);
            }

            if (recurse && depth > 0 && item.IsDirectory && !item.IsSymbolicLink) {
                try {
                    Walk(item.FullName, recurse, depth - 1, filter);
                }
                catch (Exception error) when (error is SshException or IOException) {
                    WriteError(new ErrorRecord(error, "GetChildItemFailed", Commands.SftpCommand.CategoryOf(error), Drive.ToProviderPath(item.FullName)));
                }
            }
        }
    }

    /// <inheritdoc/>
    protected override void GetChildNames(string path, ReturnContainers returnContainers) {
        string remote = Use(path);
        Run(path, "GetChildNamesFailed", () => {
            WildcardPattern? filter = string.IsNullOrEmpty(Filter) ? null : new WildcardPattern(Filter, WildcardOptions.IgnoreCase);
            foreach (ISftpFile item in List(remote)) {
                if (Stopping) {
                    return;
                }

                bool wanted = filter is null || filter.IsMatch(item.Name) || (returnContainers == ReturnContainers.ReturnAllContainers && item.IsDirectory);
                if (wanted) {
                    WriteItemObject(item.Name, Drive.ToProviderPath(item.FullName), item.IsDirectory);
                }
            }
        });
    }

    /// <inheritdoc/>
    protected override void NewItem(string path, string itemTypeName, object newItemValue) {
        string remote = Use(path);
        string type = string.IsNullOrEmpty(itemTypeName) ? "file" : itemTypeName.ToLowerInvariant();
        bool folder = FolderTypes.Contains(type);
        if (!folder && !FileTypes.Contains(type)) {
            WriteError(new ErrorRecord(
                new PSArgumentException($"The Sftp provider creates File and Directory items, not '{itemTypeName}'."),
                "UnsupportedItemType",
                ErrorCategory.InvalidArgument,
                itemTypeName));

            return;
        }

        if (!ShouldProcess(remote, folder ? "Create folder" : "Create file")) {
            return;
        }

        Run(path, "NewItemFailed", () => {
            SftpFileAttributes? existing = Attributes(path);
            if (existing is not null && (!Force || existing.IsDirectory != folder)) {
                throw new IOException($"{remote} already exists.");
            }

            if (folder) {
                if (existing is null) {
                    Client.CreateDirectory(remote);
                }
            }
            else {
                using SftpFileStream stream = Client.Open(remote, FileMode.Create, FileAccess.Write);
                switch (newItemValue is PSObject wrapped ? wrapped.BaseObject : newItemValue) {
                    case null:
                        break;
                    case byte[] bytes:
                        stream.Write(bytes);
                        break;
                    case object value:
                        stream.Write(new UTF8Encoding(false).GetBytes(value.ToString() ?? ""));
                        break;
                }
            }

            Drive.Cache.Clear();
            WriteItem(remote, Client.GetAttributes(remote));
        });
    }

    /// <inheritdoc/>
    protected override void RemoveItem(string path, bool recurse) {
        string remote = Use(path);
        if (remote == "/") {
            WriteError(new ErrorRecord(
                new PSInvalidOperationException("Removing the server's root folder is not allowed."), "RemoveRootRefused", ErrorCategory.PermissionDenied, path));

            return;
        }

        if (!ShouldProcess(remote, "Remove")) {
            return;
        }

        Run(path, "RemoveItemFailed", () => {
            SftpFileAttributes attributes = Attributes(path) ?? throw new ItemNotFoundException($"Cannot find path '{remote}' because it does not exist.");
            try {
                if (!attributes.IsDirectory) {
                    Client.DeleteFile(remote);
                }
                else if (recurse) {
                    DeleteTree(remote);
                }
                else if (List(remote).Any()) {
                    throw new IOException($"{remote} is not empty. Add -Recurse to remove it with its contents.");
                }
                else {
                    Client.DeleteDirectory(remote);
                }
            }
            finally {
                Drive.Cache.Clear();
            }
        });
    }

    private void DeleteTree(string remote) {
        foreach (ISftpFile item in List(remote).ToList()) {
            if (item.IsDirectory && !item.IsSymbolicLink) {
                DeleteTree(item.FullName);
            }
            else {
                Client.DeleteFile(item.FullName);
            }
        }

        Client.DeleteDirectory(remote);
    }

    /// <inheritdoc/>
    protected override void RenameItem(string path, string newName) {
        string remote = Use(path);
        if (string.IsNullOrEmpty(newName) || newName.IndexOfAny(['/', '\\']) >= 0 || newName is "." or "..") {
            WriteError(new ErrorRecord(
                new PSArgumentException($"'{newName}' is not a valid name. To move an item to another folder, use Move-Item."),
                "InvalidName",
                ErrorCategory.InvalidArgument,
                newName));

            return;
        }

        string target = RemotePath.Combine(RemotePath.GetDirectory(remote), newName);
        if (!ShouldProcess($"{remote} -> {target}", "Rename")) {
            return;
        }

        Run(path, "RenameItemFailed", () => {
            try {
                Client.RenameFile(remote, target);
            }
            finally {
                Drive.Cache.Clear();
            }

            WriteItem(target, Client.GetAttributes(target));
        });
    }

    /// <inheritdoc/>
    protected override void MoveItem(string path, string destination) {
        string remote = Use(path);
        if (!ReferenceEquals(DriveOf(destination), Drive)) {
            WriteError(new ErrorRecord(
                new PSArgumentException("Move-Item works within one Sftp drive. To copy between servers, download with Receive-SftpFile and upload with Send-SftpFile."),
                "MoveBetweenDrives",
                ErrorCategory.InvalidArgument,
                destination));

            return;
        }

        string target = Drive.ToRemote(destination);
        Run(path, "MoveItemFailed", () => {
            if (Attributes(destination)?.IsDirectory == true) {
                target = RemotePath.Combine(target, RemotePath.GetName(remote));
            }

            if (!ShouldProcess($"{remote} -> {target}", "Move")) {
                return;
            }

            try {
                Client.RenameFile(remote, target, isPosix: Force);
            }
            finally {
                Drive.Cache.Clear();
            }

            WriteItem(target, Client.GetAttributes(target));
        });
    }

    // ------------------------------------------------------------------ content

    /// <inheritdoc/>
    public IContentReader GetContentReader(string path) {
        string remote = Use(path);
        SftpFileAttributes attributes = Attributes(path) ?? throw new ItemNotFoundException($"Cannot find path '{remote}' because it does not exist.");
        if (attributes.IsDirectory) {
            throw new PSArgumentException($"{remote} is a folder; Get-Content reads files.");
        }

        var parameters = DynamicParameters as SftpContentParameters ?? new SftpContentParameters();

        return new SftpContentReader(Client.OpenRead(remote), TextEncoding.From(parameters.Encoding), parameters.AsByteStream, parameters.Raw);
    }

    /// <inheritdoc/>
    public object GetContentReaderDynamicParameters(string path) => new SftpContentParameters();

    /// <inheritdoc/>
    public IContentWriter GetContentWriter(string path) {
        string remote = Use(path);
        if (Attributes(path)?.IsDirectory == true) {
            throw new PSArgumentException($"{remote} is a folder; content can only be written to files.");
        }

        var parameters = DynamicParameters as SftpContentWriterParameters ?? new SftpContentWriterParameters();
        Drive.Cache.Clear();
        SftpDriveInfo drive = Drive;

        return new SftpContentWriter(
            Client.Open(remote, FileMode.OpenOrCreate, FileAccess.Write),
            TextEncoding.From(parameters.Encoding),
            parameters.AsByteStream,
            parameters.NoNewline,
            drive.Cache.Clear);
    }

    /// <inheritdoc/>
    public object GetContentWriterDynamicParameters(string path) => new SftpContentWriterParameters();

    /// <inheritdoc/>
    public void ClearContent(string path) {
        string remote = Use(path);
        if (Attributes(path)?.IsDirectory == true) {
            throw new PSArgumentException($"{remote} is a folder.");
        }

        if (ShouldProcess(remote, "Clear content")) {
            Client.Open(remote, FileMode.Create, FileAccess.Write).Dispose();
            Drive.Cache.Clear();
        }
    }

    /// <inheritdoc/>
    public object? ClearContentDynamicParameters(string path) => null;

    // ------------------------------------------------------------------ helpers

    private SftpFileAttributes? Attributes(string path) {
        string remote = Use(path);

        return Drive.Cache.Get(Drive.Session, remote);
    }

    private IEnumerable<ISftpFile> List(string remote) {
        SftpDriveInfo drive = Drive;
        foreach (ISftpFile item in Client.ListDirectory(remote)) {
            if (item.Name is "." or "..") {
                continue;
            }

            // Listings carry lstat attributes; a symbolic link to a folder is resolved when it is used.
            if (!item.IsSymbolicLink) {
                drive.Cache.Set(item.FullName, item.Attributes);
            }

            yield return item;
        }
    }

    private void WriteItem(string remote, SftpFileAttributes attributes) {
        SftpItem item = SftpItem.Create(remote, attributes, Drive.Session.Name);
        WriteItemObject(item, Drive.ToProviderPath(remote), attributes.IsDirectory);
    }

    private void Run(string path, string errorId, Action action) {
        try {
            action();
        }
        catch (Exception error) when (error is SshException or IOException or ItemNotFoundException or UnauthorizedAccessException) {
            WriteError(new ErrorRecord(error, errorId, Commands.SftpCommand.CategoryOf(error), path));
        }
    }
}
