import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

test('OpenPopup posts a flat {pageId, options} message matching the TF100Web host listener', () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const messages = [];
  window.postMessage = (msg) => messages.push(msg);

  const element = { querySelector: () => null };
  const cmd = { kind: 'OpenPopup', targetPageId: 'win00010', popupOptions: { width: 400 } };

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

  window.ScadaRuntime.CommandDispatcher.execute(element, { kind: 'ClosePopup', targetPageId: 'win00011' });
  window.ScadaRuntime.CommandDispatcher.execute(element, { kind: 'TogglePopup', targetPageId: 'win00012' });

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
  const cmd = { kind: 'WriteTag', writeMode: 'SetFromInput', writeTagId: 'tf100.mapping.10' };

  assert.doesNotThrow(() => window.ScadaRuntime.CommandDispatcher.execute(element, cmd));
  assert.equal(writes.length, 1);
  assert.equal(writes[0].tagId, 'tf100.mapping.10');
  assert.equal(writes[0].value, '72.5');
});
