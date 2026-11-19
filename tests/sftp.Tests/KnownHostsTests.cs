using System.Security.Cryptography;
using System.Text;

namespace sftp.Tests;

[TestClass]
public sealed class KnownHostsTests {
    private static readonly byte[] s_ed25519 = Blob("ssh-ed25519", 1);
    private static readonly byte[] s_rsa = Blob("ssh-rsa", 2);

    private static byte[] Blob(string keyType, byte seed) {
        byte[] name = Encoding.ASCII.GetBytes(keyType);
        byte[] blob = new byte[4 + name.Length + 32];
        blob[3] = (byte)name.Length;
        name.CopyTo(blob, 4);
        blob.AsSpan(4 + name.Length).Fill(seed);

        return blob;
    }

    private static string Line(string hosts, byte[] blob) => $"{hosts} {HostKeyVerifier.KeyTypeOf(blob)} {Convert.ToBase64String(blob)}";

    private static SshHostKey Key(string host = "sftp.example.com", int port = 22) => new(host, port, "ssh-ed25519", 255, new string('A', 43), "", "ssh-ed25519");

    [TestMethod]
    [DataRow("sftp.example.com", "sftp.example.com", 22, true)]
    [DataRow("SFTP.Example.com", "sftp.example.com", 22, true)]
    [DataRow("other.example.com,sftp.example.com", "sftp.example.com", 22, true)]
    [DataRow("sftp.example.com", "sftp.example.com", 2222, false)]
    [DataRow("[sftp.example.com]:2222", "sftp.example.com", 2222, true)]
    [DataRow("*.example.com", "sftp.example.com", 22, true)]
    [DataRow("sftp?.example.com", "sftp1.example.com", 22, true)]
    [DataRow("*.example.com,!sftp.example.com", "sftp.example.com", 22, false)]
    [DataRow("*.example.org", "sftp.example.com", 22, false)]
    public void MatchesHostPatterns(string patterns, string host, int port, bool expected) {
        KnownHostEntry entry = KnownHosts.Parse([Line(patterns, s_ed25519)]).Single();

        Assert.AreEqual(expected, entry.Matches(KnownHosts.HostField(host, port)));
    }

    [TestMethod]
    public void MatchesHashedHostNames() {
        byte[] salt = RandomNumberGenerator.GetBytes(20);
#pragma warning disable CA5350 // OpenSSH's hashed host format.
        byte[] hash = HMACSHA1.HashData(salt, Encoding.UTF8.GetBytes("[sftp.example.com]:2222"));
#pragma warning restore CA5350
        string hashed = $"|1|{Convert.ToBase64String(salt)}|{Convert.ToBase64String(hash)}";
        KnownHostEntry entry = KnownHosts.Parse([Line(hashed, s_ed25519)]).Single();

        Assert.IsTrue(entry.Matches("[sftp.example.com]:2222"));
        Assert.IsFalse(entry.Matches("sftp.example.com"));
    }

    [TestMethod]
    public void SkipsCommentsBlankLinesCertificateAuthoritiesAndGarbage() {
        string[] lines = [
            "# comment",
            "",
            "@cert-authority *.example.com " + Line("x", s_ed25519).Split(' ', 2)[1],
            "sftp.example.com ssh-ed25519 not-base64!",
            "too-short",
            Line("sftp.example.com", s_ed25519),
        ];

        Assert.AreEqual(1, KnownHosts.Parse(lines).Count());
    }

