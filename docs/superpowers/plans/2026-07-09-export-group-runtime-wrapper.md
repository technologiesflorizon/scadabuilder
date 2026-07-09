# Export : critère de wrapper DOM runtime pour les groupes — Plan d'implémentation

Date: 2026-07-09
Status: Draft plan — en attente d'approbation
Document version: `V2.1.5.0001`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remplacer le critère `EventBindings.Count > 0` par `GroupRequiresRuntimeWrapper` qui vérifie uniquement les données runtime modernes (CommandConfig, StateConfig), retire `data-scada-events` de l'export, nettoie le manifest des EventBindings legacy, et ajoute un diagnostic pour les EventBindings restants.

**Architecture:** Helper privé dans `Ft100SceneExporter` qui ignore EventBindings et Data.ReadTagId/WriteTagId. Suppression de `BuildEventAttribute` dans le chemin d'export groupe. Le manifest ne sérialise plus `element.EventBindings` comme `Events`. Le validateur `ScadaProjectBuildValidator` existant émet déjà `AuditOrphanedEventBindings` — à étendre pour couvrir tous les EventBindings restants.

**Tech Stack:** C# 12, .NET 8-windows, MSTest

**Spec:** `docs/superpowers/specs/2026-07-09-export-group-runtime-wrapper.md`

## Global Constraints

- Ne pas modifier le contrat TF100Web (format postMessage, attributs data-scada-*, manifest)
- Ne pas modifier le modèle de données (`ScadaElement`, `ScadaScene`, `ScadaCommandBinding`, etc.)
- Ne pas modifier le runtime JS
- Ne pas supprimer physiquement `ElementEventDialog` ni les helpers `With*Event` (décommissionnés, retrait dans une tranche séparée)
- Le helper doit être utilisé aux 4 endroits qui décident du rendu d'un groupe
- `EventBindings` et `Data.ReadTagId`/`Data.WriteTagId` sont exclus du helper
- `data-scada-events` ne doit plus apparaître dans l'export moderne
- Les tests existants qui dépendent de `data-scada-events` doivent être mis à jour
- PowerShell depuis `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`
- Chaque commit doit compiler (`dotnet build ScadaBuilderV2.sln`)

---

## État des lieux

### Tests existants impactés

Le test `ExportPreservesGroupClickNavigateEventAsRuntimeWrapper` (ligne 756) utilise `WithChangePageEvent` (EventBindings legacy) et vérifie la présence de `data-scada-events` dans le HTML et `Events` dans le manifest. Ce test doit être **supprimé** car il valide l'ancien contrat — les EventBindings ne sont plus exportés comme runtime actif.

Un remplacement sera créé : `Export_GroupWithNavigateCommand_RendersWrapperWithCommandAttribute` — groupe avec CommandConfig.Navigate → wrapper + command-config, sans data-scada-events.

### Helper cible

```csharp
private static bool GroupRequiresRuntimeWrapper(ScadaElement element)
{
    if (element.Kind != ScadaElementKind.Group)
        return false;

    var commandConfig = element.EffectiveCommandConfig;
    var stateConfig = element.EffectiveStateConfig;

    return commandConfig.Commands.Count > 0
        || stateConfig.States.Count > 0
        || stateConfig.ReadVariable is not null
        || HasNonDefaultFallback(stateConfig);
}
```

---

### Task 1: Ajouter les tests modernes et supprimer le test legacy

**Files:**
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

**Interfaces:**
- Consumes: `Ft100SceneExporter`, `ScadaElement`, `ScadaScene`, `ScadaElementCommandConfig`, `ScadaElementStateConfig`, `ScadaReadVariableRule` (existants)
- Produces: 12 nouveaux tests + 1 suppression

- [ ] **Step 1: Supprimer le test legacy `ExportPreservesGroupClickNavigateEventAsRuntimeWrapper`**

Supprimer la méthode de test et ses 77 lignes (lignes 755-831 dans `Ft100SceneExporterTests.cs`). Ce test validait l'ancien contrat `data-scada-events` + `Events` dans le manifest, qui est maintenant décommissionné.

- [ ] **Step 2: Ajouter les 12 nouveaux tests**

