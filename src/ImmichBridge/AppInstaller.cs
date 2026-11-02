using System.Diagnostics;

namespace ImmichBridge;

public enum InstallKind
{
    Fresh,
    Upgrade,
    Downgrade,
    Reinstall
}

// Keeps the single-file app at one stable per-user location, so the browser link never points
// at a copy in Downloads or in a temporary ZIP extraction folder that may disappear.
public sealed class AppInstaller
{
    public const string ExecutableName = "ImmichBridge.exe";
    private const string ReplacedCopyPattern = ExecutableName + ".*.old";

    public AppInstaller(string? installDirectory = null)
    {
        InstallDirectory = Path.GetFullPath(installDirectory ?? BridgePaths.InstallDirectory);
    }

    public string InstallDirectory { get; }

    public string ExecutablePath => Path.Combine(InstallDirectory, ExecutableName);

    public bool IsInstalled => File.Exists(ExecutablePath);

    public bool IsInstalledPath(string path)
    {
        return string.Equals(Path.GetFullPath(path), ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    public static InstallKind Classify(Version runningVersion, Version? installedVersion)
    {
        if (installedVersion is null)
        {
            return InstallKind.Fresh;
        }

        return runningVersion.CompareTo(installedVersion) switch
        {
            > 0 => InstallKind.Upgrade,
            < 0 => InstallKind.Downgrade,
            _ => InstallKind.Reinstall
        };
    }

    public static Version? GetFileVersion(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var info = FileVersionInfo.GetVersionInfo(path);
        return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);
    }

    public string Install(string sourcePath)
    {
        Directory.CreateDirectory(InstallDirectory);
        DeleteReplacedCopies();

        if (IsInstalledPath(sourcePath))
        {
            return ExecutablePath;
        }

        var stagingPath = ExecutablePath + ".new";
        File.Copy(sourcePath, stagingPath, overwrite: true);
        try
        {
            File.Move(stagingPath, ExecutablePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The installed copy is running (e.g. still showing a notice). Windows refuses to
            // overwrite a running exe but allows renaming it, so move it aside for later cleanup.
            File.Move(ExecutablePath, $"{ExecutablePath}.{Guid.NewGuid():N}.old");
            File.Move(stagingPath, ExecutablePath);
        }

        return ExecutablePath;
    }

    // Removes only the files this installer creates, so an unexpected file in the folder is never deleted.
    public void Uninstall(string runningPath)
    {
        if (!Directory.Exists(InstallDirectory))
        {
            return;
        }

        if (IsInstalledPath(runningPath))
        {
            ScheduleRemovalAfterExit();
            return;
        }

        File.Delete(ExecutablePath);
        DeleteReplacedCopies();
        if (!Directory.EnumerateFileSystemEntries(InstallDirectory).Any())
        {
            Directory.Delete(InstallDirectory);
        }
    }

    private void DeleteReplacedCopies()
    {
        foreach (var path in Directory.EnumerateFiles(InstallDirectory, ReplacedCopyPattern))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Still running; the next install or uninstall retries.
            }
        }
    }

    private void ScheduleRemovalAfterExit()
    {
        // A running exe cannot delete itself, so a hidden shell waits for this process to exit first.
        // rd without /s only removes the folder once it is empty.
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c ping -n 3 127.0.0.1 >nul & del /f /q \"{ExecutablePath}\" \"{Path.Combine(InstallDirectory, ReplacedCopyPattern)}\" & rd \"{InstallDirectory}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }
}
