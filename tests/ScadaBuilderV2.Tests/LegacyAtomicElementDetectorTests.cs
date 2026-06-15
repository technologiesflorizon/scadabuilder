using ScadaBuilderV2.Domain.Legacy;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.LegacyExtraction;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class LegacyAtomicElementDetectorTests
{
    [TestMethod]
    public void DetectReturnsAbsolutelyPositionedDivLayerCandidate()
    {
        const string html = """
            <html>
              <body>
                <div class="legacy-layer"
                     data-id="layer-101"
                     data-name="Main Pump Layer"
                     data-type="container"
                     style="position: absolute; left: 12px; top: 34px; width: 250px; height: 90px;"></div>
                <div data-id="not-positioned" data-name="Ignored"></div>
              </body>
            </html>
            """;
        var detector = new LegacyAtomicElementDetector();
        var sourceDocument = CreateSourceDocument();

        var candidates = detector.Detect(html, sourceDocument);

        Assert.AreEqual(1, candidates.Count);
        var candidate = candidates.Single();
        Assert.AreEqual("legacy-doc:layer-101", candidate.Id);
        Assert.AreSame(sourceDocument, candidate.SourceDocument);
        Assert.AreEqual("layer-101", candidate.SourceElementId);
        Assert.AreEqual("Main Pump Layer", candidate.SuggestedDisplayName);
        Assert.AreEqual(ScadaElementKind.Container, candidate.SuggestedKind);
        Assert.AreEqual(LegacyExtractionState.Candidate, candidate.State);
        AssertBounds(candidate.SourceBounds, 12, 34, 250, 90);
    }

    [TestMethod]
    public void DetectReturnsSvgRectAndLineCandidates()
    {
        const string html = """
            <svg width="400" height="200">
              <rect data-id="rect-1" data-name="Valve Body" x="20" y="25" width="80" height="30"></rect>
              <line data-id="line-1" data-name="Pipe Segment" x1="100" y1="50" x2="160" y2="75"></line>
              <rect x="1" y="2" width="3" height="4"></rect>
            </svg>
            """;
        var detector = new LegacyAtomicElementDetector();
        var sourceDocument = CreateSourceDocument();

        var candidates = detector.Detect(html, sourceDocument);

        Assert.AreEqual(2, candidates.Count);

        var rect = candidates.Single(candidate => candidate.SourceElementId == "rect-1");
        Assert.AreEqual("Valve Body", rect.SuggestedDisplayName);
        Assert.AreEqual(ScadaElementKind.Shape, rect.SuggestedKind);
        AssertBounds(rect.SourceBounds, 20, 25, 80, 30);

        var line = candidates.Single(candidate => candidate.SourceElementId == "line-1");
        Assert.AreEqual("Pipe Segment", line.SuggestedDisplayName);
        Assert.AreEqual(ScadaElementKind.Shape, line.SuggestedKind);
        AssertBounds(line.SourceBounds, 100, 50, 60, 25);
    }

    private static LegacySourceDocument CreateSourceDocument()
    {
        return new LegacySourceDocument(
            Id: "legacy-doc",
            DisplayName: "Legacy Screen",
            SourceSystem: "GeneratedHtml",
            SourcePath: "screens/legacy.html");
    }

    private static void AssertBounds(
        SceneBounds bounds,
        double x,
        double y,
        double width,
        double height)
    {
        Assert.AreEqual(x, bounds.X);
        Assert.AreEqual(y, bounds.Y);
        Assert.AreEqual(width, bounds.Width);
        Assert.AreEqual(height, bounds.Height);
    }
}
