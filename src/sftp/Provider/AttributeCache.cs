using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace sftp;
/// <summary>
/// Remembers item attributes for a moment. PowerShell asks whether an item exists and whether it is a folder
/// several times for each command; without the cache each question is a round trip to the server.
/// </summary>
internal sealed class AttributeCache {
    internal static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(2);
    private readonly Lock _sync = new();
    private readonly Dictionary<string, (SftpFileAttributes? Attributes, long Expires)> _entries = new(StringComparer.Ordinal);

    /// <summary>The attributes of a path, or null when it does not exist.</summary>
    public SftpFileAttributes? Get(SftpSession session, string remotePath) {
        long now = Environment.TickCount64;
        lock (_sync) {
            if (_entries.TryGetValue(remotePath, out var entry) && entry.Expires > now) {
                return entry.Attributes;
            }
        }

        SftpFileAttributes? attributes;
        try {
            attributes = session.Client.GetAttributes(remotePath);
        }
        catch (SftpPathNotFoundException) {
            attributes = null;
        }

        Set(remotePath, attributes);

        return attributes;
    }

    public void Set(string remotePath, SftpFileAttributes? attributes) {
        lock (_sync) {
            if (_entries.Count > 10_000) {
                _entries.Clear();
            }

            _entries[remotePath] = (attributes, Environment.TickCount64 + (long)Lifetime.TotalMilliseconds);
        }
    }

    /// <summary>Forgets everything; called after any change on the server.</summary>
    public void Clear() {
        lock (_sync) {
            _entries.Clear();
        }
    }
}
