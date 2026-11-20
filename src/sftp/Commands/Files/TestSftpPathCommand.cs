using System.Management.Automation;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp.Commands;
/// <summary>Tests whether files or folders exist on the server.</summary>
[Cmdlet(VerbsDiagnostic.Test, "SftpPath", DefaultParameterSetName = PathSet)]
[OutputType(typeof(bool))]
public sealed class TestSftpPathCommand : SftpCommand {
    /// <inheritdoc/>
    protected override IEnumerable<string> DrivePaths => ParameterSetName == LiteralPathSet ? LiteralPath : Path;

    /// <summary>Remote paths to test. Wildcards are supported: true when anything matches.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PathSet, ValueFromPipeline = true)]
    [SupportsWildcards]
    [ArgumentCompleter(typeof(RemotePathCompleter))]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = [];

    /// <summary>Remote paths to test, used exactly as typed. Accepts Get-SftpFile output.</summary>
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

    /// <summary>Only count files (Leaf) or folders (Container).</summary>
    [Parameter]
    public SftpPathType PathType { get; set; } = SftpPathType.Any;

    /// <inheritdoc/>
    protected override void ProcessItems() {
        bool literal = ParameterSetName == LiteralPathSet;
        foreach (string path in literal ? LiteralPath : Path) {
            bool found;
            try {
                found = RemoteItems(path, literal).Any(Wanted);
            }
            catch (Exception error) when (error is SftpPathNotFoundException or ItemNotFoundException) {
                found = false;
            }
            catch (Exception error) when (error is SshException or IOException) {
                WriteItemError(error, "TestFailed", path);
                continue;
            }

            WriteObject(found);
        }
    }

    private bool Wanted(ISftpFile item) => PathType switch {
        SftpPathType.Leaf => !item.IsDirectory,
        SftpPathType.Container => item.IsDirectory,
        _ => true,
    };
}
