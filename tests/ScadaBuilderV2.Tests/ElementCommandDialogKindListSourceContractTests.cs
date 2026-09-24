using System.IO;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// Pins three properties of `ElementCommandDialog` that no unit test can reach, because the dialog is a WPF
/// `Window` whose behaviour lives in `InitializeComponent` and its XAML: the test project links individual
/// WPF-free `ScadaBuilderV2.App` source files (see the `Compile Include` list in
/// `tests/ScadaBuilderV2.Tests/ScadaBuilderV2.Tests.csproj`), but a `Window` and its markup cannot be linked
/// or instantiated in this headless test process. The three properties: the "Type" list is built from the
/// policy rather than filtered by hand, the caller can no longer omit its quick-window context, and the
/// disabled-tooltip escape hatch WPF needs is actually present in markup.
/// </summary>
/// <remarks>
/// Before this change, `KindComboBox.ItemsSource` was `Enum.GetValues&lt;ScadaCommandKind&gt;()` narrowed by
/// two `.Where` filters that silently dropped `OpenQuickWindow`/`CloseQuickWindow` (and any already-used kind)
/// instead of disabling them - the reason the reporting operator could never find "Ouvrir une fenetre rapide"
/// with zero quick-window definitions in the project. `ElementCommandKindCatalog.CreateOptions`
/// (`ScadaBuilderV2.Application`) now always returns the six entries; this file guards that the dialog never
/// regresses back to filtering, that the 5th constructor parameter cannot silently default back to `Empty`,
/// and that `ToolTipService.ShowOnDisabled="True"` - the one WPF property the whole "grise + infobulle au
/// survol" request depends on, since a disabled control does not receive mouse events by default - stays on
/// `KindComboBox`'s item container style.
///
/// Decisions: DEC-0050.
/// Tests: this file. Unit coverage of the policy itself:
/// tests/ScadaBuilderV2.Tests/Commands/ElementCommandKindCatalogTests.cs.
/// </remarks>
[TestClass]
public sealed class ElementCommandDialogKindListSourceContractTests
{
    [TestMethod]
    public void TheDialogNoLongerBuildsItsKindListFromAllEnumValuesFilteredByWhere()
    {
        var code = ReadAppFile("ElementCommandDialog.xaml.cs");

        Assert.IsFalse(
            code.Contains("Enum.GetValues<ScadaCommandKind>()"),
            "the kind list must come from ElementCommandKindCatalog.CreateOptions, not from the raw enum.");
        Assert.IsFalse(
            code.Contains(".Where(kind"),
            "no .Where filter may remove a command kind from KindComboBox.ItemsSource - entries are disabled, "
            + "never removed.");
        StringAssert.Contains(code, "ElementCommandKindCatalog.CreateOptions(");
    }

    [TestMethod]
    public void TheFifthConstructorParameterHasNoDefaultValue()
    {
        var code = ReadAppFile("ElementCommandDialog.xaml.cs");

        Assert.IsFalse(
            code.Contains("QuickWindowCommandAuthoringContext? quickWindowContext = null"),
            "a defaultable 5th parameter is how a caller can rebuild the dialog without any quick-window "
            + "context and never notice - it must be a compiler error instead.");
        StringAssert.Contains(
            code,
            "QuickWindowCommandAuthoringContext quickWindowContext)",
            "the 5th parameter must be a required, non-nullable QuickWindowCommandAuthoringContext.");
    }

    [TestMethod]
    public void TheKindComboBoxDisabledItemsStillShowTheirTooltip()
    {
        var xaml = ReadAppFile("ElementCommandDialog.xaml");

        StringAssert.Contains(
            xaml,
            "ToolTipService.ShowOnDisabled\" Value=\"True\"",
            "WPF does not route mouse events to a disabled control by default, so without "
            + "ToolTipService.ShowOnDisabled=\"True\" the explanatory tooltip on a greyed-out Type entry would "
            + "never appear - exactly the case this feature exists to cover.");
    }

    private static string ReadAppFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.App", relativePath);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate src/ScadaBuilderV2.App/{relativePath}.");
        return string.Empty;
    }
}
