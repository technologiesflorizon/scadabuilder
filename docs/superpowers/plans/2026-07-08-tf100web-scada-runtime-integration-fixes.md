# TF100Web SCADA Runtime Integration — Correction Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the 6 contract/integration bugs found in the SCADA Builder V2 ↔ TF100Web import-and-runtime audit (2026-07-08) so that, when `TF100_INDUSTRIAL_DEPLOYMENT=True` and a station's `station_type == SCADA_BUILDER_2`, `/visualisation/` displays live tag-driven state correctly and all in-SCADA navigation (page switch, popups) stays AJAX-based with no full-page reload.

**Architecture:** No architecture change. All fixes are localized corrections to existing contracts:
- SCADA Builder V2's shared runtime JS (`src/ScadaBuilderV2.Rendering/Runtime/*.js`) must resolve tag ids exclusively through `window.ScadaRuntime.TagBridge`, which is the only place that knows how to talk to the host (`window.tf100webScadaBuilder`).
- TF100Web's `deploy_scada_builder` command and `frontend/views.py` must read the deployed package's own `manifest.json` for facts that change per export (home page), instead of a static Django setting.
- TF100Web's popup rendering must share the main document's `window` (same-document overlay) instead of a sandboxed `iframe`, since the runtime bridge (`window.tf100webScadaBuilder`), tag polling (`ScadaTagCache`), and `window.ScadaRuntime` all live on that one `window` and are not designed to be re-established inside a child frame.

**Tech Stack:** C# / .NET 8 (SCADA Builder V2 exporter + embedded JS resources), vanilla JS (runtime + TF100Web host glue, no framework), Python 3.13 / Django (TF100Web), Node.js built-in test runner (`node:test`, `node:vm` — zero new dependencies) for the JS runtime modules, MSTest (.NET) and Django `TestCase` for the two backends.

## Global Constraints

- Two separate git repositories are touched. Every file path below is prefixed with its repo root:
  - `BUILDER` = `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`
  - `TF100WEB` = `F:\Projet\Git\TF100Web`
