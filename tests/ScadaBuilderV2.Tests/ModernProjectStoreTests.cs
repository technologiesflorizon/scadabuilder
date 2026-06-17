using ScadaBuilderV2.Application.Conversion;
using ScadaBuilderV2.Application.Selection;
using ScadaBuilderV2.Domain.Editor;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ModernProjectStoreTests
{
    [TestMethod]
    public async Task SaveAndLoadScenePreservesModernInputs()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var store = new ModernProjectStore();
        var scene = ScadaScene
            .CreateEmpty("win-test", "Test scene", new(1280, 873))
            .WithBackgroundColor("#2090A0")
            .WithLegacyTextOverride("legacy:texte-001", "Température entrée été")
            .WithElement(ScadaElement.CreateInputText("input_text_001", "InputText001", 10, 20))
            .WithElement(ScadaElement.CreateInputNumeric("input_numeric_001", "InputNumeric001", 30, 40))
            .WithValueBinding("input_numeric_001", readTagId: "tf100.mapping.41")
            .WithValueBinding("input_numeric_001", writeTagId: "tf100.mapping.42");

        try
        {
            await store.SaveSceneAsync(root, scene);

            var loaded = await store.LoadOrCreateSceneAsync(root, "win-test", "Test scene", new(1280, 873));

            Assert.AreEqual(2, loaded.Elements.Count);
            Assert.AreEqual(ScadaElementKind.InputText, loaded.Elements[0].Kind);
            Assert.AreEqual(ScadaElementKind.InputNumeric, loaded.Elements[1].Kind);
            Assert.AreEqual("Texte", loaded.Elements[0].Data?.Placeholder);
            Assert.AreEqual("0", loaded.Elements[1].Data?.DisplayFormat);
            Assert.AreEqual("tf100.mapping.41", loaded.Elements[1].Data?.ReadTagId);
            Assert.AreEqual("tf100.mapping.42", loaded.Elements[1].Data?.WriteTagId);
            Assert.AreEqual("#2090A0", loaded.BackgroundColor);
            Assert.AreEqual("Température entrée été", loaded.TextOverrides.Single().Text);
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
    public async Task SaveAndLoadScenePreservesConditionalObjectVisibilityAction()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var store = new ModernProjectStore();
        var scene = ScadaScene
            .CreateEmpty("win-events", "Event scene", new(1280, 873))
            .WithElement(ScadaElement.CreateText("btn_show", "Afficher", 10, 20))
            .WithElement(ScadaElement.CreateText("pump_status", "Pompe", 100, 20))
            .WithObjectVisibilityEvent(
                "btn_show",
                ScadaEventRegistry.ClickKey,
                ScadaActionKind.Show,
                "pump_status",
                new ScadaActionCondition("tf100.mapping.running", ScadaConditionOperator.True));

        try
        {
            await store.SaveSceneAsync(root, scene);

            var loaded = await store.LoadOrCreateSceneAsync(root, "win-events", "Event scene", new(1280, 873));
            var loadedAction = loaded.ActionDefinitions.Single();

            Assert.AreEqual(ScadaActionKind.Show, loadedAction.Kind);
            Assert.AreEqual("pump_status", loadedAction.TargetElementId);
            Assert.AreEqual("tf100.mapping.running", loadedAction.Condition?.TagId);
            Assert.AreEqual(ScadaConditionOperator.True, loadedAction.Condition?.Operator);
            Assert.AreEqual(loadedAction.Id, loaded.FindElementRecursive("btn_show")?.EventBindings.Single().ActionId);
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
    public async Task SaveAndReloadPreservesText22NumericConversionAndHidesLegacySource()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var store = new ModernProjectStore();
        var text22 = new LegacyDetectedObject(
            "784",
            "Text22",
            "Text",
            "###.0",
            true,
            new SceneBounds(80, 57, 45, 24),
            new LegacyObjectStyle("Arial", 16, "rgba(0,0,0,1.000)", "transparent"));
        var converted = ElementPlusLegacyConverter.Convert(
            text22,
            ElementPlusConversionTarget.NumericReadOnly,
            new ElementPlusConversionOptions(
                "elementplus_numeric_display_784",
                "Element+ Text22",
                "Wonderware/ArchestrA",
                "win00008",
                "references/backups/AMR_REF_SCADA_backup_20260529_114507/pages/win00008.json"));
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithLegacyTextOverride("784", "###.0")
            .WithLegacyTextOverride("999", "Autre texte legacy non converti")
            .WithCommittedElementPlusConversion(converted);

        try
        {
            await store.SaveSceneAsync(root, scene);

            var loaded = await store.LoadOrCreateSceneAsync(root, "win00008", "win00008", new(1280, 873));
            await store.SaveSceneAsync(root, loaded);
            var reopened = await store.LoadOrCreateSceneAsync(root, "win00008", "win00008", new(1280, 873));

            var element = reopened.Elements.Single(item => item.Id == "elementplus_numeric_display_784");
            Assert.AreEqual(ScadaElementKind.InputNumeric, element.Kind);
            Assert.AreEqual("784", element.LegacySource?.SourceElementId);
            Assert.AreEqual("Text22", element.LegacySource?.SourceElementName);
            Assert.AreEqual("###.0", element.Data?.DisplayFormat);
            Assert.IsTrue(element.Data?.IsReadOnly == true);
            Assert.IsTrue(reopened.GetConvertedLegacySourceElementIds().Contains("784"));
            Assert.IsFalse(reopened.TextOverrides.Any(item => item.SourceElementId == "784"));
            Assert.IsTrue(reopened.TextOverrides.Any(item => item.SourceElementId == "999"));

            var savedScenePath = Path.Combine(
                root,
                "SCADA_BUILDER_V2",
                "projects",
                "AMR_REF_SCADA_V2",
                "scenes",
                "win00008.scene.json");
            var savedJson = await File.ReadAllTextAsync(savedScenePath);
            Assert.IsFalse(savedJson.Contains("\"TextOverrides\"", StringComparison.Ordinal));
            Assert.IsFalse(savedJson.Contains("\"HasPositiveSize\"", StringComparison.Ordinal));
            Assert.IsFalse(savedJson.Contains("\"IsImportedFromLegacy\"", StringComparison.Ordinal));

            var inventory = SceneElementInventorySnapshot.FromElements(
                new[]
                {
                    new LegacyElementListItem("784", "Text22", "Text", 80, 57, 45, 24, "###.0", true)
                },
                reopened.Elements.Select(item => new SceneElementListItem(
                    $"object:{item.Id}",
                    item.Id,
                    $"{item.DisplayName} [{item.Kind}]",
                    item.Kind.ToString(),
                    "Object")),
                Array.Empty<string>(),
                "elementplus_numeric_display_784",
                reopened.GetConvertedLegacySourceElementIds());

            Assert.IsFalse(inventory.Elements.Any(item => item.Key == "source:784"));
            Assert.IsTrue(inventory.Elements.Any(item => item.Key == "object:elementplus_numeric_display_784"));
            Assert.IsTrue(inventory.SelectedKeys.Contains("object:elementplus_numeric_display_784"));
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
    public async Task SaveAndReloadPreservesRealLegacyDeletion()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var store = new ModernProjectStore();
        var legacyText22 = ScadaElement.CreateLegacyStatic(
            "legacy_784",
            "Text22",
            new SceneBounds(80, 57, 45, 24),
            new LegacySourceTrace("Wonderware/ArchestrA", "win00008", "784", "Text22", "win00008.html"),
            new LegacyElementPayload("Text", "###.0", true, "Arial", 16, "#FFFFFF", "Transparent", "<text>###.0</text>", "{}"));
        var legacyText23 = ScadaElement.CreateLegacyStatic(
            "legacy_785",
            "Text23",
            new SceneBounds(90, 67, 45, 24),
            new LegacySourceTrace("Wonderware/ArchestrA", "win00008", "785", "Text23", "win00008.html"),
            new LegacyElementPayload("Text", "####", true, "Arial", 16, "#FFFFFF", "Transparent", "<text>####</text>", "{}"));
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(legacyText22)
            .WithElement(legacyText23)
            .WithLegacyElementsMaterialized();

        try
        {
            await store.SaveSceneAsync(root, scene
                .WithoutSceneObjects(["legacy_784"])
                .WithRemovedSourceElementIds(["784"]));

            var loaded = await store.LoadOrCreateSceneAsync(root, "win00008", "win00008", new(1280, 873));

            Assert.IsTrue(loaded.LegacyElementsMaterialized);
            Assert.IsNull(loaded.FindLegacyStaticBySourceElementId("784"));
            Assert.IsNotNull(loaded.FindLegacyStaticBySourceElementId("785"));
            Assert.AreEqual(1, loaded.GetLegacyStaticElements().Count);
            Assert.IsTrue(loaded.RemovedSourceIds.Contains("784"));
            Assert.IsTrue(loaded.GetSuppressedSourceElementIds().Contains("784"));
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
    public async Task SaveAndReloadPreservesPageManifestBackgroundAndObjectEvents()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var store = new ModernProjectStore();
        var button = new ScadaElement(
            "btn_next",
            "Bouton page suivante",
            ScadaElementKind.Button,
            new SceneBounds(100, 100, 160, 40),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultInput,
            new ScadaElementData("Suivant", null, null, null, null, null, null, null, null, false),
            ButtonBehavior: new ScadaButtonBehavior(
                false,
                new ScadaButtonHoverStyle(true, "#DFF3E7", "#0F2A30", "#90C030")));
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new(1440, 900))
            .WithPageType(ScadaPageType.Fragment)
            .WithBackground(new SceneBackgroundStyle(
                "#123456",
                "images/fond.png",
                "contain",
                "repeat-x",
                "left top",
                "fixed",
                "content-box",
                "padding-box",
                "multiply"))
            .WithAction(new ScadaActionDefinition(
                "action_nav_win00009",
                ScadaActionKind.Navigate,
                TargetPageId: "win00009"))
            .WithElement(button)
            .WithObjectEvent("btn_next", new ScadaObjectEventBinding("click", "action_nav_win00009"));

        try
        {
            await store.EnsureReferenceModernProjectAsync(
                root,
                [
                    new ScadaSceneReference("win00008", "win00008", "scenes/win00008.scene.json"),
                    new ScadaSceneReference("win00009", "win00009", "scenes/win00009.scene.json")
                ]);

            await store.SaveSceneAsync(root, scene);

            var loaded = await store.LoadOrCreateSceneAsync(root, "win00008", "win00008", new(1280, 873));
            Assert.AreEqual(ScadaPageType.Fragment, loaded.PageType);
            Assert.AreEqual(1440, loaded.CanvasSize.Width);
            Assert.AreEqual(900, loaded.CanvasSize.Height);
            Assert.AreEqual("#123456", loaded.EffectiveBackground.Color);
            Assert.AreEqual("images/fond.png", loaded.EffectiveBackground.Image);
            Assert.AreEqual("contain", loaded.EffectiveBackground.Size);
            Assert.AreEqual("action_nav_win00009", loaded.Elements.Single().EventBindings.Single().ActionId);
            Assert.AreEqual("#DFF3E7", loaded.Elements.Single().EffectiveButtonBehavior.EffectiveHover.Background);
            Assert.AreEqual("#90C030", loaded.Elements.Single().EffectiveButtonBehavior.EffectiveHover.BorderColor);
            Assert.AreEqual(ScadaActionKind.Navigate, loaded.ActionDefinitions.Single().Kind);

            var projectJson = await File.ReadAllTextAsync(Path.Combine(
                root,
                "SCADA_BUILDER_V2",
                "projects",
                "AMR_REF_SCADA_V2",
                "project.json"));
            StringAssert.Contains(projectJson, "\"ManifestVersion\": \"2.0\"");
            StringAssert.Contains(projectJson, "\"Type\": \"Fragment\"");
            StringAssert.Contains(projectJson, "\"Width\": 1440");
            StringAssert.Contains(projectJson, "\"Color\": \"#123456\"");
            Assert.IsFalse(projectJson.Contains("\"Events\"", StringComparison.Ordinal));
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
    public async Task SaveAndReloadPreservesPageCompositionAndHomePage()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var store = new ModernProjectStore();
        var scene = ScadaScene
            .CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithIncludeInBuild(true)
            .WithPageComposition("header_main", "footer_main");

        try
        {
            var project = await store.EnsureReferenceModernProjectAsync(
                root,
                [
                    new ScadaSceneReference("header_main", "Header", "scenes/header_main.scene.json", ScadaPageType.Header),
                    new ScadaSceneReference("footer_main", "Footer", "scenes/footer_main.scene.json", ScadaPageType.Footer),
                    new ScadaSceneReference("win00008", "win00008", "scenes/win00008.scene.json")
                ]);
            await store.SaveProjectAsync(root, project with { HomePageId = "win00008" });

            await store.SaveSceneAsync(root, scene);

            var loadedProject = await store.LoadProjectAsync(root);
            Assert.IsNotNull(loadedProject);
            Assert.AreEqual("win00008", loadedProject.HomePageId);
            Assert.AreEqual("win00008", loadedProject.EffectiveHomePageId);
            var page = loadedProject.Scenes.Single(item => item.Id == "win00008");
            Assert.IsTrue(page.IncludeInBuild);
            Assert.AreEqual("header_main", page.HeaderPageId);
            Assert.AreEqual("footer_main", page.FooterPageId);

            var loadedScene = await store.LoadOrCreateSceneAsync(root, "win00008", "win00008", new(1280, 873));
            Assert.IsTrue(loadedScene.IncludeInBuild);
            Assert.AreEqual("header_main", loadedScene.HeaderPageId);
            Assert.AreEqual("footer_main", loadedScene.FooterPageId);
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
    public async Task SaveAndReloadPreservesImportedTagCatalog()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var store = new ModernProjectStore();

        try
        {
            var project = await store.EnsureReferenceModernProjectAsync(
                root,
                [
                    new ScadaSceneReference("win00008", "win00008", "scenes/win00008.scene.json")
                ]);
            await store.SaveProjectAsync(root, project with
            {
                TagCatalog = new ScadaTagCatalog(
                    "tf100web-scada-tags-v1",
                    [
                        new ScadaTagDefinition(
                            "tf100.mapping.42",
                            "Pompe P-101 | PLC-1 | modbus://40001",
                            "Pompe P-101",
                            "analog",
                            "PLC-1",
                            "modbus",
                            "modbus://40001",
                            "Float",
                            Writeable: true)
                    ],
                    "tags.json")
            });

            var loaded = await store.LoadProjectAsync(root);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.TagCatalog?.Count);
            Assert.AreEqual("tf100.mapping.42", loaded.TagCatalog?.Tags.Single().Id);
            Assert.IsTrue(loaded.TagCatalog?.Tags.Single().Writeable ?? false);
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
    public async Task Tf100WebTagCatalogImporterReadsExportedTagSchema()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "tags.json");
        await File.WriteAllTextAsync(
            path,
            """
{
  "schema": "tf100web-scada-tags-v1",
  "count": 1,
  "tags": [
    {
      "id": "tf100.mapping.42",
      "keyword_label": "Pompe P-101",
      "keyword_type": "analog",
      "device": "PLC-1",
      "protocol": "modbus",
      "address_uri": "modbus://40001",
      "datatype_label": "Float",
      "writeable": true,
      "enabled": true,
      "unit": "bar"
    }
  ]
}
""");

        try
        {
            var catalog = await new Tf100WebTagCatalogImporter().ImportAsync(path);

            Assert.AreEqual("tf100web-scada-tags-v1", catalog.Schema);
            Assert.AreEqual(1, catalog.Count);
            var tag = catalog.Tags.Single();
            Assert.AreEqual("tf100.mapping.42", tag.Id);
            Assert.AreEqual("Pompe P-101", tag.DisplayName);
            Assert.AreEqual("Pompe P-101 | Float | PLC-1", tag.AuthoringLabel);
            Assert.AreEqual("Float", tag.Datatype);
            Assert.IsTrue(tag.Writeable);
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
    public void BuildValidationRejectsCompiledPageReferencingUncompiledHeader()
    {
        var project = ScadaProject.CreateDefault("test") with
        {
            HomePageId = "win00008",
            Scenes =
            [
                new ScadaSceneReference("header_main", "Header", "scenes/header_main.scene.json", ScadaPageType.Header, IncludeInBuild: false),
                new ScadaSceneReference("win00008", "win00008", "scenes/win00008.scene.json", HeaderPageId: "header_main")
            ]
        };

        var issues = ScadaProjectBuildValidator.Validate(project);

        Assert.IsTrue(issues.Any(issue => issue.Code == "build.header-not-compiled"));
    }
}
