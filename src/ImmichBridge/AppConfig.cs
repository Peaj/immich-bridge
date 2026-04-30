using System.Text.Json;

namespace ImmichBridge;

public sealed class BridgeConfig
{
    public List<PathMapping> Mappings { get; set; } = [];

    public Dictionary<string, AppDefinition> Apps { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public BridgeOptions Options { get; set; } = new();

    public BridgeConfig Normalize()
    {
        Options ??= new BridgeOptions();
        Mappings ??= [];
        Apps = new Dictionary<string, AppDefinition>(Apps ?? [], StringComparer.OrdinalIgnoreCase);
        return this;
    }
}

public sealed class PathMapping
{
    public string RemotePrefix { get; set; } = string.Empty;

    public string LocalPrefix { get; set; } = string.Empty;
}

public sealed class AppDefinition
{
    public string ExecutablePath { get; set; } = string.Empty;

    public string Arguments { get; set; } = "\"{file}\"";
}

public sealed class BridgeOptions
{
    public bool AllowOnlyMappedPaths { get; set; } = true;

    public bool ConfirmBeforeOpeningApps { get; set; }

    public bool VerifyLocalFileExists { get; set; } = true;

    public string? LogFile { get; set; }
}

public sealed class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public string ConfigPath { get; }

    public ConfigLoader(string? configPath = null)
    {
        ConfigPath = configPath ?? BridgePaths.ConfigFile;
    }

    public BridgeConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            throw new FileNotFoundException(
                $"Immich Bridge config was not found. Create {ConfigPath} from examples/config.example.json.",
                ConfigPath);
        }

        var json = File.ReadAllText(ConfigPath);
        var config = JsonSerializer.Deserialize<BridgeConfig>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Immich Bridge config is empty or invalid: {ConfigPath}");

        config.Normalize();
        Validate(config);
        return config;
    }

    public static void Validate(BridgeConfig config)
    {
        config.Normalize();

        foreach (var mapping in config.Mappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.RemotePrefix))
            {
                throw new InvalidOperationException("Every mapping must define RemotePrefix.");
            }

            if (string.IsNullOrWhiteSpace(mapping.LocalPrefix))
            {
                throw new InvalidOperationException($"Mapping '{mapping.RemotePrefix}' must define LocalPrefix.");
            }
        }

        foreach (var (appId, app) in config.Apps)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                throw new InvalidOperationException("App ids cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(app.ExecutablePath))
            {
                throw new InvalidOperationException($"App '{appId}' must define ExecutablePath.");
            }

            if (string.IsNullOrWhiteSpace(app.Arguments))
            {
                app.Arguments = "\"{file}\"";
            }
        }
    }
}

public static class BridgePaths
{
    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImmichBridge");

    public static string ConfigFile => Path.Combine(AppDataDirectory, "config.json");

    public static string DefaultLogFile => Path.Combine(AppDataDirectory, "logs", "helper.log");

    public static string ResolveLogFile(BridgeOptions options)
    {
        var configured = string.IsNullOrWhiteSpace(options.LogFile) ? DefaultLogFile : options.LogFile;
        return Environment.ExpandEnvironmentVariables(configured!);
    }
}
