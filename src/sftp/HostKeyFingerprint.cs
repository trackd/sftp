using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Renci.SshNet.Common;

namespace sftp;
/// <summary>An expected host key fingerprint, parsed from the formats used by OpenSSH, WinSCP and PuTTY.</summary>
internal sealed partial class HostKeyFingerprint {
    private HostKeyFingerprint(string value, bool isMd5, string original, string? keyType) {
        Value = value;
        IsMd5 = isMd5;
        Original = original;
        KeyType = keyType;
    }

    /// <summary>The key type named before the hash (for example ssh-rsa), if any.</summary>
    public string? KeyType { get; }

    /// <summary>The normalized value: unpadded Base64 for SHA-256, lowercase hex without colons for MD5.</summary>
    public string Value { get; }

    /// <summary>True for a legacy MD5 fingerprint.</summary>
    public bool IsMd5 { get; }

    /// <summary>The text as supplied.</summary>
    public string Original { get; }

    /// <summary>
    /// Parses "ssh-ed25519 255 BASE64", "SHA256:BASE64", "BASE64", "ssh-rsa 2048 aa:bb:..", "MD5:aa:bb:.." or "aa:bb:..".
    /// The algorithm and key size are informational: the hash identifies the key.
    /// </summary>
    public static HostKeyFingerprint Parse(string text) {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        string[] tokens = text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        string token = tokens[^1];
        string? keyType = tokens.Length > 1 && tokens[0].Contains('-', StringComparison.Ordinal) ? HostKeyVerifier.KeyTypeOfAlgorithm(tokens[0]) : null;
        if (token.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase)) {
            token = token[7..];
        }
        else if (token.StartsWith("MD5:", StringComparison.OrdinalIgnoreCase)) {
            token = token[4..];
        }

        if (Md5Pattern().IsMatch(token)) {
            return new HostKeyFingerprint(token.Replace(":", "", StringComparison.Ordinal).ToLowerInvariant(), true, text, keyType);
        }

        string base64 = token.TrimEnd('=');
        if (!Sha256Pattern().IsMatch(base64)) {
            throw new FormatException(
                $"'{text}' is not a host key fingerprint. Use the SHA-256 form, e.g. 'ssh-ed25519 255 <base64>' or 'SHA256:<base64>' (see Get-SshHostKey).");
        }

        return new HostKeyFingerprint(base64, false, text, keyType);
    }

    /// <summary>True when the presented host key has this fingerprint.</summary>
    public bool Matches(SshHostKey key) => IsMd5 ?
        string.Equals(Value, key.Md5.Replace(":", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase) :
        string.Equals(Value, key.Sha256, StringComparison.Ordinal);

    /// <summary>Builds the host key record from what the server presented.</summary>
    public static SshHostKey Describe(string hostName, int port, HostKeyEventArgs args) {
        string sha256 = Convert.ToBase64String(SHA256.HashData(args.HostKey)).TrimEnd('=');
        // SSH.NET's FingerPrint is the MD5 hash; hashing ourselves would need a weak-hash suppression.
        string md5 = string.Join(':', args.FingerPrint.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));

        return new SshHostKey(hostName, port, args.HostKeyName, args.KeyLength, sha256, md5, HostKeyVerifier.KeyTypeOf(args.HostKey));
    }

    [GeneratedRegex("^([0-9a-fA-F]{2}:){15}[0-9a-fA-F]{2}$")]
    private static partial Regex Md5Pattern();

    // A SHA-256 hash is 32 bytes: 43 Base64 characters without padding.
    [GeneratedRegex("^[A-Za-z0-9+/]{43}$")]
    private static partial Regex Sha256Pattern();
}
