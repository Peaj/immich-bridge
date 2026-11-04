# Changelog

All notable changes to Immich Bridge are tracked here.

## Unreleased

The browser add-ons now ask for the `nativeMessaging` permission, so Chrome and Edge ask you to accept it once after the update.

- Photos in Immich's own upload library are no longer opened in place. Copy original and Open copy with download them through Immich and use a read-only copy, and Reveal in Explorer is not offered for them. No path mapping is needed for these photos.
- Photos edited in Immich get **Open edited copy with...**, which opens a read-only copy with Immich's crop, rotation, and mirroring applied. **Open with...** still opens the original.
- Read-only copies are kept in `%TEMP%\Immich Bridge\Copies` and deleted after 7 days.
- The browser add-ons talk to the companion app through native messaging. The options page shows whether the companion is installed, set up, and up to date.
- Copy original confirms in the page, and errors such as an unmapped path or a missing file appear as a message in Immich instead of a Windows dialog.
- The action menu hides actions the installed companion doesn't support and links to the update when the companion is older than the add-on.
- Companion apps before 0.6 and the userscript keep working through the `immich-bridge://` link.
- The companion app registers its native messaging host for Chrome, Edge, and Firefox when it installs, and removes it when it uninstalls. `--check` reports the registration.
- Fixed an "already registered" error in the browser console when the add-on is installed or its Immich URL is saved.
- Browser extension packages now contain only the files the extension loads, instead of also shipping store listing material such as the artwork source file, screenshots, and store badges. The Firefox package shrinks from 10.9 MB to under 40 KB.
- Firefox store submissions sign the released package instead of the repository folder.
- The companion app ZIP no longer includes the browser extension folder; the setup wizard's artwork has been built into the app since 0.5.0.

## 0.5.0

The browser add-ons' new `Copy original` action needs Immich Bridge 0.5.0 or newer. Update the companion app before using it.

- Added a `Copy original` action that puts the original local file on the clipboard, with full-resolution image pixels for pasting into image editors.
- When the browser asks for an action the installed companion app doesn't know yet, it now explains that a newer version is needed and offers to open the download page.
- Setup now installs Immich Bridge to `%LOCALAPPDATA%\Programs\ImmichBridge`, so the browser link no longer breaks when the downloaded file is moved, deleted, or run from inside the ZIP.
- Running a newer downloaded copy offers to update the installed one; `--install` and `--uninstall` do the same from the command line.
- Added a Start menu entry for rerunning setup and a Settings > Apps entry for uninstalling.
- `--check` now reports when the browser link points outside the install folder.
- Releases include the standalone `ImmichBridge-win-x64-vX.Y.Z.exe`, and the app has its own icon.

## 0.4.1

- Fixed browser extension updates and restarts repeatedly opening the setup page.
- Added opt-in GitHub Actions publishing for Firefox Add-ons, the Chrome Web Store, and Microsoft Edge Add-ons.

## 0.4.0

- Simplified first-run setup by deriving path mappings from a full Immich file path and the matching local file.
- Added an optional setup shortcut for selecting the containing local folder when the exact file is hard to find.
- Added setup branding with the Immich Bridge app icon, welcome-page artwork, and official browser store badges.
- Added user-facing guidance for treating Immich internal-library files as read-only from desktop apps.
- Moved advanced configuration, command-line usage, protocol tests, and build details into dedicated documentation.

## 0.3.0

- Added Chromium-family extension packages for Chrome Web Store and Microsoft Edge Add-ons submission.
- Added local Chrome and Edge extension launch tasks for unpacked browser testing.
- Improved the extension options page to make companion app installation more visible.

## 0.2.1

- Updated Firefox add-on metadata for AMO review.
- Added explicit unofficial Immich integration disclosure.
- Declared local-only selected asset path handling under Firefox add-on data permissions.
- Added the MIT license.
- Added repository badges and an AI assistance note to the GitHub README.

## 0.2.0

- Added the Firefox WebExtension package for Immich UI integration.
- Added extension onboarding for configuring the Immich origin and requesting host permission only for that site.
- Kept the Tampermonkey userscript as the fallback browser integration path.
- Improved toolbar injection so it no longer depends on localized Immich button labels.
- Improved asset viewer handling across regular photo routes and map photo routes.
- Added Firefox extension linting and packaging to CI/release validation.
- Added extension release documentation and AMO submission metadata.
- Updated the browser extension icon.

## 0.1.0

- Initial Windows helper for `immich-bridge://` protocol actions.
- Reveal mapped Immich assets in Windows Explorer.
- Open mapped Immich assets with Windows' native Open With dialog.
- Optional configured app launches by app id.
- First-run setup wizard for path mapping and protocol registration.
- Firefox WebExtension for Immich asset toolbar integration.
- Tampermonkey userscript fallback for unsupported browsers.
- GitHub Actions CI and release packaging.
