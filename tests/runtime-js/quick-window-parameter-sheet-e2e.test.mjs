import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadRuntime, createFakeDom, parseFragment } from './harness.mjs';

/**
 * Task 12: proves a parameter-sheet quick window end to end — Builder export through the shared runtime.
 *
 * The fixture (`tests/conformance/quick-window-parameter-sheet-e2e/`) is a real Builder export: a
 * `QuickWindow` page with `#1` (read), `#3` (state expression) and `#4` (text) placeholders, opened from
 * a caller page through two different parameter sheets ("Ventilateur 3", "Ventilateur 5"), lowered,
 * validated (0 error) and exported by `Ft100SceneExporter` —
 * `tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetEndToEndTests.cs` produces and locks it (run
 * `SCADA_UPDATE_PARAMETER_SHEET_E2E=1` against that test to regenerate it after a deliberate review).
 *
 * This test reads that committed manifest and HTML fragment unconditionally (no environment variable, no
 * skip) and mounts the REAL exported fragment through the real `loadRegistries` → `open` → `mountInstance`
 * sequence of `quick-window-runtime.js`, with a minimal test-only `HostAdapter` standing in for the
 * production host — the same pattern `QuickWindowPreviewDocumentFactory`'s `PlaceholderMountScript` uses
 * for the Builder's own editor-only preview (no production `ScadaRuntime.HostAdapter` exists in this repo;
 * TF100Web supplies the real one on its branch).
 *
 * Decisions: DEC-0050, DEC-0052.
 * Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §12.4.
 */

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const FIXTURE_DIR = path.resolve(__dirname, '../conformance/quick-window-parameter-sheet-e2e');

