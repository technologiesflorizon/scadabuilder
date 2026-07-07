using System.IO.Compression;
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
            Assert.IsTrue(File.Exists(Path.Combine(result.ImagesDirectory, "pump.png")));
            Assert.AreEqual(1, result.CopiedImageCount);
            Assert.AreEqual("win00008.html", Path.GetFileName(result.HtmlPath));
            Assert.IsFalse(File.Exists(Path.Combine(result.ExportDirectory, "index.html")));

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            StringAssert.Contains(html, "<link rel=\"stylesheet\" href=\"css/win00008.css\">");
            StringAssert.Contains(html, "src=\"images/pump.png\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__custom_pipe-001\"");
            StringAssert.Contains(html, "data-scada-element-id=\"custom:pipe-001\"");
            Assert.IsFalse(html.Contains("F:\\", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(html.Contains(sourceRoot, StringComparison.OrdinalIgnoreCase));

            var css = await File.ReadAllTextAsync(result.CssPath);
            StringAssert.Contains(html, "class=\"ft100-source-layer\"");
            StringAssert.Contains(css, "#ft100-win00008 .ft100-source-layer .shape-layer");
            StringAssert.Contains(css, "#ft100-win00008 [data-id=\"784\"] { display: none !important; }");
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
            StringAssert.Contains(css, "#ft100-win00003 [data-id=\"8\"] {");
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
            StringAssert.Contains(css, "#ft100-win00002 [data-id=\"1\"] {");
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
            StringAssert.Contains(html, "kept.png");
            Assert.IsFalse(File.Exists(Path.Combine(result.ImagesDirectory, "removed.png")));
            Assert.IsTrue(File.Exists(Path.Combine(result.ImagesDirectory, "kept.png")));
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
            StringAssert.Contains(css, "#ft100-win00008 .ft100-element--Button, #ft100-win00008 [data-scada-events] { cursor: pointer; }");
            StringAssert.Contains(css, "#ft100-win00008 .ft100-element--Button *, #ft100-win00008 [data-scada-events] * { cursor: pointer; }");
            StringAssert.Contains(css, "#ft100-win00008 .ft100-element--Button:active, #ft100-win00008 [data-scada-events]:active { cursor: pointer; }");
            StringAssert.Contains(css, "background: #EAF5F7;");
            StringAssert.Contains(css, "color: #0F2A30;");
            StringAssert.Contains(css, "border-color: #2090A0;");
            AssertExportCssHasNoGlobalRuntimeSelectors(css);

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            StringAssert.Contains(html, "data-scada-events=");
            StringAssert.Contains(html, "action_nav_win00009");
            StringAssert.Contains(html, "<button type=\"button\"");
            StringAssert.Contains(html, "data-scada-button-kind=\"Navigation\"");
            StringAssert.Contains(html, "Suivant");
            StringAssert.Contains(html, "const root = document.getElementById(\"ft100-win00008\");");
            StringAssert.Contains(html, "root.querySelectorAll('[data-scada-events]')");
            StringAssert.Contains(html, "root.id + '__' + sanitizeElementId(elementId)");
            Assert.IsFalse(html.Contains("document.querySelectorAll('[data-scada-events]')", StringComparison.Ordinal));
            StringAssert.Contains(html, "window.location.href = '../' + encodeURIComponent(targetPageId) + '/' + encodeURIComponent(targetPageId) + '.html';");

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
            StringAssert.Contains(manifest, "\"Trigger\": \"click\"");
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
            StringAssert.Contains(html, "root.querySelectorAll('.ft100-element[data-scada-button-kind=\"Toggle\"]:not([data-scada-disabled=\"true\"])')");
            StringAssert.Contains(html, "element.setAttribute('data-scada-toggle-state', nextState);");
            StringAssert.Contains(html, "scada-builder-toggle-state-changed");
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
            StringAssert.Contains(html, "root.querySelectorAll('.ft100-element[data-scada-button-kind]:not([data-scada-disabled=\"true\"])')");
            StringAssert.Contains(html, "scada-builder-button-activated");
            StringAssert.Contains(html, "scada-builder-command-button-activated");
            StringAssert.Contains(html, "scada-builder-navigation-button-activated");
            StringAssert.Contains(html, "scada-builder-alarm-acknowledge-requested");
            StringAssert.Contains(html, "scada-builder-emergency-stop-requested");
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
    public async Task ExportPreservesGroupClickNavigateEventAsRuntimeWrapper()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var exportRoot = Path.Combine(root, "export");
        Directory.CreateDirectory(sourceRoot);
        var sourceHtmlPath = Path.Combine(sourceRoot, "group_navigation.html");
        await File.WriteAllTextAsync(sourceHtmlPath, "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var child = new ScadaElement(
            "btn_child",
            "Bouton enfant",
            ScadaElementKind.Button,
            new SceneBounds(5, 6, 80, 24),
            null,
            new ScadaElementLayout(ElementPositionMode.Relative, "group_nav"),
            ScadaElementStyle.DefaultInput,
            new ScadaElementData("Ouvrir", null, null, null, null, null, null, null, null, false));
        var group = new ScadaElement(
            "group_nav",
            "Groupe navigation",
            ScadaElementKind.Group,
            new SceneBounds(100, 200, 160, 70),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            Children: [child]);
        var scene = ScadaScene
            .CreateEmpty("win00008", "Navigation groupe", new(120, 120))
            .WithElement(group)
            .WithChangePageEvent("group_nav", ScadaEventRegistry.ClickKey, "win00009");

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            var css = await File.ReadAllTextAsync(result.CssPath);
            var manifest = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(result.HtmlPath)!, "manifest.json"));

            StringAssert.Contains(html, "id=\"ft100-win00008__group_nav\"");
            StringAssert.Contains(html, "class=\"ft100-element ft100-element--Group\"");
            StringAssert.Contains(html, "data-scada-events=");
            StringAssert.Contains(html, "action_changepage_click_group_nav_win00009");
            StringAssert.Contains(html, "id=\"ft100-win00008__btn_child\"");
            StringAssert.Contains(html, "left:5px;");
            StringAssert.Contains(html, "top:6px;");
            StringAssert.Contains(html, "background:transparent;");
            StringAssert.Contains(html, "border:0;");

            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__group_nav {");
            StringAssert.Contains(css, "#ft100-win00008 .ft100-element--Button, #ft100-win00008 [data-scada-events] { cursor: pointer; }");
            StringAssert.Contains(css, "left: 100px;");
            StringAssert.Contains(css, "top: 200px;");
            StringAssert.Contains(css, "width: 160px;");
            StringAssert.Contains(css, "height: 70px;");
            StringAssert.Contains(css, "#ft100-win00008 #ft100-win00008__btn_child {");
            StringAssert.Contains(css, "left: 5px;");
            StringAssert.Contains(css, "top: 6px;");

            StringAssert.Contains(manifest, "\"Id\": \"group_nav\"");
            StringAssert.Contains(manifest, "\"Kind\": \"Group\"");
            StringAssert.Contains(manifest, "\"Trigger\": \"click\"");
            StringAssert.Contains(manifest, "\"ActionId\": \"action_changepage_click_group_nav_win00009\"");
            StringAssert.Contains(manifest, "\"Kind\": \"navigate\"");
            StringAssert.Contains(manifest, "\"TargetPageId\": \"win00009\"");
            StringAssert.Contains(manifest, "\"RequiredDisplayWidth\": 260");
            StringAssert.Contains(manifest, "\"RequiredDisplayHeight\": 270");
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
            StringAssert.Contains(html, "scada-builder-read-tag-request");
            StringAssert.Contains(html, "window.scadaBuilderSetTagValue");
            StringAssert.Contains(html, "scada-builder-tag-value");
            StringAssert.Contains(html, "scada-builder-tag-value-applied");
            StringAssert.Contains(html, "readBindingsByTag");
            StringAssert.Contains(html, "writeValueToElement");
            StringAssert.Contains(html, "tf100webScadaBuilder.writeTag");
            StringAssert.Contains(html, "scada-builder-write-value");
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

            StringAssert.Contains(html, "data-scada-events=");
            StringAssert.Contains(html, "function evaluateCondition(condition)");
            StringAssert.Contains(html, "tf100webScadaBuilder.getTagValue");
            StringAssert.Contains(html, "window.scadaBuilderTagValues");
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

            StringAssert.Contains(html, "function evaluateConditionResult(condition)");
            StringAssert.Contains(html, "function evaluateConditionGroup(group)");
            StringAssert.Contains(html, "function evaluateActionConditions(action)");
            StringAssert.Contains(html, "missingPolicy === 'allowaction'");
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

            StringAssert.Contains(html, "window.scadaBuilderRuntime");
            StringAssert.Contains(html, "function dispatchRuntimeEvent(name, detail)");
            StringAssert.Contains(html, "function reportRuntimeError(error, context)");
            StringAssert.Contains(html, "scada-builder-page-ready");
            StringAssert.Contains(html, "scada-builder-action-executed");
            StringAssert.Contains(html, "scada-builder-runtime-error");
            StringAssert.Contains(html, "actionCount: Object.keys(actions).length");
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

            StringAssert.Contains(html, "data-scada-events=");
            StringAssert.Contains(html, "function openPopup(targetPageId, popupOptions)");
            StringAssert.Contains(html, "data-scada-popup-page-id");
            StringAssert.Contains(html, "scada-builder-popup-opened");
            StringAssert.Contains(html, "scada-builder-popup-closed");
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

            StringAssert.Contains(html, "function closePopup(targetPageId)");
            StringAssert.Contains(html, "function togglePopup(targetPageId, popupOptions)");
            StringAssert.Contains(html, "postPopupRequestToParent('closePopup', targetPageId)");
            StringAssert.Contains(html, "postPopupRequestToParent('togglePopup', targetPageId)");
            StringAssert.Contains(html, "detail.action === 'closePopup'");
            StringAssert.Contains(html, "detail.action === 'togglePopup'");
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

            StringAssert.Contains(html, "function normalizePopupOptions(options)");
            StringAssert.Contains(html, "function applyPopupPlacement(overlay, panel, options)");
            StringAssert.Contains(html, "function applyPopupSize(panel, options)");
            StringAssert.Contains(html, "options.AllowMultiple");
            StringAssert.Contains(html, "scadaPopupInstance");
            StringAssert.Contains(html, "getPopupHost(options)");
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
            StringAssert.Contains(html, "if (element.getAttribute('data-scada-disabled') === 'true')");
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
            StringAssert.Contains(html, "<svg id=\"shape-shape_arrow_001\"");
            StringAssert.Contains(html, "marker-end=\"url(#arrow-shape_arrow_001)\"");
            StringAssert.Contains(html, "x1=\"7\"");
            StringAssert.Contains(html, "y1=\"9\"");
            StringAssert.Contains(html, "x2=\"124\"");
            StringAssert.Contains(html, "y2=\"21\"");
            StringAssert.Contains(html, "stroke=\"#90C030\"");
            StringAssert.Contains(html, "stroke-dasharray=\"8 5\"");
            StringAssert.Contains(html, "opacity:0.42;");
            StringAssert.Contains(html, "transform:rotate(17deg) scaleX(1) scaleY(1);");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_circle_001\"");
            StringAssert.Contains(html, "<svg id=\"shape-shape_circle_001\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_triangle_001\"");
            StringAssert.Contains(html, "<svg id=\"shape-shape_triangle_001\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_star_001\"");
            StringAssert.Contains(html, "<svg id=\"shape-shape_star_001\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_lamp_001\"");
            StringAssert.Contains(html, "radialGradient id=\"lamp-gradient-shape_lamp_001\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_bar_001\"");
            StringAssert.Contains(html, "width=\"63.84\"");
            StringAssert.Contains(html, "height=\"24\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_tank_001\"");
            StringAssert.Contains(html, "<svg id=\"shape-shape_tank_001\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_pipe_001\"");
            StringAssert.Contains(html, "<svg id=\"shape-shape_pipe_001\"");
            StringAssert.Contains(html, "id=\"ft100-win00008__shape_pipe_v_001\"");
            StringAssert.Contains(html, "<svg id=\"shape-shape_pipe_v_001\"");
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

            var footerCss = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "win00003", "css", "win00003.css"));
            var homeCss = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "win00008", "css", "win00008.css"));
            var headerCss = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "header_main", "css", "header_main.css"));
            var footerHtml = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "win00003", "win00003.html"));
            var homeHtml = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "win00008", "win00008.html"));
            var headerHtml = await File.ReadAllTextAsync(Path.Combine(result.ExportDirectory, "header_main", "header_main.html"));

            StringAssert.Contains(footerCss, "#ft100-win00003 [data-id=\"1\"] {");
            StringAssert.Contains(homeCss, "#ft100-win00008 [data-id=\"1\"] {");
            StringAssert.Contains(homeCss, "#ft100-win00008 [data-id=\"999\"] { display: none !important; }");
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
}
