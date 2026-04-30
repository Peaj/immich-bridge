namespace ImmichBridge.Tests;

public sealed class PathMapperTests
{
    [Fact]
    public void MapPath_UsesLongestMatchingPrefix()
    {
        var mapper = new PathMapper(new BridgeConfig
        {
            Mappings =
            [
                new PathMapping { RemotePrefix = "/mnt/photos", LocalPrefix = @"Z:\Photos" },
                new PathMapping { RemotePrefix = "/mnt/photos/archive", LocalPrefix = @"Y:\Archive" }
            ]
        });

        var mapped = mapper.MapPath("/mnt/photos/archive/2024/img.jpg");

        Assert.Equal(Path.GetFullPath(@"Y:\Archive\2024\img.jpg"), mapped);
    }

    [Theory]
    [InlineData("/mnt/photos/2024/img.jpg")]
    [InlineData("\\mnt\\photos\\2024\\img.jpg")]
    [InlineData("/mnt/photos\\2024/img.jpg")]
    public void MapPath_NormalizesSlashes(string remotePath)
    {
        var mapper = new PathMapper(new BridgeConfig
        {
            Mappings =
            [
                new PathMapping { RemotePrefix = "/mnt/photos", LocalPrefix = @"Z:\Photos" }
            ]
        });

        var mapped = mapper.MapPath(remotePath);

        Assert.Equal(Path.GetFullPath(@"Z:\Photos\2024\img.jpg"), mapped);
    }

    [Fact]
    public void MapPath_RejectsUnmappedPathsByDefault()
    {
        var mapper = new PathMapper(new BridgeConfig
        {
            Mappings =
            [
                new PathMapping { RemotePrefix = "/mnt/photos", LocalPrefix = @"Z:\Photos" }
            ]
        });

        var exception = Assert.Throws<InvalidOperationException>(() => mapper.MapPath("/mnt/archive/img.jpg"));

        Assert.Contains("No mapping found", exception.Message);
    }

    [Fact]
    public void MapPath_RejectsTraversalOutsideLocalPrefix()
    {
        var mapper = new PathMapper(new BridgeConfig
        {
            Mappings =
            [
                new PathMapping { RemotePrefix = "/mnt/photos", LocalPrefix = @"Z:\Photos" }
            ]
        });

        var exception = Assert.Throws<InvalidOperationException>(() => mapper.MapPath("/mnt/photos/../secret.txt"));

        Assert.Contains("escapes local prefix", exception.Message);
    }
}
