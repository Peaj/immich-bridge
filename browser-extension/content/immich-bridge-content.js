(function () {
  'use strict';

  const extensionApi = globalThis.browser || globalThis.chrome;
  const storageKey = 'immichOrigin';

  extensionApi.storage.local.get(storageKey)
    .then(values => {
      if (values[storageKey] !== window.location.origin) {
        return;
      }

      startImmichBridge();
    })
    .catch(error => {
      console.warn('[Immich Bridge] Unable to read extension settings.', error);
    });

  function startImmichBridge() {
    if (window.__immichBridgeContentLoaded) {
      return;
    }
    window.__immichBridgeContentLoaded = true;

  const protocol = 'immich-bridge';
  const toolbarHostId = 'immich-bridge-toolbar-host';
  const menuId = 'immich-bridge-menu';
  const assetApiPrefix = '/api/assets/';
  const toastId = 'immich-bridge-toast';
  const latestReleaseUrl = 'https://github.com/Peaj/immich-bridge/releases/latest';
  const extensionVersion = extensionApi.runtime.getManifest().version;
  const actions = [
    { label: 'Reveal in Explorer', action: 'reveal', icon: 'folder' },
    { label: 'Copy original', action: 'copy', icon: 'copy' },
    { label: 'Open with...', action: 'open', icon: 'app' }
  ];
  let lastAssetId = null;
  let cachedAsset = null;
  let companionStatus = null;
  let toastTimer = 0;
  let toolbarRefreshTimer = 0;
  let lastLocation = window.location.href;
  let mutationRefreshTimer = 0;
  const routeRetryDelays = [50, 150, 300, 700, 1200, 2000, 4000];

  function extractAssetIdFromUrl() {
    const matches = [...window.location.pathname.matchAll(/[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}/gi)];
    if (matches.length === 0) {
      return null;
    }

    return matches[matches.length - 1][0];
  }

  async function getCurrentAsset() {
    const assetId = extractAssetIdFromUrl();
    if (!assetId) {
      return null;
    }

    if (assetId === lastAssetId && cachedAsset) {
      return cachedAsset;
    }

    const url = new URL(`${assetApiPrefix}${encodeURIComponent(assetId)}`, window.location.href);
    const response = await fetch(url, {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' }
    });

    if (!response.ok) {
      throw new Error(`Immich asset API returned ${response.status}`);
    }

    const asset = await response.json();
    lastAssetId = assetId;
    cachedAsset = asset;
    return asset;
  }

  // Assets in Immich's upload library have no library id. Immich says their files must never
  // be touched, so they are only handed out as read-only copies, never by path.
  function isInternalAsset(asset) {
    return Boolean(asset) && asset.libraryId == null;
  }

  async function launch(action, appId) {
    const asset = await getCurrentAsset();
    const originalPath = asset && asset.originalPath;

    if (!originalPath) {
      throw new Error('Immich asset did not include originalPath.');
    }

    if (isInternalAsset(asset)) {
      throw new Error('Photos in Immich\'s own library are only opened as read-only copies.');
    }

    if (!appId) {
      const reply = await runThroughCompanion(action, originalPath);
      if (reply && reply.ok) {
        if (action === 'copy') {
          showToast(`Copied ${asset.originalFileName || originalPath.split('/').pop()}`);
        }
        return;
      }

      if (reply && !reply.fallback) {
        showToast(reply.message || reply.error || 'Immich Bridge could not run this action.', {
          error: true,
          updateLink: reply.code === 'unsupported-action',
          setupPath: setupCodes.includes(reply.code) && canSetUpFolders() ? originalPath : null
        });
        return;
      }
    }

    // Companion versions before 0.6 (and the userscript) only understand the browser link.
    const query = new URLSearchParams();
    query.set('path', originalPath);
    if (appId) {
      query.set('app', appId);
    }

    window.location.href = `${protocol}://${action}?${query.toString()}`;
  }

  // Errors that a folder mapping fixes. The toast for them links to the options page, which
  // can find the folder from this photo's path.
  const setupCodes = ['not-mapped', 'not-configured', 'file-missing'];

  function canSetUpFolders() {
    return Boolean(companionStatus && companionStatus.connected && (companionStatus.features || []).includes('mappings'));
  }

  async function openFolderSetup(path) {
    try {
      await extensionApi.runtime.sendMessage({ type: 'immich-bridge:open-setup', path });
    } catch (error) {
      console.warn('[Immich Bridge] Unable to open the folder setup.', error);
    }
  }

  async function runThroughCompanion(action, path) {
    try {
      return await extensionApi.runtime.sendMessage({ type: 'immich-bridge:run', action, path });
    } catch (error) {
      console.warn('[Immich Bridge] Unable to reach the extension background; using the browser link.', error);
      return { fallback: true };
    }
  }

  async function refreshCompanionStatus() {
    try {
      companionStatus = await extensionApi.runtime.sendMessage({ type: 'immich-bridge:status' });
    } catch (error) {
      companionStatus = null;
    }
  }

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

  function availableMenuItems(asset) {
    const connected = Boolean(companionStatus && companionStatus.connected);
    const supportsCopies = connected && (companionStatus.features || []).includes('copies');
    const editedCopy = { label: 'Open edited copy with...', icon: 'app', copy: { purpose: 'open', edited: true } };
    let items;

    if (isInternalAsset(asset)) {
      if (!supportsCopies) {
        return [{ label: 'Update Immich Bridge to open this photo safely', url: latestReleaseUrl, icon: 'update', muted: true }];
      }

      items = [
        { label: 'Copy original', icon: 'copy', copy: { purpose: 'clipboard', edited: false } },
        { label: 'Open copy with...', icon: 'app', copy: { purpose: 'open', edited: false } }
      ];
    } else {
      // Companion versions before 0.6 report nothing; every action then uses the browser link.
      items = connected ? actions.filter(item => companionStatus.actions.includes(item.action)) : [...actions];
    }

    if (asset && asset.isEdited && supportsCopies) {
      items.push(editedCopy);
    }

    if (connected && compareVersions(companionStatus.version, extensionVersion) < 0) {
      items.push({ label: 'Update Immich Bridge', url: latestReleaseUrl, icon: 'update', muted: true });
    }

    return items;
  }

  const copyChunkBytes = 1024 * 1024;
  const progressThresholdBytes = 20 * 1024 * 1024;
  const editedExtensions = {
    'image/jpeg': '.jpg',
    'image/png': '.png',
    'image/webp': '.webp',
    'image/avif': '.avif',
    'image/heic': '.heic'
  };

  function copyFileName(asset, edited, contentType) {
    const original = asset.originalFileName || asset.originalPath.split('/').pop() || 'immich-asset';
    if (!edited) {
      return original;
    }

    const dot = original.lastIndexOf('.');
    const stem = dot > 0 ? original.slice(0, dot) : original;
    const extension = editedExtensions[(contentType || '').split(';')[0].trim()] || (dot > 0 ? original.slice(dot) : '');
    return `${stem} (edited)${extension}`;
  }

  function toBase64(bytes) {
    let binary = '';
    for (let offset = 0; offset < bytes.length; offset += 0x8000) {
      binary += String.fromCharCode.apply(null, bytes.subarray(offset, offset + 0x8000));
    }

    return btoa(binary);
  }

  // Wraps the copy port so each message can be awaited as a request/reply pair.
  function openCopyChannel() {
    const port = extensionApi.runtime.connect({ name: 'immich-bridge:copy' });
    const waiting = [];
    let failure = null;

    port.onMessage.addListener(message => {
      const next = waiting.shift();
      if (next) {
        next.resolve(message);
      }
    });
    port.onDisconnect.addListener(() => {
      failure = new Error('The connection to the Immich Bridge desktop app was closed.');
      waiting.splice(0).forEach(next => next.reject(failure));
    });

    function nextReply() {
      if (failure) {
        return Promise.reject(failure);
      }

      return new Promise((resolve, reject) => waiting.push({ resolve, reject })).then(reply => {
        if (!reply || reply.ok === false) {
          throw new Error((reply && reply.message) || 'Immich Bridge could not copy this photo.');
        }

        return reply;
      });
    }

    return {
      ready: nextReply(),
      request(message) {
        const reply = nextReply();
        port.postMessage(message);
        return reply;
      },
      close() {
        port.disconnect();
      }
    };
  }

  async function copyAsset(asset, { purpose, edited }) {
    const url = new URL(`${assetApiPrefix}${encodeURIComponent(asset.id)}/original`, window.location.href);
    if (edited) {
      url.searchParams.set('edited', 'true');
    }

    const response = await fetch(url, { credentials: 'same-origin' });
    if (!response.ok || !response.body) {
      throw new Error(`Immich returned ${response.status} for the download.`);
    }

    // A compressed response reports the compressed length, which would not match the file.
    const size = response.headers.get('Content-Encoding') ? null : Number(response.headers.get('Content-Length')) || null;
    const fileName = copyFileName(asset, edited, response.headers.get('Content-Type'));
    const channel = openCopyChannel();

    try {
      await channel.ready;
      await channel.request({ type: 'copy-start', purpose, fileName, size });

      const reader = response.body.getReader();
      let pending = new Uint8Array(0);
      let sent = 0;
      for (;;) {
        const { done, value } = await reader.read();
        if (value) {
          const combined = new Uint8Array(pending.length + value.length);
          combined.set(pending);
          combined.set(value, pending.length);
          pending = combined;
        }

        while (pending.length >= copyChunkBytes || (done && pending.length > 0)) {
          const chunk = pending.subarray(0, copyChunkBytes);
          pending = pending.slice(chunk.length);
          await channel.request({ type: 'copy-chunk', data: toBase64(chunk) });
          sent += chunk.length;
          if (size && size > progressThresholdBytes) {
            showToast(`Preparing copy… ${Math.round(sent / size * 100)} %`);
          }
        }

        if (done) {
          break;
        }
      }

      const result = await channel.request({ type: 'copy-end' });
      showToast(purpose === 'clipboard'
        ? `Copied ${result.fileName}`
        : `Opening a read-only copy of ${result.fileName}`);
    } finally {
      channel.close();
    }
  }

  function showToast(message, options = {}) {
    document.getElementById(toastId)?.remove();
    window.clearTimeout(toastTimer);

    const toast = document.createElement('div');
    toast.id = toastId;
    toast.setAttribute('role', options.error ? 'alert' : 'status');
    toast.style.cssText = [
      'position:fixed',
      'z-index:100000',
      'left:50%',
      'bottom:24px',
      'transform:translateX(-50%)',
      'max-width:min(560px, calc(100vw - 32px))',
      'display:flex',
      'align-items:center',
      'gap:12px',
      'border-radius:8px',
      `background:${options.error ? '#7f1d1d' : '#1f2937'}`,
      'color:#fff',
      'box-shadow:0 10px 24px rgba(0,0,0,.28)',
      'font:500 14px system-ui,sans-serif',
      'padding:12px 16px'
    ].join(';');
    toast.appendChild(document.createTextNode(`Immich Bridge: ${message}`));

    if (options.updateLink) {
      const link = document.createElement('a');
      link.href = latestReleaseUrl;
      link.target = '_blank';
      link.rel = 'noreferrer';
      link.textContent = 'Update';
      link.style.cssText = 'color:#fff;font-weight:600;text-decoration:underline;white-space:nowrap';
      toast.appendChild(link);
    }

    if (options.setupPath) {
      const setup = document.createElement('button');
      setup.type = 'button';
      setup.textContent = 'Set up folders';
      setup.style.cssText = 'border:1px solid #fff;border-radius:6px;background:transparent;color:#fff;cursor:pointer;font:600 13px system-ui,sans-serif;padding:6px 10px;white-space:nowrap';
      setup.addEventListener('click', () => {
        toast.remove();
        openFolderSetup(options.setupPath);
      });
      toast.appendChild(setup);
    }

    toast.addEventListener('click', event => {
      if (event.target === toast) {
        toast.remove();
      }
    });
    document.body.appendChild(toast);
    toastTimer = window.setTimeout(() => toast.remove(), options.setupPath ? 15000 : options.error ? 8000 : 3000);
  }

  function showButtonError(error) {
    console.error('[Immich Bridge]', error);
    showToast(error.message || String(error), { error: true });
  }

  const iconPaths = {
    folder: 'M10,4L12,6H20A2,2 0 0,1 22,8V18A2,2 0 0,1 20,20H4A2,2 0 0,1 2,18V6A2,2 0 0,1 4,4H10M4,8V18H20V8H4Z',
    copy: 'M19,21H8V7H19M19,5H8A2,2 0 0,0 6,7V21A2,2 0 0,0 8,23H19A2,2 0 0,0 21,21V7A2,2 0 0,0 19,5M16,1H4A2,2 0 0,0 2,3V17H4V3H16V1Z',
    app: 'M14,3V5H17.59L7.76,14.83L9.17,16.24L19,6.41V10H21V3H14M19,19H5V5H12V3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V12H19V19Z',
    update: 'M12,18A6,6 0 0,1 6,12C6,11 6.25,10.03 6.7,9.2L5.24,7.74C4.46,8.97 4,10.43 4,12A8,8 0 0,0 12,20V23L16,19L12,15M12,4V1L8,5L12,9V6A6,6 0 0,1 18,12C18,13 17.75,13.97 17.3,14.8L18.76,16.26C19.54,15.03 20,13.57 20,12A8,8 0 0,0 12,4Z'
  };

  function createSvgIcon(kind, size) {
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('width', String(size));
    svg.setAttribute('height', String(size));
    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('stroke', 'transparent');
    svg.setAttribute('stroke-width', '2');
    svg.setAttribute('role', 'img');
    svg.setAttribute('aria-hidden', 'true');

    const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    path.setAttribute('fill', 'currentColor');
    path.setAttribute('d', iconPaths[kind] || iconPaths.app);
    svg.appendChild(path);
    return svg;
  }

  function createToolbarButton() {
    const button = document.createElement('button');
    button.dataset.buttonRoot = 'true';
    button.type = 'button';
    button.setAttribute('aria-label', 'Immich Bridge');
    button.setAttribute('aria-haspopup', 'true');
    button.setAttribute('aria-controls', menuId);
    button.setAttribute('aria-expanded', 'false');
    button.title = 'Immich Bridge';
    button.className = 'flex items-center justify-center gap-1 font-medium outline-offset-2 transition-colors focus-visible:outline-2 cursor-pointer rounded-full text-base h-10 w-10 outline-dark text-dark not-disabled:hover:bg-light-100';
    button.appendChild(createSvgIcon('app', '60%'));
    button.addEventListener('click', event => {
      event.stopPropagation();
      toggleMenu(button);
    });
    return button;
  }

  function createMenuItem(item, asset) {
    const menuItem = document.createElement('button');
    menuItem.type = 'button';
    menuItem.setAttribute('role', 'menuitem');
    menuItem.style.cssText = [
      'display:flex',
      'align-items:center',
      'gap:10px',
      'width:100%',
      'border:0',
      'background:transparent',
      `color:${item.muted ? '#4b5563' : '#111827'}`,
      'cursor:pointer',
      'font:500 14px system-ui,sans-serif',
      'padding:13px 16px',
      'text-align:left',
      'white-space:nowrap'
    ].join(';');
    menuItem.append(createSvgIcon(item.icon, 18), document.createTextNode(item.label));
    menuItem.addEventListener('mouseenter', () => {
      menuItem.style.background = '#e5e7eb';
    });
    menuItem.addEventListener('mouseleave', () => {
      menuItem.style.background = 'transparent';
    });
    menuItem.addEventListener('click', async event => {
      event.preventDefault();
      event.stopPropagation();
      closeMenu();
      if (item.url) {
        window.open(item.url, '_blank', 'noreferrer');
        return;
      }

      try {
        if (item.copy) {
          await copyAsset(asset, item.copy);
        } else {
          await launch(item.action, item.appId);
        }
      } catch (error) {
        showButtonError(error);
      }
    });
    return menuItem;
  }

  function createMenu(button, asset) {
    const menu = document.createElement('div');
    menu.id = menuId;
    menu.setAttribute('role', 'menu');
    menu.setAttribute('aria-labelledby', button.id);
    menu.style.cssText = [
      'position:fixed',
      'z-index:99999',
      'min-width:220px',
      'overflow:hidden',
      'border-radius:8px',
      'background:#f1f5f9',
      'box-shadow:0 10px 24px rgba(0,0,0,.28)',
      'padding:4px 0'
    ].join(';');
    availableMenuItems(asset).forEach(item => menu.appendChild(createMenuItem(item, asset)));
    return menu;
  }

  async function toggleMenu(button) {
    const existing = document.getElementById(menuId);
    if (existing) {
      closeMenu();
      return;
    }

    // The entries depend on the asset (internal or external, edited) and on the companion.
    let asset;
    try {
      [asset] = await Promise.all([getCurrentAsset(), companionStatus ? null : refreshCompanionStatus()]);
    } catch (error) {
      showButtonError(error);
      return;
    }

    if (document.getElementById(menuId)) {
      return;
    }

    const menu = createMenu(button, asset);
    document.body.appendChild(menu);
    positionMenu(button, menu);
    button.setAttribute('aria-expanded', 'true');
    // Picks up a companion install or update for the next time the menu opens.
    refreshCompanionStatus();
  }

  function closeMenu() {
    document.getElementById(menuId)?.remove();
    document.querySelector(`#${toolbarHostId} button`)?.setAttribute('aria-expanded', 'false');
  }

  function positionMenu(button, menu) {
    const margin = 8;
    const rect = button.getBoundingClientRect();
    const menuWidth = menu.offsetWidth || 220;
    const left = Math.max(margin, Math.min(rect.left, window.innerWidth - menuWidth - margin));
    const top = Math.min(rect.bottom + margin, window.innerHeight - menu.offsetHeight - margin);
    menu.style.left = `${left}px`;
    menu.style.top = `${Math.max(margin, top)}px`;
  }

  function removeBridgeUi() {
    document.getElementById(toolbarHostId)?.remove();
    closeMenu();
    lastAssetId = null;
    cachedAsset = null;
  }

  function findDirectChild(parent, descendant) {
    let child = descendant;
    while (child.parentElement && child.parentElement !== parent) {
      child = child.parentElement;
    }

    return child.parentElement === parent ? child : descendant;
  }

  function isVisibleElement(element) {
    const rect = element.getBoundingClientRect();
    const style = window.getComputedStyle(element);
    return rect.width > 0
      && rect.height > 0
      && style.display !== 'none'
      && style.visibility !== 'hidden'
      && style.opacity !== '0';
  }

  function findToolbarButtons(toolbar) {
    return [...toolbar.querySelectorAll('button[data-button-root="true"], button')]
      .filter(button => !button.closest(`#${toolbarHostId}`) && isVisibleElement(button));
  }

  function findInsertionPoint() {
    const toolbars = [...document.querySelectorAll('#immich-asset-viewer [data-testid="asset-viewer-navbar-actions"]')]
      .filter(isVisibleElement)
      .map(toolbar => ({ toolbar, firstButton: findToolbarButtons(toolbar)[0] }))
      .filter(candidate => candidate.firstButton);

    if (toolbars.length === 0) {
      return null;
    }

    const selected = toolbars[toolbars.length - 1];
    return { parent: selected.toolbar, before: findDirectChild(selected.toolbar, selected.firstButton) };
  }

  function ensureToolbarButton() {
    const assetId = extractAssetIdFromUrl();
    if (!assetId) {
      removeBridgeUi();
      return;
    }

    const insertionPoint = findInsertionPoint();
    if (!insertionPoint) {
      return;
    }

    const existing = document.getElementById(toolbarHostId);
    if (existing && existing.dataset.assetId === assetId) {
      if (existing.parentElement !== insertionPoint.parent || existing.nextSibling !== insertionPoint.before) {
        insertionPoint.parent.insertBefore(existing, insertionPoint.before);
      }
      return;
    }

    if (existing) {
      existing.dataset.assetId = assetId;
      if (existing.parentElement !== insertionPoint.parent || existing.nextSibling !== insertionPoint.before) {
        insertionPoint.parent.insertBefore(existing, insertionPoint.before);
      }
      return;
    }

    const host = document.createElement('div');
    host.id = toolbarHostId;
    host.dataset.assetId = assetId;
    host.style.cssText = 'display:flex;align-items:center;justify-content:center;';

    const button = createToolbarButton();
    button.id = `${toolbarHostId}-button`;
    host.appendChild(button);
    insertionPoint.parent.insertBefore(host, insertionPoint.before);
  }

  function scheduleToolbarRefresh() {
    window.clearTimeout(toolbarRefreshTimer);
    toolbarRefreshTimer = window.setTimeout(ensureToolbarButton, 100);
  }

  function scheduleMutationRefresh() {
    window.clearTimeout(mutationRefreshTimer);
    mutationRefreshTimer = window.setTimeout(ensureToolbarButton, 250);
  }

  function scheduleToolbarRefreshIfLocationChanged() {
    if (lastLocation === window.location.href) {
      return;
    }

    lastLocation = window.location.href;
    if (!extractAssetIdFromUrl()) {
      removeBridgeUi();
    }
    scheduleRouteRefreshes();
  }

  function scheduleRouteRefreshes() {
    for (const delay of routeRetryDelays) {
      window.setTimeout(ensureToolbarButton, delay);
    }
  }

  for (const methodName of ['pushState', 'replaceState']) {
    const original = history[methodName];
    history[methodName] = function (...args) {
      const result = original.apply(this, args);
      if (!extractAssetIdFromUrl()) {
        removeBridgeUi();
      }
      scheduleRouteRefreshes();
      return result;
    };
  }

  window.addEventListener('popstate', scheduleRouteRefreshes);
  window.addEventListener('hashchange', scheduleRouteRefreshes);
  window.addEventListener('resize', closeMenu);
  window.addEventListener('scroll', closeMenu, true);
  window.addEventListener('click', closeMenu);
  window.addEventListener('keydown', event => {
    if (event.key === 'Escape') {
      closeMenu();
    }
  });
  new MutationObserver(scheduleMutationRefresh).observe(document.documentElement, { childList: true, subtree: true });
  window.setInterval(scheduleToolbarRefreshIfLocationChanged, 250);
  scheduleToolbarRefresh();
  refreshCompanionStatus();
  }
})();
