using ScadaBuilderV2.App;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Regression tests (fix round 2) for the bug reported on `Popup_Fan_Condenseur`: selecting a QuickWindow
/// page's `#1` placeholder in `ElementStateRuleDialog`'s Variable mode (Condition = Variable, "si vrai")
/// produced an unparsable expression ("Caractere inattendu : '}'") because the dialog composed
/// `{tag.DisplayName}`, and <see cref="QuickWindowPagePlaceholderCatalog"/> once put the sheet's source
/// description in `DisplayName` (fix round 1). These tests lock, through the exact composition logic the
/// dialogs use (<see cref="ScadaTagExpressionComposer"/>), that a placeholder entry always composes and
/// resolves correctly.
/// </summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class ScadaTagExpressionComposerTests
{
    [TestMethod]
    public void B_TheStateRuleBooleanVariableExpressionForASelectedPlaceholderValidatesAndResolvesToItsId()
    {
        var catalog = BuildSingleEntryCatalog("#1 := {FAN_03_RUN}", out var tag);

        // ElementStateRuleDialog.BuildExpressionFromVariable, Boolean datatype, "si vrai" selected.
        var expression = ScadaTagExpressionComposer.ComposeVariableExpression(
            tag, isBooleanDatatype: true, boolValue: true, comparisonOperator: null, rawValue: null);
        Assert.AreEqual("{#1} == true", expression);

        var validation = ScadaExpressionValidator.Validate(expression, catalog);
        Assert.IsTrue(validation.IsValid, string.Join(" | ", validation.Errors));

        var resolved = ScadaExpressionValidator.TryResolveTagReference("#1", catalog);
        Assert.AreEqual(TagResolveStatus.Resolved, resolved.Status);
        Assert.AreEqual("#1", resolved.CanonicalId);
    }

    [TestMethod]
    public void B_TheStateRuleOperatorAndValueVariableExpressionForASelectedPlaceholderValidatesAndResolves()
    {
        var catalog = BuildSingleEntryCatalog("#1 := {FAN_03_SPEED} * 0.1", out var tag);

        // ElementStateRuleDialog.BuildExpressionFromVariable, non-Boolean datatype, operator ">=" and value "10".
        var expression = ScadaTagExpressionComposer.ComposeVariableExpression(
            tag, isBooleanDatatype: false, boolValue: null, comparisonOperator: ">=", rawValue: "10");
        Assert.AreEqual("{#1} >= 10", expression);

        var validation = ScadaExpressionValidator.Validate(expression, catalog);
        Assert.IsTrue(validation.IsValid, string.Join(" | ", validation.Errors));

        var resolved = ScadaExpressionValidator.TryResolveTagReference("#1", catalog);
        Assert.AreEqual(TagResolveStatus.Resolved, resolved.Status);
        Assert.AreEqual("#1", resolved.CanonicalId);
    }

    [TestMethod]
    public void B_TheStateRuleBlankOperatorVariableExpressionIsTheLoneTokenAndStillResolves()
    {
        // The "--" operator choice (OperatorItem("--", "")) composes the lone token.
        var catalog = BuildSingleEntryCatalog("#1 := {FAN_03_RUN}", out var tag);

        var expression = ScadaTagExpressionComposer.ComposeVariableExpression(
            tag, isBooleanDatatype: false, boolValue: null, comparisonOperator: "", rawValue: null);
        Assert.AreEqual("{#1}", expression);

        var resolved = ScadaExpressionValidator.TryResolveTagReference("#1", catalog);
        Assert.AreEqual(TagResolveStatus.Resolved, resolved.Status);
        Assert.AreEqual("#1", resolved.CanonicalId);
    }

    [TestMethod]
    public void C_ExpressionCreationDialogStyleTokenInsertionResolvesAndValidatesOnceCompletedToABooleanExpression()
    {
        var catalog = BuildSingleEntryCatalog("#1 := {FAN_03_RUN}", out var tag);

        // ExpressionCreationDialog.OnVariableClick: ScadaTagExpressionComposer.WrapTagToken(selectedTag.TagId).
        var insertedToken = ScadaTagExpressionComposer.WrapTagToken(tag.Id);
        Assert.AreEqual("{#1}", insertedToken);

        // The inserted token alone is never flagged as referencing an unknown tag (the bug this locks),
        // even though a bare tag reference still fails the unrelated "must evaluate to boolean" root rule
        // every expression (placeholder or not) is subject to.
        var loneTokenValidation = ScadaExpressionValidator.Validate(insertedToken, catalog);
        Assert.IsFalse(
            loneTokenValidation.Errors.Any(error => error.Contains("n'existe pas", StringComparison.Ordinal)),
            string.Join(" | ", loneTokenValidation.Errors));

        // Completed to a boolean expression (as the operator toolbar buttons would), it fully validates.
        var completed = $"{insertedToken} == true";
        var validation = ScadaExpressionValidator.Validate(completed, catalog);
        Assert.IsTrue(validation.IsValid, string.Join(" | ", validation.Errors));

        var resolved = ScadaExpressionValidator.TryResolveTagReference("#1", catalog);
        Assert.AreEqual(TagResolveStatus.Resolved, resolved.Status);
        Assert.AreEqual("#1", resolved.CanonicalId);
    }

    [TestMethod]
    public void WrapTagTokenComposesFromIdNeverFromDisplayName()
    {
        // Direct lock of the regression: even if a tag's DisplayName ever diverged from its Id again by
        // mistake, the composer must still use Id.
        var tag = new ScadaTagDefinition("#1", "#1 — {BP_1A_AUTO}");
        Assert.AreEqual("{#1}", ScadaTagExpressionComposer.WrapTagToken(tag));
    }

    private static ScadaTagCatalog BuildSingleEntryCatalog(string sheetText, out ScadaTagDefinition tag)
    {
        var pageKey = Guid.NewGuid();
        var sheet = new ParameterSheet(Guid.NewGuid(), "sheet", "Feuille", sheetText);
        var project = ScadaProject.CreateDefault("ScadaTagExpressionComposerTests") with
        {
            ManifestVersion = "2.3",
            ParameterSheets = [sheet],
            TagCatalog = new ScadaTagCatalog(
                "scada-tag-expression-composer-tests",
                [
                    new ScadaTagDefinition("tf100.mapping.101", "FAN_03_RUN", Datatype: "Boolean"),
                    new ScadaTagDefinition("tf100.mapping.103", "FAN_03_SPEED", Datatype: "Decimal"),
                ])
        };
        var callerPage = ScadaScene.CreateEmpty("win00001", "win00001", CanvasSize.DefaultDesktop) with
        {
            PageKey = Guid.NewGuid(),
            PageCode = "win00001"
        };
        callerPage = callerPage.WithElement(ScadaElement.CreateButton("open", "Ouvrir", 20, 20) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding("open-cmd", "Ouvrir", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
                    ParameterSheetKey: sheet.SheetKey, QuickWindowPageKey: pageKey)
            ])
        });

        var scenes = new Dictionary<Guid, ScadaScene> { [callerPage.PageKey] = callerPage };
        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, scenes, pageKey);
        tag = catalog.Tags.Single(t => t.Id == "#1");
        return catalog;
    }
}
