namespace ImmichBridge.Tests;

public sealed class AppInstallerTests
{
    [Fact]
    public void Install_CopiesExecutableIntoInstallDirectory()
    {
        var root = CreateTempDirectory();
        var source = WriteFile(Path.Combine(root, "Downloads", "ImmichBridge-win-x64-v0.5.0.exe"), "new");
        var installer = new AppInstaller(Path.Combine(root, "Programs", "ImmichBridge"));

        var installedPath = installer.Install(source);

        Assert.Equal(installer.ExecutablePath, installedPath);
        Assert.Equal("new", File.ReadAllText(installedPath));
        Assert.True(File.Exists(source));
        Assert.True(installer.IsInstalled);
    }

    [Fact]
    public void Install_ReplacesExistingInstalledCopy()
    {
        var root = CreateTempDirectory();
        var installer = new AppInstaller(Path.Combine(root, "Programs", "ImmichBridge"));
        WriteFile(installer.ExecutablePath, "old");
        var source = WriteFile(Path.Combine(root, "Downloads", "ImmichBridge.exe"), "new");

        installer.Install(source);

        Assert.Equal("new", File.ReadAllText(installer.ExecutablePath));
        Assert.Equal([installer.ExecutablePath], Directory.GetFiles(installer.InstallDirectory));
    }

    [Fact]
    public void Install_FromInstalledCopyKeepsIt()
    {
        var root = CreateTempDirectory();
        var installer = new AppInstaller(Path.Combine(root, "Programs", "ImmichBridge"));
        WriteFile(installer.ExecutablePath, "installed");

        var installedPath = installer.Install(installer.ExecutablePath.ToUpperInvariant());

        Assert.Equal(installer.ExecutablePath, installedPath);
        Assert.Equal("installed", File.ReadAllText(installedPath));
    }

    [Fact]
    public void Install_RemovesCopiesReplacedWhileRunning()
    {
        var root = CreateTempDirectory();
        var installer = new AppInstaller(Path.Combine(root, "Programs", "ImmichBridge"));
        var replacedCopy = WriteFile($"{installer.ExecutablePath}.0123456789abcdef.old", "replaced");
        var source = WriteFile(Path.Combine(root, "Downloads", "ImmichBridge.exe"), "new");

        installer.Install(source);

        Assert.False(File.Exists(replacedCopy));
    }

    [Fact]
    public void Uninstall_FromAnotherCopyRemovesInstalledFilesAndFolder()
    {
        var root = CreateTempDirectory();
        var installer = new AppInstaller(Path.Combine(root, "Programs", "ImmichBridge"));
        WriteFile(installer.ExecutablePath, "installed");
        WriteFile($"{installer.ExecutablePath}.0123456789abcdef.old", "replaced");

        installer.Uninstall(Path.Combine(root, "Downloads", "ImmichBridge.exe"));

        Assert.False(Directory.Exists(installer.InstallDirectory));
    }

    [Fact]
    public void Uninstall_KeepsFilesItDidNotCreate()
    {
        var root = CreateTempDirectory();
        var installer = new AppInstaller(Path.Combine(root, "Programs", "ImmichBridge"));
        WriteFile(installer.ExecutablePath, "installed");
        var unrelated = WriteFile(Path.Combine(installer.InstallDirectory, "notes.txt"), "keep");

        installer.Uninstall(Path.Combine(root, "Downloads", "ImmichBridge.exe"));

        Assert.False(File.Exists(installer.ExecutablePath));
        Assert.True(File.Exists(unrelated));
    }

    [Theory]
    [InlineData("0.5.0", null, InstallKind.Fresh)]
    [InlineData("0.5.0", "0.4.1", InstallKind.Upgrade)]
    [InlineData("0.4.1", "0.5.0", InstallKind.Downgrade)]
    [InlineData("0.5.0", "0.5.0", InstallKind.Reinstall)]
    public void Classify_ComparesRunningAndInstalledVersions(string running, string? installed, InstallKind expected)
    {
        var kind = AppInstaller.Classify(Version.Parse(running), installed is null ? null : Version.Parse(installed));

        Assert.Equal(expected, kind);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ImmichBridge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
