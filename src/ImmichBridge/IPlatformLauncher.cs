namespace ImmichBridge;

public interface IPlatformLauncher
{
    void RevealFile(string localPath);

    void OpenWithApp(string executablePath, string arguments, string localPath);
}
