# Element+ Studio Import Crash — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop Studio Element+ from crashing silently when importing large legacy batches (win00059, 152 items) by shrinking captured style payloads and adding a size-guarded WebView2 navigation fallback plus a global exception handler.

**Architecture:** Two independent fixes. Fix A narrows the CSS captured during legacy extraction in the main app from a ~300-property `getComputedStyle()` dump to a render-relevant allowlist (keeps computed values, so inheritance stays resolved). Fix B makes Studio Element+ resilient: a pure size-guard helper decides between `NavigateToString` (small docs) and a temp-file `Navigate` fallback (large docs), and a `DispatcherUnhandledException` handler replaces silent process death with a visible error.

**Tech Stack:** .NET 8, WPF (`net8.0-windows`), WebView2, MSTest. Extraction logic is a JS string constant in C#; Studio rendering is C#-side document building. Tests are source-text assertions (WPF projects are not referenced by the test project) plus one true unit test against a pure helper in `ScadaBuilderV2.Application`.

## Global Constraints

- Editor artifacts must never leak into export geometry (selection overlays, handles, drag rectangles, zoom, pan are editor-only; never in `.sep`/`.sb2`).
- Preview / build / export must stay in parity by consuming one project model.
- The test project (`tests/ScadaBuilderV2.Tests`, `net8.0`) does **not** reference the WPF app projects; assert on WPF-app code via source-text reading (`ReadMainWindowSource()`, `ReadStudioFile(...)`), and only via a real assembly reference for code in `ScadaBuilderV2.Application`.
- No existing `.sep`/`.sb2` file is modified retroactively — Fix A only affects new extractions.
- Public APIs require XML docs.
- Commit after each task (frequent commits).
- Build: `dotnet build ScadaBuilderV2.sln`. Test: `dotnet test ScadaBuilderV2.sln --no-restore`.

---

## File Structure

- `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs` — **modify** `toElementMessage` (lines 552-555): swap the exclusion filter for an allowlist include filter.
- `src/ScadaBuilderV2.Application/ElementStudio/WebViewDocumentSizeGuard.cs` — **create** pure helper deciding when a document exceeds the `NavigateToString` limit (referenceable by the test project).
- `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml.cs` — **modify** `OnLoaded` (line 72) and `RefreshWorkzoneAsync` (line 697) to route through a new private `NavigateLegacySource(string)` helper that applies the size guard.
- `src/ScadaBuilderV2.ElementStudio.App/App.xaml` + `App.xaml.cs` — **modify** to register and handle `DispatcherUnhandledException`.
- `tests/ScadaBuilderV2.Tests/WebViewDocumentSizeGuardTests.cs` — **create** unit tests for the pure guard.
- `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` — **modify**: add a source-text test asserting the allowlist replaced the exclusion filter.
- `tests/ScadaBuilderV2.Tests/ElementStudioSourceRenderingTests.cs` — **modify**: update the assertion at line 14 (the exact `NavigateToString(BuildLegacySourceDocument(workspace))` call moves behind `NavigateLegacySource`).

---

## Task 1: Legacy style capture allowlist (Fix A)

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:552-555`
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: nothing consumed by other tasks (JS behavior change only). The `computedStyleText` variable and `legacyMarkup` output keep the same names and shape; only the set of serialized CSS properties shrinks.

- [ ] **Step 1: Write the failing test**

Add this method to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` (before the `ReadMainWindowSource()` helper):

```csharp
    [TestMethod]
    public void LegacyStyleCaptureUsesRenderRelevantAllowlist()
    {
        var source = ReadMainWindowSource();

        // The allowlist itself and a few load-bearing entries must be present.
        StringAssert.Contains(source, "const legacyStyleAllowlist = new Set([");
        StringAssert.Contains(source, "'fill'");
        StringAssert.Contains(source, "'stroke'");
        StringAssert.Contains(source, "'stroke-width'");
        StringAssert.Contains(source, "'filter'");
        StringAssert.Contains(source, "'font-family'");
        StringAssert.Contains(source, "'color'");
        StringAssert.Contains(source, ".filter(name => legacyStyleAllowlist.has(name))");

        // The old exclusion-based capture (serializing every computed property) is gone.
        Assert.IsFalse(
            source.Contains(".filter(name => !['outline', 'outline-color'", StringComparison.Ordinal),
            "Legacy capture must not serialize the full getComputedStyle() dump via an exclusion filter.");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "Name=LegacyStyleCaptureUsesRenderRelevantAllowlist"`
