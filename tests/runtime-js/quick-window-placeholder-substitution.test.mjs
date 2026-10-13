import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime, createFakeDom, parseFragment } from './harness.mjs';

/**
 * Tests for mount-time `#N` placeholder substitution (`ScadaRuntime.QuickWindow.substituteInstance`),
 * DEC-0052, contract §11b.1, and the gating/injection/readPort behaviour it depends on.
 */

const PORTS = {
  '#1': { sourceKind: 'Tag', available: true, tagId: 'tf100.mapping.196' },
  // The evaluator normalises the operator casing (`_canonicalOperator`), so `op` casing here is
  // indifferent to correctness.
  '#3': {
    sourceKind: 'Expression',
    available: true,
    expressionAst: {
      type: 'binary',
      op: 'Multiply',
      left: { type: 'tagRef', tagName: 'FAN_03_SPEED' },
      right: { type: 'literalNumber', value: 0.1 }
    }
  },
  '#4': { sourceKind: 'Literal', available: true, literalValue: 'Ventilateur 3' },
  '#5': { sourceKind: 'Tag', available: true, tagId: 'tf100.mapping.777' }
};

function loadQuickWindow() {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js', 'quick-window-runtime.js']);
  Object.assign(window, createFakeDom());
  return window;
}

// ── manifest helpers (open()/mountInstance() full-flow tests) ──────────────

function member(key, name, family, access, required = false) {
  return {
    MemberKey: key, Name: name, Family: family, DataType: 'Number',
    Access: access, Required: !!required, DefaultValue: null, Description: null
  };
}

/** A member named `#N`: the marker `_hasPlaceholderMembers` uses to gate substitution on. */
function placeholderMember(key, n, family, access, required = false) {
  return member(key, '#' + n, family, access, required);
}

function definition(key, namespace, members) {
  return {
    DefinitionKey: key, Code: namespace, DisplayName: namespace, InterfaceVersion: 1, Namespace: namespace,
    RelativePath: `${namespace}/${namespace}.html`, CssRelativePath: `${namespace}/css/${namespace}.css`,
    Width: 480, Height: 320, InterfaceMembers: members, PresentationDefaults: { Position: 'Center', Backdrop: true }
  };
}

function invocation(key, definitionKey, bindings, interfaceVersion = 1) {
  return {
    InvocationKey: key, DefinitionKey: definitionKey, InterfaceVersion: interfaceVersion, TitleOverride: null,
    OwnerPageKey: null, OwnerElementId: 'caller', OwnerCommandId: 'open', Bindings: bindings
  };
}

function binding(memberKey, sourceKind, extra = {}) {
  return {
    MemberKey: memberKey, SourceKind: sourceKind, TagId: null, LiteralValue: null,
    Expression: null, ExpressionAst: null, ParentMemberKey: null, ...extra
  };
}

function loadWithManifest(manifest) {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js', 'quick-window-runtime.js']);
  Object.assign(window, createFakeDom());
  const intents = [];
  window.ScadaRuntime.HostAdapter = { dispatchIntent(envelope) { intents.push(envelope); return true; } };
  window.ScadaRuntime.QuickWindow.loadRegistries(manifest);
  return { window, quickWindow: window.ScadaRuntime.QuickWindow, bridge: window.ScadaRuntime.TagBridge, intents };
}

// ── substituteInstance: attributes, JSON configs, text, style/script exclusion ──

test('read and write tag attributes equal to #N become the bound tag id', () => {
  const window = loadQuickWindow();
  const root = window.document.createElement('div');
  const el = window.document.createElement('div');
  el.setAttribute('data-scada-read-tag', '#1');
  el.setAttribute('data-scada-write-tag', '#1');
  root.appendChild(el);

  const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS);

  assert.equal(result.ok, true);
  assert.equal(el.getAttribute('data-scada-read-tag'), 'tf100.mapping.196');
  assert.equal(el.getAttribute('data-scada-write-tag'), 'tf100.mapping.196');
});

