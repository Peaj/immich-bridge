# Immich Bridge

Immich Bridge is a small Windows helper that adds workstation-native actions to Immich assets. A Tampermonkey userscript adds buttons to Immich, and a local .NET helper handles `immich-bridge://` protocol links by mapping Immich server paths to local Windows paths.

## Features

- Reveal the mapped local file in Windows Explorer.
- Open the mapped local file in a configured desktop app such as Photoshop.
- Register and unregister the `immich-bridge://` URL protocol under `HKEY_CURRENT_USER`.
- Load JSON config from `%AppData%\ImmichBridge\config.json`.
- Reject unmapped paths and unknown app ids by default.
- Log errors to `%AppData%\ImmichBridge\logs\helper.log`.

## Build and Test

```powershell
dotnet build .\ImmichBridge.slnx
dotnet test .\ImmichBridge.slnx
```

## Configure

Create `%AppData%\ImmichBridge\config.json` from `examples/config.example.json` and adjust mappings and app paths.

```json
{
  "Mappings": [
    {
      "RemotePrefix": "/mnt/immich-external/photos",
      "LocalPrefix": "Z:\\Photos"
    }
  ],
  "Apps": {
    "photoshop": {
      "ExecutablePath": "C:\\Program Files\\Adobe\\Adobe Photoshop 2025\\Photoshop.exe",
      "Arguments": "\"{file}\""
    }
  },
  "Options": {
    "AllowOnlyMappedPaths": true,
    "ConfirmBeforeOpeningApps": false,
    "VerifyLocalFileExists": true,
    "LogFile": "%AppData%\\ImmichBridge\\logs\\helper.log"
  }
}
```

## Usage

Register the protocol handler:

```powershell
dotnet run --project .\src\ImmichBridge -- --register-protocol
```

Validate path mapping without launching anything:

```powershell
dotnet run --project .\src\ImmichBridge -- --map-path "/mnt/immich-external/photos/2024/img.jpg"
```

Manual protocol tests:

```text
immich-bridge://reveal?path=%2Fmnt%2Fimmich-external%2Fphotos%2F2024%2Fimg.jpg
immich-bridge://open?app=photoshop&path=%2Fmnt%2Fimmich-external%2Fphotos%2F2024%2Fimg.jpg
```

Install `userscript/immich-bridge.user.js` in Tampermonkey. On Immich asset detail pages, it injects an Immich Bridge icon into the asset toolbar, opens a small action menu, calls `GET /api/assets/{id}` with the current browser session, reads `originalPath`, and launches `immich-bridge://` URLs.

## Security Model

Immich Bridge never accepts executable paths or shell arguments from the URL. URLs can only name an action, a configured app id, and an Immich path. By default, paths must match a configured mapping and the mapped local file must exist before anything is launched.
