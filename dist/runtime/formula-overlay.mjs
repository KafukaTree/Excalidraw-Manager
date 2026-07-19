const ROOT_ID = 'excalidraw-manager-formula-overlay';
const STORAGE_KEY = 'excalidraw-manager.formula-overlay';
const MAX_SVG_BYTES = 4 * 1024 * 1024;
const PANEL_MARGIN = 8;
const MIN_PANEL_WIDTH = 260;
const MIN_PANEL_HEIGHT = 250;

function clamp(value, minimum, maximum) {
  return Math.min(Math.max(value, minimum), maximum);
}

function isLoopbackFormulaUrl(raw) {
  try {
    const url = new URL(raw);
    return url.protocol === 'http:' && !url.username && !url.password
      && ['localhost', '127.0.0.1', '[::1]'].includes(url.hostname)
      ? url
      : null;
  } catch {
    return null;
  }
}

function loadPlacement() {
  try {
    const value = JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}');
    return value && typeof value === 'object' ? value : {};
  } catch {
    return {};
  }
}

function savePlacement(panel, open) {
  const previous = loadPlacement();
  if (panel.hidden) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ ...previous, open }));
    return;
  }
  const rect = panel.getBoundingClientRect();
  localStorage.setItem(STORAGE_KEY, JSON.stringify({
    open,
    left: Math.round(rect.left),
    top: Math.round(rect.top),
    width: Math.round(rect.width),
    height: Math.round(rect.height),
  }));
}

