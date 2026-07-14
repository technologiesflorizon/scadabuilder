using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class NativePageDocumentTests
{
    [TestMethod]
    public void FactoryBuildsBlankNativePageWithElementPlusAndNoEditorArtifacts()
    {
        var pageKey = Guid.NewGuid();
        var page = new ScadaSceneReference(
            "legacy-alias",
            "Native overview",
            $"scenes/{pageKey:N}.scene.json",
            CanvasSize: new CanvasSize(960, 540),
            Background: SceneBackgroundStyle.FromColor("#123456"),
            IncludeInBuild: false,
            PageKey: pageKey,
            PageCode: "native_overview",
            Origin: PageOrigin.Native);
        var scene = ScadaScene.CreateEmpty("legacy-alias", "Native overview", new(960, 540)) with
        {
            PageKey = pageKey,
            PageCode = "native_overview",
            Origin = PageOrigin.Native,
            Elements = [ScadaElement.CreateText("title", "Title", 20, 30)]
        };

        var document = NativePageDocumentFactory.Create(new PageDocumentInput(page, scene));

        StringAssert.Contains(document.Html, "id=\"ft100-native_overview\"");
        StringAssert.Contains(document.Html, "data-scada-width=\"960\"");
        StringAssert.Contains(document.Html, "data-scada-height=\"540\"");
        StringAssert.Contains(document.Html, "background-color:#123456");
        StringAssert.Contains(document.Html, "class=\"ft100-source-layer\"");
        StringAssert.Contains(document.Html, "class=\"ft100-elementplus-layer\"");
        StringAssert.Contains(document.Html, "id=\"ft100-native_overview__title\"");
        StringAssert.Contains(document.Css, "#ft100-native_overview");
        Assert.IsFalse(document.Html.Contains("scada-runtime", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(document.Html.Contains("selection-overlay", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(document.Html.Contains("workzone", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(document.Html.Contains(pageKey.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task PreviewMaterializesNativeHtmlAndNamespacedCss()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var pageKey = Guid.NewGuid();
        var page = new ScadaSceneReference(
            "native_page",
            "Native",
            $"scenes/{pageKey:N}.scene.json",
            PageKey: pageKey,
            PageCode: "native_page",
            Origin: PageOrigin.Native);
        var scene = ScadaScene.CreateEmpty("native_page", "Native", CanvasSize.DefaultDesktop) with
        {
            PageKey = pageKey,
            PageCode = "native_page",
            Origin = PageOrigin.Native
        };

        try
        {
            var preview = await PreviewDocument.MaterializeNativeAsync(
                new PageDocumentInput(page, scene),
                root);

            var htmlPath = preview.GetSourcePath(root);
            Assert.IsTrue(File.Exists(htmlPath));
            Assert.IsTrue(File.Exists(Path.Combine(Path.GetDirectoryName(htmlPath)!, "css", "native_page.css")));
            Assert.AreEqual("native_page", preview.PageId);
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
    public void ImportedPageCannotBeSilentlyRenderedAsNative()
    {
        var page = new ScadaSceneReference(
            "win00009",
            "Imported",
            "scenes/win00009.scene.json",
            PageKey: Guid.NewGuid(),
            PageCode: "win00009",
            Origin: PageOrigin.Imported,
            ImportProvenance: new ImportProvenance("Wonderware", SourcePath: "legacy/win00009.html"));
        var scene = ScadaScene.CreateEmpty("win00009", "Imported", CanvasSize.DefaultDesktop) with
        {
            PageKey = page.PageKey,
            PageCode = "win00009",
            Origin = PageOrigin.Imported,
            ImportProvenance = page.ImportProvenance
        };

        Assert.ThrowsException<InvalidOperationException>(() =>
            NativePageDocumentFactory.Create(new PageDocumentInput(page, scene)));
    }
}
