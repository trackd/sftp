using System.Text;

namespace sftp;
/// <summary>Maps the -Encoding names to encodings.</summary>
internal static class TextEncoding {
    public static Encoding From(string name) => name.ToLowerInvariant() switch {
        "utf8" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        "utf8bom" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        "ascii" => System.Text.Encoding.ASCII,
        "unicode" => System.Text.Encoding.Unicode,
        "bigendianunicode" => System.Text.Encoding.BigEndianUnicode,
        "utf32" => System.Text.Encoding.UTF32,
        "latin1" => System.Text.Encoding.Latin1,
        _ => throw new ArgumentException($"Unknown encoding '{name}'.", nameof(name)),
    };
}
