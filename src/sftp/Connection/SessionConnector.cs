using System.Management.Automation;
using System.Net;
using System.Security;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace sftp;
/// <summary>
/// Opens a host-key-verified session the way OpenSSH sftp does: typed parameters first, then user@ in the host
/// name, then the matching Host block in ~/.ssh/config, then the defaults (default keys, ~/.ssh/known_hosts).
/// Shared by Connect-SftpServer, one-shot transfers and Sftp drives.
/// </summary>
internal sealed class SessionConnector(SessionState state, Action<string> verbose, Action<string> warning) {
    private const int DefaultPort = 22;
    private const int DefaultTimeout = 30;
    private static readonly string[] DefaultKeyNames = ["id_ed25519", "id_ecdsa", "id_rsa"];
    private readonly HashSet<string> _keysTried = new(StringComparer.OrdinalIgnoreCase);
    private SshHostConfig _config = new();
    private string _host = "";
    private int _port = DefaultPort;
    private string _user = "";

    private string DefaultKeyFolder => state.Path.GetUnresolvedProviderPathFromPSPath("~/.ssh");

    /// <summary>Connects and returns the open session; throws <see cref="SessionConnectException"/> on failure.</summary>
    public SftpSession Connect(ConnectOptions options, CancellationToken cancellationToken) {
        ResolveTarget(options);
        HostKeyVerifier verifier = CreateVerifier(options, out bool skipHostKeyCheck);
        string user = _user;
        List<AuthenticationMethod> methods = [];
        List<IDisposable> keys = [];
        SftpSession? session = null;
        try {
            List<IPrivateKeySource> keySources = LoadKeys(options, keys);
            if (keySources.Count > 0) {
                methods.Add(new PrivateKeyAuthenticationMethod(user, [.. keySources]));
            }

            if (options.Credential is { } credential && credential.Password.Length > 0) {
                string password = new NetworkCredential("", credential.Password).Password;
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
                throw new PSArgumentException(
                    $"No private key found in {DefaultKeyFolder} ({string.Join(", ", DefaultKeyNames)}). Use -KeyFile, -PrivateKey or -Credential.");
            }

            int timeout = options.ConnectTimeout ?? _config.ConnectTimeout ?? DefaultTimeout;
            var connection = new ConnectionInfo(_host, _port, user, [.. methods]) {
                Timeout = TimeSpan.FromSeconds(timeout),
            };
            session = new SftpSession(connection, verifier);
            if (options.OperationTimeout > 0) {
                session.Client.OperationTimeout = TimeSpan.FromSeconds(options.OperationTimeout);
            }

            int keepAlive = options.KeepAliveInterval ?? _config.ServerAliveInterval ?? 0;
            if (keepAlive > 0) {
                session.Client.KeepAliveInterval = TimeSpan.FromSeconds(keepAlive);
            }

            verbose($"Connecting to {_host}:{_port} as {user}");
            session.Connect(cancellationToken);
            verbose($"Connected; host key {session.HostKey?.Fingerprint}");
            if (skipHostKeyCheck) {
                warning($"The host key was not checked. {_host} presented {session.HostKey?.Fingerprint}.");
            }

            string? saved = verifier.Commit();
            if (saved is not null) {
                warning($"{saved} Host key: {session.HostKey?.Fingerprint}");
            }

            SftpSession connected = session;
            session = null;

            return connected;
        }
        catch (PipelineStoppedException) {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw new PipelineStoppedException();
        }
        catch (Exception error) when (session?.RejectedHostKey is not null) {
            SshHostKey actual = session.RejectedHostKey;
            throw Fail(new SecurityException(session.Verifier.MismatchMessage(actual), error), "HostKeyMismatch", ErrorCategory.SecurityError, actual);
        }
        catch (SshPassPhraseNullOrEmptyException error) {
            throw Fail(new PSArgumentException("The private key is encrypted; supply -KeyPassphrase.", error), "KeyPassphraseRequired", ErrorCategory.InvalidArgument, null);
        }
        catch (SshAuthenticationException error) {
            throw Fail(error, "AuthenticationFailed", ErrorCategory.AuthenticationError, user);
        }
        catch (KeyFileException error) {
            throw Fail(error, "InvalidKeyFile", ErrorCategory.InvalidArgument, error.Path);
        }
        catch (PSArgumentException error) {
            throw Fail(error, "MissingAuthentication", ErrorCategory.InvalidArgument, null);
        }
        catch (Exception error) when (error is SshException or System.Net.Sockets.SocketException or TimeoutException) {
            throw Fail(error, "ConnectionFailed", ErrorCategory.ConnectionError, _host);
        }
        finally {
            session?.Dispose();
            foreach (IDisposable key in keys) {
                key.Dispose();
            }
        }
    }