Expected: FAIL — the allowlist strings are absent and the exclusion-filter string is still present.

- [ ] **Step 3: Apply the allowlist in the extraction script**

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, replace the current block (lines 552-555):

```javascript
      computedStyleText = Array.from(computed)
        .filter(name => !['outline', 'outline-color', 'outline-style', 'outline-width', 'outline-offset', 'box-shadow', 'cursor'].includes(name))
        .map(name => `${name}: ${computed.getPropertyValue(name)};`)
        .join(' ');
```

with:

```javascript
      const legacyStyleAllowlist = new Set([
        'fill', 'fill-opacity', 'fill-rule',
        'stroke', 'stroke-width', 'stroke-dasharray', 'stroke-linecap', 'stroke-linejoin', 'stroke-opacity',
        'opacity', 'visibility', 'display',
        'font-family', 'font-size', 'font-weight', 'font-style', 'color',
        'text-align', 'text-decoration', 'text-transform', 'letter-spacing',
        'background-color', 'background-image',
        'border-color', 'border-width', 'border-style', 'border-radius',
        'filter'
      ]);
      computedStyleText = Array.from(computed)
        .filter(name => legacyStyleAllowlist.has(name))
        .map(name => `${name}: ${computed.getPropertyValue(name)};`)
        .join(' ');
```

Leave everything else in `toElementMessage` unchanged (the `clone.setAttribute('style', ...)` merge, `rawMetadata`, and returned fields stay identical).

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "Name=LegacyStyleCaptureUsesRenderRelevantAllowlist"`
Expected: PASS.

- [ ] **Step 5: Run the full existing script-contract suite to confirm no regression**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~WebViewContextMenuScriptTests"`
Expected: PASS (all existing assertions still hold — the change only narrows serialized properties).

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "fix: capture render-relevant CSS allowlist in legacy extraction

Replaces the full ~300-property getComputedStyle() dump (~12KB/element) with
a render-relevant allowlist, cutting per-element markup ~96% while keeping
computed values so inheritance stays resolved. Prevents oversized import
packages that overflow WebView2 NavigateToString in Studio Element+."
```

---

## Task 2: Pure WebView2 document size-guard helper (Fix B, part 1)

**Files:**
- Create: `src/ScadaBuilderV2.Application/ElementStudio/WebViewDocumentSizeGuard.cs`
- Test: `tests/ScadaBuilderV2.Tests/WebViewDocumentSizeGuardTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `ScadaBuilderV2.Application.ElementStudio.WebViewDocumentSizeGuard.NavigateToStringMaxCharacters` (`public const int`), and `WebViewDocumentSizeGuard.ExceedsNavigateToStringLimit(string? document)` → `bool`. Consumed by Task 3.

- [ ] **Step 1: Write the failing test**

Create `tests/ScadaBuilderV2.Tests/WebViewDocumentSizeGuardTests.cs`:

```csharp
using ScadaBuilderV2.Application.ElementStudio;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class WebViewDocumentSizeGuardTests
{
    [TestMethod]
    public void NullDocumentDoesNotExceedLimit()
    {
        Assert.IsFalse(WebViewDocumentSizeGuard.ExceedsNavigateToStringLimit(null));
    }

    [TestMethod]
    public void SmallDocumentDoesNotExceedLimit()
    {
        var document = new string('a', 1000);
        Assert.IsFalse(WebViewDocumentSizeGuard.ExceedsNavigateToStringLimit(document));
    }

    [TestMethod]
    public void DocumentAtLimitDoesNotExceedLimit()
    {
        var document = new string('a', WebViewDocumentSizeGuard.NavigateToStringMaxCharacters);
        Assert.IsFalse(WebViewDocumentSizeGuard.ExceedsNavigateToStringLimit(document));
    }

    [TestMethod]
    public void DocumentOverLimitExceedsLimit()
    {
        var document = new string('a', WebViewDocumentSizeGuard.NavigateToStringMaxCharacters + 1);
        Assert.IsTrue(WebViewDocumentSizeGuard.ExceedsNavigateToStringLimit(document));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~WebViewDocumentSizeGuardTests"`
