namespace sftp.Tests;

[TestClass]
public sealed class SshConfigTests {
    private string _folder = "";

    [TestInitialize]
    public void CreateFolder() {
        _folder = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("n"), ".ssh");
        Directory.CreateDirectory(_folder);
    }

    [TestCleanup]
    public void RemoveFolder() => Directory.Delete(Path.GetDirectoryName(_folder)!, recursive: true);

    private string Write(string name, string text) {
        string path = Path.Join(_folder, name);
        File.WriteAllText(path, text.Replace("\r\n", "\n", StringComparison.Ordinal));

        return path;
    }

    [TestMethod]
    [DataRow("web", "web", true)]
    [DataRow("web*", "web01", true)]
    [DataRow("web?", "web1", true)]
    [DataRow("WEB", "web", true)]
    [DataRow("*.example.com !secret.example.com", "secret.example.com", false)]
    [DataRow("*.example.com !secret.example.com", "www.example.com", true)]
    [DataRow("db", "web", false)]
    public void MatchesHostPatterns(string patterns, string host, bool expected) =>
        Assert.AreEqual(expected, SshConfig.HostMatches(patterns, host));

    [TestMethod]
    public void UsesTheFirstValueAndCollectsIdentityFiles() {
        string config = Write("config", """
            # global settings come first
            ConnectTimeout 5

            Host web
                HostName 10.0.0.5
                User deploy
                Port 2222
                IdentityFile ~/.ssh/web_key

            Host *
                User fallback
                Port 22
                IdentityFile ~/.ssh/id_ed25519
                StrictHostKeyChecking accept-new
            """);

        SshHostConfig web = SshConfig.Resolve(config, "web", _folder);

        Assert.AreEqual("10.0.0.5", web.HostName);
        Assert.AreEqual("deploy", web.User);
        Assert.AreEqual(2222, web.Port);
        Assert.AreEqual(5, web.ConnectTimeout);
        Assert.AreEqual("accept-new", web.StrictHostKeyChecking);
        CollectionAssert.AreEqual(new[] { "~/.ssh/web_key", "~/.ssh/id_ed25519" }, web.IdentityFiles);

        SshHostConfig other = SshConfig.Resolve(config, "db", _folder);
        Assert.IsNull(other.HostName);
        Assert.AreEqual("fallback", other.User);
    }

    [TestMethod]
    public void ReadsIncludedFilesAndSkipsMatchBlocks() {
        Directory.CreateDirectory(Path.Join(_folder, "config.d"));
        Write(Path.Join("config.d", "work.conf"), "Host work\n  HostName work.example.com\n  User me\n");
        string config = Write("config", """
            Include config.d/*.conf
            Match exec "true"
              User never
            Host work
              Port=2200
            """);

        SshHostConfig work = SshConfig.Resolve(config, "work", _folder);

        Assert.AreEqual("work.example.com", work.HostName);
        Assert.AreEqual("me", work.User);
        Assert.AreEqual(2200, work.Port);
        CollectionAssert.AreEquivalent(new[] { "work" }, SshConfig.HostAliases(config, _folder).ToArray());
    }

    [TestMethod]
    public void IgnoresAMissingFile() {
        SshHostConfig none = SshConfig.Resolve(Path.Join(_folder, "missing"), "web", _folder);

        Assert.IsNull(none.HostName);
        Assert.IsEmpty(none.IdentityFiles);
    }

    [TestMethod]
    public void ExpandsTokens() {
        Assert.AreEqual("/home/me/.ssh/web_root", SshConfig.ExpandTokens("~/.ssh/%h_%r", "/home/me", "web", "root"));
        Assert.AreEqual("/home/me/keys/%", SshConfig.ExpandTokens("%d/keys/%%", "/home/me", "web", "root"));
    }
}
