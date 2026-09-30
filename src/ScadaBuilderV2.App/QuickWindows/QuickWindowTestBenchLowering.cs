using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// The lowered definition and invocation of one `Banc d'essai` preview request, or null when lowering
/// carried a blocking error; <see cref="Diagnostics"/> is always populated for display in the bench.
/// </summary>
public sealed record QuickWindowTestBenchLoweringResult(
    QuickWindowDefinition? Definition,
    QuickWindowInvocation? Invocation,
    IReadOnlyList<ScadaBuildValidationIssue> Diagnostics);

/// <summary>
/// Lowers, on an in-memory snapshot only, the (sheet, active page) pair the `Banc d'essai` previews
/// (DEC-0052, spec §5): a synthetic "Ouvrir une fenêtre rapide" command pairing the chosen sheet with the
/// active quick-window page is added to a throwaway caller page, never to the real project, and the whole
/// snapshot is lowered by the same <see cref="ParameterSheetLowering"/> the build and export paths use.
/// </summary>
/// <remarks>
/// Nothing this type builds or returns is ever assigned back onto <see cref="ScadaProject"/>: the caller
/// only reads <see cref="QuickWindowTestBenchLoweringResult"/> to feed the preview. No dirty flag, no
/// history entry, no persisted sheet/page/command is ever touched.
///
/// Decisions: DEC-0050, DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5, §6, §7.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowTestBenchLoweringTests.cs.
/// </remarks>
public static class QuickWindowTestBenchLowering
{
    private const string SyntheticPageCode = "qw-test-bench";
    private const string SyntheticElementId = "qw-test-bench-open";
    private const string SyntheticCommandId = "open";

    /// <summary>
    /// Lowers the (<paramref name="sheet"/>, <paramref name="activePageKey"/>) pair on a snapshot built
    /// from <paramref name="project"/> and <paramref name="activePage"/>; never mutates either.
    /// </summary>
    public static QuickWindowTestBenchLoweringResult Lower(
        ScadaProject project,
        ScadaScene activePage,
        Guid activePageKey,
        ParameterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(activePage);
        ArgumentNullException.ThrowIfNull(sheet);

        var activePageRef = project.Scenes.FirstOrDefault(page => page.PageKey == activePageKey);
        if (activePageRef is null)
        {
            return new QuickWindowTestBenchLoweringResult(null, null, [
                new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "parameter-sheet.page-missing",
                    "La page active n'a pas pu être retrouvée dans le projet.")
            ]);
        }

        var syntheticPageKey = Guid.NewGuid();
        var syntheticElement = ScadaElement.CreateButton(SyntheticElementId, "Banc d'essai", 0, 0) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding(
                    SyntheticCommandId,
                    "Ouvrir",
                    true,
                    ScadaCommandTrigger.OnClick,
                    ScadaCommandKind.OpenQuickWindow,
                    ParameterSheetKey: sheet.SheetKey,
                    QuickWindowPageKey: activePageKey)
            ])
        };
        var syntheticPageRef = new ScadaSceneReference(
            SyntheticPageCode,
            "Banc d'essai",
            $"scenes/{SyntheticPageCode}.scene.json",
            ScadaPageType.Default,
            PageKey: syntheticPageKey,
            PageCode: SyntheticPageCode);
        var syntheticScene = ScadaScene.CreateEmpty(SyntheticPageCode, "Banc d'essai", new CanvasSize(10, 10)) with
        {
            PageKey = syntheticPageKey,
            PageCode = SyntheticPageCode,
            PageType = ScadaPageType.Default,
            Elements = [syntheticElement]
        };

        var snapshotProject = project with
        {
            Scenes = [activePageRef, syntheticPageRef],
            ParameterSheets = [sheet],
            QuickWindows = [],
            QuickWindowInvocations = []
        };
        var scenes = new Dictionary<Guid, ScadaScene>
        {
            [activePageKey] = activePage,
            [syntheticPageKey] = syntheticScene
        };

        var lowered = ParameterSheetLowering.Lower(snapshotProject, scenes);
        var hasErrors = lowered.Diagnostics.Any(issue => issue.Severity == ScadaBuildValidationSeverity.Error);
        var definition = lowered.Project.EffectiveQuickWindows.FirstOrDefault(item => item.DefinitionKey == activePageKey);
        var invocation = lowered.Project.EffectiveQuickWindowInvocations.FirstOrDefault(item => item.DefinitionKey == activePageKey);

        return new QuickWindowTestBenchLoweringResult(
            hasErrors ? null : definition,
            hasErrors ? null : invocation,
            lowered.Diagnostics);
    }
}
