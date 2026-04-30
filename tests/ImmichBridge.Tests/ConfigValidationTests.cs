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
}
