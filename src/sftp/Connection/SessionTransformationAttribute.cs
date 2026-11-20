using System.Management.Automation;

namespace sftp;
/// <summary>Lets -Session take a session name as well as a session object.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
internal sealed class SessionTransformationAttribute : ArgumentTransformationAttribute {
    public override object? Transform(EngineIntrinsics engineIntrinsics, object? inputData) {
        object? value = inputData is PSObject wrapped ? wrapped.BaseObject : inputData;
        return value switch {
            null or SftpSession => value,
            string name => Resolve(name),
            System.Collections.IEnumerable items => items.Cast<object?>().Select(item => Transform(engineIntrinsics, item)).ToArray(),
            _ => value,
        };
    }

    private static SftpSession Resolve(string name) =>
        SessionRegistry.Current.Find(name) ??
        throw new ArgumentTransformationMetadataException($"No open session named '{name}'. It may have been disconnected; see Get-SftpSession, or connect again with Connect-SftpServer.");
}
