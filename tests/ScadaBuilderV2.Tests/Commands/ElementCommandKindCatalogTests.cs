using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Domain.ElementEvents.Command;

namespace ScadaBuilderV2.Tests.Commands;

/// <summary>
/// Pins the "Type" list policy of the element-command editor: the fixed six-entry ordering, the exact French
/// labels and reasons, and the rule priority when several disabling rules would apply to the same entry.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, DEC-0052.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class ElementCommandKindCatalogTests
{
    private static readonly ScadaCommandKind[] AllKindsInEnumOrder =
    [
        ScadaCommandKind.WriteTag,
        ScadaCommandKind.Navigate,
        ScadaCommandKind.OpenQuickWindow,
        ScadaCommandKind.CloseQuickWindow,
        ScadaCommandKind.OpenUrl,
        ScadaCommandKind.Back,
    ];

    private static ElementCommandKindOption Option(bool pages, bool sheets) =>
        ElementCommandKindCatalog.CreateOptions([], null, pages, sheets, false)
            .Single(o => o.Kind == ScadaCommandKind.OpenQuickWindow);

    [TestMethod]
    public void AllSixKindsAreAlwaysReturnedInEnumOrder()
    {
        // Every context combination below must still surface all six kinds - the whole point of the policy
        // is that nothing is ever removed from the list, only disabled.
        var contexts = new (IReadOnlyCollection<ScadaCommandKind> Used, ScadaCommandKind? Edited, bool HasPages, bool HasSheets, bool IsContent)[]
        {
            ([], null, false, false, false),
            ([ScadaCommandKind.WriteTag, ScadaCommandKind.Navigate, ScadaCommandKind.OpenQuickWindow,
              ScadaCommandKind.CloseQuickWindow, ScadaCommandKind.OpenUrl, ScadaCommandKind.Back], null, false, false, false),
            ([], ScadaCommandKind.OpenQuickWindow, true, true, true),
        };

        foreach (var context in contexts)
        {
            var options = ElementCommandKindCatalog.CreateOptions(context.Used, context.Edited, context.HasPages, context.HasSheets, context.IsContent);
            CollectionAssert.AreEqual(AllKindsInEnumOrder, options.Select(o => o.Kind).ToArray());
        }
    }

    [TestMethod]
    public void TheSixLabelsAreExactlyTheOnesTheOperatorMustRead()
    {
        var options = ElementCommandKindCatalog.CreateOptions([], null, hasQuickWindowPages: true, hasParameterSheets: true, isQuickWindowContent: true);

        var labels = options.ToDictionary(o => o.Kind, o => o.Label);
        Assert.AreEqual("Écrire un tag", labels[ScadaCommandKind.WriteTag]);
        Assert.AreEqual("Aller à une page", labels[ScadaCommandKind.Navigate]);
        Assert.AreEqual("Ouvrir une fenêtre rapide", labels[ScadaCommandKind.OpenQuickWindow]);
        Assert.AreEqual("Fermer la fenêtre rapide", labels[ScadaCommandKind.CloseQuickWindow]);
        Assert.AreEqual("Ouvrir une adresse web", labels[ScadaCommandKind.OpenUrl]);
        Assert.AreEqual("Revenir en arrière", labels[ScadaCommandKind.Back]);
    }

    [TestMethod]
    public void OpenQuickWindowNeedsBothAPageAndASheet()
    {
        AssertDisabled(Option(pages: false, sheets: true), ElementCommandKindCatalog.NoQuickWindowPageReason);
        AssertDisabled(Option(pages: true, sheets: false), ElementCommandKindCatalog.NoParameterSheetReason);
        var neither = Option(pages: false, sheets: false);
        Assert.IsFalse(neither.IsEnabled);
        StringAssert.Contains(neither.DisabledReason, ElementCommandKindCatalog.NoQuickWindowPageReason);
        StringAssert.Contains(neither.DisabledReason, ElementCommandKindCatalog.NoParameterSheetReason);
        Assert.IsTrue(Option(pages: true, sheets: true).IsEnabled);
    }

    private static void AssertDisabled(ElementCommandKindOption option, string reason)
    {
        Assert.IsFalse(option.IsEnabled);
        Assert.AreEqual(reason, option.DisabledReason);
    }

    [TestMethod]
    public void OpenQuickWindowIsEnabledAsSoonAsAPageAndASheetExist()
    {
        var options = ElementCommandKindCatalog.CreateOptions([], null, hasQuickWindowPages: true, hasParameterSheets: true, isQuickWindowContent: false);
        var openQuickWindow = options.Single(o => o.Kind == ScadaCommandKind.OpenQuickWindow);

        Assert.IsTrue(openQuickWindow.IsEnabled);
        Assert.IsNull(openQuickWindow.DisabledReason);
    }

    [TestMethod]
    public void CloseQuickWindowIsDisabledWithTheExactReasonOutsideQuickWindowContent()
    {
        var options = ElementCommandKindCatalog.CreateOptions([], null, hasQuickWindowPages: true, hasParameterSheets: true, isQuickWindowContent: false);
        var closeQuickWindow = options.Single(o => o.Kind == ScadaCommandKind.CloseQuickWindow);

        Assert.IsFalse(closeQuickWindow.IsEnabled);
        Assert.AreEqual("Disponible seulement depuis le contenu d'une fenêtre rapide.", closeQuickWindow.DisabledReason);
    }

    [TestMethod]
    public void CloseQuickWindowIsEnabledInsideQuickWindowContent()
    {
        var options = ElementCommandKindCatalog.CreateOptions([], null, hasQuickWindowPages: false, hasParameterSheets: false, isQuickWindowContent: true);
        var closeQuickWindow = options.Single(o => o.Kind == ScadaCommandKind.CloseQuickWindow);

        Assert.IsTrue(closeQuickWindow.IsEnabled);
        Assert.IsNull(closeQuickWindow.DisabledReason);
    }

    [TestMethod]
    public void AKindAlreadyUsedByAnotherCommandIsDisabledWithTheExactReason()
    {
        var options = ElementCommandKindCatalog.CreateOptions(
            [ScadaCommandKind.WriteTag], editedKind: null, hasQuickWindowPages: true, hasParameterSheets: true, isQuickWindowContent: true);
        var writeTag = options.Single(o => o.Kind == ScadaCommandKind.WriteTag);

        Assert.IsFalse(writeTag.IsEnabled);
        Assert.AreEqual("Ce type est déjà utilisé par une autre commande de cet élément.", writeTag.DisabledReason);
    }

    [TestMethod]
    public void AKindUsedOnlyByTheCommandBeingEditedStaysEnabled()
    {
        var options = ElementCommandKindCatalog.CreateOptions(
            [ScadaCommandKind.WriteTag], editedKind: ScadaCommandKind.WriteTag, hasQuickWindowPages: true, hasParameterSheets: true, isQuickWindowContent: true);
        var writeTag = options.Single(o => o.Kind == ScadaCommandKind.WriteTag);

        Assert.IsTrue(writeTag.IsEnabled);
        Assert.IsNull(writeTag.DisabledReason);
    }

    [TestMethod]
    public void AlreadyUsedWinsOverMissingPageOrSheetWhenBothRulesWouldApply()
    {
        // OpenQuickWindow is both already used by another command AND the project has no page/sheet.
        // Rule 1 (already used) must be reported, not rule 2 (no page/sheet) - the brief's priority order.
        var options = ElementCommandKindCatalog.CreateOptions(
            [ScadaCommandKind.OpenQuickWindow], editedKind: null, hasQuickWindowPages: false, hasParameterSheets: false, isQuickWindowContent: false);
        var openQuickWindow = options.Single(o => o.Kind == ScadaCommandKind.OpenQuickWindow);

        Assert.IsFalse(openQuickWindow.IsEnabled);
        Assert.AreEqual(
            "Ce type est déjà utilisé par une autre commande de cet élément.",
            openQuickWindow.DisabledReason);
    }

    [TestMethod]
    public void EveryOtherKindIsEnabledByDefault()
    {
        var options = ElementCommandKindCatalog.CreateOptions([], null, hasQuickWindowPages: false, hasParameterSheets: false, isQuickWindowContent: false);

        foreach (var kind in new[] { ScadaCommandKind.WriteTag, ScadaCommandKind.Navigate, ScadaCommandKind.OpenUrl, ScadaCommandKind.Back })
        {
            var option = options.Single(o => o.Kind == kind);
            Assert.IsTrue(option.IsEnabled, $"{kind} should be enabled with no rule applying to it.");
            Assert.IsNull(option.DisabledReason);
        }
    }
}
