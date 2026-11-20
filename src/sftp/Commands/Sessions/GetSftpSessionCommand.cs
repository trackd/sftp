using System.Management.Automation;

namespace sftp.Commands;
/// <summary>Lists the open SFTP sessions.</summary>
[Cmdlet(VerbsCommon.Get, "SftpSession")]
[OutputType(typeof(SftpSession))]
public sealed class GetSftpSessionCommand : PSCmdlet {
    /// <summary>Only sessions whose name matches. Wildcards are supported.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [SupportsWildcards]
    [ArgumentCompleter(typeof(SessionNameCompleter))]
    [ValidateNotNullOrEmpty]
    public string[] Name { get; set; } = ["*"];

    /// <inheritdoc/>
    protected override void ProcessRecord() {
        IReadOnlyList<SftpSession> sessions = SessionRegistry.Current.Sessions;
        foreach (string name in Name) {
            var pattern = new WildcardPattern(name, WildcardOptions.IgnoreCase);
            List<SftpSession> matches = [.. sessions.Where(session => pattern.IsMatch(session.Name))];
            if (matches.Count == 0 && !WildcardPattern.ContainsWildcardCharacters(name)) {
                WriteError(new ErrorRecord(
                    new ItemNotFoundException($"No open session named '{name}'."),
                    "SessionNotFound",
                    ErrorCategory.ObjectNotFound,
                    name));
            }

            WriteObject(matches, enumerateCollection: true);
        }
    }
}
