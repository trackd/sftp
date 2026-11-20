namespace sftp.Tests;

[TestClass]
public sealed class HostKeyFingerprintTests {
    private const string Sha256 = "X5cCJufoqU3+Or4iPYZA7O2Ztw7v49wVFfGh9+ViYOc";
    private const string Md5 = "58:d2:ac:e6:83:92:39:8f:1c:03:d5:a9:12:0a:64:71";
    private static readonly SshHostKey s_key = new("host", 22, "ssh-ed25519", 255, Sha256, Md5);

    [TestMethod]
    [DataRow("ssh-ed25519 255 " + Sha256)]
    [DataRow("SHA256:" + Sha256)]
    [DataRow("sha256:" + Sha256)]
    [DataRow(Sha256)]
    [DataRow(Sha256 + "=")]
    [DataRow("  ecdsa-sha2-nistp384 384 " + Sha256 + "  ")]
    public void AcceptsSha256Forms(string text) {
        var fingerprint = HostKeyFingerprint.Parse(text);

        Assert.IsFalse(fingerprint.IsMd5);
        Assert.IsTrue(fingerprint.Matches(s_key));
    }

    [TestMethod]
    [DataRow("ssh-rsa 2048 " + Md5)]
    [DataRow("MD5:" + Md5)]
    [DataRow("58:D2:AC:E6:83:92:39:8F:1C:03:D5:A9:12:0A:64:71")]
    public void AcceptsMd5Forms(string text) {
        var fingerprint = HostKeyFingerprint.Parse(text);

        Assert.IsTrue(fingerprint.IsMd5);
        Assert.IsTrue(fingerprint.Matches(s_key));
    }

    [TestMethod]
    public void RejectsADifferentKey() {
        var other = HostKeyFingerprint.Parse("ssh-ed25519 255 Y5cCJufoqU3+Or4iPYZA7O2Ztw7v49wVFfGh9+ViYOc");

        Assert.IsFalse(other.Matches(s_key));
    }

    [TestMethod]
    public void ShaComparisonIsCaseSensitive() {
        var lower = HostKeyFingerprint.Parse(Sha256.ToLowerInvariant());

        Assert.IsFalse(lower.Matches(s_key));
    }

    [TestMethod]
    [DataRow("not-a-fingerprint")]
    [DataRow("ssh-ed25519 255 tooShort")]
    [DataRow("58:d2:ac")]
    public void RejectsMalformedInput(string text) =>
        Assert.ThrowsExactly<FormatException>(() => HostKeyFingerprint.Parse(text));

    [TestMethod]
    public void RejectsEmptyInput() =>
        Assert.ThrowsExactly<ArgumentException>(() => HostKeyFingerprint.Parse(" "));

    [TestMethod]
    public void DescribesTheKeyInWinScpAndOpenSshForms() {
        Assert.AreEqual("ssh-ed25519 255 " + Sha256, s_key.Fingerprint);
        Assert.AreEqual("SHA256:" + Sha256, s_key.OpenSshFingerprint);
    }
}

[TestClass]
public sealed class RemotePathTests {
    [TestMethod]
    [DataRow("/upload", "a.txt", "/upload/a.txt")]
    [DataRow("/upload/", "a.txt", "/upload/a.txt")]
    [DataRow("/", "a.txt", "/a.txt")]
    [DataRow(".", "a.txt", "a.txt")]
    [DataRow("", "a.txt", "a.txt")]
    [DataRow("in", "/a.txt", "in/a.txt")]
    public void Combines(string directory, string name, string expected) =>
        Assert.AreEqual(expected, RemotePath.Combine(directory, name));

    [TestMethod]
    [DataRow("/upload/a.txt", "a.txt", "/upload")]
    [DataRow("/a.txt", "a.txt", "/")]
    [DataRow("a.txt", "a.txt", ".")]
    [DataRow("/upload/sub/", "sub", "/upload")]
    [DataRow("/", "/", "/")]
    public void SplitsNameAndDirectory(string path, string name, string directory) {
        Assert.AreEqual(name, RemotePath.GetName(path));
        Assert.AreEqual(directory, RemotePath.GetDirectory(path));
    }
}

[TestClass]
public sealed class RemotePathNormalizeTests {
    [TestMethod]
    [DataRow("/root/./foo.psd1", "/root/foo.psd1")]
    [DataRow("/root/sub/../foo.psd1", "/root/foo.psd1")]
    [DataRow("/root//sub///foo", "/root/sub/foo")]
    [DataRow("/../../etc", "/etc")]
    [DataRow("/root/.", "/root")]
    [DataRow("/", "/")]
    public void ResolvesDotSegments(string path, string expected) =>
        Assert.AreEqual(expected, RemotePath.Normalize(path));
}

[TestClass]
public sealed class KeyFilesTests {
    [TestMethod]
    [DataRow("ssh-rsa AAAAB3NzaC1yc2E user@host", true)]
    [DataRow("ecdsa-sha2-nistp256 AAAA user@host", true)]
    [DataRow("---- BEGIN SSH2 PUBLIC KEY ----", true)]
    [DataRow("-----BEGIN OPENSSH PRIVATE KEY-----", false)]
    [DataRow("PuTTY-User-Key-File-3: ssh-ed25519", false)]
    public void RecognisesPublicKeyText(string text, bool expected) =>
        Assert.AreEqual(expected, KeyFiles.IsPublicKeyText(text));

    [TestMethod]
    public void FindsThePrivateKeyNextToAPublicKey() {
        string folder = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(folder);
        try {
            File.WriteAllText(Path.Join(folder, "id_rsa"), "-----BEGIN OPENSSH PRIVATE KEY-----\n");
            File.WriteAllText(Path.Join(folder, "id_rsa.pub"), "ssh-rsa AAAA user@host\n");
            File.WriteAllText(Path.Join(folder, "lonely.pub"), "ssh-rsa AAAA user@host\n");
            File.WriteAllText(Path.Join(folder, "known_hosts"), "host ssh-rsa AAAA\n");

            Assert.AreEqual(Path.Join(folder, "id_rsa"), KeyFiles.PrivateKeyFor(Path.Join(folder, "id_rsa.pub")));
            Assert.IsNull(KeyFiles.PrivateKeyFor(Path.Join(folder, "lonely.pub")));
            CollectionAssert.AreEqual(new[] { Path.Join(folder, "id_rsa") }, KeyFiles.PrivateKeysIn(folder).ToArray());
        }
        finally {
            Directory.Delete(folder, recursive: true);
        }
    }
}
