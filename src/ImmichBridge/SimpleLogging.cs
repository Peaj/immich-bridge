namespace ImmichBridge;

public sealed class FileLogger
{
    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Error(Exception exception)
    {
        Write("ERROR", exception.ToString());
    }

    private static void Write(string level, string message)
    {
        try
        {
            var path = ResolveLogPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {level} {message}\n");
        }
        catch
        {
            // Logging must not hide the original helper behavior.
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