test('a write tag bound to a non-tag refuses the mount', () => {
  const window = loadQuickWindow();
  const root = window.document.createElement('div');
  const el = window.document.createElement('div');
  el.setAttribute('data-scada-write-tag', '#4'); // #4 is a Literal port, not Tag.
  root.appendChild(el);

  const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS);

  assert.equal(result.ok, false);
  assert.equal(result.code, 'placeholder-write-not-tag');
  // This fixture has only one element, so there is nothing that could have been rewritten before
  // the refusal — it is not a general "no partial mutation" guarantee. See the dedicated test
  // below ("a refusal does not roll back...") for what actually happens when an earlier element
  // was already rewritten.
  assert.equal(el.getAttribute('data-scada-write-tag'), '#4');
});

// Final review C1: every READ site bound to a non-tag refuses with its own code, distinct from the
// write refusal, so the diagnostic names what the operator actually did (read vs write).
function readSiteRoots(window, placeholder) {
  const make = () => window.document.createElement('div');
  const attr = make();
  attr.setAttribute('data-scada-read-tag', placeholder);
  const readVariable = make();
  readVariable.setAttribute('data-scada-state-config', JSON.stringify({ states: [], readVariable: { tagId: placeholder, displayFormat: null } }));
  const commandRead = make();
  commandRead.setAttribute('data-scada-command-config', JSON.stringify({ commands: [{ id: 'cmd', writeTagId: null, readTagId: placeholder }] }));
  return { 'data-scada-read-tag': attr, 'readVariable.tagId': readVariable, 'command readTagId': commandRead };
}

function writeSiteRoots(window, placeholder) {
  const make = () => window.document.createElement('div');
  const attr = make();
  attr.setAttribute('data-scada-write-tag', placeholder);
  const commandWrite = make();
  commandWrite.setAttribute('data-scada-command-config', JSON.stringify({ commands: [{ id: 'cmd', writeTagId: placeholder, readTagId: null }] }));
  const commandInterlock = make();
  commandInterlock.setAttribute('data-scada-command-config', JSON.stringify({ commands: [{ id: 'cmd', writeTagId: '#1', readTagId: null, interlockTagId: placeholder }] }));
  return { 'data-scada-write-tag': attr, 'command writeTagId': commandWrite, 'command interlockTagId': commandInterlock };
}

test('every read site bound to a Literal or an Expression refuses placeholder-read-not-tag', () => {
  for (const placeholder of ['#3', '#4']) { // #3 Expression, #4 Literal
    const window = loadQuickWindow();
    for (const [site, el] of Object.entries(readSiteRoots(window, placeholder))) {
      const root = window.document.createElement('div');
      root.appendChild(el);
      const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS);
      assert.equal(result.ok, false, `${site} ${placeholder}`);
      assert.equal(result.code, 'placeholder-read-not-tag', `${site} ${placeholder}`);
    }
  }
});

test('every write site bound to a Literal or an Expression keeps refusing placeholder-write-not-tag', () => {
  for (const placeholder of ['#3', '#4']) {
    const window = loadQuickWindow();
    for (const [site, el] of Object.entries(writeSiteRoots(window, placeholder))) {
      const root = window.document.createElement('div');
      root.appendChild(el);
      const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS);
      assert.equal(result.ok, false, `${site} ${placeholder}`);
      assert.equal(result.code, 'placeholder-write-not-tag', `${site} ${placeholder}`);
    }
  }
});

test('a command config interlockTagId #5 becomes the tag id, and an unbound one refuses the mount', () => {
  const window = loadQuickWindow();
  const root = window.document.createElement('div');
  const el = window.document.createElement('div');
  el.setAttribute('data-scada-command-config', JSON.stringify({
    commands: [{ id: 'cmd-1', name: 'Start', writeTagId: '#1', readTagId: null, interlockTagId: '#5' }]
  }));
  root.appendChild(el);

  assert.equal(window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS).ok, true);
  assert.equal(JSON.parse(el.getAttribute('data-scada-command-config')).commands[0].interlockTagId, 'tf100.mapping.777');

  const unboundRoot = window.document.createElement('div');
  const unbound = window.document.createElement('div');
  unbound.setAttribute('data-scada-command-config', JSON.stringify({
    commands: [{ id: 'cmd-2', writeTagId: '#1', readTagId: null, interlockTagId: '#9' }]
  }));
  unboundRoot.appendChild(unbound);
  const refused = window.ScadaRuntime.QuickWindow.substituteInstance(unboundRoot, PORTS);
  assert.equal(refused.ok, false);
  assert.equal(refused.code, 'placeholder-unbound');
});

