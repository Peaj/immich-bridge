# Release Process

Immich Bridge uses semantic versioning and GitHub Releases.

## Versioning

- `MAJOR`: breaking config, protocol, or userscript changes.
- `MINOR`: new actions, setup features, or installer improvements.
- `PATCH`: bug fixes and compatibility fixes.

Before creating a release tag, update:

- `Version`, `AssemblyVersion`, and `FileVersion` in `src/ImmichBridge/ImmichBridge.csproj`
- `@version` in `userscript/immich-bridge.user.js`
- `CHANGELOG.md`

The release workflow rejects tags that do not match the project and userscript versions.

## Create a Release

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The release workflow publishes:

- `ImmichBridge-win-x64-vX.Y.Z.zip`
- `immich-bridge-vX.Y.Z.user.js`
- `SHA256SUMS.txt`

## Installer Direction

The first release uses a self-contained `win-x64` ZIP from GitHub Releases. The app handles first-run setup, config creation, and protocol registration.

MSIX packaging and winget submission are planned after package identity, signing, and upgrade behavior are stable.
