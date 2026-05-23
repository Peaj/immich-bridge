# Immich Bridge Browser Extension

This is the WebExtension package for Immich Bridge. It replaces the Tampermonkey UI injection for supported browsers while keeping the userscript as a fallback. Firefox, Chrome, and Edge packages share the same source files with browser-specific packaging.

Immich Bridge is an unofficial integration for Immich and is not affiliated with or endorsed by the Immich project.

## Desktop Companion Required

This add-on requires the Immich Bridge desktop companion app for local file actions. Browser extensions cannot directly open Windows Explorer or launch desktop apps, so the companion app handles those OS-level actions through the `immich-bridge://` protocol.

Install the latest desktop companion app from the [Immich Bridge latest release](https://github.com/Peaj/immich-bridge/releases/latest).

The extension only runs on the Immich URL you configure. When you click an Immich Bridge action, it reads the selected Immich asset's original path and sends only that path to the locally installed companion app. It does not send page content, asset paths, telemetry, or any other data to remote servers.

## Local Testing

```powershell
npx --yes web-ext lint --source-dir .\browser-extension
npx --yes web-ext run --source-dir .\browser-extension
npm run build:chromium-extension
npm run build:edge-extension
```

After Firefox opens, use the Immich Bridge options page to enter the Immich base URL. The extension requests host permission only for that origin, then injects the toolbar button on Immich asset detail pages.

For Chrome testing, use the VS Code task `extension: run in Chrome` or load `artifacts/chromium-extension/source` as an unpacked extension after running `npm run prepare:chromium-extension`.

For Edge testing, use the VS Code task `extension: run in Edge` or load `artifacts/edge-extension/source` as an unpacked extension after running `npm run prepare:edge-extension`.

## Release Direction

Production Firefox installs should come from the public AMO listing once published. Production Chrome installs should come from the Chrome Web Store once published. Production Edge installs should come from Microsoft Edge Add-ons once published. GitHub release ZIPs are for transparency and fallback testing, not silent consumer installation.
