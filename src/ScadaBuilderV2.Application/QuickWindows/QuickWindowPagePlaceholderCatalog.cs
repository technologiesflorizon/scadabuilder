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
/// Pure and deterministic: no I/O, no WPF. Every parameter sheet of the project contributes, always: a sheet is
/// never filtered by the `Ouvrir une fenêtre rapide` commands that link it (or not) to a page, so no scene
/// other than the active page's is needed. That scene is only used to keep a `#N` the page references but no
/// sheet defines. Entries, captions, `Writeable` and `Datatype` all derive from every sheet.
///
/// Caption (stored in `Device`, display only): `Paramètre — ex. : {BP_1A_AUTO}, {BP_1B_AUTO}` - the distinct
/// sources only (no sheet name), at most <c>MaxDisplayedSources</c> of them followed by `, … (+N)` for the N
/// not shown, and never longer than <c>MaxLabelLength</c> characters.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5, §6.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPagePlaceholderCatalogTests.cs.
/// </remarks>
public static class QuickWindowPagePlaceholderCatalog
{
    private const string SchemaId = "quick-window-page-placeholder-parameters/2.0";
    private const string DeviceLabel = "Paramètre";
    private const int MaxLabelLength = 80;
    private const int MaxDisplayedSources = 3;

    /// <summary>
    /// Builds the catalog of a quick-window page: the union of `#N` entries defined by every parameter sheet of
    /// <paramref name="project"/>, plus any `#N` the page itself references but no sheet defines.
    /// </summary>
    /// <param name="project">The project owning the parameter sheets and the project tag catalog.</param>
    /// <param name="pageScene">The scene of the page the catalog is built for, or <c>null</c> when unavailable.</param>
    public static ScadaTagCatalog Build(ScadaProject project, ScadaScene? pageScene)
    {
        ArgumentNullException.ThrowIfNull(project);

        var byNumber = new SortedDictionary<int, List<(ParameterSheet Sheet, ParameterEntry Entry)>>();
        foreach (var sheet in project.EffectiveParameterSheets)
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

        // Ruling: a `#N` the page itself still references but no sheet defines must stay in the
        // catalog (marked undefined) or the current value of an existing state/command would vanish from the
        // selector the moment this catalog replaces the static #1..#999 list.
        if (pageScene is not null)
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

        // Caption: the distinct sources only, in a deterministic order (sheet name ordinal-ignore-case, then
        // SheetKey), bounded in count and length by BuildCaption.
        var sources = contributions
            .OrderBy(contribution => SheetCaptionName(contribution.Sheet), StringComparer.OrdinalIgnoreCase)
            .ThenBy(contribution => contribution.Sheet.SheetKey)
            .Select(contribution => DescribeSource(contribution.Entry))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var device = BuildCaption(sources);

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

    private static string SheetCaptionName(ParameterSheet sheet) =>
        string.IsNullOrWhiteSpace(sheet.DisplayName) ? sheet.Code : sheet.DisplayName;

    /// <summary>
    /// Builds `Paramètre — ex. : a, b, c, … (+N)` from the distinct <paramref name="sources"/>: at most
    /// <c>MaxDisplayedSources</c> values are shown, the others are counted in `+N`, and the whole label never
    /// exceeds <c>MaxLabelLength</c> characters. Whole values are dropped (and counted) before anything is cut;
    /// only when the first value alone does not fit is it truncated with "…".
    /// </summary>
    private static string BuildCaption(IReadOnlyList<string> sources)
    {
        var prefix = $"{DeviceLabel} — ex. : ";
        for (var shown = Math.Min(MaxDisplayedSources, sources.Count); shown >= 1; shown--)
        {
            var caption = prefix + string.Join(", ", sources.Take(shown)) + MoreSuffix(sources.Count - shown);
            if (caption.Length <= MaxLabelLength) return caption;
        }

        var suffix = MoreSuffix(sources.Count - 1);
        var room = Math.Max(0, MaxLabelLength - prefix.Length - suffix.Length - 1);
        return prefix + sources[0][..Math.Min(room, sources[0].Length)] + "…" + suffix;
    }

    private static string MoreSuffix(int hidden) => hidden > 0 ? $", … (+{hidden})" : string.Empty;
}
