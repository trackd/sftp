using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace sftp;
/// <summary>Completes -KeyFile with the private keys in ~/.ssh, then with ordinary file names.</summary>
internal sealed class KeyFileCompleter : IArgumentCompleter {
    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName, string parameterName, string wordToComplete, CommandAst commandAst, IDictionary fakeBoundParameters) {
        string word = wordToComplete.Trim('\'', '"');
        List<CompletionResult> results = [];
        foreach (string key in KeyFiles.PrivateKeysIn(KeyFiles.SshFolder)) {
            string shortPath = "~/.ssh/" + Path.GetFileName(key);
            bool matches = word.Length == 0 ||
                shortPath.StartsWith(word, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(key).StartsWith(word, StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith(word, StringComparison.OrdinalIgnoreCase);
            if (matches) {
                results.Add(new CompletionResult(Quote(shortPath), shortPath, CompletionResultType.ParameterValue, key));
            }
        }

        if (word.Length > 0) {
            results.AddRange(CompletionCompleters.CompleteFilename(wordToComplete));
        }

        return results;
    }

    private static string Quote(string text) => text.Contains(' ', StringComparison.Ordinal) ? $"'{text}'" : text;
}
