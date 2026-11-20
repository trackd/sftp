using System.Diagnostics;
using System.Management.Automation;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace sftp.Commands;
/// <summary>Uploads local files with SCP, for servers that do not offer SFTP.</summary>
[Cmdlet(VerbsCommunications.Send, "ScpFile", SupportsShouldProcess = true, DefaultParameterSetName = PathSet)]
[OutputType(typeof(SftpTransferResult))]
public sealed class SendScpFileCommand : SftpCommand {
    /// <inheritdoc/>
    protected override IEnumerable<string> DrivePaths => Destination is null ? [] : [Destination];

    /// <summary>Local files to upload. Wildcards are supported.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PathSet, ValueFromPipeline = true)]
    [SupportsWildcards]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = [];

    /// <summary>Local files to upload, used exactly as typed. Accepts Get-ChildItem output.</summary>
    [Parameter(Mandatory = true, ParameterSetName = LiteralPathSet, ValueFromPipelineByPropertyName = true)]
    [Alias("PSPath", "FullName", "LocalPath")]
    [ValidateNotNullOrEmpty]
    public string[] LiteralPath { get; set; } = [];

    /// <summary>The name of the session to use; filled in from the SessionName of piped files and results.</summary>
    [Parameter(ParameterSetName = LiteralPathSet, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string? SessionName { get; set; }

    /// <inheritdoc/>
    protected override string? PipedSessionName => SessionName;

    /// <summary>
    /// Where to upload: an existing folder (the files keep their names), or a new file name for a single
    /// file. A path ending with / is always a folder. Defaults to the current folder on an Sftp drive of the same session, otherwise the home folder; ~ means the home folder. SCP always overwrites existing files.
    /// </summary>
    [Parameter(Position = 1)]
    [ArgumentCompleter(typeof(RemotePathCompleter))]
    [ValidateNotNullOrEmpty]
    public string? Destination { get; set; }

    /// <inheritdoc/>
    protected override void ProcessItems() {
        bool literal = ParameterSetName == LiteralPathSet;
        foreach (string source in LocalFiles(literal ? LiteralPath : Path, literal)) {
            var timer = Stopwatch.StartNew();
            var info = new FileInfo(source);
            string destination = RemoteTarget(Destination, info.Name);
            try {
                if (!info.Exists) {
                    throw new FileNotFoundException($"File not found: {source}", source);
                }

                if (!ShouldProcess($"{source} -> {destination}", "Upload (SCP)")) {
                    continue;
                }

                ScpClient client = Current.GetScpClient();
                client.Upload(info, destination);
                WriteObject(Result("Upload", "Scp", source, destination, info.Length, null, timer.Elapsed));
            }
            catch (Exception error) when (error is SshException or IOException or ArgumentException) {
                WriteObject(Result("Upload", "Scp", source, destination, 0, error.Message, timer.Elapsed));
                WriteItemError(error, "UploadFailed", source);
            }
        }
    }
}
