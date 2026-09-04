const assert = require('node:assert/strict');
const { test } = require('node:test');

const { publishChromeExtension } = require('../../scripts/publish-chrome-extension');
const { publishEdgeExtension } = require('../../scripts/publish-edge-extension');

function jsonResponse(body, { status = 200, headers = {} } = {}) {
  return new Response(JSON.stringify(body), { status, headers });
}

test('Chrome publishing authenticates, uploads, and submits the package', async () => {
  const calls = [];
  const { privateKey } = require('node:crypto').generateKeyPairSync('rsa', { modulusLength: 1024 });
  const responses = [
    jsonResponse({ access_token: 'token' }),
    jsonResponse({ uploadState: 'SUCCEEDED', crxVersion: '0.5.0' }),
    jsonResponse({ name: 'publishers/publisher/items/extension', state: 'PENDING_REVIEW' }),
  ];
  const fetchImpl = async (url, options = {}) => {
    calls.push({ url, options });
    return responses.shift();
  };

  await publishChromeExtension(
    {
      packageBuffer: Buffer.from('package'),
      publisherId: 'publisher',
      extensionId: 'extension',
      serviceAccount: {
        client_email: 'publisher@example.test',
        private_key: privateKey.export({ type: 'pkcs8', format: 'pem' }),
      },
    },
    { fetchImpl },
  );

  assert.equal(calls.length, 3);
  assert.match(calls[1].url, /\/upload\/v2\/publishers\/publisher\/items\/extension:upload$/);
  assert.match(calls[2].url, /\/v2\/publishers\/publisher\/items\/extension:publish$/);
});

test('Edge publishing waits for package processing before submitting', async () => {
  const calls = [];
  const responses = [
    jsonResponse({}, { status: 202, headers: { Location: 'upload-operation' } }),
    jsonResponse({ status: 'Succeeded' }),
    jsonResponse({}, { status: 202, headers: { Location: 'publish-operation' } }),
    jsonResponse({ status: 'Succeeded' }),
  ];
  const fetchImpl = async (url, options = {}) => {
    calls.push({ url, options });
    return responses.shift();
  };

  await publishEdgeExtension(
    {
      packageBuffer: Buffer.from('package'),
      productId: 'product',
      clientId: 'client',
      apiKey: 'key',
      notes: 'Release notes',
    },
    { fetchImpl },
  );

  assert.equal(calls.length, 4);
  assert.match(calls[0].url, /\/products\/product\/submissions\/draft\/package$/);
  assert.match(calls[1].url, /\/draft\/package\/operations\/upload-operation$/);
  assert.match(calls[2].url, /\/products\/product\/submissions$/);
  assert.match(calls[3].url, /\/submissions\/operations\/publish-operation$/);
});
