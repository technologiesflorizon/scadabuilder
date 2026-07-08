import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

function makeFakeOverlayNode() {
  return {
    style: {},
    classList: { _classes: new Set(), add(c) { this._classes.add(c); }, remove(c) { this._classes.delete(c); }, contains(c) { return this._classes.has(c); }, length: 0 },
    dataset: {},
    setAttribute(name, value) { this.dataset[name.replace('data-', '')] = value; },
    getAttribute(name) { return this.dataset[name.replace('data-', '')]; },
  };
}

function makeFakeElement() {
  const style = {};
  const children = [];
  return {
    style,
    classList: { length: 0, add() {}, remove() {}, contains: () => false },
    hidden: false,
    _textTarget: { textContent: '' },
    _children: children,
    appendChild(node) { children.push(node); return node; },
    removeChild(node) {
      const i = children.indexOf(node);
      if (i >= 0) children.splice(i, 1);
    },
    querySelector(selector) {
      if (selector === '[data-scada-text]') return this._textTarget;
      if (selector === '[data-scada-color-filter-overlay]') {
        return children.find((c) => c.dataset && c.dataset['scada-color-filter-overlay']) || null;
      }
      return null;
    },
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

test('apply() creates a translucent overlay for colorFilterColor', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  window.document = { createElement: () => makeFakeOverlayNode() };

  const element = makeFakeElement();
  window.ScadaRuntime.EffectApplier.apply(element, { colorFilterColor: '#E53935', colorFilterOpacity: 0.35 });

  const overlay = element.querySelector('[data-scada-color-filter-overlay]');
  assert.ok(overlay, 'expected an overlay element to be created');
  assert.equal(overlay.style.backgroundColor, '#E53935');
  assert.equal(overlay.style.opacity, 0.35);
});

test('apply() reuses the same overlay node across repeated calls', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  window.document = { createElement: () => makeFakeOverlayNode() };

  const element = makeFakeElement();
  window.ScadaRuntime.EffectApplier.apply(element, { colorFilterColor: '#E53935', colorFilterOpacity: 0.35 });
  window.ScadaRuntime.EffectApplier.apply(element, { colorFilterColor: '#2090A0', colorFilterOpacity: 0.5 });

  assert.equal(element._children.length, 1, 'a second apply() must update the existing overlay, not add another');
  const overlay = element.querySelector('[data-scada-color-filter-overlay]');
  assert.equal(overlay.style.backgroundColor, '#2090A0');
});

test('apply() adds the pulsing halo class with the halo color when colorFilterHalo is true', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  window.document = { createElement: () => makeFakeOverlayNode() };

  const element = makeFakeElement();
  window.ScadaRuntime.EffectApplier.apply(element, {
    colorFilterColor: '#E53935',
    colorFilterOpacity: 0.35,
    colorFilterHalo: true,
    colorFilterHaloColor: '#FFEE00',
  });

  const overlay = element.querySelector('[data-scada-color-filter-overlay]');
  assert.ok(overlay.classList.contains('scada-anim-halo'));
  assert.equal(overlay.style.color, '#FFEE00');
});

test('apply() removes the overlay when colorFilterColor is absent', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  window.document = { createElement: () => makeFakeOverlayNode() };

  const element = makeFakeElement();
  window.ScadaRuntime.EffectApplier.apply(element, { colorFilterColor: '#E53935' });
  window.ScadaRuntime.EffectApplier.apply(element, {});

  assert.equal(element.querySelector('[data-scada-color-filter-overlay]'), null);
});
