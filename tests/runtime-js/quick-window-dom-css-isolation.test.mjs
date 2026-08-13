import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const prototypeRoot = path.resolve(__dirname, '../../tools/prototypes/quick-window-dom-css-isolation');
const revisionFile = path.join(prototypeRoot, 'prototype.js');
const assertionsFile = path.join(prototypeRoot, 'assertions.js');

// --- Hashing helper for Task 0.3b freeze ---
function hashPrototype() {
  const files = ['prototype.js', 'prototype.css', 'index.html', 'assertions.js', 'evidence.schema.json', 'README.md']
    .map(f => path.join(prototypeRoot, f))
    .filter(p => fs.existsSync(p))
    .sort();
  const h = crypto.createHash('sha256');
  for (const p of files) h.update(fs.readFileSync(p));
  return h.digest('hex');
}

// Minimal fake DOM for Node (enough for prototype.js manager)
function createFakeDocument() {
  const elementsById = new Map();

  function fakeElement(tag) {
    const el = {
      tagName: tag.toUpperCase(),
      attributes: new Map(),
      children: [],
      parentNode: null,
      dataset: {},
      style: {},
      _listeners: new Map(),
      textContent: '',
      _innerHTML: '',
      get innerHTML() { return this._innerHTML; },
      set innerHTML(v) {
        this._innerHTML = v;
        // crude parse ids inside html for fake DOM (svg gradient sentinel)
        const idRe = /id="([^"]+)"/g;
        let m;
        while ((m = idRe.exec(v)) !== null) {
          const fakeChild = fakeElement('div');
          fakeChild.setAttribute('id', m[1]);
          // also register data-author-id if present nearby
          fakeChild.setAttribute('data-author-id', 'page-gradient');
          this.appendChild(fakeChild);
        }
      },
      get id() { return this.getAttribute('id') || ''; },
      set id(v) { this.setAttribute('id', v); },
      getAttribute(n) { return this.attributes.get(n) ?? null; },
      setAttribute(n, v) {
        const old = this.attributes.get(String(n));
        if (n === 'id' && old) elementsById.delete(old);
        this.attributes.set(String(n), String(v));
        if (n === 'id') elementsById.set(String(v), this);
      },
      hasAttribute(n) { return this.attributes.has(n); },
      removeAttribute(n) {
        if (n === 'id') elementsById.delete(this.getAttribute('id'));
        this.attributes.delete(n);
      },
      appendChild(child) {
        if (child && child.__isFragment) { for (const c of child.children) this.appendChild(c); return child; }
        child.parentNode = this; this.children.push(child);
        if (child.getAttribute && child.getAttribute('id')) elementsById.set(child.getAttribute('id'), child);
        // also register nested ids recursively (for html-parsed children)
        if (child.children) for (const nested of child.children) if (nested.getAttribute && nested.getAttribute('id')) elementsById.set(nested.getAttribute('id'), nested);
        return child;
      },
      removeChild(child) {
        const i = this.children.indexOf(child);
        if (i >= 0) {
          this.children.splice(i,1);
          child.parentNode=null;
          // unregister ids recursively
          const unregister = (node) => {
            if (node.getAttribute && node.getAttribute('id')) elementsById.delete(node.getAttribute('id'));
            if (node.children) for (const c of node.children) unregister(c);
          };
          unregister(child);
        }
        return child;
      },
      querySelector(sel) {
        // very small css selector support for tests: #id, [attr="val"], .class, tag, and combinators limited to simple
        // For prototype we delegate to brute force walk
        const all = this.querySelectorAll(sel);
        return all[0] || null;
      },
      querySelectorAll(sel) {
        const result = [];
        // support comma
        for (const part of sel.split(',')) { walk(this, part.trim(), result); }
        // dedupe
        return [...new Set(result)];
      },
      addEventListener(ev, fn) {
        if (!this._listeners.has(ev)) this._listeners.set(ev, []);
        this._listeners.get(ev).push(fn);
      },
      removeEventListener(ev, fn) {
        const arr = this._listeners.get(ev) || [];
        const idx = arr.indexOf(fn); if (idx>=0) arr.splice(idx,1);
      },
      dispatchEvent(evt) {
        const arr = this._listeners.get(evt.type) || [];
        for (const fn of [...arr]) fn(evt);
        return true;
      },
      click() { this.dispatchEvent({ type: 'click', bubbles:true }); },
      focus() { fakeDoc.activeElement = this; },
      contains(other) { let n = other; while (n) { if (n===this) return true; n=n.parentNode; } return false; },
      get isConnected() { return !!this.parentNode || this===fakeDoc.body || this===fakeDoc.documentElement; }
    };
    // classList-like
    Object.defineProperty(el, 'className', { get(){ return el.getAttribute('class')||''; }, set(v){ el.setAttribute('class', v); }});
    return el;
  }

  function walk(root, selector, out) {
    if (!selector) return;
    const tokens = selector.split(/\s+/).filter(Boolean);
    function parseToken(token) {
      // Extract attrs first to avoid # inside value being mistaken for id
      const attrs = [];
      const attrRe = /\[([^\]=]+)(?:="([^"]*)")?\]/g;
      let am, tokenWithoutAttrs = token;
      while ((am = attrRe.exec(token)) !== null) attrs.push({ name: am[1], value: am[2] });
      tokenWithoutAttrs = token.replace(attrRe, '');
      let tag = null, id = null, classes = [];
      const tagMatch = tokenWithoutAttrs.match(/^([A-Za-z][A-Za-z0-9-]*|\*)/);
      if (tagMatch) { tag = tagMatch[1]; }
      const idMatch = tokenWithoutAttrs.match(/#([A-Za-z0-9_-]+)/);
      if (idMatch) id = idMatch[1];
      const classRe = /\.([A-Za-z0-9_-]+)/g; let cm;
      while ((cm = classRe.exec(tokenWithoutAttrs)) !== null) classes.push(cm[1]);
      return { tag, id, classes, attrs };
    }
    function matches(el, token) {
      if (!el || !el.getAttribute) return false;
      const { tag, id, classes, attrs } = parseToken(token);
      if (tag && tag !== '*' && el.tagName && el.tagName.toLowerCase() !== tag.toLowerCase()) return false;
      if (id && el.getAttribute('id') !== id) return false;
      for (const cls of classes) if (!(el.getAttribute('class')||'').split(/\s+/).includes(cls)) return false;
      for (const a of attrs) {
        const have = el.getAttribute(a.name);
        if (a.value === undefined) { if (have === null) return false; } else if (have !== a.value) return false;
      }
      // if token had none of above but is bare tag/class/id etc, already handled
      // also support bare [attr] without tag already via attrs
      // fallback: if token is exactly "*", handled via tag
      // If token was like "input" with no tag capture but tag==null, we need to handle
      if (!tag && !id && classes.length===0 && attrs.length===0) {
        // maybe token is like "input" not captured due to regex order? handle bare tag lowercasing
        if (el.tagName && el.tagName.toLowerCase() === token.toLowerCase()) return true;
        return false;
      }
      return true;
    }
    function matchesChain(el, toks) {
      // descendant combinator: last token must match el, previous tokens must match ancestor chain in order
      if (!matches(el, toks[toks.length-1])) return false;
      let anc = el.parentNode;
      for (let i = toks.length-2; i>=0; i--) {
        let found=false;
        while (anc) { if (matches(anc, toks[i])) { found=true; anc=anc.parentNode; break; } anc=anc.parentNode; }
        if (!found) return false;
      }
      return true;
    }
    function dfs(node) {
      for (const child of node.children || []) {
        if (matchesChain(child, tokens)) out.push(child);
        dfs(child);
      }
    }
    dfs(root);
  }

  const fakeDoc = {
    elementsById,
    activeElement: null,
    createElement(tag) { return fakeElement(tag); },
    createDocumentFragment() { const f = { __isFragment:true, children:[], appendChild(c){ this.children.push(c); return c; } }; return f; },
    getElementById(id) { return elementsById.get(id) || null; },
    querySelectorAll(sel) { return this.documentElement.querySelectorAll(sel); },
    querySelector(sel) { return this.querySelectorAll(sel)[0] || null; },
    addEventListener() {},
    removeEventListener() {},
    defaultView: { KeyboardEvent: class { constructor(t, init){ this.type=t; this.key=init.key; this.bubbles=init.bubbles; } }, performance: { now(){ return Date.now(); } } }
  };
  fakeDoc.documentElement = fakeElement('html');
  fakeDoc.body = fakeElement('body');
  fakeDoc.documentElement.appendChild(fakeDoc.body);
  fakeDoc.documentElement.getAttribute = fakeDoc.body.getAttribute.bind(fakeDoc.body);
  // hostRoot + pageRoot
  const hostRoot = fakeDoc.createElement('div'); hostRoot.setAttribute('id','qw-host-root'); fakeDoc.body.appendChild(hostRoot);
  const pageRoot = fakeDoc.createElement('div'); pageRoot.setAttribute('id','page-root'); fakeDoc.body.appendChild(pageRoot);
  const pageBtn = fakeDoc.createElement('button'); pageBtn.setAttribute('id','page-open-a'); pageBtn.textContent='open'; pageRoot.appendChild(pageBtn);
  return { doc: fakeDoc, hostRoot, pageRoot };
}

