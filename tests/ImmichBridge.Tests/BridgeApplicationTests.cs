using System.Text.Json;

namespace ImmichBridge.Tests;

public sealed class BridgeApplicationTests
{
    [Fact]
    public void Run_OpenRejectsUnknownConfiguredApp()
    {
        var configPath = WriteConfig(new BridgeConfig
        {
            Mappings =
            [
                new PathMapping { RemotePrefix = "/mnt/photos", LocalPrefix = @"Z:\Photos" }
            ],
            Options = new BridgeOptions { VerifyLocalFileExists = false }
        });

        var app = new BridgeApplication(
            new RecordingLauncher(),
            new RecordingRegistrar(),
            new ConfigLoader(configPath),
            TextWriter.Null);

        var exception = Assert.Throws<InvalidOperationException>(
            () => app.Run(["immich-bridge://open?app=photoshop&path=%2Fmnt%2Fphotos%2Fimg.jpg"]));

        Assert.Contains("Unknown app id", exception.Message);
    }

    [Fact]
    public void Run_RevealMapsPathAndLaunchesExplorer()
    {
        var configPath = WriteConfig(new BridgeConfig
        {
            Mappings =
            [
                new PathMapping { RemotePrefix = "/mnt/photos", LocalPrefix = @"Z:\Photos" }
            ],
            Options = new BridgeOptions { VerifyLocalFileExists = false }
        });
        var launcher = new RecordingLauncher();

        var app = new BridgeApplication(
            launcher,
            new RecordingRegistrar(),
            new ConfigLoader(configPath),
            TextWriter.Null);

        var exitCode = app.Run(["immich-bridge://reveal?path=%2Fmnt%2Fphotos%2Fimg.jpg"]);

        Assert.Equal(0, exitCode);
        Assert.Equal(Path.GetFullPath(@"Z:\Photos\img.jpg"), launcher.RevealedPath);
    }

    [Fact]
    public void Run_OpenWithoutAppMapsPathAndLaunchesSystemOpenWithDialog()
    {
        var configPath = WriteConfig(new BridgeConfig
        {
            Mappings =
            [
                new PathMapping { RemotePrefix = "/mnt/photos", LocalPrefix = @"Z:\Photos" }
            ],
            Options = new BridgeOptions { VerifyLocalFileExists = false }
        });
        var launcher = new RecordingLauncher();

        var app = new BridgeApplication(
            launcher,
            new RecordingRegistrar(),
            new ConfigLoader(configPath),
            TextWriter.Null);

        var exitCode = app.Run(["immich-bridge://open?path=%2Fmnt%2Fphotos%2Fimg.jpg"]);

        Assert.Equal(0, exitCode);
        Assert.Equal(Path.GetFullPath(@"Z:\Photos\img.jpg"), launcher.SystemOpenWithPath);
    }

    [Fact]
    public void Run_OpenWithCommandMapsPathAndLaunchesSystemOpenWithDialog()
    {
        var configPath = WriteConfig(new BridgeConfig
        {
            Mappings =
            [
                new PathMapping { RemotePrefix = "/mnt/photos", LocalPrefix = @"Z:\Photos" }
            ],
            Options = new BridgeOptions { VerifyLocalFileExists = false }
        });
        var launcher = new RecordingLauncher();

        var app = new BridgeApplication(
            launcher,
            new RecordingRegistrar(),
            new ConfigLoader(configPath),
            TextWriter.Null);

        var exitCode = app.Run(["--open-with", "/mnt/photos/img.jpg"]);

        Assert.Equal(0, exitCode);
        Assert.Equal(Path.GetFullPath(@"Z:\Photos\img.jpg"), launcher.SystemOpenWithPath);
    }

    private static string WriteConfig(BridgeConfig config)
    {
        var path = Path.Combine(Path.GetTempPath(), "ImmichBridge.Tests", Guid.NewGuid().ToString("N"), "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(config));
        return path;
    }

    private sealed class RecordingLauncher : IPlatformLauncher
    {
        public string? RevealedPath { get; private set; }

        public string? SystemOpenWithPath { get; private set; }

        public void RevealFile(string localPath)
        {
            RevealedPath = localPath;
        }

        public void OpenWithSystemDialog(string localPath)
        {
            SystemOpenWithPath = localPath;
        }

        public void OpenWithApp(string executablePath, string arguments, string localPath)
        {
        }
    }

    private sealed class RecordingRegistrar : IProtocolRegistrar
    {
        public bool Registered { get; set; } = true;

        public void Register(string executablePath)
        {
            Registered = true;
        }

        public void Unregister()
        {
            Registered = false;
        }

        public bool IsRegistered(string executablePath)
        {
            return Registered;
        }
    }
}
