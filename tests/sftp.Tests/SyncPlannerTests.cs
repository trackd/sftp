namespace sftp.Tests;

[TestClass]
public sealed class SyncPlannerTests {
    private static readonly DateTime Time = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static SyncEntry File(string path, long length = 10, int seconds = 0) => new(path, false, length, Time.AddSeconds(seconds));

    private static SyncEntry Folder(string path) => new(path, true, 0, Time);

    [TestMethod]
    public void CopiesMissingFilesAndCreatesMissingFoldersFirst() {
        List<SyncAction> plan = SyncPlanner.Plan([Folder("a"), Folder("a/b"), File("a/b/c.txt"), File("top.txt")], [], remove: false);

        CollectionAssert.AreEqual(
            new[] { "CreateFolder a", "CreateFolder a/b", "Copy a/b/c.txt", "Copy top.txt" },
            plan.Select(action => $"{action.Kind} {action.RelativePath}").ToArray());
    }

    [TestMethod]
    public void SkipsFilesWithTheSameSizeAndTime() {
        List<SyncAction> plan = SyncPlanner.Plan([File("same.txt"), File("close.txt", seconds: 1)], [File("same.txt"), File("close.txt")], remove: false);

        Assert.IsEmpty(plan);
    }

    [TestMethod]
    public void CopiesFilesWhoseSizeOrTimeDiffers() {
        List<SyncAction> plan = SyncPlanner.Plan(
            [File("size.txt", length: 11), File("time.txt", seconds: 60)],
            [File("size.txt"), File("time.txt")],
            remove: false);

        CollectionAssert.AreEqual(new[] { "Size differs", "Time differs" }, plan.Select(action => action.Reason).ToArray());
        Assert.IsTrue(plan.All(action => action.Kind == SyncActionKind.Copy));
    }

    [TestMethod]
    public void LeavesExtraItemsAloneWithoutRemove() {
        List<SyncAction> plan = SyncPlanner.Plan([File("keep.txt")], [File("keep.txt"), File("extra.txt"), Folder("old")], remove: false);

        Assert.IsEmpty(plan);
    }

    [TestMethod]
    public void RemovesExtraItemsDeepestFirstWithRemove() {
        List<SyncAction> plan = SyncPlanner.Plan(
            [File("keep.txt")],
            [File("keep.txt"), Folder("old"), Folder("old/inner"), File("old/inner/x.txt"), File("extra.txt")],
            remove: true);

        CollectionAssert.AreEqual(
            new[] { "old/inner/x.txt", "old/inner", "extra.txt", "old" },
            plan.Select(action => action.RelativePath).ToArray());
        Assert.IsTrue(plan.All(action => action.Kind == SyncActionKind.Remove));
    }

    [TestMethod]
    public void ReplacesAnItemWhoseTypeChangedOnlyWithRemove() {
        SyncEntry[] source = [File("thing")];
        SyncEntry[] target = [Folder("thing"), File("thing/inside.txt")];

        List<SyncAction> keep = SyncPlanner.Plan(source, target, remove: false);
        Assert.AreEqual("Copy thing Type differs", string.Join(" | ", keep.Select(a => $"{a.Kind} {a.RelativePath} {a.Reason}")));

        List<SyncAction> replace = SyncPlanner.Plan(source, target, remove: true);
        CollectionAssert.AreEqual(
            new[] { "Remove thing/inside.txt", "Remove thing", "Copy thing" },
            replace.Select(action => $"{action.Kind} {action.RelativePath}").ToArray());
    }

    [TestMethod]
    public void ComparesNamesCaseSensitively() {
        List<SyncAction> plan = SyncPlanner.Plan([File("Readme.txt")], [File("README.txt")], remove: true);

        CollectionAssert.AreEqual(
            new[] { "Remove README.txt", "Copy Readme.txt" },
            plan.Select(action => $"{action.Kind} {action.RelativePath}").ToArray());
    }
}
