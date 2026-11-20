using System.Management.Automation;
using Renci.SshNet.Common;

namespace sftp.Commands;
/// <summary>Writes text to a file on the server, replacing it or adding to the end.</summary>
[Cmdlet(VerbsCommon.Set, "SftpContent", SupportsShouldProcess = true)]
[OutputType(typeof(SftpItem))]
public sealed class SetSftpContentCommand : SftpCommand {
    /// <inheritdoc/>
    protected override IEnumerable<string> DrivePaths => [Path];

    /// <summary>The remote file to write. It is created if it does not exist.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ArgumentCompleter(typeof(RemotePathCompleter))]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = "";

    /// <summary>The text to write: each value becomes one line. Accepts pipeline input.</summary>
    [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = true)]
    [AllowEmptyString]
    [AllowNull]
    public object?[] Value { get; set; } = [];

    /// <summary>Add the text to the end of the file instead of replacing it.</summary>
    [Parameter]
    public SwitchParameter Append { get; set; }

    /// <summary>Do not end the text with a line break.</summary>
    [Parameter]
    public SwitchParameter NoNewline { get; set; }

    /// <summary>The text encoding to write. The default is UTF-8 without a byte order mark.</summary>
    [Parameter]
    [ValidateSet("utf8", "utf8BOM", "ascii", "unicode", "bigendianunicode", "utf32", "latin1")]
    public string Encoding { get; set; } = "utf8";

    /// <summary>Return the written file.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    private readonly List<string> _lines = [];
    private bool _received;

    /// <inheritdoc/>
    protected override void ProcessItems() {
        _received = true;
        foreach (object? value in Value) {
            _lines.Add(value is PSObject wrapped ? wrapped.BaseObject?.ToString() ?? "" : value?.ToString() ?? "");
        }
    }

    /// <inheritdoc/>
    protected override void EndProcessing() {
        FlushVerbose();
        if (!_received) {
            return;
        }

        string path = RemoteFullPath(Path);
        if (!ShouldProcess(path, Append ? "Append text" : "Write text")) {
            return;
        }

        // Remote servers are almost always Unix, so lines end with \n.
        string text = string.Join('\n', _lines) + (NoNewline || _lines.Count == 0 ? "" : "\n");
        try {
            byte[] bytes = TextEncoding.From(Encoding).GetBytes(text);
            byte[] preamble = Append ? [] : TextEncoding.From(Encoding).GetPreamble();
            using (Stream stream = Append ? Client.Open(path, FileMode.Append, FileAccess.Write) : Client.Open(path, FileMode.Create, FileAccess.Write)) {
                stream.Write(preamble);
                stream.Write(bytes);
            }

            if (PassThru) {
                WriteObject(ToItem(Client.Get(path)));
            }
        }
        catch (Exception error) when (error is SshException or IOException) {
            WriteItemError(error, "WriteFailed", path);
        }
    }
}
