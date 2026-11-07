using System.Text.Json;

namespace ImmichBridge.Tests;

public sealed class MappingTests
{
    [Fact]
    public void Rules_NormalizePrefixesAndKeepExistingFolders()
    {
        var folder = CreateTempFolder();

        var mappings = MappingRules.Normalize(
        [
            new PathMapping { RemotePrefix = " /external//photos/ ", LocalPrefix = $"\"{folder}\\\"" }
        ]);

        var mapping = Assert.Single(mappings);
        Assert.Equal("/external/photos", mapping.RemotePrefix);
        Assert.Equal(folder, mapping.LocalPrefix);
    }

    [Fact]
    public void Rules_KeepDriveRootsWithTheirSeparator()
    {
        var root = Path.GetPathRoot(Path.GetTempPath())!;

        var mapping = Assert.Single(MappingRules.Normalize([new PathMapping { RemotePrefix = "/external", LocalPrefix = root }]));

        Assert.Equal(root, mapping.LocalPrefix);
    }

    [Theory]
    [InlineData("", "folder")]
    [InlineData("external/photos", "folder")]
    [InlineData("/external/../etc", "folder")]
    [InlineData("/external/photos", "")]
    [InlineData("/external/photos", "relative\\folder")]
    [InlineData("/external/photos", "M:")]
    [InlineData("/external/photos", "missing")]
    public void Rules_RejectInvalidMappings(string remotePrefix, string localPrefix)
    {
        var folder = CreateTempFolder();
        var local = localPrefix switch
        {
            "folder" => folder,
            "missing" => Path.Combine(folder, "missing"),
            _ => localPrefix
        };

        Assert.Throws<InvalidOperationException>(() =>
            MappingRules.Normalize([new PathMapping { RemotePrefix = remotePrefix, LocalPrefix = local }]));
    }

    [Fact]
    public void Rules_RejectDuplicateImmichPrefixes()
    {
        var folder = CreateTempFolder();

        var exception = Assert.Throws<InvalidOperationException>(() => MappingRules.Normalize(
        [
            new PathMapping { RemotePrefix = "/external/photos", LocalPrefix = folder },
            new PathMapping { RemotePrefix = "/External/Photos/", LocalPrefix = folder }
        ]));

        Assert.Contains("more than once", exception.Message);
    }

    [Fact]
    public void Suggester_PrefersTheMappingThatCoversTheWholeLibrary()
    {
        var share = CreateTempFolder();
        CreateFile(share, "Fotos", "2024", "IMG_1.jpg");

        var suggestions = new MappingSuggester(new FolderLocations(share)).Suggest("/external/fotos/2024/IMG_1.jpg");

        // M:\Fotos\2024 would also reach the file, but /external -> M:\ covers every year.
        var suggestion = Assert.Single(suggestions);
        Assert.Equal("/external", suggestion.RemotePrefix);
        Assert.Equal(share, suggestion.LocalPrefix);
        Assert.True(File.Exists(suggestion.LocalPath));
    }

    [Fact]
    public void Suggester_FindsLibrariesOneFolderBelowTheRoot()
    {
        var drive = CreateTempFolder();
        CreateFile(drive, "Share", "Photos", "2024", "IMG_1.jpg");

        var suggestion = Assert.Single(new MappingSuggester(new FolderLocations(drive)).Suggest("/library/photos/2024/IMG_1.jpg"));

        Assert.Equal("/library", suggestion.RemotePrefix);
        Assert.Equal(Path.Combine(drive, "Share"), suggestion.LocalPrefix);
    }

    [Fact]
    public void Suggester_PrefersTheLongestMatchingTail()
    {
        var first = CreateTempFolder();
        var second = CreateTempFolder();
        CreateFile(first, "IMG_1.jpg");
        CreateFile(second, "2024", "IMG_1.jpg");

        var suggestions = new MappingSuggester(new FolderLocations(first, second)).Suggest("/photos/2024/IMG_1.jpg");

        Assert.Equal(2, suggestions.Count);
        Assert.Equal(second, suggestions[0].LocalPrefix);
        Assert.Equal("/photos", suggestions[0].RemotePrefix);
        Assert.Equal("/photos/2024", suggestions[1].RemotePrefix);
    }

