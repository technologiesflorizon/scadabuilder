# Appearance List, Color Filter, Independent Read-Variable — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an independent, tag-driven "Lecture de variable" display to Element+, a new cumulable "Filtre de couleur" appearance effect that works on `.sep` SVG components, and a cumulable-list UI for appearance modifications — plus fix the two runtime bugs that currently make `TextContent` a no-op on every element.

**Architecture:** `ScadaElementStateConfig` gains an independent, singular `ReadVariable` field evaluated separately from the `States` first-match-wins list (never blocks or is blocked by state rules). `ScadaEffectBlock` gains four `ColorFilter*` properties (plain optional fields, same pattern as the existing ones). The WPF `ElementStateRuleDialog` (already shared by both property surfaces) is restructured from 7 always-visible checkboxes into a dropdown + "+ Ajouter" cumulable list, reusing the exact same underlying controls. The runtime JS (`effect-applier.js`, `state-engine.js`) gets `{tag}`/`{valeur}` interpolation and a color-filter overlay. No TF100Web (Django) changes — proven, not assumed, by an end-to-end deploy test.

**Tech Stack:** C# / .NET 8 (Domain, Rendering, WPF App), vanilla JS (Runtime, no framework), MSTest (.NET), Node built-in test runner (`node:test`) for runtime JS, Django `TestCase` for the TF100Web end-to-end proof.

## Global Constraints

- Two repos: `BUILDER` = `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`, `TF100WEB` = `F:\Projet\Git\TF100Web`.
- `ReadVariable` is a **singular, independent** field on `ScadaElementStateConfig` — never part of the `States` list, never subject to first-match-wins.
- `ScadaEffectBlock` stays a flat record of optional properties (no nested types) — matches existing `BackgroundColor`/`BorderColor` pattern.
- Filtre de couleur is a **translucent overlay** (original content stays visible underneath), not a true recolor.
- Halo reuses the existing `.scada-anim-halo` pulsing CSS class — no new keyframes.
- Every WPF change lands in **both** `MainWindow.xaml`/`.xaml.cs` (docked panel) and `ElementPropertiesDialog.xaml`/`.xaml.cs` (double-click modal) in the same task — a prior regression (`8674523`) came from missing one of the two surfaces.
- One `ScadaCommandKind` per Element+ across its `CommandConfig.Commands`, regardless of `Trigger`.
- No TF100Web server-side code changes — verified by an end-to-end test, not assumed.
- WPF dialog code-behind has no existing unit-test coverage in this codebase (verified: no `*Dialog*Tests.cs` files exist) — WPF tasks are verified by `dotnet build` + a concrete manual test script, consistent with the project's existing test boundary (Domain/Rendering/Runtime-JS are unit-tested, WPF App is not).

---

## File Structure

- `src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaEffectBlock.cs` — add 4 `ColorFilter*` properties.
- `src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaElementStateConfig.cs` — add `ReadVariable` field.
- `src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaReadVariableRule.cs` — new record (`TagId`, `DisplayFormat`).
- `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs` — wrap Text-kind content in `<span data-scada-text>`.
- `src/ScadaBuilderV2.Rendering/Runtime/effect-applier.js` — `{tag}`/`{valeur}` interpolation, color-filter overlay.
- `src/ScadaBuilderV2.Rendering/Runtime/state-engine.js` — independent `readVariable` evaluation step.
- `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml` + `.xaml.cs` — cumulable effect list + Filtre de couleur editor.
- `src/ScadaBuilderV2.App/ElementReadVariableDialog.xaml` + `.xaml.cs` — new, dedicated dialog.
- `src/ScadaBuilderV2.App/ElementCommandDialog.xaml.cs` — one-Kind-per-element guard-rail.
- `src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml` + `.xaml.cs` — wire read-variable section + command guard-rail (modal surface).
- `src/ScadaBuilderV2.App/MainWindow.xaml` + `.xaml.cs` — wire read-variable section + command guard-rail (docked surface).
- `tests/ScadaBuilderV2.Tests/ElementEvents/*` — domain + serialization + ordering tests.
- `tests/runtime-js/*.test.mjs` — interpolation, color-filter overlay, independent read-variable tests.
- `frontend/tests_scada_deploy.py` (TF100Web) — end-to-end deploy proof.

---

## Task 1: Domain model — `ColorFilter*`, `ScadaReadVariableRule`, `ReadVariable` field

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaEffectBlock.cs`
- Modify: `src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaElementStateConfig.cs`
- Create: `src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaReadVariableRule.cs`
- Test: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaEffectBlockTests.cs`
- Test: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementStateConfigTests.cs`

**Interfaces:**
- Produces: `ScadaEffectBlock(..., ColorFilterColor: string? = null, ColorFilterOpacity: double? = null, ColorFilterHalo: bool? = null, ColorFilterHaloColor: string? = null)`.
- Produces: `ScadaReadVariableRule(TagId: string, DisplayFormat: string? = null)`.
- Produces: `ScadaElementStateConfig(QualityFallback, DefaultEffect, States, ReadVariable: ScadaReadVariableRule? = null)`.

- [ ] **Step 1: Write the failing tests**

Append to `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaEffectBlockTests.cs` (find the existing `[TestClass]` and add a method inside it):

```csharp
[TestMethod]
public void ColorFilterProperties_DefaultToNull()
{
    var effect = ScadaEffectBlock.Empty;
    Assert.IsNull(effect.ColorFilterColor);
    Assert.IsNull(effect.ColorFilterOpacity);
    Assert.IsNull(effect.ColorFilterHalo);
    Assert.IsNull(effect.ColorFilterHaloColor);
}

[TestMethod]
public void ColorFilterProperties_CanBeSetIndependentlyOfBackgroundColor()
{
    var effect = ScadaEffectBlock.Empty with
    {
        ColorFilterColor = "#E53935",
        ColorFilterOpacity = 0.35,
        ColorFilterHalo = true,
        ColorFilterHaloColor = "#E53935"
    };
    Assert.IsNull(effect.BackgroundColor);
    Assert.AreEqual("#E53935", effect.ColorFilterColor);
    Assert.AreEqual(0.35, effect.ColorFilterOpacity);
    Assert.IsTrue(effect.ColorFilterHalo);
    Assert.AreEqual("#E53935", effect.ColorFilterHaloColor);
}
```

Append to `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementStateConfigTests.cs` (inside the existing `[TestClass]`):

```csharp
[TestMethod]
public void Default_HasNoReadVariable()
{
    Assert.IsNull(ScadaElementStateConfig.Default.ReadVariable);
}

[TestMethod]
public void ReadVariable_IsIndependentOfStates()
{
    var config = ScadaElementStateConfig.Default with
    {
        ReadVariable = new ScadaReadVariableRule("tf100.mapping.42", "Debit: {valeur} L/min"),
        States = [new ScadaStateRule(
            "s1", "Alarme", true,
            ScadaExpression.FromSource("{Temp} > 80"),
            ScadaEffectBlock.Empty with { BackgroundColor = "#E53935" })]
    };

    Assert.IsNotNull(config.ReadVariable);
    Assert.AreEqual("tf100.mapping.42", config.ReadVariable.TagId);
    Assert.AreEqual(1, config.States.Count);
}
```

Check the top of `ScadaElementStateConfigTests.cs` for existing `using` directives — this test needs
`ScadaBuilderV2.Domain.ElementEvents.Expressions` (for `ScadaExpression`) already imported by the
existing file (the class already builds `ScadaStateRule` instances in other tests).

- [ ] **Step 2: Run to verify it fails**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaEffectBlockTests|FullyQualifiedName~ScadaElementStateConfigTests"`
Expected: build error — `ColorFilterColor`/`ReadVariable` don't exist yet, or `ScadaReadVariableRule` type not found.

- [ ] **Step 3: Create `ScadaReadVariableRule.cs`**

```csharp
namespace ScadaBuilderV2.Domain.ElementEvents.State;

/// <summary>
/// Independent, continuous tag-value display for an Element+ — evaluated separately from
/// <see cref="ScadaElementStateConfig.States"/> and never affected by which state rule matches.
/// </summary>
/// <remarks>
/// Decisions: DEC-0036.
/// Contracts: docs/superpowers/specs/2026-07-09-element-plus-appearance-and-read-variable-design.md.
/// </remarks>
/// <param name="TagId">The tag whose value is displayed.</param>
/// <param name="DisplayFormat">
/// Optional format string using the literal token <c>{valeur}</c> (e.g. <c>"Debit: {valeur} L/min"</c>).
/// Null or a format without <c>{valeur}</c> displays the raw tag value.
/// </param>
public sealed record ScadaReadVariableRule(
    string TagId,
    string? DisplayFormat = null);
```

- [ ] **Step 4: Modify `ScadaEffectBlock.cs`**

Find:
```csharp
public sealed record ScadaEffectBlock(
    string? BackgroundColor = null,
    string? BorderColor = null,
    double? BorderWidth = null,
    string? TextColor = null,
    string? TextContent = null,
    bool? TextVisible = null,
    bool? ElementVisible = null,
    double? Opacity = null,
    double? Rotation = null,
    ScadaAnimation? Animation = null)
```

Replace with:
```csharp
public sealed record ScadaEffectBlock(
    string? BackgroundColor = null,
    string? BorderColor = null,
    double? BorderWidth = null,
    string? TextColor = null,
    string? TextContent = null,
    bool? TextVisible = null,
    bool? ElementVisible = null,
    double? Opacity = null,
    double? Rotation = null,
    ScadaAnimation? Animation = null,
    string? ColorFilterColor = null,
    double? ColorFilterOpacity = null,
    bool? ColorFilterHalo = null,
    string? ColorFilterHaloColor = null)
```

- [ ] **Step 5: Modify `ScadaElementStateConfig.cs`**

Find:
```csharp
public sealed record ScadaElementStateConfig(
    ScadaEffectBlock QualityFallback,
    ScadaEffectBlock DefaultEffect,
    IReadOnlyList<ScadaStateRule> States)
```

Replace with:
```csharp
public sealed record ScadaElementStateConfig(
    ScadaEffectBlock QualityFallback,
    ScadaEffectBlock DefaultEffect,
    IReadOnlyList<ScadaStateRule> States,
    ScadaReadVariableRule? ReadVariable = null)
```

- [ ] **Step 6: Run to verify it passes**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaEffectBlockTests|FullyQualifiedName~ScadaElementStateConfigTests"`
Expected: PASS, all tests including the 4 new ones.

- [ ] **Step 7: Run the full .NET suite to check for regressions**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet build ScadaBuilderV2.sln --no-restore && dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 0 build errors. Same pre-existing 4 `WebViewContextMenuScriptTests` failures as the session baseline (unrelated), everything else green.

- [ ] **Step 8: Commit**

```bash
cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaEffectBlock.cs src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaElementStateConfig.cs src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaReadVariableRule.cs tests/ScadaBuilderV2.Tests/ElementEvents/ScadaEffectBlockTests.cs tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementStateConfigTests.cs
git commit -m "feat: add ColorFilter effect properties and independent ReadVariable field"
```

---

## Task 2: Exporter — `data-scada-text` target + serialization + ordering regression test

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs:948` (the `BuildElementContent` switch)
- Test: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

**Interfaces:**
- Consumes: `ScadaElementStateConfig.ReadVariable`, `ScadaEffectBlock.ColorFilter*` (Task 1).
- Produces: HTML for `ScadaElementKind.Text` elements now contains `<span data-scada-text>...</span>` instead of bare text. `data-scada-state-config` JSON gains a `readVariable` key and effects gain `colorFilterColor`/`colorFilterOpacity`/`colorFilterHalo`/`colorFilterHaloColor` keys automatically (plain properties, same `JsonSerializer` call as today — no new code needed for this part, only verified by the test in this task).

- [ ] **Step 1: Write the failing tests**

Add to `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`, near the other `ExportAsync_Includes...` tests (e.g. right after `ExportAsync_IncludesStateConfigInManifestAndHtml`):

```csharp
[TestMethod]
public async Task ExportAsync_WrapsTextElementContentInDataScadaTextSpan()
{
    var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
    var sourceRoot = Path.Combine(root, "source");
    var exportRoot = Path.Combine(root, "export");
    Directory.CreateDirectory(sourceRoot);

    var sourceHtmlPath = Path.Combine(sourceRoot, "text_span_test.html");
    await File.WriteAllTextAsync(
        sourceHtmlPath,
        """
<!doctype html>
<html>
<body>
  <div class="page"></div>
</body>
</html>
""");

    var element = new ScadaElement(
        "text_001",
        "Label",
        ScadaElementKind.Text,
        new SceneBounds(10, 20, 100, 30),
        null,
        ScadaElementLayout.Absolute,
        ScadaElementStyle.DefaultText,
        new ScadaElementData("Valeur initiale", null, null, null, null, null, null, null, null, false));

    var scene = ScadaScene.CreateEmpty("win00008", "Text Span Test", new(1280, 873)).WithElement(element);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);
        var html = await File.ReadAllTextAsync(result.HtmlPath);
        StringAssert.Contains(html, "<span data-scada-text>Valeur initiale</span>");
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