test('every read site bound to a Tag is substituted without refusal', () => {
  const window = loadQuickWindow();
  for (const [site, el] of Object.entries(readSiteRoots(window, '#1'))) {
    const root = window.document.createElement('div');
    root.appendChild(el);
    const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS);
    assert.equal(result.ok, true, site);
  }
});

test('a state expression tagRef #3 is replaced by the bound AST and the JSON stays valid', () => {
  const window = loadQuickWindow();
  const root = window.document.createElement('div');
  const el = window.document.createElement('div');
  const stateConfig = {
    states: [
      {
        id: 'state-1',
        name: 'Running',
        enabled: true,
        expression: { type: 'tagRef', tagName: '#3' },
        effect: { opacity: 1 }
      }
    ],
    readVariable: { tagId: '#1', displayFormat: null }
  };
  el.setAttribute('data-scada-state-config', JSON.stringify(stateConfig));
  root.appendChild(el);

  const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS);

  assert.equal(result.ok, true);
  const parsed = JSON.parse(el.getAttribute('data-scada-state-config'));
  assert.deepEqual(parsed.states[0].expression, {
    type: 'binary',
    op: 'Multiply',
    left: { type: 'tagRef', tagName: 'FAN_03_SPEED' },
    right: { type: 'literalNumber', value: 0.1 }
  });
  assert.equal(parsed.readVariable.tagId, 'tf100.mapping.196');
  // Untouched fields survive the parse/re-serialize round trip unchanged.
  assert.equal(parsed.states[0].name, 'Running');
  assert.deepEqual(parsed.states[0].effect, { opacity: 1 });
});

test('a command config writeTagId #1 becomes the tag id', () => {
  const window = loadQuickWindow();
  const root = window.document.createElement('div');
  const el = window.document.createElement('div');
  const commandConfig = {
    commands: [
      { id: 'cmd-1', name: 'Start', writeTagId: '#1', readTagId: null }
    ]
  };
  el.setAttribute('data-scada-command-config', JSON.stringify(commandConfig));
  root.appendChild(el);

  const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS);

  assert.equal(result.ok, true);
  const parsed = JSON.parse(el.getAttribute('data-scada-command-config'));
  assert.equal(parsed.commands[0].writeTagId, 'tf100.mapping.196');
  assert.equal(parsed.commands[0].readTagId, null);
  assert.equal(parsed.commands[0].name, 'Start');
});

test('a text node #4 becomes the literal through textContent', () => {
  const window = loadQuickWindow();
  const root = window.document.createElement('div');
  const el = window.document.createElement('span');
  el.textContent = 'Label: #4, escaped: \\#2.';
  root.appendChild(el);

  const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS);

  assert.equal(result.ok, true);
  assert.equal(el.textContent, 'Label: Ventilateur 3, escaped: #2.');
});

test('a text node bound to a tag refuses the mount', () => {
  const window = loadQuickWindow();
  const root = window.document.createElement('div');
  const el = window.document.createElement('span');
  el.textContent = 'Value: #5';
  root.appendChild(el);

  const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS);

  assert.equal(result.ok, false);
  assert.equal(result.code, 'placeholder-text-not-literal');
  // Single-element fixture again: see "a refusal does not roll back..." for the general case.
  assert.equal(el.textContent, 'Value: #5');
});

test('an unbound placeholder refuses the mount instead of showing #N', () => {
  const window = loadQuickWindow();
  const root = window.document.createElement('div');
  const el = window.document.createElement('span');
  el.textContent = 'Unknown: #9';
  root.appendChild(el);

  const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS);

  assert.equal(result.ok, false);
  assert.equal(result.code, 'placeholder-unbound');
  assert.equal(el.textContent, 'Unknown: #9');
});

