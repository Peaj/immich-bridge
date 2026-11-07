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
  const foldersSection = document.getElementById('folders');
  const samplePathInput = document.getElementById('sample-path');
  const findFolderButton = document.getElementById('find-folder');
  const suggestionList = document.getElementById('suggestions');
  const mappingList = document.getElementById('mapping-list');
  const mappingEmpty = document.getElementById('mapping-empty');
  const addMappingButton = document.getElementById('add-mapping');
  const testMappingButton = document.getElementById('test-mapping');
  const saveMappingsButton = document.getElementById('save-mappings');
  const foldersStatus = document.getElementById('folders-status');
  const nativeHostName = 'de.peaj.immich_bridge';
  const setupSamplePathKey = 'setupSamplePath';

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

  function supportsMappings(status) {
    return Boolean(status && status.connected && (status.features || []).includes('mappings'));
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
      detail = supportsMappings(status)
        ? `This extension is version ${extensionVersion}. Update the desktop companion app to use all actions.`
        : `This extension is version ${extensionVersion}. Update the desktop companion app to set up folders on this page and use all actions.`;
      linkText = 'Download update';
    } else if (!status.configured && !supportsMappings(status)) {
      title = `Immich Bridge ${status.version} needs setup`;
      detail = 'Run Immich Bridge from the Start menu to set up the path mapping on this computer.';
    } else {
      title = `Connected to Immich Bridge ${status.version}`;
      detail = 'The desktop companion app is installed. Set up the folders for your external libraries below.';
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
    return status;
  }

  // Folder mappings. The options page talks to the companion directly: only extension pages
  // can use native messaging, and the background script never relays these messages from
  // Immich pages, so a web page can't read or change the mappings.
  async function askCompanion(message) {
    let reply;
    try {
      reply = await extensionApi.runtime.sendNativeMessage(nativeHostName, message);
    } catch (error) {
      throw new Error('The Immich Bridge desktop app is not available. Check that it is installed.');
    }

    if (!reply || reply.ok === false) {
      throw new Error((reply && reply.message) || 'The Immich Bridge desktop app could not complete this step.');
    }

    return reply;
  }

  function showFoldersStatus(message, isError) {
    foldersStatus.textContent = message;
    foldersStatus.classList.toggle('error', Boolean(isError));
  }

  async function busy(button, task) {
    button.disabled = true;
    try {
      await task();
    } catch (error) {
      showFoldersStatus(error.message || String(error), true);
    } finally {
      button.disabled = false;
    }
  }

  function createMappingRow(mapping) {
    const row = document.createElement('div');
    row.className = 'mapping-row';

    const remote = document.createElement('input');
    remote.className = 'remote';
    remote.type = 'text';
    remote.spellcheck = false;
    remote.placeholder = '/external/photos';
    remote.setAttribute('aria-label', 'Immich path');
    remote.value = mapping.remotePrefix || '';

    const local = document.createElement('input');
    local.className = 'local';
    local.type = 'text';
    local.spellcheck = false;
    local.placeholder = 'M:\\Photos';
    local.setAttribute('aria-label', 'Folder on this computer');
    local.value = mapping.localPrefix || '';

    const remove = document.createElement('button');
    remove.type = 'button';
    remove.className = 'secondary remove';
    remove.textContent = '✕';
    remove.title = 'Remove this folder';
    remove.setAttribute('aria-label', 'Remove this folder');
    remove.addEventListener('click', () => {
      row.remove();
      updateEmptyState();
      showFoldersStatus('Removed. Save folders to keep this change.', false);
    });

    row.append(remote, local, remove);
    if (mapping.exists === false) {
      row.classList.add('missing');
      const warning = document.createElement('p');
      warning.className = 'mapping-warning';
      warning.textContent = 'This folder isn\'t reachable right now. Is the drive connected?';
      row.appendChild(warning);
    }

    return row;
  }

  function renderMappings(mappings) {
    mappingList.replaceChildren(...mappings.map(createMappingRow));
    updateEmptyState();
  }

  function updateEmptyState() {
    mappingEmpty.hidden = mappingList.children.length > 0;
  }

  function readMappings() {
    return [...mappingList.querySelectorAll('.mapping-row')]
      .map(row => ({
        remotePrefix: row.querySelector('.remote').value.trim(),
        localPrefix: row.querySelector('.local').value.trim()
      }))
      .filter(mapping => mapping.remotePrefix || mapping.localPrefix);
  }

  async function saveMappings(mappings) {
    const reply = await askCompanion({ type: 'save-mappings', mappings });
    renderMappings(reply.mappings);
    // The content script's cached status picks up the new setup on its next refresh.
    extensionApi.runtime.sendMessage({ type: 'immich-bridge:status', refresh: true }).catch(() => {});
    return reply.mappings;
  }

  function useSuggestion(suggestion) {
    const mappings = readMappings().filter(mapping => mapping.remotePrefix.toLowerCase() !== suggestion.remotePrefix.toLowerCase());
    mappings.push({ remotePrefix: suggestion.remotePrefix, localPrefix: suggestion.localPrefix });
    return saveMappings(mappings).then(() => {
      suggestionList.hidden = true;
      showFoldersStatus(`Saved: ${suggestion.remotePrefix} is ${suggestion.localPrefix} on this computer.`, false);
    });
  }

  function renderSuggestions(suggestions) {
    suggestionList.replaceChildren(...suggestions.map(suggestion => {
      const item = document.createElement('li');
      item.className = 'suggestion';

      const text = document.createElement('div');
      text.className = 'suggestion-text';
      const mapping = document.createElement('strong');
      mapping.textContent = `${suggestion.remotePrefix}  →  ${suggestion.localPrefix}`;
      const found = document.createElement('span');
      found.textContent = `Found ${suggestion.localPath}`;
      text.append(mapping, found);

      const use = document.createElement('button');
      use.type = 'button';
      use.textContent = 'Use this folder';
      use.addEventListener('click', () => busy(use, () => useSuggestion(suggestion)));

      item.append(text, use);
      return item;
    }));
    suggestionList.hidden = suggestions.length === 0;
  }

  async function findFolder() {
    const path = samplePathInput.value.trim();
    if (!path) {
      throw new Error('Paste an Immich file path first.');
    }

    renderSuggestions([]);
    const current = readMappings();
    if (current.length > 0) {
      const check = await askCompanion({ type: 'check-mapping', path, mappings: current }).catch(() => null);
      if (check && check.exists) {
        showFoldersStatus(`This photo already works with your folders: ${check.localPath}`, false);
        return;
      }
    }

    showFoldersStatus('Looking for this file on your drives…', false);
    const reply = await askCompanion({ type: 'suggest-mapping', path });
    renderSuggestions(reply.suggestions);
    showFoldersStatus(reply.suggestions.length > 0
      ? 'Is this the right folder? Using it saves the mapping.'
      : 'The file wasn\'t found on any drive or mapped network drive. Connect the drive or share, or add the folder yourself.', reply.suggestions.length === 0);
  }

  async function testMapping() {
    const path = samplePathInput.value.trim();
    if (!path) {
      throw new Error('Paste an Immich file path to test.');
    }

    const reply = await askCompanion({ type: 'check-mapping', path, mappings: readMappings() });
    showFoldersStatus(reply.exists
      ? `Works: this photo is ${reply.localPath}`
      : `The path maps to ${reply.localPath}, but there is no file there. Check the folder.`, !reply.exists);
  }

  async function loadFolders() {
    foldersSection.hidden = false;
    const reply = await askCompanion({ type: 'get-mappings' });
    renderMappings(reply.mappings);
    return reply.mappings;
  }

  // A not-mapped error in Immich can open this page with the photo's path, so the folder
  // can be found right away.
  async function takeSetupSamplePath() {
    const values = await extensionApi.storage.session.get(setupSamplePathKey);
    const path = values[setupSamplePathKey];
    if (!path) {
      return null;
    }

    await extensionApi.storage.session.remove(setupSamplePathKey);
    return String(path);
  }

  findFolderButton.addEventListener('click', () => busy(findFolderButton, findFolder));
  testMappingButton.addEventListener('click', () => busy(testMappingButton, testMapping));
  samplePathInput.addEventListener('keydown', event => {
    if (event.key === 'Enter') {
      event.preventDefault();
      busy(findFolderButton, findFolder);
    }
  });
  addMappingButton.addEventListener('click', () => {
    const row = createMappingRow({});
    mappingList.appendChild(row);
    updateEmptyState();
    row.querySelector('.remote').focus();
  });
  saveMappingsButton.addEventListener('click', () => busy(saveMappingsButton, async () => {
    const mappings = await saveMappings(readMappings());
    showFoldersStatus(mappings.length === 0 ? 'Saved. No folders are set up.' : 'Folders saved.', false);
  }));

  async function start() {
    const status = await loadCompanionStatus().catch(() => {
      showCompanionStatus(null);
      return null;
    });
    if (!supportsMappings(status)) {
      return;
    }

    await loadFolders();
    const samplePath = await takeSetupSamplePath();
    if (samplePath) {
      samplePathInput.value = samplePath;
      foldersSection.scrollIntoView({ block: 'start' });
      await busy(findFolderButton, findFolder);
    }
  }

  loadSettings().catch(error => {
    showStatus(error.message || String(error), true);
  });

  start().catch(error => showFoldersStatus(error.message || String(error), true));
})();
