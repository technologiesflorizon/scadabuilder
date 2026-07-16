import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

function makeElement(id, { inputValue, bindings } = {}) {
  const handlers = new Map();
  const attrs = { 'data-scada-element-id': id };
  if (bindings) attrs['data-scada-action-bindings'] = JSON.stringify(bindings);
  const input = inputValue === undefined ? null : { value: inputValue };
  return {
    style: { display: '' },
    textContent: '',
    attrs,
    getAttribute(name) { return attrs[name] ?? null; },
    setAttribute(name, value) { attrs[name] = value; },
    querySelector(selector) {
      if (selector === 'input, textarea, select') return input;
      if (selector === '[data-scada-text]') return null;
      return null;
    },
    querySelectorAll() { return []; },
    addEventListener(name, handler) {
      if (!handlers.has(name)) handlers.set(name, []);
      handlers.get(name).push(handler);
    },
    removeEventListener(name, handler) {
      const list = handlers.get(name) || [];
      const index = list.indexOf(handler);
      if (index >= 0) list.splice(index, 1);
    },
    emit(name, event = {}) { for (const handler of [...(handlers.get(name) || [])]) handler(event); },
    handlerCount(name) { return (handlers.get(name) || []).length; },
    input,
  };
}

function makeRoot(actions, elements) {
  const attrs = { 'data-scada-action-registry': JSON.stringify({ actions }) };
  return {
    attrs,
    getAttribute(name) { return attrs[name] ?? null; },
    querySelectorAll(selector) {
      if (selector === '[data-scada-action-bindings]') {
        return elements.filter((element) => element.getAttribute('data-scada-action-bindings'));
      }
      if (selector === '[data-scada-element-id]') return elements;
      return [];
    },
    dispatchEvent() {},
  };
}

function runtime(values = {}) {
  const window = loadRuntime(['expression-evaluator.js', 'tag-bridge.js', 'command-dispatcher.js', 'action-dispatcher.js']);
  const intents = [];
  const writes = [];
  window.ScadaRuntime.HostAdapter = {
    dispatchIntent(envelope) { intents.push(envelope); return true; },
  };
  window.tf100webScadaBuilder = {
    getTagValue(tagId) { return values[tagId]; },
    writeTag(tagId, value, metadata) { writes.push({ tagId, value, metadata }); return true; },
  };
  return { window, dispatcher: window.ScadaRuntime.ActionDispatcher, intents, writes };
}

test('execute covers all nine action kinds through page-local DOM or the shared host intent', () => {
  const { dispatcher, intents, writes } = runtime({ read: 42 });
  const source = makeElement('source', { inputValue: '17.5' });
  const target = makeElement('target');
  const readTarget = makeElement('read-target');
  const root = makeRoot([], [source, target, readTarget]);
  const actions = [
    { id: 'navigate', kind: 'navigate', targetPageId: 'main' },
    { id: 'show', kind: 'show', targetElementId: 'target' },
    { id: 'hide', kind: 'hide', targetElementId: 'target' },
    { id: 'toggle', kind: 'toggleVisibility', targetElementId: 'target' },
    { id: 'open', kind: 'mountFragment', targetPageId: 'popup', popupOptions: { sizePreset: 'small' } },
    { id: 'close', kind: 'closePopup', targetPageId: 'popup' },
    { id: 'toggle-popup', kind: 'togglePopup', targetPageId: 'popup' },
    { id: 'read', kind: 'readValue', tagId: 'read', targetElementId: 'read-target' },
    { id: 'write', kind: 'writeValue', tagId: 'write' },
  ];

  for (const action of actions) assert.notEqual(dispatcher.execute(source, action, root, 'page'), false, action.kind);

  assert.deepEqual(intents.map((item) => item.intent.kind), ['navigate', 'openPopup', 'closePopup', 'togglePopup']);
  assert.equal(intents[0].intent.pageId, 'main');
  assert.equal(intents[1].intent.options.sizePreset, 'small');
  assert.equal(target.style.display, '');
  assert.equal(readTarget.textContent, '42');
  assert.equal(writes.length, 1);
  assert.equal(writes[0].tagId, 'write');
  assert.equal(writes[0].value, '17.5');
  assert.equal(writes[0].metadata.actionId, 'write');
  assert.equal(writes[0].metadata.pageId, 'page');
});

