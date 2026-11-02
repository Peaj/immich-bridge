namespace ImmichBridge.Tests;

public sealed class ProtocolRequestParserTests
{
    [Fact]
    public void Parse_RevealRequiresPath()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ProtocolRequestParser.Parse("immich-bridge://reveal"));

        Assert.Contains("path", exception.Message);
    }

    [Fact]
    public void Parse_OpenAllowsMissingApp()
    {
        var request = ProtocolRequestParser.Parse("immich-bridge://open?path=%2Fmnt%2Fphotos%2Fimg.jpg");

        Assert.Equal(BridgeAction.Open, request.Action);
        Assert.Null(request.AppId);
        Assert.Equal("/mnt/photos/img.jpg", request.RemotePath);
    }

    [Fact]
    public void Parse_OpenReadsAppAndPath()
    {
        var request = ProtocolRequestParser.Parse("immich-bridge://open?app=photoshop&path=%2Fmnt%2Fphotos%2Fimg.jpg");

        Assert.Equal(BridgeAction.Open, request.Action);
        Assert.Equal("photoshop", request.AppId);
        Assert.Equal("/mnt/photos/img.jpg", request.RemotePath);
    }

    [Fact]
    public void Parse_CopyReadsPath()
    {
        var request = ProtocolRequestParser.Parse("immich-bridge://copy?path=%2Fmnt%2Fphotos%2Fimg.jpg");

        Assert.Equal(BridgeAction.Copy, request.Action);
        Assert.Null(request.AppId);
        Assert.Equal("/mnt/photos/img.jpg", request.RemotePath);
    }

    [Fact]
    public void Parse_RejectsUnknownAction()
    {
        var exception = Assert.Throws<UnsupportedActionException>(
            () => ProtocolRequestParser.Parse("immich-bridge://delete?path=%2Fmnt%2Fphotos%2Fimg.jpg"));

        Assert.Equal("delete", exception.ActionName);
        Assert.Contains("newer version", exception.Message);
    }

    [Fact]
    public void Parse_ShortensLongUnknownActionInMessage()
    {
        var actionName = new string('x', 60);

        var exception = Assert.Throws<UnsupportedActionException>(
            () => ProtocolRequestParser.Parse($"immich-bridge://{actionName}?path=%2Fmnt%2Fphotos%2Fimg.jpg"));

        Assert.DoesNotContain(actionName, exception.Message);
    }

    [Fact]
    public void Parse_RejectsNonBridgeScheme()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ProtocolRequestParser.Parse("other-scheme://reveal?path=%2Fmnt%2Fphotos%2Fimg.jpg"));

        Assert.Contains("Unsupported URI scheme", exception.Message);
    }
}
