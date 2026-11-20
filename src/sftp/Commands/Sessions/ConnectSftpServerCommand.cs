using System.Management.Automation;
using System.Security;

namespace sftp.Commands;
/// <summary>Opens an SFTP session after verifying the server's host key.</summary>
[Cmdlet(VerbsCommunications.Connect, "SftpServer", DefaultParameterSetName = UserNameSet)]
[OutputType(typeof(SftpSession))]
public sealed class ConnectSftpServerCommand : PSCmdlet {
    private const string CredentialSet = "Credential";
    private const string UserNameSet = "UserName";

    /// <summary>
    /// The server: a name, an IP address or a Host alias from ~/.ssh/config, optionally as user@host.
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    [Alias("ComputerName", "Host")]
    [ArgumentCompleter(typeof(HostNameCompleter))]
    [ValidateNotNullOrEmpty]
    public string HostName { get; set; } = "";

    /// <summary>The server port.</summary>
    [Parameter(ValueFromPipelineByPropertyName = true)]
    [ValidateRange(1, 65535)]
    public int Port { get; set; } = 22;

    /// <summary>User name and password. The password is also used for keyboard-interactive login.</summary>
    [Parameter(Mandatory = true, ParameterSetName = CredentialSet)]
    [Credential]
    public PSCredential Credential { get; set; } = null!;

    /// <summary>User name for key-only login. Defaults to your local user name.</summary>
    [Parameter(ParameterSetName = UserNameSet)]
    [ValidateNotNullOrEmpty]
    public string UserName { get; set; } = Environment.UserName;

    /// <summary>Accepted host key fingerprints; the server's key must match one of them.</summary>
    [Parameter]
    [Alias("Fingerprint")]
    [ValidateNotNullOrEmpty]
    public string[] HostKeyFingerprint { get; set; } = [];

    /// <summary>
    /// An OpenSSH known_hosts file. Unknown hosts are added on first use; a changed key is refused.
    /// Used with ~/.ssh/known_hosts when no other host key option is given.
    /// </summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? KnownHostsFile { get; set; }

    /// <summary>Accept any host key. Only for testing: you cannot tell whether you reached the real server.</summary>
    [Parameter]
    public SwitchParameter SkipHostKeyCheck { get; set; }

    /// <summary>
    /// Private key files (OpenSSH, PuTTY .ppk v2/v3, PKCS#8). Without -KeyFile and -PrivateKey, the default
    /// keys in ~/.ssh (id_ed25519, id_ecdsa, id_rsa) are tried. A .pub file uses the private key next to it.
    /// </summary>
    [Parameter]
    [ArgumentCompleter(typeof(KeyFileCompleter))]
    [ValidateNotNullOrEmpty]
    public string[] KeyFile { get; set; } = [];

    /// <summary>Private keys as text, for keys stored in a secret vault.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public SecureString[] PrivateKey { get; set; } = [];

    /// <summary>The passphrase for encrypted private keys.</summary>
    [Parameter]
    public SecureString? KeyPassphrase { get; set; }

    /// <summary>The OpenSSH client config file to read. Defaults to ~/.ssh/config.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? ConfigFile { get; set; }

    /// <summary>Do not read ~/.ssh/config.</summary>
    [Parameter]
    public SwitchParameter IgnoreSshConfig { get; set; }

    /// <summary>A name for the session, for -Session and Get-SftpSession. Defaults to user@host.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    /// <summary>Seconds to wait for the connection and login.</summary>
    [Parameter]
    [ValidateRange(1, 3600)]
    public int ConnectTimeout { get; set; } = 30;

    /// <summary>Seconds to wait for each SFTP request. 0 waits indefinitely.</summary>
    [Parameter]
    [ValidateRange(0, 86400)]
    public int OperationTimeout { get; set; }

    /// <summary>Seconds between keep-alive messages. 0 disables them.</summary>
    [Parameter]
    [ValidateRange(0, 3600)]
    public int KeepAliveInterval { get; set; }

    /// <inheritdoc/>
    protected override void ProcessRecord() {
        if (Name is not null && SessionRegistry.Current.Find(Name) is not null) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException($"A session named '{Name}' is already open. Choose another -Name, or disconnect it first."),
                "SessionNameInUse",
                ErrorCategory.ResourceExists,
                Name));
        }

        using VerboseCapture? log = VerboseCapture.Start(this);
        try {
            SftpSession session = new SessionConnector(SessionState, WriteVerbose, WriteWarning).Connect(Options(), PipelineStopToken);
            log?.Flush();
            try {
                SessionRegistry.Current.Add(session, Name);
            }
            catch {
                session.Dispose();
                throw;
            }

            WriteObject(session);
        }
        catch (SessionConnectException error) {
            log?.Flush();
            ThrowTerminatingError(error.ErrorRecord);
        }
    }

    private ConnectOptions Options() {
        bool Bound(string name) => MyInvocation.BoundParameters.ContainsKey(name);

        return new ConnectOptions {
            HostName = HostName,
            Port = Bound(nameof(Port)) ? Port : null,
            Credential = ParameterSetName == CredentialSet ? Credential : null,
            UserName = Bound(nameof(UserName)) ? UserName : null,
            HostKeyFingerprint = HostKeyFingerprint,
            KnownHostsFile = KnownHostsFile,
            SkipHostKeyCheck = SkipHostKeyCheck,
            KeyFile = KeyFile,
            PrivateKey = PrivateKey,
            KeyPassphrase = KeyPassphrase,
            ConfigFile = ConfigFile,
            IgnoreSshConfig = IgnoreSshConfig,
            ConnectTimeout = Bound(nameof(ConnectTimeout)) ? ConnectTimeout : null,
            OperationTimeout = OperationTimeout,
            KeepAliveInterval = Bound(nameof(KeepAliveInterval)) ? KeepAliveInterval : null,
        };
    }
}
