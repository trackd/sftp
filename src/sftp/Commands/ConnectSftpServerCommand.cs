using System.Management.Automation;
using System.Net;
using System.Security;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace sftp.Commands;
/// <summary>Opens an SFTP session after verifying the server's host key.</summary>
[Cmdlet(VerbsCommunications.Connect, "SftpServer", DefaultParameterSetName = CredentialSet)]
[OutputType(typeof(SftpSession))]
public sealed class ConnectSftpServerCommand : PSCmdlet {
    private const string CredentialSet = "Credential";
    private const string UserNameSet = "UserName";

    /// <summary>The server name or IP address.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    [Alias("ComputerName", "Host")]
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

    /// <summary>User name for key-only login.</summary>
    [Parameter(Mandatory = true, ParameterSetName = UserNameSet)]
    [ValidateNotNullOrEmpty]
    public string UserName { get; set; } = "";

    /// <summary>Accepted host key fingerprints; the server's key must match one of them.</summary>
    [Parameter]
    [Alias("Fingerprint")]
    [ValidateNotNullOrEmpty]
    public string[] HostKeyFingerprint { get; set; } = [];

    /// <summary>An OpenSSH known_hosts file. Unknown hosts are added on first use; a changed key is refused.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? KnownHostsFile { get; set; }

    /// <summary>Accept any host key. Only for testing: you cannot tell whether you reached the real server.</summary>
    [Parameter]
    public SwitchParameter SkipHostKeyCheck { get; set; }

    /// <summary>Private key files (OpenSSH, PuTTY .ppk v2/v3, PKCS#8).</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string[] KeyFile { get; set; } = [];

    /// <summary>Private keys as text, for keys stored in a secret vault.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public SecureString[] PrivateKey { get; set; } = [];

