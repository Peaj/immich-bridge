using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ImmichBridge;

public interface IDetachedProcessStarter
{
    void Start(string executablePath, string arguments);
}

// Answers the browser extension over native messaging: one length-prefixed JSON message in,
// one reply out, repeated until the browser closes stdin.
public sealed class NativeMessagingHost
{
    public const int ProtocolVersion = 1;
    public static readonly string[] SupportedActions = ["reveal", "copy", "open"];
    private const int MaxIncomingMessageBytes = 1024 * 1024;

    private readonly ConfigService configService;
    private readonly LocalPathResolver resolver;
    private readonly IPlatformLauncher launcher;
    private readonly IDetachedProcessStarter processStarter;
    private readonly string executablePath;
    private readonly FileLogger logger;

    public NativeMessagingHost(
        ConfigLoader configLoader,
        IPlatformLauncher launcher,
        IDetachedProcessStarter processStarter,
        string executablePath,
        FileLogger? logger = null)
    {
        configService = new ConfigService(configLoader.ConfigPath);
        resolver = new LocalPathResolver(configLoader);
        this.launcher = launcher;
        this.processStarter = processStarter;
        this.executablePath = executablePath;
        this.logger = logger ?? new FileLogger();
    }

    public static bool IsHostLaunch(string[] args)
    {
        if (args.Length >= 1 && args[0].StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return args.Length >= 2
            && args[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            && args[1] == NativeHostManifests.FirefoxAddonId;
    }

    public static string Version =>
        typeof(NativeMessagingHost).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public void Run(Stream input, Stream output)
    {
        while (ReadMessage(input) is { } message)
        {
            WriteMessage(output, Handle(message).ToJsonString());
        }
    }

    public JsonObject Handle(string message)
    {
        try
        {
            var request = JsonNode.Parse(message) as JsonObject
                ?? throw new InvalidOperationException("Expected a JSON object.");

            return request["type"]?.GetValue<string>() switch
            {
                "hello" => Hello(),
                "run" => RunAction(request),
                var type => Failure("unsupported-message", $"Unsupported message type '{type}'.")
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            logger.Error(ex);
            return Failure("invalid-message", ex.Message);
        }
    }

    public static string? ReadMessage(Stream input)
    {
        Span<byte> header = stackalloc byte[4];
        if (!TryReadExactly(input, header))
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length < 0 || length > MaxIncomingMessageBytes)
        {
            throw new InvalidDataException($"Native message length {length} is out of range.");
        }

        var body = new byte[length];
        if (!TryReadExactly(input, body))
        {
            throw new EndOfStreamException("Native message ended early.");
        }

        return Encoding.UTF8.GetString(body);
    }

    public static void WriteMessage(Stream output, string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, body.Length);
        output.Write(header);
        output.Write(body);
        output.Flush();
    }

    private JsonObject Hello()
    {
        return new JsonObject
        {
            ["ok"] = true,
            ["protocol"] = ProtocolVersion,
            ["version"] = Version,
            ["actions"] = new JsonArray(SupportedActions.Select(action => (JsonNode)action).ToArray()),
            ["configured"] = !configService.NeedsSetup()
        };
    }

    private JsonObject RunAction(JsonObject request)
    {
        var action = request["action"]?.GetValue<string>() ?? string.Empty;
        var remotePath = request["path"]?.GetValue<string>() ?? string.Empty;

        try
        {
            if (!SupportedActions.Contains(action, StringComparer.Ordinal))
            {
                throw new UnsupportedActionException(action);
            }

            var localPath = resolver.Resolve(remotePath);
            logger.Info($"Native {action} requested for '{remotePath}' -> '{localPath}'.");

            if (action == "copy")
            {
                launcher.CopyToClipboard(localPath);
            }
            else
            {
                // The browser ends this host's job when it replies, which would also end an
                // Explorer window or an app picked in Open With. A detached copy of this app
                // runs the action through the regular browser-link path instead.
                var url = $"{ProtocolRequestParser.ProtocolScheme}://{action}?path={Uri.EscapeDataString(remotePath)}";
                processStarter.Start(executablePath, $"\"{url}\"");
            }

            return new JsonObject { ["ok"] = true };
        }
        catch (Exception ex)
        {
            logger.Error(ex);
            return ex switch
            {
                UnsupportedActionException => Failure("unsupported-action", ex.Message),
                ConfigNotFoundException => Failure("not-configured", "Immich Bridge is not set up yet. Run its setup on this computer."),
                PathNotMappedException => Failure("not-mapped", ex.Message),
                FileNotFoundException => Failure("file-missing", ex.Message),
                _ => Failure("failed", ex.Message)
            };
        }
    }

    private static JsonObject Failure(string code, string message)
    {
        return new JsonObject { ["ok"] = false, ["code"] = code, ["message"] = message };
    }

    private static bool TryReadExactly(Stream input, Span<byte> buffer)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = input.Read(buffer[read..]);
            if (count == 0)
            {
                if (read == 0)
                {
                    return false;
                }

                throw new EndOfStreamException("Native message ended early.");
            }

            read += count;
        }

        return true;
    }
}

public sealed class WindowsDetachedProcessStarter : IDetachedProcessStarter
{
    private const uint CreateBreakawayFromJob = 0x01000000;
    private const int ErrorAccessDenied = 5;

    public void Start(string executablePath, string arguments)
    {
        if (TryCreateProcess(executablePath, arguments, CreateBreakawayFromJob, out var error))
        {
            return;
        }

        // A job that forbids breakaway still lets us start the process; it may just end with the host.
        if (error == ErrorAccessDenied && TryCreateProcess(executablePath, arguments, 0, out error))
        {
            return;
        }

        throw new InvalidOperationException($"Unable to start {executablePath} (Windows error {error}).");
    }

    private static bool TryCreateProcess(string executablePath, string arguments, uint flags, out int error)
    {
        var commandLine = new StringBuilder($"\"{executablePath}\" {arguments}");
        var startupInfo = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>() };
        if (!CreateProcessW(executablePath, commandLine, IntPtr.Zero, IntPtr.Zero, false, flags, IntPtr.Zero, Path.GetDirectoryName(executablePath), ref startupInfo, out var processInfo))
        {
            error = Marshal.GetLastWin32Error();
            return false;
        }

        CloseHandle(processInfo.hProcess);
        CloseHandle(processInfo.hThread);
        error = 0;
        return true;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessW(
        string lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref StartupInfo lpStartupInfo,
        out ProcessInformation lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }
}
