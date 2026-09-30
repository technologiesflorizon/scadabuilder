using ScadaBuilderV2.App.QuickWindows;
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
