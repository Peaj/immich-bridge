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
        Options.AdditionalExtensionIds ??= [];
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
    public bool ConfirmBeforeOpeningApps { get; set; }

    public bool VerifyLocalFileExists { get; set; } = true;

    public string? LogFile { get; set; }

    // Extra Chromium extension IDs allowed to use native messaging, for unpacked test builds.
    public List<string> AdditionalExtensionIds { get; set; } = [];
}

public sealed class ConfigNotFoundException : FileNotFoundException
{
    public ConfigNotFoundException(string message, string configPath)
        : base(message, configPath)
    {
    }
}

public sealed class ConfigLoader
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
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
            throw new ConfigNotFoundException(
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

        foreach (var extensionId in config.Options.AdditionalExtensionIds)
        {
            if (!NativeHostManifests.IsChromiumExtensionId(extensionId))
            {
                throw new InvalidOperationException($"'{extensionId}' in AdditionalExtensionIds is not a Chromium extension ID (32 letters a-p).");
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

public sealed class ConfigService
{
    public string ConfigPath { get; }

    public ConfigService(string? configPath = null)
    {
        ConfigPath = configPath ?? BridgePaths.ConfigFile;
    }

    // Setup is needed until a valid config exists. A config without mappings is a finished
    // setup: the mappings can be added later in the browser add-on, and photos in Immich's
    // upload library don't need any.
    public bool NeedsSetup()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return true;
            }

            new ConfigLoader(ConfigPath).Load();
            return false;
        }
        catch
        {
            return true;
        }
    }

    public BridgeConfig CreateDefaultConfig(string remotePrefix, string localPrefix)
    {
        var config = CreateDefaultConfig();
        config.Mappings.Add(new PathMapping
        {
            RemotePrefix = remotePrefix.Trim(),
            LocalPrefix = localPrefix.Trim()
        });
        return config;
    }

    public BridgeConfig CreateDefaultConfig()
    {
        return new BridgeConfig
        {
            Options = new BridgeOptions
            {
                ConfirmBeforeOpeningApps = false,
                VerifyLocalFileExists = true,
                LogFile = "%AppData%\\ImmichBridge\\logs\\helper.log"
            }
        }.Normalize();
    }

    // The current config, or a default one when none exists yet. A config that exists but
    // can't be read throws, so saving never replaces apps and options the user wrote by hand.
    public BridgeConfig LoadOrCreate()
    {
        return File.Exists(ConfigPath) ? new ConfigLoader(ConfigPath).Load() : CreateDefaultConfig();
    }

    // Replaces only the mappings and keeps apps, options, and extra extension IDs.
    public BridgeConfig SaveMappings(IEnumerable<PathMapping> mappings)
    {
        var config = LoadOrCreate();
        config.Mappings = mappings.ToList();
        Save(config);
        return config;
    }

    public void Save(BridgeConfig config)
    {
        config.Normalize();
        ConfigLoader.Validate(config);

        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, ConfigLoader.JsonOptions);
        File.WriteAllText(ConfigPath, json + Environment.NewLine);
    }
}

public static class BridgePaths
{
    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImmichBridge");

    public static string InstallDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ImmichBridge");

    public static string ConfigFile => Path.Combine(AppDataDirectory, "config.json");

    public static string DefaultLogFile => Path.Combine(AppDataDirectory, "logs", "helper.log");

    public static string ResolveLogFile(BridgeOptions options)
    {
        var configured = string.IsNullOrWhiteSpace(options.LogFile) ? DefaultLogFile : options.LogFile;
        return Environment.ExpandEnvironmentVariables(configured!);
    }
}
