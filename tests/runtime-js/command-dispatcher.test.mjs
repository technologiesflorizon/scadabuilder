import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

function makeEventTarget(config, input = null) {
  const handlers = new Map();
  const attrs = config ? { 'data-scada-command-config': JSON.stringify(config) } : {};
  return {
    dataset: {},
    isConnected: true,
    getAttribute(name) { return attrs[name] ?? null; },
    setAttribute(name, value) { attrs[name] = value; },
    querySelector(selector) { return selector === 'input, textarea' ? input : null; },
    querySelectorAll() { return []; },
    addEventListener(name, handler) {
      if (!handlers.has(name)) handlers.set(name, []);
      handlers.get(name).push(handler);
    },
    removeEventListener(name, handler) {
      const values = handlers.get(name) || [];
      const index = values.indexOf(handler);
      if (index >= 0) values.splice(index, 1);
    },
    emit(name, event = {}) {
      event.type = event.type || name;
      for (const handler of [...(handlers.get(name) || [])]) handler(event);
    },
    handlerCount(name) { return (handlers.get(name) || []).length; },
  };
}

function installWindowEvents(window) {
  const handlers = new Map();
  window.addEventListener = (name, handler) => {
    if (!handlers.has(name)) handlers.set(name, []);
    handlers.get(name).push(handler);
  };
  window.removeEventListener = (name, handler) => {
    const values = handlers.get(name) || [];
    const index = values.indexOf(handler);
    if (index >= 0) values.splice(index, 1);
  };
  window.emit = (name, event = {}) => {
    event.type = event.type || name;
    for (const handler of [...(handlers.get(name) || [])]) handler(event);
  };
}

test('OpenPopup posts a flat {pageId, options} message matching the TF100Web host listener', () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const messages = [];
  window.postMessage = (msg) => messages.push(msg);

  const element = { querySelector: () => null };
  const cmd = { kind: 'openPopup', targetPageId: 'win00010', popupOptions: { width: 400 } };

  window.ScadaRuntime.CommandDispatcher.execute(element, cmd);

  assert.equal(messages.length, 1);
  assert.equal(messages[0].source, 'scada-builder-v2');
  assert.equal(messages[0].type, 'scada-runtime-intent');
  assert.equal(messages[0].version, '1.0');
  assert.equal(messages[0].intent.kind, 'openPopup');
  assert.equal(messages[0].action, 'openPopup');
  assert.equal(messages[0].pageId, 'win00010');
  assert.equal(messages[0].options.width, 400);
});

test('ClosePopup and TogglePopup also post a flat pageId', () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const messages = [];
  window.postMessage = (msg) => messages.push(msg);
  const element = { querySelector: () => null };

  window.ScadaRuntime.CommandDispatcher.execute(element, { kind: 'closePopup', targetPageId: 'win00011' });
  window.ScadaRuntime.CommandDispatcher.execute(element, { kind: 'togglePopup', targetPageId: 'win00012' });

  assert.equal(messages[0].pageId, 'win00011');
  assert.equal(messages[1].pageId, 'win00012');
});

test('SetFromInput reads the value from the bound element without throwing', () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const writes = [];
  window.tf100webScadaBuilder = {
    getTagValue: () => null,
    writeTag: (tagId, value) => writes.push({ tagId, value }),
  };

  const fakeInput = { value: '72.5' };
  const element = { querySelector: (sel) => (sel === 'input, textarea' ? fakeInput : null) };
  const cmd = { kind: 'writeTag', writeMode: 'setFromInput', writeTagId: 'tf100.mapping.10' };

  assert.doesNotThrow(() => window.ScadaRuntime.CommandDispatcher.execute(element, cmd));
  assert.equal(writes.length, 1);
  assert.equal(writes[0].tagId, 'tf100.mapping.10');
  assert.equal(writes[0].value, '72.5');
});

test('Navigate posts a flat {pageId} message matching the _navigateCommand contract', () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const messages = [];
  window.postMessage = (msg) => messages.push(msg);

  const element = { querySelector: () => null };
  const cmd = { kind: 'navigate', trigger: 'onClick', targetPageId: 'win00059' };

  window.ScadaRuntime.CommandDispatcher.execute(element, cmd);

  assert.equal(messages.length, 1, 'navigate must post exactly one message');
  assert.equal(messages[0].source, 'scada-builder-v2');
  assert.equal(messages[0].type, 'scada-runtime-intent');
  assert.equal(messages[0].intent.kind, 'navigate');
  assert.equal(messages[0].action, 'navigate');
  assert.equal(messages[0].pageId, 'win00059');
  // Navigate must NOT leak options (popup-specific field)
  assert.equal(messages[0].options, undefined);
});

