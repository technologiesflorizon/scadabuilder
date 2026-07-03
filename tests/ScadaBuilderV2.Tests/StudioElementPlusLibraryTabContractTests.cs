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

    [TestMethod]
    public void MainWindowXamlExposesContextMenuWithDisabledEditAndThreeActions()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml");

        StringAssert.Contains(xaml, "Header=\"Editer\"");
        StringAssert.Contains(xaml, "IsEnabled=\"False\"");
        StringAssert.Contains(xaml, "Click=\"OnRenameLibraryComponentClick\"");
        StringAssert.Contains(xaml, "Click=\"OnCopyLibraryComponentClick\"");
        StringAssert.Contains(xaml, "Click=\"OnDeleteLibraryComponentClick\"");
    }

    [TestMethod]
    public void MainWindowCodeConfirmsBeforeDeletingAndUsesCopyNaming()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml.cs");

        StringAssert.Contains(code, "MessageBoxButton.YesNo");
        StringAssert.Contains(code, "ElementStudioComponentCopyNaming.GenerateCopyName(");
        StringAssert.Contains(code, "ElementStudioComponentPackageStore.GetDefaultComponentPath(");
    }

    [TestMethod]
    public void ComponentNameDialogXamlExposesNameTextBox()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "ComponentNameDialog.xaml");

        StringAssert.Contains(xaml, "x:Name=\"NameTextBox\"");
    }

    [TestMethod]
    public void MainWindowXamlRendersLibraryItemsAsPreviewTiles()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml");

        StringAssert.Contains(xaml, "xmlns:local=\"clr-namespace:ScadaBuilderV2.ElementStudio.App\"");
        StringAssert.Contains(xaml, "<WrapPanel/>");
        StringAssert.Contains(xaml, "local:HtmlPreviewControl");
        StringAssert.Contains(xaml, "Markup=\"{Binding PreviewMarkup}\"");
        StringAssert.Contains(xaml, "Text=\"{Binding IconText}\"");
        StringAssert.Contains(xaml, "Text=\"{Binding FileName}\"");
        StringAssert.Contains(xaml, "ToolTip=\"{Binding DetailText}\"");
    }

    [TestMethod]
    public void HtmlPreviewControlExistsInElementStudioApp()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "HtmlPreviewControl.cs");

        StringAssert.Contains(code, "namespace ScadaBuilderV2.ElementStudio.App;");
        StringAssert.Contains(code, "public sealed class HtmlPreviewControl : UserControl");
        StringAssert.Contains(code, "public static readonly DependencyProperty MarkupProperty");
        StringAssert.Contains(code, "browser.NavigateToString(");
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
