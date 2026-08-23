import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const RUNTIME_DIR = path.resolve(__dirname, '../../src/ScadaBuilderV2.Rendering/Runtime');

/**
 * Minimal DOM sufficient for the quick-window host manager: element creation, attributes,
 * append/remove, class/attribute selectors, listeners and focus tracking.
 */
function createFakeDocument() {
  const state = { activeElement: null, listenerCount: 0 };

  function element(tag) {
    const node = {
      tagName: String(tag).toUpperCase(),
      attributes: new Map(),
      children: [],
      parentNode: null,
      classList: new Set(),
      listeners: [],
      innerHTML: '',
      getAttribute(name) { return node.attributes.has(name) ? node.attributes.get(name) : null; },
      setAttribute(name, value) {
        node.attributes.set(name, String(value));
        if (name === 'class') {
          node.classList = new Set(String(value).split(/\s+/).filter(Boolean));
        }
      },
      appendChild(child) {
        if (child.parentNode) child.parentNode.removeChild(child);
        child.parentNode = node;
        node.children.push(child);
        return child;
      },
      removeChild(child) {
        const index = node.children.indexOf(child);
        if (index >= 0) {
          node.children.splice(index, 1);
          child.parentNode = null;
        }
        return child;
      },
      addEventListener(event, handler) {
        node.listeners.push({ event, handler });
        state.listenerCount++;
      },
      removeEventListener(event, handler) {
        const index = node.listeners.findIndex(entry => entry.event === event && entry.handler === handler);
        if (index >= 0) {
          node.listeners.splice(index, 1);
          state.listenerCount--;
        }
      },
      dispatch(event, payload) {
        for (const entry of [...node.listeners]) {
          if (entry.event === event) entry.handler(payload || {});
        }
      },
      focus() { state.activeElement = node; },
      querySelector(selector) { return descendants(node).find(candidate => matches(candidate, selector)) || null; },
      querySelectorAll(selector) { return descendants(node).filter(candidate => matches(candidate, selector)); }
    };
    return node;
  }

  function descendants(node) {
    const all = [];
    for (const child of node.children) {
      all.push(child);
      all.push(...descendants(child));
    }
    return all;
  }

  function matches(node, selector) {
    if (selector.startsWith('.')) return node.classList.has(selector.slice(1));
    if (selector.startsWith('[') && selector.endsWith(']')) {
      const inner = selector.slice(1, -1);
      const equals = inner.indexOf('=');
      if (equals < 0) return node.attributes.has(inner);
      const name = inner.slice(0, equals);
      const value = inner.slice(equals + 1).replace(/^["']|["']$/g, '');
      return node.getAttribute(name) === value;
    }
    return node.tagName === selector.toUpperCase();
  }

  const body = element('div');
  body.setAttribute('id', 'body');

  return {
    body,
    get activeElement() { return state.activeElement; },
    createElement: element,
    getElementById(id) { return descendants(body).find(node => node.getAttribute('id') === id) || null; },
    listenerCount() { return state.listenerCount; }
  };
}

function loadHost() {
  const fakeDocument = createFakeDocument();
  const sandbox = { console };
  sandbox.window = sandbox;
  sandbox.document = fakeDocument;
  const mounted = [];
  const disposed = [];
  const tagValues = {};
  sandbox.ScadaRuntime = {
    initPage(container, id) { mounted.push({ container, id }); },
    disposePage(container) { disposed.push(container); },
    onTagValuesChanged(values) { Object.assign(tagValues, values); },
    TagBridge: {
      setValues(values) { Object.assign(tagValues, values); }
    }
  };
  const context = vm.createContext(sandbox);
  vm.runInContext(fs.readFileSync(path.join(RUNTIME_DIR, 'quick-window-host.js'), 'utf8'), context, {
    filename: 'quick-window-host.js'
  });
  return { host: sandbox.ScadaRuntime.QuickWindowHost, document: fakeDocument, mounted, disposed, tagValues };
}

function createRootFactory(fakeDocument) {
  return () => {
    const root = fakeDocument.createElement('div');
    const frame = fakeDocument.createElement('div');
    frame.setAttribute('class', 'qw-frame');
    const close = fakeDocument.createElement('button');
    close.setAttribute('data-qw-close', 'self');
    frame.appendChild(close);
    root.appendChild(frame);
    return root;
  };
}

const DEFINITION_A = 'a1b2c3d4-1111-4222-8333-aaaaaaaaaaaa';
const DEFINITION_B = 'e5f6a7b8-2222-4333-8444-bbbbbbbbbbbb';
const INVOCATION_M101 = 'inv-m101';
const INVOCATION_M102 = 'inv-m102';

test('same invocation of the same definition is brought to front, never recreated', () => {
  const { host, document, mounted } = loadHost();
  const createRoot = createRootFactory(document);

  const first = host.open({ definitionKey: DEFINITION_A, invocationKey: INVOCATION_M101, createRoot });
  const second = host.open({ definitionKey: DEFINITION_A, invocationKey: INVOCATION_M101, createRoot });

  assert.equal(first.reused, false);
  assert.equal(second.reused, true, 'the same invocation is focused, not recreated');
  assert.equal(second.runtimeInstanceId, first.runtimeInstanceId);
  assert.equal(mounted.length, 1, 'the shared runtime mounts exactly one context');
  assert.equal(host.state().instances.length, 1);
});

test('another invocation of the same definition closes, disposes then recreates the context', () => {
  const { host, document, mounted, disposed } = loadHost();
  const createRoot = createRootFactory(document);

  const first = host.open({ definitionKey: DEFINITION_A, invocationKey: INVOCATION_M101, createRoot });
  const second = host.open({ definitionKey: DEFINITION_A, invocationKey: INVOCATION_M102, createRoot });

  assert.equal(second.reused, false);
  assert.notEqual(second.runtimeInstanceId, first.runtimeInstanceId);
  assert.ok(second.generation > first.generation, 'generations stay monotonic');
  assert.equal(disposed.length, 1, 'the previous context is disposed through the shared runtime');
  assert.equal(mounted.length, 2);
  const state = host.state();
  assert.equal(state.instances.length, 1, 'SinglePerDefinition keeps one live context per definition');
  assert.equal(state.instances[0].invocationKey, INVOCATION_M102);
});

test('no mapping leaks between two invocations of the same definition', () => {
  const { host, document, tagValues } = loadHost();
  const createRoot = createRootFactory(document);

  host.open({ definitionKey: DEFINITION_A, invocationKey: INVOCATION_M101, createRoot, testValues: { 'tf100.mapping.210': 'M101' } });
  assert.equal(tagValues['tf100.mapping.210'], 'M101');

  const second = host.open({ definitionKey: DEFINITION_A, invocationKey: INVOCATION_M102, createRoot, testValues: { 'tf100.mapping.310': 'M102' } });
  const root = second.instance.root;

  assert.equal(root.getAttribute('data-qw-inv'), INVOCATION_M102);
  assert.equal(root.getAttribute('data-qw-generation'), String(second.generation));
  assert.equal(host.state().instances.filter(instance => instance.invocationKey === INVOCATION_M101).length, 0);
});

test('X, Escape and Self all close the instance and clean it up', () => {
  const { host, document, disposed } = loadHost();
  const createRoot = createRootFactory(document);
  host.setHostRoot(document.body);

  const opened = host.open({ definitionKey: DEFINITION_A, invocationKey: INVOCATION_M101, createRoot });
  const listenersWhileOpen = document.listenerCount();
  opened.instance.root.querySelector('[data-qw-close=self]').dispatch('click');

  assert.equal(host.state().instances.length, 0);
  assert.equal(disposed.length, 1);
  assert.ok(document.listenerCount() < listenersWhileOpen, 'every listener is removed on dispose');
  assert.equal(document.body.children.length, 0, 'the instance root and the backdrop are removed');

  const byEscape = host.open({ definitionKey: DEFINITION_A, invocationKey: INVOCATION_M101, createRoot });
  byEscape.instance.frame.dispatch('keydown', { key: 'Escape' });
  assert.equal(host.state().instances.length, 0);

  const bySelf = host.open({ definitionKey: DEFINITION_A, invocationKey: INVOCATION_M101, createRoot });
  const inner = document.createElement('span');
  bySelf.instance.frame.appendChild(inner);
  assert.equal(host.closeSelf(inner), true, 'CloseQuickWindow(Self) resolves its own instance');
  assert.equal(host.state().instances.length, 0);
});

test('a backdrop click never closes a quick window and the backdrop is shared', () => {
  const { host, document } = loadHost();
  const createRoot = createRootFactory(document);
  host.setHostRoot(document.body);

  host.open({ definitionKey: DEFINITION_A, invocationKey: INVOCATION_M101, createRoot });
  host.open({ definitionKey: DEFINITION_B, invocationKey: 'inv-child', parentDefinitionKey: DEFINITION_A, createRoot });

  const backdrops = document.body.children.filter(child => child.getAttribute('data-qw-backdrop') === 'true');
  assert.equal(backdrops.length, 1, 'a single backdrop is shared by the whole stack');

  backdrops[0].dispatch('click');
  assert.equal(host.state().instances.length, 2, 'a backdrop click never closes anything');
});

test('nesting stays fail-closed: cycles and depth beyond Page -> A -> B are rejected', () => {
  const { host, document } = loadHost();
  const createRoot = createRootFactory(document);
  host.setHostRoot(document.body);

  host.open({ definitionKey: DEFINITION_A, invocationKey: INVOCATION_M101, createRoot });
  host.open({ definitionKey: DEFINITION_B, invocationKey: 'inv-child', parentDefinitionKey: DEFINITION_A, createRoot });

  assert.throws(
    () => host.open({ definitionKey: DEFINITION_A, invocationKey: 'inv-cycle', parentDefinitionKey: DEFINITION_B, createRoot }),
    error => error.code === 'cycle');
  assert.throws(
    () => host.open({ definitionKey: 'c9d0e1f2-3333-4444-8555-ffffffffffff', invocationKey: 'inv-deep', parentDefinitionKey: DEFINITION_B, createRoot }),
    error => error.code === 'depth-exceeded');
  assert.equal(host.state().instances.length, 2, 'a rejected open never mutates the live stack');
});

test('closing a parent cascades to every context opened above it', () => {
  const { host, document, disposed } = loadHost();
  const createRoot = createRootFactory(document);
  host.setHostRoot(document.body);

  host.open({ definitionKey: DEFINITION_A, invocationKey: INVOCATION_M101, createRoot });
  host.open({ definitionKey: DEFINITION_B, invocationKey: 'inv-child', parentDefinitionKey: DEFINITION_A, createRoot });

  host.close(DEFINITION_A);

  assert.equal(host.state().instances.length, 0);
  assert.equal(disposed.length, 2, 'the child context is disposed before its parent');
  assert.equal(document.body.children.length, 0);
});
