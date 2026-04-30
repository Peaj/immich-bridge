namespace ImmichBridge;

public sealed class SystemCheck
{
    private readonly ConfigLoader configLoader;
    private readonly IProtocolRegistrar registrar;
    private readonly string executablePath;

    public SystemCheck(ConfigLoader configLoader, IProtocolRegistrar registrar, string executablePath)
    {
        this.configLoader = configLoader;
        this.registrar = registrar;
        this.executablePath = executablePath;
    }

    public IReadOnlyList<CheckResult> Run()
    {
        var results = new List<CheckResult>();
        BridgeConfig? config = null;

        try
        {
            config = configLoader.Load();
            results.Add(CheckResult.Pass("Config file", $"Loaded {configLoader.ConfigPath}"));
        }
        catch (Exception ex)
        {
            results.Add(CheckResult.Fail("Config file", ex.Message));
            return AppendProtocolAndLogChecks(results, null);
        }

        if (config.Mappings.Count == 0)
        {
            results.Add(CheckResult.Fail("Mappings", "At least one path mapping is required."));
        }
        else
        {
            foreach (var mapping in config.Mappings)
            {
                var localPrefix = Environment.ExpandEnvironmentVariables(mapping.LocalPrefix);
                results.Add(Directory.Exists(localPrefix)
                    ? CheckResult.Pass("Mapping", $"{mapping.RemotePrefix} -> {localPrefix}")
                    : CheckResult.Fail("Mapping", $"Local folder does not exist for {mapping.RemotePrefix}: {localPrefix}"));
            }
        }

        return AppendProtocolAndLogChecks(results, config);
    }

    private IReadOnlyList<CheckResult> AppendProtocolAndLogChecks(List<CheckResult> results, BridgeConfig? config)
    {
        results.Add(registrar.IsRegistered(executablePath)
            ? CheckResult.Pass("Protocol", "immich-bridge:// is registered for this executable.")
            : CheckResult.Fail("Protocol", "immich-bridge:// is not registered for this executable. Run --register-protocol or --setup."));

        var logPath = BridgePaths.ResolveLogFile(config?.Options ?? new BridgeOptions());
        var logDirectory = Path.GetDirectoryName(logPath);
        results.Add(string.IsNullOrWhiteSpace(logDirectory)
            ? CheckResult.Fail("Log path", $"Invalid log path: {logPath}")
            : CheckResult.Pass("Log path", logPath));

        return results;
    }
}

public sealed record CheckResult(string Name, bool Passed, string Message)
{
    public static CheckResult Pass(string name, string message) => new(name, true, message);

    public static CheckResult Fail(string name, string message) => new(name, false, message);
}