- KISS / stability over feature growth (explicit user directive from this thread): no multi-version caching, no new external JS dependencies, no framework introduced for the popup fix — plain DOM APIs only, matching the existing style of `visualisation_import.js`.
- Do not touch anything under `docs/09_archive/` or delete the `# DEPRECATED` legacy SCADA code in TF100Web (`scada_package.py`, `scada_projects.py`, `_load_scada_scene`'s definition, etc.) — only the redundant *call site* to `_load_scada_scene()` inside the now-dead branch is removed, per Task 9's proof that it is unreachable. The function itself and the rest of the legacy path stay until the design's "Étape 3" cleanup.
- Every runtime JS change must keep passing the existing string-presence checks in `tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs` (no public `window.ScadaRuntime.*` symbol renamed or removed).
- Commit after each task. Two separate repos means two separate commit histories — do not try to combine commits across `BUILDER` and `TF100WEB`.

---

## File Structure

**BUILDER (SCADA Builder V2):**
- `tests/runtime-js/package.json` — new. Marks the folder as an ES-module Node project (`"type": "module"`), zero dependencies.
- `tests/runtime-js/harness.mjs` — new. Loads real runtime module files from `src/ScadaBuilderV2.Rendering/Runtime/` into a `node:vm` sandbox with a minimal `window` stub, so tests execute the actual shipped JS instead of reimplementing it.
- `tests/runtime-js/expression-evaluator.test.mjs` — new. Regression test for Bug 1 (tag-key mismatch) at the AST-walk level.
- `tests/runtime-js/state-engine.test.mjs` — new. Regression test for Bug 1 at the `StateEngine.evaluate()` level (the actual code path TF100Web drives).
- `tests/runtime-js/command-dispatcher.test.mjs` — new. Regression tests for Bug 2 (popup postMessage payload shape) and Bug 3 (`SetFromInput` crash).
- `src/ScadaBuilderV2.Rendering/Runtime/expression-evaluator.js` — modify: `tagRef` resolution routes through `TagBridge.getTagValue`.
- `src/ScadaBuilderV2.Rendering/Runtime/state-engine.js` — modify: the per-state null/quality check routes through `TagBridge.getTagValue`.
- `src/ScadaBuilderV2.Rendering/Runtime/command-dispatcher.js` — modify: flatten the popup postMessage payload; pass `element` into `_writeTagCommand` for `SetFromInput`.

**TF100WEB:**
- `core/management/commands/deploy_scada_builder.py` — modify: also copy the package's root `manifest.json` to `static/scada/manifest.json`.
- `frontend/views.py` — modify: add `_scada_home_page_id()` helper; replace the two hardcoded `home_page_id` context assignments; delete the two dead `_load_scada_scene()` call sites.
- `frontend/tests_scada_deploy.py` — new. Django tests for the manifest copy and the home-page-id helper.
- `static/asset/js/station/visualisation_import.js` — modify: `ScadaHost._createPopup` renders a same-document overlay (sharing `window.ScadaRuntime`/`window.tf100webScadaBuilder`) instead of an `iframe.srcdoc`.

---

## Task 1: Node test harness for the shared runtime JS modules

**Files:**
- Create: `BUILDER\tests\runtime-js\package.json`
- Create: `BUILDER\tests\runtime-js\harness.mjs`

**Interfaces:**
- Produces: `loadRuntime(moduleNames: string[]) -> window` — executes the named files from `src/ScadaBuilderV2.Rendering/Runtime/` in load order inside a `vm.Context`, returns the sandbox's `window` object. Later tasks' tests import this from `./harness.mjs`.

- [ ] **Step 1: Create the package manifest**

`BUILDER\tests\runtime-js\package.json`:
```json
{
  "name": "scada-builder-v2-runtime-js-tests",
  "private": true,
  "type": "module",
  "scripts": {
    "test": "node --test ."
  }
}
```

- [ ] **Step 2: Create the harness**

`BUILDER\tests\runtime-js\harness.mjs`:
```javascript
import vm from 'node:vm';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const RUNTIME_DIR = path.resolve(__dirname, '../../src/ScadaBuilderV2.Rendering/Runtime');

/**
 * Loads real runtime module files (from src/ScadaBuilderV2.Rendering/Runtime/) into a
 * sandboxed vm.Context with a minimal `window` global, in the given order. Returns the
 * sandbox's `window` so tests can call window.ScadaRuntime.* and inspect/stub globals
 * (window.postMessage, window.tf100webScadaBuilder, etc.) exactly as the browser would.
 *
 * @param {string[]} moduleNames - file names under Runtime/, e.g. ['tag-bridge.js'].
 * @returns {object} the sandbox's window object.
 */
export function loadRuntime(moduleNames) {
  const sandbox = { console };
  sandbox.window = sandbox;
  const context = vm.createContext(sandbox);

  for (const name of moduleNames) {
    const filePath = path.join(RUNTIME_DIR, name);
    const source = fs.readFileSync(filePath, 'utf8');
    vm.runInContext(source, context, { filename: name });
  }

  return context.window;
}
```

Note: `sandbox.window = sandbox` makes `window` and the sandbox's global scope the same object, so `window.ScadaRuntime = window.ScadaRuntime || {}` inside the loaded IIFEs works exactly like it does in a real browser's global scope.

- [ ] **Step 3: Verify the harness loads a real module**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node -e "import('./harness.mjs').then(({loadRuntime}) => { const w = loadRuntime(['tag-bridge.js']); console.log(typeof w.ScadaRuntime.TagBridge.getTagValue); })"`
Expected output: `function`

- [ ] **Step 4: Commit**

```bash
cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add tests/runtime-js/package.json tests/runtime-js/harness.mjs
git commit -m "test: add Node harness for executing shared runtime JS modules"
```

---

## Task 2: Fix Bug 1 — state evaluation never resolves prefixed tag ids

**Root cause:** TF100Web's tag catalog export (`frontend/scada_tags.py:83`) gives every tag the id `tf100.mapping.{id}` — this is what ends up as `tagName` in a state condition's AST. Command writes correctly resolve this id because `TagBridge.getTagValue`/`writeTag` delegate to `window.tf100webScadaBuilder`, which strips the `tf100.mapping.` prefix before reading `ScadaTagCache.values` (keyed by bare mapping id). State evaluation bypasses that bridge entirely: `expression-evaluator.js`'s `tagRef` case and `state-engine.js`'s null-check both index a raw `tagValues` object directly with the *prefixed* name, which never matches the *bare* keys — so every state is permanently treated as "quality unavailable" and every Element+ with a `StateConfig` is stuck on `QualityFallback` forever.

**Files:**
- Modify: `BUILDER\src\ScadaBuilderV2.Rendering\Runtime\expression-evaluator.js`
- Modify: `BUILDER\src\ScadaBuilderV2.Rendering\Runtime\state-engine.js`
- Create: `BUILDER\tests\runtime-js\expression-evaluator.test.mjs`
- Create: `BUILDER\tests\runtime-js\state-engine.test.mjs`

**Interfaces:**
- Consumes: `loadRuntime` from Task 1's `harness.mjs`.
- Produces: no public API change — `window.ScadaRuntime.ExpressionEvaluator.walk(ast, tagValues)` and `window.ScadaRuntime.StateEngine.evaluate(element, tagValues)` keep their existing signatures; they now prefer `window.ScadaRuntime.TagBridge.getTagValue(tagName)` over indexing `tagValues` directly, falling back to `tagValues[tagName]` only when no `TagBridge` module is loaded (keeps today's behavior for any caller that loads `expression-evaluator.js` standalone).

- [ ] **Step 1: Write the failing test for `ExpressionEvaluator`**

`BUILDER\tests\runtime-js\expression-evaluator.test.mjs`:
```javascript
import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

test('walk() resolves a "tf100.mapping.N" tagRef through TagBridge, not the raw tagValues map', () => {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js']);

  // Simulates TF100Web's window.tf100webScadaBuilder (visualisation_import.js), which
  // strips the "tf100.mapping." prefix before reading ScadaTagCache.values (bare ids).
  window.tf100webScadaBuilder = {
    getTagValue(tagId) {
      const mappingId = String(tagId).replace(/^tf100\.mapping\./, '');
      return { '42': 95 }[mappingId] ?? null;
    },
  };

  const ast = {
    type: 'binary',
    op: 'GreaterThan',
    left: { type: 'tagRef', tagName: 'tf100.mapping.42' },
    right: { type: 'literalNumber', value: 80 },
  };

  // tagValues intentionally empty/wrong-keyed: the fix must not need it when a
  // TagBridge-connected host bridge is present.
  const result = window.ScadaRuntime.ExpressionEvaluator.walk(ast, {});

  assert.equal(result, true);
});

test('walk() still resolves via the raw tagValues map when TagBridge is not loaded', () => {
  const window = loadRuntime(['expression-evaluator.js']);

  const ast = { type: 'tagRef', tagName: 'Temp' };
  const result = window.ScadaRuntime.ExpressionEvaluator.walk(ast, { Temp: 42 });

  assert.equal(result, 42);
});
```

- [ ] **Step 2: Run it to verify the first case fails**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test expression-evaluator.test.mjs`
Expected: the first test FAILS (`result` is `null`, not `true`) because `walkNode`'s `tagRef` case currently does `tagValues[tagName]` directly and ignores `TagBridge`. The second test passes already (no bridge loaded, existing raw-map behavior is what we're preserving).

- [ ] **Step 3: Fix `expression-evaluator.js`**

Find (around line 46-56):
```javascript
      case 'tagRef': {
        var tagName = node.tagName;
        if (!tagName) {
          return null;
        }
        var val = tagValues && tagValues[tagName];
        if (val === null || val === undefined) {
          return null;
        }
        return val;
      }
```

Replace with:
```javascript
      case 'tagRef': {
        var tagName = node.tagName;
        if (!tagName) {
          return null;
        }
        // Prefer TagBridge (which knows how to talk to the host's
        // window.tf100webScadaBuilder and strips id prefixes like "tf100.mapping.").
        // Fall back to the raw tagValues map only when TagBridge isn't loaded.
        var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
        var val = bridge ? bridge.getTagValue(tagName) : (tagValues && tagValues[tagName]);
        if (val === null || val === undefined) {
          return null;
        }
        return val;
      }
```

- [ ] **Step 4: Run it to verify both cases pass**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test expression-evaluator.test.mjs`
Expected: PASS (2/2)

- [ ] **Step 5: Write the failing test for `StateEngine.evaluate()`**

`BUILDER\tests\runtime-js\state-engine.test.mjs`:
```javascript
import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

