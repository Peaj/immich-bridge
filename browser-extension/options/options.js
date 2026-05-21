(function () {
  'use strict';

  const extensionApi = globalThis.browser;
  const storageKey = 'immichOrigin';
  const form = document.getElementById('settings-form');
  const urlInput = document.getElementById('immich-url');
  const status = document.getElementById('status');
  const openImmich = document.getElementById('open-immich');
  const removePermission = document.getElementById('remove-permission');

  function originToPattern(origin) {
    return `${origin.replace(/\/$/, '')}/*`;
  }

  function normalizeOrigin(value) {
    const parsed = new URL(value.trim());
    if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
      throw new Error('Enter an http or https Immich URL.');
    }

    return parsed.origin;
  }

  function showStatus(message, isError) {
    status.textContent = message;
    status.classList.toggle('error', Boolean(isError));
  }

  function updateActions(origin) {
    const hasOrigin = Boolean(origin);
    openImmich.hidden = !hasOrigin;
    removePermission.hidden = !hasOrigin;
    if (hasOrigin) {
      openImmich.href = origin;
    }
  }

  async function notifyBackground(origin) {
    const response = await extensionApi.runtime.sendMessage({
      type: 'immich-bridge:configure-origin',
      origin
    });

    if (response && response.ok === false) {
      throw new Error(response.error || 'Unable to configure Immich Bridge.');
    }
  }

  async function loadSettings() {
    const values = await extensionApi.storage.local.get(storageKey);
    const origin = values[storageKey] || '';
    urlInput.value = origin;
    updateActions(origin);
  }

  form.addEventListener('submit', async event => {
    event.preventDefault();
    showStatus('', false);

    try {
      const origin = normalizeOrigin(urlInput.value);
      const pattern = originToPattern(origin);
      const granted = await extensionApi.permissions.request({ origins: [pattern] });

      if (!granted) {
        showStatus('Permission was not granted. Immich Bridge will not run on this site.', true);
        updateActions(null);
        return;
      }

      await extensionApi.storage.local.set({ [storageKey]: origin });
      await notifyBackground(origin);
      urlInput.value = origin;
      updateActions(origin);
      showStatus('Immich Bridge is enabled for this Immich site.', false);
    } catch (error) {
      showStatus(error.message || String(error), true);
    }
  });

  removePermission.addEventListener('click', async () => {
    const origin = normalizeOrigin(urlInput.value);
    const pattern = originToPattern(origin);
    await extensionApi.permissions.remove({ origins: [pattern] });
    await extensionApi.storage.local.remove(storageKey);
    await notifyBackground(null);
    urlInput.value = '';
    updateActions(null);
    showStatus('Immich Bridge access was removed.', false);
  });

  loadSettings().catch(error => {
    showStatus(error.message || String(error), true);
  });
})();
