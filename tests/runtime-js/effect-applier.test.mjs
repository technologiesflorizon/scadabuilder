import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

function makeClassList(initial = []) {
  const values = [...initial];
  const classList = {
    add(value) {
      if (!values.includes(value)) values.push(value);
      sync();
    },
    remove(value) {
      const index = values.indexOf(value);
      if (index >= 0) values.splice(index, 1);
      sync();
    },
    contains(value) { return values.includes(value); },
  };
  function sync() {
    for (const key of Object.keys(classList)) {
      if (/^\d+$/.test(key)) delete classList[key];
    }
    values.forEach((value, index) => { classList[index] = value; });
    classList.length = values.length;
  }
  sync();
  return classList;
}

function makeFakeOverlayNode() {
  return {
    style: {},
    classList: makeClassList(),
    dataset: {},
    setAttribute(name, value) { this.dataset[name.replace('data-', '')] = value; },
    getAttribute(name) { return this.dataset[name.replace('data-', '')]; },
  };
}

function makeFakeElement() {
  const style = {};
  const children = [];
  const contentLayer = { style: {} };
  return {
    style,
    classList: makeClassList(),
    hidden: false,
    _textTarget: { textContent: '' },
    _contentLayer: contentLayer,
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
      if (selector.startsWith('button, svg')) return this._contentLayer;
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
  assert.equal(overlay.style.zIndex, '0');
  assert.equal(element._contentLayer.style.zIndex, '1');
  assert.equal(element.style.isolation, 'isolate');
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

test('apply() restores fallback opacity and border before a confirmed state', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  window.document = { createElement: () => makeFakeOverlayNode() };

  const element = makeFakeElement();
  element.style.opacity = '1';
  element.style.borderColor = '#49A9B8';
  element.style.borderWidth = '1px';

  window.ScadaRuntime.EffectApplier.apply(element, {
    opacity: 0.4,
    borderColor: '#000000',
    borderWidth: 2,
  });
  assert.equal(element.style.opacity, 0.4);
  assert.equal(element.style.borderColor, '#000000');

  window.ScadaRuntime.EffectApplier.apply(element, {
    colorFilterColor: '#12B729',
    colorFilterOpacity: 0.7,
    textContent: 'ACTIF',
  });

  assert.equal(element.style.opacity, '1');
  assert.equal(element.style.borderColor, '#49A9B8');
  assert.equal(element.style.borderWidth, '1px');
  assert.equal(element._textTarget.textContent, 'ACTIF');
});

test('apply() covers every effect field and reset() restores the complete baseline', () => {
  const window = loadRuntime(['tag-bridge.js', 'animation-controller.js', 'effect-applier.js']);
  window.document = { createElement: () => makeFakeOverlayNode() };
  window.tf100webScadaBuilder = { getTagValue: (tagId) => tagId === 'value' ? 42 : null };

  const element = makeFakeElement();
  element.style.backgroundColor = '#010203';
  element.style.borderColor = '#111213';
  element.style.borderWidth = '1px';
  element.style.color = '#212223';
  element.style.opacity = '0.9';
  element.style.transform = 'scale(1.1)';
  element.style.position = 'absolute';
  element.style.isolation = 'auto';
  element.hidden = false;
  element._textTarget.textContent = 'BASE';
  element._textTarget.hidden = false;
  element._contentLayer.style.position = 'static';
  element._contentLayer.style.zIndex = '4';

  window.ScadaRuntime.EffectApplier.apply(element, {
    backgroundColor: '#AABBCC', borderColor: '#BBCCDD', borderWidth: 3,
    textColor: '#FFFFFF', textContent: 'Value {value}', textVisible: false,
    elementVisible: false, opacity: 0.4, rotation: 15, animation: 'Blink',
    colorFilterColor: '#00AA55', colorFilterOpacity: 0.7,
    colorFilterHalo: true, colorFilterHaloColor: '#00FF88',
  });

  assert.equal(element.style.backgroundColor, '#AABBCC');
  assert.equal(element.style.borderColor, '#BBCCDD');
  assert.equal(element.style.borderWidth, '3px');
  assert.equal(element.style.color, '#FFFFFF');
  assert.equal(element._textTarget.textContent, 'Value 42');
  assert.equal(element._textTarget.hidden, true);
  assert.equal(element.hidden, true);
  assert.equal(element.style.opacity, 0.4);
  assert.equal(element.style.transform, 'scale(1.1) rotate(15deg)');
  assert.ok(element.classList.contains('scada-anim-blink'));
  assert.ok(element.querySelector('[data-scada-color-filter-overlay]').classList.contains('scada-anim-halo'));

  window.ScadaRuntime.EffectApplier.reset(element);

  assert.equal(element.style.backgroundColor, '#010203');
  assert.equal(element.style.borderColor, '#111213');
  assert.equal(element.style.borderWidth, '1px');
  assert.equal(element.style.color, '#212223');
  assert.equal(element._textTarget.textContent, 'BASE');
  assert.equal(element._textTarget.hidden, false);
  assert.equal(element.hidden, false);
  assert.equal(element.style.opacity, '0.9');
  assert.equal(element.style.transform, 'scale(1.1)');
  assert.equal(element.style.position, 'absolute');
  assert.equal(element.style.isolation, 'auto');
  assert.equal(element._contentLayer.style.position, 'static');
  assert.equal(element._contentLayer.style.zIndex, '4');
  assert.equal(element.classList.contains('scada-anim-blink'), false);
  assert.equal(element.querySelector('[data-scada-color-filter-overlay]'), null);
});

test('apply() transitions between all animations without accumulating classes', () => {
  const window = loadRuntime(['animation-controller.js', 'effect-applier.js']);
  const element = makeFakeElement();

  for (const animation of ['Blink', 'Pulse', 'Halo', 'Spin']) {
    window.ScadaRuntime.EffectApplier.apply(element, { animation });
    const expected = `scada-anim-${animation.toLowerCase()}`;
    assert.ok(element.classList.contains(expected));
    for (const other of ['blink', 'pulse', 'halo', 'spin'].filter((name) => name !== animation.toLowerCase())) {
      assert.equal(element.classList.contains(`scada-anim-${other}`), false);
    }
  }

  window.ScadaRuntime.EffectApplier.apply(element, { animation: 'None' });
  assert.equal(['blink', 'pulse', 'halo', 'spin'].some((name) => element.classList.contains(`scada-anim-${name}`)), false);
});

test('apply() restores prior text and visibility when the next effect omits them', () => {
  const window = loadRuntime(['effect-applier.js']);
  const element = makeFakeElement();
  element._textTarget.textContent = 'BASE';
  element._textTarget.hidden = false;
  element.hidden = false;

  window.ScadaRuntime.EffectApplier.apply(element, {
    textContent: 'ALARM', textVisible: false, elementVisible: false,
  });
  window.ScadaRuntime.EffectApplier.apply(element, { backgroundColor: '#123456' });

  assert.equal(element._textTarget.textContent, 'BASE');
  assert.equal(element._textTarget.hidden, false);
  assert.equal(element.hidden, false);
});
