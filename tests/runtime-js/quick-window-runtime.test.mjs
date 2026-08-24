import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

const DEFINITION_A = 'a1b2c3d4-1111-4222-8333-aaaaaaaaaaaa';
const DEFINITION_B = 'e5f6a7b8-2222-4333-8444-bbbbbbbbbbbb';
const DEFINITION_C = 'c9d0e1f2-3333-4444-8555-ffffffffffff';
const INV_M101 = 'inv-a-m101';
const INV_M102 = 'inv-a-m102';
const INV_CHILD = 'inv-b-child';
const INV_DEEP = 'inv-c-deep';
const RUNNING = 'member-running';
const COMMAND = 'member-command';
const OPTIONAL = 'member-optional';

function member(key, name, family, access, required = false) {
  return {
    MemberKey: key,
    Name: name,
    Family: family,
    DataType: 'Boolean',
    Access: access,
    Required: required,
    DefaultValue: null,
    Description: null
  };
}

function definition(key, namespace, members) {
  return {
    DefinitionKey: key,
    Code: namespace,
    DisplayName: namespace,
    InterfaceVersion: 1,
    Namespace: namespace,
    RelativePath: `${namespace}/${namespace}.html`,
    CssRelativePath: `${namespace}/css/${namespace}.css`,
    Width: 480,
    Height: 320,
    InterfaceMembers: members,
    PresentationDefaults: { Position: 'Center', Backdrop: true }
  };
}

function invocation(key, definitionKey, bindings, interfaceVersion = 1) {
  return {
    InvocationKey: key,
    DefinitionKey: definitionKey,
    InterfaceVersion: interfaceVersion,
    TitleOverride: null,
    OwnerPageKey: null,
    OwnerElementId: 'caller',
    OwnerCommandId: 'open',
    Bindings: bindings
  };
}

function binding(memberKey, sourceKind, extra = {}) {
  return {
    MemberKey: memberKey,
    SourceKind: sourceKind,
    TagId: null,
    LiteralValue: null,
    Expression: null,
    ParentMemberKey: null,
    ...extra
  };
}

function manifest(overrides = {}) {
  return {
    QuickWindows: [
      definition(DEFINITION_A, 'qw-a1b2c3d4', [
        member(RUNNING, 'Running', 'ReadState', 'Read', true),
        member(COMMAND, 'Start', 'WriteCommand', 'Write'),
        member(OPTIONAL, 'Label', 'PublicParameter', 'Read')
      ]),
      definition(DEFINITION_B, 'qw-e5f6a7b8', [member(RUNNING, 'Running', 'ReadState', 'Read', true)]),
      definition(DEFINITION_C, 'qw-c9d0e1f2', [member(RUNNING, 'Running', 'ReadState', 'Read', true)])
    ],
    QuickWindowInvocations: [
      invocation(INV_M101, DEFINITION_A, [
        binding(RUNNING, 'Tag', { TagId: 'tf100.mapping.210' }),
        binding(COMMAND, 'Tag', { TagId: 'tf100.mapping.211' })
      ]),
      invocation(INV_M102, DEFINITION_A, [
        binding(RUNNING, 'Tag', { TagId: 'tf100.mapping.310' }),
        binding(COMMAND, 'Tag', { TagId: 'tf100.mapping.311' })
      ]),
      invocation(INV_CHILD, DEFINITION_B, [binding(RUNNING, 'Tag', { TagId: 'tf100.mapping.410' })]),
      invocation(INV_DEEP, DEFINITION_C, [binding(RUNNING, 'Tag', { TagId: 'tf100.mapping.510' })])
    ],
    ...overrides
  };
}

function load(overrides) {
  const window = loadRuntime(['tag-bridge.js', 'quick-window-runtime.js']);
  const intents = [];
  window.ScadaRuntime.HostAdapter = {
    dispatchIntent(envelope) {
      intents.push(envelope);
      return true;
    }
  };
  window.ScadaRuntime.QuickWindow.loadRegistries(manifest(overrides));
  return { window, quickWindow: window.ScadaRuntime.QuickWindow, bridge: window.ScadaRuntime.TagBridge, intents };
}

test('the shared runtime never draws: opening only hands a resolved context to the host adapter', () => {
  const { quickWindow, intents } = load();

  const opened = quickWindow.open(INV_M101);

  assert.equal(opened.ok, true);
  assert.equal(intents.length, 1);
  assert.equal(intents[0].intent.kind, 'openQuickWindow');
  assert.equal(intents[0].intent.invocationKey, INV_M101);
  assert.equal(intents[0].intent.namespace, 'qw-a1b2c3d4');
  assert.equal(intents[0].intent.relativePath, 'qw-a1b2c3d4/qw-a1b2c3d4.html');
  assert.ok(intents[0].intent.runtimeInstanceId.startsWith('qw-inst-'));
});

