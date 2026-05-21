# Immich Bridge Firefox Extension

This is the Firefox-first WebExtension package for Immich Bridge. It replaces the Tampermonkey UI injection for supported browsers while keeping the userscript as a fallback.

## Local Testing

```powershell
npx --yes web-ext lint --source-dir .\browser-extension
npx --yes web-ext run --source-dir .\browser-extension
```

After Firefox opens, use the Immich Bridge options page to enter the Immich base URL. The extension requests host permission only for that origin, then injects the toolbar button on Immich asset detail pages.

## Release Direction

Production Firefox installs should come from the public AMO listing once published. GitHub release ZIPs are for transparency and fallback testing, not silent consumer installation.
