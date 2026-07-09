# TF100Web SCADA Runtime Deterministic Initialization — Implementation Plan

Date: 2026-07-09
Status: Draft implementation plan - pending execution approval
Document version: `V2.1.3.0005`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-09 | `V2.1.3.0005` | `PENDING` | Creation du plan d'implementation derive de la spec d'initialisation deterministe runtime TF100Web. |

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate the race condition between `scada-runtime.js` (deferred) and `visualisation_import.js` (blocking) that causes `ScadaRuntime.initPage()` to be silently skipped on header/body/footer slot injection, leaving command bindings, state engines, and input guards uninitialized.

**Architecture:** TF100Web's `ScadaHost` gains a runtime readiness barrier (`_waitForScadaRuntime`) and a centralized slot init path (`_initRenderedSlot`) that `_renderPart`, `_createPopup`, and `loadPage` all route through. The SCADA Builder V2 runtime JS modules are hardened for idempotent re-entry so TF100Web can safely retry `initPage`. Legacy `EventBindings` without `CommandConfig` are audited and removed from existing project data.

**Tech Stack:** JavaScript (browser, no transpilation) for TF100Web `visualisation_import.js` and SCADA Builder V2 runtime modules. Node.js `node:test` + `node:vm` for runtime JS tests. C# / .NET 8 for exporter audit diagnostic. Python/Django for TF100Web static tests.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-09-tf100web-scada-runtime-init-order-design.md` (D1–D10)
- `_renderPart(role, part)` must never silently skip `initPage` (D2)
- `ScadaHost.init()` must await runtime readiness before first `loadPage` (D3)
- Same init path for header, body, footer, popups (D4)
- Runtime modules must be idempotent for retry safety (D5)
- Do NOT remove `defer` from template as the sole fix (D6)
- Runtime must not know TF100Web slot IDs (D7)
- Legacy `data-scada-events` are decommissioned, not migrated (D8)
- `ScadaHost.init()` fire-and-forget is allowed but errors must be captured and visible (D9)
- Orphaned `EventBindings` must be cleaned in project data, audit diagnostic is a guardrail (D10)
- No changes permitted on `F:\Projet\Git\TF100Web` without authorization
- `.sb2` export at `C:\Users\mathi\Downloads\AMR_REF_SCADA_V2.sb2` is the test artifact

---

## Before You Start

- Confirm SCADA Builder V2 is on branch `debug-event-runtime-issue`.
- Confirm the only expected uncommitted SCADA Builder V2 documentation files are this plan and the matching spec, unless the user has added more work.
- Capture a fresh SCADA Builder V2 test baseline before code or project-data changes. Do not rely on historical pass/fail counts in this plan.
- Do not modify `F:\Projet\Git\TF100Web` until the user explicitly authorizes the TF100Web phase.
- Before any TF100Web work, run `git status --short --branch` in `F:\Projet\Git\TF100Web` and record the branch and dirty state.
- If TF100Web is dirty or not on the expected feature branch, stop before editing and report the state.
- Use PowerShell commands on Windows unless a step explicitly says Git Bash is required.

---

## Phase 1 — SCADA Builder V2: Runtime Idempotency

### Task 1: Make CommandDispatcher.bind idempotent

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/command-dispatcher.js:38-75`

**Interfaces:**
- Consumes: nothing (self-contained module)
- Produces: `window.ScadaRuntime.CommandDispatcher.bind(element)` — now safe for multiple calls on the same element with the same config

- [ ] **Step 1: Add fingerprint guard to `bind`**

In `command-dispatcher.js`, add the guard at the top of `bind`, right after the `if (!configRaw) { return; }` check:

```javascript
function bind(element) {
    if (!element) {
      return;
    }

    var configRaw = element.getAttribute('data-scada-command-config');
    if (!configRaw) {
      return;
    }

    // Idempotency guard — skip if already bound for this exact config.
    // innerHTML replacement destroys old DOM nodes, so dataset is always
    // fresh on first bind. This protects against double-init from retry/replay.
    if (element.dataset && element.dataset.scadaCommandBoundConfig === configRaw) {
      return;
    }
    if (element.dataset) {
      element.dataset.scadaCommandBoundConfig = configRaw;
    }

    var config;
    try {
      config = JSON.parse(configRaw);
    } catch (e) {
      return;
    }

    if (!config || !Array.isArray(config.commands)) {
      return;
    }

    for (var i = 0; i < config.commands.length; i++) {
      var cmd = config.commands[i];
      if (!cmd) {
        continue;
      }

      var trigger = cmd.trigger || 'onClick';
      var domEvent = TRIGGER_MAP[trigger] || 'click';

      (function (capturedCmd, capturedDomEvent) {
        element.addEventListener(capturedDomEvent, function (e) {
          execute(element, capturedCmd);
        });
      })(cmd, domEvent);
    }
  }
```

- [ ] **Step 2: Verify no regression in existing tests**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js"
node --test command-dispatcher.test.mjs
```

Expected: all 3 existing tests pass.

- [ ] **Step 3: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Runtime/command-dispatcher.js
git commit -m "feat: make CommandDispatcher.bind idempotent via dataset fingerprint

