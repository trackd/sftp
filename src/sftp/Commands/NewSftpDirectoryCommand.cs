using System.Management.Automation;
using Renci.SshNet.Common;

namespace sftp.Commands;
/// <summary>Creates directories on the server.</summary>
[Cmdlet(VerbsCommon.New, "SftpDirectory", SupportsShouldProcess = true)]
[OutputType(typeof(SftpItem))]
public sealed class NewSftpDirectoryCommand : SftpCommand {
    /// <summary>Remote directories to create.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
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
                    CreateWithParents(full);
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

    private void CreateWithParents(string path) {
        string current = path.StartsWith('/') ? "" : ".";
        foreach (string segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries)) {
            current = current.Length == 0 ? "/" + segment : RemotePath.Combine(current, segment);
            if (!Client.Exists(current)) {
                Client.CreateDirectory(current);
            }
            else if (!Client.GetAttributes(current).IsDirectory) {
                throw new IOException($"{current} exists and is not a directory.");
            }
        }
    }
}
