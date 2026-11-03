using System.Text.Json.Nodes;

namespace ImmichBridge.Tests;

public sealed class NativeHostManifestsTests
{
    private const string ExecutablePath = @"C:\Programs\ImmichBridge\ImmichBridge.exe";

    [Fact]
    public void ChromiumManifest_AllowsStoreExtensionsAndValidExtraIds()
    {
        var manifest = JsonNode.Parse(NativeHostManifests.BuildChromiumManifest(
            ExecutablePath,
            ["bjonmmnackaemelhjpkkhgoeocgfekam", "not-an-id", NativeHostManifests.ChromeExtensionId]))!;

        Assert.Equal("de.peaj.immich_bridge", manifest["name"]!.GetValue<string>());
        Assert.Equal(ExecutablePath, manifest["path"]!.GetValue<string>());
        Assert.Equal("stdio", manifest["type"]!.GetValue<string>());
        Assert.Equal(
            [
                "chrome-extension://ohghjemcjnaickehejdokfmjphpjoiag/",
                "chrome-extension://bgipocndkokcllfjgmiicakhlbddnjij/",
                "chrome-extension://bjonmmnackaemelhjpkkhgoeocgfekam/"
            ],
            manifest["allowed_origins"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Null(manifest["allowed_extensions"]);
    }

    [Fact]
    public void FirefoxManifest_AllowsTheAddonId()
    {
        var manifest = JsonNode.Parse(NativeHostManifests.BuildFirefoxManifest(ExecutablePath))!;

        Assert.Equal(ExecutablePath, manifest["path"]!.GetValue<string>());
        Assert.Equal(["immich-bridge@peaj.de"], manifest["allowed_extensions"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Null(manifest["allowed_origins"]);
    }

    [Fact]
    public void ReadExecutablePath_ReadsBackWrittenManifest()
    {
        var path = Path.Combine(Path.GetTempPath(), "ImmichBridge.Tests", Guid.NewGuid().ToString("N"), "host.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, NativeHostManifests.BuildFirefoxManifest(ExecutablePath));

        Assert.Equal(ExecutablePath, NativeHostManifests.ReadExecutablePath(path));
        Assert.Null(NativeHostManifests.ReadExecutablePath(path + ".missing"));
    }

    [Theory]
    [InlineData("ohghjemcjnaickehejdokfmjphpjoiag", true)]
    [InlineData("OHGHJEMCJNAICKEHEJDOKFMJPHPJOIAG", false)]
    [InlineData("ohghjemcjnaickehejdokfmjphpjoiaz", false)]
    [InlineData("ohghjemcjnaick", false)]
    public void IsChromiumExtensionId_ChecksFormat(string value, bool expected)
    {
        Assert.Equal(expected, NativeHostManifests.IsChromiumExtensionId(value));
    }
}
