# Export : critère de wrapper DOM pour les groupes — Plan d'implémentation

Date: 2026-07-09
Status: Draft plan — en attente d'approbation
Document version: `V2.1.5.0000`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remplacer le critère obsolète `EventBindings.Count > 0` par un helper `GroupRequiresRuntimeWrapper` qui matérialise un wrapper DOM pour tout groupe portant des données runtime modernes (CommandConfig, StateConfig, ReadTagId, WriteTagId), et pas seulement des EventBindings legacy.

**Architecture:** Extraire un helper privé statique dans `Ft100SceneExporter` qui centralise la décision. Remplacer les 4 occurrences du critère `EventBindings.Count > 0` par un appel à ce helper. Aucun changement de contrat TF100Web, aucun changement de modèle de données.

**Tech Stack:** C# 12, .NET 8-windows, MSTest

**Spec:** `docs/superpowers/specs/2026-07-09-export-group-runtime-wrapper.md`

## Global Constraints

- Ne pas modifier le contrat TF100Web (format postMessage, attributs data-scada-*, manifest)
- Ne pas modifier le modèle de données (`ScadaElement`, `ScadaScene`, `ScadaCommandBinding`, etc.)
- Ne pas modifier le runtime JS
- Le helper doit être utilisé aux 4 endroits qui décident du rendu d'un groupe
- Backward compat : un groupe avec seulement EventBindings doit continuer à fonctionner
- Les tests existants doivent rester verts
- PowerShell depuis `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`
- Chaque commit doit compiler (`dotnet build ScadaBuilderV2.sln`)

## Problème

L'exporteur `Ft100SceneExporter` utilise `element.EventBindings.Count > 0` comme critère unique pour décider si un `ScadaElement` de kind `Group` doit être matérialisé en wrapper `<div>` dans le HTML exporté.

Ce critère est obsolète depuis l'introduction du système CommandConfig/StateConfig. Un groupe peut désormais porter :
- `CommandConfig` avec des commandes (Navigate, WriteTag, etc.)
- `StateConfig` avec des règles d'état
- `Data.ReadTagId` / `Data.WriteTagId` pour les value bindings

...sans avoir le moindre `EventBindings`. Quand un tel groupe est rencontré, l'exporteur l'aplatit (il rend ses enfants directement dans le parent supérieur) et **tous les attributs runtime modernes sont perdus**.

### Impact

| Cas | Comportement actuel | Comportement attendu |
|---|---|---|
| Groupe avec EventBindings + CommandConfig | Wrapper rendu, OK | Wrapper rendu, OK |
| Groupe avec seulement CommandConfig | **Aplati → data-scada-command-config perdu** | Wrapper rendu avec data-scada-command-config |
| Groupe avec seulement StateConfig | **Aplati → data-scada-state-config perdu** | Wrapper rendu avec data-scada-state-config |
| Groupe avec seulement ReadTagId/WriteTagId | **Aplati → value bindings perdus** | Wrapper rendu avec data-scada-read-tag/write-tag |
| Groupe sans aucune donnée runtime | Aplati (correct) | Aplati (correct) |

### Les 4 points de décision concernés

| Ligne | Méthode | Rôle |
|---|---|---|
| 1025 | `BuildElementHtml` | Wrapper DOM HTML |
| 1659 | `AppendElementCss` | Règles CSS du wrapper |
| 1907 | `ShouldExportManifestObject` | Visibilité dans le manifest |
| 1937 | `FlattenExportedElementBounds` | Calcul de `RequiredDisplaySize` |

## Design

### Helper unique

```csharp
/// <summary>
/// Determines whether a Group element requires a runtime DOM wrapper in the
/// exported output. Returns <c>true</c> when the group carries at least one
/// piece of runtime data (legacy event bindings, modern commands, state rules,
/// non-default fallback, or tag value bindings) that needs a DOM node.
/// </summary>
private static bool GroupRequiresRuntimeWrapper(ScadaElement element)
{
    if (element.Kind != ScadaElementKind.Group)
        return false;

    return element.EventBindings.Count > 0
        || element.EffectiveCommandConfig.Commands.Count > 0
        || element.EffectiveStateConfig.States.Count > 0
        || HasNonDefaultFallback(element.EffectiveStateConfig)
        || !string.IsNullOrWhiteSpace(element.Data?.ReadTagId)
        || !string.IsNullOrWhiteSpace(element.Data?.WriteTagId);
}
```

