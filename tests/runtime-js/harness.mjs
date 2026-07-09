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