[TestMethod]
public async Task ExportAsync_IncludesReadVariableAndColorFilterInManifestAndHtml()
{
    var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
    var sourceRoot = Path.Combine(root, "source");
    var exportRoot = Path.Combine(root, "export");
    Directory.CreateDirectory(sourceRoot);

    var sourceHtmlPath = Path.Combine(sourceRoot, "read_variable_test.html");
    await File.WriteAllTextAsync(
        sourceHtmlPath,
        """
<!doctype html>
<html>
<body>
  <div class="page"></div>
</body>
</html>
""");

    var element = new ScadaElement(
        "text_002",
        "Debit",
        ScadaElementKind.Text,
        new SceneBounds(10, 20, 100, 30),
        null,
        ScadaElementLayout.Absolute,
        ScadaElementStyle.DefaultText,
        new ScadaElementData("---", null, null, null, null, null, null, null, null, false),
        StateConfig: new ScadaElementStateConfig(
            ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
            ScadaEffectBlock.Empty,
            [new ScadaStateRule(
                "s1", "Alarme", true,
                ScadaExpression.FromSource("true"),
                new ScadaEffectBlock(
                    ColorFilterColor: "#E53935",
                    ColorFilterOpacity: 0.35,
                    ColorFilterHalo: true,
                    ColorFilterHaloColor: "#E53935"))],
            ReadVariable: new ScadaReadVariableRule("tf100.mapping.42", "Debit: {valeur} L/min")));

    var scene = ScadaScene.CreateEmpty("win00008", "Read Variable Test", new(1280, 873)).WithElement(element);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

        var manifest = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "manifest.json"));
        StringAssert.Contains(manifest, "\"ReadVariable\"");
        StringAssert.Contains(manifest, "tf100.mapping.42");
        StringAssert.Contains(manifest, "\"ColorFilterColor\"");
        StringAssert.Contains(manifest, "\"ColorFilterHalo\": true");

        var html = await File.ReadAllTextAsync(result.HtmlPath);
        StringAssert.Contains(html, "&quot;readVariable&quot;");
        StringAssert.Contains(html, "&quot;colorFilterColor&quot;");
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

[TestMethod]
public async Task ExportAsync_PreservesStateAndCommandListOrderInJson()
{
    // Regression: manifest/HTML JSON must preserve the exact UI list order of States/Commands
    // (first-match-wins depends on it; ordering is the user's responsibility to set correctly).
    var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
    var sourceRoot = Path.Combine(root, "source");
    var exportRoot = Path.Combine(root, "export");
    Directory.CreateDirectory(sourceRoot);

    var sourceHtmlPath = Path.Combine(sourceRoot, "order_test.html");
    await File.WriteAllTextAsync(
        sourceHtmlPath,
        """
<!doctype html>
<html>
<body>
  <div class="page"></div>
</body>
</html>
""");

    ScadaStateRule Rule(string id, string name) => new(
        id, name, true, ScadaExpression.FromSource("true"), ScadaEffectBlock.Empty with { BackgroundColor = "#000000" });

    var element = new ScadaElement(
        "order_001",
        "Order",
        ScadaElementKind.Shape,
        new SceneBounds(10, 20, 100, 30),
        null,
        ScadaElementLayout.Absolute,
        ScadaElementStyle.DefaultInput,
        new ScadaElementData(null, null, null, null, null, null, null, null, null, false),
        StateConfig: new ScadaElementStateConfig(
            ScadaEffectBlock.Empty, ScadaEffectBlock.Empty,
            [Rule("s-third", "Troisieme"), Rule("s-first", "Premiere"), Rule("s-second", "Deuxieme")]));

    var scene = ScadaScene.CreateEmpty("win00008", "Order Test", new(1280, 873)).WithElement(element);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);
        var manifest = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "manifest.json"));

        var thirdIndex = manifest.IndexOf("s-third", StringComparison.Ordinal);
        var firstIndex = manifest.IndexOf("s-first", StringComparison.Ordinal);
        var secondIndex = manifest.IndexOf("s-second", StringComparison.Ordinal);

        Assert.IsTrue(thirdIndex >= 0 && firstIndex > thirdIndex && secondIndex > firstIndex,
            "States must serialize in the exact list order (Troisieme, Premiere, Deuxieme), not sorted.");
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
```

Check the top of `Ft100SceneExporterTests.cs` for `using ScadaBuilderV2.Domain.ElementEvents.State;` —
already present (used by the existing `ExportAsync_IncludesStateConfigInManifestAndHtml` test).

- [ ] **Step 2: Run to verify the span test fails, the other two pass already**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ExportAsync_WrapsTextElementContentInDataScadaTextSpan|FullyQualifiedName~ExportAsync_IncludesReadVariableAndColorFilterInManifestAndHtml|FullyQualifiedName~ExportAsync_PreservesStateAndCommandListOrderInJson"`
Expected: `ExportAsync_WrapsTextElementContentInDataScadaTextSpan` FAILS (no `<span data-scada-text>` in
output yet — content is bare text). The other two already PASS today (they only exercise the domain
model + `JsonSerializer`, which already round-trips new properties automatically) — confirms Task 1's
serialization needs no exporter code, only this test proving it.

- [ ] **Step 3: Wrap Text-kind content**

Find in `Ft100SceneExporter.cs` (`BuildElementContent`, around line 946-949):
```csharp
        return element.Kind switch
        {
            ScadaElementKind.Custom => ScopeSvgIds(data.Text ?? "", scope, element.Id),
            ScadaElementKind.Text => HtmlEncoder.Default.Encode(data.Text ?? element.DisplayName),
```

Replace with:
```csharp
        return element.Kind switch
        {
            ScadaElementKind.Custom => ScopeSvgIds(data.Text ?? "", scope, element.Id),
            ScadaElementKind.Text => $"<span data-scada-text>{HtmlEncoder.Default.Encode(data.Text ?? element.DisplayName)}</span>",
```

- [ ] **Step 4: Run to verify all three pass**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ExportAsync_WrapsTextElementContentInDataScadaTextSpan|FullyQualifiedName~ExportAsync_IncludesReadVariableAndColorFilterInManifestAndHtml|FullyQualifiedName~ExportAsync_PreservesStateAndCommandListOrderInJson"`
Expected: PASS (3/3)

- [ ] **Step 5: Run the full exporter suite for regressions**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~Ft100SceneExporter"`
Expected: PASS, all — including existing Text-kind assertions elsewhere (none currently assert the
old bare-text shape for `ScadaElementKind.Text`'s inner HTML specifically, per a prior check of this
file, so no other test should break; if one does, update its expected string to include the new span).

- [ ] **Step 6: Commit**

```bash
cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "fix: wrap Text-kind content in data-scada-text span; lock in State/Command list ordering"
```

---

## Task 3: Runtime JS — `{tag}`/`{valeur}` interpolation in `effect-applier.js`

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/effect-applier.js`
- Test: `tests/runtime-js/effect-applier.test.mjs` (new)

**Interfaces:**
- Consumes: `window.ScadaRuntime.TagBridge.getTagValue(tagId)` (existing, from `tag-bridge.js`).
- Produces: `window.ScadaRuntime.EffectApplier.apply(element, effect)` keeps its signature; `effect.textContent`
  containing `{TagId}` tokens is now resolved before being written. New exported helper
  `window.ScadaRuntime.EffectApplier.resolveTemplate(template, tokenPattern, resolver)` is NOT introduced —
  interpolation logic stays private inside this module (used by both `TextContent` here and, in Task 5,
  by `state-engine.js`'s `ReadVariable` handling via its own small helper — no shared export needed since
  the two use different token syntaxes, `{TagId}` vs `{valeur}`).

- [ ] **Step 1: Write the failing test**

`tests/runtime-js/effect-applier.test.mjs`:
```javascript
import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

function makeFakeElement() {
  const style = {};
  return {
    style,
    classList: { length: 0, add() {}, remove() {}, contains: () => false },
    hidden: false,
    querySelector(selector) {
      if (selector === '[data-scada-text]') return this._textTarget;
      return null;
    },
    _textTarget: { textContent: '' },
  };
}

test('apply() interpolates {TagId} tokens in textContent via TagBridge', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  window.tf100webScadaBuilder = {
    getTagValue(tagId) {
      const mappingId = String(tagId).replace(/^tf100\.mapping\./, '');
      return { '42': '95' }[mappingId] ?? null;
    },
  };

  const element = makeFakeElement();
  window.ScadaRuntime.EffectApplier.apply(element, { textContent: 'Debit: {tf100.mapping.42} L/min' });

  assert.equal(element._textTarget.textContent, 'Debit: 95 L/min');
});

test('apply() replaces an unresolved tag token with "---"', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  window.tf100webScadaBuilder = { getTagValue: () => null };

  const element = makeFakeElement();
  window.ScadaRuntime.EffectApplier.apply(element, { textContent: 'Debit: {tf100.mapping.99}' });

  assert.equal(element._textTarget.textContent, 'Debit: ---');
});

test('apply() leaves plain textContent (no tokens) unchanged', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  const element = makeFakeElement();
  window.ScadaRuntime.EffectApplier.apply(element, { textContent: 'Arret' });
  assert.equal(element._textTarget.textContent, 'Arret');
});
```

- [ ] **Step 2: Run to verify the first two fail**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test effect-applier.test.mjs`
Expected: first two tests FAIL (`textContent` is set to the literal, un-interpolated string today —
`'Debit: {tf100.mapping.42} L/min'` instead of `'Debit: 95 L/min'`). Third test already passes (no
tokens to resolve, current direct-assignment behavior is already correct for that case).

- [ ] **Step 3: Add interpolation to `effect-applier.js`**

Find (top of the IIFE, before `function apply`):
```javascript
(function () {
  'use strict';

  /**
   * Effect applier for the SCADA Builder V2 runtime.
   * Applies visual effects (state-driven style changes) to a DOM element.
   *
   * All effect properties are optional (null/undefined = skip).
   */
  function apply(element, effect) {
```

Replace with:
```javascript
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

  function apply(element, effect) {
```

Find (the `textContent` block):
```javascript
    // ── text content ─────────────────────────────────────────────────────
    if (effect.textContent != null) {
      var textTarget = element.querySelector('[data-scada-text]');
      if (textTarget) {
        textTarget.textContent = effect.textContent;
      }
    }
```

Replace with:
```javascript
    // ── text content ─────────────────────────────────────────────────────
    if (effect.textContent != null) {
      var textTarget = element.querySelector('[data-scada-text]');
      if (textTarget) {
        textTarget.textContent = resolveTagTokens(effect.textContent);
      }
    }
```

Find (the public API export, near the bottom):
```javascript
  window.ScadaRuntime.EffectApplier = {
    apply: apply
  };
```

Replace with:
```javascript
  window.ScadaRuntime.EffectApplier = {
    apply: apply,
    resolveTagTokens: resolveTagTokens
  };
```

(`resolveTagTokens` is exported so Task 5's `state-engine.js` read-variable step and Task 4's tests
can call it directly rather than duplicating the regex.)

- [ ] **Step 4: Run to verify all three pass**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test effect-applier.test.mjs`
Expected: PASS (3/3)

- [ ] **Step 5: Commit**

```bash
cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.Rendering/Runtime/effect-applier.js tests/runtime-js/effect-applier.test.mjs
git commit -m "fix: interpolate {TagId} tokens in TextContent via TagBridge

