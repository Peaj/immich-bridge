namespace ImmichBridge.Tests;

public sealed class PathMappingDeriverTests
{
    [Fact]
    public void Derive_UsesSharedRelativeSuffix()
    {
        var mapping = PathMappingDeriver.Derive(
            "/external/fotos/2026/03_29 Japan/PXL_20260402_072524748.RAW-01.COVER.jpg",
            @"M:\Fotos\2026\03_29 Japan\PXL_20260402_072524748.RAW-01.COVER.jpg");

        Assert.Equal("/external/fotos", mapping.RemotePrefix);
        Assert.Equal(Path.GetFullPath(@"M:\Fotos"), mapping.LocalPrefix);
        Assert.Equal(Path.Combine("2026", "03_29 Japan", "PXL_20260402_072524748.RAW-01.COVER.jpg"), mapping.SharedRelativePath);
    }

    [Fact]
    public void Derive_AllowsDifferentLocalLibraryFolderName()
    {
        var mapping = PathMappingDeriver.Derive(
            "/external/fotos/2026/03_29 Japan/image.jpg",
            @"D:\Immich External\2026\03_29 Japan\image.jpg");

        Assert.Equal("/external/fotos", mapping.RemotePrefix);
        Assert.Equal(Path.GetFullPath(@"D:\Immich External"), mapping.LocalPrefix);
    }

    [Fact]
    public void Derive_StripsQuotesAndNormalizesSlashes()
    {
        var mapping = PathMappingDeriver.Derive(
            "\"/external/fotos/2026/image.jpg\"",
            "'D:/Photos/2026/image.jpg'");

        Assert.Equal("/external/fotos", mapping.RemotePrefix);
        Assert.Equal(Path.GetFullPath(@"D:\Photos"), mapping.LocalPrefix);
    }

    [Fact]
    public void Derive_AvoidsBroadDriveRootMappingWhenAllLocalSegmentsMatch()
    {
        var mapping = PathMappingDeriver.Derive(
            "/fotos/2026/image.jpg",
            @"D:\Fotos\2026\image.jpg");

        Assert.Equal("/fotos", mapping.RemotePrefix);
        Assert.Equal(Path.GetFullPath(@"D:\Fotos"), mapping.LocalPrefix);
    }

    [Fact]
    public void Derive_RejectsPathsWithoutMatchingFileName()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => PathMappingDeriver.Derive(
            "/external/fotos/2026/image.jpg",
            @"D:\Photos\2026\other.jpg"));

        Assert.Contains("same file name", exception.Message);
    }
}
