# Context Menu "Ouvrir dans Studio Element+" and Library Import Naming — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore the ability to open an already-converted Element+ object's source `.sep` component in Studio Element+ for re-editing, and make newly created library components default to a meaningful name instead of the generic placeholder "Nouveau composant".

**Architecture:** Both fixes add pure, unit-testable logic to `ScadaBuilderV2.Application.ElementStudio` (naming resolution, and a mapper from a saved `.sep` component back into the existing `ElementStudioImportPackage` shape), then wire that logic into the two WPF hosts (`ScadaBuilderV2.App` for the context menu, `ScadaBuilderV2.ElementStudio.App` for the default component name) with the smallest possible change to existing, working code paths. No new file formats, no new IPC surfaces: the "edit existing `.sep`" flow reuses the existing `.ft1` import-package pipeline (`IElementStudioImportPackageWriter.WriteToProjectAsync` + `TryLaunchElementStudioAsync`) that already powers "Ouvrir dans Studio Element+" for legacy elements.

**Tech Stack:** .NET 8, WPF, MSTest (`tests/ScadaBuilderV2.Tests`), System.Text.Json.

## Global Constraints

- Work happens in the isolated worktree `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2-fix-regressions` on branch `fix/context-menu-and-import-naming` (already created from `origin/master`). Do not touch the `add-ui-feature` worktree — it has another worker's uncommitted changes.
- Commit after each task (this repo's convention per `docs/AGENTS.md`: clean worktree before starting, one commit per validated task).
- `ScadaBuilderV2.Domain` has no project references — do not add any to it.
- `ScadaBuilderV2.Tests` targets plain `net8.0` and only references `Domain`, `Application`, `Rendering`, `Infrastructure` — it cannot reference the WPF `App` or `ElementStudio.App` projects. All tests for code that lives in those two WPF projects use the existing "read the partial-class source files as text, assert on exact strings" pattern already established by `WebViewContextMenuScriptTests.ReadMainWindowSource()` and `StudioElementPlusContractTests.ReadBuilderMainWindowCode()`. Do not attempt to add a project reference from the test project to a WPF project.
- Public APIs require XML doc comments; contract-sensitive code cites `Decisions:`, `Contracts:`, `Tests:` in `<remarks>` (see `ElementStudioComponentProvenance` in `ElementStudioComponentModels.cs` for the pattern).
- Additive changes (this is one — a new capability, "edit an existing library component from the scene") get a `DEC-xxxx` entry in `docs/00_governance/DECISION_REGISTER_V2.md` and an owner-doc contract rule, per `docs/AGENTS.md`.

---

## File Structure

| File | Responsibility |
|---|---|
| `src/ScadaBuilderV2.Application/ElementStudio/ElementStudioComponentNaming.cs` (new) | Pure function: resolve a default component name from imported source names. |
| `src/ScadaBuilderV2.ElementStudio.App/ElementStudioViewModels.cs` (modify) | `ElementStudioWorkspaceViewModel` constructor seeds `componentName` via the new helper instead of a hardcoded literal. |
| `src/ScadaBuilderV2.Application/ElementStudio/ElementStudioComponentToImportPackageMapper.cs` (new) | Converts a loaded `ElementStudioComponentPackage` (an existing `.sep`) back into an `ElementStudioImportPackage`, so it can be re-opened in Studio Element+ through the existing import pipeline. |
| `src/ScadaBuilderV2.App/MainWindow.xaml.cs` (modify) | New context-menu command `object.open-in-element-studio` for converted Element+ objects; handler resolves the source `.sep`, maps it, writes it as a `.ft1`, and launches Studio Element+ (reusing `TryLaunchElementStudioAsync`). |
| `docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md` (modify) | New rule 25 documenting the modern-object "Ouvrir dans Studio Element+" command and its enablement condition. |
| `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md` (modify) | New coverage row pointing at the new tests. |
| `docs/00_governance/DECISION_REGISTER_V2.md` (modify) | New `DEC-0035` entry. |
| `tests/ScadaBuilderV2.Tests/ElementStudioComponentNamingTests.cs` (new) | Unit tests for the naming helper. |
| `tests/ScadaBuilderV2.Tests/ElementStudioComponentToImportPackageMapperTests.cs` (new) | Unit tests for the `.sep` → `.ft1` mapper. |
| `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` (modify) | Source-text assertions for the new modern-object menu command. |

---

### Task 1: Default component name resolves from the imported source name

**Files:**
- Create: `src/ScadaBuilderV2.Application/ElementStudio/ElementStudioComponentNaming.cs`
- Test: `tests/ScadaBuilderV2.Tests/ElementStudioComponentNamingTests.cs`

**Interfaces:**
- Produces: `ElementStudioComponentNaming.DefaultComponentName` (`const string`, value `"Nouveau composant"`) and `ElementStudioComponentNaming.ResolveDefaultComponentName(IReadOnlyList<string> importedSourceNames)` returning `string`. Task 2 calls this.

- [ ] **Step 1: Write the failing tests**

```csharp
using ScadaBuilderV2.Application.ElementStudio;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ElementStudioComponentNamingTests
{
    [TestMethod]
    public void ResolveDefaultComponentNameUsesFirstImportedSourceName()
    {
        var result = ElementStudioComponentNaming.ResolveDefaultComponentName(new[] { "Condenseur", "Condenseur_shadow" });

        Assert.AreEqual("Condenseur", result);
    }

    [TestMethod]
    public void ResolveDefaultComponentNameSkipsBlankNamesBeforeFirstNonBlank()
    {
        var result = ElementStudioComponentNaming.ResolveDefaultComponentName(new[] { "", "   ", "Ventilateur" });

        Assert.AreEqual("Ventilateur", result);
    }

    [TestMethod]
    public void ResolveDefaultComponentNameFallsBackToPlaceholderWhenAllNamesBlank()
    {
        var result = ElementStudioComponentNaming.ResolveDefaultComponentName(new[] { "", "  " });

        Assert.AreEqual(ElementStudioComponentNaming.DefaultComponentName, result);
    }

    [TestMethod]
    public void ResolveDefaultComponentNameFallsBackToPlaceholderWhenNoItems()
    {
        var result = ElementStudioComponentNaming.ResolveDefaultComponentName(Array.Empty<string>());

        Assert.AreEqual(ElementStudioComponentNaming.DefaultComponentName, result);
    }

    [TestMethod]
    public void ResolveDefaultComponentNameThrowsOnNullList()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            ElementStudioComponentNaming.ResolveDefaultComponentName(null!));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ElementStudioComponentNamingTests"`
Expected: FAIL (build error) — `ElementStudioComponentNaming` does not exist yet.

- [ ] **Step 3: Write the minimal implementation**

```csharp
namespace ScadaBuilderV2.Application.ElementStudio;

/// <summary>
/// Resolves the default display name Element Studio should seed a new component with.
/// </summary>
/// <remarks>
/// Decisions: DEC-0035. Contracts: none (Application-only helper, no persisted contract).
/// Tests: ElementStudioComponentNamingTests.cs.
/// </remarks>
public static class ElementStudioComponentNaming
{
    public const string DefaultComponentName = "Nouveau composant";

    public static string ResolveDefaultComponentName(IReadOnlyList<string> importedSourceNames)
    {
        ArgumentNullException.ThrowIfNull(importedSourceNames);

        var firstNonBlank = importedSourceNames.FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        return string.IsNullOrWhiteSpace(firstNonBlank) ? DefaultComponentName : firstNonBlank;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ElementStudioComponentNamingTests"`
Expected: PASS (5/5).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Application/ElementStudio/ElementStudioComponentNaming.cs tests/ScadaBuilderV2.Tests/ElementStudioComponentNamingTests.cs
git commit -m "feat: add ElementStudioComponentNaming.ResolveDefaultComponentName"
```

---

### Task 2: Wire Element Studio's workspace to seed the component name from the import

**Files:**
- Modify: `src/ScadaBuilderV2.ElementStudio.App/ElementStudioViewModels.cs:1-38`
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` (new test method, reusing the existing source-read pattern but pointed at the `ElementStudio.App` project directory)

**Interfaces:**
- Consumes: `ElementStudioComponentNaming.ResolveDefaultComponentName(IReadOnlyList<string>)` from Task 1.

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`:

```csharp
    [TestMethod]
    public void WorkspaceSeedsComponentNameFromFirstImportedSourceName()
    {
        var source = ReadElementStudioAppSource("ElementStudioViewModels.cs");

        StringAssert.Contains(source, "ElementStudioComponentNaming.ResolveDefaultComponentName");
        Assert.IsFalse(
            source.Contains("private string componentName = \"Nouveau composant\";", StringComparison.Ordinal),
            "componentName must no longer be hardcoded to the placeholder default.");
    }

    private static string ReadElementStudioAppSource(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var studioAppDir = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.ElementStudio.App");
            if (Directory.Exists(studioAppDir))
            {
                return File.ReadAllText(Path.Combine(studioAppDir, fileName));
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/ScadaBuilderV2.ElementStudio.App from test base directory.");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=WorkspaceSeedsComponentNameFromFirstImportedSourceName"`
Expected: FAIL — source still contains the hardcoded literal.

- [ ] **Step 3: Implement the minimal change**

In `src/ScadaBuilderV2.ElementStudio.App/ElementStudioViewModels.cs`, add the using and change the constructor:

```csharp
using ScadaBuilderV2.Application.ElementStudio;
```

Replace:

```csharp
    private string componentName = "Nouveau composant";
```

with:

```csharp
    private string componentName = ElementStudioComponentNaming.DefaultComponentName;
```

Then, inside the constructor (currently ends with `SetSelectedItems(ImportedItems.Take(1));`), add the seeding call right after `ImportedItems` is populated:

```csharp
    public ElementStudioWorkspaceViewModel(ElementStudioImportPackage package, IEnumerable<string> diagnostics)
    {
        Package = package;
        workzoneWidth = Math.Max(160, package.Bounds.Width);
        workzoneHeight = Math.Max(120, package.Bounds.Height);
        ImportedItems = new ObservableCollection<ElementStudioItemViewModel>(
            package.Items.Select((item, index) => new ElementStudioItemViewModel(item, index)));
        SelectedItems = new ObservableCollection<ElementStudioItemViewModel>();
        Diagnostics = new ObservableCollection<string>(diagnostics);
        nextElementIndex = ImportedItems.Count + 1;
        componentName = ElementStudioComponentNaming.ResolveDefaultComponentName(
            package.Items.Select(item => item.SourceName).ToArray());
        SetSelectedItems(ImportedItems.Take(1));
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=WorkspaceSeedsComponentNameFromFirstImportedSourceName"`
Expected: PASS.

- [ ] **Step 5: Run the full test suite to check for regressions**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: All existing tests still PASS (no other test asserts the old literal `"Nouveau composant"` as the constructor default — confirmed by searching the test project for that string before starting this task).

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.ElementStudio.App/ElementStudioViewModels.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "fix: seed Element Studio component name from imported source element name"
```

---

### Task 3: Map an existing `.sep` component back into an editable import package

**Files:**
- Create: `src/ScadaBuilderV2.Application/ElementStudio/ElementStudioComponentToImportPackageMapper.cs`
- Test: `tests/ScadaBuilderV2.Tests/ElementStudioComponentToImportPackageMapperTests.cs`

**Interfaces:**
- Consumes: `ElementStudioComponentPackage`, `ElementStudioComponent`, `ElementStudioComponentPart`, `ElementStudioComponentPartKind` (from `ElementStudioComponentModels.cs`); `ElementStudioImportPackageFactory.Create(...)`, `ElementStudioLegacyItem`, `ElementStudioPackageMetadata.Current(string)` (existing, from `ElementStudioModels.cs` / `ElementStudioImportPackageFactory.cs`).
- Produces: `ElementStudioComponentToImportPackageMapper.ToEditablePackage(ElementStudioComponentPackage sepPackage, string sepFilePath, string createdByVersion)` returning `ElementStudioImportPackage`. Task 4 calls this from `ScadaBuilderV2.App`.

- [ ] **Step 1: Write the failing tests**

```csharp
using ScadaBuilderV2.Application.ElementStudio;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ElementStudioComponentToImportPackageMapperTests
{
    [TestMethod]
    public void ToEditablePackagePreservesPartGeometryAndSetsTargetLibraryPath()
    {
        var sepPackage = CreateComponentPackage(
            CreatePart("part-1", "Condenseur_corps", ElementStudioComponentPartKind.Rectangle));

        var result = ElementStudioComponentToImportPackageMapper.ToEditablePackage(
            sepPackage,
            @"C:\projects\AMR_REF_SCADA_V2\library\elements\Condenseur.sep",
            "V2.1.3.0003");

        var item = result.Items.Single();
        Assert.AreEqual("part-1", item.SourceElementId);
        Assert.AreEqual("Condenseur_corps", item.SourceName);
        Assert.AreEqual("Rectangle", item.LegacyType);
        Assert.AreEqual(@"C:\projects\AMR_REF_SCADA_V2\library\elements", result.TargetLibraryPath);
    }

    [TestMethod]
    public void ToEditablePackageFlattensGroupPartsIntoTheirChildren()
    {
        var groupChild = CreatePart("child-1", "Aile1", ElementStudioComponentPartKind.Polygon);
        var group = CreatePart("group-1", "Ailettes", ElementStudioComponentPartKind.Group) with
        {
            Children = new[] { groupChild }
        };
        var sepPackage = CreateComponentPackage(group);

        var result = ElementStudioComponentToImportPackageMapper.ToEditablePackage(
            sepPackage,
            @"C:\lib\Condenseur.sep",
            "V2.1.3.0003");

        Assert.AreEqual(1, result.Items.Count);
        Assert.AreEqual("child-1", result.Items[0].SourceElementId);
    }

    [TestMethod]
    public void ToEditablePackageUsesSourceTraceWhenPresent()
    {
        var sourceTrace = new ElementStudioComponentSourceTrace(
            "AMR_REF_SCADA_V2",
            "win00008",
            "dist/pages/win00008.html",
            new[] { "793" });
        var sepPackage = CreateComponentPackage(
            CreatePart("part-1", "Condenseur_corps", ElementStudioComponentPartKind.Rectangle),
            sourceTrace);

        var result = ElementStudioComponentToImportPackageMapper.ToEditablePackage(
            sepPackage,
            @"C:\lib\Condenseur.sep",
            "V2.1.3.0003");

        Assert.AreEqual("AMR_REF_SCADA_V2", result.SourceProjectId);
        Assert.AreEqual("win00008", result.SourceSceneId);
        Assert.AreEqual("dist/pages/win00008.html", result.SourcePagePath);
    }

    [TestMethod]
    public void ToEditablePackageThrowsWhenComponentHasNoParts()
    {
        var sepPackage = CreateComponentPackage();

        Assert.ThrowsException<InvalidOperationException>(() =>
            ElementStudioComponentToImportPackageMapper.ToEditablePackage(
                sepPackage,
                @"C:\lib\Vide.sep",
                "V2.1.3.0003"));
    }

    private static ElementStudioComponentPackage CreateComponentPackage(
        params ElementStudioComponentPart[] parts)
    {
        return CreateComponentPackage(parts, sourceTrace: null);
    }

    private static ElementStudioComponentPackage CreateComponentPackage(
        ElementStudioComponentPart part,
        ElementStudioComponentSourceTrace sourceTrace)
    {
        return CreateComponentPackage(new[] { part }, sourceTrace);
    }

    private static ElementStudioComponentPackage CreateComponentPackage(
        ElementStudioComponentPart[] parts,
        ElementStudioComponentSourceTrace? sourceTrace)
    {
        var component = new ElementStudioComponent(
            "condenseur",
            "Condenseur",
            new SceneBounds(0, 0, 120, 80),
            new ElementStudioComponentVisual(ElementStudioComponentVisualKind.Svg, SvgMarkup: "<svg></svg>"),
            parts,
            Array.Empty<ElementStudioEmbeddedAsset>(),
            Array.Empty<ElementStudioComponentBinding>(),
            Array.Empty<ElementStudioComponentEvent>(),
            sourceTrace);
        return new ElementStudioComponentPackage(
            ElementStudioComponentMetadata.Current("V2.1.3.0003"),
            component);
    }

    private static ElementStudioComponentPart CreatePart(
        string partId,
        string name,
        ElementStudioComponentPartKind kind)
    {
        return new ElementStudioComponentPart(
            partId,
            name,
            kind,
            new SceneBounds(10, 20, 30, 40),
            ElementStudioStyleSnapshot.Default,
            Geometry: "M 10 20 L 40 20 L 40 60 Z");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ElementStudioComponentToImportPackageMapperTests"`
Expected: FAIL (build error) — `ElementStudioComponentToImportPackageMapper` does not exist yet.

- [ ] **Step 3: Write the minimal implementation**

```csharp
namespace ScadaBuilderV2.Application.ElementStudio;

/// <summary>
/// Converts a saved `.sep` component back into an <see cref="ElementStudioImportPackage"/> so an
/// already-published library component can be re-opened and re-edited in Studio Element+ through
/// the existing import pipeline, instead of only being buildable from fresh legacy captures.
/// </summary>
/// <remarks>
/// Decisions: DEC-0035. Contracts: MENUS_AND_SURFACES_CONTRACT_V2.md rule 25.
/// Tests: ElementStudioComponentToImportPackageMapperTests.cs.
/// </remarks>
public static class ElementStudioComponentToImportPackageMapper
{
    public static ElementStudioImportPackage ToEditablePackage(
        ElementStudioComponentPackage sepPackage,
        string sepFilePath,
        string createdByVersion)
    {
        ArgumentNullException.ThrowIfNull(sepPackage);
        ArgumentException.ThrowIfNullOrWhiteSpace(sepFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdByVersion);

        var component = sepPackage.Component;
        var flattenedParts = FlattenParts(component.Parts);
        if (flattenedParts.Count == 0)
        {
            throw new InvalidOperationException(
                $"Le composant '{component.Name}' ne contient aucune partie editable.");
        }

        var items = flattenedParts
            .Select((part, index) => new ElementStudioLegacyItem(
                part.PartId,
                part.Name,
                part.Kind.ToString(),
                part.Bounds,
                new SceneBounds(0, 0, 0, 0),
                part.Geometry,
                part.HtmlMarkup,
                part.Text,
                part.Style,
                index,
                null))
            .ToArray();

        var sourceTrace = component.SourceTrace;
        var targetLibraryPath = Path.GetDirectoryName(sepFilePath);
        if (string.IsNullOrWhiteSpace(targetLibraryPath))
        {
            throw new ArgumentException(
                $"Impossible de resoudre le dossier contenant '{sepFilePath}'.", nameof(sepFilePath));
        }

        return ElementStudioImportPackageFactory.Create(
            $"studio_edit_{component.ComponentId}_{Guid.NewGuid():N}",
            sourceTrace?.SourceProjectId ?? "AMR_REF_SCADA_V2",
            sourceTrace?.SourceSceneId ?? component.ComponentId,
            sourceTrace?.SourcePagePath ?? sepFilePath,
            items,
            ElementStudioPackageMetadata.Current(createdByVersion),
            targetLibraryPath);
    }

    private static List<ElementStudioComponentPart> FlattenParts(IReadOnlyList<ElementStudioComponentPart> parts)
    {
        var flattened = new List<ElementStudioComponentPart>();
        foreach (var part in parts)
        {
            if (part.Kind == ElementStudioComponentPartKind.Group && part.ChildParts.Count > 0)
            {
                flattened.AddRange(FlattenParts(part.ChildParts));
            }
            else
            {
                flattened.Add(part);
            }
        }

        return flattened;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ElementStudioComponentToImportPackageMapperTests"`
Expected: PASS (4/4).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Application/ElementStudio/ElementStudioComponentToImportPackageMapper.cs tests/ScadaBuilderV2.Tests/ElementStudioComponentToImportPackageMapperTests.cs
git commit -m "feat: add mapper to re-open an existing .sep component as an editable import package"
```

---

### Task 4: Restore "Ouvrir dans Studio Element+" for converted Element+ objects

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:3707-3744` (menu build), `:3795-3860` area (command dispatch switch), and a new private method near `OpenSelectedLegacyInElementStudioAsync` (currently starting at line 2329)
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` (new test method)

**Interfaces:**
- Consumes: `ElementStudioComponentToImportPackageMapper.ToEditablePackage(ElementStudioComponentPackage, string, string)` from Task 3; existing `_elementStudioComponentPackageStore.ReadFromPathAsync(string)`, `_elementStudioPackageWriter.WriteToProjectAsync(ElementStudioImportPackage, string)`, `TryLaunchElementStudioAsync(string?)`, `AppendElementStudioLaunchLog(string, ElementStudioLaunchResult)`, `LoadVersionText()`, `BuildLibraryRegistryAsync()` (all already defined in `MainWindow.xaml.cs`).
- Produces: nothing new consumed by later tasks — this is the last code task.

**Prerequisite reading (do not skip):** `ScadaElement.Data?.TagBinding` already carries the source library filename for any element created via `CreateElementPlusLibraryInstanceAsync` (`MainWindow.xaml.cs:4582-4607` — it passes `Path.GetFileName(packagePath)` positionally into the `TagBinding` slot of `ScadaElementData`). This is confirmed live in `projects/AMR_REF_SCADA_V2/scenes/win00008.scene.json`, where one `Custom`-kind element has `"TagBinding": "pompeAmmoniac.sep"`. Do not rename this field or add a new one — reuse it as-is.

- [ ] **Step 1: Write the failing test**

Add to `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`:

```csharp
    [TestMethod]
    public void ModernContextMenuExposesElementStudioCommandForLibraryInstances()
    {
        var source = ReadMainWindowSource();

        StringAssert.Contains(source, "\"object.open-in-element-studio\"");
        StringAssert.Contains(source, "OpenSelectedModernComponentInElementStudioAsync");
        StringAssert.Contains(source, "ElementStudioComponentToImportPackageMapper.ToEditablePackage");
        StringAssert.Contains(source, "Cet objet n'a pas ete instancie depuis la bibliotheque Element+.");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=ModernContextMenuExposesElementStudioCommandForLibraryInstances"`
Expected: FAIL — none of these strings exist yet.

- [ ] **Step 3: Add the menu command**

In `src/ScadaBuilderV2.App/MainWindow.xaml.cs`, inside `BuildContextMenuCommands`, change the `modernCommands` block (currently lines 3707-3721) to:

```csharp
            var modernCommands = new List<EditorCommandDescriptor>
            {
                new("object.properties", "Propriete", "object"),
                new("object.delete", "Supprimer la selection", "object")
            };

            if (_selectedSceneObjectIds.Count == 1)
            {
                var sourceSepFileName = selected.Kind == ScadaElementKind.Custom
                    ? selected.Data?.TagBinding
                    : null;
                modernCommands.Insert(0, new EditorCommandDescriptor(
                    "object.open-in-element-studio",
                    "Ouvrir dans Studio Element+",
                    "element-studio",
                    IsEnabled: !string.IsNullOrWhiteSpace(sourceSepFileName),
                    DisabledReason: "Cet objet n'a pas ete instancie depuis la bibliotheque Element+."));
            }

            if (_selectedSceneObjectIds.Count > 1)
            {
                modernCommands.Insert(0, new EditorCommandDescriptor("object.group", "Grouper", "group"));
            }
```

(Leave the rest of the method — the `Group`/`ungroup` and `object.order` blocks below it — unchanged.)

- [ ] **Step 4: Add the command dispatch**

In the `ExecuteEditorCommandAsync` switch (the same one containing `case "object.properties":` around line 3831 and `case "source.open-in-element-studio":` around line 3854), add:

```csharp
            case "object.open-in-element-studio":
                await OpenSelectedModernComponentInElementStudioAsync();
                break;
```

- [ ] **Step 5: Implement the handler and library lookup**

Add these two methods near `OpenSelectedLegacyInElementStudioAsync` (after its closing brace, currently line 2359):

```csharp
    private async Task OpenSelectedModernComponentInElementStudioAsync()
    {
        if (_repositoryRoot is null || _activeScene is null)
        {
            SetStatus("Studio Element+ indisponible: aucun projet ou scene active.");
            return;
        }

        var selected = _selectedSceneObjectIds.Count == 1
            ? _activeScene.FindElementRecursive(_selectedSceneObjectIds.Single())
            : null;
        var sourceSepFileName = selected?.Kind == ScadaElementKind.Custom ? selected.Data?.TagBinding : null;
        if (selected is null || string.IsNullOrWhiteSpace(sourceSepFileName))
        {
            SetStatus("Selectionnez un objet Element+ instancie depuis la bibliotheque pour ouvrir Studio Element+.");
            return;
        }

        var sepFilePath = await ResolveLibrarySepFilePathAsync(sourceSepFileName);
        if (sepFilePath is null)
        {
            SetStatus($"Composant source '{sourceSepFileName}' introuvable dans les bibliotheques Element+ configurees.");
            return;
        }

        try
        {
            var sepPackage = await _elementStudioComponentPackageStore.ReadFromPathAsync(sepFilePath);
            var version = LoadVersionText();
            var editPackage = ElementStudioComponentToImportPackageMapper.ToEditablePackage(sepPackage, sepFilePath, version);
            var projectsRoot = Path.Combine(_repositoryRoot, "SCADA_BUILDER_V2", "projects");
            var packagePath = await _elementStudioPackageWriter.WriteToProjectAsync(editPackage, projectsRoot);
            var launch = await TryLaunchElementStudioAsync(packagePath);
            AppendElementStudioLaunchLog(packagePath, launch);
            SetStatus(launch.Launched
                ? $"Studio Element+ ouvert pour edition: {Path.GetFileName(sepFilePath)}"
                : $"Package Studio Element+ cree: {packagePath}. {launch.Message}");
        }
        catch (Exception ex)
        {
            SetStatus($"Erreur ouverture Studio Element+: {ex.Message}");
        }
    }

    private async Task<string?> ResolveLibrarySepFilePathAsync(string fileName)
    {
        var registry = await BuildLibraryRegistryAsync();
        foreach (var entry in registry.Entries)
        {
            var candidatePath = Path.Combine(entry.Path, fileName);
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        return null;
    }
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test ScadaBuilderV2.sln --filter "Name=ModernContextMenuExposesElementStudioCommandForLibraryInstances"`
Expected: PASS.

- [ ] **Step 7: Build the solution and run the full test suite**

Run: `dotnet build ScadaBuilderV2.sln` — expect 0 errors (watch for `using ScadaBuilderV2.Application.ElementStudio;` already present at the top of `MainWindow.xaml.cs`; it is, since `ElementStudioImportPackage` etc. are already used there).
Run: `dotnet test ScadaBuilderV2.sln --no-restore` — expect all tests PASS, including the existing `LegacyContextMenuExposesElementStudioCommand` (unchanged) and `StudioElementPlusContractTests`.

- [ ] **Step 8: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: restore Ouvrir dans Studio Element+ for converted Element+ objects"
```

---

### Task 5: Record the contract and decision

**Files:**
- Modify: `docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md:75` (add rule 25)
- Modify: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md:107` (add coverage row)
- Modify: `docs/00_governance/DECISION_REGISTER_V2.md` (add `DEC-0035`)

- [ ] **Step 1: Add contract rule 25**

In `docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md`, after rule 24, add:

```markdown
25. The Element+ context menu exposes `object.open-in-element-studio` ("Ouvrir dans Studio Element+") for a single selected converted object only when it was instantiated from a library `.sep` (tracked via `ScadaElementData.TagBinding`); otherwise it is visible but disabled with the reason "Cet objet n'a pas ete instancie depuis la bibliotheque Element+."
```

- [ ] **Step 2: Add regression coverage row**

In `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`, after the `Studio Element+ contract` row, add:

```markdown
| Studio Element+ re-edit from scene | `WebViewContextMenuScriptTests.cs`, `ElementStudioComponentToImportPackageMapperTests.cs`, `ElementStudioComponentNamingTests.cs` |
```

- [ ] **Step 3: Add DEC-0035**

In `docs/00_governance/DECISION_REGISTER_V2.md`, after the last existing `DEC-00xx` entry, add:

```markdown
### DEC-0035 - Re-Edit Existing Element+ Library Components From The Scene

Status: Active
Created: 2026-07-06 00:00 America/Toronto
Created in commit: `PENDING`
Deprecated: N/A
Deprecated in commit: N/A
Superseded by: N/A
Owner document: `docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md`

Context:

Once a legacy element is converted to Element+, right-clicking it exposed no way back into Studio Element+ to edit its source `.sep` component; only never-converted legacy elements could open Studio Element+, and only to create a brand-new component. Users who convert to Element+ (required for grouping/resizing) lost their only path to editing that component's appearance. Separately, `.sep` components created from a library import always kept the placeholder name "Nouveau composant" because nothing seeded `ElementStudioWorkspaceViewModel.ComponentName` from the captured source element's name.

Decision:

A converted Element+ object created from a library component (`ScadaElementData.TagBinding` holds the source `.sep` filename) exposes `object.open-in-element-studio` in its context menu. Activating it reads the `.sep` via `ElementStudioComponentPackageStore`, maps it back into an `ElementStudioImportPackage` via `ElementStudioComponentToImportPackageMapper.ToEditablePackage` (flattening `Group` parts into their children), writes it through the existing `.ft1` import pipeline with `TargetLibraryPath` set to the original `.sep`'s directory, and launches Studio Element+ against it — so Save re-targets the same library folder. Separately, `ElementStudioWorkspaceViewModel` now seeds `ComponentName` via `ElementStudioComponentNaming.ResolveDefaultComponentName`, defaulting to the first imported source element's name instead of the placeholder.

Consequences:

Editing a re-opened component does not automatically overwrite the exact original `.sep` file (the Save dialog defaults to the same folder and a filename derived from `ComponentName`, but the user must confirm the save); nested groups deeper than one level are flattened on re-edit and must be re-grouped manually in Studio Element+.

Regression coverage:

`tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`, `tests/ScadaBuilderV2.Tests/ElementStudioComponentToImportPackageMapperTests.cs`, `tests/ScadaBuilderV2.Tests/ElementStudioComponentNamingTests.cs`
```

- [ ] **Step 4: Validate docs**

Run: `powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1`
Expected: PASS (no broken cross-references, version headers consistent).

- [ ] **Step 5: Commit**

```bash
git add docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md docs/08_implementation_status/REGRESSION_COVERAGE_V2.md docs/00_governance/DECISION_REGISTER_V2.md
git commit -m "docs: record DEC-0035 for Element+ library component re-edit from the scene"
```

---

### Task 6: Manual end-to-end verification

Both fixes touch WPF UI that the automated suite can only verify at the source-text/unit level. Before calling this done, run the real apps:

- [ ] **Step 1:** `dotnet build ScadaBuilderV2.sln` — 0 errors, 0 new warnings.
- [ ] **Step 2:** `dotnet run --project src/ScadaBuilderV2.App`. Open `AMR_REF_SCADA_V2`, open a scene containing a library-instantiated Element+ object (e.g. `win00008` has one with `TagBinding: "pompeAmmoniac.sep"`). Right-click it: confirm "Ouvrir dans Studio Element+" is present and enabled. Right-click a Custom Element+ object that was NOT instantiated from the library (or any non-Custom Element+ object): confirm the item is present but disabled, with the hover tooltip showing the disabled reason.
- [ ] **Step 3:** Click "Ouvrir dans Studio Element+" on the library-instantiated object. Confirm Studio Element+ launches and the workspace shows the component's shapes/parts loaded (not empty), with `ComponentName` pre-filled to the component's real name (not "Nouveau composant").
- [ ] **Step 4:** Drag a new component from the library panel into a scene. Confirm the new element's name in the Element tree is the component's real name with an index suffix (e.g. "Condenseur_1"), not "Nouveau composant_1".
- [ ] **Step 5:** In Element Studio, import a fresh legacy selection into a new component (existing flow, unaffected by this plan) and confirm `ComponentName` defaults to the selected legacy element's name, not the placeholder.
- [ ] **Step 6:** No commit for this task — it is verification only. If any step fails, return to the relevant task and fix before proceeding.