# Scene Clipboard & Keyboard Shortcuts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Select All (Ctrl+A), Copy (Ctrl+C), Cut (Ctrl+X), and Paste (Ctrl+V) for Element+ scene objects on the main canvas, and fold Undo (Ctrl+Z) / Redo (Ctrl+Y) into the same shortcut-resolution mechanism.

**Architecture:** A pure-data `ShortcutRegistry` (`ScadaBuilderV2.Application.Commands`) maps a key+modifiers pair to a command id string. A pure `SceneClipboard` (`ScadaBuilderV2.Application.Clipboard`) holds a single copied/cut snapshot. A new `SceneObjectsAddedAction` (`ScadaBuilderV2.Application.History`) mirrors the existing `SceneObjectsDeletedAction` for Paste's undo/redo. All scene mutation, WebView2 canvas repaint, and JS keydown wiring stay in `ScadaBuilderV2.App` (`MainWindow.xaml.cs` / `MainWindow.WebViewScript.cs`), following the same JS-keydown -> postMessage -> C# switch -> history action -> `ExecuteScriptAsync` pipeline already used by Delete/Undo/Redo today.

**Tech Stack:** .NET 8 / WPF, WebView2 (embedded JS in a C# string), MSTest.

## Global Constraints

- Clipboard is in-memory, internal to the app instance (not the OS clipboard), single slot (overwritten by each Copy/Cut).
- Select All selects top-level scene objects only (a group is one unit; its children are not selected individually).
- Copy/Cut/Paste operate on Element+ scene objects only (not legacy source elements).
- Paste always clones with brand-new ids (recursively into group children) and offsets each pasted top-level element by `(+20, +20)` relative to its stored clipboard position - never cumulative across repeated pastes of the same clipboard content.
- Cut pushes an undoable `SceneObjectsDeletedAction` (same as Delete); Paste pushes an undoable `SceneObjectsAddedAction`. Select All and Copy push no history entry. Undo/redo never touches clipboard content.
- New Application-layer types (`ShortcutRegistry`, `SceneClipboard`, `SceneObjectsAddedAction`) must not reference WPF or WebView2 types, matching the existing Clean Architecture layering (`docs/README.md` / `CLAUDE.md`).
- Build with `dotnet build ScadaBuilderV2.sln` and run tests with `dotnet test tests/ScadaBuilderV2.Tests --filter <Name>` after every task, from `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`.

---

## File Structure

- Create `src/ScadaBuilderV2.Application/Commands/ApplicationShortcut.cs` - `ShortcutKey`, `ShortcutModifiers`, `ApplicationShortcut` record.
- Create `src/ScadaBuilderV2.Application/Commands/ShortcutRegistry.cs` - default bindings + `Resolve`.
- Create `src/ScadaBuilderV2.Application/Clipboard/SceneClipboard.cs` - single-slot clipboard state.
- Create `src/ScadaBuilderV2.Application/History/SceneObjectsAddedAction.cs` - undoable paste action.
- Modify `src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs` - add `Key`, `CtrlKey`, `ShiftKey`, `AltKey` fields to `LegacyViewerMessage`.
- Modify `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs` - unify keydown handling for the six shortcuts; add Select All/Copy/Cut/Paste key detection.
- Modify `src/ScadaBuilderV2.App/MainWindow.xaml.cs` - add `_shortcutRegistry`/`_sceneClipboard` fields, `case "shortcut":` dispatch, `HandleShortcut`, `SelectAllSceneObjects`, `CopySelectionToClipboard`, `CutSelectionAsync`, `PasteClipboard`, `ResolveTopLevelSelectedElements`, `CloneWithNewIds`.
- Create `tests/ScadaBuilderV2.Tests/ShortcutRegistryTests.cs`.
- Create `tests/ScadaBuilderV2.Tests/SceneClipboardTests.cs`.
- Modify `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs` - add `SceneObjectsAddedAction` coverage.
- Modify `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` - add source-text wiring assertions per shortcut.

---

### Task 1: Shortcut data model and registry

**Files:**
- Create: `src/ScadaBuilderV2.Application/Commands/ApplicationShortcut.cs`
- Create: `src/ScadaBuilderV2.Application/Commands/ShortcutRegistry.cs`
- Test: `tests/ScadaBuilderV2.Tests/ShortcutRegistryTests.cs`

**Interfaces:**
- Produces: `ShortcutKey` (enum: `A, C, V, X, Y, Z`), `ShortcutModifiers` (`[Flags]` enum: `None, Control, Shift, Alt`), `ApplicationShortcut(string CommandId, ShortcutKey Key, ShortcutModifiers Modifiers)`, `ShortcutRegistry.Resolve(ShortcutKey key, ShortcutModifiers modifiers) -> string?`. Command id strings used everywhere downstream: `"selection.select-all"`, `"clipboard.copy"`, `"clipboard.cut"`, `"clipboard.paste"`, `"history.undo"`, `"history.redo"`.

- [ ] **Step 1: Write the failing test**

```csharp
using ScadaBuilderV2.Application.Commands;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ShortcutRegistryTests
{
    [TestMethod]
    public void ResolveReturnsExpectedCommandIdForEachDefaultBinding()
    {
        var registry = new ShortcutRegistry();

        Assert.AreEqual("selection.select-all", registry.Resolve(ShortcutKey.A, ShortcutModifiers.Control));
        Assert.AreEqual("clipboard.copy", registry.Resolve(ShortcutKey.C, ShortcutModifiers.Control));
        Assert.AreEqual("clipboard.paste", registry.Resolve(ShortcutKey.V, ShortcutModifiers.Control));
        Assert.AreEqual("clipboard.cut", registry.Resolve(ShortcutKey.X, ShortcutModifiers.Control));
        Assert.AreEqual("history.undo", registry.Resolve(ShortcutKey.Z, ShortcutModifiers.Control));
        Assert.AreEqual("history.redo", registry.Resolve(ShortcutKey.Y, ShortcutModifiers.Control));
    }

    [TestMethod]
    public void ResolveReturnsNullForUnknownCombination()
    {
        var registry = new ShortcutRegistry();

        Assert.IsNull(registry.Resolve(ShortcutKey.A, ShortcutModifiers.Control | ShortcutModifiers.Shift));
        Assert.IsNull(registry.Resolve(ShortcutKey.Z, ShortcutModifiers.None));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ScadaBuilderV2.Tests --filter "FullyQualifiedName~ShortcutRegistryTests"`
Expected: build error (`ShortcutRegistry`/`ShortcutKey`/`ShortcutModifiers` do not exist).

- [ ] **Step 3: Write minimal implementation**

`src/ScadaBuilderV2.Application/Commands/ApplicationShortcut.cs`:

```csharp
namespace ScadaBuilderV2.Application.Commands;

public enum ShortcutKey
{
    A,
    C,
    V,
    X,
    Y,
    Z
}

[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4
}

public sealed record ApplicationShortcut(string CommandId, ShortcutKey Key, ShortcutModifiers Modifiers);
```

`src/ScadaBuilderV2.Application/Commands/ShortcutRegistry.cs`:

```csharp
namespace ScadaBuilderV2.Application.Commands;

public sealed class ShortcutRegistry
{
    private readonly List<ApplicationShortcut> _shortcuts;

    public ShortcutRegistry()
    {
        _shortcuts =
        [
            new ApplicationShortcut("selection.select-all", ShortcutKey.A, ShortcutModifiers.Control),
            new ApplicationShortcut("clipboard.copy", ShortcutKey.C, ShortcutModifiers.Control),
            new ApplicationShortcut("clipboard.paste", ShortcutKey.V, ShortcutModifiers.Control),
            new ApplicationShortcut("clipboard.cut", ShortcutKey.X, ShortcutModifiers.Control),
            new ApplicationShortcut("history.undo", ShortcutKey.Z, ShortcutModifiers.Control),
            new ApplicationShortcut("history.redo", ShortcutKey.Y, ShortcutModifiers.Control)
        ];
    }

    public string? Resolve(ShortcutKey key, ShortcutModifiers modifiers)
    {
        return _shortcuts.FirstOrDefault(shortcut => shortcut.Key == key && shortcut.Modifiers == modifiers)?.CommandId;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ScadaBuilderV2.Tests --filter "FullyQualifiedName~ShortcutRegistryTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Application/Commands/ApplicationShortcut.cs src/ScadaBuilderV2.Application/Commands/ShortcutRegistry.cs tests/ScadaBuilderV2.Tests/ShortcutRegistryTests.cs
git commit -m "feat: add ShortcutRegistry mapping key gestures to command ids"
```

---

### Task 2: Scene clipboard state

**Files:**
- Create: `src/ScadaBuilderV2.Application/Clipboard/SceneClipboard.cs`
- Test: `tests/ScadaBuilderV2.Tests/SceneClipboardTests.cs`

**Interfaces:**
- Consumes: `ScadaElement` (`ScadaBuilderV2.Domain.Scenes`).
- Produces: `SceneClipboard.HasContent -> bool`, `SceneClipboard.Content -> IReadOnlyList<ScadaElement>?`, `SceneClipboard.Copy(IReadOnlyList<ScadaElement> elements) -> void`.

- [ ] **Step 1: Write the failing test**

```csharp
using ScadaBuilderV2.Application.Clipboard;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class SceneClipboardTests
{
    [TestMethod]
    public void IsEmptyUntilFirstCopy()
    {
        var clipboard = new SceneClipboard();

        Assert.IsFalse(clipboard.HasContent);
        Assert.IsNull(clipboard.Content);
    }

    [TestMethod]
    public void CopyStoresElementsAndOverwritesPreviousContent()
    {
        var clipboard = new SceneClipboard();
        var first = ScadaElement.CreateText("text-1", "First", 0, 0);
        var second = ScadaElement.CreateText("text-2", "Second", 10, 10);

        clipboard.Copy([first]);
        Assert.IsTrue(clipboard.HasContent);
        Assert.AreEqual(1, clipboard.Content!.Count);
        Assert.AreEqual("text-1", clipboard.Content[0].Id);

        clipboard.Copy([second]);
        Assert.AreEqual(1, clipboard.Content!.Count);
        Assert.AreEqual("text-2", clipboard.Content[0].Id);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ScadaBuilderV2.Tests --filter "FullyQualifiedName~SceneClipboardTests"`
Expected: build error (`SceneClipboard` does not exist).

- [ ] **Step 3: Write minimal implementation**

`src/ScadaBuilderV2.Application/Clipboard/SceneClipboard.cs`:

```csharp
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Clipboard;

public sealed class SceneClipboard
{
    public IReadOnlyList<ScadaElement>? Content { get; private set; }

    public bool HasContent => Content is { Count: > 0 };

    public void Copy(IReadOnlyList<ScadaElement> elements)
    {
        Content = elements;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ScadaBuilderV2.Tests --filter "FullyQualifiedName~SceneClipboardTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Application/Clipboard/SceneClipboard.cs tests/ScadaBuilderV2.Tests/SceneClipboardTests.cs
git commit -m "feat: add SceneClipboard single-slot clipboard state"
```

---

### Task 3: `SceneObjectsAddedAction` undo/redo

**Files:**
- Create: `src/ScadaBuilderV2.Application/History/SceneObjectsAddedAction.cs`
- Modify: `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs`

**Interfaces:**
- Consumes: `DeletedSceneObjectSnapshot(ScadaElement Element, string? ParentElementId, int SiblingIndex)` (already defined in `SceneObjectsDeletedAction.cs`), `IEditorHistoryAction`, `EditorHistoryContext` (both in `ScadaBuilderV2.Application.History`).
- Produces: `SceneObjectsAddedAction(string SceneId, IReadOnlyList<DeletedSceneObjectSnapshot> AddedObjects)` implementing `IEditorHistoryAction`. `UndoAsync` removes `AddedObjects` from the scene; `RedoAsync` re-inserts them at their recorded parent/sibling position.

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs`, after `SceneObjectDeleteActionUndoRedoRestoresTopLevelElement` (around line 127):

```csharp
    [TestMethod]
    public async Task SceneObjectAddedActionUndoRedoRestoresTopLevelElement()
    {
        var element = ScadaElement.CreateInputText("input-002", "Input002", 30, 40);
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(element);
        var history = new EditorHistoryService();
        var context = CreateContext(scene, replacement => scene = replacement);

        history.Push(new SceneObjectsAddedAction(
            scene.Id,
            [new DeletedSceneObjectSnapshot(element, null, 0)]));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.IsNull(scene.FindElementRecursive(element.Id));

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.IsNotNull(scene.FindElementRecursive(element.Id));
    }

    [TestMethod]
    public async Task SceneObjectAddedActionRestoresChildToOriginalParentOnRedo()
    {
        var child = CreateShape("shape-002", 7, 8);
        var group = CreateGroup("group-002", 50, 60, []);
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(group);
        var history = new EditorHistoryService();
        var context = CreateContext(scene, replacement => scene = replacement);

        history.Push(new SceneObjectsAddedAction(
            scene.Id,
            [new DeletedSceneObjectSnapshot(child, group.Id, 0)]));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.IsNull(scene.FindElementRecursive(child.Id));

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.AreEqual(group.Id, scene.FindParentOf(child.Id)?.Id);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ScadaBuilderV2.Tests --filter "Name=SceneObjectAddedActionUndoRedoRestoresTopLevelElement|Name=SceneObjectAddedActionRestoresChildToOriginalParentOnRedo"`
Expected: build error (`SceneObjectsAddedAction` does not exist).

- [ ] **Step 3: Write minimal implementation**

`src/ScadaBuilderV2.Application/History/SceneObjectsAddedAction.cs`:

```csharp
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.History;

public sealed record SceneObjectsAddedAction(
    string SceneId,
    IReadOnlyList<DeletedSceneObjectSnapshot> AddedObjects) : IEditorHistoryAction
{
    public string Label => "Coller objets";

    public bool CanMergeWith(IEditorHistoryAction next) => false;

    public IEditorHistoryAction MergeWith(IEditorHistoryAction next)
    {
        throw new InvalidOperationException("Add actions do not support merge.");
    }

    public async Task UndoAsync(EditorHistoryContext context)
    {
        var scene = context.GetActiveScene();
        if (scene is null)
        {
            context.SetStatus("Historique ignore: aucune scene active.");
            return;
        }

        scene = scene.WithoutSceneObjects(AddedObjects.Select(snapshot => snapshot.Element.Id));
        context.ReplaceActiveScene(scene);
        context.MarkDirty();
        await context.RefreshPreviewAsync();
        context.SetStatus($"{AddedObjects.Count} collage(s) annule(s).");
    }

    public async Task RedoAsync(EditorHistoryContext context)
    {
        var scene = context.GetActiveScene();
        if (scene is null)
        {
            context.SetStatus("Historique ignore: aucune scene active.");
            return;
        }

        foreach (var snapshot in AddedObjects.OrderBy(snapshot => snapshot.SiblingIndex))
        {
            scene = RestoreObject(scene, snapshot);
        }

        context.ReplaceActiveScene(scene);
        context.MarkDirty();
        await context.RefreshPreviewAsync();
        context.SetStatus($"{AddedObjects.Count} collage(s) retabli(s).");
    }

    private static ScadaScene RestoreObject(ScadaScene scene, DeletedSceneObjectSnapshot snapshot)
    {
        if (scene.FindElementRecursive(snapshot.Element.Id) is not null)
        {
            return scene;
        }

        if (string.IsNullOrWhiteSpace(snapshot.ParentElementId))
        {
            return scene with
            {
                Elements = InsertAt(scene.Elements, snapshot.Element, snapshot.SiblingIndex)
            };
        }

        var parent = scene.FindElementRecursive(snapshot.ParentElementId);
        if (parent is null)
        {
            return scene with
            {
                Elements = InsertAt(scene.Elements, snapshot.Element, snapshot.SiblingIndex)
            };
        }

        var restoredParent = parent with
        {
            Children = InsertAt(parent.ChildElements, snapshot.Element, snapshot.SiblingIndex)
        };

        return scene.WithReplacedElementRecursive(restoredParent);
    }

    private static IReadOnlyList<ScadaElement> InsertAt(
        IReadOnlyList<ScadaElement> elements,
        ScadaElement element,
        int index)
    {
        var normalizedIndex = Math.Clamp(index, 0, elements.Count);
        return elements
            .Where(existing => existing.Id != element.Id)
            .Take(normalizedIndex)
            .Concat([element])
            .Concat(elements.Where(existing => existing.Id != element.Id).Skip(normalizedIndex))
            .ToArray();
    }
}
```

Note: `RestoreObject`/`InsertAt` intentionally duplicate the logic in `SceneObjectsDeletedAction` rather than sharing a base type - both actions are small, independent, and this keeps each one readable on its own (matches the existing codebase's preference for not over-abstracting two call sites).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ScadaBuilderV2.Tests --filter "Name=SceneObjectAddedActionUndoRedoRestoresTopLevelElement|Name=SceneObjectAddedActionRestoresChildToOriginalParentOnRedo"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Application/History/SceneObjectsAddedAction.cs tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs
git commit -m "feat: add SceneObjectsAddedAction as the undoable counterpart to delete"
```

---

### Task 4: Unify keydown handling and migrate Undo/Redo dispatch

This is the riskiest task because Undo/Redo already work - it must not regress them. Do this task alone, verify manually, before adding the four new commands in later tasks.

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs:175-274` (`LegacyViewerMessage`)
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:2531-2552` (keydown handler)
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:100-145` (fields/constructor), `:1367-1372` (message switch)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `ShortcutRegistry` (Task 1), `ShortcutKey`, `ShortcutModifiers` (Task 1), existing `UndoLastSceneOperationAsync()` / `RedoLastSceneOperationAsync()` (`MainWindow.xaml.cs:6400`, `:6425`, unchanged).
- Produces: `MainWindow.HandleShortcut(string? key, bool ctrlKey, bool shiftKey, bool altKey) -> void`, `MainWindow.TryParseShortcutKey(string? key, out ShortcutKey shortcutKey) -> bool`. Later tasks (5-8) add `case` branches inside `HandleShortcut`'s switch.

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`, after `RotateDragDoesNotSetDeadCenterFields` (around line 1398):

```csharp
    [TestMethod]
    public void KeydownPostsUnifiedShortcutMessageForCtrlCombinationsAndDispatchesInHost()
    {
        // Regression guard: Ctrl+Z/Ctrl+Y used to post ad hoc {type:'undo'|'redo'}
        // messages directly from the keydown handler. They now go through the same
        // generic 'shortcut' message as the newer Select All/Copy/Cut/Paste
        // shortcuts, resolved host-side via ShortcutRegistry, so there is exactly
        // one shortcut mechanism.
        var source = ReadMainWindowSource();

        Assert.IsFalse(
            source.Contains("postMessage({ type: event.shiftKey ? 'redo' : 'undo' })", StringComparison.Ordinal),
            "The old ad hoc undo/redo postMessage call must not be reintroduced.");

        StringAssert.Contains(source, "type: 'shortcut'");
        StringAssert.Contains(source, "['a', 'c', 'v', 'x', 'y', 'z']");

        StringAssert.Contains(source, "case \"shortcut\":");
        StringAssert.Contains(source, "HandleShortcut(message.Key, message.CtrlKey, message.ShiftKey, message.AltKey);");

        var handlerStart = source.IndexOf("private void HandleShortcut(", StringComparison.Ordinal);
        Assert.IsTrue(handlerStart >= 0, "HandleShortcut method not found");
        var handlerEnd = source.IndexOf("\n    }\n", handlerStart, StringComparison.Ordinal);
        Assert.IsTrue(handlerEnd >= 0, "End of HandleShortcut not found");
        var handlerBody = source[handlerStart..handlerEnd];

        StringAssert.Contains(handlerBody, "case \"history.undo\":");
        StringAssert.Contains(handlerBody, "UndoLastSceneOperationAsync();");
        StringAssert.Contains(handlerBody, "case \"history.redo\":");
        StringAssert.Contains(handlerBody, "RedoLastSceneOperationAsync();");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ScadaBuilderV2.Tests --filter "Name=KeydownPostsUnifiedShortcutMessageForCtrlCombinationsAndDispatchesInHost"`
Expected: FAIL (old string still present, new strings missing).

- [ ] **Step 3: Write minimal implementation**

In `src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs`, inside `LegacyViewerMessage` (after the `Toggle` property, i.e. after line 241):

```csharp
        public string? Key { get; set; }

        public bool CtrlKey { get; set; }

        public bool ShiftKey { get; set; }

        public bool AltKey { get; set; }
```

In `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, replace lines 2541-2552 (the two `if ((event.ctrlKey || event.metaKey) && ...)` blocks for `z` and `y`):

```js
    if (event.ctrlKey || event.metaKey) {
      const shortcutKey = event.key.toLowerCase();
      if (['a', 'c', 'v', 'x', 'y', 'z'].includes(shortcutKey)) {
        window.chrome?.webview?.postMessage({
          type: 'shortcut',
          key: shortcutKey,
          ctrlKey: true,
          shiftKey: event.shiftKey,
          altKey: event.altKey
        });
        event.preventDefault();
        event.stopPropagation();
        return;
      }
    }
```

In `src/ScadaBuilderV2.App/MainWindow.xaml.cs`, add two fields near `_activeSelection` (after line 61):

```csharp
    private readonly ShortcutRegistry _shortcutRegistry = new();
    private readonly SceneClipboard _sceneClipboard = new();
```

`MainWindow.xaml.cs` already has `using ScadaBuilderV2.Application.Commands;` and `using ScadaBuilderV2.Application.History;` (lines 14 and 17). Add the one missing using, alongside them:

```csharp
using ScadaBuilderV2.Application.Clipboard;
```

Replace the `case "undo":` / `case "redo":` block (lines 1367-1372) with:

```csharp
                case "shortcut":
                    HandleShortcut(message.Key, message.CtrlKey, message.ShiftKey, message.AltKey);
                    break;
```

Add the dispatch method near `UndoLastSceneOperationAsync` (after line 6437, i.e. after `RedoLastSceneOperationAsync`'s closing brace):

```csharp
    private void HandleShortcut(string? key, bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (!ctrlKey || !TryParseShortcutKey(key, out var shortcutKey))
        {
            return;
        }

        var modifiers = ShortcutModifiers.Control
            | (shiftKey ? ShortcutModifiers.Shift : ShortcutModifiers.None)
            | (altKey ? ShortcutModifiers.Alt : ShortcutModifiers.None);

        switch (_shortcutRegistry.Resolve(shortcutKey, modifiers))
        {
            case "history.undo":
                _ = UndoLastSceneOperationAsync();
                break;
            case "history.redo":
                _ = RedoLastSceneOperationAsync();
                break;
        }
    }

    private static bool TryParseShortcutKey(string? key, out ShortcutKey shortcutKey)
    {
        switch ((key ?? "").ToLowerInvariant())
        {
            case "a":
                shortcutKey = ShortcutKey.A;
                return true;
            case "c":
                shortcutKey = ShortcutKey.C;
                return true;
            case "v":
                shortcutKey = ShortcutKey.V;
                return true;
            case "x":
                shortcutKey = ShortcutKey.X;
                return true;
            case "y":
                shortcutKey = ShortcutKey.Y;
                return true;
            case "z":
                shortcutKey = ShortcutKey.Z;
                return true;
            default:
                shortcutKey = default;
                return false;
        }
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ScadaBuilderV2.Tests --filter "Name=KeydownPostsUnifiedShortcutMessageForCtrlCombinationsAndDispatchesInHost"`
Expected: PASS.

Then run the full suite to confirm nothing else broke:

Run: `dotnet build ScadaBuilderV2.sln` then `dotnet test tests/ScadaBuilderV2.Tests --filter "FullyQualifiedName~WebViewContextMenuScriptTests|FullyQualifiedName~EditorHistoryServiceTests"`
Expected: PASS (pre-existing `LegacyContextMenuExposesElementStudioCommand` and `ReadOnlyNumericElementsRenderDisplayFormatWhenValueIsMissing` failures are known pre-existing failures unrelated to this change - confirm no *new* failures appear).

- [ ] **Step 5: Manual check**

Run `dotnet run --project src/ScadaBuilderV2.App`, open a scene with at least one Element+, select it, press Ctrl+Z after moving it and Ctrl+Y - confirm Undo/Redo still work exactly as before.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "refactor: unify Undo/Redo shortcuts under ShortcutRegistry dispatch"
```

---

### Task 5: Select All

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs` (`HandleShortcut` switch, new `SelectAllSceneObjects` method)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `_activeScene` (`ScadaScene?`), `_selectedSceneObjectIds`/`_selectedSourceObjectIds` (`HashSet<string>`), `RefreshSelectionUi()`, `RefreshModernSceneUi()`, `ExecuteLegacyViewerCommandAsync(LegacyViewerCommand)`, `LegacyViewerCommand("selectObject", Ids: ...)` (all pre-existing, `MainWindow.xaml.cs`).
- Produces: `MainWindow.SelectAllSceneObjects() -> void`.

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`:

```csharp
    [TestMethod]
    public void SelectAllShortcutDispatchesToSelectAllSceneObjects()
    {
        var source = ReadMainWindowSource();

        var handlerStart = source.IndexOf("private void HandleShortcut(", StringComparison.Ordinal);
        Assert.IsTrue(handlerStart >= 0, "HandleShortcut method not found");
        var handlerEnd = source.IndexOf("\n    }\n", handlerStart, StringComparison.Ordinal);
        var handlerBody = source[handlerStart..handlerEnd];

        StringAssert.Contains(handlerBody, "case \"selection.select-all\":");
        StringAssert.Contains(handlerBody, "SelectAllSceneObjects();");

        StringAssert.Contains(source, "private void SelectAllSceneObjects()");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ScadaBuilderV2.Tests --filter "Name=SelectAllShortcutDispatchesToSelectAllSceneObjects"`
Expected: FAIL (`SelectAllSceneObjects` and the `case` don't exist yet).

- [ ] **Step 3: Write minimal implementation**

In `src/ScadaBuilderV2.App/MainWindow.xaml.cs`, add a case to `HandleShortcut`'s switch (from Task 4):

```csharp
            case "selection.select-all":
                SelectAllSceneObjects();
                break;
```

Add the method near `SelectModernElement` (after its closing brace, line 4771):

```csharp
    private void SelectAllSceneObjects()
    {
        if (_activeScene is null)
        {
            return;
        }

        _selectedSourceObjectIds.Clear();
        _selectedSceneObjectIds.Clear();
        foreach (var element in _activeScene.Elements)
        {
            _selectedSceneObjectIds.Add(element.Id);
        }

        _selectedSceneObject = _activeScene.Elements.Count > 0 ? _activeScene.Elements[^1] : null;

        RefreshSelectionUi();
        RefreshModernSceneUi();
        _ = ExecuteLegacyViewerCommandAsync(new LegacyViewerCommand("selectObject", Ids: _selectedSceneObjectIds.ToArray()));
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet build ScadaBuilderV2.sln` then `dotnet test tests/ScadaBuilderV2.Tests --filter "Name=SelectAllShortcutDispatchesToSelectAllSceneObjects"`
Expected: PASS.

- [ ] **Step 5: Manual check**

Run the app, open a scene with several Element+ objects (mix of standalone and at least one group), press Ctrl+A - confirm all top-level objects show selected, and a group's children are not individually selected.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: add Ctrl+A select-all for top-level scene objects"
```

---

### Task 6: Copy

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `_sceneClipboard` (Task 4), `ContainsElement(ScadaElement, string) -> bool` (pre-existing, `MainWindow.xaml.cs:5898`).
- Produces: `MainWindow.ResolveTopLevelSelectedElements() -> IReadOnlyList<ScadaElement>` (also consumed by Task 7), `MainWindow.CopySelectionToClipboard() -> void`.

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`:

```csharp
    [TestMethod]
    public void CopyShortcutDispatchesToCopySelectionToClipboard()
    {
        var source = ReadMainWindowSource();

        var handlerStart = source.IndexOf("private void HandleShortcut(", StringComparison.Ordinal);
        var handlerEnd = source.IndexOf("\n    }\n", handlerStart, StringComparison.Ordinal);
        var handlerBody = source[handlerStart..handlerEnd];

        StringAssert.Contains(handlerBody, "case \"clipboard.copy\":");
        StringAssert.Contains(handlerBody, "CopySelectionToClipboard();");

        StringAssert.Contains(source, "private void CopySelectionToClipboard()");
        StringAssert.Contains(source, "private IReadOnlyList<ScadaElement> ResolveTopLevelSelectedElements()");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ScadaBuilderV2.Tests --filter "Name=CopyShortcutDispatchesToCopySelectionToClipboard"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

In `src/ScadaBuilderV2.App/MainWindow.xaml.cs`, add a case to `HandleShortcut`'s switch:

```csharp
            case "clipboard.copy":
                CopySelectionToClipboard();
                break;
```

Add both methods near `ContainsElement` (after its closing brace, line 5901):

```csharp
    private IReadOnlyList<ScadaElement> ResolveTopLevelSelectedElements()
    {
        if (_activeScene is null)
        {
            return Array.Empty<ScadaElement>();
        }

        var selected = _selectedSceneObjectIds
            .Select(id => _activeScene.FindElementRecursive(id))
            .Where(element => element is not null)
            .Select(element => element!)
            .ToArray();

        return selected
            .Where(element => !selected.Any(candidate =>
                !string.Equals(candidate.Id, element.Id, StringComparison.Ordinal) &&
                ContainsElement(candidate, element.Id)))
            .ToArray();
    }

    private void CopySelectionToClipboard()
    {
        var selectedElements = ResolveTopLevelSelectedElements();
        if (selectedElements.Count == 0)
        {
            SetStatus("Aucun objet selectionne a copier.");
            return;
        }

        _sceneClipboard.Copy(selectedElements);
        SetStatus($"{selectedElements.Count} objet(s) copie(s).");
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet build ScadaBuilderV2.sln` then `dotnet test tests/ScadaBuilderV2.Tests --filter "Name=CopyShortcutDispatchesToCopySelectionToClipboard"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: add Ctrl+C copy selected Element+ objects to the scene clipboard"
```

---

### Task 7: Cut

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `ResolveTopLevelSelectedElements()` (Task 6), `_sceneClipboard.Copy(...)` (Task 2), `DeletedSceneObjectSnapshot`, `SceneObjectsDeletedAction` (pre-existing), `GetSiblingIndex(ScadaScene, ScadaElement)` (pre-existing, `MainWindow.xaml.cs:2162`).
- Produces: `MainWindow.CutSelectionAsync() -> Task`.

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`:

```csharp
    [TestMethod]
    public void CutShortcutDispatchesToCutSelectionAsyncAndCopiesBeforeDeleting()
    {
        var source = ReadMainWindowSource();

        var handlerStart = source.IndexOf("private void HandleShortcut(", StringComparison.Ordinal);
        var handlerEnd = source.IndexOf("\n    }\n", handlerStart, StringComparison.Ordinal);
        var handlerBody = source[handlerStart..handlerEnd];

        StringAssert.Contains(handlerBody, "case \"clipboard.cut\":");
        StringAssert.Contains(handlerBody, "CutSelectionAsync();");

        var methodStart = source.IndexOf("private async Task CutSelectionAsync()", StringComparison.Ordinal);
        Assert.IsTrue(methodStart >= 0, "CutSelectionAsync method not found");
        var methodEnd = source.IndexOf("\n    }\n", methodStart, StringComparison.Ordinal);
        var methodBody = source[methodStart..methodEnd];

        var clipboardCopyIndex = methodBody.IndexOf("_sceneClipboard.Copy(", StringComparison.Ordinal);
        var historyPushIndex = methodBody.IndexOf("new SceneObjectsDeletedAction(", StringComparison.Ordinal);
        Assert.IsTrue(clipboardCopyIndex >= 0, "Cut must copy to the clipboard");
        Assert.IsTrue(historyPushIndex >= 0, "Cut must push a SceneObjectsDeletedAction");
        Assert.IsTrue(clipboardCopyIndex < historyPushIndex,
            "Cut must snapshot the clipboard before removing the elements from the scene.");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ScadaBuilderV2.Tests --filter "Name=CutShortcutDispatchesToCutSelectionAsyncAndCopiesBeforeDeleting"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

In `src/ScadaBuilderV2.App/MainWindow.xaml.cs`, add a case to `HandleShortcut`'s switch:

```csharp
            case "clipboard.cut":
                _ = CutSelectionAsync();
                break;
```

Add the method near `DeleteSelectedSceneObjectsAsync` (after its closing brace, line 2122):

```csharp
    private async Task CutSelectionAsync()
    {
        if (_activeScene is null)
        {
            SetStatus("Aucune scene active pour couper la selection.");
            return;
        }

        var selectedElements = ResolveTopLevelSelectedElements();
        if (selectedElements.Count == 0)
        {
            SetStatus("Aucun objet selectionne a couper.");
            return;
        }

        _sceneClipboard.Copy(selectedElements);

        var deletedSnapshots = selectedElements
            .Select(element => new DeletedSceneObjectSnapshot(
                element,
                _activeScene.FindParentOf(element.Id)?.Id,
                GetSiblingIndex(_activeScene, element)))
            .ToArray();

        _activeScene = _activeScene.WithoutSceneObjects(selectedElements.Select(element => element.Id));

        _selectedSourceObjectIds.Clear();
        _selectedSceneObject = null;
        _selectedSceneObjectIds.Clear();
        _activeSceneTab?.History.Push(new SceneObjectsDeletedAction(_activeScene.Id, deletedSnapshots));
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        await RenderModernSceneAsync();
        SetStatus($"{deletedSnapshots.Length} objet(s) coupe(s). Undo disponible. Presse-papier mis a jour.");
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet build ScadaBuilderV2.sln` then `dotnet test tests/ScadaBuilderV2.Tests --filter "Name=CutShortcutDispatchesToCutSelectionAsyncAndCopiesBeforeDeleting"`
Expected: PASS.

- [ ] **Step 5: Manual check**

Run the app, select an Element+, press Ctrl+X - confirm it disappears from the scene, Ctrl+Z restores it, and the clipboard still has content afterwards (verified in Task 8's manual check via Paste).

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: add Ctrl+X cut selected Element+ objects (undoable, copies to clipboard)"
```

---

### Task 8: Paste

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `_sceneClipboard.HasContent` / `.Content` (Task 2), `SceneObjectsAddedAction` (Task 3), `GetSiblingIndex` (pre-existing).
- Produces: `MainWindow.PasteClipboard() -> void`, `MainWindow.CloneWithNewIds(ScadaElement, double offsetX, double offsetY) -> ScadaElement`.

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`:

```csharp
    [TestMethod]
    public void PasteShortcutDispatchesToPasteClipboardAndOffsetsByTwentyPixels()
    {
        var source = ReadMainWindowSource();

        var handlerStart = source.IndexOf("private void HandleShortcut(", StringComparison.Ordinal);
        var handlerEnd = source.IndexOf("\n    }\n", handlerStart, StringComparison.Ordinal);
        var handlerBody = source[handlerStart..handlerEnd];

        StringAssert.Contains(handlerBody, "case \"clipboard.paste\":");
        StringAssert.Contains(handlerBody, "PasteClipboard();");

        StringAssert.Contains(source, "private void PasteClipboard()");
        StringAssert.Contains(source, "CloneWithNewIds(element, 20, 20)");

        var cloneStart = source.IndexOf("private static ScadaElement CloneWithNewIds(", StringComparison.Ordinal);
        Assert.IsTrue(cloneStart >= 0, "CloneWithNewIds method not found");
        var cloneEnd = source.IndexOf("\n    }\n", cloneStart, StringComparison.Ordinal);
        var cloneBody = source[cloneStart..cloneEnd];
        StringAssert.Contains(cloneBody, "Guid.NewGuid()");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ScadaBuilderV2.Tests --filter "Name=PasteShortcutDispatchesToPasteClipboardAndOffsetsByTwentyPixels"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

In `src/ScadaBuilderV2.App/MainWindow.xaml.cs`, add a case to `HandleShortcut`'s switch:

```csharp
            case "clipboard.paste":
                PasteClipboard();
                break;
```

Add the clone helper near `ContainsElement` (after `ResolveTopLevelSelectedElements` from Task 6):

```csharp
    private static ScadaElement CloneWithNewIds(ScadaElement element, double offsetX, double offsetY)
    {
        var clonedChildren = element.Children?
            .Select(child => CloneWithNewIds(child, 0, 0))
            .ToArray();

        return element with
        {
            Id = Guid.NewGuid().ToString("N"),
            Bounds = element.Bounds with { X = element.Bounds.X + offsetX, Y = element.Bounds.Y + offsetY },
            Children = clonedChildren
        };
    }
```

Add the paste method near `CutSelectionAsync` (Task 7):

```csharp
    private void PasteClipboard()
    {
        if (_activeScene is null || !_sceneClipboard.HasContent)
        {
            SetStatus("Presse-papier vide ou aucune scene active.");
            return;
        }

        var pasted = _sceneClipboard.Content!
            .Select(element => CloneWithNewIds(element, 20, 20))
            .ToArray();

        var scene = _activeScene;
        foreach (var element in pasted)
        {
            scene = scene.WithElement(element);
        }

        var addedSnapshots = pasted
            .Select(element => new DeletedSceneObjectSnapshot(element, null, GetSiblingIndex(scene, element)))
            .ToArray();

        _activeScene = scene;
        _activeSceneTab?.History.Push(new SceneObjectsAddedAction(_activeScene.Id, addedSnapshots));

        _selectedSourceObjectIds.Clear();
        _selectedSceneObjectIds.Clear();
        foreach (var element in pasted)
        {
            _selectedSceneObjectIds.Add(element.Id);
        }

        _selectedSceneObject = pasted.Length > 0 ? pasted[^1] : null;

        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        _ = RenderModernSceneAsync();
        _ = ExecuteLegacyViewerCommandAsync(new LegacyViewerCommand("selectObject", Ids: _selectedSceneObjectIds.ToArray()));
        SetStatus($"{pasted.Length} objet(s) colle(s). Undo disponible.");
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet build ScadaBuilderV2.sln` then `dotnet test tests/ScadaBuilderV2.Tests --filter "Name=PasteShortcutDispatchesToPasteClipboardAndOffsetsByTwentyPixels"`
Expected: PASS.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test tests/ScadaBuilderV2.Tests --no-build`
Expected: same pass/fail counts as the pre-existing baseline (the two known pre-existing failures noted in Task 4, no new failures).

- [ ] **Step 6: Manual check**

Run the app:
1. Select a single Element+, Ctrl+C, Ctrl+V twice - confirm two new copies appear, both offset `(+20, +20)` from the original (not stacking further from each other).
2. Select a group with children, Ctrl+C, Ctrl+V - confirm the pasted group's children keep their relative layout.
3. Ctrl+X an object, Ctrl+Z to restore it, then Ctrl+V - confirm both the restored original and a freshly pasted copy exist.
4. Ctrl+A, Ctrl+C, Ctrl+V - confirm multi-object copy/paste works and the new objects become the active selection.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: add Ctrl+V paste with fresh ids and a 20px offset (undoable)"
```

---

## Self-Review Notes

- **Spec coverage:** Select All (Task 5), Copy (Task 6), Cut (Task 7), Paste (Task 8), Undo/Redo unified (Task 4), `ShortcutRegistry` (Task 1), `SceneClipboard` (Task 2), `SceneObjectsAddedAction` (Task 3) - every design section has a task.
- **Known accepted duplication:** `SceneObjectsAddedAction`'s `RestoreObject`/`InsertAt` duplicate `SceneObjectsDeletedAction`'s (Task 3); `ResolveTopLevelSelectedElements` (Task 6) duplicates the inline pruning logic already inside `DeleteSelectedSceneObjectsAsync` rather than refactoring that existing, working method - both are called out inline to avoid surprising a reviewer.
- **Pre-existing test failures:** `LegacyContextMenuExposesElementStudioCommand` and `ReadOnlyNumericElementsRenderDisplayFormatWhenValueIsMissing` already fail on `master` before this plan; Task 4's step 4 explicitly names them so nobody mistakes them for a regression introduced here.
