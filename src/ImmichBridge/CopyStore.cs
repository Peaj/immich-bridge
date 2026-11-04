namespace ImmichBridge;

// Holds read-only copies of Immich assets that the browser downloaded through Immich's API.
// Internal-library originals must never be opened in place, so they are only ever handed
// to desktop apps through these copies.
public sealed class CopyStore
{
    private const int MaxFileNameLength = 120;
    private const string FallbackFileName = "immich-asset";
    private static readonly string[] ReservedDeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    public CopyStore(string? root = null)
    {
        Root = Path.GetFullPath(root ?? DefaultRoot);
    }

    public static string DefaultRoot => Path.Combine(Path.GetTempPath(), "Immich Bridge", "Copies");

    public static TimeSpan RetentionPeriod => TimeSpan.FromDays(7);

    public string Root { get; }

    // Every copy gets its own folder so the original file name can be kept without collisions.
    public string CreateCopyPath(string? requestedName)
    {
        var folder = Path.Combine(Root, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..24]);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, SanitizeFileName(requestedName));
    }

    public static string SanitizeFileName(string? requestedName)
    {
        // Take the last path segment by hand: Path.GetFileName would read "a:b.jpg" as a drive.
        var name = (requestedName ?? string.Empty).Replace('/', '\\');
        name = name[(name.LastIndexOf('\\') + 1)..];
        var invalid = Path.GetInvalidFileNameChars();
        name = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim().TrimEnd('.');

        if (string.IsNullOrWhiteSpace(name) || name.Trim('.', '_').Length == 0)
        {
            name = FallbackFileName;
        }

        if (ReservedDeviceNames.Contains(Path.GetFileNameWithoutExtension(name), StringComparer.OrdinalIgnoreCase))
        {
            name = "_" + name;
        }

        if (name.Length > MaxFileNameLength)
        {
            var extension = Path.GetExtension(name);
            extension = extension.Length <= 10 ? extension : string.Empty;
            name = name[..(MaxFileNameLength - extension.Length)] + extension;
        }

        return name;
    }

    public bool IsInsideCopies(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static void MarkReadOnly(string path)
    {
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
    }

    public void Discard(string path)
    {
        if (!IsInsideCopies(path))
        {
            return;
        }

        TryDeleteFolder(Path.GetDirectoryName(path)!);
    }

    public void DeleteOlderThan(TimeSpan age, DateTime now)
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        foreach (var folder in Directory.EnumerateDirectories(Root))
        {
            if (now - Directory.GetCreationTime(folder) > age)
            {
                TryDeleteFolder(folder);
            }
        }
    }

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still open in a desktop app; the next cleanup retries.
        }
    }
}
