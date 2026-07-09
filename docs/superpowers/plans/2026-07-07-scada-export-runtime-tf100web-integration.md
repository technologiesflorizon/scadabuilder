# SCADA Export Runtime & TF100Web Industrial Integration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Serialize state/command configs in the .sb2 export, ship a shared JS runtime that TF100Web executes, and deploy pages directly to templates/static via a Django management command.

**Architecture:** The Builder generates a self-contained .sb2 package with content-hashed assets, a shared `scada-runtime.<hash>.js` referenced by all pages, and StateConfig/CommandConfig serialized in both manifest JSON and HTML data attributes. TF100Web deploys via `manage.py deploy_scada_builder`, serves pages as Django templates, and `visualisation_import.js` orchestrates AJAX navigation, tag polling, edit-lock protection, and popup management.

**Tech Stack:** .NET 8 / C# (Builder V2 exporter), JavaScript ES6 (runtime + visualisation_import.js), Python 3 / Django (TF100Web management command + views), MSTest (Builder tests).

## Global Constraints

- Namespace `ScadaBuilderV2.Domain.ElementEvents` already exists. Do not rename or restructure.
- `ScadaElement.StateConfig` and `ScadaElement.CommandConfig` are already wired. Do not modify `ScadaSceneModels.cs`.
- Content hash: SHA-256, first 8 hex chars lowercase, applied to CSS, images, and runtime JS filenames.
- Runtime JS modules live in `src/ScadaBuilderV2.Rendering/Runtime/` as embedded resources (build action: EmbeddedResource).
- `Ft100SceneExporter` is a `sealed partial class`. New methods go in the same file or a new partial.
- TF100Web changes are gated behind `settings.TF100_INDUSTRIAL_DEPLOYMENT` and `station_type == SCADA_BUILDER_2`.
- Do NOT delete deprecated TF100Web code in this iteration — mark with `# DEPRECATED:` comments only.
- The `<script>` in exported HTML is replaced by `<script src="../scada-runtime.<hash>.js" defer>` — the old `BuildRuntimeScript` is removed.
- Edit lock timeout: 30 seconds. Polling interval: 500ms.
- All public C# APIs require XML doc `<summary>`. Contract-sensitive code cites `Decisions:`/`Contracts:`/`Tests:` in `<remarks>`.
- TDD: every task starts with a failing test, then implementation, then commit.

---

### Task 1: Expression evaluator JS module

**Files:**
- Create: `src/ScadaBuilderV2.Rendering/Runtime/expression-evaluator.js`
- Test: `tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs` (create)

**Interfaces:**
- Produces: `window.ScadaRuntime.ExpressionEvaluator = { walk(node, tagValues) }` — walks a serialized AST node and returns a value (number, boolean, string, or null for unavailable tag). `tagValues` is a `{tagName: value|null}` map.

- [ ] **Step 1: Write the failing C# test verifying the JS module is embedded**

```csharp
// tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScadaBuilderV2.Tests.Runtime;

[TestClass]
public sealed class RuntimeJsModulesTests
{
    [TestMethod]
    public void ExpressionEvaluatorModule_IsEmbeddedResource()
    {
        var assembly = typeof(Ft100SceneExporter).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("expression-evaluator.js", StringComparison.Ordinal));

        Assert.IsNotNull(resourceName, "expression-evaluator.js not found as embedded resource");

        using var stream = assembly.GetManifestResourceStream(resourceName);
        Assert.IsNotNull(stream);
        using var reader = new StreamReader(stream!);
        var content = reader.ReadToEnd();

        Assert.IsTrue(content.Contains("ScadaRuntime.ExpressionEvaluator"),
            "Module must expose ScadaRuntime.ExpressionEvaluator");
        Assert.IsTrue(content.Contains("function walk"),
            "Module must define walk function");
        Assert.IsTrue(content.Contains("tagRef"), "Must handle tagRef node type");
        Assert.IsTrue(content.Contains("literalNumber"), "Must handle literalNumber node type");
        Assert.IsTrue(content.Contains("func"), "Must handle func node type (ABS, MIN, MAX, BIT)");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~RuntimeJsModulesTests.ExpressionEvaluatorModule_IsEmbeddedResource"`
Expected: FAIL (resource not found).

- [ ] **Step 3: Create the JS module file**

```javascript
// src/ScadaBuilderV2.Rendering/Runtime/expression-evaluator.js
// Expression AST walker for SCADA runtime.
// Walks the serialized AST (source of truth, per STATE_COMMAND_RUNTIME_CONTRACT_V1 §3).
// Contracts: docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md.

(function () {
  const evaluator = {};

  function isNullish(v) { return v === null || v === undefined; }

  evaluator.walk = function (node, tagValues) {
    if (!node || !node.type) return null;
    switch (node.type) {
      case 'literalNumber': return node.value;
      case 'literalBool':   return node.value;
      case 'literalString': return node.value;
      case 'tagRef': {
        const raw = tagValues[node.tagName];
        return isNullish(raw) ? null : raw;
      }
      case 'unary': {
        const operand = evaluator.walk(node.operand, tagValues);
        if (isNullish(operand)) return null;
        if (node.op === 'Not') return !operand;
        if (node.op === 'Negate') return -operand;
        return null;
      }
      case 'binary': {
        const left = evaluator.walk(node.left, tagValues);
        const right = evaluator.walk(node.right, tagValues);
        if (isNullish(left) || isNullish(right)) return null;
        switch (node.op) {
          case 'Add':              return left + right;
          case 'Subtract':         return left - right;
          case 'Multiply':         return left * right;
          case 'Divide':           return right === 0 ? (evaluator._flagError('division by zero'), null) : left / right;
          case 'Modulo':           return right === 0 ? (evaluator._flagError('modulo by zero'), null) : left % right;
          case 'Equal':            return left === right;
          case 'NotEqual':         return left !== right;
          case 'LessThan':         return left < right;
          case 'LessThanOrEqual':  return left <= right;
          case 'GreaterThan':      return left > right;
          case 'GreaterThanOrEqual': return left >= right;
          case 'And':              return !!(left && right);
          case 'Or':               return !!(left || right);
          default: return null;
        }
      }
      case 'func': {
        const args = (node.args || []).map(function (a) { return evaluator.walk(a, tagValues); });
        if (args.some(function (a) { return isNullish(a); })) return null;
        switch (node.name) {
          case 'ABS': return Math.abs(args[0]);
          case 'MIN': return Math.min(args[0], args[1]);
          case 'MAX': return Math.max(args[0], args[1]);
          case 'BIT': {
            const val = args[0];
            const bit = args[1];
            if (typeof val !== 'number' || typeof bit !== 'number') return null;
            return ((val >> bit) & 1) === 1;
          }
          default: return null;
        }
      }
      default: return null;
    }
  };

  evaluator._errorFlag = false;
  evaluator._flagError = function (msg) { evaluator._errorFlag = true; return null; };
  evaluator.resetError = function () { evaluator._errorFlag = false; };
  evaluator.hasError = function () { return evaluator._errorFlag; };

  window.ScadaRuntime = window.ScadaRuntime || {};
  window.ScadaRuntime.ExpressionEvaluator = evaluator;
})();
```

- [ ] **Step 4: Set build action to EmbeddedResource**

In `src/ScadaBuilderV2.Rendering/ScadaBuilderV2.Rendering.csproj`, add:

```xml
<ItemGroup>
  <EmbeddedResource Include="Runtime\expression-evaluator.js" />
</ItemGroup>
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~RuntimeJsModulesTests.ExpressionEvaluatorModule_IsEmbeddedResource"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Runtime/expression-evaluator.js
git add src/ScadaBuilderV2.Rendering/ScadaBuilderV2.Rendering.csproj
git add tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs
git commit -m "feat: add expression evaluator JS module (AST walker)"
```

---

### Task 2: Effect applier + State engine JS modules

**Files:**
- Create: `src/ScadaBuilderV2.Rendering/Runtime/effect-applier.js`
- Create: `src/ScadaBuilderV2.Rendering/Runtime/state-engine.js`
- Modify: `tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs`

**Interfaces:**
- Produces: `window.ScadaRuntime.EffectApplier = { apply(element, effect) }` — applies a ScadaEffectBlock to a DOM element. Null properties are skipped.
- Produces: `window.ScadaRuntime.StateEngine = { evaluate(element, tagValues), initPage(container, pageId), pauseElement(id), resumeElement(id) }` — first-match-wins state resolution.

- [ ] **Step 1: Write failing tests**

```csharp
[TestMethod]
public void EffectApplierModule_IsEmbeddedResource()
{
    var assembly = typeof(Ft100SceneExporter).Assembly;
    var resourceName = assembly.GetManifestResourceNames()
        .FirstOrDefault(n => n.EndsWith("effect-applier.js", StringComparison.Ordinal));

    Assert.IsNotNull(resourceName, "effect-applier.js not found as embedded resource");
    using var stream = assembly.GetManifestResourceStream(resourceName);
    Assert.IsNotNull(stream);
    using var reader = new StreamReader(stream!);
    var content = reader.ReadToEnd();

    Assert.IsTrue(content.Contains("ScadaRuntime.EffectApplier"));
    Assert.IsTrue(content.Contains("backgroundColor"), "Must handle backgroundColor");
    Assert.IsTrue(content.Contains("borderColor"), "Must handle borderColor");
    Assert.IsTrue(content.Contains("opacity"), "Must handle opacity");
    Assert.IsTrue(content.Contains("rotation"), "Must handle rotation");
    Assert.IsTrue(content.Contains("animation"), "Must handle animation");
}

[TestMethod]
public void StateEngineModule_IsEmbeddedResource()
{
    var assembly = typeof(Ft100SceneExporter).Assembly;
    var resourceName = assembly.GetManifestResourceNames()
        .FirstOrDefault(n => n.EndsWith("state-engine.js", StringComparison.Ordinal));

    Assert.IsNotNull(resourceName, "state-engine.js not found as embedded resource");
    using var stream = assembly.GetManifestResourceStream(resourceName);
    Assert.IsNotNull(stream);
    using var reader = new StreamReader(stream!);
    var content = reader.ReadToEnd();

    Assert.IsTrue(content.Contains("ScadaRuntime.StateEngine"));
    Assert.IsTrue(content.Contains("first-match-wins"), "Must document first-match-wins");
    Assert.IsTrue(content.Contains("qualityFallback"), "Must handle quality fallback");
    Assert.IsTrue(content.Contains("defaultEffect"), "Must handle default/rest effect");
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~RuntimeJsModulesTests"`
Expected: 2 FAIL (effect-applier and state-engine resources not found), 1 PASS (evaluator).

- [ ] **Step 3: Write effect-applier.js**

```javascript
// src/ScadaBuilderV2.Rendering/Runtime/effect-applier.js
// Applies a ScadaEffectBlock to a DOM element. Null properties are skipped.
// Contracts: docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md §2.

(function () {
  const applier = {};

  applier.apply = function (element, effect) {
    if (!element || !effect) return;

    if (effect.backgroundColor != null) element.style.backgroundColor = effect.backgroundColor;
    if (effect.borderColor != null)     element.style.borderColor = effect.borderColor;
    if (effect.borderWidth != null)     element.style.borderWidth = effect.borderWidth + 'px';
    if (effect.textColor != null)       element.style.color = effect.textColor;
    if (effect.textVisible != null) {
      var textEl = element.querySelector('[data-scada-text]');
      if (textEl) textEl.hidden = !effect.textVisible;
    }
    if (effect.textContent != null) {
      var textEl = element.querySelector('[data-scada-text]');
      if (textEl) textEl.textContent = effect.textContent;
    }
    if (effect.elementVisible != null)  element.hidden = !effect.elementVisible;
    if (effect.opacity != null)         element.style.opacity = String(effect.opacity);
    if (effect.rotation != null) {
      element.style.transform = (element.style.transform || '')
        .replace(/rotate\([^)]*\)/g, '')
        .trim();
      element.style.transform += ' rotate(' + effect.rotation + 'deg)';
    }
    if (effect.animation != null) {
      // Remove existing animation classes
      var animClasses = ['scada-anim-blink', 'scada-anim-pulse', 'scada-anim-halo', 'scada-anim-spin'];
      animClasses.forEach(function (c) { element.classList.remove(c); });
      if (effect.animation !== 'None') {
        element.classList.add('scada-anim-' + effect.animation.toLowerCase());
      }
    }
  };

  window.ScadaRuntime = window.ScadaRuntime || {};
  window.ScadaRuntime.EffectApplier = applier;
})();
```

