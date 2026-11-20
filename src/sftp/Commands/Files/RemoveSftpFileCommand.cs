using System.Management.Automation;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp.Commands;
/// <summary>Deletes files or directories on the server.</summary>
[Cmdlet(VerbsCommon.Remove, "SftpFile", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium, DefaultParameterSetName = PathSet)]
[OutputType(typeof(void))]
public sealed class RemoveSftpFileCommand : SftpCommand {
    /// <inheritdoc/>
    protected override IEnumerable<string> DrivePaths => ParameterSetName == LiteralPathSet ? LiteralPath : Path;

    /// <summary>Remote files or folders to delete. Wildcards are supported.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PathSet, ValueFromPipeline = true)]
    [SupportsWildcards]
    [ArgumentCompleter(typeof(RemotePathCompleter))]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = [];

    /// <summary>Remote files or folders to delete, used exactly as typed. Accepts Get-SftpFile and transfer results.</summary>
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

    /// <summary>Delete directories and everything in them.</summary>
    [Parameter]
    public SwitchParameter Recurse { get; set; }

    /// <inheritdoc/>
    protected override void ProcessItems() {
        bool literal = ParameterSetName == LiteralPathSet;
        foreach (string path in literal ? LiteralPath : Path) {
            try {
                foreach (ISftpFile item in RemoteItems(path, literal)) {
                    if (item.IsDirectory && !item.IsSymbolicLink) {
                        if (ShouldProcess(item.FullName, Recurse ? "Remove directory and contents" : "Remove empty directory")) {
                            RemoveDirectory(item.FullName);
                        }
                    }
                    else if (ShouldProcess(item.FullName, "Remove file")) {
                        Client.DeleteFile(item.FullName);
                    }
                }
            }
            catch (Exception error) when (error is SshException or IOException or ItemNotFoundException) {
                WriteItemError(error, "RemoveFailed", path);
            }
        }
    }

    private void RemoveDirectory(string directory) {
        if (Recurse) {
            foreach (ISftpFile child in List(directory).ToList()) {
                if (child.IsDirectory && !child.IsSymbolicLink) {
                    RemoveDirectory(child.FullName);
                }
                else {
                    Client.DeleteFile(child.FullName);
                }
            }
        }

        Client.DeleteDirectory(directory);
    }
}