function makeFakeElement(id, stateConfigJson) {
  return {
    id,
    _attrs: { 'data-scada-state-config': stateConfigJson },
    getAttribute(name) {
      return Object.prototype.hasOwnProperty.call(this._attrs, name) ? this._attrs[name] : null;
    },
    querySelector() {
      return null;
    },
  };
}

test('evaluate() applies the matching state effect when the tag comes from the host bridge, not QualityFallback', () => {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js', 'effect-applier.js', 'state-engine.js']);

  // Same host-bridge simulation as visualisation_import.js: bare mapping-id keys.
  window.tf100webScadaBuilder = {
    getTagValue(tagId) {
      const mappingId = String(tagId).replace(/^tf100\.mapping\./, '');
      return { '42': 95 }[mappingId] ?? null;
    },
  };

  const stateConfig = {
    qualityFallback: { opacity: 0.4 },
    defaultEffect: {},
    states: [
      {
        id: 's1',
        name: 'Alarme haute',
        enabled: true,
        expression: {
          ast: {
            type: 'binary',
            op: 'GreaterThan',
            left: { type: 'tagRef', tagName: 'tf100.mapping.42' },
            right: { type: 'literalNumber', value: 80 },
          },
        },
        effect: { backgroundColor: '#E53935' },
      },
    ],
  };

  const applied = [];
  window.ScadaRuntime.EffectApplier.apply = (element, effect) => applied.push(effect);

  const element = makeFakeElement('el1', JSON.stringify(stateConfig));

  // The tagValues param mirrors what onTagValuesChanged actually passes today
  // (ScadaTagCache.values — bare ids). It must NOT be needed for this to work.
  window.ScadaRuntime.StateEngine.evaluate(element, { '42': 95 });

  assert.deepEqual(applied, [{ backgroundColor: '#E53935' }],
    'expected the matching state effect, not QualityFallback ({ opacity: 0.4 })');
});
```

- [ ] **Step 6: Run it to verify it fails**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test state-engine.test.mjs`
Expected: FAIL — `applied` is `[{ opacity: 0.4 }]` (QualityFallback), not the alarm effect, because the null-check loop in `evaluate()` also indexes the raw map directly.

- [ ] **Step 7: Fix `state-engine.js`**

Find (around line 180-191, inside `evaluate()`):
```javascript
      // Collect tags from expression AST, skip state if any tag is null
      var tags = _collectTags(expression.ast);
      var anyNull = false;
      for (var t = 0; t < tags.length; t++) {
        if (tagValues[tags[t]] === null || tagValues[tags[t]] === undefined) {
          anyNull = true;
          break;
        }
      }
      if (anyNull) {
        continue;
      }

      // Evaluate expression
      var result = evaluator.walk(expression.ast, tagValues);
```

Replace with:
```javascript
      // Collect tags from expression AST, skip state if any tag is null.
      // Resolve through TagBridge (same as ExpressionEvaluator.walk) so the null-check
      // and the actual evaluation agree on where a tag's value comes from.
      var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
      var tags = _collectTags(expression.ast);
      var anyNull = false;
      for (var t = 0; t < tags.length; t++) {
        var tv = bridge ? bridge.getTagValue(tags[t]) : tagValues[tags[t]];
        if (tv === null || tv === undefined) {
          anyNull = true;
          break;
        }
      }
      if (anyNull) {
        continue;
      }

      // Evaluate expression
      var result = evaluator.walk(expression.ast, tagValues);
```

- [ ] **Step 8: Run it to verify it passes**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test state-engine.test.mjs`
Expected: PASS (1/1)

- [ ] **Step 9: Run the full Node runtime-js suite and the .NET suite**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test .`
Expected: all pass (3 tests so far).

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~RuntimeJsModulesTests"`
Expected: unchanged, all pass — confirms no public `window.ScadaRuntime.*` symbol was removed or renamed.

- [ ] **Step 10: Commit**

```bash
cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.Rendering/Runtime/expression-evaluator.js src/ScadaBuilderV2.Rendering/Runtime/state-engine.js tests/runtime-js/expression-evaluator.test.mjs tests/runtime-js/state-engine.test.mjs
git commit -m "fix: resolve state-rule tag values through TagBridge instead of a raw map

State conditions reference tags as \"tf100.mapping.N\" (per frontend/scada_tags.py's
export), but the host's live-value cache is keyed by the bare mapping id. Command
writes already went through TagBridge, which strips the prefix via
window.tf100webScadaBuilder; state evaluation bypassed TagBridge and indexed the raw
map directly, so every state was permanently treated as quality-unavailable."
```

---

## Task 3: Fix Bugs 2 and 3 — command-dispatcher.js contract mismatches

**Root cause (Bug 2):** `command-dispatcher.js`'s `_postMessage(action, payload)` helper nests popup data under `payload: { pageId, options }`, but TF100Web's message listener (`visualisation_import.js`) reads `msg.pageId` / `msg.options` directly on the message — the same flat shape `_navigateCommand` already uses correctly. `OpenPopup`/`ClosePopup`/`TogglePopup` therefore always deliver `pageId: undefined` to the host and never do anything.

**Root cause (Bug 3):** `_writeTagCommand(cmd)` takes only `cmd`, but its `SetFromInput` branch references `element.querySelector(...)` — `element` isn't in scope in that function, so triggering a `SetFromInput` write throws `ReferenceError: element is not defined` and the write silently never happens.

**Files:**
- Modify: `BUILDER\src\ScadaBuilderV2.Rendering\Runtime\command-dispatcher.js`
- Create: `BUILDER\tests\runtime-js\command-dispatcher.test.mjs`

**Interfaces:**
- Consumes: `loadRuntime` from Task 1.
- Produces: no public API change — `window.ScadaRuntime.CommandDispatcher.{bind, execute, _run}` keep their signatures. `_postMessage` now takes `(action, pageId, options)` instead of `(action, payload)` — internal to this module, no other file calls it.

- [ ] **Step 1: Write the failing tests**

`BUILDER\tests\runtime-js\command-dispatcher.test.mjs`:
```javascript
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
  assert.deepEqual(messages[0].options, { width: 400 });
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
```

