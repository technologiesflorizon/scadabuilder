using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.QuickWindows;

/// <summary>
/// Builds the `#N` tag catalog offered to the state/command selectors of a page authored as
/// <see cref="ScadaPageType.QuickWindow"/>, from the project's parameter sheets instead of a static
/// `#1`..`#999` placeholder list (DEC-0052).
/// </summary>
/// <remarks>
/// Pure and deterministic: no I/O, no WPF. <see cref="Build"/> only sees the scenes its caller already has in
/// memory - it never captures a workspace snapshot itself, so a page whose linking command lives on a page not
/// currently loaded cannot be found; that case degrades to the "no sheet linked" rule (every sheet retained),
/// which is always at least as permissive as the real answer and never hides a currently used value.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5, §6.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPagePlaceholderCatalogTests.cs.
/// </remarks>
public static class QuickWindowPagePlaceholderCatalog
{
    private const string SchemaId = "quick-window-page-placeholder-parameters/2.0";
    private const string DeviceLabel = "Paramètre";
    private const int MaxLabelLength = 160;

    /// <summary>
    /// Builds the catalog for <paramref name="pageKey"/>: the union of `#N` entries defined by every parameter
    /// sheet linked to that page by an `Ouvrir une fenêtre rapide` command (or, when none is linked yet, every
    /// sheet of the project), plus any `#N` the page itself references but no retained sheet defines.
    /// </summary>
    /// <param name="project">The project owning the parameter sheets and the project tag catalog.</param>
    /// <param name="scenes">Every scene currently available in memory, keyed by page key; need not be complete.</param>
    /// <param name="pageKey">The quick-window page the catalog is built for.</param>
    public static ScadaTagCatalog Build(ScadaProject project, IReadOnlyDictionary<Guid, ScadaScene> scenes, Guid pageKey)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(scenes);

        var linkedSheetKeys = FindLinkedSheetKeys(scenes, pageKey);
        var retainedSheets = linkedSheetKeys.Count > 0
            ? project.EffectiveParameterSheets.Where(sheet => linkedSheetKeys.Contains(sheet.SheetKey)).ToArray()
            : project.EffectiveParameterSheets;

        var byNumber = new SortedDictionary<int, List<(ParameterSheet Sheet, ParameterEntry Entry)>>();
        foreach (var sheet in retainedSheets)
        {
            var parse = ParameterSheetParser.Parse(sheet.Text);
            foreach (var entry in parse.Entries.Values)
            {
                if (!byNumber.TryGetValue(entry.Number, out var contributions))
                {
                    contributions = new List<(ParameterSheet Sheet, ParameterEntry Entry)>();
                    byNumber[entry.Number] = contributions;
                }

                contributions.Add((sheet, entry));
            }
        }

        // Ruling: a `#N` the page itself still references but no retained sheet defines must stay in the
        // catalog (marked undefined) or the current value of an existing state/command would vanish from the
        // selector the moment this catalog replaces the static #1..#999 list.
        if (scenes.TryGetValue(pageKey, out var pageScene))
        {
            var (usages, _) = QuickWindowPlaceholderUsageCollector.Collect(pageScene);
            foreach (var usage in usages)
            {
                if (!byNumber.ContainsKey(usage.Number))
                {
                    byNumber[usage.Number] = new List<(ParameterSheet Sheet, ParameterEntry Entry)>();
                }
            }
        }

        var tags = byNumber
            .Select(pair => BuildTagDefinition(pair.Key, pair.Value, project.TagCatalog))
            .ToArray();

