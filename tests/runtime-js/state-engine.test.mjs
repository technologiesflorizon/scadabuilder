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

  // Effect objects are JSON.parse()'d inside the vm sandbox (a separate JS realm), so
  // their prototype differs from this file's Object.prototype — compare fields directly
  // instead of a realm-sensitive deep-equality check.
  assert.equal(applied.length, 1,
    'expected exactly one effect application (the matching state), not QualityFallback');
  assert.equal(applied[0].backgroundColor, '#E53935',
    'expected the matching state effect, not QualityFallback ({ opacity: 0.4 })');
});

test('evaluate() applies readVariable text independently of which state matches', () => {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js', 'effect-applier.js', 'state-engine.js']);

  window.tf100webScadaBuilder = {
    getTagValue(tagId) {
      const mappingId = String(tagId).replace(/^tf100\.mapping\./, '');
      return { '42': '95' }[mappingId] ?? null;
    },
  };

  const stateConfig = {
    qualityFallback: { opacity: 0.4 },
    defaultEffect: {},
    readVariable: { tagId: 'tf100.mapping.42', displayFormat: 'Debit: {valeur} L/min' },
    states: [
      {
        id: 's1', name: 'Alarme', enabled: true,
        expression: { ast: { type: 'literalBool', value: true } },
        effect: { backgroundColor: '#E53935' }, // no textContent — must not block readVariable's text
      },
    ],
  };

  const applied = [];
  window.ScadaRuntime.EffectApplier.apply = (el, effect) => applied.push(effect);

  const textTarget = { textContent: '' };
  const element = {
    id: 'el1',
    _attrs: { 'data-scada-state-config': JSON.stringify(stateConfig) },
    getAttribute(name) { return Object.prototype.hasOwnProperty.call(this._attrs, name) ? this._attrs[name] : null; },
    querySelector(selector) { return selector === '[data-scada-text]' ? textTarget : null; },
  };

  window.ScadaRuntime.StateEngine.evaluate(element, {});

  assert.equal(textTarget.textContent, 'Debit: 95 L/min', 'readVariable must set the text independently of EffectApplier.apply being stubbed out');
  assert.equal(applied.length, 1, 'the matching state effect must still apply (background color)');
  assert.equal(applied[0].backgroundColor, '#E53935');
});

test('evaluate() lets a matched state\'s own textContent override readVariable for that cycle', () => {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js', 'effect-applier.js', 'state-engine.js']);

  window.tf100webScadaBuilder = { getTagValue: (tagId) => ({ '42': '95' }[tagId.replace(/^tf100\.mapping\./, '')] ?? null) };

  const stateConfig = {
    qualityFallback: {},
    defaultEffect: {},
    readVariable: { tagId: 'tf100.mapping.42' },
    states: [
      {
        id: 's1', name: 'Erreur', enabled: true,
        expression: { ast: { type: 'literalBool', value: true } },
        effect: { textContent: '---' },
      },
    ],
  };

  const textTarget = { textContent: '' };
  const element = {
    id: 'el1',
    _attrs: { 'data-scada-state-config': JSON.stringify(stateConfig) },
    getAttribute(name) { return Object.prototype.hasOwnProperty.call(this._attrs, name) ? this._attrs[name] : null; },
    querySelector(selector) { return selector === '[data-scada-text]' ? textTarget : null; },
  };

  window.ScadaRuntime.StateEngine.evaluate(element, {});

  assert.equal(textTarget.textContent, '---', "the matched state's explicit textContent must win over readVariable");
});