Expected: FAIL to compile — `WebViewDocumentSizeGuard` does not exist.

- [ ] **Step 3: Create the helper**

Create `src/ScadaBuilderV2.Application/ElementStudio/WebViewDocumentSizeGuard.cs`:

```csharp
namespace ScadaBuilderV2.Application.ElementStudio;

/// <summary>
/// Decides whether an HTML document is too large for <c>CoreWebView2.NavigateToString</c>,
/// which rejects payloads beyond a documented ~2 MB (UTF-16) limit with an
/// <see cref="System.ArgumentException"/>. Callers that exceed the limit must navigate to a
/// temp file instead.
/// </summary>
/// <remarks>
/// Decisions: DEC-import-crash-navigate-fallback.
/// Contracts: Studio Element+ legacy source rendering.
/// Tests: ScadaBuilderV2.Tests.WebViewDocumentSizeGuardTests.
/// </remarks>
public static class WebViewDocumentSizeGuard
{
    /// <summary>
    /// Maximum document length (UTF-16 chars) considered safe for
    /// <c>NavigateToString</c>. Set below the ~2,097,152 hard ceiling to leave headroom.
    /// </summary>
    public const int NavigateToStringMaxCharacters = 2_000_000;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="document"/> is longer than
    /// <see cref="NavigateToStringMaxCharacters"/> and must use the temp-file navigation
    /// fallback. Returns <see langword="false"/> for <see langword="null"/>.
    /// </summary>
    public static bool ExceedsNavigateToStringLimit(string? document)
    {
        return document is not null && document.Length > NavigateToStringMaxCharacters;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~WebViewDocumentSizeGuardTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Application/ElementStudio/WebViewDocumentSizeGuard.cs tests/ScadaBuilderV2.Tests/WebViewDocumentSizeGuardTests.cs
git commit -m "feat: add WebViewDocumentSizeGuard for NavigateToString limit

Pure, unit-tested helper deciding when an HTML document exceeds the
WebView2 NavigateToString ~2MB limit and must use a temp-file fallback."
```

---

## Task 3: Size-guarded navigation in Studio Element+ (Fix B, part 2)

**Files:**
- Modify: `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml.cs` (line 72 `OnLoaded`, line 697 `RefreshWorkzoneAsync`; add `NavigateLegacySource` helper; add `using ScadaBuilderV2.Application.ElementStudio;` — already imported at line 11)
- Test: `tests/ScadaBuilderV2.Tests/ElementStudioSourceRenderingTests.cs:14`

**Interfaces:**
- Consumes: `WebViewDocumentSizeGuard.ExceedsNavigateToStringLimit(string?)` from Task 2.
- Produces: `MainWindow.NavigateLegacySource(string document)` (private instance method) — used only within `MainWindow`.

- [ ] **Step 1: Update the existing source-text test**

In `tests/ScadaBuilderV2.Tests/ElementStudioSourceRenderingTests.cs`, replace the assertion at line 14:

```csharp
        StringAssert.Contains(code, "LegacySourceWebView.NavigateToString(BuildLegacySourceDocument(workspace));");
```

with:

```csharp
        StringAssert.Contains(code, "NavigateLegacySource(BuildLegacySourceDocument(workspace));");
        StringAssert.Contains(code, "WebViewDocumentSizeGuard.ExceedsNavigateToStringLimit(document)");
        StringAssert.Contains(code, "LegacySourceWebView.CoreWebView2.Navigate(");
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "Name=StudioUsesWebView2ForLegacySourceLayer"`
Expected: FAIL — `NavigateLegacySource` / guard / `Navigate(` strings are not yet in the code.

- [ ] **Step 3: Route OnLoaded and RefreshWorkzoneAsync through the new helper**

In `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml.cs`, at line 72 replace:

```csharp
        LegacySourceWebView.NavigateToString(BuildLegacySourceDocument(workspace));
```

with:

```csharp
        NavigateLegacySource(BuildLegacySourceDocument(workspace));
```

At line 697 (inside `RefreshWorkzoneAsync`) replace:

```csharp
        LegacySourceWebView.NavigateToString(BuildLegacySourceDocument(workspace));
```

with:

```csharp
        NavigateLegacySource(BuildLegacySourceDocument(workspace));
```

