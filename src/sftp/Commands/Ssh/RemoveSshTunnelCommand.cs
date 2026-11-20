using System.Management.Automation;

namespace sftp.Commands;
/// <summary>Closes SSH tunnels.</summary>
[OutputType(typeof(void))]
[Cmdlet(VerbsCommon.Remove, "SshTunnel", SupportsShouldProcess = true, DefaultParameterSetName = TunnelSet)]
public sealed class RemoveSshTunnelCommand : PSCmdlet {
    private const string TunnelSet = "Tunnel";
    private const string IdSet = "Id";

    /// <summary>The tunnels to close, from New-SshTunnel or Get-SshTunnel.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = TunnelSet, ValueFromPipeline = true)]
    [ValidateNotNull]
    public SshTunnel[] Tunnel { get; set; } = [];

    /// <summary>The numbers of the tunnels to close.</summary>
    [Parameter(Mandatory = true, ParameterSetName = IdSet)]
    [ValidateNotNullOrEmpty]
    public int[] Id { get; set; } = [];

    /// <inheritdoc/>
    protected override void ProcessRecord() {
        IEnumerable<SshTunnel> tunnels = Tunnel;
        if (ParameterSetName == IdSet) {
            List<SshTunnel> open = [.. SessionRegistry.Current.Sessions.SelectMany(session => session.Tunnels)];
            foreach (int id in Id.Where(id => open.All(tunnel => tunnel.Id != id))) {
                WriteError(new ErrorRecord(
                    new ItemNotFoundException($"No open tunnel with Id {id}. See Get-SshTunnel."), "TunnelNotFound", ErrorCategory.ObjectNotFound, id));
            }

            tunnels = open.Where(tunnel => Id.Contains(tunnel.Id));
        }

        foreach (SshTunnel tunnel in tunnels) {
            if (ShouldProcess($"{tunnel.Id}: {tunnel}", "Close tunnel")) {
                tunnel.Session.CloseTunnel(tunnel);
                WriteVerbose($"Tunnel {tunnel.Id} closed.");
            }
        }
    }
}
