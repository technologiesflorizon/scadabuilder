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
/// Controller fix round 1: the snapshot carries every sheet (<see cref="ScadaProject.EffectiveParameterSheets"/>)
/// and every `QuickWindow`-typed page reference of <paramref name="project"/> — not only the chosen sheet
/// and the active page — because <see cref="ParameterSheetLowering"/> lowers every authored
/// "Ouvrir une fenêtre rapide" command it finds anywhere in the snapshot, including one nested inside the
/// active page's own content that pairs a *different* sheet with a *different* page. Trimming either list
/// down to the one pair under test made such an unrelated, otherwise-correct command look broken
/// (`parameter-sheet.sheet-missing`/`parameter-sheet.page-not-quick-window`) purely because its sheet or
/// target page was missing from the snapshot, not because anything is actually wrong with it. The preview
/// is still refused only on an error that concerns the pairing under test — the synthetic command itself,
/// the active page's own definition, or the chosen sheet's own syntax (<see cref="IsBlocking"/>) — every
/// other diagnostic (e.g. about an unrelated command, or a nested target page whose scene is not loaded)
/// is still returned in <see cref="QuickWindowTestBenchLoweringResult.Diagnostics"/>, shown in the bench,
/// but never blocks the preview.
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

        // Every QuickWindow page reference travels into the snapshot (the active page included, since it
        // is itself QuickWindow-typed); a nested open-quick-window command elsewhere in the project may
        // target any of them, and excluding one produces a false "page-not-quick-window"/page-missing
        // diagnostic for a pairing that is not even the one under test.
        var quickWindowPageRefs = project.Scenes.Where(page => page.Type == ScadaPageType.QuickWindow).ToArray();
        if (!quickWindowPageRefs.Any(page => page.PageKey == activePageKey))
        {
            quickWindowPageRefs = [.. quickWindowPageRefs, activePageRef];
        }

        var snapshotProject = project with
        {
            Scenes = [.. quickWindowPageRefs, syntheticPageRef],
            ParameterSheets = project.EffectiveParameterSheets,
            QuickWindows = [],
            QuickWindowInvocations = []
        };
        var scenes = new Dictionary<Guid, ScadaScene>
        {
            [activePageKey] = activePage,
            [syntheticPageKey] = syntheticScene
        };

        var lowered = ParameterSheetLowering.Lower(snapshotProject, scenes);
        var hasBlockingErrors = lowered.Diagnostics.Any(issue => IsBlocking(issue, activePageKey, sheet.SheetKey));
        var definition = lowered.Project.EffectiveQuickWindows.FirstOrDefault(item => item.DefinitionKey == activePageKey);
        var invocation = lowered.Project.EffectiveQuickWindowInvocations.FirstOrDefault(item => item.DefinitionKey == activePageKey);

        return new QuickWindowTestBenchLoweringResult(
            hasBlockingErrors ? null : definition,
            hasBlockingErrors ? null : invocation,
            lowered.Diagnostics);
    }

    /// <summary>
    /// Returns whether one lowering diagnostic concerns the pairing under test — the synthetic caller
    /// command, the active page's own definition build (a page-level diagnostic, never tied to a specific
    /// authored command), or the chosen sheet's own syntax — and must therefore refuse the preview. Every
    /// other error (an unrelated command elsewhere in the snapshot, a nested target page's missing scene,
    /// …) is real and shown, but never blocks a pairing it does not describe.
    /// </summary>
    private static bool IsBlocking(ScadaBuildValidationIssue issue, Guid activePageKey, Guid sheetKey) =>
        issue.Severity == ScadaBuildValidationSeverity.Error &&
        (
            (issue.ElementId == SyntheticElementId && issue.CommandId == SyntheticCommandId) ||
            (issue.CommandId is null && issue.PageKey == activePageKey) ||
            (issue.Code == "parameter-sheet.syntax" && issue.TargetKey == sheetKey)
        );
}
