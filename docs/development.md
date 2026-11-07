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

The browser add-ons expose `Reveal in Explorer`, `Copy original`, and `Open with...` for external libraries. Internal-library photos get `Copy original` and `Open copy with...`, both as read-only copies, and photos edited in Immich also get `Open edited copy with...`. Direct app ids are supported by the companion app protocol but are not exposed from the Immich browser UI at the moment.

Mappings should point to external libraries or other user-managed media folders. The browser add-on never hands out Immich's internal upload-library files by path; it opens them as read-only copies (see [Read-only copies](#read-only-copies)). Only the userscript and companion apps before 0.6 would still open a mapped internal file directly.

## Companion App Commands

Install the running build to `%LOCALAPPDATA%\Programs\ImmichBridge`, register the protocol handler and native messaging host for the installed copy, and add the Start menu shortcut and Settings > Apps entry:

```powershell
.\ImmichBridge.exe --install
```

Setup does the same when you finish it, and launching a downloaded copy without arguments offers to install or update. Remove everything again, optionally including settings and logs:

```powershell
.\ImmichBridge.exe --uninstall
```

Register the protocol handler and native messaging host for the current build without installing it, which is handy while developing:

```powershell
dotnet run --project .\src\ImmichBridge -- --register-protocol
```

`--check` reports both registrations as failing in that case, because they point outside the install folder.

Run the setup wizard:

```powershell
dotnet run --project .\src\ImmichBridge -- --setup
```

Check config, mappings, install location, protocol and native messaging registration, and log path:

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

On Immich asset detail pages, the browser integration injects an Immich Bridge icon into the asset toolbar, opens a small action menu, calls `GET /api/assets/{id}` with the current browser session, and reads `originalPath`. The browser extension then hands the action to the companion app through native messaging. The userscript, and the extension when the companion is older than 0.6 or not installed, launch `immich-bridge://` URLs instead.

The default `Open with...` action uses Windows' native app picker, so it does not need app ids hardcoded in the browser extension or userscript.

## Native Messaging

The companion app registers itself as the native messaging host `de.peaj.immich_bridge` whenever it installs (`--install`, the setup wizard, or the install prompt) and for dev builds with `--register-protocol`. It writes two host manifests next to the exe and points these per-user registry keys at them:

| Browser | Registry key (HKCU) | Manifest |
|---|---|---|
| Chrome | `Software\Google\Chrome\NativeMessagingHosts\de.peaj.immich_bridge` | `de.peaj.immich_bridge.chromium.json` |
| Edge | `Software\Microsoft\Edge\NativeMessagingHosts\de.peaj.immich_bridge` | `de.peaj.immich_bridge.chromium.json` |
| Firefox | `Software\Mozilla\NativeMessagingHosts\de.peaj.immich_bridge` | `de.peaj.immich_bridge.firefox.json` |

Only the store builds may connect: the Chromium manifest lists the Chrome Web Store and Edge Add-ons extension IDs, and the Firefox manifest lists the add-on ID `immich-bridge@peaj.de`. Web pages and other extensions cannot reach the host.

When a browser starts the exe (with a `chrome-extension://` origin, or with a manifest path and the Firefox add-on ID), it runs as the host: it reads length-prefixed JSON messages from stdin and answers on stdout, without a console or dialogs.

- `{"type":"hello"}` answers with `ok`, the protocol version (3), the app `version`, the supported `actions` and `features`, and whether a config exists (`configured`). The extension uses it for the options page status, to hide actions the companion doesn't know, and to offer an update.
- `{"type":"run","action":"reveal|copy|open","path":"<Immich path>"}` maps and checks the path exactly like a browser-link launch. It answers `{"ok":true}` or `{"ok":false,"code":...,"message":...}` with the code `unsupported-action`, `not-configured`, `not-mapped`, `file-missing`, or `failed`.

- `{"type":"copy-start"|"copy-chunk"|"copy-end", ...}` receives a file download for a read-only copy; see below. `hello` lists `copies` in `features` when the companion supports this.

