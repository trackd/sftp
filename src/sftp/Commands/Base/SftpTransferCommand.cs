using System.Management.Automation;
using System.Security;

namespace sftp.Commands;
/// <summary>
/// Transfer commands that can also connect for a single run: with -HostName they open their own session,
/// use it for the whole pipeline and close it when they finish, fail or are stopped.
/// </summary>
public abstract class SftpTransferCommand : SftpCommand {
    /// <summary>
    /// Connect to this server for this command only, instead of using an open session: a name, an IP address or
    /// a Host alias from ~/.ssh/config, optionally as user@host.
    /// </summary>
    [Parameter]
    [Alias("ComputerName")]
    [ArgumentCompleter(typeof(HostNameCompleter))]
    [ValidateNotNullOrEmpty]
    public string? HostName { get; set; }

    /// <summary>The server port, with -HostName.</summary>
    [Parameter]
    [ValidateRange(1, 65535)]
    public int Port { get; set; } = 22;

    /// <summary>User name and password, with -HostName.</summary>
    [Parameter]
    [Credential]
    public PSCredential? Credential { get; set; }

    /// <summary>User name for key login, with -HostName.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? UserName { get; set; }

    /// <summary>Private key files, with -HostName.</summary>
    [Parameter]
    [ArgumentCompleter(typeof(KeyFileCompleter))]
    [ValidateNotNullOrEmpty]
    public string[] KeyFile { get; set; } = [];

    /// <summary>The passphrase for encrypted private keys, with -HostName.</summary>
    [Parameter]
    public SecureString? KeyPassphrase { get; set; }

    /// <summary>Accepted host key fingerprints, with -HostName.</summary>
    [Parameter]
    [Alias("Fingerprint")]
    [ValidateNotNullOrEmpty]
    public string[] HostKeyFingerprint { get; set; } = [];

    /// <summary>An OpenSSH known_hosts file, with -HostName.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? KnownHostsFile { get; set; }

    /// <summary>Accept any host key, with -HostName. Only for testing.</summary>
    [Parameter]
    public SwitchParameter SkipHostKeyCheck { get; set; }

    private static readonly string[] ConnectionParameters =
        [nameof(Port), nameof(Credential), nameof(UserName), nameof(KeyFile), nameof(KeyPassphrase), nameof(HostKeyFingerprint), nameof(KnownHostsFile), nameof(SkipHostKeyCheck)];

    /// <inheritdoc/>
    protected override void BeginProcessing() {
        base.BeginProcessing();
        if (HostName is null) {
            string? stray = ConnectionParameters.FirstOrDefault(MyInvocation.BoundParameters.ContainsKey);
            if (stray is not null) {
                ThrowTerminatingError(new ErrorRecord(
                    new PSArgumentException($"-{stray} is only used with -HostName, to connect for this command."),
                    "HostNameRequired",
                    ErrorCategory.InvalidArgument,
                    stray));
            }

            return;
        }

        if (Session is not null) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException("Use either -Session or -HostName, not both."),
                "SessionAndHostName",
                ErrorCategory.InvalidArgument,
                HostName));
        }

        if (Credential is not null && UserName is not null) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException("Use either -Credential or -UserName, not both."),
                "CredentialAndUserName",
                ErrorCategory.InvalidArgument,
                UserName));
        }

        var options = new ConnectOptions {
            HostName = HostName,
            Port = MyInvocation.BoundParameters.ContainsKey(nameof(Port)) ? Port : null,
            Credential = Credential,
            UserName = UserName,
            KeyFile = KeyFile,
            KeyPassphrase = KeyPassphrase,
            HostKeyFingerprint = HostKeyFingerprint,
            KnownHostsFile = KnownHostsFile,
            SkipHostKeyCheck = SkipHostKeyCheck,
        };
        SftpSession session;
        try {
            session = new SessionConnector(SessionState, WriteVerbose, WriteWarning).Connect(options, PipelineStopToken);
        }
        catch (SessionConnectException error) {
            FlushVerbose();
            ThrowTerminatingError(error.ErrorRecord);

            return;
        }

        UseOwnedSession(session);
        // Registered so piped results can find it by name, but never as the current session.
        SessionRegistry.Current.Add(session, null, isPrivate: true);
        FlushVerbose();
    }
}
