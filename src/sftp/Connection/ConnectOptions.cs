using System.Management.Automation;
using System.Security;

namespace sftp;
/// <summary>What to connect to and how to log in; null values mean "not given", so config and defaults apply.</summary>
internal sealed class ConnectOptions {
    public required string HostName { get; init; }
    public int? Port { get; init; }
    public PSCredential? Credential { get; init; }
    public string? UserName { get; init; }
    public string[] HostKeyFingerprint { get; init; } = [];
    public string? KnownHostsFile { get; init; }
    public bool SkipHostKeyCheck { get; init; }
    public string[] KeyFile { get; init; } = [];
    public SecureString[] PrivateKey { get; init; } = [];
    public SecureString? KeyPassphrase { get; init; }
    public string? ConfigFile { get; init; }
    public bool IgnoreSshConfig { get; init; }
    public int? ConnectTimeout { get; init; }
    public int OperationTimeout { get; init; }
    public int? KeepAliveInterval { get; init; }
}