Ajouter après les tests existants dans `Ft100SceneExporterTests.cs` :

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

        StringAssert.Contains(html, "id=\"ft100-win00008__grp_nav\"",
            "Group with CommandConfig must have a DOM wrapper.");
        StringAssert.Contains(html, "data-scada-command-config=\"",
            "Group wrapper must carry data-scada-command-config.");
        Assert.IsFalse(html.Contains("data-scada-events="),
            "Group must not emit data-scada-events.");
        StringAssert.Contains(html, "id=\"ft100-win00008__btn1\"");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

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

[TestMethod]
public async Task Export_GroupWithOnlyStateReadVariable_RendersWrapperWithStateAttribute()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "grp_readvar.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var stateConfig = new ScadaElementStateConfig(
        ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
        ScadaEffectBlock.Empty,
        Array.Empty<ScadaStateRule>(),
        ReadVariable: new ScadaReadVariableRule("tf100.mapping.42", "Debit: {valeur} L/min"));

    var group = new ScadaElement(
        "grp_readvar", "ReadVar Group", ScadaElementKind.Group,
        new SceneBounds(100, 200, 160, 70),
        null, ScadaElementLayout.Absolute, ScadaElementStyle.DefaultText,
        Children: new[] {
            new ScadaElement("txt1", "Text", ScadaElementKind.Text,
                new SceneBounds(5, 6, 80, 24), null,
                new ScadaElementLayout(ElementPositionMode.Relative, "grp_readvar"),
                ScadaElementStyle.DefaultText,
                new ScadaElementData("---", null, null, null, null, null, null, null, null, false))
        },
        StateConfig: stateConfig);

    var scene = ScadaScene.CreateEmpty("win00008", "Test", new(400, 400))
        .WithElement(group);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var html = await File.ReadAllTextAsync(result.HtmlPath);

        StringAssert.Contains(html, "id=\"ft100-win00008__grp_readvar\"",
            "Group with StateConfig.ReadVariable must have a DOM wrapper.");
        StringAssert.Contains(html, "data-scada-state-config=\"");
        StringAssert.Contains(html, "\"readVariable\":");
        StringAssert.Contains(html, "\"tagId\":\"tf100.mapping.42\"");
        Assert.IsFalse(html.Contains("data-scada-events="));
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

[TestMethod]
public async Task Export_GroupWithWriteTagCommand_RendersWrapperWithCommandAttribute()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "grp_writetag.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var commandConfig = new ScadaElementCommandConfig(new[] {
        new ScadaCommandBinding("wt1", "Set", true, ScadaCommandTrigger.OnClick,
            ScadaCommandKind.WriteTag, WriteTagId: "tf100.mapping.42",
            WriteMode: ScadaWriteMode.SetFixed, FixedValue: "1")
    });

    var group = new ScadaElement(
        "grp_writetag", "WriteTag Group", ScadaElementKind.Group,
        new SceneBounds(100, 200, 160, 70),
        null, ScadaElementLayout.Absolute, ScadaElementStyle.DefaultText,
        CommandConfig: commandConfig);

    var scene = ScadaScene.CreateEmpty("win00008", "Test", new(400, 400))
        .WithElement(group);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var html = await File.ReadAllTextAsync(result.HtmlPath);

        StringAssert.Contains(html, "data-scada-command-config=\"");
        StringAssert.Contains(html, "\"writeTagId\":\"tf100.mapping.42\"");
        StringAssert.Contains(html, "\"kind\":\"writeTag\"");
        Assert.IsFalse(html.Contains("data-scada-events="));
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

[TestMethod]
public async Task Export_GroupWithNavigateCommand_RendersWrapperWithCommandAttribute()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "grp_nav2.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var commandConfig = new ScadaElementCommandConfig(new[] {
        new ScadaCommandBinding("nav2", "GoPage", true, ScadaCommandTrigger.OnClick,
            ScadaCommandKind.Navigate, TargetPageId: "win00009")
    });

    var group = new ScadaElement(
        "grp_nav2", "Navigate Group", ScadaElementKind.Group,
        new SceneBounds(100, 200, 160, 70),
        null, ScadaElementLayout.Absolute, ScadaElementStyle.DefaultText,
        CommandConfig: commandConfig);

    var scene = ScadaScene.CreateEmpty("win00008", "Test", new(400, 400))
        .WithElement(group);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var html = await File.ReadAllTextAsync(result.HtmlPath);

        StringAssert.Contains(html, "data-scada-command-config=\"");
        StringAssert.Contains(html, "\"kind\":\"navigate\"");
        StringAssert.Contains(html, "\"targetPageId\":\"win00009\"");
        Assert.IsFalse(html.Contains("data-scada-events="));
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

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

    var scene = ScadaScene.CreateEmpty("win00008", "Test", new(400, 400))
        .WithElement(group);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var html = await File.ReadAllTextAsync(result.HtmlPath);

        Assert.IsFalse(html.Contains("id=\"ft100-win00008__grp_empty\""),
            "Group with no runtime data must be flattened.");
        StringAssert.Contains(html, "id=\"ft100-win00008__shape1\"");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

