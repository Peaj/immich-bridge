using Microsoft.Win32;

namespace ImmichBridge;

public interface IInstallRegistration
{
    void Register(string executablePath, Version version);

    void Unregister();
}

// Per-user Start menu shortcut and Settings > Apps entry, so the installed copy can be found,
// set up again, and uninstalled without administrator rights.
public sealed class WindowsInstallRegistration : IInstallRegistration
{
    public const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ImmichBridge";

    public static string StartMenuShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Immich Bridge.lnk");

    public void Register(string executablePath, Version version)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath, true)
            ?? throw new InvalidOperationException("Unable to create the Immich Bridge uninstall registry key."))
        {
            key.SetValue("DisplayName", "Immich Bridge");
            key.SetValue("DisplayVersion", version.ToString(3));
            key.SetValue("Publisher", "Immich Bridge Contributors");
            key.SetValue("DisplayIcon", executablePath);
            key.SetValue("InstallLocation", Path.GetDirectoryName(executablePath) ?? string.Empty);
            key.SetValue("UninstallString", $"\"{executablePath}\" --uninstall");
            key.SetValue("URLInfoAbout", "https://github.com/Peaj/immich-bridge");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", (int)(new FileInfo(executablePath).Length / 1024), RegistryValueKind.DWord);
        }

        TryCreateShortcut(executablePath);
    }

    public void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryPath, false);
        File.Delete(StartMenuShortcutPath);
    }

    private static void TryCreateShortcut(string executablePath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(StartMenuShortcutPath);
            shortcut.TargetPath = executablePath;
            shortcut.Arguments = "--setup";
            shortcut.WorkingDirectory = Path.GetDirectoryName(executablePath);
            shortcut.Description = "Set up Immich Bridge";
            shortcut.Save();
        }
        catch
        {
            // Windows Script Host can be disabled by policy; the shortcut is only a convenience.
        }
    }
}
