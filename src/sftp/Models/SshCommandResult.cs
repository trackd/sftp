namespace sftp;
/// <summary>The outcome of a command run with Invoke-SshCommand.</summary>
/// <param name="Command">The command that ran.</param>
/// <param name="Output">What the command wrote to standard output.</param>
/// <param name="Error">What the command wrote to standard error.</param>
/// <param name="ExitStatus">The exit code; 0 means success. Null when the command was killed by a signal.</param>
/// <param name="Duration">How long the command took.</param>
public sealed record SshCommandResult(string Command, string Output, string Error, int? ExitStatus, TimeSpan Duration) {
    /// <summary>True when the command exited with code 0.</summary>
    public bool Success => ExitStatus == 0;

    /// <summary>The name of the session the command ran on.</summary>
    public string SessionName { get; init; } = "";
}