Prevents double-binding of click handlers when ScadaRuntime.initPage
is called multiple times on the same slot (retry/replay after runtime
readiness barrier). The fingerprint compares the raw config attribute
string; innerHTML replacement naturally resets the dataset, so this
is safe for the current slot-replacement flow."
```

---

### Task 2: Verify StateEngine.initPage is already idempotent

**Files:**
- Inspect: `src/ScadaBuilderV2.Rendering/Runtime/state-engine.js:279-293`

**Interfaces:**
- Consumes: nothing
- Produces: confirmation that `StateEngine.initPage` is safe for multiple calls

- [ ] **Step 1: Read and confirm the code**

`StateEngine.initPage` only resets `_paused` and `_stateCache` entries for elements in its container. It does not create timers, register event listeners, or accumulate state. Calling it multiple times on the same container is a no-op after the first call (deleting already-deleted keys from `_paused`, setting already-null cache entries to null).

```javascript
// state-engine.js lines 279-293 — no change needed
function initPage(container, pageId) {
    if (!container) { return; }
    var elements = container.querySelectorAll('[data-scada-state-config]');
    for (var i = 0; i < elements.length; i++) {
      var id = elements[i].getAttribute('data-scada-element-id') || elements[i].id;
      if (!id) { continue; }
      delete _paused[id];
      _stateCache[id] = null;
    }
}
```

Verdict: already idempotent. No code change.

- [ ] **Step 2: Add idempotency test**

Append to `tests/runtime-js/state-engine.test.mjs`:

```javascript
test('initPage is idempotent — calling it twice on the same container does not corrupt state', () => {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js', 'effect-applier.js', 'state-engine.js']);

  window.tf100webScadaBuilder = {
    getTagValue(tagId) {
      const mappingId = String(tagId).replace(/^tf100\.mapping\./, '');
      return { '42': 95 }[mappingId] ?? null;
    },
  };

  const stateConfig = {
    defaultEffect: {},
    states: [
      {
        id: 's1', name: 'Alarme', enabled: true,
        expression: {
          ast: {
            type: 'binary', op: 'GreaterThan',
            left: { type: 'tagRef', tagName: 'tf100.mapping.42' },
            right: { type: 'literalNumber', value: 80 },
          },
        },
        effect: { backgroundColor: '#E53935' },
      },
    ],
  };

  const applied = [];
  window.ScadaRuntime.EffectApplier.apply = (element, effect) => applied.push({ id: element.id, effect });

  const element = {
    id: 'el_idem',
    _attrs: { 'data-scada-state-config': JSON.stringify(stateConfig), 'data-scada-element-id': 'el_idem' },
    getAttribute(name) { return Object.prototype.hasOwnProperty.call(this._attrs, name) ? this._attrs[name] : null; },
    querySelector() { return null; },
  };
  const container = { querySelectorAll: (sel) => (sel === '[data-scada-state-config]' ? [element] : []) };

  // Call initPage twice — second call must not throw, corrupt, or change behavior
  window.ScadaRuntime.StateEngine.initPage(container, 'page1');
  window.ScadaRuntime.StateEngine.initPage(container, 'page1');

  window.ScadaRuntime.StateEngine.evaluate(element, { '42': 95 });
  assert.equal(applied.length, 1, 'state engine evaluate still works after double initPage');
  assert.equal(applied[0].effect.backgroundColor, '#E53935');
});
```

- [ ] **Step 3: Run tests**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js"
node --test state-engine.test.mjs
```

Expected: 5 tests pass (4 existing + 1 new).

- [ ] **Step 4: Commit**

```bash
git add tests/runtime-js/state-engine.test.mjs
git commit -m "test: add StateEngine.initPage idempotency regression test

Confirms that calling initPage twice on the same container is safe —
it resets pause/cache state without creating timers or event listeners."
```

---

### Task 3: Verify InputEditGuard.watch is already idempotent

**Files:**
- Inspect: `src/ScadaBuilderV2.Rendering/Runtime/input-edit-guard.js:36-76`

**Interfaces:**
- Consumes: nothing
- Produces: confirmation that `InputEditGuard.watch` is safe for multiple calls

- [ ] **Step 1: Read and confirm the code**

`InputEditGuard.watch` already guards against double-binding at lines 52-55:

```javascript
// Prevent duplicate handler registration
if (element.getAttribute('data-scada-edit-guard') === 'attached') {
    return;
}
element.setAttribute('data-scada-edit-guard', 'attached');
```

Verdict: already idempotent. No code change.

- [ ] **Step 2: Commit (empty — documentation-only if desired, or skip)**

No code change needed. The existing `data-scada-edit-guard` attribute guard already prevents double-binding. Note this in the commit message for Task 1 or skip this commit.

---

## Phase 2 — TF100Web: Host Lifecycle Refactor (Authorization Gate)

> **Authorization required before modifying `F:\Projet\Git\TF100Web`.**

### Task 4: Add `_waitForScadaRuntime`, `_initRenderedSlot`, and `_showRuntimeError` to ScadaHost

**Files:**
- Modify: `static/asset/js/station/visualisation_import.js` — inside `const ScadaHost = { ... };`

**Interfaces:**
- Produces:
  - `ScadaHost._waitForScadaRuntime(timeoutMs)` → `Promise<void>` — resolves when `window.ScadaRuntime.initPage` exists, rejects after timeout
  - `ScadaHost._initRenderedSlot(slot, pageId)` → `Promise<void>` — waits for runtime then calls `initPage`, marks `dataset` markers
  - `ScadaHost._showRuntimeError(message)` — renders visible error on `#scada-host`

- [ ] **Step 1: Add `_waitForScadaRuntime` method**

Insert after `_slotForRole` in the `ScadaHost` object (after line ~62):

