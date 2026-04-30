using System.Diagnostics;

namespace ImmichBridge;

public sealed class WindowsLauncher : IPlatformLauncher
{
    public void RevealFile(string localPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{localPath}\"",
            UseShellExecute = true
        });
    }

    public void OpenWithApp(string executablePath, string arguments, string localPath)
    {
        var resolvedArguments = arguments.Replace("{file}", localPath, StringComparison.Ordinal);

        Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = resolvedArguments,
            UseShellExecute = false
        });
    }
}