- [ ] **Step 2: Run to verify all three fail**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test command-dispatcher.test.mjs`
Expected: FAIL —
- Test 1/2: `messages[0].pageId` is `undefined` (currently nested at `messages[0].payload.pageId`).
- Test 3: throws `ReferenceError: element is not defined`.

- [ ] **Step 3: Fix the popup payload shape**

Find (around line 133-143):
```javascript
      // ── Popup commands ─────────────────────────────────────────────────
      case 'OpenPopup':
        _postMessage('openPopup', { pageId: cmd.targetPageId, options: cmd.popupOptions });
        break;

      case 'ClosePopup':
        _postMessage('closePopup', { pageId: cmd.targetPageId });
        break;

      case 'TogglePopup':
        _postMessage('togglePopup', { pageId: cmd.targetPageId, options: cmd.popupOptions });
        break;
```

Replace with:
```javascript
      // ── Popup commands ─────────────────────────────────────────────────
      case 'OpenPopup':
        _postMessage('openPopup', cmd.targetPageId, cmd.popupOptions);
        break;

      case 'ClosePopup':
        _postMessage('closePopup', cmd.targetPageId);
        break;

      case 'TogglePopup':
        _postMessage('togglePopup', cmd.targetPageId, cmd.popupOptions);
        break;
```

Find (around line 227-233):
```javascript
  function _postMessage(action, payload) {
    window.postMessage({
      source: 'scada-builder-v2',
      action: action,
      payload: payload
    }, '*');
  }
```

Replace with:
```javascript
  function _postMessage(action, pageId, options) {
    // Flat shape — matches _navigateCommand and the TF100Web host listener
    // (visualisation_import.js reads msg.pageId / msg.options directly).
    window.postMessage({
      source: 'scada-builder-v2',
      action: action,
      pageId: pageId,
      options: options
    }, '*');
  }
