using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class PreviewDocumentTests
{
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
}
