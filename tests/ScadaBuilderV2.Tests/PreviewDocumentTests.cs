using ScadaBuilderV2.Rendering;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Tests.RuntimeContracts;
using System.Text.RegularExpressions;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class PreviewDocumentTests
{
    [TestMethod]
    public async Task ConformancePreviewAndExportUseTheSameModelMarkupWithoutEditorArtifacts()
    {
        var model = ScadaV2RuntimeConformanceProjectFactory.Create();
        var page = model.Project.Scenes.Single(item => item.Id == ScadaV2RuntimeConformanceProjectFactory.MainPageId);
        var scene = model.Pages.Single(item => item.Scene.Id == page.Id).Scene;
        var preview = NativePageDocumentFactory.Create(new PageDocumentInput(page, scene), model.Project.TagCatalog);
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var exported = await new Ft100SceneExporter().ExportAsync(scene, null, root, model.Project);
            var exportHtml = await File.ReadAllTextAsync(exported.HtmlPath);
            Assert.AreEqual(CanonicalMarkup(preview.Html), CanonicalMarkup(exportHtml));

            foreach (var elementId in Flatten(scene.Elements)
                         .Where(element => element.Kind != ScadaElementKind.Group)
                         .Select(element => element.Id))
            {
                var scopedId = $"ft100-{scene.Id}__{elementId.Replace(':', '_')}";
                StringAssert.Contains(preview.Html, scopedId, $"Preview is missing model object '{elementId}'.");
                StringAssert.Contains(exportHtml, scopedId, $"Export is missing model object '{elementId}'.");
            }

            foreach (var forbidden in new[] { "selection-overlay", "drag-rectangle", "data-editor-locked", "scada-editor-" })
            {
                Assert.IsFalse(preview.Html.Contains(forbidden, StringComparison.OrdinalIgnoreCase), forbidden);
                Assert.IsFalse(exportHtml.Contains(forbidden, StringComparison.OrdinalIgnoreCase), forbidden);
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void GetSourcePathCombinesPreviewRootAndRelativeHtmlSource()
    {
        var document = new PreviewDocument(
            pageId: "scene-win00008",
            title: "win00008 preview",
            relativeHtmlSource: Path.Combine("preview", "win00008.html"));
        var previewRoot = Path.Combine(Path.GetTempPath(), "scada-preview");

        var sourcePath = document.GetSourcePath(previewRoot);

        Assert.AreEqual(Path.GetFullPath(Path.Combine(previewRoot, "preview", "win00008.html")), sourcePath);
    }

    [TestMethod]
    public void GetSourceUriReturnsAbsoluteFileUri()
    {
        var document = new PreviewDocument("scene-win00009", "win00009 preview", Path.Combine("preview", "win00009.html"));
        var previewRoot = Path.Combine(Path.GetTempPath(), "scada-preview");

        var sourceUri = document.GetSourceUri(previewRoot);

        Assert.IsTrue(sourceUri.IsAbsoluteUri);
        Assert.AreEqual(Uri.UriSchemeFile, sourceUri.Scheme);
        Assert.AreEqual(document.GetSourcePath(previewRoot), sourceUri.LocalPath);
    }

    [TestMethod]
    public void ConstructorRejectsAbsoluteHtmlSource()
    {
        var absoluteSource = Path.Combine(Path.GetTempPath(), "preview", "win00008.html");

        Assert.ThrowsException<ArgumentException>(
            () => new PreviewDocument("scene-win00008", "win00008 preview", absoluteSource));
    }

    [TestMethod]
    public void GetSourcePathRejectsTraversalOutsidePreviewRoot()
    {
        var document = new PreviewDocument("scene-win00008", "win00008 preview", Path.Combine("..", "win00008.html"));
        var previewRoot = Path.Combine(Path.GetTempPath(), "scada-preview");

        Assert.ThrowsException<InvalidOperationException>(() => document.GetSourcePath(previewRoot));
    }

    [TestMethod]
    public void PageSourceProjectionRejectsTraversalOutsideImportedRoot()
    {
        var projection = new PageSourceProjection(
            Path.Combine(Path.GetTempPath(), "imported-page"),
            Path.Combine("..", "outside.html"),
            "Wonderware");

        Assert.ThrowsException<InvalidOperationException>(() => projection.GetSourcePath());
    }

    [TestMethod]
    public async Task PreviewRendererPreservesNumericCellAttributesWithoutBindingOrEditorArtifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var table = ScadaElement.CreateTable("table:numeric", "Tableau", 10, 20, 2, 2);
        table = table with
        {
            Table = table.Table! with
            {
                Cells = table.Table.EffectiveCells.Select(cell => cell.Row == 1 && cell.Column == 1
                    ? cell with
                    {
                        Content = new ScadaTableCellContent(
                            ScadaTableCellContentKind.InputNumeric,
                            Placeholder: "Pression",
                            NumericValue: 12.5,
                            Minimum: 0,
                            Maximum: 100,
                            Step: 0.5,
                            IsReadOnly: true,
                            DisplayFormat: "##.#"),
                        ValueBindings = new ScadaTableCellValueBindings("secret.read", "secret.write")
                    }
                    : cell).ToArray()
            }
        };
        var scene = ScadaScene.CreateEmpty("preview-table", "Preview", new(640, 480)).WithElement(table);

        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, null, root);
            var html = await File.ReadAllTextAsync(result.HtmlPath);
            StringAssert.Contains(html, "ft100-preview-table__table_numeric__cell-1-1");
            StringAssert.Contains(html, "type=\"number\"");
            StringAssert.Contains(html, "value=\"12.5\"");
            StringAssert.Contains(html, "placeholder=\"Pression\"");
            StringAssert.Contains(html, "min=\"0\"");
            StringAssert.Contains(html, "max=\"100\"");
            StringAssert.Contains(html, "step=\"0.5\"");
            StringAssert.Contains(html, "readonly");
            StringAssert.Contains(html, "data-scada-display-format=\"##.#\"");
            Assert.IsFalse(html.Contains("secret.read", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("secret.write", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("scada-editor-table-header", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static string CanonicalMarkup(string html)
    {
        var canonical = Regex.Replace(
            html.TrimStart('\uFEFF'),
            @"\s*<script src=""\.\./scada-runtime\.[a-f0-9]+\.js""(?: defer)?></script>",
            string.Empty);
        canonical = Regex.Replace(
            canonical,
            @"css/conformance-main(?:\.[a-f0-9]+)?\.css",
            "css/conformance-main.css");
        canonical = canonical.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Regex.Replace(canonical, @">\s+<", "><").Trim();
    }

    private static IEnumerable<ScadaElement> Flatten(IEnumerable<ScadaElement> elements)
    {
        foreach (var element in elements)
        {
            yield return element;
            foreach (var child in Flatten(element.Children ?? [])) yield return child;
        }
    }
}
