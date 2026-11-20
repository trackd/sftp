using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace sftp;
/// <summary>Completes -Session and -Name with the names of the open sessions.</summary>
internal sealed class SessionNameCompleter : IArgumentCompleter {
    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName, string parameterName, string wordToComplete, CommandAst commandAst, IDictionary fakeBoundParameters) {
        string word = wordToComplete.Trim('\'', '"');
        foreach (SftpSession session in SessionRegistry.Current.Sessions) {
            if (session.Name.StartsWith(word, StringComparison.OrdinalIgnoreCase)) {
                string text = session.Name.IndexOfAny([' ', '(', ')', '@']) >= 0 ? $"'{session.Name}'" : session.Name;
                yield return new CompletionResult(text, session.Name, CompletionResultType.ParameterValue, $"{session.UserName}@{session.HostName}:{session.Port}");
            }
        }
    }
}
