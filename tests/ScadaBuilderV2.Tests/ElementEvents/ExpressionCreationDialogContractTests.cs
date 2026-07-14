namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class ExpressionCreationDialogContractTests
{
    [TestMethod]
    public void TagSelectionDialog_OnlyEnabledTagsAndSortsByDisplayName()
    {
        var source = ReadAppFile("TagSelectionDialog.xaml.cs");

        StringAssert.Contains(source, ".Where(tag => tag.Enabled)");
        StringAssert.Contains(source, ".OrderBy(tag => tag.DisplayName, StringComparer.OrdinalIgnoreCase)");
    }

    [TestMethod]
    public void TagSelectionDialog_SupportsDoubleClickAndExplicitSelection()
    {
        var xaml = ReadAppFile("TagSelectionDialog.xaml");
        var source = ReadAppFile("TagSelectionDialog.xaml.cs");

        StringAssert.Contains(xaml, "MouseDoubleClick=\"OnTagDoubleClick\"");
        StringAssert.Contains(xaml, "Content=\"Sélectionner\"");
        StringAssert.Contains(source, "SelectedTag");
        StringAssert.Contains(source, "DialogResult = true");
    }

    [TestMethod]
    public void ExpressionCreationDialog_ExposesOperatorsTooltipsAndApplyContract()
    {
        var xaml = ReadAppFile("ExpressionCreationDialog.xaml");

        foreach (var token in new[] { "Content=\"Variable\"", "Content=\">\"", "Content=\"&lt;\"", "Content=\"&gt;=\"", "Content=\"&lt;=\"", "Content=\"==\"", "Content=\"!=\"", "Tag=\" &amp;&amp; \"", "Tag=\" || \"", "Content=\"Appliquer\"", "Content=\"Annuler\"", "Content=\"Effacer\"" })
            StringAssert.Contains(xaml, token);

        StringAssert.Contains(xaml, "ToolTip=");
    }

    [TestMethod]
    public void ExpressionCreationDialog_UsesLocalCopyAndCaretContract()
    {
        var source = ReadAppFile("ExpressionCreationDialog.xaml.cs");

        StringAssert.Contains(source, "int? initialCaretIndex");
        StringAssert.Contains(source, "caretIndex is null || caretIndex.Value <= 0");
        StringAssert.Contains(source, "ExpressionTextBox.CaretIndex");
        StringAssert.Contains(source, "ResultExpression = ExpressionTextBox.Text");
        StringAssert.Contains(source, "ScadaExpressionValidator.Validate");
    }

    [TestMethod]
    public void ElementStateRuleDialog_WiresExpressionToolAndApplyResult()
    {
        var xaml = ReadAppFile("ElementStateRuleDialog.xaml");
        var source = ReadAppFile("ElementStateRuleDialog.xaml.cs");

        StringAssert.Contains(xaml, "Text=\"Expression :\"");
        StringAssert.Contains(xaml, "x:Name=\"ExpressionToolButton\"");
        StringAssert.Contains(xaml, "Content=\"Outil\"");
        StringAssert.Contains(source, "new ExpressionCreationDialog(");
        StringAssert.Contains(source, "ExpressionTextBox.CaretIndex");
        StringAssert.Contains(source, "assistant.ResultExpression");
    }

    [TestMethod]
    public void ElementStateRuleDialog_PreservesExistingExpressionValidationFlow()
    {
        var source = ReadAppFile("ElementStateRuleDialog.xaml.cs");

        StringAssert.Contains(source, "ScadaExpression.FromAst");
        StringAssert.Contains(source, "ResolveTagIds");
        StringAssert.Contains(source, "OnExpressionTextChanged");
    }

    private static string ReadAppFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ScadaBuilderV2.sln")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "Repository root could not be located from the test output directory.");
        var path = Path.Combine(directory!.FullName, "src", "ScadaBuilderV2.App", fileName);
        Assert.IsTrue(File.Exists(path), $"Expected application source file was not found: {path}");
        return File.ReadAllText(path);
    }
}
