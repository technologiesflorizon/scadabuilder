(function () {
  'use strict';

  /**
   * SCADA runtime entry point for the SCADA Builder V2 runtime.
   * Initializes pages and handles tag value changes by coordinating
   * all runtime modules.
   *
   * Dependencies: All other ScadaRuntime modules (Tasks 1-4).
   */

  var _version = '{{RUNTIME_VERSION}}';

  /**
   * Initializes a page within the given container.
   * Calls StateEngine.initPage, binds CommandDispatcher on all
   * [data-scada-command-config] elements, watches inputs via
   * InputEditGuard.watch, and dispatches 'scada-builder-page-ready'.
   *
   * @param {Element} container - The DOM container for the page.
   * @param {string}  pageId    - Unique page identifier.
   */
  function initPage(container, pageId) {
    if (!container) {
      return;
    }

    // Initialize state engine
    if (window.ScadaRuntime && window.ScadaRuntime.StateEngine) {
      window.ScadaRuntime.StateEngine.initPage(container, pageId);
    }

    if (window.ScadaRuntime && window.ScadaRuntime.ActionDispatcher) {
      window.ScadaRuntime.ActionDispatcher.initPage(container, pageId);
    }

    // Bind command dispatchers
    var commandElements = container.querySelectorAll('[data-scada-command-config]');
    for (var i = 0; i < commandElements.length; i++) {
      if (window.ScadaRuntime && window.ScadaRuntime.CommandDispatcher) {
        window.ScadaRuntime.CommandDispatcher.bind(commandElements[i]);
      }
    }

    // Watch inputs via InputEditGuard
    var inputContainers = container.querySelectorAll('[data-scada-read-tag], [data-scada-write-tag]');
    for (var j = 0; j < inputContainers.length; j++) {
      if (window.ScadaRuntime && window.ScadaRuntime.InputEditGuard) {
        window.ScadaRuntime.InputEditGuard.watch(inputContainers[j]);
      }
    }

    // Dispatch page-ready event
    container.dispatchEvent(new CustomEvent('scada-builder-page-ready', {
      bubbles: true,
      detail: { pageId: pageId }
    }));
  }

  function disposePage(container) {
    if (!container) {
      return;
    }
    if (window.ScadaRuntime && window.ScadaRuntime.CommandDispatcher &&
        typeof window.ScadaRuntime.CommandDispatcher.dispose === 'function') {
      window.ScadaRuntime.CommandDispatcher.dispose(container);
    }
    if (window.ScadaRuntime && window.ScadaRuntime.ActionDispatcher &&
        typeof window.ScadaRuntime.ActionDispatcher.dispose === 'function') {
      window.ScadaRuntime.ActionDispatcher.dispose(container);
    }
    if (window.ScadaRuntime && window.ScadaRuntime.InputEditGuard &&
        typeof window.ScadaRuntime.InputEditGuard.dispose === 'function') {
      window.ScadaRuntime.InputEditGuard.dispose(container);
    }
  }

  /**
   * Handles tag value changes from the host.
   * Updates TagBridge values and re-evaluates all state-config elements.
   *
   * @param {object} tagValues - Map of tagId -> value.
   */
  function onTagValuesChanged(tagValues) {
    if (!tagValues || typeof tagValues !== 'object') {
      return;
    }

    // Update TagBridge values
    if (window.ScadaRuntime && window.ScadaRuntime.TagBridge) {
      window.ScadaRuntime.TagBridge.setValues(tagValues);
    }

    // Evaluate all state-config elements
    var stateElements = document.querySelectorAll('[data-scada-state-config]');
    for (var i = 0; i < stateElements.length; i++) {
      if (window.ScadaRuntime && window.ScadaRuntime.StateEngine) {
        window.ScadaRuntime.StateEngine.evaluate(stateElements[i], tagValues);
      }
    }
  }

  // ── public API ──────────────────────────────────────────────────────────

  window.ScadaRuntime = window.ScadaRuntime || {};

  window.ScadaRuntime.version = _version;
  window.ScadaRuntime.initPage = initPage;
  window.ScadaRuntime.disposePage = disposePage;
  window.ScadaRuntime.onTagValuesChanged = onTagValuesChanged;
})();