- [ ] **Step 4: Write state-engine.js**

```javascript
// src/ScadaBuilderV2.Rendering/Runtime/state-engine.js
// First-match-wins state evaluation loop.
// Contracts: docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md §4.

(function () {
  var engine = {};
  var pausedElements = {};
  var stateCache = {}; // elementId -> lastAppliedStateId (for change detection)

  function configFor(element) {
    try {
      return JSON.parse(element.getAttribute('data-scada-state-config') || 'null');
    } catch (e) {
      return null;
    }
  }

  function tagsInNode(node) {
    var tags = [];
    if (!node || !node.type) return tags;
    if (node.type === 'tagRef') { tags.push(node.tagName); }
    if (node.type === 'unary') { tags = tags.concat(tagsInNode(node.operand)); }
    if (node.type === 'binary') {
      tags = tags.concat(tagsInNode(node.left));
      tags = tags.concat(tagsInNode(node.right));
    }
    if (node.type === 'func' && node.args) {
      node.args.forEach(function (a) { tags = tags.concat(tagsInNode(a)); });
    }
    return tags;
  }

  engine.evaluate = function (element, tagValues) {
    var elementId = element.getAttribute('data-scada-element-id') || element.id;
    if (pausedElements[elementId]) return; // skip — input edit lock active

    var config = configFor(element);
    if (!config || !config.states || config.states.length === 0) return;

    var evaluator = window.ScadaRuntime.ExpressionEvaluator;
    var applier = window.ScadaRuntime.EffectApplier;
    evaluator.resetError();
    var allSkipped = true;
    var matched = false;

    for (var i = 0; i < config.states.length; i++) {
      var state = config.states[i];
      if (!state.enabled) continue;

      // Check quality: skip if any referenced tag is null
      var refs = tagsInNode(state.expression.ast);
      var hasNullTag = refs.some(function (tag) { return tagValues[tag] === null; });
      if (hasNullTag) continue; // quality skip

      allSkipped = false;
      var result = evaluator.walk(state.expression.ast, tagValues);

      if (result === true) {
        if (stateCache[elementId] !== state.id) {
          applier.apply(element, state.effect);
          stateCache[elementId] = state.id;
        }
        matched = true;
        if (evaluator.hasError()) engine._showErrorBadge(element, true);
        return;
      }
    }

    // No state matched
    if (allSkipped) {
      if (stateCache[elementId] !== '__quality__') {
        applier.apply(element, config.qualityFallback || { opacity: 0.4, borderColor: '#000000', borderWidth: 2 });
        stateCache[elementId] = '__quality__';
      }
    } else {
      if (stateCache[elementId] !== '__default__') {
        applier.apply(element, config.defaultEffect || {});
        stateCache[elementId] = '__default__';
      }
    }

    engine._showErrorBadge(element, evaluator.hasError());
  };

  engine.initPage = function (container, pageId) {
    stateCache = {};
    pausedElements = {};
    var elements = container.querySelectorAll('[data-scada-state-config]');
    elements.forEach(function (el) {
      stateCache[el.getAttribute('data-scada-element-id') || el.id] = null;
    });
  };

  engine.pauseElement = function (elementId) {
    pausedElements[elementId] = true;
  };

  engine.resumeElement = function (elementId) {
    pausedElements[elementId] = false;
    stateCache[elementId] = null; // force re-eval on next cycle
  };

  engine._showErrorBadge = function (element, show) {
    var badge = element.querySelector('.scada-error-badge');
    if (show) {
      if (!badge) {
        badge = document.createElement('span');
        badge.className = 'scada-error-badge';
        badge.textContent = '!';
        badge.title = 'Erreur d\'évaluation d\'expression';
        element.style.position = element.style.position || 'relative';
        element.appendChild(badge);
      }
      var textEl = element.querySelector('[data-scada-text]');
      if (textEl) textEl.textContent = '---';
    } else {
      if (badge) badge.remove();
    }
  };

  window.ScadaRuntime = window.ScadaRuntime || {};
  window.ScadaRuntime.StateEngine = engine;
})();
```

- [ ] **Step 5: Register both files as EmbeddedResource in .csproj**

```xml
<EmbeddedResource Include="Runtime\effect-applier.js" />
<EmbeddedResource Include="Runtime\state-engine.js" />
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~RuntimeJsModulesTests"`
Expected: all PASS.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Runtime/effect-applier.js
git add src/ScadaBuilderV2.Rendering/Runtime/state-engine.js
git add src/ScadaBuilderV2.Rendering/ScadaBuilderV2.Rendering.csproj
git add tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs
git commit -m "feat: add effect applier and state engine JS modules"
```

---

### Task 3: Animation controller + Command dispatcher + Tag bridge JS modules

**Files:**
- Create: `src/ScadaBuilderV2.Rendering/Runtime/animation-controller.js`
- Create: `src/ScadaBuilderV2.Rendering/Runtime/command-dispatcher.js`
- Create: `src/ScadaBuilderV2.Rendering/Runtime/tag-bridge.js`
- Modify: `tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs`

**Interfaces:**
- Produces: `window.ScadaRuntime.AnimationController` — animation CSS class management.
- Produces: `window.ScadaRuntime.CommandDispatcher = { bind(element), execute(element, command) }` — binds triggers to DOM events, executes commands.
- Produces: `window.ScadaRuntime.TagBridge = { getTagValue(tagId), writeTag(tagId, value, payload), setTagValue(tagId, value), onValuesChanged(callback) }` — tag interface with host fallback.

- [ ] **Step 1: Write failing tests**

```csharp
[TestMethod]
public void AnimationControllerModule_IsEmbeddedResource()
{
    var assembly = typeof(Ft100SceneExporter).Assembly;
    var resourceName = assembly.GetManifestResourceNames()
        .FirstOrDefault(n => n.EndsWith("animation-controller.js", StringComparison.Ordinal));
    Assert.IsNotNull(resourceName);
}

[TestMethod]
public void CommandDispatcherModule_IsEmbeddedResource()
{
    var assembly = typeof(Ft100SceneExporter).Assembly;
    var resourceName = assembly.GetManifestResourceNames()
        .FirstOrDefault(n => n.EndsWith("command-dispatcher.js", StringComparison.Ordinal));
    Assert.IsNotNull(resourceName);
    using var stream = assembly.GetManifestResourceStream(resourceName);
    using var reader = new StreamReader(stream!);
    var content = reader.ReadToEnd();
    Assert.IsTrue(content.Contains("WriteTag"), "Must handle WriteTag");
    Assert.IsTrue(content.Contains("Navigate"), "Must handle Navigate");
    Assert.IsTrue(content.Contains("Toggle"), "Must handle Toggle write mode");
    Assert.IsTrue(content.Contains("confirmation"), "Must handle confirmation");
}

