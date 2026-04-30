# Immich Bridge

## Goal

Build a small local helper that adds workstation-native actions to Immich assets, starting with:

* Reveal file in Windows Explorer
* Open file in Photoshop
* Later: open in other configured applications

The helper bridges the gap between Immich's server/container paths and the user's local OS paths.

Example:

```text
Immich / container path:
/mnt/immich-external/photos/2024/trip/img123.jpg

Windows path:
Z:\Photos\2024\trip\img123.jpg
```

## Core Idea

Immich runs as a web app and should not directly launch local programs. Browsers intentionally restrict this for security reasons.

Instead, use a two-part architecture:

1. A browser-side script adds custom buttons to the Immich UI.
2. A local helper app receives a custom URL protocol request and launches the native action.

```text
Immich UI button
  -> immich-bridge://reveal?path=/mnt/immich-external/photos/2024/trip/img123.jpg
  -> Windows protocol handler
  -> local .NET helper
  -> path mapping
  -> explorer.exe /select,"Z:\Photos\2024\trip\img123.jpg"
```

## Recommended First Version

Start Windows-only.

Cross-platform support is possible, but implementing it immediately is likely overscoping. The path mapping logic can be cross-platform-shaped from the beginning, but OS integration should be Windows-only for v1.

The tricky platform-specific parts are:

| Feature                | Windows              | macOS                    | Linux                                     |
| ---------------------- | -------------------- | ------------------------ | ----------------------------------------- |
| Custom URL protocol    | Registry             | App bundle / Info.plist  | .desktop MIME handler                     |
| Reveal in file manager | explorer.exe /select | open -R                  | xdg-open / file-manager-specific behavior |
| Open in external app   | Process launch       | App bundle handling      | Distro/app-dependent                      |
| Installation           | Registry setup       | App bundle/signing later | Desktop environment differences           |

## Technology Choice

Use **C# / .NET 10** for the local helper.

Suggested target framework:

```xml
<TargetFramework>net10.0-windows</TargetFramework>
```

Reasons:

* Good fit for Windows-native process launching
* Easy protocol registration via registry
* Easy single-file executable publishing
* Familiar development environment
* No need for Electron or a bundled browser
* Cleaner deployment than Python for this use case

Avoid Electron for v1. It is too heavy for a small path mapper and process launcher.

Avoid making this a full Immich fork at first. A userscript/browser extension is much easier to iterate on.

## Architecture

### Components

```text
Immich Web UI
  + Tampermonkey userscript
      Adds custom buttons to asset detail view
      Fetches or extracts asset path
      Calls immich-bridge://...

Windows Protocol Handler
  Registered for immich-bridge://
  Launches local helper executable

Immich Bridge Helper
  Parses URI
  Maps remote/container path to local path
  Executes configured action
```

### Data Flow

```text
User clicks "Reveal in Explorer"
  -> userscript gets current Immich asset id/path
  -> userscript builds URL:
     immich-bridge://reveal?path=/mnt/immich-external/photos/2024/img.jpg
  -> browser opens custom protocol
  -> Windows starts helper executable
  -> helper loads config
  -> helper maps remote prefix to local prefix
  -> helper validates resulting path
  -> helper launches Explorer
```

## URI Design

Use a custom protocol:

```text
immich-bridge://reveal?path=/mnt/immich-external/photos/2024/img.jpg
immich-bridge://open?app=photoshop&path=/mnt/immich-external/photos/2024/img.jpg
immich-bridge://open?app=affinity&path=/mnt/immich-external/photos/2024/img.jpg
```

Supported actions for v1:

```text
reveal
open
```

Potential later actions:

```text
copy-path
open-folder
open-terminal
edit-metadata
```

## Configuration

Store config in:

```text
%AppData%\ImmichBridge\config.json
```

Example:

```json
{
  "Mappings": [
    {
      "RemotePrefix": "/mnt/immich-external/photos",
      "LocalPrefix": "Z:\\Photos"
    },
    {
      "RemotePrefix": "/mnt/archive",
      "LocalPrefix": "Y:\\Archive"
    }
  ],
  "Apps": {
    "photoshop": {
      "ExecutablePath": "C:\\Program Files\\Adobe\\Adobe Photoshop 2025\\Photoshop.exe",
      "Arguments": "\"{file}\""
    },
    "affinity": {
      "ExecutablePath": "C:\\Program Files\\Affinity\\Photo 2\\Photo.exe",
      "Arguments": "\"{file}\""
    }
  },
  "Options": {
    "AllowOnlyMappedPaths": true,
    "ConfirmBeforeOpeningApps": false,
    "LogFile": "%AppData%\\ImmichBridge\\logs\\helper.log"
  }
}
```

## Path Mapping

The mapping system should be simple prefix replacement.

Example:

```csharp
public string MapPath(string remotePath)
{
    foreach(var mapping in Config.Mappings)
    {
        if(!remotePath.StartsWith(mapping.RemotePrefix, StringComparison.OrdinalIgnoreCase)) continue;

        var relative = remotePath[mapping.RemotePrefix.Length..]
            .TrimStart('/', '\\')
            .Replace('/', '\\');

        return Path.Combine(mapping.LocalPrefix, relative);
    }

    throw new InvalidOperationException($"No mapping found for path: {remotePath}");
}
```

Important behavior:

* Longest matching prefix should win.
* Normalize slashes before comparison.
* Reject unmapped paths by default.
* Optionally check `File.Exists(localPath)` before launching.

## Platform Abstraction

Keep the internal design cross-platform-shaped, but only implement Windows first.

