(function () {
  'use strict';

  var EDIT_TIMEOUT = 30000;
  var _locks = new WeakMap();
  var _activeLocks = new Set();
  var _watched = new WeakSet();

  function _resolveElement(target) {
    if (target && typeof target === 'object') return target;
    return target && window.document && typeof window.document.getElementById === 'function'
      ? window.document.getElementById(target)
      : null;
  }

  function _createOverlay() {
    var div = document.createElement('div');
    div.className = 'scada-input-edit-overlay';
    div.style.cssText =
      'position:absolute;inset:0;background:rgba(15,42,48,0.06);border:2px solid rgba(15,42,48,0.32);border-radius:4px;pointer-events:none;z-index:10';
    div.setAttribute('data-scada-input-overlay', '');
    return div;
  }

  function _readTagId(element, inputEl) {
    var candidates = [
      element.getAttribute && element.getAttribute('data-scada-read-tag'),
      element.getAttribute && element.getAttribute('data-scada-mapping-id'),
      inputEl.getAttribute && inputEl.getAttribute('data-scada-read-tag'),
      inputEl.getAttribute && inputEl.getAttribute('data-scada-mapping-id')
    ];
    for (var i = 0; i < candidates.length; i++) {
      if (candidates[i]) return candidates[i];
    }
    return null;
  }

  function _refreshTimer(lock) {
    if (lock.timerId) window.clearTimeout(lock.timerId);
    lock.timerId = window.setTimeout(function () {
      var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
      var value = bridge && lock.readTagId ? bridge.getTagValue(lock.readTagId) : null;
      if (value !== null && value !== undefined) lock.inputEl.value = value;
      release(lock.element);
      if (typeof lock.inputEl.blur === 'function') lock.inputEl.blur();
    }, EDIT_TIMEOUT);
  }

  function watch(element) {
    if (!element || _watched.has(element)) return;
    var inputEl = element.querySelector && element.querySelector('input, textarea, select');
    if (!inputEl) return;
    _watched.add(element);
    if (element.setAttribute) element.setAttribute('data-scada-edit-guard', 'attached');

    inputEl.addEventListener('focus', function () { lock(element, inputEl); });
    inputEl.addEventListener('blur', function () { release(element); });
    inputEl.addEventListener('change', function () { release(element); });
    inputEl.addEventListener('input', function () {
      var active = _locks.get(element);
      if (active) _refreshTimer(active);
    });
    inputEl.addEventListener('keydown', function (event) {
      if (event.key === 'Escape') {
        var active = _locks.get(element);
        if (active) inputEl.value = active.startingValue;
        release(element);
      } else if (event.key === 'Enter') {
        release(element);
      }
    });
  }

  function lock(target, inputEl) {
    var element = _resolveElement(target);
    if (!element) return;
    inputEl = inputEl || (element.querySelector && element.querySelector('input, textarea, select'));
    if (!inputEl) return;
    release(element);

    var parent = inputEl.parentElement;
    var overlay = _createOverlay();
    var parentPosition = parent && parent.style ? parent.style.position || '' : '';
    if (parent) {
      var computed = typeof window.getComputedStyle === 'function' ? window.getComputedStyle(parent) : null;
      if (computed && computed.position === 'static' && parent.style) parent.style.position = 'relative';
      parent.appendChild(overlay);
    }

    var record = {
      element: element,
      inputEl: inputEl,
      overlay: overlay,
      parent: parent,
      parentPosition: parentPosition,
      startingValue: inputEl.value,
      readTagId: _readTagId(element, inputEl),
      timerId: null,
      observer: null
    };
    _locks.set(element, record);
    _activeLocks.add(record);
    var stateEngine = window.ScadaRuntime && window.ScadaRuntime.StateEngine;
    if (stateEngine) stateEngine.pauseElement(element);

    if (typeof window.MutationObserver === 'function' && window.document) {
      record.observer = new window.MutationObserver(function () {
        if (element.isConnected === false) release(element);
      });
      record.observer.observe(window.document.documentElement || window.document, { childList: true, subtree: true });
    }
    _refreshTimer(record);
  }

  function release(target) {
    var element = _resolveElement(target);
    var record = element ? _locks.get(element) : null;
    if (!record) return;
    if (record.timerId) window.clearTimeout(record.timerId);
    if (record.observer) record.observer.disconnect();
    if (record.overlay && record.overlay.parentNode) record.overlay.parentNode.removeChild(record.overlay);
    if (record.parent && record.parent.style) record.parent.style.position = record.parentPosition;
    var stateEngine = window.ScadaRuntime && window.ScadaRuntime.StateEngine;
    if (stateEngine) stateEngine.resumeElement(element);
    _locks.delete(element);
    _activeLocks.delete(record);
  }

  function isLocked(target) {
    var element = _resolveElement(target);
    return !!(element && _locks.has(element));
  }

  function dispose(container) {
    var records = Array.from(_activeLocks);
    for (var i = 0; i < records.length; i++) {
      var element = records[i].element;
      if (element === container || (container && typeof container.contains === 'function' && container.contains(element))) {
        release(element);
      }
    }
  }

  window.ScadaRuntime = window.ScadaRuntime || {};
  window.ScadaRuntime.InputEditGuard = {
    watch: watch,
    lock: lock,
    release: release,
    dispose: dispose,
    isLocked: isLocked,
    editTimeout: EDIT_TIMEOUT
  };
})();