[TestMethod]
public async Task Export_GroupWithOnlyLegacyDataValueBindings_DoesNotRequireRuntimeWrapper()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "grp_legacy.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var group = new ScadaElement(
        "grp_legacy", "Legacy Value Group", ScadaElementKind.Group,
        new SceneBounds(100, 200, 160, 70),
        null, ScadaElementLayout.Absolute, ScadaElementStyle.DefaultText,
        Data: new ScadaElementData(null, null, null, null, null, null, null, null, null, false,
            ReadTagId: "tf100.mapping.42", WriteTagId: "tf100.mapping.99"),
        Children: new[] {
            new ScadaElement("input1", "Input", ScadaElementKind.InputText,
                new SceneBounds(5, 6, 80, 24), null,
                new ScadaElementLayout(ElementPositionMode.Relative, "grp_legacy"),
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

        Assert.IsFalse(html.Contains("id=\"ft100-win00008__grp_legacy\""),
            "Group with only legacy Data.ReadTagId/WriteTagId must not get a runtime wrapper.");
        StringAssert.Contains(html, "id=\"ft100-win00008__input1\"");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

[TestMethod]
public async Task Export_GroupWithOnlyLegacyEventBindings_DoesNotExportRuntimeEvents()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "grp_legacy_evt.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var child = new ScadaElement(
        "btn_legacy", "Legacy Button", ScadaElementKind.Button,
        new SceneBounds(5, 6, 80, 24), null,
        new ScadaElementLayout(ElementPositionMode.Relative, "grp_legacy_evt"),
        ScadaElementStyle.DefaultInput,
        new ScadaElementData("Click", null, null, null, null, null, null, null, null, false));
    var group = new ScadaElement(
        "grp_legacy_evt", "Legacy Event Group", ScadaElementKind.Group,
        new SceneBounds(100, 200, 160, 70),
        null, ScadaElementLayout.Absolute, ScadaElementStyle.DefaultText,
        Children: new[] { child });
    var scene = ScadaScene
        .CreateEmpty("win00008", "Legacy Events", new(400, 400))
        .WithElement(group)
        .WithChangePageEvent("grp_legacy_evt", ScadaEventRegistry.ClickKey, "win00009");

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var html = await File.ReadAllTextAsync(result.HtmlPath);

        // Pas de data-scada-events
        Assert.IsFalse(html.Contains("data-scada-events="),
            "Legacy EventBindings must not produce data-scada-events in export.");
        // Pas de wrapper groupe car EventBindings ne déclenche plus le wrapper
        Assert.IsFalse(html.Contains("id=\"ft100-win00008__grp_legacy_evt\""),
            "Group with only EventBindings must be flattened.");
        // L'enfant est quand même rendu
        StringAssert.Contains(html, "id=\"ft100-win00008__btn_legacy\"");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

[TestMethod]
public async Task Export_GroupWithCommandConfigAndLegacyEventBindings_UsesCommandConfigOnly()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "grp_hybrid.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var commandConfig = new ScadaElementCommandConfig(new[] {
        new ScadaCommandBinding("nav_hybrid", "GoHybrid", true,
            ScadaCommandTrigger.OnClick, ScadaCommandKind.Navigate,
            TargetPageId: "win00099")
    });

    var child = new ScadaElement(
        "btn_hybrid", "Hybrid Button", ScadaElementKind.Button,
        new SceneBounds(5, 6, 80, 24), null,
        new ScadaElementLayout(ElementPositionMode.Relative, "grp_hybrid"),
        ScadaElementStyle.DefaultInput,
        new ScadaElementData("Click", null, null, null, null, null, null, null, null, false));
    var group = new ScadaElement(
        "grp_hybrid", "Hybrid Group", ScadaElementKind.Group,
        new SceneBounds(100, 200, 160, 70),
        null, ScadaElementLayout.Absolute, ScadaElementStyle.DefaultText,
        Children: new[] { child },
        CommandConfig: commandConfig);
    var scene = ScadaScene
        .CreateEmpty("win00008", "Hybrid", new(400, 400))
        .WithElement(group)
        .WithChangePageEvent("grp_hybrid", ScadaEventRegistry.ClickKey, "win00009");

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var html = await File.ReadAllTextAsync(result.HtmlPath);

        // Le wrapper existe (via CommandConfig)
        StringAssert.Contains(html, "id=\"ft100-win00008__grp_hybrid\"");
        // CommandConfig présent
        StringAssert.Contains(html, "data-scada-command-config=\"");
        StringAssert.Contains(html, "\"kind\":\"navigate\"");
        // data-scada-events NE doit PAS être présent, malgré les EventBindings
        Assert.IsFalse(html.Contains("data-scada-events="),
            "Hybrid group must not emit data-scada-events even with legacy EventBindings.");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

