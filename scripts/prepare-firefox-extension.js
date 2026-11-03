const path = require('node:path');
const { prepareExtensionSource, repoRoot } = require('./prepare-extension-source');

const outputRoot = prepareExtensionSource({
  manifestFile: 'manifest.json',
  outputRoot: path.join(repoRoot, 'artifacts', 'firefox-extension', 'source')
});

console.log(`Prepared Firefox extension source at ${path.relative(repoRoot, outputRoot)}`);
