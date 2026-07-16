(function () {
  'use strict';

  /**
   * State engine for the SCADA Builder V2 runtime.
   * Evaluates state configurations (data-scada-state-config) on DOM elements
   * and applies matching visual effects based on live tag values.
   *
   * Dependencies: window.ScadaRuntime.ExpressionEvaluator (Task 1)
   *               window.ScadaRuntime.EffectApplier (Task 2)
   */

  // ── internal state ──────────────────────────────────────────────────────

  /** Map of element id -> true for paused (edit-locked) elements. */
  var _paused = {};
  var _pausedElements = new WeakSet();

  /** Element-local cache avoids collisions when composed slots reuse source ids. */
  var _stateCache = new WeakMap();

  /** The evaluator reference (resolved lazily). */
  var _evaluator = null;
  var _effectApplier = null;

  function _getEvaluator() {
    if (!_evaluator) {
      _evaluator = window.ScadaRuntime && window.ScadaRuntime.ExpressionEvaluator;
    }
    return _evaluator;
  }

  function _getEffectApplier() {
    if (!_effectApplier) {
      _effectApplier = window.ScadaRuntime && window.ScadaRuntime.EffectApplier;
    }
    return _effectApplier;
  }

  // ── tag collection from expression AST ──────────────────────────────────

  /**
   * Recursively collects all tagName values from an expression AST.
   * Returns an array of tag name strings (may contain duplicates).
   */
  function _collectTags(node) {
    if (!node || typeof node !== 'object') {
      return [];
    }

    var tags = [];

    if (node.type === 'tagRef' && node.tagName) {
      tags.push(node.tagName);
    }

    if (node.type === 'unary' && node.operand) {
      tags = tags.concat(_collectTags(node.operand));
    }

    if (node.type === 'binary') {
      if (node.left) {
        tags = tags.concat(_collectTags(node.left));
      }
      if (node.right) {
        tags = tags.concat(_collectTags(node.right));
      }
    }

    if (node.type === 'func' && node.args && Array.isArray(node.args)) {
      for (var i = 0; i < node.args.length; i++) {
        tags = tags.concat(_collectTags(node.args[i]));
      }
    }

    return tags;
  }

  // ── independent read-variable ────────────────────────────────────────────

  /**
   * Writes the live value of readVariable.tagId onto [data-scada-text], independently of the
   * States first-match-wins loop. A matched state's own explicit textContent (applied afterward
   * by the normal loop) overrides this for that cycle.
   *
   * @param {Element} element      - DOM element with data-scada-state-config.
   * @param {object}  readVariable - { tagId, displayFormat? } or undefined/null if not configured.
   * @param {object}  tagValues    - unused directly (resolution goes through TagBridge).
   */
  function _applyReadVariable(element, readVariable, tagValues) {
    if (!readVariable || !readVariable.tagId) {
      return;
    }

    var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
    var value = bridge ? bridge.getTagValue(readVariable.tagId) : tagValues[readVariable.tagId];
    var text = value === null || value === undefined ? '---' : String(value);

    var format = readVariable.displayFormat;
    var resolved = format && format.indexOf('{valeur}') !== -1
      ? format.replace(/\{valeur\}/g, text)
      : text;

    var textTarget = element.querySelector('[data-scada-text]');
    if (textTarget) {
      textTarget.textContent = resolved;
    }
  }

  function _effectTokenSignature(effect) {
    if (!effect || typeof effect.textContent !== 'string' || effect.textContent.indexOf('{') === -1) {
      return '';
    }
    var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
    return effect.textContent.replace(/\{([^}]+)\}/g, function (match, tagId) {
      var value = bridge ? bridge.getTagValue(tagId) : null;
      return tagId + '=' + (value === null || value === undefined ? '<missing>' : String(value));
    });
  }

  function _applySelectedEffect(element, cacheKey, effect, readVariable, tagValues) {
    var applier = _getEffectApplier();
    var selectedEffect = effect || {};
    var signature = cacheKey + '|' + _effectTokenSignature(selectedEffect);
    if (_stateCache.get(element) !== signature) {
      applier.apply(element, selectedEffect);
      _stateCache.set(element, signature);
    }
    if (selectedEffect.textContent == null) {
      _applyReadVariable(element, readVariable, tagValues);
    }
  }

  // ── error badge ───────────────────────────────────────────────────────────

  /**
   * Shows or hides the expression-evaluation error badge on an element.
   * When an error is active, a red "!" badge is appended and every
   * [data-scada-text] child is forced to "---". The badge is removed and
   * text is restored (by the next successful evaluation) when the error clears.
   *
   * @param {Element} element - DOM element to badge.
   * @param {boolean} show    - true to show the badge, false to remove it.
   */
  function _showErrorBadge(element, show) {
    if (!element) {
      return;
    }

    var badge = element.querySelector('.scada-error-badge');

    if (show) {
      if (!badge) {
        badge = document.createElement('span');
        badge.className = 'scada-error-badge';
        badge.textContent = '!';
        badge.title = "Erreur d'évaluation d'expression";
        badge.style.cssText =
          'position:absolute;top:2px;right:2px;background:#E53935;color:#fff;border-radius:50%;width:16px;height:16px;font-size:10px;display:flex;align-items:center;justify-content:center;z-index:5';
        element.appendChild(badge);
      }
      // Force "---" on any data-scada-text elements
      var textEls = element.querySelectorAll('[data-scada-text]');
      for (var i = 0; i < textEls.length; i++) {
        textEls[i].textContent = '---';
      }
    } else {
      if (badge) {
        badge.remove();
      }
      // Text is restored by the next successful evaluation applying the effect
    }
  }

  // ── evaluate ────────────────────────────────────────────────────────────

  /**
   * Evaluates the state configuration on an element against tag values.
   *
   * @param {Element} element     - DOM element with data-scada-state-config.
   * @param {object}  tagValues   - Map of tagName -> value.
   */
  function evaluate(element, tagValues) {
    if (!element) {
      return;
    }

    var elementId = element.id;

    // Skip if paused (edit lock)
    if (_pausedElements.has(element) || (elementId && _paused[elementId])) {
      return;
    }

    var configRaw = element.getAttribute('data-scada-state-config');
    if (!configRaw) {
      return;
    }

    var config;
    try {
      config = JSON.parse(configRaw);
    } catch (e) {
      // Invalid JSON — silently skip
      return;
    }

    if (!config) {
      return;
    }

    if (!Array.isArray(config.states)) {
      _applyReadVariable(element, config.readVariable, tagValues);
      return;
    }

    var evaluator = _getEvaluator();
    var applier = _getEffectApplier();

    if (!evaluator || !applier) {
      return;
    }

    var evaluableStateCount = 0;
    var hasEvaluationError = false;

    // Loop states top-to-bottom (first-match-wins)
    for (var i = 0; i < config.states.length; i++) {
      var state = config.states[i];

      // Skip disabled states
      if (state.enabled === false) {
        continue;
      }

      var expression = state.expression;
      if (!expression || !expression.ast) {
        continue;
      }

      // Collect tags from expression AST, skip state if any tag is null.
      // Resolve through TagBridge (same as ExpressionEvaluator.walk) so the null-check
      // and the actual evaluation agree on where a tag's value comes from.
      var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
      var tags = _collectTags(expression.ast);
      var anyNull = false;
      for (var t = 0; t < tags.length; t++) {
        var tv = bridge ? bridge.getTagValue(tags[t]) : tagValues[tags[t]];
        if (tv === null || tv === undefined) {
          anyNull = true;
          break;
        }
      }
      if (anyNull) {
        continue;
      }

      // Evaluate expression
      evaluableStateCount++;
      evaluator.resetError();
      var result = evaluator.walk(expression.ast, tagValues);
      if (evaluator.hasError()) {
        hasEvaluationError = true;
        continue;
      }

      if (result === true) {
        _applySelectedEffect(element, 'state:' + String(state.id || i), state.effect, config.readVariable, tagValues);
        _showErrorBadge(element, hasEvaluationError);
        return;
      }
    }

    // Quality fallback is reserved for a pass where no enabled rule could be evaluated.
    if (evaluableStateCount === 0 && config.qualityFallback) {
      _applySelectedEffect(element, 'quality', config.qualityFallback, config.readVariable, tagValues);
      _showErrorBadge(element, false);
      return;
    }

    _applySelectedEffect(element, 'default', config.defaultEffect, config.readVariable, tagValues);
    _showErrorBadge(element, hasEvaluationError);
  }

  // ── initPage ────────────────────────────────────────────────────────────

  /**
   * Scans the container for elements with data-scada-state-config and resets
   * their pause/cache state only — never other containers' elements.
   *
   * Multiple containers (e.g. a composed page's header/body/footer slots) can
   * each call initPage independently without clobbering each other's edit-lock
   * (pauseElement) or state-cache entries. Does NOT evaluate — the host calls
   * evaluate() per element after initPage.
   *
   * @param {Element} container  - The DOM container to scan (e.g. a page root).
   * @param {string}  pageId     - Unique page identifier (for namespacing).
   */
  function initPage(container, pageId) {
    if (!container) {
      return;
    }

    var elements = container.querySelectorAll('[data-scada-state-config]');
    for (var i = 0; i < elements.length; i++) {
      var id = elements[i].getAttribute('data-scada-element-id') || elements[i].id;
      if (!id) {
        continue;
      }
      _pausedElements.delete(elements[i]);
      var applier = _getEffectApplier();
      if (applier && typeof applier.reset === 'function') {
        applier.reset(elements[i]);
      }
      _stateCache.delete(elements[i]);
    }
  }

  // ── pause / resume ──────────────────────────────────────────────────────

  /**
   * Marks an element as paused (edit lock). The engine will skip evaluate()
   * for paused elements.
   *
   * @param {string} id  - Element id.
   */
  function pauseElement(target) {
    if (target && typeof target === 'object') {
      _pausedElements.add(target);
    } else if (target) {
      _paused[target] = true;
    }
  }

  /**
   * Unmarks an element as paused, allowing evaluate() to process it again.
   *
   * @param {string} id  - Element id.
   */
  function resumeElement(target) {
    if (target && typeof target === 'object') {
      _pausedElements.delete(target);
    } else if (target) {
      delete _paused[target];
    }
  }

  // ── public API ──────────────────────────────────────────────────────────

  window.ScadaRuntime = window.ScadaRuntime || {};

  window.ScadaRuntime.StateEngine = {
    evaluate: evaluate,
    initPage: initPage,
    pauseElement: pauseElement,
    resumeElement: resumeElement
  };
})();