Copy runs inside the host, and the page shows the confirmation. Reveal and Open with need windows that outlive the host, but browsers end the host's Windows job as soon as it answers. So the host starts a detached copy of the exe with the matching `immich-bridge://` URL, using `CREATE_BREAKAWAY_FROM_JOB`.

### Read-only copies

Internal-library assets (`libraryId` is `null`) and the **Open edited copy with...** entry use copies instead of paths:

1. The content script downloads `GET /api/assets/{id}/original` with the page's Immich session. For edited copies it adds `?edited=true`, which returns Immich's crop/rotate/mirror applied.
2. It streams the file over a `runtime.connect` port named `immich-bridge:copy` to the background script. The background script checks that the port comes from the configured Immich site and relays to a `connectNative` port.
3. The messages are `copy-start` (`purpose` `open` or `clipboard`, `fileName`, optional `size`), then `copy-chunk` with up to 1 MiB of base64 data each, then `copy-end`. The page waits for every reply before sending the next message.
4. The host writes the file to `%TEMP%\Immich Bridge\Copies\<timestamp-id>\<file name>` and marks it read-only. For `clipboard` it copies it to the clipboard. For `open` it starts `ImmichBridge.exe --open-copy "<copy>"` detached, which shows the Windows Open With dialog. `--open-copy` refuses any path outside the copies folder.
5. Copies older than 7 days are deleted the next time a browser starts the host. Copies still open in an app are skipped.

If the companion is older than 0.6 or doesn't report `copies`, internal assets only show an update hint. They never fall back to the `immich-bridge://` link, which would use the original path.

### Folder setup in the browser

When `hello` lists `mappings` in `features`, the options page shows and edits the path mappings. It sends these messages with `runtime.sendNativeMessage` itself. Only extension pages can use native messaging, and the background script relays nothing but `run` and the copy messages from Immich pages, so a web page can't read or change the mappings.

- `{"type":"get-mappings"}` answers with `mappings` (`remotePrefix`, `localPrefix`, and whether the folder `exists`) and `configExists`.
- `{"type":"suggest-mapping","path":"<Immich file path>"}` looks for the file on this computer. For `/external/photos/2024/IMG_1.jpg` it checks `photos\2024\IMG_1.jpg`, then `2024\IMG_1.jpg`, and so on, below every fixed, removable, and network drive root, their direct subfolders, and the Pictures and Videos folders. Longer matching tails rank first, and a drive that doesn't answer within 10 seconds is skipped. It answers up to five `suggestions` with `remotePrefix`, `localPrefix`, and the `localPath` it found.
- `{"type":"check-mapping","path":"...","mappings":[...]}` maps a path with unsaved mappings and answers the `localPath` and whether the file `exists`, or the code `not-mapped`.
- `{"type":"save-mappings","mappings":[...]}` replaces the mappings and keeps apps, options, and `AdditionalExtensionIds`. Immich prefixes must start with `/`, local folders must be full paths to existing folders, and each Immich prefix may appear once; otherwise it answers `invalid-mapping` and changes nothing. A config file that can't be read is never overwritten (`config-invalid`).

When a `run` fails with `not-mapped`, `not-configured`, or `file-missing`, the toast in Immich offers **Set up folders**. It asks the background script (`immich-bridge:open-setup`, accepted only from the configured Immich site) to store the photo's path in session storage and open the options page, which fills it in and searches right away. Saving still needs a click.

A config without mappings is a finished setup: the wizard's **Set up folders later in the browser add-on** option writes one, and `--check` passes without mappings. Rerunning the wizard keeps the other mappings, apps, and options, and replaces only a mapping with the same Immich prefix.

Unpacked Chromium extensions get a different extension ID than the store build. To test one, add its ID to `config.json` and register again:

```json
"Options": {
  "AdditionalExtensionIds": ["abcdefghijklmnopabcdefghijklmnop"]
}
```

Rerunning the setup wizard keeps this list. The Firefox add-on ID is fixed in `manifest.json`, so `web-ext run` works without extra configuration.

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

Mappings can be changed from the add-on's options page, never from a web page: Immich pages can only reach `run` and the copy messages through the background script. Apps (executables and arguments) can't be set from the browser at all and stay in `config.json`.
