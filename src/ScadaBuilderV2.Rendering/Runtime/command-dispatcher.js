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
   * the configured triggers to DOM events.
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

      (function (el, command, phase) {
        el.addEventListener(domEvent, function (e) {
          execute(el, command, phase);
        });
      })(element, cmd.command, cmd.phase);
    }
  }

  // ── execute ─────────────────────────────────────────────────────────────

  /**
   * Executes a command on an element. If the command has a confirmation gate,
   * the gate is checked before _run is invoked.
   *
   * @param {Element} element  - DOM element the command is bound to.
   * @param {string}  command  - Command name (WriteTag, Navigate, OpenPopup, etc.).
   * @param {string}  [phase]  - Optional phase (Momentary, Toggle, SetFixed, SetFromInput).
   */
  function execute(element, command, phase) {
    if (!element || !command) {
      return;
    }

    // Confirmation gate — check for data-scada-confirm attribute
    var confirmMessage = element.getAttribute('data-scada-confirm');
    if (confirmMessage) {
      if (!window.confirm(confirmMessage)) {
        return;
      }
    }

    _run(element, command, phase);
  }

  // ── _run ────────────────────────────────────────────────────────────────

  /**
   * Internal command dispatch. Routes to the correct handler based on the
   * command name.
   *
   * @param {Element} element  - DOM element the command is bound to.
   * @param {string}  command  - Command name.
   * @param {string}  [phase]  - Optional phase.
   */
  function _run(element, command, phase) {
    if (!element || !command) {
      return;
    }

    switch (command) {
      // ── WriteTag variants ──────────────────────────────────────────────
      case 'WriteTag':
        _writeTagCommand(element, phase);
        break;

      // ── Navigation ─────────────────────────────────────────────────────
      case 'Navigate':
        _navigateCommand(element);
        break;

      // ── Popup commands ─────────────────────────────────────────────────
      case 'OpenPopup':
        _postMessage('openPopup', _getCommandConfig(element));
        break;

      case 'ClosePopup':
        _postMessage('closePopup', _getCommandConfig(element));
        break;

      case 'TogglePopup':
        _postMessage('togglePopup', _getCommandConfig(element));
        break;

      // ── OpenUrl ────────────────────────────────────────────────────────
      case 'OpenUrl':
        _openUrlCommand(element);
        break;

      // ── Back ───────────────────────────────────────────────────────────
      case 'Back':
        _postMessage('back', null);
        break;

      default:
        break;
    }
  }

  // ── command helpers ─────────────────────────────────────────────────────

  function _getCommandConfig(element) {
    var configRaw = element.getAttribute('data-scada-command-config');
    if (!configRaw) {
      return null;
    }
    try {
      return JSON.parse(configRaw);
    } catch (e) {
      return null;
    }
  }

  function _writeTagCommand(element, phase) {
    var config = _getCommandConfig(element);
    if (!config || !config.tagId) {
      return;
    }

    var value;
    switch (phase) {
      case 'Momentary':
        value = 1;
        break;
      case 'Toggle':
        value = null; // toggled value determined by host
        break;
      case 'SetFixed':
        value = config.fixedValue;
        break;
      case 'SetFromInput':
        value = config.inputValue;
        break;
      default:
        value = config.fixedValue;
        break;
    }

    _postMessage('writeTag', {
      tagId: config.tagId,
      value: value,
      phase: phase
    });
  }

  function _navigateCommand(element) {
    var config = _getCommandConfig(element);
    if (!config || !config.pageId) {
      return;
    }

    _postMessage('navigate', {
      pageId: config.pageId
    });
  }

  function _openUrlCommand(element) {
    var config = _getCommandConfig(element);
    if (!config || !config.url) {
      return;
    }

    window.open(config.url, config.target || '_blank');
  }

  function _postMessage(action, payload) {
    if (window.parent && window.parent !== window) {
      window.parent.postMessage({
        type: 'scada-command',
        action: action,
        payload: payload
      }, '*');
    }
  }

  // ── public API ──────────────────────────────────────────────────────────

  window.ScadaRuntime = window.ScadaRuntime || {};

  window.ScadaRuntime.CommandDispatcher = {
    bind: bind,
    execute: execute,
    _run: _run
  };
})();
