namespace sftp;
/// <summary>An SSH server host key and its fingerprints.</summary>
/// <param name="HostName">The server that presented the key.</param>
/// <param name="Port">The server port.</param>
/// <param name="Algorithm">The host key algorithm, for example ssh-ed25519.</param>
/// <param name="KeyLength">The key length in bits.</param>
/// <param name="Sha256">The unpadded Base64 SHA-256 fingerprint.</param>
/// <param name="Md5">The colon-separated hexadecimal MD5 fingerprint (legacy).</param>
/// <param name="KeyType">The key type, for example ssh-rsa; empty when unknown.</param>
public sealed record SshHostKey(string HostName, int Port, string Algorithm, int KeyLength, string Sha256, string Md5, string KeyType = "") {
    /// <summary>The fingerprint in "algorithm bits sha256" form, accepted by -HostKeyFingerprint.</summary>
    public string Fingerprint => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{(KeyType.Length > 0 ? KeyType : Algorithm)} {KeyLength} {Sha256}");

    /// <summary>The fingerprint in OpenSSH "SHA256:..." form.</summary>
    public string OpenSshFingerprint => "SHA256:" + Sha256;
}
