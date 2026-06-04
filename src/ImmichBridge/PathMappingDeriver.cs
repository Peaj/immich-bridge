namespace ImmichBridge;

public sealed record DerivedPathMapping(string RemotePrefix, string LocalPrefix, string SharedRelativePath);

public static class PathMappingDeriver
{
    public static DerivedPathMapping Derive(string immichFilePath, string localFilePath)
    {
        var remotePath = NormalizeImmichPath(immichFilePath);
        var localPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(Unquote(localFilePath)));
        var localRoot = Path.GetPathRoot(localPath)
            ?? throw new ArgumentException("Local file path must be absolute.", nameof(localFilePath));

        var remoteSegments = SplitRemotePath(remotePath);
        var localSegments = SplitLocalPath(localPath, localRoot);
        var matchingSegments = CountMatchingSuffixSegments(remoteSegments, localSegments);
        if (matchingSegments == 0)
        {
            throw new InvalidOperationException("The Immich path and local file path do not end with the same file name.");
        }

        if (ShouldKeepLastLocalFolderAsPrefix(remoteSegments, localSegments, matchingSegments))
        {
            matchingSegments--;
        }

        if (matchingSegments == 0)
        {
            throw new InvalidOperationException("The matching path is too short to derive a safe mapping.");
        }

        var remotePrefixSegments = remoteSegments.Take(remoteSegments.Length - matchingSegments).ToArray();
        var localPrefixSegments = localSegments.Take(localSegments.Length - matchingSegments).ToArray();
        var sharedSegments = remoteSegments.Skip(remoteSegments.Length - matchingSegments).ToArray();

        var remotePrefix = "/" + string.Join('/', remotePrefixSegments);
        var localPrefix = localPrefixSegments.Length == 0
            ? localRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : Path.Combine([localRoot, .. localPrefixSegments]);
        var sharedRelativePath = string.Join(Path.DirectorySeparatorChar, sharedSegments);

        return new DerivedPathMapping(remotePrefix, localPrefix, sharedRelativePath);
    }

    public static string GetRemoteFileName(string immichFilePath)
    {
        var remotePath = NormalizeImmichPath(immichFilePath);
        var fileName = SplitRemotePath(remotePath).LastOrDefault();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidOperationException("Immich file path must include a file name.");
        }

        return fileName;
    }

    private static string NormalizeImmichPath(string path)
    {
        var normalized = Unquote(path).Replace('\\', '/').Trim();
        if (!normalized.StartsWith('/'))
        {
            throw new ArgumentException("Immich file path must start with '/'.", nameof(path));
        }

        return normalized.TrimEnd('/');
    }

    private static string Unquote(string value)
    {
        return value.Trim().Trim('"', '\'');
    }

    private static string[] SplitRemotePath(string path)
    {
        return path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string[] SplitLocalPath(string path, string root)
    {
        var relativePath = path[root.Length..];
        return relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int CountMatchingSuffixSegments(string[] remoteSegments, string[] localSegments)
    {
        var max = Math.Min(remoteSegments.Length, localSegments.Length);
        var count = 0;
        while (count < max
            && remoteSegments[^(count + 1)].Equals(localSegments[^(count + 1)], StringComparison.OrdinalIgnoreCase))
        {
            count++;
        }

        return count;
    }

    private static bool ShouldKeepLastLocalFolderAsPrefix(string[] remoteSegments, string[] localSegments, int matchingSegments)
    {
        return matchingSegments > 1
            && matchingSegments == localSegments.Length
            && localSegments.Length > 1;
    }
}
