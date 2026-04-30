namespace ImmichBridge;

public sealed class FileLogger
{
    public void Error(Exception exception)
    {
        try
        {
            var path = ResolveLogPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] ERROR {exception}\n");
        }
        catch
        {
            // Logging must not hide the original helper failure.
        }
    }

    private static string ResolveLogPath()
    {
        try
        {
            if (File.Exists(BridgePaths.ConfigFile))
            {
                var config = new ConfigLoader().Load();
                return BridgePaths.ResolveLogFile(config.Options);
            }
        }
        catch
        {
            // Fall back to the default log path when config loading itself failed.
        }

        return BridgePaths.DefaultLogFile;
    }
}
