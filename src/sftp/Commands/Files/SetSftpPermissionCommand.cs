using System.Management.Automation;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp.Commands;
/// <summary>Changes the Unix permissions of files and folders on the server, like chmod.</summary>
[Cmdlet(VerbsCommon.Set, "SftpPermission", SupportsShouldProcess = true, DefaultParameterSetName = PathSet)]
[OutputType(typeof(SftpPermissionInfo))]
public sealed class SetSftpPermissionCommand : SftpCommand {
    /// <inheritdoc/>
    protected override IEnumerable<string> DrivePaths => ParameterSetName == LiteralPathSet ? LiteralPath : Path;

    /// <summary>Remote paths. Wildcards are supported.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PathSet, ValueFromPipeline = true)]
    [SupportsWildcards]
    [ArgumentCompleter(typeof(RemotePathCompleter))]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = [];

    /// <summary>Remote paths, used exactly as typed. Accepts Get-SftpFile output.</summary>
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

    /// <summary>The new permissions: octal like 0644 or 755, or symbolic like u+x, go-w or u=rw,go=r.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string Mode { get; set; } = "";

    /// <summary>Also change everything inside the folders.</summary>
    [Parameter]
    public SwitchParameter Recurse { get; set; }

    /// <summary>Return the new permissions of each changed item.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <inheritdoc/>
    protected override void BeginProcessing() {
        base.BeginProcessing();
        try {
            _ = PermissionMode.Apply(Mode, 0, false);
        }
        catch (FormatException error) {
            ThrowTerminatingError(new ErrorRecord(error, "InvalidMode", ErrorCategory.InvalidArgument, Mode));
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
                WriteItemError(error, "SetPermissionFailed", path);
                continue;
            }

            foreach (ISftpFile item in items) {
                Change(item.FullName);
                if (Recurse && item.IsDirectory && !item.IsSymbolicLink) {
                    ChangeTree(item.FullName);
                }
            }
        }
    }

    private void ChangeTree(string folder) {
        List<ISftpFile> children;
        try {
            children = [.. List(folder)];
        }
        catch (Exception error) when (error is SshException or IOException) {
            WriteItemError(error, "SetPermissionFailed", folder);

            return;
        }

        foreach (ISftpFile child in children) {
            // Like chmod -R: links are not followed.
            if (child.IsSymbolicLink) {
                continue;
            }

            Change(child.FullName);
            if (child.IsDirectory) {
                ChangeTree(child.FullName);
            }
        }
    }

    private void Change(string fullName) {
        try {
            SftpFileAttributes attributes = Client.GetAttributes(fullName);
            int current = PermissionMode.From(attributes);
            int wanted = PermissionMode.Apply(Mode, current, attributes.IsDirectory);
            string change = $"{PermissionMode.ToOctal(current)} -> {PermissionMode.ToOctal(wanted)}";
            if (wanted != current && ShouldProcess($"{fullName} ({change})", "Set permissions")) {
                PermissionMode.Set(attributes, wanted);
                Client.SetAttributes(fullName, attributes);
                WriteVerbose($"{fullName}: {change}");
            }

            if (PassThru) {
                WriteObject(GetSftpPermissionCommand.Describe(fullName, wanted == current ? attributes : Client.GetAttributes(fullName), Current.Name));
            }
        }
        catch (Exception error) when (error is SshException or IOException) {
            WriteItemError(error, "SetPermissionFailed", fullName);
        }
    }
}