```javascript
  /**
   * Waits for window.ScadaRuntime.initPage to become available.
   * Returns immediately if already present; otherwise polls and listens
   * to window.load. Rejects after timeoutMs with a diagnostic message.
   *
   * @param {number} [timeoutMs=5000]
   * @returns {Promise<void>}
   */
  _waitForScadaRuntime(timeoutMs) {
    var limit = typeof timeoutMs === 'number' && timeoutMs > 0 ? timeoutMs : 5000;
    var self = this;

    return new Promise(function (resolve, reject) {
      if (window.ScadaRuntime && typeof window.ScadaRuntime.initPage === 'function') {
        resolve();
        return;
      }

      var start = Date.now();
      var timer;

      function check() {
        if (window.ScadaRuntime && typeof window.ScadaRuntime.initPage === 'function') {
          if (timer) clearTimeout(timer);
          resolve();
          return;
        }
        if (Date.now() - start >= limit) {
          if (timer) clearTimeout(timer);
          window.removeEventListener('load', check);
          var msg = 'Runtime SCADA Builder indisponible: impossible d\'initialiser la page.';
          console.error('scada: ' + msg);
          reject(new Error(msg));
          return;
        }
        timer = setTimeout(check, 50);
      }

      window.addEventListener('load', function () {
        // Recheck immediately on window.load
        check();
      }, { once: true });

      check();
    });
  },
```

- [ ] **Step 2: Add `_initRenderedSlot` method**

Insert after `_waitForScadaRuntime`:

```javascript
  /**
   * Waits for the SCADA runtime and then initializes a rendered slot.
   * Marks the slot with dataset markers for observability.
   *
   * @param {Element} slot    - The DOM container (e.g. #scada-host-footer).
   * @param {string}  pageId  - Unique page identifier.
   * @returns {Promise<void>}
   */
  async _initRenderedSlot(slot, pageId) {
    if (!slot || !pageId) {
      return;
    }

    try {
      await this._waitForScadaRuntime();
      window.ScadaRuntime.initPage(slot, pageId);
      slot.dataset.scadaRuntimeInitialized = '1';
      slot.dataset.scadaRuntimePageId = pageId;
    } catch (error) {
      slot.dataset.scadaRuntimeInitialized = 'error';
      console.error('scada: _initRenderedSlot failed for', pageId, error);
      this._showRuntimeError(error.message || 'Erreur d\'initialisation du runtime SCADA.');
    }
  },
```

- [ ] **Step 3: Add `_showRuntimeError` method**

Insert after `_initRenderedSlot`:

```javascript
  /**
   * Shows a visible runtime error overlay on #scada-host without crashing
   * the station UI. Subsequent calls replace the previous message.
   *
   * @param {string} message - Diagnostic message to display.
   */
  _showRuntimeError(message) {
    var host = document.getElementById('scada-host');
    if (!host) return;

    var existing = host.querySelector('.scada-runtime-error');
    if (existing) {
      existing.textContent = message;
      return;
    }

    var banner = document.createElement('div');
    banner.className = 'scada-runtime-error';
    banner.textContent = message;
    banner.style.cssText =
      'position:absolute;top:8px;left:50%;transform:translateX(-50%);z-index:9999;' +
      'background:#E53935;color:#fff;padding:8px 20px;border-radius:6px;' +
      'font-family:sans-serif;font-size:13px;font-weight:600;' +
      'box-shadow:0 2px 12px rgba(0,0,0,0.24);pointer-events:none;';
    host.style.position = host.style.position || 'relative';
    host.appendChild(banner);
  },
```

- [ ] **Step 4: Verify syntax — no unintended breakage**

Load the page in a browser, check console for no syntax errors. The new methods are inert until called.

- [ ] **Step 5: Commit**

```bash
git add static/asset/js/station/visualisation_import.js
git commit -m "feat: add ScadaHost runtime readiness barrier and slot init methods

- _waitForScadaRuntime: polls for window.ScadaRuntime.initPage, resolves
  immediately if present, rejects after 5s with diagnostic
- _initRenderedSlot: awaits runtime then calls initPage, marks dataset
  markers for observability (scadaRuntimeInitialized, scadaRuntimePageId)
- _showRuntimeError: visible red banner on #scada-host for runtime errors"
```

---

### Task 5: Make ScadaHost.init async with runtime wait, update call site

**Files:**
- Modify: `static/asset/js/station/visualisation_import.js` — `ScadaHost.init` and top-level call site

**Interfaces:**
- Consumes: `ScadaHost._waitForScadaRuntime` (Task 4)
- Changes: `init()` becomes `async`, call site catches errors

- [ ] **Step 1: Make `init` async**

Replace the existing `init` method (currently sync, ~lines 57-61):

Before:
```javascript
  init(scadaHostEl) {
    const homePageId = scadaHostEl.dataset.scadaHomePage || 'win00009';
    ScadaTagCache.startPolling();
    this.loadPage(homePageId);
  },
```

After:
```javascript
  async init(scadaHostEl) {
    const homePageId = scadaHostEl.dataset.scadaHomePage || 'win00009';
    ScadaTagCache.startPolling();
    try {
      await this._waitForScadaRuntime();
    } catch (error) {
      this._showRuntimeError(error.message || 'Runtime SCADA Builder indisponible.');
      return;
    }
    await this.loadPage(homePageId);
  },
```

- [ ] **Step 2: Update the fire-and-forget call site with error handling**

Find the call site at the bottom of `visualisation_import.js` (currently ~line 995):

Before:
```javascript
  if (isNewScadaHost) {
    ScadaHost.init(scadaHostEl);
  }
```

After:
```javascript
  if (isNewScadaHost) {
    ScadaHost.init(scadaHostEl).catch(function (error) {
      console.error('scada: host initialization failed', error);
      ScadaHost._showRuntimeError('Runtime SCADA Builder indisponible.');
    });
  }
```

