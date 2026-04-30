// ==UserScript==
// @name         Immich Bridge
// @namespace    https://github.com/local/immich-bridge
// @version      0.1.0
// @description  Adds Immich Bridge local workstation actions to Immich asset detail pages.
// @match        *://*/*
// @grant        none
// ==/UserScript==

(function () {
  'use strict';

  const protocol = 'immich-bridge';
  const toolbarId = 'immich-bridge-toolbar';
  const assetApiPrefix = '/api/assets/';
  let lastAssetId = null;
  let cachedAsset = null;

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

  function createButton(label, onClick) {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = label;
    button.style.cssText = [
      'background:#2563eb',
      'border:1px solid #1d4ed8',
      'border-radius:6px',
      'color:#fff',
      'cursor:pointer',
      'font:500 13px system-ui,sans-serif',
      'line-height:1',
      'padding:8px 10px',
      'white-space:nowrap'
    ].join(';');
    button.addEventListener('click', async () => {
      try {
        await onClick();
      } catch (error) {
        showButtonError(error);
      }
    });
    return button;
  }

  function removeToolbar() {
    document.getElementById(toolbarId)?.remove();
    lastAssetId = null;
    cachedAsset = null;
  }

  function ensureToolbar() {
    const assetId = extractAssetIdFromUrl();
    if (!assetId) {
      removeToolbar();
      return;
    }

    const existing = document.getElementById(toolbarId);
    if (existing && existing.dataset.assetId === assetId) {
      return;
    }

    existing?.remove();

    const toolbar = document.createElement('div');
    toolbar.id = toolbarId;
    toolbar.dataset.assetId = assetId;
    toolbar.style.cssText = [
      'position:fixed',
      'right:16px',
      'top:72px',
      'z-index:99999',
      'display:flex',
      'gap:8px',
      'align-items:center',
      'background:rgba(17,24,39,.92)',
      'border:1px solid rgba(255,255,255,.16)',
      'border-radius:8px',
      'box-shadow:0 8px 24px rgba(0,0,0,.22)',
      'padding:8px'
    ].join(';');

    toolbar.append(
      createButton('Reveal in Explorer', () => launch('reveal')),
      createButton('Open in Photoshop', () => launch('open', 'photoshop'))
    );

    document.body.appendChild(toolbar);
  }

  function scheduleToolbarRefresh() {
    window.setTimeout(ensureToolbar, 100);
  }

  for (const methodName of ['pushState', 'replaceState']) {
    const original = history[methodName];
    history[methodName] = function (...args) {
      const result = original.apply(this, args);
      scheduleToolbarRefresh();
      return result;
    };
  }

  window.addEventListener('popstate', scheduleToolbarRefresh);
  new MutationObserver(scheduleToolbarRefresh).observe(document.documentElement, { childList: true, subtree: true });
  scheduleToolbarRefresh();
})();
