using System.Diagnostics;
using System.Windows.Forms;

namespace ImmichBridge;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Started by a browser: stdin/stdout carry the extension's messages, so no console or dialogs.
        if (NativeMessagingHost.IsHostLaunch(args))
        {
            return RunNativeHost();
        }

        if (!LooksLikeProtocolLaunch(args))
        {
            ConsoleBridge.AttachToParentConsole();
        }

        ApplicationConfiguration.Initialize();

        var logger = new FileLogger();
        try
        {
            var app = new BridgeApplication(
                new WindowsLauncher(),
                new WindowsProtocolRegistrar(),
                new ConfigLoader(),
                Console.Out);

            return app.Run(args);
        }
        catch (UnsupportedActionException ex) when (LooksLikeProtocolLaunch(args))
        {
            logger.Error(ex);
            OfferUpdate(ex);
            return 1;
        }
        catch (Exception ex)
        {
            logger.Error(ex);

            if (LooksLikeProtocolLaunch(args) || IsOpenCopyLaunch(args))
            {
                MessageBox.Show(ex.Message, "Immich Bridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                Console.Error.WriteLine(ex.Message);
            }

            return 1;
        }
    }

    private static int RunNativeHost()
    {
        var logger = new FileLogger();
        try
        {
            var copyStore = new CopyStore();
            copyStore.DeleteOlderThan(CopyStore.RetentionPeriod, DateTime.Now);

            var host = new NativeMessagingHost(
                new ConfigLoader(),
                new WindowsLauncher(),
                new WindowsDetachedProcessStarter(),
                Environment.ProcessPath ?? throw new InvalidOperationException("Unable to determine the Immich Bridge executable path."),
                logger,
                copyStore);

            using var input = Console.OpenStandardInput();
            using var output = Console.OpenStandardOutput();
            host.Run(input, output);
            return 0;
        }
        catch (Exception ex)
        {
            logger.Error(ex);
            return 1;
        }
    }

    private static void OfferUpdate(UnsupportedActionException exception)
    {
        var answer = MessageBox.Show(
            $"{exception.Message}\n\nOpen the download page for the latest version?",
            "Immich Bridge",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (answer == DialogResult.Yes)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = UnsupportedActionException.DownloadUrl,
                UseShellExecute = true
            });
        }
    }

    // Started detached by the native host, so there is no console to report errors to.
    private static bool IsOpenCopyLaunch(string[] args)
    {
        return args.Length > 0 && StringComparer.OrdinalIgnoreCase.Equals(args[0], BridgeApplication.OpenCopyCommand);
    }

    private static bool LooksLikeProtocolLaunch(string[] args)
    {
        return args.Length > 0
            && args[0].StartsWith(ProtocolRequestParser.ProtocolScheme + ":", StringComparison.OrdinalIgnoreCase);
    }
}
