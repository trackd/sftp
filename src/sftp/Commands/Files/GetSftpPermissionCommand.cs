using System.Management.Automation;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp.Commands;
/// <summary>Shows the Unix permissions of files and folders on the server.</summary>
[Cmdlet(VerbsCommon.Get, "SftpPermission", DefaultParameterSetName = PathSet)]
[OutputType(typeof(SftpPermissionInfo))]
public sealed class GetSftpPermissionCommand : SftpCommand {
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

    /// <inheritdoc/>
    protected override void ProcessItems() {
        bool literal = ParameterSetName == LiteralPathSet;
        foreach (string path in literal ? LiteralPath : Path) {
            try {
                foreach (ISftpFile item in RemoteItems(path, literal)) {
                    WriteObject(Describe(item.FullName, item.Attributes));
                }
            }
            catch (Exception error) when (error is SshException or IOException or ItemNotFoundException) {
                WriteItemError(error, "GetPermissionFailed", path);
            }
        }
    }

    /// <summary>Describes the permissions of a remote item.</summary>
    internal static SftpPermissionInfo Describe(string fullName, SftpFileAttributes attributes, string sessionName) {
        int bits = PermissionMode.From(attributes);

        return new SftpPermissionInfo(
            fullName, PermissionMode.ToSymbolic(bits, attributes.IsDirectory), PermissionMode.ToOctal(bits), bits, attributes.UserId, attributes.GroupId, attributes.IsDirectory) {
            SessionName = sessionName,
        };
    }

    private SftpPermissionInfo Describe(string fullName, SftpFileAttributes attributes) => Describe(fullName, attributes, Current.Name);
}