TextContent has been documented as supporting \"Debit: {Flow}\"-style interpolation
since the 2026-07-07 design, but effect-applier.js assigned the literal string
as-is - the token would render verbatim instead of the tag's value."
```

---

## Task 4: Runtime JS — Filtre de couleur overlay in `effect-applier.js`

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/effect-applier.js`
- Test: `tests/runtime-js/effect-applier.test.mjs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `apply(element, effect)` creates/updates/removes a child overlay element when
  `effect.colorFilterColor` is set/unset. The overlay is tagged `data-scada-color-filter-overlay="1"`
  so repeated `apply()` calls find and reuse the same node (via `element.querySelector`) instead of
  stacking duplicates.

- [ ] **Step 1: Write the failing tests**

Append to `tests/runtime-js/effect-applier.test.mjs`. This module's fake element needs
`appendChild`/a child registry to support overlay creation — extend `makeFakeElement()`:

Find:
```javascript
function makeFakeElement() {
  const style = {};
  return {
    style,
    classList: { length: 0, add() {}, remove() {}, contains: () => false },
    hidden: false,
    querySelector(selector) {
      if (selector === '[data-scada-text]') return this._textTarget;
      return null;
    },
    _textTarget: { textContent: '' },
  };
}
```

Replace with:
```javascript
function makeFakeOverlayNode() {
  return {
    style: {},
    classList: { _classes: new Set(), add(c) { this._classes.add(c); }, remove(c) { this._classes.delete(c); }, contains(c) { return this._classes.has(c); }, length: 0 },
    dataset: {},
    setAttribute(name, value) { this.dataset[name.replace('data-', '')] = value; },
    getAttribute(name) { return this.dataset[name.replace('data-', '')]; },
  };
}

function makeFakeElement() {
  const style = {};
  const children = [];
  return {
    style,
    classList: { length: 0, add() {}, remove() {}, contains: () => false },
    hidden: false,
    _textTarget: { textContent: '' },
    _children: children,
    appendChild(node) { children.push(node); return node; },
    removeChild(node) {
      const i = children.indexOf(node);
      if (i >= 0) children.splice(i, 1);
    },
    querySelector(selector) {
      if (selector === '[data-scada-text]') return this._textTarget;
      if (selector === '[data-scada-color-filter-overlay]') {
        return children.find((c) => c.dataset && c.dataset['scada-color-filter-overlay']) || null;
      }
      return null;
    },
  };
}
```

Then add the new tests at the end of the file:
```javascript
test('apply() creates a translucent overlay for colorFilterColor', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  window.document = { createElement: () => makeFakeOverlayNode() };

  const element = makeFakeElement();
  window.ScadaRuntime.EffectApplier.apply(element, { colorFilterColor: '#E53935', colorFilterOpacity: 0.35 });

  const overlay = element.querySelector('[data-scada-color-filter-overlay]');
  assert.ok(overlay, 'expected an overlay element to be created');
  assert.equal(overlay.style.backgroundColor, '#E53935');
  assert.equal(overlay.style.opacity, 0.35);
});

test('apply() reuses the same overlay node across repeated calls', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  window.document = { createElement: () => makeFakeOverlayNode() };

  const element = makeFakeElement();
  window.ScadaRuntime.EffectApplier.apply(element, { colorFilterColor: '#E53935', colorFilterOpacity: 0.35 });
  window.ScadaRuntime.EffectApplier.apply(element, { colorFilterColor: '#2090A0', colorFilterOpacity: 0.5 });

  assert.equal(element._children.length, 1, 'a second apply() must update the existing overlay, not add another');
  const overlay = element.querySelector('[data-scada-color-filter-overlay]');
  assert.equal(overlay.style.backgroundColor, '#2090A0');
});

test('apply() adds the pulsing halo class with the halo color when colorFilterHalo is true', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  window.document = { createElement: () => makeFakeOverlayNode() };

  const element = makeFakeElement();
  window.ScadaRuntime.EffectApplier.apply(element, {
    colorFilterColor: '#E53935',
    colorFilterOpacity: 0.35,
    colorFilterHalo: true,
    colorFilterHaloColor: '#FFEE00',
  });

  const overlay = element.querySelector('[data-scada-color-filter-overlay]');
  assert.ok(overlay.classList.contains('scada-anim-halo'));
  assert.equal(overlay.style.color, '#FFEE00');
});

test('apply() removes the overlay when colorFilterColor is absent', () => {
  const window = loadRuntime(['tag-bridge.js', 'effect-applier.js']);
  window.document = { createElement: () => makeFakeOverlayNode() };

  const element = makeFakeElement();
  window.ScadaRuntime.EffectApplier.apply(element, { colorFilterColor: '#E53935' });
  window.ScadaRuntime.EffectApplier.apply(element, {});

  assert.equal(element.querySelector('[data-scada-color-filter-overlay]'), null);
});
```

- [ ] **Step 2: Run to verify all four fail**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test effect-applier.test.mjs`
Expected: the 4 new tests FAIL (no overlay logic exists yet); the 3 from Task 3 still PASS.

- [ ] **Step 3: Add the overlay logic**

Find (the `animation` block, near the end of `apply()`):
```javascript
    // ── animation ────────────────────────────────────────────────────────
    if (effect.animation !== null && effect.animation !== undefined) {
      // Remove all scada-anim-* classes
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
      // If a non-null/non-None animation name was provided, add the class
      if (effect.animation !== 'None' && effect.animation !== 'none') {
        classList.add('scada-anim-' + effect.animation);
      }
    }
  }
```

Replace with:
```javascript
    // ── animation ────────────────────────────────────────────────────────
    if (effect.animation !== null && effect.animation !== undefined) {
      // Remove all scada-anim-* classes
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
      // If a non-null/non-None animation name was provided, add the class
      if (effect.animation !== 'None' && effect.animation !== 'none') {
        classList.add('scada-anim-' + effect.animation);
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
  }
```

- [ ] **Step 4: Run to verify all pass**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test effect-applier.test.mjs`
Expected: PASS (7/7)

- [ ] **Step 5: Run the full Node suite for regressions**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test *.test.mjs`
Expected: all pass (13 tests total: 7 effect-applier + 2 expression-evaluator + 1 state-engine + 3 command-dispatcher).

- [ ] **Step 6: Commit**

```bash
cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.Rendering/Runtime/effect-applier.js tests/runtime-js/effect-applier.test.mjs
git commit -m "feat: add Filtre de couleur overlay to effect-applier.js

Translucent color overlay (not a true recolor) so it works on .sep SVG components
where BackgroundColor is invisible (the SVG paints over the wrapper div). Optional
halo reuses the existing scada-anim-halo pulsing CSS class."
```

---

## Task 5: Runtime JS — independent `ReadVariable` evaluation in `state-engine.js`

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/state-engine.js`
- Test: `tests/runtime-js/state-engine.test.mjs`

**Interfaces:**
- Consumes: `window.ScadaRuntime.TagBridge.getTagValue` (existing), `window.ScadaRuntime.EffectApplier.resolveTagTokens`
  is NOT reused here (different token syntax, `{valeur}` not `{TagId}`) — a small dedicated substitution
  is added instead.
- Produces: `evaluate(element, tagValues)` keeps its signature. Reads a new attribute
  `data-scada-read-variable-config` (separate from `data-scada-state-config`, since `ReadVariable` is
  serialized as a sibling of `states`/`qualityFallback`/`defaultEffect` — see Task 6 for why it gets its
  own attribute rather than being nested inside the same JSON blob the states loop already parses).

**Design note for this step:** Task 2's exporter test asserts `readVariable` is nested inside the
SAME `data-scada-state-config` JSON as `states`/`qualityFallback`/`defaultEffect` (matching §5 of the
design doc). `state-engine.js` already parses that one JSON blob once per `evaluate()` call — reuse
`config.readVariable` from that parse, no second attribute needed. (This note supersedes the
"separate attribute" idea above — keeping one JSON payload avoids a second `JSON.parse` and a second
exporter attribute to keep in sync.)

- [ ] **Step 1: Write the failing test**

Append to `tests/runtime-js/state-engine.test.mjs`:
```javascript
test('evaluate() applies readVariable text independently of which state matches', () => {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js', 'effect-applier.js', 'state-engine.js']);

  window.tf100webScadaBuilder = {
    getTagValue(tagId) {
      const mappingId = String(tagId).replace(/^tf100\.mapping\./, '');
      return { '42': '95' }[mappingId] ?? null;
    },
  };

  const stateConfig = {
    qualityFallback: { opacity: 0.4 },
    defaultEffect: {},
    readVariable: { tagId: 'tf100.mapping.42', displayFormat: 'Debit: {valeur} L/min' },
    states: [
      {
        id: 's1', name: 'Alarme', enabled: true,
        expression: { ast: { type: 'literalBool', value: true } },
        effect: { backgroundColor: '#E53935' }, // no textContent — must not block readVariable's text
      },
    ],
  };

  const applied = [];
  window.ScadaRuntime.EffectApplier.apply = (el, effect) => applied.push(effect);

  const textTarget = { textContent: '' };
  const element = {
    id: 'el1',
    _attrs: { 'data-scada-state-config': JSON.stringify(stateConfig) },
    getAttribute(name) { return Object.prototype.hasOwnProperty.call(this._attrs, name) ? this._attrs[name] : null; },
    querySelector(selector) { return selector === '[data-scada-text]' ? textTarget : null; },
  };

  window.ScadaRuntime.StateEngine.evaluate(element, {});

  assert.equal(textTarget.textContent, 'Debit: 95 L/min', 'readVariable must set the text independently of EffectApplier.apply being stubbed out');
  assert.equal(applied.length, 1, 'the matching state effect must still apply (background color)');
  assert.equal(applied[0].backgroundColor, '#E53935');
});

test('evaluate() lets a matched state\'s own textContent override readVariable for that cycle', () => {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js', 'effect-applier.js', 'state-engine.js']);

  window.tf100webScadaBuilder = { getTagValue: (tagId) => ({ '42': '95' }[tagId.replace(/^tf100\.mapping\./, '')] ?? null) };

  const stateConfig = {
    qualityFallback: {},
    defaultEffect: {},
    readVariable: { tagId: 'tf100.mapping.42' },
    states: [
      {
        id: 's1', name: 'Erreur', enabled: true,
        expression: { ast: { type: 'literalBool', value: true } },
        effect: { textContent: '---' },
      },
    ],
  };

  const textTarget = { textContent: '' };
  const element = {
    id: 'el1',
    _attrs: { 'data-scada-state-config': JSON.stringify(stateConfig) },
    getAttribute(name) { return Object.prototype.hasOwnProperty.call(this._attrs, name) ? this._attrs[name] : null; },
    querySelector(selector) { return selector === '[data-scada-text]' ? textTarget : null; },
  };

  window.ScadaRuntime.StateEngine.evaluate(element, {});

  assert.equal(textTarget.textContent, '---', "the matched state's explicit textContent must win over readVariable");
});
```

- [ ] **Step 2: Run to verify both fail**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test state-engine.test.mjs`
Expected: FAIL — `data-scada-read-variable-config`/`readVariable` isn't handled at all today, so
`textTarget.textContent` stays `''` in both tests.

- [ ] **Step 3: Add the independent read-variable step**

Find in `state-engine.js` (the top of `evaluate()`, right after the `config` JSON is parsed and
validated — around line 152, just before `var evaluator = _getEvaluator();`):
```javascript
    if (!config || !Array.isArray(config.states)) {
      return;
    }

    var evaluator = _getEvaluator();
```

Replace with:
```javascript
    if (!config) {
      return;
    }

    // Independent pass: readVariable is never blocked by, and never blocks, the States loop below.
    _applyReadVariable(element, config.readVariable, tagValues);

    if (!Array.isArray(config.states)) {
      return;
    }

    var evaluator = _getEvaluator();
```

Add a new function near `_collectTags` (after its closing brace, before the "── error badge ──" section):
```javascript
  // ── independent read-variable ────────────────────────────────────────────

  /**
   * Writes the live value of readVariable.tagId onto [data-scada-text], independently of the
   * States first-match-wins loop. A matched state's own explicit textContent (applied afterward
   * by the normal loop) overrides this for that cycle.
   *
   * @param {Element} element      - DOM element with data-scada-state-config.
   * @param {object}  readVariable - { tagId, displayFormat? } or undefined/null if not configured.
   * @param {object}  tagValues    - unused directly (resolution goes through TagBridge, see Task 3/4).
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
```

- [ ] **Step 4: Run to verify both pass**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test state-engine.test.mjs`
Expected: PASS (3/3 — the 2 new plus the existing one from the earlier correction plan).

- [ ] **Step 5: Run the full Node and .NET suites for regressions**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test *.test.mjs`
Expected: all pass (15 tests).

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~RuntimeJsModulesTests"`
Expected: unchanged, all pass.

- [ ] **Step 6: Commit**

```bash
cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.Rendering/Runtime/state-engine.js tests/runtime-js/state-engine.test.mjs
git commit -m "feat: evaluate readVariable independently of the States first-match-wins loop

readVariable sets the element's live tag-value text on every cycle, regardless of
which state (if any) matches. A state's own explicit textContent still overrides
it for that cycle (e.g. the error badge forcing ---), applied afterward by the
existing loop."
```

---

## Task 6: WPF — `ElementStateRuleDialog` cumulable effect list + Filtre de couleur

**Files:**
- Modify: `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml`
- Modify: `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs`

