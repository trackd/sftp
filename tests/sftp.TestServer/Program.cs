using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace sftp.TestServer;

/// <summary>Settings shared by all connections.</summary>
internal sealed class ServerOptions {
    public required string Root { get; init; }
    public required string User { get; init; }
    public string? Password { get; init; }
    public required IReadOnlyList<byte[]> AuthorizedKeys { get; init; }
    public bool PosixRename { get; init; } = true;
    public bool StatVfs { get; init; } = true;
    public System.Collections.Concurrent.ConcurrentDictionary<string, uint> Permissions { get; } = new(StringComparer.Ordinal);
    public required ECDsa HostKey { get; init; }
    public required byte[] HostKeyBlob { get; init; }
}

internal static class Program {
    /// <summary>
    /// sftp.TestServer --root DIR --user NAME [--password PW] [--authorized-key FILE.pub] [--port N] [--no-posix-rename] [--no-statvfs]
    /// Prints one JSON line with the port and host key fingerprint, then serves until stdin closes.
    /// </summary>
    private static int Main(string[] args) {
        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++) {
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) {
                arguments[args[i]] = args[++i];
            }
            else {
                flags.Add(args[i]);
            }
        }

        if (!arguments.TryGetValue("--root", out string? root) || !arguments.TryGetValue("--user", out string? user)) {
            Console.Error.WriteLine("Usage: sftp.TestServer --root DIR --user NAME [--password PW] [--authorized-key FILE.pub] [--port N] [--no-posix-rename] [--no-statvfs]");

            return 2;
        }

        Directory.CreateDirectory(root);
        var keys = new List<byte[]>();
        if (arguments.TryGetValue("--authorized-key", out string? keyFile)) {
            foreach (string line in File.ReadAllLines(keyFile)) {
                string[] fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length >= 2) {
                    keys.Add(Convert.FromBase64String(fields[1]));
                }
            }
        }

        var hostKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = hostKey.ExportParameters(false);
        byte[] hostKeyBlob = new SshWriter()
            .String("ecdsa-sha2-nistp256")
            .String("nistp256")
            .Binary([0x04, .. parameters.Q.X!, .. parameters.Q.Y!])
            .ToArray();
        var options = new ServerOptions {
            Root = Path.GetFullPath(root),
            User = user,
            Password = arguments.GetValueOrDefault("--password"),
            AuthorizedKeys = keys,
            PosixRename = !flags.Contains("--no-posix-rename"),
            StatVfs = !flags.Contains("--no-statvfs"),
            HostKey = hostKey,
            HostKeyBlob = hostKeyBlob,
        };

        var listener = new TcpListener(IPAddress.Loopback, int.Parse(arguments.GetValueOrDefault("--port", "0"), System.Globalization.CultureInfo.InvariantCulture));
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        string fingerprint = "ecdsa-sha2-nistp256 256 " + Convert.ToBase64String(SHA256.HashData(hostKeyBlob)).TrimEnd('=');
        Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object> { ["port"] = port, ["fingerprint"] = fingerprint }));
        Console.Out.Flush();

        // Stop when the parent closes stdin (or the process is killed).
        var shutdown = new Thread(() => {
            Console.In.ReadToEnd();
            listener.Stop();
        }) { IsBackground = true };
        shutdown.Start();

        try {
            while (true) {
                Socket socket = listener.AcceptSocket();
                socket.NoDelay = true;
                var connection = new SshConnection(socket, options);
                new Thread(connection.Run) { IsBackground = true, Name = "ssh connection" }.Start();
            }
        }
        catch (SocketException) {
            // Listener stopped.
        }

        return 0;
    }
}
