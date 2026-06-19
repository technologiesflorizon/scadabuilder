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
}
