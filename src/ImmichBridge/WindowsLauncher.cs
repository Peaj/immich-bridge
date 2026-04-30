using System.Diagnostics;
using System.Runtime.InteropServices;

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

    public void OpenWithSystemDialog(string localPath)
    {
        var info = new OpenAsInfo
        {
            File = localPath,
            Class = null,
            Flags = OpenAsInfoFlags.AllowRegistration | OpenAsInfoFlags.Exec
        };

        var result = SHOpenWithDialog(IntPtr.Zero, ref info);
        if (result == HResultCancelled)
        {
            return;
        }

        Marshal.ThrowExceptionForHR(result);
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

    private const int HResultCancelled = unchecked((int)0x800704C7);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHOpenWithDialog(IntPtr hwndParent, ref OpenAsInfo openAsInfo);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenAsInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string File;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Class;

        public OpenAsInfoFlags Flags;
    }

    [Flags]
    private enum OpenAsInfoFlags
    {
        AllowRegistration = 0x00000001,
        Exec = 0x00000004
    }
}
