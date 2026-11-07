namespace ImmichBridge;

// Checks path mappings that come from outside the config file (the browser add-on's options
// page) before they are saved: Immich prefixes are absolute paths, local folders are
// absolute, existing folders, and every Immich prefix appears once.
public static class MappingRules
{
    public const int MaxMappings = 50;
    private const int MaxPathLength = 1024;

    public static List<PathMapping> Normalize(IEnumerable<PathMapping> mappings)
    {
        var normalized = new List<PathMapping>();
        foreach (var mapping in mappings)
        {
            var remotePrefix = NormalizeRemotePrefix(mapping.RemotePrefix);
            var localPrefix = NormalizeLocalPrefix(mapping.LocalPrefix, remotePrefix);

            if (normalized.Any(existing => SameRemotePrefix(existing.RemotePrefix, remotePrefix)))
            {
                throw new InvalidOperationException($"The Immich path {remotePrefix} is mapped more than once.");
            }

            normalized.Add(new PathMapping { RemotePrefix = remotePrefix, LocalPrefix = localPrefix });
        }

        if (normalized.Count > MaxMappings)
        {
            throw new InvalidOperationException($"Immich Bridge supports up to {MaxMappings} mappings.");
        }

        return normalized;
    }

    public static bool SameRemotePrefix(string left, string right)
    {
        return string.Equals(TrimRemote(left), TrimRemote(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRemotePrefix(string? value)
    {
        var remotePrefix = TrimRemote(value);
        if (remotePrefix.Length == 0 || remotePrefix.Length > MaxPathLength)
        {
            throw new InvalidOperationException("Enter the Immich path as it appears in Immich, for example /external/photos.");
        }

        if (!remotePrefix.StartsWith('/'))
        {
            throw new InvalidOperationException($"The Immich path {remotePrefix} must start with /.");
        }

        if (remotePrefix.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException($"The Immich path {remotePrefix} can't contain . or .. segments.");
        }

        return remotePrefix;
    }

    private static string NormalizeLocalPrefix(string? value, string remotePrefix)
    {
        var trimmed = (value ?? string.Empty).Trim().Trim('"');
        if (trimmed.Length == 0 || trimmed.Length > MaxPathLength)
        {
            throw new InvalidOperationException($"Choose the local folder for {remotePrefix}.");
        }

        if (!Path.IsPathFullyQualified(trimmed))
        {
            throw new InvalidOperationException($"The local folder for {remotePrefix} must be a full path, such as M:\\Photos or \\\\server\\share\\Photos.");
        }

        var localPrefix = Path.GetFullPath(trimmed);
        var root = Path.GetPathRoot(localPrefix);
        if (!string.Equals(localPrefix, root, StringComparison.OrdinalIgnoreCase))
        {
            localPrefix = localPrefix.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        if (!Directory.Exists(localPrefix))
        {
            throw new InvalidOperationException($"The local folder {localPrefix} doesn't exist or isn't reachable.");
        }

        return localPrefix;
    }

    private static string TrimRemote(string? value)
    {
        var remote = (value ?? string.Empty).Trim().Replace('\\', '/');
        while (remote.Contains("//", StringComparison.Ordinal))
        {
            remote = remote.Replace("//", "/", StringComparison.Ordinal);
        }

        return remote.Length > 1 ? remote.TrimEnd('/') : remote;
    }
}
