namespace ImmichBridge;

public sealed class PathMapper
{
    private readonly BridgeConfig config;

    public PathMapper(BridgeConfig config)
    {
        this.config = config.Normalize();
    }

    public string MapPath(string remotePath)
    {
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            throw new ArgumentException("Remote path is required.", nameof(remotePath));
        }

        var normalizedRemote = NormalizeRemotePath(remotePath);
        var bestMatch = config.Mappings
            .Select(mapping => new
            {
                Mapping = mapping,
                NormalizedRemotePrefix = NormalizeRemotePath(mapping.RemotePrefix).TrimEnd('/')
            })
            .Where(candidate => IsPrefixMatch(normalizedRemote, candidate.NormalizedRemotePrefix))
            .OrderByDescending(candidate => candidate.NormalizedRemotePrefix.Length)
            .FirstOrDefault();

        if (bestMatch is null)
        {
            throw new InvalidOperationException($"No mapping found for path: {remotePath}");
        }

        var relative = normalizedRemote[bestMatch.NormalizedRemotePrefix.Length..].TrimStart('/');
        var relativeWindowsPath = relative.Replace('/', Path.DirectorySeparatorChar);
        var localRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(bestMatch.Mapping.LocalPrefix));
        var localPath = Path.GetFullPath(Path.Combine(localRoot, relativeWindowsPath));

        if (!IsSameOrChildPath(localRoot, localPath))
        {
            throw new InvalidOperationException($"Mapped path escapes local prefix: {remotePath}");
        }

        return localPath;
    }

    private static string NormalizeRemotePath(string path)
    {
        return path.Trim().Replace('\\', '/');
    }

    private static bool IsPrefixMatch(string remotePath, string remotePrefix)
    {
        return remotePath.Equals(remotePrefix, StringComparison.OrdinalIgnoreCase)
            || remotePath.StartsWith(remotePrefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameOrChildPath(string parent, string candidate)
    {
        var normalizedParent = EnsureTrailingSeparator(Path.GetFullPath(parent));
        var normalizedCandidate = Path.GetFullPath(candidate);

        return normalizedCandidate.Equals(normalizedParent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
