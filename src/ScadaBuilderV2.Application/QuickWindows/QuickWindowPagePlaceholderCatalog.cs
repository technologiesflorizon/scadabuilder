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
/// Pure and deterministic: no I/O, no WPF. <see cref="Build"/> never captures a workspace snapshot itself; it
/// only sees the <paramref name="scenes"/> its caller passes in, so finding every linking command project-wide
/// is the caller's responsibility (fix round 1, DEC-0052: the App layer passes a coherent workspace snapshot's
/// scenes, overlaid with every open tab's live scene, from <c>MainWindow.BuildActiveQuickWindowPagePlaceholderCatalog</c>).
/// A caller that passes a partial <paramref name="scenes"/> map degrades safely rather than incorrectly: a
/// linking command on a page missing from that map is not found, which falls back to the "no sheet linked"
/// rule (every sheet retained) - always at least as permissive as the real answer, never narrower.
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
        // DisplayName MUST equal Id exactly (fix round 2, bug report): DisplayName is an identifier-bearing
        // field throughout the app, not a display-only one - ElementStateRuleDialog.BuildExpressionFromVariable
        // and ExpressionCreationDialog compose `{tag.DisplayName}` directly into expression source text, and
        // ScadaExpressionValidator.TryResolveTagReference matches a parsed `{...}` token against DisplayName
        // as its second resolution step. Putting the sheet's source description there produced
        // `{#1 — {BP_1A_AUTO}}` - unparsable ("Caractere inattendu : '}'") - the moment a state rule or
        // expression was composed from the dropdown. The source description now lives in Device, which every
        // consumer treats as caption-only (`AuthoringLabel`'s ` | {Device}` segment, the `TagLabel` helpers'
        // ` ({Device})` suffix, the Catalogue Tags panel and its device filter - all display text, never
        // parsed, matched, or persisted as an identifier; audited across every `ScadaTagDefinition.Device`/
        // `.DisplayName` consumer in App/Application/Domain before this change).
        var id = $"#{number}";
        if (contributions.Count == 0)
        {
            // Writeable ruling (fix round 1): an undefined #N still already reaches a write site on the page
            // (see the "Plus" rule above) and must stay offered to the command selector's write-tag combo
            // (TableCellNumericInputInspector.Inspect / TableEditCoordinator.SetCellValueBinding filter/refuse
            // on Writeable) until the sheet is fixed; refusing it outright would be a regression on an
            // already-authored write command, worse than the bug this catalog fixes.
            return new ScadaTagDefinition(id, id, Device: $"{DeviceLabel} — (non défini dans les feuilles)", Writeable: true, Enabled: true);
        }

        var sources = contributions
            .Select(contribution => DescribeSource(contribution.Entry))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var device = Truncate($"{DeviceLabel} — {string.Join(" / ", sources)}");

        // Writeable/Datatype ruling (fix round 1): several sheets may legitimately bind the same #N to
        // different tags (e.g. two fan speed setpoints), so Writeable does not require a single contribution -
        // it requires every contribution to be a Tag reference that resolves to a writable project tag.
        // Writeable is false as soon as one contribution is a literal/expression, or one Tag reference does
        // not resolve, or resolves to a non-writable tag. Datatype is reported only when every resolved Tag
        // contribution agrees; a mix (or none resolved) reports no datatype rather than guessing one.
        var tagContributions = contributions.Where(contribution => contribution.Entry.Kind == ParameterValueKind.Tag).ToArray();
        var hasNonTagContribution = tagContributions.Length != contributions.Count;
        var resolvedTags = new List<ScadaTagDefinition>();
        if (projectTagCatalog is not null)
        {
            foreach (var contribution in tagContributions)
            {
                var tagName = ((ScadaExprTagRef)contribution.Entry.Node).TagName;
                var resolved = ScadaExpressionValidator.TryResolveTagReference(tagName, projectTagCatalog);
                if (resolved.Status != TagResolveStatus.Resolved) continue;
                var tag = projectTagCatalog.Tags.FirstOrDefault(candidate => string.Equals(candidate.Id, resolved.CanonicalId, StringComparison.Ordinal));
                if (tag is not null) resolvedTags.Add(tag);
            }
        }

        var writeable = !hasNonTagContribution &&
            tagContributions.Length > 0 &&
            resolvedTags.Count == tagContributions.Length &&
            resolvedTags.All(tag => tag.Writeable);

        var distinctDatatypes = resolvedTags.Select(tag => tag.Datatype).Distinct(StringComparer.Ordinal).ToArray();
        var datatype = resolvedTags.Count > 0 && distinctDatatypes.Length == 1 ? distinctDatatypes[0] : null;

        return new ScadaTagDefinition(id, id, Device: device, Datatype: datatype, Writeable: writeable, Enabled: true);
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
    /// `#1`, `#2`, `#10` (every tag's <c>DisplayName</c> equals its <c>Id</c> exactly, fix round 2) would
    /// scramble them back to `#1, #10, #100, ...` - the very bug this catalog exists to fix. An ordinary
    /// project tag catalog is never composed entirely of `#N` ids (and a mixed one isn't this catalog at all),
    /// so this never changes the sort of a normal page's tags.
    /// </remarks>
    public static bool IsPlaceholderCatalog(IReadOnlyCollection<ScadaTagDefinition> tags) =>
        tags.Count > 0 && tags.All(tag => QuickWindowPlaceholder.TryParseName(tag.Id, out _));

    /// <summary>
    /// Orders <paramref name="tags"/> for one tag selector: unchanged (already numeric, per <see cref="Build"/>)
    /// when <see cref="IsPlaceholderCatalog"/> holds, otherwise sorted by <see cref="ScadaTagDefinition.DisplayName"/>
    /// exactly as every selector did before this catalog existed (fix round 1, DEC-0052).
    /// </summary>
    /// <remarks>
    /// Shared by every tag selector (`ElementStateRuleDialog`, `ElementCommandDialog`, `TagSelectionDialog`,
    /// `ElementReadVariableDialog`, `ElementEventDialog`) so a sixth selector added later cannot reintroduce the
    /// reported bug by re-sorting this catalog lexically. <paramref name="comparer"/> lets each caller keep its
    /// own pre-existing string comparer for a normal project tag catalog; passing none reproduces a plain
    /// `OrderBy(tag => tag.DisplayName)` with no comparer argument, exactly as before.
    /// </remarks>
    public static IReadOnlyList<ScadaTagDefinition> OrderForSelector(
        IEnumerable<ScadaTagDefinition> tags,
        IComparer<string>? comparer = null)
    {
        var materialized = tags as ScadaTagDefinition[] ?? tags.ToArray();
        if (IsPlaceholderCatalog(materialized)) return materialized;
        return comparer is null
            ? materialized.OrderBy(tag => tag.DisplayName).ToArray()
            : materialized.OrderBy(tag => tag.DisplayName, comparer).ToArray();
    }

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
