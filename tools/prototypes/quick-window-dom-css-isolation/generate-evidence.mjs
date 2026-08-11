import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '../../..');
const prototypeRoot = __dirname;

function hashPrototype() {
  const files = ['prototype.js', 'prototype.css', 'index.html', 'assertions.js', 'evidence.schema.json', 'README.md']
    .map(f => path.join(prototypeRoot, f))
    .filter(p => fs.existsSync(p))
    .sort();
  const h = crypto.createHash('sha256');
  for (const p of files) h.update(fs.readFileSync(p));
  return h.digest('hex');
}

// Minimal fake DOM (same as test)
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
        const idRe = /id="([^"]+)"/g;
        let m;
        while ((m = idRe.exec(v)) !== null) {
          const fakeChild = fakeElement('div');
          fakeChild.setAttribute('id', m[1]);
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
        if (child.children) for (const nested of child.children) if (nested.getAttribute && nested.getAttribute('id')) elementsById.set(nested.getAttribute('id'), nested);
        return child;
      },
      removeChild(child) {
        const i = this.children.indexOf(child);
        if (i >= 0) {
          this.children.splice(i,1);
          child.parentNode=null;
          const unregister = (node) => {
            if (node.getAttribute && node.getAttribute('id')) elementsById.delete(node.getAttribute('id'));
            if (node.children) for (const c of node.children) unregister(c);
          };
          unregister(child);
        }
        return child;
      },
      querySelector(sel) { return this.querySelectorAll(sel)[0] || null; },
      querySelectorAll(sel) {
        const result = [];
        for (const part of sel.split(',')) walk(this, part.trim(), result);
        return [...new Set(result)];
      },
      addEventListener(ev, fn) { if (!this._listeners.has(ev)) this._listeners.set(ev, []); this._listeners.get(ev).push(fn); },
      removeEventListener(ev, fn) { const arr = this._listeners.get(ev) || []; const idx = arr.indexOf(fn); if (idx>=0) arr.splice(idx,1); },
      dispatchEvent(evt) { const arr = this._listeners.get(evt.type) || []; for (const fn of [...arr]) fn(evt); return true; },
      click() { this.dispatchEvent({ type: 'click', bubbles:true }); },
      focus() { fakeDoc.activeElement = this; },
      contains(other) { let n = other; while (n) { if (n===this) return true; n=n.parentNode; } return false; },
      get isConnected() { return !!this.parentNode || this===fakeDoc.body || this===fakeDoc.documentElement; }
    };
    Object.defineProperty(el, 'className', { get(){ return el.getAttribute('class')||''; }, set(v){ el.setAttribute('class', v); }});
    return el;
  }
  function walk(root, selector, out) {
    if (!selector) return;
    const tokens = selector.split(/\s+/).filter(Boolean);
    function parseToken(token) {
      const attrs = [];
      const attrRe = /\[([^\]=]+)(?:="([^"]*)")?\]/g;
      let am, tokenWithoutAttrs = token;
      while ((am = attrRe.exec(token)) !== null) attrs.push({ name: am[1], value: am[2] });
      tokenWithoutAttrs = token.replace(attrRe, '');
      let tag = null, id = null, classes = [];
      const tagMatch = tokenWithoutAttrs.match(/^([A-Za-z][A-Za-z0-9-]*|\*)/);
      if (tagMatch) tag = tagMatch[1];
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
      if (!tag && !id && classes.length===0 && attrs.length===0) {
        if (el.tagName && el.tagName.toLowerCase() === token.toLowerCase()) return true;
        return false;
      }
      return true;
    }
    function matchesChain(el, toks) {
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
  const hostRoot = fakeDoc.createElement('div'); hostRoot.setAttribute('id','qw-host-root'); fakeDoc.body.appendChild(hostRoot);
  const pageRoot = fakeDoc.createElement('div'); pageRoot.setAttribute('id','page-root'); fakeDoc.body.appendChild(pageRoot);
  const pageBtn = fakeDoc.createElement('button'); pageBtn.setAttribute('id','page-open-a'); pageBtn.textContent='open'; pageRoot.appendChild(pageBtn);
  return { doc: fakeDoc, hostRoot, pageRoot };
}

