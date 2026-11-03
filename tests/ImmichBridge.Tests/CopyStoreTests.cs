namespace ImmichBridge.Tests;

public sealed class CopyStoreTests
{
    [Theory]
    [InlineData("IMG_1234.jpg", "IMG_1234.jpg")]
    [InlineData("../../Windows/evil.exe", "evil.exe")]
    [InlineData(@"..\..\secret.txt", "secret.txt")]
    [InlineData("a:b*c?.png", "a_b_c_.png")]
    [InlineData("CON.jpg", "_CON.jpg")]
    [InlineData("", "immich-asset")]
    [InlineData(null, "immich-asset")]
    [InlineData("...", "immich-asset")]
    public void SanitizeFileName_KeepsOnlyASafeFileName(string? requested, string expected)
    {
        Assert.Equal(expected, CopyStore.SanitizeFileName(requested));
    }

    [Fact]
    public void SanitizeFileName_CapsLengthAndKeepsExtension()
    {
        var name = CopyStore.SanitizeFileName(new string('x', 300) + ".jpeg");

        Assert.Equal(120, name.Length);
        Assert.EndsWith(".jpeg", name);
    }

    [Fact]
    public void CreateCopyPath_UsesAFreshFolderInsideTheStore()
    {
        var store = new CopyStore(CreateRoot());

        var first = store.CreateCopyPath("IMG_1.jpg");
        var second = store.CreateCopyPath("IMG_1.jpg");

        Assert.NotEqual(Path.GetDirectoryName(first), Path.GetDirectoryName(second));
        Assert.Equal("IMG_1.jpg", Path.GetFileName(first));
        Assert.True(store.IsInsideCopies(first));
        Assert.True(Directory.Exists(Path.GetDirectoryName(first)));
    }

    [Fact]
    public void IsInsideCopies_RejectsOtherPaths()
    {
        var root = CreateRoot();
        var store = new CopyStore(root);

        Assert.False(store.IsInsideCopies(@"M:\Immich\library\img.jpg"));
        Assert.False(store.IsInsideCopies(root));
        Assert.False(store.IsInsideCopies(root + "-other\\img.jpg"));
        Assert.False(store.IsInsideCopies(Path.Combine(root, "..", "img.jpg")));
    }

    [Fact]
    public void DeleteOlderThan_RemovesExpiredReadOnlyCopiesAndKeepsRecentOnes()
    {
        var store = new CopyStore(CreateRoot());
        var expired = WriteCopy(store, "old.jpg");
        var recent = WriteCopy(store, "new.jpg");
        Directory.SetCreationTime(Path.GetDirectoryName(expired)!, DateTime.Now.AddDays(-8));

        store.DeleteOlderThan(CopyStore.RetentionPeriod, DateTime.Now);

        Assert.False(Directory.Exists(Path.GetDirectoryName(expired)));
        Assert.True(File.Exists(recent));
    }

    [Fact]
    public void DeleteOlderThan_SkipsCopiesThatAreStillOpen()
    {
        var store = new CopyStore(CreateRoot());
        var copy = WriteCopy(store, "open.jpg");
        Directory.SetCreationTime(Path.GetDirectoryName(copy)!, DateTime.Now.AddDays(-8));

        using (File.Open(copy, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            store.DeleteOlderThan(CopyStore.RetentionPeriod, DateTime.Now);
        }

        Assert.True(File.Exists(copy));
    }

    private static string WriteCopy(CopyStore store, string name)
    {
        var path = store.CreateCopyPath(name);
        File.WriteAllText(path, "copy");
        CopyStore.MarkReadOnly(path);
        return path;
    }

    private static string CreateRoot()
    {
        return Path.Combine(Path.GetTempPath(), "ImmichBridge.Tests", Guid.NewGuid().ToString("N"), "Copies");
    }
}
