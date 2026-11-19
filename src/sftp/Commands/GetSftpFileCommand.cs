using System.Management.Automation;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp.Commands;
/// <summary>Lists files and directories on the server.</summary>
[Cmdlet(VerbsCommon.Get, "SftpFile", DefaultParameterSetName = PathSet)]
[OutputType(typeof(SftpItem))]
public sealed class GetSftpFileCommand : SftpCommand {
    /// <summary>Folders to list, or files to show. Wildcards are supported. Defaults to the home folder.</summary>
    [Parameter(Position = 0, ParameterSetName = PathSet, ValueFromPipeline = true)]
    [SupportsWildcards]
    [ValidateNotNullOrEmpty]
    public string[] Path { get; set; } = ["."];

    /// <summary>Folders to list, or files to show, used exactly as typed.</summary>
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

    /// <summary>A wildcard pattern that item names must match.</summary>
    [Parameter]
    [SupportsWildcards]
    [ValidateNotNullOrEmpty]
    public string Filter { get; set; } = "*";

    /// <summary>Match -Filter case-sensitively.</summary>
    [Parameter]
    public SwitchParameter CaseSensitive { get; set; }

    /// <summary>Include subdirectories. Symbolic links to directories are not followed.</summary>
    [Parameter]
    public SwitchParameter Recurse { get; set; }

    /// <summary>Return only files.</summary>
    [Parameter]
    public SwitchParameter File { get; set; }

    /// <summary>Return only directories.</summary>
    [Parameter]
    public SwitchParameter Directory { get; set; }

    private WildcardPattern _pattern = null!;

    /// <inheritdoc/>
    protected override void BeginProcessing() {
        base.BeginProcessing();
        _pattern = new WildcardPattern(Filter, CaseSensitive ? WildcardOptions.None : WildcardOptions.IgnoreCase);
    }

    /// <inheritdoc/>
    protected override void ProcessItems() {
        bool literal = ParameterSetName == LiteralPathSet;
        foreach (string path in literal ? LiteralPath : Path) {
            try {
                bool wildcard = !literal && WildcardPattern.ContainsWildcardCharacters(RemotePath.GetName(path));
                foreach (ISftpFile item in RemoteItems(path, literal)) {
                    // A folder named directly is listed; folders matched by a wildcard are returned themselves.
                    if (item.IsDirectory && !wildcard) {
                        ListTree(item.FullName);
                        continue;
                    }

                    if (Wanted(item)) {
                        WriteObject(ToItem(item));
                    }

                    if (wildcard && Recurse && item.IsDirectory && !item.IsSymbolicLink) {
                        ListTree(item.FullName);
                    }
                }
            }
            catch (Exception error) when (error is SshException or IOException or ArgumentException or ItemNotFoundException) {
                WriteItemError(error, "ListFailed", path);
            }
        }
    }

    private void ListTree(string directory) {
        List<string> subdirectories = [];
        foreach (ISftpFile item in List(directory)) {
            if (Wanted(item)) {
                WriteObject(ToItem(item));
            }

            if (Recurse && item.IsDirectory && !item.IsSymbolicLink) {
                subdirectories.Add(item.FullName);
            }
        }

        foreach (string subdirectory in subdirectories) {
            try {
                ListTree(subdirectory);
            }
            catch (Exception error) when (error is SshException or IOException) {
                WriteItemError(error, "ListFailed", subdirectory);
            }
        }
    }

    private bool Wanted(ISftpFile item) =>
        _pattern.IsMatch(item.Name) && (!File || !item.IsDirectory) && (!Directory || item.IsDirectory);
}
