using System.Diagnostics;
using System.Windows.Forms;

namespace ImmichBridge;

public sealed class BridgeApplication
{
    private readonly IPlatformLauncher launcher;
    private readonly IProtocolRegistrar registrar;
    private readonly ConfigLoader configLoader;
    private readonly TextWriter output;
    private readonly FileLogger logger;

    public BridgeApplication(
        IPlatformLauncher launcher,
        IProtocolRegistrar registrar,
        ConfigLoader configLoader,
        TextWriter output,
        FileLogger? logger = null)
    {
        this.launcher = launcher;
        this.registrar = registrar;
        this.configLoader = configLoader;
        this.output = output;
        this.logger = logger ?? new FileLogger();
    }

    public int Run(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            WriteUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var command = args[0];
        if (StringComparer.OrdinalIgnoreCase.Equals(command, "--register-protocol"))
        {
            registrar.Register(GetExecutablePath());
            output.WriteLine("Registered immich-bridge:// protocol for Immich Bridge.");
            return 0;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(command, "--unregister-protocol"))
        {
            registrar.Unregister();
            output.WriteLine("Unregistered immich-bridge:// protocol for Immich Bridge.");
            return 0;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(command, "--map-path"))
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                throw new ArgumentException("--map-path requires a remote path argument.");
            }

            var config = configLoader.Load();
            var mapper = new PathMapper(config);
            output.WriteLine(mapper.MapPath(args[1]));
            return 0;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(command, "--open-with"))
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                throw new ArgumentException("--open-with requires a remote path argument.");
            }

            var localPath = MapAndValidateLocalPath(args[1]);
            output.WriteLine($"Opening Windows Open With dialog for: {localPath}");
            logger.Info($"CLI open-with requested for '{args[1]}' -> '{localPath}'.");
            launcher.OpenWithSystemDialog(localPath);
            return 0;
        }

        var request = ProtocolRequestParser.Parse(command);
        ExecuteRequest(request);
        return 0;
    }

    private void ExecuteRequest(ProtocolRequest request)
    {
        var config = configLoader.Load();
        var localPath = MapAndValidateLocalPath(request.RemotePath, config);
        logger.Info($"Protocol {request.Action} requested for '{request.RemotePath}' -> '{localPath}'.");

        if (request.Action == BridgeAction.Reveal)
        {
            launcher.RevealFile(localPath);
            logger.Info($"Explorer reveal launched for '{localPath}'.");
            return;
        }

        if (string.IsNullOrWhiteSpace(request.AppId))
        {
            launcher.OpenWithSystemDialog(localPath);
            logger.Info($"Windows Open With dialog launched for '{localPath}'.");
            return;
        }

        if (!config.Apps.TryGetValue(request.AppId ?? string.Empty, out var app))
        {
            throw new InvalidOperationException($"Unknown app id '{request.AppId}'. Add it to config.json Apps first.");
        }

        var executablePath = Environment.ExpandEnvironmentVariables(app.ExecutablePath);
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException($"Configured executable does not exist for app '{request.AppId}': {executablePath}", executablePath);
        }

        if (config.Options.ConfirmBeforeOpeningApps && !ConfirmOpen(request.AppId!, localPath))
        {
            return;
        }

        launcher.OpenWithApp(executablePath, app.Arguments, localPath);
        logger.Info($"Configured app '{request.AppId}' launched for '{localPath}' using '{executablePath}'.");
    }

    private string MapAndValidateLocalPath(string remotePath, BridgeConfig? config = null)
    {
        config ??= configLoader.Load();
        var localPath = new PathMapper(config).MapPath(remotePath);

        if (config.Options.VerifyLocalFileExists && !File.Exists(localPath))
        {
            throw new FileNotFoundException($"Mapped local file does not exist: {localPath}", localPath);
        }

        return localPath;
    }

    private static bool ConfirmOpen(string appId, string localPath)
    {
        var result = MessageBox.Show(
            $"Open this file with '{appId}'?\n\n{localPath}",
            "Immich Bridge",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        return result == DialogResult.Yes;
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Unable to determine the Immich Bridge executable path.");
    }

    private static bool IsHelp(string value)
    {
        return value is "-h" or "--help" or "/?";
    }

    private void WriteUsage()
    {
        output.WriteLine("Immich Bridge");
        output.WriteLine();
        output.WriteLine("Usage:");
        output.WriteLine("  ImmichBridge.exe --register-protocol");
        output.WriteLine("  ImmichBridge.exe --unregister-protocol");
        output.WriteLine("  ImmichBridge.exe --map-path <remotePath>");
        output.WriteLine("  ImmichBridge.exe --open-with <remotePath>");
        output.WriteLine("  ImmichBridge.exe \"immich-bridge://reveal?path=<remotePath>\"");
        output.WriteLine("  ImmichBridge.exe \"immich-bridge://open?path=<remotePath>\"");
        output.WriteLine("  ImmichBridge.exe \"immich-bridge://open?app=<appId>&path=<remotePath>\"");
    }
}
