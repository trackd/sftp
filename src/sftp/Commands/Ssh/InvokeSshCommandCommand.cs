using System.Diagnostics;
using System.Management.Automation;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace sftp.Commands;
/// <summary>Runs a command on the server over SSH.</summary>
[Cmdlet(VerbsLifecycle.Invoke, "SshCommand", SupportsShouldProcess = true)]
[OutputType(typeof(SshCommandResult))]
public sealed class InvokeSshCommandCommand : SftpCommand {
    /// <summary>The command to run, as you would type it in the server's shell. Accepts pipeline input.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [ValidateNotNullOrEmpty]
    public string[] Command { get; set; } = [];

    /// <summary>Seconds to wait for each command before stopping it. 0 waits as long as needed.</summary>
    [Parameter]
    [ValidateRange(0, 86400)]
    public int Timeout { get; set; }

    /// <inheritdoc/>
    protected override void ProcessItems() {
        foreach (string text in Command) {
            if (!ShouldProcess($"{Current.Name}: {text}", "Run command")) {
                continue;
            }

            var timer = Stopwatch.StartNew();
            try {
                SshClient client = Current.GetSshClient();
                using SshCommand command = client.CreateCommand(text);
                if (Timeout > 0) {
                    command.CommandTimeout = TimeSpan.FromSeconds(Timeout);
                }

                command.ExecuteAsync(PipelineStopToken).GetAwaiter().GetResult();
                var result = new SshCommandResult(text, command.Result, command.Error, command.ExitStatus, timer.Elapsed) { SessionName = Current.Name };
                WriteObject(result);
                if (!result.Success) {
                    string reason = result.ExitStatus is { } code ? $"exit code {code}" : $"signal {command.ExitSignal}";
                    string detail = result.Error.Trim() is { Length: > 0 } stderr ? $": {stderr}" : ".";
                    WriteError(new ErrorRecord(
                        new InvalidOperationException($"'{text}' failed with {reason}{detail}"),
                        "CommandFailed",
                        ErrorCategory.InvalidResult,
                        result));
                }
            }
            catch (OperationCanceledException) when (PipelineStopToken.IsCancellationRequested) {
                throw new PipelineStoppedException();
            }
            catch (Exception error) when (error is SshException or IOException or InvalidOperationException) {
                WriteItemError(error, error is SshOperationTimeoutException ? "CommandTimedOut" : "CommandFailed", text);
            }
        }
    }
}