test('bind is idempotent — double bind on the same element fires command only once', () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const messages = [];
  window.postMessage = (msg) => messages.push(msg);

  var config = { commands: [{ kind: 'navigate', trigger: 'onClick', targetPageId: 'win00059' }] };
  var configJson = JSON.stringify(config);
  var clickHandlers = [];
  var element = {
    _attrs: { 'data-scada-command-config': configJson },
    _dataset: {},
    get dataset() { return this._dataset; },
    set dataset(v) { this._dataset = v; },
    getAttribute: function (name) {
      return Object.prototype.hasOwnProperty.call(this._attrs, name) ? this._attrs[name] : null;
    },
    addEventListener: function (event, handler) {
      clickHandlers.push(handler);
    },
    click: function () {
      clickHandlers.forEach(function (h) { h({}); });
    },
  };

  // Bind twice with the same config
  window.ScadaRuntime.CommandDispatcher.bind(element);
  window.ScadaRuntime.CommandDispatcher.bind(element);

  // Click once
  element.click();

  assert.equal(messages.length, 1, 'double bind must not double-dispatch');
  assert.equal(messages[0].pageId, 'win00059');
});

test('bind maps all five serialized triggers and ignores disabled commands', () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const messages = [];
  window.postMessage = (message) => messages.push(message);
  const triggers = [
    ['onClick', 'click'], ['onRelease', 'mouseup'], ['onHover', 'mouseenter'],
    ['onHoverEnter', 'mouseenter'], ['onHoverExit', 'mouseleave'],
  ];
  const commands = triggers.map(([trigger], index) => ({
    id: `cmd-${index}`, enabled: true, kind: 'navigate', trigger, targetPageId: `page-${index}`,
  }));
  commands.push({ id: 'disabled', enabled: false, kind: 'navigate', trigger: 'onClick', targetPageId: 'never' });
  const element = makeEventTarget({ commands });

  window.ScadaRuntime.CommandDispatcher.bind(element);
  for (const [, eventName] of triggers) element.emit(eventName);

  assert.equal(messages.some((message) => message.pageId === 'never'), false);
  assert.deepEqual(new Set(messages.map((message) => message.pageId)), new Set(commands.slice(0, 5).map((command) => command.targetPageId)));
});

test('all seven command kinds use TagBridge or the one versioned host-intent envelope', () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const messages = [];
  const writes = [];
  window.postMessage = (message) => messages.push(message);
  window.tf100webScadaBuilder = {
    getTagValue: () => false,
    writeTag: (tagId, value, payload) => { writes.push({ tagId, value, payload }); return true; },
  };
  const element = { querySelector: () => null };
  const commands = [
    { id: 'write', kind: 'writeTag', writeMode: 'setFixed', writeTagId: 'write', fixedValue: '1' },
    { id: 'nav', kind: 'navigate', targetPageId: 'main' },
    { id: 'open', kind: 'openPopup', targetPageId: 'popup' },
    { id: 'toggle-popup', kind: 'togglePopup', targetPageId: 'popup' },
    { id: 'close', kind: 'closePopup', targetPageId: 'popup' },
    { id: 'url', kind: 'openUrl', url: 'https://example.invalid', newTab: true },
    { id: 'back', kind: 'back' },
  ];

  for (const command of commands) window.ScadaRuntime.CommandDispatcher.execute(element, command);

  assert.equal(writes.length, 1);
  assert.deepEqual(messages.map((message) => message.intent.kind),
    ['navigate', 'openPopup', 'togglePopup', 'closePopup', 'openUrl', 'back']);
  assert.ok(messages.every((message) => message.type === 'scada-runtime-intent' && message.version === '1.0'));
  assert.equal(messages[4].intent.url, 'https://example.invalid');
  assert.equal(messages[4].intent.newTab, true);
});

test('write modes validate values, use distinct read tags, and preserve command metadata', () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const writes = [];
  const values = { read: '1' };
  window.tf100webScadaBuilder = {
    getTagValue: (tagId) => values[tagId] ?? null,
    writeTag: (tagId, value, payload) => { writes.push({ tagId, value, payload }); return true; },
  };
  const element = { querySelector: () => ({ value: '72.5' }) };
  const dispatcher = window.ScadaRuntime.CommandDispatcher;

  dispatcher.execute(element, { id: 'toggle', kind: 'writeTag', writeMode: 'toggle', readTagId: 'read', writeTagId: 'write' });
  dispatcher.execute(element, { id: 'fixed', kind: 'writeTag', writeMode: 'setFixed', writeTagId: 'write', fixedValue: '12.5' });
  dispatcher.execute(element, { id: 'input', kind: 'writeTag', writeMode: 'setFromInput', writeTagId: 'write' });
  dispatcher.execute(element, { id: 'missing-toggle', kind: 'writeTag', writeMode: 'toggle', readTagId: 'missing', writeTagId: 'write' });
  dispatcher.execute(element, { id: 'missing-fixed', kind: 'writeTag', writeMode: 'setFixed', writeTagId: 'write' });

  assert.deepEqual(writes.map((write) => write.value), ['0', '12.5', '72.5']);
  assert.deepEqual(writes.map((write) => write.payload.mode), ['Toggle', 'SetFixed', 'SetFromInput']);
  assert.ok(writes.every((write) => write.payload.commandId && write.payload.contractVersion === '1.0'));
});

