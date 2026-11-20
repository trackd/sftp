using System.Management.Automation;
using Renci.SshNet.Common;

namespace sftp.Commands;
/// <summary>Creates directories on the server.</summary>
[Cmdlet(VerbsCommon.New, "SftpDirectory", SupportsShouldProcess = true)]
[OutputType(typeof(SftpItem))]
public sealed class NewSftpDirectoryCommand : SftpCommand {
    /// <inheritdoc/>
    protected override IEnumerable<string> DrivePaths => Path;

    /// <summary>Remote directories to create.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [ArgumentCompleter(typeof(RemotePathCompleter))]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = [];

    /// <summary>Create missing parent directories, and succeed if the directory already exists.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <inheritdoc/>
    protected override void ProcessItems() {
        foreach (string path in Path) {
            try {
                string full = RemoteFullPath(path);
                if (!ShouldProcess(full, "Create directory")) {
                    continue;
                }

                if (Force) {
                    EnsureRemoteFolder(full);
                }
                else {
                    Client.CreateDirectory(full);
                }

                WriteObject(ToItem(Client.Get(full)));
            }
            catch (Exception error) when (error is SshException or IOException) {
                WriteItemError(error, "CreateDirectoryFailed", path);
            }
        }
    }
}
