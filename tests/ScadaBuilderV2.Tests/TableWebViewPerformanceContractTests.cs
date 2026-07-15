using System.Diagnostics;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableWebViewPerformanceContractTests
{
    [TestMethod]
    public void MaximumGridUsesBatchedDomConstructionAndDelegatedCellEvents()
    {
        var source = ReadProjectFile("src", "ScadaBuilderV2.App", "TableEditor", "TableWebViewScript.cs");
        StringAssert.Contains(source, "createDocumentFragment");
        StringAssert.Contains(source, "grid.appendChild(cellFragment)");
        StringAssert.Contains(source, "grid.addEventListener('pointerdown'");
        StringAssert.Contains(source, "grid.addEventListener('pointerover'");
        StringAssert.Contains(source, "context.measureText(placeholder).width");
        StringAssert.Contains(source, "distributeDeficit");
        StringAssert.Contains(source, "deficit*(weights[i]||1)/total");
        StringAssert.Contains(source, "Math.ceil(columnSizes[i]*2)/2");
        Assert.IsFalse(source.Contains("node.addEventListener('pointerdown'", StringComparison.Ordinal));
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
