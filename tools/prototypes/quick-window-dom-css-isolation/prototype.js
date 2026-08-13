/* QuickWindow DOM/CSS isolation prototype | PrototypeRevision 1.0.2 | FR-020 gate
 * Host-agnostic. No src/ modification. Runs in browser (WebView2) and Node vm.
 * Exposes window.QuickWindowPrototype for browser and ES module exports for Node.
 * Implements:
 *  - Page -> A -> B deterministic nesting (depth 2 valid, depth 3 rejected, cycles rejected)
 *  - Stable namespace derived from QuickWindowDefinitionKey: qw-<first8>
 *  - SinglePerDefinition, generation monotonic, stale hydration rejection
 *  - Root-scoped queries, attribute triple, shared TagCache/Poller/Bridge instrumentation
 */
(function (global) {
  'use strict';

  const PrototypeRevision = '1.0.2';

  // Canonical identities as per FR-027 (no fourth identifier)
  const DefinitionKeys = {
    A: 'a1b2c3d4-1111-4222-8333-aaaaaaaaaaaa',
    B: 'e5f6a7b8-2222-4333-8444-bbbbbbbbbbbb',
    C: 'c9d0e1f2-3333-4444-8555-ffffffffffff'
  };
  const InvocationKeys = {
    A_M101: 'inv-a-m101-3333-4444-8555-cccccccccccc',
    A_M102: 'inv-a-m102-3333-4444-8555-dddddddddddd',
    B_CHILD: 'inv-b-child-4444-5555-8666-eeeeeeeeeeee',
    C_CHILD: 'inv-c-child-5555-6666-8777-gggggggggggg'
  };

  function namespaceFor(definitionKey) {
    return 'qw-' + definitionKey.slice(0, 8).toLowerCase();
  }

  // Sentinel element ids that are intentionally identical in authoring but must be isolated by namespace
  const SentinelAuthorIds = ['sensorValue', 'actionButton', 'statusLabel', 'page-gradient'];

  // --- Simulated runtime services (singletons) ---
  const TagCache = {
    pollerCount: 1,
    subscriptions: new Map(),
    dependencies: new Map(),
    writes: [],
    subscribe(tagId, owner) {
      const key = tagId + '|' + owner;
      this.subscriptions.set(key, (this.subscriptions.get(key) || 0) + 1);
      this.dependencies.set(owner, (this.dependencies.get(owner) || 0) + 1);
    },
    unsubscribeAll(owner) {
      for (const k of [...this.subscriptions.keys()]) if (k.endsWith('|' + owner)) this.subscriptions.delete(k);
      this.dependencies.delete(owner);
    },
    write(tagId, value, owner) {
      this.writes.push({ tagId, value, owner, ts: Date.now() });
    },
    reset() {
      this.subscriptions.clear(); this.dependencies.clear(); this.writes.length = 0;
    },
    subscriptionCount() { return this.subscriptions.size; }
  };

  const Instrument = {
    listeners: 0,
    observers: 0,
    timers: 0,
    _baselineListeners: 0,
    _baselineObservers: 0,
    _baselineTimers: 0,
    captureBaseline() {
      this._baselineListeners = this.listeners;
      this._baselineObservers = this.observers;
      this._baselineTimers = this.timers;
    },
    deltaListeners() { return this.listeners - this._baselineListeners; },
    deltaObservers() { return this.observers - this._baselineObservers; },
    deltaTimers() { return this.timers - this._baselineTimers; },
    reset() { this.listeners = this.observers = this.timers = 0; }
  };

  // --- DOM helper (works with real document or fake document for Node) ---
  function createElement(doc, tag, attrs, children) {
    const el = doc.createElement(tag);
    if (attrs) for (const [k, v] of Object.entries(attrs)) {
      if (k === 'text') el.textContent = v;
      else if (k === 'html') el.innerHTML = v;
      else el.setAttribute(k, v);
    }
    if (children) for (const c of children) el.appendChild(c);
    return el;
  }

  function bindingFor(definitionKey, invocationKey) {
    if (definitionKey === DefinitionKeys.A && invocationKey === InvocationKeys.A_M101)
      return { readTagId: 'tf100.mapping.210', writeTagId: 'tf100.mapping.211' };
    if (definitionKey === DefinitionKeys.A && invocationKey === InvocationKeys.A_M102)
      return { readTagId: 'tf100.mapping.310', writeTagId: 'tf100.mapping.311' };
    if (definitionKey === DefinitionKeys.B)
      return { readTagId: 'tf100.mapping.410', writeTagId: 'tf100.mapping.411' };
    return { readTagId: 'tf100.mapping.510', writeTagId: 'tf100.mapping.511' };
  }

  // Build content fragment for a definition with intentionally colliding author ids
  function buildVisualContent(doc, definitionKey, invocationKey) {
    const ns = namespaceFor(definitionKey);
    const binding = bindingFor(definitionKey, invocationKey);
    const frag = doc.createDocumentFragment();
    // id, for, aria-*, href, url(#...), class, animation, table, input, state/command targets all present
    const label = createElement(doc, 'label', { for: ns + '__sensorValue', text: 'Sensor ' + ns });
    const input = createElement(doc, 'input', { id: ns + '__sensorValue', type: 'text', value: '—', 'aria-describedby': ns + '__statusLabel' });
    // original author id would have been "sensorValue" – namespaced version proves isolation
    input.setAttribute('data-author-id', 'sensorValue');
    input.setAttribute('data-scada-role', 'sensor-input');
    input.setAttribute('data-scada-mapping-id', binding.readTagId);

    const button = createElement(doc, 'button', { id: ns + '__actionButton', text: 'Action', 'aria-controls': ns + '__statusLabel', class: 'sentinel shared-class' });
    button.setAttribute('data-author-id', 'actionButton');
    button.setAttribute('data-scada-command-config', JSON.stringify({ commands: [{ kind: 'writeTag', writeTagId: binding.writeTagId }] }));

    const status = createElement(doc, 'div', { id: ns + '__statusLabel', class: 'sentinel shared-class', text: 'Status —' });
    status.setAttribute('data-author-id', 'statusLabel');
    status.setAttribute('data-scada-state-config', '{}');

    // svg with url(#...) reference
    const svg = createElement(doc, 'div', { html: `<svg width="40" height="20"><defs><linearGradient id="${ns}__page-gradient"><stop offset="0%" stop-color="red"/></linearGradient></defs><rect class="svg-sentinel" width="40" height="20"/></svg>` });
    svg.firstChild?.setAttribute?.('data-author-id', 'page-gradient');

    // table sentinel
    const table = createElement(doc, 'table', { class: 'sentinel-table' });
    const tr = createElement(doc, 'tr'); tr.appendChild(createElement(doc, 'td', { text: ns + ' cell' })); table.appendChild(tr);

    // href / xlink:href sentinel
    const link = createElement(doc, 'a', { href: '#' + ns + '__sensorValue', text: 'jump' });

    // animation sentinel
    const anim = createElement(doc, 'div', { class: 'sentinel animated', text: 'anim ' + ns });

    frag.appendChild(label);
    frag.appendChild(input);
    frag.appendChild(button);
    frag.appendChild(status);
    frag.appendChild(svg);
    frag.appendChild(table);
    frag.appendChild(link);
    frag.appendChild(anim);
    return frag;
  }

  // QuickWindowManager
  class QuickWindowManager {
    constructor(doc) {
      this.doc = doc;
      this._generations = 0;
      this._active = new Map(); // defKey -> instance
      this._allInstances = new Map(); // instanceId -> instance
      this._stack = []; // ordered open stack for Page->A->B
      this._hostRoot = null;
      this._pageRoot = null;
      this._backdrop = null;
    }
    setHostRoot(el) { this._hostRoot = el; }
    setPageRoot(el) { this._pageRoot = el; }

    _nextGeneration() { return ++this._generations; }

    _isActive(definitionKey) { return this._active.has(definitionKey); }

    // Validate depth 2 and cycle
    _validateOpen(definitionKey, parentDefinitionKey) {
      if (parentDefinitionKey) {
        if (definitionKey === parentDefinitionKey) return { ok: false, code: 'cycle' };
        // cycle direct A->B->A check: if target already in stack ancestor
        if (this._stack.some(entry => entry.definitionKey === definitionKey)) return { ok: false, code: 'cycle' };
        // depth check: if parent already is depth 2, reject
        const parentDepth = this._depthOf(parentDefinitionKey);
        if (parentDepth >= 2) return { ok: false, code: 'depth-exceeded' };
      } else {
        // page->A depth 1 always ok unless depth exceeded elsewhere
        if (this._stack.length >= 2) {
          // Page->A->B already 2, opening another at page level while both active should close previous per SinglePerDefinition? Allow.
        }
      }
      return { ok: true };
    }

    _depthOf(definitionKey) {
      const idx = this._stack.findIndex(e => e.definitionKey === definitionKey);
      if (idx < 0) return 0;
      return idx + 1;
    }

    async open(definitionKey, invocationKey, parentDefinitionKey) {
      // Validate BEFORE any SinglePerDefinition mutation — cycles and depth must be fail-closed even if existing would be closed
      const preValidation = this._validateOpen(definitionKey, parentDefinitionKey);
      if (!preValidation.ok) {
        const err = new Error(preValidation.code);
        err.code = preValidation.code;
        throw err;
      }

      const generation = this._nextGeneration();
      const ns = namespaceFor(definitionKey);
      const runtimeInstanceId = 'qw-inst-' + generation + '-' + ns;
      const requested = { definitionKey, invocationKey, runtimeInstanceId, generation, parentDefinitionKey: parentDefinitionKey || null };

      // SinglePerDefinition: if same definition already active
      const existing = this._active.get(definitionKey);
      if (existing) {
        if (existing.invocationKey === invocationKey) {
          // same invocation => bring to front (no recreate)
          this._bringToFront(existing);
          return { instance: existing, reused: true, generation };
        }
        // different invocation same definition => close/dispose then recreate
        await this.close(definitionKey);
      }

      // Create instance structure but start Mounting
      const instance = {
        definitionKey, invocationKey, runtimeInstanceId, generation, ns,
        state: 'Mounting',
        root: null,
        backdrop: null,
        focusReturn: this.doc.activeElement || null,
        hydrationGeneration: generation,
        subscriptionsOwner: runtimeInstanceId,
        listeners: [],
        observer: null,
        timers: [],
        disposed: false
      };
      this._allInstances.set(runtimeInstanceId, instance);
      this._active.set(definitionKey, instance);
      this._stack.push({ definitionKey, runtimeInstanceId });

      // Simulate async mounting/hydration with stale rejection
      // Create DOM root
      const root = this.doc.createElement('div');
      root.setAttribute('data-qw-def', ns);
      root.setAttribute('data-qw-inv', invocationKey);
      root.setAttribute('data-qw-inst', runtimeInstanceId);
      root.setAttribute('data-qw-generation', String(generation));
      // Scoped content
      const titleBar = createElement(this.doc, 'div', { class: 'qw-titlebar' });
      const title = createElement(this.doc, 'span', { text: (definitionKey === DefinitionKeys.A ? 'Fenêtre A' : 'Fenêtre B') + ' — ' + invocationKey.slice(0, 8) });
      const closeBtn = createElement(this.doc, 'button', { text: 'X', 'aria-label': 'Fermer', class: 'qw-close', type: 'button' });
      titleBar.appendChild(title); titleBar.appendChild(closeBtn);
      const content = createElement(this.doc, 'div', { class: 'qw-content' });
      content.appendChild(buildVisualContent(this.doc, definitionKey, invocationKey));
      const frame = createElement(this.doc, 'div', { class: 'qw-frame', role: 'dialog', 'aria-modal': 'true', tabindex: '-1' });
      frame.appendChild(titleBar); frame.appendChild(content);
      root.appendChild(frame);

      // Backdrop shared (only one regardless of stacking FR-UI-09)
      if (!this._backdrop) {
        this._backdrop = createElement(this.doc, 'div', { class: 'qw-backdrop' });
        this._backdrop.setAttribute('data-qw-backdrop', 'true');
        (this._hostRoot || this.doc.body).appendChild(this._backdrop);
        Instrument.listeners++; // backdrop click listener (does NOT close per FR-UI-07)
        const bdClick = () => { /* backdrop click does not close */ };
        this._backdrop.addEventListener('click', bdClick);
        instance.listeners.push({ target: this._backdrop, event: 'click', handler: bdClick });
      }

      (this._hostRoot || this.doc.body).appendChild(root);
      instance.root = root;
      instance.frame = frame;
      instance.closeBtn = closeBtn;
      instance.state = 'Mounting';

      // Instrument: listeners, observer, timers, subscriptions, cache deps
      const onClose = () => this.close(definitionKey);
      closeBtn.addEventListener('click', onClose); Instrument.listeners++; instance.listeners.push({ target: closeBtn, event: 'click', handler: onClose });
      const onKeyDown = (e) => {
        if (e.key === 'Escape') {
          this.close(definitionKey);
          return;
        }
        if (e.key !== 'Tab') return;
        const focusable = [...frame.querySelectorAll('button,input,a[href]')];
        if (focusable.length === 0) {
          e.preventDefault?.();
          frame.focus();
          return;
        }
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (e.shiftKey && this.doc.activeElement === first) {
          e.preventDefault?.();
          last.focus();
        } else if (!e.shiftKey && this.doc.activeElement === last) {
          e.preventDefault?.();
          first.focus();
        }
      };
      frame.addEventListener('keydown', onKeyDown); Instrument.listeners++; instance.listeners.push({ target: frame, event: 'keydown', handler: onKeyDown });
      const actionButton = root.querySelector('[data-scada-command-config]');
      const binding = bindingFor(definitionKey, invocationKey);
      const onWrite = () => TagCache.write(binding.writeTagId, true, runtimeInstanceId);
      actionButton.addEventListener('click', onWrite); Instrument.listeners++; instance.listeners.push({ target: actionButton, event: 'click', handler: onWrite });
      // The real browser path owns a real observer; the Node harness uses the same lifecycle counter contract.
      Instrument.observers++;
      if (typeof global.MutationObserver === 'function') {
        const observer = new global.MutationObserver(() => {});
        observer.observe(root, { attributes: true, childList: true, subtree: true });
        instance.observer = { disconnect() { observer.disconnect(); Instrument.observers--; } };
      } else {
        instance.observer = { disconnect() { Instrument.observers--; } };
      }
      // cache subscription
      TagCache.subscribe(binding.readTagId, runtimeInstanceId);
      TagCache.subscribe(binding.writeTagId, runtimeInstanceId);
      // timer sentinel
      const t = setTimeout(() => {}, 10000); Instrument.timers++; instance.timers.push(t);

      // Hydrating phase async
      instance.state = 'Hydrating';
      await new Promise(r => setTimeout(r, 20));
      // stale hydration check: if generation not current active for this def, reject
      const current = this._active.get(definitionKey);
      if (!current || current.generation !== generation) {
        // stale - cleanup this instance without becoming Active
        instance.state = 'Disposed';
        this._cleanupInstance(instance);
        if (current && current.generation !== generation) { /* keep current */ }
        else this._active.delete(definitionKey);
        throw new Error('stale-hydration-rejected');
      }
      instance.state = 'Active';
      // initial focus
      try { frame.focus(); } catch (e) {}
      return { instance, reused: false, generation };
    }

    _bringToFront(instance) {
      // move root to end for z-order
      if (instance.root && instance.root.parentNode) {
        instance.root.parentNode.appendChild(instance.root);
      }
      try { instance.frame?.focus(); } catch (e) {}
    }

    async close(definitionKey) {
      const inst = this._active.get(definitionKey);
      if (!inst) return false;
      // If B is child of A, closing A must cascade dispose B first (FR-UI-09 behaviour)
      const idx = this._stack.findIndex(e => e.definitionKey === definitionKey);
      if (idx >= 0) {
        // close children above
        const children = this._stack.slice(idx + 1);
        for (let i = children.length - 1; i >= 0; i--) {
          await this._disposeInstance(this._active.get(children[i].definitionKey));
        }
      }
      await this._disposeInstance(inst);
      return true;
    }

    async _disposeInstance(instance) {
      if (!instance) return;
      if (instance.disposePromise) return instance.disposePromise;
      instance.state = 'Closing';
      instance.disposed = true;
      instance.disposePromise = (async () => {
        // await a tick to expose the Closing race deterministically
        await new Promise(r => setTimeout(r, 5));
        this._cleanupInstance(instance);
        if (this._active.get(instance.definitionKey) === instance)
          this._active.delete(instance.definitionKey);
        this._stack = this._stack.filter(e => e.runtimeInstanceId !== instance.runtimeInstanceId);
        // backdrop only removed when stack empty (shared) - listeners already cleaned in _cleanupInstance
        if (this._stack.length === 0 && this._backdrop) {
          if (this._backdrop.parentNode) this._backdrop.parentNode.removeChild(this._backdrop);
          this._backdrop = null;
        }
        try { instance.focusReturn?.focus?.(); } catch (e) {}
        instance.state = 'Disposed';
      })();
      return instance.disposePromise;
    }

    _cleanupInstance(instance) {
      if (!instance.root) return;
      // remove listeners
      for (const { target, event, handler } of instance.listeners) {
        try { target.removeEventListener(event, handler); Instrument.listeners--; } catch (e) {}
      }
      instance.listeners.length = 0;
      if (instance.observer) { try { instance.observer.disconnect(); } catch (e) {} instance.observer = null; }
      for (const t of instance.timers) { clearTimeout(t); Instrument.timers--; }
      instance.timers.length = 0;
      TagCache.unsubscribeAll(instance.subscriptionsOwner);
      // writes: no new writes after dispose is checked by caller; we do not add
      if (instance.root.parentNode) instance.root.parentNode.removeChild(instance.root);
      instance.root = null;
    }

    disposeAll() {
      for (const inst of [...this._active.values()]) this._disposeInstance(inst);
    }

    // Query helpers for assertions: must be root-scoped (FR-020)
    queryIn(instance, selector) {
      if (!instance?.root) return null;
      return instance.root.querySelector(selector);
    }
    queryAllIn(instance, selector) {
      if (!instance?.root) return [];
      return [...instance.root.querySelectorAll(selector)];
    }
    // Global query must NOT find quick-window internals if isolation holds (except via root)
  }

  // Performance helper
  async function measurePerformance(managerFactory) {
    const hot = [], cold = [];
    // warmup 10
    for (let i = 0; i < 10; i++) {
      const m = managerFactory();
      await m.open(DefinitionKeys.A, InvocationKeys.A_M101);
      await m.close(DefinitionKeys.A);
    }
    // 30 cold
    for (let i = 0; i < 30; i++) {
      // cold: fresh manager, measure request->Active
      const m = managerFactory();
      const t0 = performance.now();
      await m.open(DefinitionKeys.A, InvocationKeys.A_M101);
      const t1 = performance.now();
      cold.push(t1 - t0);
      await m.close(DefinitionKeys.A);
    }
    // 100 hot (same manager warm)
    const hotManager = managerFactory();
    for (let i = 0; i < 100; i++) {
      const t0 = performance.now();
      await hotManager.open(DefinitionKeys.A, InvocationKeys.A_M101);
      const t1 = performance.now();
      hot.push(t1 - t0);
      await hotManager.close(DefinitionKeys.A);
    }
    function p50(arr) { const s = [...arr].sort((a,b)=>a-b); return s[Math.floor(s.length*0.5)]; }
    function p95(arr) { const s = [...arr].sort((a,b)=>a-b); return s[Math.floor(s.length*0.95)]; }
    return { p50Hot: p50(hot), p95Hot: p95(hot), p50Cold: p50(cold), p95Cold: p95(cold), hot, cold };
  }

  const api = {
    PrototypeRevision,
    DefinitionKeys,
    InvocationKeys,
    namespaceFor,
    SentinelAuthorIds,
    TagCache,
    Instrument,
    QuickWindowManager,
    buildVisualContent,
    bindingFor,
    measurePerformance,
    createElement
  };

  // browser global
  if (typeof global !== 'undefined') global.QuickWindowPrototype = api;
  // Node ESM interop: if module exists, export
  if (typeof module !== 'undefined' && module.exports) module.exports = api;

  // ES module export for import
  if (typeof globalThis !== 'undefined') globalThis.QuickWindowPrototype = api;

})(typeof window !== 'undefined' ? window : typeof globalThis !== 'undefined' ? globalThis : this);
