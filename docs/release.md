# Release Process

Immich Bridge uses semantic versioning and GitHub Releases.

## Versioning

- `MAJOR`: breaking config, protocol, browser extension, or userscript changes.
- `MINOR`: new actions, setup features, browser integration, or installer improvements.
- `PATCH`: bug fixes and compatibility fixes.

Before creating a release tag, update:

- `Version`, `AssemblyVersion`, and `FileVersion` in `src/ImmichBridge/ImmichBridge.csproj`
- `@version` in `userscript/immich-bridge.user.js`
- `version` in `browser-extension/manifest.json`
- AMO submission notes in `browser-extension/amo-metadata.json`, when extension review guidance changes
- `CHANGELOG.md`

The release workflow rejects tags that do not match the project, userscript, and Firefox extension versions.

## Create a Release

```powershell
git tag v0.2.0
git push origin v0.2.0
```

The release workflow publishes:

- `ImmichBridge-win-x64-vX.Y.Z.zip`
- `immich-bridge-vX.Y.Z.user.js`
- `immich-bridge-firefox-vX.Y.Z.zip`
- `SHA256SUMS.txt`

## Installer Direction

The first release uses a self-contained `win-x64` ZIP from GitHub Releases. The app handles first-run setup, config creation, and protocol registration.

Production Firefox extension installs should use the public Mozilla Add-ons listing. The GitHub extension ZIP is included for source transparency and local testing. Consumer setup should open the store listing and must not attempt silent extension installation. Chrome and Edge packages should be added after the Firefox package stabilizes and should also use public store listings for normal users.

MSIX packaging and winget submission are planned after package identity, signing, and upgrade behavior are stable.
