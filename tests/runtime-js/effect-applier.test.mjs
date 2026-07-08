import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

function makeFakeElement() {
  const style = {};
  return {
    style,
    classList: { length: 0, add() {}, remove() {}, contains: () => false },
    hidden: false,
    querySelector(selector) {
      if (selector === '[data-scada-text]') return this._textTarget;
      return null;
    },
    _textTarget: { textContent: '' },
  };
}

test('apply() interpolates {TagId} tokens in textContent via TagBridge', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  window.tf100webScadaBuilder = {
    getTagValue(tagId) {
      const mappingId = String(tagId).replace(/^tf100\.mapping\./, '');
      return { '42': '95' }[mappingId] ?? null;
    },
  };

  const element = makeFakeElement();
  window.ScadaRuntime.EffectApplier.apply(element, { textContent: 'Debit: {tf100.mapping.42} L/min' });

  assert.equal(element._textTarget.textContent, 'Debit: 95 L/min');
});

test('apply() replaces an unresolved tag token with "---"', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  window.tf100webScadaBuilder = { getTagValue: () => null };

  const element = makeFakeElement();
  window.ScadaRuntime.EffectApplier.apply(element, { textContent: 'Debit: {tf100.mapping.99}' });

  assert.equal(element._textTarget.textContent, 'Debit: ---');
});

test('apply() leaves plain textContent (no tokens) unchanged', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  const element = makeFakeElement();
  window.ScadaRuntime.EffectApplier.apply(element, { textContent: 'Arret' });
  assert.equal(element._textTarget.textContent, 'Arret');
});