test('a refusal does not roll back elements already rewritten earlier in the same pass', () => {
  const window = loadQuickWindow();
  const root = window.document.createElement('div');
  const first = window.document.createElement('div');
  first.setAttribute('data-scada-read-tag', '#1');
  const second = window.document.createElement('div');
  second.setAttribute('data-scada-write-tag', '#4'); // #4 is Literal, not Tag: refuses.
  root.appendChild(first);
  root.appendChild(second);

  const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS);

  assert.equal(result.ok, false);
  assert.equal(result.code, 'placeholder-write-not-tag');
  // substituteInstance never rolls back an element it already rewrote before hitting a refusal
  // later in the pass. This is harmless only because mountInstance disposes the instance (and the
  // host discards/detaches the whole instance root — see quick-window-host.js disposeInstance) on
  // any refusal, so the partially-rewritten root is never shown to a user.
  assert.equal(first.getAttribute('data-scada-read-tag'), 'tf100.mapping.196');
});

test('style attributes with hex colours are never touched', () => {
  const window = loadQuickWindow();
  const root = window.document.createElement('div');
  const el = window.document.createElement('div');
  el.setAttribute('style', 'color:#0F2A30');
  el.setAttribute('data-scada-read-tag', '#1');
  root.appendChild(el);

  const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS);

  assert.equal(result.ok, true);
  assert.equal(el.getAttribute('style'), 'color:#0F2A30');
  assert.equal(el.getAttribute('data-scada-read-tag'), 'tf100.mapping.196');
});

test('text inside <style> or <script> (HTML or SVG, lowercase) is never walked', () => {
  const window = loadQuickWindow();
  const root = window.document.createElement('div');

  // Plain CSS/JS source containing bare `#NNN` tokens that would otherwise refuse the mount
  // (no port named '#333' or '#12' exists) if the TreeWalker ever visited them.
  const style = window.document.createElement('style');
  style.textContent = '.cls-1{fill:#333;}';
  root.appendChild(style);

  const script = window.document.createElement('script');
  script.textContent = "var token = '#12';";
  root.appendChild(script);

  const label = window.document.createElement('span');
  label.textContent = 'Nom: #4';
  root.appendChild(label);

  const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS);

  assert.equal(result.ok, true);
  assert.equal(style.textContent, '.cls-1{fill:#333;}');
  assert.equal(script.textContent, "var token = '#12';");
  assert.equal(label.textContent, 'Nom: Ventilateur 3');
});

test('malformed or null JSON in a state/command config refuses placeholder-config-invalid', () => {
  const window = loadQuickWindow();
  const cases = [
    ['data-scada-state-config', '{not json'],
    ['data-scada-state-config', 'null'],
    ['data-scada-command-config', '{not json'],
    ['data-scada-command-config', 'null']
  ];

  for (const [attr, value] of cases) {
    const root = window.document.createElement('div');
    const el = window.document.createElement('div');
    el.setAttribute(attr, value);
    root.appendChild(el);

    const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS);

    assert.equal(result.ok, false, `${attr}=${value}`);
    assert.equal(result.code, 'placeholder-config-invalid', `${attr}=${value}`);
  }
});

test('a #N tagRef bound to an Expression port with no AST refuses placeholder-expression-unavailable', () => {
  const window = loadQuickWindow();
  const root = window.document.createElement('div');
  const el = window.document.createElement('div');
  const stateConfig = {
    states: [{ id: 's', name: 'S', enabled: true, expression: { type: 'tagRef', tagName: '#6' }, effect: {} }]
  };
  el.setAttribute('data-scada-state-config', JSON.stringify(stateConfig));
  root.appendChild(el);

  const ports = Object.assign({}, PORTS, {
    '#6': { sourceKind: 'Expression', available: true, expressionAst: null }
  });

  const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, ports);

  assert.equal(result.ok, false);
  assert.equal(result.code, 'placeholder-expression-unavailable');
});

