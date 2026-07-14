using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class Ft100SceneExporterTests
{
    [TestMethod]
    public async Task ExportCreatesBrowserOpenableFolderWithRelativeCssAndImages()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var assetRoot = Path.Combine(sourceRoot, "assets");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(assetRoot);

        var imagePath = Path.Combine(assetRoot, "pump.png");
        await File.WriteAllBytesAsync(imagePath, [0x89, 0x50, 0x4E, 0x47]);
        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008_test.html");
        await File.WriteAllTextAsync(
            sourceHtmlPath,
            """
<!doctype html>
<html>
<head><style>.page { position: relative; }</style></head>
<body>
  <div class="wrap">
    <div class="page">
      <svg class="shape-layer" viewBox="0 0 1280 873"></svg>
      <img class="layer" data-id="legacy-image-001" src="assets/pump.png" />
      <div class="layer" data-id="784">####</div>
    </div>
  </div>
</body>
</html>
""");

        var scene = ScadaScene
            .CreateEmpty("win00008", "Condenseurs", new(1280, 873))
            .WithCommittedElementPlusConversion(new ScadaElement(
                "custom:pipe-001",
                "Pipe 001",
                ScadaElementKind.Custom,
                new SceneBounds(100, 110, 24, 80),
                new LegacySourceTrace("Wonderware", "win00008", "784", "Text22", null),
                ScadaElementLayout.Absolute,
                ScadaElementStyle.DefaultText,
                new ScadaElementData(
                    "<svg viewBox=\"0 0 24 80\"><rect width=\"24\" height=\"80\" /></svg>",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "Svg",
                    "pipe.sep",
                    false)));

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            Assert.IsTrue(File.Exists(result.HtmlPath));
            Assert.IsTrue(File.Exists(result.CssPath));
            // Image filenames now include content hash (e.g., pump.a1b2c3d.png).
            var pumpFiles = Directory.GetFiles(result.ImagesDirectory, "pump.*.png");
            Assert.AreEqual(1, pumpFiles.Length, "Pump image must exist with content hash");
            Assert.AreEqual(1, result.CopiedImageCount);
            Assert.AreEqual("win00008.html", Path.GetFileName(result.HtmlPath));
            Assert.IsFalse(File.Exists(Path.Combine(result.ExportDirectory, "index.html")));

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            StringAssert.Contains(html, "<link rel=\"stylesheet\" href=\"css/win00008.");
            StringAssert.Contains(html, ".css\">");
            StringAssert.Contains(html, "src=\"images/pump.");
            StringAssert.Contains(html, ".png\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__custom_pipe-001\"");
            StringAssert.Contains(html, "data-scada-element-id=\"custom:pipe-001\"");
            Assert.IsFalse(html.Contains("F:\\", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(html.Contains(sourceRoot, StringComparison.OrdinalIgnoreCase));

            var css = await File.ReadAllTextAsync(result.CssPath);
            StringAssert.Contains(html, "class=\"ft100-source-layer\"");
            StringAssert.Contains(css, "#ft100-win00008 .ft100-source-layer .shape-layer");
            StringAssert.Contains(css, "#ft100-win00008 .ft100-source-layer [data-id=\"784\"], #ft100-win00008 .ft100-legacy-layer [data-id=\"784\"] { display: none !important; }");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__custom_pipe-001");
            AssertExportCssHasNoGlobalRuntimeSelectors(css);
            Assert.IsFalse(
                css.Contains("\n[data-id=\"784\"] { display: none !important; }", StringComparison.Ordinal),
                "Suppressed source CSS must be scoped to the exported page root so composed header/body/footer pages cannot hide each other.");
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
    public async Task ExportAppliesEditedSourceElementBoundsByDataId()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00003_test.html");
        await File.WriteAllTextAsync(
            sourceHtmlPath,
            """
<!doctype html>
<html>
<body>
  <div class="page">
    <svg class="shape-layer" viewBox="0 0 1280 84" width="1280" height="84" xmlns="http://www.w3.org/2000/svg"><rect x="149" y="4" width="133" height="22" data-id="21" /></svg>
    <button class="layer" data-id="8" style="position:absolute; left:1px; top:2px; width:3px; height:4px; display:flex; font-family:'Microsoft Sans Serif', Arial, sans-serif;">Button8</button>
  </div>
</body>
</html>
""");

        var button = ScadaElement.CreateLegacyStatic(
            "source_button_8",
            "Button8",
            new SceneBounds(1144, 14, 138, 72),
            new LegacySourceTrace("Wonderware/ArchestrA", "win00003", "8", "Button8", "win00003.html"),
            new LegacyElementPayload(
                "Button",
                "",
                true,
                "\"Microsoft Sans Serif\", Arial, sans-serif",
                10,
                "rgb(211, 211, 211)",
                "rgb(211, 211, 211)",
                null,
                null));
        var rectangle = ScadaElement.CreateLegacyStatic(
            "source_rect_21",
            "Rectangle6",
            new SceneBounds(150, 5, 133, 22),
            new LegacySourceTrace("Wonderware/ArchestrA", "win00003", "21", "Rectangle6", "win00003.html"),
            new LegacyElementPayload(
                "rect",
                "",
                false,
                "\"Segoe UI\", Arial, sans-serif",
                16,
                "rgb(232, 237, 245)",
                "rgba(0, 0, 0, 0)",
                null,
                null));
        var scene = ScadaScene
            .CreateEmpty("win00003", "Navigation", new(1280, 120))
            .WithElement(button)
            .WithElement(rectangle)
            .WithLegacyElementsMaterialized();

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var css = await File.ReadAllTextAsync(result.CssPath);
            StringAssert.Contains(css, "#ft100-win00003 .ft100-source-layer [data-id=\"8\"], #ft100-win00003 .ft100-legacy-layer [data-id=\"8\"] {");
            StringAssert.Contains(css, "left: 1144px !important;");
            StringAssert.Contains(css, "top: 14px !important;");
            StringAssert.Contains(css, "width: 138px;");
            StringAssert.Contains(css, "height: 72px;");

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            StringAssert.Contains(html, "data-id=\"8\"");
            StringAssert.Contains(html, "display:flex;");
            StringAssert.Contains(html, "font-family:'Microsoft Sans Serif', Arial, sans-serif;");
            Assert.IsFalse(html.Contains("&#x27;Microsoft Sans Serif&#x27;", StringComparison.Ordinal));
            StringAssert.Contains(html, "left:1144px !important;");
            StringAssert.Contains(html, "top:14px !important;");
            StringAssert.Contains(html, "width:138px !important;");
            StringAssert.Contains(html, "height:72px !important;");
            StringAssert.Contains(html, "data-id=\"21\"");
            Assert.IsFalse(
                html.Contains("""data-id="21" style=""", StringComparison.Ordinal)
                    || html.Contains("""data-id="21"  style=""", StringComparison.Ordinal),
                "SVG source shapes must keep SVG geometry attributes instead of receiving HTML absolute-position inline style.");
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
    public async Task ExportKeepsLegacyStaticAsSourceProjectionOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00002_test.html");
        await File.WriteAllTextAsync(
            sourceHtmlPath,
            """
<!doctype html>
<html>
<body>
  <div class="page">
    <div class="layer" data-id="1">Rectangle1</div>
  </div>
</body>
</html>
""");

        var legacyStatic = ScadaElement.CreateLegacyStatic(
            "legacy_1",
            "Rectangle1",
            new SceneBounds(12, 34, 56, 78),
            new LegacySourceTrace("Wonderware/ArchestrA", "win00002", "1", "Rectangle1", "win00002.html"),
            new LegacyElementPayload(
                "Rectangle",
                "",
                true,
                "\"Microsoft Sans Serif\", Arial, sans-serif",
                10,
                "rgb(0, 0, 0)",
                "rgb(255, 255, 255)",
                null,
                null));
        var text = new ScadaElement(
            "modern_text_1",
            "Modern Text",
            ScadaElementKind.Text,
            new SceneBounds(100, 110, 120, 30),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData("Modern", null, null, null, null, null, null, null, null, false));
        var scene = ScadaScene
            .CreateEmpty("win00002", "Header", new(1280, 120))
            .WithElement(legacyStatic)
            .WithElement(text)
            .WithLegacyElementsMaterialized();

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            StringAssert.Contains(html, "data-id=\"1\"");
            StringAssert.Contains(html, "id=\"ft100-win00002__modern_text_1\"");
            StringAssert.Contains(html, "data-scada-element-id=\"modern_text_1\"");
            Assert.IsFalse(html.Contains("ft100-element--LegacyStatic", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("id=\"legacy_1\"", StringComparison.Ordinal));

            var css = await File.ReadAllTextAsync(result.CssPath);
            StringAssert.Contains(css, "#ft100-win00002 .ft100-source-layer [data-id=\"1\"], #ft100-win00002 .ft100-legacy-layer [data-id=\"1\"] {");
            StringAssert.Contains(css, "left: 12px !important;");
            StringAssert.Contains(css, "#ft100-win00002 #ft100-win00002__modern_text_1");
            AssertExportCssHasNoGlobalRuntimeSelectors(css);

            var manifest = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "manifest.json"));
            StringAssert.Contains(manifest, "\"Id\": \"modern_text_1\"");
            StringAssert.Contains(manifest, "\"Id\": \"legacy_1\"");
            StringAssert.Contains(manifest, "\"Kind\": \"LegacyStatic\"");
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
    public async Task ExportWritesInlineCriticalGeometryForFragmentComposition()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00002_test.html");
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

        var menu = ScadaElement.CreateText("elementplus_text_2", "Menu Principal", 535, 10) with
        {
            Bounds = new SceneBounds(535, 10, 162, 30)
        };
        var temperature = ScadaElement.CreateText("elementplus_text_9", "T° extérieure", 535, 51) with
        {
            Bounds = new SceneBounds(535, 51, 164, 24)
        };
        var numeric = new ScadaElement(
            "input_numeric_001",
            "Temperature exterieure",
            ScadaElementKind.InputNumeric,
            new SceneBounds(640, 47, 48, 31),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultInput,
            new ScadaElementData(null, null, null, null, null, null, null, null, null, false));
        var scene = ScadaScene
            .CreateEmpty("win00002", "Header", new(1280, 120))
            .WithPageType(ScadaPageType.Header)
            .WithElement(menu)
            .WithElement(temperature)
            .WithElement(numeric);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            StringAssert.Contains(html, "data-scada-page-type=\"header\"");
            StringAssert.Contains(html, "data-scada-width=\"1280\"");
            StringAssert.Contains(html, "data-scada-height=\"120\"");
            StringAssert.Contains(html, "style=\"position:relative;width:1280px;height:120px;");
            StringAssert.Contains(html, "id=\"ft100-win00002__elementplus_text_2\"");
            StringAssert.Contains(html, "data-scada-element-id=\"elementplus_text_2\"");
            StringAssert.Contains(html, "style=\"position:absolute;box-sizing:border-box;overflow:visible;pointer-events:auto;left:535px;top:10px;width:162px;height:30px;");
            StringAssert.Contains(html, "id=\"ft100-win00002__elementplus_text_9\"");
            StringAssert.Contains(html, "left:535px;top:51px;width:164px;height:24px;");
            StringAssert.Contains(html, "id=\"ft100-win00002__input_numeric_001\"");
            StringAssert.Contains(html, "left:640px;top:47px;width:48px;height:31px;");
            StringAssert.Contains(html, "style=\"width:100%;height:100%;box-sizing:border-box;\"");

            var readme = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "README.txt"));
            StringAssert.Contains(readme, "For fragment composition, inject the complete div with id ft100-win00002 into the target slot");
            StringAssert.Contains(readme, "css/win00002.css remains the complete runtime stylesheet");
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
    public async Task ExportAppliesLegacyTextOverridesAndRepairsFrenchTextEncoding()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008_text.html");
        await File.WriteAllTextAsync(
            sourceHtmlPath,
            """
<!doctype html>
<html>
<body>
  <div class="page">
    <div class="layer" data-id="101">TÃƒÂ©mperature ÃƒÂ©tÃƒÂ© Ã‚Â°C</div>
    <svg class="shape-layer">
      <text data-id="102">Ancien texte</text>
    </svg>
  </div>
</body>
</html>
""");

        var scene = ScadaScene
            .CreateEmpty("win00008", "Condenseurs", new(1280, 873))
            .WithLegacyTextOverride("102", "Pression évaporateur été °C");

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var browserText = System.Net.WebUtility.HtmlDecode(html);
            StringAssert.Contains(browserText, "Témperature été °C");
            StringAssert.Contains(browserText, "Pression évaporateur été °C");
            Assert.IsFalse(browserText.Contains("Ancien texte", StringComparison.Ordinal));
            Assert.IsFalse(browserText.Contains("Ã", StringComparison.Ordinal));
            Assert.IsFalse(browserText.Contains("Â", StringComparison.Ordinal));
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
    public async Task ExportOmitsRemovedSourceTextFromHtml()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00002_test.html");
        await File.WriteAllTextAsync(
            sourceHtmlPath,
            """
<!doctype html>
<html>
<body>
  <div class="page">
    <div class="layer" data-id="1">Metro-Richelieu</div>
    <div class="layer" data-id="2">Menu Principal</div>
    <div class="layer" data-id="5">Delisle Despaux ass.</div>
  </div>
</body>
</html>
""");

        var scene = ScadaScene
            .CreateEmpty("win00002", "Menu principal", new(1280, 120))
            .WithRemovedSourceElementIds(["1", "5"]);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            Assert.IsFalse(html.Contains("Metro-Richelieu", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("Delisle Despaux ass.", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("data-id=\"1\"", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("data-id=\"5\"", StringComparison.Ordinal));
            StringAssert.Contains(html, "Menu Principal");
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
    public async Task ExportOmitsRemovedSourceImageFromHtmlAndCopiedAssets()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var assetRoot = Path.Combine(sourceRoot, "assets");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(assetRoot);

        await File.WriteAllBytesAsync(Path.Combine(assetRoot, "removed.png"), [0x89, 0x50, 0x4E, 0x47]);
        await File.WriteAllBytesAsync(Path.Combine(assetRoot, "kept.png"), [0x89, 0x50, 0x4E, 0x47]);
        var sourceHtmlPath = Path.Combine(sourceRoot, "win00002_test.html");
        await File.WriteAllTextAsync(
            sourceHtmlPath,
            """
<!doctype html>
<html>
<body>
  <div class="page">
    <img class="layer" data-id="3" src="assets/removed.png" />
    <img class="layer" data-id="4" src="assets/kept.png" />
  </div>
</body>
</html>
""");

        var scene = ScadaScene
            .CreateEmpty("win00002", "Menu principal", new(1280, 873))
            .WithRemovedSourceElementIds(["3"]);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            Assert.IsFalse(html.Contains("removed.png", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("data-id=\"3\"", StringComparison.Ordinal));
            StringAssert.Contains(html, "kept.");
            StringAssert.Contains(html, ".png");
            Assert.IsFalse(File.Exists(Path.Combine(result.ImagesDirectory, "removed.png")));
            // Image filenames now include content hash (e.g., kept.a1b2c3d.png).
            var keptFiles = Directory.GetFiles(result.ImagesDirectory, "kept.*.png");
            Assert.AreEqual(1, keptFiles.Length, "Kept image must exist with content hash");
            Assert.AreEqual(1, result.CopiedImageCount);
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
    public async Task ExportWritesDjangoManifestAndObjectOwnedClickNavigateAction()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008_action.html");
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

        var button = new ScadaElement(
            "btn_next",
            "Page suivante",
            ScadaElementKind.Button,
            new SceneBounds(10, 20, 120, 30),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultInput,
            new ScadaElementData("Suivant", null, null, null, null, null, null, null, null, false),
            ButtonKind: ScadaButtonKind.Navigation);
        var scene = ScadaScene
            .CreateEmpty("win00008", "Navigation", new(1440, 900))
            .WithPageType(ScadaPageType.Default)
            .WithPageComposition("header_main", "footer_main")
            .WithBackground(new SceneBackgroundStyle(
                "#123456",
                "images/background.png",
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
        var project = ScadaProject.CreateDefault("Runtime") with
        {
            HomePageId = "win00008",
            Scenes =
            [
                new ScadaSceneReference("header_main", "Header", "scenes/header_main.scene.json", ScadaPageType.Header),
                new ScadaSceneReference("footer_main", "Footer", "scenes/footer_main.scene.json", ScadaPageType.Footer),
                new ScadaSceneReference(
                    "win00008",
                    "Navigation",
                    "scenes/win00008.scene.json",
                    ScadaPageType.Default,
                    new CanvasSize(1440, 900),
                    null,
                    true,
                    "header_main",
                    "footer_main"),
                new ScadaSceneReference("win00009", "Autre page", "scenes/win00009.scene.json")
            ]
        };

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot, project);
            var sceneDirectory = Path.GetDirectoryName(result.HtmlPath)!;
            var manifestPath = Path.Combine(sceneDirectory, "manifest.json");

            Assert.IsTrue(File.Exists(manifestPath));

            var css = await File.ReadAllTextAsync(result.CssPath);
            StringAssert.Contains(css, "#ft100-win00008.ft100-scada-scene {");
            StringAssert.Contains(css, "width: 1440px;");
            StringAssert.Contains(css, "height: 900px;");
            StringAssert.Contains(css, "background-color: #123456;");
            StringAssert.Contains(css, "background-size: contain;");
            StringAssert.Contains(css, "background-repeat: repeat-x;");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__btn_next:hover {");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__btn_next:active,");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__btn_next[data-scada-toggle-state=\"on\"] {");
            StringAssert.Contains(css, "#ft100-win00008 .ft100-element--Button, #ft100-win00008 [data-scada-command-config] { cursor: pointer; }");
            StringAssert.Contains(css, "#ft100-win00008 .ft100-element--Button *, #ft100-win00008 [data-scada-command-config] * { cursor: pointer; }");
            StringAssert.Contains(css, "#ft100-win00008 .ft100-element--Button:active, #ft100-win00008 [data-scada-command-config]:active { cursor: pointer; }");
            StringAssert.Contains(css, "background: #EAF5F7;");
            StringAssert.Contains(css, "color: #0F2A30;");
            StringAssert.Contains(css, "border-color: #2090A0;");
            AssertExportCssHasNoGlobalRuntimeSelectors(css);

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            Assert.IsFalse(html.Contains("data-scada-events="),
                "data-scada-events is decommissioned and must not appear in export.");
            Assert.IsFalse(html.Contains("action_nav_win00009"),
                "Legacy action IDs from EventBindings must not appear in HTML.");
            StringAssert.Contains(html, "<button type=\"button\"");
            StringAssert.Contains(html, "data-scada-button-kind=\"Navigation\"");
            StringAssert.Contains(html, "Suivant");
            AssertHtmlReferencesSharedRuntime(html, exportRoot);

            var manifest = await File.ReadAllTextAsync(manifestPath);
            StringAssert.Contains(manifest, "\"ManifestVersion\": \"2.1\"");
            StringAssert.Contains(manifest, "\"HomePageId\": \"win00008\"");
            StringAssert.Contains(manifest, "\"RelativePath\": \"win00008.html\"");
            StringAssert.Contains(manifest, "\"Type\": \"default\"");
            StringAssert.Contains(manifest, "\"IncludeInBuild\": true");
            StringAssert.Contains(manifest, "\"IsHome\": true");
            StringAssert.Contains(manifest, "\"HeaderPageId\": \"header_main\"");
            StringAssert.Contains(manifest, "\"FooterPageId\": \"footer_main\"");
            StringAssert.Contains(manifest, "\"Width\": 1440");
            StringAssert.Contains(manifest, "\"RequiredDisplayWidth\": 1440");
            StringAssert.Contains(manifest, "\"RequiredDisplayHeight\": 900");
            StringAssert.Contains(manifest, "\"Events\"");
            StringAssert.Contains(manifest, "\"ButtonKind\": \"Navigation\"");
            StringAssert.Contains(manifest, "\"ButtonBehavior\"");
            StringAssert.Contains(manifest, "\"IsDisabled\": false");
            StringAssert.Contains(manifest, "\"Background\": \"#EAF5F7\"");
            StringAssert.Contains(manifest, "\"BorderColor\": \"#2090A0\"");
            StringAssert.Contains(manifest, "\"Pressed\"");
            StringAssert.Contains(manifest, "\"Background\": \"#0F7280\"");
            Assert.IsFalse(manifest.Contains("\"Trigger\": \"click\""),
                "Manifest objects must not serialize legacy EventBindings Trigger.");
            Assert.IsFalse(manifest.Contains("\"ActionId\": \"action_"),
                "Manifest objects must not serialize legacy EventBindings ActionId.");
            StringAssert.Contains(manifest, "\"Kind\": \"navigate\"");
            StringAssert.Contains(manifest, "\"TargetPageId\": \"win00009\"");
            Assert.IsFalse(manifest.Contains("\"PageEvents\"", StringComparison.Ordinal));
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
    public async Task ExportWritesToggleButtonRuntimeStateOnWrapper()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008_toggle.html");
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

        var toggleButton = ScadaElement.CreateButton("btn_toggle", "Pompe", 10, 20, ScadaButtonKind.Toggle);
        var scene = ScadaScene
            .CreateEmpty("win00008", "Toggle", new(320, 240))
            .WithElement(toggleButton);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var css = await File.ReadAllTextAsync(result.CssPath);
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__btn_toggle[data-scada-toggle-state=\"on\"] {");

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            StringAssert.Contains(html, "id=\"ft100-win00008__btn_toggle\"");
            StringAssert.Contains(html, "data-scada-button-kind=\"Toggle\" data-scada-toggle-state=\"off\"");
            StringAssert.Contains(html, "<button type=\"button\" data-scada-button-kind=\"Toggle\"");
            Assert.IsFalse(
                html.Contains("<button type=\"button\" data-scada-button-kind=\"Toggle\" data-scada-toggle-state=", StringComparison.Ordinal),
                "The runtime toggle state belongs to the exported Element+ wrapper, not the inner button.");
            AssertHtmlReferencesSharedRuntime(html, exportRoot);
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
    public async Task ExportWritesStandardButtonActivationRuntimeEvents()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008_standard_buttons.html");
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

        var scene = ScadaScene
            .CreateEmpty("win00008", "Standard Buttons", new(640, 480))
            .WithElement(ScadaElement.CreateButton("btn_command", "Commande", 10, 20, ScadaButtonKind.Command))
            .WithElement(ScadaElement.CreateButton("btn_navigation", "Navigation", 10, 70, ScadaButtonKind.Navigation))
            .WithElement(ScadaElement.CreateButton("btn_ack", "Acquitter", 10, 120, ScadaButtonKind.AlarmAcknowledge))
            .WithElement(ScadaElement.CreateButton("btn_stop", "STOP", 10, 170, ScadaButtonKind.EmergencyStop));

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var manifest = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(result.HtmlPath)!, "manifest.json"));

            StringAssert.Contains(html, "data-scada-button-kind=\"Command\"");
            StringAssert.Contains(html, "data-scada-button-kind=\"Navigation\"");
            StringAssert.Contains(html, "data-scada-button-kind=\"AlarmAcknowledge\"");
            StringAssert.Contains(html, "data-scada-button-kind=\"EmergencyStop\"");
            AssertHtmlReferencesSharedRuntime(html, exportRoot);
            StringAssert.Contains(manifest, "\"ButtonKind\": \"Command\"");
            StringAssert.Contains(manifest, "\"ButtonKind\": \"Navigation\"");
            StringAssert.Contains(manifest, "\"ButtonKind\": \"AlarmAcknowledge\"");
            StringAssert.Contains(manifest, "\"ButtonKind\": \"EmergencyStop\"");
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
    public async Task ExportIncludesImportedTagsAndValueBindingRuntimeHook()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);
        var sourceHtmlPath = Path.Combine(sourceRoot, "value_binding.html");
        await File.WriteAllTextAsync(sourceHtmlPath, "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var baseInput = ScadaElement.CreateInputNumeric("input_sp", "Consigne", 10, 20);
        var input = baseInput with
        {
            Data = baseInput.Data! with
            {
                DisplayFormat = "fixed:1",
                IsReadOnly = false,
                Minimum = 10,
                Maximum = 99
            }
        };
        var baseDisplay = ScadaElement.CreateInputNumeric("display_temperature", "Temperature", 30, 60, isReadOnly: true);
        var display = baseDisplay with
        {
            Data = baseDisplay.Data! with
            {
                DisplayFormat = "###.#",
                Minimum = 10,
                Maximum = 99
            }
        };
        var scene = ScadaScene
            .CreateEmpty("win00008", "Value binding", new(1280, 873))
            .WithElement(input)
            .WithElement(display)
            .WithValueBinding("input_sp", readTagId: "tf100.mapping.41")
            .WithValueBinding("input_sp", writeTagId: "tf100.mapping.42")
            .WithValueBinding("display_temperature", readTagId: "tf100.mapping.41");
        var project = ScadaProject.CreateDefault("Runtime") with
        {
            Scenes = [new ScadaSceneReference("win00008", "Value binding", "scenes/win00008.scene.json")],
            TagCatalog = new ScadaTagCatalog(
                "tf100web-scada-tags-v1",
                [
                    new ScadaTagDefinition(
                        "tf100.mapping.41",
                        "Pression",
                        "Pression",
                        "analog",
                        "PLC-1",
                        "modbus",
                        "modbus://40001",
                        "Float",
                        Writeable: false),
                    new ScadaTagDefinition(
                        "tf100.mapping.42",
                        "Consigne",
                        "Consigne",
                        "analog",
                        "PLC-1",
                        "modbus",
                        "modbus://40002",
                        "Float",
                        Writeable: true)
                ])
        };

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot, project);
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var manifest = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(result.HtmlPath)!, "manifest.json"));

            StringAssert.Contains(html, "data-scada-read-tag=\"tf100.mapping.41\"");
            StringAssert.Contains(html, "data-scada-write-tag=\"tf100.mapping.42\"");
            AssertHtmlReferencesSharedRuntime(html, exportRoot);
            StringAssert.Contains(manifest, "\"ReadTagId\": \"tf100.mapping.41\"");
            StringAssert.Contains(manifest, "\"WriteTagId\": \"tf100.mapping.42\"");
            StringAssert.Contains(manifest, "\"DisplayFormat\": \"fixed:1\"");
            StringAssert.Contains(manifest, "\"DisplayFormat\": \"###.#\"");
            StringAssert.Contains(manifest, "\"IsReadOnly\": true");
            StringAssert.Contains(manifest, "\"Min\": 10");
            StringAssert.Contains(manifest, "\"Max\": 99");
            StringAssert.Contains(manifest, "\"Tags\"");
            StringAssert.Contains(manifest, "\"DisplayName\": \"Consigne\"");
            StringAssert.Contains(manifest, "\"Writeable\": true");
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
    public async Task ExportIncludesConditionalObjectVisibilityRuntimeHook()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);
        var sourceHtmlPath = Path.Combine(sourceRoot, "conditional_visibility.html");
        await File.WriteAllTextAsync(sourceHtmlPath, "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var button = ScadaElement.CreateText("btn_show", "Afficher", 10, 20);
        var target = ScadaElement.CreateText("pump_status", "Pompe", 100, 20);
        var scene = ScadaScene
            .CreateEmpty("win00008", "Conditional visibility", new(1280, 873))
            .WithElement(button)
            .WithElement(target)
            .WithObjectVisibilityEvent(
                "btn_show",
                ScadaEventRegistry.ClickKey,
                ScadaActionKind.Show,
                "pump_status",
                new ScadaActionCondition("tf100.mapping.running", ScadaConditionOperator.True));
        var project = ScadaProject.CreateDefault("Runtime") with
        {
            Scenes = [new ScadaSceneReference("win00008", "Conditional visibility", "scenes/win00008.scene.json")],
            TagCatalog = new ScadaTagCatalog(
                "tf100web-scada-tags-v1",
                [
                    new ScadaTagDefinition(
                        "tf100.mapping.running",
                        "Pompe en marche",
                        "Pompe en marche",
                        "digital",
                        "PLC-1",
                        "modbus",
                        "modbus://00001",
                        "Boolean")
                ])
        };

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot, project);
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var manifest = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(result.HtmlPath)!, "manifest.json"));

            Assert.IsFalse(html.Contains("data-scada-events="),
                "data-scada-events is decommissioned and must not appear in export.");
            AssertHtmlReferencesSharedRuntime(html, exportRoot);
            StringAssert.Contains(manifest, "\"Kind\": \"show\"");
            StringAssert.Contains(manifest, "\"TargetElementId\": \"pump_status\"");
            StringAssert.Contains(manifest, "\"Condition\"");
            StringAssert.Contains(manifest, "\"TagId\": \"tf100.mapping.running\"");
            StringAssert.Contains(manifest, "\"Operator\": \"true\"");
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
    public async Task ExportIncludesCompoundConditionRuntimeHook()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);
        var sourceHtmlPath = Path.Combine(sourceRoot, "compound_conditions.html");
        await File.WriteAllTextAsync(sourceHtmlPath, "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var scene = ScadaScene
            .CreateEmpty("win00008", "Compound conditions", new(1280, 873))
            .WithElement(ScadaElement.CreateText("btn_show", "Afficher", 10, 20))
            .WithElement(ScadaElement.CreateText("pump_status", "Pompe", 100, 20))
            .WithObjectVisibilityEvent(
                "btn_show",
                ScadaEventRegistry.ClickKey,
                ScadaActionKind.Show,
                "pump_status",
                conditionGroup: new ScadaActionConditionGroup(
                    [
                        new ScadaActionCondition("tf100.mapping.running", ScadaConditionOperator.True),
                        new ScadaActionCondition("tf100.mapping.pressure", ScadaConditionOperator.GreaterThan, "12.5")
                    ],
                    ScadaConditionGroupMode.Any,
                    ScadaMissingConditionPolicy.AllowAction));
        var project = ScadaProject.CreateDefault("Runtime") with
        {
            Scenes = [new ScadaSceneReference("win00008", "Compound conditions", "scenes/win00008.scene.json")],
            TagCatalog = new ScadaTagCatalog(
                "tf100web-scada-tags-v1",
                [
                    new ScadaTagDefinition("tf100.mapping.running", "Pompe en marche", Datatype: "Boolean", Device: "PLC-1"),
                    new ScadaTagDefinition("tf100.mapping.pressure", "Pression", Datatype: "Float", Device: "PLC-1")
                ])
        };

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot, project);
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var manifest = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(result.HtmlPath)!, "manifest.json"));

            AssertHtmlReferencesSharedRuntime(html, exportRoot);
            StringAssert.Contains(manifest, "\"ConditionGroup\"");
            StringAssert.Contains(manifest, "\"Mode\": \"any\"");
            StringAssert.Contains(manifest, "\"MissingTagPolicy\": \"allowAction\"");
            StringAssert.Contains(manifest, "\"TagId\": \"tf100.mapping.pressure\"");
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
    public async Task ExportIncludesRuntimeLifecycleBridge()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);
        var sourceHtmlPath = Path.Combine(sourceRoot, "lifecycle.html");
        await File.WriteAllTextAsync(sourceHtmlPath, "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var scene = ScadaScene
            .CreateEmpty("win00008", "Lifecycle", new(1280, 873))
            .WithElement(ScadaElement.CreateText("btn_next", "Suite", 10, 20))
            .WithChangePageEvent("btn_next", ScadaEventRegistry.ClickKey, "win00009");

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);
            var html = await File.ReadAllTextAsync(result.HtmlPath);

            AssertHtmlReferencesSharedRuntime(html, exportRoot);
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
    public async Task ExportIncludesOpenPopupRuntimeHook()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);
        var sourceHtmlPath = Path.Combine(sourceRoot, "open_popup.html");
        await File.WriteAllTextAsync(sourceHtmlPath, "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var button = ScadaElement.CreateText("btn_popup", "Details", 10, 20);
        var scene = ScadaScene
            .CreateEmpty("win00008", "Open popup", new(1280, 873))
            .WithElement(button)
            .WithOpenPopupEvent("btn_popup", ScadaEventRegistry.ClickKey, "popup_pump");
        var project = ScadaProject.CreateDefault("Runtime") with
        {
            Scenes =
            [
                new ScadaSceneReference("win00008", "Open popup", "scenes/win00008.scene.json"),
                new ScadaSceneReference("popup_pump", "Popup pompe", "scenes/popup_pump.scene.json", ScadaPageType.Fragment)
            ]
        };

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot, project);
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var manifest = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(result.HtmlPath)!, "manifest.json"));

            Assert.IsFalse(html.Contains("data-scada-events="),
                "data-scada-events is decommissioned and must not appear in export.");
            AssertHtmlReferencesSharedRuntime(html, exportRoot);
            StringAssert.Contains(manifest, "\"Kind\": \"mountFragment\"");
            StringAssert.Contains(manifest, "\"TargetPageId\": \"popup_pump\"");
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
    public async Task ExportIncludesCloseAndTogglePopupRuntimeHooks()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);
        var sourceHtmlPath = Path.Combine(sourceRoot, "close_toggle_popup.html");
        await File.WriteAllTextAsync(sourceHtmlPath, "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var scene = ScadaScene
            .CreateEmpty("win00008", "Close toggle popup", new(1280, 873))
            .WithElement(ScadaElement.CreateText("btn_close", "Fermer", 10, 20))
            .WithElement(ScadaElement.CreateText("btn_toggle", "Basculer", 10, 60))
            .WithClosePopupEvent("btn_close", ScadaEventRegistry.ClickKey, "popup_pump")
            .WithTogglePopupEvent("btn_toggle", ScadaEventRegistry.ClickKey, "popup_pump");
        var project = ScadaProject.CreateDefault("Runtime") with
        {
            Scenes =
            [
                new ScadaSceneReference("win00008", "Close toggle popup", "scenes/win00008.scene.json"),
                new ScadaSceneReference("popup_pump", "Popup pompe", "scenes/popup_pump.scene.json", ScadaPageType.Fragment)
            ]
        };

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot, project);
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var manifest = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(result.HtmlPath)!, "manifest.json"));

            AssertHtmlReferencesSharedRuntime(html, exportRoot);
            StringAssert.Contains(manifest, "\"Kind\": \"closePopup\"");
            StringAssert.Contains(manifest, "\"Kind\": \"togglePopup\"");
            StringAssert.Contains(manifest, "\"TargetPageId\": \"popup_pump\"");
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
    public async Task ExportIncludesAdvancedPopupRuntimeOptions()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);
        var sourceHtmlPath = Path.Combine(sourceRoot, "advanced_popup.html");
        await File.WriteAllTextAsync(sourceHtmlPath, "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var scene = ScadaScene
            .CreateEmpty("win00008", "Advanced popup", new(1280, 873))
            .WithElement(ScadaElement.CreateText("btn_popup", "Details", 10, 20))
            .WithElement(ScadaElement.CreateText("host_faceplate", "Host", 100, 20))
            .WithOpenPopupEvent(
                "btn_popup",
                ScadaEventRegistry.ClickKey,
                "popup_pump",
                new ScadaPopupOptions(
                    ScadaPopupPosition.HostRegion,
                    ScadaPopupSizePreset.Medium,
                    AllowMultiple: true,
                    ResetOnOpen: false,
                    HostRegionId: "host_faceplate"));
        var project = ScadaProject.CreateDefault("Runtime") with
        {
            Scenes =
            [
                new ScadaSceneReference("win00008", "Advanced popup", "scenes/win00008.scene.json"),
                new ScadaSceneReference("popup_pump", "Popup pompe", "scenes/popup_pump.scene.json", ScadaPageType.Fragment)
            ]
        };

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot, project);
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var manifest = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(result.HtmlPath)!, "manifest.json"));

            AssertHtmlReferencesSharedRuntime(html, exportRoot);
            StringAssert.Contains(manifest, "\"PopupOptions\"");
            StringAssert.Contains(manifest, "\"Position\": \"hostRegion\"");
            StringAssert.Contains(manifest, "\"SizePreset\": \"medium\"");
            StringAssert.Contains(manifest, "\"AllowMultiple\": true");
            StringAssert.Contains(manifest, "\"ResetOnOpen\": false");
            StringAssert.Contains(manifest, "\"HostRegionId\": \"host_faceplate\"");
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
    public async Task ExportWritesDisabledButtonRuntimeStateAndOmitsHoverCss()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);
        var sourceHtmlPath = Path.Combine(sourceRoot, "disabled_button.html");
        await File.WriteAllTextAsync(sourceHtmlPath, "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var button = new ScadaElement(
            "btn_disabled",
            "Desactive",
            ScadaElementKind.Button,
            new SceneBounds(10, 20, 120, 30),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultInput,
            new ScadaElementData("Stop", null, null, null, null, null, null, null, null, false),
            ButtonBehavior: new ScadaButtonBehavior(true, ScadaButtonHoverStyle.Default));
        var scene = ScadaScene
            .CreateEmpty("win00008", "Disabled", new(1440, 900))
            .WithElement(button);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);
            var css = await File.ReadAllTextAsync(result.CssPath);
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var manifest = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(result.HtmlPath)!, "manifest.json"));

            StringAssert.Contains(html, "<button type=\"button\"");
            StringAssert.Contains(html, "data-scada-disabled=\"true\" aria-disabled=\"true\"");
            StringAssert.Contains(html, "<button type=\"button\" data-scada-button-kind=\"Command\" disabled aria-disabled=\"true\"");
            AssertHtmlReferencesSharedRuntime(html, exportRoot);
            Assert.IsFalse(css.Contains("#ft100-win00008 #ft100-win00008__btn_disabled:hover", StringComparison.Ordinal));
            Assert.IsFalse(css.Contains("#ft100-win00008 #ft100-win00008__btn_disabled:active", StringComparison.Ordinal));
            StringAssert.Contains(css, "#ft100-win00008 .ft100-element--Button[data-scada-disabled=\"true\"], #ft100-win00008 .ft100-element--Button[data-scada-disabled=\"true\"] * { cursor: not-allowed; opacity: 0.62; }");
            StringAssert.Contains(manifest, "\"ButtonBehavior\"");
            StringAssert.Contains(manifest, "\"IsDisabled\": true");
            StringAssert.Contains(manifest, "\"Enabled\": true");
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
    public async Task ExportManifestReportsRequiredDisplaySizeFromExportedGeometry()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008_required_size.html");
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

        var nestedElement = ScadaElement.CreateText("text_nested", "Nested", 20, 50) with
        {
            Bounds = new SceneBounds(20, 50, 260, 90)
        };
        var group = new ScadaElement(
            "group_required",
            "Group required",
            ScadaElementKind.Group,
            new SceneBounds(1200, 800, 1, 1),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            null,
            [nestedElement]);
        var scene = ScadaScene
            .CreateEmpty("win00008", "Required size", new(1280, 873))
            .WithElement(group);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var manifest = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "manifest.json"));
            StringAssert.Contains(manifest, "\"Width\": 1280");
            StringAssert.Contains(manifest, "\"Height\": 873");
            StringAssert.Contains(manifest, "\"RequiredDisplayWidth\": 1480");
            StringAssert.Contains(manifest, "\"RequiredDisplayHeight\": 940");
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
    public async Task ExportedElementStyleComposesFlipScaleWithRotationTransform()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008_mirror.html");
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

        var input = ScadaElement.CreateInputText("input_mirror_001", "InputMirror001", 40, 50) with
        {
            Style = ScadaElementStyle.DefaultInput with
            {
                Rotation = 17,
                FlipHorizontally = true,
                FlipVertically = false
            }
        };
        var scene = ScadaScene
            .CreateEmpty("win00008", "Miroir", new(1280, 873))
            .WithElement(input);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            StringAssert.Contains(html, "transform:rotate(17deg) scaleX(-1) scaleY(1);");
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
    public async Task ExportRendersStandardShapeElementAsScopedSvg()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008_shapes.html");
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

        var arrow = ScadaElement.CreateShape("shape_arrow_001", "Fleche001", ScadaShapeKind.Arrow, 40, 50) with
        {
            Style = ScadaElementStyle.DefaultInput with
            {
                Background = "Transparent",
                BorderColor = "#90C030",
                BorderWidth = 3,
                BorderStyle = "Dashed",
                Opacity = 0.42,
                Rotation = 17
            },
            Data = new ScadaElementData(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                ShapeStartX: 7,
                ShapeStartY: 9,
                ShapeEndX: 124,
                ShapeEndY: 21)
        };
        var circle = ScadaElement.CreateShape("shape_circle_001", "Cercle001", ScadaShapeKind.Circle, 40, 120);
        var triangle = ScadaElement.CreateShape("shape_triangle_001", "Triangle001", ScadaShapeKind.Triangle, 40, 220);
        var star = ScadaElement.CreateShape("shape_star_001", "Etoile001", ScadaShapeKind.Star, 140, 220);
        var lamp = ScadaElement.CreateShape("shape_lamp_001", "Voyant001", ScadaShapeKind.IndicatorLamp, 80, 120);
        var bar = ScadaElement.CreateShape("shape_bar_001", "BarreHorizontale001", ScadaShapeKind.HorizontalBar, 140, 120) with
        {
            Data = new ScadaElementData(null, null, 42, 0, 100, null, null, "0", null, false)
        };
        var tank = ScadaElement.CreateShape("shape_tank_001", "Reservoir001", ScadaShapeKind.Tank, 220, 120) with
        {
            Data = new ScadaElementData(null, null, 75, 0, 100, null, null, "0", null, false)
        };
        var pipe = ScadaElement.CreateShape("shape_pipe_001", "TuyauHorizontal001", ScadaShapeKind.PipeHorizontal, 340, 150);
        var pipeVertical = ScadaElement.CreateShape("shape_pipe_v_001", "TuyauVertical001", ScadaShapeKind.PipeVertical, 460, 120);
        var valve = ScadaElement.CreateShape("shape_valve_001", "Vanne001", ScadaShapeKind.Valve, 520, 150);
        var pump = ScadaElement.CreateShape("shape_pump_001", "Pompe001", ScadaShapeKind.Pump, 640, 150);
        var motor = ScadaElement.CreateShape("shape_motor_001", "Moteur001", ScadaShapeKind.Motor, 760, 150);
        var fan = ScadaElement.CreateShape("shape_fan_001", "Ventilateur001", ScadaShapeKind.Fan, 880, 150);
        var conveyor = ScadaElement.CreateShape("shape_conveyor_001", "Convoyeur001", ScadaShapeKind.Conveyor, 1000, 150);
        var gauge = ScadaElement.CreateShape("shape_gauge_001", "Jauge001", ScadaShapeKind.Gauge, 220, 300) with
        {
            Data = new ScadaElementData(null, null, 55, 0, 100, null, null, "0", null, false)
        };
        var switchShape = ScadaElement.CreateShape("shape_switch_001", "Interrupteur001", ScadaShapeKind.Switch, 340, 300);
        var breaker = ScadaElement.CreateShape("shape_breaker_001", "Disjoncteur001", ScadaShapeKind.Breaker, 460, 300);
        var transformer = ScadaElement.CreateShape("shape_transformer_001", "Transformateur001", ScadaShapeKind.Transformer, 580, 300);
        var alarmBeacon = ScadaElement.CreateShape("shape_alarm_001", "BaliseAlarme001", ScadaShapeKind.AlarmBeacon, 720, 300);
        var scene = ScadaScene
            .CreateEmpty("win00008", "Formes", new(1280, 873))
            .WithElement(arrow)
            .WithElement(circle)
            .WithElement(triangle)
            .WithElement(star)
            .WithElement(lamp)
            .WithElement(bar)
            .WithElement(tank)
            .WithElement(pipe)
            .WithElement(pipeVertical)
            .WithElement(valve)
            .WithElement(pump)
            .WithElement(motor)
            .WithElement(fan)
            .WithElement(conveyor)
            .WithElement(gauge)
            .WithElement(switchShape)
            .WithElement(breaker)
            .WithElement(transformer)
            .WithElement(alarmBeacon);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_arrow_001\"");
            StringAssert.Contains(html, "data-scada-element-id=\"shape_arrow_001\"");
            StringAssert.Contains(html, "<svg id=\"ft100-win00008__shape_arrow_001__shape\"");
            StringAssert.Contains(html, "marker-end=\"url(#ft100-win00008__shape_arrow_001__arrow)\"");
            StringAssert.Contains(html, "x1=\"7\"");
            StringAssert.Contains(html, "y1=\"9\"");
            StringAssert.Contains(html, "x2=\"124\"");
            StringAssert.Contains(html, "y2=\"21\"");
            StringAssert.Contains(html, "stroke=\"#90C030\"");
            StringAssert.Contains(html, "stroke-dasharray=\"8 5\"");
            StringAssert.Contains(html, "opacity:0.42;");
            StringAssert.Contains(html, "transform:rotate(17deg) scaleX(1) scaleY(1);");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_circle_001\"");
            StringAssert.Contains(html, "<svg id=\"ft100-win00008__shape_circle_001__shape\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_triangle_001\"");
            StringAssert.Contains(html, "<svg id=\"ft100-win00008__shape_triangle_001__shape\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_star_001\"");
            StringAssert.Contains(html, "<svg id=\"ft100-win00008__shape_star_001__shape\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_lamp_001\"");
            StringAssert.Contains(html, "radialGradient id=\"ft100-win00008__shape_lamp_001__lamp-gradient\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_bar_001\"");
            StringAssert.Contains(html, "width=\"63.84\"");
            StringAssert.Contains(html, "height=\"24\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_tank_001\"");
            StringAssert.Contains(html, "<svg id=\"ft100-win00008__shape_tank_001__shape\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_pipe_001\"");
            StringAssert.Contains(html, "<svg id=\"ft100-win00008__shape_pipe_001__shape\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_pipe_v_001\"");
            StringAssert.Contains(html, "<svg id=\"ft100-win00008__shape_pipe_v_001__shape\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_valve_001\"");
            StringAssert.Contains(html, "<polygon points=");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_pump_001\"");
            StringAssert.Contains(html, "<circle cx=");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_motor_001\"");
            StringAssert.Contains(html, ">M</text>");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_fan_001\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_conveyor_001\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_gauge_001\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_switch_001\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_breaker_001\"");
            StringAssert.Contains(html, ">CB</text>");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_transformer_001\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_alarm_001\"");
            StringAssert.Contains(html, "Q 36 10.56 54.72 63.36");

            var css = await File.ReadAllTextAsync(result.CssPath);
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_arrow_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_circle_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_triangle_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_star_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_lamp_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_bar_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_tank_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_pipe_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_pipe_v_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_valve_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_pump_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_motor_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_fan_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_conveyor_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_gauge_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_switch_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_breaker_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_transformer_001");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__shape_alarm_001");
            StringAssert.Contains(css, "background: transparent;");
            StringAssert.Contains(css, "border: 0 none transparent;");
            AssertExportCssHasNoGlobalRuntimeSelectors(css);

            var manifest = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "manifest.json"));
            StringAssert.Contains(manifest, "\"Kind\": \"Shape\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"Arrow\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"Circle\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"Triangle\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"Star\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"IndicatorLamp\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"HorizontalBar\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"Tank\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"PipeHorizontal\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"PipeVertical\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"Valve\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"Pump\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"Motor\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"Fan\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"Conveyor\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"Gauge\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"Switch\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"Breaker\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"Transformer\"");
            StringAssert.Contains(manifest, "\"ShapeKind\": \"AlarmBeacon\"");
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
    public async Task ExportProjectWritesCompiledPagesAndAggregateManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        async Task<string> WriteSourceAsync(string pageId)
        {
            var path = Path.Combine(sourceRoot, $"{pageId}.html");
            await File.WriteAllTextAsync(
                path,
                $$"""
<!doctype html>
<html>
<body>
  <div class="page"><div class="layer" data-id="{{pageId}}-text">{{pageId}}</div></div>
</body>
</html>
""");
            return path;
        }

        var headerSource = await WriteSourceAsync("header_main");
        var footerSource = await WriteSourceAsync("footer_main");
        var homeSource = await WriteSourceAsync("win00008");

        var header = ScadaScene
            .CreateEmpty("header_main", "Header", new(1280, 80))
            .WithPageType(ScadaPageType.Header);
        var footer = ScadaScene
            .CreateEmpty("footer_main", "Footer", new(1280, 60))
            .WithPageType(ScadaPageType.Footer);
        var home = ScadaScene
            .CreateEmpty("win00008", "Home", new(1280, 873))
            .WithPageComposition("header_main", "footer_main");
        var skipped = ScadaScene
            .CreateEmpty("win00099", "Skipped", new(1280, 873))
            .WithIncludeInBuild(false);
        var project = ScadaProject.CreateDefault("Runtime") with
        {
            HomePageId = "win00008",
            Scenes =
            [
                new ScadaSceneReference("header_main", "Header", "scenes/header_main.scene.json", ScadaPageType.Header),
                new ScadaSceneReference("footer_main", "Footer", "scenes/footer_main.scene.json", ScadaPageType.Footer),
                new ScadaSceneReference(
                    "win00008",
                    "Home",
                    "scenes/win00008.scene.json",
                    HeaderPageId: "header_main",
                    FooterPageId: "footer_main"),
                new ScadaSceneReference("win00099", "Skipped", "scenes/win00099.scene.json", IncludeInBuild: false)
            ]
        };

        try
        {
            var stalePackageDirectory = Path.Combine(exportRoot, Ft100SceneExporter.ProjectPackageDirectoryName);
            Directory.CreateDirectory(stalePackageDirectory);
            await File.WriteAllTextAsync(Path.Combine(stalePackageDirectory, "stale.txt"), "old export");

            var result = await new Ft100SceneExporter().ExportProjectAsync(
                project,
                [
                    new Ft100ProjectPageExportInput(header, headerSource),
                    new Ft100ProjectPageExportInput(footer, footerSource),
                    new Ft100ProjectPageExportInput(home, homeSource),
                    new Ft100ProjectPageExportInput(skipped, homeSource)
                ],
                exportRoot);

            var packageDirectory = Path.Combine(exportRoot, Ft100SceneExporter.ProjectPackageDirectoryName);
            Assert.AreEqual(packageDirectory, result.ExportDirectory);
            Assert.AreEqual(3, result.PageResults.Count);
            Assert.IsTrue(File.Exists(Path.Combine(packageDirectory, "manifest.json")));
            Assert.IsTrue(File.Exists(Path.Combine(packageDirectory, "win00008", "win00008.html")));
            Assert.IsTrue(File.Exists(Path.Combine(packageDirectory, "header_main", "header_main.html")));
            Assert.IsTrue(File.Exists(Path.Combine(packageDirectory, "footer_main", "footer_main.html")));
            Assert.IsFalse(File.Exists(Path.Combine(packageDirectory, "stale.txt")));
            Assert.IsFalse(Directory.Exists(Path.Combine(packageDirectory, "win00099")));

            var manifest = await File.ReadAllTextAsync(result.ManifestPath);
            StringAssert.Contains(manifest, "\"Name\": \"Runtime\"");
            StringAssert.Contains(manifest, "\"HomePageId\": \"win00008\"");
            StringAssert.Contains(manifest, "\"RelativePath\": \"win00008/win00008.html\"");
            StringAssert.Contains(manifest, "\"RelativePath\": \"header_main/header_main.html\"");
            StringAssert.Contains(manifest, "\"HeaderPageId\": \"header_main\"");
            StringAssert.Contains(manifest, "\"FooterPageId\": \"footer_main\"");
            Assert.IsFalse(manifest.Contains("win00099", StringComparison.Ordinal));
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
    public async Task ExportProjectWritesNativePageWithoutImportedHtmlSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var pageKey = Guid.NewGuid();
        var page = new ScadaSceneReference(
            "legacy_alias",
            "Native",
            $"scenes/{pageKey:N}.scene.json",
            PageKey: pageKey,
            PageCode: "native_page",
            Origin: PageOrigin.Native);
        var project = ScadaProject.CreateDefault("Native") with
        {
            HomePageKey = pageKey,
            Scenes = [page]
        };
        var scene = ScadaScene.CreateEmpty("legacy_alias", "Native", CanvasSize.DefaultDesktop) with
        {
            PageKey = pageKey,
            PageCode = "native_page",
            Origin = PageOrigin.Native,
            Elements = [ScadaElement.CreateText("native_title", "Title", 10, 10)]
        };

        try
        {
            var result = await new Ft100SceneExporter().ExportProjectAsync(
                project,
                [new Ft100ProjectPageExportInput(scene, SourceHtmlPath: null, page)],
                root);

            var htmlPath = Path.Combine(result.ExportDirectory, "native_page", "native_page.html");
            Assert.IsTrue(File.Exists(htmlPath));
            var html = await File.ReadAllTextAsync(htmlPath);
            StringAssert.Contains(html, "id=\"ft100-native_page\"");
            StringAssert.Contains(html, "id=\"ft100-native_page__native_title\"");
            StringAssert.Contains(html, "class=\"ft100-source-layer\"");
            StringAssert.Contains(html, "scada-runtime.");
            Assert.AreEqual(0, result.CopiedImageCount);
            Assert.IsTrue(Ft100PackageValidator.ValidatePackageDirectory(result.ExportDirectory).IsValid);
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
    public async Task ExportProjectResolvesPageKeysToHumanCodesWithoutChangingSb2ManifestContract()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        Directory.CreateDirectory(sourceRoot);

        async Task<string> WriteSourceAsync(string name)
        {
            var path = Path.Combine(sourceRoot, $"{name}.html");
            await File.WriteAllTextAsync(path, "<!doctype html><html><body><div class=\"page\"></div></body></html>");
            return path;
        }

        var headerSource = await WriteSourceAsync("header");
        var homeSource = await WriteSourceAsync("home");
        var targetSource = await WriteSourceAsync("target");
        var headerKey = Guid.NewGuid();
        var homeKey = Guid.NewGuid();
        var targetKey = Guid.NewGuid();

        ScadaElement CreateNavigationButton(Guid? targetPageKey, string? targetPageId)
        {
            return ScadaElement.CreateButton("navigate", "Navigate", 10, 10) with
            {
                CommandConfig = new ScadaElementCommandConfig([
                    new ScadaCommandBinding(
                        "navigate-command",
                        "Navigate",
                        true,
                        ScadaCommandTrigger.OnClick,
                        ScadaCommandKind.Navigate,
                        TargetPageId: targetPageId,
                        TargetPageKey: targetPageKey)
                ])
            };
        }

        var legacyProject = ScadaProject.CreateDefault("Runtime identity") with
        {
            HomePageId = "process_home",
            Scenes =
            [
                new ScadaSceneReference("top_bar", "Header", "scenes/header.scene.json", ScadaPageType.Header),
                new ScadaSceneReference("process_home", "Home", "scenes/home.scene.json", HeaderPageId: "top_bar"),
                new ScadaSceneReference("process_target", "Target", "scenes/target.scene.json")
            ]
        };
        var legacyScenes = new[]
        {
            ScadaScene.CreateEmpty("top_bar", "Header", new(1280, 80)).WithPageType(ScadaPageType.Header),
            ScadaScene.CreateEmpty("process_home", "Home", new(1280, 873))
                .WithPageComposition("top_bar", null)
                .WithAction(new ScadaActionDefinition("navigate-action", ScadaActionKind.Navigate, TargetPageId: "process_target"))
                .WithElement(CreateNavigationButton(null, "process_target")),
            ScadaScene.CreateEmpty("process_target", "Target", new(1280, 873))
        };

        var keyedProject = ScadaProject.CreateDefault("Runtime identity") with
        {
            HomePageId = "legacy_home",
            HomePageKey = homeKey,
            Scenes =
            [
                new ScadaSceneReference("legacy_header", "Header", "scenes/header.scene.json", ScadaPageType.Header,
                    PageKey: headerKey, PageCode: "top_bar"),
                new ScadaSceneReference("legacy_home", "Home", "scenes/home.scene.json",
                    PageKey: homeKey, PageCode: "process_home", HeaderPageKey: headerKey),
                new ScadaSceneReference("legacy_target", "Target", "scenes/target.scene.json",
                    PageKey: targetKey, PageCode: "process_target")
            ]
        };
        var keyedScenes = new[]
        {
            ScadaScene.CreateEmpty("legacy_header", "Header", new(1280, 80)) with
            {
                PageType = ScadaPageType.Header,
                PageKey = headerKey,
                PageCode = "top_bar"
            },
            ScadaScene.CreateEmpty("legacy_home", "Home", new(1280, 873)) with
            {
                PageKey = homeKey,
                PageCode = "process_home",
                HeaderPageKey = headerKey,
                Actions = [new ScadaActionDefinition("navigate-action", ScadaActionKind.Navigate, TargetPageKey: targetKey)],
                Elements = [CreateNavigationButton(targetKey, null)]
            },
            ScadaScene.CreateEmpty("legacy_target", "Target", new(1280, 873)) with
            {
                PageKey = targetKey,
                PageCode = "process_target"
            }
        };

        try
        {
            var inputs = new[] { headerSource, homeSource, targetSource };
            var legacyResult = await new Ft100SceneExporter().ExportProjectAsync(
                legacyProject,
                legacyScenes.Select((scene, index) => new Ft100ProjectPageExportInput(scene, inputs[index])).ToArray(),
                Path.Combine(root, "legacy-export"));
            var keyedResult = await new Ft100SceneExporter().ExportProjectAsync(
                keyedProject,
                keyedScenes.Select((scene, index) => new Ft100ProjectPageExportInput(scene, inputs[index])).ToArray(),
                Path.Combine(root, "keyed-export"));

            var legacyManifest = await File.ReadAllTextAsync(legacyResult.ManifestPath);
            var keyedManifest = await File.ReadAllTextAsync(keyedResult.ManifestPath);
            Assert.AreEqual(legacyManifest, keyedManifest);
            StringAssert.Contains(keyedManifest, "\"HomePageId\": \"process_home\"");
            StringAssert.Contains(keyedManifest, "\"HeaderPageId\": \"top_bar\"");
            StringAssert.Contains(keyedManifest, "\"TargetPageId\": \"process_target\"");
            Assert.IsFalse(keyedManifest.Contains("PageKey", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(keyedManifest.Contains(headerKey.ToString(), StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(keyedManifest.Contains(homeKey.ToString(), StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(keyedManifest.Contains(targetKey.ToString(), StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(File.Exists(Path.Combine(keyedResult.ExportDirectory, "process_home", "process_home.html")));

            var homeHtml = await File.ReadAllTextAsync(Path.Combine(
                keyedResult.ExportDirectory,
                "process_home",
                "process_home.html"));
            StringAssert.Contains(homeHtml, "id=\"ft100-process_home\"");
            Assert.IsFalse(homeHtml.Contains("PageKey", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(homeHtml.Contains(targetKey.ToString(), StringComparison.OrdinalIgnoreCase));

            var validation = Ft100PackageValidator.ValidatePackageDirectory(keyedResult.ExportDirectory);
            Assert.IsTrue(validation.IsValid, string.Join(Environment.NewLine, validation.Errors.Select(error => error.Message)));
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
    public void PageRuntimeIdentityResolverRejectsPartiallyMigratedProjects()
    {
        var project = ScadaProject.CreateDefault("Partial") with
        {
            Scenes =
            [
                new ScadaSceneReference("page_one", "One", "scenes/one.scene.json", PageKey: Guid.NewGuid()),
                new ScadaSceneReference("page_two", "Two", "scenes/two.scene.json")
            ]
        };

        var exception = Assert.ThrowsException<InvalidOperationException>(() => new PageRuntimeIdentityResolver(project));
        StringAssert.Contains(exception.Message, "Every page must have a PageKey");
    }

    [TestMethod]
    public async Task ExportCssScopesImportedSourceDataIdsToPageRootForComposition()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var headerSource = await WriteSourceAsync("header_main", "1", "Header Button");
        var homeSource = await WriteSourceAsync("win00008", "1", "Home Button");
        var footerSource = await WriteSourceAsync("win00003", "1", "Footer Button");

        static async Task<string> WriteSourceAsync(string pageId, string dataId, string text)
        {
            var path = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", "composition-css-source", Guid.NewGuid().ToString("N"), $"{pageId}.html");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(
                path,
                $$"""
<!doctype html>
<html>
<body>
  <div class="page"><button class="layer" data-id="{{dataId}}">{{text}}</button></div>
</body>
</html>
""");
            return path;
        }

        var header = ScadaScene
            .CreateEmpty("header_main", "Header", new(1280, 80))
            .WithPageType(ScadaPageType.Header)
            .WithElement(CreateSourceButton("header_1", "1", 10, 10))
            .WithElement(CreateModernButton("Button1", "Header modern", 200, 10));
        var home = ScadaScene
            .CreateEmpty("win00008", "Home", new(1280, 873))
            .WithPageComposition("header_main", "win00003")
            .WithElement(CreateSourceButton("home_1", "1", 20, 20))
            .WithElement(CreateModernButton("Button1", "Home modern", 210, 20))
            .WithRemovedSourceElementIds(["999"]);
        var footer = ScadaScene
            .CreateEmpty("win00003", "Footer", new(1280, 120))
            .WithPageType(ScadaPageType.Footer)
            .WithElement(CreateSourceButton("footer_1", "1", 30, 30))
            .WithElement(CreateModernButton("Button1", "Footer modern", 220, 30));
        var project = ScadaProject.CreateDefault("Runtime") with
        {
            HomePageId = "win00008",
            Scenes =
            [
                new ScadaSceneReference("header_main", "Header", "scenes/header_main.scene.json", ScadaPageType.Header),
                new ScadaSceneReference("win00008", "Home", "scenes/win00008.scene.json", HeaderPageId: "header_main", FooterPageId: "win00003"),
                new ScadaSceneReference("win00003", "Footer", "scenes/win00003.scene.json", ScadaPageType.Footer)
            ]
        };

        try
        {
            var result = await new Ft100SceneExporter().ExportProjectAsync(
                project,
                [
                    new Ft100ProjectPageExportInput(header, headerSource),
                    new Ft100ProjectPageExportInput(home, homeSource),
                    new Ft100ProjectPageExportInput(footer, footerSource)
                ],
                exportRoot);

            // CSS filenames now include content hash (e.g., win00003.a1b2c3d.css).
            var footerCssPath = Directory.GetFiles(Path.Combine(result.ExportDirectory, "win00003", "css"), "win00003.*.css").First();
            var homeCssPath = Directory.GetFiles(Path.Combine(result.ExportDirectory, "win00008", "css"), "win00008.*.css").First();
            var headerCssPath = Directory.GetFiles(Path.Combine(result.ExportDirectory, "header_main", "css"), "header_main.*.css").First();
            var footerCss = await File.ReadAllTextAsync(footerCssPath);
            var homeCss = await File.ReadAllTextAsync(homeCssPath);
            var headerCss = await File.ReadAllTextAsync(headerCssPath);
            var footerHtml = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "win00003", "win00003.html"));
            var homeHtml = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "win00008", "win00008.html"));
            var headerHtml = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "header_main", "header_main.html"));

            StringAssert.Contains(footerCss, "#ft100-win00003 .ft100-source-layer [data-id=\"1\"], #ft100-win00003 .ft100-legacy-layer [data-id=\"1\"] {");
            StringAssert.Contains(homeCss, "#ft100-win00008 .ft100-source-layer [data-id=\"1\"], #ft100-win00008 .ft100-legacy-layer [data-id=\"1\"] {");
            StringAssert.Contains(homeCss, "#ft100-win00008 .ft100-source-layer [data-id=\"999\"], #ft100-win00008 .ft100-legacy-layer [data-id=\"999\"] { display: none !important; }");
            StringAssert.Contains(headerCss, "#ft100-header_main #ft100-header_main__Button1");
            StringAssert.Contains(homeCss, "#ft100-win00008 #ft100-win00008__Button1");
            StringAssert.Contains(footerCss, "#ft100-win00003 #ft100-win00003__Button1");
            StringAssert.Contains(headerHtml, "id=\"ft100-header_main__Button1\"");
            StringAssert.Contains(homeHtml, "id=\"ft100-win00008__Button1\"");
            StringAssert.Contains(footerHtml, "id=\"ft100-win00003__Button1\"");
            Assert.IsFalse(headerHtml.Contains("<div id=\"Button1\"", StringComparison.Ordinal));
            Assert.IsFalse(homeHtml.Contains("<div id=\"Button1\"", StringComparison.Ordinal));
            Assert.IsFalse(footerHtml.Contains("<div id=\"Button1\"", StringComparison.Ordinal));
            Assert.IsFalse(footerCss.Contains("\n[data-id=\"1\"] {", StringComparison.Ordinal));
            Assert.IsFalse(homeCss.Contains("\n[data-id=\"1\"] {", StringComparison.Ordinal));
            Assert.IsFalse(homeCss.Contains("\n[data-id=\"999\"] { display: none !important; }", StringComparison.Ordinal));
            Assert.IsFalse(footerCss.Contains("\n#Button1 {", StringComparison.Ordinal));
            Assert.IsFalse(homeCss.Contains("\n#Button1 {", StringComparison.Ordinal));
            AssertExportCssHasNoGlobalRuntimeSelectors(headerCss);
            AssertExportCssHasNoGlobalRuntimeSelectors(homeCss);
            AssertExportCssHasNoGlobalRuntimeSelectors(footerCss);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
            if (Directory.Exists(Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", "composition-css-source")))
            {
                Directory.Delete(Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", "composition-css-source"), recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task ExportProjectArchiveWritesSb2RootAndScopesLegacyDomIds()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(exportRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008.html");
        await File.WriteAllTextAsync(
            sourceHtmlPath,
            """
<!doctype html>
<html>
<body>
  <div class="page">
    <svg class="shape-layer">
      <defs><clipPath id="Mask"><rect width="10" height="10" /></clipPath></defs>
      <rect id="Mask" clip-path="url(#Mask)" />
    </svg>
    <div id="Button1" class="layer" data-id="1"><a href="#Button1">Start</a></div>
  </div>
</body>
</html>
""");

        var scene = ScadaScene.CreateEmpty("win00008", "Home", new(1280, 873));
        var project = ScadaProject.CreateDefault("Runtime") with
        {
            HomePageId = "win00008",
            Scenes =
            [
                new ScadaSceneReference("win00008", "Home", "scenes/win00008.scene.json")
            ]
        };

        try
        {
            var archivePath = Path.Combine(exportRoot, "runtime.sb2");
            var result = await new Ft100SceneExporter().ExportProjectArchiveAsync(
                project,
                [new Ft100ProjectPageExportInput(scene, sourceHtmlPath)],
                archivePath);

            Assert.AreEqual(archivePath, result.ArchivePath);
            Assert.AreEqual(Ft100SceneExporter.ProjectPackageDirectoryName, result.PackageRootName);
            Assert.AreEqual($"{Ft100SceneExporter.ProjectPackageDirectoryName}/manifest.json", result.ManifestRelativePath);
            Assert.AreEqual(1, result.PageCount);
            Assert.IsTrue(result.Validation.IsValid);
            Assert.IsTrue(File.Exists(archivePath));

            using var archive = ZipFile.OpenRead(archivePath);
            Assert.IsNotNull(archive.GetEntry($"{Ft100SceneExporter.ProjectPackageDirectoryName}/manifest.json"));
            var htmlEntry = archive.GetEntry($"{Ft100SceneExporter.ProjectPackageDirectoryName}/win00008/win00008.html");
            Assert.IsNotNull(htmlEntry);

            using var htmlStream = htmlEntry.Open();
            using var reader = new StreamReader(htmlStream);
            var html = await reader.ReadToEndAsync();

            StringAssert.Contains(html, "id=\"ft100-win00008\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__legacy-Mask\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__legacy-Mask-2\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__legacy-Button1\"");
            StringAssert.Contains(html, "clip-path=\"url(#ft100-win00008__legacy-Mask)\"");
            StringAssert.Contains(html, "href=\"#ft100-win00008__legacy-Button1\"");
            Assert.IsFalse(html.Contains("id=\"Mask\"", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("id=\"Button1\"", StringComparison.Ordinal));
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
    public async Task PackageValidatorAcceptsIndentedPageScopedCssIdSelectors()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(root, Ft100SceneExporter.ProjectPackageDirectoryName);
        var pageRoot = Path.Combine(packageRoot, "win00002");
        var cssRoot = Path.Combine(pageRoot, "css");
        Directory.CreateDirectory(cssRoot);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(packageRoot, "manifest.json"),
                """
{
  "Name": "Runtime",
  "HomePageId": "win00002",
  "Pages": [
    {
      "Id": "win00002",
      "Title": "Header",
      "Type": "header",
      "IncludeInBuild": true,
      "RelativePath": "win00002/win00002.html"
    }
  ]
}
""");
            await File.WriteAllTextAsync(
                Path.Combine(pageRoot, "win00002.html"),
                """
<!doctype html>
<html>
<body>
  <div id="ft100-win00002"><div id="ft100-win00002__element_1"></div></div>
</body>
</html>
""");
            await File.WriteAllTextAsync(
                Path.Combine(cssRoot, "win00002.css"),
                """
  #ft100-win00002 {
    position: relative;
  }
    #ft100-win00002__element_1 {
      left: 0;
    }
""");

            // Include a mock runtime JS to satisfy package validation
            await File.WriteAllTextAsync(
                Path.Combine(packageRoot, "scada-runtime.a0000000.js"),
                "// mock runtime\n");

            var validation = Ft100PackageValidator.ValidatePackageDirectory(packageRoot);

            Assert.IsFalse(validation.Errors.Any(issue => issue.Code == "global-id-selector"));
            Assert.IsTrue(validation.IsValid);
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
    public void ContentHash_Returns8CharLowercaseHex()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var filePath = Path.Combine(root, "test.txt");
            File.WriteAllText(filePath, "hello world");

            var hash = Ft100SceneExporter.ContentHash(filePath);

            Assert.AreEqual(8, hash.Length);
            Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(hash, "^[0-9a-f]{8}$"));
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
    public void ContentHash_SameContent_SameHash()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file1 = Path.Combine(root, "a.txt");
            var file2 = Path.Combine(root, "b.txt");
            File.WriteAllText(file1, "same content");
            File.WriteAllText(file2, "same content");

            var hash1 = Ft100SceneExporter.ContentHash(file1);
            var hash2 = Ft100SceneExporter.ContentHash(file2);

            Assert.AreEqual(hash1, hash2);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ScadaElement CreateSourceButton(string id, string sourceId, double x, double y)
    {
        return ScadaElement.CreateLegacyStatic(
            id,
            $"Button{sourceId}",
            new SceneBounds(x, y, 100, 40),
            new LegacySourceTrace("Wonderware/ArchestrA", "test", sourceId, $"Button{sourceId}", "test.html"),
            new LegacyElementPayload("Button", $"Button{sourceId}", true, "Arial", 10, "#000", "#ddd", null, null));
    }

    private static ScadaElement CreateModernButton(string id, string text, double x, double y)
    {
        return new ScadaElement(
            id,
            text,
            ScadaElementKind.Button,
            new SceneBounds(x, y, 100, 40),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultInput,
            new ScadaElementData(text, null, null, null, null, null, null, null, null, false));
    }

    private static void AssertExportCssHasNoGlobalRuntimeSelectors(string css)
    {
        Assert.IsFalse(
            System.Text.RegularExpressions.Regex.IsMatch(css, @"(?m)^\s*:root\b"),
            "FT100 CSS must not emit package-global :root variables because header/body/footer CSS files share one document in TF100Web.");
        Assert.IsFalse(
            System.Text.RegularExpressions.Regex.IsMatch(css, @"(?m)^\s*(html|body)\b"),
            "FT100 CSS must not emit package-global html/body rules.");
        Assert.IsFalse(
            System.Text.RegularExpressions.Regex.IsMatch(css, @"(?m)^\s*\[data-id="),
            "FT100 source data-id selectors must be scoped to the exported page root.");
        Assert.IsFalse(
            System.Text.RegularExpressions.Regex.IsMatch(css, @"(?m)^\s*\.ft100-"),
            "FT100 layer selectors must be scoped to the exported page root.");
        Assert.IsFalse(
            System.Text.RegularExpressions.Regex.IsMatch(css, @"(?m)^\s*#(?!ft100-)"),
            "FT100 Element+ id selectors must use page-prefixed DOM ids under the exported page root.");
    }

    private static string AssertHtmlReferencesSharedRuntime(string html, string exportDirectory)
    {
        var runtimeFiles = Directory.GetFiles(exportDirectory, "scada-runtime.*.js");
        Assert.AreEqual(1, runtimeFiles.Length, "Expected exactly one scada-runtime.*.js file in export directory.");
        var fileName = Path.GetFileName(runtimeFiles[0]);
        Assert.IsTrue(
            System.Text.RegularExpressions.Regex.IsMatch(fileName, @"^scada-runtime\.[0-9a-f]{8}\.js$"),
            "Runtime filename must be scada-runtime.<8-char-hex>.js");
        StringAssert.Contains(html, $"../{fileName}");
        StringAssert.Contains(html, "<script src=");
        Assert.IsFalse(html.Contains("<script>\n"), "HTML must not contain inline script blocks - runtime must be external");
        return fileName;
    }

    [TestMethod]
    public async Task ExportProjectAsync_WritesSharedRuntimeFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008_test.html");
        await File.WriteAllTextAsync(
            sourceHtmlPath,
            """
<!doctype html>
<html>
<body>
  <div class="page"><div class="layer" data-id="1">Home</div></div>
</body>
</html>
""");

        var scene = ScadaScene
            .CreateEmpty("win00008", "Home", new(1280, 873));
        var project = ScadaProject.CreateDefault("Runtime") with
        {
            HomePageId = "win00008",
            Scenes =
            [
                new ScadaSceneReference("win00008", "Home", "scenes/win00008.scene.json")
            ]
        };

        try
        {
            var result = await new Ft100SceneExporter().ExportProjectAsync(
                project,
                [new Ft100ProjectPageExportInput(scene, sourceHtmlPath)],
                exportRoot);

            var packageDirectory = result.ExportDirectory;

            // Shared runtime file exists at package root with correct name pattern
            var runtimeFiles = Directory.GetFiles(packageDirectory, "scada-runtime.*.js");
            Assert.AreEqual(1, runtimeFiles.Length);
            var runtimeFileName = Path.GetFileName(runtimeFiles[0]);
            Assert.IsTrue(
                System.Text.RegularExpressions.Regex.IsMatch(runtimeFileName, @"^scada-runtime\.[0-9a-f]{8}\.js$"));

            // HTML references external runtime via <script src>, not inline script
            var htmlPath = Path.Combine(packageDirectory, "win00008", "win00008.html");
            var html = await File.ReadAllTextAsync(htmlPath);
            StringAssert.Contains(html, $"<script src=\"../{runtimeFileName}\" defer></script>");
            Assert.IsFalse(html.Contains("<script>\n"),
                "HTML must not contain inline <script> blocks (BuildRuntimeScript was removed)");
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
    public async Task ExportAsync_IncludesStateConfigInManifestAndHtml()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "state_config_test.html");
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
            "element_state_001",
            "State Element",
            ScadaElementKind.Text,
            new SceneBounds(10, 20, 100, 30),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData("Stateful", null, null, null, null, null, null, null, null, false),
            StateConfig: new ScadaElementStateConfig(
                ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
                ScadaEffectBlock.Empty,
                [new ScadaStateRule(
                    "alarm_state",
                    "Alarm",
                    true,
                    ScadaExpression.FromSource("true"),
                    new ScadaEffectBlock(Animation: ScadaAnimation.Blink))]
            )
        );

        var scene = ScadaScene
            .CreateEmpty("win00008", "State Config Test", new(1280, 873))
            .WithElement(element);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var manifest = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "manifest.json"));
            StringAssert.Contains(manifest, "\"StateConfig\"");
            StringAssert.Contains(manifest, "\"States\"");
            StringAssert.Contains(manifest, "\"Animation\": \"blink\"");
            // Verify AST is serialized (the JS runtime reads expression.ast to evaluate states).
            StringAssert.Contains(manifest, "\"ast\"");
            // Manifest is PascalCase (backward compat), so Animation is PascalCase with camelCase enum value.
            StringAssert.Contains(manifest, "\"Animation\": \"blink\"");

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            StringAssert.Contains(html, "data-scada-state-config=\"");
            // HTML data attribute uses &quot; encoding for JSON string delimiters.
            StringAssert.Contains(html, "&quot;ast&quot;");
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

    [TestMethod]
    public async Task ExportAsync_CustomElementWithMismatchedViewBox_ForcesPreserveAspectRatioNone()
    {
        // Regression: a library .sep component's native SVG viewBox (authored size) can differ
        // from the instance's placed/resized Bounds (e.g. Vertical_Piping: Bounds 15x218 vs
        // native viewBox 11x333, seen in win00008's exported pompeAmmoniac/Vertical_Piping
        // instances). Without preserveAspectRatio="none", the browser's default "xMidYMid meet"
        // letterboxes/crops the artwork inside the resized box instead of stretching it to fill,
        // mirroring the WebView preview's runtime normalization (MainWindow.WebViewScript.cs,
        // Custom element handling) which always forces preserveAspectRatio="none".
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008_test.html");
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
            "pump_1",
            "pump_1",
            ScadaElementKind.Custom,
            new SceneBounds(10, 20, 15, 218),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData(
                "<svg viewBox=\"0 0 11 333\"><rect width=\"11\" height=\"333\" fill=\"gray\" /></svg>",
                null, null, null, null, null, null,
                "Svg",
                "Vertical_Piping.sep",
                false));

        var scene = ScadaScene
            .CreateEmpty("win00008", "Mismatched Custom", new(1280, 873))
            .WithElement(element);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            StringAssert.Contains(html, "preserveAspectRatio=\"none\"");

            var svgTagStart = html.IndexOf("<svg viewBox=\"0 0 11 333\"", StringComparison.Ordinal);
            Assert.AreNotEqual(-1, svgTagStart, "Expected the Custom element's native SVG to survive export.");
            var svgTagEnd = html.IndexOf('>', svgTagStart);
            var svgOpenTag = html[svgTagStart..(svgTagEnd + 1)];
            StringAssert.Contains(svgOpenTag, "preserveAspectRatio=\"none\"");
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
    public async Task ExportAsync_CustomElementSharesDataIdWithSuppressedLegacySource_DoesNotHideItsOwnSvgContent()
    {
        // Regression: pompeAmmoniac (win00008) was built by converting an existing legacy pump
        // icon (raw SVG shapes with numeric data-id, e.g. "228"/Polygon139) into a Custom .sep
        // library component. The Custom element's own SVG markup preserves those original
        // data-id attributes on its shapes. Converting a legacy element registers its source id
        // as "suppressed" (scene.GetSuppressedSourceElementIds()) so the leftover legacy copy in
        // .ft100-source-layer gets hidden — but the CSS selector previously wasn't scoped to that
        // layer, so "[data-id=\"228\"] { display: none !important; }" also matched — and hid —
        // the identically-numbered shape living inside the Custom element's own SVG in
        // .ft100-elementplus-layer, making the component render as empty/near-invisible.
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008_test.html");
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

        var pumpElement = new ScadaElement(
            "pump_1",
            "pompeAmmoniac",
            ScadaElementKind.Custom,
            new SceneBounds(10, 20, 140, 60),
            new LegacySourceTrace("html", "win00008_test.html", "228", "Polygon139", null),
            null,
            ScadaElementStyle.DefaultText,
            new ScadaElementData(
                "<svg viewBox=\"0 0 140 60\"><polygon points=\"1,1 2,1 2,2 1,2\" fill=\"gray\" data-name=\"Polygon139\" data-id=\"228\" /></svg>",
                null, null, null, null, null, null,
                "Svg",
                "pompeAmmoniac.sep",
                false));

        var scene = ScadaScene
            .CreateEmpty("win00008", "Pump conversion", new(1280, 873))
            .WithElement(pumpElement);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var css = await File.ReadAllTextAsync(result.CssPath);
            StringAssert.Contains(
                css,
                "#ft100-win00008 .ft100-source-layer [data-id=\"228\"], #ft100-win00008 .ft100-legacy-layer [data-id=\"228\"] { display: none !important; }");
            Assert.IsFalse(
                css.Contains("\n#ft100-win00008 [data-id=\"228\"] {", StringComparison.Ordinal),
                "Suppression rule must not use a bare page-scoped selector that also matches data-id copies inside .ft100-elementplus-layer.");

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var svgStart = html.IndexOf("<svg viewBox=\"0 0 140 60\"", StringComparison.Ordinal);
            Assert.AreNotEqual(-1, svgStart, "Expected the Custom element's native SVG to survive export.");
            var polygonStart = html.IndexOf("data-id=\"228\"", svgStart, StringComparison.Ordinal);
            Assert.AreNotEqual(-1, polygonStart, "Expected the converted polygon to keep its original data-id.");
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
    public async Task ExportAsync_TwoInstancesOfSameLibraryComponent_ProduceDistinctScopedSvgIds()
    {
        // Regression: library .sep components share internal part ids (e.g. "Element001").
        // Placing two instances of the same component on one page must not collide after
        // scoping (see docs/superpowers/specs export runtime refactor, win00008 export failure
        // "duplicate DOM id 'ft100-win00008__svg-Element001'").
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008_test.html");
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

        static ScadaElement CreatePumpInstance(string elementId) => new(
            elementId,
            elementId,
            ScadaElementKind.Custom,
            new SceneBounds(10, 20, 100, 30),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData(
                "<svg viewBox=\"0 0 24 80\"><rect id=\"Element001\" width=\"24\" height=\"80\" fill=\"url(#Element001)\" /></svg>",
                null, null, null, null, null, null,
                "Svg",
                "pump.sep",
                false));

        var scene = ScadaScene
            .CreateEmpty("win00008", "Two Pumps", new(1280, 873))
            .WithElement(CreatePumpInstance("pump_1"))
            .WithElement(CreatePumpInstance("pump_2"));

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var ids = System.Text.RegularExpressions.Regex.Matches(html, """(?<![\w:-])id\s*=\s*["']([^"']+)["']""")
                .Select(match => match.Groups[1].Value)
                .ToArray();
            var duplicates = ids.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
            Assert.AreEqual(0, duplicates.Length, $"Duplicate DOM ids found: {string.Join(", ", duplicates)}");

            StringAssert.Contains(html, "ft100-win00008__pump_1__svg-Element001");
            StringAssert.Contains(html, "ft100-win00008__pump_2__svg-Element001");

            var validation = Ft100PackageValidator.ValidatePackageDirectory(result.ExportDirectory);
            Assert.IsFalse(validation.Errors.Any(issue => issue.Code is "duplicate-dom-id" or "unscoped-dom-id"),
                string.Join("; ", validation.Errors.Select(issue => issue.Message)));
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
    public async Task ExportAsync_ShapeElement_ProducesPageScopedSvgIds()
    {
        // Regression: BuildShape emitted unscoped internal ids ("shape-{elementId}",
        // "arrow-{elementId}", "lamp-gradient-{elementId}") that never carried the
        // "ft100-{page}__" prefix required by Ft100PackageValidator, so any page with a
        // Shape element failed export with "contains non page-scoped DOM id 'shape-...'".
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008_test.html");
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

        var arrow = ScadaElement.CreateShape("ba1625de403e4c589b88826fa2a601de", "Fleche", ScadaShapeKind.Arrow, 40, 50);
        var lamp = ScadaElement.CreateShape("shape_lamp_regress", "Voyant", ScadaShapeKind.IndicatorLamp, 80, 120);

        var scene = ScadaScene
            .CreateEmpty("win00008", "Formes", new(1280, 873))
            .WithElement(arrow)
            .WithElement(lamp);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            StringAssert.Contains(html, "<svg id=\"ft100-win00008__ba1625de403e4c589b88826fa2a601de__shape\"");
            StringAssert.Contains(html, "marker-end=\"url(#ft100-win00008__ba1625de403e4c589b88826fa2a601de__arrow)\"");
            StringAssert.Contains(html, "radialGradient id=\"ft100-win00008__shape_lamp_regress__lamp-gradient\"");

            var validation = Ft100PackageValidator.ValidatePackageDirectory(result.ExportDirectory);
            Assert.IsFalse(validation.Errors.Any(issue => issue.Code is "duplicate-dom-id" or "unscoped-dom-id"),
                string.Join("; ", validation.Errors.Select(issue => issue.Message)));
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
    public async Task ValidatePackageDirectory_FindsContentHashedCssFile_NoMissingCssWarning()
    {
        // Regression: the exporter renames "<page>.css" to "<page>.<hash>.css" for cache-busting
        // (ContentHash, ExportAsync). Ft100PackageValidator looked for the exact un-hashed
        // filename, so every exported page produced a spurious "missing-css" warning and its
        // CSS content was never actually validated (global-selector checks silently skipped).
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "win00008_test.html");
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

        var scene = ScadaScene.CreateEmpty("win00008", "Home", new(1280, 873));

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var cssDirectory = Path.GetDirectoryName(result.CssPath)!;
            var cssFiles = Directory.GetFiles(cssDirectory, "win00008.*.css");
            Assert.AreEqual(1, cssFiles.Length, "Expected content-hashed CSS file to exist.");
            Assert.IsFalse(File.Exists(Path.Combine(cssDirectory, "win00008.css")), "Un-hashed CSS filename should not exist.");

            var validation = Ft100PackageValidator.ValidatePackageDirectory(result.ExportDirectory);
            Assert.IsFalse(validation.Warnings.Any(issue => issue.Code == "missing-css"),
                string.Join("; ", validation.Warnings.Select(issue => issue.Message)));
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
    public async Task ExportAsync_IncludesAnimationKeyframesInCss()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);

        var sourceHtmlPath = Path.Combine(sourceRoot, "animation_test.html");
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

        var scene = ScadaScene.CreateEmpty("testscene", "Animation Test", new(800, 600));

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);
            var css = await File.ReadAllTextAsync(result.CssPath);

            // Page-scoped @keyframes rules
            var prefix = "ft100-testscene---scada-";
            StringAssert.Contains(css, $"@keyframes {prefix}blink");
            StringAssert.Contains(css, $"@keyframes {prefix}pulse");
            StringAssert.Contains(css, $"@keyframes {prefix}halo");
            StringAssert.Contains(css, $"@keyframes {prefix}spin");

            // Animation CSS classes referencing page-scoped keyframes
            StringAssert.Contains(css, ".scada-anim-blink");
            StringAssert.Contains(css, ".scada-anim-pulse");
            StringAssert.Contains(css, ".scada-anim-halo");
            StringAssert.Contains(css, ".scada-anim-spin");
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
    public void ValidatePackageDirectory_ErrorsWhenRuntimeJsIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(root, Ft100SceneExporter.ProjectPackageDirectoryName);
        var pageRoot = Path.Combine(packageRoot, "win00002");
        var cssRoot = Path.Combine(pageRoot, "css");
        Directory.CreateDirectory(cssRoot);

        try
        {
            File.WriteAllText(
                Path.Combine(packageRoot, "manifest.json"),
                """
{
  "Name": "Runtime",
  "HomePageId": "win00002",
  "Pages": [
    {
      "Id": "win00002",
      "Title": "Header",
      "Type": "header",
      "IncludeInBuild": true,
      "RelativePath": "win00002/win00002.html"
    }
  ]
}
""");
            File.WriteAllText(
                Path.Combine(pageRoot, "win00002.html"),
                """
<!doctype html>
<html>
<body>
  <div id="ft100-win00002"><div id="ft100-win00002__element_1"></div></div>
</body>
</html>
""");
            File.WriteAllText(
                Path.Combine(cssRoot, "win00002.css"),
                """
#ft100-win00002 {
  position: relative;
}
""");

            var validation = Ft100PackageValidator.ValidatePackageDirectory(packageRoot);

            Assert.IsFalse(validation.IsValid);
            Assert.IsTrue(
                validation.Errors.Any(e => e.Message.Contains("runtime", StringComparison.OrdinalIgnoreCase)),
                "Expected at least one error mentioning 'runtime'.");
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
    public async Task ExportProjectArchive_ProducesCompleteSb2WithStateCommandRuntime()
    {
        var project = ScadaProject.CreateDefault("e2e-project");
        var scene = ScadaScene.CreateEmpty("e2e-page", "E2E Page", new(1280, 873));

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
            null,
            StateConfig: stateConfig,
            CommandConfig: commandConfig);

        scene = scene.WithElement(element);
        project = project with
        {
            HomePageId = "e2e-page",
            Scenes = [new ScadaSceneReference("e2e-page", "E2E Page", "scenes/e2e-page.scene.json")]
        };

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

            Assert.IsTrue(result.Validation.IsValid, "Package must pass validation");
            Assert.IsTrue(File.Exists(result.ArchivePath), "Archive file must exist");

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
            var manifestEntry = zip.Entries.First(e => e.FullName.EndsWith("manifest.json"));
            using var manifestStream = manifestEntry.Open();
            using var manifestDoc = await JsonDocument.ParseAsync(manifestStream);
            var root = manifestDoc.RootElement;
            var pages = root.GetProperty("Pages");
            var firstPage = pages[0];
            var objects = firstPage.GetProperty("Objects");
            var firstObject = objects[0];

            Assert.IsTrue(firstObject.TryGetProperty("StateConfig", out var _),
                "Manifest must have StateConfig");
            Assert.IsTrue(firstObject.TryGetProperty("CommandConfig", out var _),
                "Manifest must have CommandConfig");

            // Verify HTML content
            var htmlEntry = zip.Entries.First(e => e.FullName.EndsWith(".html"));
            using var htmlStream = htmlEntry.Open();
            using var htmlReader = new StreamReader(htmlStream);
            var html = await htmlReader.ReadToEndAsync();

            Assert.IsTrue(html.Contains("data-scada-state-config"), "HTML must have data-scada-state-config");
            Assert.IsTrue(html.Contains("data-scada-command-config"), "HTML must have data-scada-command-config");
            Assert.IsTrue(html.Contains("<script src="), "HTML must use external script reference");
        }
        finally
        {
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
        }
    }

    [TestMethod]
    public void AuditOrphanedEventBindings_WarnsForNavigateEventWithoutCommandConfig()
    {
        var scene = ScadaScene.CreateEmpty("test_audit_scene", "Test Scene", new CanvasSize(1280, 960));
        var element = new ScadaElement(
            Id: "orphan_btn",
            DisplayName: "Orphan Button",
            Kind: ScadaElementKind.Button,
            Bounds: new SceneBounds(0, 0, 100, 40),
            Children: null,
            LegacySource: null,
            Layout: ScadaElementLayout.Absolute,
            Style: ScadaElementStyle.DefaultText,
            Data: null,
            LegacyPayload: null,
            Events:
            [
                new ScadaObjectEventBinding(
                    Trigger: "click",
                    ActionId: "action_changepage_click_orphan_btn_win00099",
                    StopPropagation: true,
                    PreventDefault: false)
            ],
            ButtonBehavior: null,
            ShapeKind: null,
            ButtonKind: null,
            StateConfig: null,
            CommandConfig: null);

        scene = scene.WithElement(element);

        var issues = new List<ScadaBuildValidationIssue>();
        ScadaProjectBuildValidator.AuditOrphanedEventBindings(issues, scene);

        Assert.AreEqual(1, issues.Count);
        Assert.AreEqual(ScadaBuildValidationSeverity.Warning, issues[0].Severity);
        StringAssert.Contains(issues[0].Message, "orphan_btn");
    }

    [TestMethod]
    public void AuditOrphanedEventBindings_SilentWhenCommandConfigPresent()
    {
        var scene = ScadaScene.CreateEmpty("test_audit_scene", "Test Scene", new CanvasSize(1280, 960));
        var element = new ScadaElement(
            Id: "healthy_btn",
            DisplayName: "Healthy Button",
            Kind: ScadaElementKind.Group,
            Bounds: new SceneBounds(0, 0, 100, 40),
            Children: null,
            LegacySource: null,
            Layout: ScadaElementLayout.Absolute,
            Style: ScadaElementStyle.DefaultText,
            Data: null,
            LegacyPayload: null,
            Events:
            [
                new ScadaObjectEventBinding(
                    Trigger: "click",
                    ActionId: "action_changepage_click_healthy_btn_win00004",
                    StopPropagation: true,
                    PreventDefault: false)
            ],
            ButtonBehavior: null,
            ShapeKind: null,
            ButtonKind: null,
            StateConfig: null,
            CommandConfig: new ScadaElementCommandConfig(
                Commands:
                [
                    new ScadaCommandBinding(
                        Id: "nav1",
                        Name: "Go",
                        Enabled: true,
                        Trigger: ScadaCommandTrigger.OnClick,
                        Kind: ScadaCommandKind.Navigate,
                        TargetPageId: "win00004")
                ]));

        scene = scene.WithElement(element);

        var issues = new List<ScadaBuildValidationIssue>();
        ScadaProjectBuildValidator.AuditOrphanedEventBindings(issues, scene);

        Assert.AreEqual(1, issues.Count, "Element with legacy EventBindings must warn even with CommandConfig.");
        StringAssert.Contains(issues[0].Message, "EventBindings decommissionnes");
        StringAssert.Contains(issues[0].Message, "configuration moderne");
    }

    [TestMethod]
    public void AuditOrphanedEventBindings_NoWarningsForCleanScene()
    {
        var scene = ScadaScene.CreateEmpty("test_clean_scene", "Clean Scene", new CanvasSize(1280, 960));
        var element = new ScadaElement(
            Id: "clean_text",
            DisplayName: "Just Text",
            Kind: ScadaElementKind.Text,
            Bounds: new SceneBounds(0, 0, 100, 40),
            Children: null,
            LegacySource: null,
            Layout: ScadaElementLayout.Absolute,
            Style: ScadaElementStyle.DefaultText,
            Data: new ScadaElementData(
                Text: "Hello",
                Placeholder: null,
                Value: null,
                Minimum: null,
                Maximum: null,
                Decimals: null,
                Unit: null,
                DisplayFormat: null,
                TagBinding: null,
                IsReadOnly: false),
            LegacyPayload: null,
            Events: null,
            ButtonBehavior: null,
            ShapeKind: null,
            ButtonKind: null,
            StateConfig: null,
            CommandConfig: null);

        scene = scene.WithElement(element);

        var issues = new List<ScadaBuildValidationIssue>();
        ScadaProjectBuildValidator.AuditOrphanedEventBindings(issues, scene);

        Assert.AreEqual(0, issues.Count, "Scene with no EventBindings should produce no warnings");
    }

    [TestMethod]
    public void ElementWithNavigateCommandConfig_DoesNotProduceLegacyEventBindings()
    {
        // Regression lock: an element authored through the current CommandConfig path
        // must not generate legacy EventBindings as a side effect.
        var element = new ScadaElement(
            Id: "nav_group",
            DisplayName: "Navigation Group",
            Kind: ScadaElementKind.Group,
            Bounds: new SceneBounds(0, 0, 138, 44),
            Children: null,
            LegacySource: null,
            Layout: ScadaElementLayout.Absolute,
            Style: ScadaElementStyle.DefaultText,
            Data: null,
            LegacyPayload: null,
            Events: null,
            ButtonBehavior: null,
            ShapeKind: null,
            ButtonKind: null,
            StateConfig: null,
            CommandConfig: new ScadaElementCommandConfig(
                Commands:
                [
                    new ScadaCommandBinding(
                        Id: "nav_cmd_1",
                        Name: "GoToPage",
                        Enabled: true,
                        Trigger: ScadaCommandTrigger.OnClick,
                        Kind: ScadaCommandKind.Navigate,
                        TargetPageId: "win00099")
                ]));

        // The element's Events should remain null — CommandConfig is the canonical
        // model for the new runtime. EventBindings are legacy-only.
        Assert.IsTrue(
            element.Events is null or { Count: 0 },
            "New Navigate CommandConfig must not produce legacy EventBindings");
    }

    [TestMethod]
    public async Task ExportAsync_TagRefWithTagId_ExportsCanonicalTagName()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(root, "source");
        Directory.CreateDirectory(sourceDir);
        var sourceHtmlPath = Path.Combine(sourceDir, "tagid_test.html");
        await File.WriteAllTextAsync(sourceHtmlPath,
            "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var expr = new ScadaExpression(
            "{PE_16} == true",
            new ScadaExprBinary(ScadaExprBinaryOp.Equal,
                new ScadaExprTagRef("PE_16", "tf100.mapping.161"),
                new ScadaExprLiteralBool(true)),
            new[] { "PE_16" });

        var element = new ScadaElement(
            "el_canonical", "Canonical", ScadaElementKind.Text,
            new SceneBounds(10, 20, 100, 30), null, ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData("test", null, null, null, null, null, null, null, null, false),
            StateConfig: new ScadaElementStateConfig(
                ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
                ScadaEffectBlock.Empty,
                new[] { new ScadaStateRule("s1", "Running", true, expr,
                    new ScadaEffectBlock(ColorFilterColor: "#12B729")) }));

        var scene = ScadaScene.CreateEmpty("win00008", "Test", new(1280, 873)).WithElement(element);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(
                scene, sourceHtmlPath, Path.Combine(root, "export"));
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var decoded = html.Replace("&quot;", "\"");

            StringAssert.Contains(decoded, "\"tagName\":\"tf100.mapping.161\"",
                "Exported AST must use canonical Id as tagName.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ExportAsync_TagRefWithTagId_NormalizesEvenWithoutCatalog()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(root, "source");
        Directory.CreateDirectory(sourceDir);
        var sourceHtmlPath = Path.Combine(sourceDir, "nocat_test.html");
        await File.WriteAllTextAsync(sourceHtmlPath,
            "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var expr = new ScadaExpression(
            "{PE_16} == true",
            new ScadaExprBinary(ScadaExprBinaryOp.Equal,
                new ScadaExprTagRef("PE_16", "tf100.mapping.161"),
                new ScadaExprLiteralBool(true)),
            new[] { "PE_16" });

        var element = new ScadaElement(
            "el_nocat", "NoCatalog", ScadaElementKind.Text,
            new SceneBounds(10, 20, 100, 30), null, ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData("test", null, null, null, null, null, null, null, null, false),
            StateConfig: new ScadaElementStateConfig(
                ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
                ScadaEffectBlock.Empty,
                new[] { new ScadaStateRule("s1", "R", true, expr,
                    new ScadaEffectBlock(ColorFilterColor: "#12B729")) }));

        var scene = ScadaScene.CreateEmpty("win00008", "NoCat", new(1280, 873)).WithElement(element);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(
                scene, sourceHtmlPath, Path.Combine(root, "export"));
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var decoded = html.Replace("&quot;", "\"");

            StringAssert.Contains(decoded, "\"tagName\":\"tf100.mapping.161\"",
                "TagRef with TagId must be normalized even without a catalog.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ExportAsync_UnresolvedTagRef_EmitsWarning()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(root, "source");
        Directory.CreateDirectory(sourceDir);
        var sourceHtmlPath = Path.Combine(sourceDir, "unresolved_test.html");
        await File.WriteAllTextAsync(sourceHtmlPath,
            "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var expr = new ScadaExpression(
            "{TagInconnu} == true",
            new ScadaExprBinary(ScadaExprBinaryOp.Equal,
                new ScadaExprTagRef("TagInconnu"),
                new ScadaExprLiteralBool(true)),
            new[] { "TagInconnu" });

        var element = new ScadaElement(
            "el_unresolved", "Unresolved", ScadaElementKind.Text,
            new SceneBounds(10, 20, 100, 30), null, ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData("test", null, null, null, null, null, null, null, null, false),
            StateConfig: new ScadaElementStateConfig(
                ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
                ScadaEffectBlock.Empty,
                new[] { new ScadaStateRule("s1", "Unk", true, expr,
                    new ScadaEffectBlock(ColorFilterColor: "#E53935")) }));

        var scene = ScadaScene.CreateEmpty("win00008", "Unresolved", new(1280, 873)).WithElement(element);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(
                scene, sourceHtmlPath, Path.Combine(root, "export"));
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var decoded = html.Replace("&quot;", "\"");

            StringAssert.Contains(decoded, "\"tagName\":\"TagInconnu\"",
                "Unresolved tag ref must be left as-is for qualityFallback.");
            Assert.IsTrue(result.Warnings.Count > 0,
                "Export must warn about unresolved tag references.");
            Assert.IsTrue(result.Warnings.Any(w => w.Contains("TagInconnu")),
                "Warning must mention the unresolved tag name.");
            Assert.AreEqual(1, result.Warnings.Count(w => w.Contains("TagInconnu")),
                "HTML and manifest normalization must not duplicate the same unresolved warning.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ExportProjectAsync_UnresolvedTagRef_AggregatesProjectWarningsOnce()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(root, "source");
        Directory.CreateDirectory(sourceDir);
        var sourceHtmlPath = Path.Combine(sourceDir, "project_unresolved_test.html");
        await File.WriteAllTextAsync(sourceHtmlPath,
            "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var expr = new ScadaExpression(
            "{TagInconnu} == true",
            new ScadaExprBinary(ScadaExprBinaryOp.Equal,
                new ScadaExprTagRef("TagInconnu"),
                new ScadaExprLiteralBool(true)),
            new[] { "TagInconnu" });

        var element = new ScadaElement(
            "el_project_unresolved", "Project unresolved", ScadaElementKind.Text,
            new SceneBounds(10, 20, 100, 30), null, ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData("test", null, null, null, null, null, null, null, null, false),
            StateConfig: new ScadaElementStateConfig(
                ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
                ScadaEffectBlock.Empty,
                new[] { new ScadaStateRule("s1", "Unk", true, expr,
                    new ScadaEffectBlock(ColorFilterColor: "#E53935")) }));

        var scene = ScadaScene.CreateEmpty("win00008", "Project unresolved", new(1280, 873)).WithElement(element);
        var project = ScadaProject.CreateDefault("ProjectUnresolved") with
        {
            HomePageId = "win00008",
            Scenes = [new ScadaSceneReference("win00008", "Project unresolved", "scenes/win00008.scene.json")]
        };

        try
        {
            var result = await new Ft100SceneExporter().ExportProjectAsync(
                project,
                [new Ft100ProjectPageExportInput(scene, sourceHtmlPath)],
                Path.Combine(root, "export"));

            Assert.AreEqual(1, result.Warnings.Count(w => w.Contains("TagInconnu")),
                "Project export must expose deduplicated unresolved tag warnings.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ExportProjectArchiveAsync_Sb2Package_CoherentStateConfigInZipManifestAndHtml()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(root, "source");
        Directory.CreateDirectory(sourceDir);
        var sourceHtmlPath = Path.Combine(sourceDir, "sb2_canonical_test.html");
        await File.WriteAllTextAsync(sourceHtmlPath,
            "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var catalog = new ScadaTagCatalog("v1", new[]
        {
            new ScadaTagDefinition("tf100.mapping.195", "Noeud1_N15_03_Commande_MC_120A", Datatype: "bool"),
            new ScadaTagDefinition("tf100.mapping.196", "Noeud1_N15_04_Commande_MC_120C", Datatype: "bool"),
        });
        var greenExpr = new ScadaExpression(
            "{Noeud1_N15_03_Commande_MC_120A} == true",
            new ScadaExprBinary(ScadaExprBinaryOp.Equal,
                new ScadaExprTagRef("Noeud1_N15_03_Commande_MC_120A"),
                new ScadaExprLiteralBool(true)),
            new[] { "Noeud1_N15_03_Commande_MC_120A" });
        var redExpr = new ScadaExpression(
            "{Noeud1_N15_04_Commande_MC_120C} == false",
            new ScadaExprBinary(ScadaExprBinaryOp.Equal,
                new ScadaExprTagRef("Noeud1_N15_04_Commande_MC_120C"),
                new ScadaExprLiteralBool(false)),
            new[] { "Noeud1_N15_04_Commande_MC_120C" });

        var element = new ScadaElement(
            "el_sb2_canonical", "SB2 canonical", ScadaElementKind.Text,
            new SceneBounds(10, 20, 100, 30), null, ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData("test", null, null, null, null, null, null, null, null, false),
            StateConfig: new ScadaElementStateConfig(
                ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
                ScadaEffectBlock.Empty,
                new[]
                {
                    new ScadaStateRule("green", "Green", true, greenExpr,
                        new ScadaEffectBlock(ColorFilterColor: "#12B729")),
                    new ScadaStateRule("red", "Red", true, redExpr,
                        new ScadaEffectBlock(ColorFilterColor: "#E53935"))
                }));

        var scene = ScadaScene.CreateEmpty("win00008", "SB2 canonical", new(1280, 873)).WithElement(element);
        var project = ScadaProject.CreateDefault("SB2Canonical") with
        {
            HomePageId = "win00008",
            Scenes = [new ScadaSceneReference("win00008", "SB2 canonical", "scenes/win00008.scene.json")],
            TagCatalog = catalog
        };
        var archivePath = Path.Combine(root, "export.sb2");

        try
        {
            var result = await new Ft100SceneExporter().ExportProjectArchiveAsync(
                project,
                [new Ft100ProjectPageExportInput(scene, sourceHtmlPath)],
                archivePath);

            Assert.IsTrue(result.Validation.IsValid, "Package must pass validation.");
            using var zip = ZipFile.OpenRead(result.ArchivePath);
            var manifestEntry = zip.GetEntry("scada-builder-v2-ft100-package/manifest.json");
            var htmlEntry = zip.GetEntry("scada-builder-v2-ft100-package/win00008/win00008.html");
            Assert.IsNotNull(manifestEntry, "Root manifest must exist in the .sb2 package.");
            Assert.IsNotNull(htmlEntry, "Page HTML must exist in the .sb2 package.");

            using var manifestReader = new StreamReader(manifestEntry!.Open(), Encoding.UTF8);
            var manifest = await manifestReader.ReadToEndAsync();
            using var htmlReader = new StreamReader(htmlEntry!.Open(), Encoding.UTF8);
            var html = (await htmlReader.ReadToEndAsync()).Replace("&quot;", "\"");

            StringAssert.Contains(manifest, "tf100.mapping.195");
            StringAssert.Contains(manifest, "tf100.mapping.196");
            StringAssert.Contains(html, "\"tagName\":\"tf100.mapping.195\"");
            StringAssert.Contains(html, "\"tagName\":\"tf100.mapping.196\"");
            Assert.IsFalse(manifest.Contains("\"TagName\": \"Noeud1_N15_03_Commande_MC_120A\"", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("\"tagName\":\"Noeud1_N15_03_Commande_MC_120A\"", StringComparison.Ordinal));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ExportAsync_ExportedAst_UsesLowercaseOpValues()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(root, "source");
        Directory.CreateDirectory(sourceDir);
        var sourceHtmlPath = Path.Combine(sourceDir, "casing_test.html");
        await File.WriteAllTextAsync(sourceHtmlPath,
            "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var expr = new ScadaExpression(
            "{tf100.mapping.196} == true",
            new ScadaExprBinary(ScadaExprBinaryOp.Equal,
                new ScadaExprTagRef("tf100.mapping.196", "tf100.mapping.196"),
                new ScadaExprLiteralBool(true)),
            new[] { "tf100.mapping.196" });
        var element = new ScadaElement(
            "el_casing", "Casing", ScadaElementKind.Text,
            new SceneBounds(10, 20, 100, 30), null, ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData("test", null, null, null, null, null, null, null, null, false),
            StateConfig: new ScadaElementStateConfig(
                ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
                ScadaEffectBlock.Empty,
                new[] { new ScadaStateRule("s1", "R", true, expr, ScadaEffectBlock.Empty) }));
        var scene = ScadaScene.CreateEmpty("win00008", "Casing", new(1280, 873)).WithElement(element);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(
                scene, sourceHtmlPath, Path.Combine(root, "export"));
            var html = (await File.ReadAllTextAsync(result.HtmlPath)).Replace("&quot;", "\"");
            var manifest = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "manifest.json"));

            StringAssert.Contains(html, "\"op\":\"equal\"");
            StringAssert.Contains(manifest, "\"Op\": \"equal\"");
            Assert.IsFalse(html.Contains("\"op\":\"Equal\"", StringComparison.Ordinal));
            Assert.IsFalse(manifest.Contains("\"Op\": \"Equal\"", StringComparison.Ordinal));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task ExportAsync_AmbiguousTagRef_ThrowsInvalidOperationException()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(root, "source");
        Directory.CreateDirectory(sourceDir);
        var sourceHtmlPath = Path.Combine(sourceDir, "ambiguous_test.html");
        await File.WriteAllTextAsync(sourceHtmlPath,
            "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var expr = new ScadaExpression(
            "{DuplicateLabel} == true",
            new ScadaExprBinary(ScadaExprBinaryOp.Equal,
                new ScadaExprTagRef("DuplicateLabel"),
                new ScadaExprLiteralBool(true)),
            new[] { "DuplicateLabel" });

        var element = new ScadaElement(
            "el_ambig", "Ambiguous", ScadaElementKind.Text,
            new SceneBounds(10, 20, 100, 30), null, ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData("test", null, null, null, null, null, null, null, null, false),
            StateConfig: new ScadaElementStateConfig(
                ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
                ScadaEffectBlock.Empty,
                new[] { new ScadaStateRule("s1", "Amb", true, expr,
                    new ScadaEffectBlock(ColorFilterColor: "#E53935")) }));

        var scene = ScadaScene.CreateEmpty("win00008", "Ambiguous", new(1280, 873)).WithElement(element);

        var catalog = new ScadaTagCatalog("v1", new[]
        {
            new ScadaTagDefinition("tf100.mapping.200", "DuplicateLabel", Datatype: "float"),
            new ScadaTagDefinition("tf100.mapping.201", "DuplicateLabel", Datatype: "bool"),
        });
        var project = ScadaProject.CreateDefault("TestProject") with { TagCatalog = catalog };

        try
        {
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => new Ft100SceneExporter().ExportAsync(
                    scene, sourceHtmlPath, Path.Combine(root, "export"), project),
                "Ambiguous tag reference must block export (D8).");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static string CreateTempExportDir()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "source"));
        return root;
    }

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
            var decoded = html.Replace("&quot;", "\"");

            StringAssert.Contains(html, "data-scada-command-config=\"");
            StringAssert.Contains(decoded, "\"kind\":\"navigate\"");
            StringAssert.Contains(decoded, "\"targetPageId\":\"win00009\"");
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
            var decoded = html.Replace("&quot;", "\"");

            StringAssert.Contains(html, "data-scada-command-config=\"");
            StringAssert.Contains(decoded, "\"writeTagId\":\"tf100.mapping.42\"");
            StringAssert.Contains(decoded, "\"kind\":\"writeTag\"");
            Assert.IsFalse(html.Contains("data-scada-events="));
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
            var decoded = html.Replace("&quot;", "\"");
            StringAssert.Contains(decoded, "\"readVariable\":");
            StringAssert.Contains(decoded, "\"tagId\":\"tf100.mapping.42\"");
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
        var sourceHtmlPath = Path.Combine(root, "source", "grp_legacy_val.html");
        await File.WriteAllTextAsync(sourceHtmlPath,
            "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var group = new ScadaElement(
            "grp_legacy_val", "Legacy Value Group", ScadaElementKind.Group,
            new SceneBounds(100, 200, 160, 70),
            null, ScadaElementLayout.Absolute, ScadaElementStyle.DefaultText,
            Data: new ScadaElementData(null, null, null, null, null, null, null, null, null, false,
                ReadTagId: "tf100.mapping.42", WriteTagId: "tf100.mapping.99"),
            Children: new[] {
                new ScadaElement("input1", "Input", ScadaElementKind.InputText,
                    new SceneBounds(5, 6, 80, 24), null,
                    new ScadaElementLayout(ElementPositionMode.Relative, "grp_legacy_val"),
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

            Assert.IsFalse(html.Contains("id=\"ft100-win00008__grp_legacy_val\""),
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

            Assert.IsFalse(html.Contains("data-scada-events="),
                "Legacy EventBindings must not produce data-scada-events in export.");
            Assert.IsFalse(html.Contains("id=\"ft100-win00008__grp_legacy_evt\""),
                "Group with only EventBindings must be flattened.");
            StringAssert.Contains(html, "id=\"ft100-win00008__btn_legacy\"");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task Export_NonGroupWithLegacyEventBindings_DoesNotExportRuntimeEvents()
    {
        var root = CreateTempExportDir();
        var sourceHtmlPath = Path.Combine(root, "source", "nongrp_legacy.html");
        await File.WriteAllTextAsync(sourceHtmlPath,
            "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var button = new ScadaElement(
            "btn_legacy2", "Legacy Button 2", ScadaElementKind.Button,
            new SceneBounds(10, 20, 100, 40),
            null, ScadaElementLayout.Absolute, ScadaElementStyle.DefaultInput,
            new ScadaElementData("Click Me", null, null, null, null, null, null, null, null, false));
        var scene = ScadaScene
            .CreateEmpty("win00008", "NonGroup Legacy", new(400, 400))
            .WithElement(button)
            .WithChangePageEvent("btn_legacy2", ScadaEventRegistry.ClickKey, "win00009");

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(
                scene, sourceHtmlPath, Path.Combine(root, "export"));
            var html = await File.ReadAllTextAsync(result.HtmlPath);

            Assert.IsFalse(html.Contains("data-scada-events="),
                "Non-group element must not emit data-scada-events.");
            var css = await File.ReadAllTextAsync(result.CssPath);
            Assert.IsFalse(css.Contains("data-scada-events"),
                "Exported CSS must not contain data-scada-events selector.");
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
            var decoded = html.Replace("&quot;", "\"");

            StringAssert.Contains(html, "id=\"ft100-win00008__grp_hybrid\"");
            StringAssert.Contains(html, "data-scada-command-config=\"");
            StringAssert.Contains(decoded, "\"kind\":\"navigate\"");
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

            StringAssert.Contains(manifest, "\"Id\": \"grp_man\"");
            StringAssert.Contains(manifest, "\"CommandConfig\":");
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

            StringAssert.Contains(html, "id=\"ft100-win00008__grp_geo\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_geo\"");
            StringAssert.Contains(html, "left:5px");
            StringAssert.Contains(html, "top:6px");
            StringAssert.Contains(html, "width:80px");
            StringAssert.Contains(html, "height:24px");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public void AuthoringWorkflow_StaticCheck_DoesNotExposeLegacyEventDialog()
    {
        // Static file content check: verify that active UI commands do not
        // route to the decommissioned EventBindings authoring path.
        var mainWindowCs = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "src", "ScadaBuilderV2.App", "MainWindow.xaml.cs"));
        var webViewScript = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "src", "ScadaBuilderV2.App", "MainWindow.WebViewScript.cs"));
        var mainWindowXaml = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "src", "ScadaBuilderV2.App", "MainWindow.xaml"));

        // Context menu must not expose an "events" or "element-plus.events" command.
        Assert.IsFalse(webViewScript.Contains("\"object.events\""),
            "Context menu descriptors must not expose 'object.events' command.");
        Assert.IsFalse(webViewScript.Contains("\"element-plus.events\""),
            "Context menu descriptors must not expose 'element-plus.events' command.");

        // Ribbon/XAML must not expose a button or command related to legacy EventBindings authoring.
        // The term 'Events' alone can appear in unrelated contexts, but we check for the decommissioned
        // dialog reference and menu item header.
        Assert.IsFalse(
            mainWindowXaml.Contains("ElementEventDialog") && mainWindowXaml.Contains("Header=\"Ev"),
            "Ribbon/XAML must not expose an EventBindings authoring command.");

        // The executeCommand switch in the JS must not route to legacy event paths.
        Assert.IsFalse(webViewScript.Contains("case 'object.events':"),
            "executeCommand must not route 'object.events'.");
        Assert.IsFalse(webViewScript.Contains("case 'element-plus.events':"),
            "executeCommand must not route 'element-plus.events'.");

        Assert.IsTrue(mainWindowCs.Contains("RejectDecommissionedElementEvents(message.Id)"),
            "The WPF bridge must reject legacy event-dialog messages.");
        Assert.IsFalse(mainWindowCs.Contains("ShowModernElementEvents(message.Id)"),
            "The WPF bridge must not open the legacy event dialog from incoming messages.");
    }

    [TestMethod]
    public async Task ExportAsync_EmitsAdvancedElementStyleInInlineAndCssPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);
        var sourceHtmlPath = Path.Combine(sourceRoot, "style_test.html");
        await File.WriteAllTextAsync(sourceHtmlPath, "<html><body><div class=\"page\"></div></body></html>");
        var style = ScadaElementStyle.DefaultText with
        {
            FontWeight = "Bold",
            FontStyle = "Italic",
            TextDecoration = ["Underline", "LineThrough"],
            TextAlign = "Center",
            TextTransform = "Uppercase",
            LetterSpacing = 1.5,
            LineHeight = 24,
            BorderStyle = "Inset",
            BorderRadius = new ScadaBorderRadius(4, 8, 12, 16),
            Opacity = 0.5,
            Rotation = 45
        };
        var element = new ScadaElement("style-1", "Style", ScadaElementKind.Text,
            new SceneBounds(10, 10, 200, 40), null, ScadaElementLayout.Absolute, style,
            new ScadaElementData("Style", null, null, null, null, null, null, null, null, false));
        var scene = ScadaScene.CreateEmpty("style-test", "Style Test", new(800, 600)).WithElement(element);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var css = await File.ReadAllTextAsync(result.CssPath);

            foreach (var content in new[] { html, css })
            {
                StringAssert.Contains(content, "font-weight:bold");
                StringAssert.Contains(content, "font-style:italic");
                StringAssert.Contains(content, "text-decoration:underline line-through");
                StringAssert.Contains(content, "border-radius:4px 8px 12px 16px");
            }
            StringAssert.Contains(css, "border: 0px inset");
            StringAssert.Contains(css, "opacity:0.5");
            StringAssert.Contains(css, "rotate(45deg)");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = Path.GetDirectoryName(typeof(Ft100SceneExporterTests).Assembly.Location)!;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ScadaBuilderV2.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return dir ?? throw new InvalidOperationException("Cannot find repo root.");
    }
}
