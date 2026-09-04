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

function loadBackground(initialStorage = {}) {
  const storage = { ...initialStorage };
  let optionsOpenCount = 0;
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
      onInstalled: events.installed,
      onMessage: events.message,
      onStartup: events.startup,
      openOptionsPage: async () => {
        optionsOpenCount++;
      }
    },
    scripting: {
      registerContentScripts: async () => {},
      unregisterContentScripts: async () => {}
    },
    storage: {
      local: {
        async get(key) {
          return { [key]: storage[key] };
        },
        async set(values) {
          Object.assign(storage, values);
        }
      }
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
    getOptionsOpenCount: () => optionsOpenCount
  };
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
