using System.Management.Automation;
using System.Net.Sockets;
using Renci.SshNet.Common;

namespace sftp.Commands;
/// <summary>Forwards a local port to a host and port reachable from the server.</summary>
[Cmdlet(VerbsCommon.New, "SshTunnel", SupportsShouldProcess = true)]
[OutputType(typeof(SshTunnel))]
public sealed class NewSshTunnelCommand : SftpCommand {
    /// <summary>The host to reach, as seen from the server. Use localhost for a service on the server itself.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string RemoteHost { get; set; } = "";

    /// <summary>The port to reach on the remote host.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateRange(1, 65535)]
    public int RemotePort { get; set; }

    /// <summary>The port on this computer to listen on. 0, the default, picks a free port.</summary>
    [Parameter]
    [ValidateRange(0, 65535)]
    public int LocalPort { get; set; }

    /// <summary>The address on this computer to listen on. The default, 127.0.0.1, only accepts connections from this computer.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string LocalAddress { get; set; } = "127.0.0.1";

    /// <inheritdoc/>
    protected override void ProcessItems() {
        string target = $"{LocalAddress}:{LocalPort} -> {RemoteHost}:{RemotePort} via {Current.Name}";
        if (!ShouldProcess(target, "Open tunnel")) {
            return;
        }

        try {
            SshTunnel tunnel = Current.OpenTunnel(LocalAddress, (uint)LocalPort, RemoteHost, (uint)RemotePort);
            WriteVerbose($"Tunnel {tunnel.Id} open: {tunnel}");
            WriteObject(tunnel);
        }
        catch (Exception error) when (error is SshException or SocketException or IOException or InvalidOperationException) {
            WriteError(new ErrorRecord(error, "TunnelFailed", error is SocketException ? ErrorCategory.ResourceUnavailable : CategoryOf(error), target));
        }
    }
}
