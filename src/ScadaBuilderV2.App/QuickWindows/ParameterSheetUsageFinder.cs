using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>One `Ouvrir une fenêtre rapide` command that references a parameter sheet (DEC-0052).</summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetUsageFinderTests.cs.
/// </remarks>
public sealed record ParameterSheetUsage(string PageCode, string ElementId, string CommandId, string CommandName);

/// <summary>
/// Finds every open-quick-window command that references one parameter sheet, without WPF or file I/O, so
/// deletion can warn the operator before removing a sheet that is still in use (DEC-0052).
/// </summary>
/// <remarks>
/// Mirrors the element-flattening walk of <c>QuickWindowDependencyAnalyzer</c>: pages are visited in their
/// durable project order, and each page's elements are visited depth-first so a command nested in a child
/// element is found exactly like a top-level one.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetUsageFinderTests.cs.
/// </remarks>
public static class ParameterSheetUsageFinder
{
    /// <summary>Returns every open-quick-window command whose `ParameterSheetKey` equals <paramref name="sheetKey"/>.</summary>
    public static IReadOnlyList<ParameterSheetUsage> Find(PageWorkspaceSnapshot snapshot, Guid sheetKey)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var usages = new List<ParameterSheetUsage>();

        foreach (var page in snapshot.Project.Scenes)
        {
            if (!snapshot.Scenes.TryGetValue(page.PageKey, out var scene)) continue;

            foreach (var element in Flatten(scene.Elements))
            {
                foreach (var command in element.EffectiveCommandConfig.Commands)
                {
                    if (command.Kind != ScadaCommandKind.OpenQuickWindow || command.ParameterSheetKey != sheetKey) continue;
                    usages.Add(new ParameterSheetUsage(page.EffectivePageCode, element.Id, command.Id, command.Name));
                }
            }
        }

        return usages;
    }

    private static IEnumerable<ScadaElement> Flatten(IEnumerable<ScadaElement> elements)
    {
        foreach (var element in elements)
        {
            yield return element;
            foreach (var child in Flatten(element.ChildElements))
                yield return child;
        }
    }
}