**Interfaces:**
- Consumes: `ScadaEffectBlock` (Task 1, now with `ColorFilter*`).
- Produces: `ElementStateRuleDialog(ScadaStateRule? existingRule, ScadaTagCatalog? tagCatalog)` keeps its
  constructor signature and `Result: ScadaStateRule?` property — no caller changes needed in `MainWindow.xaml.cs`
  or `ElementPropertiesDialog.xaml.cs` for this task.

**No automated test** — WPF dialog code-behind has no test harness in this codebase (see Global
Constraints). Verified by `dotnet build` + the manual script in Step 5.

- [ ] **Step 1: Replace the effect section in `ElementStateRuleDialog.xaml`**

Find (the entire `<ScrollViewer Grid.Row="1">...</ScrollViewer>` block, lines 77-112):
```xml
        <ScrollViewer Grid.Row="1">
            <StackPanel>
                <CheckBox x:Name="BackgroundEnabledCheckBox" Content="Couleur de fond" Checked="OnEffectToggleChanged" Unchecked="OnEffectToggleChanged"/>
                <local:ColorPickerField x:Name="BackgroundColorPicker" Margin="20,2,0,8"/>

                <CheckBox x:Name="BorderEnabledCheckBox" Content="Bordure" Checked="OnEffectToggleChanged" Unchecked="OnEffectToggleChanged"/>
                <StackPanel Orientation="Horizontal" Margin="20,2,0,8">
                    <local:ColorPickerField x:Name="BorderColorPicker"/>
                    <TextBox x:Name="BorderWidthTextBox" Width="60" Margin="8,0,0,0"/>
                </StackPanel>

                <CheckBox x:Name="TextEnabledCheckBox" Content="Texte" Checked="OnEffectToggleChanged" Unchecked="OnEffectToggleChanged"/>
                <StackPanel Margin="20,2,0,8">
                    <TextBox x:Name="TextContentTextBox" Margin="0,0,0,4"/>
                    <local:ColorPickerField x:Name="TextColorPicker"/>
                    <CheckBox x:Name="TextVisibleCheckBox" Content="Visible" Margin="0,4,0,0"/>
                </StackPanel>

                <CheckBox x:Name="ElementVisibleEnabledCheckBox" Content="Visibilite de l'element" Checked="OnEffectToggleChanged" Unchecked="OnEffectToggleChanged"/>
                <CheckBox x:Name="ElementVisibleCheckBox" Content="Visible" Margin="20,2,0,8"/>

                <CheckBox x:Name="OpacityEnabledCheckBox" Content="Opacite" Checked="OnEffectToggleChanged" Unchecked="OnEffectToggleChanged"/>
                <Slider x:Name="OpacitySlider" Minimum="0" Maximum="1" Margin="20,2,0,8"/>

                <CheckBox x:Name="RotationEnabledCheckBox" Content="Rotation (deg)" Checked="OnEffectToggleChanged" Unchecked="OnEffectToggleChanged"/>
                <TextBox x:Name="RotationTextBox" Width="80" Margin="20,2,0,8" HorizontalAlignment="Left"/>

                <CheckBox x:Name="AnimationEnabledCheckBox" Content="Animation" Checked="OnEffectToggleChanged" Unchecked="OnEffectToggleChanged"/>
                <ComboBox x:Name="AnimationComboBox" Margin="20,2,0,8" HorizontalAlignment="Left" Width="160"/>

                <TextBlock Text="Apercu" Foreground="{StaticResource MutedBrush}" Margin="0,8,0,4"/>
                <Border x:Name="PreviewBorder" Width="160" Height="90" BorderBrush="{StaticResource BorderBrushSoft}" BorderThickness="1">
                    <TextBlock x:Name="PreviewText" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
            </StackPanel>
        </ScrollViewer>
```

Replace with:
```xml
        <ScrollViewer Grid.Row="1">
            <StackPanel>
                <TextBlock Text="Modifications d'apparence" FontWeight="SemiBold" Foreground="{StaticResource InkBrush}" Margin="0,0,0,4"/>
                <ListBox x:Name="ActiveEffectsListBox" Height="90" SelectionMode="Single" Margin="0,0,0,4"
                         DisplayMemberPath="Summary"
                         MouseDoubleClick="OnEditActiveEffectClick"/>
                <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
                    <ComboBox x:Name="EffectTypeComboBox" Width="160" Margin="0,0,6,0" DisplayMemberPath="Label"/>
                    <Button Content="+ Ajouter" Click="OnAddActiveEffectClick" Margin="0,0,6,0"/>
                    <Button Content="Editer" Click="OnEditActiveEffectClick" Margin="0,0,6,0"/>
                    <Button Content="Supprimer" Click="OnRemoveActiveEffectClick"/>
                </StackPanel>

                <Border BorderBrush="{StaticResource BorderBrushSoft}" BorderThickness="1" Padding="8" Margin="0,0,0,8">
                    <StackPanel x:Name="EffectEditorPanel" Visibility="Collapsed">
                        <StackPanel x:Name="BackgroundColorEditor" Visibility="Collapsed">
                            <TextBlock Text="Couleur de fond" Foreground="{StaticResource MutedBrush}"/>
                            <local:ColorPickerField x:Name="BackgroundColorPicker" Margin="0,2,0,0"/>
                        </StackPanel>

                        <StackPanel x:Name="BorderEditor" Visibility="Collapsed">
                            <TextBlock Text="Bordure" Foreground="{StaticResource MutedBrush}"/>
                            <StackPanel Orientation="Horizontal" Margin="0,2,0,0">
                                <local:ColorPickerField x:Name="BorderColorPicker"/>
                                <TextBox x:Name="BorderWidthTextBox" Width="60" Margin="8,0,0,0"/>
                            </StackPanel>
                        </StackPanel>

                        <StackPanel x:Name="TextEditor" Visibility="Collapsed">
                            <TextBlock Text="Texte" Foreground="{StaticResource MutedBrush}"/>
                            <TextBox x:Name="TextContentTextBox" Margin="0,2,0,4"/>
                            <local:ColorPickerField x:Name="TextColorPicker" Margin="0,0,0,4"/>
                            <CheckBox x:Name="TextVisibleCheckBox" Content="Visible"/>
                        </StackPanel>

                        <StackPanel x:Name="ElementVisibleEditor" Visibility="Collapsed">
                            <TextBlock Text="Visibilite de l'element" Foreground="{StaticResource MutedBrush}"/>
                            <CheckBox x:Name="ElementVisibleCheckBox" Content="Visible" Margin="0,2,0,0"/>
                        </StackPanel>

                        <StackPanel x:Name="OpacityEditor" Visibility="Collapsed">
                            <TextBlock Text="Opacite" Foreground="{StaticResource MutedBrush}"/>
                            <Slider x:Name="OpacitySlider" Minimum="0" Maximum="1" Margin="0,2,0,0"/>
                        </StackPanel>

                        <StackPanel x:Name="RotationEditor" Visibility="Collapsed">
                            <TextBlock Text="Rotation (deg)" Foreground="{StaticResource MutedBrush}"/>
                            <TextBox x:Name="RotationTextBox" Width="80" Margin="0,2,0,0" HorizontalAlignment="Left"/>
                        </StackPanel>

                        <StackPanel x:Name="AnimationEditor" Visibility="Collapsed">
                            <TextBlock Text="Animation" Foreground="{StaticResource MutedBrush}"/>
                            <ComboBox x:Name="AnimationComboBox" Margin="0,2,0,0" HorizontalAlignment="Left" Width="160"/>
                        </StackPanel>

                        <StackPanel x:Name="ColorFilterEditor" Visibility="Collapsed">
                            <TextBlock Text="Filtre de couleur" Foreground="{StaticResource MutedBrush}"/>
                            <local:ColorPickerField x:Name="ColorFilterColorPicker" Margin="0,2,0,4"/>
                            <TextBlock Text="Opacite du filtre" Foreground="{StaticResource MutedBrush}"/>
                            <Slider x:Name="ColorFilterOpacitySlider" Minimum="0" Maximum="1" Margin="0,2,0,4"/>
                            <CheckBox x:Name="ColorFilterHaloCheckBox" Content="Halo" Checked="OnColorFilterHaloChanged" Unchecked="OnColorFilterHaloChanged"/>
                            <StackPanel x:Name="ColorFilterHaloPanel" Visibility="Collapsed" Margin="0,4,0,0">
                                <TextBlock Text="Couleur du halo" Foreground="{StaticResource MutedBrush}"/>
                                <local:ColorPickerField x:Name="ColorFilterHaloColorPicker" Margin="0,2,0,0"/>
                            </StackPanel>
                        </StackPanel>
                    </StackPanel>
                </Border>

                <TextBlock Text="Apercu" Foreground="{StaticResource MutedBrush}" Margin="0,8,0,4"/>
                <Border x:Name="PreviewBorder" Width="160" Height="90" BorderBrush="{StaticResource BorderBrushSoft}" BorderThickness="1">
                    <TextBlock x:Name="PreviewText" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
            </StackPanel>
        </ScrollViewer>
```

- [ ] **Step 2: Replace the effect-management code in `ElementStateRuleDialog.xaml.cs`**

Find the constructor body:
```csharp
    public ElementStateRuleDialog(ScadaStateRule? existingRule, ScadaTagCatalog? tagCatalog)
    {
        InitializeComponent();
        _tagCatalog = tagCatalog;
        _ruleId = existingRule?.Id ?? Guid.NewGuid().ToString("n");

        AnimationComboBox.ItemsSource = Enum.GetValues<ScadaAnimation>();

        PopulateTagComboBox();
        OperatorComboBox.ItemsSource = _operatorItems;
        OperatorComboBox.SelectedIndex = 4; // "=" par defaut
        BoolTrueRadio.IsChecked = true;
        VariableModeRadio.IsChecked = true; // Mode Variable par defaut

        if (existingRule is not null)
        {
            NameTextBox.Text = existingRule.Name;
            RestoreExpression(existingRule.Expression.Source);
            LoadEffect(existingRule.Effect);
        }

        if (ExpressionModeRadio.IsChecked == true)
            ValidateExpression();
    }
```

