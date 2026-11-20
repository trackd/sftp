using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Language;
using Renci.SshNet.Sftp;

namespace sftp;
/// <summary>
/// Completes remote paths by listing the folder being typed, on the session given with -Session (or the
/// current session). Folders end with / so Tab can continue into them.
/// </summary>
internal sealed class RemotePathCompleter : IArgumentCompleter {
    private const int MaxResults = 500;

    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName, string parameterName, string wordToComplete, CommandAst commandAst, IDictionary fakeBoundParameters) {
        SftpSession? session = SessionFrom(fakeBoundParameters);
        if (session is null || !session.IsConnected) {
            return [];
        }

        string word = wordToComplete.Trim('\'', '"');
        int slash = word.LastIndexOf('/');
        string typedFolder = slash >= 0 ? word[..(slash + 1)] : (word == "~" ? "~/" : "");
        string prefix = slash >= 0 ? word[(slash + 1)..] : (word == "~" ? "" : word);
        string folder = session.FullPath(typedFolder.Length == 0 ? "." : typedFolder);
        List<CompletionResult> results = [];
        try {
            foreach (ISftpFile item in session.Client.ListDirectory(folder).OrderBy(item => !item.IsDirectory).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)) {
                if (item.Name is "." or ".." || !item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string text = typedFolder + item.Name + (item.IsDirectory ? "/" : "");
                results.Add(new CompletionResult(Quote(text), item.Name + (item.IsDirectory ? "/" : ""), CompletionResultType.ProviderItem, item.FullName));
                if (results.Count >= MaxResults) {
                    break;
                }
            }
        }
        catch (Exception error) when (error is Renci.SshNet.Common.SshException or IOException or InvalidOperationException) {
            return [];
        }

        return results;
    }

    private static SftpSession? SessionFrom(IDictionary fakeBoundParameters) {
        object? value = fakeBoundParameters.Contains("Session") ? fakeBoundParameters["Session"] : null;
        if (value is PSObject wrapped) {
            value = wrapped.BaseObject;
        }

        return value switch {
            SftpSession session => session,
            string name => SessionRegistry.Current.Find(name),
            _ => SessionRegistry.Current.Default,
        };
    }

    private static string Quote(string text) =>
        text.IndexOfAny([' ', '\'', '$', '`', '(', ')', '{', '}', ';', ',', '&']) >= 0 ? "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'" : text;
}
