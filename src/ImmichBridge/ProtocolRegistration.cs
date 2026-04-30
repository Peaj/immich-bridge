using Microsoft.Win32;

namespace ImmichBridge;

public interface IProtocolRegistrar
{
    void Register(string executablePath);

    void Unregister();
}

public sealed class WindowsProtocolRegistrar : IProtocolRegistrar
{
    public const string RegistryPath = @"Software\Classes\immich-bridge";

    public void Register(string executablePath)
    {
        using var protocolKey = Registry.CurrentUser.CreateSubKey(RegistryPath, true)
            ?? throw new InvalidOperationException("Unable to create immich-bridge protocol registry key.");

        protocolKey.SetValue(null, "URL:Immich Bridge Protocol");
        protocolKey.SetValue("URL Protocol", string.Empty);

        using var commandKey = protocolKey.CreateSubKey(@"shell\open\command", true)
            ?? throw new InvalidOperationException("Unable to create immich-bridge command registry key.");

        commandKey.SetValue(null, BuildCommand(executablePath));
    }

    public void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false);
    }

    public static string BuildCommand(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }

        return $"\"{executablePath}\" \"%1\"";
    }
}
