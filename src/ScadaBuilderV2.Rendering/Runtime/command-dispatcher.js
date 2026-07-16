(function () {
  'use strict';

  var INTENT_VERSION = '1.0';
  var TRIGGER_MAP = {
    onClick: 'click',
    onRelease: 'mouseup',
    onHover: 'mouseenter',
    onHoverEnter: 'mouseenter',
    onHoverExit: 'mouseleave'
  };
  var _bindings = new WeakMap();
  var _pendingCommands = new WeakMap();

  function _diagnose(code, cmd, detail) {
    if (window.console && typeof window.console.warn === 'function') {
      window.console.warn('SCADA command ' + code, {
        commandId: cmd && cmd.id ? cmd.id : '',
        detail: detail || ''
      });
    }
  }

  function _listen(target, eventName, handler, listeners, options) {
    if (!target || typeof target.addEventListener !== 'function') return;
    target.addEventListener(eventName, handler, options);
    listeners.push({ target: target, eventName: eventName, handler: handler, options: options });
  }

  function _removeListeners(listeners) {
    for (var i = 0; i < listeners.length; i++) {
      var listener = listeners[i];
      if (listener.target && typeof listener.target.removeEventListener === 'function') {
        listener.target.removeEventListener(listener.eventName, listener.handler, listener.options);
      }
    }
    listeners.length = 0;
  }

  function _confirm(cmd, accepted) {
    if (!cmd.confirmation || !cmd.confirmation.message) {
      accepted();
      return;
    }
    var confirmFn = window.ScadaRuntime && window.ScadaRuntime.showConfirmation;
    if (typeof confirmFn === 'function') {
      confirmFn(cmd.confirmation.message, accepted);
      return;
    }
    if (typeof window.confirm === 'function' && window.confirm(cmd.confirmation.message)) {
      accepted();
    }
  }

  function _dispatchIntent(kind, cmd, payload) {
    var intent = {
      id: cmd && cmd.id ? cmd.id : '',
      kind: kind
    };
    payload = payload || {};
    for (var key in payload) {
      if (Object.prototype.hasOwnProperty.call(payload, key) && payload[key] !== undefined) {
        intent[key] = payload[key];
      }
    }

    var envelope = {
      source: 'scada-builder-v2',
      type: 'scada-runtime-intent',
      version: INTENT_VERSION,
      intent: intent,
      // 2.1/2.2 compatibility aliases; no duplicate command semantics live here.
      action: kind,
      pageId: intent.pageId,
      options: intent.options
    };
    var adapter = window.ScadaRuntime && window.ScadaRuntime.HostAdapter;
    if (adapter && typeof adapter.dispatchIntent === 'function') {
      return adapter.dispatchIntent(envelope);
    }
    if (typeof window.postMessage === 'function') {
      window.postMessage(envelope, '*');
      return true;
    }
    _diagnose('host-unavailable', cmd, kind);
    return false;
  }

  function dispatchIntent(kind, actionId, payload) {
    return _dispatchIntent(kind, { id: actionId || '' }, payload || {});
  }

  function _trackWrite(cmd, result) {
    if (!result || typeof result.then !== 'function') return result;
    _pendingCommands.set(cmd, result);
    return result.then(function (ok) {
      if (ok === false) _diagnose('write-rejected', cmd);
      return ok;
    }, function (error) {
      _diagnose('write-failed', cmd, error && error.message ? error.message : String(error));
      return false;
    }).then(function (resultValue) {
      _pendingCommands.delete(cmd);
      return resultValue;
    });
  }

  function _write(cmd, value, payload) {
    if (!cmd.writeTagId || value === null || value === undefined || value === '') {
      _diagnose('write-data-missing', cmd);
      return false;
    }
    if (_pendingCommands.has(cmd)) {
      _diagnose('write-in-flight', cmd);
      return false;
    }
    var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
    if (!bridge || typeof bridge.writeTag !== 'function') {
      _diagnose('tag-bridge-unavailable', cmd);
      return false;
    }
    var metadata = payload || {};
    metadata.commandId = cmd.id || '';
    metadata.contractVersion = INTENT_VERSION;
    return _trackWrite(cmd, bridge.writeTag(cmd.writeTagId, value, metadata));
  }

  function _booleanValue(value) {
    if (value === null || value === undefined) return null;
    if (typeof value === 'boolean') return value;
    if (typeof value === 'number') return value !== 0;
    var normalized = String(value).trim().toLowerCase();
    if (normalized === '') return null;
    if (normalized === '0' || normalized === 'false' || normalized === 'off' || normalized === 'no') return false;
    if (normalized === '1' || normalized === 'true' || normalized === 'on' || normalized === 'yes') return true;
    return Boolean(value);
  }

  function _writeTagCommand(cmd, element, phase) {
    var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
    if (!bridge) return false;
    switch (cmd.writeMode) {
      case 'momentary':
        if (phase !== 'press' && phase !== 'release') {
          _diagnose('momentary-phase-required', cmd);
          return false;
        }
        return _write(cmd, phase === 'press' ? cmd.onValue : cmd.offValue, {
          mode: 'Momentary', phase: phase
        });
      case 'toggle': {
        var current = bridge.getTagValue(cmd.readTagId || cmd.writeTagId);
        var booleanValue = _booleanValue(current);
        if (booleanValue === null) {
          _diagnose('toggle-read-missing', cmd);
          return false;
        }
        return _write(cmd, booleanValue ? '0' : '1', { mode: 'Toggle' });
      }
      case 'setFixed':
        return _write(cmd, cmd.fixedValue, { mode: 'SetFixed' });
      case 'setFromInput': {
        var input = element && typeof element.querySelector === 'function'
          ? element.querySelector('input, textarea')
          : null;
        return input ? _write(cmd, input.value, { mode: 'SetFromInput' }) : false;
      }
      default:
        _diagnose('write-mode-unsupported', cmd, cmd.writeMode);
        return false;
    }
  }

  function _run(element, cmd) {
    if (!element || !cmd || cmd.enabled === false) return false;
    switch (cmd.kind) {
      case 'writeTag':
        return _writeTagCommand(cmd, element);
      case 'navigate':
        return cmd.targetPageId
          ? _dispatchIntent('navigate', cmd, { pageId: cmd.targetPageId })
          : false;
      case 'openPopup':
      case 'togglePopup':
      case 'closePopup':
        return cmd.targetPageId
          ? _dispatchIntent(cmd.kind, cmd, { pageId: cmd.targetPageId, options: cmd.popupOptions })
          : false;
      case 'openUrl':
        return cmd.url
          ? _dispatchIntent('openUrl', cmd, { url: cmd.url, newTab: cmd.newTab === true })
          : false;
      case 'back':
        return _dispatchIntent('back', cmd, {});
      default:
        _diagnose('kind-unsupported', cmd, cmd.kind);
        return false;
    }
  }

  function execute(element, cmd) {
    if (!element || !cmd || cmd.enabled === false) return false;
    if (cmd.kind === 'writeTag' && cmd.writeMode === 'momentary') {
      _diagnose('momentary-requires-bound-press-release', cmd);
      return false;
    }
    var result = false;
    _confirm(cmd, function () { result = _run(element, cmd); });
    return result;
  }

  function _bindMomentary(element, cmd, binding) {
    var active = { pressed: false, wroteOn: false, pressResult: null, pointerId: null, observer: null };
    binding.momentary.push({ cmd: cmd, active: active });

    function cleanupCycle() {
      if (active.observer) {
        active.observer.disconnect();
        active.observer = null;
      }
      active.pointerId = null;
    }

    function release() {
      if (!active.pressed && !active.wroteOn) return;
      active.pressed = false;
      if (active.wroteOn) {
        active.wroteOn = false;
        if (active.pressResult && typeof active.pressResult.then === 'function') {
          active.pressResult.then(function (ok) {
            if (ok !== false) _writeTagCommand(cmd, element, 'release');
          });
        } else {
          _writeTagCommand(cmd, element, 'release');
        }
        active.pressResult = null;
      }
      cleanupCycle();
    }

    function press(event) {
      if (cmd.enabled === false || active.pressed) return;
      if (event && event.type === 'pointerdown' && event.button !== undefined && event.button !== 0) return;
      active.pressed = true;
      active.pointerId = event && event.pointerId !== undefined ? event.pointerId : null;
      if (active.pointerId !== null && typeof element.setPointerCapture === 'function') {
        try { element.setPointerCapture(active.pointerId); } catch (ignored) { }
      }
      if (typeof window.MutationObserver === 'function' && window.document) {
        active.observer = new window.MutationObserver(function () {
          if (element.isConnected === false) release();
        });
        active.observer.observe(window.document.documentElement || window.document, { childList: true, subtree: true });
      }
      _confirm(cmd, function () {
        if (!active.pressed) return;
        var result = _writeTagCommand(cmd, element, 'press');
        active.wroteOn = result !== false;
        active.pressResult = result;
      });
    }

    function keyPress(event) {
      if (event.key === 'Enter' || event.key === ' ') press(event);
    }
    function keyRelease(event) {
      if (event.key === 'Enter' || event.key === ' ') release();
    }

    _listen(element, 'pointerdown', press, binding.listeners);
    _listen(element, 'pointerup', release, binding.listeners);
    _listen(element, 'pointercancel', release, binding.listeners);
    _listen(element, 'lostpointercapture', release, binding.listeners);
    _listen(element, 'keydown', keyPress, binding.listeners);
    _listen(element, 'keyup', keyRelease, binding.listeners);
    _listen(window, 'pointerup', release, binding.listeners);
    _listen(window, 'pointercancel', release, binding.listeners);
    _listen(window, 'blur', release, binding.listeners);
    active.release = release;
  }

  function bind(element) {
    if (!element || typeof element.getAttribute !== 'function') return;
    var configRaw = element.getAttribute('data-scada-command-config');
    if (!configRaw) return;
    var existing = _bindings.get(element);
    if (existing && existing.configRaw === configRaw) return;
    if (existing) unbind(element);

    var config;
    try { config = JSON.parse(configRaw); } catch (error) { return; }
    if (!config || !Array.isArray(config.commands)) return;

    var binding = { configRaw: configRaw, listeners: [], momentary: [] };
    _bindings.set(element, binding);
    if (element.dataset) element.dataset.scadaCommandBoundConfig = configRaw;

    for (var i = 0; i < config.commands.length; i++) {
      var cmd = config.commands[i];
      if (!cmd || cmd.enabled === false) continue;
      if (cmd.kind === 'writeTag' && cmd.writeMode === 'momentary') {
        _bindMomentary(element, cmd, binding);
        continue;
      }
      (function (capturedCmd) {
        var eventName = TRIGGER_MAP[capturedCmd.trigger || 'onClick'];
        if (!eventName) {
          _diagnose('trigger-unsupported', capturedCmd, capturedCmd.trigger);
          return;
        }
        _listen(element, eventName, function () { execute(element, capturedCmd); }, binding.listeners);
      })(cmd);
    }
  }

  function unbind(element) {
    var binding = element ? _bindings.get(element) : null;
    if (!binding) return;
    for (var i = 0; i < binding.momentary.length; i++) {
      var active = binding.momentary[i].active;
      if (active && typeof active.release === 'function') active.release();
    }
    _removeListeners(binding.listeners);
    _bindings.delete(element);
    if (element.dataset) delete element.dataset.scadaCommandBoundConfig;
  }

  function dispose(container) {
    if (!container) return;
    if (_bindings.has(container)) unbind(container);
    if (typeof container.querySelectorAll !== 'function') return;
    var elements = container.querySelectorAll('[data-scada-command-config]');
    for (var i = 0; i < elements.length; i++) unbind(elements[i]);
  }

  window.ScadaRuntime = window.ScadaRuntime || {};
  window.ScadaRuntime.CommandDispatcher = {
    bind: bind,
    unbind: unbind,
    dispose: dispose,
    execute: execute,
    dispatchIntent: dispatchIntent,
    _run: _run,
    intentVersion: INTENT_VERSION
  };
})();