    private static SessionConnectException Fail(Exception error, string id, ErrorCategory category, object? target) =>
        new(new ErrorRecord(error, id, category, target));

    private HostKeyVerifier CreateVerifier(ConnectOptions options, out bool skipHostKeyCheck) {
        string? knownHostsFile = options.KnownHostsFile;
        skipHostKeyCheck = options.SkipHostKeyCheck;
        int given = (options.HostKeyFingerprint.Length > 0 ? 1 : 0) + (knownHostsFile is null ? 0 : 1) + (skipHostKeyCheck ? 1 : 0);
        bool addUnknownHosts = true;
        if (given == 0) {
            // Like ssh: the known_hosts file from ~/.ssh/config or the default one, with StrictHostKeyChecking.
            knownHostsFile = _config.UserKnownHostsFile is { } file ? ExpandConfigPath(file) : Path.Join(DefaultKeyFolder, "known_hosts");
            switch (_config.StrictHostKeyChecking) {
                case "yes":
                    addUnknownHosts = false;
                    break;
                case "no" or "off":
                    knownHostsFile = null;
                    skipHostKeyCheck = true;
                    break;
            }
        }
        else if (given > 1) {
            throw Fail(
                new PSArgumentException("Use only one of -HostKeyFingerprint, -KnownHostsFile or -SkipHostKeyCheck."),
                "HostKeyCheckRequired",
                ErrorCategory.InvalidArgument,
                null);
        }

        try {
            return skipHostKeyCheck ? new SkipVerifier() :
                knownHostsFile is not null ? new KnownHostsVerifier(state.Path.GetUnresolvedProviderPathFromPSPath(knownHostsFile), _host, _port, addUnknownHosts) :
                new FingerprintVerifier([.. options.HostKeyFingerprint.Select(HostKeyFingerprint.Parse)]);
        }
        catch (FormatException error) {
            throw Fail(error, "InvalidHostKeyFingerprint", ErrorCategory.InvalidArgument, options.HostKeyFingerprint);
        }
        catch (IOException error) {
            throw Fail(error, "KnownHostsUnreadable", ErrorCategory.ReadError, knownHostsFile);
        }
    }

    private void ResolveTarget(ConnectOptions options) {
        string alias = options.HostName;
        string? hostUser = null;
        int at = alias.LastIndexOf('@');
        if (at > 0) {
            hostUser = alias[..at];
            alias = alias[(at + 1)..];
        }

        string configFile = options.ConfigFile is null ? Path.Join(DefaultKeyFolder, "config") : state.Path.GetUnresolvedProviderPathFromPSPath(options.ConfigFile);
        _config = options.IgnoreSshConfig ? new SshHostConfig() : SshConfig.Resolve(configFile, alias, DefaultKeyFolder);
        _host = _config.HostName is { } configured ? configured.Replace("%h", alias, StringComparison.Ordinal) : alias;
        _port = options.Port ?? _config.Port ?? DefaultPort;
        _user = options.Credential?.UserName ?? options.UserName ?? hostUser ?? _config.User ?? Environment.UserName;
        if (_config.HostName is not null || _config.User is not null || _config.Port is not null) {
            verbose($"{configFile}: {alias} is {_user}@{_host}:{_port}");
        }
    }

