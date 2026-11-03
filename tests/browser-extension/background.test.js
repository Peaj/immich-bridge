const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const backgroundSource = fs.readFileSync(
  path.resolve(__dirname, '..', '..', 'browser-extension', 'background.js'),
  'utf8'
);

function createEvent() {
  let listener;
  return {
    addListener(callback) {
      listener = callback;
    },
    dispatch(...args) {
      assert.ok(listener, 'Expected event listener to be registered.');
      return listener(...args);
    }
  };
}

function createStorageArea(values) {
  return {
    async get(key) {
      return { [key]: values[key] };
    },
    async set(updates) {
      Object.assign(values, updates);
    }
  };
}

function createPortEvent() {
  const listeners = [];
  return {
    addListener(callback) {
      listeners.push(callback);
    },
    dispatch(...args) {
      listeners.forEach(listener => listener(...args));
    }
  };
}

// A runtime.Port double; `peer` receives what this side posts.
function createPort(name, sender) {
  const port = {
    name,
    sender,
    posted: [],
    disconnected: false,
    onMessage: createPortEvent(),
    onDisconnect: createPortEvent(),
    postMessage(message) {
      port.posted.push(message);
    },
    disconnect() {
      if (!port.disconnected) {
        port.disconnected = true;
      }
    }
  };
  return port;
}

function loadBackground(initialStorage = {}, nativeHost = async () => ({ ok: true })) {
  const storage = { ...initialStorage };
  const sessionStorage = {};
  const nativeMessages = [];
  const registrationErrors = [];
  let registeredScript = false;
  let optionsOpenCount = 0;
  const tick = () => new Promise(resolve => setTimeout(resolve, 1));
  const events = {
    installed: createEvent(),
    startup: createEvent(),
    actionClicked: createEvent(),
    permissionRemoved: createEvent(),
    message: createEvent(),
    connect: createEvent()
  };
  const nativePorts = [];

  const extensionApi = {
    action: { onClicked: events.actionClicked },
    permissions: {
      contains: async () => true,
      onRemoved: events.permissionRemoved
    },
    runtime: {
      id: 'extension-id',
      onInstalled: events.installed,
      onMessage: events.message,
      onConnect: events.connect,
      onStartup: events.startup,
      connectNative: hostName => {
        const port = createPort(hostName);
        nativePorts.push(port);
        return port;
      },
      openOptionsPage: async () => {
        optionsOpenCount++;
      },
      sendNativeMessage: async (hostName, message) => {
        nativeMessages.push({ hostName, message });
        return nativeHost(message);
      }
    },
    scripting: {
      // Mimics the browser: registering an id that is still registered fails.
      registerContentScripts: async () => {
        await tick();
        if (registeredScript) {
          const error = new Error('Content script with id "immich-bridge-content" is already registered.');
          registrationErrors.push(error);
          throw error;
        }
        registeredScript = true;
      },
      unregisterContentScripts: async () => {
        await tick();
        registeredScript = false;
      }
    },
    storage: {
      local: createStorageArea(storage),
      session: createStorageArea(sessionStorage)
    }
  };

  vm.runInNewContext(backgroundSource, {
    URL,
    chrome: extensionApi,
    console
  });

  return {
    events,
    storage,
    sessionStorage,
    nativeMessages,
    nativePorts,
    registrationErrors,
    getOptionsOpenCount: () => optionsOpenCount
  };
}

const immichOrigin = 'https://photos.example.com';
const immichTab = { id: 'extension-id', tab: { id: 1 }, url: `${immichOrigin}/photos/abc` };

function send(background, message, sender = {}) {
  return new Promise(resolve => {
    const handled = background.events.message.dispatch(message, sender, resolve);
    if (handled !== true) {
      resolve(undefined);
    }
  });
}

test('opens onboarding once on a fresh install', async () => {
  const background = loadBackground();

  await background.events.installed.dispatch({ reason: 'install' });
  await background.events.installed.dispatch({ reason: 'install' });

  assert.equal(background.getOptionsOpenCount(), 1);
  assert.equal(background.storage.onboardingShown, true);
});

test('does not open options after an extension update', async () => {
  const background = loadBackground();

  await background.events.installed.dispatch({ reason: 'update' });

  assert.equal(background.getOptionsOpenCount(), 0);
});

test('does not open options during browser startup', async () => {
  const background = loadBackground({ onboardingShown: true });

  await background.events.startup.dispatch();

  assert.equal(background.getOptionsOpenCount(), 0);
});

test('still opens options after an explicit toolbar click', async () => {
  const background = loadBackground({ onboardingShown: true });

  await background.events.actionClicked.dispatch();

  assert.equal(background.getOptionsOpenCount(), 1);
});

test('concurrent reconfiguration does not register the content script twice', async () => {
  const background = loadBackground({ immichOrigin, onboardingShown: true });

  await Promise.all([
    background.events.installed.dispatch({ reason: 'update' }),
    background.events.startup.dispatch(),
    send(background, { type: 'immich-bridge:configure-origin', origin: immichOrigin })
  ]);

  assert.equal(background.registrationErrors.length, 0);
});