test('momentary writes real press/release phases and unbind releases an active command', () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  installWindowEvents(window);
  const writes = [];
  window.tf100webScadaBuilder = {
    getTagValue: () => null,
    writeTag: (tagId, value, payload) => { writes.push({ tagId, value, payload }); return true; },
  };
  const command = {
    id: 'momentary', enabled: true, kind: 'writeTag', writeMode: 'momentary',
    writeTagId: 'coil', onValue: '1', offValue: '0', trigger: 'onClick',
  };
  const element = makeEventTarget({ commands: [command] });
  window.ScadaRuntime.CommandDispatcher.bind(element);

  element.emit('pointerdown', { button: 0, pointerId: 7 });
  window.emit('pointerup');
  element.emit('pointerdown', { button: 0, pointerId: 8 });
  window.ScadaRuntime.CommandDispatcher.unbind(element);

  assert.deepEqual(writes.map((write) => [write.value, write.payload.phase]),
    [['1', 'press'], ['0', 'release'], ['1', 'press'], ['0', 'release']]);
  assert.equal(element.handlerCount('pointerdown'), 0);
});

test('momentary confirmation never energizes after the operator already released', () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  installWindowEvents(window);
  const writes = [];
  let accept;
  window.ScadaRuntime.showConfirmation = (_message, callback) => { accept = callback; };
  window.tf100webScadaBuilder = { writeTag: (...args) => { writes.push(args); return true; } };
  const command = {
    id: 'confirmed-momentary', kind: 'writeTag', writeMode: 'momentary', writeTagId: 'coil',
    onValue: '1', offValue: '0', confirmation: { message: 'Confirm' },
  };
  const element = makeEventTarget({ commands: [command] });
  window.ScadaRuntime.CommandDispatcher.bind(element);
  element.emit('pointerdown', { button: 0 });
  element.emit('pointerup');
  accept();
  assert.equal(writes.length, 0);
});

test('momentary waits for asynchronous press permission before sending release', async () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  installWindowEvents(window);
  const writes = [];
  let resolvePress;
  window.tf100webScadaBuilder = {
    writeTag: (_tagId, value, payload) => {
      writes.push({ value, phase: payload.phase });
      if (payload.phase === 'press') return new Promise((resolve) => { resolvePress = resolve; });
      return Promise.resolve(true);
    },
  };
  const command = {
    id: 'async-momentary', kind: 'writeTag', writeMode: 'momentary', writeTagId: 'coil',
    onValue: '1', offValue: '0',
  };
  const element = makeEventTarget({ commands: [command] });
  window.ScadaRuntime.CommandDispatcher.bind(element);
  element.emit('pointerdown', { button: 0 });
  window.emit('pointerup');
  assert.deepEqual(writes, [{ value: '1', phase: 'press' }]);

  resolvePress(true);
  await new Promise((resolve) => setImmediate(resolve));
  assert.deepEqual(writes, [{ value: '1', phase: 'press' }, { value: '0', phase: 'release' }]);
});

test('confirmation gates execution and asynchronous writes suppress duplicate commands', async () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const messages = [];
  let accept;
  window.postMessage = (message) => messages.push(message);
  window.ScadaRuntime.showConfirmation = (_message, callback) => { accept = callback; };
  const element = { querySelector: () => null };
  window.ScadaRuntime.CommandDispatcher.execute(element, {
    id: 'confirmed-nav', kind: 'navigate', targetPageId: 'main', confirmation: { message: 'Confirm' },
  });
  assert.equal(messages.length, 0);
  accept();
  assert.equal(messages.length, 1);

  let resolveWrite;
  const writes = [];
  window.tf100webScadaBuilder = {
    writeTag: (...args) => { writes.push(args); return new Promise((resolve) => { resolveWrite = resolve; }); },
  };
  const command = { id: 'async', kind: 'writeTag', writeMode: 'setFixed', writeTagId: 'write', fixedValue: '1' };
  const first = window.ScadaRuntime.CommandDispatcher.execute(element, command);
  const second = window.ScadaRuntime.CommandDispatcher.execute(element, command);
  assert.equal(second, false);
  assert.equal(writes.length, 1);
  resolveWrite(false);
  assert.equal(await first, false, 'host permission/write rejection propagates to the caller');
});

test('HostAdapter receives the canonical envelope without a parallel postMessage dispatch', () => {
  const window = loadRuntime(['command-dispatcher.js']);
  const adapterMessages = [];
  const postMessages = [];
  window.postMessage = (message) => postMessages.push(message);
  window.ScadaRuntime.HostAdapter = { dispatchIntent: (message) => { adapterMessages.push(message); return true; } };

  window.ScadaRuntime.CommandDispatcher.execute({ querySelector: () => null }, { kind: 'back' });

  assert.equal(adapterMessages.length, 1);
  assert.equal(adapterMessages[0].intent.kind, 'back');
  assert.equal(postMessages.length, 0);
});
