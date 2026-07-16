(function () {
  'use strict';

  var TRIGGER_MAP = {
    onclick: 'click',
    click: 'click',
    onrelease: 'pointerup',
    pointerup: 'pointerup',
    mouseup: 'pointerup',
    onhover: 'mouseenter',
    onhoverenter: 'mouseenter',
    mouseenter: 'mouseenter',
    onhoverexit: 'mouseleave',
    mouseleave: 'mouseleave'
  };
  var _pages = new WeakMap();
  var _visibilityBaselines = new WeakMap();

  function _warn(code, detail) {
    if (window.console && typeof window.console.warn === 'function') {
      window.console.warn('SCADA action ' + code, detail || '');
    }
  }

  function _parseAttribute(element, name, fallback) {
    if (!element || typeof element.getAttribute !== 'function') return fallback;
    var raw = element.getAttribute(name);
    if (!raw) return fallback;
    try { return JSON.parse(raw); } catch (error) {
      _warn('invalid-json', { attribute: name, error: String(error) });
      return fallback;
    }
  }

  function _property(value, camelName, pascalName) {
    if (!value) return undefined;
    return value[camelName] !== undefined ? value[camelName] : value[pascalName];
  }

  function _normalizeAction(action) {
    if (!action) return null;
    return {
      id: _property(action, 'id', 'Id'),
      kind: String(_property(action, 'kind', 'Kind') || '').replace(/^./, function (c) { return c.toLowerCase(); }),
      targetPageId: _property(action, 'targetPageId', 'TargetPageId'),
      targetElementId: _property(action, 'targetElementId', 'TargetElementId'),
      tagId: _property(action, 'tagId', 'TagId'),
      value: _property(action, 'value', 'Value'),
      condition: _property(action, 'condition', 'Condition'),
      conditionGroup: _property(action, 'conditionGroup', 'ConditionGroup'),
      popupOptions: _property(action, 'popupOptions', 'PopupOptions')
    };
  }

  function _normalizeBinding(binding) {
    return {
      trigger: _property(binding, 'trigger', 'Trigger'),
      actionId: _property(binding, 'actionId', 'ActionId'),
      stopPropagation: _property(binding, 'stopPropagation', 'StopPropagation') === true,
      preventDefault: _property(binding, 'preventDefault', 'PreventDefault') === true
    };
  }

  function _boolean(value) {
    if (typeof value === 'boolean') return value;
    if (typeof value === 'number') return value !== 0;
    var normalized = String(value).trim().toLowerCase();
    if (normalized === 'true' || normalized === '1' || normalized === 'on' || normalized === 'yes') return true;
    if (normalized === 'false' || normalized === '0' || normalized === 'off' || normalized === 'no') return false;
    return null;
  }

  function _literalNode(actual, raw) {
    if (typeof actual === 'number') return { type: 'literalNumber', value: raw };
    if (typeof actual === 'boolean') {
      var booleanValue = _boolean(raw);
      return { type: 'literalBool', value: booleanValue === true };
    }
    return { type: 'literalString', value: raw };
  }

  function _evaluateCondition(condition) {
    if (!condition) return { missing: false, matched: true };
    var tagId = _property(condition, 'tagId', 'TagId');
    var operator = String(_property(condition, 'operator', 'Operator') || '').toLowerCase();
    var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
    var actual = bridge && typeof bridge.getTagValue === 'function' ? bridge.getTagValue(tagId) : undefined;
    if (actual === undefined || actual === null) return { missing: true, matched: false };
    if (operator === 'true') return { missing: false, matched: actual === true };
    if (operator === 'false') return { missing: false, matched: actual === false };

    var evaluator = window.ScadaRuntime && window.ScadaRuntime.ExpressionEvaluator;
    if (!evaluator || typeof evaluator.walk !== 'function') {
      _warn('expression-evaluator-unavailable', operator);
      return { missing: false, matched: false };
    }
    var binaryOperators = {
      equals: 'Equal',
      notequals: 'NotEqual',
      greaterthan: 'GreaterThan',
      greaterthanorequal: 'GreaterThanOrEqual',
      lessthan: 'LessThan',
      lessthanorequal: 'LessThanOrEqual'
    };
    var binaryOperator = binaryOperators[operator];
    if (!binaryOperator) {
      _warn('condition-operator-unsupported', operator);
      return { missing: false, matched: false };
    }
    if (typeof evaluator.resetError === 'function') evaluator.resetError();
    var matched = evaluator.walk({
      type: 'binary',
      op: binaryOperator,
      left: { type: 'tagRef', tagName: tagId },
      right: _literalNode(actual, _property(condition, 'compareValue', 'CompareValue'))
    }, {});
    if (typeof evaluator.hasError === 'function' && evaluator.hasError()) {
      _warn('condition-evaluation-error', typeof evaluator.getError === 'function' ? evaluator.getError() : operator);
      return { missing: false, matched: false };
    }
    return { missing: false, matched: matched === true };
  }

  function _conditionsAllow(action) {
    var single = _evaluateCondition(action.condition);
    if (single.missing || !single.matched) return false;
    if (!action.conditionGroup) return true;

    var conditions = _property(action.conditionGroup, 'conditions', 'Conditions') || [];
    var mode = String(_property(action.conditionGroup, 'mode', 'Mode') || 'all').toLowerCase();
    var missingPolicy = String(_property(action.conditionGroup, 'missingTagPolicy', 'MissingTagPolicy') || 'blockAction').toLowerCase();
    var results = [];
    for (var i = 0; i < conditions.length; i++) {
      var result = _evaluateCondition(conditions[i]);
      if (result.missing) return missingPolicy === 'allowaction';
      results.push(result.matched);
    }
    if (results.length === 0) return false;
    return mode === 'any' ? results.some(Boolean) : results.every(Boolean);
  }

  function _findTarget(root, elementId) {
    if (!root || !elementId) return null;
    if (typeof root.getAttribute === 'function' && root.getAttribute('data-scada-element-id') === elementId) return root;
    if (typeof root.querySelectorAll !== 'function') return null;
    var elements = root.querySelectorAll('[data-scada-element-id]');
    for (var i = 0; i < elements.length; i++) {
      if (elements[i].getAttribute('data-scada-element-id') === elementId) return elements[i];
    }
    return null;
  }

  function _valueTarget(source, root, action) {
    return action.targetElementId ? _findTarget(root, action.targetElementId) : source;
  }

  function _setElementValue(element, value) {
    if (!element) return false;
    var input = typeof element.matches === 'function' && element.matches('input, textarea, select')
      ? element
      : (typeof element.querySelector === 'function' ? element.querySelector('input, textarea, select') : null);
    if (input) {
      input.value = value === null || value === undefined ? '' : String(value);
      return true;
    }
    var text = typeof element.querySelector === 'function' ? element.querySelector('[data-scada-text]') : null;
    (text || element).textContent = value === null || value === undefined ? '' : String(value);
    return true;
  }

  function _readElementValue(element) {
    if (!element) return undefined;
    var input = typeof element.matches === 'function' && element.matches('input, textarea, select')
      ? element
      : (typeof element.querySelector === 'function' ? element.querySelector('input, textarea, select') : null);
    return input ? input.value : undefined;
  }

  function _setVisibility(target, visible) {
    if (!target || !target.style) return false;
    if (!visible && !_visibilityBaselines.has(target) && target.style.display !== 'none') {
      _visibilityBaselines.set(target, target.style.display || '');
    }
    target.style.display = visible ? (_visibilityBaselines.get(target) || '') : 'none';
    if (typeof target.setAttribute === 'function') target.setAttribute('aria-hidden', visible ? 'false' : 'true');
    return true;
  }

  function _dispatchIntent(kind, action, payload) {
    var dispatcher = window.ScadaRuntime && window.ScadaRuntime.CommandDispatcher;
    return dispatcher && typeof dispatcher.dispatchIntent === 'function'
      ? dispatcher.dispatchIntent(kind, action.id, payload)
      : false;
  }

  function execute(source, action, root, pageId) {
    action = _normalizeAction(action);
    if (!action || !action.id || !_conditionsAllow(action)) return false;
    var target;
    var bridge;
    var result = false;
    switch (action.kind) {
      case 'navigate':
        result = action.targetPageId ? _dispatchIntent('navigate', action, { pageId: action.targetPageId }) : false;
        break;
      case 'show':
      case 'hide':
      case 'toggleVisibility':
        target = _findTarget(root, action.targetElementId);
        if (!target) break;
        if (action.kind === 'show') result = _setVisibility(target, true);
        else if (action.kind === 'hide') result = _setVisibility(target, false);
        else result = _setVisibility(target, target.style.display === 'none');
        break;
      case 'mountFragment':
        result = action.targetPageId ? _dispatchIntent('openPopup', action, { pageId: action.targetPageId, options: action.popupOptions }) : false;
        break;
      case 'closePopup':
      case 'togglePopup':
        result = action.targetPageId ? _dispatchIntent(action.kind, action, { pageId: action.targetPageId, options: action.popupOptions }) : false;
        break;
      case 'readValue':
        bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
        var readValue = bridge && typeof bridge.getTagValue === 'function'
          ? bridge.getTagValue(action.tagId) : undefined;
        result = readValue === undefined || readValue === null
          ? false : _setElementValue(_valueTarget(source, root, action), readValue);
        break;
      case 'writeValue':
        bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
        var value = _readElementValue(_valueTarget(source, root, action));
        result = bridge && typeof bridge.writeTag === 'function' && action.tagId && value !== undefined
          ? bridge.writeTag(action.tagId, value, { actionId: action.id, pageId: pageId || '' })
          : false;
        break;
      default:
        _warn('kind-unsupported', action.kind);
    }
    return result;
  }

  function _emit(container, type, detail) {
    if (!container || typeof container.dispatchEvent !== 'function' || typeof window.CustomEvent !== 'function') return;
    container.dispatchEvent(new window.CustomEvent(type, { bubbles: true, detail: detail }));
  }

  function initPage(container, pageId) {
    if (!container) return;
    dispose(container);
    var registryPayload = _parseAttribute(container, 'data-scada-action-registry', { actions: [] });
    var actionsPayload = _property(registryPayload, 'actions', 'Actions') || registryPayload || [];
    if (!Array.isArray(actionsPayload)) actionsPayload = [];
    var actions = {};
    for (var i = 0; i < actionsPayload.length; i++) {
      var normalized = _normalizeAction(actionsPayload[i]);
      if (normalized && normalized.id) actions[normalized.id] = normalized;
    }
    var listeners = [];
    var elements = typeof container.querySelectorAll === 'function'
      ? container.querySelectorAll('[data-scada-action-bindings]') : [];
    for (var e = 0; e < elements.length; e++) {
      (function (element) {
        var payload = _parseAttribute(element, 'data-scada-action-bindings', []);
        var bindings = Array.isArray(payload) ? payload.map(_normalizeBinding) : [];
        var byEvent = {};
        for (var b = 0; b < bindings.length; b++) {
          var eventName = TRIGGER_MAP[String(bindings[b].trigger || '').toLowerCase()];
          if (!eventName) { _warn('trigger-unsupported', bindings[b].trigger); continue; }
          if (!byEvent[eventName]) byEvent[eventName] = [];
          byEvent[eventName].push(bindings[b]);
        }
        Object.keys(byEvent).forEach(function (eventName) {
          var orderedBindings = byEvent[eventName];
          var handler = function (event) {
            if (typeof element.getAttribute === 'function' && element.getAttribute('data-scada-disabled') === 'true') return;
            for (var j = 0; j < orderedBindings.length; j++) {
              var binding = orderedBindings[j];
              if (binding.preventDefault && event && typeof event.preventDefault === 'function') event.preventDefault();
              if (binding.stopPropagation && event && typeof event.stopPropagation === 'function') event.stopPropagation();
              var action = actions[binding.actionId];
              if (!action) { _warn('definition-missing', binding.actionId); continue; }
              var result = execute(element, action, container, pageId);
              _emit(container, 'scada-builder-action-executed', { pageId: pageId, actionId: action.id, result: result });
            }
          };
          element.addEventListener(eventName, handler);
          listeners.push({ element: element, eventName: eventName, handler: handler });
        });
      })(elements[e]);
    }
    _pages.set(container, { listeners: listeners });
  }

  function dispose(container) {
    var page = container ? _pages.get(container) : null;
    if (!page) return;
    for (var i = 0; i < page.listeners.length; i++) {
      var listener = page.listeners[i];
      listener.element.removeEventListener(listener.eventName, listener.handler);
    }
    _pages.delete(container);
  }

  window.ScadaRuntime = window.ScadaRuntime || {};
  window.ScadaRuntime.ActionDispatcher = {
    initPage: initPage,
    dispose: dispose,
    execute: execute,
    evaluateCondition: _evaluateCondition,
    conditionsAllow: _conditionsAllow
  };
})();
