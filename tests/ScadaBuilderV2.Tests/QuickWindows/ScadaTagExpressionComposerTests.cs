using ScadaBuilderV2.App;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Regression tests for two bugs on <see cref="ScadaTagExpressionComposer"/>: (fix round 2) selecting a
/// QuickWindow page's `#1` placeholder in `ElementStateRuleDialog`'s Variable mode produced an unparsable
/// expression ("Caractere inattendu : '}'") because the dialog composed `{tag.DisplayName}` while
/// <see cref="QuickWindowPagePlaceholderCatalog"/> put the sheet's source description in `DisplayName` (fix
/// round 1); and (fix round 3) composing from `Id` instead to fix that broke every *normal* project tag
/// selection - a real catalog has `Id` `"tf100.mapping.161"` and `DisplayName` `"PE_16"`, so the rule read
/// `{tf100.mapping.161} > 0` instead of `{PE_16} > 0`. The composer now always composes from `DisplayName`,
/// which is correct for a normal tag by construction and for a placeholder entry because `DisplayName == Id`
/// holds for it (DEC-0052). These tests lock both: a normal tag composes from its `DisplayName`, and a
/// placeholder entry still composes and resolves correctly.
/// </summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class ScadaTagExpressionComposerTests
{
    [TestMethod]
    public void WrapTagTokenComposesFromDisplayNameForANormalProjectTag()
    {
        // Fix round 3 regression lock: a real project tag's Id ("tf100.mapping.161") is not what the
        // operator expects to see and re-edit; DisplayName ("PE_16") is.
        var tag = new ScadaTagDefinition("tf100.mapping.161", "PE_16", Datatype: "Decimal");
        Assert.AreEqual("{PE_16}", ScadaTagExpressionComposer.WrapTagToken(tag));
    }

    [TestMethod]
    public void WrapTagTokenComposesFromDisplayNameForAPlaceholderTagToo()
    {
        // DisplayName == Id for a placeholder entry (DEC-0052), so composing from DisplayName stays correct.
        var tag = new ScadaTagDefinition("#1", "#1");
        Assert.AreEqual("{#1}", ScadaTagExpressionComposer.WrapTagToken(tag));
    }

    [TestMethod]
    public void TheStateRuleOperatorAndValueVariableExpressionForANormalProjectTagUsesItsDisplayName()
    {
        // ElementStateRuleDialog.BuildExpressionFromVariable on an ordinary page: Decimal tag "PE_16"
        // (Id "tf100.mapping.161"), operator ">" and value "0" - must read `{PE_16} > 0`, never
        // `{tf100.mapping.161} > 0` (the round-2 regression this round fixes).
        var tag = new ScadaTagDefinition("tf100.mapping.161", "PE_16", Datatype: "Decimal");
        var expression = ScadaTagExpressionComposer.ComposeVariableExpression(
            tag, isBooleanDatatype: false, boolValue: null, comparisonOperator: ">", rawValue: "0");
        Assert.AreEqual("{PE_16} > 0", expression);

        var catalog = new ScadaTagCatalog("scada-tag-expression-composer-tests-normal", [tag]);
        var validation = ScadaExpressionValidator.Validate(expression, catalog);
        Assert.IsTrue(validation.IsValid, string.Join(" | ", validation.Errors));

        var resolved = ScadaExpressionValidator.TryResolveTagReference("PE_16", catalog);
        Assert.AreEqual(TagResolveStatus.Resolved, resolved.Status);
        Assert.AreEqual("tf100.mapping.161", resolved.CanonicalId);
    }

    [TestMethod]
    public void ExpressionCreationDialogStyleTokenInsertionForANormalProjectTagUsesItsDisplayName()
    {
        // ExpressionCreationDialog.OnVariableClick composes ScadaTagExpressionComposer.WrapTagToken(selectedTag.DisplayName).
        var insertedToken = ScadaTagExpressionComposer.WrapTagToken("PE_16");
        Assert.AreEqual("{PE_16}", insertedToken);
    }

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

        // ExpressionCreationDialog.OnVariableClick: ScadaTagExpressionComposer.WrapTagToken(selectedTag.DisplayName).
        var insertedToken = ScadaTagExpressionComposer.WrapTagToken(tag.DisplayName);
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
        var catalog = QuickWindowPagePlaceholderCatalog.Build(project, null);
        tag = catalog.Tags.Single(t => t.Id == "#1");
        return catalog;
    }
}
