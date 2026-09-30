using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Pins the WPF-free save rules of the "Configuration de la fenêtre rapide" dialog and of
/// `ElementCommandDialog`'s `OpenQuickWindow` save gate, including the legacy DEC-0050 command allowance.
/// </summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class QuickWindowConfigurationRulesTests
{
    private static readonly Guid SheetKey = Guid.NewGuid();
    private static readonly Guid PageKey = Guid.NewGuid();
    private static readonly Guid InvocationKey = Guid.NewGuid();

    [TestMethod]
    public void ANavigateCommandNeverOffersAQuickWindowPage()
    {
        // Final review M3: a QuickWindow page leaves the page inventory at lowering; navigating to it is an error.
        var pages = new[]
        {
            new ScadaSceneReference("synoptique", "Synoptique", "scenes/synoptique.scene.json", ScadaPageType.Default),
            new ScadaSceneReference("popup_fan", "Popup", "scenes/popup_fan.scene.json", ScadaPageType.QuickWindow),
            new ScadaSceneReference("entete", "En-tête", "scenes/entete.scene.json", ScadaPageType.Header),
        };

        CollectionAssert.AreEqual(
            new[] { "synoptique", "entete" },
            QuickWindowConfigurationRules.NavigateTargetPages(pages).Select(page => page.Id).ToArray());
    }

    [TestMethod]
    public void TheConfigurationSubDialogOpensByItselfOnlyWhenCreatingAnOpenCommand()
    {
        Assert.IsTrue(QuickWindowConfigurationRules.ShouldOpenConfigurationOnLoad(isNewCommand: true, ScadaCommandKind.OpenQuickWindow));
        Assert.IsFalse(QuickWindowConfigurationRules.ShouldOpenConfigurationOnLoad(isNewCommand: false, ScadaCommandKind.OpenQuickWindow),
            "editing an existing command must not reopen the sub-dialog on load");
        Assert.IsFalse(QuickWindowConfigurationRules.ShouldOpenConfigurationOnLoad(isNewCommand: true, ScadaCommandKind.WriteTag));
    }

    [TestMethod]
    public void ElementCommandDialogUsesTheNavigateAndOnLoadRules()
    {
        var source = ReadAppFile("ElementCommandDialog.xaml.cs");
        StringAssert.Contains(source, "TargetPageComboBox.ItemsSource = navigateTargets;");
        StringAssert.Contains(source, "QuickWindowConfigurationRules.NavigateTargetPages(pageReferences)");
        StringAssert.Contains(source, "QuickWindowConfigurationRules.ShouldOpenConfigurationOnLoad(");
        Assert.IsFalse(source.Contains("TargetPageComboBox.ItemsSource = pageReferences;", StringComparison.Ordinal));
    }

    private static string ReadAppFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.App", fileName);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate src/ScadaBuilderV2.App/{fileName}.");
        return string.Empty;
    }

    [TestMethod]
    public void CanSaveConfigurationRequiresBothASheetAndAPage()
    {
        Assert.IsFalse(QuickWindowConfigurationRules.CanSaveConfiguration(null, null));
        Assert.IsFalse(QuickWindowConfigurationRules.CanSaveConfiguration(SheetKey, null));
        Assert.IsFalse(QuickWindowConfigurationRules.CanSaveConfiguration(null, PageKey));
        Assert.IsTrue(QuickWindowConfigurationRules.CanSaveConfiguration(SheetKey, PageKey));
    }

    [TestMethod]
    public void CanSaveOpenQuickWindowCommandAcceptsASheetAndPagePair()
    {
        Assert.IsTrue(QuickWindowConfigurationRules.CanSaveOpenQuickWindowCommand(SheetKey, PageKey, isUnmodifiedLegacyInvocation: false));
    }

    [TestMethod]
    public void CanSaveOpenQuickWindowCommandRefusesAHalfConfiguredPairWhenNotLegacy()
    {
        Assert.IsFalse(QuickWindowConfigurationRules.CanSaveOpenQuickWindowCommand(SheetKey, null, isUnmodifiedLegacyInvocation: false));
    }

    [TestMethod]
    public void CanSaveOpenQuickWindowCommandAcceptsAnUnmodifiedLegacyInvocation()
    {
        Assert.IsTrue(QuickWindowConfigurationRules.CanSaveOpenQuickWindowCommand(null, null, isUnmodifiedLegacyInvocation: true));
    }

    [TestMethod]
    public void CanSaveOpenQuickWindowCommandRefusesAnEmptySelectionWithoutTheLegacyAllowance()
    {
        Assert.IsFalse(QuickWindowConfigurationRules.CanSaveOpenQuickWindowCommand(null, null, isUnmodifiedLegacyInvocation: false));
    }

    [TestMethod]
    public void IsUnmodifiedLegacyInvocationRequiresALegacyInvocationKeyAndNoSheetOrPageAnywhere()
    {
        Assert.IsTrue(QuickWindowConfigurationRules.IsUnmodifiedLegacyInvocation(
            existingCommandIsOpenQuickWindow: true,
            existingQuickWindowInvocationKey: InvocationKey,
            existingParameterSheetKey: null,
            existingQuickWindowPageKey: null,
            currentParameterSheetKey: null,
            currentQuickWindowPageKey: null));
    }

    [TestMethod]
    public void IsUnmodifiedLegacyInvocationIsFalseWhenTheOperatorPickedASheetOrPageInThisSession()
    {
        Assert.IsFalse(QuickWindowConfigurationRules.IsUnmodifiedLegacyInvocation(
            existingCommandIsOpenQuickWindow: true,
            existingQuickWindowInvocationKey: InvocationKey,
            existingParameterSheetKey: null,
            existingQuickWindowPageKey: null,
            currentParameterSheetKey: SheetKey,
            currentQuickWindowPageKey: null));
    }

    [TestMethod]
    public void IsUnmodifiedLegacyInvocationIsFalseWhenTheExistingCommandAlreadyCarriesASheetOrPage()
    {
        Assert.IsFalse(QuickWindowConfigurationRules.IsUnmodifiedLegacyInvocation(
            existingCommandIsOpenQuickWindow: true,
            existingQuickWindowInvocationKey: InvocationKey,
            existingParameterSheetKey: SheetKey,
            existingQuickWindowPageKey: PageKey,
            currentParameterSheetKey: SheetKey,
            currentQuickWindowPageKey: PageKey));
    }

    [TestMethod]
    public void IsUnmodifiedLegacyInvocationIsFalseWhenNoLegacyInvocationKeyExists()
    {
        Assert.IsFalse(QuickWindowConfigurationRules.IsUnmodifiedLegacyInvocation(
            existingCommandIsOpenQuickWindow: true,
            existingQuickWindowInvocationKey: null,
            existingParameterSheetKey: null,
            existingQuickWindowPageKey: null,
            currentParameterSheetKey: null,
            currentQuickWindowPageKey: null));
    }

    [TestMethod]
    public void IsUnmodifiedLegacyInvocationIsFalseWhenTheEditedKindIsNotOpenQuickWindow()
    {
        Assert.IsFalse(QuickWindowConfigurationRules.IsUnmodifiedLegacyInvocation(
            existingCommandIsOpenQuickWindow: false,
            existingQuickWindowInvocationKey: InvocationKey,
            existingParameterSheetKey: null,
            existingQuickWindowPageKey: null,
            currentParameterSheetKey: null,
            currentQuickWindowPageKey: null));
    }
}
