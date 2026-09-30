using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Locks the parameter-sheet editor view-model: the smallest free `#N`, one line appended per rule in the
/// existing expression grammar, a refusal rather than a broken line for an un-escapable literal quote, and
/// diagnostics refreshed from the text alone (DEC-0052).
/// </summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class ParameterSheetEditorViewModelTests
{
    private static ParameterSheet Sheet(string text) => new(Guid.NewGuid(), "v3", "Ventilateur 3", text);

    [TestMethod]
    public void NextFreeNumberIsTheSmallestUnusedOne()
    {
        Assert.AreEqual(2, new ParameterSheetEditorViewModel(Sheet("#1 := 1\n#3 := 1"), null).NextFreeNumber);
    }

    [TestMethod]
    public void AppendRuleAddsOneLineInTheExistingGrammar()
    {
        var vm = new ParameterSheetEditorViewModel(Sheet("#1 := {A}"), null);

        vm.AppendRule(2, ParameterValueKind.Tag, "FAN_03_RUN");
        vm.AppendRule(3, ParameterValueKind.Literal, "Ventilateur 3");
        vm.AppendRule(4, ParameterValueKind.Expression, "{A} * 0.1");

        StringAssert.EndsWith(vm.Text.Replace("\r\n", "\n"), "#2 := {FAN_03_RUN}\n#3 := \"Ventilateur 3\"\n#4 := {A} * 0.1");
        Assert.AreEqual(0, vm.Diagnostics.Count);
    }

    [TestMethod]
    public void ALiteralWithAQuoteIsRefusedRatherThanProducingABrokenLine()
    {
        var vm = new ParameterSheetEditorViewModel(Sheet(""), null);

        Assert.ThrowsException<ArgumentException>(() => vm.AppendRule(1, ParameterValueKind.Literal, "dit \"x\""));
    }

    [TestMethod]
    public void ATagRuleWritesTheReadableNameTheLoweringResolvesNotTheMappingId()
    {
        // Final review M2: the dialog wrote {tf100.mapping.N}; the operator picked "FAN_03_RUN" in the list.
        var catalog = new ScadaTagCatalog("tests",
        [
            new ScadaTagDefinition("tf100.mapping.101", "FAN_03_RUN", Datatype: "Boolean"),
            new ScadaTagDefinition("tf100.mapping.201", "DUPLICATE"),
            new ScadaTagDefinition("tf100.mapping.202", "DUPLICATE"),
            new ScadaTagDefinition("tf100.mapping.301", " "),
        ]);

        Assert.AreEqual("FAN_03_RUN", ParameterSheetEditorViewModel.TagReferenceFor(catalog.Tags[0], catalog));
        Assert.AreEqual("tf100.mapping.201", ParameterSheetEditorViewModel.TagReferenceFor(catalog.Tags[1], catalog), "an ambiguous name falls back to the id");
        Assert.AreEqual("tf100.mapping.301", ParameterSheetEditorViewModel.TagReferenceFor(catalog.Tags[3], catalog), "a blank name falls back to the id");

        var vm = new ParameterSheetEditorViewModel(Sheet(""), catalog);
        vm.AppendRule(1, ParameterValueKind.Tag, ParameterSheetEditorViewModel.TagReferenceFor(catalog.Tags[0], catalog));
        Assert.AreEqual("#1 := {FAN_03_RUN}", vm.Text);
    }

    [TestMethod]
    public void ARuleIsValidatedByTheSheetParserBeforeItIsAdded()
    {
        var vm = new ParameterSheetEditorViewModel(Sheet("#1 := {A}"), null);

        Assert.IsNull(vm.ValidateRule(2, ParameterValueKind.Expression, "{A} * 0.1"));
        Assert.IsNull(vm.ValidateRule(2, ParameterValueKind.Literal, "Ventilateur 3"));
        Assert.IsNotNull(vm.ValidateRule(2, ParameterValueKind.Expression, "{A} * "), "an invalid expression is refused");
        Assert.IsNotNull(vm.ValidateRule(2, ParameterValueKind.Expression, "{#3} + 1"), "a reference to another #N is refused");
        Assert.IsNotNull(vm.ValidateRule(1, ParameterValueKind.Literal, "x"), "an already defined #N is refused");
        Assert.IsNotNull(vm.ValidateRule(2, ParameterValueKind.Literal, "dit \"x\""), "a quote in a literal is refused");
        Assert.AreEqual("#1 := {A}", vm.Text, "validating never mutates the text");
    }

    [TestMethod]
    public void EditingTheTextRefreshesDiagnostics()
    {
        var vm = new ParameterSheetEditorViewModel(Sheet(""), null);

        vm.Text = "#1 := {A}\n#1 := {B}";

        Assert.AreEqual(2, vm.Diagnostics.Single().Line);
    }

    [TestMethod]
    public void ToSheetKeepsTheKeyAndReturnsTheEditedText()
    {
        var sheet = Sheet("#1 := 1");
        var vm = new ParameterSheetEditorViewModel(sheet, null) { Text = "#1 := 2" };

        Assert.AreEqual(sheet.SheetKey, vm.ToSheet().SheetKey);
        Assert.AreEqual("#1 := 2", vm.ToSheet().Text);
    }
}