test('two invocations of the same placeholder definition mount independently from the same template', () => {
  const TEMPLATE = '<div><div data-scada-read-tag="#1"></div><span>Nom: #4</span></div>';
  const { window, quickWindow } = loadWithManifest({
    QuickWindows: [definition('def-reuse', 'qw-reuse', [
      placeholderMember('member-1', 1, 'ReadState', 'Read', true),
      placeholderMember('member-4', 4, 'PublicParameter', 'Read', true)
    ])],
    QuickWindowInvocations: [
      invocation('inv-reuse-a', 'def-reuse', [
        binding('member-1', 'Tag', { TagId: 'tf100.mapping.100' }),
        binding('member-4', 'Literal', { LiteralValue: 'Premiere instance' })
      ]),
      invocation('inv-reuse-b', 'def-reuse', [
        binding('member-1', 'Tag', { TagId: 'tf100.mapping.200' }),
        binding('member-4', 'Literal', { LiteralValue: 'Seconde instance' })
      ])
    ]
  });

  const rootA = parseFragment(window.document, TEMPLATE);
  const openedA = quickWindow.open('inv-reuse-a');
  assert.equal(openedA.ok, true);
  assert.equal(quickWindow.mountInstance(openedA.runtimeInstanceId, rootA).ok, true);
  // DEC-0050 SinglePerDefinition: the host never keeps two live contexts of one definition: the
  // invocations are exercised one after the other, as the equivalent test in
  // quick-window-runtime.test.mjs already does.
  quickWindow.close(openedA.runtimeInstanceId);

  const rootB = parseFragment(window.document, TEMPLATE);
  const openedB = quickWindow.open('inv-reuse-b');
  assert.equal(openedB.ok, true);
  assert.equal(quickWindow.mountInstance(openedB.runtimeInstanceId, rootB).ok, true);

  assert.equal(rootA.childNodes[0].getAttribute('data-scada-read-tag'), 'tf100.mapping.100');
  assert.equal(rootA.childNodes[1].textContent, 'Nom: Premiere instance');
  assert.equal(rootB.childNodes[0].getAttribute('data-scada-read-tag'), 'tf100.mapping.200');
  assert.equal(rootB.childNodes[1].textContent, 'Nom: Seconde instance');
  // Nothing leaked from B back onto A's (closed) root: each instance rewrote only its own DOM.
  assert.equal(rootA.childNodes[0].getAttribute('data-scada-read-tag'), 'tf100.mapping.100');
  assert.equal(rootA.childNodes[1].textContent, 'Nom: Premiere instance');
});

test('999 placeholders are substituted in a single pass', () => {
  const window = loadQuickWindow();
  const root = window.document.createElement('div');
  const ports = {};
  for (let n = 1; n <= 999; n++) {
    const el = window.document.createElement('div');
    el.setAttribute('data-scada-read-tag', '#' + n);
    root.appendChild(el);
    ports['#' + n] = { sourceKind: 'Tag', available: true, tagId: 'tf100.mapping.' + n };
  }

  let queryCalls = 0;
  const originalQuerySelectorAll = root.querySelectorAll.bind(root);
  root.querySelectorAll = function (selector) {
    queryCalls += 1;
    return originalQuerySelectorAll(selector);
  };

  let walkerCalls = 0;
  const originalCreateTreeWalker = window.document.createTreeWalker.bind(window.document);
  window.document.createTreeWalker = function (...args) {
    walkerCalls += 1;
    return originalCreateTreeWalker(...args);
  };

  const result = window.ScadaRuntime.QuickWindow.substituteInstance(root, ports);

  assert.equal(result.ok, true);
  assert.equal(queryCalls, 1, 'querySelectorAll is called exactly once for the whole pass');
  assert.equal(walkerCalls, 1, 'createTreeWalker is created exactly once for the whole pass');
  assert.equal(root.childNodes[0].getAttribute('data-scada-read-tag'), 'tf100.mapping.1');
  assert.equal(root.childNodes[498].getAttribute('data-scada-read-tag'), 'tf100.mapping.499');
  assert.equal(root.childNodes[998].getAttribute('data-scada-read-tag'), 'tf100.mapping.999');
});

// ── mountInstance gating: only a placeholder (#N-member) definition substitutes ────────────────