Replace with:
```csharp
    public ElementStateRuleDialog(ScadaStateRule? existingRule, ScadaTagCatalog? tagCatalog)
    {
        InitializeComponent();
        _tagCatalog = tagCatalog;
        _ruleId = existingRule?.Id ?? Guid.NewGuid().ToString("n");

        AnimationComboBox.ItemsSource = Enum.GetValues<ScadaAnimation>();

        PopulateTagComboBox();
        OperatorComboBox.ItemsSource = _operatorItems;
        OperatorComboBox.SelectedIndex = 4; // "=" par defaut
        BoolTrueRadio.IsChecked = true;
        VariableModeRadio.IsChecked = true; // Mode Variable par defaut

        if (existingRule is not null)
        {
            NameTextBox.Text = existingRule.Name;
            RestoreExpression(existingRule.Expression.Source);
            LoadEffect(existingRule.Effect);
        }

        RefreshEffectTypeComboBox();
        RefreshActiveEffectsList();
        if (_activeKinds.Count > 0)
        {
            ShowEffectEditor(_activeKinds.First());
            ActiveEffectsListBox.SelectedIndex = 0;
        }

        if (ExpressionModeRadio.IsChecked == true)
            ValidateExpression();
    }

    private enum EffectKind { BackgroundColor, Border, Text, ElementVisible, Opacity, Rotation, Animation, ColorFilter }

    private sealed record EffectTypeItem(EffectKind Kind, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record EffectListItem(EffectKind Kind, string Summary);

    private static readonly (EffectKind Kind, string Label)[] _effectTypeLabels =
    [
        (EffectKind.BackgroundColor, "Couleur de fond"),
        (EffectKind.Border, "Bordure"),
        (EffectKind.Text, "Texte"),
        (EffectKind.ElementVisible, "Visibilite"),
        (EffectKind.Opacity, "Opacite"),
        (EffectKind.Rotation, "Rotation"),
        (EffectKind.Animation, "Animation"),
        (EffectKind.ColorFilter, "Filtre de couleur")
    ];

    private readonly HashSet<EffectKind> _activeKinds = new();

    private void RefreshEffectTypeComboBox()
    {
        var available = _effectTypeLabels
            .Where(x => !_activeKinds.Contains(x.Kind))
            .Select(x => new EffectTypeItem(x.Kind, x.Label))
            .ToArray();
        EffectTypeComboBox.ItemsSource = available;
        if (available.Length > 0)
        {
            EffectTypeComboBox.SelectedIndex = 0;
        }
    }

    private void RefreshActiveEffectsList()
    {
        var items = _effectTypeLabels
            .Where(x => _activeKinds.Contains(x.Kind))
            .Select(x => new EffectListItem(x.Kind, BuildEffectSummary(x.Kind)))
            .ToArray();
        ActiveEffectsListBox.ItemsSource = items;
    }

    private string BuildEffectSummary(EffectKind kind) => kind switch
    {
        EffectKind.BackgroundColor => $"Couleur de fond: {BackgroundColorPicker.Value}",
        EffectKind.Border => $"Bordure: {BorderColorPicker.Value} ({BorderWidthTextBox.Text}px)",
        EffectKind.Text => $"Texte: {(string.IsNullOrWhiteSpace(TextContentTextBox.Text) ? "(vide)" : TextContentTextBox.Text)}",
        EffectKind.ElementVisible => $"Visibilite: {(ElementVisibleCheckBox.IsChecked == true ? "Visible" : "Masque")}",
        EffectKind.Opacity => $"Opacite: {OpacitySlider.Value:0.00}",
        EffectKind.Rotation => $"Rotation: {RotationTextBox.Text} deg",
        EffectKind.Animation => $"Animation: {AnimationComboBox.SelectedItem}",
        EffectKind.ColorFilter => $"Filtre de couleur: {ColorFilterColorPicker.Value} ({ColorFilterOpacitySlider.Value:0.00}){(ColorFilterHaloCheckBox.IsChecked == true ? ", halo" : "")}",
        _ => kind.ToString()
    };

    private void ShowEffectEditor(EffectKind kind)
    {
        EffectEditorPanel.Visibility = Visibility.Visible;
        BackgroundColorEditor.Visibility = kind == EffectKind.BackgroundColor ? Visibility.Visible : Visibility.Collapsed;
        BorderEditor.Visibility = kind == EffectKind.Border ? Visibility.Visible : Visibility.Collapsed;
        TextEditor.Visibility = kind == EffectKind.Text ? Visibility.Visible : Visibility.Collapsed;
        ElementVisibleEditor.Visibility = kind == EffectKind.ElementVisible ? Visibility.Visible : Visibility.Collapsed;
        OpacityEditor.Visibility = kind == EffectKind.Opacity ? Visibility.Visible : Visibility.Collapsed;
        RotationEditor.Visibility = kind == EffectKind.Rotation ? Visibility.Visible : Visibility.Collapsed;
        AnimationEditor.Visibility = kind == EffectKind.Animation ? Visibility.Visible : Visibility.Collapsed;
        ColorFilterEditor.Visibility = kind == EffectKind.ColorFilter ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAddActiveEffectClick(object sender, RoutedEventArgs e)
    {
        if (EffectTypeComboBox.SelectedItem is not EffectTypeItem item)
        {
            return;
        }

        _activeKinds.Add(item.Kind);
        if (item.Kind == EffectKind.ColorFilter)
        {
            if (string.IsNullOrWhiteSpace(ColorFilterColorPicker.Value))
            {
                ColorFilterColorPicker.SetColor("#E53935");
            }
            if (string.IsNullOrWhiteSpace(ColorFilterHaloColorPicker.Value))
            {
                ColorFilterHaloColorPicker.SetColor(ColorFilterColorPicker.Value);
            }
        }

        RefreshEffectTypeComboBox();
        RefreshActiveEffectsList();
        ShowEffectEditor(item.Kind);
        UpdatePreview();
    }

    private void OnEditActiveEffectClick(object sender, RoutedEventArgs e)
    {
        if (ActiveEffectsListBox.SelectedItem is not EffectListItem item)
        {
            return;
        }

        ShowEffectEditor(item.Kind);
    }

    private void OnRemoveActiveEffectClick(object sender, RoutedEventArgs e)
    {
        if (ActiveEffectsListBox.SelectedItem is not EffectListItem item)
        {
            return;
        }

        _activeKinds.Remove(item.Kind);
        RefreshEffectTypeComboBox();
        RefreshActiveEffectsList();
        EffectEditorPanel.Visibility = Visibility.Collapsed;
        UpdatePreview();
    }

    private void OnColorFilterHaloChanged(object sender, RoutedEventArgs e)
    {
        ColorFilterHaloPanel.Visibility = ColorFilterHaloCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        RefreshActiveEffectsList();
    }
```

Find `LoadEffect` (replace the whole method):
```csharp
    private void LoadEffect(ScadaEffectBlock effect)
    {
        if (effect.BackgroundColor is not null)
        {
            BackgroundEnabledCheckBox.IsChecked = true;
            BackgroundColorPicker.SetColor(effect.BackgroundColor);
        }

        if (effect.BorderColor is not null)
        {
            BorderEnabledCheckBox.IsChecked = true;
            BorderColorPicker.SetColor(effect.BorderColor);
            BorderWidthTextBox.Text = (effect.BorderWidth ?? 1).ToString(CultureInfo.InvariantCulture);
        }

        if (effect.TextContent is not null || effect.TextColor is not null || effect.TextVisible is not null)
        {
            TextEnabledCheckBox.IsChecked = true;
            TextContentTextBox.Text = effect.TextContent ?? string.Empty;
            TextColorPicker.SetColor(effect.TextColor ?? "#000000");
            TextVisibleCheckBox.IsChecked = effect.TextVisible ?? true;
        }

        if (effect.ElementVisible is not null)
        {
            ElementVisibleEnabledCheckBox.IsChecked = true;
            ElementVisibleCheckBox.IsChecked = effect.ElementVisible;
        }

        if (effect.Opacity is not null)
        {
            OpacityEnabledCheckBox.IsChecked = true;
            OpacitySlider.Value = effect.Opacity.Value;
        }

        if (effect.Rotation is not null)
        {
            RotationEnabledCheckBox.IsChecked = true;
            RotationTextBox.Text = effect.Rotation.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (effect.Animation is not null)
        {
            AnimationEnabledCheckBox.IsChecked = true;
            AnimationComboBox.SelectedItem = effect.Animation.Value;
        }
    }
```

Replace with:
```csharp
    private void LoadEffect(ScadaEffectBlock effect)
    {
        if (effect.BackgroundColor is not null)
        {
            _activeKinds.Add(EffectKind.BackgroundColor);
            BackgroundColorPicker.SetColor(effect.BackgroundColor);
        }

        if (effect.BorderColor is not null)
        {
            _activeKinds.Add(EffectKind.Border);
            BorderColorPicker.SetColor(effect.BorderColor);
            BorderWidthTextBox.Text = (effect.BorderWidth ?? 1).ToString(CultureInfo.InvariantCulture);
        }

        if (effect.TextContent is not null || effect.TextColor is not null || effect.TextVisible is not null)
        {
            _activeKinds.Add(EffectKind.Text);
            TextContentTextBox.Text = effect.TextContent ?? string.Empty;
            TextColorPicker.SetColor(effect.TextColor ?? "#000000");
            TextVisibleCheckBox.IsChecked = effect.TextVisible ?? true;
        }

        if (effect.ElementVisible is not null)
        {
            _activeKinds.Add(EffectKind.ElementVisible);
            ElementVisibleCheckBox.IsChecked = effect.ElementVisible;
        }

        if (effect.Opacity is not null)
        {
            _activeKinds.Add(EffectKind.Opacity);
            OpacitySlider.Value = effect.Opacity.Value;
        }

        if (effect.Rotation is not null)
        {
            _activeKinds.Add(EffectKind.Rotation);
            RotationTextBox.Text = effect.Rotation.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (effect.Animation is not null)
        {
            _activeKinds.Add(EffectKind.Animation);
            AnimationComboBox.SelectedItem = effect.Animation.Value;
        }

        if (effect.ColorFilterColor is not null)
        {
            _activeKinds.Add(EffectKind.ColorFilter);
            ColorFilterColorPicker.SetColor(effect.ColorFilterColor);
            ColorFilterOpacitySlider.Value = effect.ColorFilterOpacity ?? 1.0;
            ColorFilterHaloCheckBox.IsChecked = effect.ColorFilterHalo ?? false;
            ColorFilterHaloPanel.Visibility = ColorFilterHaloCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ColorFilterHaloColorPicker.SetColor(effect.ColorFilterHaloColor ?? effect.ColorFilterColor);
        }
    }
```

Find `OnEffectToggleChanged` and delete it (no longer referenced — replaced by the click/changed
handlers added above):
```csharp
    private void OnEffectToggleChanged(object sender, RoutedEventArgs e) => UpdatePreview();
```

Find `BuildEffectFromUi` (replace the whole method):
```csharp
    private ScadaEffectBlock BuildEffectFromUi()
    {
        return new ScadaEffectBlock(
            BackgroundColor: BackgroundEnabledCheckBox.IsChecked == true ? BackgroundColorPicker.Value : null,
            BorderColor: BorderEnabledCheckBox.IsChecked == true ? BorderColorPicker.Value : null,
            BorderWidth: BorderEnabledCheckBox.IsChecked == true && double.TryParse(BorderWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ? width : null,
            TextColor: TextEnabledCheckBox.IsChecked == true ? TextColorPicker.Value : null,
            TextContent: TextEnabledCheckBox.IsChecked == true ? TextContentTextBox.Text : null,
            TextVisible: TextEnabledCheckBox.IsChecked == true ? TextVisibleCheckBox.IsChecked : null,
            ElementVisible: ElementVisibleEnabledCheckBox.IsChecked == true ? ElementVisibleCheckBox.IsChecked : null,
            Opacity: OpacityEnabledCheckBox.IsChecked == true ? OpacitySlider.Value : null,
            Rotation: RotationEnabledCheckBox.IsChecked == true && double.TryParse(RotationTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var rotation) ? rotation : null,
            Animation: AnimationEnabledCheckBox.IsChecked == true ? (ScadaAnimation?)AnimationComboBox.SelectedItem : null);
    }
```

Replace with:
```csharp
    private ScadaEffectBlock BuildEffectFromUi()
    {
        return new ScadaEffectBlock(
            BackgroundColor: _activeKinds.Contains(EffectKind.BackgroundColor) ? BackgroundColorPicker.Value : null,
            BorderColor: _activeKinds.Contains(EffectKind.Border) ? BorderColorPicker.Value : null,
            BorderWidth: _activeKinds.Contains(EffectKind.Border) && double.TryParse(BorderWidthTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ? width : null,
            TextColor: _activeKinds.Contains(EffectKind.Text) ? TextColorPicker.Value : null,
            TextContent: _activeKinds.Contains(EffectKind.Text) ? TextContentTextBox.Text : null,
            TextVisible: _activeKinds.Contains(EffectKind.Text) ? TextVisibleCheckBox.IsChecked : null,
            ElementVisible: _activeKinds.Contains(EffectKind.ElementVisible) ? ElementVisibleCheckBox.IsChecked : null,
            Opacity: _activeKinds.Contains(EffectKind.Opacity) ? OpacitySlider.Value : null,
            Rotation: _activeKinds.Contains(EffectKind.Rotation) && double.TryParse(RotationTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var rotation) ? rotation : null,
            Animation: _activeKinds.Contains(EffectKind.Animation) ? (ScadaAnimation?)AnimationComboBox.SelectedItem : null,
            ColorFilterColor: _activeKinds.Contains(EffectKind.ColorFilter) ? ColorFilterColorPicker.Value : null,
            ColorFilterOpacity: _activeKinds.Contains(EffectKind.ColorFilter) ? ColorFilterOpacitySlider.Value : null,
            ColorFilterHalo: _activeKinds.Contains(EffectKind.ColorFilter) ? ColorFilterHaloCheckBox.IsChecked : null,
            ColorFilterHaloColor: _activeKinds.Contains(EffectKind.ColorFilter) && ColorFilterHaloCheckBox.IsChecked == true ? ColorFilterHaloColorPicker.Value : null);
    }
```

- [ ] **Step 3: Build**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet build ScadaBuilderV2.sln --no-restore`
Expected: 0 errors. If `ElementPropertiesDialog.xaml.cs`/`MainWindow.xaml.cs` fail to build, close the
running `ScadaBuilderV2.App.exe` process first (file lock), per the earlier session's build-lock issue.

- [ ] **Step 4: Run the full .NET suite for regressions**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet test ScadaBuilderV2.sln --no-restore`
Expected: same baseline as before this task (4 pre-existing unrelated failures).

- [ ] **Step 5: Manual verification script**

Run the app (`dotnet run --project src/ScadaBuilderV2.App`), open a project, select an Element+, open
its properties (both via the docked panel and via double-click, since `ElementStateRuleDialog` is
shared — verifying once from either entry point covers both):
1. État tab → "+ Ajouter" → dialog opens with an empty active-effects list and the dropdown showing
   all 8 types.
