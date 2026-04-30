using System.Text.Json;

namespace ImmichBridge.Tests;

public sealed class SystemCheckTests
{
    [Fact]
    public void Run_ReportsMissingConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), "ImmichBridge.Tests", Guid.NewGuid().ToString("N"), "config.json");
        var check = new SystemCheck(new ConfigLoader(path), new RecordingRegistrar(), @"C:\ImmichBridge.exe");

        var results = check.Run();

        Assert.Contains(results, result => result.Name == "Config file" && !result.Passed);
    }

    [Fact]
    public void Run_ReportsUnregisteredProtocol()
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

        var check = new SystemCheck(new ConfigLoader(configPath), new RecordingRegistrar { Registered = false }, @"C:\ImmichBridge.exe");

        var results = check.Run();

        Assert.Contains(results, result => result.Name == "Protocol" && !result.Passed);
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

        var check = new SystemCheck(new ConfigLoader(configPath), new RecordingRegistrar(), @"C:\ImmichBridge.exe");

        var results = check.Run();

        Assert.Contains(results, result => result.Name == "Mapping" && !result.Passed);
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
