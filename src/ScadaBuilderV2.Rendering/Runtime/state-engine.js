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

  /** Map of element id -> cached state (initPage pre-cache). */
  var _stateCache = {};

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
    if (elementId && _paused[elementId]) {
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

    if (!config || !Array.isArray(config.states)) {
      return;
    }

    var evaluator = _getEvaluator();
    var applier = _getEffectApplier();

    if (!evaluator || !applier) {
      return;
    }

    // Reset any error flag before walking
    evaluator.resetError();

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

      // Collect tags from expression AST, skip state if any tag is null
      var tags = _collectTags(expression.ast);
      var anyNull = false;
      for (var t = 0; t < tags.length; t++) {
        if (tagValues[tags[t]] === null || tagValues[tags[t]] === undefined) {
          anyNull = true;
          break;
        }
      }
      if (anyNull) {
        continue;
      }

      // Evaluate expression
      var result = evaluator.walk(expression.ast, tagValues);

      if (result === true) {
        // Match — apply effect if state changed, cache state id, return
        if (state.effect && _stateCache[elementId] !== state.id) {
          applier.apply(element, state.effect);
          _stateCache[elementId] = state.id;
        }
        return;
      }
    }

    // All states skipped — apply qualityFallback if present
    if (config.qualityFallback && _stateCache[elementId] !== '__quality__') {
      applier.apply(element, config.qualityFallback);
      _stateCache[elementId] = '__quality__';
      return;
    }

    // No match — apply defaultEffect if present
    if (config.defaultEffect && _stateCache[elementId] !== '__default__') {
      applier.apply(element, config.defaultEffect);
      _stateCache[elementId] = '__default__';
    }

    // Check for expression evaluation errors and show/hide the badge
    _showErrorBadge(element, evaluator.hasError());
  }

  // ── initPage ────────────────────────────────────────────────────────────

  /**
   * Resets caches and scans the container for elements with data-scada-state-config.
   * Does NOT evaluate — the host calls evaluate() per element after initPage.
   *
   * @param {Element} container  - The DOM container to scan (e.g. page root).
   * @param {string}  pageId     - Unique page identifier (for namespacing).
   */
  function initPage(container, pageId) {
    // Reset paused state and state cache
    _paused = {};
    _stateCache = {};

    if (!container) {
      return;
    }

    // Pre-cache all elements with data-scada-state-config
    var elements = container.querySelectorAll('[data-scada-state-config]');
    for (var i = 0; i < elements.length; i++) {
      _stateCache[elements[i].getAttribute('data-scada-element-id') || elements[i].id] = null;
    }
  }

  // ── pause / resume ──────────────────────────────────────────────────────

  /**
   * Marks an element as paused (edit lock). The engine will skip evaluate()
   * for paused elements.
   *
   * @param {string} id  - Element id.
   */
  function pauseElement(id) {
    if (id) {
      _paused[id] = true;
    }
  }

  /**
   * Unmarks an element as paused, allowing evaluate() to process it again.
   *
   * @param {string} id  - Element id.
   */
  function resumeElement(id) {
    if (id) {
      delete _paused[id];
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
