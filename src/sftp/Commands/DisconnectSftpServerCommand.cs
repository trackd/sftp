using System.Management.Automation;

namespace sftp.Commands;
/// <summary>Closes SFTP sessions.</summary>
[Cmdlet(VerbsCommunications.Disconnect, "SftpServer")]
[OutputType(typeof(void))]
public sealed class DisconnectSftpServerCommand : PSCmdlet {
    /// <summary>The sessions to close, as session objects or names. Defaults to the current session.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [SessionTransformation]
    [ValidateNotNull]
    public SftpSession[]? Session { get; set; }

    /// <inheritdoc/>
    protected override void ProcessRecord() {
        SftpSession[] sessions = Session ?? (SessionRegistry.Current.Default is { } current ? [current] : []);
        if (sessions.Length == 0) {
            WriteError(new ErrorRecord(
                new InvalidOperationException("Not connected; there is no session to close."),
                "NotConnected",
                ErrorCategory.ConnectionError,
                null));
        }

        foreach (SftpSession session in sessions) {
            WriteVerbose($"Disconnecting {session.Name}");
            session.Dispose();
        }
    }
}
