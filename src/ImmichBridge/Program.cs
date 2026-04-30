using System.Windows.Forms;

namespace ImmichBridge;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
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
        catch (Exception ex)
        {
            logger.Error(ex);

            if (LooksLikeProtocolLaunch(args))
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

    private static bool LooksLikeProtocolLaunch(string[] args)
    {
        return args.Length > 0
            && args[0].StartsWith(ProtocolRequestParser.ProtocolScheme + ":", StringComparison.OrdinalIgnoreCase);
    }
}
