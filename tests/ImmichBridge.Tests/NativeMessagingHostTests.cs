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
        Assert.True(reply["configured"]!.GetValue<bool>());
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

            Host = new NativeMessagingHost(new ConfigLoader(configPath), Launcher, Starter, ExecutablePath);
        }

        public string LocalFile { get; }

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