test('runs actions through the native host', async () => {
  const background = loadBackground({ immichOrigin }, async () => ({ ok: true }));

  const reply = await send(background, { type: 'immich-bridge:run', action: 'copy', path: '/mnt/photos/img.jpg' }, immichTab);

  assert.equal(reply.ok, true);
  const run = background.nativeMessages.find(entry => entry.message.type === 'run');
  assert.equal(run.hostName, 'de.peaj.immich_bridge');
  assert.equal(run.message.action, 'copy');
  assert.equal(run.message.path, '/mnt/photos/img.jpg');
});

test('passes companion errors back to the page', async () => {
  const background = loadBackground({ immichOrigin }, async () => ({ ok: false, code: 'file-missing', message: 'Missing' }));

  const reply = await send(background, { type: 'immich-bridge:run', action: 'open', path: '/mnt/photos/img.jpg' }, immichTab);

  assert.equal(reply.code, 'file-missing');
  assert.equal(reply.fallback, undefined);
});

test('falls back to the browser link when no native host is installed', async () => {
  const background = loadBackground({ immichOrigin }, async () => {
    throw new Error('Specified native messaging host not found.');
  });

  const reply = await send(background, { type: 'immich-bridge:run', action: 'reveal', path: '/mnt/photos/img.jpg' }, immichTab);

  assert.equal(reply.fallback, true);
  assert.equal(background.sessionStorage.companionStatus.connected, false);
});

test('rejects runs from pages other than the configured Immich site', async () => {
  const background = loadBackground({ immichOrigin });

  const reply = await send(background, { type: 'immich-bridge:run', action: 'copy', path: '/x.jpg' }, {
    id: 'extension-id',
    tab: { id: 2 },
    url: 'https://evil.example.com/'
  });

  assert.equal(reply.code, 'forbidden');
  assert.equal(background.nativeMessages.filter(entry => entry.message.type === 'run').length, 0);
});

test('caches the companion status until a refresh is requested', async () => {
  const background = loadBackground({}, async () => ({ ok: true, version: '0.6.0', actions: ['reveal'], configured: true }));
  const helloCount = () => background.nativeMessages.filter(entry => entry.message.type === 'hello').length;

  const first = await send(background, { type: 'immich-bridge:status' });
  const countAfterFirst = helloCount();
  const cached = await send(background, { type: 'immich-bridge:status' });
  await send(background, { type: 'immich-bridge:status', refresh: true });

  // Objects from the vm context have a different prototype, so compare plain copies.
  const plain = value => JSON.parse(JSON.stringify(value));
  assert.deepEqual(plain(first), { connected: true, version: '0.6.0', actions: ['reveal'], features: [], configured: true });
  assert.deepEqual(plain(cached), plain(first));
  assert.equal(helloCount(), countAfterFirst + 1);
});

async function connectCopyPort(background, sender = immichTab) {
  const port = createPort('immich-bridge:copy', sender);
  background.events.connect.dispatch(port);
  for (let attempt = 0; attempt < 20 && port.posted.length === 0; attempt++) {
    await new Promise(resolve => setTimeout(resolve, 1));
  }
  return port;
}

test('relays copy messages between the page and the native host', async () => {
  const background = loadBackground({ immichOrigin });

  const page = await connectCopyPort(background);
  assert.equal(page.posted[0].type, 'ready');
  const native = background.nativePorts[0];
  assert.equal(native.name, 'de.peaj.immich_bridge');

  page.onMessage.dispatch({ type: 'copy-start', purpose: 'open', fileName: 'IMG.jpg', size: 3, extra: 'dropped' });
  page.onMessage.dispatch({ type: 'run', action: 'reveal', path: '/x.jpg' });
  native.onMessage.dispatch({ ok: true });

  assert.equal(native.posted.length, 1);
  assert.equal(native.posted[0].type, 'copy-start');
  assert.equal(native.posted[0].fileName, 'IMG.jpg');
  assert.equal(native.posted[0].extra, undefined);
  assert.equal(page.posted[1].ok, true);
});

test('rejects copy ports from other sites without reaching the host', async () => {
  const background = loadBackground({ immichOrigin });

  const page = await connectCopyPort(background, { id: 'extension-id', tab: { id: 2 }, url: 'https://evil.example.com/' });

  assert.equal(page.posted[0].code, 'forbidden');
  assert.equal(page.disconnected, true);
  assert.equal(background.nativePorts.length, 0);
});

test('reports a closed native connection to the page', async () => {
  const background = loadBackground({ immichOrigin });

  const page = await connectCopyPort(background);
  background.nativePorts[0].onDisconnect.dispatch();

  assert.equal(page.posted.at(-1).code, 'disconnected');
  assert.equal(page.disconnected, true);
});

test('closes the native connection when the page goes away', async () => {
  const background = loadBackground({ immichOrigin });

  const page = await connectCopyPort(background);
  page.onDisconnect.dispatch();

  assert.equal(background.nativePorts[0].disconnected, true);
});
