const path = require('node:path');
const { prepareExtensionSource, repoRoot } = require('./prepare-extension-source');

const outputRoot = prepareExtensionSource({
  manifestFile: 'manifest.chromium.json',
  outputRoot: path.join(repoRoot, 'artifacts', 'chromium-extension', 'source')
});

console.log(`Prepared Chromium extension source at ${path.relative(repoRoot, outputRoot)}`);
