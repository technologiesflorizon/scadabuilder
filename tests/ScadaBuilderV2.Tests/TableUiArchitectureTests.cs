using ScadaBuilderV2.Application.Tables;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class TableUiArchitectureTests
{
    [TestMethod]
    public void ContextMenuExposesSpreadsheetOperationsAndCorrectEnablement()
    {
        var table = ScadaTableDefinition.CreateDefault(3, 3);
        var commands = TableContextMenuProvider.Build(table, new ScadaTableRange(0, 0, 1, 1), canPaste: false);
        var flat = Flatten(commands).ToDictionary(command => command.Id, StringComparer.Ordinal);

        Assert.IsFalse(flat["table.paste"].IsEnabled);
        Assert.IsTrue(flat["table.merge"].IsEnabled);
        Assert.IsFalse(flat["table.unmerge"].IsEnabled);
        CollectionAssert.IsSubsetOf(
            new[] { "table.copy", "table.paste", "table.row.insert", "table.column.insert", "table.row.delete", "table.column.delete", "table.clear", "table.format", "table.row.height", "table.column.width", "table.merge", "table.unmerge" },
            flat.Keys.ToArray());
    }

    [TestMethod]
    public void TableUiLogicIsSplitFromMainWindowAndInsertDispatchIsCatalogDriven()
    {
        var main = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");
        var web = ReadProjectFile("src", "ScadaBuilderV2.App", "TableEditor", "TableWebViewScript.cs");
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml");

        StringAssert.Contains(main, "InsertToolCatalog.Find(commandId)");
        Assert.IsFalse(main.Contains("case \"insert.shape.", StringComparison.Ordinal));
        Assert.IsFalse(main.Contains("case \"insert.hmi.", StringComparison.Ordinal));
        Assert.IsFalse(main.Contains("case \"insert.button.", StringComparison.Ordinal));
        Assert.IsFalse(main.Contains("ScadaTableOperations.", StringComparison.Ordinal));
        StringAssert.Contains(web, "tableTrackResize");
        StringAssert.Contains(web, "tableCellEdit");
        StringAssert.Contains(xaml, "x:Name=\"InsertFamilySurface\"");
        StringAssert.Contains(xaml, "Header=\"Tableau\"");
    }

    private static IEnumerable<ScadaBuilderV2.Application.Commands.EditorCommandDescriptor> Flatten(
        IEnumerable<ScadaBuilderV2.Application.Commands.EditorCommandDescriptor> commands) =>
        commands.SelectMany(command => new[] { command }.Concat(command.Children is null ? [] : Flatten(command.Children)));

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
