const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const sourceRoot = path.join(repoRoot, 'browser-extension');
const outputRoot = path.join(repoRoot, 'artifacts', 'edge-extension', 'source');

const copiedEntries = [
  'background.js',
  'content',
  'icons',
  'options'
];

fs.rmSync(outputRoot, { recursive: true, force: true });
fs.mkdirSync(outputRoot, { recursive: true });

for (const entry of copiedEntries) {
  fs.cpSync(path.join(sourceRoot, entry), path.join(outputRoot, entry), {
    recursive: true
  });
}

fs.copyFileSync(
  path.join(sourceRoot, 'manifest.chromium.json'),
  path.join(outputRoot, 'manifest.json')
);

console.log(`Prepared Edge extension source at ${path.relative(repoRoot, outputRoot)}`);
