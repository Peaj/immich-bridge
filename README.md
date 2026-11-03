# Immich Bridge

<img width="440" height="192" alt="grafik" align="right" src="https://github.com/user-attachments/assets/1859d9e7-0e3f-491b-af63-a791f182148a" />

[![Release](https://img.shields.io/github/v/release/Peaj/immich-bridge?label=release)](https://github.com/Peaj/immich-bridge/releases/latest)
[![License](https://img.shields.io/github/license/Peaj/immich-bridge?label=license)](LICENSE)
[![Last commit](https://img.shields.io/github/last-commit/Peaj/immich-bridge?label=last%20commit)](https://github.com/Peaj/immich-bridge/commits/main)
[![CI](https://img.shields.io/github/actions/workflow/status/Peaj/immich-bridge/ci.yml?branch=main&label=ci)](https://github.com/Peaj/immich-bridge/actions/workflows/ci.yml)

Immich Bridge adds local desktop actions to Immich asset pages. It lets you jump from an Immich photo or video in the browser to the matching local file on your Windows machine.

It consists of a browser add-on and a small Windows companion app. The add-on adds the Immich Bridge button to Immich, and the companion app opens the matching local file on your PC.

## Features

- Reveal the local file in Windows Explorer.
- Open the local file with Windows' native Open With dialog.
- Copy the original file to the clipboard, ready to paste into Explorer, email, messengers, or image editors.

Immich Bridge is useful when Immich stores assets from folders that are also available on your Windows workstation, for example through a mapped drive, SMB share, external disk, or synced folder.

> [!NOTE]
> Path mappings are meant for external libraries and other folders you manage yourself. Photos in Immich's own upload library are never opened in place, because Immich asks that its storage is not touched. With the browser add-on and companion app 0.6 or newer, Copy original and Open with download those photos through Immich and hand you a read-only copy. Reveal in Explorer is not offered for them, and no path mapping is needed.
>
> Photos edited in Immich (crop, rotate, mirror) also offer **Open edited copy with...**, a read-only copy with Immich's edits applied. **Open with...** still opens the untouched original. Immich keeps applying its edits on top of the original, so don't save an edited copy back over it, or the edits would be applied twice.

> [!NOTE]
> AI assistance was used during development of this project. Code, design, and release decisions remain reviewed and maintained by the project author.

## Install

Download `ImmichBridge-win-x64-vX.Y.Z.exe` from the [latest GitHub release](https://github.com/Peaj/immich-bridge/releases/latest) and run it. It doesn't matter where you save it.

On first launch, Immich Bridge opens a setup wizard that:

- asks you to copy a full file path from Immich's asset Details panel;
- lets you choose the matching local file or the folder that contains it;
- derives and validates the path mapping automatically;
- installs itself to `%LOCALAPPDATA%\Programs\ImmichBridge` and sets up the local browser-to-app launcher there;
- points you to the browser add-ons, with the Tampermonkey userscript as a fallback.

After setup you can delete the downloaded file. No admin rights are required.

To update, download the new release and run it; it offers to update the installed copy and keeps your settings. Rerun setup from the **Immich Bridge** Start menu entry, and uninstall it from **Settings > Apps**.

### Browser Add-ons

Click your browser to install the add-on:

<p>
  <a href="https://chromewebstore.google.com/detail/ohghjemcjnaickehejdokfmjphpjoiag"><img alt="Available in the Chrome Web Store" src="browser-extension/store-badges/chrome-web-store.png" height="48"></a>
  <a href="https://microsoftedge.microsoft.com/addons/detail/immich-bridge/bgipocndkokcllfjgmiicakhlbddnjij"><img alt="Get it from Microsoft Edge" src="browser-extension/store-badges/edge-add-ons.png" height="48"></a>
  <a href="https://addons.mozilla.org/en-US/firefox/addon/immich-bridge/"><img alt="Get the add-on for Firefox" src="browser-extension/store-badges/firefox-add-ons.png" height="48"></a>
</p>

After installing, open the Immich Bridge extension options page, enter your Immich base URL, and grant access for that site. The extension stores only that local browser setting and injects the toolbar button only on the configured Immich origin.

For unsupported browsers, store-review delays, or users who prefer Tampermonkey, install `userscript/immich-bridge.user.js` in Tampermonkey. You can use the copy bundled in the release ZIP, the `.user.js` release asset, or the latest script directly from GitHub:

```text
https://raw.githubusercontent.com/Peaj/immich-bridge/main/userscript/immich-bridge.user.js
```

## Documentation

- [Privacy policy](PRIVACY.md)
- [Development and advanced usage](docs/development.md)
- [Release process](docs/release.md)

## Security

Immich Bridge never accepts executable paths or shell arguments from the browser. It only receives an action and an Immich asset path, maps that path through your local configuration, and requires the mapped local file to exist before launching anything.