2. Pick "Filtre de couleur", click "+ Ajouter" → a summary line appears in the list, the Filtre de
   couleur editor shows below with a default red color, 100% opacity, halo unchecked.
3. Check "Halo" → a "Couleur du halo" picker appears, pre-filled with the same red.
4. Pick "Couleur de fond" from the (now 7-item) dropdown, "+ Ajouter" → second summary line appears;
   editor switches to show only the background color picker.
5. Double-click the "Filtre de couleur" line in the list → editor switches back to it, values still
   there (opacity, halo, halo color unchanged from step 3).
6. "Supprimer" the "Couleur de fond" line → it disappears from the list, reappears in the dropdown.
7. Save → reopen the same rule (Éditer) → both remaining active effects (Filtre de couleur) are
   restored with their exact values.

- [ ] **Step 6: Commit**

```bash
cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs
git commit -m "feat: cumulable dropdown+list UI for state effects, add Filtre de couleur editor

Replaces 7 always-visible checkboxes with a type dropdown + \"+ Ajouter\" list,
reusing the same underlying controls (now shown one at a time). Shared by both
property surfaces (MainWindow docked panel, ElementPropertiesDialog modal) since
they already construct the same ElementStateRuleDialog class."
```

---

## Task 7: WPF — `ElementReadVariableDialog` (new)

**Files:**
- Create: `src/ScadaBuilderV2.App/ElementReadVariableDialog.xaml`
- Create: `src/ScadaBuilderV2.App/ElementReadVariableDialog.xaml.cs`

**Interfaces:**
- Produces: `ElementReadVariableDialog(ScadaReadVariableRule? existing, ScadaTagCatalog? tagCatalog)` with
  `Result: ScadaReadVariableRule?` — same constructor/result shape as `ElementStateRuleDialog`/
  `ElementCommandDialog`, so Tasks 8-9's glue code follows the identical pattern already used for those.

**No automated test** — same WPF boundary as Task 6. Verified by `dotnet build` + manual script.

- [ ] **Step 1: Create `ElementReadVariableDialog.xaml`**

```xml
<Window x:Class="ScadaBuilderV2.App.ElementReadVariableDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Lecture de variable" Width="420" Height="260"
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
    <Window.Resources>
        <SolidColorBrush x:Key="InkBrush" Color="#0F2A30"/>
        <SolidColorBrush x:Key="MutedBrush" Color="#5E7A82"/>
        <SolidColorBrush x:Key="PanelBrush" Color="#F7FBF5"/>
        <Style x:Key="PrimaryButtonStyle" TargetType="Button">
            <Setter Property="Background" Value="#2090A0"/>
            <Setter Property="BorderBrush" Value="#0F7280"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="Padding" Value="12,6"/>
        </Style>
    </Window.Resources>
    <Grid Background="{StaticResource PanelBrush}" Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0">
            <TextBlock Text="Tag" Foreground="{StaticResource MutedBrush}"/>
            <ComboBox x:Name="TagComboBox" Margin="0,2,0,12" DisplayMemberPath="DisplayName"/>

            <TextBlock Text="Format d'affichage (optionnel)" Foreground="{StaticResource MutedBrush}"/>
            <TextBox x:Name="DisplayFormatTextBox" Margin="0,2,0,4"/>
            <TextBlock Text="Utilisez {valeur} pour inserer la valeur du tag, ex: &quot;Debit: {valeur} L/min&quot;. Vide = valeur brute."
                       Foreground="{StaticResource MutedBrush}" TextWrapping="Wrap" FontSize="11" Margin="0,0,0,8"/>

            <TextBlock x:Name="ValidationText" TextWrapping="Wrap" Foreground="Firebrick"/>
        </StackPanel>

        <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
            <Button Content="Annuler" IsCancel="True" Margin="0,0,8,0"/>
            <Button Content="Enregistrer" Style="{StaticResource PrimaryButtonStyle}" Click="OnSaveClick"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 2: Create `ElementReadVariableDialog.xaml.cs`**

```csharp
using System.Windows;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App;

public partial class ElementReadVariableDialog : Window
{
    private sealed record TagItem(string TagId, string DisplayName);

    private readonly ScadaTagCatalog? _tagCatalog;

    public ElementReadVariableDialog(ScadaReadVariableRule? existing, ScadaTagCatalog? tagCatalog)
    {
        InitializeComponent();
        _tagCatalog = tagCatalog;

        var items = (tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .Where(tag => tag.Enabled)
            .OrderBy(tag => tag.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(tag => new TagItem(tag.Id, tag.AuthoringLabel))
            .ToArray();
        TagComboBox.ItemsSource = items;

        if (existing is not null)
        {
            TagComboBox.SelectedItem = items.FirstOrDefault(t => t.TagId == existing.TagId);
            DisplayFormatTextBox.Text = existing.DisplayFormat ?? string.Empty;
        }
        else if (items.Length > 0)
        {
            TagComboBox.SelectedIndex = 0;
        }
    }

    public ScadaReadVariableRule? Result { get; private set; }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (TagComboBox.SelectedItem is not TagItem tag)
        {
            ValidationText.Text = "Selectionnez un tag.";
            return;
        }

        Result = new ScadaReadVariableRule(
            tag.TagId,
            string.IsNullOrWhiteSpace(DisplayFormatTextBox.Text) ? null : DisplayFormatTextBox.Text.Trim());

        DialogResult = true;
    }
}
```

- [ ] **Step 3: Build**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet build ScadaBuilderV2.sln --no-restore`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.App/ElementReadVariableDialog.xaml src/ScadaBuilderV2.App/ElementReadVariableDialog.xaml.cs
git commit -m "feat: add ElementReadVariableDialog (Tag + display format)"
```

---

## Task 8: WPF — wire "Lecture de variable" into `MainWindow.xaml` (docked panel)

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml:1009-1024` (État tab)
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `ElementReadVariableDialog` (Task 7), `ScadaElementStateConfig.ReadVariable` (Task 1),
  `ScadaScene.WithElementStateConfig` (existing).

- [ ] **Step 1: Add the read-variable section to the État tab**

Find in `MainWindow.xaml` (lines 1009-1024):
```xml
                                        <TabItem Header="Etat">
                                            <StackPanel Margin="8">
                                                <TextBlock Text="Evenement d'affichage d'etat"/>
                                                <ListBox x:Name="StateRulesListBox" Height="220" SelectionMode="Single" Margin="0,6,0,0"
                                                         DisplayMemberPath="Name"
                                                         MouseDoubleClick="OnStateRuleDoubleClick"/>
                                                <StackPanel Orientation="Horizontal" Margin="0,6,0,0">
                                                    <Button Content="+ Ajouter" Click="OnAddStateRuleClick" Margin="0,0,6,0"/>
                                                    <Button Content="Monter" Click="OnMoveStateRuleUpClick" Margin="0,0,6,0"/>
                                                    <Button Content="Descendre" Click="OnMoveStateRuleDownClick" Margin="0,0,6,0"/>
                                                    <Button Content="Editer" Click="OnEditStateRuleClick" Margin="0,0,6,0"/>
                                                    <Button Content="Supprimer" Click="OnDeleteStateRuleClick" Margin="0,0,6,0"/>
                                                    <ToggleButton x:Name="TestStateRuleToggle" Content="Test" Click="OnTestStateRuleToggleClick"/>
                                                </StackPanel>
                                            </StackPanel>
                                        </TabItem>
```

Replace with:
```xml
                                        <TabItem Header="Etat">
                                            <StackPanel Margin="8">
                                                <TextBlock Text="Evenement d'affichage d'etat"/>
                                                <ListBox x:Name="StateRulesListBox" Height="220" SelectionMode="Single" Margin="0,6,0,0"
                                                         DisplayMemberPath="Name"
                                                         MouseDoubleClick="OnStateRuleDoubleClick"/>
                                                <StackPanel Orientation="Horizontal" Margin="0,6,0,0">
                                                    <Button Content="+ Ajouter" Click="OnAddStateRuleClick" Margin="0,0,6,0"/>
                                                    <Button Content="Monter" Click="OnMoveStateRuleUpClick" Margin="0,0,6,0"/>
                                                    <Button Content="Descendre" Click="OnMoveStateRuleDownClick" Margin="0,0,6,0"/>
                                                    <Button Content="Editer" Click="OnEditStateRuleClick" Margin="0,0,6,0"/>
                                                    <Button Content="Supprimer" Click="OnDeleteStateRuleClick" Margin="0,0,6,0"/>
                                                    <ToggleButton x:Name="TestStateRuleToggle" Content="Test" Click="OnTestStateRuleToggleClick"/>
                                                </StackPanel>

                                                <TextBlock Text="Lecture de variable" Margin="0,16,0,0"/>
                                                <TextBlock x:Name="ReadVariableSummaryText" Foreground="{StaticResource MutedBrush}" Margin="0,4,0,4" TextWrapping="Wrap"/>
                                                <StackPanel Orientation="Horizontal">
                                                    <Button x:Name="AddReadVariableButton" Content="+ Lecture de variable..." Click="OnEditReadVariableClick" Margin="0,0,6,0"/>
                                                    <Button x:Name="EditReadVariableButton" Content="Modifier..." Click="OnEditReadVariableClick" Margin="0,0,6,0"/>
                                                    <Button x:Name="RemoveReadVariableButton" Content="Supprimer" Click="OnRemoveReadVariableClick"/>
                                                </StackPanel>
                                            </StackPanel>
                                        </TabItem>
```

- [ ] **Step 2: Add the glue code in `MainWindow.xaml.cs`**

Find (`RefreshStateAndCommandTabs`, around line 5660-5665):
```csharp
    private void RefreshStateAndCommandTabs()
    {
        var element = _activeScene?.FindElementRecursive(_selectedSceneObject?.Id ?? string.Empty);
        StateRulesListBox.ItemsSource = element?.EffectiveStateConfig.States;
        CommandsListBox.ItemsSource = element?.EffectiveCommandConfig.Commands;
    }
```

Replace with:
```csharp
    private void RefreshStateAndCommandTabs()
    {
        var element = _activeScene?.FindElementRecursive(_selectedSceneObject?.Id ?? string.Empty);
        StateRulesListBox.ItemsSource = element?.EffectiveStateConfig.States;
        CommandsListBox.ItemsSource = element?.EffectiveCommandConfig.Commands;

        var readVariable = element?.EffectiveStateConfig.ReadVariable;
        var hasReadVariable = readVariable is not null;
        ReadVariableSummaryText.Text = hasReadVariable
            ? $"Lecture: {FormatProjectTag(readVariable!.TagId)}{(string.IsNullOrWhiteSpace(readVariable.DisplayFormat) ? "" : $" -> {readVariable.DisplayFormat}")}"
            : "Aucune lecture de variable configuree.";
        AddReadVariableButton.Visibility = hasReadVariable ? Visibility.Collapsed : Visibility.Visible;
        EditReadVariableButton.Visibility = hasReadVariable ? Visibility.Visible : Visibility.Collapsed;
        RemoveReadVariableButton.Visibility = hasReadVariable ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnEditReadVariableClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null || _selectedSceneObject is null)
        {
            return;
        }

        var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
        if (element is null)
        {
            return;
        }

        var dialog = new ElementReadVariableDialog(element.EffectiveStateConfig.ReadVariable, _modernProject?.TagCatalog) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        var config = element.EffectiveStateConfig with { ReadVariable = dialog.Result };
        _activeScene = _activeScene.WithElementStateConfig(_selectedSceneObject.Id, config);
        RefreshStateAndCommandTabs();
    }

    private void OnRemoveReadVariableClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null || _selectedSceneObject is null)
        {
            return;
        }

        var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
        if (element is null)
        {
            return;
        }

        var config = element.EffectiveStateConfig with { ReadVariable = null };
        _activeScene = _activeScene.WithElementStateConfig(_selectedSceneObject.Id, config);
        RefreshStateAndCommandTabs();
    }
```

(`FormatProjectTag` already exists in `MainWindow.xaml.cs` — used by `FormatElementEventsSummary`.)

- [ ] **Step 3: Build**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet build ScadaBuilderV2.sln --no-restore`
Expected: 0 errors.

- [ ] **Step 4: Manual verification**

Run the app, select an Element+ of kind Text, État tab: confirm "Aucune lecture de variable
configuree." shows initially with only "+ Lecture de variable..." visible; click it, pick a tag,
save; confirm the summary updates and the button set switches to Modifier/Supprimer; click
Supprimer; confirm it reverts to the initial empty state.

- [ ] **Step 5: Commit**

```bash
cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs
git commit -m "feat: wire Lecture de variable section into the docked Etat tab"
```

---

## Task 9: WPF — wire "Lecture de variable" into `ElementPropertiesDialog` (modal)

**Files:**
- Modify: `src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml:225-241` (État tab)
- Modify: `src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml.cs`

**Interfaces:**
- Same as Task 8, adapted to this surface's `currentElement`/`SaveStateConfig` pattern instead of
  `_activeScene.WithElementStateConfig`.

- [ ] **Step 1: Add the read-variable section to the État tab**

Find in `ElementPropertiesDialog.xaml` (lines 225-241):
```xml
            <TabItem Header="Etat">
                <StackPanel Margin="0,8,8,0">
                    <TextBlock Text="Evenement d'affichage d'etat"
                               FontWeight="SemiBold"
                               Foreground="{StaticResource InkBrush}"/>
                    <ListBox x:Name="StateRulesListBox" Height="220" SelectionMode="Single" Margin="0,8,0,0"
                             DisplayMemberPath="Name"
                             MouseDoubleClick="OnStateRuleDoubleClick"/>
                    <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                        <Button Content="+ Ajouter" Click="OnAddStateRuleClick" Margin="0,0,6,0"/>
                        <Button Content="Monter" Click="OnMoveStateRuleUpClick" Margin="0,0,6,0"/>
                        <Button Content="Descendre" Click="OnMoveStateRuleDownClick" Margin="0,0,6,0"/>
                        <Button Content="Editer" Click="OnEditStateRuleClick" Margin="0,0,6,0"/>
                        <Button Content="Supprimer" Click="OnDeleteStateRuleClick"/>
                    </StackPanel>
                </StackPanel>
            </TabItem>
```

Replace with:
```xml
            <TabItem Header="Etat">
                <StackPanel Margin="0,8,8,0">
                    <TextBlock Text="Evenement d'affichage d'etat"
                               FontWeight="SemiBold"
                               Foreground="{StaticResource InkBrush}"/>
                    <ListBox x:Name="StateRulesListBox" Height="220" SelectionMode="Single" Margin="0,8,0,0"
                             DisplayMemberPath="Name"
                             MouseDoubleClick="OnStateRuleDoubleClick"/>
                    <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                        <Button Content="+ Ajouter" Click="OnAddStateRuleClick" Margin="0,0,6,0"/>
                        <Button Content="Monter" Click="OnMoveStateRuleUpClick" Margin="0,0,6,0"/>
                        <Button Content="Descendre" Click="OnMoveStateRuleDownClick" Margin="0,0,6,0"/>
                        <Button Content="Editer" Click="OnEditStateRuleClick" Margin="0,0,6,0"/>
                        <Button Content="Supprimer" Click="OnDeleteStateRuleClick"/>
                    </StackPanel>