    [TestMethod]
    public void AddsAnUnknownHostOnFirstUseAndTrustsItAfterwards() {
        string path = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "known_hosts");
        try {
            var first = new KnownHostsVerifier(path, "sftp.example.com", 2222);
            Assert.IsTrue(first.Verify(Key(port: 2222), s_ed25519));
            Assert.AreEqual("KnownHostsAdded", first.Mode);
            Assert.IsNotNull(first.Commit());
            StringAssert.StartsWith(File.ReadAllText(path), "[sftp.example.com]:2222 ssh-ed25519 ");

            var second = new KnownHostsVerifier(path, "sftp.example.com", 2222);
            CollectionAssert.AreEqual(new[] { "ssh-ed25519" }, second.KeyTypes.ToArray());
            Assert.IsTrue(second.Verify(Key(port: 2222), s_ed25519));
            Assert.AreEqual("KnownHosts", second.Mode);
            Assert.IsNull(second.Commit());
            Assert.IsFalse(second.Verify(Key(port: 2222), Blob("ssh-ed25519", 9)), "a changed key must be refused");
        }
        finally {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [TestMethod]
    public void RefusesRevokedKeys() {
        string path = Path.GetTempFileName();
        try {
            File.WriteAllLines(path, ["@revoked " + Line("sftp.example.com", s_ed25519)]);
            var verifier = new KnownHostsVerifier(path, "sftp.example.com", 22);

            Assert.IsFalse(verifier.Verify(Key(), s_ed25519));
        }
        finally {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void AppendsOnANewLineWhenTheFileLacksATrailingNewLine() {
        string path = Path.GetTempFileName();
        try {
            File.WriteAllText(path, Line("other.example.com", s_rsa));
            var verifier = new KnownHostsVerifier(path, "sftp.example.com", 22);
            verifier.Verify(Key(), s_ed25519);
            verifier.Commit();

            Assert.HasCount(2, File.ReadAllLines(path));
        }
        finally {
            File.Delete(path);
        }
    }
}

[TestClass]
public sealed class HostKeyAlgorithmTests {
    [TestMethod]
    [DataRow("rsa-sha2-512", "ssh-rsa")]
    [DataRow("rsa-sha2-256-cert-v01@openssh.com", "ssh-rsa")]
    [DataRow("ssh-ed25519-cert-v01@openssh.com", "ssh-ed25519")]
    [DataRow("ecdsa-sha2-nistp384", "ecdsa-sha2-nistp384")]
    public void MapsAlgorithmsToKeyTypes(string algorithm, string keyType) =>
        Assert.AreEqual(keyType, HostKeyVerifier.KeyTypeOfAlgorithm(algorithm));

    [TestMethod]
    public void RestrictsNegotiationToTheExpectedKeyTypes() {
        var algorithms = new Dictionary<string, int> { ["ssh-ed25519"] = 1, ["ecdsa-sha2-nistp256"] = 2, ["rsa-sha2-512"] = 3, ["ssh-rsa"] = 4 };
        HostKeyVerifier.Restrict(algorithms, ["ssh-rsa"]);

        CollectionAssert.AreEquivalent(new[] { "rsa-sha2-512", "ssh-rsa" }, algorithms.Keys.ToArray());
    }

    [TestMethod]
    public void LeavesNegotiationAloneWhenNothingWouldRemain() {
        var algorithms = new Dictionary<string, int> { ["ssh-ed25519"] = 1 };
        HostKeyVerifier.Restrict(algorithms, ["ssh-dss"]);

        Assert.HasCount(1, algorithms);
    }

    [TestMethod]
    public void FingerprintsNameTheirKeyType() {
        Assert.AreEqual("ssh-rsa", HostKeyFingerprint.Parse("ssh-rsa 2048 58:d2:ac:e6:83:92:39:8f:1c:03:d5:a9:12:0a:64:71").KeyType);
        Assert.AreEqual("ssh-rsa", HostKeyFingerprint.Parse("rsa-sha2-512 2048 X5cCJufoqU3+Or4iPYZA7O2Ztw7v49wVFfGh9+ViYOc").KeyType);
        Assert.IsNull(HostKeyFingerprint.Parse("SHA256:X5cCJufoqU3+Or4iPYZA7O2Ztw7v49wVFfGh9+ViYOc").KeyType);
    }

    [TestMethod]
    public void RestrictsOnlyWhenEveryFingerprintNamesItsType() {
        var named = new FingerprintVerifier([HostKeyFingerprint.Parse("ssh-ed25519 255 X5cCJufoqU3+Or4iPYZA7O2Ztw7v49wVFfGh9+ViYOc")]);
        var mixed = new FingerprintVerifier([
            HostKeyFingerprint.Parse("ssh-ed25519 255 X5cCJufoqU3+Or4iPYZA7O2Ztw7v49wVFfGh9+ViYOc"),
            HostKeyFingerprint.Parse("SHA256:Y5cCJufoqU3+Or4iPYZA7O2Ztw7v49wVFfGh9+ViYOc"),
        ]);

        CollectionAssert.AreEqual(new[] { "ssh-ed25519" }, named.KeyTypes.ToArray());
        Assert.IsEmpty(mixed.KeyTypes);
    }
}