test('two invocations of one definition keep strictly independent mappings', () => {
  const { quickWindow, bridge } = load();
  bridge.setValues({ 'tf100.mapping.210': true, 'tf100.mapping.310': false });

  // One definition never has two live contexts: the invocations are exercised one after the other.
  const first = quickWindow.open(INV_M101);
  quickWindow.mountInstance(first.runtimeInstanceId, null);
  assert.equal(quickWindow.readPort(first.runtimeInstanceId, 'Running').value, true);
  quickWindow.writePort(first.runtimeInstanceId, 'Start', true);
  assert.equal(bridge.getTagValue('tf100.mapping.211'), true);
  assert.equal(bridge.getTagValue('tf100.mapping.311'), undefined, 'M101 never writes the M102 mapping');
  quickWindow.close(first.runtimeInstanceId);

  const second = quickWindow.open(INV_M102);
  quickWindow.mountInstance(second.runtimeInstanceId, null);
  assert.equal(quickWindow.readPort(second.runtimeInstanceId, 'Running').value, false);
  quickWindow.writePort(second.runtimeInstanceId, 'Start', false);
  assert.equal(bridge.getTagValue('tf100.mapping.311'), false);
  assert.equal(bridge.getTagValue('tf100.mapping.211'), true, 'M102 never rewrites the M101 mapping');
  assert.equal(
    bridge.subscriptions(first.runtimeInstanceId).length,
    0,
    'the closed invocation keeps no subscription behind');
});

test('a required port left unbound is refused and nothing is subscribed', () => {
  const { quickWindow, bridge } = load({
    QuickWindowInvocations: [invocation(INV_M101, DEFINITION_A, [binding(COMMAND, 'Tag', { TagId: 'tf100.mapping.211' })])]
  });

  const opened = quickWindow.open(INV_M101);

  assert.equal(opened.ok, false);
  assert.equal(opened.code, 'required-port-unbound');
  assert.equal(Object.keys(bridge.subscriptions()).length, 0, 'a refused open subscribes nothing');
});

test('an optional unbound port stays neutral and unavailable', () => {
  const { quickWindow } = load();

  const opened = quickWindow.open(INV_M101);
  quickWindow.mountInstance(opened.runtimeInstanceId, null);

  const label = quickWindow.readPort(opened.runtimeInstanceId, 'Label');
  assert.equal(label.ok, false);
  assert.equal(label.code, 'port-unavailable');
  assert.equal(opened.ports.Label.available, false);
});

test('injection is rejected before any subscription and emits no write', () => {
  for (const payload of [
    binding(RUNNING, 'Literal', { LiteralValue: '<script>steal()</script>' }),
    binding(RUNNING, 'Expression', { Expression: '../../etc/passwd' }),
    binding(RUNNING, 'Literal', { LiteralValue: '#injected' }),
    binding(RUNNING, 'Tag', { TagId: 'javascript:alert(1)' })
  ]) {
    const { quickWindow, bridge, intents } = load({
      QuickWindowInvocations: [invocation(INV_M101, DEFINITION_A, [payload, binding(COMMAND, 'Tag', { TagId: 'tf100.mapping.211' })])]
    });

    const opened = quickWindow.open(INV_M101);

    assert.equal(opened.ok, false);
    assert.equal(opened.code, 'injection-rejected');
    assert.equal(intents.length, 0, 'a rejected invocation never reaches the host');
    assert.equal(Object.keys(bridge.subscriptions()).length, 0, 'nothing is subscribed before the rejection');
    assert.equal(bridge.getTagValue('tf100.mapping.211'), undefined, 'no write is emitted');
  }
});

test('an outdated invocation is refused by its interface version', () => {
  const { quickWindow } = load({
    QuickWindowInvocations: [invocation(INV_M101, DEFINITION_A, [binding(RUNNING, 'Tag', { TagId: 'tf100.mapping.210' })], 2)]
  });

  const opened = quickWindow.open(INV_M101);

  assert.equal(opened.ok, false);
  assert.equal(opened.code, 'interface-version-mismatch');
});

