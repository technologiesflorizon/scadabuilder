import vm from 'node:vm';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const RUNTIME_DIR = path.resolve(__dirname, '../../src/ScadaBuilderV2.Rendering/Runtime');

/**
 * Loads real runtime module files (from src/ScadaBuilderV2.Rendering/Runtime/) into a
 * sandboxed vm.Context with a minimal `window` global, in the given order. Returns the
 * sandbox's `window` so tests can call window.ScadaRuntime.* and inspect/stub globals
 * (window.postMessage, window.tf100webScadaBuilder, etc.) exactly as the browser would.
 *
 * @param {string[]} moduleNames - file names under Runtime/, e.g. ['tag-bridge.js'].
 * @returns {object} the sandbox's window object.
 */
export function loadRuntime(moduleNames) {
  const sandbox = { console };
  sandbox.window = sandbox;
  const context = vm.createContext(sandbox);

  for (const name of moduleNames) {
    const filePath = path.join(RUNTIME_DIR, name);
    const source = fs.readFileSync(filePath, 'utf8');
    vm.runInContext(source, context, { filename: name });
  }

  return context.window;
}

// ── minimal fake DOM ────────────────────────────────────────────────────────
//
// Node has no DOM. `substituteInstance` (quick-window-runtime.js) needs just enough of one to
// run for real inside the vm sandbox: elements with get/setAttribute, a single compound
// querySelectorAll (comma-separated attribute-presence / tag selectors, no combinators — the
// only shape substituteInstance ever issues), and a TreeWalker over text nodes. Kept here,
// rather than duplicated per test file, because any test exercising DOM-mounted substitution
// needs the same shape.

class FakeNode {
  constructor(nodeType) {
    this.nodeType = nodeType;
    this.parentNode = null;
    this.childNodes = [];
  }
}

class FakeText extends FakeNode {
  constructor(text) {
    super(3); // Node.TEXT_NODE
    this.textContent = text || '';
  }
}

class FakeElement extends FakeNode {
  constructor(tagName) {
    super(1); // Node.ELEMENT_NODE
    this.tagName = String(tagName).toUpperCase();
    this._attributes = new Map();
  }

  appendChild(child) {
    child.parentNode = this;
    this.childNodes.push(child);
    return child;
  }

  setAttribute(name, value) {
    this._attributes.set(String(name), String(value));
  }

  getAttribute(name) {
    return this._attributes.has(name) ? this._attributes.get(name) : null;
  }

  hasAttribute(name) {
    return this._attributes.has(name);
  }

  get textContent() {
    var text = '';
    for (var i = 0; i < this.childNodes.length; i++) {
      text += this.childNodes[i].textContent;
    }
    return text;
  }

  set textContent(value) {
    this.childNodes = [];
    this.appendChild(new FakeText(value));
  }

  querySelectorAll(selector) {
    return _queryAll(this, selector);
  }
}

function _matchesSimple(el, token) {
  var tagMatch = token.match(/^[A-Za-z][A-Za-z0-9-]*/);
  var tag = tagMatch ? tagMatch[0] : null;
  if (tag && el.tagName !== tag.toUpperCase()) {
    return false;
  }
  var attrRe = /\[([^\]=]+)(?:="([^"]*)")?\]/g;
  var match;
  while ((match = attrRe.exec(token)) !== null) {
    var name = match[1];
    var value = match[2];
    if (!el.hasAttribute(name)) return false;
    if (value !== undefined && el.getAttribute(name) !== value) return false;
  }
  return true;
}

function _queryAll(root, selector) {
  var parts = selector.split(',').map(function (part) { return part.trim(); });
  var result = [];
  (function walk(node) {
    for (var i = 0; i < node.childNodes.length; i++) {
      var child = node.childNodes[i];
      if (child.nodeType === 1) {
        for (var p = 0; p < parts.length; p++) {
          if (_matchesSimple(child, parts[p])) {
            result.push(child);
            break;
          }
        }
        walk(child);
      }
    }
  })(root);
  return result;
}

/**
 * Builds a minimal fake `document`/`NodeFilter` pair sufficient for
 * `ScadaRuntime.QuickWindow.substituteInstance`: element creation, attribute get/set, a single
 * compound `querySelectorAll`, and `createTreeWalker` over text nodes. Attach both onto the
 * sandbox's `window` (e.g. `Object.assign(window, createFakeDom())`) before building a fixture.
 *
 * @returns {{document: object, NodeFilter: object}}
 */
export function createFakeDom() {
  var NodeFilter = { SHOW_TEXT: 4 };

  var document = {
    createElement: function (tag) { return new FakeElement(tag); },
    createTextNode: function (text) { return new FakeText(text); },
    createTreeWalker: function (root, whatToShow) {
      var nodes = [];
      (function collect(node) {
        for (var i = 0; i < node.childNodes.length; i++) {
          var child = node.childNodes[i];
          if (whatToShow === NodeFilter.SHOW_TEXT ? child.nodeType === 3 : true) {
            nodes.push(child);
          }
          collect(child);
        }
      })(root);
      var index = -1;
      return {
        nextNode: function () {
          index += 1;
          return index < nodes.length ? nodes[index] : null;
        }
      };
    }
  };

  return { document: document, NodeFilter: NodeFilter };
}

/**
 * Builds one DOM tree from a tiny well-formed HTML fragment (single root element, `"` quoted
 * attributes only, no self-closing tags, no comments): enough to let a fixture be written once as
 * a template string and cloned into independent trees per call — e.g. to prove two mounted
 * instances of the "same page" never share state. Uses the `document` a `createFakeDom()` call
 * produced.
 *
 * @param {object} document - The fake `document` (from `createFakeDom()`).
 * @param {string} html     - A single-root HTML fragment.
 * @returns {object} the root element.
 */
/**
 * Decodes the handful of named/numeric HTML entities the exporter (`HtmlEncoder.Default.Encode`) ever
 * produces, exactly as a real browser's parser would while building attribute values and text nodes.
 * Without this, an attribute serialized as JSON (`data-scada-state-config`, `&quot;`-escaped) comes back
 * from `getAttribute` with the literal three-character sequence `&quot;` instead of a `"`, and
 * `JSON.parse` on it fails — a real DOM never has this problem.
 */
function _decodeHtmlEntities(value) {
  return value
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, '\'')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&amp;/g, '&');
}

export function parseFragment(document, html) {
  var tokenRe = /<\/[a-zA-Z][^>]*>|<[a-zA-Z][^>]*>|[^<]+/g;
  var attrRe = /([a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*"([^"]*)"/g;
  var root = null;
  var stack = [];
  var match;
  while ((match = tokenRe.exec(html)) !== null) {
    var token = match[0];
    if (token[0] === '<' && token[1] === '/') {
      stack.pop();
      continue;
    }
    if (token[0] === '<') {
      var inner = token.slice(1, -1);
      var tagMatch = inner.match(/^[a-zA-Z][a-zA-Z0-9-]*/);
      var element = document.createElement(tagMatch[0]);
      var rest = inner.slice(tagMatch[0].length);
      var attrMatch;
      attrRe.lastIndex = 0;
      while ((attrMatch = attrRe.exec(rest)) !== null) {
        element.setAttribute(attrMatch[1], _decodeHtmlEntities(attrMatch[2]));
      }
      if (stack.length > 0) {
        stack[stack.length - 1].appendChild(element);
      } else {
        root = element;
      }
      stack.push(element);
      continue;
    }
    if (stack.length > 0) {
      stack[stack.length - 1].appendChild(document.createTextNode(_decodeHtmlEntities(token)));
    }
  }
  return root;
}
