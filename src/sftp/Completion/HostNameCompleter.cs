using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace sftp;
/// <summary>Completes -HostName with the Host aliases in ~/.ssh/config and the hosts in ~/.ssh/known_hosts.</summary>
internal sealed class HostNameCompleter : IArgumentCompleter {
    public IEnumerable<CompletionResult> CompleteArgument(
        string commandName, string parameterName, string wordToComplete, CommandAst commandAst, IDictionary fakeBoundParameters) {
        string word = wordToComplete.Trim('\'', '"');
        // Keep a typed user@ prefix and complete the host after it.
        int at = word.LastIndexOf('@');
        string user = at >= 0 ? word[..(at + 1)] : "";
        string host = at >= 0 ? word[(at + 1)..] : word;
        string folder = KeyFiles.SshFolder;
        IEnumerable<string> fromConfig = SafeList(() => SshConfig.HostAliases(Path.Join(folder, "config"), folder));
        IEnumerable<string> fromKnownHosts = SafeList(() => KnownHostNames(Path.Join(folder, "known_hosts")));

        return fromConfig.Select(name => (name, source: "~/.ssh/config"))
            .Concat(fromKnownHosts.Select(name => (name, source: "~/.ssh/known_hosts")))
            .Where(entry => entry.name.StartsWith(host, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(entry => entry.name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new CompletionResult(user + entry.name, entry.name, CompletionResultType.ParameterValue, $"{entry.name} ({entry.source})"))
            .ToList();
    }

    /// <summary>Plain host names on port 22 from known_hosts (hashed and [host]:port entries are skipped).</summary>
    private static IEnumerable<string> KnownHostNames(string file) {
        if (!File.Exists(file)) {
            return [];
        }

        return KnownHosts.Parse(File.ReadLines(file))
            .SelectMany(entry => entry.Patterns)
            .Where(pattern => pattern.Length > 0 && pattern[0] is not ('|' or '[' or '!') && pattern.IndexOfAny(['*', '?']) < 0);
    }

    private static List<string> SafeList(Func<IEnumerable<string>> source) {
        try {
            return [.. source()];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            return [];
        }
    }
}