function loadPrototypeInVm(doc) {
  const srcProto = fs.readFileSync(revisionFile, 'utf8');
  const srcAssert = fs.readFileSync(assertionsFile, 'utf8');
  const sandbox = {
    console, setTimeout, clearTimeout, setInterval, clearInterval,
    performance: { now: () => Date.now() },
    document: doc,
    window: { document: doc, QuickWindowPrototype: undefined, performance: { now: ()=>Date.now() } },
    globalThis: {},
    module: { exports: {} }
  };
  sandbox.global = sandbox.window;
  sandbox.globalThis = sandbox.window;
  sandbox.window.globalThis = sandbox.window;
  sandbox.window.setTimeout = setTimeout; sandbox.window.clearTimeout = clearTimeout;
  sandbox.window.document = doc;
  vm.createContext(sandbox);
  vm.runInContext(srcProto, sandbox, { filename: 'prototype.js' });
  // After prototype, window.QuickWindowPrototype exists
  sandbox.window.QuickWindowPrototype = sandbox.window.QuickWindowPrototype || sandbox.QuickWindowPrototype || sandbox.global.QuickWindowPrototype;
  if (!sandbox.window.QuickWindowPrototype) {
    // Try globalThis
    sandbox.window.QuickWindowPrototype = sandbox.global.QuickWindowPrototype;
  }
  // Also eval assertions which attaches QuickWindowAssertions to window/globalThis
  // Provide global reference
  sandbox.global.QuickWindowPrototype = sandbox.window.QuickWindowPrototype;
  sandbox.globalThis.QuickWindowPrototype = sandbox.window.QuickWindowPrototype;
  vm.runInContext(srcAssert, sandbox, { filename: 'assertions.js' });
  const P = sandbox.window.QuickWindowPrototype || sandbox.global.QuickWindowPrototype || sandbox.QuickWindowPrototype;
  const A = sandbox.window.QuickWindowAssertions || sandbox.global.QuickWindowAssertions;
  return { P, A, sandbox };
}

