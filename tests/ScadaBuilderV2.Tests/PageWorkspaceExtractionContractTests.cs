namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class PageWorkspaceExtractionContractTests
{
    [TestMethod]
    public void MainWindowDelegatesModernInventoryTabsPersistenceAndExportPreparation()
    {
        var shell = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");
        var controller = ReadProjectFile("src", "ScadaBuilderV2.App", "Pages", "PageWorkspaceController.cs");
        var exportBuilder = ReadProjectFile("src", "ScadaBuilderV2.App", "Pages", "PageExportInputBuilder.cs");

        Assert.IsFalse(shell.Contains("new ScadaSceneReference", StringComparison.Ordinal));
        Assert.IsFalse(shell.Contains("SaveSceneAsync", StringComparison.Ordinal));
        Assert.IsFalse(shell.Contains("ReferenceScadaPage", StringComparison.Ordinal));
        Assert.IsFalse(shell.Contains("_referenceProject.Pages.ToDictionary", StringComparison.Ordinal));
        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(shell, @"HomePageId\s*="));

        StringAssert.Contains(shell, "PagesListBox.ItemsSource = _modernProject.Scenes");
        StringAssert.Contains(shell, "_pageWorkspaceController.OpenAsync");
        StringAssert.Contains(shell, "_pageExportInputBuilder.BuildAsync");
        StringAssert.Contains(controller, "ReadWorkspaceSnapshotAsync");
        StringAssert.Contains(controller, "SaveWorkspaceSnapshotAsync");
        StringAssert.Contains(controller, "ReconcileProjectFromOpenScenes");
        StringAssert.Contains(exportBuilder, "ProjectOverride: project");
        StringAssert.Contains(exportBuilder, "source?.GetSourcePath()");
    }

    [TestMethod]
    public void ModernTabIdentityNeverDependsOnImportedInventory()
    {
        var entry = ReadProjectFile("src", "ScadaBuilderV2.App", "Pages", "PageWorkspaceEntry.cs");
        var tab = ReadProjectFile("src", "ScadaBuilderV2.App", "Workspace", "SceneWorkspaceTab.cs");

        StringAssert.Contains(entry, "ScadaSceneReference Page");
        StringAssert.Contains(entry, "ImportProvenance?");
        StringAssert.Contains(tab, "public Guid PageKey");
        Assert.IsFalse(entry.Contains("ReferenceScadaPage", StringComparison.Ordinal));
        Assert.IsFalse(tab.Contains("ReferenceScadaPage", StringComparison.Ordinal));
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
        return string.Empty;
    }
}