`HasNonDefaultFallback` existe déjà (ligne 1125) — pas besoin de la dupliquer.

### Remplacement

Remplacer chaque occurrence de `element.EventBindings.Count > 0` dans un contexte de groupe par `GroupRequiresRuntimeWrapper(element)`.

---

### Task 1: Écrire les tests de régression

**Files:**
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

**Interfaces:**
- Consumes: `Ft100SceneExporter`, `ScadaElement`, `ScadaScene` (existant)
- Produces: 3 nouveaux tests qui échouent avant l'implémentation

- [ ] **Step 1: Ajouter le test — groupe avec CommandConfig sans EventBindings**

Dans `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`, ajouter :

```csharp
[TestMethod]
public async Task Export_GroupWithOnlyCommandConfig_RendersWrapperWithCommandAttribute()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "grp_cmd.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var commandConfig = new ScadaElementCommandConfig(new[] {
        new ScadaCommandBinding("nav1", "Go", true, ScadaCommandTrigger.OnClick,
            ScadaCommandKind.Navigate, TargetPageId: "win00099")
    });

    var group = new ScadaElement(
        "grp_nav", "Nav Group", ScadaElementKind.Group,
        new SceneBounds(100, 200, 160, 70),
        null, ScadaElementLayout.Absolute, ScadaElementStyle.DefaultText,
        Children: new[] {
            new ScadaElement("btn1", "Btn", ScadaElementKind.Button,
                new SceneBounds(5, 6, 80, 24), null,
                new ScadaElementLayout(ElementPositionMode.Relative, "grp_nav"),
                ScadaElementStyle.DefaultInput,
                new ScadaElementData("Go", null, null, null, null, null, null, null, null, false))
        },
        CommandConfig: commandConfig);

    var scene = ScadaScene.CreateEmpty("win00008", "Test", new(400, 400))
        .WithElement(group);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var html = await File.ReadAllTextAsync(result.HtmlPath);

        // Le groupe doit avoir un wrapper DOM
        StringAssert.Contains(html, "id=\"ft100-win00008__grp_nav\"",
            "Group with CommandConfig must have a DOM wrapper.");
        // L'attribut command-config doit être présent sur le wrapper
        StringAssert.Contains(html, "data-scada-command-config=\"",
            "Group wrapper must carry data-scada-command-config.");
        // L'attribut ne doit PAS contenir data-scada-events (pas d'EventBindings)
        Assert.IsFalse(html.Contains("data-scada-events="),
            "Group without EventBindings must not emit data-scada-events.");
        // L'enfant doit être rendu
        StringAssert.Contains(html, "id=\"ft100-win00008__btn1\"");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
```

- [ ] **Step 2: Ajouter le test — groupe avec StateConfig sans EventBindings**

```csharp
[TestMethod]
public async Task Export_GroupWithOnlyStateConfig_RendersWrapperWithStateAttribute()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "grp_state.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var stateConfig = new ScadaElementStateConfig(
        ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
        ScadaEffectBlock.Empty,
        new[] {
            new ScadaStateRule("s1", "Running", true,
                new ScadaExpression("{Motor}>0",
                    new ScadaExprBinary(ScadaExprBinaryOp.GreaterThan,
                        new ScadaExprTagRef("Motor"), new ScadaExprLiteralNumber(0)),
                    new[] { "Motor" }),
                ScadaEffectBlock.Empty with { BackgroundColor = "#4CAF50" })
        });

    var group = new ScadaElement(
        "grp_state", "State Group", ScadaElementKind.Group,
        new SceneBounds(100, 200, 160, 70),
        null, ScadaElementLayout.Absolute, ScadaElementStyle.DefaultText,
        Children: new[] {
            new ScadaElement("shape1", "Shape", ScadaElementKind.Shape,
                new SceneBounds(5, 6, 80, 24), null,
                new ScadaElementLayout(ElementPositionMode.Relative, "grp_state"),
                ScadaElementStyle.DefaultText,
                new ScadaElementData(null, null, null, null, null, null, null, null, null, false),
                ShapeKind: ScadaShapeKind.Rectangle)
        },
        StateConfig: stateConfig);

    var scene = ScadaScene.CreateEmpty("win00008", "Test", new(400, 400))
        .WithElement(group);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var html = await File.ReadAllTextAsync(result.HtmlPath);

        StringAssert.Contains(html, "id=\"ft100-win00008__grp_state\"",
            "Group with StateConfig must have a DOM wrapper.");
        StringAssert.Contains(html, "data-scada-state-config=\"",
            "Group wrapper must carry data-scada-state-config.");
        Assert.IsFalse(html.Contains("data-scada-events="));
        StringAssert.Contains(html, "id=\"ft100-win00008__shape1\"");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
```