test('prototype hash is stable and PrototypeRevision is 1.0.2', () => {
  const proto = fs.readFileSync(revisionFile, 'utf8');
  assert.match(proto, /PrototypeRevision\s*=\s*'1\.0\.2'/);
  const hash = hashPrototype();
  assert.equal(hash.length, 64);
  // Stored hash file may not exist yet - just ensure computed
  assert.ok(/^[a-f0-9]{64}$/.test(hash));
});

test('fixture DOM/CSS isolation: Page -> A -> B, ids/classes/styles isolated', async () => {
  const { doc, hostRoot, pageRoot } = createFakeDocument();
  const { P, A } = loadPrototypeInVm(doc);
  assert.ok(P, 'QuickWindowPrototype loaded');
  assert.ok(A, 'QuickWindowAssertions loaded');
  assert.equal(P.PrototypeRevision, '1.0.2');
  assert.equal(P.TagCache.pollerCount, 1);
  P.Instrument.captureBaseline();
  P.TagCache.reset();
  const manager = new P.QuickWindowManager(doc);
  manager.setHostRoot(hostRoot);
  manager.setPageRoot(pageRoot);
  const ctx = { doc, manager, pageRoot, hostRoot };
  const core = await A.runCoreAssertions(ctx);
  const perf = await A.runPerformance(ctx);
  const all = [...core, ...perf.assertions];
  const failed = all.filter(a => a.status === 'FAIL');
  if (failed.length) {
    console.error('Failed assertions:', JSON.stringify(failed, null, 2));
  }
  assert.equal(failed.length, 0, `Core+perf must be 100% PASS, failed: ${failed.map(f=>f.id).join(', ')}`);
  // metrics sanity
  assert.ok(perf.metrics.p95HotMs <= 500, `p95 hot ${perf.metrics.p95HotMs} <=500`);
  assert.ok(perf.metrics.p95ColdMs <= 1500, `p95 cold ${perf.metrics.p95ColdMs} <=1500`);
  assert.equal(P.TagCache.subscriptionCount(), 0, 'subscriptions cleared after dispose');
  assert.equal(P.Instrument.deltaListeners(), 0, 'listeners zero after dispose');
});