- [ ] **Step 4: Add the `NavigateLegacySource` helper**

In the same file, add this private method immediately after `RefreshWorkzoneAsync` (after its closing brace, near line 699):

```csharp
    private void NavigateLegacySource(string document)
    {
        if (WebViewDocumentSizeGuard.ExceedsNavigateToStringLimit(document)
            && LegacySourceWebView.CoreWebView2 is not null)
        {
            var tempPath = Path.Combine(
                Path.GetTempPath(),
                $"studio-legacy-source-{Guid.NewGuid():N}.html");
            File.WriteAllText(tempPath, document);
            workspace.Diagnostics.Add(
                $"Document source volumineux ({document.Length:N0} caracteres): charge via fichier temporaire pour contourner la limite de NavigateToString.");
            LegacySourceWebView.CoreWebView2.Navigate(new Uri(tempPath).AbsoluteUri);
            return;
        }

        LegacySourceWebView.NavigateToString(document);
    }
```

(`System.IO` is already imported at line 2; `ScadaBuilderV2.Application.ElementStudio` at line 11; `Guid`/`Uri` are in the global `System` namespace.)

- [ ] **Step 5: Run the updated source-text test**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "Name=StudioUsesWebView2ForLegacySourceLayer"`
Expected: PASS.

- [ ] **Step 6: Build the whole solution**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: `La génération a réussi.` / 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/ElementStudioSourceRenderingTests.cs
git commit -m "fix: size-guard Studio Element+ legacy source navigation

Routes OnLoaded and RefreshWorkzoneAsync through NavigateLegacySource, which
falls back to a temp-file Navigate when the document exceeds the WebView2
NavigateToString ~2MB limit instead of throwing ArgumentException."
```

---

## Task 4: Global DispatcherUnhandledException handler (Fix B, part 3)

**Files:**
- Modify: `src/ScadaBuilderV2.ElementStudio.App/App.xaml`
- Modify: `src/ScadaBuilderV2.ElementStudio.App/App.xaml.cs`
- Test: `tests/ScadaBuilderV2.Tests/ElementStudioSourceRenderingTests.cs`

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces: `App.OnDispatcherUnhandledException(object, DispatcherUnhandledExceptionEventArgs)` — wired via XAML.

- [ ] **Step 1: Write the failing source-text test**

Add to `tests/ScadaBuilderV2.Tests/ElementStudioSourceRenderingTests.cs` (before the `ReadStudioFile` helper):

```csharp
    [TestMethod]
    public void StudioAppHandlesDispatcherUnhandledExceptions()
    {
        var xaml = ReadStudioFile("App.xaml");
        var code = ReadStudioFile("App.xaml.cs");

        StringAssert.Contains(xaml, "DispatcherUnhandledException=\"OnDispatcherUnhandledException\"");
        StringAssert.Contains(code, "private void OnDispatcherUnhandledException(");
        StringAssert.Contains(code, "e.Handled = true;");
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "Name=StudioAppHandlesDispatcherUnhandledExceptions"`
Expected: FAIL — the handler is not registered.

- [ ] **Step 3: Register the handler in App.xaml**

Replace `src/ScadaBuilderV2.ElementStudio.App/App.xaml` content with:

```xml
<Application x:Class="ScadaBuilderV2.ElementStudio.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Startup="OnStartup"
             DispatcherUnhandledException="OnDispatcherUnhandledException">
    <Application.Resources>
    </Application.Resources>
</Application>
```

- [ ] **Step 4: Add the handler in App.xaml.cs**

In `src/ScadaBuilderV2.ElementStudio.App/App.xaml.cs`, add this method after `OnStartup` (before the class closing brace):

```csharp
    private void OnDispatcherUnhandledException(
        object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        System.Windows.MessageBox.Show(
            $"Erreur imprevue dans Studio Element+:\n\n{e.Exception.GetType().Name}: {e.Exception.Message}\n\n" +
            "L'operation a echoue mais le Studio reste ouvert.",
            "Studio Element+ - Erreur",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
        e.Handled = true;
    }
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "Name=StudioAppHandlesDispatcherUnhandledExceptions"`
Expected: PASS.