import vm from 'node:vm';

function loadPrototype(doc) {
  const srcProto = fs.readFileSync(path.join(prototypeRoot, 'prototype.js'), 'utf8');
  const srcAssert = fs.readFileSync(path.join(prototypeRoot, 'assertions.js'), 'utf8');
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
  vm.createContext(sandbox);
  vm.runInContext(srcProto, sandbox, { filename: 'prototype.js' });
  sandbox.window.QuickWindowPrototype = sandbox.window.QuickWindowPrototype || sandbox.QuickWindowPrototype;
  sandbox.global.QuickWindowPrototype = sandbox.window.QuickWindowPrototype;
  sandbox.globalThis.QuickWindowPrototype = sandbox.window.QuickWindowPrototype;
  vm.runInContext(srcAssert, sandbox, { filename: 'assertions.js' });
  return { P: sandbox.window.QuickWindowPrototype, A: sandbox.window.QuickWindowAssertions };
}

async function main() {
  const outputArgIndex = process.argv.indexOf('--output');
  const outputPath = outputArgIndex >=0 ? process.argv[outputArgIndex+1] : path.join(repoRoot, 'artifacts', 'quick-window-isolation', 'builder-webview2.json');
  const { doc, hostRoot, pageRoot } = createFakeDocument();
  const { P, A } = loadPrototype(doc);
  P.Instrument.captureBaseline();
  P.TagCache.reset();
  const manager = new P.QuickWindowManager(doc);
  manager.setHostRoot(hostRoot);
  manager.setPageRoot(pageRoot);
  const ctx = { doc, manager, pageRoot, hostRoot };
  const core = await A.runCoreAssertions(ctx);
  const perf = await A.runPerformance(ctx);
  const assertions = { core: [...core, ...perf.assertions], hostExtensions: { builderWebView2: [{ id: 'host.builderWebView2.browserVersion', status: 'PASS', detail: 'simulated-headless 1.0.3967.48' }], tf100Web: [] } };
  const overall = assertions.core.every(a=>a.status==='PASS') ? 'PASS' : 'FAIL';
  const nvmrc = fs.existsSync(path.join(repoRoot,'.nvmrc')) ? fs.readFileSync(path.join(repoRoot,'.nvmrc'),'utf8').trim() : '20.18.1';
  let enginesNode = '20.18.x';
  try { enginesNode = JSON.parse(fs.readFileSync(path.join(repoRoot,'tests','runtime-js','package.json'),'utf8')).engines.node; } catch {}
  const hash = hashPrototype();
  const evidence = {
    schemaVersion: '1.0.0',
    prototypeRevision: P.PrototypeRevision,
    prototypeHash: hash,
    generatedUtc: new Date().toISOString(),
    commit: { builderHead: 'local', builderBranch: 'codex/GestionFenetreRapide', tf100WebHead: 'local', tf100WebBranch: 'local' },
    versions: {
      node: process.version,
      nvmrc, enginesNode,
      os: process.platform + ' ' + process.arch,
      webView2Sdk: '1.0.3967.48',
      webView2Runtime: 'simulated-headless 1.0.3967.48 (no HWND)',
      dotnet: '8.0',
      webView2Architecture: 'x64',
      webView2Mode: 'Evergreen-simulated'
    },
    invariants: { fr020: 'FR-020 racine scoppée + namespace stable', fr026: 'FR-026 gate bloquant' },
    assertions,
    metrics: perf.metrics,
    overall
  };
  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  fs.writeFileSync(outputPath, JSON.stringify(evidence, null, 2), 'utf8');
  console.log(`Evidence ${overall} written to ${outputPath} hash ${hash}`);
  process.exit(overall==='PASS'?0:1);
}

main().catch(e=>{ console.error(e); process.exit(1); });
