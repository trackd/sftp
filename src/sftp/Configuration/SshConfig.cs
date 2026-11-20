using System.Text.RegularExpressions;

namespace sftp;
/// <summary>The settings ~/.ssh/config gives one host. Null means "not set".</summary>
internal sealed class SshHostConfig {
    public string? HostName { get; set; }
    public string? User { get; set; }
    public int? Port { get; set; }
    public List<string> IdentityFiles { get; } = [];
    public bool? IdentitiesOnly { get; set; }
    public string? UserKnownHostsFile { get; set; }
    public string? StrictHostKeyChecking { get; set; }
    public int? ConnectTimeout { get; set; }
    public int? ServerAliveInterval { get; set; }
}

/// <summary>
/// Reads OpenSSH client configuration (ssh_config(5)): Host blocks with wildcards and negation, Include,
/// and the first-value-wins rule. Match blocks are skipped; options this module cannot use are ignored.
/// </summary>
internal static class SshConfig {
    private static readonly char[] s_separators = [' ', '\t'];

    /// <summary>Resolves the settings for a host alias as typed on the command line.</summary>
    public static SshHostConfig Resolve(string configFile, string host, string sshFolder) {
        var result = new SshHostConfig();
        if (File.Exists(configFile)) {
            Apply(configFile, host, sshFolder, result, depth: 0);
        }

        return result;
    }

    /// <summary>The concrete host aliases defined in a config file (no wildcards), for completion.</summary>
    public static IEnumerable<string> HostAliases(string configFile, string sshFolder) {
        HashSet<string> aliases = new(StringComparer.OrdinalIgnoreCase);
        CollectAliases(configFile, sshFolder, aliases, depth: 0);

        return aliases;
    }

    private static void Apply(string file, string host, string sshFolder, SshHostConfig result, int depth) {
        // Lines before the first Host block apply to every host.
        bool active = true;
        foreach ((string keyword, string value) in Lines(file)) {
            switch (keyword) {
                case "host":
                    active = HostMatches(value, host);
                    continue;
                case "match":
                    // Match conditions are not evaluated; never apply their options.
                    active = false;
                    continue;
                case "include" when depth < 16:
                    foreach (string included in Expand(value, sshFolder)) {
                        Apply(included, host, sshFolder, result, depth + 1);
                    }

                    continue;
            }

            if (!active) {
                continue;
            }

            switch (keyword) {
                case "hostname":
                    result.HostName ??= value;
                    break;
                case "user":
                    result.User ??= value;
                    break;
                case "port" when int.TryParse(value, out int port):
                    result.Port ??= port;
                    break;
                case "identityfile":
                    result.IdentityFiles.Add(value);
                    break;
                case "identitiesonly":
                    result.IdentitiesOnly ??= IsYes(value);
                    break;
                case "userknownhostsfile":
                    result.UserKnownHostsFile ??= value.Split(s_separators, StringSplitOptions.RemoveEmptyEntries)[0];
                    break;
                case "stricthostkeychecking":
                    result.StrictHostKeyChecking ??= value.ToLowerInvariant();
                    break;
                case "connecttimeout" when int.TryParse(value, out int timeout):
                    result.ConnectTimeout ??= timeout;
                    break;
                case "serveraliveinterval" when int.TryParse(value, out int interval):
                    result.ServerAliveInterval ??= interval;
                    break;
            }
        }
    }

    private static void CollectAliases(string file, string sshFolder, HashSet<string> aliases, int depth) {
        if (!File.Exists(file)) {
            return;
        }

        foreach ((string keyword, string value) in Lines(file)) {
            if (keyword == "host") {
                foreach (string pattern in value.Split(s_separators, StringSplitOptions.RemoveEmptyEntries)) {
                    if (!pattern.StartsWith('!') && pattern.IndexOfAny(['*', '?']) < 0) {
                        aliases.Add(pattern);
                    }
                }
            }
            else if (keyword == "include" && depth < 16) {
                foreach (string included in Expand(value, sshFolder)) {
                    CollectAliases(included, sshFolder, aliases, depth + 1);
                }
            }
        }
    }

    /// <summary>"Host a b !c": matches when any pattern matches and no negated pattern does.</summary>
    internal static bool HostMatches(string patterns, string host) {
        bool matched = false;
        foreach (string pattern in patterns.Split(s_separators, StringSplitOptions.RemoveEmptyEntries)) {
            bool negated = pattern.StartsWith('!');
            string body = negated ? pattern[1..] : pattern;
            string regex = "^" + Regex.Escape(body).Replace(@"\*", ".*", StringComparison.Ordinal).Replace(@"\?", ".", StringComparison.Ordinal) + "$";
            if (!Regex.IsMatch(host, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) {
                continue;
            }

            if (negated) {
                return false;
            }

            matched = true;
        }

        return matched;
    }

    /// <summary>Expands ssh_config path tokens: ~, %d (home), %u (local user), %h (host), %r (remote user), %%.</summary>
    internal static string ExpandTokens(string value, string home, string host, string remoteUser) {
        string expanded = value.StartsWith("~/", StringComparison.Ordinal) || value == "~" ? home + value[1..] : value;

        return Regex.Replace(expanded, "%[%dhur]", match => match.Value[1] switch {
            '%' => "%",
            'd' => home,
            'u' => Environment.UserName,
            'h' => host,
            'r' => remoteUser,
            _ => match.Value,
        }, RegexOptions.None, TimeSpan.FromSeconds(1));
    }

    private static IEnumerable<string> Expand(string patterns, string sshFolder) {
        string home = Path.GetDirectoryName(sshFolder) ?? sshFolder;
        foreach (string raw in patterns.Split(s_separators, StringSplitOptions.RemoveEmptyEntries)) {
            string path = raw.StartsWith("~/", StringComparison.Ordinal) ? Path.Join(home, raw[2..]) :
                Path.IsPathRooted(raw) ? raw : Path.Join(sshFolder, raw);
            string? directory = Path.GetDirectoryName(path);
            string name = Path.GetFileName(path);
            if (directory is null || !Directory.Exists(directory)) {
                continue;
            }

            if (name.IndexOfAny(['*', '?']) < 0) {
                if (File.Exists(path)) {
                    yield return path;
                }

                continue;
            }

            foreach (string file in Directory.EnumerateFiles(directory, name).Order(StringComparer.Ordinal)) {
                yield return file;
            }
        }
    }

    /// <summary>Keyword (lowercase) and value pairs; "Keyword value", "Keyword=value", quotes removed.</summary>
    private static IEnumerable<(string Keyword, string Value)> Lines(string file) {
        foreach (string raw in File.ReadLines(file)) {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) {
                continue;
            }

            Match match = Regex.Match(line, @"^(?<key>[A-Za-z]+)\s*(=\s*|\s+)(?<value>.+)$", RegexOptions.None, TimeSpan.FromSeconds(1));
            if (match.Success) {
                yield return (match.Groups["key"].Value.ToLowerInvariant(), match.Groups["value"].Value.Trim().Trim('"'));
            }
        }
    }

    private static bool IsYes(string value) => value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value.Equals("true", StringComparison.OrdinalIgnoreCase);
}
