import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime, createFakeDom } from './harness.mjs';

/**
 * Tests for mount-time `#N` placeholder substitution (`ScadaRuntime.QuickWindow.substituteInstance`),
 * DEC-0052, contract §11b.1.
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
  // The attribute is left untouched on refusal: no partial mutation is observable.
  assert.equal(el.getAttribute('data-scada-write-tag'), '#4');
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
  // No partial write: the text node keeps its unsubstituted placeholder.
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

test('two successive instances of the same page show their own values', () => {
  function buildFixture() {
    const window = loadQuickWindow();
    const root = window.document.createElement('div');
    const el = window.document.createElement('div');
    el.setAttribute('data-scada-read-tag', '#1');
    const label = window.document.createElement('span');
    label.textContent = 'Nom: #4';
    el.appendChild(label);
    root.appendChild(el);
    return { window, root, el, label };
  }

  const first = buildFixture();
  const portsFirst = {
    '#1': { sourceKind: 'Tag', available: true, tagId: 'tf100.mapping.100' },
    '#4': { sourceKind: 'Literal', available: true, literalValue: 'Premiere instance' }
  };
  const resultFirst = first.window.ScadaRuntime.QuickWindow.substituteInstance(first.root, portsFirst);

  const second = buildFixture();
  const portsSecond = {
    '#1': { sourceKind: 'Tag', available: true, tagId: 'tf100.mapping.200' },
    '#4': { sourceKind: 'Literal', available: true, literalValue: 'Seconde instance' }
  };
  const resultSecond = second.window.ScadaRuntime.QuickWindow.substituteInstance(second.root, portsSecond);

  assert.equal(resultFirst.ok, true);
  assert.equal(resultSecond.ok, true);
  assert.equal(first.el.getAttribute('data-scada-read-tag'), 'tf100.mapping.100');
  assert.equal(first.label.textContent, 'Nom: Premiere instance');
  assert.equal(second.el.getAttribute('data-scada-read-tag'), 'tf100.mapping.200');
  assert.equal(second.label.textContent, 'Nom: Seconde instance');
  // Nothing leaked from the second instance back onto the first.
  assert.equal(first.el.getAttribute('data-scada-read-tag'), 'tf100.mapping.100');
  assert.equal(first.label.textContent, 'Nom: Premiere instance');
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

// ── readPort/Expression regression (KNOWN_GAPS_V2 #36) ──────────────────────

const DEFINITION_KEY = 'f1e2d3c4-4444-4666-8777-cccccccccccc';
const INVOCATION_KEY = 'inv-expr-1';
const SPEED_MEMBER = 'member-speed';

function member(key, name, family, access, required) {
  return {
    MemberKey: key, Name: name, Family: family, DataType: 'Number',
    Access: access, Required: !!required, DefaultValue: null, Description: null
  };
}

function definition(key, namespace, members) {
  return {
    DefinitionKey: key, Code: namespace, DisplayName: namespace, InterfaceVersion: 1, Namespace: namespace,
    RelativePath: `${namespace}/${namespace}.html`, CssRelativePath: `${namespace}/css/${namespace}.css`,
    Width: 480, Height: 320, InterfaceMembers: members, PresentationDefaults: { Position: 'Center', Backdrop: true }
  };
}

function invocation(key, definitionKey, bindings) {
  return {
    InvocationKey: key, DefinitionKey: definitionKey, InterfaceVersion: 1, TitleOverride: null,
    OwnerPageKey: null, OwnerElementId: 'caller', OwnerCommandId: 'open', Bindings: bindings
  };
}

function binding(memberKey, sourceKind, extra = {}) {
  return {
    MemberKey: memberKey, SourceKind: sourceKind, TagId: null, LiteralValue: null,
    Expression: null, ExpressionAst: null, ParentMemberKey: null, ...extra
  };
}

test('readPort evaluates an Expression port from its AST', () => {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js', 'quick-window-runtime.js']);
  window.ScadaRuntime.HostAdapter = { dispatchIntent() { return true; } };
  window.ScadaRuntime.TagBridge.setValues({ 'tf100.mapping.99': 5 });

  window.ScadaRuntime.QuickWindow.loadRegistries({
    QuickWindows: [definition(DEFINITION_KEY, 'qw-expr', [
      member(SPEED_MEMBER, 'Speed', 'ReadState', 'Read', true)
    ])],
    QuickWindowInvocations: [invocation(INVOCATION_KEY, DEFINITION_KEY, [
      binding(SPEED_MEMBER, 'Expression', {
        ExpressionAst: {
          type: 'binary',
          op: 'multiply',
          left: { type: 'tagRef', tagName: 'tf100.mapping.99' },
          right: { type: 'literalNumber', value: 2 }
        }
      })
    ])]
  });

  const opened = window.ScadaRuntime.QuickWindow.open(INVOCATION_KEY);
  assert.equal(opened.ok, true);
  window.ScadaRuntime.QuickWindow.mountInstance(opened.runtimeInstanceId, null);

  const read = window.ScadaRuntime.QuickWindow.readPort(opened.runtimeInstanceId, 'Speed');

  assert.equal(read.ok, true);
  assert.equal(read.value, 10);
});
