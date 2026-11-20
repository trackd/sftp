using System.Management.Automation;

namespace sftp;
/// <summary>The parameters Set-Content and Add-Content add on Sftp drives.</summary>
public sealed class SftpContentWriterParameters {
    /// <summary>The text encoding of the file. The default is UTF-8 without a byte order mark.</summary>
    [Parameter]
    [ValidateSet("utf8", "utf8BOM", "ascii", "unicode", "bigendianunicode", "utf32", "latin1")]
    public string Encoding { get; set; } = "utf8";

    /// <summary>Write bytes instead of text.</summary>
    [Parameter]
    public SwitchParameter AsByteStream { get; set; }

    /// <summary>Do not add a line ending after each value.</summary>
    [Parameter]
    public SwitchParameter NoNewline { get; set; }
}