- [ ] **Step 3: Verify syntax**

Load the page, check console. The runtime barrier should resolve immediately in normal conditions (runtime loads before fetch completes). No functional change for the happy path.

- [ ] **Step 4: Commit**

```bash
git add static/asset/js/station/visualisation_import.js
git commit -m "fix: make ScadaHost.init async, await runtime before first loadPage

ScadaHost.init now calls _waitForScadaRuntime before loadPage, ensuring
the home page's header/body/footer slots are initialized with the runtime
ready. The fire-and-forget call site catches rejections and shows a
visible diagnostic via _showRuntimeError."
```

---

### Task 6: Make _renderPart async and route through _initRenderedSlot

**Files:**
- Modify: `static/asset/js/station/visualisation_import.js` — `_renderPart` method

**Interfaces:**
- Consumes: `ScadaHost._initRenderedSlot` (Task 4)
- Changes: `_renderPart` becomes `async`, always calls `_initRenderedSlot`

- [ ] **Step 1: Replace `_renderPart` with async version**

Before (~lines 92-99):
```javascript
  _renderPart(role, part) {
    const slot = this._slotForRole(role);
    if (!slot) return;
    slot.innerHTML = part.html;
    this._injectCssIfNeeded(part.css_hash, part.page_id);
    if (window.ScadaRuntime && window.ScadaRuntime.initPage) {
      window.ScadaRuntime.initPage(slot, part.page_id);
    }
  },
```

After:
```javascript
  async _renderPart(role, part) {
    const slot = this._slotForRole(role);
    if (!slot || !part) return;

    slot.innerHTML = part.html;
    this._injectCssIfNeeded(part.css_hash, part.page_id);
    await this._initRenderedSlot(slot, part.page_id);
  },
```

- [ ] **Step 2: Update `loadPage` to await all part renders**

Find the `forEach` loop in `loadPage` (~lines 109-120) and change to `for...of` with `await`:

Before:
```javascript
      ['header', 'body', 'footer'].forEach((role) => {
        const part = parts.find((candidate) => candidate.role === role);
        if (!part) {
          if (this._currentPageIdForRole(role) !== null) {
            this._clearSlot(role);
            this._setCurrentPageIdForRole(role, null);
          }
          return;
        }
        if (role === 'body' || part.page_id !== this._currentPageIdForRole(role)) {
          this._renderPart(role, part);
          this._setCurrentPageIdForRole(role, part.page_id);
        }
      });
```

After:
```javascript
      for (var i = 0; i < ['header', 'body', 'footer'].length; i++) {
        var role = ['header', 'body', 'footer'][i];
        var part = parts.find(function (candidate) { return candidate.role === role; });
        if (!part) {
          if (this._currentPageIdForRole(role) !== null) {
            this._clearSlot(role);
            this._setCurrentPageIdForRole(role, null);
          }
          continue;
        }
        if (role === 'body' || part.page_id !== this._currentPageIdForRole(role)) {
          await this._renderPart(role, part);
          this._setCurrentPageIdForRole(role, part.page_id);
        }
      }
```

This preserves the sequential header → body → footer order (important for composed page height calculation) while awaiting each slot's runtime initialization.

- [ ] **Step 3: Verify syntax**

Load the page, open a page with header/footer. Check that `data-scada-runtime-initialized="1"` appears on all three slots.

- [ ] **Step 4: Commit**

```bash
git add static/asset/js/station/visualisation_import.js
git commit -m "fix: make _renderPart async, always route through _initRenderedSlot

_renderPart now unconditionally awaits the runtime barrier before
considering a slot rendered. loadPage awaits each part sequentially
(header -> body -> footer). The silent skip when ScadaRuntime.initPage
is absent is eliminated — the barrier polls until the runtime loads
or times out with a visible diagnostic."
```

---

### Task 7: Route popup rendering through _initRenderedSlot

**Files:**
- Modify: `static/asset/js/station/visualisation_import.js` — `_createPopup` method

**Interfaces:**
- Consumes: `ScadaHost._initRenderedSlot` (Task 4)
- Changes: popup content init uses the same centralized path

- [ ] **Step 1: Update `_createPopup`**

Find `_createPopup` (~lines 264-280). After the line where `content.innerHTML = bodyPart.html`, add the `_initRenderedSlot` call:

Before (conceptual — find the exact code in the file):
```javascript
    content.innerHTML = part.html;
```

After:
```javascript
    content.innerHTML = part.html;
    this._initRenderedSlot(content, part.page_id);
```

If `_createPopup` is synchronous, make it `async` and `await` the call. If the popup injects multiple parts, init only the `body` part's slot.

- [ ] **Step 2: Verify syntax**

Check for `async`/`await` consistency — any caller of `_createPopup` must handle the promise (fire-and-forget is acceptable for popups; the `.catch` inside `_initRenderedSlot` already handles errors).

- [ ] **Step 3: Commit**

```bash
git add static/asset/js/station/visualisation_import.js
git commit -m "fix: route popup content through _initRenderedSlot

Popups now use the same runtime initialization path as header/body/footer
slots, ensuring popup command bindings and state engines are active."
```

---

### Task 8: Validate the fix end-to-end

**Files:**
- None modified (verification only)

**Prerequisites:**
- `.sb2` deployed to TF100Web STATIC_ROOT via `manage.py deploy_scada_builder`
- Browser with DevTools open

- [ ] **Step 1: Verify header/footer initialization markers**

