# Changelog

All notable changes to Immich Bridge are tracked here.

## Unreleased

- Added Chromium-family extension packages for Chrome Web Store and Microsoft Edge Add-ons submission.
- Added local Chrome and Edge extension launch tasks for unpacked browser testing.

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
