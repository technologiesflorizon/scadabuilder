import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

function makeInput(value = '') {
  const handlers = new Map();
  return {
    value,
    parentElement: null,
    addEventListener(name, handler) {
      if (!handlers.has(name)) handlers.set(name, []);
      handlers.get(name).push(handler);
    },
    emit(name, event = {}) {
      for (const handler of [...(handlers.get(name) || [])]) handler(event);
    },
    getAttribute() { return null; },
    blur() { this.blurred = true; },
    handlerCount(name) { return (handlers.get(name) || []).length; },
  };
}

function makeElement(id, input, readTagId = 'read-tag') {
  const attrs = { 'data-scada-read-tag': readTagId };
  return {
    id,
    isConnected: true,
    querySelector: () => input,
    getAttribute: (name) => attrs[name] ?? null,
    setAttribute: (name, value) => { attrs[name] = value; },
  };
}

function installEnvironment(window) {
  let nextTimer = 1;
  const timers = new Map();
  window.setTimeout = (callback) => { const id = nextTimer++; timers.set(id, callback); return id; };
  window.clearTimeout = (id) => timers.delete(id);
  window.runTimer = (id = [...timers.keys()][0]) => {
    const callback = timers.get(id);
    timers.delete(id);
    if (callback) callback();
  };
  window.getComputedStyle = () => ({ position: 'static' });
  window.document = {
    createElement() {
      return {
        className: '', style: {}, parentNode: null,
        setAttribute() {},
      };
    },
    getElementById() { return null; },
  };
  return timers;
}

function attachParent(input) {
  const children = [];
  const parent = {
    style: { position: '' },
    appendChild(node) { children.push(node); node.parentNode = this; },
    removeChild(node) { children.splice(children.indexOf(node), 1); node.parentNode = null; },
    children,
  };
  input.parentElement = parent;
  return parent;
}

test('watch is idempotent and locks duplicate DOM ids by element identity', () => {
  const window = loadRuntime(['input-edit-guard.js']);
  installEnvironment(window);
  const paused = [];
  const resumed = [];
  window.ScadaRuntime.StateEngine = {
    pauseElement: (element) => paused.push(element),
    resumeElement: (element) => resumed.push(element),
  };
  const firstInput = makeInput('1');
  const secondInput = makeInput('2');
  attachParent(firstInput);
  attachParent(secondInput);
  const first = makeElement('duplicate', firstInput);
  const second = makeElement('duplicate', secondInput);
  const guard = window.ScadaRuntime.InputEditGuard;

  guard.watch(first);
  guard.watch(first);
  guard.watch(second);
  firstInput.emit('focus');
  secondInput.emit('focus');

  assert.equal(firstInput.handlerCount('focus'), 1);
  assert.equal(guard.isLocked(first), true);
  assert.equal(guard.isLocked(second), true);
  assert.deepEqual(paused, [first, second]);
  guard.release(first);
  assert.equal(guard.isLocked(first), false);
  assert.equal(guard.isLocked(second), true);
  assert.deepEqual(resumed, [first]);
});

test('Escape restores the starting value and release restores parent layout', () => {
  const window = loadRuntime(['input-edit-guard.js']);
  installEnvironment(window);
  window.ScadaRuntime.StateEngine = { pauseElement() {}, resumeElement() {} };
  const input = makeInput('12');
  const parent = attachParent(input);
  const element = makeElement('input', input);
  const guard = window.ScadaRuntime.InputEditGuard;
  guard.watch(element);

  input.emit('focus');
  assert.equal(parent.style.position, 'relative');
  assert.equal(parent.children.length, 1);
  input.value = '99';
  input.emit('keydown', { key: 'Escape' });

  assert.equal(input.value, '12');
  assert.equal(parent.style.position, '');
  assert.equal(parent.children.length, 0);
  assert.equal(guard.isLocked(element), false);
});

test('input activity refreshes timeout and timeout restores the configured read tag', () => {
  const window = loadRuntime(['input-edit-guard.js']);
  const timers = installEnvironment(window);
  window.ScadaRuntime.StateEngine = { pauseElement() {}, resumeElement() {} };
  window.ScadaRuntime.TagBridge = { getTagValue: (tagId) => tagId === 'confirmed-read' ? 42 : null };
  const input = makeInput('12');
  attachParent(input);
  const element = makeElement('dom-id-is-not-a-tag', input, 'confirmed-read');
  const guard = window.ScadaRuntime.InputEditGuard;
  guard.watch(element);
  input.emit('focus');
  const firstTimer = [...timers.keys()][0];
  input.emit('input');
  assert.equal(timers.has(firstTimer), false, 'typing must replace the inactivity timer');

  input.value = '99';
  window.runTimer();
  assert.equal(input.value, 42);
  assert.equal(input.blurred, true);
  assert.equal(guard.isLocked(element), false);
});

test('dispose releases only active inputs contained by the replaced DOM root', () => {
  const window = loadRuntime(['input-edit-guard.js']);
  installEnvironment(window);
  window.ScadaRuntime.StateEngine = { pauseElement() {}, resumeElement() {} };
  const firstInput = makeInput();
  const secondInput = makeInput();
  attachParent(firstInput);
  attachParent(secondInput);
  const first = makeElement('first', firstInput);
  const second = makeElement('second', secondInput);
  const guard = window.ScadaRuntime.InputEditGuard;
  guard.lock(first, firstInput);
  guard.lock(second, secondInput);

  guard.dispose({ contains: (element) => element === first });
  assert.equal(guard.isLocked(first), false);
  assert.equal(guard.isLocked(second), true);
});
