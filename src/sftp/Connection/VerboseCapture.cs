using System.Collections.Concurrent;
using System.Management.Automation;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace sftp;
/// <summary>
/// Collects SSH.NET log messages while a command runs with -Verbose. SSH.NET logs from its own background
/// threads, where WriteVerbose is not allowed, so messages are queued here and written by the command on the
/// pipeline thread when it calls <see cref="Flush"/>.
/// </summary>
internal sealed class VerboseCapture : IDisposable {
    private const int MaxQueued = 10_000;
    private static readonly Lock s_sync = new();
    private static VerboseCapture[] s_active = [];
    private static int s_initialized;
    private readonly ConcurrentQueue<string> _messages = new();
    private readonly Cmdlet _cmdlet;
    private int _dropped;

    private VerboseCapture(Cmdlet cmdlet) => _cmdlet = cmdlet;

    /// <summary>True while at least one command is collecting messages.</summary>
    internal static bool AnyActive => Volatile.Read(ref s_active).Length > 0;

    /// <summary>Sends SSH.NET logging here. Must run before SSH.NET objects are created, as they keep their logger.</summary>
    internal static void EnsureInitialized() {
        if (Interlocked.Exchange(ref s_initialized, 1) == 0) {
            SshNetLoggingConfiguration.InitializeLogging(new VerboseLoggerFactory());
        }
    }

    /// <summary>Starts collecting for the command when it runs with -Verbose; otherwise returns null.</summary>
    internal static VerboseCapture? Start(PSCmdlet cmdlet) {
        EnsureInitialized();
        if (!IsVerbose(cmdlet)) {
            return null;
        }

        var capture = new VerboseCapture(cmdlet);
        lock (s_sync) {
            s_active = [.. s_active, capture];
        }

        return capture;
    }

    private static bool IsVerbose(PSCmdlet cmdlet) {
        if (cmdlet.MyInvocation.BoundParameters.TryGetValue("Verbose", out object? value)) {
            return value is SwitchParameter { IsPresent: true } || value is true;
        }

        return cmdlet.GetVariableValue("VerbosePreference") is ActionPreference preference && preference != ActionPreference.SilentlyContinue &&
            preference != ActionPreference.Ignore;
    }

    internal static void Publish(string message) {
        foreach (VerboseCapture capture in Volatile.Read(ref s_active)) {
            if (capture._messages.Count < MaxQueued) {
                capture._messages.Enqueue(message);
            }
            else {
                Interlocked.Increment(ref capture._dropped);
            }
        }
    }

    /// <summary>Writes the queued messages as verbose output. Call only on the pipeline thread.</summary>
    public void Flush() {
        while (_messages.TryDequeue(out string? message)) {
            _cmdlet.WriteVerbose(message);
        }

        int dropped = Interlocked.Exchange(ref _dropped, 0);
        if (dropped > 0) {
            _cmdlet.WriteVerbose($"SSH.NET: {dropped} more log messages were not shown.");
        }
    }

    /// <inheritdoc/>
    public void Dispose() {
        lock (s_sync) {
            s_active = [.. s_active.Where(capture => !ReferenceEquals(capture, this))];
        }
    }

    private sealed class VerboseLoggerFactory : ILoggerFactory {
        public ILogger CreateLogger(string categoryName) => new VerboseLogger(categoryName[(categoryName.LastIndexOf('.') + 1)..]);

        public void AddProvider(ILoggerProvider provider) {
        }

        public void Dispose() {
        }
    }

    private sealed class VerboseLogger(string category) : ILogger {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug && AnyActive;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (!IsEnabled(logLevel)) {
                return;
            }

            string message = formatter(state, exception);
            if (exception is not null) {
                message += $" ({exception.Message})";
            }

            Publish($"SSH.NET {category}: {message}");
        }
    }
}