                    <TextBlock Text="Lecture de variable" FontWeight="SemiBold" Foreground="{StaticResource InkBrush}" Margin="0,16,0,0"/>
                    <TextBlock x:Name="ReadVariableSummaryText" Foreground="{StaticResource MutedBrush}" Margin="0,4,0,4" TextWrapping="Wrap"/>
                    <StackPanel Orientation="Horizontal">
                        <Button x:Name="AddReadVariableButton" Content="+ Lecture de variable..." Click="OnEditReadVariableClick" Margin="0,0,6,0"/>
                        <Button x:Name="EditReadVariableButton" Content="Modifier..." Click="OnEditReadVariableClick" Margin="0,0,6,0"/>
                        <Button x:Name="RemoveReadVariableButton" Content="Supprimer" Click="OnRemoveReadVariableClick"/>
                    </StackPanel>
                </StackPanel>
            </TabItem>
```

- [ ] **Step 2: Add the glue code in `ElementPropertiesDialog.xaml.cs`**

Find `RefreshStateAndCommandLists`:
```csharp
    private void RefreshStateAndCommandLists()
    {
        StateRulesListBox.ItemsSource = currentElement.EffectiveStateConfig.States;
        CommandsListBox.ItemsSource = currentElement.EffectiveCommandConfig.Commands;
    }
```

Replace with:
```csharp
    private void RefreshStateAndCommandLists()
    {
        StateRulesListBox.ItemsSource = currentElement.EffectiveStateConfig.States;
        CommandsListBox.ItemsSource = currentElement.EffectiveCommandConfig.Commands;

        var readVariable = currentElement.EffectiveStateConfig.ReadVariable;
        var hasReadVariable = readVariable is not null;
        ReadVariableSummaryText.Text = hasReadVariable
            ? $"Lecture: {readVariable!.TagId}{(string.IsNullOrWhiteSpace(readVariable.DisplayFormat) ? "" : $" -> {readVariable.DisplayFormat}")}"
            : "Aucune lecture de variable configuree.";
        AddReadVariableButton.Visibility = hasReadVariable ? Visibility.Collapsed : Visibility.Visible;
        EditReadVariableButton.Visibility = hasReadVariable ? Visibility.Visible : Visibility.Collapsed;
        RemoveReadVariableButton.Visibility = hasReadVariable ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnEditReadVariableClick(object sender, RoutedEventArgs e)
    {
        if (SaveStateConfig is null)
        {
            return;
        }

        var dialog = new ElementReadVariableDialog(currentElement.EffectiveStateConfig.ReadVariable, tagCatalog) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        currentElement = SaveStateConfig(currentElement.EffectiveStateConfig with { ReadVariable = dialog.Result });
        RefreshStateAndCommandLists();
    }

    private void OnRemoveReadVariableClick(object sender, RoutedEventArgs e)
    {
        if (SaveStateConfig is null)
        {
            return;
        }

        currentElement = SaveStateConfig(currentElement.EffectiveStateConfig with { ReadVariable = null });
        RefreshStateAndCommandLists();
    }
```

Check the field name for the tag catalog constructor parameter used elsewhere in this file (the
`ElementCommandDialog`/`ElementStateRuleDialog` construction calls, e.g. around line 60/137) — reuse
that exact field name (likely `tagCatalog`, a private field set in the constructor) instead of
introducing a new one.

**Note for the implementer:** `ReadVariableSummaryText.Text` in this file uses `readVariable!.TagId`
directly (no `FormatProjectTag`-style label lookup) because `ElementPropertiesDialog` does not carry
`_modernProject` — only `tagCatalog`. This is an intentional, small display inconsistency between the
two surfaces (docked panel shows the tag's authoring label, this modal shows the raw tag id) that is
acceptable for this iteration; note it as a possible follow-up, not a blocking gap.

- [ ] **Step 3: Build**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet build ScadaBuilderV2.sln --no-restore`
Expected: 0 errors.

- [ ] **Step 4: Manual verification**

Double-click an Element+ of kind Text to open `ElementPropertiesDialog`; repeat the same script as
Task 8 Step 4 on this surface.

- [ ] **Step 5: Commit**

```bash
cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml.cs
git commit -m "feat: wire Lecture de variable section into the modal Etat tab"
```

---

## Task 10: WPF — one-`Kind`-per-Element+ guard-rail for Commande

**Files:**
- Modify: `src/ScadaBuilderV2.App/ElementCommandDialog.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/ElementCommandDialog.xaml` (validation text)
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs` (`OnAddCommandClick`/`OnEditCommandClick`)
- Modify: `src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml.cs` (`OnAddCommandClick`/`OnEditCommandClick`)

**Interfaces:**
- `ElementCommandDialog` constructor gains a 4th parameter: `IReadOnlyCollection<ScadaCommandKind> usedKinds`
  (the `Kind`s already used by *other* commands on this element — empty when adding, excludes the
  command being edited). Existing 3-parameter callers must be updated; there are no other constructors
  to preserve (`Result: ScadaCommandBinding?` unchanged).

- [ ] **Step 1: Add a validation TextBlock to `ElementCommandDialog.xaml`**

Find (end of the file, around line 56-63):
```xml
            <CheckBox x:Name="ConfirmationCheckBox" Content="Demander confirmation" Margin="0,8,0,4"/>
            <TextBox x:Name="ConfirmationMessageTextBox" Margin="0,0,0,8"/>
        </StackPanel>

        <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="Annuler" IsCancel="True" Margin="0,0,8,0"/>
            <Button Content="Enregistrer" Style="{StaticResource PrimaryButtonStyle}" Click="OnSaveClick"/>
        </StackPanel>
```

Replace with:
```xml
            <CheckBox x:Name="ConfirmationCheckBox" Content="Demander confirmation" Margin="0,8,0,4"/>
            <TextBox x:Name="ConfirmationMessageTextBox" Margin="0,0,0,8"/>
        </StackPanel>

        <StackPanel Grid.Row="1">
            <TextBlock x:Name="ValidationText" TextWrapping="Wrap" Foreground="Firebrick" Margin="0,0,0,8"/>
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                <Button Content="Annuler" IsCancel="True" Margin="0,0,8,0"/>
                <Button Content="Enregistrer" Style="{StaticResource PrimaryButtonStyle}" Click="OnSaveClick"/>
            </StackPanel>
        </StackPanel>
```

- [ ] **Step 2: Modify `ElementCommandDialog.xaml.cs`**

Find:
```csharp
    public ElementCommandDialog(ScadaCommandBinding? existingCommand, IReadOnlyList<ScadaSceneReference> pageReferences, ScadaTagCatalog? tagCatalog)
    {
        InitializeComponent();
        _pageReferences = pageReferences;
        _commandId = existingCommand?.Id ?? Guid.NewGuid().ToString("n");

        TriggerComboBox.ItemsSource = Enum.GetValues<ScadaCommandTrigger>();
        KindComboBox.ItemsSource = Enum.GetValues<ScadaCommandKind>();
```

Replace with:
```csharp
    private readonly IReadOnlyCollection<ScadaCommandKind> _usedKinds;

    public ElementCommandDialog(
        ScadaCommandBinding? existingCommand,
        IReadOnlyList<ScadaSceneReference> pageReferences,
        ScadaTagCatalog? tagCatalog,
        IReadOnlyCollection<ScadaCommandKind> usedKinds)
    {
        InitializeComponent();
        _pageReferences = pageReferences;
        _commandId = existingCommand?.Id ?? Guid.NewGuid().ToString("n");
        _usedKinds = usedKinds;

        TriggerComboBox.ItemsSource = Enum.GetValues<ScadaCommandTrigger>();
        KindComboBox.ItemsSource = Enum.GetValues<ScadaCommandKind>().Where(kind => !usedKinds.Contains(kind)).ToArray();
```

Find `OnSaveClick`:
```csharp
    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text) || TriggerComboBox.SelectedItem is null || KindComboBox.SelectedItem is null)
        {
            return;
        }

        var kind = (ScadaCommandKind)KindComboBox.SelectedItem;
```

Replace with:
```csharp
    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text) || TriggerComboBox.SelectedItem is null || KindComboBox.SelectedItem is null)
        {
            return;
        }

        var kind = (ScadaCommandKind)KindComboBox.SelectedItem;
        if (_usedKinds.Contains(kind))
        {
            ValidationText.Text = $"Une commande '{kind}' existe deja pour cet Element+.";
            return;
        }
```

- [ ] **Step 3: Update the two callers in `MainWindow.xaml.cs`**

Find `OnAddCommandClick`:
```csharp
        var dialog = new ElementCommandDialog(null, GetCurrentSceneReferences(), _modernProject?.TagCatalog) { Owner = this };
```

Replace with:
```csharp
        var usedKinds = element.EffectiveCommandConfig.Commands.Select(c => c.Kind).ToArray();
        var dialog = new ElementCommandDialog(null, GetCurrentSceneReferences(), _modernProject?.TagCatalog, usedKinds) { Owner = this };
```

**Note:** at this exact call site, `element` isn't resolved yet in the existing code — check the
existing method body (`OnAddCommandClick`, around line 5799-5824): `element` is currently looked up
*after* the dialog is shown (`var element = _activeScene.FindElementRecursive(...)` comes after
`dialog.ShowDialog()`). Move that lookup to happen *before* constructing the dialog, so `usedKinds`
can be computed from it. The full corrected method:
```csharp
    private void OnAddCommandClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null || _selectedSceneObject is null)
        {
            return;
        }

        var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
        if (element is null)
        {
            return;
        }

        var usedKinds = element.EffectiveCommandConfig.Commands.Select(c => c.Kind).ToArray();
        var dialog = new ElementCommandDialog(null, GetCurrentSceneReferences(), _modernProject?.TagCatalog, usedKinds) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        var config = element.EffectiveCommandConfig with
        {
            Commands = element.EffectiveCommandConfig.Commands.Append(dialog.Result).ToArray()
        };
        _activeScene = _activeScene.WithElementCommandConfig(_selectedSceneObject.Id, config);
        RefreshStateAndCommandTabs();
    }