function safeSvgText(value) {
  if (typeof value !== 'string' || value.length === 0 || new Blob([value]).size > MAX_SVG_BYTES) return null;
  const documentValue = new DOMParser().parseFromString(value, 'image/svg+xml');
  const svg = documentValue.documentElement;
  if (!svg || svg.localName !== 'svg' || documentValue.doctype
    || documentValue.querySelector('parsererror, script, style, foreignObject')) return null;
  for (const element of documentValue.querySelectorAll('*')) {
    for (const attribute of [...element.attributes]) {
      const name = attribute.name.toLowerCase();
      const attributeValue = attribute.value.trim().toLowerCase();
      if (name.startsWith('on')) return null;
      if ((name === 'href' || name === 'xlink:href') && attributeValue && !attributeValue.startsWith('#')) return null;
      if (/url\s*\(/i.test(attributeValue) && !/^url\(\s*#[^)]+\s*\)$/i.test(attributeValue)) return null;
    }
  }
  return new XMLSerializer().serializeToString(svg);
}

function visibleCanvasPoint() {
  const excalidraw = document.querySelector('.excalidraw');
  if (!excalidraw) return null;
  const rect = excalidraw.getBoundingClientRect();
  const candidates = [
    [.5, .55], [.5, .7], [.35, .55], [.65, .55], [.35, .75], [.65, .75], [.5, .35],
  ];
  for (const [xRatio, yRatio] of candidates) {
    const x = Math.round(rect.left + rect.width * xRatio);
    const y = Math.round(rect.top + rect.height * yRatio);
    const target = document.elementFromPoint(x, y);
    if (target instanceof HTMLCanvasElement && excalidraw.contains(target)) return { target, x, y };
  }
  return null;
}

function dispatchSvgPaste(svg) {
  const point = visibleCanvasPoint();
  if (!point) throw new Error('No visible Excalidraw canvas is available');
  const transfer = new DataTransfer();
  transfer.setData('text/plain', svg);
  transfer.setData('image/svg+xml', svg);
  transfer.items.add(new File([svg], 'formula.svg', { type: 'image/svg+xml' }));
  const { target, x, y } = point;
  const temporaryTabIndex = !target.hasAttribute('tabindex');
  if (temporaryTabIndex) target.setAttribute('tabindex', '-1');
  target.focus?.({ preventScroll: true });
  try {
    target.dispatchEvent(new PointerEvent('pointermove', {
      bubbles: true,
      clientX: x,
      clientY: y,
      pointerId: 1,
      pointerType: 'mouse',
      isPrimary: true,
    }));
    target.dispatchEvent(new ClipboardEvent('paste', {
      bubbles: true,
      cancelable: true,
      clipboardData: transfer,
    }));
  } finally {
    if (temporaryTabIndex) target.removeAttribute('tabindex');
  }
}

function clampPanel(panel) {
  const rect = panel.getBoundingClientRect();
  const availableWidth = Math.max(1, innerWidth - PANEL_MARGIN * 2);
  const availableHeight = Math.max(1, innerHeight - PANEL_MARGIN * 2);
  const minimumWidth = Math.min(MIN_PANEL_WIDTH, availableWidth);
  const minimumHeight = Math.min(MIN_PANEL_HEIGHT, availableHeight);
  const width = clamp(rect.width, minimumWidth, availableWidth);
  const height = clamp(rect.height, minimumHeight, availableHeight);
  const left = clamp(rect.left, PANEL_MARGIN, Math.max(PANEL_MARGIN, innerWidth - width - PANEL_MARGIN));
  const top = clamp(rect.top, PANEL_MARGIN, Math.max(PANEL_MARGIN, innerHeight - height - PANEL_MARGIN));
  Object.assign(panel.style, { width: `${width}px`, height: `${height}px`, left: `${left}px`, top: `${top}px` });
}

export function startFormulaOverlay(rawUrl) {
  if (document.getElementById(ROOT_ID)) return;
  const formulaUrl = isLoopbackFormulaUrl(rawUrl);
  if (!formulaUrl) return;
  formulaUrl.searchParams.set('compact', '1');
  formulaUrl.searchParams.set('embed', '1');

  const zh = (navigator.language || '').toLowerCase().startsWith('zh');
  const labels = zh ? {
    title: '公式悬浮窗', open: '公式', close: '收起', full: '完整编辑器',
    hint: 'Ctrl+Enter 复制所选格式 · Ctrl+Alt+O 截图 OCR', inserted: 'SVG 已发送到画板',
    fallback: 'SVG 已复制，请在画板按 Ctrl+V', invalid: '拒绝了无效的 SVG',
  } : {
    title: 'Formula palette', open: 'Formula', close: 'Collapse', full: 'Full editor',
    hint: 'Ctrl+Enter copies the selected format · Ctrl+Alt+O captures OCR', inserted: 'SVG sent to the board',
    fallback: 'SVG copied; press Ctrl+V on the board', invalid: 'Invalid SVG was rejected',
  };

  const root = document.createElement('div');
  root.id = ROOT_ID;
  const shadow = root.attachShadow({ mode: 'open' });
  shadow.innerHTML = `
    <style>
      :host { all: initial; }
      * { box-sizing: border-box; }
      #launcher { position: fixed; right: 14px; bottom: 82px; z-index: 2147483600; border: 1px solid #dde1e8;
        border-radius: 10px; padding: 9px 14px; color: #1c1f2a; background: #fff; box-shadow: 0 2px 6px #1018280f, 0 8px 22px #10182814;
        font: 600 13px/1.2 system-ui, sans-serif; cursor: pointer; transition: border-color .15s, color .15s, background .15s; }
      #launcher:hover { border-color: #343a46; color: #343a46; background: #f0f1f3; }
      #panel { position: fixed; z-index: 2147483601;
        container-type: inline-size;
        min-width: min(${MIN_PANEL_WIDTH}px, calc(100vw - ${PANEL_MARGIN * 2}px));
        min-height: min(${MIN_PANEL_HEIGHT}px, calc(100vh - ${PANEL_MARGIN * 2}px));
        width: 520px; height: 520px; border: 1px solid #dde1e8; border-radius: 13px; overflow: hidden; background: #fff;
        box-shadow: 0 4px 10px #1018280d, 0 18px 48px #1018281f; font: 13px/1.3 system-ui, sans-serif; }
      #panel[hidden] { display: none; }
      header { height: 44px; display: flex; align-items: center; gap: 6px; padding: 0 7px 0 12px; color: #1c1f2a;
        background: #fafbfc; border-bottom: 1px solid #e7eaef; user-select: none; cursor: move; }
      header strong { flex: 1 1 auto; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 13px; font-weight: 650; }
      header .header-hint { min-width: 0; overflow: hidden; text-overflow: ellipsis; color: #69707f; font-size: 11px; white-space: nowrap; }
      header button, header a { border: 0; border-radius: 7px; padding: 6px 8px; color: #55606e; background: transparent;
        flex: 0 0 auto; display: inline-flex; align-items: center; justify-content: center; text-decoration: none;
        font: 12px/1 system-ui, sans-serif; cursor: pointer; }
      .header-action-icon { display: none; font-size: 15px; }
      header button:hover, header a:hover { color: #343a46; background: #f0f1f3; }
      iframe { display: block; width: 100%; height: calc(100% - 44px); border: 0; background: #fff; }
      .resize-handle { position: absolute; z-index: 5; display: block; touch-action: none; user-select: none; }
      .resize-handle[data-edge="n"] { top: 0; left: 12px; right: 12px; height: 8px; cursor: ns-resize; }
      .resize-handle[data-edge="s"] { bottom: 0; left: 12px; right: 12px; height: 8px; cursor: ns-resize; }
      .resize-handle[data-edge="e"] { top: 12px; right: 0; bottom: 12px; width: 8px; cursor: ew-resize; }
      .resize-handle[data-edge="w"] { top: 12px; left: 0; bottom: 12px; width: 8px; cursor: ew-resize; }
      .resize-handle[data-edge="ne"] { top: 0; right: 0; width: 14px; height: 14px; cursor: nesw-resize; }
      .resize-handle[data-edge="nw"] { top: 0; left: 0; width: 14px; height: 14px; cursor: nwse-resize; }
      .resize-handle[data-edge="se"] { right: 0; bottom: 0; width: 14px; height: 14px; cursor: nwse-resize; }
      .resize-handle[data-edge="sw"] { left: 0; bottom: 0; width: 14px; height: 14px; cursor: nesw-resize; }
      #toast { position: fixed; left: 50%; bottom: 28px; z-index: 2147483602; transform: translateX(-50%) translateY(12px);
        max-width: min(480px, calc(100vw - 32px)); padding: 9px 14px; border-radius: 10px; color: #fff; background: #20242e;
        box-shadow: 0 10px 30px #0000003d; opacity: 0; pointer-events: none; transition: .16s ease;
        font: 13px/1.3 system-ui, sans-serif; }
      #toast.show { opacity: 1; transform: translateX(-50%) translateY(0); }
      @container (max-width: 440px) {
        header { gap: 3px; padding-left: 8px; padding-right: 4px; }
        header .header-hint, .header-action-label { display: none; }
        .header-action-icon { display: inline; }
        header button, header a { width: 30px; height: 30px; padding: 0; }
      }
      @media (prefers-color-scheme: dark) {
        #launcher, #panel { color: #e8eaf0; background: #15181d; border-color: #38404b; }
        #launcher:hover { color: #d6dae1; background: #2b3038; border-color: #59616c; }
        header { color: #e8eaf0; background: #1b1f26; border-color: #262b33; }
        header .header-hint { color: #99a1af; } header button, header a { color: #c6cdd8; }
        header button:hover, header a:hover { color: #d6dae1; background: #2b3038; }
      }
    </style>
    <button id="launcher" type="button" title="Ctrl+Alt+F">ƒx&nbsp; ${labels.open}</button>
    <section id="panel" role="dialog" aria-label="${labels.title}" hidden>
      <header id="drag-handle">
        <strong>${labels.title}</strong><span class="header-hint">${labels.hint}</span>
        <a id="full" target="_blank" rel="noopener" aria-label="${labels.full}" title="${labels.full}"><span class="header-action-icon" aria-hidden="true">□</span><span class="header-action-label">${labels.full}</span></a>
        <button id="close" type="button" aria-label="${labels.close}">✕</button>
      </header>
      <iframe id="frame" title="${labels.title}" allow="clipboard-read; clipboard-write"></iframe>
      ${['n', 'ne', 'e', 'se', 's', 'sw', 'w', 'nw'].map((edge) => `<span class="resize-handle" data-edge="${edge}" aria-hidden="true"></span>`).join('')}
    </section>
    <div id="toast" role="status"></div>`;
  document.body.append(root);

  const launcher = shadow.getElementById('launcher');
  const panel = shadow.getElementById('panel');
  const frame = shadow.getElementById('frame');
  const full = shadow.getElementById('full');
  const close = shadow.getElementById('close');
  const handle = shadow.getElementById('drag-handle');
  const toast = shadow.getElementById('toast');
  const placement = loadPlacement();
  let toastTimer;
  let loaded = false;
  let frameReady = false;
  let pendingCapture = false;

  const fullUrl = new URL(formulaUrl);
  fullUrl.searchParams.delete('compact');
  fullUrl.searchParams.delete('embed');
  full.href = fullUrl.href;
  Object.assign(panel.style, {
    left: `${Number.isFinite(placement.left) ? placement.left : Math.max(8, innerWidth - 540)}px`,
    top: `${Number.isFinite(placement.top) ? placement.top : 70}px`,
    width: `${Number.isFinite(placement.width) ? placement.width : 520}px`,
    height: `${Number.isFinite(placement.height) ? placement.height : 520}px`,
  });

  const showToast = (text) => {
    clearTimeout(toastTimer);
    toast.textContent = text;
    toast.classList.add('show');
    toastTimer = setTimeout(() => toast.classList.remove('show'), 2400);
  };
  const setOpen = (open) => {
    if (!open && !panel.hidden) savePlacement(panel, false);
    panel.hidden = !open;
    launcher.hidden = open;
    if (open) {
      if (!loaded) { frame.src = formulaUrl.href; loaded = true; }
      requestAnimationFrame(() => { clampPanel(panel); frame.focus(); });
    }
    if (open) savePlacement(panel, true);
  };
  const sendCaptureRequest = () => {
    setOpen(true);
    if (frameReady) {
      frame.contentWindow?.postMessage({ type: 'excalidraw-manager:capture-formula' }, formulaUrl.origin);
    } else {
      pendingCapture = true;
    }
  };
  frame.addEventListener('load', () => {
    frameReady = true;
    if (!pendingCapture) return;
    pendingCapture = false;
    frame.contentWindow?.postMessage({ type: 'excalidraw-manager:capture-formula' }, formulaUrl.origin);
  });
  launcher.addEventListener('click', () => setOpen(true));
  close.addEventListener('click', () => setOpen(false));
  addEventListener('resize', () => { if (!panel.hidden) clampPanel(panel); });

  let drag = null;
  handle.addEventListener('pointerdown', (event) => {
    if (!event.isPrimary || event.button !== 0 || event.target.closest('button, a')) return;
    const rect = panel.getBoundingClientRect();
    drag = { pointerId: event.pointerId, x: event.clientX, y: event.clientY, left: rect.left, top: rect.top };
    handle.setPointerCapture(event.pointerId);
  });
  handle.addEventListener('pointermove', (event) => {
    if (!drag || event.pointerId !== drag.pointerId) return;
    panel.style.left = `${drag.left + event.clientX - drag.x}px`;
    panel.style.top = `${drag.top + event.clientY - drag.y}px`;
    clampPanel(panel);
  });
  const endDrag = (event) => {
    if (!drag || event.pointerId !== drag.pointerId) return;
    drag = null;
    savePlacement(panel, true);
  };
  handle.addEventListener('pointerup', endDrag);
  handle.addEventListener('pointercancel', endDrag);
  handle.addEventListener('lostpointercapture', endDrag);

  for (const resizeHandle of shadow.querySelectorAll('.resize-handle')) {
    let resize = null;
    resizeHandle.addEventListener('pointerdown', (event) => {
      if (!event.isPrimary || event.button !== 0) return;
      event.preventDefault();
      event.stopPropagation();
      const rect = panel.getBoundingClientRect();
      resize = {
        pointerId: event.pointerId,
        edge: resizeHandle.dataset.edge,
        pointerX: event.clientX,
        pointerY: event.clientY,
        left: rect.left,
        top: rect.top,
        right: rect.right,
        bottom: rect.bottom,
      };
      resizeHandle.setPointerCapture(event.pointerId);
    });
    resizeHandle.addEventListener('pointermove', (event) => {
      if (!resize || event.pointerId !== resize.pointerId) return;
      const availableWidth = Math.max(1, innerWidth - PANEL_MARGIN * 2);
      const availableHeight = Math.max(1, innerHeight - PANEL_MARGIN * 2);
      const minimumWidth = Math.min(MIN_PANEL_WIDTH, availableWidth);
      const minimumHeight = Math.min(MIN_PANEL_HEIGHT, availableHeight);
      const deltaX = event.clientX - resize.pointerX;
      const deltaY = event.clientY - resize.pointerY;
      let { left, top, right, bottom } = resize;
      if (resize.edge.includes('w')) left = clamp(resize.left + deltaX, PANEL_MARGIN, resize.right - minimumWidth);
      if (resize.edge.includes('e')) right = clamp(resize.right + deltaX, resize.left + minimumWidth, innerWidth - PANEL_MARGIN);
      if (resize.edge.includes('n')) top = clamp(resize.top + deltaY, PANEL_MARGIN, resize.bottom - minimumHeight);
      if (resize.edge.includes('s')) bottom = clamp(resize.bottom + deltaY, resize.top + minimumHeight, innerHeight - PANEL_MARGIN);
      Object.assign(panel.style, {
        left: `${left}px`,
        top: `${top}px`,
        width: `${right - left}px`,
        height: `${bottom - top}px`,
      });
    });
    const endResize = (event) => {
      if (!resize || event.pointerId !== resize.pointerId) return;
      resize = null;
      clampPanel(panel);
      savePlacement(panel, true);
    };
    resizeHandle.addEventListener('pointerup', endResize);
    resizeHandle.addEventListener('pointercancel', endResize);
    resizeHandle.addEventListener('lostpointercapture', endResize);
  }
  new ResizeObserver(() => { if (!panel.hidden) savePlacement(panel, true); }).observe(panel);

  addEventListener('keydown', (event) => {
    if ((event.ctrlKey || event.metaKey) && event.altKey && event.key.toLowerCase() === 'f') {
      event.preventDefault();
      setOpen(panel.hidden);
      return;
    }
    if ((event.ctrlKey || event.metaKey) && event.altKey && event.key.toLowerCase() === 'o') {
      event.preventDefault();
      sendCaptureRequest();
    }
  }, true);

  async function handleSvgInsert(data, restoreFocusTarget) {
    const svg = safeSvgText(data?.svg);
    if (!svg) { showToast(labels.invalid); return; }
    window.focus();
    try {
      // Best-effort clipboard fallback. The iframe also attempts this while it
      // still owns the Ctrl+Enter/click user activation.
      navigator.clipboard?.writeText(svg).catch(() => {});
      await new Promise((resolve) => requestAnimationFrame(resolve));
      // Keep the palette visible, but make it transparent to hit testing for
      // the instant in which Excalidraw chooses a canvas insertion point.
      const previousPointerEvents = panel.style.pointerEvents;
      panel.style.pointerEvents = 'none';
      try {
        dispatchSvgPaste(svg);
      } finally {
        panel.style.pointerEvents = previousPointerEvents;
      }
      showToast(labels.inserted);
      requestAnimationFrame(() => restoreFocusTarget?.focus());
    } catch (error) {
      console.error('Formula SVG insertion failed:', error);
      try {
        await navigator.clipboard.writeText(svg);
        showToast(labels.fallback);
      } catch {
        showToast(labels.invalid);
      }
    }
  }

  addEventListener('message', (event) => {
    if (event.origin !== formulaUrl.origin || event.source !== frame.contentWindow
      || event.data?.type !== 'excalidraw-manager:insert-svg') return;
    handleSvgInsert(event.data, frame.contentWindow);
  });

  if (placement.open) setOpen(true);
}
