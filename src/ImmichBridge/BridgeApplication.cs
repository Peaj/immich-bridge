using System.Diagnostics;
using System.Windows.Forms;

namespace ImmichBridge;

public sealed class BridgeApplication
{
    private readonly IPlatformLauncher launcher;
    private readonly IProtocolRegistrar registrar;
    private readonly ConfigLoader configLoader;
    private readonly ConfigService configService;
    private readonly TextWriter output;
    private readonly FileLogger logger;
    private readonly AppInstaller installer;
    private readonly IInstallRegistration installRegistration;
    private readonly string executablePath;

    public BridgeApplication(
        IPlatformLauncher launcher,
        IProtocolRegistrar registrar,
        ConfigLoader configLoader,
        TextWriter output,
        FileLogger? logger = null,
        AppInstaller? installer = null,
        IInstallRegistration? installRegistration = null,
        string? executablePath = null)
    {
        this.launcher = launcher;
        this.registrar = registrar;
        this.configLoader = configLoader;
        configService = new ConfigService(configLoader.ConfigPath);
        this.output = output;
        this.logger = logger ?? new FileLogger();
        this.installer = installer ?? new AppInstaller();
        this.installRegistration = installRegistration ?? new WindowsInstallRegistration();
        this.executablePath = executablePath ?? GetExecutablePath();
    }

    public int Run(string[] args)
    {
        if (args.Length == 0)
        {
            if (configService.NeedsSetup())
            {
                ShowSetupWizard();
                return 0;
            }

            if (!installer.IsInstalledPath(executablePath))
            {
                // A configured user launching a downloaded copy is installing or updating.
                OfferInstall();
                return 0;
            }

            WriteUsage();
            return 0;
        }

        if (IsHelp(args[0]))
        {
            WriteUsage();
            return 0;
        }

        var command = args[0];
        if (StringComparer.OrdinalIgnoreCase.Equals(command, "--setup"))
        {
            ShowSetupWizard();
            return 0;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(command, "--check"))
        {
            return RunCheck();
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(command, "--install"))
        {
            var installedPath = Install();
            output.WriteLine($"Installed Immich Bridge to {installedPath} and registered the immich-bridge:// protocol.");
            return 0;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(command, "--uninstall"))
        {
            Uninstall();
            return 0;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(command, "--register-protocol"))
        {
            registrar.Register(executablePath);
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

    private string Install()
    {
        var installedPath = installer.Install(executablePath);
        registrar.Register(installedPath);
        installRegistration.Register(installedPath, AppInstaller.GetFileVersion(installedPath)!);
        logger.Info($"Installed '{executablePath}' to '{installedPath}'.");
        return installedPath;
    }

    private void OfferInstall()
    {
        var runningVersion = AppInstaller.GetFileVersion(executablePath)!;
        var installedVersion = AppInstaller.GetFileVersion(installer.ExecutablePath);
        var kind = AppInstaller.Classify(runningVersion, installedVersion);
        var message = kind switch
        {
            InstallKind.Fresh => $"Install Immich Bridge {runningVersion.ToString(3)} to {installer.InstallDirectory}?\n\nThe browser link will use the installed copy, so this file can be deleted afterwards.",
            InstallKind.Upgrade => $"Update Immich Bridge from {installedVersion!.ToString(3)} to {runningVersion.ToString(3)}?",
            InstallKind.Downgrade => $"A newer Immich Bridge ({installedVersion!.ToString(3)}) is already installed. Replace it with this older version ({runningVersion.ToString(3)})?",
            _ => $"Immich Bridge {runningVersion.ToString(3)} is already installed. Reinstall it from this file?"
        };

        var result = MessageBox.Show(
            message,
            "Immich Bridge",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            kind == InstallKind.Downgrade ? MessageBoxDefaultButton.Button2 : MessageBoxDefaultButton.Button1);
        if (result != DialogResult.Yes)
        {
            return;
        }

        var installedPath = Install();
        MessageBox.Show(
            $"Immich Bridge {runningVersion.ToString(3)} is installed at:\n{installedPath}\n\nYou can delete the file you downloaded.",
            "Immich Bridge",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void Uninstall()
    {
        var confirm = MessageBox.Show(
            "Uninstall Immich Bridge?\n\nThis removes the app, its browser link, and its Start menu entry.",
            "Immich Bridge",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.OK)
        {
            return;
        }

        var removeSettings = MessageBox.Show(
            $"Also delete your settings and logs?\n\n{BridgePaths.AppDataDirectory}",
            "Immich Bridge",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;

        registrar.Unregister();
        installRegistration.Unregister();
        installer.Uninstall(executablePath);
        logger.Info("Immich Bridge uninstalled.");

        if (removeSettings && Directory.Exists(BridgePaths.AppDataDirectory))
        {
            Directory.Delete(BridgePaths.AppDataDirectory, recursive: true);
        }

        output.WriteLine("Immich Bridge was uninstalled.");
        MessageBox.Show("Immich Bridge was uninstalled.", "Immich Bridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        output.WriteLine("  ImmichBridge.exe --install");
        output.WriteLine("  ImmichBridge.exe --uninstall");
        output.WriteLine("  ImmichBridge.exe --register-protocol");
        output.WriteLine("  ImmichBridge.exe --unregister-protocol");
        output.WriteLine("  ImmichBridge.exe --setup");
        output.WriteLine("  ImmichBridge.exe --check");
        output.WriteLine("  ImmichBridge.exe --map-path <remotePath>");
        output.WriteLine("  ImmichBridge.exe --open-with <remotePath>");
        output.WriteLine("  ImmichBridge.exe \"immich-bridge://reveal?path=<remotePath>\"");
        output.WriteLine("  ImmichBridge.exe \"immich-bridge://open?path=<remotePath>\"");
        output.WriteLine("  ImmichBridge.exe \"immich-bridge://open?app=<appId>&path=<remotePath>\"");
    }

    private void ShowSetupWizard()
    {
        using var form = new SetupWizardForm(configService, launcher, executablePath, installer.InstallDirectory, Install);
        form.ShowDialog();
    }

    private int RunCheck()
    {
        var check = new SystemCheck(configLoader, registrar, installer);
        var results = check.Run();
        foreach (var result in results)
        {
            output.WriteLine($"{(result.Passed ? "OK" : "FAIL")} {result.Name}: {result.Message}");
        }

        return results.All(result => result.Passed) ? 0 : 1;
    }
}
