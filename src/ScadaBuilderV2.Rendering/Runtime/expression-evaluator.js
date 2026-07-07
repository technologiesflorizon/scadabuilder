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

  // ── internal walk (no error-reset) ──────────────────────────────────────

  function walkNode(node, tagValues) {
    if (!node || typeof node !== 'object') {
      return null;
    }

    switch (node.type) {
      case 'literalNumber':
        return typeof node.value === 'number' ? node.value : Number(node.value);

      case 'literalBool':
        return node.value === true;

      case 'literalString':
        return String(node.value != null ? node.value : '');

      case 'tagRef': {
        var tagName = node.tagName;
        if (!tagName) {
          return null;
        }
        var val = tagValues && tagValues[tagName];
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
        if (node.op === 'Not') {
          return !operand;
        }
        if (node.op === 'Negate') {
          return -Number(operand);
        }
        return null;
      }

      case 'binary': {
        if (node.op === 'And') {
          var andLeft = walkNode(node.left, tagValues);
          if (!andLeft) {
            return false;
          }
          var andRight = walkNode(node.right, tagValues);
          return Boolean(andRight);
        }

        if (node.op === 'Or') {
          var orLeft = walkNode(node.left, tagValues);
          if (orLeft) {
            return true;
          }
          var orRight = walkNode(node.right, tagValues);
          return Boolean(orRight);
        }

        var left = walkNode(node.left, tagValues);
        var right = walkNode(node.right, tagValues);

        if (left === null || right === null) {
          return null;
        }

        var nLeft = Number(left);
        var nRight = Number(right);

        switch (node.op) {
          case 'Add':
            return nLeft + nRight;
          case 'Subtract':
            return nLeft - nRight;
          case 'Multiply':
            return nLeft * nRight;
          case 'Divide':
            if (nRight === 0) {
              _flagError('Division by zero');
              return null;
            }
            return nLeft / nRight;
          case 'Modulo':
            if (nRight === 0) {
              _flagError('Modulo by zero');
              return null;
            }
            return nLeft % nRight;
          case 'Equal':
            return left === right || nLeft === nRight;
          case 'NotEqual':
            return left !== right && nLeft !== nRight;
          case 'LessThan':
            return nLeft < nRight;
          case 'LessThanOrEqual':
            return nLeft <= nRight;
          case 'GreaterThan':
            return nLeft > nRight;
          case 'GreaterThanOrEqual':
            return nLeft >= nRight;
          default:
            return null;
        }
      }

      case 'func': {
        if (!node.name || !node.args || !Array.isArray(node.args)) {
          return null;
        }
        var name = node.name.toUpperCase();

        if (name === 'ABS') {
          if (node.args.length < 1) return null;
          var absArg = walkNode(node.args[0], tagValues);
          if (absArg === null) return null;
          return Math.abs(Number(absArg));
        }

        if (name === 'MIN') {
          if (node.args.length < 2) return null;
          var minA = walkNode(node.args[0], tagValues);
          var minB = walkNode(node.args[1], tagValues);
          if (minA === null || minB === null) return null;
          return Math.min(Number(minA), Number(minB));
        }

        if (name === 'MAX') {
          if (node.args.length < 2) return null;
          var maxA = walkNode(node.args[0], tagValues);
          var maxB = walkNode(node.args[1], tagValues);
          if (maxA === null || maxB === null) return null;
          return Math.max(Number(maxA), Number(maxB));
        }

        if (name === 'BIT') {
          if (node.args.length < 2) return null;
          var bitTag = walkNode(node.args[0], tagValues);
          var bitN = walkNode(node.args[1], tagValues);
          if (bitTag === null || bitN === null) return null;
          var val = Number(bitTag);
          var bit = Number(bitN);
          if (!Number.isFinite(val) || !Number.isFinite(bit)) return null;
          return ((val >> bit) & 1) === 1;
        }

        return null;
      }

      default:
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
   * After walk() returns, call hasError() to check for divide-by-zero etc.
   */
  function walk(node, tagValues) {
    return walkNode(node, tagValues || {});
  }

  // ── public API ──────────────────────────────────────────────────────────

  window.ScadaRuntime = window.ScadaRuntime || {};

  /** @type {({walk, _errorFlag, _flagError, resetError, hasError})} */
  var publicApi = {
    walk: walk,
    _flagError: _flagError,
    resetError: resetError,
    hasError: hasError
  };

  Object.defineProperty(publicApi, '_errorFlag', {
    get: function () { return _errorFlag; },
    enumerable: true
  });

  window.ScadaRuntime.ExpressionEvaluator = publicApi;
})();