- [ ] **Step 6: Build the solution**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.ElementStudio.App/App.xaml src/ScadaBuilderV2.ElementStudio.App/App.xaml.cs tests/ScadaBuilderV2.Tests/ElementStudioSourceRenderingTests.cs
git commit -m "fix: handle DispatcherUnhandledException in Studio Element+

Replaces silent process death on unhandled async-void exceptions with a
visible error dialog, keeping the Studio open."
```

---

## Task 5: End-to-end verification against real win00059 data

**Files:**
- No source changes. Verification only, using the real package
  `projects/AMR_REF_SCADA_V2/.studio/imports/studio_win00059_20260708_203057.ft1`.

**Interfaces:**
- Consumes: all prior tasks' behavior.
- Produces: nothing.

> Note: the checked-in `.ft1` was produced *before* Fix A, so it still carries the
> oversized markup — it is the exact crash repro and validates the Fix B fallback.
> Fix A is validated by re-extracting fresh (manual, in-app) and confirming shrinkage.

- [ ] **Step 1: Confirm the pre-fix crash is gone with the oversized package**

Run (Git Bash):
```bash
cd "F:/Groupe AMR/SCADA_AMR_GROUP/SCADA_BUILDER_V2"
timeout 45 dotnet run --project src/ScadaBuilderV2.ElementStudio.App -- "projects/AMR_REF_SCADA_V2/.studio/imports/studio_win00059_20260708_203057.ft1" > /tmp/es_verify.log 2>&1; echo "EXIT=$?"; cat /tmp/es_verify.log
```
Expected: **no** `System.ArgumentException ... NavigateToString` in the log. The
window stays open until the timeout kills it (the temp-file fallback loads the 152
items). A non-zero exit from `timeout` killing the process is fine; an
`Unhandled exception` stack trace is a FAIL.

- [ ] **Step 2: Run the full test suite**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: all tests PASS (0 failed).

- [ ] **Step 3: Manual re-extraction + `.sep` + `.sb2` visual non-regression (win00059)**

Manual, in the running apps (documented for the operator; no automation):
1. In SCADA Builder V2, open `win00059`, select the same legacy batch, right-click →
   "Ouvrir dans Studio Element+". Confirm Studio opens with all 152 items and does
   **not** auto-close.
2. Confirm the freshly re-extracted package is dramatically smaller than the old
   3.9 MB `.ft1` (the allowlist is active for new extractions).
3. Save the batch as a `.sep` component, place it on a scene, export to `.sb2`.
4. Open the exported page HTML in a browser and compare visually to the current
   (pre-fix) rendering of the same elements — fills, strokes, fonts, colors,
   `drop-shadow` filters must match. Any missing visual effect is a regression:
   widen the Task 1 allowlist with the specific property and repeat.

- [ ] **Step 4: Manual visual non-regression on the known-good baseline (win00009)**

Repeat Step 3 (extract → `.sep` → `.sb2` → visual compare) for `win00009` (the
known-good reference scene) to confirm the allowlist introduces no regression on an
already-validated scene.

- [ ] **Step 5: Commit any allowlist adjustments (only if Step 3/4 required changes)**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs
git commit -m "fix: widen legacy style allowlist after visual non-regression check"
```

---

## Self-Review

**Spec coverage:**
- Spec §4.1 (allowlist capture) → Task 1. ✓
- Spec §4.2 (size guard + DispatcherUnhandledException) → Tasks 2, 3 (guard + navigation) and 4 (handler). ✓
- Spec §4.3 (validation: win00059 re-extract, `.sep`/`.sb2` compare, win00009, automated tests) → Task 5 (manual steps 3-4) + Tasks 1-4 automated tests. ✓
- Spec §5 (out of scope: no revert of `9751349`, no HtmlPreviewControl migration, no exhaustive async-void audit) → respected; no task touches those. ✓

**Placeholder scan:** No TBD/TODO; every code step shows full code; commands have expected output. Task 5 manual steps are inherently manual (visual comparison in TF100Web) and are described concretely, not deferred.

**Type consistency:** `WebViewDocumentSizeGuard.ExceedsNavigateToStringLimit(string?)` and `NavigateToStringMaxCharacters` are defined in Task 2 and consumed identically in Task 3 and the Task 2 tests. `NavigateLegacySource(string)` is defined and called with the same signature in Task 3. `OnDispatcherUnhandledException` matches the XAML attribute in Task 4.