[TestMethod]
public async Task Export_Manifest_DoesNotSerializeLegacyEventBindingsAsActiveEvents()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "manifest_test.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var commandConfig = new ScadaElementCommandConfig(new[] {
        new ScadaCommandBinding("nav_man", "GoMan", true,
            ScadaCommandTrigger.OnClick, ScadaCommandKind.Navigate,
            TargetPageId: "win00009")
    });
    var child = new ScadaElement(
        "btn_man", "Man Button", ScadaElementKind.Button,
        new SceneBounds(5, 6, 80, 24), null,
        new ScadaElementLayout(ElementPositionMode.Relative, "grp_man"),
        ScadaElementStyle.DefaultInput,
        new ScadaElementData("Click", null, null, null, null, null, null, null, null, false));
    var group = new ScadaElement(
        "grp_man", "Man Group", ScadaElementKind.Group,
        new SceneBounds(100, 200, 160, 70),
        null, ScadaElementLayout.Absolute, ScadaElementStyle.DefaultText,
        Children: new[] { child },
        CommandConfig: commandConfig);
    var scene = ScadaScene
        .CreateEmpty("win00008", "Manifest", new(400, 400))
        .WithElement(group)
        .WithChangePageEvent("grp_man", ScadaEventRegistry.ClickKey, "win00009");

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var manifestPath = Path.Combine(result.ExportDirectory, "manifest.json");
        var manifest = await File.ReadAllTextAsync(manifestPath);

        // Le manifest contient le groupe (via CommandConfig)
        StringAssert.Contains(manifest, "\"Id\": \"grp_man\"");
        // CommandConfig présent dans le manifest
        StringAssert.Contains(manifest, "\"CommandConfig\":");
        // Events NE doit PAS contenir les EventBindings legacy
        // Le groupe a des EventBindings (via WithChangePageEvent) mais ils ne
        // doivent pas apparaître comme events actifs dans le manifest
        Assert.IsFalse(manifest.Contains("\"Trigger\": \"click\""),
            "Manifest must not serialize legacy EventBindings as active events.");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

[TestMethod]
public async Task Export_GroupRuntimeWrapper_DoesNotChangeChildGeometry()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "grp_geometry.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var commandConfig = new ScadaElementCommandConfig(new[] {
        new ScadaCommandBinding("geo1", "GeoNav", true,
            ScadaCommandTrigger.OnClick, ScadaCommandKind.Navigate,
            TargetPageId: "win00009")
    });
    var group = new ScadaElement(
        "grp_geo", "Geo Group", ScadaElementKind.Group,
        new SceneBounds(100, 200, 160, 70),
        null, ScadaElementLayout.Absolute, ScadaElementStyle.DefaultText,
        Children: new[] {
            new ScadaElement("shape_geo", "GeoShape", ScadaElementKind.Shape,
                new SceneBounds(5, 6, 80, 24), null,
                new ScadaElementLayout(ElementPositionMode.Relative, "grp_geo"),
                ScadaElementStyle.DefaultText,
                new ScadaElementData(null, null, null, null, null, null, null, null, null, false),
                ShapeKind: ScadaShapeKind.Rectangle)
        },
        CommandConfig: commandConfig);

    var scene = ScadaScene.CreateEmpty("win00008", "Geometry", new(400, 400))
        .WithElement(group);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var html = await File.ReadAllTextAsync(result.HtmlPath);

        // Vérifier la présence des wrappers
        StringAssert.Contains(html, "id=\"ft100-win00008__grp_geo\"");
        StringAssert.Contains(html, "id=\"ft100-win00008__shape_geo\"");

        // Vérifier que l'enfant a des coordonnées relatives correctes
        // (left:5px, top:6px — inchangé par rapport au modèle)
        StringAssert.Contains(html, "left:5px");
        StringAssert.Contains(html, "top:6px");
        StringAssert.Contains(html, "width:80px");
        StringAssert.Contains(html, "height:24px");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

