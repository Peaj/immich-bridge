(function () {
  'use strict';

  const contentScriptId = 'immich-bridge-content';
  const storageKey = 'immichOrigin';
  const onboardingShownKey = 'onboardingShown';
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

  async function registerContentScript(origin) {
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

  async function handleInstalled(details) {
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

  extensionApi.runtime.onStartup.addListener(configureFromStorage);

  extensionApi.action.onClicked.addListener(() => {
    extensionApi.runtime.openOptionsPage();
  });

  extensionApi.permissions.onRemoved.addListener(configureFromStorage);

  extensionApi.runtime.onMessage.addListener((message, _sender, sendResponse) => {
    if (!message || message.type !== 'immich-bridge:configure-origin') {
      return undefined;
    }

    const response = registerContentScript(message.origin || null)
      .then(() => ({ ok: true }))
      .catch(error => ({ ok: false, error: error.message || String(error) }));

    if (isFirefox) {
      return response;
    }

    response.then(sendResponse);
    return true;
  });

  configureFromStorage();
})();
