const fs = require('node:fs/promises');

const edgeAddonsBaseUrl = 'https://api.addons.microsoftedge.microsoft.com/v1';

function requireValue(value, name) {
  if (!value) {
    throw new Error(`${name} is required.`);
  }

  return value;
}

async function readResponse(response, operation) {
  const body = await response.text();
  let data = {};

  if (body) {
    try {
      data = JSON.parse(body);
    } catch {
      data = { message: body };
    }
  }

  if (!response.ok) {
    throw new Error(`${operation} failed (${response.status}): ${body || response.statusText}`);
  }

  return data;
}

function getOperationId(response, operation) {
  const location = response.headers.get('location');
  if (!location) {
    throw new Error(`${operation} did not return an operation ID.`);
  }

  return location.split('/').filter(Boolean).at(-1);
}

async function waitForOperation({ url, headers, operation, fetchImpl, sleep, attempts = 60 }) {
  for (let attempt = 1; attempt <= attempts; attempt += 1) {
    const response = await fetchImpl(url, { headers });
    const result = await readResponse(response, operation);
    const status = String(result.status || '').toLowerCase();

    if (status === 'succeeded' || status === 'success') {
      return result;
    }

    if (status === 'failed' || status === 'failure') {
      throw new Error(`${operation} failed: ${result.message || JSON.stringify(result)}`);
    }

    if (status && status !== 'inprogress' && status !== 'in_progress') {
      throw new Error(`${operation} returned unexpected state ${result.status}.`);
    }

    if (attempt < attempts) {
      await sleep(5000);
    }
  }

  throw new Error(`Timed out waiting for ${operation.toLowerCase()}.`);
}

async function publishEdgeExtension(
  { packageBuffer, productId, clientId, apiKey, notes },
  { fetchImpl = fetch, sleep = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds)) } = {},
) {
  requireValue(packageBuffer, 'Edge extension package');
  requireValue(productId, 'EDGE_PRODUCT_ID');
  requireValue(clientId, 'EDGE_CLIENT_ID');
  requireValue(apiKey, 'EDGE_API_KEY');

  const productPath = `products/${encodeURIComponent(productId)}/submissions`;
  const headers = {
    Authorization: `ApiKey ${apiKey}`,
    'X-ClientID': clientId,
  };
  const uploadResponse = await fetchImpl(`${edgeAddonsBaseUrl}/${productPath}/draft/package`, {
    method: 'POST',
    headers: { ...headers, 'Content-Type': 'application/zip' },
    body: packageBuffer,
  });
  await readResponse(uploadResponse, 'Edge package upload');
  const uploadOperationId = getOperationId(uploadResponse, 'Edge package upload');

  await waitForOperation({
    url: `${edgeAddonsBaseUrl}/${productPath}/draft/package/operations/${encodeURIComponent(uploadOperationId)}`,
    headers,
    operation: 'Edge package processing',
    fetchImpl,
    sleep,
  });

  const publishResponse = await fetchImpl(`${edgeAddonsBaseUrl}/${productPath}`, {
    method: 'POST',
    headers: { ...headers, 'Content-Type': 'application/json' },
    body: JSON.stringify({ notes: notes || 'Automated release submission from GitHub Actions.' }),
  });
  await readResponse(publishResponse, 'Edge publish submission');
  const publishOperationId = getOperationId(publishResponse, 'Edge publish submission');

  return waitForOperation({
    url: `${edgeAddonsBaseUrl}/${productPath}/operations/${encodeURIComponent(publishOperationId)}`,
    headers,
    operation: 'Edge publish submission',
    fetchImpl,
    sleep,
  });
}

async function main() {
  const packagePath = requireValue(process.argv[2], 'Extension package path');
  const result = await publishEdgeExtension({
    packageBuffer: await fs.readFile(packagePath),
    productId: process.env.EDGE_PRODUCT_ID,
    clientId: process.env.EDGE_CLIENT_ID,
    apiKey: process.env.EDGE_API_KEY,
    notes: process.env.EDGE_CERTIFICATION_NOTES,
  });

  console.log(`Edge submission accepted: ${JSON.stringify(result)}`);
}

if (require.main === module) {
  main().catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}

module.exports = { publishEdgeExtension };