```csharp
public interface IPlatformLauncher
{
    void RevealFile(string localPath);
    void OpenWithApp(string executablePath, string arguments, string localPath);
}
```

Windows implementation:

```csharp
public sealed class WindowsLauncher : IPlatformLauncher
{
    public void RevealFile(string localPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{localPath}\"",
            UseShellExecute = true
        });
    }

    public void OpenWithApp(string executablePath, string arguments, string localPath)
    {
        var resolvedArguments = arguments.Replace("{file}", localPath);

        Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = resolvedArguments,
            UseShellExecute = false
        });
    }
}
```

## Protocol Registration

For Windows, register a custom URL protocol in the registry.

Conceptual registry structure:

```text
HKEY_CURRENT_USER\Software\Classes\immich-bridge
  (Default) = "URL:Immich Bridge Protocol"
  "URL Protocol" = ""

HKEY_CURRENT_USER\Software\Classes\immich-bridge\shell\open\command
  (Default) = "C:\Path\To\ImmichBridge.exe" "%1"
```

The helper can support commands like:

```text
ImmichBridge.exe --register-protocol
ImmichBridge.exe --unregister-protocol
ImmichBridge.exe "immich-bridge://reveal?path=..."
```

Use `HKEY_CURRENT_USER` so admin rights are not required.

## Browser Integration

Start with Tampermonkey.

Reasons:

* Fastest iteration
* No need to package a browser extension immediately
* Good enough for a personal/local workflow
* Easier while Immich UI structure is still moving

Later, this can become a proper browser extension.

### Userscript Responsibilities

* Detect current asset detail page
* Get the current asset id
* Fetch asset metadata from Immich API
* Read the original/server-side path
* Add buttons to the UI:

  * Reveal in Explorer
  * Open in Photoshop
* Build and open the custom protocol URL

Example generated URL:

```js
const url = `immich-bridge://reveal?path=${encodeURIComponent(originalPath)}`;
window.location.href = url;
```

## Security Considerations

Do not blindly open arbitrary paths or commands.

Minimum safety rules:

* Only allow mapped paths.
* Never execute commands from the URI directly.
* Only allow app ids defined in config.
* Do not allow arbitrary executable paths from the URI.
* Decode and normalize paths carefully.
* Prefer `HKEY_CURRENT_USER` registration.
* Log rejected requests.

Good:

```text
immich-bridge://open?app=photoshop&path=/mapped/path/file.jpg
```

Bad:

```text
immich-bridge://open?exe=C:\Windows\System32\cmd.exe&args=...
```

## Error Handling

The helper should handle common failures clearly:

* No config file found
* Invalid URI
* Unknown action
* No path parameter
* No mapping found
* Local file does not exist
* Unknown app id
* Configured executable does not exist
* Process launch failed

For v1, errors can be written to a log file and optionally shown in a small message box.

## MVP Scope

### v1 Must Have

* Windows-only .NET 10 helper
* JSON config
* Prefix-based path mapping
* `immich-bridge://reveal?path=...`
* `immich-bridge://open?app=photoshop&path=...`
* Registry protocol registration command
* Tampermonkey script adding buttons to Immich asset view

### v1 Should Not Have

* Full cross-platform implementation
* Electron UI
* Full installer
* Immich server patch/fork
* Arbitrary command execution
* Complex rule engine
* Automatic app detection

## Future Extensions

* Proper browser extension
* Tray app with config UI
* Per-filetype default actions
* Multiple app buttons
* Open in Lightroom / Affinity / DaVinci / Blender
* Copy local path to clipboard
* Open containing folder
* macOS support
* Linux support
* Optional local HTTP API instead of protocol handler
* Immich plugin/native integration if Immich supports that in the future

## Suggested Repository Structure

```text
immich-bridge-helper/
  README.md
  src/
    ImmichBridge/
      ImmichBridge.csproj
      Program.cs
      Config.cs
      PathMapper.cs
      ProtocolRegistration.cs
      IPlatformLauncher.cs
      WindowsLauncher.cs
  userscript/
    immich-bridge.user.js
  examples/
    config.example.json
  docs/
    concept.md
```

## Codex Starting Tasks

1. Create a .NET 10 Windows console app.
2. Implement config loading from `%AppData%\\ImmichBridge\\config.json`.
3. Implement path mapping with longest-prefix matching.
4. Implement URI parsing for `immich-bridge://reveal` and `immich-bridge://open`.
5. Implement Windows Explorer reveal action.
6. Implement configured app launch action.
7. Implement protocol registration and unregistration commands.
8. Add a minimal Tampermonkey script that adds test buttons to Immich.
9. Replace test path with real current-asset original path from Immich API.
10. Add basic logging and useful error messages.

## Open Questions

* Which exact Immich API endpoint should be used to fetch the current asset path?
* Does the current logged-in Immich session allow the userscript to call that endpoint without extra auth handling?
* How stable is the current Immich asset detail page DOM?
* Should the helper show message boxes on errors or only log them?
* Should Photoshop be configured manually or auto-detected later?
* Should there be a dry-run/test command for validating path mappings?

## Preferred Development Direction

Build the local helper cleanly first, independent from Immich.

Then add the userscript.

This avoids debugging browser integration and Windows process launching at the same time.

Suggested order:

1. Manually run helper with sample URI.
2. Confirm path mapping works.
3. Confirm Explorer reveal works.
4. Confirm Photoshop open works.
5. Register protocol.
6. Test protocol from browser address bar.
7. Add Immich userscript buttons.
8. Integrate real asset path lookup.
