(function () {
  'use strict';

  /**
   * Command dispatcher for the SCADA Builder V2 runtime.
   * Reads data-scada-command-config attributes from DOM elements, binds
   * triggers to DOM events, and dispatches commands (write-tag, navigation,
   * popup, open-url, back).
   *
   * Trigger map:
   *   OnClick      -> click
   *   OnRelease    -> mouseup
   *   OnHover      -> mouseenter
   *   OnHoverEnter -> mouseenter
   *   OnHoverExit  -> mouseleave
   */

  var TRIGGER_MAP = {
    OnClick: 'click',
    OnRelease: 'mouseup',
    OnHover: 'mouseenter',
    OnHoverEnter: 'mouseenter',
    OnHoverExit: 'mouseleave'
  };

  // ── bind ────────────────────────────────────────────────────────────────

  /**
   * Reads data-scada-command-config from the element, parses it, and binds
   * the configured triggers to DOM events. Passes the full command object
   * to the handler so it can access kind, writeTagId, readTagId, writeMode,
   * fixedValue, targetPageId, url, etc.
   *
   * @param {Element} element  - DOM element with data-scada-command-config.
   */
  function bind(element) {
    if (!element) {
      return;
    }

    var configRaw = element.getAttribute('data-scada-command-config');
    if (!configRaw) {
      return;
    }

    var config;
    try {
      config = JSON.parse(configRaw);
    } catch (e) {
      return;
    }

    if (!config || !Array.isArray(config.commands)) {
      return;
    }

    for (var i = 0; i < config.commands.length; i++) {
      var cmd = config.commands[i];
      if (!cmd) {
        continue;
      }

      var trigger = cmd.trigger || 'OnClick';
      var domEvent = TRIGGER_MAP[trigger] || 'click';

      // Capture cmd and domEvent in a per-iteration closure.
      (function (capturedCmd, capturedDomEvent) {
        element.addEventListener(capturedDomEvent, function (e) {
          execute(element, capturedCmd);
        });
      })(cmd, domEvent);
    }
  }

  // ── execute ─────────────────────────────────────────────────────────────

  /**
   * Executes a command object on an element. If the command has a confirmation
   * gate, the gate is checked before _run is invoked.
   *
   * @param {Element} element  - DOM element the command is bound to.
   * @param {object}  cmd      - The full command object from JSON.
   */
  function execute(element, cmd) {
    if (!element || !cmd) {
      return;
    }

    // Confirmation gate — check for confirmation.message on the command object
    if (cmd.confirmation && cmd.confirmation.message) {
      var confirmFn = window.ScadaRuntime && window.ScadaRuntime.showConfirmation;
      if (typeof confirmFn === 'function') {
        confirmFn(cmd.confirmation.message, function () {
          _run(element, cmd);
        });
        return;
      }
      // Fallback: native confirm
      if (!window.confirm(cmd.confirmation.message)) {
        return;
      }
    }

    _run(element, cmd);
  }

  // ── _run ────────────────────────────────────────────────────────────────

  /**
   * Internal command dispatch. Routes to the correct handler based on the
   * command kind.
   *
   * @param {Element} element  - DOM element the command is bound to.
   * @param {object}  cmd      - The full command object.
   */
  function _run(element, cmd) {
    if (!element || !cmd) {
      return;
    }

    switch (cmd.kind) {
      // ── WriteTag variants ──────────────────────────────────────────────
      case 'WriteTag':
        _writeTagCommand(cmd, element);
        break;

      // ── Navigation ─────────────────────────────────────────────────────
      case 'Navigate':
        _navigateCommand(cmd);
        break;

      // ── Popup commands ─────────────────────────────────────────────────
      case 'OpenPopup':
        _postMessage('openPopup', cmd.targetPageId, cmd.popupOptions);
        break;

      case 'ClosePopup':
        _postMessage('closePopup', cmd.targetPageId);
        break;

      case 'TogglePopup':
        _postMessage('togglePopup', cmd.targetPageId, cmd.popupOptions);
        break;

      // ── OpenUrl ────────────────────────────────────────────────────────
      case 'OpenUrl':
        if (cmd.url) {
          window.open(cmd.url, cmd.newTab ? '_blank' : '_self');
        }
        break;

      // ── Back ───────────────────────────────────────────────────────────
      case 'Back':
        window.history.back();
        break;

      default:
        break;
    }
  }

  // ── command helpers ─────────────────────────────────────────────────────

  /**
   * Executes a WriteTag command. Uses the command object's writeTagId,
   * writeMode, onValue/offValue, fixedValue, readTagId properties.
   *
   * @param {object} cmd - The full command object.
   */
  function _writeTagCommand(cmd, element) {
    if (!cmd || !cmd.writeTagId) {
      return;
    }

    var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
    if (!bridge) {
      return;
    }

    switch (cmd.writeMode) {
      case 'Momentary':
        // Momentary is handled as two commands: press (onValue) and release (offValue).
        // For a single click/dispatch, default to press cycle.
        bridge.writeTag(cmd.writeTagId, cmd.onValue, { phase: 'press' });
        break;

      case 'Toggle':
        var current = bridge.getTagValue(cmd.readTagId || cmd.writeTagId);
        var boolVal = !!(current && current !== '0' && current !== 'false');
        bridge.writeTag(cmd.writeTagId, boolVal ? '0' : '1', { mode: 'Toggle' });
        break;

      case 'SetFixed':
        bridge.writeTag(cmd.writeTagId, cmd.fixedValue, { mode: 'SetFixed' });
        break;

      case 'SetFromInput':
        var input = element ? element.querySelector('input, textarea') : null;
        if (input) {
          bridge.writeTag(cmd.writeTagId, input.value, { mode: 'SetFromInput' });
        }
        break;

      default:
        bridge.writeTag(cmd.writeTagId, cmd.fixedValue, { mode: 'SetFixed' });
        break;
    }
  }

  /**
   * Executes a Navigate command. Uses the command object's targetPageId.
   *
   * @param {object} cmd - The full command object.
   */
  function _navigateCommand(cmd) {
    if (!cmd || !cmd.targetPageId) {
      return;
    }

    window.postMessage({
      source: 'scada-builder-v2',
      action: 'navigate',
      pageId: cmd.targetPageId
    }, '*');
  }

  function _postMessage(action, pageId, options) {
    // Flat shape — matches _navigateCommand and the TF100Web host listener
    // (visualisation_import.js reads msg.pageId / msg.options directly).
    window.postMessage({
      source: 'scada-builder-v2',
      action: action,
      pageId: pageId,
      options: options
    }, '*');
  }

  // ── public API ──────────────────────────────────────────────────────────

  window.ScadaRuntime = window.ScadaRuntime || {};

  window.ScadaRuntime.CommandDispatcher = {
    bind: bind,
    execute: execute,
    _run: _run
  };
})();
