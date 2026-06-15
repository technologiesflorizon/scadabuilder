using System.Text.Json;
using ScadaBuilderV2.Domain.Legacy;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ReferenceProjectModelTests
{
    [TestMethod]
    public void ReferenceProjectDeserializesReadOnlyPagePathsAndPageIds()
    {
        const string json = """
            {
              "name": "AMR_REF_SCADA",
              "version": "0.1.0",
              "target": {
                "platform": "tf100-web",
                "basePath": "/",
                "offline": true
              },
              "theme": "amr_default",
              "pages": [
                "pages/win00002.json",
                "pages/win00008.json",
                "pages/nested/win00096.json"
              ],
              "components": [],
              "assets": {
                "paths": [
                  "assets",
                  "08_web_modernized/html_pages"
                ]
              },
              "imports": {
                "updated_web_root": "08_web_modernized",
                "updated_pages_root": "08_web_modernized/html_pages",
                "updated_pages_pattern": "win*_updated.html",
                "source_reference": "F:/MetroSearch/SCADA_AMR_GROUP/08_web_modernized",
                "imported_at": "2026-04-20",
                "updated_imported_count": 53
              },
              "build": {
                "minify": true,
                "sourcemaps": false
              }
            }
            """;

        var project = JsonSerializer.Deserialize<ReferenceProject>(json);

        Assert.IsNotNull(project);
        Assert.AreEqual("AMR_REF_SCADA", project.Name);
        Assert.AreEqual("tf100-web", project.Target?.Platform);
        CollectionAssert.AreEqual(
            new[] { "pages/win00002.json", "pages/win00008.json", "pages/nested/win00096.json" },
            project.PagePaths.ToArray());
        CollectionAssert.AreEqual(new[] { "win00002", "win00008", "win00096" }, project.PageIds.ToArray());
        CollectionAssert.AreEqual(new[] { "assets", "08_web_modernized/html_pages" }, project.Assets.Paths.ToArray());
        Assert.AreEqual(new DateOnly(2026, 4, 20), project.Imports?.ImportedAt);
        Assert.IsTrue(project.Build?.Minify);

        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)project.PagePaths).Add("pages/win99999.json"));
    }

    [TestMethod]
    public void ReferencePageDeserializesLegacyInventorySummary()
    {
        const string json = """
            {
              "id": "win00008",
              "title": "win00008",
              "size": {
                "width": 1280,
                "height": 873
              },
              "background": "rgba(0,0,0,1)",
              "layers": [
                {
                  "id": "legacy_embed",
                  "type": "legacy_embed",
                  "x": 0,
                  "y": 0,
                  "width": 1280,
                  "height": 873,
                  "src": "../assets/html_pages/win00008_updated.html",
                  "sandbox": "allow-scripts allow-same-origin",
                  "scrolling": "auto"
                }
              ],
              "bindings": [],
              "navigation": [],
              "layout": {
                "mode": "fixed"
              },
              "legacy": {
                "source_html": "08_web_modernized/html_pages/win00008_updated.html",
                "inventory": {
                  "summary": {
                    "shape_layers": 1,
                    "scripts": 2,
                    "layers_total": 51,
                    "type_Image": 11,
                    "type_Text": 40,
                    "svg_shapes_total": 542
                  }
                }
              }
            }
            """;

        var page = JsonSerializer.Deserialize<ReferencePage>(json);

        Assert.IsNotNull(page);
        Assert.AreEqual("win00008", page.Id);
        Assert.AreEqual(1280, page.Size.Width);
        Assert.AreEqual("legacy_embed", page.Layers[0].Id);
        Assert.AreEqual("fixed", page.Layout?.Mode);
        Assert.AreEqual("08_web_modernized/html_pages/win00008_updated.html", page.Legacy?.SourceHtml);

        var summary = page.Legacy?.Inventory?.Summary;
        Assert.IsNotNull(summary);
        Assert.AreEqual(1, summary.ShapeLayers);
        Assert.AreEqual(2, summary.Scripts);
        Assert.AreEqual(51, summary.LayersTotal);
        Assert.AreEqual(11, summary.TypeImage);
        Assert.AreEqual(40, summary.TypeText);
        Assert.AreEqual(542, summary.SvgShapesTotal);
    }
}
