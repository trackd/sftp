using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace sftp;
/// <summary>One line of an OpenSSH known_hosts file.</summary>
internal sealed record KnownHostEntry(string[] Patterns, string KeyType, byte[] Key, bool Revoked) {
    /// <summary>True when the entry applies to the host ("name" or "[name]:port").</summary>
    public bool Matches(string hostField) {
        bool matched = false;
        foreach (string pattern in Patterns) {
            bool negated = pattern.StartsWith('!');
            string body = negated ? pattern[1..] : pattern;
            if (!KnownHosts.PatternMatches(body, hostField)) {
                continue;
            }

            if (negated) {
                return false;
            }

            matched = true;
        }

        return matched;
    }
}

/// <summary>Reads OpenSSH known_hosts files: plain, [host]:port, wildcard and hashed host names.</summary>
internal static class KnownHosts {
    /// <summary>The host as written in known_hosts: the name for port 22, "[name]:port" otherwise.</summary>
    public static string HostField(string host, int port) => port == 22 ? host : $"[{host}]:{port}";

    public static IEnumerable<KnownHostEntry> Parse(IEnumerable<string> lines) {
        foreach (string raw in lines) {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) {
                continue;
            }

            string[] fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            bool revoked = false;
            int index = 0;
            if (fields[0].StartsWith('@')) {
                // Certificate authorities are not supported; revoked keys are always refused.
                if (fields[0] != "@revoked") {
                    continue;
                }

                revoked = true;
                index = 1;
            }

            if (fields.Length < index + 3) {
                continue;
            }

            byte[] key;
            try {
                key = Convert.FromBase64String(fields[index + 2]);
            }
            catch (FormatException) {
                continue;
            }

            yield return new KnownHostEntry(fields[index].Split(','), fields[index + 1], key, revoked);
        }
    }

    public static bool PatternMatches(string pattern, string hostField) {
        if (pattern.StartsWith("|1|", StringComparison.Ordinal)) {
            string[] parts = pattern.Split('|');
            if (parts.Length != 4) {
                return false;
            }

            try {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expected = Convert.FromBase64String(parts[3]);
                // OpenSSH hashes host names with HMAC-SHA1; this is its file format, not a security choice.
#pragma warning disable CA5350
                byte[] actual = HMACSHA1.HashData(salt, Encoding.UTF8.GetBytes(hostField));
#pragma warning restore CA5350

                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException) {
                return false;
            }
        }

        if (pattern.Contains('*', StringComparison.Ordinal) || pattern.Contains('?', StringComparison.Ordinal)) {
            string regex = "^" + Regex.Escape(pattern).Replace(@"\*", ".*", StringComparison.Ordinal).Replace(@"\?", ".", StringComparison.Ordinal) + "$";

            return Regex.IsMatch(hostField, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }

        return string.Equals(pattern, hostField, StringComparison.OrdinalIgnoreCase);
    }
}
