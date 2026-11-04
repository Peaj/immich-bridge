using System.Text.Json;
using System.Text.Json.Nodes;

namespace ImmichBridge.Tests;

public sealed class NativeMessagingHostTests
{
    private const string ExecutablePath = @"C:\Programs\ImmichBridge\ImmichBridge.exe";

    [Theory]
    [InlineData(new[] { "chrome-extension://ohghjemcjnaickehejdokfmjphpjoiag/", "--parent-window=0" }, true)]
    [InlineData(new[] { @"C:\Programs\ImmichBridge\de.peaj.immich_bridge.firefox.json", "immich-bridge@peaj.de" }, true)]
    [InlineData(new[] { @"C:\Other\host.json", "someone-else@example.com" }, false)]
    [InlineData(new[] { "immich-bridge://reveal?path=%2Fx.jpg" }, false)]
    [InlineData(new[] { "--setup" }, false)]
    public void IsHostLaunch_RecognizesBrowserLaunches(string[] args, bool expected)
    {
        Assert.Equal(expected, NativeMessagingHost.IsHostLaunch(args));
    }

    [Fact]
    public void Messages_RoundTripWithLengthPrefix()
    {
        using var stream = new MemoryStream();
        var large = new string('x', 200_000);

        NativeMessagingHost.WriteMessage(stream, "{\"type\":\"hello\"}");
        NativeMessagingHost.WriteMessage(stream, $"{{\"value\":\"{large}\"}}");
        stream.Position = 0;

        Assert.Equal(16, BitConverter.ToInt32(stream.ToArray(), 0));
        Assert.Equal("{\"type\":\"hello\"}", NativeMessagingHost.ReadMessage(stream));
        Assert.Contains(large, NativeMessagingHost.ReadMessage(stream));
        Assert.Null(NativeMessagingHost.ReadMessage(stream));
    }

    [Fact]
    public void Run_AnswersEveryMessageUntilInputEnds()
    {
        var fixture = new HostFixture();
        using var input = new MemoryStream();
        NativeMessagingHost.WriteMessage(input, "{\"type\":\"hello\"}");
        NativeMessagingHost.WriteMessage(input, "{\"type\":\"hello\"}");
        input.Position = 0;
        using var output = new MemoryStream();

        fixture.Host.Run(input, output);

        output.Position = 0;
        Assert.NotNull(NativeMessagingHost.ReadMessage(output));
        Assert.NotNull(NativeMessagingHost.ReadMessage(output));
        Assert.Null(NativeMessagingHost.ReadMessage(output));
    }

