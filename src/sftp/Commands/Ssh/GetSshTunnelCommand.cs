using System.Management.Automation;

namespace sftp.Commands;
/// <summary>Lists the open SSH tunnels.</summary>
[Cmdlet(VerbsCommon.Get, "SshTunnel")]
[OutputType(typeof(SshTunnel))]
public sealed class GetSshTunnelCommand : PSCmdlet {
    /// <summary>Only the tunnels with these numbers.</summary>
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty]
    public int[] Id { get; set; } = [];

    /// <summary>Only the tunnels of this session, given as a session object or its name.</summary>
    [Parameter]
    [SessionTransformation]
    [ArgumentCompleter(typeof(SessionNameCompleter))]
    public SftpSession? Session { get; set; }

    /// <inheritdoc/>
    protected override void EndProcessing() {
        IEnumerable<SftpSession> sessions = Session is null ? SessionRegistry.Current.Sessions : [Session];
        foreach (SshTunnel tunnel in sessions.SelectMany(session => session.Tunnels)) {
            if (Id.Length == 0 || Id.Contains(tunnel.Id)) {
                WriteObject(tunnel);
            }
        }
    }
}
