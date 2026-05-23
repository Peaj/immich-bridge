const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const firefoxManifestPath = path.join(repoRoot, 'browser-extension', 'manifest.json');
const chromiumManifestPath = path.join(repoRoot, 'artifacts', 'chromium-extension', 'source', 'manifest.json');

const firefoxManifest = JSON.parse(fs.readFileSync(firefoxManifestPath, 'utf8'));
const chromiumManifest = JSON.parse(fs.readFileSync(chromiumManifestPath, 'utf8'));

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

assert(chromiumManifest.manifest_version === 3, 'Chromium manifest must use Manifest V3.');
assert(chromiumManifest.version === firefoxManifest.version, 'Chromium manifest version must match Firefox manifest version.');
assert(chromiumManifest.background?.service_worker === 'background.js', 'Chromium manifest must use background.service_worker.');
assert(!chromiumManifest.background?.scripts, 'Chromium manifest must not use Firefox background.scripts.');
assert(!chromiumManifest.browser_specific_settings, 'Chromium manifest must not include Firefox browser_specific_settings.');
assert(Array.isArray(chromiumManifest.optional_host_permissions), 'Chromium manifest must declare optional_host_permissions.');

for (const relativePath of [
  chromiumManifest.background.service_worker,
  chromiumManifest.options_ui?.page,
  chromiumManifest.icons?.['128'],
  chromiumManifest.action?.default_icon?.['128']
]) {
  assert(relativePath, `Missing manifest path: ${relativePath}`);
  assert(fs.existsSync(path.join(repoRoot, 'artifacts', 'chromium-extension', 'source', relativePath)), `Manifest path does not exist: ${relativePath}`);
}

console.log('Chromium extension manifest validation OK');
