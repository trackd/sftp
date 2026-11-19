using System.Management.Automation;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace sftp.Commands;
/// <summary>Reads an SSH server's host key without logging in.</summary>
[Cmdlet(VerbsCommon.Get, "SshHostKey")]
[OutputType(typeof(SshHostKey))]
public sealed class GetSshHostKeyCommand : PSCmdlet {
    /// <summary>The server name or IP address.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("ComputerName", "Host")]
    [ValidateNotNullOrEmpty]
    public string[] HostName { get; set; } = [];

    /// <summary>The server port.</summary>
    [Parameter(ValueFromPipelineByPropertyName = true)]
    [ValidateRange(1, 65535)]
    public int Port { get; set; } = 22;

    /// <summary>Seconds to wait for the server.</summary>
    [Parameter]
    [ValidateRange(1, 3600)]
    public int ConnectTimeout { get; set; } = 30;

    /// <inheritdoc/>
    protected override void ProcessRecord() {
        foreach (string host in HostName) {
            SshHostKey? key = null;
            Exception? failure = null;
            var connection = new ConnectionInfo(host, Port, "host-key-scan", new NoneAuthenticationMethod("host-key-scan")) {
                Timeout = TimeSpan.FromSeconds(ConnectTimeout),
            };
            using (var client = new SftpClient(connection)) {
                client.HostKeyReceived += (_, e) => {
                    key = HostKeyFingerprint.Describe(host, Port, e);
                    // Refuse the key so no login is attempted.
                    e.CanTrust = false;
                };
                try {
                    client.ConnectAsync(PipelineStopToken).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) when (PipelineStopToken.IsCancellationRequested) {
                    throw new PipelineStoppedException();
                }
                catch (Exception error) when (error is SshException or System.Net.Sockets.SocketException or TimeoutException) {
                    failure = error;
                }
            }

            if (key is not null) {
                WriteObject(key);
            }
            else {
                WriteError(new ErrorRecord(
                    failure ?? new InvalidOperationException($"{host}:{Port} did not present a host key."),
                    "HostKeyScanFailed",
                    ErrorCategory.ConnectionError,
                    host));
            }
        }
    }
}