- [ ] **Step 3: Ajouter le test — groupe sans aucune donnée runtime reste aplati**

```csharp
[TestMethod]
public async Task Export_GroupWithNoRuntimeData_FlattensChildren()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "grp_empty.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var group = new ScadaElement(
        "grp_empty", "Empty Group", ScadaElementKind.Group,
        new SceneBounds(100, 200, 160, 70),
        null, ScadaElementLayout.Absolute, ScadaElementStyle.DefaultText,
        Children: new[] {
            new ScadaElement("shape1", "Shape", ScadaElementKind.Shape,
                new SceneBounds(5, 6, 80, 24), null,
                new ScadaElementLayout(ElementPositionMode.Relative, "grp_empty"),
                ScadaElementStyle.DefaultText,
                new ScadaElementData(null, null, null, null, null, null, null, null, null, false),
                ShapeKind: ScadaShapeKind.Rectangle)
        });
    // Pas de EventBindings, CommandConfig, StateConfig, ni tag bindings

    var scene = ScadaScene.CreateEmpty("win00008", "Test", new(400, 400))
        .WithElement(group);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var html = await File.ReadAllTextAsync(result.HtmlPath);

        // Le groupe SANS données runtime ne doit PAS avoir de wrapper
        Assert.IsFalse(html.Contains("id=\"ft100-win00008__grp_empty\""),
            "Group with no runtime data must be flattened (no wrapper).");
        // Mais l'enfant doit être rendu (positionné en absolu vs le parent)
        StringAssert.Contains(html, "id=\"ft100-win00008__shape1\"");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
```

- [ ] **Step 4: Ajouter le test — groupe avec ReadTagId/WriteTagId sans EventBindings**

```csharp
[TestMethod]
public async Task Export_GroupWithOnlyValueBindings_RendersWrapperWithTagAttributes()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "grp_value.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var group = new ScadaElement(
        "grp_value", "Value Group", ScadaElementKind.Group,
        new SceneBounds(100, 200, 160, 70),
        null, ScadaElementLayout.Absolute, ScadaElementStyle.DefaultText,
        Data: new ScadaElementData(null, null, null, null, null, null, null, null, null, false,
            ReadTagId: "tf100.mapping.42", WriteTagId: "tf100.mapping.99"),
        Children: new[] {
            new ScadaElement("input1", "Input", ScadaElementKind.InputText,
                new SceneBounds(5, 6, 80, 24), null,
                new ScadaElementLayout(ElementPositionMode.Relative, "grp_value"),
                ScadaElementStyle.DefaultInput,
                new ScadaElementData(null, "Texte", null, null, null, null, null, null, null, false))
        });

    var scene = ScadaScene.CreateEmpty("win00008", "Test", new(400, 400))
        .WithElement(group);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var html = await File.ReadAllTextAsync(result.HtmlPath);

        StringAssert.Contains(html, "id=\"ft100-win00008__grp_value\"",
            "Group with value bindings must have a DOM wrapper.");
        StringAssert.Contains(html, "data-scada-read-tag=\"tf100.mapping.42\"");
        StringAssert.Contains(html, "data-scada-write-tag=\"tf100.mapping.99\"");
        Assert.IsFalse(html.Contains("data-scada-events="));
        StringAssert.Contains(html, "id=\"ft100-win00008__input1\"");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
```

- [ ] **Step 5: Run tests to verify they fail**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Export_GroupWithOnly"
```

Expected: 4 tests FAIL — les groupes sans EventBindings sont aplatis, les attributs runtime sont absents.

- [ ] **Step 6: Commit**

```bash
git add tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "test: add regression tests for group runtime wrapper criteria

