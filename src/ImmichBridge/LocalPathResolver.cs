namespace ImmichBridge;

// Turns an Immich path into a local file the same way for browser-link launches and the
// native messaging host, so both paths enforce the configured mappings identically.
public sealed class LocalPathResolver
{
    private readonly ConfigLoader configLoader;

    public LocalPathResolver(ConfigLoader configLoader)
    {
        this.configLoader = configLoader;
    }

    public string Resolve(string remotePath, BridgeConfig? config = null)
    {
        config ??= configLoader.Load();
        var localPath = new PathMapper(config).MapPath(remotePath);

        if (config.Options.VerifyLocalFileExists && !File.Exists(localPath))
        {
            throw new FileNotFoundException($"Mapped local file does not exist: {localPath}", localPath);
        }

        return localPath;
    }
}
