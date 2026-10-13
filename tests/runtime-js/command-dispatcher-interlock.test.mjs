import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

/** Toggle interlock (DEC-0054): interlock written to 0 first, then the write tag to 1 only if accepted. */

const element = { querySelector: () => null };
const command = {
  id: 'start', kind: 'writeTag', writeMode: 'toggle', writeTagId: 'start', interlockTagId: 'stop',
};

function setup(values, writeTag) {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const writes = [];
  const warnings = [];
  window.console = { warn: (code) => warnings.push(code) };
  window.tf100webScadaBuilder = {
    getTagValue: (tagId) => values[tagId] ?? null,
    writeTag: (tagId, value, payload) => { writes.push({ tagId, value, mode: payload.mode }); return writeTag(tagId, value); },
  };
  return { dispatcher: window.ScadaRuntime.CommandDispatcher, writes, warnings };
}

test('target 1: the interlock is written to 0 before the write tag is written to 1', () => {
  const { dispatcher, writes } = setup({ start: false }, () => true);

  assert.notEqual(dispatcher.execute(element, command), false);

  assert.deepEqual(writes, [
    { tagId: 'stop', value: '0', mode: 'ToggleInterlock' },
    { tagId: 'start', value: '1', mode: 'Toggle' },
  ]);
});

test('target 0: the interlock is never written', () => {
  const { dispatcher, writes } = setup({ start: true }, () => true);

  dispatcher.execute(element, command);

  assert.deepEqual(writes, [{ tagId: 'start', value: '0', mode: 'Toggle' }]);
});

test('a synchronously refused interlock never writes 1', () => {
  const { dispatcher, writes, warnings } = setup({ start: false }, (tagId) => tagId !== 'stop');

  assert.equal(dispatcher.execute(element, command), false);

  assert.deepEqual(writes.map((w) => w.tagId), ['stop']);
  assert.ok(warnings.includes('SCADA command interlock-write-failed'), warnings.join(','));
});

test('an asynchronously refused or failed interlock never writes 1', async () => {
  for (const outcome of ['resolve-false', 'reject']) {
    const { dispatcher, writes, warnings } = setup({ start: false }, (tagId) => (tagId === 'stop'
      ? (outcome === 'reject' ? Promise.reject(new Error('HTTP 502')) : Promise.resolve(false))
      : Promise.resolve(true)));

    assert.equal(await dispatcher.execute(element, command), false, outcome);

    assert.deepEqual(writes.map((w) => w.tagId), ['stop'], outcome);
    assert.ok(warnings.includes('SCADA command interlock-write-failed'), `${outcome}: ${warnings.join(',')}`);
  }
});

test('an asynchronously accepted interlock writes 1 and resolves to the write result', async () => {
  const { dispatcher, writes } = setup({ start: false }, () => Promise.resolve(true));

  assert.equal(await dispatcher.execute(element, command), true);

  assert.deepEqual(writes.map((w) => [w.tagId, w.value]), [['stop', '0'], ['start', '1']]);
});

test('a click during the sequence is refused and writes nothing', async () => {
  let release;
  const { dispatcher, writes, warnings } = setup({ start: false }, (tagId) => (tagId === 'stop'
    ? new Promise((resolve) => { release = resolve; })
    : Promise.resolve(true)));

  const first = dispatcher.execute(element, command);
  assert.equal(dispatcher.execute(element, command), false);
  assert.ok(warnings.includes('SCADA command write-in-flight'));
  assert.equal(writes.length, 1);

  release(true);
  assert.equal(await first, true);
  assert.deepEqual(writes.map((w) => w.tagId), ['stop', 'start']);
});
