namespace ScadaBuilderV2.App;

// The WebView2 canvas bootstrap/extraction script, isolated from MainWindow.xaml.cs
// as a behavior-preserving split. This is data (a raw-string JS/CSS bundle), not logic.
public partial class MainWindow
{
    private const string LegacyExtractionScript = """
(() => {
  if (window.scadaSceneEditor || window.scadaLegacyExtraction) {
    const api = window.scadaSceneEditor || window.scadaLegacyExtraction;
    api.refresh();
    return;
  }

  const selectableSelector = '[data-id]:not(.scada-modern-element)';
  const inventorySelector = '.layer[data-id]:not(.scada-modern-element), .shape-layer [data-id]';
  const selected = new Set();
  const hidden = new Set();
  const removedNodes = new Map();

  const style = document.createElement('style');
  style.textContent = `
    [data-scada-selected="true"] {
      outline: 2px solid #2090a0 !important;
      outline-offset: 2px !important;
      filter: drop-shadow(0 0 5px rgba(32,144,160,.55));
    }
    #scada-extract-marquee {
      position: fixed;
      z-index: 2147483645;
      border: 1px solid #2090a0;
      background: rgba(32,144,160,.14);
      pointer-events: none;
      display: none;
    }
    #scada-extract-menu {
      position: fixed;
      z-index: 2147483647;
      min-width: 168px;
      padding: 6px;
      border: 1px solid rgba(15,42,48,.18);
      background: #fff;
      box-shadow: 0 10px 28px rgba(15,42,48,.18);
      display: none;
      font: 13px Segoe UI, Arial, sans-serif;
      pointer-events: auto;
    }
    #scada-extract-menu button {
      width: 100%;
      display: block;
      margin: 0;
      padding: 7px 9px;
      border: 0;
      background: transparent;
      color: #0f2a30;
      text-align: left;
      cursor: pointer;
    }
    #scada-extract-menu button:hover { background: #e0f2d0; }
    #scada-extract-menu button:disabled,
    #scada-extract-menu button[aria-disabled="true"] {
      color: #8a9aa0;
      cursor: not-allowed;
      background: transparent;
    }
    #scada-extract-menu .submenu {
      position: relative;
    }
    #scada-extract-menu .submenu::after {
      content: '';
      position: absolute;
      left: 100%;
      top: -8px;
      width: 14px;
      height: calc(100% + 16px);
      background: transparent;
    }
    #scada-extract-menu .submenu[data-submenu-x="left"]::after {
      left: auto;
      right: 100%;
    }
    #scada-extract-menu .submenu > button {
      padding-right: 24px;
    }
    #scada-extract-menu .submenu > button::after {
      content: '>';
      position: absolute;
      right: 10px;
      color: #5f747a;
    }
    #scada-extract-menu .submenu[data-submenu-x="left"] > button::after {
      content: '<';
    }
    #scada-extract-menu .submenu-panel {
      position: absolute;
      left: calc(100% - 1px);
      top: -4px;
      min-width: 190px;
      padding: 6px;
      border: 1px solid rgba(15,42,48,.18);
      background: #fff;
      box-shadow: 0 10px 28px rgba(15,42,48,.18);
      display: none;
    }
    #scada-extract-menu .submenu[data-submenu-x="left"] .submenu-panel {
      left: auto;
      right: calc(100% - 1px);
    }
    #scada-extract-menu .submenu:hover > .submenu-panel,
    #scada-extract-menu .submenu:focus-within > .submenu-panel {
      display: block;
    }
    #scada-modern-layer {
      position: absolute;
      inset: 0;
      z-index: 2147483000;
      pointer-events: none;
    }
    .scada-modern-element {
      position: absolute;
      box-sizing: border-box;
      pointer-events: auto;
      display: flex;
      align-items: center;
      padding: 0 8px;
      color: #0f2a30;
      background: #fff;
      border: 1px solid #8aa0a6;
      font: 14px Segoe UI, Arial, sans-serif;
      user-select: none;
      cursor: pointer;
    }
    body.scada-placement-active .scada-modern-element {
      pointer-events: none;
    }
    .scada-modern-element[data-selected="true"] {
      outline: 2px solid #2090a0;
      outline-offset: 2px;
      box-shadow: 0 0 0 4px rgba(32,144,160,.20), 0 8px 22px rgba(15,42,48,.18);
    }
    .scada-modern-group {
      align-items: stretch;
      padding: 0;
      background: transparent !important;
      border: 1px dashed transparent !important;
    }
    .scada-modern-group[data-selected="true"] {
      border-color: rgba(32,144,160,.85) !important;
    }
    .scada-modern-group[data-group-context="true"] {
      outline: 2px solid #2090a0;
      outline-offset: 3px;
      border-color: rgba(32,144,160,.55) !important;
      box-shadow: 0 0 0 4px rgba(32,144,160,.16);
    }
    .scada-modern-child[data-selected="true"] {
      outline: 2px solid #f2c230;
      outline-offset: 2px;
      box-shadow: 0 0 0 4px rgba(242,194,48,.24), 0 6px 18px rgba(15,42,48,.16);
    }
    .scada-modern-element input {
      width: 100%;
      height: 100%;
      border: 0;
      outline: 0;
      background: transparent;
      color: inherit;
      font: inherit;
      pointer-events: none;
    }
    .scada-modern-badge {
      position: absolute;
      left: -2px;
      top: -24px;
      height: 20px;
      max-width: 240px;
      padding: 2px 7px;
      border-radius: 4px;
      background: #2090a0;
      color: #fff;
      font: 12px Segoe UI, Arial, sans-serif;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      display: none;
      pointer-events: none;
    }
    .scada-modern-element[data-selected="true"] > .scada-modern-badge {
      display: block;
    }
    .scada-modern-handle {
      position: absolute;
      width: 9px;
      height: 9px;
      border: 1px solid #ffffff;
      background: #2090a0;
      box-shadow: 0 1px 3px rgba(15,42,48,.25);
      display: none;
    }
    .scada-modern-element[data-selected="true"] > .scada-modern-handle {
      display: block;
    }
    .scada-modern-handle[data-handle="nw"] { left: -6px; top: -6px; cursor: nwse-resize; }
    .scada-modern-handle[data-handle="ne"] { right: -6px; top: -6px; cursor: grab; }
    .scada-modern-handle[data-handle="sw"] { left: -6px; bottom: -6px; cursor: nesw-resize; }
    .scada-modern-handle[data-handle="se"] { right: -6px; bottom: -6px; cursor: nwse-resize; }
    .scada-modern-handle[data-handle="n"] { left: 50%; top: -6px; transform: translateX(-50%); cursor: ns-resize; }
    .scada-modern-handle[data-handle="s"] { left: 50%; bottom: -6px; transform: translateX(-50%); cursor: ns-resize; }
    .scada-modern-handle[data-handle="e"] { right: -6px; top: 50%; transform: translateY(-50%); cursor: ew-resize; }
    .scada-modern-handle[data-handle="w"] { left: -6px; top: 50%; transform: translateY(-50%); cursor: ew-resize; }
    #scada-rotation-badge {
      position: fixed;
      display: none;
      padding: 2px 6px;
      border-radius: 4px;
      background: #0f2a30;
      color: #ffffff;
      font: 12px "Segoe UI", sans-serif;
      pointer-events: none;
      z-index: 9999;
      transform: translate(12px, -50%);
    }
    #scada-rotation-input {
      position: fixed;
      display: none;
      width: 64px;
      padding: 3px 6px;
      border: 1px solid #2090a0;
      border-radius: 4px;
      font: 12px "Segoe UI", sans-serif;
      z-index: 9999;
    }
    body.scada-placement-active,
    body.scada-placement-active * {
      cursor: crosshair !important;
    }
    #scada-placement-preview {
      position: absolute;
      left: 0;
      top: 0;
      width: 1px;
      height: 1px;
      z-index: 2147483644;
      overflow: visible;
      pointer-events: none;
    }
    #scada-placement-preview line {
      stroke: #2090a0;
      stroke-width: 2;
      stroke-dasharray: 6 4;
      vector-effect: non-scaling-stroke;
    }
    #scada-text-editor {
      position: fixed;
      z-index: 2147483647;
      min-width: 80px;
      padding: 4px 6px;
      border: 2px solid #2090a0;
      background: #ffffff;
      color: #0f2a30;
      box-shadow: 0 10px 28px rgba(15,42,48,.20);
      font: 14px Segoe UI, Arial, sans-serif;
      outline: 0;
    }
    #scada-scene-resize-handle {
      position: absolute;
      right: -1px;
      bottom: -1px;
      width: 18px;
      height: 18px;
      z-index: 2147483200;
      box-sizing: border-box;
      border-left: 1px solid rgba(15,42,48,.35);
      border-top: 1px solid rgba(15,42,48,.35);
      background:
        linear-gradient(135deg, transparent 0 45%, rgba(15,42,48,.18) 45% 55%, transparent 55%),
        linear-gradient(135deg, transparent 0 62%, rgba(15,42,48,.28) 62% 70%, transparent 70%),
        rgba(255,255,255,.92);
      cursor: nwse-resize;
      pointer-events: auto;
      touch-action: none;
    }
  `;
  document.head.appendChild(style);

  const marquee = document.createElement('div');
  marquee.id = 'scada-extract-marquee';
  document.body.appendChild(marquee);

  const menu = document.createElement('div');
  menu.id = 'scada-extract-menu';
  document.body.appendChild(menu);

  let modernElements = [];
  let selectedModernId = null;
  let sceneCanvasResize = null;
  const selectedModernIds = new Set();
  let placementKind = null;
  let placementShapeKind = null;
  let placementIsTwoPoint = false;
  let placementStart = null;
  let placementPreview = null;
  let sourceDrag = null;
  let modernDrag = null;
  let lastObjectContextTargetId = null;
  let activeTextEditor = null;

  document.querySelectorAll('button.layer[disabled], input.layer[disabled], select.layer[disabled], textarea.layer[disabled]')
    .forEach(el => {
      el.setAttribute('data-scada-was-disabled', 'true');
      el.disabled = false;
      el.setAttribute('aria-disabled', 'true');
    });

  function getId(el) {
    return el && el.getAttribute ? (el.getAttribute('data-id') || '') : '';
  }

  function selectableSelectorForId(id) {
    const escaped = CSS.escape(id || '');
    return `[data-id="${escaped}"]:not(.scada-modern-element)`;
  }

  function getSelectableElementById(id) {
    return id ? document.querySelector(selectableSelectorForId(id)) : null;
  }

  function getSelectableElements() {
    return Array.from(document.querySelectorAll(selectableSelector))
      .filter(el => getId(el) && !hidden.has(getId(el)));
  }

  function getInventoryElements() {
    return Array.from(document.querySelectorAll(inventorySelector))
      .filter(el => getId(el) && !hidden.has(getId(el)));
  }

  function rememberRemovedSourceElement(el, id) {
    if (!el || !id || !el.parentNode || removedNodes.has(id)) return;
    removedNodes.set(id, {
      node: el,
      parent: el.parentNode,
      nextSibling: el.nextSibling
    });
  }

  function removeSourceElement(el) {
    const id = getId(el);
    if (!id) return;
    hidden.add(id);
    selected.delete(id);
    el.removeAttribute('data-scada-selected');
    el.removeAttribute('data-scada-deleted');
    rememberRemovedSourceElement(el, id);
    if (el.parentNode) {
      el.remove();
    }
  }

  function removeSourceElements(ids) {
    const removeIds = new Set(Array.isArray(ids) ? ids : []);
    if (!removeIds.size) return;
    removeIds.forEach(id => {
      const normalizedId = `${id || ''}`.trim();
      if (!normalizedId) return;
      const el = getSelectableElementById(normalizedId);
      if (el) {
        removeSourceElement(el);
      } else {
        hidden.add(normalizedId);
        selected.delete(normalizedId);
      }
    });
    postInventory();
    postSelection();
  }

  function restoreSourceElement(id, shouldSelect = false) {
    const normalizedId = `${id || ''}`.trim();
    if (!normalizedId) return;
    const entry = removedNodes.get(normalizedId);
    if (entry && entry.node && entry.parent && entry.parent.isConnected) {
      if (entry.nextSibling && entry.nextSibling.parentNode === entry.parent) {
        entry.parent.insertBefore(entry.node, entry.nextSibling);
      } else {
        entry.parent.appendChild(entry.node);
      }
    }
    removedNodes.delete(normalizedId);

    const el = getSelectableElementById(normalizedId);
    hidden.delete(normalizedId);
    selected.delete(normalizedId);
    if (!el) return;
    el.style.display = '';
    el.removeAttribute('data-scada-selected');
    el.removeAttribute('data-scada-deleted');
    if (shouldSelect) {
      selected.add(normalizedId);
      el.setAttribute('data-scada-selected', 'true');
    }
  }

  function getRenderOrder(el) {
    return getSelectableElements().indexOf(el);
  }

  function getElementBounds(el) {
    const rect = el.getBoundingClientRect();
    const surface = getPageSurface();
    const surfaceRect = surface.getBoundingClientRect();
    return {
      x: rect.left - surfaceRect.left + surface.scrollLeft,
      y: rect.top - surfaceRect.top + surface.scrollTop,
      width: rect.width,
      height: rect.height
    };
  }

  function setSourceElementGeometry(el, geometry) {
    if (!el || !geometry) return;
    if (isSvgSourceShape(el)) {
      setSvgSourceElementGeometry(el, geometry);
      return;
    }
    el.style.position = window.getComputedStyle(el).position === 'static' ? 'absolute' : el.style.position;
    el.style.left = `${Math.max(0, Math.round(geometry.x))}px`;
    el.style.top = `${Math.max(0, Math.round(geometry.y))}px`;
    if (Number.isFinite(geometry.width) && geometry.width > 0) {
      el.style.width = `${Math.max(1, Math.round(geometry.width))}px`;
    }
    if (Number.isFinite(geometry.height) && geometry.height > 0) {
      el.style.height = `${Math.max(1, Math.round(geometry.height))}px`;
    }
    el.style.transform = '';
  }

  function isSvgSourceShape(el) {
    return !!(el && el.ownerSVGElement && el !== el.ownerSVGElement);
  }

  function setSvgSourceElementGeometry(el, geometry) {
    const surface = getPageSurface();
    const surfaceRect = surface.getBoundingClientRect();
    const svg = el.ownerSVGElement;
    const svgRect = svg.getBoundingClientRect();
    const viewBox = svg.viewBox && svg.viewBox.baseVal ? svg.viewBox.baseVal : null;
    const scaleX = viewBox && svgRect.width ? viewBox.width / svgRect.width : 1;
    const scaleY = viewBox && svgRect.height ? viewBox.height / svgRect.height : 1;
    const originX = svgRect.left - surfaceRect.left + surface.scrollLeft;
    const originY = svgRect.top - surfaceRect.top + surface.scrollTop;
    const x = Math.max(0, Math.round(((geometry.x || 0) - originX) * scaleX + (viewBox ? viewBox.x : 0)));
    const y = Math.max(0, Math.round(((geometry.y || 0) - originY) * scaleY + (viewBox ? viewBox.y : 0)));
    const width = Number.isFinite(geometry.width) && geometry.width > 0
      ? Math.max(1, Math.round(geometry.width * scaleX))
      : null;
    const height = Number.isFinite(geometry.height) && geometry.height > 0
      ? Math.max(1, Math.round(geometry.height * scaleY))
      : null;

    const tag = (el.tagName || '').toLowerCase();
    if (tag === 'rect' || tag === 'image' || tag === 'foreignobject' || el.hasAttribute('x')) {
      el.setAttribute('x', `${x}`);
      el.setAttribute('y', `${y}`);
      if (width !== null) el.setAttribute('width', `${width}`);
      if (height !== null) el.setAttribute('height', `${height}`);
      el.removeAttribute('transform');
      return;
    }

    try {
      const box = el.getBBox();
      el.setAttribute('transform', `translate(${x - Math.round(box.x)} ${y - Math.round(box.y)})`);
    } catch {
    }
  }

  function selectedSourceElements() {
    return getSelectableElements().filter(el => selected.has(getId(el)));
  }

  function applySourceElementBounds(bounds) {
    if (!Array.isArray(bounds)) return;
    bounds.forEach(item => {
      const id = item?.Id || item?.id;
      if (!id) return;
      const el = getSelectableElementById(id);
      if (!el) return;
      setSourceElementGeometry(el, {
        x: item.X ?? item.x ?? 0,
        y: item.Y ?? item.y ?? 0,
        width: item.Width ?? item.width ?? el.offsetWidth,
        height: item.Height ?? item.height ?? el.offsetHeight
      });
    });
    postInventory();
    postSelection();
  }

  function toElementMessage(el, options = {}) {
    const bounds = getElementBounds(el);
    const computed = window.getComputedStyle(el);
    const includeLegacyMarkup = options.includeLegacyMarkup === true;
    let computedStyleText = '';
    let legacyMarkup = '';
    if (includeLegacyMarkup) {
      const clone = el.cloneNode(true);
      clone.removeAttribute('data-scada-selected');
      computedStyleText = Array.from(computed)
        .filter(name => !['outline', 'outline-color', 'outline-style', 'outline-width', 'outline-offset', 'box-shadow', 'cursor'].includes(name))
        .map(name => `${name}: ${computed.getPropertyValue(name)};`)
        .join(' ');
      clone.removeAttribute('class');
      clone.setAttribute('style', `${computedStyleText} ${clone.getAttribute('style') || ''}`.trim());
      legacyMarkup = clone.outerHTML || '';
    }
    const rawMetadata = {
      tagName: el.tagName.toLowerCase(),
      computedStyleText,
      transform: computed.transform || '',
      opacity: computed.opacity || '',
      display: computed.display || '',
      position: computed.position || '',
      left: computed.left || '',
      top: computed.top || '',
      fill: computed.fill || '',
      stroke: computed.stroke || '',
      strokeWidth: computed.strokeWidth || '',
      zIndex: computed.zIndex || ''
    };
    return {
      id: getId(el),
      name: el.getAttribute('data-name') || getId(el),
      elementType: el.getAttribute('data-type') || el.tagName.toLowerCase(),
      text: getEditableText(el),
      isTextLike: isTextLikeElement(el),
      x: bounds.x,
      y: bounds.y,
      width: bounds.width,
      height: bounds.height,
      fontFamily: computed.fontFamily || '',
      fontSize: parseFloat(computed.fontSize || '0') || 0,
      foreground: computed.color || '',
      background: computed.backgroundColor || '',
      legacyMarkup,
      rawMetadataJson: includeLegacyMarkup ? JSON.stringify(rawMetadata) : '',
      renderOrder: getRenderOrder(el)
    };
  }

  function findSelectable(target) {
    return target && target.closest ? target.closest(selectableSelector) : null;
  }

  function getEditableText(el) {
    if (!el) return '';
    if ((el.tagName || '').toLowerCase() === 'button') return el.textContent || el.value || '';
    if ('value' in el && typeof el.value === 'string') return el.value;
    return el.textContent || '';
  }

  function setEditableText(el, text) {
    if (!el) return;
    if ((el.tagName || '').toLowerCase() === 'button') {
      el.textContent = text;
      return;
    }
    if ('value' in el && typeof el.value === 'string') {
      el.value = text;
      return;
    }
    el.textContent = text;
  }

  function isTextLikeElement(el) {
    const type = (el?.getAttribute?.('data-type') || '').toLowerCase();
    const tag = (el?.tagName || '').toLowerCase();
    return type.includes('text') ||
      tag === 'text' ||
      tag === 'span' ||
      tag === 'label' ||
      tag === 'button' ||
      tag === 'input' ||
      tag === 'textarea' ||
      tag === 'div';
  }

  function closeTextEditor(commit) {
    if (!activeTextEditor) return;
    const { editor, target, id, originalText } = activeTextEditor;
    const newText = editor.value;
    editor.remove();
    activeTextEditor = null;

    if (!commit) {
      setEditableText(target, originalText);
      return;
    }

    setEditableText(target, newText);
    window.chrome?.webview?.postMessage({ type: 'editLegacyText', id, text: newText });
    postInventory();
  }

  function beginLegacyTextEdit(target) {
    if (!target || !isTextLikeElement(target)) return false;
    closeTextEditor(false);
    const id = getId(target);
    if (!id) return false;

    const rect = target.getBoundingClientRect();
    const originalText = getEditableText(target);
    const editor = document.createElement('input');
    editor.id = 'scada-text-editor';
    editor.type = 'text';
    editor.value = originalText;
    editor.style.left = `${Math.max(0, rect.left)}px`;
    editor.style.top = `${Math.max(0, rect.top)}px`;
    editor.style.width = `${Math.max(90, rect.width)}px`;
    editor.style.height = `${Math.max(28, rect.height)}px`;
    document.body.appendChild(editor);
    activeTextEditor = { editor, target, id, originalText };
    editor.focus();
    editor.select();

    editor.addEventListener('keydown', event => {
      if (event.key === 'Enter') {
        closeTextEditor(true);
        event.preventDefault();
      }
      if (event.key === 'Escape') {
        closeTextEditor(false);
        event.preventDefault();
      }
    });
    editor.addEventListener('blur', () => closeTextEditor(true));
    return true;
  }

  function setSelected(el, value) {
    const id = getId(el);
    if (!id) return;
    if (value) {
      selected.add(id);
      el.setAttribute('data-scada-selected', 'true');
    } else {
      selected.delete(id);
      el.removeAttribute('data-scada-selected');
    }
  }

  function clearSelection() {
    getSelectableElements().forEach(el => el.removeAttribute('data-scada-selected'));
    selected.clear();
    postSelection();
  }

  function clearModernSelection(post = true) {
    selectedModernId = null;
    selectedModernIds.clear();
    document.querySelectorAll('.scada-modern-element').forEach(element => {
      element.dataset.selected = 'false';
    });
    if (post) {
      window.chrome?.webview?.postMessage({ type: 'clearObjectSelection' });
    }
  }

  function clearAllSelection(post = true) {
    getSelectableElements().forEach(el => el.removeAttribute('data-scada-selected'));
    selected.clear();
    clearModernSelection(false);
    postSelection();
    if (post) {
      window.chrome?.webview?.postMessage({ type: 'clearAllSelection' });
    }
  }

  function selectLegacyElements(ids) {
    const requested = new Set(Array.isArray(ids) ? ids : []);
    getSelectableElements().forEach(el => {
      setSelected(el, requested.has(getId(el)));
    });
    if (requested.size > 0) {
      clearModernSelection(false);
    }
    postSelection();
  }

  function postSelection() {
    const items = getSelectableElements()
      .filter(el => selected.has(getId(el)))
      .map(toElementMessage);
    window.chrome?.webview?.postMessage({ type: 'selection', items });
  }

  function getSelectedMessages() {
    return getSelectableElements()
      .filter(el => selected.has(getId(el)))
      .map(toElementMessage);
  }

  function getSelectedMessagesForStudio() {
    return getSelectableElements()
      .filter(el => selected.has(getId(el)))
      .map(el => toElementMessage(el, { includeLegacyMarkup: true }));
  }

  function postInventory() {
    const items = getInventoryElements().map(toElementMessage);
    window.chrome?.webview?.postMessage({ type: 'inventory', items });
  }

  function hideMenu() {
    menu.style.display = 'none';
  }

  function renderContextMenuCommands(commands) {
    menu.textContent = '';
    const createCommandButton = command => {
      const button = document.createElement('button');
      button.type = 'button';
      button.dataset.commandId = command.Id;
      button.textContent = command.Label || command.Id;
      if (command.IsEnabled === false || command.isEnabled === false) {
        button.disabled = true;
        button.setAttribute('aria-disabled', 'true');
        const reason = command.DisabledReason || command.disabledReason || '';
        if (reason) {
          button.title = reason;
        }
      }
      return button;
    };

    const createCommandNode = command => {
      const children = command.Children || command.children || [];
      if (Array.isArray(children) && children.length) {
        const wrapper = document.createElement('div');
        wrapper.className = 'submenu';
        const parent = document.createElement('button');
        parent.type = 'button';
        parent.textContent = command.Label || command.Id;
        parent.setAttribute('aria-haspopup', 'true');
        wrapper.appendChild(parent);
        const panel = document.createElement('div');
        panel.className = 'submenu-panel';
        children
          .filter(child => child && child.Id)
          .forEach(child => panel.appendChild(createCommandButton(child)));
        wrapper.appendChild(panel);
        wrapper.addEventListener('mouseenter', () => positionSubmenuPanel(wrapper));
        wrapper.addEventListener('focusin', () => positionSubmenuPanel(wrapper));
        return wrapper;
      }

      return createCommandButton(command);
    };

    (Array.isArray(commands) ? commands : [])
      .filter(command => command && command.Id)
      .forEach(command => menu.appendChild(createCommandNode(command)));
  }

  function getContextMenuBounds() {
    const margin = 8;
    return {
      left: margin,
      top: margin,
      right: Math.max(margin, window.innerWidth - margin),
      bottom: Math.max(margin, window.innerHeight - margin)
    };
  }

  function clampContextCoordinate(value, size, min, max) {
    if (!Number.isFinite(value)) {
      return min;
    }

    return Math.max(min, Math.min(value, max - size));
  }

  function positionSubmenuPanel(wrapper) {
    const panel = wrapper.querySelector(':scope > .submenu-panel');
    if (!panel) {
      return;
    }

    const bounds = getContextMenuBounds();
    delete wrapper.dataset.submenuX;
    panel.style.left = '';
    panel.style.right = '';
    panel.style.top = '-4px';
    panel.style.maxHeight = '';
    panel.style.overflowY = '';
    panel.style.visibility = 'hidden';
    panel.style.display = 'block';

    const wrapperRect = wrapper.getBoundingClientRect();
    const panelRect = panel.getBoundingClientRect();
    const opensRight = wrapperRect.right + panelRect.width <= bounds.right;
    const opensLeft = wrapperRect.left - panelRect.width >= bounds.left;
    if (!opensRight && opensLeft) {
      wrapper.dataset.submenuX = 'left';
      panel.style.left = 'auto';
      panel.style.right = 'calc(100% - 1px)';
    }

    const maxPanelHeight = Math.max(42, window.innerHeight - 16);
    const panelHeight = Math.min(panelRect.height, maxPanelHeight);
    const desiredTop = -4;
    const viewportTop = wrapperRect.top + desiredTop;
    const adjustedViewportTop = clampContextCoordinate(viewportTop, panelHeight, bounds.top, bounds.bottom);
    panel.style.top = `${adjustedViewportTop - wrapperRect.top}px`;
    if (panelRect.height > maxPanelHeight) {
      panel.style.maxHeight = `${maxPanelHeight}px`;
      panel.style.overflowY = 'auto';
    }

    panel.style.display = '';
    panel.style.visibility = '';
  }

  function showContextMenu(payload) {
    const commands = payload?.commands || payload?.Commands || [];
    renderContextMenuCommands(commands);
    if (!menu.children.length) {
      hideMenu();
      return;
    }
    const x = payload?.x ?? payload?.X ?? 0;
    const y = payload?.y ?? payload?.Y ?? 0;
    const bounds = getContextMenuBounds();
    menu.style.maxHeight = '';
    menu.style.overflowY = '';
    menu.style.visibility = 'hidden';
    menu.style.display = 'block';
    menu.style.left = `${bounds.left}px`;
    menu.style.top = `${bounds.top}px`;
    const menuRect = menu.getBoundingClientRect();
    const maxMenuHeight = Math.max(42, window.innerHeight - 16);
    const menuWidth = Math.max(180, menuRect.width || 180);
    const menuHeight = Math.min(Math.max(42, menuRect.height || 42), maxMenuHeight);
    if (menuRect.height > maxMenuHeight) {
      menu.style.maxHeight = `${maxMenuHeight}px`;
      menu.style.overflowY = 'auto';
    }
    const left = clampContextCoordinate(x, menuWidth, bounds.left, bounds.right);
    const top = clampContextCoordinate(y, menuHeight, bounds.top, bounds.bottom);
    menu.style.left = `${left}px`;
    menu.style.top = `${top}px`;
    menu.style.visibility = '';
  }

  function getPageSurface() {
    return document.querySelector('.page') || document.querySelector('#scada-root') || document.body;
  }

  function getSurfacePoint(event, surface = getPageSurface()) {
    const rect = surface.getBoundingClientRect();
    return {
      x: Math.round(event.clientX - rect.left + surface.scrollLeft),
      y: Math.round(event.clientY - rect.top + surface.scrollTop)
    };
  }

  function clearPlacementPreview() {
    placementPreview?.remove?.();
    placementPreview = null;
  }

  function clearPlacementState(postCancel = false) {
    placementKind = null;
    placementShapeKind = null;
    placementIsTwoPoint = false;
    placementStart = null;
    clearPlacementPreview();
    document.body.classList.remove('scada-placement-active');
    if (postCancel) {
      window.chrome?.webview?.postMessage({ type: 'cancelPlacement' });
    }
  }

  function updateTwoPointPlacementPreview(point) {
    if (!placementStart) {
      return;
    }
    const surface = getPageSurface();
    if (!placementPreview) {
      placementPreview = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      placementPreview.id = 'scada-placement-preview';
      const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
      line.setAttribute('x1', '0');
      line.setAttribute('y1', '0');
      line.setAttribute('x2', '0');
      line.setAttribute('y2', '0');
      placementPreview.appendChild(line);
      surface.appendChild(placementPreview);
    } else if (placementPreview.parentElement !== surface) {
      surface.appendChild(placementPreview);
    }

    const left = Math.min(placementStart.x, point.x);
    const top = Math.min(placementStart.y, point.y);
    const width = Math.max(1, Math.abs(point.x - placementStart.x));
    const height = Math.max(1, Math.abs(point.y - placementStart.y));
    const line = placementPreview.querySelector('line');
    placementPreview.style.left = `${left}px`;
    placementPreview.style.top = `${top}px`;
    placementPreview.style.width = `${width}px`;
    placementPreview.style.height = `${height}px`;
    placementPreview.setAttribute('viewBox', `0 0 ${width} ${height}`);
    line?.setAttribute('x1', `${placementStart.x - left}`);
    line?.setAttribute('y1', `${placementStart.y - top}`);
    line?.setAttribute('x2', `${point.x - left}`);
    line?.setAttribute('y2', `${point.y - top}`);
  }

  function ensureModernLayer() {
    const surface = getPageSurface();
    let layer = document.getElementById('scada-modern-layer');
    if (!layer) {
      layer = document.createElement('div');
      layer.id = 'scada-modern-layer';
      surface.appendChild(layer);
    }
    const position = window.getComputedStyle(surface).position;
    if (position === 'static') {
      surface.style.position = 'relative';
    }
    return layer;
  }

  function setSceneSurfaceSize(width, height) {
    const boundedWidth = Math.max(160, Math.round(width || 0));
    const boundedHeight = Math.max(120, Math.round(height || 0));
    const surface = getPageSurface();
    document.documentElement.style.setProperty('--page-w', `${boundedWidth}px`);
    document.documentElement.style.setProperty('--page-h', `${boundedHeight}px`);
    surface.style.setProperty('--page-w', `${boundedWidth}px`);
    surface.style.setProperty('--page-h', `${boundedHeight}px`);
    surface.style.width = `${boundedWidth}px`;
    surface.style.height = `${boundedHeight}px`;
    surface.style.minWidth = `${boundedWidth}px`;
    surface.style.minHeight = `${boundedHeight}px`;
    surface.style.overflow = 'hidden';

    const layer = document.getElementById('scada-modern-layer');
    if (layer && layer.parentElement === surface) {
      layer.style.width = `${boundedWidth}px`;
      layer.style.height = `${boundedHeight}px`;
    }

    return { width: boundedWidth, height: boundedHeight };
  }

  function ensureSceneResizeHandle() {
    const surface = getPageSurface();
    let handle = document.getElementById('scada-scene-resize-handle');
    if (!handle) {
      handle = document.createElement('div');
      handle.id = 'scada-scene-resize-handle';
      handle.setAttribute('aria-label', 'Redimensionner la workzone');
      surface.appendChild(handle);
    } else if (handle.parentElement !== surface) {
      surface.appendChild(handle);
    }

    const position = window.getComputedStyle(surface).position;
    if (position === 'static') {
      surface.style.position = 'relative';
    }

    return handle;
  }

  function cssText(value, fallback) {
    return value === undefined || value === null || value === '' ? fallback : value;
  }

  function shadowCss(preset) {
    if (preset === 'Soft') return '0 8px 18px rgba(15,42,48,.16)';
    if (preset === 'Raised') return '0 12px 26px rgba(15,42,48,.24)';
    if (preset === 'Inset') return 'inset 0 2px 8px rgba(15,42,48,.22)';
    return 'none';
  }

  function getModernElementById(id) {
    return modernElements.find(element => element.Id === id) || null;
  }

  function postModernGeometry(id, before, after) {
    window.chrome?.webview?.postMessage({
      type: 'updateSceneObjectGeometry',
      id,
      beforeX: Math.max(0, Math.round(before.x)),
      beforeY: Math.max(0, Math.round(before.y)),
      beforeWidth: Math.max(8, Math.round(before.width)),
      beforeHeight: Math.max(8, Math.round(before.height)),
      x: Math.max(0, Math.round(after.x)),
      y: Math.max(0, Math.round(after.y)),
      width: Math.max(8, Math.round(after.width)),
      height: Math.max(8, Math.round(after.height))
    });
  }

  function postModernRotation(id, rotation) {
    window.chrome?.webview?.postMessage({
      type: 'updateSceneObjectRotation',
      id,
      rotation
    });
  }

  function postModernGroupResize(id, before, after, children) {
    window.chrome?.webview?.postMessage({
      type: 'resizeSceneGroupWithChildren',
      id,
      beforeX: Math.max(0, Math.round(before.x)),
      beforeY: Math.max(0, Math.round(before.y)),
      beforeWidth: Math.max(8, Math.round(before.width)),
      beforeHeight: Math.max(8, Math.round(before.height)),
      x: Math.max(0, Math.round(after.x)),
      y: Math.max(0, Math.round(after.y)),
      width: Math.max(8, Math.round(after.width)),
      height: Math.max(8, Math.round(after.height)),
      children: children.map(child => ({
        id: child.id,
        beforeX: Math.round(child.geometry.x),
        beforeY: Math.round(child.geometry.y),
        beforeWidth: Math.max(1, Math.round(child.geometry.width)),
        beforeHeight: Math.max(1, Math.round(child.geometry.height)),
        x: Math.round(child.after.x),
        y: Math.round(child.after.y),
        width: Math.max(1, Math.round(child.after.width)),
        height: Math.max(1, Math.round(child.after.height))
      }))
    });
  }

  function postSelectionMove(targetKind, ids, deltaX, deltaY, items = null) {
    window.chrome?.webview?.postMessage({
      type: 'moveSelectionBy',
      targetKind,
      ids,
      items,
      deltaX: Math.round(deltaX),
      deltaY: Math.round(deltaY)
    });
  }

  function selectModernElementInDom(id) {
    selectedModernIds.clear();
    if (id) selectedModernIds.add(id);
    selectedModernId = id || null;
    document.querySelectorAll('.scada-modern-element').forEach(element => {
      element.dataset.selected = selectedModernIds.has(element.dataset.id) ? 'true' : 'false';
    });
    const selectedElement = selectedModernId
      ? document.querySelector(`.scada-modern-element[data-id="${CSS.escape(selectedModernId)}"]`)
      : null;
    selectedElement?.focus?.({ preventScroll: true });
  }

  function syncModernSelectionInDom() {
    document.querySelectorAll('.scada-modern-element').forEach(element => {
      element.dataset.selected = selectedModernIds.has(element.dataset.id) ? 'true' : 'false';
    });
    const selectedElement = selectedModernId
      ? document.querySelector(`.scada-modern-element[data-id="${CSS.escape(selectedModernId)}"]`)
      : null;
    selectedElement?.focus?.({ preventScroll: true });
  }

  function addModernElementToSelection(id) {
    if (!id) return;
    selectedModernIds.add(id);
    selectedModernId = id;
    syncModernSelectionInDom();
  }

  function toggleModernElementInSelection(id) {
    if (!id) return;
    if (selectedModernIds.has(id)) {
      selectedModernIds.delete(id);
      selectedModernId = selectedModernIds.size ? Array.from(selectedModernIds).at(-1) : null;
    } else {
      selectedModernIds.add(id);
      selectedModernId = id;
    }
    syncModernSelectionInDom();
  }

  function readWrapperGeometry(wrapper) {
    return {
      x: parseFloat(wrapper.style.left) || 0,
      y: parseFloat(wrapper.style.top) || 0,
      width: parseFloat(wrapper.style.width) || wrapper.offsetWidth,
      height: parseFloat(wrapper.style.height) || wrapper.offsetHeight
    };
  }

  function setWrapperGeometry(wrapper, geometry) {
    wrapper.style.left = `${Math.max(0, geometry.x)}px`;
    wrapper.style.top = `${Math.max(0, geometry.y)}px`;
    wrapper.style.width = `${Math.max(8, geometry.width)}px`;
    wrapper.style.height = `${Math.max(8, geometry.height)}px`;
  }

  function getWrapperRotation(wrapper) {
    const match = /rotate\(([-\d.]+)deg\)/.exec(wrapper.style.transform || '');
    return match ? parseFloat(match[1]) || 0 : 0;
  }

  function clampNearAxis(startPos, startSize, delta) {
    const clampedDelta = Math.max(-startPos, Math.min(delta, startSize - 8));
    return { pos: startPos + clampedDelta, size: startSize - clampedDelta };
  }

  function getSceneMoveWrapper(wrapper) {
    if (!wrapper?.classList?.contains('scada-modern-child')) {
      return wrapper;
    }
    return wrapper.parentElement?.closest?.('.scada-modern-group') || wrapper;
  }

  function svgDashArray(style) {
    const borderStyle = String(style.BorderStyle || 'Solid').toLowerCase();
    if (borderStyle === 'dashed') return '8 5';
    if (borderStyle === 'dotted') return '2 4';
    return '';
  }

  function renderShapeElement(element, style) {
    const shapeKind = String(element.ShapeKind || element.shapeKind || 'Rectangle').toLowerCase();
    const data = element.Data || {};
    const strokeWidth = Math.max(0, Number(style.BorderWidth ?? 2));
    const halfStroke = Math.max(1, strokeWidth / 2);
    const stroke = cssText(style.BorderColor, '#2090a0');
    const fill = shapeKind === 'line' || shapeKind === 'arrow'
      ? 'transparent'
      : cssText(style.Background, '#dff3e7');
    const dashArray = svgDashArray(style);
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('viewBox', `0 0 ${Math.max(1, element.Width)} ${Math.max(1, element.Height)}`);
    svg.setAttribute('width', '100%');
    svg.setAttribute('height', '100%');
    svg.setAttribute('preserveAspectRatio', 'none');
    svg.style.display = 'block';
    svg.style.pointerEvents = 'none';

    const setStroke = node => {
      node.setAttribute('stroke', stroke);
      node.setAttribute('stroke-width', `${strokeWidth}`);
      if (dashArray) {
        node.setAttribute('stroke-dasharray', dashArray);
      }
      node.setAttribute('vector-effect', 'non-scaling-stroke');
    };

    const clampPercent = value => {
      const parsed = Number(value);
      if (!Number.isFinite(parsed)) return 65;
      return Math.max(0, Math.min(100, parsed));
    };

    if (shapeKind === 'indicatorlamp') {
      const gradient = document.createElementNS(svg.namespaceURI, 'radialGradient');
      gradient.setAttribute('id', `lamp-gradient-${element.Id}`);
      gradient.setAttribute('cx', '35%');
      gradient.setAttribute('cy', '28%');
      gradient.setAttribute('r', '70%');
      const highlight = document.createElementNS(svg.namespaceURI, 'stop');
      highlight.setAttribute('offset', '0%');
      highlight.setAttribute('stop-color', '#ffffff');
      highlight.setAttribute('stop-opacity', '0.85');
      const color = document.createElementNS(svg.namespaceURI, 'stop');
      color.setAttribute('offset', '42%');
      color.setAttribute('stop-color', fill);
      const shade = document.createElementNS(svg.namespaceURI, 'stop');
      shade.setAttribute('offset', '100%');
      shade.setAttribute('stop-color', stroke);
      gradient.appendChild(highlight);
      gradient.appendChild(color);
      gradient.appendChild(shade);
      const defs = document.createElementNS(svg.namespaceURI, 'defs');
      defs.appendChild(gradient);
      svg.appendChild(defs);

      const lamp = document.createElementNS(svg.namespaceURI, 'circle');
      lamp.setAttribute('cx', `${element.Width / 2}`);
      lamp.setAttribute('cy', `${element.Height / 2}`);
      lamp.setAttribute('r', `${Math.max(0, Math.min(element.Width, element.Height) / 2 - halfStroke)}`);
      lamp.setAttribute('fill', `url(#lamp-gradient-${element.Id})`);
      setStroke(lamp);
      svg.appendChild(lamp);
      return svg;
    }

    if (shapeKind === 'horizontalbar' || shapeKind === 'verticalbar') {
      const percent = clampPercent(data.Value ?? data.value);
      const track = document.createElementNS(svg.namespaceURI, 'rect');
      track.setAttribute('x', `${halfStroke}`);
      track.setAttribute('y', `${halfStroke}`);
      track.setAttribute('width', `${Math.max(0, element.Width - strokeWidth)}`);
      track.setAttribute('height', `${Math.max(0, element.Height - strokeWidth)}`);
      track.setAttribute('rx', `${Math.min(8, Math.min(element.Width, element.Height) * 0.2)}`);
      track.setAttribute('fill', '#f7fbf5');
      setStroke(track);
      svg.appendChild(track);

      const fillRect = document.createElementNS(svg.namespaceURI, 'rect');
      const innerX = halfStroke + 3;
      const innerY = halfStroke + 3;
      const innerWidth = Math.max(0, element.Width - strokeWidth - 6);
      const innerHeight = Math.max(0, element.Height - strokeWidth - 6);
      fillRect.setAttribute('fill', fill);
      fillRect.setAttribute('rx', `${Math.min(5, Math.min(innerWidth, innerHeight) * 0.18)}`);
      if (shapeKind === 'horizontalbar') {
        fillRect.setAttribute('x', `${innerX}`);
        fillRect.setAttribute('y', `${innerY}`);
        fillRect.setAttribute('width', `${innerWidth * (percent / 100)}`);
        fillRect.setAttribute('height', `${innerHeight}`);
      } else {
        const fillHeight = innerHeight * (percent / 100);
        fillRect.setAttribute('x', `${innerX}`);
        fillRect.setAttribute('y', `${innerY + innerHeight - fillHeight}`);
        fillRect.setAttribute('width', `${innerWidth}`);
        fillRect.setAttribute('height', `${fillHeight}`);
      }
      svg.appendChild(fillRect);
      return svg;
    }

    if (shapeKind === 'tank') {
      const percent = clampPercent(data.Value ?? data.value);
      const bodyTop = halfStroke + 8;
      const bodyHeight = Math.max(0, element.Height - strokeWidth - 16);
      const bodyWidth = Math.max(0, element.Width - strokeWidth);
      const innerX = halfStroke + 6;
      const innerY = bodyTop + 6;
      const innerWidth = Math.max(0, bodyWidth - 12);
      const innerHeight = Math.max(0, bodyHeight - 12);
      const fillHeight = innerHeight * (percent / 100);

      const body = document.createElementNS(svg.namespaceURI, 'rect');
      body.setAttribute('x', `${halfStroke}`);
      body.setAttribute('y', `${bodyTop}`);
      body.setAttribute('width', `${bodyWidth}`);
      body.setAttribute('height', `${bodyHeight}`);
      body.setAttribute('rx', `${Math.min(10, element.Width * 0.12)}`);
      body.setAttribute('fill', '#f7fbf5');
      setStroke(body);
      svg.appendChild(body);

      const level = document.createElementNS(svg.namespaceURI, 'rect');
      level.setAttribute('x', `${innerX}`);
      level.setAttribute('y', `${innerY + innerHeight - fillHeight}`);
      level.setAttribute('width', `${innerWidth}`);
      level.setAttribute('height', `${fillHeight}`);
      level.setAttribute('rx', `${Math.min(5, element.Width * 0.06)}`);
      level.setAttribute('fill', fill);
      svg.appendChild(level);

      const top = document.createElementNS(svg.namespaceURI, 'ellipse');
      top.setAttribute('cx', `${element.Width / 2}`);
      top.setAttribute('cy', `${bodyTop}`);
      top.setAttribute('rx', `${Math.max(0, bodyWidth / 2)}`);
      top.setAttribute('ry', `${Math.max(3, Math.min(12, element.Height * 0.08))}`);
      top.setAttribute('fill', '#f7fbf5');
      setStroke(top);
      svg.appendChild(top);
      return svg;
    }

    if (shapeKind === 'pipehorizontal' || shapeKind === 'pipevertical') {
      const pipe = document.createElementNS(svg.namespaceURI, 'rect');
      const isVertical = shapeKind === 'pipevertical';
      pipe.setAttribute('x', `${isVertical ? element.Width * 0.25 : halfStroke}`);
      pipe.setAttribute('y', `${isVertical ? halfStroke : element.Height * 0.25}`);
      pipe.setAttribute('width', `${isVertical ? element.Width * 0.5 : Math.max(0, element.Width - strokeWidth)}`);
      pipe.setAttribute('height', `${isVertical ? Math.max(0, element.Height - strokeWidth) : element.Height * 0.5}`);
      pipe.setAttribute('rx', `${Math.min(8, Math.min(element.Width, element.Height) * 0.2)}`);
      pipe.setAttribute('fill', fill);
      setStroke(pipe);
      svg.appendChild(pipe);
      return svg;
    }

    if (shapeKind === 'valve') {
      const left = document.createElementNS(svg.namespaceURI, 'polygon');
      left.setAttribute('points', `${halfStroke},${halfStroke} ${element.Width / 2},${element.Height / 2} ${halfStroke},${element.Height - halfStroke}`);
      left.setAttribute('fill', fill);
      setStroke(left);
      svg.appendChild(left);

      const right = document.createElementNS(svg.namespaceURI, 'polygon');
      right.setAttribute('points', `${element.Width - halfStroke},${halfStroke} ${element.Width / 2},${element.Height / 2} ${element.Width - halfStroke},${element.Height - halfStroke}`);
      right.setAttribute('fill', fill);
      setStroke(right);
      svg.appendChild(right);

      const stem = document.createElementNS(svg.namespaceURI, 'line');
      stem.setAttribute('x1', `${element.Width / 2}`);
      stem.setAttribute('y1', `${halfStroke}`);
      stem.setAttribute('x2', `${element.Width / 2}`);
      stem.setAttribute('y2', `${element.Height / 2}`);
      setStroke(stem);
      svg.appendChild(stem);
      return svg;
    }

    if (shapeKind === 'pump') {
      const radius = Math.max(0, Math.min(element.Width, element.Height) * 0.38 - halfStroke);
      const cx = element.Width * 0.42;
      const cy = element.Height / 2;
      const casing = document.createElementNS(svg.namespaceURI, 'circle');
      casing.setAttribute('cx', `${cx}`);
      casing.setAttribute('cy', `${cy}`);
      casing.setAttribute('r', `${radius}`);
      casing.setAttribute('fill', fill);
      setStroke(casing);
      svg.appendChild(casing);

      const outlet = document.createElementNS(svg.namespaceURI, 'rect');
      outlet.setAttribute('x', `${cx + radius * 0.65}`);
      outlet.setAttribute('y', `${cy - Math.max(5, radius * 0.22)}`);
      outlet.setAttribute('width', `${Math.max(8, element.Width - (cx + radius * 0.65) - halfStroke)}`);
      outlet.setAttribute('height', `${Math.max(10, radius * 0.44)}`);
      outlet.setAttribute('fill', fill);
      setStroke(outlet);
      svg.appendChild(outlet);

      const impeller = document.createElementNS(svg.namespaceURI, 'path');
      impeller.setAttribute('d', `M ${cx - radius * 0.35} ${cy - radius * 0.25} L ${cx + radius * 0.42} ${cy} L ${cx - radius * 0.35} ${cy + radius * 0.25} Z`);
      impeller.setAttribute('fill', stroke);
      svg.appendChild(impeller);
      return svg;
    }

    if (shapeKind === 'motor') {
      const body = document.createElementNS(svg.namespaceURI, 'rect');
      body.setAttribute('x', `${halfStroke + element.Width * 0.08}`);
      body.setAttribute('y', `${halfStroke + element.Height * 0.18}`);
      body.setAttribute('width', `${Math.max(0, element.Width * 0.7 - strokeWidth)}`);
      body.setAttribute('height', `${Math.max(0, element.Height * 0.58 - strokeWidth)}`);
      body.setAttribute('rx', `${Math.min(10, element.Height * 0.16)}`);
      body.setAttribute('fill', fill);
      setStroke(body);
      svg.appendChild(body);

      const shaft = document.createElementNS(svg.namespaceURI, 'rect');
      shaft.setAttribute('x', `${element.Width * 0.78}`);
      shaft.setAttribute('y', `${element.Height * 0.42}`);
      shaft.setAttribute('width', `${Math.max(6, element.Width * 0.16 - halfStroke)}`);
      shaft.setAttribute('height', `${Math.max(6, element.Height * 0.16)}`);
      shaft.setAttribute('fill', '#f7fbf5');
      setStroke(shaft);
      svg.appendChild(shaft);

      const label = document.createElementNS(svg.namespaceURI, 'text');
      label.setAttribute('x', `${element.Width * 0.42}`);
      label.setAttribute('y', `${element.Height * 0.56}`);
      label.setAttribute('text-anchor', 'middle');
      label.setAttribute('font-size', `${Math.max(12, Math.min(element.Width, element.Height) * 0.24)}`);
      label.setAttribute('font-family', 'Segoe UI, Arial, sans-serif');
      label.setAttribute('font-weight', '700');
      label.setAttribute('fill', stroke);
      label.textContent = 'M';
      svg.appendChild(label);
      return svg;
    }

    if (shapeKind === 'fan') {
      const cx = element.Width / 2;
      const cy = element.Height / 2;
      const radius = Math.max(0, Math.min(element.Width, element.Height) * 0.44 - halfStroke);
      const housing = document.createElementNS(svg.namespaceURI, 'circle');
      housing.setAttribute('cx', `${cx}`);
      housing.setAttribute('cy', `${cy}`);
      housing.setAttribute('r', `${radius}`);
      housing.setAttribute('fill', '#f7fbf5');
      setStroke(housing);
      svg.appendChild(housing);

      [[0, -1], [0.86, 0.5], [-0.86, 0.5]].forEach(([dx, dy]) => {
        const blade = document.createElementNS(svg.namespaceURI, 'path');
        blade.setAttribute('d', `M ${cx} ${cy} Q ${cx + dx * radius * 0.48} ${cy + dy * radius * 0.48} ${cx + dx * radius * 0.18 - dy * radius * 0.28} ${cy + dy * radius * 0.18 + dx * radius * 0.28} Q ${cx + dx * radius * 0.72} ${cy + dy * radius * 0.72} ${cx + dx * radius * 0.86 - dy * radius * 0.14} ${cy + dy * radius * 0.86 + dx * radius * 0.14} Q ${cx + dx * radius * 0.45} ${cy + dy * radius * 0.45} ${cx} ${cy} Z`);
        blade.setAttribute('fill', fill);
        setStroke(blade);
        svg.appendChild(blade);
      });

      const hub = document.createElementNS(svg.namespaceURI, 'circle');
      hub.setAttribute('cx', `${cx}`);
      hub.setAttribute('cy', `${cy}`);
      hub.setAttribute('r', `${Math.max(4, radius * 0.16)}`);
      hub.setAttribute('fill', stroke);
      svg.appendChild(hub);
      return svg;
    }

    if (shapeKind === 'conveyor') {
      const belt = document.createElementNS(svg.namespaceURI, 'rect');
      belt.setAttribute('x', `${halfStroke}`);
      belt.setAttribute('y', `${element.Height * 0.25}`);
      belt.setAttribute('width', `${Math.max(0, element.Width - strokeWidth)}`);
      belt.setAttribute('height', `${element.Height * 0.42}`);
      belt.setAttribute('rx', `${Math.min(8, element.Height * 0.18)}`);
      belt.setAttribute('fill', fill);
      setStroke(belt);
      svg.appendChild(belt);

      [0.18, 0.5, 0.82].forEach(position => {
        const roller = document.createElementNS(svg.namespaceURI, 'circle');
        roller.setAttribute('cx', `${element.Width * position}`);
        roller.setAttribute('cy', `${element.Height * 0.72}`);
        roller.setAttribute('r', `${Math.max(4, element.Height * 0.1)}`);
        roller.setAttribute('fill', '#f7fbf5');
        setStroke(roller);
        svg.appendChild(roller);
      });

      const topLine = document.createElementNS(svg.namespaceURI, 'line');
      topLine.setAttribute('x1', `${halfStroke + 6}`);
      topLine.setAttribute('y1', `${element.Height * 0.36}`);
      topLine.setAttribute('x2', `${element.Width - halfStroke - 6}`);
      topLine.setAttribute('y2', `${element.Height * 0.36}`);
      setStroke(topLine);
      svg.appendChild(topLine);
      return svg;
    }

    if (shapeKind === 'gauge') {
      const percent = clampPercent(data.Value ?? data.value);
      const cx = element.Width / 2;
      const cy = element.Height * 0.58;
      const radius = Math.max(0, Math.min(element.Width, element.Height) * 0.42 - halfStroke);
      const face = document.createElementNS(svg.namespaceURI, 'circle');
      face.setAttribute('cx', `${cx}`);
      face.setAttribute('cy', `${cy}`);
      face.setAttribute('r', `${radius}`);
      face.setAttribute('fill', '#f7fbf5');
      setStroke(face);
      svg.appendChild(face);

      const angle = (-140 + (percent * 280 / 100)) * Math.PI / 180;
      const needle = document.createElementNS(svg.namespaceURI, 'line');
      needle.setAttribute('x1', `${cx}`);
      needle.setAttribute('y1', `${cy}`);
      needle.setAttribute('x2', `${cx + Math.cos(angle) * radius * 0.72}`);
      needle.setAttribute('y2', `${cy + Math.sin(angle) * radius * 0.72}`);
      needle.setAttribute('stroke', stroke);
      needle.setAttribute('stroke-width', `${Math.max(2, strokeWidth + 1)}`);
      needle.setAttribute('vector-effect', 'non-scaling-stroke');
      svg.appendChild(needle);

      const hub = document.createElementNS(svg.namespaceURI, 'circle');
      hub.setAttribute('cx', `${cx}`);
      hub.setAttribute('cy', `${cy}`);
      hub.setAttribute('r', `${Math.max(3, radius * 0.08)}`);
      hub.setAttribute('fill', fill);
      setStroke(hub);
      svg.appendChild(hub);
      return svg;
    }

    if (shapeKind === 'switch') {
      const y = element.Height * 0.55;
      const leftX = element.Width * 0.22;
      const rightX = element.Width * 0.78;
      const bladeEndX = element.Width * 0.64;
      const bladeEndY = element.Height * 0.28;
      const bus = document.createElementNS(svg.namespaceURI, 'line');
      bus.setAttribute('x1', `${halfStroke}`);
      bus.setAttribute('y1', `${y}`);
      bus.setAttribute('x2', `${element.Width - halfStroke}`);
      bus.setAttribute('y2', `${y}`);
      setStroke(bus);
      svg.appendChild(bus);

      [leftX, rightX].forEach(x => {
        const terminal = document.createElementNS(svg.namespaceURI, 'circle');
        terminal.setAttribute('cx', `${x}`);
        terminal.setAttribute('cy', `${y}`);
        terminal.setAttribute('r', `${Math.max(4, Math.min(element.Width, element.Height) * 0.08)}`);
        terminal.setAttribute('fill', '#f7fbf5');
        setStroke(terminal);
        svg.appendChild(terminal);
      });

      const blade = document.createElementNS(svg.namespaceURI, 'line');
      blade.setAttribute('x1', `${leftX}`);
      blade.setAttribute('y1', `${y}`);
      blade.setAttribute('x2', `${bladeEndX}`);
      blade.setAttribute('y2', `${bladeEndY}`);
      setStroke(blade);
      svg.appendChild(blade);
      return svg;
    }

    if (shapeKind === 'breaker') {
      const body = document.createElementNS(svg.namespaceURI, 'rect');
      body.setAttribute('x', `${halfStroke + element.Width * 0.14}`);
      body.setAttribute('y', `${halfStroke + element.Height * 0.12}`);
      body.setAttribute('width', `${Math.max(0, element.Width * 0.72 - strokeWidth)}`);
      body.setAttribute('height', `${Math.max(0, element.Height * 0.76 - strokeWidth)}`);
      body.setAttribute('rx', `${Math.min(8, element.Height * 0.12)}`);
      body.setAttribute('fill', '#f7fbf5');
      setStroke(body);
      svg.appendChild(body);

      const lever = document.createElementNS(svg.namespaceURI, 'line');
      lever.setAttribute('x1', `${element.Width * 0.37}`);
      lever.setAttribute('y1', `${element.Height * 0.68}`);
      lever.setAttribute('x2', `${element.Width * 0.63}`);
      lever.setAttribute('y2', `${element.Height * 0.34}`);
      lever.setAttribute('stroke', stroke);
      lever.setAttribute('stroke-width', `${Math.max(2, strokeWidth + 1)}`);
      lever.setAttribute('vector-effect', 'non-scaling-stroke');
      svg.appendChild(lever);

      const label = document.createElementNS(svg.namespaceURI, 'text');
      label.setAttribute('x', `${element.Width * 0.5}`);
      label.setAttribute('y', `${element.Height * 0.48}`);
      label.setAttribute('text-anchor', 'middle');
      label.setAttribute('font-size', `${Math.max(10, Math.min(element.Width, element.Height) * 0.18)}`);
      label.setAttribute('font-family', 'Segoe UI, Arial, sans-serif');
      label.setAttribute('font-weight', '700');
      label.setAttribute('fill', stroke);
      label.textContent = 'CB';
      svg.appendChild(label);
      return svg;
    }

    if (shapeKind === 'transformer') {
      const coreLeft = document.createElementNS(svg.namespaceURI, 'line');
      coreLeft.setAttribute('x1', `${element.Width * 0.47}`);
      coreLeft.setAttribute('y1', `${element.Height * 0.2}`);
      coreLeft.setAttribute('x2', `${element.Width * 0.47}`);
      coreLeft.setAttribute('y2', `${element.Height * 0.8}`);
      setStroke(coreLeft);
      svg.appendChild(coreLeft);

      const coreRight = document.createElementNS(svg.namespaceURI, 'line');
      coreRight.setAttribute('x1', `${element.Width * 0.53}`);
      coreRight.setAttribute('y1', `${element.Height * 0.2}`);
      coreRight.setAttribute('x2', `${element.Width * 0.53}`);
      coreRight.setAttribute('y2', `${element.Height * 0.8}`);
      setStroke(coreRight);
      svg.appendChild(coreRight);

      [0.28, 0.72].forEach(cx => {
        [0.34, 0.5, 0.66].forEach(cy => {
          const coil = document.createElementNS(svg.namespaceURI, 'ellipse');
          coil.setAttribute('cx', `${element.Width * cx}`);
          coil.setAttribute('cy', `${element.Height * cy}`);
          coil.setAttribute('rx', `${Math.max(5, element.Width * 0.12)}`);
          coil.setAttribute('ry', `${Math.max(5, element.Height * 0.11)}`);
          coil.setAttribute('fill', 'none');
          setStroke(coil);
          svg.appendChild(coil);
        });
      });
      return svg;
    }

    if (shapeKind === 'alarmbeacon') {
      const base = document.createElementNS(svg.namespaceURI, 'rect');
      base.setAttribute('x', `${element.Width * 0.22}`);
      base.setAttribute('y', `${element.Height * 0.72}`);
      base.setAttribute('width', `${element.Width * 0.56}`);
      base.setAttribute('height', `${element.Height * 0.14}`);
      base.setAttribute('rx', `${Math.min(6, element.Height * 0.05)}`);
      base.setAttribute('fill', '#f7fbf5');
      setStroke(base);
      svg.appendChild(base);

      const dome = document.createElementNS(svg.namespaceURI, 'path');
      dome.setAttribute('d', `M ${element.Width * 0.24} ${element.Height * 0.72} Q ${element.Width * 0.5} ${element.Height * 0.12} ${element.Width * 0.76} ${element.Height * 0.72} Z`);
      dome.setAttribute('fill', fill);
      setStroke(dome);
      svg.appendChild(dome);

      [[0.5, 0.04, 0.5, 0.16], [0.16, 0.32, 0.28, 0.42], [0.84, 0.32, 0.72, 0.42]].forEach(([x1, y1, x2, y2]) => {
        const ray = document.createElementNS(svg.namespaceURI, 'line');
        ray.setAttribute('x1', `${element.Width * x1}`);
        ray.setAttribute('y1', `${element.Height * y1}`);
        ray.setAttribute('x2', `${element.Width * x2}`);
        ray.setAttribute('y2', `${element.Height * y2}`);
        ray.setAttribute('stroke', stroke);
        ray.setAttribute('stroke-width', `${Math.max(2, strokeWidth + 1)}`);
        ray.setAttribute('vector-effect', 'non-scaling-stroke');
        svg.appendChild(ray);
      });
      return svg;
    }

    if (shapeKind === 'circle') {
      const circle = document.createElementNS(svg.namespaceURI, 'circle');
      circle.setAttribute('cx', `${element.Width / 2}`);
      circle.setAttribute('cy', `${element.Height / 2}`);
      circle.setAttribute('r', `${Math.max(0, Math.min(element.Width, element.Height) / 2 - halfStroke)}`);
      circle.setAttribute('fill', fill);
      setStroke(circle);
      svg.appendChild(circle);
      return svg;
    }

    if (shapeKind === 'ellipse') {
      const ellipse = document.createElementNS(svg.namespaceURI, 'ellipse');
      ellipse.setAttribute('cx', `${element.Width / 2}`);
      ellipse.setAttribute('cy', `${element.Height / 2}`);
      ellipse.setAttribute('rx', `${Math.max(0, (element.Width / 2) - halfStroke)}`);
      ellipse.setAttribute('ry', `${Math.max(0, (element.Height / 2) - halfStroke)}`);
      ellipse.setAttribute('fill', fill);
      setStroke(ellipse);
      svg.appendChild(ellipse);
      return svg;
    }

    if (shapeKind === 'triangle') {
      const triangle = document.createElementNS(svg.namespaceURI, 'polygon');
      triangle.setAttribute('points', `${element.Width / 2},${halfStroke} ${Math.max(halfStroke, element.Width - halfStroke)},${Math.max(halfStroke, element.Height - halfStroke)} ${halfStroke},${Math.max(halfStroke, element.Height - halfStroke)}`);
      triangle.setAttribute('fill', fill);
      setStroke(triangle);
      svg.appendChild(triangle);
      return svg;
    }

    if (shapeKind === 'star') {
      const cx = element.Width / 2;
      const cy = element.Height / 2;
      const outer = Math.max(0, Math.min(element.Width, element.Height) / 2 - halfStroke);
      const inner = outer * 0.45;
      const points = Array.from({ length: 10 }, (_, index) => {
        const radius = index % 2 === 0 ? outer : inner;
        const angle = (-90 + index * 36) * Math.PI / 180;
        return `${cx + Math.cos(angle) * radius},${cy + Math.sin(angle) * radius}`;
      }).join(' ');
      const star = document.createElementNS(svg.namespaceURI, 'polygon');
      star.setAttribute('points', points);
      star.setAttribute('fill', fill);
      setStroke(star);
      svg.appendChild(star);
      return svg;
    }

    if (shapeKind === 'line' || shapeKind === 'arrow') {
      if (shapeKind === 'arrow') {
        const marker = document.createElementNS(svg.namespaceURI, 'marker');
        marker.setAttribute('id', `arrow-${element.Id}`);
        marker.setAttribute('viewBox', '0 0 10 10');
        marker.setAttribute('refX', '10');
        marker.setAttribute('refY', '5');
        marker.setAttribute('markerWidth', '7');
        marker.setAttribute('markerHeight', '7');
        marker.setAttribute('orient', 'auto-start-reverse');
        const arrowHead = document.createElementNS(svg.namespaceURI, 'path');
        arrowHead.setAttribute('d', 'M 0 0 L 10 5 L 0 10 z');
        arrowHead.setAttribute('fill', stroke);
        marker.appendChild(arrowHead);
        svg.appendChild(marker);
      }

      const line = document.createElementNS(svg.namespaceURI, 'line');
      const startX = Number(data.ShapeStartX ?? data.shapeStartX);
      const startY = Number(data.ShapeStartY ?? data.shapeStartY);
      const endX = Number(data.ShapeEndX ?? data.shapeEndX);
      const endY = Number(data.ShapeEndY ?? data.shapeEndY);
      line.setAttribute('x1', `${Number.isFinite(startX) ? startX : halfStroke}`);
      line.setAttribute('y1', `${Number.isFinite(startY) ? startY : element.Height / 2}`);
      line.setAttribute('x2', `${Number.isFinite(endX) ? endX : Math.max(halfStroke, element.Width - halfStroke - (shapeKind === 'arrow' ? 7 : 0))}`);
      line.setAttribute('y2', `${Number.isFinite(endY) ? endY : element.Height / 2}`);
      if (shapeKind === 'arrow') {
        line.setAttribute('marker-end', `url(#arrow-${element.Id})`);
      }
      setStroke(line);
      svg.appendChild(line);
      return svg;
    }

    const rect = document.createElementNS(svg.namespaceURI, 'rect');
    rect.setAttribute('x', `${halfStroke}`);
    rect.setAttribute('y', `${halfStroke}`);
    rect.setAttribute('width', `${Math.max(0, element.Width - strokeWidth)}`);
    rect.setAttribute('height', `${Math.max(0, element.Height - strokeWidth)}`);
    if (shapeKind === 'roundedrectangle') {
      rect.setAttribute('rx', `${Math.min(element.Width, element.Height) * 0.12}`);
      rect.setAttribute('ry', `${Math.min(element.Width, element.Height) * 0.12}`);
    }
    rect.setAttribute('fill', fill);
    setStroke(rect);
    svg.appendChild(rect);
    return svg;
  }

  function renderModernElements(elements) {
    modernElements = Array.isArray(elements) ? elements : [];
    const layer = ensureModernLayer();
    layer.innerHTML = '';
    const renderElement = (element, parentWrapper = null) => {
      const style = element.Style || {};
      const data = element.Data || {};
      const buttonBehavior = element.ButtonBehavior || {};
      const buttonKind = String(element.ButtonKind || element.buttonKind || 'Command');
      const wrapper = document.createElement('div');
      const isGroup = element.Kind === 'Group';
      wrapper.className = `scada-modern-element${isGroup ? ' scada-modern-group' : ''}${parentWrapper ? ' scada-modern-child' : ''}`;
      wrapper.tabIndex = 0;
      wrapper.dataset.id = element.Id;
      wrapper.dataset.selected = element.IsSelected ? 'true' : 'false';
      wrapper.dataset.groupContext = element.IsGroupContextSelected ? 'true' : 'false';
      if (parentWrapper?.dataset?.id) {
        wrapper.dataset.parentGroupId = parentWrapper.dataset.id;
      }
      if (element.Kind === 'Button') {
        wrapper.dataset.scadaButtonKind = buttonKind;
        wrapper.dataset.scadaButtonBehavior = JSON.stringify(buttonBehavior || {});
        if (buttonKind === 'Toggle') {
          wrapper.dataset.scadaToggleState = 'off';
        }
        if (buttonBehavior.IsDisabled === true) {
          wrapper.dataset.scadaDisabled = 'true';
          wrapper.setAttribute('aria-disabled', 'true');
        }
      }
      if (element.IsSelected) {
        selectedModernIds.add(element.Id);
        selectedModernId = element.Id;
      }
      wrapper.style.left = `${element.X}px`;
      wrapper.style.top = `${element.Y}px`;
      wrapper.style.width = `${element.Width}px`;
      wrapper.style.height = `${element.Height}px`;
      wrapper.style.zIndex = `${Number(element.RenderIndex ?? element.renderIndex ?? 0)}`;
      wrapper.style.fontFamily = cssText(style.FontFamily, 'Segoe UI');
      wrapper.style.fontSize = `${cssText(style.FontSize, 14)}px`;
      wrapper.style.color = cssText(style.Foreground, '#0f2a30');
      wrapper.style.background = cssText(style.Background, '#ffffff');
      wrapper.style.borderStyle = cssText(style.BorderStyle, 'solid').toLowerCase();
      wrapper.style.borderWidth = `${cssText(style.BorderWidth, 1)}px`;
      wrapper.style.borderColor = cssText(style.BorderColor, '#8aa0a6');
      wrapper.style.boxShadow = shadowCss(style.ShadowPreset);
      wrapper.style.opacity = `${Math.max(0, Math.min(1, Number(style.Opacity ?? 1)))}`;
      wrapper.style.transformOrigin = 'center center';
      wrapper.style.transform = `rotate(${Number(style.Rotation ?? 0)}deg)`;
      if (style.AdvancedCss) {
        wrapper.style.cssText += ';' + style.AdvancedCss;
      }
      if (isGroup) {
        (element.Children || element.children || []).forEach(child => {
          wrapper.appendChild(renderElement(child, wrapper));
        });
      } else if (element.Kind === 'Shape') {
        wrapper.style.background = 'transparent';
        wrapper.style.border = '0';
        wrapper.style.padding = '0';
        wrapper.appendChild(renderShapeElement(element, style));
      } else if (element.Kind === 'Text') {
        const text = document.createElement('span');
        text.textContent = data.Text || element.DisplayName || 'Texte';
        text.style.width = '100%';
        text.style.overflow = 'hidden';
        text.style.textOverflow = 'ellipsis';
        text.style.whiteSpace = 'nowrap';
        wrapper.appendChild(text);
      } else if (element.Kind === 'Button') {
        const button = document.createElement('button');
        button.type = 'button';
        button.dataset.scadaButtonKind = buttonKind;
        button.dataset.scadaButtonBehavior = JSON.stringify(buttonBehavior || {});
        button.textContent = data.Text || data.Placeholder || element.DisplayName || 'Bouton';
        button.style.width = '100%';
        button.style.height = '100%';
        button.style.boxSizing = 'border-box';
        button.style.font = 'inherit';
        button.style.color = 'inherit';
        button.style.background = 'transparent';
        button.style.border = '0';
        button.style.overflow = 'hidden';
        button.style.textOverflow = 'ellipsis';
        button.style.whiteSpace = 'nowrap';
        button.style.pointerEvents = 'none';
        wrapper.appendChild(button);
      } else if (element.Kind === 'InputNumeric' && data.IsReadOnly === true) {
        const value = document.createElement('span');
        value.textContent = data.Value ?? data.DisplayFormat ?? data.Placeholder ?? '';
        value.style.width = '100%';
        value.style.overflow = 'hidden';
        value.style.textOverflow = 'ellipsis';
        value.style.whiteSpace = 'nowrap';
        wrapper.appendChild(value);
      } else if (element.Kind === 'Custom') {
        wrapper.style.padding = '0';
        wrapper.style.alignItems = 'stretch';
        wrapper.style.justifyContent = 'stretch';
        const custom = document.createElement('div');
        custom.className = 'scada-modern-custom-content';
        custom.style.width = '100%';
        custom.style.height = '100%';
        custom.style.pointerEvents = 'none';
        custom.style.overflow = 'visible';
        custom.innerHTML = data.Text || '';
        custom.querySelectorAll('svg').forEach(svg => {
          svg.style.width = '100%';
          svg.style.height = '100%';
          svg.style.display = 'block';
          svg.style.overflow = 'visible';
        });
        wrapper.appendChild(custom);
      } else {
        const input = document.createElement('input');
        input.type = element.Kind === 'InputNumeric' ? 'number' : 'text';
        input.readOnly = data.IsReadOnly === true;
        input.placeholder = data.Placeholder || '';
        input.value = element.Kind === 'InputNumeric'
          ? (data.Value ?? '')
          : (data.Text ?? '');
        wrapper.appendChild(input);
      }

      const badge = document.createElement('div');
      badge.className = 'scada-modern-badge';
      badge.textContent = `${element.DisplayName || element.Id} - ${element.Kind}`;
      wrapper.appendChild(badge);

      ['nw', 'ne', 'sw', 'se', 'n', 's', 'e', 'w'].forEach(handle => {
        const grip = document.createElement('span');
        grip.className = 'scada-modern-handle';
        grip.dataset.handle = handle;
        wrapper.appendChild(grip);
      });

      wrapper.addEventListener('pointerdown', event => {
        if (placementKind) {
          return;
        }
        if (event.target?.closest?.('.scada-modern-element') !== wrapper) {
          return;
        }
        event.preventDefault();
        event.stopPropagation();
        const sceneMoveWrapper = getSceneMoveWrapper(wrapper);
        const sceneMoveId = sceneMoveWrapper?.dataset?.id || element.Id;
        const preserveModernSelection = !event.ctrlKey && !event.shiftKey && selectedModernIds.has(sceneMoveId);
        if (event.ctrlKey || event.shiftKey) {
          toggleModernElementInSelection(sceneMoveId);
        } else {
          clearSelection();
          if (!preserveModernSelection) {
            selectModernElementInDom(sceneMoveId);
          }
        }
        if (!preserveModernSelection) {
          window.chrome?.webview?.postMessage({
            type: 'selectSceneObject',
            id: sceneMoveId,
            additive: event.ctrlKey || event.shiftKey,
            toggle: event.ctrlKey || event.shiftKey
          });
        }
        const geometry = readWrapperGeometry(sceneMoveWrapper);
        const isResize = event.target?.classList?.contains('scada-modern-handle');
        const groupChildren = isResize && sceneMoveWrapper.classList.contains('scada-modern-group')
          ? Array.from(sceneMoveWrapper.querySelectorAll('.scada-modern-element')).map(child => ({
              id: child.dataset.id,
              wrapper: child,
              geometry: readWrapperGeometry(child)
            }))
          : [];
        const movingWrappers = isResize
          ? [sceneMoveWrapper]
          : Array.from(document.querySelectorAll('.scada-modern-element'))
              .filter(item => selectedModernIds.has(item.dataset.id))
              .map(item => getSceneMoveWrapper(item))
              .filter((item, index, items) => item && items.indexOf(item) === index);
        modernDrag = {
          id: sceneMoveId,
          wrapper: sceneMoveWrapper,
          mode: event.target?.dataset?.handle === 'ne' ? 'rotate' : (isResize ? 'resize' : 'move'),
          handle: event.target?.dataset?.handle || '',
          startClientX: event.clientX,
          startClientY: event.clientY,
          startX: geometry.x,
          startY: geometry.y,
          startWidth: geometry.width,
          startHeight: geometry.height,
          aspectRatio: geometry.height > 0 ? geometry.width / geometry.height : null,
          groupChildren,
          items: movingWrappers.map(item => ({
            id: item.dataset.id,
            wrapper: item,
            geometry: readWrapperGeometry(item)
          }))
        };
        if (modernDrag.mode === 'rotate') {
          modernDrag.startRotation = getWrapperRotation(sceneMoveWrapper);
        }
        sceneMoveWrapper.setPointerCapture?.(event.pointerId);
      }, true);

      wrapper.addEventListener('dblclick', event => {
        if (placementKind) {
          return;
        }
        if (event.target?.closest?.('.scada-modern-element') !== wrapper) {
          return;
        }
        event.preventDefault();
        event.stopPropagation();
        if (!event.ctrlKey && !event.shiftKey) {
          clearSelection();
        }
        selectedModernId = element.Id;
        selectModernElementInDom(element.Id);
        window.chrome?.webview?.postMessage({ type: 'openSceneObjectProperties', id: element.Id });
      }, true);

      wrapper.addEventListener('contextmenu', event => {
        if (placementKind) {
          return;
        }
        if (event.target?.closest?.('.scada-modern-element') !== wrapper) {
          return;
        }
        event.preventDefault();
        event.stopPropagation();
        const sceneContextWrapper = getSceneMoveWrapper(wrapper);
        const sceneContextId = sceneContextWrapper?.dataset?.id || element.Id;
        lastObjectContextTargetId = sceneContextId;
        if (!selectedModernIds.has(sceneContextId) && !event.ctrlKey && !event.shiftKey) {
          clearSelection();
          selectModernElementInDom(sceneContextId);
        } else if (event.ctrlKey || event.shiftKey) {
          toggleModernElementInSelection(sceneContextId);
        }
        window.chrome?.webview?.postMessage({
          type: 'contextMenuRequest',
          targetKind: 'object',
          id: sceneContextId,
          x: event.clientX,
          y: event.clientY,
          backgroundColor: getBackgroundColor()
        });
      }, true);

      return wrapper;
    };
    modernElements.forEach(element => layer.appendChild(renderElement(element)));
  }

  function applyTextOverrides(overrides) {
    if (!Array.isArray(overrides)) return;
    overrides.forEach(overrideItem => {
      if (!overrideItem || !overrideItem.Id) return;
      const target = getSelectableElementById(overrideItem.Id);
      if (target) {
        setEditableText(target, overrideItem.Text || '');
      }
    });
    postInventory();
  }

  function getBackgroundColor() {
    const surface = getPageSurface();
    return window.getComputedStyle(surface).backgroundColor || surface.style.backgroundColor || '#000000';
  }

  function rectsIntersect(a, b) {
    return !(a.right < b.left || a.left > b.right || a.bottom < b.top || a.top > b.bottom);
  }

  function isEditableKeyboardTarget(target) {
    if (!target) return false;
    if (activeTextEditor?.editor === target) return true;
    const editable = target.closest?.('input, textarea, select, [contenteditable]');
    if (!editable) return false;
    const tag = (editable.tagName || '').toLowerCase();
    if (tag === 'input' || tag === 'textarea' || tag === 'select') {
      return editable.disabled !== true && editable.readOnly !== true;
    }
    return editable.getAttribute('contenteditable') !== 'false';
  }

  let drag = null;

  document.addEventListener('click', event => {
    if (!menu.contains(event.target)) {
      hideMenu();
    }
  }, true);

  document.addEventListener('dblclick', event => {
    const target = findSelectable(event.target);
    if (!target) return;
    if (beginLegacyTextEdit(target)) {
      event.preventDefault();
      event.stopPropagation();
    }
  }, true);

  function openContextMenu(event) {
    if (event.__scadaContextMenuHandled) return;
    if (event.target?.closest?.('.scada-modern-element')) return;
    event.__scadaContextMenuHandled = true;
    hideMenu();
    const target = findSelectable(event.target);
    if (target && !selected.has(getId(target))) {
      if (!event.ctrlKey && !event.shiftKey) {
        clearSelection();
        clearModernSelection();
      }
      setSelected(target, true);
      postSelection();
    } else if (!target && selected.size === 0) {
      clearAllSelection(false);
    }

    event.preventDefault();
    event.stopPropagation();
    const hasLegacySelection = target || selected.size > 0;
    window.chrome?.webview?.postMessage({
      type: 'contextMenuRequest',
      targetKind: hasLegacySelection ? 'source' : 'background',
      items: hasLegacySelection ? getSelectedMessages() : [],
      x: event.clientX,
      y: event.clientY,
      backgroundColor: getBackgroundColor()
    });
  }

  document.addEventListener('pointerdown', event => {
    if (activeTextEditor && event.target !== activeTextEditor.editor) {
      closeTextEditor(true);
    }
    if (event.button === 2) {
      return;
    }
    if (event.button !== 0) return;
    if (event.target?.closest?.('#scada-scene-resize-handle')) {
      const surface = getPageSurface();
      sceneCanvasResize = {
        pointerId: event.pointerId,
        startClientX: event.clientX,
        startClientY: event.clientY,
        startWidth: surface.offsetWidth || surface.getBoundingClientRect().width || 160,
        startHeight: surface.offsetHeight || surface.getBoundingClientRect().height || 120
      };
      event.target.setPointerCapture?.(event.pointerId);
      event.preventDefault();
      event.stopPropagation();
      return;
    }
    if (event.target && menu.contains(event.target)) {
      event.stopPropagation();
      return;
    }
    hideMenu();

    if (placementKind) {
      const surface = getPageSurface();
      const point = getSurfacePoint(event, surface);
      if (placementIsTwoPoint) {
        if (!placementStart) {
          placementStart = point;
          updateTwoPointPlacementPreview(point);
        } else {
          const start = placementStart;
          const kind = placementKind;
          const shapeKind = placementShapeKind;
          clearPlacementState(false);
          window.chrome?.webview?.postMessage({
            type: 'placeTwoPointElement',
            kind,
            shapeKind,
            x: start.x,
            y: start.y,
            x2: point.x,
            y2: point.y
          });
        }
      } else {
        const kind = placementKind;
        clearPlacementState(false);
        const shapeKind = placementShapeKind;
        window.chrome?.webview?.postMessage({ type: 'placeElement', kind, shapeKind, x: point.x, y: point.y });
      }
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (event.target?.closest?.('.scada-modern-element')) {
      return;
    }

    const target = findSelectable(event.target);

    if (target) {
      const targetId = getId(target);
      if (event.altKey) {
        setSelected(target, false);
      } else if (event.ctrlKey || event.shiftKey) {
        setSelected(target, !selected.has(getId(target)));
      } else {
        if (!selected.has(targetId)) {
          clearSelection();
        }
        clearModernSelection();
        setSelected(target, true);
      }
      postSelection();
      if (!event.altKey && !event.ctrlKey && !event.shiftKey && !event.metaKey && selected.has(targetId)) {
        sourceDrag = {
          pointerId: event.pointerId,
          captureTarget: target,
          startClientX: event.clientX,
          startClientY: event.clientY,
          didDrag: false,
          items: selectedSourceElements().map(el => ({
            id: getId(el),
            el,
            geometry: getElementBounds(el)
          }))
        };
        target.setPointerCapture?.(event.pointerId);
      }
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    drag = {
      startX: event.clientX,
      startY: event.clientY,
      remove: event.altKey
    };
    marquee.style.left = `${drag.startX}px`;
    marquee.style.top = `${drag.startY}px`;
    marquee.style.width = '0px';
    marquee.style.height = '0px';
    marquee.style.display = 'block';
  }, true);

  function ensureRotationBadge() {
    let badge = document.getElementById('scada-rotation-badge');
    if (!badge) {
      badge = document.createElement('div');
      badge.id = 'scada-rotation-badge';
      document.body.appendChild(badge);
    }
    return badge;
  }

  function updateRotationBadge(clientX, clientY, angleDeg) {
    const badge = ensureRotationBadge();
    badge.textContent = `${angleDeg.toFixed(1)}°`;
    badge.style.left = `${clientX}px`;
    badge.style.top = `${clientY}px`;
    badge.style.display = 'block';
  }

  function hideRotationBadge() {
    const badge = document.getElementById('scada-rotation-badge');
    if (badge) {
      badge.style.display = 'none';
    }
  }

  function ensureRotationInput() {
    let input = document.getElementById('scada-rotation-input');
    if (!input) {
      input = document.createElement('input');
      input.id = 'scada-rotation-input';
      input.type = 'text';
      document.body.appendChild(input);
    }
    return input;
  }

  function beginCustomRotationEntry(anchorX, anchorY) {
    if (!lastObjectContextTargetId) return;
    const targetId = lastObjectContextTargetId;
    const input = ensureRotationInput();
    const targetWrapper = document.querySelector(`.scada-modern-element[data-id="${targetId}"]`);
    input.value = targetWrapper ? getWrapperRotation(targetWrapper).toFixed(1) : '0';
    input.style.left = `${anchorX}px`;
    input.style.top = `${anchorY}px`;
    input.style.display = 'block';

    const decimalPattern = /^-?\d{1,3}(\.\d)?$/;
    const liveTypingPattern = /^-?\d{0,3}(\.\d?)?$/;
    const onInput = () => {
      if (input.value !== '' && !liveTypingPattern.test(input.value)) {
        input.value = input.value.slice(0, -1);
      }
    };

    const commit = () => {
      const parsed = parseFloat(input.value);
      if (!Number.isNaN(parsed)) {
        let normalized = parsed % 360;
        if (normalized < 0) {
          normalized += 360;
        }
        normalized = Math.round(normalized * 10) / 10;
        if (normalized >= 360) {
          normalized -= 360;
        }
        postModernRotation(targetId, normalized);
      }
      cleanup();
    };

    const cancel = () => cleanup();

    const onKeyDown = event => {
      if (event.key === 'Enter') {
        event.preventDefault();
        commit();
      } else if (event.key === 'Escape') {
        event.preventDefault();
        cancel();
      }
    };

    function cleanup() {
      input.removeEventListener('input', onInput);
      input.removeEventListener('keydown', onKeyDown);
      input.removeEventListener('blur', commit);
      input.style.display = 'none';
    }

    input.addEventListener('input', onInput);
    input.addEventListener('keydown', onKeyDown);
    input.addEventListener('blur', commit);
    input.focus();
    input.select();
  }

  document.addEventListener('pointermove', event => {
    if (placementKind && placementIsTwoPoint && placementStart) {
      updateTwoPointPlacementPreview(getSurfacePoint(event));
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (sceneCanvasResize) {
      const width = sceneCanvasResize.startWidth + event.clientX - sceneCanvasResize.startClientX;
      const height = sceneCanvasResize.startHeight + event.clientY - sceneCanvasResize.startClientY;
      const previewSize = setSceneSurfaceSize(width, height);
      window.chrome?.webview?.postMessage({
        type: 'previewSceneCanvasResize',
        width: previewSize.width,
        height: previewSize.height
      });
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (modernDrag) {
      const dx = event.clientX - modernDrag.startClientX;
      const dy = event.clientY - modernDrag.startClientY;
      const geometry = {
        x: modernDrag.startX,
        y: modernDrag.startY,
        width: modernDrag.startWidth,
        height: modernDrag.startHeight
      };

      if (modernDrag.mode === 'rotate') {
        const wrapperRect = modernDrag.wrapper.getBoundingClientRect();
        const pivotClientX = wrapperRect.left + wrapperRect.width / 2;
        const pivotClientY = wrapperRect.top + wrapperRect.height / 2;
        const angleRad = Math.atan2(event.clientY - pivotClientY, event.clientX - pivotClientX);
        let angleDeg = angleRad * (180 / Math.PI) + 90;
        if (event.ctrlKey) {
          angleDeg = Math.round(angleDeg / 90) * 90;
        }
        let normalized = angleDeg % 360;
        if (normalized < 0) {
          normalized += 360;
        }
        normalized = Math.round(normalized * 10) / 10;
        if (normalized >= 360) {
          normalized -= 360;
        }
        modernDrag.wrapper.style.transformOrigin = 'center center';
        modernDrag.wrapper.style.transform = `rotate(${normalized}deg)`;
        modernDrag.currentRotation = normalized;
        updateRotationBadge(event.clientX, event.clientY, normalized);
      } else if (modernDrag.mode === 'move') {
        (modernDrag.items || []).forEach(item => {
          setWrapperGeometry(item.wrapper, {
            x: item.geometry.x + dx,
            y: item.geometry.y + dy,
            width: item.geometry.width,
            height: item.geometry.height
          });
        });
      } else {
        if (modernDrag.handle.includes('w')) {
          const clampedX = clampNearAxis(modernDrag.startX, modernDrag.startWidth, dx);
          geometry.x = clampedX.pos;
          geometry.width = clampedX.size;
        } else if (modernDrag.handle.includes('e')) {
          geometry.width = Math.max(8, modernDrag.startWidth + dx);
        }
        if (modernDrag.handle.includes('n')) {
          const clampedY = clampNearAxis(modernDrag.startY, modernDrag.startHeight, dy);
          geometry.y = clampedY.pos;
          geometry.height = clampedY.size;
        } else if (modernDrag.handle.includes('s')) {
          geometry.height = Math.max(8, modernDrag.startHeight + dy);
        }

        if (event.shiftKey && modernDrag.handle.length === 2 && modernDrag.aspectRatio) {
          const widthRatioChange = Math.abs(geometry.width - modernDrag.startWidth) / modernDrag.startWidth;
          const heightRatioChange = Math.abs(geometry.height - modernDrag.startHeight) / modernDrag.startHeight;
          if (widthRatioChange >= heightRatioChange) {
            geometry.height = Math.max(8, geometry.width / modernDrag.aspectRatio);
          } else {
            geometry.width = Math.max(8, geometry.height * modernDrag.aspectRatio);
          }
          if (modernDrag.handle.includes('n')) {
            geometry.y = modernDrag.startY + (modernDrag.startHeight - geometry.height);
            if (geometry.y < 0) {
              geometry.height = modernDrag.startY + modernDrag.startHeight;
              geometry.y = 0;
            }
          }
          if (modernDrag.handle.includes('w')) {
            geometry.x = modernDrag.startX + (modernDrag.startWidth - geometry.width);
            if (geometry.x < 0) {
              geometry.width = modernDrag.startX + modernDrag.startWidth;
              geometry.x = 0;
            }
          }
        }

        setWrapperGeometry(modernDrag.wrapper, geometry);

        if (modernDrag.groupChildren.length) {
          const scaleX = geometry.width / modernDrag.startWidth;
          const scaleY = geometry.height / modernDrag.startHeight;
          modernDrag.groupChildren.forEach(child => {
            setWrapperGeometry(child.wrapper, {
              x: child.geometry.x * scaleX,
              y: child.geometry.y * scaleY,
              width: child.geometry.width * scaleX,
              height: child.geometry.height * scaleY
            });
          });
        }
      }

      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (sourceDrag) {
      const dx = event.clientX - sourceDrag.startClientX;
      const dy = event.clientY - sourceDrag.startClientY;
      sourceDrag.didDrag = sourceDrag.didDrag || Math.abs(dx) > 3 || Math.abs(dy) > 3;
      if (sourceDrag.didDrag) {
        sourceDrag.items.forEach(item => setSourceElementGeometry(item.el, {
          x: item.geometry.x + dx,
          y: item.geometry.y + dy,
          width: item.geometry.width,
          height: item.geometry.height
        }));
      }
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (!drag) return;
    const left = Math.min(drag.startX, event.clientX);
    const top = Math.min(drag.startY, event.clientY);
    const width = Math.abs(event.clientX - drag.startX);
    const height = Math.abs(event.clientY - drag.startY);
    marquee.style.left = `${left}px`;
    marquee.style.top = `${top}px`;
    marquee.style.width = `${width}px`;
    marquee.style.height = `${height}px`;
  }, true);

  document.addEventListener('pointerup', event => {
    if (sceneCanvasResize) {
      const width = sceneCanvasResize.startWidth + event.clientX - sceneCanvasResize.startClientX;
      const height = sceneCanvasResize.startHeight + event.clientY - sceneCanvasResize.startClientY;
      const finalSize = setSceneSurfaceSize(width, height);
      window.chrome?.webview?.postMessage({
        type: 'resizeSceneCanvas',
        beforeWidth: sceneCanvasResize.startWidth,
        beforeHeight: sceneCanvasResize.startHeight,
        width: finalSize.width,
        height: finalSize.height
      });
      sceneCanvasResize = null;
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (modernDrag) {
      if (modernDrag.mode === 'rotate') {
        postModernRotation(modernDrag.id, modernDrag.currentRotation ?? modernDrag.startRotation);
        hideRotationBadge();
        modernDrag = null;
        event.preventDefault();
        event.stopPropagation();
        return;
      }
      const geometry = readWrapperGeometry(modernDrag.wrapper);
      if (modernDrag.mode === 'move' && (modernDrag.items || []).length > 1) {
        postSelectionMove(
          'object',
          modernDrag.items.map(item => item.id).filter(Boolean),
          event.clientX - modernDrag.startClientX,
          event.clientY - modernDrag.startClientY);
      } else if (modernDrag.mode === 'resize' && modernDrag.groupChildren.length) {
        postModernGroupResize(
          modernDrag.id,
          {
            x: modernDrag.startX,
            y: modernDrag.startY,
            width: modernDrag.startWidth,
            height: modernDrag.startHeight
          },
          geometry,
          modernDrag.groupChildren.map(child => ({
            id: child.id,
            geometry: child.geometry,
            after: readWrapperGeometry(child.wrapper)
          })));
      } else {
        postModernGeometry(
          modernDrag.id,
          {
            x: modernDrag.startX,
            y: modernDrag.startY,
            width: modernDrag.startWidth,
            height: modernDrag.startHeight
          },
          geometry);
      }
      modernDrag = null;
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (sourceDrag) {
      const deltaX = event.clientX - sourceDrag.startClientX;
      const deltaY = event.clientY - sourceDrag.startClientY;
      if (sourceDrag.didDrag) {
        postSelectionMove(
          'source',
          sourceDrag.items.map(item => item.id).filter(Boolean),
          deltaX,
          deltaY,
          sourceDrag.items.map(item => toElementMessage(item.el)));
        postInventory();
        postSelection();
      }
      try {
        sourceDrag.captureTarget?.releasePointerCapture?.(sourceDrag.pointerId);
      } catch {
      }
      sourceDrag = null;
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (!drag) return;
    const box = marquee.getBoundingClientRect();
    marquee.style.display = 'none';

    if (box.width > 3 && box.height > 3) {
      getSelectableElements()
        .filter(el => rectsIntersect(box, el.getBoundingClientRect()))
        .forEach(el => setSelected(el, !drag.remove));
      postSelection();
    } else if (!drag.remove && !event.ctrlKey && !event.shiftKey) {
      clearAllSelection();
    }

    drag = null;
    event.preventDefault();
  }, true);

  document.addEventListener('keydown', event => {
    if (isEditableKeyboardTarget(event.target)) {
      return;
    }
    if (placementKind && event.key === 'Escape') {
      clearPlacementState(true);
      event.preventDefault();
      event.stopPropagation();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') {
      window.chrome?.webview?.postMessage({ type: event.shiftKey ? 'redo' : 'undo' });
      event.preventDefault();
      event.stopPropagation();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'y') {
      window.chrome?.webview?.postMessage({ type: 'redo' });
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (!selectedModernId) return;

    const wrapper = document.querySelector(`.scada-modern-element[data-id="${CSS.escape(selectedModernId)}"]`);
    if (!wrapper) return;

    if (event.key === 'Backspace') {
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (event.key === 'Delete') {
      window.chrome?.webview?.postMessage({ type: 'deleteSceneObject', id: selectedModernId });
      selectedModernIds.delete(selectedModernId);
      selectedModernId = null;
      syncModernSelectionInDom();
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    const arrows = {
      ArrowLeft: [-1, 0],
      ArrowRight: [1, 0],
      ArrowUp: [0, -1],
      ArrowDown: [0, 1]
    };
    const delta = arrows[event.key];
    if (!delta) return;

    const step = event.shiftKey ? 10 : 1;
    const geometry = readWrapperGeometry(wrapper);
    const before = { ...geometry };
    geometry.x += delta[0] * step;
    geometry.y += delta[1] * step;
    setWrapperGeometry(wrapper, geometry);
    postModernGeometry(selectedModernId, before, geometry);
    event.preventDefault();
    event.stopPropagation();
  }, true);

  document.addEventListener('contextmenu', openContextMenu, true);
  menu.addEventListener('click', event => {
    if (event.target?.disabled || event.target?.getAttribute?.('aria-disabled') === 'true') {
      event.preventDefault();
      event.stopPropagation();
      return;
    }
    const commandId = event.target?.getAttribute?.('data-command-id');
    if (!commandId) return;
    event.preventDefault();
    event.stopPropagation();
    if (commandId === 'object.rotation.custom') {
      const anchorRect = event.target.getBoundingClientRect();
      hideMenu();
      beginCustomRotationEntry(anchorRect.left, anchorRect.top);
      return;
    }
    hideMenu();
    window.chrome?.webview?.postMessage({
      type: 'executeCommand',
      commandId,
      items: getSelectedMessages(),
      backgroundColor: getBackgroundColor()
    });
  });

  function hideSelected() {
    getSelectableElements()
      .filter(el => selected.has(getId(el)))
      .forEach(removeSourceElement);
    selected.clear();
    postInventory();
    postSelection();
  }

  function hideLegacyElements(ids) {
    removeSourceElements(ids);
  }

  function removeLegacyElements(ids) {
    removeSourceElements(ids);
  }

  function deleteSelected() {
    getSelectableElements()
      .filter(el => selected.has(getId(el)))
      .forEach(removeSourceElement);
    selected.clear();
    postInventory();
    postSelection();
  }

  function restoreHidden() {
    const restoreIds = new Set([...hidden, ...removedNodes.keys()]);
    restoreIds.forEach(id => restoreSourceElement(id, false));
    hidden.clear();
    document.querySelectorAll(selectableSelector).forEach(el => {
      el.style.display = '';
      el.removeAttribute('data-scada-selected');
      el.removeAttribute('data-scada-deleted');
    });
    selected.clear();
    postInventory();
    postSelection();
  }

  function restoreLegacyElements(ids) {
    const restoreIds = new Set(Array.isArray(ids) ? ids : []);
    if (!restoreIds.size) return;
    restoreIds.forEach(id => restoreSourceElement(id, true));
    postInventory();
    postSelection();
  }

  const sceneEditorApi = {
    refresh() {
      ensureSceneResizeHandle();
      postInventory();
      postSelection();
    },
    command(command) {
      const action = typeof command === 'string' ? command : command?.Action;
      if (action === 'beginPlacement') {
        clearModernSelection(false);
        clearPlacementState(false);
        placementKind = command?.Kind || null;
        placementShapeKind = command?.ShapeKind || command?.shapeKind || null;
        placementIsTwoPoint = command?.IsTwoPoint === true || command?.isTwoPoint === true;
        document.body.classList.toggle('scada-placement-active', !!placementKind);
        return;
      }
      if (action === 'selectObject' || action === 'selectModern') {
        const ids = command?.Ids || command?.ids || [];
        selectedModernIds.clear();
        if (Array.isArray(ids) && ids.length) {
          ids.forEach(id => { if (id) selectedModernIds.add(id); });
          selectedModernId = ids.at(-1) || null;
          syncModernSelectionInDom();
        } else {
          selectModernElementInDom(command?.Id || null);
        }
        return;
      }
      if (action === 'hideSelected') hideSelected();
      if (action === 'deleteSelected') deleteSelected();
      if (action === 'restoreHidden') restoreHidden();
      if (action === 'clearSelection') clearAllSelection();
    },
    showContextMenu,
    getSelectedMessagesForStudio,
    selectLegacyElements,
    hideLegacyElements,
    removeLegacyElements,
    restoreLegacyElements,
    setBackgroundColor(color) {
      const surface = getPageSurface();
      surface.style.backgroundColor = color;
      document.body.style.backgroundColor = color;
    },
    setCanvasSize(size) {
      setSceneSurfaceSize(size?.width ?? size?.Width, size?.height ?? size?.Height);
      ensureSceneResizeHandle();
    },
    applyTextOverrides,
    applySourceElementBounds,
    renderModernElements
  };
  window.scadaSceneEditor = sceneEditorApi;
  window.scadaLegacyExtraction = sceneEditorApi;

  ensureSceneResizeHandle();
  postInventory();
  postSelection();
})();
""";
}