function readFixture() {
  const manifestPath = path.join(FIXTURE_DIR, 'manifest.json');
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  assert.equal(manifest.QuickWindows.length, 1, 'the fixture opens the same definition from two sheets');
  const definition = manifest.QuickWindows[0];
  const htmlPath = path.join(FIXTURE_DIR, definition.RelativePath.replace(/\//g, path.sep));
  const document = fs.readFileSync(htmlPath, 'utf8');
  // The exported document is `<!doctype html><html>...<body ...>{scene root div}</body></html>`
  // (`Ft100SceneExporter.BuildDocumentHtml`); the scene root div is the one well-formed fragment a real
  // host mounts. No self-closing tag reaches it (the .NET fixture test asserts this at generation time),
  // so the harness's minimal fragment parser can walk it exactly as it would a hand-written fixture.
  const bodyMatch = document.match(/<body[^>]*>([\s\S]*)<\/body>/);
  assert.ok(bodyMatch, 'the exported document must carry a <body> the instance content comes from');
  const fragmentTemplate = bodyMatch[1].trim();
  return { manifest, definition, fragmentTemplate };
}

function invocationNamed(manifest, literalValue) {
  const members = manifest.QuickWindows[0].InterfaceMembers;
  const member4 = members.find((m) => m.Name === '#4');
  return manifest.QuickWindowInvocations.find((invocation) =>
    invocation.Bindings.some((b) => b.MemberKey === member4.MemberKey && b.LiteralValue === literalValue));
}

function bindingFor(invocation, manifest, placeholderName) {
  const members = manifest.QuickWindows[0].InterfaceMembers;
  const member = members.find((m) => m.Name === placeholderName);
  return invocation.Bindings.find((b) => b.MemberKey === member.MemberKey);
}

function loadE2eRuntime() {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js', 'quick-window-runtime.js']);
  Object.assign(window, createFakeDom());
  return window;
}

/**
 * Fix round 1: installs a stand-in for `window.tf100webScadaBuilder` that reproduces, exactly, how
 * the real TF100Web runtime resolves a tag value (`tag-bridge.js`'s `getTagValue` delegates to it
 * when present) — stripping the `tf100.mapping.` prefix and reading a map keyed by the bare mapping
 * id, mirroring `ScadaTagCache.values` in TF100Web's `visualisation_import.js`. Without this, the
 * fallback in `tag-bridge.js` reads its own local cache keyed by WHATEVER string a tagRef carries —
 * which is exactly how a raw, uncanonicalized tag NAME ("FAN_03_SPEED") inside an Expression
 * binding's AST could look resolved in this test (any key "works" against a same-repo local cache)
 * while never animating anything in the real host, whose cache is keyed by mapping id alone. Seeding
 * `values` by mapping id and asserting through this stand-in is what would have caught that: a
 * tagRef left as "FAN_03_SPEED" resolves to `undefined` here, exactly as it would have in production.
 *
 * @param {object} window - The sandbox window (`loadE2eRuntime()`'s return value).
 * @param {Record<string, number|boolean|string>} valuesByMappingId - e.g. `{ '302': 10 }` for `tf100.mapping.302`.
 */
function installProductionLikeTagBridge(window, valuesByMappingId) {
  window.tf100webScadaBuilder = {
    getTagValue(tagId) {
      const mappingId = String(tagId).replace(/^tf100\.mapping\./, '');
      return Object.prototype.hasOwnProperty.call(valuesByMappingId, mappingId)
        ? valuesByMappingId[mappingId]
        : undefined;
    },
    writeTag() { return true; },
  };
}

test('opening the Ventilateur 3 invocation mounts the real exported fragment with its own tag, text and state expression', () => {
  const { manifest, fragmentTemplate } = readFixture();
  const window = loadE2eRuntime();
  const quickWindow = window.ScadaRuntime.QuickWindow;
  quickWindow.loadRegistries(manifest);

  const mountedRoots = {};
  window.ScadaRuntime.HostAdapter = {
    dispatchIntent(envelope) {
      const intent = envelope.intent;
      if (intent.kind !== 'openQuickWindow') return true;
      const root = parseFragment(window.document, fragmentTemplate);
      const mounted = quickWindow.mountInstance(intent.runtimeInstanceId, root);
      mountedRoots[intent.runtimeInstanceId] = { root, mounted };
      return true;
    }
  };

  const invocationFan3 = invocationNamed(manifest, 'Ventilateur 3');
  assert.ok(invocationFan3, 'the fixture must carry an invocation whose #4 literal is "Ventilateur 3"');
  const binding1 = bindingFor(invocationFan3, manifest, '#1');

  const opened = quickWindow.open(invocationFan3.InvocationKey);
  assert.equal(opened.ok, true, JSON.stringify(opened));
  const { root, mounted } = mountedRoots[opened.runtimeInstanceId];
  assert.equal(mounted.ok, true, JSON.stringify(mounted));

  const readElement = findByAttribute(root, 'data-scada-read-tag');
  assert.equal(readElement.getAttribute('data-scada-read-tag'), binding1.TagId);

  const textElement = findByText(root, 'Ventilateur 3');
  assert.ok(textElement, 'the #4 placeholder must be substituted by the "Ventilateur 3" literal');

  const stateElement = findByAttribute(root, 'data-scada-state-config');
  const stateConfig = JSON.parse(stateElement.getAttribute('data-scada-state-config'));
  const binding3 = bindingFor(invocationFan3, manifest, '#3');
  assert.deepEqual(
    stateConfig.states[0].expression.ast.left,
    binding3.ExpressionAst,
    'the #3 tagRef inside the state condition must be substituted by Ventilateur 3\'s own expression AST');

  // Fix round 1: structural AST equality (above) is not enough — it would pass just as well with a
  // raw, uncanonicalized tag NAME baked into the AST, which never resolves to a value in the real
  // host. Prove the substituted tagRef is something a PRODUCTION lookup (keyed by mapping id, never
  // by a human-readable tag name) actually resolves: FAN_03_SPEED = 10 -> {#3} = 10 * 0.1 = 1 ->
  // {#3} == 1 is true, exactly the state that selects "Actif" in the real host.
  installProductionLikeTagBridge(window, { '302': 10 });
  const evaluated = window.ScadaRuntime.ExpressionEvaluator.walk(stateConfig.states[0].expression.ast, {});
  assert.equal(evaluated, true,
    'the #3 expression must evaluate true against a production-shaped, mapping-id-keyed tag bridge');

  quickWindow.close(opened.runtimeInstanceId);
});

test('closing Ventilateur 3 and opening Ventilateur 5 shows only the second sheet\'s values, with nothing left from the first', () => {
  const { manifest, fragmentTemplate } = readFixture();
  const window = loadE2eRuntime();
  const quickWindow = window.ScadaRuntime.QuickWindow;
  quickWindow.loadRegistries(manifest);

  const mountedRoots = {};
  window.ScadaRuntime.HostAdapter = {
    dispatchIntent(envelope) {
      const intent = envelope.intent;
      if (intent.kind !== 'openQuickWindow') return true;
      const root = parseFragment(window.document, fragmentTemplate);
      const mounted = quickWindow.mountInstance(intent.runtimeInstanceId, root);
      mountedRoots[intent.runtimeInstanceId] = { root, mounted };
      return true;
    }
  };

  const invocationFan3 = invocationNamed(manifest, 'Ventilateur 3');
  const invocationFan5 = invocationNamed(manifest, 'Ventilateur 5');
  assert.ok(invocationFan5, 'the fixture must carry an invocation whose #4 literal is "Ventilateur 5"');
  assert.notEqual(invocationFan3.InvocationKey, invocationFan5.InvocationKey);

  // DEC-0050 SinglePerDefinition: the two invocations of the same definition are opened one after the
  // other, never concurrently — the first is closed before the second opens.
  const openedFan3 = quickWindow.open(invocationFan3.InvocationKey);
  assert.equal(openedFan3.ok, true);
  const fan3 = mountedRoots[openedFan3.runtimeInstanceId];
  assert.equal(fan3.mounted.ok, true);
  quickWindow.close(openedFan3.runtimeInstanceId);

  const openedFan5 = quickWindow.open(invocationFan5.InvocationKey);
  assert.equal(openedFan5.ok, true);
  const fan5 = mountedRoots[openedFan5.runtimeInstanceId];
  assert.equal(fan5.mounted.ok, true, JSON.stringify(fan5.mounted));

  const binding1Fan5 = bindingFor(invocationFan5, manifest, '#1');
  const readElementFan5 = findByAttribute(fan5.root, 'data-scada-read-tag');
  assert.equal(readElementFan5.getAttribute('data-scada-read-tag'), binding1Fan5.TagId);
  assert.notEqual(readElementFan5.getAttribute('data-scada-read-tag'), bindingFor(invocationFan3, manifest, '#1').TagId);

  const textElementFan5 = findByText(fan5.root, 'Ventilateur 5');
  assert.ok(textElementFan5, 'the second instance must show the "Ventilateur 5" literal');
  assert.equal(findByText(fan5.root, 'Ventilateur 3'), null, 'nothing from the first (closed) instance leaks into the second');

  const stateElementFan5 = findByAttribute(fan5.root, 'data-scada-state-config');
  const stateConfigFan5 = JSON.parse(stateElementFan5.getAttribute('data-scada-state-config'));
  const binding3Fan5 = bindingFor(invocationFan5, manifest, '#3');
  const binding3Fan3 = bindingFor(invocationFan3, manifest, '#3');
  assert.deepEqual(stateConfigFan5.states[0].expression.ast.left, binding3Fan5.ExpressionAst);
  assert.notDeepEqual(stateConfigFan5.states[0].expression.ast.left, binding3Fan3.ExpressionAst,
    'the mounted second instance must carry Ventilateur 5\'s own expression, not a leftover of the first');

  // Fix round 1: same production-shaped, mapping-id-keyed proof as the first test, for the second
  // sheet's own mapping (tf100.mapping.502 = FAN_05_SPEED) — and FAN_03_SPEED's own mapping id is
  // deliberately left unseeded, so a leftover reference to the first sheet's tag would resolve to
  // `undefined` (an unavailable tag skips the state, never a false positive true/false).
  installProductionLikeTagBridge(window, { '502': 10 });
  const evaluatedFan5 = window.ScadaRuntime.ExpressionEvaluator.walk(stateConfigFan5.states[0].expression.ast, {});
  assert.equal(evaluatedFan5, true,
    'Ventilateur 5\'s #3 expression must evaluate true against its own mapping id (FAN_05_SPEED = 10 -> 10*0.1 == 1)');

  // The first (closed) instance's root itself is untouched by anything done to the second: each mounted
  // instance rewrote only its own DOM tree, exactly like the reuse test in
  // quick-window-placeholder-substitution.test.mjs.
  assert.ok(findByText(fan3.root, 'Ventilateur 3'), 'the first root is not mutated by opening the second');
});

// ── tiny fake-DOM search helpers (the harness's FakeElement has no getElementsBy*/matches) ──────────────

function findByAttribute(root, attributeName) {
  const stack = [root];
  while (stack.length > 0) {
    const node = stack.pop();
    if (node.nodeType === 1 && node.hasAttribute(attributeName)) return node;
    if (node.childNodes) stack.push(...node.childNodes);
  }
  return null;
}

function findByText(root, text) {
  const stack = [root];
  while (stack.length > 0) {
    const node = stack.pop();
    if (node.nodeType === 1 && node.tagName === 'SPAN' && node.textContent.indexOf(text) !== -1) return node;
    if (node.childNodes) stack.push(...node.childNodes);
  }
  return null;
}
