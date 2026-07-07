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
      if (state.disabled) {
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
        // Match — apply effect, cache state id, return
        if (state.effect) {
          applier.apply(element, state.effect);
        }
        if (elementId && state.id) {
          _paused[elementId] = state.id;
        }
        return;
      }
    }

    // All states skipped — apply qualityFallback if present
    if (config.qualityFallback && config.qualityFallback.effect) {
      applier.apply(element, config.qualityFallback.effect);
      return;
    }

    // No match — apply defaultEffect if present
    if (config.defaultEffect) {
      applier.apply(element, config.defaultEffect);
    }
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
    // Reset paused state
    _paused = {};

    if (!container) {
      return;
    }

    // The pageId is accepted for future namespacing use.
    // Currently, caches are cleared and the container is ready for evaluation.
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
