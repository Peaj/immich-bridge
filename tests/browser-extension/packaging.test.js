const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { test } = require('node:test');

const { prepareExtensionSource } = require('../../scripts/prepare-extension-source');

function listFiles(root, relative = '') {
  return fs.readdirSync(path.join(root, relative), { withFileTypes: true }).flatMap((entry) => {
    const entryPath = path.posix.join(relative, entry.name);
    return entry.isDirectory() ? listFiles(root, entryPath) : [entryPath];
  });
}

for (const manifestFile of ['manifest.json', 'manifest.chromium.json']) {
  test(`${manifestFile} package contains only runtime files`, () => {
    const outputRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'immich-bridge-package-'));
    try {
      prepareExtensionSource({ manifestFile, outputRoot });
      const files = listFiles(outputRoot);
      const manifest = JSON.parse(fs.readFileSync(path.join(outputRoot, 'manifest.json'), 'utf8'));

      for (const file of files) {
        assert.match(
          file,
          /^(manifest\.json|background\.js|content\/.+|options\/.+|icons\/.+\.(png|svg))$/,
          `Unexpected file in package: ${file}`
        );
      }

      const iconPaths = [...Object.values(manifest.icons), ...Object.values(manifest.action.default_icon)];
      for (const iconPath of iconPaths) {
        assert.ok(files.includes(iconPath), `Manifest icon missing from package: ${iconPath}`);
      }

      const packagedIcons = files.filter((file) => file.startsWith('icons/'));
      for (const icon of packagedIcons) {
        assert.ok(iconPaths.includes(icon), `Icon not referenced by the manifest: ${icon}`);
      }
    } finally {
      fs.rmSync(outputRoot, { recursive: true, force: true });
    }
  });
}