    /// <summary>The passphrase for encrypted private keys.</summary>
    [Parameter]
    public SecureString? KeyPassphrase { get; set; }

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
        int options = (HostKeyFingerprint.Length > 0 ? 1 : 0) + (KnownHostsFile is null ? 0 : 1) + (SkipHostKeyCheck ? 1 : 0);
        if (options != 1) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException("Specify exactly one of -HostKeyFingerprint, -KnownHostsFile or -SkipHostKeyCheck."),
                "HostKeyCheckRequired",
                ErrorCategory.InvalidArgument,
                null));
        }

        if (Name is not null && SessionRegistry.Current.Find(Name) is not null) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException($"A session named '{Name}' is already open. Choose another -Name, or disconnect it first."),
                "SessionNameInUse",
                ErrorCategory.ResourceExists,
                Name));
        }

        HostKeyVerifier verifier;
        try {
            verifier = SkipHostKeyCheck ? new SkipVerifier() :
                KnownHostsFile is not null ? new KnownHostsVerifier(SessionState.Path.GetUnresolvedProviderPathFromPSPath(KnownHostsFile), HostName, Port) :
                new FingerprintVerifier([.. HostKeyFingerprint.Select(sftp.HostKeyFingerprint.Parse)]);
        }
        catch (FormatException error) {
            ThrowTerminatingError(new ErrorRecord(error, "InvalidHostKeyFingerprint", ErrorCategory.InvalidArgument, HostKeyFingerprint));

            return;
        }
        catch (IOException error) {
            ThrowTerminatingError(new ErrorRecord(error, "KnownHostsUnreadable", ErrorCategory.ReadError, KnownHostsFile));

            return;
        }

        string user = ParameterSetName == CredentialSet ? Credential.UserName : UserName;
        List<AuthenticationMethod> methods = [];
        List<IDisposable> keys = [];
        SftpSession? session = null;
        try {
            List<IPrivateKeySource> keySources = LoadKeys(keys);
            if (keySources.Count > 0) {
                methods.Add(new PrivateKeyAuthenticationMethod(user, [.. keySources]));
            }

            if (ParameterSetName == CredentialSet && Credential.Password.Length > 0) {
                string password = new NetworkCredential("", Credential.Password).Password;
                methods.Add(new PasswordAuthenticationMethod(user, Encoding.UTF8.GetBytes(password)));
                var interactive = new KeyboardInteractiveAuthenticationMethod(user);
                interactive.AuthenticationPrompt += (_, e) => {
                    foreach (AuthenticationPrompt prompt in e.Prompts.Where(p => !p.IsEchoed)) {
                        prompt.Response = password;
                    }
                };
                methods.Add(interactive);
            }

            if (methods.Count == 0) {
                throw new PSArgumentException("Supply a password (-Credential), -KeyFile or -PrivateKey.");
            }

            var connection = new ConnectionInfo(HostName, Port, user, [.. methods]) {
                Timeout = TimeSpan.FromSeconds(ConnectTimeout),
            };
            session = new SftpSession(connection, verifier);
            if (OperationTimeout > 0) {
                session.Client.OperationTimeout = TimeSpan.FromSeconds(OperationTimeout);
            }

            if (KeepAliveInterval > 0) {
                session.Client.KeepAliveInterval = TimeSpan.FromSeconds(KeepAliveInterval);
            }

            WriteVerbose($"Connecting to {HostName}:{Port} as {user}");
            session.Connect(PipelineStopToken);
            WriteVerbose($"Connected; host key {session.HostKey?.Fingerprint}");
            if (SkipHostKeyCheck) {
                WriteWarning($"The host key was not checked. {HostName} presented {session.HostKey?.Fingerprint}.");
            }

            string? saved = verifier.Commit();
            if (saved is not null) {
                WriteWarning($"{saved} Host key: {session.HostKey?.Fingerprint}");
            }
            SessionRegistry.Current.Add(session, Name);
            WriteObject(session);
            session = null;
        }
        catch (PipelineStoppedException) {
            throw;
        }
        catch (OperationCanceledException) when (PipelineStopToken.IsCancellationRequested) {
            throw new PipelineStoppedException();
        }
        catch (Exception error) when (session?.RejectedHostKey is not null) {
            SshHostKey actual = session.RejectedHostKey;
            ThrowTerminatingError(new ErrorRecord(
                new SecurityException(session.Verifier.MismatchMessage(actual), error),
                "HostKeyMismatch",
                ErrorCategory.SecurityError,
                actual));
        }
        catch (SshPassPhraseNullOrEmptyException error) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException("The private key is encrypted; supply -KeyPassphrase.", error),
                "KeyPassphraseRequired",
                ErrorCategory.InvalidArgument,
                null));
        }
        catch (SshAuthenticationException error) {
            ThrowTerminatingError(new ErrorRecord(error, "AuthenticationFailed", ErrorCategory.AuthenticationError, user));
        }
        catch (PSArgumentException error) {
            ThrowTerminatingError(new ErrorRecord(error, "MissingAuthentication", ErrorCategory.InvalidArgument, null));
        }
        catch (Exception error) when (error is SshException or System.Net.Sockets.SocketException or TimeoutException) {
            ThrowTerminatingError(new ErrorRecord(error, "ConnectionFailed", ErrorCategory.ConnectionError, HostName));
        }
        finally {
            session?.Dispose();
            foreach (IDisposable key in keys) {
                key.Dispose();
            }
        }
    }

    private List<IPrivateKeySource> LoadKeys(List<IDisposable> owned) {
        string? passphrase = KeyPassphrase is null ? null : new NetworkCredential("", KeyPassphrase).Password;
        List<IPrivateKeySource> sources = [];
        foreach (string path in KeyFile) {
            string resolved = SessionState.Path.GetUnresolvedProviderPathFromPSPath(path);
            if (!File.Exists(resolved)) {
                throw new PSArgumentException($"Key file not found: {resolved}");
            }

            sources.Add(new PrivateKeyFile(resolved, passphrase));
        }

        foreach (SecureString key in PrivateKey) {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(new NetworkCredential("", key).Password));
            owned.Add(stream);
            sources.Add(new PrivateKeyFile(stream, passphrase));
        }

        return sources;
    }
}
