(function () {
  'use strict';

  const contentScriptId = 'immich-bridge-content';
  const storageKey = 'immichOrigin';
  const onboardingShownKey = 'onboardingShown';
  const companionStatusKey = 'companionStatus';
  const nativeHostName = 'de.peaj.immich_bridge';
  const extensionApi = globalThis.browser || globalThis.chrome;
  const isFirefox = Boolean(globalThis.browser);

  function originToPattern(origin) {
    const url = new URL(origin);
    return `${url.protocol}//${url.hostname}/*`;
  }

  async function getStoredOrigin() {
    const values = await extensionApi.storage.local.get(storageKey);
    return values[storageKey] || null;
  }

  async function unregisterContentScript() {
    try {
      await extensionApi.scripting.unregisterContentScripts({ ids: [contentScriptId] });
    } catch (error) {
      if (!String(error && error.message).includes('Nonexistent script ID')) {
        console.warn('[Immich Bridge] Unable to unregister content script.', error);
      }
    }
  }

  // Startup, install, and the options page can all reconfigure at once. Running them one
  // after another keeps an unregister/register pair from interleaving with another one,
  // which fails with "already registered".
  let registrationQueue = Promise.resolve();

  function registerContentScript(origin) {
    const registration = registrationQueue.then(() => replaceContentScript(origin));
    registrationQueue = registration.catch(() => {});
    return registration;
  }

  async function replaceContentScript(origin) {
    await unregisterContentScript();

    if (!origin) {
      return;
    }

    const pattern = originToPattern(origin);
    const hasPermission = await extensionApi.permissions.contains({ origins: [pattern] });
    if (!hasPermission) {
      return;
    }

    await extensionApi.scripting.registerContentScripts([
      {
        id: contentScriptId,
        matches: [pattern],
        js: ['content/immich-bridge-content.js'],
        runAt: 'document_idle',
        allFrames: false,
        persistAcrossSessions: true
      }
    ]);
  }

  async function configureFromStorage() {
    await registerContentScript(await getStoredOrigin());
  }

  // Asks the desktop companion for its version and actions. Companion versions before 0.6
  // have no native host; they are reported as not connected and keep working through the
  // immich-bridge:// link.
  async function refreshCompanionStatus() {
    let status;
    try {
      const reply = await extensionApi.runtime.sendNativeMessage(nativeHostName, { type: 'hello' });
      status = reply && reply.ok
        ? {
          connected: true,
          version: reply.version,
          actions: reply.actions || [],
          features: reply.features || [],
          configured: Boolean(reply.configured)
        }
        : { connected: false };
    } catch (error) {
      status = { connected: false };
    }

    await extensionApi.storage.session.set({ [companionStatusKey]: status });
    return status;
  }

  async function getCompanionStatus(refresh) {
    if (!refresh) {
      const values = await extensionApi.storage.session.get(companionStatusKey);
      if (values[companionStatusKey]) {
        return values[companionStatusKey];
      }
    }

    return refreshCompanionStatus();
  }

  async function isFromConfiguredImmich(sender) {
    if (!sender || sender.id !== extensionApi.runtime.id || !sender.tab || !sender.url) {
      return false;
    }

    return new URL(sender.url).origin === await getStoredOrigin();
  }

  async function runAction(message, sender) {
    if (!await isFromConfiguredImmich(sender)) {
      return { ok: false, code: 'forbidden', message: 'Immich Bridge only runs on the configured Immich site.' };
    }

    try {
      return await extensionApi.runtime.sendNativeMessage(nativeHostName, {
        type: 'run',
        action: String(message.action || ''),
        path: String(message.path || '')
      });
    } catch (error) {
      // No reachable native host: let the page fall back to the immich-bridge:// link.
      await extensionApi.storage.session.set({ [companionStatusKey]: { connected: false } });
      return { fallback: true };
    }
  }

  const setupSamplePathKey = 'setupSamplePath';
  const maxSamplePathLength = 2048;

  // Opens the options page from an error in Immich, with the photo's path ready for finding
  // its folder. The page only gets to prefill that one field; saving still takes a click.
  async function openSetup(message, sender) {
    if (!await isFromConfiguredImmich(sender)) {
      return { ok: false, code: 'forbidden', message: 'Immich Bridge only runs on the configured Immich site.' };
    }

    const path = String(message.path || '').slice(0, maxSamplePathLength);
    if (path.startsWith('/')) {
      await extensionApi.storage.session.set({ [setupSamplePathKey]: path });
    }

    await extensionApi.runtime.openOptionsPage();
    return { ok: true };
  }

  async function configureOrigin(message) {
    await registerContentScript(message.origin || null);
    return { ok: true };
  }

  const copyMessageTypes = ['copy-start', 'copy-chunk', 'copy-end'];

  // Relays a file download from the Immich page to the companion app, one message at a time.
  // The page waits for each reply before sending the next chunk, so nothing piles up here.
  async function relayCopy(port) {
    let closed = false;
    const reply = message => {
      if (!closed) {
        port.postMessage(message);
      }
    };
    port.onDisconnect.addListener(() => {
      closed = true;
    });

    if (!await isFromConfiguredImmich(port.sender)) {
      reply({ ok: false, code: 'forbidden', message: 'Immich Bridge only runs on the configured Immich site.' });
      port.disconnect();
      return;
    }

    let nativePort;
    try {
      nativePort = extensionApi.runtime.connectNative(nativeHostName);
    } catch (error) {
      reply({ ok: false, code: 'disconnected', message: 'The Immich Bridge desktop app is not available.' });
      port.disconnect();
      return;
    }

    nativePort.onMessage.addListener(reply);
    nativePort.onDisconnect.addListener(() => {
      reply({ ok: false, code: 'disconnected', message: 'The Immich Bridge desktop app closed the connection.' });
      if (!closed) {
        port.disconnect();
      }
    });
    port.onDisconnect.addListener(() => nativePort.disconnect());
    port.onMessage.addListener(message => {
      if (!message || !copyMessageTypes.includes(message.type)) {
        return;
      }

      nativePort.postMessage({
        type: message.type,
        purpose: message.purpose,
        fileName: message.fileName,
        size: message.size,
        data: message.data
      });
    });

    reply({ ok: true, type: 'ready' });
  }

  const messageHandlers = {
    'immich-bridge:configure-origin': message => configureOrigin(message),
    'immich-bridge:run': (message, sender) => runAction(message, sender),
    'immich-bridge:open-setup': (message, sender) => openSetup(message, sender),
    'immich-bridge:status': message => getCompanionStatus(Boolean(message.refresh))
  };

  async function handleInstalled(details) {
    refreshCompanionStatus().catch(error => console.warn('[Immich Bridge] Unable to check the desktop companion.', error));
    await configureFromStorage();

    if (!details || details.reason !== 'install') {
      return;
    }

    const values = await extensionApi.storage.local.get(onboardingShownKey);
    if (values[onboardingShownKey]) {
      return;
    }

    // Persist first so duplicate lifecycle events cannot open multiple tabs.
    await extensionApi.storage.local.set({ [onboardingShownKey]: true });
    await extensionApi.runtime.openOptionsPage();
  }

  extensionApi.runtime.onInstalled.addListener(details => {
    return handleInstalled(details).catch(error => {
      console.error('[Immich Bridge] Unable to handle extension installation.', error);
    });
  });

  extensionApi.runtime.onStartup.addListener(() => {
    refreshCompanionStatus().catch(error => console.warn('[Immich Bridge] Unable to check the desktop companion.', error));
    return configureFromStorage();
  });

  extensionApi.action.onClicked.addListener(() => {
    extensionApi.runtime.openOptionsPage();
  });

  extensionApi.permissions.onRemoved.addListener(configureFromStorage);

  extensionApi.runtime.onMessage.addListener((message, sender, sendResponse) => {
    const handler = message && messageHandlers[message.type];
    if (!handler) {
      return undefined;
    }

    const response = handler(message, sender)
      .catch(error => ({ ok: false, error: error.message || String(error) }));

    if (isFirefox) {
      return response;
    }

    response.then(sendResponse);
    return true;
  });

  extensionApi.runtime.onConnect.addListener(port => {
    if (port.name !== 'immich-bridge:copy') {
      return;
    }

    relayCopy(port).catch(error => {
      console.error('[Immich Bridge] Unable to relay a copy.', error);
      port.disconnect();
    });
  });

  configureFromStorage();
})();
