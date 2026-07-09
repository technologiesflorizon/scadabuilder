import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

test('OpenPopup posts a flat {pageId, options} message matching the TF100Web host listener', () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const messages = [];
  window.postMessage = (msg) => messages.push(msg);

  const element = { querySelector: () => null };
  const cmd = { kind: 'openPopup', targetPageId: 'win00010', popupOptions: { width: 400 } };

  window.ScadaRuntime.CommandDispatcher.execute(element, cmd);

  assert.equal(messages.length, 1);
  assert.equal(messages[0].source, 'scada-builder-v2');
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
