namespace sftp;
/// <summary>Helpers for SFTP paths, which always use forward slashes.</summary>
internal static class RemotePath {
    /// <summary>Joins a directory and a child name.</summary>
    public static string Combine(string directory, string name) {
        if (string.IsNullOrEmpty(directory) || directory == ".") {
            return name;
        }

        return directory.TrimEnd('/') + "/" + name.TrimStart('/');
    }

    /// <summary>
    /// Resolves "." and ".." segments and duplicate slashes in an absolute path, as a POSIX shell would.
    /// ".." never climbs above "/".
    /// </summary>
    public static string Normalize(string path) {
        List<string> parts = [];
        foreach (string part in path.Split('/', StringSplitOptions.RemoveEmptyEntries)) {
            if (part == ".") {
                continue;
            }

            if (part == "..") {
                if (parts.Count > 0) {
                    parts.RemoveAt(parts.Count - 1);
                }

                continue;
            }

            parts.Add(part);
        }

        return "/" + string.Join('/', parts);
    }

    /// <summary>Returns the last path segment.</summary>
    public static string GetName(string path) {
        string trimmed = path.Length > 1 ? path.TrimEnd('/') : path;
        if (trimmed == "/") {
            return trimmed;
        }

        int slash = trimmed.LastIndexOf('/');

        return slash < 0 ? trimmed : trimmed[(slash + 1)..];
    }

    /// <summary>Returns the parent directory, "/" for top-level items and "." for relative names.</summary>
    public static string GetDirectory(string path) {
        string trimmed = path.Length > 1 ? path.TrimEnd('/') : path;
        int slash = trimmed.LastIndexOf('/');

        return slash switch {
            < 0 => ".",
            0 => "/",
            _ => trimmed[..slash],
        };
    }
}