```javascript
document.querySelector('#scada-host-header').dataset.scadaRuntimeInitialized
// Expected: "1"

document.querySelector('#scada-host-footer').dataset.scadaRuntimeInitialized
// Expected: "1"

document.querySelector('#scada-host-body').dataset.scadaRuntimeInitialized
// Expected: "1"
```

- [ ] **Step 2: Verify footer command elements are present**

```javascript
document.querySelectorAll('#scada-host-footer [data-scada-command-config]').length
// Expected: 2
```

- [ ] **Step 3: Click "Page d'accueil" (Button1 in group_001)**

Expected: navigates to win00004 (Compteurs page)

- [ ] **Step 4: Click "Compresseur" (Button2 in group_002)**

Expected: navigates to win00059 (Compresseur page)

- [ ] **Step 5: Verify no manual initPage call was needed**

The buttons work without any console intervention. If any step fails, check:
1. Console for `_initRenderedSlot` error logs
2. `window.ScadaRuntime.initPage` exists at page load time
3. `data-scada-runtime-initialized` markers on all three slots

---

## Phase 3 — SCADA Builder V2: Test Fixes and Legacy Decommission

### Task 9: Fix runtime JS test fixtures — PascalCase to camelCase

**Files:**
- Modify: `tests/runtime-js/command-dispatcher.test.mjs`

**Interfaces:**
- Consumes: `harness.mjs` `loadRuntime` helper
- Produces: tests that use the same camelCase payloads the production exporter emits

- [ ] **Step 1: Fix PascalCase `kind` values in test fixtures**

Replace PascalCase enum values with camelCase to match what `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` emits:

```javascript
import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

test('OpenPopup posts a flat {pageId, options} message matching the TF100Web host listener', () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const messages = [];
  window.postMessage = (msg) => messages.push(msg);

  const element = { querySelector: () => null };
  // Changed: kind: 'OpenPopup' -> 'openPopup'
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

  // Changed: PascalCase -> camelCase
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
  // Changed: kind: 'WriteTag' -> 'writeTag', writeMode: 'SetFromInput' -> 'setFromInput'
  const cmd = { kind: 'writeTag', writeMode: 'setFromInput', writeTagId: 'tf100.mapping.10' };

  assert.doesNotThrow(() => window.ScadaRuntime.CommandDispatcher.execute(element, cmd));
  assert.equal(writes.length, 1);
  assert.equal(writes[0].tagId, 'tf100.mapping.10');
  assert.equal(writes[0].value, '72.5');
});
```

- [ ] **Step 2: Run tests to verify they pass with camelCase**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js"
node --test command-dispatcher.test.mjs
```

Expected: 3 tests pass.

- [ ] **Step 3: Commit**

```bash
git add tests/runtime-js/command-dispatcher.test.mjs
git commit -m "test: fix command-dispatcher test fixtures to use camelCase

The C# exporter uses JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
so enum values are serialized as 'openPopup' not 'OpenPopup', 'writeTag'
not 'WriteTag', 'setFromInput' not 'SetFromInput', etc. The previous
PascalCase fixtures tested against a contract that didn't match what the
runtime actually receives from the exporter."
```

---

### Task 10: Add navigate command test to runtime JS

**Files:**
- Modify: `tests/runtime-js/command-dispatcher.test.mjs`

**Interfaces:**
- Consumes: `harness.mjs` `loadRuntime` helper
- Produces: test verifying navigate command posts correct message

- [ ] **Step 1: Add navigate test**

Append to `command-dispatcher.test.mjs`:

```javascript
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
```

- [ ] **Step 2: Run the new test**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js"
node --test --test-name-pattern="Navigate posts" command-dispatcher.test.mjs
```

Expected: 1 test passes.

- [ ] **Step 3: Run all command-dispatcher tests**

```powershell
node --test command-dispatcher.test.mjs
```

Expected: 4 tests pass (3 existing + 1 new navigate).

- [ ] **Step 4: Commit**

```bash
git add tests/runtime-js/command-dispatcher.test.mjs
git commit -m "test: add navigate command dispatch test

Verifies that Execute({kind:'navigate', targetPageId:'win00059'}) posts
{source:'scada-builder-v2', action:'navigate', pageId:'win00059'}
matching the TF100Web host listener contract."
```

---

### Task 11: Add CommandDispatcher.bind idempotency test

**Files:**
- Modify: `tests/runtime-js/command-dispatcher.test.mjs`

**Interfaces:**
- Consumes: `harness.mjs` `loadRuntime` helper, idempotency fingerprint from Task 1
- Produces: test verifying double-bind does not double-dispatch

- [ ] **Step 1: Add idempotency test**

Append to `command-dispatcher.test.mjs`:

```javascript
test('bind is idempotent — double bind on the same element fires command only once', () => {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const messages = [];
  window.postMessage = (msg) => messages.push(msg);

  // Build a fake element with a data-scada-command-config attribute
  var config = { commands: [{ kind: 'navigate', trigger: 'onClick', targetPageId: 'win00059' }] };
  var configJson = JSON.stringify(config);
  var clickHandler = null;
  var element = {
    _attrs: { 'data-scada-command-config': configJson },
    _dataset: {},
    get dataset() { return this._dataset; },
    set dataset(v) { this._dataset = v; },
    getAttribute: function (name) {
      return Object.prototype.hasOwnProperty.call(this._attrs, name) ? this._attrs[name] : null;
    },
    addEventListener: function (event, handler) {
      clickHandler = handler;
    },
    click: function () {
      if (clickHandler) clickHandler({});
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
```

- [ ] **Step 2: Run the test — verify it FAILS before Task 1 idempotency**

