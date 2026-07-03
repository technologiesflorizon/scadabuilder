namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ConfigurationWindowContractTests
{
    [TestMethod]
    public void ConfigurationWindowXamlExposesLibraryTabControls()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "ConfigurationWindow.xaml");

        StringAssert.Contains(xaml, "Header=\"Librairie\"");
        StringAssert.Contains(xaml, "x:Name=\"LibraryListView\"");
        StringAssert.Contains(xaml, "x:Name=\"AddLibraryButton\"");
        StringAssert.Contains(xaml, "x:Name=\"RenameLibraryButton\"");
        StringAssert.Contains(xaml, "x:Name=\"ChangePathLibraryButton\"");
        StringAssert.Contains(xaml, "x:Name=\"RemoveLibraryButton\"");
    }

    [TestMethod]
    public void ConfigurationWindowCodeGuardsDefaultEntryAndUsesRegistryMutations()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.App", "ConfigurationWindow.xaml.cs");

        StringAssert.Contains(code, "selected.IsDefault");
        StringAssert.Contains(code, "Registry.Add(");
        StringAssert.Contains(code, "Registry.Rename(");
        StringAssert.Contains(code, "Registry.UpdatePath(");
        StringAssert.Contains(code, "Registry.Remove(");
    }

    [TestMethod]
    public void MainWindowOpensConfigurationWindowFromToolSettingsCommand()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");

        StringAssert.Contains(code, "case \"tool.settings\":");
        StringAssert.Contains(code, "OpenConfigurationWindowAsync");
    }

    [TestMethod]
    public void MainWindowXamlReplacesLibraryLabelWithSelectorComboBox()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml");

        StringAssert.Contains(xaml, "x:Name=\"LibrarySelectorComboBox\"");
        StringAssert.Contains(xaml, "SelectionChanged=\"OnLibrarySelectorSelectionChanged\"");
        Assert.IsFalse(xaml.Contains("Text=\"Element+ disponibles\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MainWindowUsesActiveLibraryRootForRefreshAndWatcher()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");

        StringAssert.Contains(code, "private string? ResolveActiveLibraryRoot(bool create)");
        StringAssert.Contains(code, "private async Task RefreshLibrarySelectorAsync()");
        StringAssert.Contains(code, "OnLibrarySelectorSelectionChanged");
    }

    [TestMethod]
    public void ElementStudioXamlReplacesSaveAsSepButtonWithSplitButton()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml");

        StringAssert.Contains(xaml, "x:Name=\"AddToLibraryButton\"");
        StringAssert.Contains(xaml, "x:Name=\"AddToLibraryArrowButton\"");
        StringAssert.Contains(xaml, "Click=\"OnAddToLibraryArrowClick\"");
        Assert.IsFalse(xaml.Contains("Content=\"Save as .sep\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ElementStudioCodeSavesDirectlyToChosenLibraryFromArrowMenu()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml.cs");

        StringAssert.Contains(code, "OnAddToLibraryArrowClick");
        StringAssert.Contains(code, "componentPackageStore.WriteToLibraryAsync(");
        StringAssert.Contains(code, "OnSaveComponentAsClick");
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
