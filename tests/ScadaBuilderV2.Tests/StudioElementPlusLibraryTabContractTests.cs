namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class StudioElementPlusLibraryTabContractTests
{
    [TestMethod]
    public void MainWindowXamlExposesLibraryTabWithSelectorAndList()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml");

        StringAssert.Contains(xaml, "Header=\"Librairie\"");
        StringAssert.Contains(xaml, "x:Name=\"StudioLibrarySelectorComboBox\"");
        StringAssert.Contains(xaml, "x:Name=\"StudioLibraryListBox\"");
        StringAssert.Contains(xaml, "SelectionChanged=\"OnStudioLibrarySelectorSelectionChanged\"");
    }

    [TestMethod]
    public void MainWindowCodeLoadsLibraryItemsThroughSharedReader()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml.cs");

        StringAssert.Contains(code, "private async Task<IReadOnlyList<LibraryEntry>> BuildLibraryEntriesAsync()");
        StringAssert.Contains(code, "private async Task RefreshLibrarySelectorAsync()");
        StringAssert.Contains(code, "private async Task RefreshLibraryItemsAsync()");
        StringAssert.Contains(code, "elementPlusLibraryReader.ReadAsync(");
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