If Task 1's idempotency guard is not yet in place, this test will fail with `messages.length === 2`. That's expected — run it to confirm, then ensure Task 1 has been committed.

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js"
node --test command-dispatcher.test.mjs
```

Expected with Task 1: 5 tests pass (4 existing + 1 idempotency).

- [ ] **Step 3: Commit**

```bash
git add tests/runtime-js/command-dispatcher.test.mjs
git commit -m "test: add CommandDispatcher.bind idempotency regression test

Verifies that calling bind() twice on the same element with the same
data-scada-command-config fires the command exactly once on click."
```

---

### Task 12: Decommission legacy EventBindings from AMR_REF_SCADA_V2 project data

**Files:**
- Modify: `projects/AMR_REF_SCADA_V2/scenes/win00003.scene.json`
- Modify: any other scene files with orphaned `EventBindings`

**Note:** This task modifies project data, not source code. The scope may expand if audit reveals EventBindings orphans in additional scenes.

- [ ] **Step 1: Audit AMR_REF_SCADA_V2 for EventBindings without CommandConfig**

Use a PowerShell script to scan all `scene.json` files for elements where `Events`
is non-empty but `CommandConfig` is null or has empty `Commands`:

```powershell
$sceneRoot = "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\projects\AMR_REF_SCADA_V2\scenes"
Get-ChildItem -LiteralPath $sceneRoot -Filter "*.scene.json" | ForEach-Object {
  $sceneFile = $_
  $json = Get-Content -LiteralPath $sceneFile.FullName -Raw | ConvertFrom-Json
  $objects = @()
  if ($json.Elements) { $objects += $json.Elements }
  if ($json.Objects) { $objects += $json.Objects }

  foreach ($element in $objects) {
    $events = @($element.Events)
    $commands = @($element.CommandConfig.Commands)
    if ($events.Count -gt 0 -and $commands.Count -eq 0) {
      [pscustomobject]@{
        Scene = $sceneFile.Name
        ElementId = $element.Id
        DisplayName = $element.DisplayName
        EventCount = $events.Count
      }
    }
  }
} | Format-Table -AutoSize
```

For win00003, the known orphans are:
- `group_003` — Events with ActionId `action_changepage_click_group_003_win00017` (Navigate → win00017), CommandConfig: null
- `group_004` — Events with ActionId `action_changepage_click_group_004_win00007` (Navigate → win00007), CommandConfig: null
- `group_008` — Events with ActionId `action_changepage_click_group_008_win00008` (Navigate → win00008), CommandConfig: null
- Plus standalone buttons with Navigate Events but no CommandConfig

- [ ] **Step 2: For each scene with orphans, backup then clean**

For **win00003.scene.json** (the confirmed case):

```powershell
$sceneRoot = "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\projects\AMR_REF_SCADA_V2\scenes"
Copy-Item -LiteralPath "$sceneRoot\win00003.scene.json" -Destination "$sceneRoot\win00003.scene.json.bak"
```

Edit `win00003.scene.json`:

a) For `group_003`: set `"Events": null`
b) For `group_004`: set `"Events": null`
c) For `group_008`: set `"Events": null`
d) For any standalone buttons with orphaned navigate Events but no CommandConfig: set `"Events": null`
e) Remove orphaned `Actions` entries from the top-level `"Actions"` array that no longer have any EventBinding referencing them. The actions to remove are those whose IDs only appear in the cleaned Events.

Actions to remove from `win00003.scene.json` (no EventBinding references remain after above cleanup):
- `action_changepage_click_group_003_win00017`
- `action_changepage_click_group_004_win00007`
- `action_changepage_click_group_008_win00008`

Keep `action_changepage_click_group_001_win00004` and `action_changepage_click_group_002_win00059` — they have corresponding CommandConfig entries.

- [ ] **Step 3: Verify `group_001` and `group_002` are untouched**

```powershell
$scene = Get-Content -LiteralPath "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\projects\AMR_REF_SCADA_V2\scenes\win00003.scene.json" -Raw | ConvertFrom-Json
$scene.Elements |
  Where-Object { $_.Id -in @("group_001", "group_002") } |
  Select-Object Id, @{Name="CommandCount"; Expression={ @($_.CommandConfig.Commands).Count }}
```

Expected: both still have non-null `CommandConfig` with navigate commands.

- [ ] **Step 4: Run the full test suite to verify no breakage**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet test ScadaBuilderV2.sln --no-restore
```

Expected: same result as the fresh baseline captured in `Before You Start`; any new failure must be investigated before commit.

- [ ] **Step 5: Commit**

```bash
git add projects/AMR_REF_SCADA_V2/scenes/win00003.scene.json
git commit -m "chore: decommission legacy EventBindings orphans in win00003

Removed EventBindings without CommandConfig equivalent from group_003,
group_004, group_008, and standalone buttons. These artifacts were
left over from the legacy event model and are not read by the new
#scada-host runtime (which binds data-scada-command-config, not
data-scada-events). Orphaned Actions entries removed.

group_001 (Page d'accueil) and group_002 (Compresseur) are preserved —
they have valid CommandConfig with navigate commands.

See spec D8, D10."
```

---

