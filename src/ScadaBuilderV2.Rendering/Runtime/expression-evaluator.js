(function () {
  'use strict';

  /**
   * Expression evaluator for the SCADA Builder V2 serialized AST format.
   * Walks a serialized expression AST node and returns the computed value
   * (number, boolean, string, or null for unavailable tag).
   *
   * Internal error flag: set by divide-by-zero or modulo-by-zero.
   * Does NOT block evaluation — the caller checks hasError() after walk().
   */
  var _errorFlag = false;
  var _errorMessage = null;

  function _flagError(msg) {
    _errorFlag = true;
    _errorMessage = msg || 'evaluation error';
  }

  function resetError() {
    _errorFlag = false;
    _errorMessage = null;
  }

  function hasError() {
    return _errorFlag;
  }

  function getError() {
    return _errorMessage;
  }

  function _number(value, context) {
    if (value === null || value === undefined || (typeof value === 'string' && value.trim() === '')) {
      _flagError((context || 'Value') + ' is not numeric');
      return null;
    }
    var result = Number(value);
    if (!Number.isFinite(result)) {
      _flagError((context || 'Value') + ' is not finite');
      return null;
    }
    return result;
  }

  function _comparableNumber(value) {
    if (value === null || value === undefined || (typeof value === 'string' && value.trim() === '')) {
      return null;
    }
    var result = Number(value);
    return Number.isFinite(result) ? result : null;
  }

  // Exported AST enums use lower camel case (for example `greaterThan`),
  // while older projects can still contain the original Pascal-case spelling.
  // Normalize both representations at the runtime boundary.
  function _canonicalOperator(value) {
    var text = String(value || '');
    return text ? text.charAt(0).toUpperCase() + text.slice(1) : '';
  }

  // ── internal walk (no error-reset) ──────────────────────────────────────

  function walkNode(node, tagValues) {
    if (!node || typeof node !== 'object') {
      return null;
    }

    switch (node.type) {
      case 'literalNumber':
        return _number(node.value, 'Numeric literal');

      case 'literalBool':
        return node.value === true;

      case 'literalString':
        return String(node.value != null ? node.value : '');

      case 'tagRef': {
        var tagName = node.tagName;
        if (!tagName) {
          return null;
        }
        // Prefer TagBridge (which knows how to talk to the host's
        // window.tf100webScadaBuilder and strips id prefixes like "tf100.mapping.").
        // Fall back to the raw tagValues map only when TagBridge isn't loaded.
        var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
        var val = bridge ? bridge.getTagValue(tagName) : (tagValues && tagValues[tagName]);
        if (val === null || val === undefined) {
          return null;
        }
        return val;
      }

      case 'unary': {
        var operand = walkNode(node.operand, tagValues);
        if (operand === null) {
          return null;
        }
        var unaryOperator = _canonicalOperator(node.op);
        if (unaryOperator === 'Not') {
          return !operand;
        }
        if (unaryOperator === 'Negate') {
          var negated = _number(operand, 'Unary operand');
          return negated === null ? null : -negated;
        }
        _flagError('Unknown unary operator: ' + String(node.op || ''));
        return null;
      }

      case 'binary': {
        var binaryOperator = _canonicalOperator(node.op);
        if (binaryOperator === 'And') {
          var andLeft = walkNode(node.left, tagValues);
          if (andLeft === null) {
            return null;
          }
          if (!Boolean(andLeft)) {
            return false;
          }
          var andRight = walkNode(node.right, tagValues);
          return andRight === null ? null : Boolean(andRight);
        }

        if (binaryOperator === 'Or') {
          var orLeft = walkNode(node.left, tagValues);
          if (orLeft !== null && Boolean(orLeft)) {
            return true;
          }
          var orRight = walkNode(node.right, tagValues);
          return orRight === null ? null : Boolean(orRight);
        }

        var left = walkNode(node.left, tagValues);
        var right = walkNode(node.right, tagValues);

        if (left === null || right === null) {
          return null;
        }

        switch (binaryOperator) {
          case 'Add': {
            var addLeft = _number(left, 'Left operand');
            var addRight = _number(right, 'Right operand');
            if (addLeft === null || addRight === null) return null;
            return addLeft + addRight;
          }
          case 'Subtract':
          case 'Multiply':
          case 'Divide':
          case 'Modulo':
          case 'LessThan':
          case 'LessThanOrEqual':
          case 'GreaterThan':
          case 'GreaterThanOrEqual': {
            var nLeft = _number(left, 'Left operand');
            var nRight = _number(right, 'Right operand');
            if (nLeft === null || nRight === null) return null;
            if (binaryOperator === 'Subtract') return nLeft - nRight;
            if (binaryOperator === 'Multiply') return nLeft * nRight;
            if (binaryOperator === 'Divide') {
              if (nRight === 0) {
                _flagError('Division by zero');
                return null;
              }
              return nLeft / nRight;
            }
            if (binaryOperator === 'Modulo') {
              if (nRight === 0) {
                _flagError('Modulo by zero');
                return null;
              }
              return nLeft % nRight;
            }
            if (binaryOperator === 'LessThan') return nLeft < nRight;
            if (binaryOperator === 'LessThanOrEqual') return nLeft <= nRight;
            if (binaryOperator === 'GreaterThan') return nLeft > nRight;
            return nLeft >= nRight;
          }
          case 'Equal': {
            if (left === right) return true;
            var equalLeft = _comparableNumber(left);
            var equalRight = _comparableNumber(right);
            return equalLeft !== null && equalRight !== null && equalLeft === equalRight;
          }
          case 'NotEqual': {
            if (left === right) return false;
            var notEqualLeft = _comparableNumber(left);
            var notEqualRight = _comparableNumber(right);
            return notEqualLeft === null || notEqualRight === null || notEqualLeft !== notEqualRight;
          }
          default:
            _flagError('Unknown binary operator: ' + String(node.op || ''));
            return null;
        }
      }

      case 'func': {
        if (!node.name || !node.args || !Array.isArray(node.args)) {
          _flagError('Invalid function expression');
          return null;
        }
        var name = node.name.toUpperCase();

        if (name === 'ABS') {
          if (node.args.length !== 1) {
            _flagError('ABS expects one argument');
            return null;
          }
          var absArg = walkNode(node.args[0], tagValues);
          if (absArg === null) return null;
          var absNumber = _number(absArg, 'ABS argument');
          return absNumber === null ? null : Math.abs(absNumber);
        }

        if (name === 'MIN' || name === 'MAX') {
          if (node.args.length !== 2) {
            _flagError(name + ' expects two arguments');
            return null;
          }
          var first = walkNode(node.args[0], tagValues);
          var second = walkNode(node.args[1], tagValues);
          if (first === null || second === null) return null;
          var firstNumber = _number(first, name + ' first argument');
          var secondNumber = _number(second, name + ' second argument');
          if (firstNumber === null || secondNumber === null) return null;
          return name === 'MIN' ? Math.min(firstNumber, secondNumber) : Math.max(firstNumber, secondNumber);
        }

        if (name === 'BIT') {
          if (node.args.length !== 2) {
            _flagError('BIT expects two arguments');
            return null;
          }
          var bitValue = walkNode(node.args[0], tagValues);
          var bitIndex = walkNode(node.args[1], tagValues);
          if (bitValue === null || bitIndex === null) return null;
          var valueNumber = _number(bitValue, 'BIT value');
          var indexNumber = _number(bitIndex, 'BIT index');
          if (valueNumber === null || indexNumber === null) return null;
          if (!Number.isInteger(indexNumber) || indexNumber < 0 || indexNumber > 31) {
            _flagError('BIT index must be an integer from 0 to 31');
            return null;
          }
          return ((valueNumber >>> indexNumber) & 1) === 1;
        }

        _flagError('Unknown function: ' + name);
        return null;
      }

      default:
        _flagError('Unknown expression node: ' + String(node.type || ''));
        return null;
    }
  }

  /**
   * Walks a serialized expression AST node and returns the computed value.
   *
   * @param {object} node     - The AST node (must have a `type` property).
   * @param {object} tagValues - Map of tagName → value for runtime tag resolution.
   * @returns {number|boolean|string|null} Evaluated value, or null if a tag is unavailable.
   *
   * Before calling walk(), the host should call resetError().
   * After walk() returns, call hasError() to check for invalid arithmetic or AST data.
   */
  function walk(node, tagValues) {
    return walkNode(node, tagValues || {});
  }

  // ── public API ──────────────────────────────────────────────────────────

  window.ScadaRuntime = window.ScadaRuntime || {};

  /** @type {({walk, _errorFlag, _flagError, resetError, hasError, getError})} */
  var publicApi = {
    walk: walk,
    _flagError: _flagError,
    resetError: resetError,
    hasError: hasError,
    getError: getError
  };

  Object.defineProperty(publicApi, '_errorFlag', {
    get: function () { return _errorFlag; },
    enumerable: true
  });

  window.ScadaRuntime.ExpressionEvaluator = publicApi;
})();
