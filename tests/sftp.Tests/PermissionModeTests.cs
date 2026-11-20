namespace sftp.Tests;

[TestClass]
public sealed class PermissionModeTests {
    [TestMethod]
    [DataRow("0644", 0x1A4)]
    [DataRow("644", 0x1A4)]
    [DataRow("755", 0x1ED)]
    [DataRow("4755", 0x9ED)]
    [DataRow("1777", 0x3FF)]
    [DataRow("0", 0)]
    public void OctalModesReplaceTheBits(string mode, int expected) =>
        Assert.AreEqual(expected, PermissionMode.Apply(mode, 0x1FF, isDirectory: false));

    [TestMethod]
    [DataRow("u+x", 0x1A4, 0x1E4)]
    [DataRow("go-r", 0x1A4, 0x180)]
    [DataRow("a+x", 0x1A4, 0x1ED)]
    [DataRow("+x", 0x1A4, 0x1ED)]
    [DataRow("u=rw,go=r", 0x1FF, 0x1A4)]
    [DataRow("o=", 0x1FF, 0x1F8)]
    [DataRow("ug+w,o-rwx", 0x124, 0x1B0)]
    [DataRow("u+s", 0x1ED, 0x9ED)]
    [DataRow("g+s", 0x1ED, 0x5ED)]
    [DataRow("+t", 0x1FF, 0x3FF)]
    [DataRow("u-x+w", 0x140, 0x180)]
    public void SymbolicModesChangeTheCurrentBits(string mode, int current, int expected) =>
        Assert.AreEqual(expected, PermissionMode.Apply(mode, current, isDirectory: false), PermissionMode.ToOctal(PermissionMode.Apply(mode, current, false)));

    [TestMethod]
    public void CapitalXOnlyAddsExecuteToFoldersAndExecutableFiles() {
        Assert.AreEqual(0x1ED, PermissionMode.Apply("a+X", 0x1A4, isDirectory: true));
        Assert.AreEqual(0x1A4, PermissionMode.Apply("a+X", 0x1A4, isDirectory: false));
        Assert.AreEqual(0x1ED, PermissionMode.Apply("a+X", 0x1E4, isDirectory: false));
    }

    [TestMethod]
    public void EqualsClearsSetUidForTheOwner() =>
        Assert.AreEqual(0x1ED, PermissionMode.Apply("u=rwx", 0x9ED, isDirectory: false));

    [TestMethod]
    [DataRow("")]
    [DataRow("u")]
    [DataRow("u+q")]
    [DataRow("z+x")]
    [DataRow("0888")]
    [DataRow("12345")]
    [DataRow("u+x,")]
    public void InvalidModesAreRejected(string mode) =>
        Assert.ThrowsExactly<FormatException>(() => PermissionMode.Apply(mode, 0x1A4, isDirectory: false));

    [TestMethod]
    [DataRow(0x1A4, false, "-rw-r--r--")]
    [DataRow(0x1ED, true, "drwxr-xr-x")]
    [DataRow(0x9ED, false, "-rwsr-xr-x")]
    [DataRow(0x5A4, false, "-rw-r-Sr--")]
    [DataRow(0x3FF, true, "drwxrwxrwt")]
    [DataRow(0x3FE, true, "drwxrwxrwT")]
    public void FormatsLikeLs(int bits, bool isDirectory, string expected) =>
        Assert.AreEqual(expected, PermissionMode.ToSymbolic(bits, isDirectory));

    [TestMethod]
    public void FormatsOctalWithFourDigits() {
        Assert.AreEqual("0644", PermissionMode.ToOctal(0x1A4));
        Assert.AreEqual("4755", PermissionMode.ToOctal(0x9ED));
        Assert.AreEqual("0000", PermissionMode.ToOctal(0));
    }
}