    private string ExpandConfigPath(string value) {
        string home = Path.GetDirectoryName(DefaultKeyFolder) ?? DefaultKeyFolder;
        string expanded = SshConfig.ExpandTokens(value, home, _host, _user);

        return Path.IsPathRooted(expanded) ? expanded : Path.Join(home, expanded);
    }

    private List<IPrivateKeySource> LoadKeys(ConnectOptions options, List<IDisposable> owned) {
        string? passphrase = options.KeyPassphrase is null ? null : new NetworkCredential("", options.KeyPassphrase).Password;
        List<IPrivateKeySource> sources = [];
        foreach (string path in options.KeyFile) {
            string resolved = state.Path.GetUnresolvedProviderPathFromPSPath(path);
            if (!File.Exists(resolved)) {
                throw new KeyFileException($"Key file not found: {resolved}", resolved);
            }

            if (KeyFiles.IsPublicKey(resolved)) {
                string? privateKey = KeyFiles.PrivateKeyFor(resolved);
                if (privateKey is null) {
                    throw new KeyFileException(
                        $"'{resolved}' is a public key. Use the matching private key file (usually the same name without .pub).", resolved);
                }

                verbose($"'{resolved}' is a public key; using the private key '{privateKey}'.");
                resolved = privateKey;
            }

            sources.Add(Load(() => new PrivateKeyFile(resolved, passphrase), resolved));
        }

        foreach (SecureString key in options.PrivateKey) {
            string text = new NetworkCredential("", key).Password;
            if (KeyFiles.IsPublicKeyText(text)) {
                throw new KeyFileException("-PrivateKey contains a public key. Pass the private key instead.", "-PrivateKey");
            }

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            owned.Add(stream);
            sources.Add(Load(() => new PrivateKeyFile(stream, passphrase), "-PrivateKey"));
        }

        if (options.KeyFile.Length == 0 && options.PrivateKey.Length == 0) {
            sources.AddRange(_config.IdentityFiles.Select(ExpandConfigPath).SelectMany(path => TryLoad(path, passphrase)));
            if (_config.IdentitiesOnly != true) {
                sources.AddRange(DefaultKeyNames.Select(name => Path.Join(DefaultKeyFolder, name)).SelectMany(path => TryLoad(path, passphrase)));
            }
        }

        return sources;
    }

    /// <summary>Loads a config or default key; missing or unreadable ones are skipped, like ssh does.</summary>
    private IEnumerable<IPrivateKeySource> TryLoad(string path, string? passphrase) {
        if (!_keysTried.Add(path)) {
            yield break;
        }

        if (!File.Exists(path) && KeyFiles.PrivateKeyFor(path) is null) {
            yield break;
        }

        // IdentityFile entries sometimes name the .pub file; the private key next to it is what logs in.
        if (KeyFiles.IsPublicKey(path)) {
            path = KeyFiles.PrivateKeyFor(path) ?? path;
        }

        PrivateKeyFile? key = null;
        try {
            key = new PrivateKeyFile(path, passphrase);
        }
        catch (Exception error) when (error is SshException or IOException or UnauthorizedAccessException or ArgumentException) {
            verbose($"Skipping key '{path}': {error.Message}");
        }

        if (key is not null) {
            verbose($"Trying key '{path}'.");
            yield return key;
        }
    }

    private static PrivateKeyFile Load(Func<PrivateKeyFile> load, string source) {
        try {
            return load();
        }
        catch (SshPassPhraseNullOrEmptyException) {
            throw;
        }
        catch (Exception error) when (error is SshException or ArgumentException or InvalidOperationException) {
            throw new KeyFileException($"Could not read the private key '{source}': {error.Message}", source, error);
        }
    }

    /// <summary>A key that is missing, public or unreadable.</summary>
    private sealed class KeyFileException(string message, string path, Exception? inner = null) : PSArgumentException(message, inner) {
        public string Path { get; } = path;
    }
}
