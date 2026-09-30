using ScadaBuilderV2.App.QuickWindows;

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