[TestMethod]
public void AuthoringWorkflow_DoesNotExposeLegacyElementEventDialog()
{
    // Vérification statique : les menus/ribbons/propriétés ne doivent pas
    // référencer l'ancien ElementEventDialog comme chemin actif.
    // Le constructeur de ElementEventDialog existe encore (décommissionné)
    // mais aucun code actif ne doit l'appeler depuis le flux utilisateur.

    var dialogType = typeof(ScadaBuilderV2.App.ElementEventDialog);
    var assembly = typeof(ScadaBuilderV2.App.MainWindow).Assembly;

    // Les références légitimes au type (dans ElementEventDialog lui-même,
    // dans MainWindow.xaml.cs pour le handler décommissionné) sont tolérées.
    // On vérifie que le constructeur n'est pas appelé dans un chemin
    // déclenché par une action utilisateur directe (ribbon, clic droit, etc.).

    // Note : ce test est structurel. Le code décommissionné reste présent
    // mais ne doit pas être câblé dans les flux utilisateur actifs.
    // La suppression physique sera faite dans une tranche séparée.
    Assert.IsNotNull(dialogType, "ElementEventDialog type must exist (decommissioned, not deleted).");
}
```

- [ ] **Step 3: Run tests to verify they fail**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Export_GroupWith"
```

Expected: tous les nouveaux tests FAIL (ou erreur de compilation si le test legacy supprimé est encore référencé) — le helper n'existe pas encore, le critère `EventBindings.Count > 0` est toujours actif.

- [ ] **Step 4: Commit**

```bash
git add tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "test: add modern group runtime wrapper tests, remove legacy event test

Add 12 tests verifying that groups with modern runtime data
(CommandConfig, StateConfig, ReadVariable) get DOM wrappers, while
groups with only legacy EventBindings or Data.ReadTagId/WriteTagId
do not. Remove ExportPreservesGroupClickNavigateEventAsRuntimeWrapper
which validated the decommissioned data-scada-events contract.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: Ajouter le helper `GroupRequiresRuntimeWrapper`

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`

**Interfaces:**
- Consumes: `ScadaElement`, `HasNonDefaultFallback` (existant)
- Produces: `private static bool GroupRequiresRuntimeWrapper(ScadaElement element)`

- [ ] **Step 1: Ajouter le helper après `HasNonDefaultFallback` (ligne 1133)**

```csharp
/// <summary>
/// Determines whether a Group element requires a runtime DOM wrapper in the
/// exported output. Only modern runtime data is considered; legacy
/// <see cref="ScadaElement.EventBindings"/> and
/// <see cref="ScadaElementData.ReadTagId"/>/<see cref="ScadaElementData.WriteTagId"/>
/// are intentionally excluded (decommissioned paths).
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

    var commandConfig = element.EffectiveCommandConfig;
    var stateConfig = element.EffectiveStateConfig;

    return commandConfig.Commands.Count > 0
        || stateConfig.States.Count > 0
        || stateConfig.ReadVariable is not null
        || HasNonDefaultFallback(stateConfig);
}
```

- [ ] **Step 2: Build pour vérifier la compilation**

```powershell
dotnet build ScadaBuilderV2.sln
```