```

Find `OnEditCommandClick`:
```csharp
        var dialog = new ElementCommandDialog(selected, GetCurrentSceneReferences(), _modernProject?.TagCatalog) { Owner = this };
```

Replace with:
```csharp
        var usedKinds = element.EffectiveCommandConfig.Commands
            .Where(c => c.Id != selected.Id)
            .Select(c => c.Kind)
            .ToArray();
        var dialog = new ElementCommandDialog(selected, GetCurrentSceneReferences(), _modernProject?.TagCatalog, usedKinds) { Owner = this };
```

**Note:** same ordering issue — `OnEditCommandClick`'s existing body resolves `element` *after* the
dialog call too. Move the `element` lookup (and the `CommandsListBox.SelectedItem is not ScadaCommandBinding selected`
check, which must also happen first since `usedKinds` needs `selected.Id`) above the dialog
construction. The full corrected method:
```csharp
    private void OnEditCommandClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null || _selectedSceneObject is null)
        {
            return;
        }

        if (CommandsListBox.SelectedItem is not ScadaCommandBinding selected)
        {
            return;
        }

        var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
        if (element is null)
        {
            return;
        }

        var usedKinds = element.EffectiveCommandConfig.Commands
            .Where(c => c.Id != selected.Id)
            .Select(c => c.Kind)
            .ToArray();
        var dialog = new ElementCommandDialog(selected, GetCurrentSceneReferences(), _modernProject?.TagCatalog, usedKinds) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        var commands = element.EffectiveCommandConfig.Commands
            .Select(command => command.Id == dialog.Result.Id ? dialog.Result : command)
            .ToArray();
        _activeScene = _activeScene.WithElementCommandConfig(_selectedSceneObject.Id, element.EffectiveCommandConfig with { Commands = commands });
        RefreshStateAndCommandTabs();
    }
```

- [ ] **Step 4: Update the two callers in `ElementPropertiesDialog.xaml.cs`**

Same reordering pattern (this surface already resolves `currentElement` synchronously via the field,
no `FindElementRecursive` needed — only the `CommandsListBox.SelectedItem` check needs to move above
the dialog construction in `OnEditCommandClick`). Find:
```csharp
    private void OnAddCommandClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ElementCommandDialog(null, pageReferences, tagCatalog) { Owner = this };
```

Replace with:
```csharp
    private void OnAddCommandClick(object sender, RoutedEventArgs e)
    {
        var usedKinds = currentElement.EffectiveCommandConfig.Commands.Select(c => c.Kind).ToArray();
        var dialog = new ElementCommandDialog(null, pageReferences, tagCatalog, usedKinds) { Owner = this };
```

Find:
```csharp
    private void OnEditCommandClick(object sender, RoutedEventArgs e)
    {
        if (CommandsListBox.SelectedItem is not ScadaCommandBinding selected || SaveCommandConfig is null)
        {
            return;
        }

        var dialog = new ElementCommandDialog(selected, pageReferences, tagCatalog) { Owner = this };
```

Replace with:
```csharp
    private void OnEditCommandClick(object sender, RoutedEventArgs e)
    {
        if (CommandsListBox.SelectedItem is not ScadaCommandBinding selected || SaveCommandConfig is null)
        {
            return;
        }

        var usedKinds = currentElement.EffectiveCommandConfig.Commands
            .Where(c => c.Id != selected.Id)
            .Select(c => c.Kind)
            .ToArray();
        var dialog = new ElementCommandDialog(selected, pageReferences, tagCatalog, usedKinds) { Owner = this };
```

Confirmed field names in this file (`ElementPropertiesDialog.xaml.cs:20-21`): `private readonly
IReadOnlyList<ScadaSceneReference> pageReferences;` and `private readonly ScadaTagCatalog? tagCatalog;`
— the replacements above use the correct existing names, no renaming needed.

- [ ] **Step 5: Build**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet build ScadaBuilderV2.sln --no-restore`
Expected: 0 errors. This is a constructor signature change — the build will fail loudly at any missed
call site, which confirms all 4 callers (2 per surface) were updated.

- [ ] **Step 6: Run the full .NET suite for regressions**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet test ScadaBuilderV2.sln --no-restore`
Expected: same baseline (4 pre-existing unrelated failures).

- [ ] **Step 7: Manual verification**

Open an Element+'s Commande tab (either surface), "+ Ajouter" a `Navigate` command → save. "+ Ajouter"
again → confirm `Navigate` no longer appears in the Kind dropdown (only the other 6 remain). Add
`OpenPopup`. Edit the `Navigate` command → confirm its own Kind (`Navigate`) is still selectable/shown
for itself (not excluded), and `OpenPopup` is excluded.

- [ ] **Step 8: Commit**

```bash
cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git add src/ScadaBuilderV2.App/ElementCommandDialog.xaml src/ScadaBuilderV2.App/ElementCommandDialog.xaml.cs src/ScadaBuilderV2.App/MainWindow.xaml.cs src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml.cs
git commit -m "feat: enforce one command Kind per Element+ (Navigate, OpenPopup, etc.)"
```

---

## Task 11: TF100Web — end-to-end deploy proof (no server-side changes needed)

**Files:**
- Modify: `frontend/tests_scada_deploy.py` (TF100Web repo)

**Interfaces:**
- Consumes: `deploy_scada_builder` management command (existing, TF100Web), a hand-built `.sb2` zip
  fixture containing the new `readVariable`/`colorFilter*` JSON shapes (hand-built rather than
  produced by the .NET exporter, since this test lives in the TF100Web repo and has no dependency on
  the Builder V2 build — matches the existing `_build_test_package` pattern in this same file).

- [ ] **Step 1: Write the test**

Append to `frontend/tests_scada_deploy.py` (TF100Web repo):
```python
class ScadaPackagePageServesNewEffectFieldsTests(SimpleTestCase):
    def test_page_with_read_variable_and_color_filter_serves_without_error(self):
        with TemporaryDirectory() as static_root, TemporaryDirectory() as pkg_dir:
            manifest = {
                "HomePageId": "win00009",
                "Pages": [{"Id": "win00009", "Type": "default", "IncludeInBuild": True}],
            }
            state_config = {
                "qualityFallback": {"opacity": 0.4},
                "defaultEffect": {},
                "readVariable": {"tagId": "tf100.mapping.42", "displayFormat": "Debit: {valeur} L/min"},
                "states": [{
                    "id": "s1", "name": "Alarme", "enabled": True,
                    "expression": {"source": "true", "ast": {"type": "literalBool", "value": True}},
                    "effect": {
                        "colorFilterColor": "#E53935", "colorFilterOpacity": 0.35,
                        "colorFilterHalo": True, "colorFilterHaloColor": "#E53935",
                    },
                }],
            }
            import html
            page_html = (
                '<!doctype html><html><body>'
                f'<div id="ft100-win00009" data-scada-width="1280" data-scada-height="873">'
                f'<div id="ft100-win00009__el1" data-scada-state-config="{html.escape(json.dumps(state_config))}">'
                '<span data-scada-text>---</span></div></div>'
                '<link rel="stylesheet" href="css/win00009.abc12345.css">'
                '</body></html>'
            )

            package_path = Path(pkg_dir) / "export.sb2"
            with zipfile.ZipFile(package_path, "w") as zf:
                zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/manifest.json", json.dumps(manifest))
                zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/win00009/win00009.html", page_html)
                zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/win00009/css/win00009.abc12345.css", "")
                zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/scada-runtime.deadbeef.js", "// runtime")

            with override_settings(STATIC_ROOT=static_root, TF100_INDUSTRIAL_DEPLOYMENT=True):
                call_command("deploy_scada_builder", str(package_path))

                from django.contrib.auth import get_user_model
                from .models import StationConfig

                User = get_user_model()
                user = User.objects.create_user(username="tester2", password="x")
                self.client.force_login(user)
                StationConfig.objects.update_or_create(
                    pk=1, defaults={"station_type": StationConfig.StationTypeChoices.SCADA_BUILDER_2}
                )

                response = self.client.get("/visualisation/scada/page/win00009/")
                self.assertEqual(response.status_code, 200)
                payload = response.json()
                self.assertIn("readVariable", payload["html"])
                self.assertIn("colorFilterColor", payload["html"])
                self.assertEqual(payload["css_hash"], "abc12345")
```

Add `from django.test import TestCase` is not needed (this uses `self.client`, available on
`SimpleTestCase` via Django's test client, which doesn't require DB access — confirmed by checking
that `scada_package_page`'s only DB read is `StationConfig.objects.filter(pk=1).first()`; creating a
user via `create_user` DOES need a DB, so this specific test class must be `django.test.TestCase`,
not `SimpleTestCase` — override the class declaration accordingly:
`class ScadaPackagePageServesNewEffectFieldsTests(TestCase):`).

- [ ] **Step 2: Attempt to run it, document the environment limitation**

Run: `cd "F:\Projet\Git\TF100Web" && python manage.py test frontend.tests_scada_deploy.ScadaPackagePageServesNewEffectFieldsTests -v 2`

This will very likely hit the same two pre-existing, unrelated environment blockers already
documented earlier in this session: `ModuleNotFoundError: No module named 'fcntl'` (Linux-only import
in `protocol/opcua_browse.py`, transitively imported via URL resolution) and/or a MySQL connection
failure for the test database on this Windows dev machine. If either occurs, fall back to the
isolated-verification approach already used successfully for this repo's other tests this session:

```bash
cd "F:\Projet\Git\TF100Web" && python -c "
import django, os
os.environ.setdefault('DJANGO_SETTINGS_MODULE', 'tf100web.settings')
django.setup()
# then exercise deploy_scada_builder + a direct call into frontend.views.scada_package_page's
# helper functions (_extract_html_element_by_id, _extract_css_hash_from_html) against the same
# hand-built page_html string used in the test above, asserting the fragment contains
# 'readVariable' and 'colorFilterColor', and css_hash == 'abc12345' -- this exercises the exact
# same parsing logic without needing the Django test client/URL routing/DB.
"
```

This isolated path is sufficient to prove the design's §8 claim (TF100Web's HTML/CSS extraction is
opaque to the new JSON fields) even when the full Django test runner can't execute in this sandbox —
the same reasoning already applied successfully to Tasks 4 and 5 of the prior correction plan this
session.

- [ ] **Step 3: Commit**

```bash
cd "F:\Projet\Git\TF100Web"
git add frontend/tests_scada_deploy.py
git commit -m "test: prove scada_package_page serves readVariable/colorFilter fields unchanged

No Django-side parsing of data-scada-state-config exists (confirmed by reading
_extract_html_element_by_id/_extract_css_hash_from_html) -- this test locks that
in so a future TF100Web change can't silently start depending on the JSON shape."
```

---

## Task 12: Final end-to-end verification

**Files:** none (verification only).

- [ ] **Step 1: Full .NET suite**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2" && dotnet build ScadaBuilderV2.sln --no-restore && dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 0 build errors; same 4 pre-existing `WebViewContextMenuScriptTests` failures as the session
baseline, everything else green — including all `Ft100SceneExporterTests`, `ScadaEffectBlockTests`,
`ScadaElementStateConfigTests`, `RuntimeJsModulesTests`.

- [ ] **Step 2: Full Node runtime-js suite**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test *.test.mjs`
Expected: all pass (15 tests: 7 effect-applier, 2 expression-evaluator, 3 state-engine, 3 command-dispatcher).

- [ ] **Step 3: TF100Web verification (or isolated fallback per Task 11 Step 2)**

Expected: no new failures beyond the documented pre-existing environment baseline.

- [ ] **Step 4: Manual end-to-end script (real app, real export)**

1. In SCADA Builder V2, on a Text Element+: configure "Lecture de variable" (Task 8/9) with a tag,
   and add a State rule (Task 6) with a `Filtre de couleur` (red, 40% opacity, halo on) triggered by
   some condition. Export the scene (`.sb2` or full project archive).
2. Confirm the exported HTML for that element contains `<span data-scada-text>` and the
   `data-scada-state-config` JSON contains both `readVariable` and `colorFilterColor` (open the
   exported `.html` file in a text editor, or unzip the `.sb2` and inspect).
3. Open the exported `.html` directly in a browser (no TF100Web needed for this check — the runtime
   is self-contained). Confirm: the Text element continuously shows the tag's value (or `---` if no
   live value in standalone mode), and — if a live/mock tag value can be injected via
   `window.scadaBuilderTagValues` in devtools — the color filter overlay and halo appear when the
   state condition is met, and the read-variable text stays visible even while the color filter is
   active (proving the two are independent, per D2).

- [ ] **Step 5: If all checks pass, this plan is complete.** If any check fails, return to Systematic
  Debugging (Phase 1) rather than layering a fix on an unverified change.