### Task 13: Add export audit diagnostic for orphaned EventBindings

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`
- Potentially create: `src/ScadaBuilderV2.Domain/Scenes/ScadaSceneEventAudit.cs`

**Interfaces:**
- Consumes: `ScadaScene` objects during export
- Produces: warning diagnostics when a scene contains EventBindings not backed by CommandConfig

- [ ] **Step 1: Add audit method**

In `Ft100SceneExporter.cs`, add a method that scans a scene for orphaned legacy events and returns diagnostics:

```csharp
/// <summary>
/// Scans a scene for Element+ objects that have legacy EventBindings but no
/// CommandConfig equivalent. These are artifacts from the pre-CommandConfig
/// event model and will not function in the new runtime.
/// </summary>
public static IReadOnlyList<ScadaBuildValidationIssue> AuditOrphanedEventBindings(ScadaScene scene)
{
    var issues = new List<ScadaBuildValidationIssue>();

    foreach (var element in scene.EnumerateElementsRecursive())
    {
        if (element.Events is not { Count: > 0 })
            continue;

        var hasCommandConfig = element is { CommandConfig.Commands.Count: > 0 };
        if (hasCommandConfig)
            continue;

        var navigateEvents = element.Events
            .Where(e => e.ActionId?.Contains("changepage", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (navigateEvents.Count > 0)
        {
            issues.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Warning,
                $"Scene '{scene.Id}': element '{element.Id}' ('{element.DisplayName}') has legacy " +
                $"navigate EventBinding(s) without a CommandConfig equivalent. " +
                $"These events will not function in the TF100Web #scada-host runtime. " +
                $"Remove the EventBindings or add a CommandConfig with a Navigate command.",
                scene.Id));
        }
    }

    return issues;
}
```

- [ ] **Step 2: Call audit during export**

In the `ExportAsync` method, after validation, add the audit check:

```csharp
// After existing validation block (line ~63-72 in ExportAsync)
var orphanWarnings = AuditOrphanedEventBindings(scene);
foreach (var warning in orphanWarnings)
{
    // Log warning — does not block export
    Debug.WriteLine($"[SCADA Export Audit] {warning.Message}");
}
```

If there's a project-level export method that iterates scenes, collect warnings across all scenes and surface them.

- [ ] **Step 3: Add test for audit diagnostic**

In `tests/ScadaBuilderV2.Tests/`, add a test that creates a scene with an element that has Events but no CommandConfig and verifies `AuditOrphanedEventBindings` returns a warning:

```csharp
[TestMethod]
public void AuditOrphanedEventBindings_WarnsForNavigateEventWithoutCommandConfig()
{
    var scene = ScadaScene.Create("test_scene");
    var element = new ScadaElement(
        Id: "orphan_btn",
        DisplayName: "Orphan Button",
        Kind: ScadaElementKind.Group,
        ...,
        CommandConfig: null,
        Events:
        [
            new ScadaElementEventBinding(
                Trigger: "click",
                ActionId: "action_changepage_click_orphan_btn_win00099",
                StopPropagation: true,
                PreventDefault: false)
        ]
    );
    scene = scene.WithElement(element);

    var warnings = Ft100SceneExporter.AuditOrphanedEventBindings(scene);

    Assert.AreEqual(1, warnings.Count);
    Assert.AreEqual(ScadaBuildValidationSeverity.Warning, warnings[0].Severity);
    StringAssert.Contains(warnings[0].Message, "orphan_btn");
}

[TestMethod]
public void AuditOrphanedEventBindings_SilentWhenCommandConfigPresent()
{
    var scene = ScadaScene.Create("test_scene");
    var element = new ScadaElement(
        Id: "healthy_btn",
        DisplayName: "Healthy Button",
        Kind: ScadaElementKind.Group,
        ...,
        CommandConfig: new ScadaElementCommandConfig(Commands:
        [
            new ScadaCommandBinding(
                Id: "nav1",
                Name: "Go",
                Enabled: true,
                Trigger: ScadaCommandTrigger.OnClick,
                Kind: ScadaCommandKind.Navigate,
                TargetPageId: "win00004")
        ]),
        Events:
        [
            new ScadaElementEventBinding(
                Trigger: "click",
                ActionId: "action_changepage_click_healthy_btn_win00004",
                StopPropagation: true,
                PreventDefault: false)
        ]
    );
    scene = scene.WithElement(element);

    var warnings = Ft100SceneExporter.AuditOrphanedEventBindings(scene);

    Assert.AreEqual(0, warnings.Count, "Element with both Events and CommandConfig should not warn");
}
```

- [ ] **Step 4: Run tests**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~AuditOrphanedEventBindings"
```

Expected: 2 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs tests/ScadaBuilderV2.Tests/
git commit -m "feat: add export audit diagnostic for orphaned EventBindings

AuditOrphanedEventBindings scans scenes during export and emits warnings
for elements that have legacy navigate EventBindings without a corresponding
CommandConfig. These will be non-functional in the #scada-host runtime.
The diagnostic is a guardrail — project data cleanup is the primary fix.

