(function () {
  'use strict';

  /**
   * Portable quick-window semantics of the shared SCADA Builder V2 runtime.
   *
   * This module owns no overlay, no chrome, no focus and no mounting policy: opening, closing and
   * focusing are delegated to the versioned host adapter through the existing runtime intent envelope.
   * What lives here is what must behave identically in every host: registry resolution, typed port
   * resolution, required-port validation, injection rejection before any subscription, instance-scoped
   * state and command evaluation, cross-instance write rejection and idempotent cleanup.
   *
   * Every quick-window capability stays `Blocked` until its own promotion: this module is shipped inert
   * and no package may declare a quick-window capability yet.
   *
   * Decisions: DEC-0047, DEC-0050, FR-007, FR-009, FR-010, FR-021, FR-022, FR-023.
   * Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §12.
   * Tests: tests/runtime-js/quick-window-runtime.test.mjs.
   */

  var CONTRACT_VERSION = '1.0';
  var INJECTION_PATTERNS = [
    /<\s*script/i,
    /<\s*\/\s*script/i,
    /javascript\s*:/i,
    /\.\.\//,
    /^\s*#/,
    /on[a-z]+\s*=/i
  ];

  var _definitions = {};   // definitionKey -> definition entry
  var _invocations = {};   // invocationKey -> invocation entry
  var _instances = {};     // runtimeInstanceId -> instance context
  var _latestByDefinition = {}; // definitionKey -> latest requested generation
  var _generation = 0;
  var _diagnostics = [];

  function _diagnose(code, detail) {
    _diagnostics.push({ code: code, detail: detail || '' });
    if (window.console && typeof window.console.warn === 'function') {
      window.console.warn('SCADA quick-window ' + code, detail || '');
    }
  }

  function _isInjection(value) {
    if (typeof value !== 'string' || value.length === 0) {
      return false;
    }
    for (var i = 0; i < INJECTION_PATTERNS.length; i++) {
      if (INJECTION_PATTERNS[i].test(value)) {
        return true;
      }
    }
    return false;
  }

  /** Indexes the manifest registries. Registry casing is PascalCase, as frozen by the package contract. */
  function loadRegistries(manifest) {
    _definitions = {};
    _invocations = {};
    if (!manifest || typeof manifest !== 'object') {
      return { definitions: 0, invocations: 0 };
    }

    var definitions = manifest.QuickWindows || [];
    for (var d = 0; d < definitions.length; d++) {
      var definition = definitions[d];
      if (definition && definition.DefinitionKey) {
        _definitions[definition.DefinitionKey] = definition;
      }
    }

    var invocations = manifest.QuickWindowInvocations || [];
    for (var i = 0; i < invocations.length; i++) {
      var invocation = invocations[i];
      if (invocation && invocation.InvocationKey) {
        _invocations[invocation.InvocationKey] = invocation;
      }
    }

    return { definitions: definitions.length, invocations: invocations.length };
  }

  function _members(definition) {
    return (definition && definition.InterfaceMembers) || [];
  }

  function _publicMembers(definition) {
    var result = [];
    var members = _members(definition);
    for (var i = 0; i < members.length; i++) {
      if (members[i].Family !== 'PrivateVariable' && members[i].Family !== 'PrivateConstant') {
        result.push(members[i]);
      }
    }
    return result;
  }

  /**
   * Resolves one invocation into a port map, or returns the reason why it cannot be resolved.
   * Injection is rejected here, before any subscription is created and before any write may happen.
   */
  function resolveInvocation(invocationKey, parentContext) {
    var invocation = _invocations[invocationKey];
    if (!invocation) {
      return { ok: false, code: 'invocation-missing', ports: {} };
    }

    var definition = _definitions[invocation.DefinitionKey];
    if (!definition) {
      return { ok: false, code: 'definition-missing', ports: {} };
    }

    if (invocation.InterfaceVersion !== definition.InterfaceVersion) {
      return { ok: false, code: 'interface-version-mismatch', ports: {} };
    }

    var bindings = invocation.Bindings || [];
    var bindingsByMember = {};
    for (var b = 0; b < bindings.length; b++) {
      var binding = bindings[b];
      if (_isInjection(binding.LiteralValue) || _isInjection(binding.Expression) || _isInjection(binding.TagId)) {
        // Rejected before subscription: no read is registered and no write may ever be emitted.
        return { ok: false, code: 'injection-rejected', ports: {}, memberKey: binding.MemberKey };
      }
      bindingsByMember[binding.MemberKey] = binding;
    }

    var ports = {};
    var members = _publicMembers(definition);
    for (var m = 0; m < members.length; m++) {
      var member = members[m];
      var bound = bindingsByMember[member.MemberKey];
      var absent = !bound || bound.SourceKind === 'None';
      if (absent && member.Required === true) {
        return { ok: false, code: 'required-port-unbound', ports: {}, memberKey: member.MemberKey };
      }

      if (absent) {
        // An optional unbound port is neutral: no subscription, no write, runtime value unavailable.
        ports[member.Name] = {
          memberKey: member.MemberKey,
          sourceKind: 'None',
          available: false,
          writable: false
        };
        continue;
      }

      if (bound.SourceKind === 'ParentPort') {
        var parentPort = parentContext && parentContext.portsByKey
          ? parentContext.portsByKey[bound.ParentMemberKey]
          : null;
        if (!parentPort) {
          return { ok: false, code: 'parent-port-missing', ports: {}, memberKey: member.MemberKey };
        }
        ports[member.Name] = {
          memberKey: member.MemberKey,
          sourceKind: 'ParentPort',
          available: parentPort.available,
          writable: parentPort.writable,
          tagId: parentPort.tagId,
          parentMemberKey: bound.ParentMemberKey
        };
        continue;
      }

      ports[member.Name] = {
        memberKey: member.MemberKey,
        sourceKind: bound.SourceKind,
        available: true,
        writable: bound.SourceKind === 'Tag' &&
          (member.Access === 'Write' || member.Access === 'ReadWrite'),
        tagId: bound.TagId,
        literalValue: bound.LiteralValue,
        expression: bound.Expression
      };
    }

    return { ok: true, code: 'resolved', definition: definition, invocation: invocation, ports: ports };
  }

  function _indexByKey(ports) {
    var byKey = {};
    for (var name in ports) {
      if (Object.prototype.hasOwnProperty.call(ports, name)) {
        byKey[ports[name].memberKey] = ports[name];
      }
    }
    return byKey;
  }

  /**
   * Asks the host to open one invocation. The shared runtime never draws a frame: it validates, resolves
   * and hands a fully resolved context to the versioned host adapter.
   */
  function open(invocationKey, options) {
    options = options || {};
    var parentContext = options.parentInstanceId ? _instances[options.parentInstanceId] : null;
    if (options.parentInstanceId && !parentContext) {
      _diagnose('parent-instance-missing', options.parentInstanceId);
      return { ok: false, code: 'parent-instance-missing' };
    }

    var resolution = resolveInvocation(invocationKey, parentContext);
    if (!resolution.ok) {
      _diagnose(resolution.code, invocationKey);
      return { ok: false, code: resolution.code, memberKey: resolution.memberKey };
    }

    // A cycle is reported as a cycle even when the same chain is also too deep: the operator needs the
    // precise reason, and the prototype frozen in Phase 0 classifies it the same way.
    var ancestor = parentContext;
    while (ancestor) {
      if (ancestor.definitionKey === resolution.invocation.DefinitionKey) {
        _diagnose('cycle-rejected', invocationKey);
        return { ok: false, code: 'cycle-rejected' };
      }
      ancestor = ancestor.parentInstanceId ? _instances[ancestor.parentInstanceId] : null;
    }

    var depth = parentContext ? parentContext.depth + 1 : 1;
    if (depth > 2) {
      _diagnose('depth-exceeded', invocationKey);
      return { ok: false, code: 'depth-exceeded' };
    }

    var generation = ++_generation;
    var runtimeInstanceId = 'qw-inst-' + generation + '-' + resolution.definition.Namespace;
    var context = {
      runtimeInstanceId: runtimeInstanceId,
      invocationKey: invocationKey,
      definitionKey: resolution.invocation.DefinitionKey,
      namespace: resolution.definition.Namespace,
      generation: generation,
      depth: depth,
      parentInstanceId: parentContext ? parentContext.runtimeInstanceId : null,
      ports: resolution.ports,
      portsByKey: _indexByKey(resolution.ports),
      state: 'Requested',
      root: null,
      subscriptions: []
    };
    _instances[runtimeInstanceId] = context;
    _latestByDefinition[context.definitionKey] = generation;

    var envelope = {
      source: 'scada-builder-v2',
      type: 'scada-runtime-intent',
      version: CONTRACT_VERSION,
      intent: {
        kind: 'openQuickWindow',
        invocationKey: invocationKey,
        definitionKey: context.definitionKey,
        namespace: context.namespace,
        runtimeInstanceId: runtimeInstanceId,
        generation: generation,
        relativePath: resolution.definition.RelativePath,
        cssRelativePath: resolution.definition.CssRelativePath,
        presentation: resolution.definition.PresentationDefaults || null,
        title: resolution.invocation.TitleOverride || null
      }
    };

    var adapter = window.ScadaRuntime && window.ScadaRuntime.HostAdapter;
    if (!adapter || typeof adapter.dispatchIntent !== 'function') {
      delete _instances[runtimeInstanceId];
      _diagnose('host-unavailable', invocationKey);
      return { ok: false, code: 'host-unavailable' };
    }

    adapter.dispatchIntent(envelope);
    return { ok: true, code: 'requested', runtimeInstanceId: runtimeInstanceId, generation: generation, ports: context.ports };
  }

  /**
   * Mounts one resolved instance into the DOM root the host created. Mounting delegates to the shared
   * page runtime: no second state engine, dispatcher, tag cache or poller is created.
   */
  function mountInstance(runtimeInstanceId, root) {
    var context = _instances[runtimeInstanceId];
    if (!context) {
      _diagnose('instance-missing', runtimeInstanceId);
      return { ok: false, code: 'instance-missing' };
    }

    var latest = _latestByDefinition[context.definitionKey];
    if (context.state === 'Requested' && latest !== undefined && latest !== context.generation) {
      // A newer generation of the same definition was requested while this one was mounting.
      disposeInstance(runtimeInstanceId);
      _diagnose('stale-hydration-rejected', runtimeInstanceId);
      return { ok: false, code: 'stale-hydration-rejected' };
    }

    context.root = root || null;
    context.state = 'Active';
    if (root && window.ScadaRuntime && typeof window.ScadaRuntime.initPage === 'function') {
      window.ScadaRuntime.initPage(root, runtimeInstanceId);
    }

    for (var name in context.ports) {
      if (!Object.prototype.hasOwnProperty.call(context.ports, name)) continue;
      var port = context.ports[name];
      if (port.sourceKind === 'Tag' && port.tagId) {
        context.subscriptions.push(port.tagId);
        if (window.ScadaRuntime && window.ScadaRuntime.TagBridge &&
            typeof window.ScadaRuntime.TagBridge.subscribe === 'function') {
          window.ScadaRuntime.TagBridge.subscribe(port.tagId, runtimeInstanceId);
        }
      }
    }

    return { ok: true, code: 'active', runtimeInstanceId: runtimeInstanceId };
  }

  /** Reads one port of one instance, honouring its own typed source only. */
  function readPort(runtimeInstanceId, portName) {
    var context = _instances[runtimeInstanceId];
    if (!context) return { ok: false, code: 'instance-missing' };
    var port = context.ports[portName];
    if (!port) return { ok: false, code: 'port-missing' };
    if (!port.available) return { ok: false, code: 'port-unavailable' };

    if (port.sourceKind === 'Literal') {
      return { ok: true, value: port.literalValue };
    }
    if (port.sourceKind === 'Expression') {
      var evaluator = window.ScadaRuntime && window.ScadaRuntime.ExpressionEvaluator;
      if (!evaluator || typeof evaluator.evaluate !== 'function') {
        return { ok: false, code: 'expression-unavailable' };
      }
      return { ok: true, value: evaluator.evaluate(port.expression) };
    }
    if (port.tagId && window.ScadaRuntime && window.ScadaRuntime.TagBridge) {
      return { ok: true, value: window.ScadaRuntime.TagBridge.getTagValue(port.tagId) };
    }
    return { ok: false, code: 'port-unavailable' };
  }

  /**
   * Writes one port of one instance. A write is accepted only through a writable port of that very
   * instance: no instance may ever write the mapping of another invocation.
   */
  function writePort(runtimeInstanceId, portName, value) {
    var context = _instances[runtimeInstanceId];
    if (!context) return { ok: false, code: 'instance-missing' };
    if (context.state !== 'Active') return { ok: false, code: 'instance-not-active' };

    var port = context.ports[portName];
    if (!port) return { ok: false, code: 'port-missing' };
    if (!port.writable || !port.tagId) {
      _diagnose('write-rejected', portName);
      return { ok: false, code: 'write-rejected' };
    }

    var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
    if (!bridge || typeof bridge.writeTag !== 'function') {
      return { ok: false, code: 'bridge-unavailable' };
    }

    bridge.writeTag(port.tagId, value, { owner: runtimeInstanceId });
    return { ok: true, code: 'written', tagId: port.tagId };
  }

  /** Disposes one instance. Calling it twice is a no-op: cleanup is idempotent. */
  function disposeInstance(runtimeInstanceId) {
    var context = _instances[runtimeInstanceId];
    if (!context) {
      return { ok: true, code: 'already-disposed' };
    }

    delete _instances[runtimeInstanceId];
    context.state = 'Disposed';

    if (context.root && window.ScadaRuntime && typeof window.ScadaRuntime.disposePage === 'function') {
      window.ScadaRuntime.disposePage(context.root);
    }

    var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
    if (bridge && typeof bridge.unsubscribeAll === 'function') {
      bridge.unsubscribeAll(runtimeInstanceId);
    }
    context.subscriptions.length = 0;
    context.root = null;
    return { ok: true, code: 'disposed' };
  }

  /** Asks the host to close one instance; `Self` resolves the instance owning an element. */
  function close(runtimeInstanceId) {
    var context = _instances[runtimeInstanceId];
    if (!context) {
      return { ok: false, code: 'instance-missing' };
    }

    var adapter = window.ScadaRuntime && window.ScadaRuntime.HostAdapter;
    if (adapter && typeof adapter.dispatchIntent === 'function') {
      adapter.dispatchIntent({
        source: 'scada-builder-v2',
        type: 'scada-runtime-intent',
        version: CONTRACT_VERSION,
        intent: {
          kind: 'closeQuickWindow',
          runtimeInstanceId: runtimeInstanceId,
          invocationKey: context.invocationKey,
          definitionKey: context.definitionKey
        }
      });
    }

    return disposeInstance(runtimeInstanceId);
  }

  /** Resolves the instance owning one element, for the `Self` close target. */
  function instanceOf(element) {
    var node = element;
    while (node && (!node.getAttribute || !node.getAttribute('data-qw-inst'))) {
      node = node.parentNode;
    }
    return node ? node.getAttribute('data-qw-inst') : null;
  }

  /** Returns the live instances, for host adapters and conformance probes. */
  function state() {
    var instances = [];
    for (var id in _instances) {
      if (!Object.prototype.hasOwnProperty.call(_instances, id)) continue;
      var context = _instances[id];
      instances.push({
        runtimeInstanceId: id,
        invocationKey: context.invocationKey,
        definitionKey: context.definitionKey,
        namespace: context.namespace,
        generation: context.generation,
        depth: context.depth,
        state: context.state,
        subscriptions: context.subscriptions.slice()
      });
    }
    return { generation: _generation, instances: instances, diagnostics: _diagnostics.slice() };
  }

  /** Clears every registry, instance and diagnostic. Used by hosts on navigation and by tests. */
  function reset() {
    for (var id in _instances) {
      if (Object.prototype.hasOwnProperty.call(_instances, id)) {
        disposeInstance(id);
      }
    }
    _definitions = {};
    _invocations = {};
    _instances = {};
    _latestByDefinition = {};
    _generation = 0;
    _diagnostics = [];
  }

  // ── public API ──────────────────────────────────────────────────────────

  window.ScadaRuntime = window.ScadaRuntime || {};

  window.ScadaRuntime.QuickWindow = {
    contractVersion: CONTRACT_VERSION,
    loadRegistries: loadRegistries,
    resolveInvocation: resolveInvocation,
    open: open,
    mountInstance: mountInstance,
    readPort: readPort,
    writePort: writePort,
    close: close,
    disposeInstance: disposeInstance,
    instanceOf: instanceOf,
    state: state,
    reset: reset
  };
})();
