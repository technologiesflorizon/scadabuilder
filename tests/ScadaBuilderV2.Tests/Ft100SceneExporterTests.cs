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
            StringAssert.Contains(html, "id=\"custom_pipe-001\"");
            Assert.IsFalse(html.Contains("F:\\", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(html.Contains(sourceRoot, StringComparison.OrdinalIgnoreCase));

            var css = await File.ReadAllTextAsync(result.CssPath);
            StringAssert.Contains(html, "class=\"ft100-source-layer\"");
            StringAssert.Contains(css, ".ft100-source-layer .shape-layer");
            StringAssert.Contains(css, "[data-id=\"784\"] { display: none !important; }");
            StringAssert.Contains(css, "#custom_pipe-001");
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
    <button class="layer" data-id="8">Button8</button>
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
        var scene = ScadaScene
            .CreateEmpty("win00003", "Navigation", new(1280, 120))
            .WithElement(button)
            .WithLegacyElementsMaterialized();

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, exportRoot);

            var css = await File.ReadAllTextAsync(result.CssPath);
            StringAssert.Contains(css, "[data-id=\"8\"] {");
            StringAssert.Contains(css, "left: 1144px !important;");
            StringAssert.Contains(css, "top: 14px !important;");
            StringAssert.Contains(css, "width: 138px;");
            StringAssert.Contains(css, "height: 72px;");
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
            StringAssert.Contains(html, "id=\"modern_text_1\"");
            Assert.IsFalse(html.Contains("ft100-element--LegacyStatic", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("id=\"legacy_1\"", StringComparison.Ordinal));

            var css = await File.ReadAllTextAsync(result.CssPath);
            StringAssert.Contains(css, "[data-id=\"1\"] {");
            StringAssert.Contains(css, "left: 12px !important;");
            StringAssert.Contains(css, "#modern_text_1");

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
            new ScadaElementData("Suivant", null, null, null, null, null, null, null, null, false));
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
            StringAssert.Contains(css, "--ft100-scada-width: 1440px;");
            StringAssert.Contains(css, "--ft100-scada-height: 900px;");
            StringAssert.Contains(css, "background-color: #123456;");
            StringAssert.Contains(css, "background-size: contain;");
            StringAssert.Contains(css, "background-repeat: repeat-x;");

            var html = await File.ReadAllTextAsync(result.HtmlPath);
            StringAssert.Contains(html, "data-scada-events=");
            StringAssert.Contains(html, "action_nav_win00009");
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
}
