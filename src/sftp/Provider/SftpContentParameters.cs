using System.Management.Automation;

namespace sftp;
/// <summary>The parameters Get-Content adds on Sftp drives.</summary>
public sealed class SftpContentParameters {
    /// <summary>The text encoding of the file. The default is UTF-8.</summary>
    [Parameter]
    [ValidateSet("utf8", "utf8BOM", "ascii", "unicode", "bigendianunicode", "utf32", "latin1")]
    public string Encoding { get; set; } = "utf8";

    /// <summary>Read bytes instead of text.</summary>
    [Parameter]
    public SwitchParameter AsByteStream { get; set; }

    /// <summary>Return the whole file as one string (or one byte array) instead of line by line.</summary>
    [Parameter]
    public SwitchParameter Raw { get; set; }
}
