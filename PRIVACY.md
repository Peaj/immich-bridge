# Privacy Policy

Immich Bridge does not collect, sell, transfer, or share user data with third parties.

The browser extension stores the configured Immich URL locally in the browser. This setting is used to request access only for the Immich site selected by the user.

When the user clicks an Immich Bridge action, the extension reads the selected Immich asset path from the configured Immich web app and passes only that path to the locally installed Immich Bridge desktop companion app, through the browser's native messaging channel or the `immich-bridge://` protocol. The extension also asks the companion app for its version and supported actions, so it can show whether the companion needs an update. This exchange stays on the user's computer.

For photos stored in Immich's own upload library, and for copies with Immich's edits applied, the extension downloads the photo from the user's configured Immich server using the user's existing Immich session. It passes the file only to the local companion app, which saves it as a read-only copy in the user's temporary folder and deletes it after 7 days.

The extension's options page can show and change the folder mappings in that local configuration. It sends them, and an Immich file path the user enters or chooses to set up, only to the local companion app, which can search the user's drives for the matching file. The mappings are stored only by the companion app.

The desktop companion app maps the received Immich path to a local Windows path according to the user's local configuration. It can then reveal the mapped file in Windows Explorer or open it with a local desktop application.

Immich Bridge does not send page content, asset paths, telemetry, analytics, browsing history, credentials, or personal information to remote servers.

Immich Bridge is an unofficial integration for Immich and is not affiliated with or endorsed by the Immich project.
