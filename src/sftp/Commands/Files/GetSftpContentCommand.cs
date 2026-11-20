using System.Management.Automation;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp.Commands;
/// <summary>Reads text files on the server.</summary>
[Cmdlet(VerbsCommon.Get, "SftpContent", DefaultParameterSetName = PathSet)]
[OutputType(typeof(string))]
public sealed class GetSftpContentCommand : SftpCommand {
    /// <inheritdoc/>
    protected override IEnumerable<string> DrivePaths => ParameterSetName == LiteralPathSet ? LiteralPath : Path;

    /// <summary>Remote files to read. Wildcards are supported.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PathSet, ValueFromPipeline = true)]
    [SupportsWildcards]
    [ArgumentCompleter(typeof(RemotePathCompleter))]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = [];

    /// <summary>Remote files to read, used exactly as typed. Accepts Get-SftpFile output.</summary>
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

    /// <summary>Return the whole file as one string instead of one string per line.</summary>
    [Parameter]
    public SwitchParameter Raw { get; set; }

    /// <summary>The text encoding of the file. The default is UTF-8.</summary>
    [Parameter]
    [ValidateSet("utf8", "utf8BOM", "ascii", "unicode", "bigendianunicode", "utf32", "latin1")]
    public string Encoding { get; set; } = "utf8";

    /// <inheritdoc/>
    protected override void ProcessItems() {
        bool literal = ParameterSetName == LiteralPathSet;
        foreach (string path in literal ? LiteralPath : Path) {
            try {
                foreach (ISftpFile item in RemoteItems(path, literal)) {
                    if (item.IsDirectory) {
                        throw new IOException($"{item.FullName} is a folder.");
                    }

                    Read(item.FullName);
                }
            }
            catch (Exception error) when (error is SshException or IOException or ItemNotFoundException) {
                WriteItemError(error, "ReadFailed", path);
            }
        }
    }

    private void Read(string path) {
        using var reader = new StreamReader(Client.OpenRead(path), TextEncoding.From(Encoding), detectEncodingFromByteOrderMarks: true);
        if (Raw) {
            WriteObject(reader.ReadToEnd());

            return;
        }

        while (reader.ReadLine() is { } line) {
            WriteObject(line);
        }
    }
}
