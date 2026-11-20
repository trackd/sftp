using System.Globalization;
using System.Text;
using Renci.SshNet.Sftp;

namespace sftp;
/// <summary>Unix permission modes: octal (0644) and chmod-style symbolic (u+x,go-w) forms.</summary>
internal static class PermissionMode {
    private const int SetUid = 0x800, SetGid = 0x400, Sticky = 0x200;

    /// <summary>
    /// Works out the new permission bits. Octal modes replace the bits; symbolic modes change the current
    /// ones like chmod (an empty "who" means everyone).
    /// </summary>
    /// <exception cref="FormatException">The mode is not valid.</exception>
    public static int Apply(string mode, int current, bool isDirectory) {
        ArgumentNullException.ThrowIfNull(mode);
        string text = mode.Trim();
        if (text.Length is >= 1 and <= 4 && text.All(c => c is >= '0' and <= '7')) {
            return Convert.ToInt32(text, 8);
        }

        int result = current & 0xFFF;
        foreach (string clause in text.Split(',')) {
            result = ApplyClause(clause, result, isDirectory, mode);
        }

        return result;
    }

    private static int ApplyClause(string clause, int current, bool isDirectory, string mode) {
        int index = 0;
        int who = 0;
        while (index < clause.Length && clause[index] is 'u' or 'g' or 'o' or 'a') {
            who |= clause[index] switch {
                'u' => 0x9C0,
                'g' => 0x438,
                'o' => 0x207,
                _ => 0xFFF,
            };
            index++;
        }

        if (who == 0) {
            who = 0xFFF;
        }

        if (index >= clause.Length) {
            throw Invalid(mode);
        }

        while (index < clause.Length) {
            char op = clause[index++];
            if (op is not ('+' or '-' or '=')) {
                throw Invalid(mode);
            }

            int bits = 0;
            while (index < clause.Length && clause[index] is not ('+' or '-' or '=')) {
                bits |= clause[index++] switch {
                    'r' => 0x124,
                    'w' => 0x092,
                    'x' => 0x049,
                    // Execute only for folders and for files that are already executable by someone.
                    'X' => isDirectory || (current & 0x049) != 0 ? 0x049 : 0,
                    's' => SetUid | SetGid,
                    't' => Sticky,
                    _ => throw Invalid(mode),
                };
            }

            bits &= who;
            current = op switch {
                '+' => current | bits,
                '-' => current & ~bits,
                _ => (current & ~who) | bits,
            };
        }

        return current;
    }

    private static FormatException Invalid(string mode) =>
        new($"'{mode}' is not a permission mode. Use octal like 0644, or symbolic like u+x, go-w or u=rw,go=r.");

    /// <summary>The permission bits of an item, including setuid, setgid and sticky.</summary>
    public static int From(SftpFileAttributes attributes) {
        int bits = 0;
        bits |= attributes.IsUIDBitSet ? SetUid : 0;
        bits |= attributes.IsGroupIDBitSet ? SetGid : 0;
        bits |= attributes.IsStickyBitSet ? Sticky : 0;
        bits |= attributes.OwnerCanRead ? 0x100 : 0;
        bits |= attributes.OwnerCanWrite ? 0x80 : 0;
        bits |= attributes.OwnerCanExecute ? 0x40 : 0;
        bits |= attributes.GroupCanRead ? 0x20 : 0;
        bits |= attributes.GroupCanWrite ? 0x10 : 0;
        bits |= attributes.GroupCanExecute ? 0x8 : 0;
        bits |= attributes.OthersCanRead ? 0x4 : 0;
        bits |= attributes.OthersCanWrite ? 0x2 : 0;
        bits |= attributes.OthersCanExecute ? 0x1 : 0;

        return bits;
    }

    /// <summary>Sets the permission bits on attributes; only changed attributes are sent to the server.</summary>
    public static void Set(SftpFileAttributes attributes, int bits) {
        attributes.IsUIDBitSet = (bits & SetUid) != 0;
        attributes.IsGroupIDBitSet = (bits & SetGid) != 0;
        attributes.IsStickyBitSet = (bits & Sticky) != 0;
        attributes.OwnerCanRead = (bits & 0x100) != 0;
        attributes.OwnerCanWrite = (bits & 0x80) != 0;
        attributes.OwnerCanExecute = (bits & 0x40) != 0;
        attributes.GroupCanRead = (bits & 0x20) != 0;
        attributes.GroupCanWrite = (bits & 0x10) != 0;
        attributes.GroupCanExecute = (bits & 0x8) != 0;
        attributes.OthersCanRead = (bits & 0x4) != 0;
        attributes.OthersCanWrite = (bits & 0x2) != 0;
        attributes.OthersCanExecute = (bits & 0x1) != 0;
    }

    /// <summary>Four-digit octal, for example 0644.</summary>
    public static string ToOctal(int bits) => Convert.ToString(bits & 0xFFF, 8).PadLeft(4, '0');

    /// <summary>The ls -l form, for example drwxr-xr-x.</summary>
    public static string ToSymbolic(int bits, bool isDirectory) {
        var text = new StringBuilder(10);
        text.Append(isDirectory ? 'd' : '-');
        AppendTriplet(text, bits >> 6, (bits & SetUid) != 0, 's');
        AppendTriplet(text, bits >> 3, (bits & SetGid) != 0, 's');
        AppendTriplet(text, bits, (bits & Sticky) != 0, 't');

        return text.ToString();
    }

    private static void AppendTriplet(StringBuilder text, int bits, bool special, char specialChar) {
        text.Append((bits & 4) != 0 ? 'r' : '-');
        text.Append((bits & 2) != 0 ? 'w' : '-');
        bool execute = (bits & 1) != 0;
        text.Append(special ? (execute ? specialChar : char.ToUpper(specialChar, CultureInfo.InvariantCulture)) : execute ? 'x' : '-');
    }
}
