const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const firefoxManifestPath = path.join(repoRoot, 'browser-extension', 'manifest.json');
const edgeManifestPath = path.join(repoRoot, 'artifacts', 'edge-extension', 'source', 'manifest.json');

const firefoxManifest = JSON.parse(fs.readFileSync(firefoxManifestPath, 'utf8'));
const edgeManifest = JSON.parse(fs.readFileSync(edgeManifestPath, 'utf8'));

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

assert(edgeManifest.manifest_version === 3, 'Edge manifest must use Manifest V3.');
assert(edgeManifest.version === firefoxManifest.version, 'Edge manifest version must match Firefox manifest version.');
assert(edgeManifest.background?.service_worker === 'background.js', 'Edge manifest must use background.service_worker.');
assert(!edgeManifest.background?.scripts, 'Edge manifest must not use Firefox background.scripts.');
assert(!edgeManifest.browser_specific_settings, 'Edge manifest must not include Firefox browser_specific_settings.');
assert(Array.isArray(edgeManifest.optional_host_permissions), 'Edge manifest must declare optional_host_permissions.');

for (const relativePath of [
  edgeManifest.background.service_worker,
  edgeManifest.options_ui?.page,
  edgeManifest.icons?.['128'],
  edgeManifest.action?.default_icon?.['128']
]) {
  assert(relativePath, `Missing manifest path: ${relativePath}`);
  assert(fs.existsSync(path.join(repoRoot, 'artifacts', 'edge-extension', 'source', relativePath)), `Manifest path does not exist: ${relativePath}`);
}

console.log('Edge extension manifest validation OK');
