# Immich Bridge Firefox Extension

This is the Firefox-first WebExtension package for Immich Bridge. It replaces the Tampermonkey UI injection for supported browsers while keeping the userscript as a fallback.

Immich Bridge is an unofficial integration for Immich and is not affiliated with or endorsed by the Immich project.

## Desktop Companion Required

This add-on requires the Immich Bridge desktop companion app for local file actions. Firefox extensions cannot directly open Windows Explorer or launch desktop apps, so the companion app handles those OS-level actions through the `immich-bridge://` protocol.

Install the latest desktop companion app from the [Immich Bridge latest release](https://github.com/Peaj/immich-bridge/releases/latest).

The extension only runs on the Immich URL you configure. When you click an Immich Bridge action, it reads the selected Immich asset's original path and sends only that path to the locally installed companion app. It does not send page content, asset paths, telemetry, or any other data to remote servers.

## Local Testing

```powershell
npx --yes web-ext lint --source-dir .\browser-extension
npx --yes web-ext run --source-dir .\browser-extension
```

After Firefox opens, use the Immich Bridge options page to enter the Immich base URL. The extension requests host permission only for that origin, then injects the toolbar button on Immich asset detail pages.

## Release Direction

Production Firefox installs should come from the public AMO listing once published. GitHub release ZIPs are for transparency and fallback testing, not silent consumer installation.