```

- [ ] **Step 4: Fix the `SetFromInput` crash**

Find (around line 116-125):
```javascript
    switch (cmd.kind) {
      // ── WriteTag variants ──────────────────────────────────────────────
      case 'WriteTag':
        _writeTagCommand(cmd);
        break;
```

Replace with:
```javascript
    switch (cmd.kind) {
      // ── WriteTag variants ──────────────────────────────────────────────
      case 'WriteTag':
        _writeTagCommand(cmd, element);
        break;
```

Find (around line 170-207):
```javascript
  function _writeTagCommand(cmd) {
    if (!cmd || !cmd.writeTagId) {
      return;
    }

    var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
    if (!bridge) {
      return;
    }

    switch (cmd.writeMode) {
      case 'Momentary':
        // Momentary is handled as two commands: press (onValue) and release (offValue).
        // For a single click/dispatch, default to press cycle.
        bridge.writeTag(cmd.writeTagId, cmd.onValue, { phase: 'press' });
        break;

      case 'Toggle':
        var current = bridge.getTagValue(cmd.readTagId || cmd.writeTagId);
        var boolVal = !!(current && current !== '0' && current !== 'false');
        bridge.writeTag(cmd.writeTagId, boolVal ? '0' : '1', { mode: 'Toggle' });
        break;

      case 'SetFixed':
        bridge.writeTag(cmd.writeTagId, cmd.fixedValue, { mode: 'SetFixed' });
        break;

      case 'SetFromInput':
        var input = element.querySelector('input, textarea');
        if (input) {
          bridge.writeTag(cmd.writeTagId, input.value, { mode: 'SetFromInput' });
        }
        break;

      default:
        bridge.writeTag(cmd.writeTagId, cmd.fixedValue, { mode: 'SetFixed' });
        break;
    }
  }
```

Replace with:
```javascript
  function _writeTagCommand(cmd, element) {
    if (!cmd || !cmd.writeTagId) {
      return;
    }

    var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
    if (!bridge) {
      return;
    }

    switch (cmd.writeMode) {
      case 'Momentary':
        // Momentary is handled as two commands: press (onValue) and release (offValue).
        // For a single click/dispatch, default to press cycle.
        bridge.writeTag(cmd.writeTagId, cmd.onValue, { phase: 'press' });
        break;

      case 'Toggle':
        var current = bridge.getTagValue(cmd.readTagId || cmd.writeTagId);
        var boolVal = !!(current && current !== '0' && current !== 'false');
        bridge.writeTag(cmd.writeTagId, boolVal ? '0' : '1', { mode: 'Toggle' });
        break;

      case 'SetFixed':
        bridge.writeTag(cmd.writeTagId, cmd.fixedValue, { mode: 'SetFixed' });
        break;

      case 'SetFromInput':
        var input = element ? element.querySelector('input, textarea') : null;
        if (input) {
          bridge.writeTag(cmd.writeTagId, input.value, { mode: 'SetFromInput' });
        }
        break;

      default:
        bridge.writeTag(cmd.writeTagId, cmd.fixedValue, { mode: 'SetFixed' });
        break;
    }
  }
```

- [ ] **Step 5: Run to verify all three pass**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test command-dispatcher.test.mjs`
Expected: PASS (3/3)

- [ ] **Step 6: Run the full Node suite and the .NET suite**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test .`
Expected: all pass (6 tests total).

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet test ScadaBuilderV2.sln --no-restore`
Expected: same baseline as before this plan (the 4 pre-existing `WebViewContextMenuScriptTests` failures are unrelated and untouched; everything else green).

- [ ] **Step 7: Commit**

```bash
cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.Rendering/Runtime/command-dispatcher.js tests/runtime-js/command-dispatcher.test.mjs
git commit -m "fix: flatten popup postMessage payload and fix SetFromInput crash

OpenPopup/ClosePopup/TogglePopup nested {pageId, options} under a payload key that
the TF100Web host listener never reads (it reads msg.pageId/msg.options directly,
same as the already-working Navigate command) — popups never opened. Separately,
_writeTagCommand's SetFromInput branch referenced an out-of-scope `element`
variable, throwing on every attempt."
```

---

## Task 4: Fix Bug 5 — home page id must come from the deployed package, not a hardcoded setting

**Root cause:** `frontend/views.py` sets `context["home_page_id"]` from `getattr(settings, "SCADA_HOME_PAGE_ID", "win00009")` — a static Django setting that's never updated by `deploy_scada_builder`. If a future export sets a different `HomePageId` in `manifest.json`, TF100Web keeps opening the stale page until someone manually edits the Django setting. `deploy_scada_builder` doesn't even copy `manifest.json` today, so there's nothing to read yet.

**Files:**
- Modify: `TF100WEB\core\management\commands\deploy_scada_builder.py`
- Modify: `TF100WEB\frontend\views.py`
- Create: `TF100WEB\frontend\tests_scada_deploy.py`

**Interfaces:**
- Produces: `frontend.views._scada_home_page_id() -> str` — reads `HomePageId` from `STATIC_ROOT/scada/manifest.json`, falls back to `getattr(settings, "SCADA_HOME_PAGE_ID", "win00009")` if the file is missing or invalid.

- [ ] **Step 1: Write the failing test for the manifest copy**

`TF100WEB\frontend\tests_scada_deploy.py`:
```python
import json
import shutil
import zipfile
from io import BytesIO
from pathlib import Path
from tempfile import TemporaryDirectory

from django.core.management import call_command
from django.test import SimpleTestCase, override_settings

from .scada_package import SCADA_PACKAGE_DIR_NAME


def _build_test_package(home_page_id="win00009"):
    """Builds a minimal in-memory .sb2 zip with one page and a manifest.json."""
    buffer = BytesIO()
    with zipfile.ZipFile(buffer, "w") as zf:
        manifest = {
            "HomePageId": home_page_id,
            "Pages": [{"Id": "win00009", "Type": "default", "IncludeInBuild": True}],
        }
        zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/manifest.json", json.dumps(manifest))
        zf.writestr(
            f"{SCADA_PACKAGE_DIR_NAME}/win00009/win00009.html",
            '<div id="ft100-win00009"></div>',
        )
        zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/win00009/css/win00009.abc12345.css", "")
        zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/scada-runtime.deadbeef.js", "// runtime")
    return buffer.getvalue()


class DeployScadaBuilderManifestTests(SimpleTestCase):
    def test_deploy_copies_manifest_json_to_static_root(self):
        with TemporaryDirectory() as static_root, TemporaryDirectory() as pkg_dir:
            package_path = Path(pkg_dir) / "export.sb2"
            package_path.write_bytes(_build_test_package(home_page_id="win00042"))

            with override_settings(STATIC_ROOT=static_root):
                call_command("deploy_scada_builder", str(package_path))

                deployed_manifest = Path(static_root) / "scada" / "manifest.json"
                self.assertTrue(deployed_manifest.is_file(), "manifest.json must be copied to STATIC_ROOT/scada/")
                manifest = json.loads(deployed_manifest.read_text(encoding="utf-8"))
                self.assertEqual(manifest["HomePageId"], "win00042")
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd "F:\Projet\Git\TF100Web" && python manage.py test frontend.tests_scada_deploy -v 2`
Expected: FAIL — `deployed_manifest.is_file()` is `False` (current `deploy_scada_builder.py` never copies `manifest.json`).

- [ ] **Step 3: Fix `deploy_scada_builder.py`**

Find (around line 29-33, the counters):
```python
        # --- Stats counters ---
        html_count = 0
        runtime_js_count = 0
        css_count = 0
        image_count = 0
```

Replace with:
```python
        # --- Stats counters ---
        html_count = 0
        runtime_js_count = 0
        css_count = 0
        image_count = 0
        manifest_count = 0
```

Find (around line 89-96, the HTML branch — the manifest branch goes right after it, both are terminal per-file checks):
```python
                # 4. */*.html  -->  static/scada/pages/<page_dir>/<name>.html
                if relative.suffix == ".html" and len(parts) >= 2:
                    page_dir = parts[0]
                    dest = static_root / "scada" / "pages" / page_dir / rel_name
                    dest.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(str(file_path), str(dest))
                    html_count += 1
                    continue
```

Replace with:
```python
                # 4. */*.html  -->  static/scada/pages/<page_dir>/<name>.html
                if relative.suffix == ".html" and len(parts) >= 2:
                    page_dir = parts[0]
                    dest = static_root / "scada" / "pages" / page_dir / rel_name
                    dest.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(str(file_path), str(dest))
                    html_count += 1
                    continue

                # 5. manifest.json (package root)  -->  static/scada/manifest.json
                # Read by frontend.views._scada_home_page_id() so the SCADA host always
                # opens the page the export actually marked as HomePageId.
                if rel_name == "manifest.json" and len(parts) == 1:
                    dest = static_root / "scada" / "manifest.json"
                    dest.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(str(file_path), str(dest))
                    manifest_count += 1
                    continue
```

Find (around line 109-113, the report):
```python
        self.stdout.write(f"Deployed {html_count} page HTML file(s)")
        self.stdout.write(f"Deployed {runtime_js_count} runtime JS file(s)")
        self.stdout.write(f"Deployed {css_count} CSS file(s)")
        self.stdout.write(f"Deployed {image_count} image(s)")
```

Replace with:
```python
        self.stdout.write(f"Deployed {html_count} page HTML file(s)")
        self.stdout.write(f"Deployed {runtime_js_count} runtime JS file(s)")
        self.stdout.write(f"Deployed {css_count} CSS file(s)")
        self.stdout.write(f"Deployed {image_count} image(s)")
        self.stdout.write(f"Deployed {manifest_count} manifest.json file(s)")
```

- [ ] **Step 4: Run it to verify it passes**

Run: `cd "F:\Projet\Git\TF100Web" && python manage.py test frontend.tests_scada_deploy -v 2`
Expected: PASS (1/1)

- [ ] **Step 5: Write the failing test for `_scada_home_page_id()`**

Append to `TF100WEB\frontend\tests_scada_deploy.py`:
```python
from . import views


class ScadaHomePageIdTests(SimpleTestCase):
    def test_reads_home_page_id_from_deployed_manifest(self):
        with TemporaryDirectory() as static_root:
            scada_dir = Path(static_root) / "scada"
            scada_dir.mkdir(parents=True)
            (scada_dir / "manifest.json").write_text(
                json.dumps({"HomePageId": "win00042"}), encoding="utf-8"
            )

            with override_settings(STATIC_ROOT=static_root, SCADA_HOME_PAGE_ID="win00009"):
                self.assertEqual(views._scada_home_page_id(), "win00042")

    def test_falls_back_to_setting_when_manifest_missing(self):
        with TemporaryDirectory() as static_root:
            with override_settings(STATIC_ROOT=static_root, SCADA_HOME_PAGE_ID="win00009"):
                self.assertEqual(views._scada_home_page_id(), "win00009")

    def test_falls_back_to_setting_when_manifest_is_invalid_json(self):
        with TemporaryDirectory() as static_root:
            scada_dir = Path(static_root) / "scada"
            scada_dir.mkdir(parents=True)
            (scada_dir / "manifest.json").write_text("{not json", encoding="utf-8")

            with override_settings(STATIC_ROOT=static_root, SCADA_HOME_PAGE_ID="win00009"):
                self.assertEqual(views._scada_home_page_id(), "win00009")
```

- [ ] **Step 6: Run it to verify it fails**

Run: `cd "F:\Projet\Git\TF100Web" && python manage.py test frontend.tests_scada_deploy -v 2`
Expected: FAIL with `AttributeError: module 'frontend.views' has no attribute '_scada_home_page_id'`.

- [ ] **Step 7: Add the helper and wire it in**

In `TF100WEB\frontend\views.py`, find (around line 240-245, right after `_extract_page_dimension_from_html`):
```python
def _extract_page_dimension_from_html(html, attr):
    pattern = re.compile(rf'{re.escape(attr)}\s*=\s*"([^"]*)"')
    match = pattern.search(html)
    if match:
        return match.group(1)
    return ""
```

Add immediately after it:
```python
def _scada_home_page_id() -> str:
    """Returns the SCADA home page id, read fresh from the deployed manifest.json.

    deploy_scada_builder copies the package's manifest.json to
    STATIC_ROOT/scada/manifest.json. Reading it on every call (no caching) means a
    fresh deploy takes effect immediately, consistent with the rest of the
    static-file SCADA serving path (no Gunicorn restart required).
    """
    fallback = getattr(settings, "SCADA_HOME_PAGE_ID", "win00009")
    manifest_path = Path(getattr(settings, "STATIC_ROOT", "")) / "scada" / "manifest.json"
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return fallback
    home_page_id = manifest.get("HomePageId")
    return home_page_id if isinstance(home_page_id, str) and home_page_id else fallback
```

Then find (two occurrences — `StationVisualisationView.get`, around line 1196, and `StationVisualisationView.post`, around line 1402):
```python
        context["home_page_id"] = getattr(settings, "SCADA_HOME_PAGE_ID", "win00009")
```

Replace **both** occurrences with:
```python
        context["home_page_id"] = _scada_home_page_id()
```

- [ ] **Step 8: Run it to verify it passes**

Run: `cd "F:\Projet\Git\TF100Web" && python manage.py test frontend.tests_scada_deploy -v 2`
Expected: PASS (4/4)

- [ ] **Step 9: Run the broader frontend suite to check for regressions**

Run: `cd "F:\Projet\Git\TF100Web" && python manage.py test frontend -v 2`
Expected: same pre-existing baseline as documented in `.superpowers/sdd/task-15-report.md` (2 failures / 4 errors, all pre-existing environment issues — missing `403.html`/`scada_builder.html` templates and temp-dir filesystem expectations, unrelated to this change). No *new* failures.

- [ ] **Step 10: Commit**

```bash
cd "F:\Projet\Git\TF100Web"
git add core/management/commands/deploy_scada_builder.py frontend/views.py frontend/tests_scada_deploy.py
git commit -m "fix: derive SCADA home page id from the deployed manifest, not a static setting

deploy_scada_builder now also copies the package's manifest.json to
STATIC_ROOT/scada/manifest.json. frontend.views._scada_home_page_id() reads its
HomePageId on every request (falling back to settings.SCADA_HOME_PAGE_ID), so a
new export's home page takes effect without editing Django settings."
```

---

## Task 5: Remove the always-dead `_load_scada_scene()` call

**Root cause:** `scada_scene` is populated by `_load_scada_scene()` only when `TF100_INDUSTRIAL_DEPLOYMENT and station_type == SCADA_BUILDER_2` — but `visualisation.html` only ever reads `{% if scada_scene %}` inside the `{% else %}` branch of that *exact same* condition. `scada_scene` is therefore **always `None` at the only place that inspects it** — the legacy ZIP-package parse (`_load_scada_scene`) runs on every request for a SCADA_BUILDER_2 industrial station and its result is provably never used. Confirmed already marked `# DEPRECATED` (commit `51e7153`); this task removes the redundant call, not the function itself (per Global Constraints).

**Files:**
- Modify: `TF100WEB\frontend\views.py` (two call sites, `StationVisualisationView.get` and `.post`)
- Modify: `TF100WEB\frontend\tests_scada_deploy.py`

**Interfaces:**
- No public interface change — `scada_scene` stays `None` in the exact cases where it was already `None` (all of them, per the proof above); only the wasted `_load_scada_scene()` call is removed.

- [ ] **Step 1: Write the failing test**

Append to `TF100WEB\frontend\tests_scada_deploy.py`:
```python
from unittest.mock import patch

from django.contrib.auth import get_user_model
from django.test import TestCase
from django.urls import reverse

from .models import StationConfig


class ScadaSceneDeadCodeTests(TestCase):
    def setUp(self):
        User = get_user_model()
        self.user = User.objects.create_user(username="tester", password="x")
        self.client.force_login(self.user)
        StationConfig.objects.update_or_create(
            pk=1, defaults={"station_type": StationConfig.StationTypeChoices.SCADA_BUILDER_2}
        )

    @override_settings(TF100_INDUSTRIAL_DEPLOYMENT=True)
    def test_load_scada_scene_not_called_for_industrial_scada_builder_2(self):
        with patch("frontend.views._load_scada_scene") as mocked:
            self.client.get(reverse("frontend_station_visualisation"))
            mocked.assert_not_called()
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd "F:\Projet\Git\TF100Web" && python manage.py test frontend.tests_scada_deploy.ScadaSceneDeadCodeTests -v 2`
Expected: FAIL — `mocked.assert_not_called()` raises because the current code still calls `_load_scada_scene()`.

- [ ] **Step 3: Remove the two dead call sites**

In `TF100WEB\frontend\views.py`, find (in `StationVisualisationView.get`, around line 1133-1135):
```python
        scada_scene = None
        if settings.TF100_INDUSTRIAL_DEPLOYMENT and config.station_type == StationConfig.StationTypeChoices.SCADA_BUILDER_2:
            scada_scene = _load_scada_scene()
```

Replace with:
```python
        # visualisation.html only reads {% if scada_scene %} in the *else* branch of this
        # exact condition (industrial + SCADA_BUILDER_2 renders the new scada-host/AJAX
        # branch instead) — scada_scene is therefore always None here. Parsing the legacy
        # package (_load_scada_scene, DEPRECATED) would be pure wasted I/O.
        scada_scene = None
```

Find (in `StationVisualisationView.post`, around line 1339-1341):
```python
        scada_scene = None
        if settings.TF100_INDUSTRIAL_DEPLOYMENT and config.station_type == StationConfig.StationTypeChoices.SCADA_BUILDER_2:
            scada_scene = _load_scada_scene()
```

Replace with the same comment+assignment as above:
```python
        # visualisation.html only reads {% if scada_scene %} in the *else* branch of this
        # exact condition (industrial + SCADA_BUILDER_2 renders the new scada-host/AJAX
        # branch instead) — scada_scene is therefore always None here. Parsing the legacy
        # package (_load_scada_scene, DEPRECATED) would be pure wasted I/O.
        scada_scene = None
```

- [ ] **Step 4: Run it to verify it passes**

Run: `cd "F:\Projet\Git\TF100Web" && python manage.py test frontend.tests_scada_deploy -v 2`
Expected: PASS (5/5)

- [ ] **Step 5: Run the broader frontend suite**

Run: `cd "F:\Projet\Git\TF100Web" && python manage.py test frontend -v 2`
Expected: same pre-existing baseline as Task 4 Step 9 — no new failures.

- [ ] **Step 6: Commit**

```bash
cd "F:\Projet\Git\TF100Web"
git add frontend/views.py frontend/tests_scada_deploy.py
git commit -m "perf: stop parsing the legacy SCADA package on every industrial SCADA_BUILDER_2 request

scada_scene is only ever read by visualisation.html's else-branch of the exact
condition that populated it, so it was always None where it mattered. Removes a
per-request ZIP parse that never affected rendering."
```

---

## Task 6: Fix Bug 4 — popups must run in the same window as the runtime, not a sandboxed iframe

**Root cause:** `ScadaHost._createPopup` builds an `<iframe>` and sets `iframe.srcdoc = data.html` — just the raw `<div id="ft100-...">` fragment, with no `<link>` stylesheet and no `<script>` tag. Even once Task 3 fixes the payload bug so popups *open*, the iframe has its own separate `window`: `window.tf100webScadaBuilder`, `window.ScadaRuntime`, and `ScadaTagCache`'s polling all live on the *parent* window and are never available inside the iframe's document. The popup would render completely unstyled with no working commands or live state.

**Decision:** Render the popup as a same-document overlay (a positioned `<div>` appended to `#scada-host`, matching the existing overlay/panel/close-button chrome already built for the iframe version) instead of an `<iframe>`. This keeps `window.ScadaRuntime`/`window.tf100webScadaBuilder`/`ScadaTagCache` all in scope for the popup's content with zero extra bridging code — CSS is added to `<head>` exactly like the main page already does (`ScadaHost.loadPage`'s CSS-injection block), and `window.ScadaRuntime.initPage(content, pageId)` is called on the injected content just like the main page flow. This is a deviation from `docs/03_runtime_contracts/.../2026-07-07-scada-export-runtime-tf100web-integration-design.md` §8.4 (which specified `iframe.srcdoc`) — flagged here because that design's premise (no extra network request for popup content) is unaffected, but the "isolated iframe" framing is dropped since CSS is already strictly page-scoped (validated by `Ft100PackageValidator`, no global selectors permitted), so there is no real isolation need, and the iframe approach cannot work at all without it.

**Files:**
- Modify: `TF100WEB\static\asset\js\station\visualisation_import.js`

**Interfaces:**
- No public interface change — `ScadaHost._createPopup(pageId, options)` keeps the same signature and caller (`window.addEventListener('message', ...)` from Task 3's fix).

- [ ] **Step 1: Manual verification of current (broken) behavior — before the fix**

There is no browser-DOM test harness in TF100Web today (Django tests are Python-only; the Node harness added in Task 1 lives in the other repo and only covers the runtime module files, not TF100Web's own JS). This step is a manual browser check, run once before the fix and once after, using the `verify` skill's philosophy of exercising the real flow rather than only trusting a diff:

1. On a TF100Web dev instance with `TF100_INDUSTRIAL_DEPLOYMENT=True` and a `StationConfig` set to `SCADA_BUILDER_2`, deploy a `.sb2` package that has at least one Element+ with an `OpenPopup` command bound to a button.
2. Open `/visualisation/`, open the browser devtools console, click the button.
3. Confirm (current, broken): nothing visible happens (Bug 2, fixed in Task 3) — after Task 3 alone, confirm an `<iframe>` popup opens but shows unstyled, colorless content and clicking anything inside it does nothing.

- [ ] **Step 2: Rewrite `_createPopup` as a same-document overlay**

Find in `TF100WEB\static\asset\js\station\visualisation_import.js` (around line 145-182):
```javascript
  _createPopup(pageId, options) {
    fetch(`/visualisation/scada/page/${encodeURIComponent(pageId)}/`)
      .then(r => r.json())
      .then(data => {
        if (!data.html) return;

        const overlay = document.createElement('div');
        overlay.className = 'scada-popup-overlay';
        overlay.dataset.scadaPopupPageId = pageId;
        overlay.style.cssText = 'position:absolute;inset:0;z-index:10000;'
          + 'background:rgba(0,0,0,0.28);display:flex;align-items:center;justify-content:center;pointer-events:auto;';

        const panel = document.createElement('div');
        panel.style.cssText = 'position:relative;background:#fff;'
          + 'border:1px solid rgba(15,42,48,0.24);box-shadow:0 16px 42px rgba(15,42,48,0.28);'
          + 'width:80%;height:80%;max-width:960px;max-height:720px;';

        const closeBtn = document.createElement('button');
        closeBtn.type = 'button';
        closeBtn.textContent = '×';
        closeBtn.style.cssText = 'position:absolute;top:8px;right:8px;z-index:1;'
          + 'width:28px;height:28px;border:0;background:rgba(15,42,48,0.08);'
          + 'border-radius:50%;font-size:18px;cursor:pointer;color:#0f2a30;';
        closeBtn.onclick = function () { overlay.remove(); };
        panel.appendChild(closeBtn);

        const iframe = document.createElement('iframe');
        iframe.srcdoc = data.html;
        iframe.style.cssText = 'width:100%;height:100%;border:0;';
        panel.appendChild(iframe);

        overlay.appendChild(panel);
        overlay.addEventListener('click', function (e) {
          if (e.target === overlay) overlay.remove();
        });
        document.getElementById('scada-host').appendChild(overlay);
      });
  },
```

Replace with:
```javascript
  _createPopup(pageId, options) {
    fetch(`/visualisation/scada/page/${encodeURIComponent(pageId)}/`)
      .then(r => r.json())
      .then(data => {
        if (!data.html) return;

        // Same-document overlay, not an iframe: window.ScadaRuntime,
        // window.tf100webScadaBuilder, and ScadaTagCache's polling all live on this
        // window, and popup content needs all three (live state colors, commands).
        // Page CSS is already strictly scoped under #ft100-<pageId> (enforced by
        // Ft100PackageValidator, no global selectors permitted), so there's no
        // cross-page leakage risk from sharing the document.
        if (data.css_hash && !this.currentCssHashes.has(data.css_hash)) {
          const link = document.createElement('link');
          link.rel = 'stylesheet';
          link.href = `/static/scada/css/${encodeURIComponent(pageId)}.${data.css_hash}.css`;
          document.head.appendChild(link);
          this.currentCssHashes.add(data.css_hash);
        }

        const overlay = document.createElement('div');
        overlay.className = 'scada-popup-overlay';
        overlay.dataset.scadaPopupPageId = pageId;
        overlay.style.cssText = 'position:absolute;inset:0;z-index:10000;'
          + 'background:rgba(0,0,0,0.28);display:flex;align-items:center;justify-content:center;pointer-events:auto;';

        const panel = document.createElement('div');
        panel.style.cssText = 'position:relative;background:#fff;'
          + 'border:1px solid rgba(15,42,48,0.24);box-shadow:0 16px 42px rgba(15,42,48,0.28);'
          + 'width:80%;height:80%;max-width:960px;max-height:720px;overflow:auto;';

        const closeBtn = document.createElement('button');
        closeBtn.type = 'button';
        closeBtn.textContent = '×';
        closeBtn.style.cssText = 'position:absolute;top:8px;right:8px;z-index:1;'
          + 'width:28px;height:28px;border:0;background:rgba(15,42,48,0.08);'
          + 'border-radius:50%;font-size:18px;cursor:pointer;color:#0f2a30;';
        closeBtn.onclick = function () { overlay.remove(); };
        panel.appendChild(closeBtn);

        const content = document.createElement('div');
        content.innerHTML = data.html;
        panel.appendChild(content);

        overlay.appendChild(panel);
        overlay.addEventListener('click', function (e) {
          if (e.target === overlay) overlay.remove();
        });
        document.getElementById('scada-host').appendChild(overlay);

        if (window.ScadaRuntime && window.ScadaRuntime.initPage) {
          window.ScadaRuntime.initPage(content, pageId);
        }
      });
  },
```

- [ ] **Step 3: Manual verification after the fix**

Repeat Step 1's flow: click the `OpenPopup`-bound button.
Expected: the popup overlay appears styled exactly like the target page (colors, fonts, layout match what that page looks like when opened directly), any live-tag-driven state colors on elements inside the popup update the same way they do on the main page, and any command button inside the popup (write/navigate/close) works. Click outside the panel or the `×` button closes it (`overlay.remove()`, unchanged).

- [ ] **Step 4: Commit**

```bash
cd "F:\Projet\Git\TF100Web"
git add static/asset/js/station/visualisation_import.js
git commit -m "fix: render SCADA popups as same-document overlays instead of sandboxed iframes

iframe.srcdoc had no page CSS and no access to window.ScadaRuntime/
window.tf100webScadaBuilder/ScadaTagCache (all parent-window-only), so popup
content was unstyled and non-interactive even once the postMessage payload bug
was fixed. A same-document overlay reuses the exact runtime already loaded for
the main page — page CSS is strictly scoped (Ft100PackageValidator forbids
global selectors), so there is no cross-page leakage risk."
```

---

## Task 7: Final end-to-end verification

**Files:** none (verification only).

- [ ] **Step 1: Full .NET suite (SCADA Builder V2)**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet build ScadaBuilderV2.sln --no-restore && dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 0 build errors; same 4 pre-existing `WebViewContextMenuScriptTests` failures as the session baseline (unrelated to this plan), everything else green — including all `Ft100SceneExporterTests` and `RuntimeJsModulesTests`.

- [ ] **Step 2: Full Node runtime-js suite (SCADA Builder V2)**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test .`
Expected: all pass (6 tests: 2 expression-evaluator, 1 state-engine, 3 command-dispatcher).

- [ ] **Step 3: Full Django frontend suite (TF100Web)**

Run: `cd "F:\Projet\Git\TF100Web" && python manage.py test frontend -v 2`
Expected: same pre-existing baseline documented in `.superpowers/sdd/task-15-report.md` plus the 5 new tests in `frontend.tests_scada_deploy` passing — no new failures beyond the documented pre-existing ones.

- [ ] **Step 4: Manual browser verification against the two questions this plan was scoped to answer**

On a TF100Web dev instance with `TF100_INDUSTRIAL_DEPLOYMENT=True`, `StationConfig.station_type == SCADA_BUILDER_2`, and a freshly `deploy_scada_builder`-deployed `.sb2` package containing at least one Element+ with a `StateConfig` (tag-driven color) and one with a `CommandConfig` (`Navigate` and `OpenPopup`):

1. Open `/visualisation/` — confirm the SCADA zone renders (not blank, not an error), and any Element+ with a `StateConfig` shows its **live tag-driven color**, not the permanent semi-transparent/black-border `QualityFallback` look.
2. Click a button bound to `Navigate` — confirm the URL updates (`history.pushState`) and the new page's content swaps in via the Network tab showing a `fetch` to `/visualisation/scada/page/<id>/` (XHR/fetch), **not** a full-page navigation (no full-document reload in the Network tab's "Doc" row).
3. Click a button bound to `OpenPopup` — confirm the popup opens, is fully styled, and any state/command elements inside it work; confirm opening/closing it does **not** trigger a full-page reload either (same fetch-based network pattern).
4. Change the exported project's home page and re-run `deploy_scada_builder` — confirm `/visualisation/` now opens the *new* home page without any Django settings edit or process restart.

- [ ] **Step 5: If all four checks pass, this plan is complete.** If any check fails, return to Systematic Debugging (Phase 1: reproduce, gather evidence) rather than patching ad hoc — do not layer a second fix on top of an unverified first one.
