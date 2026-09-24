using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Domain.ElementEvents.Command;

namespace ScadaBuilderV2.Tests.Commands;

/// <summary>
/// Pins the "Type" list policy of the element-command editor: the fixed six-entry ordering, the exact French
/// labels and reasons, and the rule priority when several disabling rules would apply to the same entry.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050.
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

    [TestMethod]
    public void AllSixKindsAreAlwaysReturnedInEnumOrder()
    {
        // Every context combination below must still surface all six kinds - the whole point of the policy
        // is that nothing is ever removed from the list, only disabled.
        var contexts = new (IReadOnlyCollection<ScadaCommandKind> Used, ScadaCommandKind? Edited, bool HasDefs, bool IsContent)[]
        {
            ([], null, false, false),
            ([ScadaCommandKind.WriteTag, ScadaCommandKind.Navigate, ScadaCommandKind.OpenQuickWindow,
              ScadaCommandKind.CloseQuickWindow, ScadaCommandKind.OpenUrl, ScadaCommandKind.Back], null, false, false),
            ([], ScadaCommandKind.OpenQuickWindow, true, true),
        };

        foreach (var context in contexts)
        {
            var options = ElementCommandKindCatalog.CreateOptions(context.Used, context.Edited, context.HasDefs, context.IsContent);
            CollectionAssert.AreEqual(AllKindsInEnumOrder, options.Select(o => o.Kind).ToArray());
        }
    }

    [TestMethod]
    public void TheSixLabelsAreExactlyTheOnesTheOperatorMustRead()
    {
        var options = ElementCommandKindCatalog.CreateOptions([], null, hasQuickWindowDefinitions: true, isQuickWindowContent: true);

        var labels = options.ToDictionary(o => o.Kind, o => o.Label);
        Assert.AreEqual("Écrire un tag", labels[ScadaCommandKind.WriteTag]);
        Assert.AreEqual("Aller à une page", labels[ScadaCommandKind.Navigate]);
        Assert.AreEqual("Ouvrir une fenêtre rapide", labels[ScadaCommandKind.OpenQuickWindow]);
        Assert.AreEqual("Fermer la fenêtre rapide", labels[ScadaCommandKind.CloseQuickWindow]);
        Assert.AreEqual("Ouvrir une adresse web", labels[ScadaCommandKind.OpenUrl]);
        Assert.AreEqual("Revenir en arrière", labels[ScadaCommandKind.Back]);
    }

    [TestMethod]
    public void OpenQuickWindowIsDisabledWithTheExactReasonWhenNoDefinitionExists()
    {
        var options = ElementCommandKindCatalog.CreateOptions([], null, hasQuickWindowDefinitions: false, isQuickWindowContent: true);
        var openQuickWindow = options.Single(o => o.Kind == ScadaCommandKind.OpenQuickWindow);

        Assert.IsFalse(openQuickWindow.IsEnabled);
        Assert.AreEqual(
            "Aucune fenêtre rapide n'existe dans ce projet. Créez-en une dans le panneau « Fenêtres rapides ».",
            openQuickWindow.DisabledReason);
    }

    [TestMethod]
    public void OpenQuickWindowIsEnabledAssoonAsOneDefinitionExists()
    {
        var options = ElementCommandKindCatalog.CreateOptions([], null, hasQuickWindowDefinitions: true, isQuickWindowContent: false);
        var openQuickWindow = options.Single(o => o.Kind == ScadaCommandKind.OpenQuickWindow);

        Assert.IsTrue(openQuickWindow.IsEnabled);
        Assert.IsNull(openQuickWindow.DisabledReason);
    }

    [TestMethod]
    public void CloseQuickWindowIsDisabledWithTheExactReasonOutsideQuickWindowContent()
    {
        var options = ElementCommandKindCatalog.CreateOptions([], null, hasQuickWindowDefinitions: true, isQuickWindowContent: false);
        var closeQuickWindow = options.Single(o => o.Kind == ScadaCommandKind.CloseQuickWindow);

        Assert.IsFalse(closeQuickWindow.IsEnabled);
        Assert.AreEqual("Disponible seulement depuis le contenu d'une fenêtre rapide.", closeQuickWindow.DisabledReason);
    }

    [TestMethod]
    public void CloseQuickWindowIsEnabledInsideQuickWindowContent()
    {
        var options = ElementCommandKindCatalog.CreateOptions([], null, hasQuickWindowDefinitions: false, isQuickWindowContent: true);
        var closeQuickWindow = options.Single(o => o.Kind == ScadaCommandKind.CloseQuickWindow);

        Assert.IsTrue(closeQuickWindow.IsEnabled);
        Assert.IsNull(closeQuickWindow.DisabledReason);
    }

    [TestMethod]
    public void AKindAlreadyUsedByAnotherCommandIsDisabledWithTheExactReason()
    {
        var options = ElementCommandKindCatalog.CreateOptions(
            [ScadaCommandKind.WriteTag], editedKind: null, hasQuickWindowDefinitions: true, isQuickWindowContent: true);
        var writeTag = options.Single(o => o.Kind == ScadaCommandKind.WriteTag);

        Assert.IsFalse(writeTag.IsEnabled);
        Assert.AreEqual("Ce type est déjà utilisé par une autre commande de cet élément.", writeTag.DisabledReason);
    }

    [TestMethod]
    public void AKindUsedOnlyByTheCommandBeingEditedStaysEnabled()
    {
        var options = ElementCommandKindCatalog.CreateOptions(
            [ScadaCommandKind.WriteTag], editedKind: ScadaCommandKind.WriteTag, hasQuickWindowDefinitions: true, isQuickWindowContent: true);
        var writeTag = options.Single(o => o.Kind == ScadaCommandKind.WriteTag);

        Assert.IsTrue(writeTag.IsEnabled);
        Assert.IsNull(writeTag.DisabledReason);
    }

    [TestMethod]
    public void AlreadyUsedWinsOverNoDefinitionsWhenBothRulesWouldApply()
    {
        // OpenQuickWindow is both already used by another command AND the project has no definitions.
        // Rule 1 (already used) must be reported, not rule 2 (no definitions) - the brief's priority order.
        var options = ElementCommandKindCatalog.CreateOptions(
            [ScadaCommandKind.OpenQuickWindow], editedKind: null, hasQuickWindowDefinitions: false, isQuickWindowContent: false);
        var openQuickWindow = options.Single(o => o.Kind == ScadaCommandKind.OpenQuickWindow);

        Assert.IsFalse(openQuickWindow.IsEnabled);
        Assert.AreEqual(
            "Ce type est déjà utilisé par une autre commande de cet élément.",
            openQuickWindow.DisabledReason);
    }

    [TestMethod]
    public void EveryOtherKindIsEnabledByDefault()
    {
        var options = ElementCommandKindCatalog.CreateOptions([], null, hasQuickWindowDefinitions: false, isQuickWindowContent: false);

        foreach (var kind in new[] { ScadaCommandKind.WriteTag, ScadaCommandKind.Navigate, ScadaCommandKind.OpenUrl, ScadaCommandKind.Back })
        {
            var option = options.Single(o => o.Kind == kind);
            Assert.IsTrue(option.IsEnabled, $"{kind} should be enabled with no rule applying to it.");
            Assert.IsNull(option.DisabledReason);
        }
    }
}
