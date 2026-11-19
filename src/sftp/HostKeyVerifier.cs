using System.Buffers.Binary;
using System.Text;

namespace sftp;
/// <summary>Decides whether to trust the host key a server presents.</summary>
internal abstract class HostKeyVerifier {
    /// <summary>How the key was checked, shown on the session.</summary>
    public abstract string Mode { get; }

    /// <summary>Key types (for example ssh-ed25519) to ask the server for. Empty means any.</summary>
    public virtual IReadOnlyCollection<string> KeyTypes => [];

    /// <summary>Returns true when the key is trusted.</summary>
    public abstract bool Verify(SshHostKey key, byte[] blob);

    /// <summary>Explains a rejected key.</summary>
    public abstract string MismatchMessage(SshHostKey key);

    /// <summary>Saves anything learned during a successful connection; returns a message for the user, if any.</summary>
    public virtual string? Commit() => null;

    /// <summary>Reads the key type from the start of an SSH public key blob.</summary>
    public static string KeyTypeOf(ReadOnlySpan<byte> blob) {
        if (blob.Length < 4) {
            return "";
        }

        int length = (int)BinaryPrimitives.ReadUInt32BigEndian(blob);

        return length <= blob.Length - 4 ? Encoding.ASCII.GetString(blob.Slice(4, length)) : "";
    }

    /// <summary>Maps a host key algorithm (rsa-sha2-512, ssh-ed25519-cert-v01@openssh.com, ...) to its key type.</summary>
    public static string KeyTypeOfAlgorithm(string algorithm) {
        const string certificate = "-cert-v01@openssh.com";
        string type = algorithm.EndsWith(certificate, StringComparison.Ordinal) ? algorithm[..^certificate.Length] : algorithm;

        return type.StartsWith("rsa-sha2-", StringComparison.Ordinal) ? "ssh-rsa" : type;
    }

    /// <summary>
    /// Removes host key algorithms for other key types, so a server with several keys presents the one
    /// we can check instead of one we would wrongly reject.
    /// </summary>
    public static void Restrict<TValue>(IDictionary<string, TValue> algorithms, IReadOnlyCollection<string> keyTypes) {
        if (keyTypes.Count == 0) {
            return;
        }

        List<string> remove = [.. algorithms.Keys.Where(algorithm => !keyTypes.Contains(KeyTypeOfAlgorithm(algorithm)))];
        if (remove.Count == algorithms.Count) {
            return;
        }

        foreach (string algorithm in remove) {
            algorithms.Remove(algorithm);
        }
    }
}

/// <summary>Trusts keys matching pinned fingerprints.</summary>
internal sealed class FingerprintVerifier(IReadOnlyList<HostKeyFingerprint> fingerprints) : HostKeyVerifier {
    public override string Mode => "Fingerprint";

    // Restrict only when every fingerprint names its key type; a bare hash could be any type.
    public override IReadOnlyCollection<string> KeyTypes =>
        fingerprints.All(f => f.KeyType is not null) ? [.. fingerprints.Select(f => f.KeyType!).Distinct(StringComparer.Ordinal)] : [];

    public override bool Verify(SshHostKey key, byte[] blob) => fingerprints.Any(f => f.Matches(key));

    public override string MismatchMessage(SshHostKey key) =>
        $"Host key mismatch for {key.HostName}:{key.Port}. The server presented {key.Fingerprint}, which is not in -HostKeyFingerprint. " +
        "If the server's key changed, confirm the new fingerprint with the server's owner before using it.";
}

/// <summary>Trusts keys listed in an OpenSSH known_hosts file, and adds unknown hosts on first use.</summary>
internal sealed class KnownHostsVerifier : HostKeyVerifier {
    private readonly string _path;
    private readonly string _hostField;
    private readonly List<KnownHostEntry> _entries;
    private string? _pendingLine;

    public KnownHostsVerifier(string path, string host, int port) {
        _path = path;
        _hostField = KnownHosts.HostField(host, port);
        _entries = File.Exists(path) ? [.. KnownHosts.Parse(File.ReadLines(path)).Where(e => e.Matches(_hostField))] : [];
    }

    public override string Mode => _pendingLine is null ? "KnownHosts" : "KnownHostsAdded";

    public override IReadOnlyCollection<string> KeyTypes =>
        [.. _entries.Where(e => !e.Revoked).Select(e => e.KeyType).Distinct(StringComparer.Ordinal)];

    public override bool Verify(SshHostKey key, byte[] blob) {
        if (_entries.Any(e => e.Revoked && e.Key.AsSpan().SequenceEqual(blob))) {
            return false;
        }

        List<KnownHostEntry> trusted = [.. _entries.Where(e => !e.Revoked)];
        if (trusted.Count > 0) {
            return trusted.Any(e => e.Key.AsSpan().SequenceEqual(blob));
        }

        _pendingLine = $"{_hostField} {KeyTypeOf(blob)} {Convert.ToBase64String(blob)}";

        return true;
    }

    public override string MismatchMessage(SshHostKey key) =>
        $"Host key mismatch for {key.HostName}:{key.Port}. The server presented {key.Fingerprint}, which does not match {_path}. " +
        "If the server's key changed, confirm the new fingerprint with the server's owner, then remove the old entry from the file.";

    public override string? Commit() {
        if (_pendingLine is null) {
            return null;
        }

        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        bool needsNewLine = File.Exists(_path) && new FileInfo(_path).Length > 0 && !File.ReadAllText(_path).EndsWith('\n');
        File.AppendAllText(_path, (needsNewLine ? "\n" : "") + _pendingLine + "\n");

        return $"Added {_hostField} to {_path}.";
    }
}

/// <summary>Trusts any key.</summary>
internal sealed class SkipVerifier : HostKeyVerifier {
    public override string Mode => "Skipped";

    public override bool Verify(SshHostKey key, byte[] blob) => true;

    public override string MismatchMessage(SshHostKey key) => "";
}
