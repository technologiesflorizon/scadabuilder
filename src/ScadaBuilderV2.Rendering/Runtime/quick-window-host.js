(function () {
  'use strict';

  /**
   * Quick-window host manager for the SCADA Builder V2 editor preview.
   *
   * Adapted from the frozen Phase 0 isolation prototype (PrototypeRevision 1.0.2) so a single
   * semantics exists: SinglePerDefinition, monotonic generations, stale-hydration rejection,
   * root-scoped DOM/CSS isolation, `X` / `Escape` / `Self` close and cascade dispose.
   *
   * It never owns a second runtime: mounting delegates to ScadaRuntime.initPage and disposal to
   * ScadaRuntime.disposePage, and every value read or written goes through the shared TagBridge.
   *
   * Decisions: DEC-0050, FR-020, FR-021, FR-022, FR-UI-07, FR-UI-08, FR-UI-09.
   * Tests: tests/runtime-js/quick-window-host.test.mjs,
   *        tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPreviewTests.cs.
   */

  var _generation = 0;
  var _active = {};        // definitionKey -> instance
  var _stack = [];         // ordered open stack: Page -> A -> B
  var _backdrop = null;
  var _hostRoot = null;

  function doc() {
    return typeof document !== 'undefined' ? document : null;
  }

  function hostRoot() {
    return _hostRoot || (doc() ? doc().body : null);
  }

  function setHostRoot(element) {
    _hostRoot = element || null;
  }

  function namespaceFor(definitionKey) {
    return 'qw-' + String(definitionKey).replace(/-/g, '').slice(0, 8).toLowerCase();
  }

  function depthOf(definitionKey) {
    for (var i = 0; i < _stack.length; i++) {
      if (_stack[i].definitionKey === definitionKey) {
        return i + 1;
      }
    }
    return 0;
  }

  /** Fail-closed nesting rules: no cycle, never deeper than Page -> A -> B. */
  function validateOpen(definitionKey, parentDefinitionKey) {
    if (!parentDefinitionKey) {
      return null;
    }
    if (definitionKey === parentDefinitionKey) {
      return 'cycle';
    }
    for (var i = 0; i < _stack.length; i++) {
      if (_stack[i].definitionKey === definitionKey) {
        return 'cycle';
      }
    }
    return depthOf(parentDefinitionKey) >= 2 ? 'depth-exceeded' : null;
  }

  function ensureBackdrop() {
    var document_ = doc();
    if (_backdrop || !document_) {
      return _backdrop;
    }
    _backdrop = document_.createElement('div');
    _backdrop.setAttribute('class', 'qw-backdrop');
    _backdrop.setAttribute('data-qw-backdrop', 'true');
    // A backdrop click never closes a quick window (FR-UI-07).
    hostRoot().appendChild(_backdrop);
    return _backdrop;
  }

  function releaseBackdrop() {
    if (_stack.length > 0 || !_backdrop) {
      return;
    }
    if (_backdrop.parentNode) {
      _backdrop.parentNode.removeChild(_backdrop);
    }
    _backdrop = null;
  }

  function bringToFront(instance) {
    if (instance.root && instance.root.parentNode) {
      instance.root.parentNode.appendChild(instance.root);
    }
    focus(instance.frame);
  }

  function focus(element) {
    if (element && typeof element.focus === 'function') {
      try {
        element.focus();
      } catch (error) {
        /* focus is best effort */
      }
    }
  }

  function cloneTemplate(templateId) {
    var document_ = doc();
    if (!document_) {
      return null;
    }
    var template = document_.getElementById(templateId);
    if (!template) {
      return null;
    }
    var host = document_.createElement('div');
    host.innerHTML = template.innerHTML !== undefined ? template.innerHTML : '';
    return host.children && host.children.length ? host.children[0] : null;
  }

  function applyTestValues(values) {
    if (!values || !window.ScadaRuntime || !window.ScadaRuntime.TagBridge) {
      return;
    }
    window.ScadaRuntime.TagBridge.setValues(values);
    if (typeof window.ScadaRuntime.onTagValuesChanged === 'function') {
      window.ScadaRuntime.onTagValuesChanged(values);
    }
  }

  /**
   * Opens one instance of a definition.
   *
   * @param {object} request - {definitionKey, invocationKey, parentDefinitionKey, templateId,
   *                            createRoot, testValues}
   * @returns {object} {reused, generation, runtimeInstanceId, instance}
   */
  function open(request) {
    if (!request || !request.definitionKey || !request.invocationKey) {
      throw new Error('quick-window.open requires a definitionKey and an invocationKey');
    }

    var rejection = validateOpen(request.definitionKey, request.parentDefinitionKey);
    if (rejection) {
      var error = new Error(rejection);
      error.code = rejection;
      throw error;
    }

    var existing = _active[request.definitionKey];
    if (existing) {
      if (existing.invocationKey === request.invocationKey) {
        // Same invocation of the same definition is brought to front, never recreated.
        bringToFront(existing);
        return { reused: true, generation: existing.generation, runtimeInstanceId: existing.runtimeInstanceId, instance: existing };
      }
      // Another invocation of the same definition closes and disposes the current context first.
      close(request.definitionKey);
    }

    var generation = ++_generation;
    var ns = namespaceFor(request.definitionKey);
    var runtimeInstanceId = 'qw-inst-' + generation + '-' + ns;
    var root = typeof request.createRoot === 'function'
      ? request.createRoot()
      : cloneTemplate(request.templateId);
    if (!root) {
      throw new Error('quick-window.open could not materialize the instance root');
    }

    root.setAttribute('data-qw-def', ns);
    root.setAttribute('data-qw-inv', String(request.invocationKey));
    root.setAttribute('data-qw-inst', runtimeInstanceId);
    root.setAttribute('data-qw-generation', String(generation));

    var instance = {
      definitionKey: request.definitionKey,
      invocationKey: request.invocationKey,
      parentDefinitionKey: request.parentDefinitionKey || null,
      runtimeInstanceId: runtimeInstanceId,
      generation: generation,
      namespace: ns,
      state: 'Mounting',
      root: root,
      frame: null,
      listeners: [],
      focusReturn: doc() ? doc().activeElement : null,
      disposed: false
    };

    ensureBackdrop();
    hostRoot().appendChild(root);
    _active[request.definitionKey] = instance;
    _stack.push({ definitionKey: request.definitionKey, runtimeInstanceId: runtimeInstanceId });

    instance.frame = root.querySelector('.qw-frame') || root;
    var closeButton = root.querySelector('[data-qw-close]');
    if (closeButton) {
      var onClose = function () { close(instance.definitionKey); };
      closeButton.addEventListener('click', onClose);
      instance.listeners.push({ target: closeButton, event: 'click', handler: onClose });
    }

    var onKeyDown = function (event) {
      if (event && event.key === 'Escape') {
        close(instance.definitionKey);
      }
    };
    instance.frame.addEventListener('keydown', onKeyDown);
    instance.listeners.push({ target: instance.frame, event: 'keydown', handler: onKeyDown });

    // The mount is delegated to the shared runtime: no second state engine, dispatcher nor poller.
    instance.state = 'Hydrating';
    if (window.ScadaRuntime && typeof window.ScadaRuntime.initPage === 'function') {
      window.ScadaRuntime.initPage(root, runtimeInstanceId);
    }

    // Stale hydration: another generation of the same definition won during mounting.
    var current = _active[instance.definitionKey];
    if (!current || current.generation !== generation) {
      disposeInstance(instance);
      var stale = new Error('stale-hydration-rejected');
      stale.code = 'stale-hydration-rejected';
      throw stale;
    }

    applyTestValues(request.testValues);
    instance.state = 'Active';
    focus(instance.frame);
    return { reused: false, generation: generation, runtimeInstanceId: runtimeInstanceId, instance: instance };
  }

  /** Closes one definition and cascades to every context opened above it. */
  function close(definitionKey) {
    var instance = _active[definitionKey];
    if (!instance) {
      return false;
    }
    var index = -1;
    for (var i = 0; i < _stack.length; i++) {
      if (_stack[i].definitionKey === definitionKey) {
        index = i;
        break;
      }
    }
    if (index >= 0) {
      var children = _stack.slice(index + 1);
      for (var c = children.length - 1; c >= 0; c--) {
        disposeInstance(_active[children[c].definitionKey]);
      }
    }
    disposeInstance(instance);
    return true;
  }

  /** Closes the quick window owning the given element: the `Self` target of CloseQuickWindow. */
  function closeSelf(element) {
    var node = element;
    while (node && (!node.getAttribute || !node.getAttribute('data-qw-inst'))) {
      node = node.parentNode;
    }
    if (!node) {
      return false;
    }
    var instanceId = node.getAttribute('data-qw-inst');
    for (var key in _active) {
      if (Object.prototype.hasOwnProperty.call(_active, key) && _active[key].runtimeInstanceId === instanceId) {
        return close(key);
      }
    }
    return false;
  }

  function disposeInstance(instance) {
    if (!instance || instance.disposed) {
      return;
    }
    instance.state = 'Closing';
    instance.disposed = true;

    for (var i = 0; i < instance.listeners.length; i++) {
      var entry = instance.listeners[i];
      try {
        entry.target.removeEventListener(entry.event, entry.handler);
      } catch (error) {
        /* a detached target is already clean */
      }
    }
    instance.listeners.length = 0;

    if (instance.root) {
      if (window.ScadaRuntime && typeof window.ScadaRuntime.disposePage === 'function') {
        window.ScadaRuntime.disposePage(instance.root);
      }
      if (instance.root.parentNode) {
        instance.root.parentNode.removeChild(instance.root);
      }
    }
    instance.root = null;
    instance.frame = null;

    if (_active[instance.definitionKey] === instance) {
      delete _active[instance.definitionKey];
    }
    var remaining = [];
    for (var s = 0; s < _stack.length; s++) {
      if (_stack[s].runtimeInstanceId !== instance.runtimeInstanceId) {
        remaining.push(_stack[s]);
      }
    }
    _stack = remaining;
    releaseBackdrop();
    focus(instance.focusReturn);
    instance.state = 'Disposed';
  }

  function disposeAll() {
    var keys = [];
    for (var key in _active) {
      if (Object.prototype.hasOwnProperty.call(_active, key)) {
        keys.push(key);
      }
    }
    for (var i = 0; i < keys.length; i++) {
      disposeInstance(_active[keys[i]]);
    }
    _generation = 0;
  }

  /** Returns the current host state for the editor test bench and the conformance fixtures. */
  function state() {
    var instances = [];
    for (var i = 0; i < _stack.length; i++) {
      var entry = _stack[i];
      var instance = _active[entry.definitionKey];
      if (!instance) {
        continue;
      }
      instances.push({
        definitionKey: instance.definitionKey,
        invocationKey: instance.invocationKey,
        runtimeInstanceId: instance.runtimeInstanceId,
        generation: instance.generation,
        namespace: instance.namespace,
        state: instance.state,
        depth: i + 1
      });
    }
    return { generation: _generation, instances: instances, hasBackdrop: !!_backdrop };
  }

  // ── public API ──────────────────────────────────────────────────────────

  window.ScadaRuntime = window.ScadaRuntime || {};

  window.ScadaRuntime.QuickWindowHost = {
    setHostRoot: setHostRoot,
    namespaceFor: namespaceFor,
    open: open,
    close: close,
    closeSelf: closeSelf,
    disposeAll: disposeAll,
    applyTestValues: applyTestValues,
    state: state
  };
})();
