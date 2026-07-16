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

test('evaluate() restores the button baseline after quality fallback resolves', () => {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js', 'effect-applier.js', 'state-engine.js']);
  let confirmedValue = null;
  window.tf100webScadaBuilder = { getTagValue: () => confirmedValue };

  const makeClassList = () => ({
    _classes: new Set(), length: 0,
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
    qualityFallback: { opacity: 0.4, borderColor: '#000000', borderWidth: 2 },
    defaultEffect: {},
    states: [{
      id: 'active', enabled: true,
      expression: { ast: { type: 'binary', op: 'Equal', left: { type: 'tagRef', tagName: 'tf100.mapping.614' }, right: { type: 'literalBool', value: true } } },
      effect: { colorFilterColor: '#12B729', colorFilterOpacity: 0.70, textContent: 'ACTIF' },
    }],
  };

  const label = { textContent: 'ON/OFF', hidden: false };
  const contentLayer = { style: {} };
  const children = [];
  const element = {
    id: 'defrost-toggle-fallback',
    hidden: false,
    style: { opacity: '1', borderColor: '#49A9B8', borderWidth: '1px' },
    classList: makeClassList(),
    getAttribute(name) { return name === 'data-scada-state-config' ? JSON.stringify(config) : null; },
    querySelector(selector) {
      if (selector === '[data-scada-text]') return label;
      if (selector === '[data-scada-color-filter-overlay]') return children.find((node) => node.dataset.scadaColorFilterOverlay) || null;
      if (selector === '.scada-error-badge') return null;
      if (selector.startsWith('button, svg')) return contentLayer;
      return null;
    },
    querySelectorAll(selector) { return selector === '[data-scada-text]' ? [label] : []; },
    appendChild(node) { children.push(node); return node; },
    removeChild(node) { children.splice(children.indexOf(node), 1); },
  };

  window.ScadaRuntime.StateEngine.evaluate(element, {});
  assert.equal(element.style.opacity, 0.4);
  assert.equal(element.style.borderColor, '#000000');

  confirmedValue = true;
  window.ScadaRuntime.StateEngine.evaluate(element, {});
  assert.equal(element.style.opacity, '1');
  assert.equal(element.style.borderColor, '#49A9B8');
  assert.equal(element.style.borderWidth, '1px');
  assert.equal(label.textContent, 'ACTIF');
  assert.equal(children[0].style.zIndex, '0');
  assert.equal(contentLayer.style.zIndex, '1');
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

test('evaluate() is first-match-wins, skips disabled rules, and uses default only for a valid no-match', () => {
  const window = loadRuntime(['expression-evaluator.js', 'effect-applier.js', 'state-engine.js']);
  const applied = [];
  window.ScadaRuntime.EffectApplier.apply = (_element, effect) => applied.push(effect);
  const config = {
    qualityFallback: { textContent: 'QUALITY' },
    defaultEffect: { textContent: 'DEFAULT' },
    states: [
      { id: 'disabled', enabled: false, expression: { ast: { type: 'literalBool', value: true } }, effect: { textContent: 'DISABLED' } },
      { id: 'false', enabled: true, expression: { ast: { type: 'literalBool', value: false } }, effect: { textContent: 'FALSE' } },
      { id: 'first', enabled: true, expression: { ast: { type: 'literalBool', value: true } }, effect: { textContent: 'FIRST' } },
      { id: 'second', enabled: true, expression: { ast: { type: 'literalBool', value: true } }, effect: { textContent: 'SECOND' } },
    ],
  };
  const element = makeFakeElement('first-match', JSON.stringify(config));
  window.ScadaRuntime.StateEngine.evaluate(element, {});
  assert.equal(applied.length, 1);
  assert.equal(applied[0].textContent, 'FIRST');

  config.states = [{ id: 'false', enabled: true, expression: { ast: { type: 'literalBool', value: false } }, effect: {} }];
  element._attrs['data-scada-state-config'] = JSON.stringify(config);
  window.ScadaRuntime.StateEngine.evaluate(element, {});
  assert.equal(applied.at(-1).textContent, 'DEFAULT');
});

test('evaluate() uses quality fallback only when no rule is evaluable and reports evaluation errors on default', () => {
  const window = loadRuntime(['expression-evaluator.js', 'effect-applier.js', 'state-engine.js']);
  const applied = [];
  window.ScadaRuntime.EffectApplier.apply = (_element, effect) => applied.push(effect);
  window.document = { createElement: () => ({ className: '', textContent: '', title: '', style: {}, remove() {} }) };
  const badgeHolder = { badge: null };
  const config = {
    qualityFallback: { textContent: 'QUALITY' },
    defaultEffect: { textContent: 'DEFAULT' },
    states: [{
      id: 'tag-state', enabled: true,
      expression: { ast: { type: 'tagRef', tagName: 'missing' } },
      effect: { textContent: 'MATCH' },
    }],
  };
  const element = {
    id: 'quality-semantics',
    getAttribute: () => JSON.stringify(config),
    querySelector(selector) { return selector === '.scada-error-badge' ? badgeHolder.badge : null; },
    querySelectorAll() { return []; },
    appendChild(node) { badgeHolder.badge = node; node.remove = () => { badgeHolder.badge = null; }; },
  };

  window.ScadaRuntime.StateEngine.evaluate(element, {});
  assert.equal(applied.at(-1).textContent, 'QUALITY');
  assert.equal(badgeHolder.badge, null, 'missing data is a quality fallback, not an expression error badge');

  config.states.push({
    id: 'valid-false', enabled: true,
    expression: { ast: { type: 'literalBool', value: false } }, effect: {},
  });
  window.ScadaRuntime.StateEngine.evaluate(element, {});
  assert.equal(applied.at(-1).textContent, 'DEFAULT', 'one evaluable false rule selects default even if another rule has missing data');
  config.states.pop();

  config.states[0].expression.ast = {
    type: 'binary', op: 'Divide',
    left: { type: 'literalNumber', value: 1 }, right: { type: 'literalNumber', value: 0 },
  };
  window.ScadaRuntime.StateEngine.evaluate(element, {});
  assert.equal(applied.at(-1).textContent, 'DEFAULT');
  assert.ok(badgeHolder.badge, 'invalid arithmetic exposes the expression error badge');

  config.states.push({
    id: 'later-match', enabled: true,
    expression: { ast: { type: 'literalBool', value: true } }, effect: { textContent: 'LATER' },
  });
  window.ScadaRuntime.StateEngine.evaluate(element, {});
  assert.equal(applied.at(-1).textContent, 'LATER');
  assert.ok(badgeHolder.badge, 'an earlier evaluation error remains visible when a later rule matches');
  config.states.pop();

  config.states[0].expression.ast = { type: 'literalBool', value: false };
  window.ScadaRuntime.StateEngine.evaluate(element, {});
  assert.equal(applied.at(-1).textContent, 'DEFAULT');
  assert.equal(badgeHolder.badge, null, 'a later valid cycle clears the prior error badge');
});

test('evaluate() refreshes readVariable and effect tokens while the same state stays selected', () => {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js', 'effect-applier.js', 'state-engine.js']);
  const values = { read: 1, token: 'A' };
  window.tf100webScadaBuilder = { getTagValue: (tagId) => values[tagId] ?? null };
  const applied = [];
  window.ScadaRuntime.EffectApplier.apply = (_element, effect) => applied.push(effect.textContent ?? null);
  const textTarget = { textContent: '' };
  const config = {
    readVariable: { tagId: 'read', displayFormat: 'Read={valeur}' },
    defaultEffect: {}, qualityFallback: {},
    states: [{
      id: 'dynamic', enabled: true,
      expression: { ast: { type: 'literalBool', value: true } },
      effect: { textContent: 'Token={token}' },
    }],
  };
  const element = {
    id: 'dynamic-state',
    getAttribute: () => JSON.stringify(config),
    querySelector: (selector) => selector === '[data-scada-text]' ? textTarget : null,
  };

  window.ScadaRuntime.StateEngine.evaluate(element, {});
  values.token = 'B';
  window.ScadaRuntime.StateEngine.evaluate(element, {});
  assert.equal(applied.length, 2, 'a changed token value must reapply the selected effect');

  config.states[0].effect = {};
  window.ScadaRuntime.StateEngine.evaluate(element, {});
  assert.equal(textTarget.textContent, 'Read=1');
  values.read = 2;
  window.ScadaRuntime.StateEngine.evaluate(element, {});
  assert.equal(textTarget.textContent, 'Read=2', 'readVariable must refresh even when the selected state id is unchanged');
});

test('evaluate() caches by element identity so repeated ids in composed slots stay independent', () => {
  const window = loadRuntime(['expression-evaluator.js', 'effect-applier.js', 'state-engine.js']);
  const applied = [];
  window.ScadaRuntime.EffectApplier.apply = (element) => applied.push(element.slot);
  const config = JSON.stringify({
    defaultEffect: {}, qualityFallback: {},
    states: [{ id: 'on', enabled: true, expression: { ast: { type: 'literalBool', value: true } }, effect: { opacity: 1 } }],
  });
  const first = { ...makeFakeElement('shared-id', config), slot: 'header' };
  const second = { ...makeFakeElement('shared-id', config), slot: 'body' };

  window.ScadaRuntime.StateEngine.evaluate(first, {});
  window.ScadaRuntime.StateEngine.evaluate(second, {});
  assert.deepEqual(applied, ['header', 'body']);
});

test('initPage resets only its elements through EffectApplier before the next hydration', () => {
  const window = loadRuntime(['expression-evaluator.js', 'effect-applier.js', 'state-engine.js']);
  const reset = [];
  window.ScadaRuntime.EffectApplier.reset = (element) => reset.push(element.id);
  const first = makeFakeElement('first', '{}');
  const second = makeFakeElement('second', '{}');
  const container = { querySelectorAll: () => [first, second] };

  window.ScadaRuntime.StateEngine.initPage(container, 'page');
  assert.deepEqual(reset, ['first', 'second']);
});