Four tests verify that groups with modern runtime data (CommandConfig,
StateConfig, value bindings) get a DOM wrapper even without legacy
EventBindings, and empty groups remain flattened.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: Extraire le helper et remplacer les 4 occurrences

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`

**Interfaces:**
- Consumes: `ScadaElement`, `HasNonDefaultFallback` (existant)
- Produces: `private static bool GroupRequiresRuntimeWrapper(ScadaElement element)`

- [ ] **Step 1: Ajouter le helper dans `Ft100SceneExporter.cs`**

Ajouter après `HasNonDefaultFallback` (après la ligne 1133) :

```csharp
/// <summary>
/// Determines whether a Group element requires a runtime DOM wrapper in the
/// exported output. Returns <c>true</c> when the group carries at least one
/// piece of runtime data (legacy event bindings, modern commands, state rules,
/// non-default quality fallback, or tag value bindings) that needs a DOM node
/// to be reachable by the TF100Web runtime.
/// </summary>
/// <remarks>
/// Decisions: DEC-0040.
/// Contracts: docs/superpowers/specs/2026-07-09-export-group-runtime-wrapper.md.
/// Tests: tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
/// </remarks>
private static bool GroupRequiresRuntimeWrapper(ScadaElement element)
{
    if (element.Kind != ScadaElementKind.Group)
        return false;

    return element.EventBindings.Count > 0
        || element.EffectiveCommandConfig.Commands.Count > 0
        || element.EffectiveStateConfig.States.Count > 0
        || HasNonDefaultFallback(element.EffectiveStateConfig)
        || !string.IsNullOrWhiteSpace(element.Data?.ReadTagId)
        || !string.IsNullOrWhiteSpace(element.Data?.WriteTagId);
}
```

- [ ] **Step 2: Remplacer dans `BuildElementHtml` (ligne 1025)**

```csharp
// Avant :
if (element.EventBindings.Count > 0)

// Après :
if (GroupRequiresRuntimeWrapper(element))
```

- [ ] **Step 3: Remplacer dans `AppendElementCss` (ligne 1659)**

```csharp
// Avant :
if (element.EventBindings.Count > 0)

// Après :
if (GroupRequiresRuntimeWrapper(element))
```

- [ ] **Step 4: Remplacer dans `ShouldExportManifestObject` (ligne 1907)**

```csharp
// Avant :
return element.Kind != ScadaElementKind.Group || element.EventBindings.Count > 0;

// Après :
return element.Kind != ScadaElementKind.Group || GroupRequiresRuntimeWrapper(element);
```

- [ ] **Step 5: Remplacer dans `FlattenExportedElementBounds` (ligne 1937)**

```csharp
// Avant :
if (element.EventBindings.Count > 0)

// Après :
if (GroupRequiresRuntimeWrapper(element))
```

- [ ] **Step 6: Build et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Ft100SceneExporterTests"
```

Expected: tous les tests verts (anciens + 4 nouveaux).

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs
git commit -m "fix: use modern runtime data as group wrapper criterion in exporter

Replace the obsolete EventBindings.Count > 0 check with a new
GroupRequiresRuntimeWrapper helper that also considers CommandConfig,
StateConfig, and value bindings. This ensures groups carrying modern
runtime data are materialized as DOM wrappers in exported HTML, CSS,
manifest, and bounds calculation.

Four call sites updated: BuildElementHtml, AppendElementCss,
ShouldExportManifestObject, FlattenExportedElementBounds.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: Vérification de régression complète

- [ ] **Step 1: Build + tests complets**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln
```

Tous les tests doivent passer.

- [ ] **Step 2: Vérifier le test existant `ExportPreservesGroupClickNavigateEventAsRuntimeWrapper`**

Ce test vérifie qu'un groupe avec EventBindings (via `WithChangePageEvent`) a bien un wrapper. Il doit continuer à passer — le helper inclut `EventBindings.Count > 0`.

- [ ] **Step 3: Vérifier le test existant `ExportProjectArchive_ProducesCompleteSb2WithStateCommandRuntime`**

Ce test exporte un élément Button (pas un groupe) avec StateConfig + CommandConfig. Il doit continuer à passer sans changement.

- [ ] **Step 4: Commit final (si applicable)**

```bash
git add docs/superpowers/plans/2026-07-09-export-group-runtime-wrapper.md
git add docs/superpowers/specs/2026-07-09-export-group-runtime-wrapper.md
git commit -m "docs: finalize export group runtime wrapper spec and plan"
```

---

### Vérification finale

- [ ] **Build + tests complets**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln
```

Tous les tests doivent passer, y compris les tests existants non modifiés.
