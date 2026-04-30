namespace ImmichBridge.Tests;

public sealed class ConfigValidationTests
{
    [Fact]
    public void Validate_RejectsMappingWithoutRemotePrefix()
    {
        var config = new BridgeConfig
        {
            Mappings =
            [
                new PathMapping { LocalPrefix = @"Z:\Photos" }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Validate(config));

        Assert.Contains("RemotePrefix", exception.Message);
    }

    [Fact]
    public void Validate_RejectsAppWithoutExecutablePath()
    {
        var config = new BridgeConfig
        {
            Apps = new Dictionary<string, AppDefinition>
            {
                ["photoshop"] = new AppDefinition()
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Validate(config));

        Assert.Contains("ExecutablePath", exception.Message);
    }

    [Fact]
    public void ConfigService_NeedsSetupWhenConfigIsMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), "ImmichBridge.Tests", Guid.NewGuid().ToString("N"), "config.json");

        var service = new ConfigService(path);

        Assert.True(service.NeedsSetup());
    }

    [Fact]
    public void ConfigService_SaveCreatesConfigWithMapping()
    {
        var path = Path.Combine(Path.GetTempPath(), "ImmichBridge.Tests", Guid.NewGuid().ToString("N"), "config.json");
        var service = new ConfigService(path);

        service.Save(service.CreateDefaultConfig("/external/fotos", @"M:\Fotos"));

        var config = new ConfigLoader(path).Load();
        Assert.False(service.NeedsSetup());
        Assert.Single(config.Mappings);
        Assert.Equal("/external/fotos", config.Mappings[0].RemotePrefix);
        Assert.Equal(@"M:\Fotos", config.Mappings[0].LocalPrefix);
    }
}
