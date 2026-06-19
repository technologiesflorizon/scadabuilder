using ScadaBuilderV2.Application.Commands;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class RibbonCommandCatalogTests
{
    [TestMethod]
    public void DefaultCatalogDefinesExpectedTopRibbonTabs()
    {
        var tabs = RibbonCommandCatalog.CreateDefault();

        CollectionAssert.AreEqual(
            new[] { "File", "Edit", "Screen", "Selection", "Tools", "Insert" },
            tabs.Keys.ToArray());
    }

    [TestMethod]
    public void DefaultCatalogUsesStableUniqueCommandIds()
    {
        var commands = RibbonCommandCatalog.EnumerateCommands(RibbonCommandCatalog.CreateDefault()).ToArray();
        var duplicateIds = commands
            .GroupBy(command => command.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.AreEqual(0, duplicateIds.Length, $"Duplicate ribbon command ids: {string.Join(", ", duplicateIds)}");
        CollectionAssert.Contains(commands.Select(command => command.Id).ToArray(), "project.save");
        CollectionAssert.Contains(commands.Select(command => command.Id).ToArray(), "export.ft100.sb2");
        CollectionAssert.Contains(commands.Select(command => command.Id).ToArray(), "insert.button.emergency-stop");
    }

    [TestMethod]
    public void DefaultCatalogRequiresSemanticIconKeys()
    {
        var commands = RibbonCommandCatalog.EnumerateCommands(RibbonCommandCatalog.CreateDefault()).ToArray();
        var invalidIconKeys = commands
            .Where(command => string.IsNullOrWhiteSpace(command.IconKey) || !command.IconKey.StartsWith("Icon.", StringComparison.Ordinal))
            .Select(command => $"{command.Id}:{command.IconKey}")
            .ToArray();

        Assert.AreEqual(0, invalidIconKeys.Length, $"Invalid ribbon icon keys: {string.Join(", ", invalidIconKeys)}");
    }

    [TestMethod]
    public void DisabledCommandsExposeReason()
    {
        var disabledCommands = RibbonCommandCatalog
            .EnumerateCommands(RibbonCommandCatalog.CreateDefault())
            .Where(command => !command.IsEnabled)
            .ToArray();

        Assert.IsTrue(disabledCommands.Length > 0, "The catalog should keep visible future commands explicit.");
        Assert.IsTrue(disabledCommands.All(command => !string.IsNullOrWhiteSpace(command.DisabledReason)));
    }

    [TestMethod]
    public void SelectionGroupCommandsAreExecutableFromRibbonCatalog()
    {
        var commands = RibbonCommandCatalog
            .EnumerateCommands(RibbonCommandCatalog.CreateDefault())
            .ToDictionary(command => command.Id, StringComparer.Ordinal);

        Assert.IsTrue(commands["object.group"].IsEnabled);
        Assert.IsTrue(commands["object.ungroup"].IsEnabled);
        Assert.IsNull(commands["object.group"].DisabledReason);
        Assert.IsNull(commands["object.ungroup"].DisabledReason);
    }

    [TestMethod]
    public void MainRibbonUsesOnlyDynamicCommandSurface()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml");
        var code = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");

        StringAssert.Contains(xaml, "x:Name=\"RibbonCommandSurface\"");
        StringAssert.Contains(xaml, "ItemTemplate=\"{StaticResource RibbonGroupTemplate}\"");
        Assert.IsFalse(xaml.Contains("x:Name=\"FileRibbon\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("x:Name=\"EditRibbon\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("x:Name=\"InsertRibbon\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("x:Name=\"ScreenRibbon\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("x:Name=\"SelectionRibbon\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("x:Name=\"ToolsRibbon\"", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("FileRibbon.Visibility", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("EditRibbon.Visibility", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("InsertRibbon.Visibility", StringComparison.Ordinal));
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate project file: {Path.Combine(parts)}");
        return "";
    }
}
