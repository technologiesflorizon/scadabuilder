(function () {
  'use strict';

  /**
   * Effect applier for the SCADA Builder V2 runtime.
   * Applies visual effects (state-driven style changes) to a DOM element.
   *
   * All effect properties are optional (null/undefined = skip).
   */

  /**
   * Resolves {TagId} tokens in a text template using TagBridge. Unresolved tokens
   * (tag missing or value null) become "---", consistent with the state-engine error badge.
   *
   * @param {string} template - Text containing zero or more {TagId} tokens.
   * @returns {string}
   */
  function resolveTagTokens(template) {
    var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
    if (!bridge) {
      return template;
    }
    return template.replace(/\{([^}]+)\}/g, function (match, tagId) {
      var value = bridge.getTagValue(tagId);
      return value === null || value === undefined ? '---' : String(value);
    });
  }

  var _baselines = new WeakMap();
  var _previousEffects = new WeakMap();

  var VISUAL_BASE_SELECTOR = 'svg, canvas, img, table';
  var SEMANTIC_FOREGROUND_SELECTOR = 'button, input, textarea, select, [data-scada-text]';
  var BACKGROUND_EFFECT_TARGET_SELECTOR = '[data-scada-effect-background-target]';
  var BORDER_EFFECT_TARGET_SELECTOR = '[data-scada-effect-border-target]';

  function _styleValue(style, name) {
    return style && style[name] != null ? style[name] : '';
  }

  function _queryLayers(element, selector) {
    if (!element) {
      return [];
    }
    if (typeof element.querySelectorAll === 'function') {
      return Array.prototype.slice.call(element.querySelectorAll(selector));
    }
    if (typeof element.querySelector === 'function') {
      var match = element.querySelector(selector);
      return match ? [match] : [];
    }
    return [];
  }

  function _captureLayerStyles(element) {
    var layers = _queryLayers(element, VISUAL_BASE_SELECTOR)
      .concat(_queryLayers(element, SEMANTIC_FOREGROUND_SELECTOR));
    var uniqueLayers = [];
    for (var i = 0; i < layers.length; i++) {
      if (layers[i] && uniqueLayers.indexOf(layers[i]) < 0) {
        uniqueLayers.push(layers[i]);
      }
    }
    return uniqueLayers.map(function (layer) {
      return {
        layer: layer,
        position: _styleValue(layer.style, 'position'),
        zIndex: _styleValue(layer.style, 'zIndex')
      };
    });
  }

  function _captureEffectTargetStyles(element) {
    var backgroundTargets = _queryLayers(element, BACKGROUND_EFFECT_TARGET_SELECTOR);
    var borderTargets = _queryLayers(element, BORDER_EFFECT_TARGET_SELECTOR);
    var snapshots = [];
    var targets = backgroundTargets.concat(borderTargets);
    for (var i = 0; i < targets.length; i++) {
      var target = targets[i];
      if (!target || !target.style || snapshots.some(function (item) { return item.target === target; })) {
        continue;
      }
      snapshots.push({
        target: target,
        fill: _styleValue(target.style, 'fill'),
        stroke: _styleValue(target.style, 'stroke'),
        strokeWidth: _styleValue(target.style, 'strokeWidth')
      });
    }
    return snapshots;
  }

  function _restoreEffectTargetStyles(targetStyles, property) {
    for (var i = 0; i < targetStyles.length; i++) {
      var snapshot = targetStyles[i];
      if (snapshot.target && snapshot.target.style) {
        snapshot.target.style[property] = snapshot[property];
      }
    }
  }

  function _restoreLayerStyles(layerStyles) {
    for (var i = 0; i < layerStyles.length; i++) {
      var snapshot = layerStyles[i];
      if (!snapshot.layer || !snapshot.layer.style) {
        continue;
      }
      snapshot.layer.style.position = snapshot.position;
      snapshot.layer.style.zIndex = snapshot.zIndex;
    }
  }

  function _containsSemanticForeground(layer) {
    return !!(layer && typeof layer.querySelector === 'function' &&
      layer.querySelector(SEMANTIC_FOREGROUND_SELECTOR));
  }

  function _baselineFor(element) {
    var baseline = _baselines.get(element);
    if (baseline) {
      return baseline;
    }
    var textTarget = element.querySelector('[data-scada-text]');
    baseline = {
      backgroundColor: _styleValue(element.style, 'backgroundColor'),
      borderColor: _styleValue(element.style, 'borderColor'),
      borderWidth: _styleValue(element.style, 'borderWidth'),
      color: _styleValue(element.style, 'color'),
      opacity: _styleValue(element.style, 'opacity'),
      transform: _styleValue(element.style, 'transform'),
      position: _styleValue(element.style, 'position'),
      isolation: _styleValue(element.style, 'isolation'),
      hidden: !!element.hidden,
      textHidden: textTarget ? !!textTarget.hidden : false,
      textContent: textTarget ? textTarget.textContent : '',
      layerStyles: _captureLayerStyles(element),
      effectTargetStyles: _captureEffectTargetStyles(element)
    };
    _baselines.set(element, baseline);
    return baseline;
  }

  function _removeAnimationClasses(element) {
    var classList = element.classList;
    var animClasses = [];
    for (var i = 0; i < classList.length; i++) {
      if (classList[i].indexOf('scada-anim-') === 0) {
        animClasses.push(classList[i]);
      }
    }
    for (var j = 0; j < animClasses.length; j++) {
      classList.remove(animClasses[j]);
    }
  }

  // State effects are transitions, not cumulative style patches. Restore only
  // the properties controlled by the previous effect before applying the next.
  function _restorePreviousEffect(element) {
    var previous = _previousEffects.get(element);
    if (!previous) {
      return;
    }
    var baseline = _baselineFor(element);
    if (previous.backgroundColor != null) {
      var backgroundTargets = _queryLayers(element, BACKGROUND_EFFECT_TARGET_SELECTOR);
      if (backgroundTargets.length) _restoreEffectTargetStyles(baseline.effectTargetStyles || [], 'fill');
      else element.style.backgroundColor = baseline.backgroundColor;
    }
    if (previous.borderColor != null) {
      var borderColorTargets = _queryLayers(element, BORDER_EFFECT_TARGET_SELECTOR);
      if (borderColorTargets.length) _restoreEffectTargetStyles(baseline.effectTargetStyles || [], 'stroke');
      else element.style.borderColor = baseline.borderColor;
    }
    if (previous.borderWidth != null) {
      var borderWidthTargets = _queryLayers(element, BORDER_EFFECT_TARGET_SELECTOR);
      if (borderWidthTargets.length) _restoreEffectTargetStyles(baseline.effectTargetStyles || [], 'strokeWidth');
      else element.style.borderWidth = baseline.borderWidth;
    }
    if (previous.textColor != null) element.style.color = baseline.color;
    if (previous.opacity != null) element.style.opacity = baseline.opacity;
    if (previous.rotation != null) element.style.transform = baseline.transform;
    if (previous.elementVisible != null) element.hidden = baseline.hidden;
    if (previous.textVisible != null) {
      var textTarget = element.querySelector('[data-scada-text]');
      if (textTarget) textTarget.hidden = baseline.textHidden;
    }
    if (previous.textContent != null) {
      var textContentTarget = element.querySelector('[data-scada-text]');
      if (textContentTarget) textContentTarget.textContent = baseline.textContent;
    }
    if (previous.animation !== null && previous.animation !== undefined) {
      var animationController = window.ScadaRuntime && window.ScadaRuntime.AnimationController;
      if (animationController) animationController.clearAnimation(element);
      else _removeAnimationClasses(element);
    }
    if (previous.colorFilterColor != null) {
      var overlay = element.querySelector('[data-scada-color-filter-overlay]');
      if (overlay && overlay.parentNode === element) element.removeChild(overlay);
      else if (overlay) element.removeChild(overlay);
      element.style.position = baseline.position;
      element.style.isolation = baseline.isolation;
      _restoreLayerStyles(baseline.layerStyles || []);
    }
  }

  function _placeColorFilterLayer(element, overlay) {
    // Keep the element wrapper as the isolated stacking owner so this runtime-only
    // effect never changes the authored order between sibling scene objects.
    // Opaque SVG/image/canvas/table geometry stays below the tint, while semantic
    // text and interactive controls remain above it and fully usable.
    overlay.style.zIndex = '1';
    overlay.style.borderRadius = 'inherit';
    element.style.isolation = 'isolate';
    if (!element.style.position) {
      element.style.position = 'relative';
    }

    var visualLayers = _queryLayers(element, VISUAL_BASE_SELECTOR);
    for (var i = 0; i < visualLayers.length; i++) {
      if (!visualLayers[i].style.position) visualLayers[i].style.position = 'relative';
      // A visual container such as a table may own an input. `z-index: 0` would
      // create a nested stacking context and trap that input below the overlay,
      // so keep such containers at the automatic base layer.
      visualLayers[i].style.zIndex = _containsSemanticForeground(visualLayers[i]) ? 'auto' : '0';
    }

    var semanticLayers = _queryLayers(element, SEMANTIC_FOREGROUND_SELECTOR);
    for (var j = 0; j < semanticLayers.length; j++) {
      if (!semanticLayers[j].style.position) semanticLayers[j].style.position = 'relative';
      semanticLayers[j].style.zIndex = '2';
    }
  }

  function apply(element, effect) {
    if (!element || !effect) {
      return;
    }

    _baselineFor(element);
    _restorePreviousEffect(element);

    // ── background color ────────────────────────────────────────────────
    if (effect.backgroundColor != null) {
      var backgroundTargets = _queryLayers(element, BACKGROUND_EFFECT_TARGET_SELECTOR);
      if (backgroundTargets.length) {
        for (var backgroundIndex = 0; backgroundIndex < backgroundTargets.length; backgroundIndex++) {
          backgroundTargets[backgroundIndex].style.fill = effect.backgroundColor;
        }
      } else {
        element.style.backgroundColor = effect.backgroundColor;
      }
    }

    // ── border color ─────────────────────────────────────────────────────
    if (effect.borderColor != null) {
      var borderColorTargets = _queryLayers(element, BORDER_EFFECT_TARGET_SELECTOR);
      if (borderColorTargets.length) {
        for (var borderColorIndex = 0; borderColorIndex < borderColorTargets.length; borderColorIndex++) {
          borderColorTargets[borderColorIndex].style.stroke = effect.borderColor;
        }
      } else {
        element.style.borderColor = effect.borderColor;
      }
    }

    // ── border width ─────────────────────────────────────────────────────
    if (effect.borderWidth != null) {
      var borderWidthTargets = _queryLayers(element, BORDER_EFFECT_TARGET_SELECTOR);
      if (borderWidthTargets.length) {
        for (var borderWidthIndex = 0; borderWidthIndex < borderWidthTargets.length; borderWidthIndex++) {
          borderWidthTargets[borderWidthIndex].style.strokeWidth = effect.borderWidth + 'px';
        }
      } else {
        element.style.borderWidth = effect.borderWidth + 'px';
      }
    }

    // ── text color ───────────────────────────────────────────────────────
    if (effect.textColor != null) {
      element.style.color = effect.textColor;
    }

    // ── text content ─────────────────────────────────────────────────────
    if (effect.textContent != null) {
      var textTarget = element.querySelector('[data-scada-text]');
      if (textTarget) {
        textTarget.textContent = resolveTagTokens(effect.textContent);
      }
    }

    // ── text visible ─────────────────────────────────────────────────────
    if (effect.textVisible != null) {
      var textTarget2 = element.querySelector('[data-scada-text]');
      if (textTarget2) {
        textTarget2.hidden = !effect.textVisible;
      }
    }

    // ── element visible ──────────────────────────────────────────────────
    if (effect.elementVisible != null) {
      element.hidden = !effect.elementVisible;
    }

    // ── opacity ──────────────────────────────────────────────────────────
    if (effect.opacity != null) {
      element.style.opacity = effect.opacity;
    }

    // ── rotation ─────────────────────────────────────────────────────────
    if (effect.rotation != null) {
      element.style.transform = (element.style.transform || '') + ' rotate(' + effect.rotation + 'deg)';
    }

    // ── animation ────────────────────────────────────────────────────────
    if (effect.animation !== null && effect.animation !== undefined) {
      var controller = window.ScadaRuntime && window.ScadaRuntime.AnimationController;
      var animationName = String(effect.animation).toLowerCase();
      if (controller) {
        controller.setAnimation(element, animationName === 'none' ? null : animationName);
      } else {
        _removeAnimationClasses(element);
        if (animationName !== 'none') element.classList.add('scada-anim-' + animationName);
      }
    }

    // ── color filter (translucent overlay, works on any element incl. SVG .sep) ──
    var existingOverlay = element.querySelector('[data-scada-color-filter-overlay]');
    if (effect.colorFilterColor != null) {
      var overlay = existingOverlay;
      if (!overlay) {
        overlay = document.createElement('div');
        overlay.setAttribute('data-scada-color-filter-overlay', '1');
        overlay.style.position = 'absolute';
        overlay.style.inset = '0';
        overlay.style.pointerEvents = 'none';
        element.appendChild(overlay);
      }
      _placeColorFilterLayer(element, overlay);
      overlay.style.backgroundColor = effect.colorFilterColor;
      overlay.style.opacity = effect.colorFilterOpacity != null ? effect.colorFilterOpacity : 1;
      if (effect.colorFilterHalo) {
        overlay.classList.add('scada-anim-halo');
        overlay.style.color = effect.colorFilterHaloColor || effect.colorFilterColor;
      } else {
        overlay.classList.remove('scada-anim-halo');
      }
    } else if (existingOverlay) {
      element.removeChild(existingOverlay);
    }

    _previousEffects.set(element, effect);
  }

  function reset(element) {
    if (!element) {
      return;
    }
    _restorePreviousEffect(element);
    _previousEffects.delete(element);
    _baselines.delete(element);
  }

  // ── public API ──────────────────────────────────────────────────────────

  window.ScadaRuntime = window.ScadaRuntime || {};

  window.ScadaRuntime.EffectApplier = {
    apply: apply,
    reset: reset,
    resolveTagTokens: resolveTagTokens
  };
})();
