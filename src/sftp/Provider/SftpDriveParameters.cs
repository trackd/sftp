using System.Management.Automation;

namespace sftp;
/// <summary>The parameters New-PSDrive adds for the Sftp provider.</summary>
public sealed class SftpDriveParameters {
    /// <summary>The open session to use, as a session object or its name.</summary>
    [Parameter]
    [SessionTransformation]
    [ArgumentCompleter(typeof(SessionNameCompleter))]
    public SftpSession? Session { get; set; }

    /// <summary>Connect to this server for the drive (closed again by Remove-PSDrive). Accepts user@host and ~/.ssh/config aliases.</summary>
    [Parameter]
    [ArgumentCompleter(typeof(HostNameCompleter))]
    [ValidateNotNullOrEmpty]
    public string? HostName { get; set; }

    /// <summary>The server port, with -HostName.</summary>
    [Parameter]
    [ValidateRange(1, 65535)]
    public int? Port { get; set; }

    /// <summary>User name for key login, with -HostName. Use -Credential for a password.</summary>
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
    public System.Security.SecureString? KeyPassphrase { get; set; }

    /// <summary>Accepted host key fingerprints, with -HostName.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string[] HostKeyFingerprint { get; set; } = [];

    /// <summary>An OpenSSH known_hosts file, with -HostName.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? KnownHostsFile { get; set; }

    /// <summary>Accept any host key, with -HostName. Only for testing.</summary>
    [Parameter]
    public SwitchParameter SkipHostKeyCheck { get; set; }
}