test('a definition with named ports only mounts unchanged: substitution is not applied', () => {
  const { window, quickWindow } = loadWithManifest({
    QuickWindows: [definition('def-named', 'qw-named', [
      member('member-speed', 'Speed', 'ReadState', 'Read', true)
    ])],
    QuickWindowInvocations: [invocation('inv-named', 'def-named', [
      binding('member-speed', 'Tag', { TagId: 'tf100.mapping.55' })
    ])]
  });

  const root = window.document.createElement('div');
  const label = window.document.createElement('span');
  label.textContent = 'Pompe #1'; // '#1' is not a member of this definition at all.
  root.appendChild(label);

  const opened = quickWindow.open('inv-named');
  assert.equal(opened.ok, true);
  const mounted = quickWindow.mountInstance(opened.runtimeInstanceId, root);

  // If substitution had run, '#1' would have no matching port and the mount would refuse
  // (placeholder-unbound). It mounts clean instead: substitution never ran.
  assert.equal(mounted.ok, true);
  assert.equal(label.textContent, 'Pompe #1');
  assert.equal(quickWindow.state().diagnostics.length, 0);
});

// ── mountInstance: refusal and success, initPage ordering, disposal, diagnostics ────────────────

test('mountInstance refuses on a substitution failure: disposes the instance, records a diagnostic, never calls initPage', () => {
  const { window, quickWindow } = loadWithManifest({
    QuickWindows: [definition('def-refuse', 'qw-refuse', [
      placeholderMember('member-1', 1, 'ReadState', 'Read', true)
    ])],
    QuickWindowInvocations: [invocation('inv-refuse', 'def-refuse', [
      binding('member-1', 'Tag', { TagId: 'tf100.mapping.1' })
    ])]
  });

  const root = window.document.createElement('div');
  const label = window.document.createElement('span');
  label.textContent = 'Valeur: #9'; // No '#9' member exists on this definition.
  root.appendChild(label);

  let initPageCalls = 0;
  window.ScadaRuntime.initPage = function () { initPageCalls += 1; };

  const opened = quickWindow.open('inv-refuse');
  assert.equal(opened.ok, true);

  const mounted = quickWindow.mountInstance(opened.runtimeInstanceId, root);

  assert.equal(mounted.ok, false);
  assert.equal(mounted.code, 'placeholder-unbound');
  assert.equal(initPageCalls, 0, 'initPage must never run on a refused mount');

  const afterState = quickWindow.state();
  assert.equal(
    afterState.instances.some((i) => i.runtimeInstanceId === opened.runtimeInstanceId),
    false,
    'the instance is disposed on refusal');
  assert.ok(
    afterState.diagnostics.some((d) => d.code === 'placeholder-unbound'),
    'a diagnostic is recorded for the refusal');
});

test('mountInstance substitutes before calling initPage exactly once on success', () => {
  const { window, quickWindow } = loadWithManifest({
    QuickWindows: [definition('def-success', 'qw-success', [
      placeholderMember('member-1', 1, 'ReadState', 'Read', true)
    ])],
    QuickWindowInvocations: [invocation('inv-success', 'def-success', [
      binding('member-1', 'Tag', { TagId: 'tf100.mapping.42' })
    ])]
  });

  const root = window.document.createElement('div');
  const el = window.document.createElement('div');
  el.setAttribute('data-scada-read-tag', '#1');
  root.appendChild(el);

  let initPageCalls = 0;
  let attributeAtInitPageTime = null;
  window.ScadaRuntime.initPage = function (mountedRoot) {
    initPageCalls += 1;
    attributeAtInitPageTime = el.getAttribute('data-scada-read-tag');
    assert.equal(mountedRoot, root);
  };

  const opened = quickWindow.open('inv-success');
  assert.equal(opened.ok, true);
  const mounted = quickWindow.mountInstance(opened.runtimeInstanceId, root);

  assert.equal(mounted.ok, true);
  assert.equal(initPageCalls, 1);
  assert.equal(attributeAtInitPageTime, 'tf100.mapping.42', 'substitution already ran by the time initPage is called');
});

// ── _isAstInjection coverage ─────────────────────────────────────────────────

test('_isAstInjection screens a literalString value carried by ExpressionAst', () => {
  const { quickWindow, intents } = loadWithManifest({
    QuickWindows: [definition('def-ast-inj-1', 'qw-ast-inj-1', [
      member('member-1', 'Label', 'PublicParameter', 'Read', true)
    ])],
    QuickWindowInvocations: [invocation('inv-ast-inj-1', 'def-ast-inj-1', [
      binding('member-1', 'Expression', {
        ExpressionAst: { type: 'literalString', value: '<script>steal()</script>' }
      })
    ])]
  });

  const opened = quickWindow.open('inv-ast-inj-1');

  assert.equal(opened.ok, false);
  assert.equal(opened.code, 'injection-rejected');
  assert.equal(intents.length, 0, 'a rejected invocation never reaches the host');
});

