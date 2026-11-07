namespace ImmichBridge;

public sealed record MappingSuggestion(string RemotePrefix, string LocalPrefix, string LocalPath);

public interface ISearchLocations
{
    // Folders where an Immich library could start: drive roots, their direct subfolders,
    // and the user's picture and video folders.
    IEnumerable<string> GetRoots();

    IEnumerable<string> GetSubfolders(string root);

    bool FileExists(string path);
}

// Finds where an Immich file lives on this computer from its Immich path alone. For
// /external/photos/2024/IMG_1.jpg it looks for photos\2024\IMG_1.jpg, then 2024\IMG_1.jpg,
// and so on, below each search location. The longest matching tail wins, because it is
// the strongest evidence that the folders really correspond.
public sealed class MappingSuggester
{
    public const int MaxSuggestions = 5;
    private readonly ISearchLocations locations;
    private readonly TimeSpan timeout;

    public MappingSuggester(ISearchLocations? locations = null, TimeSpan? timeout = null)
    {
        this.locations = locations ?? new WindowsSearchLocations();
        this.timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    public IReadOnlyList<MappingSuggestion> Suggest(string immichFilePath)
    {
        var segments = SplitRemotePath(immichFilePath);
        if (segments.Length < 2)
        {
            throw new InvalidOperationException("Enter the full Immich file path, for example /external/photos/2024/IMG_1234.jpg.");
        }

        // Each drive is searched on its own task: a disconnected network drive can block for
        // a long time and must not hold up the others.
        var searches = locations.GetRoots()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(root => Task.Run(() => SearchBelow(root, segments)))
            .ToArray();
        Task.WaitAll(searches, timeout);

        // A drive root and one of its folders often reach the same file (M:\ with
        // photos\2024\IMG_1.jpg, M:\Photos with 2024\IMG_1.jpg). Both are right for this file,
        // but only the broader one also covers the library's other folders (2023, 2025, ...),
        // so it is the one suggested.
        return searches
            .Where(search => search.IsCompletedSuccessfully)
            .SelectMany(search => search.Result)
            .OrderByDescending(candidate => candidate.TailLength)
            .ThenBy(candidate => candidate.Suggestion.LocalPrefix, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Suggestion)
            .DistinctBy(suggestion => suggestion.LocalPath, StringComparer.OrdinalIgnoreCase)
            .Take(MaxSuggestions)
            .ToList();
    }

    private List<Candidate> SearchBelow(string root, string[] segments)
    {
        var candidates = new List<Candidate>();
        AddMatch(candidates, root, segments);
        foreach (var subfolder in SafeSubfolders(root))
        {
            AddMatch(candidates, subfolder, segments);
        }

        return candidates;
    }

    private IEnumerable<string> SafeSubfolders(string root)
    {
        try
        {
            return locations.GetSubfolders(root).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void AddMatch(List<Candidate> candidates, string folder, string[] segments)
    {
        // Keep at least one segment as the Immich prefix: mapping "/" would claim every path.
        for (var prefixLength = 1; prefixLength < segments.Length; prefixLength++)
        {
            var tail = segments[prefixLength..];
            var localPath = Path.Combine([folder, .. tail]);
            bool exists;
            try
            {
                exists = locations.FileExists(localPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                exists = false;
            }

            if (exists)
            {
                var remotePrefix = "/" + string.Join('/', segments[..prefixLength]);
                var localPrefix = TrimFolder(folder);
                candidates.Add(new Candidate(new MappingSuggestion(remotePrefix, localPrefix, localPath), tail.Length));
                return;
            }
        }
    }

    private static string TrimFolder(string folder)
    {
        var root = Path.GetPathRoot(folder);
        return string.Equals(folder, root, StringComparison.OrdinalIgnoreCase)
            ? folder
            : folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string[] SplitRemotePath(string? path)
    {
        var normalized = (path ?? string.Empty).Trim().Trim('"', '\'').Replace('\\', '/');
        if (!normalized.StartsWith('/'))
        {
            throw new InvalidOperationException("The Immich file path must start with /.");
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(segment => segment is "." or ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new InvalidOperationException("The Immich file path contains characters that can't appear in a Windows path.");
        }

        return segments;
    }

    private sealed record Candidate(MappingSuggestion Suggestion, int TailLength);
}

public sealed class WindowsSearchLocations : ISearchLocations
{
    private static readonly EnumerationOptions SubfolderOptions = new()
    {
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        IgnoreInaccessible = true,
        RecurseSubdirectories = false
    };

    public IEnumerable<string> GetRoots()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType is DriveType.Fixed or DriveType.Network or DriveType.Removable)
            {
                yield return drive.RootDirectory.FullName;
            }
        }

        foreach (var folder in new[] { Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyVideos })
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(path))
            {
                yield return path;
            }
        }
    }

    public IEnumerable<string> GetSubfolders(string root)
    {
        return Directory.Exists(root) ? Directory.EnumerateDirectories(root, "*", SubfolderOptions) : [];
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }
}
