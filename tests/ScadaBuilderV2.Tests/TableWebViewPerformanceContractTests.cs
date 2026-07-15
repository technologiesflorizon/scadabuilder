using System.Diagnostics;
using ScadaBuilderV2.Application.Tables;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableWebViewPerformanceContractTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void MaximumGridUsesBatchedDomConstructionAndDelegatedCellEvents()
    {
        var source = ReadProjectFile("src", "ScadaBuilderV2.App", "TableEditor", "TableWebViewScript.cs");
        StringAssert.Contains(source, "createDocumentFragment");
        StringAssert.Contains(source, "grid.appendChild(cellFragment)");
        StringAssert.Contains(source, "grid.addEventListener('pointerdown'");
        StringAssert.Contains(source, "grid.addEventListener('pointerover'");
        StringAssert.Contains(source, "let cellSelectionDrag = null;");
        StringAssert.Contains(source, "cellSelectionDrag?.grid!==grid");
        StringAssert.Contains(source, "event.button!==0");
        StringAssert.Contains(source, "document.addEventListener('pointercancel'");
        StringAssert.Contains(source, "context.measureText(placeholder).width");
        StringAssert.Contains(source, "distributeDeficit");
        StringAssert.Contains(source, "deficit*(weights[i]||1)/total");
        StringAssert.Contains(source, "Math.ceil(columnSizes[i]*2)/2");
        Assert.IsFalse(source.Contains("node.addEventListener('pointerdown'", StringComparison.Ordinal));
    }

    [TestMethod]
    public void EditorGuidesDoNotCoverCellHitTargetsAndHeaderScopesShareSelectionRendering()
    {
        var source = ReadProjectFile("src", "ScadaBuilderV2.App", "TableEditor", "TableWebViewScript.cs");

        StringAssert.Contains(source, ".scada-editor-table { display:grid; width:100%; height:100%; overflow:visible; }");
        StringAssert.Contains(source, ".scada-editor-table-header.column { top:-18px; height:18px; }");
        StringAssert.Contains(source, ".scada-editor-table-header.row { left:-24px; width:24px; }");
        StringAssert.Contains(source, ".scada-editor-table-corner { left:-24px; top:-18px;");
        StringAssert.Contains(source, "const selectRange = (start, end, scope='cells')");
        StringAssert.Contains(source, "selectRange(anchor,{row:index,column:(table.Columns||[]).length-1},'row')");
        StringAssert.Contains(source, "selectRange(anchor,{row:(table.Rows||[]).length-1,column:index},'column')");
    }

    [TestMethod]
    public void MaximumGridDomainEditsStayWithinInteractiveSafetyBudget()
    {
        var table = ScadaTableDefinition.CreateDefault(64, 64, firstRowIsHeader: true);
        var stopwatch = Stopwatch.StartNew();
        for (var index = 0; index < 100; index++)
        {
            var column = index % 64;
            table = ScadaTableTrackOperations.SetColumnWidth(table, [column], 96 + index % 5);
            table = ScadaTableFormatOperations.ApplyFormat(table, new(ScadaTableFormatScopeKind.Cells, new(index % 64, column, index % 64, column)), new(Background: "#F6FAFB"));
        }
        stopwatch.Stop();

        Assert.AreEqual(4096, table.EffectiveCells.Count);
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"64x64 domain edits took {stopwatch.Elapsed}.");
    }

    [TestMethod]
    public void MaximumGridRendererSelectionAndResizeMeasurementsAreRecorded()
    {
        var table = ScadaTableDefinition.CreateDefault(64, 64, firstRowIsHeader: true);
        var element = ScadaElement.CreateTable("table_perf", "Performance", 0, 0, 64, 64) with { Table = table };
        var scene = ScadaScene.CreateEmpty("table-performance", "Table performance", new(8000, 4000)).WithElement(element);
        var page = new ScadaSceneReference(scene.Id, scene.Title, $"scenes/{scene.Id}.scene.json", CanvasSize: scene.CanvasSize, Origin: PageOrigin.Native);

        var stopwatch = Stopwatch.StartNew();
        var document = NativePageDocumentFactory.Create(new PageDocumentInput(page, scene));
        stopwatch.Stop();
        var initialRenderMs = stopwatch.Elapsed.TotalMilliseconds;

        var selectionSamples = new List<double>();
        var resizeSamples = new List<double>();
        for (var index = 0; index < 100; index++)
        {
            stopwatch.Restart();
            _ = TablePropertiesInspector.Inspect(table, new(
                ScadaTableFormatScopeKind.Cells,
                new(index % 48, index % 48, index % 48 + 15, index % 48 + 15)));
            stopwatch.Stop();
            selectionSamples.Add(stopwatch.Elapsed.TotalMilliseconds);

            stopwatch.Restart();
            _ = ScadaTableTrackOperations.SetColumnWidth(table, [index % 64], 96 + index % 5);
            stopwatch.Stop();
            resizeSamples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        var selectionP95 = Percentile95(selectionSamples);
        var resizeP95 = Percentile95(resizeSamples);
        TestContext.WriteLine($"64x64 renderer initial={initialRenderMs:0.###} ms; selection inspector p95={selectionP95:0.###} ms; resize p95={resizeP95:0.###} ms; html={document.Html.Length} chars.");
        Assert.IsTrue(initialRenderMs < 5000, $"Initial 64x64 rendering took {initialRenderMs:0.###} ms.");
        Assert.IsTrue(selectionP95 < 250, $"Selection inspection p95 was {selectionP95:0.###} ms.");
        Assert.IsTrue(resizeP95 < 100, $"Resize p95 was {resizeP95:0.###} ms.");
    }

    private static double Percentile95(IReadOnlyList<double> samples) =>
        samples.Order().ElementAt((int)Math.Ceiling(samples.Count * 0.95) - 1);

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }
        Assert.Fail($"Unable to locate project file: {Path.Combine(parts)}");
        return "";
    }
}
