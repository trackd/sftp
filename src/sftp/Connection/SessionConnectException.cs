using System.Management.Automation;

namespace sftp;
/// <summary>A connection failure, with the error record the calling command should report.</summary>
internal sealed class SessionConnectException(ErrorRecord record) : Exception(record.Exception.Message, record.Exception) {
    public ErrorRecord ErrorRecord { get; } = record;
}
