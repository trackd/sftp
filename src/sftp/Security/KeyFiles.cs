namespace sftp;
/// <summary>Recognises SSH key files.</summary>
internal static class KeyFiles {
    private static readonly string[] s_publicKeyPrefixes = ["ssh-", "ecdsa-sha2-", "sk-ssh-", "sk-ecdsa-", "---- BEGIN SSH2 PUBLIC KEY"];

    /// <summary>The user's ~/.ssh folder, as completion sees it ($HOME first, then the profile folder).</summary>
    public static string SshFolder =>
        Path.Join(Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } home ? home : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");

    public static bool IsPublicKeyText(string text) {
        string start = text.TrimStart();

        return s_publicKeyPrefixes.Any(prefix => start.StartsWith(prefix, StringComparison.Ordinal));
    }

    public static bool IsPublicKey(string path) =>
        path.EndsWith(".pub", StringComparison.OrdinalIgnoreCase) || IsPublicKeyText(FirstLine(path));

    public static bool IsPrivateKey(string path) {
        string line = FirstLine(path);

        return line.Contains("PRIVATE KEY", StringComparison.Ordinal) || line.StartsWith("PuTTY-User-Key-File", StringComparison.Ordinal);
    }

    /// <summary>The private key next to a .pub file, if there is one.</summary>
    public static string? PrivateKeyFor(string publicKeyPath) {
        if (!publicKeyPath.EndsWith(".pub", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        string candidate = publicKeyPath[..^4];

        return File.Exists(candidate) && !IsPublicKey(candidate) ? candidate : null;
    }

    /// <summary>The private key files in a folder.</summary>
    public static IEnumerable<string> PrivateKeysIn(string folder) {
        if (!Directory.Exists(folder)) {
            return [];
        }

        return Directory.EnumerateFiles(folder)
            .Where(file => !file.EndsWith(".pub", StringComparison.OrdinalIgnoreCase) && IsPrivateKey(file))
            .Order(StringComparer.OrdinalIgnoreCase);
    }

    private static string FirstLine(string path) {
        try {
            using var reader = new StreamReader(path);
            char[] buffer = new char[200];
            int read = reader.Read(buffer, 0, buffer.Length);
            string text = new(buffer, 0, read);
            int newLine = text.IndexOfAny(['\r', '\n']);

            return newLine < 0 ? text : text[..newLine];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            return "";
        }
    }
}
