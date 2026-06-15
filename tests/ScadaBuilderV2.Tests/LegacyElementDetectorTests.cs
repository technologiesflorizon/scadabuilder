using ScadaBuilderV2.Domain.Legacy;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.LegacyExtraction;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class LegacyElementDetectorTests
{
    [TestMethod]
    public void DetectAtomicElementsReadsAbsolutelyPositionedLayer()
    {
        var detector = new LegacyElementDetector();
        var source = new LegacySourceDocument("win00007", "win00007 raw", "Wonderware.ArchestrA", "03_web_legacy/html_pages/win00007.html");
        const string html = """
            <div class="layer" data-type="Text" data-name="Text29" data-id="795"
                 style="position:absolute; left:46px; top:151px; width:68px; height:21px;">Consigne</div>
            """;

        var candidates = detector.DetectAtomicElements(source, html);

        Assert.AreEqual(1, candidates.Count);
        Assert.AreEqual("795", candidates[0].SourceElementId);
        Assert.AreEqual("Text29", candidates[0].SuggestedDisplayName);
        Assert.AreEqual(ScadaElementKind.Text, candidates[0].SuggestedKind);
        Assert.AreEqual(46, candidates[0].SourceBounds.X);
        Assert.AreEqual(151, candidates[0].SourceBounds.Y);
        Assert.AreEqual(68, candidates[0].SourceBounds.Width);
        Assert.AreEqual(21, candidates[0].SourceBounds.Height);
    }

    [TestMethod]
    public void DetectAtomicElementsReadsSvgRectAndLineWithoutGrouping()
    {
        var detector = new LegacyElementDetector();
        var source = new LegacySourceDocument("win00007", "win00007 raw", "Wonderware.ArchestrA", "03_web_legacy/html_pages/win00007.html");
        const string html = """
            <svg>
              <rect x="41" y="284" width="119" height="30" fill="red" data-name="Rectangle1" data-id="46" />
              <line x1="158.0" y1="284.0" x2="158.0" y2="315.0" data-name="Line46" data-id="49" />
            </svg>
            """;

        var candidates = detector.DetectAtomicElements(source, html);

        Assert.AreEqual(2, candidates.Count);
        Assert.AreEqual("46", candidates[0].SourceElementId);
        Assert.AreEqual(ScadaElementKind.Shape, candidates[0].SuggestedKind);
        Assert.AreEqual(41, candidates[0].SourceBounds.X);
        Assert.AreEqual("49", candidates[1].SourceElementId);
        Assert.AreEqual(1, candidates[1].SourceBounds.Width);
        Assert.AreEqual(31, candidates[1].SourceBounds.Height);
    }
}
