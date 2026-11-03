const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const sourceRoot = path.join(repoRoot, 'browser-extension');

// Only the files the extension loads at runtime. browser-extension/ also holds store
// listing material (screenshots, badges, artwork sources, AMO metadata) that must not ship.
const runtimeEntries = [
  'background.js',
  'content',
  'options'
];

function manifestIconPaths(manifest) {
  const iconSets = [manifest.icons, manifest.action?.default_icon];
  return [...new Set(iconSets.flatMap((icons) => Object.values(icons ?? {})))];
}

function prepareExtensionSource({ manifestFile, outputRoot }) {
  const manifest = JSON.parse(fs.readFileSync(path.join(sourceRoot, manifestFile), 'utf8'));

  fs.rmSync(outputRoot, { recursive: true, force: true });
  fs.mkdirSync(outputRoot, { recursive: true });

  for (const entry of [...runtimeEntries, ...manifestIconPaths(manifest)]) {
    const target = path.join(outputRoot, entry);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.cpSync(path.join(sourceRoot, entry), target, { recursive: true });
  }

  fs.copyFileSync(path.join(sourceRoot, manifestFile), path.join(outputRoot, 'manifest.json'));
  return outputRoot;
}

module.exports = { prepareExtensionSource, manifestIconPaths, repoRoot };