test('lifecycle: 100 cycles, cascade, stale hydration, races, depth/cycle rejection', async () => {
  const { doc, hostRoot, pageRoot } = createFakeDocument();
  const { P } = loadPrototypeInVm(doc);
  const manager = new P.QuickWindowManager(doc);
  manager.setHostRoot(hostRoot);
  P.TagCache.reset();
  P.Instrument.captureBaseline();
  for (let i=0;i<100;i++) { await manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101); await manager.close(P.DefinitionKeys.A); }
  assert.equal(P.TagCache.pollerCount, 1);
  assert.equal(P.TagCache.subscriptionCount(), 0);
  assert.equal(P.Instrument.deltaListeners(), 0, 'listeners zero after 100 cycles');
  // depth 3 rejection explicit using C as third level
  await manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101);
  await manager.open(P.DefinitionKeys.B, P.InvocationKeys.B_CHILD, P.DefinitionKeys.A);
  let thrownDepth = null;
  try { await manager.open(P.DefinitionKeys.C, P.InvocationKeys.C_CHILD, P.DefinitionKeys.B); } catch(e){ thrownDepth=e; }
  assert.ok(thrownDepth && (thrownDepth.code==='depth-exceeded' || thrownDepth.message==='depth-exceeded'), 'depth-exceeded for Page->A->B->C');
  let thrownCycle = null;
  try { await manager.open(P.DefinitionKeys.A, P.InvocationKeys.A_M101, P.DefinitionKeys.B); } catch(e){ thrownCycle=e; }
  assert.ok(thrownCycle && (thrownCycle.code==='cycle' || thrownCycle.message==='cycle'), 'cycle A->B->A');
  await manager.close(P.DefinitionKeys.A);
  assert.equal(manager._active.size, 0);
});

test('engine Node pinned: .nvmrc and engines.node must match 20.18.x', () => {
  const nvmrc = fs.readFileSync(path.resolve(__dirname,'../../.nvmrc'),'utf8').trim();
  const pkg = JSON.parse(fs.readFileSync(path.join(__dirname,'package.json'),'utf8'));
  assert.match(nvmrc, /^20\.18\./);
  assert.equal(pkg.engines.node, '20.18.x');
  assert.match(process.version, /^v20\.18\./, `installed Node ${process.version} must match the Phase 0 gate`);
});

test('PrototypeRevision hash file is frozen and synchronized (Task 0.3b)', () => {
  const h1 = hashPrototype();
  const h2 = hashPrototype();
  assert.equal(h1, h2);
  const frozen = fs.readFileSync(path.join(prototypeRoot, 'prototype.sha256'), 'utf8').trim();
  assert.equal(frozen, h1);
});
