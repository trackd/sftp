using System.Globalization;
using System.Text;

namespace sftp.TestServer;

/// <summary>
/// The SCP protocol behind "scp -t" (upload) and "scp -f" (download). Commands are split like a
/// POSIX shell would; any unquoted shell metacharacter is treated as an injection attempt and refused.
/// </summary>
internal static class Scp {
    public static Action<Channel>? Create(string command, ServerOptions options) {
        List<string>? words = SplitShellWords(command);
        if (words is null) {
            return channel => {
                // A real shell would have run the injected command; record that it happened.
                File.WriteAllText(Path.Join(options.Root, "injection-detected.txt"), command);
                channel.WriteError("refused: unquoted shell metacharacters\n");
                channel.Exit(127);
            };
        }

        switch (words.FirstOrDefault()) {
            // A few commands so Invoke-SshCommand can be tested: output, errors and exit codes.
            case "echo":
                return channel => {
                    channel.Write(Encoding.UTF8.GetBytes(string.Join(' ', words.Skip(1)) + "\n"));
                    channel.Exit(0);
                };
            case "fail":
                return channel => {
                    channel.WriteError(string.Join(' ', words.Skip(1)) + "\n");
                    channel.Exit(1);
                };
            case "exit" when words.Count == 2 && uint.TryParse(words[1], out uint code):
                return channel => channel.Exit(code);
            case "pwd":
                return channel => {
                    channel.Write("/\n"u8);
                    channel.Exit(0);
                };
        }

        if (words.Count < 2 || words[0] != "scp") {
            return null;
        }

        bool sink = false, source = false, preserve = false;
        string? path = null;
        foreach (string word in words.Skip(1)) {
            if (word.StartsWith('-') && path is null) {
                sink |= word.Contains('t', StringComparison.Ordinal);
                source |= word.Contains('f', StringComparison.Ordinal);
                preserve |= word.Contains('p', StringComparison.Ordinal);
            }
            else {
                path = word;
            }
        }

        if (path is null || sink == source) {
            return null;
        }

        string local = MapPath(options.Root, path);

        return sink ? channel => Receive(channel, local) : channel => Send(channel, local, preserve);
    }

    private static string MapPath(string root, string path) {
        var parts = new List<string>();
        foreach (string part in path.Split('/', StringSplitOptions.RemoveEmptyEntries)) {
            if (part == "..") {
                if (parts.Count > 0) {
                    parts.RemoveAt(parts.Count - 1);
                }
            }
            else if (part != ".") {
                parts.Add(part);
            }
        }

        return parts.Count == 0 ? root : Path.Join([root, .. parts]);
    }

    private static void Receive(Channel channel, string target) {
        channel.Write([0]);
        while (true) {
            string? line = ReadLine(channel);
            if (line is null) {
                break;
            }

            switch (line[0]) {
                case 'C': {
                    string[] fields = line[1..].Split(' ', 3);
                    long size = long.Parse(fields[1], CultureInfo.InvariantCulture);
                    string file = Directory.Exists(target) ? Path.Join(target, fields[2]) : target;
                    channel.Write([0]);
                    using (var stream = new FileStream(file, FileMode.Create, FileAccess.Write)) {
                        byte[] buffer = new byte[32 * 1024];
                        while (size > 0) {
                            int read = channel.Input.Read(buffer.AsSpan(0, (int)Math.Min(buffer.Length, size)));
                            if (read == 0) {
                                throw new EndOfStreamException();
                            }

                            stream.Write(buffer, 0, read);
                            size -= read;
                        }
                    }

                    channel.Input.ReadByte();
                    channel.Write([0]);
                    break;
                }
                case 'T' or 'D' or 'E':
                    channel.Write([0]);
                    break;
                default:
                    channel.Write([2, .. Encoding.UTF8.GetBytes("unsupported\n")]);
                    break;
            }
        }

        channel.Exit(0);
    }

    private static void Send(Channel channel, string file, bool preserve) {
        if (channel.Input.ReadByte() != 0) {
            channel.Exit(1);

            return;
        }

        if (!File.Exists(file)) {
            channel.Write([1, .. Encoding.UTF8.GetBytes($"scp: {Path.GetFileName(file)}: No such file or directory\n")]);
            channel.Exit(1);

            return;
        }

        var info = new FileInfo(file);
        if (preserve) {
            long time = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero).ToUnixTimeSeconds();
            channel.Write(Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"T{time} 0 {time} 0\n")));
            channel.Input.ReadByte();
        }

        channel.Write(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"C0644 {info.Length} {info.Name}\n")));
        channel.Input.ReadByte();
        channel.Write(File.ReadAllBytes(file));
        channel.Write([0]);
        channel.Input.ReadByte();
        channel.Exit(0);
    }

    private static string? ReadLine(Channel channel) {
        var bytes = new List<byte>();
        while (true) {
            int b = channel.Input.ReadByte();
            if (b < 0) {
                return bytes.Count == 0 ? null : Encoding.UTF8.GetString([.. bytes]);
            }

            if (b == '\n') {
                return Encoding.UTF8.GetString([.. bytes]);
            }

            bytes.Add((byte)b);
        }
    }

    /// <summary>Splits words like a POSIX shell; returns null when an unquoted metacharacter appears.</summary>
    internal static List<string>? SplitShellWords(string command) {
        var words = new List<string>();
        var current = new StringBuilder();
        bool inWord = false;
        for (int i = 0; i < command.Length; i++) {
            char c = command[i];
            switch (c) {
                case '\'': {
                    int end = command.IndexOf('\'', i + 1);
                    if (end < 0) {
                        return null;
                    }

                    current.Append(command, i + 1, end - i - 1);
                    i = end;
                    inWord = true;
                    break;
                }
                case '"': {
                    i++;
                    while (i < command.Length && command[i] != '"') {
                        if (command[i] == '\\' && i + 1 < command.Length && "\"\\$`".Contains(command[i + 1], StringComparison.Ordinal)) {
                            i++;
                        }
                        else if (command[i] is '$' or '`') {
                            return null;
                        }

                        current.Append(command[i]);
                        i++;
                    }

                    if (i >= command.Length) {
                        return null;
                    }

                    inWord = true;
                    break;
                }
                case '\\' when i + 1 < command.Length:
                    current.Append(command[++i]);
                    inWord = true;
                    break;
                case ' ' or '\t':
                    if (inWord) {
                        words.Add(current.ToString());
                        current.Clear();
                        inWord = false;
                    }

                    break;
                case ';' or '&' or '|' or '<' or '>' or '`' or '$' or '(' or ')' or '\n' or '*' or '?':
                    return null;
                default:
                    current.Append(c);
                    inWord = true;
                    break;
            }
        }

        if (inWord) {
            words.Add(current.ToString());
        }

        return words;
    }
}
