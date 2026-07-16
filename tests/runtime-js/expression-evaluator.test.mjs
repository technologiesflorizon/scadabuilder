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

test('walk() covers every serialized literal and unary node', () => {
  const evaluator = loadRuntime(['expression-evaluator.js']).ScadaRuntime.ExpressionEvaluator;
  const cases = [
    [{ type: 'literalNumber', value: 12.5 }, 12.5],
    [{ type: 'literalNumber', value: '8.25' }, 8.25],
    [{ type: 'literalBool', value: true }, true],
    [{ type: 'literalBool', value: false }, false],
    [{ type: 'literalString', value: 'ready' }, 'ready'],
    [{ type: 'unary', op: 'Not', operand: { type: 'literalBool', value: true } }, false],
    [{ type: 'unary', op: 'Negate', operand: { type: 'literalNumber', value: 3 } }, -3],
  ];

  for (const [ast, expected] of cases) {
    evaluator.resetError();
    assert.equal(evaluator.walk(ast, {}), expected);
    assert.equal(evaluator.hasError(), false);
  }
});

test('walk() covers every binary operator with deterministic numeric coercion', () => {
  const evaluator = loadRuntime(['expression-evaluator.js']).ScadaRuntime.ExpressionEvaluator;
  const binary = (op, left, right) => ({
    type: 'binary', op,
    left: { type: typeof left === 'boolean' ? 'literalBool' : typeof left === 'string' ? 'literalString' : 'literalNumber', value: left },
    right: { type: typeof right === 'boolean' ? 'literalBool' : typeof right === 'string' ? 'literalString' : 'literalNumber', value: right },
  });
  const cases = [
    ['Add', '2', 3, 5],
    ['Subtract', 7, 2, 5],
    ['Multiply', 4, 3, 12],
    ['Divide', 9, 3, 3],
    ['Modulo', 9, 4, 1],
    ['Equal', '2', 2, true],
    ['Equal', 'ready', 'ready', true],
    ['NotEqual', 'ready', 'stopped', true],
    ['LessThan', 1, 2, true],
    ['LessThanOrEqual', 2, 2, true],
    ['GreaterThan', 3, 2, true],
    ['GreaterThanOrEqual', 3, 3, true],
    ['And', true, false, false],
    ['Or', false, true, true],
  ];

  for (const [op, left, right, expected] of cases) {
    evaluator.resetError();
    assert.equal(evaluator.walk(binary(op, left, right), {}), expected, op);
    assert.equal(evaluator.hasError(), false, op);
  }
});

test('walk() covers every canonical function and validates arity and BIT index', () => {
  const evaluator = loadRuntime(['expression-evaluator.js']).ScadaRuntime.ExpressionEvaluator;
  const literal = (value) => ({ type: 'literalNumber', value });
  const cases = [
    [{ type: 'func', name: 'ABS', args: [literal(-4)] }, 4],
    [{ type: 'func', name: 'MIN', args: [literal(4), literal(2)] }, 2],
    [{ type: 'func', name: 'MAX', args: [literal(4), literal(2)] }, 4],
    [{ type: 'func', name: 'BIT', args: [literal(4), literal(2)] }, true],
    [{ type: 'func', name: 'bit', args: [literal(4), literal(1)] }, false],
  ];

  for (const [ast, expected] of cases) {
    evaluator.resetError();
    assert.equal(evaluator.walk(ast, {}), expected);
    assert.equal(evaluator.hasError(), false);
  }

  for (const ast of [
    { type: 'func', name: 'ABS', args: [] },
    { type: 'func', name: 'BIT', args: [literal(4), literal(32)] },
    { type: 'func', name: 'UNKNOWN', args: [] },
  ]) {
    evaluator.resetError();
    assert.equal(evaluator.walk(ast, {}), null);
    assert.equal(evaluator.hasError(), true);
    assert.ok(evaluator.getError());
  }
});

test('walk() propagates unavailable tags but preserves boolean short-circuit results', () => {
  const evaluator = loadRuntime(['expression-evaluator.js']).ScadaRuntime.ExpressionEvaluator;
  const missing = { type: 'tagRef', tagName: 'missing' };
  const bool = (value) => ({ type: 'literalBool', value });
  const binary = (op, left, right) => ({ type: 'binary', op, left, right });

  evaluator.resetError();
  assert.equal(evaluator.walk(missing, {}), null);
  assert.equal(evaluator.hasError(), false);
  assert.equal(evaluator.walk(binary('And', bool(false), missing), {}), false);
  assert.equal(evaluator.walk(binary('And', bool(true), missing), {}), null);
  assert.equal(evaluator.walk(binary('Or', bool(true), missing), {}), true);
  assert.equal(evaluator.walk(binary('Or', bool(false), missing), {}), null);
});

test('walk() reports invalid arithmetic, non-finite values, and unknown AST variants', () => {
  const evaluator = loadRuntime(['expression-evaluator.js']).ScadaRuntime.ExpressionEvaluator;
  const number = (value) => ({ type: 'literalNumber', value });
  const cases = [
    { type: 'binary', op: 'Divide', left: number(1), right: number(0) },
    { type: 'binary', op: 'Modulo', left: number(1), right: number(0) },
    { type: 'binary', op: 'Add', left: { type: 'literalString', value: 'not-a-number' }, right: number(1) },
    { type: 'literalNumber', value: 'Infinity' },
    { type: 'unary', op: 'Unknown', operand: number(1) },
    { type: 'binary', op: 'Unknown', left: number(1), right: number(1) },
    { type: 'unknown' },
  ];

  for (const ast of cases) {
    evaluator.resetError();
    assert.equal(evaluator.walk(ast, {}), null);
    assert.equal(evaluator.hasError(), true);
    assert.ok(evaluator.getError());
  }
});
