using Renci.SshNet;
using Renci.SshNet.Common;

namespace sftp;
/// <summary>An open, host-key-verified SFTP connection. Create it with Connect-SftpServer and close it with Disconnect-SftpServer.</summary>
public sealed class SftpSession : IDisposable {
    private readonly ConnectionInfo _connection;
    private readonly HostKeyVerifier _verifier;
    private ScpClient? _scp;
    private SessionRegistry? _registry;
    private bool _disposed;

    internal SftpSession(ConnectionInfo connection, HostKeyVerifier verifier) {
        _connection = connection;
        _verifier = verifier;
        HostKeyVerifier.Restrict(connection.HostKeyAlgorithms, verifier.KeyTypes);
        Client = new SftpClient(connection);
        Client.HostKeyReceived += OnHostKeyReceived;
    }

    /// <summary>The session's name, shown on the files and results it produces. Use it with -Session.</summary>
    public string Name { get; private set; } = "";

    /// <summary>The server name.</summary>
    public string HostName => _connection.Host;

    /// <summary>The server port.</summary>
    public int Port => _connection.Port;

    /// <summary>The user name.</summary>
    public string UserName => _connection.Username;

    /// <summary>How the host key was checked: Fingerprint, KnownHosts, KnownHostsAdded or Skipped.</summary>
    public string HostKeyVerification => _verifier.Mode;

    /// <summary>The server's host key, available once connected.</summary>
    public SshHostKey? HostKey { get; private set; }

    /// <summary>True while the SFTP connection is open.</summary>
    public bool IsConnected => !_disposed && Client.IsConnected;

    /// <summary>The remote working directory that relative paths resolve against.</summary>
    public string WorkingDirectory => IsConnected ? Client.WorkingDirectory : "";

    /// <summary>When the session was opened.</summary>
    public DateTime ConnectedAt { get; private set; }

    /// <summary>True for the session commands use when -Session is omitted.</summary>
    public bool IsCurrent => _registry is not null && ReferenceEquals(_registry.Default, this);

    internal SftpClient Client { get; }

    /// <summary>The key the server presented when it did not match, for error reporting.</summary>
    internal SshHostKey? RejectedHostKey { get; private set; }

    internal HostKeyVerifier Verifier => _verifier;

    internal void Register(SessionRegistry registry, string name) {
        _registry = registry;
        Name = name;
    }

    internal void Connect(CancellationToken cancellationToken) {
        Client.ConnectAsync(cancellationToken).GetAwaiter().GetResult();
        ConnectedAt = DateTime.Now;
    }

    /// <summary>Returns an SCP client on a second connection with the same credentials and host key check.</summary>
    internal ScpClient GetScpClient() {
        if (_scp is null) {
            // Shell-quote remote paths: SCP passes them to the server's shell, so unquoted names allow command injection.
            _scp = new ScpClient(_connection, RemotePathTransformation.ShellQuote);
            _scp.HostKeyReceived += OnHostKeyReceived;
        }

        if (!_scp.IsConnected) {
            _scp.Connect();
        }

        return _scp;
    }

    private void OnHostKeyReceived(object? sender, HostKeyEventArgs e) {
        SshHostKey key = HostKeyFingerprint.Describe(_connection.Host, _connection.Port, e);
        e.CanTrust = _verifier.Verify(key, e.HostKey);
        if (e.CanTrust) {
            HostKey = key;
        }
        else {
            RejectedHostKey = key;
        }
    }

    /// <inheritdoc/>
    public void Dispose() {
        _disposed = true;
        _registry?.Remove(this);
        _scp?.Dispose();
        Client.Dispose();
    }

    /// <inheritdoc/>
    public override string ToString() => Name.Length > 0 ? Name : $"{UserName}@{HostName}:{Port}";
}
