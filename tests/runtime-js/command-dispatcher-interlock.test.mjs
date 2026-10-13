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

test('an asynchronously refused interlock logs interlock-write-failed only, never write-rejected', async () => {
  for (const outcome of ['resolve-false', 'reject']) {
    const { dispatcher, warnings } = setup({ start: false }, (tagId) => (tagId === 'stop'
      ? (outcome === 'reject' ? Promise.reject(new Error('HTTP 502')) : Promise.resolve(false))
      : Promise.resolve(true)));

    assert.equal(await dispatcher.execute(element, command), false, outcome);

    assert.ok(warnings.includes('SCADA command interlock-write-failed'), `${outcome}: ${warnings.join(',')}`);
    assert.ok(!warnings.includes('SCADA command write-rejected'), `${outcome}: ${warnings.join(',')}`);
    assert.ok(!warnings.includes('SCADA command write-failed'), `${outcome}: ${warnings.join(',')}`);
  }
});

test('a refused second write still logs write-rejected', async () => {
  const { dispatcher, warnings } = setup({ start: false }, (tagId) => Promise.resolve(tagId === 'stop'));

  assert.equal(await dispatcher.execute(element, command), false);

  assert.ok(warnings.includes('SCADA command write-rejected'), warnings.join(','));
  assert.ok(!warnings.includes('SCADA command interlock-write-failed'), warnings.join(','));
});

/** Départ (writes start, interlocks stop) and Arrêt (writes stop, interlocks start): the reciprocal setup. */
const depart = { id: 'depart', kind: 'writeTag', writeMode: 'toggle', writeTagId: 'start', interlockTagId: 'stop' };
const arret = { id: 'arret', kind: 'writeTag', writeMode: 'toggle', writeTagId: 'stop', interlockTagId: 'start' };

function deferredHost() {
  const pending = [];
  const writeTag = () => new Promise((resolve, reject) => { pending.push({ resolve, reject }); });
  return { pending, writeTag };
}

async function flush() {
  for (let i = 0; i < 20; i++) await Promise.resolve();
}

test('two interlocked commands clicked within one round trip are serialized, never interleaved', async () => {
  const host = deferredHost();
  const { dispatcher, writes } = setup({ start: false, stop: false }, host.writeTag);

  const first = dispatcher.execute(element, depart);
  const second = dispatcher.execute(element, arret);
  assert.notEqual(second, false, 'the corrective Arrêt click is queued, never refused');
  assert.deepEqual(writes.map((w) => [w.tagId, w.value]), [['stop', '0']]);

  while (host.pending.length) {
    host.pending.shift().resolve(true);
    await flush();
  }

  assert.equal(await first, true);
  assert.equal(await second, true);
  assert.deepEqual(writes.map((w) => [w.tagId, w.value]), [
    ['stop', '0'], ['start', '1'], ['start', '0'], ['stop', '1'],
  ]);
  const final = {};
  for (const w of writes) final[w.tagId] = w.value;
  assert.deepEqual(final, { start: '0', stop: '1' });
});

test('a refused or failed first sequence does not block the queued one', async () => {
  for (const outcome of ['interlock-false', 'interlock-reject', 'write-reject', 'write-throw']) {
    const host = deferredHost();
    let call = 0;
    const { dispatcher, writes } = setup({ start: false, stop: false }, (tagId, value) => {
      call++;
      if (call === 2 && outcome === 'write-throw') throw new Error('bridge down');
      return host.writeTag(tagId, value);
    });

    const first = dispatcher.execute(element, depart);
    const second = dispatcher.execute(element, arret);

    if (outcome === 'interlock-false') host.pending.shift().resolve(false);
    else if (outcome === 'interlock-reject') host.pending.shift().reject(new Error('HTTP 502'));
    else {
      host.pending.shift().resolve(true);
      await flush();
      if (outcome === 'write-reject') host.pending.shift().reject(new Error('HTTP 502'));
    }
    assert.equal(await first, false, outcome);

    while (host.pending.length) {
      host.pending.shift().resolve(true);
      await flush();
    }
    assert.equal(await second, true, outcome);
    assert.deepEqual(writes.slice(-2).map((w) => [w.tagId, w.value]), [['start', '0'], ['stop', '1']], outcome);
  }
});

test('a lone sequence on a synchronous host writes both tags immediately, with no await', () => {
  const { dispatcher, writes } = setup({ start: false, stop: false }, () => true);

  assert.equal(dispatcher.execute(element, depart), true);
  assert.deepEqual(writes.map((w) => [w.tagId, w.value]), [['stop', '0'], ['start', '1']]);
  assert.equal(dispatcher.execute(element, arret), true);
  assert.deepEqual(writes.map((w) => [w.tagId, w.value]), [['stop', '0'], ['start', '1'], ['start', '0'], ['stop', '1']]);
});

test('once an asynchronous sequence has settled, the next sequence starts synchronously again', async () => {
  let async = true;
  const { dispatcher, writes } = setup({ start: false, stop: false }, () => (async ? Promise.resolve(true) : true));

  assert.equal(await dispatcher.execute(element, depart), true);
  async = false;

  assert.equal(dispatcher.execute(element, arret), true);
  assert.deepEqual(writes.map((w) => [w.tagId, w.value]), [['stop', '0'], ['start', '1'], ['start', '0'], ['stop', '1']]);
});

test('a second click on the same queued command is refused with write-in-flight', async () => {
  const host = deferredHost();
  const { dispatcher, writes, warnings } = setup({ start: false, stop: false }, host.writeTag);

  const first = dispatcher.execute(element, depart);
  const second = dispatcher.execute(element, arret);
  assert.equal(dispatcher.execute(element, arret), false);
  assert.ok(warnings.includes('SCADA command write-in-flight'), warnings.join(','));
  assert.equal(writes.length, 1);

  while (host.pending.length) {
    host.pending.shift().resolve(true);
    await flush();
  }
  assert.equal(await first, true);
  assert.equal(await second, true);
  assert.equal(writes.length, 4);
});
