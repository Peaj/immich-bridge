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
- `version` in `browser-extension/manifest.chromium.json`
- AMO submission notes in `browser-extension/amo-metadata.json`, when extension review guidance changes
- `CHANGELOG.md`

The release workflow rejects tags that do not match the project, userscript, Firefox extension, and Chromium-family extension versions. Chrome and Edge packages both use `browser-extension/manifest.chromium.json`.

## Create a Release

```powershell
git tag v0.4.0
git push origin v0.4.0
```

The release workflow publishes:

- `ImmichBridge-win-x64-vX.Y.Z.zip`
- `immich-bridge-vX.Y.Z.user.js`
- `immich-bridge-firefox-vX.Y.Z.zip`
- `immich-bridge-chromium-vX.Y.Z.zip`
- `immich-bridge-edge-vX.Y.Z.zip`
- `SHA256SUMS.txt`

After the GitHub release is created, the workflow can independently submit the browser packages to Mozilla Add-ons, the Chrome Web Store, and Microsoft Edge Add-ons. Each store still reviews the submitted version before making it public.

## Automatic Browser Store Publishing

Store publishing is opt-in. Configure a store's credentials and variables in **GitHub repository settings > Secrets and variables > Actions**, then set that store's `PUBLISH_*` variable to `true`. A missing or false variable skips that store without affecting the GitHub release or the other stores.

### Firefox

1. Create AMO API credentials at <https://addons.mozilla.org/developers/addon/api/key/>.
2. Add repository secrets `AMO_JWT_ISSUER` and `AMO_JWT_SECRET`.
3. Add repository variable `PUBLISH_FIREFOX_EXTENSION` with value `true`.

The workflow uses `web-ext sign --channel listed` with `browser-extension/amo-metadata.json`. The add-on ID comes from `browser-extension/manifest.json`.

### Chrome

1. Enable the Chrome Web Store API in a Google Cloud project and create a service account with a JSON key.
2. In the Chrome Web Store Developer Dashboard, add the service account email under **Account** so it can manage this publisher's items.
3. Add the complete JSON key as repository secret `CHROME_SERVICE_ACCOUNT_JSON`.
4. Add repository variable `CHROME_PUBLISHER_ID` using the value shown under **Publisher > Settings**.
5. Add repository variable `CHROME_EXTENSION_ID` with value `ohghjemcjnaickehejdokfmjphpjoiag`.
6. Add repository variable `PUBLISH_CHROME_EXTENSION` with value `true`.

The workflow uses the Chrome Web Store API V2 to upload the package and submit it with `DEFAULT_PUBLISH`, which makes the update public after it passes review. See Google's [service account setup](https://developer.chrome.com/docs/webstore/service-accounts) and [Web Store API guide](https://developer.chrome.com/docs/webstore/using-api).

### Edge

1. In Partner Center, open **Microsoft Edge > Publish API**, enable the current API experience, and create API credentials.
2. Add repository secrets `EDGE_CLIENT_ID` and `EDGE_API_KEY`.
3. Add repository variable `EDGE_PRODUCT_ID` using the product GUID from the extension overview. This is not the public 32-character Edge extension ID.
4. Add repository variable `PUBLISH_EDGE_EXTENSION` with value `true`.

The workflow uploads and submits the package through the Microsoft Edge Add-ons Update REST API. API keys expire, so renew the key and update `EDGE_API_KEY` before its expiry date. See Microsoft's [Edge Add-ons API setup](https://learn.microsoft.com/en-us/microsoft-edge/extensions/update/api/using-addons-api).

All three jobs submit package updates to existing listings. Keep store listing text, screenshots, privacy declarations, and other dashboard metadata current manually; the Chrome and Edge package APIs do not update those fields.

## Installer Direction

The first release uses a self-contained `win-x64` ZIP from GitHub Releases. The app handles first-run setup, config creation, and protocol registration.

Production Firefox extension installs should use the public Mozilla Add-ons listing. Production Chrome installs should use the Chrome Web Store once published. Production Edge installs should use Microsoft Edge Add-ons once published. The GitHub extension ZIPs are included for source transparency and local testing. Consumer setup should open the store listing and must not attempt silent extension installation.

MSIX packaging and winget submission are planned after package identity, signing, and upgrade behavior are stable.
