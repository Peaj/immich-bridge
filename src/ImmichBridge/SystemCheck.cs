namespace ImmichBridge;

public sealed class SystemCheck
{
    private readonly ConfigLoader configLoader;
    private readonly IProtocolRegistrar registrar;
    private readonly AppInstaller installer;
    private readonly INativeHostRegistrar nativeHostRegistrar;

    public SystemCheck(ConfigLoader configLoader, IProtocolRegistrar registrar, AppInstaller installer, INativeHostRegistrar nativeHostRegistrar)
    {
        this.configLoader = configLoader;
        this.registrar = registrar;
        this.installer = installer;
        this.nativeHostRegistrar = nativeHostRegistrar;
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
            // Photos in Immich's upload library work without mappings; external libraries need
            // one, which the browser add-on's options page can add.
            results.Add(CheckResult.Pass("Mappings", "No path mappings yet. Add them in the browser add-on's options page or with --setup to use external libraries."));
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
        results.Add(installer.IsInstalled
            ? CheckResult.Pass("Install", $"Installed at {installer.ExecutablePath}")
            : CheckResult.Fail("Install", $"Not installed at {installer.ExecutablePath}. Run --install or --setup."));

        results.Add(CheckProtocol());

        results.Add(nativeHostRegistrar.IsRegistered(installer.ExecutablePath)
            ? CheckResult.Pass("Native messaging", $"Chrome, Edge, and Firefox can reach {installer.ExecutablePath}")
            : CheckResult.Fail("Native messaging", "The browser extensions cannot reach the installed copy. Run --install or --setup."));

        var logPath = BridgePaths.ResolveLogFile(config?.Options ?? new BridgeOptions());
        var logDirectory = Path.GetDirectoryName(logPath);
        results.Add(string.IsNullOrWhiteSpace(logDirectory)
            ? CheckResult.Fail("Log path", $"Invalid log path: {logPath}")
            : CheckResult.Pass("Log path", logPath));

        return results;
    }

    private CheckResult CheckProtocol()
    {
        var registeredPath = registrar.GetRegisteredExecutablePath();
        if (registeredPath is null)
        {
            return CheckResult.Fail("Protocol", "immich-bridge:// is not registered. Run --install or --setup.");
        }

        if (!File.Exists(registeredPath))
        {
            return CheckResult.Fail("Protocol", $"immich-bridge:// points to a missing file: {registeredPath}. Run --install or --setup.");
        }

        return installer.IsInstalledPath(registeredPath)
            ? CheckResult.Pass("Protocol", $"immich-bridge:// is registered for {registeredPath}")
            : CheckResult.Fail("Protocol", $"immich-bridge:// points to {registeredPath} instead of the installed copy, so it breaks if that file is moved or deleted. Run --install.");
    }
}

public sealed record CheckResult(string Name, bool Passed, string Message)
{
    public static CheckResult Pass(string name, string message) => new(name, true, message);

    public static CheckResult Fail(string name, string message) => new(name, false, message);
}