    [Fact]
    public void Hello_ReportsVersionActionsAndSetupState()
    {
        var fixture = new HostFixture();

        var reply = fixture.Host.Handle("{\"type\":\"hello\"}");

        Assert.True(reply["ok"]!.GetValue<bool>());
        Assert.Equal(NativeMessagingHost.ProtocolVersion, reply["protocol"]!.GetValue<int>());
        Assert.Equal(NativeMessagingHost.Version, reply["version"]!.GetValue<string>());
        Assert.Equal(["reveal", "copy", "open"], reply["actions"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(["copies"], reply["features"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.True(reply["configured"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("clipboard")]
    [InlineData("open")]
    public void Copy_WritesExactBytesAsReadOnlyCopy(string purpose)
    {
        var fixture = new HostFixture();
        var bytes = Enumerable.Range(0, 3_000_000).Select(index => (byte)(index % 251)).ToArray();

        Assert.True(fixture.Host.Handle(CopyStart(purpose, "IMG_1234.jpg", bytes.Length))["ok"]!.GetValue<bool>());
        foreach (var chunk in bytes.Chunk(1024 * 1024))
        {
            Assert.True(fixture.Host.Handle(CopyChunk(chunk))["ok"]!.GetValue<bool>());
        }
        var reply = fixture.Host.Handle("{\"type\":\"copy-end\"}");

        Assert.True(reply["ok"]!.GetValue<bool>());
        Assert.Equal("IMG_1234.jpg", reply["fileName"]!.GetValue<string>());
        var copy = Assert.Single(Directory.GetFiles(fixture.CopyStore.Root, "*", SearchOption.AllDirectories));
        Assert.Equal(bytes, File.ReadAllBytes(copy));
        Assert.True(File.GetAttributes(copy).HasFlag(FileAttributes.ReadOnly));

        if (purpose == "clipboard")
        {
            Assert.Equal(copy, fixture.Launcher.CopiedPath);
            Assert.Null(fixture.Starter.Arguments);
        }
        else
        {
            Assert.Equal($"--open-copy \"{copy}\"", fixture.Starter.Arguments);
            Assert.Null(fixture.Launcher.CopiedPath);
        }
    }

    [Fact]
    public void Copy_RejectsChunksWithoutStart()
    {
        var fixture = new HostFixture();

        Assert.False(fixture.Host.Handle(CopyChunk([1, 2, 3]))["ok"]!.GetValue<bool>());
        Assert.False(fixture.Host.Handle("{\"type\":\"copy-end\"}")["ok"]!.GetValue<bool>());
    }

    [Fact]
    public void Copy_DiscardsIncompleteCopies()
    {
        var fixture = new HostFixture();

        fixture.Host.Handle(CopyStart("open", "IMG.jpg", 10));
        fixture.Host.Handle(CopyChunk([1, 2, 3]));
        var reply = fixture.Host.Handle("{\"type\":\"copy-end\"}");

        Assert.Equal("failed", reply["code"]!.GetValue<string>());
        Assert.Null(fixture.Starter.Arguments);
        Assert.Empty(Directory.GetFiles(fixture.CopyStore.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Copy_DiscardsPartialCopyWhenTheBrowserDisconnects()
    {
        var fixture = new HostFixture();
        using var input = new MemoryStream();
        NativeMessagingHost.WriteMessage(input, CopyStart("open", "IMG.jpg", 10));
        NativeMessagingHost.WriteMessage(input, CopyChunk([1, 2, 3]));
        input.Position = 0;

        fixture.Host.Run(input, new MemoryStream());

        Assert.Empty(Directory.GetFiles(fixture.CopyStore.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Copy_RejectsUnknownPurpose()
    {
        var fixture = new HostFixture();

        var reply = fixture.Host.Handle(CopyStart("reveal", "IMG.jpg", 3));

        Assert.False(reply["ok"]!.GetValue<bool>());
    }

    private static string CopyStart(string purpose, string fileName, long size)
    {
        return new JsonObject { ["type"] = "copy-start", ["purpose"] = purpose, ["fileName"] = fileName, ["size"] = size }.ToJsonString();
    }

    private static string CopyChunk(byte[] data)
    {
        return new JsonObject { ["type"] = "copy-chunk", ["data"] = Convert.ToBase64String(data) }.ToJsonString();
    }

    [Fact]
    public void Hello_ReportsMissingSetup()
    {
        var fixture = new HostFixture(writeConfig: false);

        var reply = fixture.Host.Handle("{\"type\":\"hello\"}");

        Assert.False(reply["configured"]!.GetValue<bool>());
    }

    [Fact]
    public void Run_CopiesInsideTheHostWithoutNotice()
    {
        var fixture = new HostFixture();

        var reply = fixture.Host.Handle(RunMessage("copy", "/mnt/photos/img.jpg"));

        Assert.True(reply["ok"]!.GetValue<bool>());
        Assert.Equal(fixture.LocalFile, fixture.Launcher.CopiedPath);
        Assert.False(fixture.Launcher.NoticeShown);
        Assert.Null(fixture.Starter.Arguments);
    }

    [Theory]
    [InlineData("reveal")]
    [InlineData("open")]
    public void Run_StartsDetachedLinkLaunchForWindowActions(string action)
    {
        var fixture = new HostFixture();

        var reply = fixture.Host.Handle(RunMessage(action, "/mnt/photos/img.jpg"));

        Assert.True(reply["ok"]!.GetValue<bool>());
        Assert.Equal(ExecutablePath, fixture.Starter.ExecutablePath);
        Assert.Equal($"\"immich-bridge://{action}?path=%2Fmnt%2Fphotos%2Fimg.jpg\"", fixture.Starter.Arguments);
        Assert.Null(fixture.Launcher.CopiedPath);
    }

    [Theory]
    [InlineData("share", "/mnt/photos/img.jpg", "unsupported-action")]
    [InlineData("copy", "/elsewhere/img.jpg", "not-mapped")]
    [InlineData("copy", "/mnt/photos/missing.jpg", "file-missing")]
    public void Run_ReportsErrorCodes(string action, string path, string expectedCode)
    {
        var fixture = new HostFixture();

        var reply = fixture.Host.Handle(RunMessage(action, path));

        Assert.False(reply["ok"]!.GetValue<bool>());
        Assert.Equal(expectedCode, reply["code"]!.GetValue<string>());
        Assert.Null(fixture.Starter.Arguments);
        Assert.Null(fixture.Launcher.CopiedPath);
    }

    [Fact]
    public void Run_ReportsMissingSetup()
    {
        var fixture = new HostFixture(writeConfig: false);

        var reply = fixture.Host.Handle(RunMessage("copy", "/mnt/photos/img.jpg"));

        Assert.Equal("not-configured", reply["code"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"type\":\"delete-everything\"}")]
    public void Handle_RejectsMalformedMessages(string message)
    {
        var fixture = new HostFixture();

        var reply = fixture.Host.Handle(message);

        Assert.False(reply["ok"]!.GetValue<bool>());
        Assert.Null(fixture.Starter.Arguments);
    }

    private static string RunMessage(string action, string path)
    {
        return new JsonObject { ["type"] = "run", ["action"] = action, ["path"] = path }.ToJsonString();
    }

    private sealed class HostFixture
    {
        public HostFixture(bool writeConfig = true)
        {
            var root = Path.Combine(Path.GetTempPath(), "ImmichBridge.Tests", Guid.NewGuid().ToString("N"));
            var photos = Path.Combine(root, "Photos");
            Directory.CreateDirectory(photos);
            LocalFile = Path.Combine(photos, "img.jpg");
            File.WriteAllText(LocalFile, "jpg");

            var configPath = Path.Combine(root, "config.json");
            if (writeConfig)
            {
                File.WriteAllText(configPath, JsonSerializer.Serialize(new BridgeConfig
                {
                    Mappings = [new PathMapping { RemotePrefix = "/mnt/photos", LocalPrefix = photos }]
                }));
            }

            CopyStore = new CopyStore(Path.Combine(root, "Copies"));
            Directory.CreateDirectory(CopyStore.Root);
            Host = new NativeMessagingHost(new ConfigLoader(configPath), Launcher, Starter, ExecutablePath, copyStore: CopyStore);
        }

        public string LocalFile { get; }

        public CopyStore CopyStore { get; }

        public RecordingLauncher Launcher { get; } = new();

        public RecordingStarter Starter { get; } = new();

        public NativeMessagingHost Host { get; }
    }

    private sealed class RecordingStarter : IDetachedProcessStarter
    {
        public string? ExecutablePath { get; private set; }

        public string? Arguments { get; private set; }

        public void Start(string executablePath, string arguments)
        {
            ExecutablePath = executablePath;
            Arguments = arguments;
        }
    }

    private sealed class RecordingLauncher : IPlatformLauncher
    {
        public string? CopiedPath { get; private set; }

        public bool NoticeShown { get; private set; }

        public void RevealFile(string localPath)
        {
        }

        public void OpenWithSystemDialog(string localPath)
        {
        }

        public void OpenWithApp(string executablePath, string arguments, string localPath)
        {
        }

        public void CopyToClipboard(string localPath)
        {
            CopiedPath = localPath;
        }

        public void ShowCopyNotice(string localPath)
        {
            NoticeShown = true;
        }
    }
}
