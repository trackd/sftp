using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Runtime.CompilerServices;

namespace sftp;
/// <summary>
/// The open sessions of each runspace, by name. Files and results only carry the name; the session
/// itself lives here until it is disconnected. Each runspace has its own list, so parallel jobs in one
/// process never see each other's sessions.
/// </summary>
internal sealed class SessionRegistry {
    private static readonly ConditionalWeakTable<Runspace, SessionRegistry> s_registries = [];
    private readonly Lock _sync = new();
    private readonly List<SftpSession> _sessions = [];

    /// <summary>The registry of the runspace running the current command.</summary>
    public static SessionRegistry Current =>
        s_registries.GetValue(Runspace.DefaultRunspace ?? throw new InvalidOperationException("No runspace."), _ => new SessionRegistry());

    /// <summary>Open sessions, oldest first.</summary>
    public IReadOnlyList<SftpSession> Sessions {
        get {
            lock (_sync) {
                return [.. _sessions];
            }
        }
    }

    /// <summary>The most recently opened session that is still open.</summary>
    public SftpSession? Default {
        get {
            lock (_sync) {
                return _sessions.Count > 0 ? _sessions[^1] : null;
            }
        }
    }

    /// <summary>Adds a session under the requested name, or a unique user@host[:port] name.</summary>
    public void Add(SftpSession session, string? name) {
        lock (_sync) {
            string baseName = name ?? (session.Port == 22 ? $"{session.UserName}@{session.HostName}" : $"{session.UserName}@{session.HostName}:{session.Port}");
            if (name is not null && _sessions.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))) {
                throw new PSArgumentException($"A session named '{name}' is already open. Choose another -Name, or disconnect it first.");
            }

            string unique = baseName;
            for (int i = 2; _sessions.Any(s => string.Equals(s.Name, unique, StringComparison.OrdinalIgnoreCase)); i++) {
                unique = $"{baseName} ({i})";
            }

            session.Register(this, unique);
            _sessions.Add(session);
        }
    }

    public void Remove(SftpSession session) {
        lock (_sync) {
            _sessions.Remove(session);
        }
    }

    public SftpSession? Find(string name) {
        lock (_sync) {
            return _sessions.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}

/// <summary>Lets -Session take a session name as well as a session object.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
internal sealed class SessionTransformationAttribute : ArgumentTransformationAttribute {
    public override object? Transform(EngineIntrinsics engineIntrinsics, object? inputData) {
        object? value = inputData is PSObject wrapped ? wrapped.BaseObject : inputData;
        return value switch {
            null or SftpSession => value,
            string name => Resolve(name),
            System.Collections.IEnumerable items => items.Cast<object?>().Select(item => Transform(engineIntrinsics, item)).ToArray(),
            _ => value,
        };
    }

    private static SftpSession Resolve(string name) =>
        SessionRegistry.Current.Find(name) ??
        throw new ArgumentTransformationMetadataException($"No open session named '{name}'. It may have been disconnected; see Get-SftpSession, or connect again with Connect-SftpServer.");
}
