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
