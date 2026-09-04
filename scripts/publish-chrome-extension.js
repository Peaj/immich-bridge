const crypto = require('node:crypto');
const fs = require('node:fs/promises');

const chromeWebStoreScope = 'https://www.googleapis.com/auth/chromewebstore';
const chromeWebStoreBaseUrl = 'https://chromewebstore.googleapis.com';

function requireValue(value, name) {
  if (!value) {
    throw new Error(`${name} is required.`);
  }

  return value;
}

function encodeBase64Url(value) {
  return Buffer.from(value).toString('base64url');
}

async function readJsonResponse(response, operation) {
  const body = await response.text();
  let data = {};

  if (body) {
    try {
      data = JSON.parse(body);
    } catch {
      throw new Error(`${operation} returned invalid JSON (${response.status}): ${body}`);
    }
  }

  if (!response.ok) {
    throw new Error(`${operation} failed (${response.status}): ${body || response.statusText}`);
  }

  return data;
}

async function getAccessToken(serviceAccount, fetchImpl) {
  const now = Math.floor(Date.now() / 1000);
  const header = encodeBase64Url(JSON.stringify({ alg: 'RS256', typ: 'JWT' }));
  const claims = encodeBase64Url(
    JSON.stringify({
      iss: requireValue(serviceAccount.client_email, 'Service account client_email'),
      scope: chromeWebStoreScope,
      aud: 'https://oauth2.googleapis.com/token',
      iat: now,
      exp: now + 3600,
    }),
  );
  const unsignedToken = `${header}.${claims}`;
  const signature = crypto.sign(
    'RSA-SHA256',
    Buffer.from(unsignedToken),
    requireValue(serviceAccount.private_key, 'Service account private_key'),
  );
  const assertion = `${unsignedToken}.${signature.toString('base64url')}`;
  const body = new URLSearchParams({
    grant_type: 'urn:ietf:params:oauth:grant-type:jwt-bearer',
    assertion,
  });
  const response = await fetchImpl('https://oauth2.googleapis.com/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
  });
  const data = await readJsonResponse(response, 'Chrome authentication');

  return requireValue(data.access_token, 'Chrome access token');
}

async function waitForUpload({ statusUrl, headers, fetchImpl, sleep, attempts = 30 }) {
  for (let attempt = 1; attempt <= attempts; attempt += 1) {
    const response = await fetchImpl(statusUrl, { headers });
    const status = await readJsonResponse(response, 'Chrome upload status');
    const uploadState = status.lastAsyncUploadState;

    if (uploadState === 'SUCCEEDED') {
      return;
    }

    if (uploadState && uploadState !== 'UPLOAD_IN_PROGRESS') {
      throw new Error(`Chrome package processing ended with state ${uploadState}.`);
    }

    if (attempt < attempts) {
      await sleep(5000);
    }
  }

  throw new Error('Timed out waiting for Chrome to process the extension package.');
}

async function publishChromeExtension(
  { packageBuffer, publisherId, extensionId, serviceAccount },
  { fetchImpl = fetch, sleep = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds)) } = {},
) {
  requireValue(packageBuffer, 'Chrome extension package');
  requireValue(publisherId, 'CHROME_PUBLISHER_ID');
  requireValue(extensionId, 'CHROME_EXTENSION_ID');

  const accessToken = await getAccessToken(serviceAccount, fetchImpl);
  const headers = { Authorization: `Bearer ${accessToken}` };
  const itemPath = `publishers/${encodeURIComponent(publisherId)}/items/${encodeURIComponent(extensionId)}`;
  const uploadResponse = await fetchImpl(`${chromeWebStoreBaseUrl}/upload/v2/${itemPath}:upload`, {
    method: 'POST',
    headers: { ...headers, 'Content-Type': 'application/zip' },
    body: packageBuffer,
  });
  const upload = await readJsonResponse(uploadResponse, 'Chrome package upload');

  if (upload.uploadState === 'UPLOAD_IN_PROGRESS') {
    await waitForUpload({
      statusUrl: `${chromeWebStoreBaseUrl}/v2/${itemPath}:fetchStatus`,
      headers,
      fetchImpl,
      sleep,
    });
  } else if (upload.uploadState !== 'SUCCEEDED') {
    throw new Error(`Chrome package upload ended with state ${upload.uploadState || 'unknown'}.`);
  }

  const publishResponse = await fetchImpl(`${chromeWebStoreBaseUrl}/v2/${itemPath}:publish`, {
    method: 'POST',
    headers: { ...headers, 'Content-Type': 'application/json' },
    body: JSON.stringify({ publishType: 'DEFAULT_PUBLISH' }),
  });

  return readJsonResponse(publishResponse, 'Chrome publish submission');
}

async function main() {
  const packagePath = requireValue(process.argv[2], 'Extension package path');
  const serviceAccountJson = requireValue(process.env.CHROME_SERVICE_ACCOUNT_JSON, 'CHROME_SERVICE_ACCOUNT_JSON');
  let serviceAccount;

  try {
    serviceAccount = JSON.parse(serviceAccountJson);
  } catch (error) {
    throw new Error(`CHROME_SERVICE_ACCOUNT_JSON is not valid JSON: ${error.message}`);
  }

  const result = await publishChromeExtension({
    packageBuffer: await fs.readFile(packagePath),
    publisherId: process.env.CHROME_PUBLISHER_ID,
    extensionId: process.env.CHROME_EXTENSION_ID,
    serviceAccount,
  });

  console.log(`Chrome submission accepted: ${JSON.stringify(result)}`);
}

if (require.main === module) {
  main().catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}

module.exports = { publishChromeExtension };
