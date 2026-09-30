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

  // `#N` placeholder tokens (DEC-0052, contract §11b.1): 1..999, no leading zero.
  var PLACEHOLDER_EXACT = /^#([1-9][0-9]{0,2})$/;
  var PLACEHOLDER_IN_TEXT = /(\\?)#([0-9]{1,3})(?![0-9A-Fa-f])/g;

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

  /**
   * Collects every `tagName` (tagRef) and string `value` (literalString) carried by a serialized
   * expression AST, so injection screening can inspect them before any binding is trusted.
   */
  function _astInjectionStrings(node, out) {
    if (!node || typeof node !== 'object') {
      return;
    }
    if (node.type === 'tagRef' && typeof node.tagName === 'string') {
      out.push(node.tagName);
    }
    if (node.type === 'literalString' && typeof node.value === 'string') {
      out.push(node.value);
    }
    for (var key in node) {
      if (!Object.prototype.hasOwnProperty.call(node, key)) continue;
      var value = node[key];
      if (value && typeof value === 'object') {
        if (Array.isArray(value)) {
          for (var i = 0; i < value.length; i++) {
            _astInjectionStrings(value[i], out);
          }
        } else {
          _astInjectionStrings(value, out);
        }
      }
    }
  }

  /** True when any string value carried by an `ExpressionAst` matches an injection pattern. */
  function _isAstInjection(ast) {
    var strings = [];
    _astInjectionStrings(ast, strings);
    for (var i = 0; i < strings.length; i++) {
      if (_isInjection(strings[i])) {
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
   * True only for a definition compiled from a parameter-sheet `QuickWindow` page: its members are
   * anonymous placeholder tokens (`#1`..`#999`) rather than human-readable names. Mount-time `#N`
   * substitution runs only for such a definition — a pre-existing DEC-0050 quick window with named
   * ports (e.g. `Speed`) mounts exactly as before, with no substitution and no possible refusal, so a
   * literal `#N` in its own text or markup (e.g. a label "Pompe #1") is left untouched.
   */
  function _hasPlaceholderMembers(definition) {
    var members = _members(definition);
    for (var i = 0; i < members.length; i++) {
      if (PLACEHOLDER_EXACT.test(members[i] && members[i].Name)) {
        return true;
      }
    }
    return false;
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
      if (_isInjection(binding.LiteralValue) || _isInjection(binding.Expression) || _isInjection(binding.TagId) ||
          _isAstInjection(binding.ExpressionAst)) {
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
        expression: bound.Expression,
        expressionAst: bound.ExpressionAst || null
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
      substitutionEnabled: _hasPlaceholderMembers(resolution.definition),
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

  // ── #N placeholder substitution (DEC-0052, contract §11b.1) ───────────────
  //
  // A QuickWindowDefinition compiled from a parameter-sheet page keeps its `#N` tokens (`#1` to
  // `#999`) in its Content. `substituteInstance` rewrites every one of them, once per instance, from
  // the resolved ports of that instance's invocation, before the shared page runtime initializes.
  // `style` attributes are never read nor written by this pass: a hex colour like `#0F2A30` is never
  // searched for a placeholder there.

  function _refuse(code, detail) {
    _diagnose(code, detail);
    return { ok: false, code: code, detail: detail };
  }

  /**
   * Resolves a `#N` placeholder to a tag id, or the reason it cannot be used as a tag. `site` is
   * `'read'` for a read site (`data-scada-read-tag`, `readVariable.tagId`, a command's `readTagId`)
   * and `'write'` for a write site, so the refusal names the site that needed the tag.
   */
  function _tagFor(ports, name, site) {
    var port = ports[name];
    if (!port || !port.available) return { error: 'placeholder-unbound' };
    if (port.sourceKind !== 'Tag' || !port.tagId) {
      return { error: site === 'read' ? 'placeholder-read-not-tag' : 'placeholder-write-not-tag' };
    }
    return { tagId: port.tagId };
  }

  /** Builds the AST node a bound port substitutes for a `#N` tagRef inside an expression AST. */
  function _nodeFor(port) {
    if (port.sourceKind === 'Tag') return { type: 'tagRef', tagName: port.tagId };
    if (port.sourceKind === 'Literal') return { type: 'literalString', value: String(port.literalValue) };
    return port.expressionAst;
  }

  /**
   * Walks a serialized expression AST, substituting every `#N` tagRef node from its bound port.
   * Returns `{ node }` on success or `{ error, detail }` on refusal — never throws and never mutates
   * the input node (a fresh copy is built so a refusal never leaves a partially rewritten AST).
   */
  function _substituteAst(node, ports) {
    if (!node || typeof node !== 'object') return { node: node };
    if (node.type === 'tagRef' && PLACEHOLDER_EXACT.test(node.tagName || '')) {
      var port = ports[node.tagName];
      if (!port || !port.available) return { error: 'placeholder-unbound', detail: node.tagName };
      if (port.sourceKind === 'Expression' && !port.expressionAst) {
        return { error: 'placeholder-expression-unavailable', detail: node.tagName };
      }
      return { node: _nodeFor(port) };
    }
    var copy = {};
    for (var key in node) {
      if (!Object.prototype.hasOwnProperty.call(node, key)) continue;
      var value = node[key];
      if (value && typeof value === 'object') {
        if (Array.isArray(value)) {
          var items = [];
          for (var i = 0; i < value.length; i++) {
            var item = _substituteAst(value[i], ports);
            if (item.error) return item;
            items.push(item.node);
          }
          copy[key] = items;
        } else {
          var child = _substituteAst(value, ports);
          if (child.error) return child;
          copy[key] = child.node;
        }
      } else {
        copy[key] = value;
      }
    }
    return { node: copy };
  }

  /** True when a text node sits inside a `<style>` or `<script>` element (HTML or SVG, case-insensitive). */
  function _hasStyleOrScriptAncestor(node) {
    var ancestor = node.parentNode;
    while (ancestor) {
      if (ancestor.tagName) {
        var tag = String(ancestor.tagName).toUpperCase();
        if (tag === 'STYLE' || tag === 'SCRIPT') return true;
      }
      ancestor = ancestor.parentNode;
    }
    return false;
  }

  /** Parses a `data-scada-*-config` attribute as a non-null JSON object, or the refusal reason. */
  function _parseConfig(raw) {
    var parsed;
    try {
      parsed = JSON.parse(raw);
    } catch (e) {
      return { error: 'placeholder-config-invalid' };
    }
    if (!parsed || typeof parsed !== 'object') {
      return { error: 'placeholder-config-invalid' };
    }
    return { config: parsed };
  }

  /**
   * Substitutes every `#N` placeholder of a mounted quick-window instance's DOM, in a single pass,
   * from the resolved ports of that instance. Covers `data-scada-read-tag`/`data-scada-write-tag`
   * (exact-match attribute values only), `data-scada-state-config`/`data-scada-command-config` (parsed
   * as JSON, substituted, then re-serialized — never a raw string replacement on the JSON), and text
   * nodes (rewritten through `textContent` only, never `innerHTML`). `style` attributes are never
   * inspected, and a text node inside a `<style>`/`<script>` element (HTML or SVG) is never walked —
   * a bare CSS/JS `#` token there (e.g. `.cls-1{fill:#333;}`) is never mistaken for a placeholder.
   * Refuses (instead of ever rendering a raw `#N`) with one of `placeholder-unbound`,
   * `placeholder-read-not-tag` (a read site bound to a non-tag), `placeholder-write-not-tag` (a write
   * site bound to a non-tag), `placeholder-text-not-literal`, `placeholder-expression-unavailable`
   * (a `#N` bound to an `Expression` port whose `expressionAst` is null/absent) or
   * `placeholder-config-invalid` (a `data-scada-state-config`/`data-scada-command-config` attribute
   * that is not parseable JSON, or whose JSON is not an object). A refusal does not roll back
   * elements already rewritten earlier in the same pass — the host discards the whole instance root
   * on refusal (see `quick-window-host.js`), so a partial rewrite is never observable.
   *
   * @param {object} root  - The instance's mounted DOM root.
   * @param {object} ports - The instance's resolved ports, keyed by definition member name.
   * @returns {{ok: boolean, code: string, detail?: string}}
   */
  function substituteInstance(root, ports) {
    var elements = root.querySelectorAll(
      '[data-scada-read-tag],[data-scada-write-tag],[data-scada-state-config],[data-scada-command-config]');

    for (var e = 0; e < elements.length; e++) {
      var element = elements[e];

      var readTag = element.getAttribute('data-scada-read-tag');
      if (readTag !== null && PLACEHOLDER_EXACT.test(readTag)) {
        var readResolved = _tagFor(ports, readTag, 'read');
        if (readResolved.error) return _refuse(readResolved.error, readTag);
        element.setAttribute('data-scada-read-tag', readResolved.tagId);
      }

      var writeTag = element.getAttribute('data-scada-write-tag');
      if (writeTag !== null && PLACEHOLDER_EXACT.test(writeTag)) {
        var writeResolved = _tagFor(ports, writeTag, 'write');
        if (writeResolved.error) return _refuse(writeResolved.error, writeTag);
        element.setAttribute('data-scada-write-tag', writeResolved.tagId);
      }

      var stateConfigRaw = element.getAttribute('data-scada-state-config');
      if (stateConfigRaw !== null) {
        var parsedStateConfig = _parseConfig(stateConfigRaw);
        if (parsedStateConfig.error) return _refuse(parsedStateConfig.error, 'data-scada-state-config');
        var stateConfig = parsedStateConfig.config;
        var states = stateConfig.states || [];
        for (var s = 0; s < states.length; s++) {
          if (!states[s] || states[s].expression === undefined) continue;
          var substitutedExpression = _substituteAst(states[s].expression, ports);
          if (substitutedExpression.error) return _refuse(substitutedExpression.error, substitutedExpression.detail);
          states[s].expression = substitutedExpression.node;
        }
        if (stateConfig.readVariable && typeof stateConfig.readVariable.tagId === 'string' &&
            PLACEHOLDER_EXACT.test(stateConfig.readVariable.tagId)) {
          var readVariableResolved = _tagFor(ports, stateConfig.readVariable.tagId, 'read');
          if (readVariableResolved.error) return _refuse(readVariableResolved.error, stateConfig.readVariable.tagId);
          stateConfig.readVariable.tagId = readVariableResolved.tagId;
        }
        element.setAttribute('data-scada-state-config', JSON.stringify(stateConfig));
      }

      var commandConfigRaw = element.getAttribute('data-scada-command-config');
      if (commandConfigRaw !== null) {
        var parsedCommandConfig = _parseConfig(commandConfigRaw);
        if (parsedCommandConfig.error) return _refuse(parsedCommandConfig.error, 'data-scada-command-config');
        var commandConfig = parsedCommandConfig.config;
        var commands = commandConfig.commands || [];
        for (var c = 0; c < commands.length; c++) {
          var command = commands[c];
          if (!command) continue;
          if (typeof command.writeTagId === 'string' && PLACEHOLDER_EXACT.test(command.writeTagId)) {
            var writeCommandResolved = _tagFor(ports, command.writeTagId, 'write');
            if (writeCommandResolved.error) return _refuse(writeCommandResolved.error, command.writeTagId);
            command.writeTagId = writeCommandResolved.tagId;
          }
          if (typeof command.readTagId === 'string' && PLACEHOLDER_EXACT.test(command.readTagId)) {
            var readCommandResolved = _tagFor(ports, command.readTagId, 'read');
            if (readCommandResolved.error) return _refuse(readCommandResolved.error, command.readTagId);
            command.readTagId = readCommandResolved.tagId;
          }
        }
        element.setAttribute('data-scada-command-config', JSON.stringify(commandConfig));
      }
    }

    var walker = window.document.createTreeWalker(root, window.NodeFilter.SHOW_TEXT);
    var textNode = walker.nextNode();
    while (textNode) {
      if (_hasStyleOrScriptAncestor(textNode)) {
        textNode = walker.nextNode();
        continue;
      }
      var original = textNode.textContent;
      if (original && original.indexOf('#') !== -1) {
        var refusal = null;
        var replaced = original.replace(PLACEHOLDER_IN_TEXT, function (match, escape, digits) {
          if (refusal) return match;
          if (escape === '\\') return '#' + digits;
          var name = '#' + digits;
          var port = ports[name];
          if (!port || !port.available) {
            refusal = { code: 'placeholder-unbound', detail: name };
            return match;
          }
          if (port.sourceKind !== 'Literal') {
            refusal = { code: 'placeholder-text-not-literal', detail: name };
            return match;
          }
          return String(port.literalValue);
        });
        if (refusal) return _refuse(refusal.code, refusal.detail);
        textNode.textContent = replaced;
      }
      textNode = walker.nextNode();
    }

    return { ok: true, code: 'substituted' };
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
    if (root && context.substitutionEnabled) {
      var substituted = substituteInstance(root, context.ports || {});
      if (!substituted.ok) {
        disposeInstance(runtimeInstanceId);
        return { ok: false, code: substituted.code };
      }
    }
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
      // The evaluator walks the serialized AST, never the source text: `port.expression` is the
      // original text kept for round-tripping/UI, but only `expressionAst` is evaluable.
      if (!port.expressionAst) {
        return { ok: false, code: 'expression-unavailable' };
      }
      var evaluator = window.ScadaRuntime && window.ScadaRuntime.ExpressionEvaluator;
      if (!evaluator || typeof evaluator.walk !== 'function' ||
          typeof evaluator.resetError !== 'function' || typeof evaluator.hasError !== 'function') {
        return { ok: false, code: 'expression-unavailable' };
      }
      evaluator.resetError();
      var value = evaluator.walk(port.expressionAst, {});
      if (evaluator.hasError()) {
        return { ok: false, code: 'expression-error' };
      }
      return { ok: true, value: value };
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
    substituteInstance: substituteInstance,
    readPort: readPort,
    writePort: writePort,
    close: close,
    disposeInstance: disposeInstance,
    instanceOf: instanceOf,
    state: state,
    reset: reset
  };
})();
