namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableWebViewMessageAdapterTests
{
    [TestMethod]
    public void AdapterOwnsAllTypedSchemasAndReturnsDiagnosticsForInvalidMessages()
    {
        var source = ReadProjectFile("src", "ScadaBuilderV2.App", "TableEditor", "TableWebViewMessageAdapter.cs");
        StringAssert.Contains(source, "TryParse(string json, out ITableWebViewRequest? request, out string? error)");
        StringAssert.Contains(source, "tableSelection");
        StringAssert.Contains(source, "tableCellEdit");
        StringAssert.Contains(source, "tableTrackResize");
        StringAssert.Contains(source, "tableAutoFitMeasured");
        StringAssert.Contains(source, "The table selection range is not normalized.");
        StringAssert.Contains(source, "The table content kind is invalid.");
        StringAssert.Contains(source, "Invalid table bridge message:");
        StringAssert.Contains(source, "double.IsFinite");
    }

    [TestMethod]
    public void RawTableJsonStopsAtTheSingleAdapterBoundary()
    {
        var integration = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.TableIntegration.cs");
        var main = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");
        StringAssert.Contains(integration, "TableWebViewMessageAdapter.TryParse(json, out var request, out var error)");
        StringAssert.Contains(integration, "SetStatus(error");
        Assert.IsFalse(main.Contains("JsonSerializer.Deserialize<TableSelectionRequest>", StringComparison.Ordinal));
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