    [Fact]
    public void Suggester_NeverSuggestsMappingTheImmichRoot()
    {
        var drive = CreateTempFolder();
        CreateFile(drive, "photos", "IMG_1.jpg");

        var suggestion = Assert.Single(new MappingSuggester(new FolderLocations(drive)).Suggest("/photos/IMG_1.jpg"));

        Assert.Equal("/photos", suggestion.RemotePrefix);
    }

    [Fact]
    public void Suggester_ReturnsNothingWhenTheFileIsNowhere()
    {
        var drive = CreateTempFolder();

        Assert.Empty(new MappingSuggester(new FolderLocations(drive)).Suggest("/external/photos/IMG_1.jpg"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("IMG_1.jpg")]
    [InlineData("/IMG_1.jpg")]
    [InlineData("/external/../IMG_1.jpg")]
    [InlineData("/external/a|b.jpg")]
    public void Suggester_RejectsPathsThatAreNotImmichFilePaths(string path)
    {
        Assert.Throws<InvalidOperationException>(() => new MappingSuggester(new FolderLocations(CreateTempFolder())).Suggest(path));
    }

    [Fact]
    public void Suggester_SkipsLocationsThatDoNotAnswerInTime()
    {
        var drive = CreateTempFolder();
        CreateFile(drive, "photos", "IMG_1.jpg");
        var locations = new FolderLocations(drive) { SlowRoot = @"Z:\" };

        var suggestions = new MappingSuggester(locations, TimeSpan.FromMilliseconds(500)).Suggest("/library/photos/IMG_1.jpg");

        Assert.Single(suggestions);
    }

    [Fact]
    public void ConfigService_SaveMappingsKeepsAppsAndOptions()
    {
        var folder = CreateTempFolder();
        var configPath = Path.Combine(folder, "config.json");
        File.WriteAllText(configPath, JsonSerializer.Serialize(new BridgeConfig
        {
            Mappings = [new PathMapping { RemotePrefix = "/old", LocalPrefix = folder }],
            Apps = new Dictionary<string, AppDefinition> { ["photoshop"] = new() { ExecutablePath = @"C:\PS\Photoshop.exe" } },
            Options = new BridgeOptions { ConfirmBeforeOpeningApps = true, AdditionalExtensionIds = ["fceamgaohbchbenjbcpcnghkgenaacgf"] }
        }));
        var service = new ConfigService(configPath);

        service.SaveMappings([new PathMapping { RemotePrefix = "/external/photos", LocalPrefix = folder }]);

        var config = new ConfigLoader(configPath).Load();
        Assert.Equal("/external/photos", Assert.Single(config.Mappings).RemotePrefix);
        Assert.Equal(@"C:\PS\Photoshop.exe", config.Apps["photoshop"].ExecutablePath);
        Assert.True(config.Options.ConfirmBeforeOpeningApps);
        Assert.Equal(["fceamgaohbchbenjbcpcnghkgenaacgf"], config.Options.AdditionalExtensionIds);
    }

    [Fact]
    public void ConfigService_SaveMappingsLeavesUnreadableConfigUntouched()
    {
        var folder = CreateTempFolder();
        var configPath = Path.Combine(folder, "config.json");
        File.WriteAllText(configPath, "{ not json");
        var service = new ConfigService(configPath);

        Assert.ThrowsAny<Exception>(() => service.SaveMappings([new PathMapping { RemotePrefix = "/a", LocalPrefix = folder }]));

        Assert.Equal("{ not json", File.ReadAllText(configPath));
    }

    [Fact]
    public void ConfigService_ConfigWithoutMappingsIsAFinishedSetup()
    {
        var configPath = Path.Combine(CreateTempFolder(), "config.json");
        var service = new ConfigService(configPath);

        service.SaveMappings([]);

        Assert.False(service.NeedsSetup());
        Assert.Empty(new ConfigLoader(configPath).Load().Mappings);
    }

    private static string CreateTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ImmichBridge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void CreateFile(string root, params string[] segments)
    {
        var path = Path.Combine([root, .. segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "jpg");
    }

    private sealed class FolderLocations(params string[] roots) : ISearchLocations
    {
        public string? SlowRoot { get; init; }

        public IEnumerable<string> GetRoots()
        {
            return SlowRoot is null ? roots : [SlowRoot, .. roots];
        }

        public IEnumerable<string> GetSubfolders(string root)
        {
            if (root == SlowRoot)
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
                return [];
            }

            return Directory.EnumerateDirectories(root);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }
    }
}