Spec: D8, D10."
```

---

### Task 14: Add regression test — new Navigate command does not produce EventBindings

**Files:**
- Create or Modify: `tests/ScadaBuilderV2.Tests/` — new or existing test file for event model

**Interfaces:**
- Consumes: domain model `ScadaElement`, `ScadaCommandBinding`, `ScadaScene`
- Produces: test confirming current authoring path is clean

- [ ] **Step 1: Add regression test**

Add to `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaSceneElementEventsTests.cs` (create if it doesn't exist, or add to existing event test file):

```csharp
[TestMethod]
public void ElementWithNavigateCommandConfig_DoesNotProduceLegacyEventBindings()
{
    // An element authored through the current CommandConfig path must not
    // generate legacy EventBindings as a side effect. This regression lock
    // ensures we never reintroduce the old data-scada-events flow for
    // new navigate commands.

    var element = ScadaElement.CreateGroup(
        id: "nav_group",
        displayName: "Navigation Group",
        bounds: new ScadaElementBounds(0, 0, 138, 44));

    var commandConfig = new ScadaElementCommandConfig(
        Commands:
        [
            new ScadaCommandBinding(
                Id: "nav_cmd_1",
                Name: "GoToPage",
                Enabled: true,
                Trigger: ScadaCommandTrigger.OnClick,
                Kind: ScadaCommandKind.Navigate,
                TargetPageId: "win00099")
        ]);

    element = element with { CommandConfig = commandConfig };

    // The element's Events should remain null or empty — CommandConfig is
    // the canonical model for the new runtime. EventBindings are legacy-only.
    Assert.IsTrue(
        element.Events is null or { Count: 0 },
        "New Navigate CommandConfig must not produce legacy EventBindings");
}
```

- [ ] **Step 2: Run the test**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~DoesNotProduceLegacyEventBindings"
```

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/ScadaBuilderV2.Tests/
git commit -m "test: regression lock — CommandConfig navigate does not produce EventBindings

Ensures that the current authoring path (CommandConfig-only) never
reintroduces legacy EventBindings as a side effect. The new runtime
reads data-scada-command-config exclusively.

Spec: D8, Section 7.3 item 3."
```

---

## Phase 4 — TF100Web Static Tests

### Task 15: Add static regression tests for runtime init contract

**Files:**
- Modify: `frontend/tests_scada_package.py`

**Interfaces:**
- Consumes: `visualisation_import.js` source as string
- Produces: static assertions about the presence of required functions and patterns

- [ ] **Step 1: Add test class for runtime init contract**

```python
import re
from pathlib import Path

class ScadaRuntimeInitContractTests(unittest.TestCase):
    """Static checks that visualisation_import.js contains the runtime init contract."""

    @classmethod
    def setUpClass(cls):
        js_path = Path(__file__).resolve().parent.parent / "static" / "asset" / "js" / "station" / "visualisation_import.js"
        cls.js_source = js_path.read_text(encoding="utf-8")

    def test_contains_wait_for_scada_runtime(self):
        self.assertIn("_waitForScadaRuntime", self.js_source,
                      "visualisation_import.js must contain _waitForScadaRuntime method")

    def test_contains_init_rendered_slot(self):
        self.assertIn("_initRenderedSlot", self.js_source,
                      "visualisation_import.js must contain _initRenderedSlot method")

    def test_render_part_uses_init_rendered_slot(self):
        # _renderPart must call _initRenderedSlot, not the old guard pattern
        old_guard = "if (window.ScadaRuntime && window.ScadaRuntime.initPage) {\n      window.ScadaRuntime.initPage"
        self.assertNotIn(old_guard, self.js_source,
                         "_renderPart must not contain the old silent-skip guard pattern")
        # Must contain the new path
        self.assertIn("_initRenderedSlot", self.js_source,
                      "_renderPart must reference _initRenderedSlot")

    def test_load_page_awaits_parts(self):
        # loadPage must await _renderPart for header/body/footer
        self.assertIn("await this._renderPart", self.js_source,
                      "loadPage must await _renderPart for each slot")

    def test_create_popup_uses_init_rendered_slot(self):
        self.assertIn("await this._initRenderedSlot", self.js_source,
                      "_createPopup must await _initRenderedSlot for popup content")

    def test_init_call_site_catches_errors(self):
        # The fire-and-forget call site must have .catch() or equivalent error handling
        self.assertIn(".catch", self.js_source,
                      "ScadaHost.init call site must catch async errors")
```

- [ ] **Step 2: Run the static tests**

```powershell
Set-Location "F:\Projet\Git\TF100Web"
python -m pytest frontend/tests_scada_package.py::ScadaRuntimeInitContractTests -v
```

Expected: 6 tests pass (all contract elements are present after Tasks 4–7 implementation).

- [ ] **Step 3: Commit**

```bash
git add frontend/tests_scada_package.py
git commit -m "test: add static contract tests for SCADA runtime init lifecycle

Verifies visualisation_import.js contains:
- _waitForScadaRuntime barrier
- _initRenderedSlot centralized slot init
- _renderPart routes through _initRenderedSlot (old guard removed)
- loadPage awaits part renders
- _createPopup uses _initRenderedSlot
- init() call site catches async errors

Spec: Section 7.1."
```

---

## Validation Checklist

- [ ] Runtime JS tests pass with camelCase payloads (`node --test command-dispatcher.test.mjs`)
- [ ] Runtime JS navigate test verifies `{source, action:'navigate', pageId}` message
- [ ] Runtime JS idempotency test verifies single dispatch on double-bind
- [ ] StateEngine idempotency confirmed (no code change needed)
- [ ] InputEditGuard idempotency confirmed (`data-scada-edit-guard` guard already exists)
- [ ] TF100Web static contract tests pass (6 tests)
- [ ] `Page d'accueil` (group_001) navigates to win00004 without manual `initPage` call
- [ ] `Compresseur` (group_002) navigates to win00059 without manual `initPage` call
- [ ] `#scada-host-header`, `#scada-host-body`, `#scada-host-footer` all marked `data-scada-runtime-initialized="1"`
- [ ] Popup with runtime content initialized via `_initRenderedSlot`
- [ ] Orphaned EventBindings cleaned from AMR_REF_SCADA_V2 project data
- [ ] Export audit warns on orphaned EventBindings without CommandConfig
- [ ] New Navigate CommandConfig does not produce legacy EventBindings (regression)
- [ ] Full SCADA Builder V2 test suite: same result as the fresh baseline captured before implementation
