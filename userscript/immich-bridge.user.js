// ==UserScript==
// @name         Immich Bridge
// @namespace    https://github.com/Peaj/immich-bridge
// @version      0.4.0
// @description  Adds Immich Bridge local workstation actions to Immich asset detail pages.
// @match        *://*/*
// @grant        none
// ==/UserScript==

(function () {
  'use strict';

  const protocol = 'immich-bridge';
  const toolbarHostId = 'immich-bridge-toolbar-host';
  const menuId = 'immich-bridge-menu';
  const assetApiPrefix = '/api/assets/';
  const actions = [
    { label: 'Reveal in Explorer', action: 'reveal', icon: 'folder' },
    { label: 'Open with...', action: 'open', icon: 'app' }
  ];
  let lastAssetId = null;
  let cachedAsset = null;
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

    const response = await fetch(`${assetApiPrefix}${encodeURIComponent(assetId)}`, {
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

  async function launch(action, appId) {
    const asset = await getCurrentAsset();
    const originalPath = asset && asset.originalPath;

    if (!originalPath) {
      throw new Error('Immich asset did not include originalPath.');
    }

    const query = new URLSearchParams();
    query.set('path', originalPath);
    if (appId) {
      query.set('app', appId);
    }

    window.location.href = `${protocol}://${action}?${query.toString()}`;
  }

  function showButtonError(error) {
    console.error('[Immich Bridge]', error);
    window.alert(`Immich Bridge: ${error.message || error}`);
  }

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
    path.setAttribute('d', kind === 'folder'
      ? 'M10,4L12,6H20A2,2 0 0,1 22,8V18A2,2 0 0,1 20,20H4A2,2 0 0,1 2,18V6A2,2 0 0,1 4,4H10M4,8V18H20V8H4Z'
      : 'M14,3V5H17.59L7.76,14.83L9.17,16.24L19,6.41V10H21V3H14M19,19H5V5H12V3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V12H19V19Z');
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

  function createMenuItem(item) {
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
      'color:#111827',
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
      try {
        await launch(item.action, item.appId);
      } catch (error) {
        showButtonError(error);
      }
    });
    return menuItem;
  }

  function createMenu(button) {
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
    actions.forEach(action => menu.appendChild(createMenuItem(action)));
    return menu;
  }

  function toggleMenu(button) {
    const existing = document.getElementById(menuId);
    if (existing) {
      closeMenu();
      return;
    }

    const menu = createMenu(button);
    document.body.appendChild(menu);
    positionMenu(button, menu);
    button.setAttribute('aria-expanded', 'true');
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
    window.setTimeout(ensureToolbarButton, 100);
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
})();