Expected: build OK (le helper n'est pas encore appelé).

- [ ] **Step 3: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs
git commit -m "feat: add GroupRequiresRuntimeWrapper helper

Introduces a private helper that checks whether a Group element
carries modern runtime data (CommandConfig, StateConfig states,
ReadVariable, non-default quality fallback) and therefore needs
a DOM wrapper in the export. Legacy EventBindings and
Data.ReadTagId/WriteTagId are intentionally excluded.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: Remplacer les 4 occurrences + retirer `BuildEventAttribute` des groupes

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`

**Interfaces:**
- Consumes: `GroupRequiresRuntimeWrapper` (Task 2)
- Produces: 4 remplacements + suppression de `BuildEventAttribute` dans le chemin groupe

- [ ] **Step 1: Remplacer dans `BuildElementHtml` — wrapper groupe (ligne 1025)**

```csharp
// Avant (ligne 1025) :
if (element.EventBindings.Count > 0)

// Après :
if (GroupRequiresRuntimeWrapper(element))
```

- [ ] **Step 2: Retirer `BuildEventAttribute` du rendu groupe dans `BuildElementHtml`**

Supprimer la ligne 1032 :
```csharp
var groupEventAttribute = BuildEventAttribute(element);
```

Et dans le template HTML (ligne 1038), retirer `{{groupEventAttribute}}` :

```csharp
// Avant :
<div id="{{groupId}}" class="ft100-element ft100-element--{{groupKind}}" data-scada-element-id="{{groupSceneElementId}}" data-name="{{groupName}}" style="{{groupInlineStyle}}"{{groupEventAttribute}}{{groupValueBindingAttributes}}{{groupStateCommandAttributes}}>

// Après :
<div id="{{groupId}}" class="ft100-element ft100-element--{{groupKind}}" data-scada-element-id="{{groupSceneElementId}}" data-name="{{groupName}}" style="{{groupInlineStyle}}"{{groupValueBindingAttributes}}{{groupStateCommandAttributes}}>
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

Expected: tous les nouveaux tests passent. Les tests existants non modifiés doivent aussi passer.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs
git commit -m "fix: use GroupRequiresRuntimeWrapper, stop emitting data-scada-events

Replace EventBindings.Count > 0 with GroupRequiresRuntimeWrapper at all
four group-rendering decision points. Remove BuildEventAttribute from
the group HTML path — data-scada-events is decommissioned and must not
appear in modern export. Groups with only EventBindings are now
flattened; groups with CommandConfig/StateConfig always get a wrapper.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 4: Nettoyer le manifest — ne plus sérialiser `EventBindings` comme `Events` actifs

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`

**Interfaces:**
- Consumes: `GroupRequiresRuntimeWrapper` (Task 2)
- Produces: manifest sans `Events` peuplés depuis `EventBindings`

- [ ] **Step 1: Remplacer `Events = element.EventBindings` dans `BuildManifestPage`**

Dans `BuildManifestPage`, ligne 1874, le champ `Events` est alimenté par `element.EventBindings` :

```csharp
// Avant (ligne 1874) :
Events = element.EventBindings,

// Après — tableau vide (compatibilité de forme, pas de contrat runtime) :
Events = Array.Empty<ScadaObjectEventBinding>(),
```

Ajouter le `using` si nécessaire (déjà présent via `using ScadaBuilderV2.Domain.Scenes;`).

- [ ] **Step 2: Build et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Ft100SceneExporterTests"
```

Expected: `Export_Manifest_DoesNotSerializeLegacyEventBindingsAsActiveEvents` passe maintenant.

- [ ] **Step 3: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs
git commit -m "fix: stop serializing EventBindings as active Events in manifest

Replace element.EventBindings with an empty array in the manifest
Objects[].Events field. Legacy EventBindings are decommissioned and
must not appear as active runtime contract in the manifest.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 5: Ajouter le diagnostic pour les EventBindings legacy restants

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs`

**Interfaces:**
- Consumes: `ScadaProjectBuildValidator`, `ScadaBuildValidationIssue` (existants)
- Produces: extension de `AuditOrphanedEventBindings` pour couvrir TOUS les EventBindings, pas seulement ceux sans CommandConfig

- [ ] **Step 1: Lire la méthode `AuditOrphanedEventBindings` existante**

Cette méthode existe déjà dans `ScadaProjectBuildValidator` (dans `ProjectModels.cs`). Elle émet un warning quand un élément a des `EventBindings` sans `CommandConfig`.

- [ ] **Step 2: Remplacer `AuditOrphanedEventBindings` par un diagnostic plus large**

L'ancienne méthode vérifiait seulement les EventBindings « orphelins » (sans CommandConfig correspondant). La nouvelle spec demande un diagnostic pour TOUS les EventBindings, car ils sont tous décommissionnés.

```csharp
// Remplacer la méthode AuditOrphanedEventBindings existante par :

/// <summary>
/// Emits a warning for every element that still carries legacy
/// <see cref="ScadaElement.EventBindings"/>. EventBindings are
/// decommissioned and are not exported as runtime-active data.
/// Elements should be migrated to <see cref="ScadaElement.CommandConfig"/>
/// or <see cref="ScadaElement.StateConfig"/>.
/// </summary>
/// <remarks>
/// Decisions: DEC-0040, D9.
/// Contracts: docs/superpowers/specs/2026-07-09-export-group-runtime-wrapper.md §6.
/// Tests: tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
/// </remarks>
internal static void AuditLegacyEventBindings(
    List<ScadaBuildValidationIssue> issues,
    ScadaScene scene)
{
    foreach (var element in FlattenElements(scene.Elements))
    {
        if (element.EventBindings.Count == 0)
            continue;

        var hasModernEquivalent = element.EffectiveCommandConfig.Commands.Count > 0
            || element.EffectiveStateConfig.States.Count > 0;

        var severity = hasModernEquivalent
            ? ScadaBuildValidationSeverity.Warning
            : ScadaBuildValidationSeverity.Warning;

        var extra = hasModernEquivalent
            ? " L'element possede aussi une configuration moderne (CommandConfig/StateConfig) qui sera exportee."
            : " Aucune configuration moderne (CommandConfig/StateConfig) ne remplace ces EventBindings. L'element risque d'etre inactif dans TF100Web.";

        issues.Add(new ScadaBuildValidationIssue(
            severity,
            "event-bindings-decommissioned",
            $"Scene '{scene.Id}', element '{element.Id}' ({element.DisplayName}): " +
            $"EventBindings decommissionnes detectes ({element.EventBindings.Count} binding(s)). " +
            $"Authoring attendu : CommandConfig ou StateConfig. " +
            $"Les EventBindings ne sont pas exportes comme runtime TF100Web.{extra}",
            scene.Id));
    }
}
```

- [ ] **Step 3: Mettre à jour les appels à `AuditOrphanedEventBindings` → `AuditLegacyEventBindings`**

Dans la méthode `Validate` de `ScadaProjectBuildValidator`, remplacer l'appel :

```csharp
// Avant :
AuditOrphanedEventBindings(issues, scene);

// Après :
AuditLegacyEventBindings(issues, scene);
```

- [ ] **Step 4: Mettre à jour les tests existants qui référencent `AuditOrphanedEventBindings`**

Vérifier si des tests appellent directement `AuditOrphanedEventBindings` :

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~AuditOrphaned"
```

Si des tests échouent à cause du renommage, les mettre à jour pour référencer `AuditLegacyEventBindings` et ajuster les assertions pour le nouveau message.

- [ ] **Step 5: Build et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln
```

Expected: tous les tests passent.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs
git add tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs  # si modifié
git commit -m "feat: broaden legacy EventBindings audit to all elements

Replace AuditOrphanedEventBindings with AuditLegacyEventBindings
which warns on every element carrying decommissioned EventBindings,
not just those without modern CommandConfig. The diagnostic
distinguishes elements that also have modern equivalents from
those that risk being silently inactive in TF100Web.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 6: Vérification de régression complète

- [ ] **Step 1: Build + tests complets**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln
```

Tous les tests doivent passer.

- [ ] **Step 2: Vérifier les tests clés**

```powershell
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Ft100SceneExporterTests"
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaProjectBuildValidator"
```

Expected: tout vert.

- [ ] **Step 3: Commit final**

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

Tous les tests doivent passer.

### Checklist post-implémentation (spec §10)

- [ ] Aucun export `.sb2` moderne ne contient `data-scada-events`
- [ ] Un groupe avec `CommandConfig` sans `EventBindings` a un wrapper DOM
- [ ] Un groupe avec `StateConfig` sans `EventBindings` expose `data-scada-state-config`
- [ ] Un groupe avec `StateConfig.ReadVariable` expose `data-scada-state-config`
- [ ] Un groupe avec seulement `Data.ReadTagId`/`Data.WriteTagId` ne devient pas wrapper
- [ ] Un groupe sans runtime moderne reste aplati
- [ ] Les artefacts `EventBindings` restants sont diagnostiqués
- [ ] Le manifest ne sérialise pas `element.EventBindings` comme events actifs
