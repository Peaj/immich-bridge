namespace ImmichBridge;

public enum BridgeAction
{
    Reveal,
    Open
}

public sealed record ProtocolRequest(BridgeAction Action, string RemotePath, string? AppId);

public static class ProtocolRequestParser
{
    public const string ProtocolScheme = "immich-bridge";

    public static ProtocolRequest Parse(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Invalid Immich Bridge URI: {value}");
        }

        if (!uri.Scheme.Equals(ProtocolScheme, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported URI scheme '{uri.Scheme}'. Expected '{ProtocolScheme}'.");
        }

        var actionName = GetActionName(uri);
        var query = ParseQuery(uri.Query);

        if (!query.TryGetValue("path", out var remotePath) || string.IsNullOrWhiteSpace(remotePath))
        {
            throw new InvalidOperationException("Immich Bridge URI requires a non-empty 'path' parameter.");
        }

        return actionName.ToLowerInvariant() switch
        {
            "reveal" => new ProtocolRequest(BridgeAction.Reveal, remotePath, null),
            "open" => new ProtocolRequest(BridgeAction.Open, remotePath, GetRequiredAppId(query)),
            _ => throw new InvalidOperationException($"Unknown Immich Bridge action '{actionName}'.")
        };
    }

    private static string GetActionName(Uri uri)
    {
        var action = string.IsNullOrWhiteSpace(uri.Host)
            ? uri.AbsolutePath.Trim('/')
            : uri.Host;

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new InvalidOperationException("Immich Bridge URI requires an action.");
        }

        return action;
    }

    private static string GetRequiredAppId(IReadOnlyDictionary<string, string> query)
    {
        if (!query.TryGetValue("app", out var appId) || string.IsNullOrWhiteSpace(appId))
        {
            throw new InvalidOperationException("Open action requires a non-empty 'app' parameter.");
        }

        return appId;
    }

    private static Dictionary<string, string> ParseQuery(string queryString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trimmed = queryString.TrimStart('?');

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return result;
        }

        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equalsIndex = part.IndexOf('=');
            var rawKey = equalsIndex >= 0 ? part[..equalsIndex] : part;
            var rawValue = equalsIndex >= 0 ? part[(equalsIndex + 1)..] : string.Empty;

            var key = Uri.UnescapeDataString(rawKey.Replace("+", " "));
            var value = Uri.UnescapeDataString(rawValue.Replace("+", " "));
            result[key] = value;
        }

        return result;
    }
}
