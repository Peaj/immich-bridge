# Changelog

All notable changes to Immich Bridge are tracked here.

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