test('conditions cover all operators, group All/Any, and explicit missing-tag policies', () => {
  const { dispatcher } = runtime({ number: 10, boolTrue: true, boolFalse: false, text: 'ready' });
  const cases = [
    ['equals', 'text', 'ready', true],
    ['notEquals', 'text', 'stopped', true],
    ['greaterThan', 'number', '9', true],
    ['greaterThanOrEqual', 'number', '10', true],
    ['lessThan', 'number', '11', true],
    ['lessThanOrEqual', 'number', '10', true],
    ['true', 'boolTrue', null, true],
    ['false', 'boolFalse', null, true],
  ];
  for (const [operator, tagId, compareValue, expected] of cases) {
    assert.equal(dispatcher.evaluateCondition({ tagId, operator, compareValue }).matched, expected, operator);
  }

  const base = { id: 'conditional', kind: 'hide', targetElementId: 'target' };
  assert.equal(dispatcher.conditionsAllow({ ...base, condition: { tagId: 'missing', operator: 'equals', compareValue: '1' } }), false);
  assert.equal(dispatcher.conditionsAllow({
    ...base,
    conditionGroup: {
      mode: 'all', missingTagPolicy: 'blockAction',
      conditions: [{ tagId: 'boolTrue', operator: 'true' }, { tagId: 'number', operator: 'equals', compareValue: '10' }],
    },
  }), true);
  assert.equal(dispatcher.conditionsAllow({
    ...base,
    conditionGroup: {
      mode: 'any', missingTagPolicy: 'blockAction',
      conditions: [{ tagId: 'boolFalse', operator: 'true' }, { tagId: 'number', operator: 'greaterThan', compareValue: '20' }],
    },
  }), false);
  for (const [missingTagPolicy, expected] of [['blockAction', false], ['allowAction', true]]) {
    assert.equal(dispatcher.conditionsAllow({
      ...base,
      conditionGroup: { mode: 'all', missingTagPolicy, conditions: [{ tagId: 'missing', operator: 'equals', compareValue: '1' }] },
    }), expected, missingTagPolicy);
  }
});

test('initPage preserves binding order and propagation flags and is idempotent', () => {
  const actions = [
    { id: 'first', kind: 'navigate', targetPageId: 'first' },
    { id: 'second', kind: 'navigate', targetPageId: 'second' },
  ];
  const element = makeElement('source', { bindings: [
    { trigger: 'OnClick', actionId: 'first', preventDefault: true },
    { trigger: 'click', actionId: 'second', stopPropagation: true },
  ] });
  const root = makeRoot(actions, [element]);
  const { dispatcher, intents } = runtime();
  let prevented = 0;
  let stopped = 0;
  dispatcher.initPage(root, 'page');
  dispatcher.initPage(root, 'page');
  element.emit('click', { preventDefault() { prevented++; }, stopPropagation() { stopped++; } });

  assert.deepEqual(intents.map((item) => item.intent.pageId), ['first', 'second']);
  assert.equal(prevented, 1);
  assert.equal(stopped, 1);
  assert.equal(element.handlerCount('click'), 1);
  dispatcher.dispose(root);
  assert.equal(element.handlerCount('click'), 0);
});

test('object targets never escape the current page root', () => {
  const { dispatcher } = runtime();
  const local = makeElement('same-id');
  const foreign = makeElement('same-id');
  const localRoot = makeRoot([], [local]);
  const foreignRoot = makeRoot([], [foreign]);
  dispatcher.execute(local, { id: 'hide-local', kind: 'hide', targetElementId: 'same-id' }, localRoot, 'local');

  assert.equal(local.style.display, 'none');
  assert.equal(foreign.style.display, '');
  assert.notEqual(localRoot, foreignRoot);
});

test('show restores an initially hidden target and hide restores its authored display baseline', () => {
  const { dispatcher } = runtime();
  const target = makeElement('target');
  const root = makeRoot([], [target]);
  target.style.display = 'none';
  dispatcher.execute(target, { id: 'show', kind: 'show', targetElementId: 'target' }, root, 'page');
  assert.equal(target.style.display, '');
  target.style.display = 'inline-flex';
  dispatcher.execute(target, { id: 'hide', kind: 'hide', targetElementId: 'target' }, root, 'page');
  dispatcher.execute(target, { id: 'show-again', kind: 'show', targetElementId: 'target' }, root, 'page');
  assert.equal(target.style.display, 'inline-flex');
});

test('disabled action sources fail closed without dispatch', () => {
  const actions = [{ id: 'navigate', kind: 'navigate', targetPageId: 'main' }];
  const element = makeElement('source', { bindings: [{ trigger: 'click', actionId: 'navigate' }] });
  element.setAttribute('data-scada-disabled', 'true');
  const root = makeRoot(actions, [element]);
  const { dispatcher, intents } = runtime();
  dispatcher.initPage(root, 'page');
  element.emit('click');
  assert.equal(intents.length, 0);
});
