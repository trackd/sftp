using System.Management.Automation;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp.Commands;
/// <summary>Moves or renames a file or directory on the server.</summary>
[Cmdlet(VerbsCommon.Move, "SftpFile", SupportsShouldProcess = true, DefaultParameterSetName = PathSet)]
[OutputType(typeof(SftpItem))]
public sealed class MoveSftpFileCommand : SftpCommand {
    /// <summary>Remote files or folders to move. Wildcards are supported.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PathSet, ValueFromPipeline = true)]
    [SupportsWildcards]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = [];

    /// <summary>Remote files or folders to move, used exactly as typed. Accepts Get-SftpFile and transfer results.</summary>
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

    /// <summary>The new path, or an existing directory to move the items into.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string Destination { get; set; } = "";

    /// <summary>Replace an existing file at the destination.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <summary>Return the moved items.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    /// <inheritdoc/>
    protected override void ProcessItems() {
        bool literal = ParameterSetName == LiteralPathSet;
        foreach (string path in literal ? LiteralPath : Path) {
            try {
                foreach (ISftpFile item in RemoteItems(path, literal)) {
                    Move(item.FullName);
                }
            }
            catch (Exception error) when (error is SshException or IOException or ItemNotFoundException) {
                WriteItemError(error, "MoveFailed", path);
            }
        }
    }

    private void Move(string source) {
        string target = RemoteFullPath(Destination);
        if (Destination.EndsWith('/') || IsDirectory(target)) {
            target = RemotePath.Combine(target, RemotePath.GetName(source));
        }

        if (!ShouldProcess($"{source} -> {target}", "Move")) {
            return;
        }

        bool exists = Client.Exists(target);
        if (exists && !Force) {
            throw new IOException($"{target} already exists. Use -Force to replace it.");
        }

        if (exists) {
            try {
                Client.RenameFile(source, target, isPosix: true);
            }
            catch (Exception error) when (error is SshException or NotSupportedException) {
                Client.DeleteFile(target);
                Client.RenameFile(source, target, isPosix: false);
            }
        }
        else {
            Client.RenameFile(source, target, isPosix: false);
        }

        if (PassThru) {
            WriteObject(ToItem(Client.Get(target)));
        }
    }

    private bool IsDirectory(string path) {
        try {
            return Client.GetAttributes(path).IsDirectory;
        }
        catch (SftpPathNotFoundException) {
            return false;
        }
    }
}