test('nesting is fail-closed beyond Page -> A -> B and on any cycle', () => {
  const { quickWindow } = load();

  const parent = quickWindow.open(INV_M101);
  quickWindow.mountInstance(parent.runtimeInstanceId, null);
  const child = quickWindow.open(INV_CHILD, { parentInstanceId: parent.runtimeInstanceId });
  quickWindow.mountInstance(child.runtimeInstanceId, null);

  const tooDeep = quickWindow.open(INV_DEEP, { parentInstanceId: child.runtimeInstanceId });
  assert.equal(tooDeep.ok, false);
  assert.equal(tooDeep.code, 'depth-exceeded');

  const cycle = quickWindow.open(INV_M102, { parentInstanceId: child.runtimeInstanceId });
  assert.equal(cycle.ok, false);
  assert.equal(cycle.code, 'cycle-rejected');
  assert.equal(quickWindow.state().instances.length, 2, 'a refused open never mutates the live stack');
});

test('a stale hydration is rejected and disposes its own context', () => {
  const { quickWindow } = load();

  const stale = quickWindow.open(INV_M101);
  const winner = quickWindow.open(INV_M102);

  const mounted = quickWindow.mountInstance(stale.runtimeInstanceId, null);

  assert.equal(mounted.ok, false);
  assert.equal(mounted.code, 'stale-hydration-rejected');
  assert.equal(quickWindow.mountInstance(winner.runtimeInstanceId, null).ok, true);
  assert.equal(
    quickWindow.state().instances.map(instance => instance.invocationKey).join(','),
    INV_M102);
});

test('a write is refused on a non writable port and after disposal', () => {
  const { quickWindow, bridge } = load();

  const opened = quickWindow.open(INV_M101);
  quickWindow.mountInstance(opened.runtimeInstanceId, null);

  const readOnly = quickWindow.writePort(opened.runtimeInstanceId, 'Running', true);
  assert.equal(readOnly.ok, false);
  assert.equal(readOnly.code, 'write-rejected');
  assert.equal(bridge.getTagValue('tf100.mapping.210'), undefined);

  quickWindow.disposeInstance(opened.runtimeInstanceId);
  const afterDispose = quickWindow.writePort(opened.runtimeInstanceId, 'Start', true);
  assert.equal(afterDispose.ok, false);
  assert.equal(afterDispose.code, 'instance-missing');
  assert.equal(bridge.getTagValue('tf100.mapping.211'), undefined);
});

test('cleanup is idempotent and drops exactly its own subscriptions', () => {
  const { quickWindow, bridge } = load();

  // Two different definitions may be live at once: A as the parent, B as its child.
  const first = quickWindow.open(INV_M101);
  quickWindow.mountInstance(first.runtimeInstanceId, null);
  const second = quickWindow.open(INV_CHILD, { parentInstanceId: first.runtimeInstanceId });
  quickWindow.mountInstance(second.runtimeInstanceId, null);

  assert.equal(bridge.subscriptions(first.runtimeInstanceId).length, 2);

  assert.equal(quickWindow.disposeInstance(first.runtimeInstanceId).code, 'disposed');
  assert.equal(quickWindow.disposeInstance(first.runtimeInstanceId).code, 'already-disposed');
  assert.equal(bridge.subscriptions(first.runtimeInstanceId).length, 0);
  assert.equal(
    bridge.subscriptions(second.runtimeInstanceId).length,
    1,
    'disposing one instance never touches another');
});

test('closing asks the host then disposes the context', () => {
  const { quickWindow, intents } = load();

  const opened = quickWindow.open(INV_M101);
  quickWindow.mountInstance(opened.runtimeInstanceId, null);
  const closed = quickWindow.close(opened.runtimeInstanceId);

  assert.equal(closed.ok, true);
  assert.equal(intents.at(-1).intent.kind, 'closeQuickWindow');
  assert.equal(intents.at(-1).intent.runtimeInstanceId, opened.runtimeInstanceId);
  assert.equal(quickWindow.state().instances.length, 0);
});

test('the module owns no overlay, chrome nor focus policy', async () => {
  const fs = await import('node:fs');
  const path = await import('node:path');
  const url = await import('node:url');
  const here = path.dirname(url.fileURLToPath(import.meta.url));
  const source = fs.readFileSync(
    path.resolve(here, '../../src/ScadaBuilderV2.Rendering/Runtime/quick-window-runtime.js'),
    'utf8');
  const executable = source
    .split('\n')
    .filter(line => !line.trimStart().startsWith('*') && !line.trimStart().startsWith('/*') && !line.trimStart().startsWith('//'))
    .join('\n');

  for (const forbidden of ['createElement', 'appendChild', 'backdrop', 'qw-frame', '.focus(']) {
    assert.ok(!executable.includes(forbidden), `${forbidden} belongs to the host adapter, not the shared runtime`);
  }
});