test('_isAstInjection screens a tagName value carried by ExpressionAst', () => {
  const { quickWindow, intents } = loadWithManifest({
    QuickWindows: [definition('def-ast-inj-2', 'qw-ast-inj-2', [
      member('member-1', 'Label', 'PublicParameter', 'Read', true)
    ])],
    QuickWindowInvocations: [invocation('inv-ast-inj-2', 'def-ast-inj-2', [
      binding('member-1', 'Expression', {
        ExpressionAst: { type: 'tagRef', tagName: 'javascript:alert(1)' }
      })
    ])]
  });

  const opened = quickWindow.open('inv-ast-inj-2');

  assert.equal(opened.ok, false);
  assert.equal(opened.code, 'injection-rejected');
  assert.equal(intents.length, 0, 'a rejected invocation never reaches the host');
});

// ── readPort/Expression (KNOWN_GAPS_V2 #36) ─────────────────────────────────

test('readPort evaluates an Expression port from its AST', () => {
  const { quickWindow, bridge } = loadWithManifest({
    QuickWindows: [definition('def-expr', 'qw-expr', [
      member('member-speed', 'Speed', 'ReadState', 'Read', true)
    ])],
    QuickWindowInvocations: [invocation('inv-expr', 'def-expr', [
      binding('member-speed', 'Expression', {
        ExpressionAst: {
          type: 'binary',
          op: 'multiply',
          left: { type: 'tagRef', tagName: 'tf100.mapping.99' },
          right: { type: 'literalNumber', value: 2 }
        }
      })
    ])]
  });
  bridge.setValues({ 'tf100.mapping.99': 5 });

  const opened = quickWindow.open('inv-expr');
  assert.equal(opened.ok, true);
  quickWindow.mountInstance(opened.runtimeInstanceId, null);

  const read = quickWindow.readPort(opened.runtimeInstanceId, 'Speed');

  assert.equal(read.ok, true);
  assert.equal(read.value, 10);
});

test('readPort refuses expression-unavailable when an Expression port has no AST', () => {
  const { quickWindow } = loadWithManifest({
    QuickWindows: [definition('def-no-ast', 'qw-no-ast', [
      member('member-x', 'X', 'ReadState', 'Read', true)
    ])],
    QuickWindowInvocations: [invocation('inv-no-ast', 'def-no-ast', [
      // An Expression binding with no AST is not "None" and not an injection: it resolves, but
      // the port stays unusable for evaluation.
      binding('member-x', 'Expression', { Expression: 'X', ExpressionAst: null })
    ])]
  });

  const opened = quickWindow.open('inv-no-ast');
  assert.equal(opened.ok, true);
  quickWindow.mountInstance(opened.runtimeInstanceId, null);

  const read = quickWindow.readPort(opened.runtimeInstanceId, 'X');

  assert.equal(read.ok, false);
  assert.equal(read.code, 'expression-unavailable');
});

test('readPort resets and checks evaluator error state, returning expression-error on a division by zero', () => {
  const { quickWindow } = loadWithManifest({
    QuickWindows: [definition('def-div0', 'qw-div0', [
      member('member-ratio', 'Ratio', 'ReadState', 'Read', true)
    ])],
    QuickWindowInvocations: [invocation('inv-div0', 'def-div0', [
      binding('member-ratio', 'Expression', {
        ExpressionAst: {
          type: 'binary',
          op: 'Divide',
          left: { type: 'literalNumber', value: 1 },
          right: { type: 'literalNumber', value: 0 }
        }
      })
    ])]
  });

  const opened = quickWindow.open('inv-div0');
  assert.equal(opened.ok, true);
  quickWindow.mountInstance(opened.runtimeInstanceId, null);

  const read = quickWindow.readPort(opened.runtimeInstanceId, 'Ratio');

  assert.equal(read.ok, false);
  assert.equal(read.code, 'expression-error');
});
