(function () {
  'use strict';

  const extensionApi = globalThis.browser || globalThis.chrome;
  const storageKey = 'immichOrigin';
  const form = document.getElementById('settings-form');
  const urlInput = document.getElementById('immich-url');
  const status = document.getElementById('status');
  const openImmich = document.getElementById('open-immich');
  const removePermission = document.getElementById('remove-permission');
  const companionSection = document.getElementById('companion');
  const companionTitle = document.getElementById('companion-title');
  const companionDetail = document.getElementById('companion-detail');
  const companionDownload = document.getElementById('companion-download');

  function originToPattern(origin) {
    const url = new URL(origin);
    return `${url.protocol}//${url.hostname}/*`;
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

  function compareVersions(left, right) {
    const leftParts = String(left).split('.').map(Number);
    const rightParts = String(right).split('.').map(Number);
    for (let index = 0; index < Math.max(leftParts.length, rightParts.length); index++) {
      const difference = (leftParts[index] || 0) - (rightParts[index] || 0);
      if (difference !== 0) {
        return difference;
      }
    }

    return 0;
  }

  function showCompanionStatus(status) {
    const extensionVersion = extensionApi.runtime.getManifest().version;
    let title;
    let detail;
    let linkText = null;

    if (!status || !status.connected) {
      title = 'Desktop companion app not detected';
      detail = 'Install Immich Bridge 0.6 or newer on this computer to enable Explorer, Copy original, and Open With actions from Immich. Older versions keep working through the immich-bridge:// link but cannot report results or updates.';
      linkText = 'Download companion app';
    } else if (compareVersions(status.version, extensionVersion) < 0) {
      title = `Update available for Immich Bridge ${status.version}`;
      detail = `This extension is version ${extensionVersion}. Update the desktop companion app to use all actions.`;
      linkText = 'Download update';
    } else if (!status.configured) {
      title = `Immich Bridge ${status.version} needs setup`;
      detail = 'Run Immich Bridge from the Start menu to set up the path mapping on this computer.';
    } else {
      title = `Connected to Immich Bridge ${status.version}`;
      detail = 'The desktop companion app is installed and set up.';
    }

    companionSection.classList.toggle('connected', Boolean(status && status.connected && !linkText));
    companionTitle.textContent = title;
    companionDetail.textContent = detail;
    companionDownload.hidden = !linkText;
    if (linkText) {
      companionDownload.textContent = linkText;
    }
  }

  async function loadCompanionStatus() {
    const status = await extensionApi.runtime.sendMessage({ type: 'immich-bridge:status', refresh: true });
    showCompanionStatus(status);
  }

  loadSettings().catch(error => {
    showStatus(error.message || String(error), true);
  });

  loadCompanionStatus().catch(() => showCompanionStatus(null));
})();
