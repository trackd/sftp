namespace sftp;
/// <summary>A file or folder in a tree being synchronized, by its path relative to the tree's root (with / separators).</summary>
internal sealed record SyncEntry(string RelativePath, bool IsDirectory, long Length, DateTime LastWriteTimeUtc);

/// <summary>What Sync-SftpFolder does to one item.</summary>
internal enum SyncActionKind {
    /// <summary>Delete an item that is only in the destination.</summary>
    Remove,

    /// <summary>Create a folder that is missing in the destination.</summary>
    CreateFolder,

    /// <summary>Copy a file that is missing or different in the destination.</summary>
    Copy,
}

/// <summary>One step of a synchronization.</summary>
internal sealed record SyncAction(SyncActionKind Kind, string RelativePath, bool IsDirectory, string Reason);

/// <summary>Compares two folder trees by size and modification time and works out what to copy and delete.</summary>
internal static class SyncPlanner {
    /// <summary>
    /// Modification times closer than this count as equal. SFTP version 3 only carries whole seconds, and some
    /// file systems round to two seconds.
    /// </summary>
    public static readonly TimeSpan TimeTolerance = TimeSpan.FromSeconds(2);

    /// <summary>True when the destination file is up to date: same size and the same modification time.</summary>
    public static bool IsSame(SyncEntry source, SyncEntry target) =>
        source.Length == target.Length && (source.LastWriteTimeUtc - target.LastWriteTimeUtc).Duration() < TimeTolerance;

    /// <summary>
    /// The steps that make the destination match the source: removals first (deepest first, only with
    /// <paramref name="remove"/>), then missing folders (shallowest first), then files to copy.
    /// </summary>
    public static List<SyncAction> Plan(IEnumerable<SyncEntry> source, IEnumerable<SyncEntry> target, bool remove) {
        Dictionary<string, SyncEntry> sources = source.ToDictionary(entry => entry.RelativePath, StringComparer.Ordinal);
        Dictionary<string, SyncEntry> targets = target.ToDictionary(entry => entry.RelativePath, StringComparer.Ordinal);
        List<SyncAction> removals = [];
        List<SyncAction> folders = [];
        List<SyncAction> copies = [];
        HashSet<string> removed = new(StringComparer.Ordinal);
        if (remove) {
            foreach (SyncEntry entry in targets.Values) {
                bool keep = sources.TryGetValue(entry.RelativePath, out SyncEntry? match) && match.IsDirectory == entry.IsDirectory;
                if (!keep) {
                    removals.Add(new SyncAction(SyncActionKind.Remove, entry.RelativePath, entry.IsDirectory, match is null ? "Not in source" : "Type differs"));
                    removed.Add(entry.RelativePath);
                }
            }
        }

        foreach (SyncEntry entry in sources.Values) {
            bool exists = targets.TryGetValue(entry.RelativePath, out SyncEntry? existing) && !removed.Contains(entry.RelativePath);
            if (entry.IsDirectory) {
                if (!exists) {
                    folders.Add(new SyncAction(SyncActionKind.CreateFolder, entry.RelativePath, true, "Missing"));
                }

                continue;
            }

            if (!exists) {
                copies.Add(new SyncAction(SyncActionKind.Copy, entry.RelativePath, false, "Missing"));
            }
            else if (existing!.IsDirectory) {
                copies.Add(new SyncAction(SyncActionKind.Copy, entry.RelativePath, false, "Type differs"));
            }
            else if (!IsSame(entry, existing)) {
                copies.Add(new SyncAction(SyncActionKind.Copy, entry.RelativePath, false, entry.Length != existing.Length ? "Size differs" : "Time differs"));
            }
        }

        return [
            .. removals.OrderByDescending(Depth).ThenBy(action => action.RelativePath, StringComparer.Ordinal),
            .. folders.OrderBy(Depth).ThenBy(action => action.RelativePath, StringComparer.Ordinal),
            .. copies.OrderBy(action => action.RelativePath, StringComparer.Ordinal),
        ];
    }

    private static int Depth(SyncAction action) => action.RelativePath.Count(c => c == '/');
}