[TestMethod]
public void TagBridgeModule_IsEmbeddedResource()
{
    var assembly = typeof(Ft100SceneExporter).Assembly;
    var resourceName = assembly.GetManifestResourceNames()
        .FirstOrDefault(n => n.EndsWith("tag-bridge.js", StringComparison.Ordinal));
    Assert.IsNotNull(resourceName);
    using var stream = assembly.GetManifestResourceStream(resourceName);
    using var reader = new StreamReader(stream!);
    var content = reader.ReadToEnd();
    Assert.IsTrue(content.Contains("getTagValue"), "Must expose getTagValue");
    Assert.IsTrue(content.Contains("writeTag"), "Must expose writeTag");
    Assert.IsTrue(content.Contains("tf100webScadaBuilder"), "Must bridge to host");
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~RuntimeJsModulesTests"`
Expected: 3 FAIL.

- [ ] **Step 3: Write animation-controller.js**

```javascript
// src/ScadaBuilderV2.Rendering/Runtime/animation-controller.js
// Manages CSS animation classes on Element+ wrappers.
// One animation per state (spec D7). Classes are page-scoped via keyframe names.

(function () {
  var controller = {};
  var ANIM_CLASSES = ['scada-anim-blink', 'scada-anim-pulse', 'scada-anim-halo', 'scada-anim-spin'];

  controller.set = function (element, animation) {
    ANIM_CLASSES.forEach(function (c) { element.classList.remove(c); });
    if (animation && animation !== 'None') {
      element.classList.add('scada-anim-' + animation.toLowerCase());
    }
  };

  controller.clear = function (element) {
    ANIM_CLASSES.forEach(function (c) { element.classList.remove(c); });
  };

  window.ScadaRuntime = window.ScadaRuntime || {};
  window.ScadaRuntime.AnimationController = controller;
})();
```

- [ ] **Step 4: Write command-dispatcher.js**

```javascript
// src/ScadaBuilderV2.Rendering/Runtime/command-dispatcher.js
// Binds operator triggers to Element+ commands and executes them.
// Contracts: docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md §5.

(function () {
  var dispatcher = {};

  var TRIGGER_MAP = {
    OnClick: 'click',
    OnRelease: 'mouseup',
    OnHover: 'mouseenter',
    OnHoverEnter: 'mouseenter',
    OnHoverExit: 'mouseleave'
  };

  function configFor(element) {
    try {
      return JSON.parse(element.getAttribute('data-scada-command-config') || 'null');
    } catch (e) {
      return null;
    }
  }

  dispatcher.bind = function (element) {
    var config = configFor(element);
    if (!config || !config.commands) return;

    config.commands.forEach(function (cmd) {
      if (!cmd.enabled) return;
      var eventName = TRIGGER_MAP[cmd.trigger];
      if (!eventName) return;

      element.addEventListener(eventName, function (e) {
        // Momentary: handle press/release pair
        if (cmd.kind === 'WriteTag' && cmd.writeMode === 'Momentary') {
          if (eventName === 'mouseup' || eventName === 'click') {
            dispatcher.execute(element, cmd, 'release');
          } else {
            dispatcher.execute(element, cmd, 'press');
          }
          return;
        }
        dispatcher.execute(element, cmd, 'single');
      });
    });
  };

  dispatcher.execute = function (element, command, phase) {
    if (command.confirmation && command.confirmation.message && phase !== 'release') {
      window.ScadaRuntime.showConfirmation(command.confirmation.message, function () {
        dispatcher._run(element, command, phase);
      });
      return;
    }

    dispatcher._run(element, command, phase);
  };

  dispatcher._run = function (element, command, phase) {
    var bridge = window.ScadaRuntime.TagBridge;
    switch (command.kind) {
      case 'WriteTag':
        if (command.writeMode === 'Momentary') {
          var val = phase === 'release' ? command.offValue : command.onValue;
          bridge.writeTag(command.writeTagId, val, { phase: phase });
        } else if (command.writeMode === 'Toggle') {
          var current = bridge.getTagValue(command.readTagId || command.writeTagId);
          var boolVal = !!(current && current !== '0' && current !== 'false');
          bridge.writeTag(command.writeTagId, boolVal ? '0' : '1', { mode: 'Toggle' });
        } else if (command.writeMode === 'SetFixed') {
          bridge.writeTag(command.writeTagId, command.fixedValue, { mode: 'SetFixed' });
        } else if (command.writeMode === 'SetFromInput') {
          var input = element.querySelector('input');
          if (input) bridge.writeTag(command.writeTagId, input.value, { mode: 'SetFromInput' });
        }
        break;
      case 'Navigate':
        window.postMessage({ source: 'scada-builder-v2', action: 'navigate', pageId: command.targetPageId }, '*');
        break;
      case 'OpenPopup':
        window.postMessage({ source: 'scada-builder-v2', action: 'openPopup', pageId: command.targetPageId, options: command.popupOptions }, '*');
        break;
      case 'ClosePopup':
        window.postMessage({ source: 'scada-builder-v2', action: 'closePopup', pageId: command.targetPageId }, '*');
        break;
      case 'TogglePopup':
        window.postMessage({ source: 'scada-builder-v2', action: 'togglePopup', pageId: command.targetPageId, options: command.popupOptions }, '*');
        break;
      case 'OpenUrl':
        window.open(command.url, command.newTab ? '_blank' : '_self');
        break;
      case 'Back':
        window.history.back();
        break;
    }
  };

  window.ScadaRuntime = window.ScadaRuntime || {};
  window.ScadaRuntime.CommandDispatcher = dispatcher;
})();
```

- [ ] **Step 5: Write tag-bridge.js**

```javascript
// src/ScadaBuilderV2.Rendering/Runtime/tag-bridge.js
// Tag read/write bridge. Prefers host (tf100webScadaBuilder), falls back to local cache.
// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §2.

(function () {
  var bridge = {};
  var localValues = {};

  bridge.getTagValue = function (tagId) {
    if (!tagId) return null;
    // Prefer host bridge
    if (window.tf100webScadaBuilder && typeof window.tf100webScadaBuilder.getTagValue === 'function') {
      return window.tf100webScadaBuilder.getTagValue(tagId);
    }
    // Fallback: local cache
    if (Object.prototype.hasOwnProperty.call(localValues, tagId)) {
      return localValues[tagId];
    }
    return null;
  };

  bridge.writeTag = function (tagId, value, payload) {
    if (!tagId) return;
    if (window.tf100webScadaBuilder && typeof window.tf100webScadaBuilder.writeTag === 'function') {
      window.tf100webScadaBuilder.writeTag(tagId, value, payload);
    }
    // Also update local cache
    localValues[tagId] = value;
  };

  bridge.setTagValue = function (tagId, value) {
    localValues[tagId] = value;
  };

  bridge.setValues = function (values) {
    Object.keys(values).forEach(function (k) { localValues[k] = values[k]; });
  };

  window.ScadaRuntime = window.ScadaRuntime || {};
  window.ScadaRuntime.TagBridge = bridge;
})();
```

- [ ] **Step 6: Register in .csproj**

```xml
<EmbeddedResource Include="Runtime\animation-controller.js" />
<EmbeddedResource Include="Runtime\command-dispatcher.js" />
<EmbeddedResource Include="Runtime\tag-bridge.js" />
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~RuntimeJsModulesTests"`
Expected: all PASS.

- [ ] **Step 8: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Runtime/animation-controller.js
git add src/ScadaBuilderV2.Rendering/Runtime/command-dispatcher.js
git add src/ScadaBuilderV2.Rendering/Runtime/tag-bridge.js
git add src/ScadaBuilderV2.Rendering/ScadaBuilderV2.Rendering.csproj
git add tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs
git commit -m "feat: add animation controller, command dispatcher, and tag bridge JS modules"
```

---

### Task 4: Input edit guard + Confirmation modal JS modules

**Files:**
- Create: `src/ScadaBuilderV2.Rendering/Runtime/input-edit-guard.js`
- Create: `src/ScadaBuilderV2.Rendering/Runtime/confirmation-modal.js`
- Modify: `tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs`

**Interfaces:**
- Produces: `window.ScadaRuntime.InputEditGuard = { watch(element), lock(elementId, inputEl), release(elementId), isLocked(elementId) }` — 30s edit protection.
- Produces: `window.ScadaRuntime.showConfirmation(message, onAccepted)` — modal dialog.

- [ ] **Step 1: Write failing tests**

```csharp
[TestMethod]
public void InputEditGuardModule_IsEmbeddedResource()
{
    var assembly = typeof(Ft100SceneExporter).Assembly;
    var resourceName = assembly.GetManifestResourceNames()
        .FirstOrDefault(n => n.EndsWith("input-edit-guard.js", StringComparison.Ordinal));
    Assert.IsNotNull(resourceName);
    using var stream = assembly.GetManifestResourceStream(resourceName);
    using var reader = new StreamReader(stream!);
    var content = reader.ReadToEnd();
    Assert.IsTrue(content.Contains("30000"), "Must have 30s timeout");
    Assert.IsTrue(content.Contains("scada-input-edit-overlay"), "Must create backshadow overlay");
}

[TestMethod]
public void ConfirmationModalModule_IsEmbeddedResource()
{
    var assembly = typeof(Ft100SceneExporter).Assembly;
    var resourceName = assembly.GetManifestResourceNames()
        .FirstOrDefault(n => n.EndsWith("confirmation-modal.js", StringComparison.Ordinal));
    Assert.IsNotNull(resourceName);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~RuntimeJsModulesTests"`
Expected: 2 FAIL.

- [ ] **Step 3: Write input-edit-guard.js**

```javascript
// src/ScadaBuilderV2.Rendering/Runtime/input-edit-guard.js
// Protects input editing from polling overwrites. 30s timeout, backshadow overlay.
// Design: docs/superpowers/specs/2026-07-07-scada-export-runtime-tf100web-integration-design.md §5.8

(function () {
  var guard = {};
  var locks = {};
  var EDIT_TIMEOUT = 30000;

  guard.watch = function (element) {
    var input = element.matches('input, textarea, select')
      ? element
      : element.querySelector('input, textarea, select');
    if (!input) return;
    var elementId = element.getAttribute('data-scada-element-id') || element.id;

    input.addEventListener('focus', function () {
      guard.lock(elementId, input);
    });

    ['blur', 'change'].forEach(function (evt) {
      input.addEventListener(evt, function () {
        guard.release(elementId);
      });
    });

    input.addEventListener('keydown', function (e) {
      if (e.key === 'Enter' || e.key === 'Tab') {
        guard.release(elementId);
      }
    });
  };

  guard.lock = function (elementId, inputEl) {
    // Pause state engine for this element
    if (window.ScadaRuntime.StateEngine) {
      window.ScadaRuntime.StateEngine.pauseElement(elementId);
    }

    // Create backshadow overlay
    var parent = inputEl.parentElement;
    if (parent && getComputedStyle(parent).position === 'static') {
      parent.style.position = 'relative';
    }
    var overlay = document.createElement('div');
    overlay.className = 'scada-input-edit-overlay';
    overlay.id = 'scada-edit-overlay-' + elementId;
    if (parent) parent.appendChild(overlay);

    var timer = setTimeout(function () {
      guard.release(elementId);
      // Refresh input with latest tag value
      if (window.ScadaRuntime.TagBridge && inputEl.dataset.scadaReadTag) {
        var val = window.ScadaRuntime.TagBridge.getTagValue(inputEl.dataset.scadaReadTag);
        if (val !== null && val !== undefined) inputEl.value = val;
      }
      inputEl.blur();
    }, EDIT_TIMEOUT);

    locks[elementId] = { timer: timer, overlay: overlay, input: inputEl };
  };

  guard.release = function (elementId) {
    var lock = locks[elementId];
    if (!lock) return;
    clearTimeout(lock.timer);
    if (lock.overlay && lock.overlay.parentElement) lock.overlay.remove();
    if (window.ScadaRuntime.StateEngine) {
      window.ScadaRuntime.StateEngine.resumeElement(elementId);
    }
    delete locks[elementId];
  };

  guard.isLocked = function (elementId) {
    return !!locks[elementId];
  };

  window.ScadaRuntime = window.ScadaRuntime || {};
  window.ScadaRuntime.InputEditGuard = guard;
})();
```

- [ ] **Step 4: Write confirmation-modal.js**

```javascript
// src/ScadaBuilderV2.Rendering/Runtime/confirmation-modal.js
// Operator confirmation dialog before executing a command.
// Design: docs/superpowers/specs/2026-07-07-scada-export-runtime-tf100web-integration-design.md §5.5

(function () {
  function showConfirmation(message, onAccepted) {
    var existing = document.querySelector('.scada-confirm-overlay');
    if (existing) existing.remove();

    var overlay = document.createElement('div');
    overlay.className = 'scada-confirm-overlay';
    overlay.style.cssText = 'position:fixed;inset:0;z-index:99999;background:rgba(15,42,48,0.42);'
      + 'display:flex;align-items:center;justify-content:center;';

    var dialog = document.createElement('div');
    dialog.style.cssText = 'background:#fff;padding:24px 32px;border-radius:8px;'
      + 'box-shadow:0 8px 32px rgba(15,42,48,0.28);max-width:420px;text-align:center;'
      + 'font-family:Segoe UI,Arial,sans-serif;';

    var msg = document.createElement('p');
    msg.textContent = message;
    msg.style.cssText = 'font-size:16px;color:#0f2a30;margin:0 0 20px 0;';

    var btnRow = document.createElement('div');
    btnRow.style.cssText = 'display:flex;gap:12px;justify-content:center;';

    var cancelBtn = document.createElement('button');
    cancelBtn.textContent = 'Annuler';
    cancelBtn.style.cssText = 'padding:8px 20px;border:1px solid #c0c8cc;border-radius:6px;'
      + 'background:#f7f9fa;color:#0f2a30;font-size:14px;cursor:pointer;';
    cancelBtn.onclick = function () { overlay.remove(); };

    var confirmBtn = document.createElement('button');
    confirmBtn.textContent = 'Confirmer';
    confirmBtn.style.cssText = 'padding:8px 20px;border:0;border-radius:6px;'
      + 'background:#0f2a30;color:#fff;font-size:14px;cursor:pointer;font-weight:600;';
    confirmBtn.onclick = function () {
      overlay.remove();
      if (onAccepted) onAccepted();
    };

    btnRow.appendChild(cancelBtn);
    btnRow.appendChild(confirmBtn);
    dialog.appendChild(msg);
    dialog.appendChild(btnRow);
    overlay.appendChild(dialog);
    document.body.appendChild(overlay);

    confirmBtn.focus();
  }

  window.ScadaRuntime = window.ScadaRuntime || {};
  window.ScadaRuntime.showConfirmation = showConfirmation;
})();
```

- [ ] **Step 5: Register in .csproj**

```xml
<EmbeddedResource Include="Runtime\input-edit-guard.js" />
<EmbeddedResource Include="Runtime\confirmation-modal.js" />
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~RuntimeJsModulesTests"`
Expected: all PASS.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Runtime/input-edit-guard.js
git add src/ScadaBuilderV2.Rendering/Runtime/confirmation-modal.js
git add src/ScadaBuilderV2.Rendering/ScadaBuilderV2.Rendering.csproj
git add tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs
git commit -m "feat: add input edit guard and confirmation modal JS modules"
```

---

### Task 5: Runtime entry point — concatenation + namespace + lifecycle

**Files:**
- Create: `src/ScadaBuilderV2.Rendering/Runtime/scada-runtime.js`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs` — add `GetRuntimeScript()` method
- Modify: `tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs`

**Interfaces:**
- Produces: `Ft100SceneExporter.GetRuntimeScript()` → `string` — returns concatenated + versioned runtime JS.
- Produces (JS): `window.ScadaRuntime.initPage(container, pageId)` — called by host after fragment injection.
- Produces (JS): `window.ScadaRuntime.version` — version string for cache-busting.

- [ ] **Step 1: Write failing test for GetRuntimeScript**

```csharp
[TestMethod]
public void GetRuntimeScript_ReturnsConcatenatedModules()
{
    var script = Ft100SceneExporter.GetRuntimeScript();

    Assert.IsFalse(string.IsNullOrWhiteSpace(script), "Runtime script must not be empty");
    Assert.IsTrue(script.Contains("ScadaRuntime.ExpressionEvaluator"), "Must include evaluator");
    Assert.IsTrue(script.Contains("ScadaRuntime.StateEngine"), "Must include state engine");
    Assert.IsTrue(script.Contains("ScadaRuntime.EffectApplier"), "Must include effect applier");
    Assert.IsTrue(script.Contains("ScadaRuntime.AnimationController"), "Must include animation controller");
    Assert.IsTrue(script.Contains("ScadaRuntime.CommandDispatcher"), "Must include command dispatcher");
    Assert.IsTrue(script.Contains("ScadaRuntime.TagBridge"), "Must include tag bridge");
    Assert.IsTrue(script.Contains("ScadaRuntime.InputEditGuard"), "Must include input edit guard");
    Assert.IsTrue(script.Contains("ScadaRuntime.initPage"), "Must expose initPage lifecycle");
    Assert.IsTrue(script.Contains("ScadaRuntime.version"), "Must expose version");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~GetRuntimeScript_ReturnsConcatenatedModules"`
Expected: FAIL (method not found).

- [ ] **Step 3: Write scada-runtime.js entry point**

```javascript
// src/ScadaBuilderV2.Rendering/Runtime/scada-runtime.js
// SCADA Runtime — shared JS entry point for all exported pages.
// Version: injected at export time by Ft100SceneExporter.
// Contracts: docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md

(function () {
  var runtime = window.ScadaRuntime || {};
  runtime.version = '{{RUNTIME_VERSION}}';

  runtime.initPage = function (container, pageId) {
    if (!container) return;

    // Initialize state engine
    if (runtime.StateEngine) {
      runtime.StateEngine.initPage(container, pageId);
    }

    // Bind commands on all elements with command config
    var cmdElements = container.querySelectorAll('[data-scada-command-config]');
    for (var i = 0; i < cmdElements.length; i++) {
      if (runtime.CommandDispatcher) {
        runtime.CommandDispatcher.bind(cmdElements[i]);
      }
    }

    // Watch inputs for edit protection
    var inputs = container.querySelectorAll('input, textarea, select');
    for (var j = 0; j < inputs.length; j++) {
      if (runtime.InputEditGuard) {
        runtime.InputEditGuard.watch(inputs[j]);
      }
    }

    // Dispatch page-ready event
    window.dispatchEvent(new CustomEvent('scada-builder-page-ready', {
      detail: { pageId: pageId || '', rootId: container.id }
    }));
  };

  // Tag value change handler (called by host polling)
  runtime.onTagValuesChanged = function (tagValues) {
    if (runtime.TagBridge) runtime.TagBridge.setValues(tagValues);
    var container = document.querySelector('.ft100-scada-scene');
    if (!container || !runtime.StateEngine) return;
    var elements = container.querySelectorAll('[data-scada-state-config]');
    for (var i = 0; i < elements.length; i++) {
      runtime.StateEngine.evaluate(elements[i], tagValues);
    }
  };

  window.ScadaRuntime = runtime;
})();
```

- [ ] **Step 4: Add GetRuntimeScript to Ft100SceneExporter**

In `Ft100SceneExporter.cs`, add:

```csharp
/// <summary>
/// Returns the concatenated SCADA runtime JavaScript from embedded resource modules.
/// </summary>
/// <remarks>
/// Contracts: docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md.
/// Tests: tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs.
/// </remarks>
public static string GetRuntimeScript()
{
    var assembly = typeof(Ft100SceneExporter).Assembly;
    var moduleNames = new[]
    {
        "expression-evaluator.js",
        "effect-applier.js",
        "state-engine.js",
        "animation-controller.js",
        "command-dispatcher.js",
        "tag-bridge.js",
        "input-edit-guard.js",
        "confirmation-modal.js",
        "scada-runtime.js"
    };

    var sb = new StringBuilder();
    sb.AppendLine("// SCADA Builder V2 Runtime — generated at export");
    sb.AppendLine("// Do not edit manually. Source: src/ScadaBuilderV2.Rendering/Runtime/");
    sb.AppendLine();

    foreach (var moduleName in moduleNames)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(moduleName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Runtime module '{moduleName}' not found as embedded resource.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Cannot read runtime module '{moduleName}'.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = reader.ReadToEnd();
        sb.AppendLine(content);
        sb.AppendLine();
    }

    return sb.ToString();
}
```

- [ ] **Step 5: Register scada-runtime.js in .csproj**

```xml
<EmbeddedResource Include="Runtime\scada-runtime.js" />
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~RuntimeJsModulesTests"`
Expected: all PASS.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Runtime/scada-runtime.js
git add src/ScadaBuilderV2.Rendering/ScadaBuilderV2.Rendering.csproj
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs
git add tests/ScadaBuilderV2.Tests/Runtime/RuntimeJsModulesTests.cs
git commit -m "feat: add runtime entry point and GetRuntimeScript exporter method"
```

---

### Task 6: JsonDerivedType on ScadaExprNode + ContentHash helper

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExprNode.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs` — add `ContentHash` method
- Test: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExprNodeTests.cs` (modify existing)
- Test: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs` (modify existing)

**Interfaces:**
- Produces: `ScadaExprNode` hierarchy serializes with polymorphic JSON type discriminators.
- Produces: `Ft100SceneExporter.ContentHash(string filePath)` → `string` — 8-char hex SHA-256.

- [ ] **Step 1: Write failing test for JsonDerivedType serialization**

```csharp
// In tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExprNodeTests.cs, add:

[TestMethod]
public void ExprNode_RoundTripsThroughJsonWithTypeDiscriminator()
{
    var expr = new ScadaExprBinary(
        ScadaExprBinaryOp.GreaterThan,
        new ScadaExprTagRef("Temp"),
        new ScadaExprLiteralNumber(80));

    var json = JsonSerializer.Serialize(expr, new JsonSerializerOptions
    {
        WriteIndent = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    });

    Assert.IsTrue(json.Contains("\"type\":\"binary\""), "Must emit type discriminator for binary node");
    Assert.IsTrue(json.Contains("\"type\":\"tagRef\""), "Must emit type discriminator for tagRef node");
    Assert.IsTrue(json.Contains("\"type\":\"literalNumber\""), "Must emit type discriminator for literalNumber node");

    var deserialized = JsonSerializer.Deserialize<ScadaExprNode>(json, new JsonSerializerOptions
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    });

    Assert.IsInstanceOfType(deserialized, typeof(ScadaExprBinary));
    var binary = (ScadaExprBinary)deserialized!;
    Assert.AreEqual(ScadaExprBinaryOp.GreaterThan, binary.Op);
    Assert.IsInstanceOfType(binary.Left, typeof(ScadaExprTagRef));
    Assert.AreEqual("Temp", ((ScadaExprTagRef)binary.Left).TagName);
    Assert.IsInstanceOfType(binary.Right, typeof(ScadaExprLiteralNumber));
    Assert.AreEqual(80.0, ((ScadaExprLiteralNumber)binary.Right).Value);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ExprNode_RoundTripsThroughJsonWithTypeDiscriminator"`
Expected: FAIL (JSON won't have type discriminator, deserialization may fail).

- [ ] **Step 3: Add JsonDerivedType attributes**

In `ScadaExprNode.cs`, add before `public abstract record ScadaExprNode`:

```csharp
using System.Text.Json.Serialization;

[JsonDerivedType(typeof(ScadaExprLiteralNumber), "literalNumber")]
[JsonDerivedType(typeof(ScadaExprLiteralBool), "literalBool")]
[JsonDerivedType(typeof(ScadaExprLiteralString), "literalString")]
[JsonDerivedType(typeof(ScadaExprTagRef), "tagRef")]
[JsonDerivedType(typeof(ScadaExprUnary), "unary")]
[JsonDerivedType(typeof(ScadaExprBinary), "binary")]
[JsonDerivedType(typeof(ScadaExprFunc), "func")]
public abstract record ScadaExprNode;
```

- [ ] **Step 4: Write failing test for ContentHash**

```csharp
// In tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs, add:

[TestMethod]
public void ContentHash_Returns8CharHexLowercase()
{
    var tmpFile = Path.GetTempFileName();
    try
    {
        File.WriteAllText(tmpFile, "test content for hashing");
        var hash = Ft100SceneExporter.ContentHash(tmpFile);

        Assert.AreEqual(8, hash.Length, "Hash must be exactly 8 characters");
        Assert.IsTrue(hash.All(c => char.IsAsciiHexDigit(c)),
            "Hash must contain only hex digits");
        Assert.IsTrue(hash.All(c => !char.IsUpper(c) || char.IsDigit(c)),
            "Hash must be lowercase");
    }
    finally
    {
        if (File.Exists(tmpFile)) File.Delete(tmpFile);
    }
}

[TestMethod]
public void ContentHash_SameContent_ProducesSameHash()
{
    var tmp1 = Path.GetTempFileName();
    var tmp2 = Path.GetTempFileName();
    try
    {
        File.WriteAllText(tmp1, "same content");
        File.WriteAllText(tmp2, "same content");

        Assert.AreEqual(
            Ft100SceneExporter.ContentHash(tmp1),
            Ft100SceneExporter.ContentHash(tmp2));
    }
    finally
    {
        if (File.Exists(tmp1)) File.Delete(tmp1);
        if (File.Exists(tmp2)) File.Delete(tmp2);
    }
}
```

- [ ] **Step 5: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ContentHash"`
Expected: FAIL (method not found).

- [ ] **Step 6: Add ContentHash to Ft100SceneExporter**

```csharp
/// <summary>
/// Returns the first 8 lowercase hex characters of a file's SHA-256 hash,
/// used for immutable cache-busting filenames on exported assets.
/// </summary>
public static string ContentHash(string filePath)
{
    using var sha = System.Security.Cryptography.SHA256.Create();
    var bytes = File.ReadAllBytes(filePath);
    var hash = sha.ComputeHash(bytes);
    return Convert.ToHexString(hash)[..8].ToLowerInvariant();
}
```

- [ ] **Step 7: Run all tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ExprNode_RoundTripsThroughJsonWithTypeDiscriminator|FullyQualifiedName~ContentHash"`
Expected: all PASS.

- [ ] **Step 8: Commit**

```bash
git add src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExprNode.cs
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs
git add tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExprNodeTests.cs
git add tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "feat: add JsonDerivedType to AST nodes and ContentHash helper"
```

---

### Task 7: ExportSharedRuntime + asset hash renaming in export

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs` — `ExportProjectAsync` calls `ExportSharedRuntime`, `CopyAndRewriteImageAssets` renames with hash, CSS path uses hash
- Test: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

**Interfaces:**
- Produces: `ExportSharedRuntime(string exportDirectory)` → `string` (hash)
- Modifies: `BuildHtml` emits `<script src="../scada-runtime.<hash>.js" defer>` instead of inline script
- Modifies: `CopyAndRewriteImageAssets` renames destination files with content hash

- [ ] **Step 1: Write failing test for ExportSharedRuntime**

```csharp
[TestMethod]
public async Task ExportProjectAsync_WritesSharedRuntimeFile()
{
    var project = CreateValidProject("runtime-test-project");
    var scene = CreateValidScene("page1", "Page 1", ScadaPageType.Default);
    project = project.WithScene(scene);
    var tmpDir = Path.Combine(Path.GetTempPath(), "scada-test-runtime-" + Guid.NewGuid().ToString("N"));
    var sourceHtml = Path.Combine(tmpDir, "page1_source.html");
    Directory.CreateDirectory(tmpDir);
    File.WriteAllText(sourceHtml,
        "<!doctype html><html><body><div class=\"page\"><div id=\"ft100-page1\">content</div></div></body></html>");

    try
    {
        var exporter = new Ft100SceneExporter();
        var input = new Ft100ProjectPageExportInput(scene, sourceHtml);
        await exporter.ExportProjectAsync(project, new[] { input }, tmpDir);

        // Verify runtime JS exists at package root
        var runtimeFiles = Directory.GetFiles(
            Path.Combine(tmpDir, Ft100SceneExporter.ProjectPackageDirectoryName),
            "scada-runtime.*.js");
        Assert.AreEqual(1, runtimeFiles.Length, "Must export exactly one scada-runtime.<hash>.js file");

        var runtimeFile = runtimeFiles[0];
        var runtimeContent = File.ReadAllText(runtimeFile);
        Assert.IsTrue(runtimeContent.Contains("ScadaRuntime.initPage"),
            "Runtime must contain initPage lifecycle");

        // Verify HTML references the runtime
        var pageHtmlPath = Path.Combine(
            tmpDir, Ft100SceneExporter.ProjectPackageDirectoryName, "page1", "page1.html");
        Assert.IsTrue(File.Exists(pageHtmlPath), "Page HTML must exist");
        var pageHtml = File.ReadAllText(pageHtmlPath);
        var runtimeFileName = Path.GetFileName(runtimeFile);
        Assert.IsTrue(pageHtml.Contains($"../{runtimeFileName}"),
            $"Page HTML must reference runtime as ../{runtimeFileName}");
        Assert.IsTrue(pageHtml.Contains("<script src="),
            "Page HTML must use <script src> for runtime");
    }
    finally
    {
        if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ExportProjectAsync_WritesSharedRuntimeFile"`
Expected: FAIL (no runtime file written, HTML still has inline script).

- [ ] **Step 3: Add ExportSharedRuntime method**

```csharp
/// <summary>
/// Writes the concatenated runtime JS to the package root, versioned by content hash.
/// Returns the content hash so pages can reference the correct filename.
/// </summary>
private static string ExportSharedRuntime(string packageDirectory)
{
    var script = GetRuntimeScript();
    var version = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    script = script.Replace("{{RUNTIME_VERSION}}", version);

    // Write temp file to compute hash
    var tmpPath = Path.GetTempFileName();
    try
    {
        File.WriteAllText(tmpPath, script, Encoding.UTF8);
        var hash = ContentHash(tmpPath);
        var runtimeFileName = $"scada-runtime.{hash}.js";
        var runtimePath = Path.Combine(packageDirectory, runtimeFileName);
        File.WriteAllText(runtimePath, script, Encoding.UTF8);
        return hash;
    }
    finally
    {
        if (File.Exists(tmpPath)) File.Delete(tmpPath);
    }
}
```

- [ ] **Step 4: Update ExportProjectAsync to call ExportSharedRuntime**

In `ExportProjectAsync`, after `RecreateProjectPackageDirectory`, add:

```csharp
var runtimeHash = ExportSharedRuntime(packageDirectory);
```

- [ ] **Step 5: Update BuildHtml to use <script src>**

Replace the inline script block in `BuildHtml` (the `{runtimeScript}` variable and its injection) with:

```csharp
var runtimeSrc = $"../scada-runtime.{runtimeHash}.js";
```

And in the HTML template, replace:

```html
  <script>
{{Indent(runtimeScript, 4)}}
  </script>
```

With:

```html
  <script src="{{runtimeSrc}}" defer></script>
```

The `BuildHtml` signature changes to accept `runtimeHash`:

```csharp
private static string BuildHtml(ScadaScene scene, string cssFileName, string sourceContent, string runtimeHash)
```

And `ExportAsync` passes it through from the project-level export context. For per-scene exports (standalone), compute the hash on the fly:

```csharp
// In ExportAsync, if runtimeHash is not provided (standalone export):
var runtimeHash = ExportSharedRuntime(exportDirectory);
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ExportProjectAsync_WritesSharedRuntimeFile"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs
git add tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "feat: export shared runtime JS and reference via <script src> in HTML"
```

---

### Task 8: StateCommand data attributes + manifest serialization

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs` — `BuildElementHtml`, `BuildManifestPage`
- Test: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

**Interfaces:**
- Produces: `BuildStateCommandAttributes(ScadaElement)` → `string` — HTML attributes for state/command config.
- Modifies: `BuildManifestPage` Objects[] includes `StateConfig` and `CommandConfig`.

- [ ] **Step 1: Write failing test**

```csharp
[TestMethod]
public async Task ExportAsync_IncludesStateConfigInManifestAndHtml()
{
    var scene = CreateValidScene("state-page", "State Page", ScadaPageType.Default);
    var stateConfig = new ScadaElementStateConfig(
        QualityFallback: ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
        DefaultEffect: ScadaEffectBlock.Empty,
        States: new[] {
            new ScadaStateRule("s1", "Alarme", true,
                new ScadaExpression("{T1}>80",
                    new ScadaExprBinary(ScadaExprBinaryOp.GreaterThan,
                        new ScadaExprTagRef("T1"), new ScadaExprLiteralNumber(80)),
                    new[] { "T1" }),
                ScadaEffectBlock.Empty with { BackgroundColor = "#E53935", Animation = ScadaAnimation.Blink })
        });

    var element = new ScadaElement("el1", "Pompe", ScadaElementKind.Button,
        new SceneBounds(10, 20, 100, 80),
        stateConfig: stateConfig);

    scene = scene.WithElement(element);
    var project = CreateValidProject("test-project").WithScene(scene);
    var tmpDir = Path.Combine(Path.GetTempPath(), "scada-test-sc-" + Guid.NewGuid().ToString("N"));
    var sourceHtml = Path.Combine(tmpDir, "page.html");
    Directory.CreateDirectory(tmpDir);
    File.WriteAllText(sourceHtml,
        "<!doctype html><html><body><div class=\"page\"><div id=\"ft100-state-page\">x</div></div></body></html>");

    try
    {
        var exporter = new Ft100SceneExporter();
        await exporter.ExportAsync(scene, sourceHtml, tmpDir, project);

        // Check manifest
        var manifestPath = Path.Combine(tmpDir, "state-page", "manifest.json");
        var manifestJson = File.ReadAllText(manifestPath);
        Assert.IsTrue(manifestJson.Contains("StateConfig"), "Manifest must contain StateConfig");
        Assert.IsTrue(manifestJson.Contains("\"backgroundColor\":\"#E53935\""),
            "Must serialize effect background color");
        Assert.IsTrue(manifestJson.Contains("\"animation\":\"Blink\""),
            "Must serialize animation enum");
        Assert.IsTrue(manifestJson.Contains("\"type\":\"binary\""),
            "Must serialize AST type discriminator");

        // Check HTML
        var htmlPath = Path.Combine(tmpDir, "state-page", "state-page.html");
        var html = File.ReadAllText(htmlPath);
        Assert.IsTrue(html.Contains("data-scada-state-config"),
            "HTML must contain data-scada-state-config attribute");
        Assert.IsTrue(html.Contains("\"backgroundColor\":\"#E53935\""),
            "HTML data attribute must contain effect data");
    }
    finally
    {
        if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ExportAsync_IncludesStateConfigInManifestAndHtml"`
Expected: FAIL (StateConfig not in manifest/HTML).

- [ ] **Step 3: Add BuildStateCommandAttributes**

```csharp
private static string BuildStateCommandAttributes(ScadaElement element)
{
    var attributes = new StringBuilder();
    var stateConfig = element.EffectiveStateConfig;

    // Only emit if non-default (has states or non-default fallback)
    var hasCustomFallback = stateConfig.QualityFallback.Opacity != 0.4
        || stateConfig.QualityFallback.BorderColor != "#000000"
        || stateConfig.QualityFallback.BorderWidth != 2;
    if (stateConfig.States.Count > 0 || hasCustomFallback)
    {
        attributes.Append(" data-scada-state-config=\"");
        attributes.Append(HtmlEncoder.Default.Encode(
            JsonSerializer.Serialize(stateConfig, ManifestJsonOptions)));
        attributes.Append('"');
    }

    var commandConfig = element.EffectiveCommandConfig;
    if (commandConfig.Commands.Count > 0)
    {
        attributes.Append(" data-scada-command-config=\"");
        attributes.Append(HtmlEncoder.Default.Encode(
            JsonSerializer.Serialize(commandConfig, ManifestJsonOptions)));
        attributes.Append('"');
    }

    return attributes.ToString();
}
```

- [ ] **Step 4: Add StateConfig/CommandConfig to BuildManifestPage Objects[]**

In the anonymous object in `BuildManifestPage`, after `ValueBindings`, add:

```csharp
StateConfig = element.EffectiveStateConfig.States.Count > 0 || HasNonDefaultFallback(element.EffectiveStateConfig)
    ? element.EffectiveStateConfig : null,
CommandConfig = element.EffectiveCommandConfig.Commands.Count > 0
    ? element.EffectiveCommandConfig : null
```

And add the helper:

```csharp
private static bool HasNonDefaultFallback(ScadaElementStateConfig config)
{
    return config.QualityFallback.Opacity != 0.4
        || config.QualityFallback.BorderColor != "#000000"
        || config.QualityFallback.BorderWidth != 2
        || config.DefaultEffect != ScadaEffectBlock.Empty;
}
```

- [ ] **Step 5: Wire BuildStateCommandAttributes into BuildElementHtml**

In `BuildElementHtml`, after `BuildButtonRuntimeAttributes(element)`, add the call:

```csharp
var stateCommandAttributes = BuildStateCommandAttributes(element);
```

And include it in the div template output alongside the other attributes.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ExportAsync_IncludesStateConfigInManifestAndHtml"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs
git add tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "feat: serialize StateConfig/CommandConfig in manifest and HTML data attributes"
```

---

### Task 9: Animation keyframes in CSS + remove old BuildRuntimeScript

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs` — `BuildCss`, remove `BuildRuntimeScript`
- Test: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

- [ ] **Step 1: Write failing test for keyframes**

```csharp
[TestMethod]
public async Task ExportAsync_IncludesAnimationKeyframesInCss()
{
    var scene = CreateValidScene("anim-test", "Anim Page", ScadaPageType.Default);
    var tmpDir = Path.Combine(Path.GetTempPath(), "scada-test-anim-" + Guid.NewGuid().ToString("N"));
    var sourceHtml = Path.Combine(tmpDir, "page.html");
    Directory.CreateDirectory(tmpDir);
    File.WriteAllText(sourceHtml,
        "<!doctype html><html><body><div class=\"page\"><div id=\"ft100-anim-test\">x</div></div></body></html>");

    try
    {
        var exporter = new Ft100SceneExporter();
        await exporter.ExportAsync(scene, sourceHtml, tmpDir);
        var cssPath = Path.Combine(tmpDir, "anim-test", "css", "anim-test.css");
        var css = File.ReadAllText(cssPath);

        Assert.IsTrue(css.Contains("@keyframes"), "CSS must contain animation keyframes");
        Assert.IsTrue(css.Contains("scada-blink"), "Must define scada-blink animation");
        Assert.IsTrue(css.Contains("scada-pulse"), "Must define scada-pulse animation");
        Assert.IsTrue(css.Contains("scada-halo"), "Must define scada-halo animation");
        Assert.IsTrue(css.Contains("scada-spin"), "Must define scada-spin animation");
        Assert.IsTrue(css.Contains(".scada-anim-blink"), "Must have blink class");
        Assert.IsTrue(css.Contains(".scada-anim-spin"), "Must have spin class");
        // Keyframes must be page-scoped
        Assert.IsTrue(css.Contains("ft100-anim-test---"), "Keyframe names must be page-scoped");
    }
    finally
    {
        if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ExportAsync_IncludesAnimationKeyframesInCss"`
Expected: FAIL.

- [ ] **Step 3: Add keyframes to BuildCss**

In `BuildCss`, before the closing of the method, add:

```csharp
private static void AppendAnimationKeyframes(StringBuilder css, Ft100ExportScope scope)
{
    var animPrefix = scope.AnimationName("scada");
    css.AppendLine($"@keyframes {animPrefix}-blink {{ 0%,100%{{opacity:1}} 50%{{opacity:0.15}} }}");
    css.AppendLine($"@keyframes {animPrefix}-pulse {{ 0%,100%{{transform:scale(1)}} 50%{{transform:scale(1.05)}} }}");
    css.AppendLine($"@keyframes {animPrefix}-halo  {{ 0%,100%{{box-shadow:0 0 2px currentColor}} 50%{{box-shadow:0 0 14px currentColor}} }}");
    css.AppendLine($"@keyframes {animPrefix}-spin  {{ 0%{{transform:rotate(0deg)}} 100%{{transform:rotate(360deg)}} }}");
    css.AppendLine();
    css.AppendLine($".scada-anim-blink {{ animation: {animPrefix}-blink 0.6s step-end infinite; }}");
    css.AppendLine($".scada-anim-pulse {{ animation: {animPrefix}-pulse 1s ease-in-out infinite; }}");
    css.AppendLine($".scada-anim-halo  {{ animation: {animPrefix}-halo 1.8s ease-in-out infinite; }}");
    css.AppendLine($".scada-anim-spin  {{ animation: {animPrefix}-spin 1.2s linear infinite; }}");
}
```

Update `Ft100ExportScope` to add the AnimationName method:

```csharp
public string AnimationName(string name)
{
    return $"{RootDomId}---{CssIdentifier(name)}";
}
```

- [ ] **Step 4: Remove BuildRuntimeScript and its callers**

- Delete `BuildRuntimeScript` method entirely (lines 1560-2219 in current `Ft100SceneExporter.cs`)
- Remove the `actionsJson`, `rootIdJson`, `runtimeScript` variables from `BuildHtml`
- The `BuildHtml` method no longer takes or generates inline script — it uses `<script src>` only

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ExportAsync_IncludesAnimationKeyframesInCss"`
Expected: PASS.

Run full suite to check no regression from removing BuildRuntimeScript:
`dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Ft100SceneExporterTests"`
Expected: all PASS (update any tests that expected inline script).

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs
git add tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "feat: add animation keyframes to CSS, remove inline BuildRuntimeScript"
```

---

### Task 10: Ft100PackageValidation update

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100PackageValidation.cs`
- Test: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
[TestMethod]
public void ValidatePackageDirectory_ErrorsWhenRuntimeJsIsMissing()
{
    var tmpDir = Path.Combine(Path.GetTempPath(), "scada-val-runtime-" + Guid.NewGuid().ToString("N"));
    var pkgDir = Path.Combine(tmpDir, Ft100SceneExporter.ProjectPackageDirectoryName);
    Directory.CreateDirectory(pkgDir);

    // Create a minimal valid manifest
    var manifest = new { Name = "test", ManifestVersion = "2.1", HomePageId = (string?)null,
        Pages = new[] { new { Id = "p1", Name = "P1", IncludeInBuild = true, RelativePath = "p1/p1.html" } },
        Actions = Array.Empty<object>(), Tags = Array.Empty<object>() };
    File.WriteAllText(Path.Combine(pkgDir, "manifest.json"),
        JsonSerializer.Serialize(manifest));

    // Create a valid page directory with HTML
    var pageDir = Path.Combine(pkgDir, "p1");
    Directory.CreateDirectory(pageDir);
    var cssDir = Path.Combine(pageDir, "css");
    Directory.CreateDirectory(cssDir);
    File.WriteAllText(Path.Combine(pageDir, "p1.html"),
        "<!doctype html><html><body><div id=\"ft100-p1\">content</div></body></html>");
    File.WriteAllText(Path.Combine(cssDir, "p1.css"), "");

    try
    {
        var result = Ft100PackageValidator.ValidatePackageDirectory(pkgDir);

        Assert.IsFalse(result.IsValid, "Package without runtime JS must be invalid");
        Assert.IsTrue(result.Errors.Any(e => e.Message.Contains("runtime", StringComparison.OrdinalIgnoreCase)),
            "Error must mention missing runtime JS");
    }
    finally
    {
        if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ValidatePackageDirectory_ErrorsWhenRuntimeJsIsMissing"`
Expected: FAIL (package validates as OK despite no runtime JS).

- [ ] **Step 3: Add runtime validation to Ft100PackageValidator**

In `ValidatePackageDirectory`, after validating the manifest and pages, add:

```csharp
var runtimeFiles = Directory.GetFiles(packageDirectory, "scada-runtime.*.js");
if (runtimeFiles.Length == 0)
{
    errors.Add(new ScadaBuildValidationIssue(
        ScadaBuildValidationSeverity.Error,
        "Missing scada-runtime.<hash>.js at package root. The shared runtime JS file is required for all SCADA Builder V2 packages."));
}
else if (runtimeFiles.Length > 1)
{
    errors.Add(new ScadaBuildValidationIssue(
        ScadaBuildValidationSeverity.Error,
        "Multiple scada-runtime.<hash>.js files found. Exactly one runtime file is required at the package root."));
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ValidatePackageDirectory_ErrorsWhenRuntimeJsIsMissing"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Ft100PackageValidation.cs
git add tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "feat: validate presence of scada-runtime JS in package"
```

---

### Task 11: deploy_scada_builder Django management command

**Files:**
- Create: `F:\Projet\Git\TF100Web\core\management\commands\deploy_scada_builder.py`
- Test: `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py` (add test class)

**Interfaces:**
- Produces: `python manage.py deploy_scada_builder <path/to/package.sb2>` — extracts .sb2, copies files to templates/static, runs collectstatic.

- [ ] **Step 1: Write the management command**

```python
# core/management/commands/deploy_scada_builder.py
"""Deploy a SCADA Builder V2 .sb2 package into templates/ and static/ directories.

Usage:
    python manage.py deploy_scada_builder /path/to/export.sb2
"""

import shutil
import tempfile
from pathlib import Path

from django.conf import settings
from django.core.management import BaseCommand, call_command


class Command(BaseCommand):
    help = "Deploie un package SCADA Builder V2 .sb2 dans templates/ et static/"

    def add_arguments(self, parser):
        parser.add_argument("package_path", type=str, help="Chemin vers le fichier .sb2")

    def handle(self, *args, **options):
        sb2_path = Path(options["package_path"]).resolve()
        if not sb2_path.is_file():
            self.stderr.write(self.style.ERROR(f"Fichier introuvable : {sb2_path}"))
            return

        templates_dir = self._resolve_templates_dir()
        static_root = self._resolve_static_root()

        with tempfile.TemporaryDirectory() as staging:
            shutil.unpack_archive(str(sb2_path), staging, "zip")
            pkg_dir = Path(staging) / "scada-builder-v2-ft100-package"
            if not pkg_dir.is_dir():
                self.stderr.write(self.style.ERROR(
                    "Package invalide : scada-builder-v2-ft100-package/ absent du .sb2"))
                return

            copied = {"html": 0, "js": 0, "css": 0, "images": 0}

            # 1. HTML -> templates/frontend/scada/pages/
            for html in pkg_dir.glob("*/*.html"):
                dest = templates_dir / "frontend" / "scada" / "pages" / html.parent.name
                dest.mkdir(parents=True, exist_ok=True)
                shutil.copy2(str(html), str(dest / html.name))
                copied["html"] += 1
                self.stdout.write(f"  HTML: {html.parent.name}/{html.name}")

            # 2. Runtime JS -> static/scada/js/
            js_dest = static_root / "scada" / "js"
            js_dest.mkdir(parents=True, exist_ok=True)
            for js in pkg_dir.glob("scada-runtime.*.js"):
                shutil.copy2(str(js), str(js_dest / js.name))
                copied["js"] += 1
                self.stdout.write(f"  JS:   {js.name}")

            # 3. CSS -> static/scada/css/
            css_dest = static_root / "scada" / "css"
            css_dest.mkdir(parents=True, exist_ok=True)
            for css in pkg_dir.glob("*/css/*.css"):
                shutil.copy2(str(css), str(css_dest / css.name))
                copied["css"] += 1
                self.stdout.write(f"  CSS:  {css.name}")

            # 4. Images -> static/scada/images/
            img_dest = static_root / "scada" / "images"
            img_dest.mkdir(parents=True, exist_ok=True)
            for img in pkg_dir.glob("*/images/*"):
                if img.is_file():
                    shutil.copy2(str(img), str(img_dest / img.name))
                    copied["images"] += 1

            self.stdout.write(
                f"  Images: {copied['images']} fichiers"
            )

        # 5. collectstatic
        self.stdout.write("Lancement de collectstatic...")
        call_command("collectstatic", "--noinput", verbosity=0)

        self.stdout.write(self.style.SUCCESS(
            f"Package SCADA deploye : "
            f"{copied['html']} pages, "
            f"{copied['js']} runtime, "
            f"{copied['css']} CSS, "
            f"{copied['images']} images. "
            f"Redemarre Gunicorn pour recharger les templates."
        ))

    def _resolve_templates_dir(self) -> Path:
        templates_setting = getattr(settings, "TEMPLATES", [])
        if templates_setting:
            dirs = templates_setting[0].get("DIRS", [])
            if dirs:
                return Path(dirs[0])
        # Fallback
        return Path(getattr(settings, "BASE_DIR", ".")) / "templates"

    def _resolve_static_root(self) -> Path:
        return Path(getattr(settings, "STATIC_ROOT", "."))
```

- [ ] **Step 2: Add test**

```python
# In frontend/tests_scada_package.py, add:

class DeployScadaBuilderCommandTests(SimpleTestCase):
    def setUp(self):
        self.temp_dir = tempfile.mkdtemp()
        self.templates_dir = Path(self.temp_dir) / "templates"
        self.static_dir = Path(self.temp_dir) / "static"
        self.templates_dir.mkdir(parents=True)
        self.static_dir.mkdir(parents=True)

    def tearDown(self):
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def _create_minimal_sb2(self) -> Path:
        """Create a minimal valid .sb2 with one page and runtime."""
        pkg_dir = Path(self.temp_dir) / "scada-builder-v2-ft100-package"
        pkg_dir.mkdir(parents=True)

        # manifest.json
        manifest = {
            "Name": "test", "ManifestVersion": "2.1",
            "Pages": [{
                "Id": "win00001", "Name": "Test", "Type": "default",
                "IncludeInBuild": True, "RelativePath": "win00001/win00001.html",
                "Width": 800, "Height": 600
            }],
            "Actions": [], "Tags": []
        }
        (pkg_dir / "manifest.json").write_text(json.dumps(manifest))

        # Page HTML
        page_dir = pkg_dir / "win00001"
        page_dir.mkdir()
        (page_dir / "win00001.html").write_text(
            '<!doctype html><html><head>'
            '<link rel="stylesheet" href="css/win00001.a1b2c3d.css">'
            '<script src="../scada-runtime.d4e5f6g.js" defer></script>'
            '</head><body><div id="ft100-win00001">content</div></body></html>'
        )

        # CSS
        css_dir = page_dir / "css"
        css_dir.mkdir()
        (css_dir / "win00001.a1b2c3d.css").write_text("/* css */")

        # Runtime
        (pkg_dir / "scada-runtime.d4e5f6g.js").write_text(
            'window.ScadaRuntime={version:"test",initPage:function(){}};'
        )

        # Images
        img_dir = page_dir / "images"
        img_dir.mkdir()
        (img_dir / "icon.svg").write_text("<svg></svg>")

        # Package as .sb2
        sb2_path = Path(self.temp_dir) / "test.sb2"
        with zipfile.ZipFile(str(sb2_path), "w", zipfile.ZIP_DEFLATED) as zf:
            for f in pkg_dir.rglob("*"):
                if f.is_file():
                    zf.write(f, f.relative_to(self.temp_dir))

        return sb2_path

    @override_settings(STATIC_ROOT=None)
    def test_command_copies_files_to_templates_and_static(self):
        sb2 = self._create_minimal_sb2()

        with override_settings(
            TEMPLATES=[{"DIRS": [str(self.templates_dir)], "BACKEND": "django.template.backends.django.DjangoTemplates"}],
            STATIC_ROOT=str(self.static_dir),
        ):
            call_command("deploy_scada_builder", str(sb2))

        # Check HTML in templates
        page_html = self.templates_dir / "frontend" / "scada" / "pages" / "win00001" / "win00001.html"
        self.assertTrue(page_html.is_file(), f"Page HTML missing at {page_html}")

        # Check runtime JS in static
        runtime_js = self.static_dir / "scada" / "js" / "scada-runtime.d4e5f6g.js"
        self.assertTrue(runtime_js.is_file(), f"Runtime JS missing at {runtime_js}")

        # Check CSS in static
        css = self.static_dir / "scada" / "css" / "win00001.a1b2c3d.css"
        self.assertTrue(css.is_file(), f"CSS missing at {css}")

        # Check image in static
        img = self.static_dir / "scada" / "images" / "icon.svg"
        self.assertTrue(img.is_file(), f"Image missing at {img}")
```

- [ ] **Step 3: Run test to verify it works**

Run: `cd F:\Projet\Git\TF100Web && python manage.py test frontend.tests_scada_package.DeployScadaBuilderCommandTests -v 2`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
cd F:/Projet/Git/TF100Web
git add core/management/commands/deploy_scada_builder.py
git add frontend/tests_scada_package.py
git commit -m "feat: add deploy_scada_builder management command"
```

---

### Task 12: Django views + URLs + visualisation.html

**Files:**
- Modify: `F:\Projet\Git\TF100Web\frontend\views.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\urls.py`
- Modify: `F:\Projet\Git\TF100Web\templates\frontend\station\visualisation.html`

- [ ] **Step 1: Add view functions to views.py**

```python
# Add at end of frontend/views.py

@login_required
def scada_page_view(request, page_id: str):
    """Charge une page SCADA Builder V2 complète (HTML standalone)."""
    if not getattr(settings, "TF100_INDUSTRIAL_DEPLOYMENT", False):
        raise Http404
    config = StationConfig.objects.filter(pk=1).first()
    if config is None or config.station_type != StationConfig.StationTypeChoices.SCADA_BUILDER_2:
        raise Http404

    template_path = f"frontend/scada/pages/{page_id}/{page_id}.html"
    try:
        get_template(template_path)
    except TemplateDoesNotExist:
        raise Http404

    return render(request, template_path, {
        "page_id": page_id,
        "is_scada_page": True,
    })


@login_required
def scada_page_json(request, page_id: str):
    """Retourne le HTML complet d'une page SCADA pour navigation AJAX."""
    if not getattr(settings, "TF100_INDUSTRIAL_DEPLOYMENT", False):
        raise Http404

    template_path = f"frontend/scada/pages/{page_id}/{page_id}.html"
    try:
        template = get_template(template_path)
        html = template.render({}, request)
    except TemplateDoesNotExist:
        raise Http404

    fragment = _extract_html_element_by_id(html, f"ft100-{page_id}")

    return JsonResponse({
        "page_id": page_id,
        "fragment": fragment or "",
        "css_hash": _extract_css_hash_from_html(html),
        "width": _extract_page_dimension_from_html(html, "width"),
        "height": _extract_page_dimension_from_html(html, "height"),
    })


def _extract_css_hash_from_html(html: str) -> str:
    """Extrait le hash du fichier CSS depuis la balise <link>."""
    import re
    match = re.search(
        r'<link\s+[^>]*href="[^"]*/([^/"]+)\.([a-f0-9]{8})\.css"',
        html, re.IGNORECASE
    )
    return match.group(2) if match else ""


def _extract_page_dimension_from_html(html: str, attr: str) -> int:
    """Extrait une dimension depuis data-scada-width/data-scada-height."""
    import re
    match = re.search(
        rf'data-scada-{attr}="(\d+)"', html, re.IGNORECASE
    )
    return int(match.group(1)) if match else 0


def _extract_html_element_by_id(html: str, element_id: str) -> str:
    """Extrait un élément HTML par son ID (div uniquement)."""
    import re
    marker = f'id="{element_id}"'
    start_marker = html.find(marker)
    if start_marker < 0:
        marker = f"id='{element_id}'"
        start_marker = html.find(marker)
    if start_marker < 0:
        return ""

    start = html.rfind("<div", 0, start_marker)
    if start < 0:
        return ""

    tag_re = re.compile(r"</?div\b[^>]*>", re.IGNORECASE)
    depth = 0
    for m in tag_re.finditer(html, start):
        if m.group(0).startswith("</"):
            depth -= 1
            if depth == 0:
                return html[start:m.end()]
        else:
            depth += 1
    return ""
```

**Note:** `_extract_html_element_by_id` is moved from `scada_package.py` (which will be deprecated). Copy the implementation rather than importing from deprecated code.

- [ ] **Step 2: Add URL routes**

```python
# In frontend/urls.py, add:
path("scada/page/<str:page_id>/", scada_page_view, name="scada_page"),
path("scada/api/page/<str:page_id>/", scada_page_json, name="scada_page_json"),
```

And add the import:

```python
from frontend.views import scada_page_view, scada_page_json  # add to existing imports
```

- [ ] **Step 3: Update visualisation.html**

In `templates/frontend/station/visualisation.html`, replace the SCADA_BUILDER_2 section with:

```django
{% if station.station_type == 'SCADA_BUILDER_2' and is_industrial_deployment %}
  <div id="scada-host"
       class="scada-host"
       data-scada-home-page="{{ home_page_id|default:'win00009' }}"
       data-scada-runtime-version="{{ runtime_version|default:'' }}"
       data-mapping-snapshot-url="{% url 'mapping_snapshot' %}"
       data-mapping-write-url="{% url 'mapping_write' %}"
       data-live-refresh-ms="500"
       data-can-write="1">
    {# La première page est chargée par visualisation_import.js #}
  </div>

  {# Charger le runtime SCADA partagé #}
  <script src="{% static 'scada/js/scada-runtime.'|add:runtime_version|add:'.js' %}" defer></script>
{% endif %}
```

- [ ] **Step 4: Run TF100Web test suite**

Run: `cd F:\Projet\Git\TF100Web && python manage.py test frontend -v 2`
Verify no regressions.

- [ ] **Step 5: Commit**

```bash
cd F:/Projet/Git/TF100Web
git add frontend/views.py frontend/urls.py
git add templates/frontend/station/visualisation.html
git commit -m "feat: add SCADA page views, JSON endpoint, and update visualisation template"
```

---

### Task 13: visualisation_import.js — ScadaHost + ScadaTagCache

**Files:**
- Modify: `F:\Projet\Git\TF100Web\static\asset\js\station\visualisation_import.js`

- [ ] **Step 1: Add ScadaHost module**

At the beginning of `visualisation_import.js` (after the existing `"use strict"` block), add:

```javascript
// === SCADA Builder V2 Runtime Host ===
const ScadaHost = {
  currentPageId: null,
  currentCssHashes: new Set(),
  runtimeLoaded: false,
  pendingEditLock: null,
  actionsById: {},

  async init(homePageId) {
    // Start tag polling immediately (values needed by runtime)
    ScadaTagCache.startPolling();

    // Load the runtime script injected by the server
    if (window.ScadaRuntime && window.ScadaRuntime.initPage) {
      this.runtimeLoaded = true;
    } else {
      // Wait for deferred script
      await new Promise((resolve) => {
        const check = () => {
          if (window.ScadaRuntime && window.ScadaRuntime.initPage) {
            resolve();
          } else {
            setTimeout(check, 50);
          }
        };
        check();
      });
      this.runtimeLoaded = true;
    }

    // Wire tag change events to runtime
    window.addEventListener('scada-tag-values-changed', (e) => {
      if (window.ScadaRuntime && window.ScadaRuntime.onTagValuesChanged) {
        window.ScadaRuntime.onTagValuesChanged(e.detail.values);
      }
    });

    // Load home page
    await this.loadPage(homePageId);
  },

  async loadPage(pageId) {
    if (!pageId) return;
    ScadaHost._showLoading(true);

    try {
      const resp = await fetch(`/scada/api/page/${encodeURIComponent(pageId)}/`);
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
      const data = await resp.json();

      if (!data.fragment) {
        console.error('scada: empty fragment for page', pageId);
        return;
      }

      // Inject CSS if not already loaded
      if (data.css_hash && !this.currentCssHashes.has(data.css_hash)) {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = `/static/scada/css/${encodeURIComponent(pageId)}.${data.css_hash}.css`;
        document.head.appendChild(link);
        this.currentCssHashes.add(data.css_hash);
      }

      // Inject fragment
      const host = document.getElementById('scada-host');
      if (!host) return;
      host.innerHTML = data.fragment;

      // Re-initialize runtime for new page
      if (window.ScadaRuntime && window.ScadaRuntime.initPage) {
        window.ScadaRuntime.initPage(host, pageId);
      }

      this.currentPageId = pageId;
      console.log('scada: page loaded', pageId);
    } catch (err) {
      console.error('scada: failed to load page', pageId, err);
    } finally {
      ScadaHost._showLoading(false);
    }
  },

  _showLoading(show) {
    let backdrop = document.getElementById('scada-loading-backdrop');
    if (show) {
      if (!backdrop) {
        backdrop = document.createElement('div');
        backdrop.id = 'scada-loading-backdrop';
        backdrop.style.cssText = 'position:absolute;inset:0;z-index:9000;'
          + 'background:rgba(15,42,48,0.08);display:flex;align-items:center;justify-content:center;';
        const spinner = document.createElement('div');
        spinner.className = 'scada-spinner';
        spinner.style.cssText = 'width:32px;height:32px;border:3px solid rgba(15,42,48,0.16);'
          + 'border-top-color:#0f2a30;border-radius:50%;animation:scada-spin 0.8s linear infinite;';
        backdrop.appendChild(spinner);
        const host = document.getElementById('scada-host');
        if (host) host.parentElement.appendChild(backdrop);
      }
    } else {
      if (backdrop) backdrop.remove();
    }
  }
};
```

- [ ] **Step 2: Add ScadaTagCache module**

```javascript
// === SCADA Tag Cache ===
const ScadaTagCache = {
  values: {},
  pollingTimer: null,

  async poll() {
    const snapshotUrl = document.getElementById('scada-host')?.dataset.mappingSnapshotUrl;
    if (!snapshotUrl) return;

    try {
      const resp = await fetch(snapshotUrl);
      if (!resp.ok) return;
      const data = await resp.json();
      let changed = false;

      for (const [id, snapshot] of Object.entries(data.mappings || {})) {
        const val = snapshot.value;
        if (ScadaTagCache.values[id] !== val) {
          ScadaTagCache.values[id] = val;
          changed = true;
        }
      }

      if (changed) {
        window.dispatchEvent(new CustomEvent('scada-tag-values-changed', {
          detail: { values: { ...ScadaTagCache.values } }
        }));
      }
    } catch (err) {
      console.warn('scada: tag poll failed', err);
    }
  },

  startPolling() {
    const interval = parseInt(
      document.getElementById('scada-host')?.dataset.liveRefreshMs || '500',
      10
    );
    this.stopPolling();
    this.poll(); // immediate first poll
    this.pollingTimer = setInterval(() => this.poll(), Math.max(500, interval));
  },

  stopPolling() {
    if (this.pollingTimer) {
      clearInterval(this.pollingTimer);
      this.pollingTimer = null;
    }
  }
};
```

- [ ] **Step 3: Expose bridge to runtime**

```javascript
// === Bridge exposed to SCADA Builder V2 runtime ===
window.tf100webScadaBuilder = {
  getTagValue(tagId) {
    if (!tagId) return null;
    const mappingId = String(tagId).replace(/^tf100\.mapping\./, '');
    return ScadaTagCache.values[mappingId] ?? null;
  },

  async writeTag(tagId, value, payload) {
    if (!tagId) return false;
    // Block if edit lock is active
    if (ScadaHost.pendingEditLock) {
      console.debug('scada: writeTag blocked — edit lock active');
      return false;
    }
    const mappingId = String(tagId).replace(/^tf100\.mapping\./, '');
    const writeUrl = document.getElementById('scada-host')?.dataset.mappingWriteUrl;
    if (!writeUrl) return false;

    try {
      const resp = await fetch(writeUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-CSRFToken': csrfToken() },
        body: JSON.stringify({ mapping_id: mappingId, value: value })
      });
      return resp.ok;
    } catch (err) {
      console.error('scada: writeTag failed', err);
      return false;
    }
  }
};
```

- [ ] **Step 4: Wire initialization on DOM ready**

Replace the existing initialization at the bottom of `visualisation_import.js` that checks for `root.classList.contains("scada-host")` — add the SCADA Builder 2 path:

```javascript
// SCADA Builder V2 initialization
const scadaHost = document.getElementById('scada-host');
if (scadaHost && scadaHost.classList.contains('scada-host')) {
  const homePageId = scadaHost.dataset.scadaHomePage;
  ScadaHost.init(homePageId).catch((err) => {
    console.error('scada: host init failed', err);
  });
}
```

- [ ] **Step 5: Run TF100Web tests**

Run: `cd F:\Projet\Git\TF100Web && python manage.py test frontend -v 2`
Expected: no regressions.

- [ ] **Step 6: Commit**

```bash
cd F:/Projet/Git/TF100Web
git add static/asset/js/station/visualisation_import.js
git commit -m "feat: add ScadaHost, ScadaTagCache, and tag bridge to visualisation_import.js"
```

---

### Task 14: visualisation_import.js — edit lock + popups + navigation

**Files:**
- Modify: `F:\Projet\Git\TF100Web\static\asset\js\station\visualisation_import.js`

- [ ] **Step 1: Add edit lock support**

```javascript
// === Edit Lock (extends ScadaHost) ===
ScadaHost.acquireEditLock = function (elementId, inputElement) {
  this.pendingEditLock = {
    elementId: elementId,
    since: Date.now(),
    inputElement: inputElement
  };

  const overlay = document.createElement('div');
  overlay.className = 'scada-input-edit-overlay';
  overlay.id = `scada-edit-overlay-${elementId}`;
  overlay.style.cssText =
    'position:absolute;inset:0;background:rgba(15,42,48,0.06);'
    + 'border:2px solid rgba(15,42,48,0.32);border-radius:4px;'
    + 'pointer-events:none;z-index:10;'
    + 'animation:scada-edit-pulse 2s ease-in-out infinite;';

  const parent = inputElement.parentElement;
  if (parent) {
    if (getComputedStyle(parent).position === 'static') {
      parent.style.position = 'relative';
    }
    parent.appendChild(overlay);
  }

  const timer = setTimeout(() => {
    ScadaHost.releaseEditLock();
    inputElement.blur();
  }, 30000);

  this.pendingEditLock.overlay = overlay;
  this.pendingEditLock.timer = timer;
};

ScadaHost.releaseEditLock = function () {
  if (!this.pendingEditLock) return;
  if (this.pendingEditLock.timer) clearTimeout(this.pendingEditLock.timer);
  if (this.pendingEditLock.overlay && this.pendingEditLock.overlay.parentElement) {
    this.pendingEditLock.overlay.remove();
  }
  this.pendingEditLock = null;
};
```

- [ ] **Step 2: Add popup management**

```javascript
// === Popup Management ===
window.addEventListener('message', function (event) {
  const msg = event.data;
  if (!msg || msg.source !== 'scada-builder-v2') return;

  switch (msg.action) {
    case 'navigate':
      history.pushState({ pageId: msg.pageId }, '', `/scada/page/${encodeURIComponent(msg.pageId)}/`);
      ScadaHost.loadPage(msg.pageId);
      break;
    case 'openPopup':
      ScadaHost._createPopup(msg.pageId, msg.options || {});
      break;
    case 'closePopup':
      ScadaHost._closePopup(msg.pageId);
      break;
    case 'togglePopup':
      ScadaHost._getPopupOverlay(msg.pageId)
        ? ScadaHost._closePopup(msg.pageId)
        : ScadaHost._createPopup(msg.pageId, msg.options || {});
      break;
  }
});

ScadaHost._createPopup = function (pageId, options) {
  fetch(`/scada/api/page/${encodeURIComponent(pageId)}/`)
    .then(r => r.json())
    .then(data => {
      if (!data.fragment) return;

      const overlay = document.createElement('div');
      overlay.className = 'scada-popup-overlay';
      overlay.dataset.scadaPopupPageId = pageId;
      overlay.style.cssText = 'position:absolute;inset:0;z-index:10000;'
        + 'background:rgba(0,0,0,0.28);display:flex;align-items:center;justify-content:center;pointer-events:auto;';

      const panel = document.createElement('div');
      panel.style.cssText = 'position:relative;background:#fff;'
        + 'border:1px solid rgba(15,42,48,0.24);box-shadow:0 16px 42px rgba(15,42,48,0.28);'
        + 'width:80%;height:80%;max-width:960px;max-height:720px;';

      const closeBtn = document.createElement('button');
      closeBtn.type = 'button';
      closeBtn.textContent = '×';
      closeBtn.style.cssText = 'position:absolute;top:8px;right:8px;z-index:1;'
        + 'width:28px;height:28px;border:0;background:rgba(15,42,48,0.08);'
        + 'border-radius:50%;font-size:18px;cursor:pointer;color:#0f2a30;';
      closeBtn.onclick = function () { overlay.remove(); };
      panel.appendChild(closeBtn);

      const iframe = document.createElement('iframe');
      iframe.srcdoc = data.fragment;
      iframe.style.cssText = 'width:100%;height:100%;border:0;';
      panel.appendChild(iframe);

      overlay.appendChild(panel);
      overlay.addEventListener('click', function (e) {
        if (e.target === overlay) overlay.remove();
      });
      document.getElementById('scada-host').appendChild(overlay);
    });
};

ScadaHost._closePopup = function (pageId) {
  const existing = ScadaHost._getPopupOverlay(pageId);
  if (existing) existing.remove();
};

ScadaHost._getPopupOverlay = function (pageId) {
  const host = document.getElementById('scada-host');
  if (!host) return null;
  return host.querySelector(`[data-scada-popup-page-id="${pageId}"]`);
};
```

- [ ] **Step 3: Add browser navigation support**

```javascript
// === Browser History Support ===
window.addEventListener('popstate', function (e) {
  if (e.state && e.state.pageId) {
    ScadaHost.loadPage(e.state.pageId);
  }
});
```

- [ ] **Step 4: Run TF100Web tests**

Run: `cd F:\Projet\Git\TF100Web && python manage.py test frontend -v 2`
Expected: no regressions.

- [ ] **Step 5: Commit**

```bash
cd F:/Projet/Git/TF100Web
git add static/asset/js/station/visualisation_import.js
git commit -m "feat: add edit lock, popup management, and browser navigation to host"
```

---

### Task 15: Deprecate legacy TF100Web code

**Files:**
- Modify: `F:\Projet\Git\TF100Web\frontend\scada_package.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\scada_projects.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\views.py` (mark deprecated functions)
- Modify: `F:\Projet\Git\TF100Web\docs\SCADA_BUILDER_SB2_RUNTIME.md`

- [ ] **Step 1: Mark scada_package.py as DEPRECATED**

Add at the top of `scada_package.py`:

```python
# DEPRECATED: removals scheduled for next deployment cycle.
# Replaced by deploy_scada_builder management command and direct template serving.
# See docs/superpowers/specs/2026-07-07-scada-export-runtime-tf100web-integration-design.md
# This module is retained only for backward compatibility during validation.
```

- [ ] **Step 2: Mark scada_projects.py as DEPRECATED**

```python
# DEPRECATED: removals scheduled for next deployment cycle.
# Replaced by deploy_scada_builder management command.
```

- [ ] **Step 3: Mark deprecated code in views.py**

Add `# DEPRECATED:` comments above each function/mixin listed in spec §7.6.

- [ ] **Step 4: Update documentation**

Update `docs/SCADA_BUILDER_SB2_RUNTIME.md` with a new section documenting the new deployment flow:

```markdown
## Deploy workflow (current)

1. Export .sb2 from SCADA Builder V2
2. Copy .sb2 to TF100Web server
3. Run `python manage.py deploy_scada_builder /path/to/export.sb2`
4. Restart Gunicorn: `systemctl restart tf100web`
5. Pages are served at `/scada/page/<page-id>/`

The deployment command copies:
- HTML pages -> templates/frontend/scada/pages/
- Runtime JS -> static/scada/js/
- CSS -> static/scada/css/
- Images -> static/scada/images/
```

- [ ] **Step 5: Run TF100Web test suite — verify no regressions**

Run: `cd F:\Projet\Git\TF100Web && python manage.py test frontend -v 2`
Expected: all PASS.

- [ ] **Step 6: Commit**

```bash
cd F:/Projet/Git/TF100Web
git add frontend/scada_package.py frontend/scada_projects.py frontend/views.py
git add docs/SCADA_BUILDER_SB2_RUNTIME.md
git commit -m "chore: deprecate legacy SCADA intake code, update documentation"
```

---

### Task 16: Full integration test

**Files:**
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

- [ ] **Step 1: Write end-to-end integration test**

```csharp
[TestMethod]
public async Task ExportProjectArchive_ProducesCompleteSb2WithStateCommandRuntime()
{
    var project = CreateValidProject("e2e-project");
    var scene = CreateValidScene("e2e-page", "E2E Page", ScadaPageType.Default);

    var stateConfig = new ScadaElementStateConfig(
        QualityFallback: ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
        DefaultEffect: ScadaEffectBlock.Empty,
        States: new[] {
            new ScadaStateRule("s1", "Running", true,
                new ScadaExpression("{Motor}>0",
                    new ScadaExprBinary(ScadaExprBinaryOp.GreaterThan,
                        new ScadaExprTagRef("Motor"), new ScadaExprLiteralNumber(0)),
                    new[] { "Motor" }),
                ScadaEffectBlock.Empty with { BackgroundColor = "#4CAF50", Animation = ScadaAnimation.Spin })
        });

    var commandConfig = new ScadaElementCommandConfig(new[] {
        new ScadaCommandBinding("c1", "Start", true, ScadaCommandTrigger.OnClick,
            ScadaCommandKind.WriteTag, WriteTagId: "tf100.mapping.42",
            WriteMode: ScadaWriteMode.Toggle,
            Confirmation: new ScadaConfirmation("Start motor?"))
    });

    var element = new ScadaElement("elem1", "Motor", ScadaElementKind.Button,
        new SceneBounds(10, 20, 100, 80),
        stateConfig: stateConfig,
        commandConfig: commandConfig);

    scene = scene.WithElement(element);
    project = project.WithScene(scene);

    var tmpDir = Path.Combine(Path.GetTempPath(), "scada-e2e-" + Guid.NewGuid().ToString("N"));
    var sourceHtml = Path.Combine(tmpDir, "e2e-page.html");
    Directory.CreateDirectory(tmpDir);
    File.WriteAllText(sourceHtml, "<!doctype html><html><body><div class=\"page\"><div id=\"ft100-e2e-page\">x</div></div></body></html>");

    try
    {
        var exporter = new Ft100SceneExporter();
        var input = new Ft100ProjectPageExportInput(scene, sourceHtml);
        var archivePath = Path.Combine(tmpDir, "export.sb2");
        var result = await exporter.ExportProjectArchiveAsync(project, new[] { input }, archivePath);

        Assert.IsTrue(result.IsValid, "Package must pass validation");
        Assert.IsTrue(File.Exists(result.ArchivePath), "Archive file must exist");

        // Verify .sb2 is a valid ZIP
        using var zip = ZipFile.OpenRead(result.ArchivePath);
        var entries = zip.Entries.Select(e => e.FullName).ToArray();

        Assert.IsTrue(entries.Any(e => e.StartsWith("scada-builder-v2-ft100-package/manifest.json")),
            "Must contain manifest");
        Assert.IsTrue(entries.Any(e => e.Contains("scada-runtime.") && e.EndsWith(".js")),
            "Must contain runtime JS");
        Assert.IsTrue(entries.Any(e => e.Contains("/e2e-page.html")),
            "Must contain page HTML");
        Assert.IsTrue(entries.Any(e => e.Contains("/css/e2e-page.") && e.EndsWith(".css")),
            "Must contain CSS with content hash");

        // Verify manifest content
        var manifestEntry = entries.First(e => e.FullName.EndsWith("manifest.json"));
        using var manifestStream = manifestEntry.Open();
        using var manifestDoc = await JsonDocument.ParseAsync(manifestStream);
        var root = manifestDoc.RootElement;
        var pages = root.GetProperty("Pages");
        var firstPage = pages[0];
        var objects = firstPage.GetProperty("Objects");
        var firstObject = objects[0];

        Assert.IsTrue(firstObject.TryGetProperty("StateConfig", out var stateProp),
            "Manifest must have StateConfig");
        Assert.IsTrue(firstObject.TryGetProperty("CommandConfig", out var cmdProp),
            "Manifest must have CommandConfig");

        // Verify HTML content
        var htmlEntry = entries.First(e => e.FullName.EndsWith(".html"));
        using var htmlStream = htmlEntry.Open();
        using var htmlReader = new StreamReader(htmlStream);
        var html = await htmlReader.ReadToEndAsync();

        Assert.IsTrue(html.Contains("data-scada-state-config"), "HTML must have data-scada-state-config");
        Assert.IsTrue(html.Contains("data-scada-command-config"), "HTML must have data-scada-command-config");
        Assert.IsTrue(html.Contains("<script src="), "HTML must use external script reference");
        Assert.IsFalse(html.Contains("ft100-source-layer"), "Must not leak editor layers");
    }
    finally
    {
        if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
    }
}
```

- [ ] **Step 2: Run integration test**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ExportProjectArchive_ProducesCompleteSb2WithStateCommandRuntime"`
Expected: PASS.

- [ ] **Step 3: Run full test suite**

Run: `dotnet test ScadaBuilderV2.sln`
Expected: all tests PASS.

- [ ] **Step 4: Commit**

```bash
git add tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "test: add end-to-end export integration test with state/command runtime"
```