        return new ScadaTagCatalog(SchemaId, tags);
    }

    /// <summary>
    /// Returns the `SheetKey` of every parameter sheet paired with <paramref name="pageKey"/> by an
    /// `Ouvrir une fenêtre rapide` command, wherever that command is posed among <paramref name="scenes"/>.
    /// Mirrors the element-flattening walk of <c>QuickWindowDependencyAnalyzer</c>/<c>ParameterSheetUsageFinder</c>.
    /// </summary>
    private static HashSet<Guid> FindLinkedSheetKeys(IReadOnlyDictionary<Guid, ScadaScene> scenes, Guid pageKey)
    {
        var keys = new HashSet<Guid>();
        foreach (var scene in scenes.Values)
        {
            foreach (var element in Flatten(scene.Elements ?? Array.Empty<ScadaElement>()))
            {
                foreach (var command in element.EffectiveCommandConfig.Commands)
                {
                    if (command.Kind == ScadaCommandKind.OpenQuickWindow &&
                        command.QuickWindowPageKey == pageKey &&
                        command.ParameterSheetKey is { } sheetKey)
                    {
                        keys.Add(sheetKey);
                    }
                }
            }
        }

        return keys;
    }

    private static ScadaTagDefinition BuildTagDefinition(
        int number,
        IReadOnlyList<(ParameterSheet Sheet, ParameterEntry Entry)> contributions,
        ScadaTagCatalog? projectTagCatalog)
    {
        var id = $"#{number}";
        if (contributions.Count == 0)
        {
            return new ScadaTagDefinition(id, $"{id} — (non défini dans les feuilles)", Device: DeviceLabel, Writeable: false, Enabled: true);
        }

        var sources = contributions
            .Select(contribution => DescribeSource(contribution.Entry))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var label = Truncate($"{id} — {string.Join(" / ", sources)}");

        // Writeable/Datatype ruling (brief §1): only a single resolved Tag-kind contribution carries the
        // project tag's Writeable/Datatype; a literal, an expression, or more than one Tag contribution
        // (several sheets may legitimately bind the same #N to different tags) keeps the safe Writeable: false
        // default rather than guessing which contribution should win.
        var tagContributions = contributions.Where(contribution => contribution.Entry.Kind == ParameterValueKind.Tag).ToArray();
        var writeable = false;
        string? datatype = null;
        if (tagContributions.Length == 1 && projectTagCatalog is not null)
        {
            var tagName = ((ScadaExprTagRef)tagContributions[0].Entry.Node).TagName;
            var resolved = ScadaExpressionValidator.TryResolveTagReference(tagName, projectTagCatalog);
            if (resolved.Status == TagResolveStatus.Resolved)
            {
                var tag = projectTagCatalog.Tags.FirstOrDefault(candidate => string.Equals(candidate.Id, resolved.CanonicalId, StringComparison.Ordinal));
                if (tag is not null)
                {
                    writeable = tag.Writeable;
                    datatype = tag.Datatype;
                }
            }
        }

        return new ScadaTagDefinition(id, label, Device: DeviceLabel, Datatype: datatype, Writeable: writeable, Enabled: true);
    }

    private static string DescribeSource(ParameterEntry entry) => entry.Kind switch
    {
        ParameterValueKind.Tag => $"{{{((ScadaExprTagRef)entry.Node).TagName}}}",
        ParameterValueKind.Literal => $"\"{((ScadaExprLiteralString)entry.Node).Value}\"",
        _ => entry.Source
    };

    /// <summary>
    /// Returns whether every one of <paramref name="tags"/> is shaped like a `#N` placeholder Id (DEC-0052).
    /// </summary>
    /// <remarks>
    /// Used by the state/command selectors to skip their usual DisplayName sort when offered this catalog:
    /// <see cref="Build"/> already returns its tags in ascending numeric order, and a lexical re-sort of
    /// `#1 — ...`, `#2 — ...`, `#10 — ...` would scramble them back to `#1, #10, #100, ...` - the very bug
    /// this catalog exists to fix. An ordinary project tag catalog is never composed entirely of `#N` ids
    /// (and a mixed one isn't this catalog at all), so this never changes the sort of a normal page's tags.
    /// </remarks>
    public static bool IsPlaceholderCatalog(IReadOnlyCollection<ScadaTagDefinition> tags) =>
        tags.Count > 0 && tags.All(tag => QuickWindowPlaceholder.TryParseName(tag.Id, out _));

    private static string Truncate(string label) =>
        label.Length <= MaxLabelLength ? label : label[..(MaxLabelLength - 1)] + "…";

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
