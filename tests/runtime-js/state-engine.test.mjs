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

test('evaluate() updates a button semantic label and color filter from the confirmed boolean tag', () => {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js', 'effect-applier.js', 'state-engine.js']);
  let confirmedValue = true;
  window.tf100webScadaBuilder = { getTagValue: () => confirmedValue };

  const makeClassList = () => ({
    _classes: new Set(),
    length: 0,
    add(value) { this._classes.add(value); },
    remove(value) { this._classes.delete(value); },
    contains(value) { return this._classes.has(value); },
  });
  window.document = {
    createElement() {
      return {
        style: {}, dataset: {}, classList: makeClassList(),
        setAttribute(name, value) {
          if (name === 'data-scada-color-filter-overlay') this.dataset.scadaColorFilterOverlay = value;
        },
      };
    },
  };

  const config = {
    qualityFallback: {},
    defaultEffect: {},
    states: [
      {
        id: 'active', name: 'Actif', enabled: true,
        expression: { ast: { type: 'binary', op: 'Equal', left: { type: 'tagRef', tagName: 'tf100.mapping.614' }, right: { type: 'literalBool', value: true } } },
        effect: { colorFilterColor: '#12B729', colorFilterOpacity: 0.70, textContent: 'ACTIF' },
      },
      {
        id: 'stopped', name: 'Arrete', enabled: true,
        expression: { ast: { type: 'binary', op: 'Equal', left: { type: 'tagRef', tagName: 'tf100.mapping.614' }, right: { type: 'literalBool', value: false } } },
        effect: { colorFilterColor: '#E53935', colorFilterOpacity: 0.70, textContent: 'ARRÊTÉ' },
      },
    ],
  };

  const label = { textContent: 'ON/OFF' };
  const children = [];
  const element = {
    id: 'defrost-toggle',
    style: {},
    classList: makeClassList(),
    getAttribute(name) { return name === 'data-scada-state-config' ? JSON.stringify(config) : null; },
    querySelector(selector) {
      if (selector === '[data-scada-text]') return label;
      if (selector === '[data-scada-color-filter-overlay]') return children.find((node) => node.dataset.scadaColorFilterOverlay) || null;
      if (selector === '.scada-error-badge') return null;
      return null;
    },
    querySelectorAll(selector) { return selector === '[data-scada-text]' ? [label] : []; },
    appendChild(node) { children.push(node); return node; },
    removeChild(node) { children.splice(children.indexOf(node), 1); },
  };

  window.ScadaRuntime.StateEngine.evaluate(element, {});
  assert.equal(label.textContent, 'ACTIF');
  assert.equal(children[0].style.backgroundColor, '#12B729');
  assert.equal(children[0].style.opacity, 0.70);

  confirmedValue = false;
  window.ScadaRuntime.StateEngine.evaluate(element, {});
  assert.equal(label.textContent, 'ARRÊTÉ');
  assert.equal(children[0].style.backgroundColor, '#E53935');
  assert.equal(children[0].style.opacity, 0.70);
});

test('initPage only resets pause/cache state for elements within its own container, not the whole page', () => {
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
        id: 's1',
        name: 'Alarme',
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
  window.ScadaRuntime.EffectApplier.apply = (element, effect) => applied.push({ id: element.id, effect });

  const headerElement = makeFakeElement('header_el1', JSON.stringify(stateConfig));
  const headerContainer = { querySelectorAll: (sel) => (sel === '[data-scada-state-config]' ? [headerElement] : []) };
  const bodyContainer = { querySelectorAll: () => [] };

  // Header initializes once; its bound input gets locked for editing (pauseElement).
  window.ScadaRuntime.StateEngine.initPage(headerContainer, 'hdr01');
  window.ScadaRuntime.StateEngine.pauseElement('header_el1');

  // A body-only navigation re-initializes an unrelated container.
  window.ScadaRuntime.StateEngine.initPage(bodyContainer, 'win00009');

  // The header's element must still be paused: initPage on a different container
  // must not resume elements it doesn't own.
  window.ScadaRuntime.StateEngine.evaluate(headerElement, { '42': 95 });

  assert.equal(applied.length, 0,
    'header_el1 was edit-locked (paused) and must stay paused after an unrelated container re-initializes');
});

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
