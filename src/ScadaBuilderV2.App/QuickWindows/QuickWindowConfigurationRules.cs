using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// WPF-free decision rules shared by <c>QuickWindowConfigurationDialog</c> and <c>ElementCommandDialog</c>:
/// when the configuration dialog's "Enregistrer" is enabled, and when an `OpenQuickWindow` command may be
/// saved by the caller dialog.
/// </summary>
/// <remarks>
/// Kept apart from the `Window` code-behind so the rule itself can be unit-tested; `ElementCommandDialog` and
/// `QuickWindowConfigurationDialog` are WPF `Window`s the headless test process cannot instantiate.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowConfigurationRulesTests.cs.
/// </remarks>
public static class QuickWindowConfigurationRules
{
    /// <summary>
    /// Gets whether "Enregistrer" is enabled in the "Configuration de la fenêtre rapide" dialog: both a
    /// parameter sheet and a quick-window page must be chosen.
    /// </summary>
    public static bool CanSaveConfiguration(Guid? parameterSheetKey, Guid? quickWindowPageKey) =>
        parameterSheetKey is not null && quickWindowPageKey is not null;

    /// <summary>
    /// Gets whether `ElementCommandDialog` may save an `OpenQuickWindow` command: either a sheet and a page
    /// are both chosen, or the command is an untouched pre-existing DEC-0050 command that only ever carried a
    /// `QuickWindowInvocationKey` (no sheet, no page) and whose sheet/page selection was never touched in this
    /// editing session - that legacy command keeps loading and saving exactly as authored.
    /// </summary>
    public static bool CanSaveOpenQuickWindowCommand(
        Guid? parameterSheetKey,
        Guid? quickWindowPageKey,
        bool isUnmodifiedLegacyInvocation) =>
        CanSaveConfiguration(parameterSheetKey, quickWindowPageKey) || isUnmodifiedLegacyInvocation;

    /// <summary>
    /// Gets the pages a <see cref="ScadaCommandKind.Navigate"/> command may target: every page except the pages
    /// of type Fenêtre rapide, which the lowering removes from the page inventory (they are opened through
    /// "Ouvrir une fenêtre rapide", never navigated to).
    /// </summary>
    public static IReadOnlyList<ScadaSceneReference> NavigateTargetPages(IEnumerable<ScadaSceneReference> pages) =>
        pages.Where(page => page.Type != ScadaPageType.QuickWindow).ToArray();

    /// <summary>
    /// Gets whether `ElementCommandDialog` opens the "Configuration de la fenêtre rapide" sub-dialog by itself when
    /// it loads: only while creating a command whose kind is already `OpenQuickWindow`. Editing an existing
    /// command never reopens it on load; the operator uses "Configurer…" (or picks the kind again).
    /// </summary>
    public static bool ShouldOpenConfigurationOnLoad(bool isNewCommand, ScadaCommandKind? selectedKind) =>
        isNewCommand && selectedKind == ScadaCommandKind.OpenQuickWindow;

    /// <summary>
    /// Gets whether one existing command is an untouched legacy DEC-0050 `OpenQuickWindow` command: it never
    /// carried a sheet/page pair, it does carry a legacy invocation key, and the dialog's current sheet/page
    /// selection is still empty (the operator never picked a sheet or a page for it in this session).
    /// </summary>
    public static bool IsUnmodifiedLegacyInvocation(
        bool existingCommandIsOpenQuickWindow,
        Guid? existingQuickWindowInvocationKey,
        Guid? existingParameterSheetKey,
        Guid? existingQuickWindowPageKey,
        Guid? currentParameterSheetKey,
        Guid? currentQuickWindowPageKey) =>
        existingCommandIsOpenQuickWindow
        && existingQuickWindowInvocationKey is not null
        && existingParameterSheetKey is null
        && existingQuickWindowPageKey is null
        && currentParameterSheetKey is null
        && currentQuickWindowPageKey is null;
}
