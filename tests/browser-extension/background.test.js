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
    message: createEvent()
  };

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
      onStartup: events.startup,
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
  assert.deepEqual(plain(first), { connected: true, version: '0.6.0', actions: ['reveal'], configured: true });
  assert.deepEqual(plain(cached), plain(first));
  assert.equal(helloCount(), countAfterFirst + 1);
});
