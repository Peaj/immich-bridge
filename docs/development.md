# Development and Advanced Usage

This page collects the lower-level details that are useful while developing, testing, or troubleshooting Immich Bridge. Normal users should use the first-run setup wizard instead of editing files manually.

## Configuration

Rerun setup at any time:

```powershell
.\ImmichBridge.exe --setup
```

The internal config file is stored at `%AppData%\ImmichBridge\config.json`:

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
    "ConfirmBeforeOpeningApps": false,
    "VerifyLocalFileExists": true,
    "LogFile": "%AppData%\\ImmichBridge\\logs\\helper.log"
  }
}
```

The browser add-ons currently expose `Reveal in Explorer`, `Copy original`, and `Open with...`. Direct app ids are supported by the companion app protocol but are not exposed from the Immich browser UI at the moment.

Prefer mappings that point to external libraries or other user-managed media folders. Avoid mapping Immich's internal upload/library storage unless you treat the mapped files as read-only from desktop apps.

## Companion App Commands

Install the running build to `%LOCALAPPDATA%\Programs\ImmichBridge`, register the protocol handler for the installed copy, and add the Start menu shortcut and Settings > Apps entry:

```powershell
.\ImmichBridge.exe --install
```

Setup does the same when you finish it, and launching a downloaded copy without arguments offers to install or update. Remove everything again, optionally including settings and logs:

```powershell
.\ImmichBridge.exe --uninstall
```

Register the protocol handler for the current build without installing it, which is handy while developing:

```powershell
dotnet run --project .\src\ImmichBridge -- --register-protocol
```

`--check` reports a protocol registered this way as failing, because it points outside the install folder.

Run the setup wizard:

```powershell
dotnet run --project .\src\ImmichBridge -- --setup
```

Check config, mappings, install location, protocol registration, and log path:

```powershell
dotnet run --project .\src\ImmichBridge -- --check
```

Validate path mapping without launching anything:

```powershell
dotnet run --project .\src\ImmichBridge -- --map-path "/mnt/immich-external/photos/2024/img.jpg"
```

Test the Windows Open With dialog from the terminal:

```powershell
dotnet run --project .\src\ImmichBridge -- --open-with "/mnt/immich-external/photos/2024/img.jpg"
```

This uses Windows' `SHOpenWithDialog` shell API. If the file type already has a default app, Windows may show a compact chooser or launch the selected app after confirmation depending on your system settings.

Test copying the original file to the clipboard:

```powershell
dotnet run --project .\src\ImmichBridge -- --copy "/mnt/immich-external/photos/2024/img.jpg"
```

The clipboard receives the file itself, the same as Explorer's Copy, so pasting into Explorer, mail attachments, or messengers uses the untouched original. Images that Windows GDI+ can decode (JPEG, PNG, GIF, BMP, TIFF) are also added as full-resolution pixels with EXIF orientation applied, so image editors paste them as a picture. PNG files additionally provide their original bytes in the `PNG` clipboard format to keep transparency. Other formats, such as videos, RAW, or HEIC, are copied as files only.

When testing protocol launches, inspect the helper log:

```powershell
Get-Content "$env:APPDATA\ImmichBridge\logs\helper.log" -Tail 80 -Wait
```

Manual protocol tests:

```text
immich-bridge://reveal?path=%2Fmnt%2Fimmich-external%2Fphotos%2F2024%2Fimg.jpg
immich-bridge://open?path=%2Fmnt%2Fimmich-external%2Fphotos%2F2024%2Fimg.jpg
immich-bridge://copy?path=%2Fmnt%2Fimmich-external%2Fphotos%2F2024%2Fimg.jpg
```

## Browser Integration

On Immich asset detail pages, the browser integration injects an Immich Bridge icon into the asset toolbar, opens a small action menu, calls `GET /api/assets/{id}` with the current browser session, reads `originalPath`, and launches `immich-bridge://` URLs.

The default `Open with...` action uses Windows' native app picker, so it does not need app ids hardcoded in the browser extension or userscript.

## Build and Test

```powershell
dotnet build .\ImmichBridge.slnx
dotnet test .\ImmichBridge.slnx
npm run check:extension
npm run lint:extension
npm run build:extensions
```

### Test builds from CI

Every CI run, for pull requests and for `main`, uploads test builds that are kept for 14 days. Download them from the **Artifacts** section of the run's summary page in GitHub Actions:

- `ImmichBridge-win-x64-buildN`: the single-file `ImmichBridge.exe`, built the same way as a release.
- `immich-bridge-extensions-buildN`:
  - `immich-bridge-firefox.zip`: load it in `about:debugging` > **This Firefox** > **Load Temporary Add-on**.
  - `chromium` and `edge`: load these folders unpacked from `chrome://extensions` or `edge://extensions` with developer mode enabled.

Test builds carry the version from the project files, so a build made after a release reports the same version as that release.

## Releases

Immich Bridge uses semantic versioning and GitHub Releases. Release tags use `vX.Y.Z`; the release workflow verifies that the tag, app version, userscript version, Firefox extension version, and Chromium-family extension version match. See [release.md](release.md) for the release process.

## Security Model

Immich Bridge never accepts executable paths or shell arguments from the URL. URLs can only name an action, an optional configured app id, and an Immich path. Paths must match a configured mapping, and the mapped local file must exist by default before anything is launched.
