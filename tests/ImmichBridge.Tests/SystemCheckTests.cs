using System.Text.Json;

namespace ImmichBridge.Tests;

public sealed class SystemCheckTests
{
    [Fact]
    public void Run_ReportsMissingConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), "ImmichBridge.Tests", Guid.NewGuid().ToString("N"), "config.json");
        var check = new SystemCheck(new ConfigLoader(path), new RecordingRegistrar(), CreateInstaller());

        var results = check.Run();

        Assert.Contains(results, result => result.Name == "Config file" && !result.Passed);
    }

    [Fact]
    public void Run_ReportsUnregisteredProtocol()
    {
        var configPath = WriteValidConfig();

        var check = new SystemCheck(new ConfigLoader(configPath), new RecordingRegistrar(), CreateInstaller());

        var results = check.Run();

        Assert.Contains(results, result => result.Name == "Protocol" && !result.Passed);
    }

    [Fact]
    public void Run_ReportsProtocolOutsideInstallFolder()
    {
        var configPath = WriteValidConfig();
        var installer = CreateInstaller(installed: true);
        var downloadedPath = Path.Combine(Path.GetDirectoryName(configPath)!, "ImmichBridge.exe");
        File.WriteAllText(downloadedPath, "exe");

        var check = new SystemCheck(new ConfigLoader(configPath), new RecordingRegistrar { RegisteredPath = downloadedPath }, installer);

        var results = check.Run();

        Assert.Contains(results, result => result.Name == "Protocol" && !result.Passed && result.Message.Contains("instead of the installed copy"));
    }

    [Fact]
    public void Run_AcceptsProtocolRegisteredForInstalledCopy()
    {
        var configPath = WriteValidConfig();
        var installer = CreateInstaller(installed: true);

        var check = new SystemCheck(new ConfigLoader(configPath), new RecordingRegistrar { RegisteredPath = installer.ExecutablePath }, installer);

        var results = check.Run();

        Assert.Contains(results, result => result.Name == "Install" && result.Passed);
        Assert.Contains(results, result => result.Name == "Protocol" && result.Passed);
    }

    [Fact]
    public void Run_ReportsMissingMappingFolder()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ImmichBridge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var missingDirectory = Path.Combine(directory, "missing");
        var configPath = Path.Combine(directory, "config.json");
        File.WriteAllText(configPath, JsonSerializer.Serialize(new BridgeConfig
        {
            Mappings =
            [
                new PathMapping { RemotePrefix = "/external/fotos", LocalPrefix = missingDirectory }
            ]
        }));

        var check = new SystemCheck(new ConfigLoader(configPath), new RecordingRegistrar(), CreateInstaller());

        var results = check.Run();

        Assert.Contains(results, result => result.Name == "Mapping" && !result.Passed);
    }

    private static string WriteValidConfig()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ImmichBridge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "config.json");
        File.WriteAllText(configPath, JsonSerializer.Serialize(new BridgeConfig
        {
            Mappings =
            [
                new PathMapping { RemotePrefix = "/external/fotos", LocalPrefix = directory }
            ]
        }));
        return configPath;
    }

    private static AppInstaller CreateInstaller(bool installed = false)
    {
        var installer = new AppInstaller(Path.Combine(Path.GetTempPath(), "ImmichBridge.Tests", Guid.NewGuid().ToString("N"), "Programs", "ImmichBridge"));
        if (installed)
        {
            Directory.CreateDirectory(installer.InstallDirectory);
            File.WriteAllText(installer.ExecutablePath, "exe");
        }

        return installer;
    }

    private sealed class RecordingRegistrar : IProtocolRegistrar
    {
        public string? RegisteredPath { get; set; }

        public void Register(string executablePath)
        {
            RegisteredPath = executablePath;
        }

        public void Unregister()
        {
            RegisteredPath = null;
        }

        public bool IsRegistered(string executablePath)
        {
            return string.Equals(RegisteredPath, executablePath, StringComparison.OrdinalIgnoreCase);
        }

        public string? GetRegisteredExecutablePath()
        {
            return RegisteredPath;
        }
    }
}
