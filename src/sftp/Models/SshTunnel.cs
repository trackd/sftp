using Renci.SshNet;

namespace sftp;
/// <summary>
/// A local port forward: connections to LocalAddress:LocalPort on this computer reach RemoteHost:RemotePort as
/// seen from the server. Create it with New-SshTunnel and close it with Remove-SshTunnel.
/// </summary>
public sealed class SshTunnel {
    private static int s_nextId;
    private readonly SftpSession _session;
    private readonly ForwardedPortLocal _port;

    internal SshTunnel(SftpSession session, ForwardedPortLocal port) {
        _session = session;
        _port = port;
        Id = Interlocked.Increment(ref s_nextId);
        LocalAddress = port.BoundHost;
        LocalPort = (int)port.BoundPort;
        RemoteHost = port.Host;
        RemotePort = (int)port.Port;
    }

    /// <summary>A number that identifies the tunnel, for Remove-SshTunnel -Id.</summary>
    public int Id { get; }

    /// <summary>The address on this computer that accepts connections.</summary>
    public string LocalAddress { get; }

    /// <summary>The port on this computer that accepts connections.</summary>
    public int LocalPort { get; }

    /// <summary>The host connections are forwarded to, as seen from the server.</summary>
    public string RemoteHost { get; }

    /// <summary>The port connections are forwarded to.</summary>
    public int RemotePort { get; }

    /// <summary>The name of the session that carries the tunnel.</summary>
    public string SessionName => _session.Name;

    /// <summary>True while the tunnel accepts connections.</summary>
    public bool IsOpen => _port.IsStarted && _session.IsConnected;

    internal SftpSession Session => _session;

    internal void Stop(SshClient? client) {
        try {
            if (_port.IsStarted) {
                _port.Stop();
            }

            client?.RemoveForwardedPort(_port);
        }
        catch (Exception error) when (error is Renci.SshNet.Common.SshException or ObjectDisposedException or InvalidOperationException) {
            // The connection is already gone.
        }

        _port.Dispose();
    }

    /// <inheritdoc/>
    public override string ToString() => $"{LocalAddress}:{LocalPort} -> {RemoteHost}:{RemotePort}";
}
