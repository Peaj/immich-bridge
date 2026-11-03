using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace ImmichBridge;

public interface INativeHostRegistrar
{
    void Register(string executablePath, IEnumerable<string> additionalExtensionIds);

    void Unregister();

    bool IsRegistered(string executablePath);
}

// Lets the store builds of the browser extension start this app through native messaging.
// Browsers only launch hosts whose manifest lists the calling extension, so web pages and
// other extensions cannot reach it.
public static class NativeHostManifests
{
    public const string HostName = "de.peaj.immich_bridge";
    public const string ChromeExtensionId = "ohghjemcjnaickehejdokfmjphpjoiag";
    public const string EdgeExtensionId = "bgipocndkokcllfjgmiicakhlbddnjij";
    public const string FirefoxAddonId = "immich-bridge@peaj.de";
    public const string ChromiumManifestFileName = HostName + ".chromium.json";
    public const string FirefoxManifestFileName = HostName + ".firefox.json";
    private const string Description = "Immich Bridge desktop companion";

    public static bool IsChromiumExtensionId(string value)
    {
        return value.Length == 32 && value.All(character => character is >= 'a' and <= 'p');
    }

    public static string BuildChromiumManifest(string executablePath, IEnumerable<string> additionalExtensionIds)
    {
        var extensionIds = new[] { ChromeExtensionId, EdgeExtensionId }
            .Concat(additionalExtensionIds.Where(IsChromiumExtensionId))
            .Distinct(StringComparer.Ordinal);

        var manifest = CreateManifest(executablePath);
        manifest["allowed_origins"] = new JsonArray(extensionIds.Select(id => (JsonNode)$"chrome-extension://{id}/").ToArray());
        return manifest.ToJsonString(ConfigLoader.JsonOptions);
    }

    public static string BuildFirefoxManifest(string executablePath)
    {
        var manifest = CreateManifest(executablePath);
        manifest["allowed_extensions"] = new JsonArray(FirefoxAddonId);
        return manifest.ToJsonString(ConfigLoader.JsonOptions);
    }

    public static string? ReadExecutablePath(string manifestPath)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(manifestPath))?["path"]?.GetValue<string>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static JsonObject CreateManifest(string executablePath)
    {
        return new JsonObject
        {
            ["name"] = HostName,
            ["description"] = Description,
            ["path"] = executablePath,
            ["type"] = "stdio"
        };
    }
}

public sealed class WindowsNativeHostRegistrar : INativeHostRegistrar
{
    private const string ChromeRegistryPath = @"Software\Google\Chrome\NativeMessagingHosts\" + NativeHostManifests.HostName;
    private const string EdgeRegistryPath = @"Software\Microsoft\Edge\NativeMessagingHosts\" + NativeHostManifests.HostName;
    private const string FirefoxRegistryPath = @"Software\Mozilla\NativeMessagingHosts\" + NativeHostManifests.HostName;

    public void Register(string executablePath, IEnumerable<string> additionalExtensionIds)
    {
        var directory = Path.GetDirectoryName(executablePath)
            ?? throw new InvalidOperationException($"Unable to determine the folder of {executablePath}.");
        var chromiumManifestPath = Path.Combine(directory, NativeHostManifests.ChromiumManifestFileName);
        var firefoxManifestPath = Path.Combine(directory, NativeHostManifests.FirefoxManifestFileName);

        File.WriteAllText(chromiumManifestPath, NativeHostManifests.BuildChromiumManifest(executablePath, additionalExtensionIds));
        File.WriteAllText(firefoxManifestPath, NativeHostManifests.BuildFirefoxManifest(executablePath));

        SetManifestPath(ChromeRegistryPath, chromiumManifestPath);
        SetManifestPath(EdgeRegistryPath, chromiumManifestPath);
        SetManifestPath(FirefoxRegistryPath, firefoxManifestPath);
    }

    public void Unregister()
    {
        foreach (var registryPath in new[] { ChromeRegistryPath, EdgeRegistryPath, FirefoxRegistryPath })
        {
            var manifestPath = ReadManifestPath(registryPath);
            Registry.CurrentUser.DeleteSubKeyTree(registryPath, false);
            if (manifestPath is not null && IsOwnManifest(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    public bool IsRegistered(string executablePath)
    {
        return new[] { ChromeRegistryPath, EdgeRegistryPath, FirefoxRegistryPath }.All(registryPath =>
        {
            var manifestPath = ReadManifestPath(registryPath);
            return manifestPath is not null
                && string.Equals(NativeHostManifests.ReadExecutablePath(manifestPath), executablePath, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool IsOwnManifest(string manifestPath)
    {
        var fileName = Path.GetFileName(manifestPath);
        return fileName.Equals(NativeHostManifests.ChromiumManifestFileName, StringComparison.OrdinalIgnoreCase)
            || fileName.Equals(NativeHostManifests.FirefoxManifestFileName, StringComparison.OrdinalIgnoreCase);
    }

    private static void SetManifestPath(string registryPath, string manifestPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(registryPath, true)
            ?? throw new InvalidOperationException($"Unable to create registry key {registryPath}.");
        key.SetValue(null, manifestPath);
    }

    private static string? ReadManifestPath(string registryPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(registryPath, false);
        return key?.GetValue(null) as string;
    }
}
