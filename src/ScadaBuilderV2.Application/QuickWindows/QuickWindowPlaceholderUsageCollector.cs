using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.QuickWindows;

/// <summary>Where a `#N` placeholder is used inside a quick-window page.</summary>
public enum PlaceholderUsageSite
{
    /// <summary><c>Data.ReadTagId</c>, <c>Data.TagBinding</c> or a table cell read binding.</summary>
    ReadTag,
    /// <summary><c>Data.WriteTagId</c> or a table cell write binding.</summary>
    WriteTag,
    /// <summary>A <c>{#N}</c> tag reference inside a display-state expression.</summary>
    StateExpression,
    /// <summary>The read-variable display rule.</summary>
    ReadVariable,
    /// <summary>The tag written by an Element+ command.</summary>
    CommandWriteTag,
    /// <summary>The tag read back by an Element+ command.</summary>
    CommandReadTag,
    /// <summary>A token inside a displayed text.</summary>
    Text
}

/// <summary>One use of placeholder <paramref name="Number"/> by an element (and optionally one of its commands).</summary>
public sealed record PlaceholderUsage(int Number, PlaceholderUsageSite Site, string ElementId, string? CommandId);

/// <summary>A `#…` token of a displayed text that is not a valid placeholder, such as <c>#0</c> or <c>#012</c>.</summary>
public sealed record InvalidPlaceholderToken(string Raw, string ElementId);

/// <summary>
/// A valid `#N` (or `{#N}`) token found in a field the mount-time substitution never rewrites, such as a
/// state effect's text, a read-variable display format, a command confirmation or an input's attribute.
/// </summary>
/// <param name="Raw">The token as written, e.g. <c>#4</c>.</param>
/// <param name="ElementId">The element carrying the field.</param>
/// <param name="Field">The field, named for the diagnostic (e.g. <c>StateConfig.States[run].Effect.TextContent</c>).</param>
public sealed record UnsupportedPlaceholderUse(string Raw, string ElementId, string Field);

/// <summary>Collects every `#N` placeholder used by a quick-window page.</summary>
/// <remarks>
/// Only binding fields and displayed texts are read; styles and colours never are, so <c>#0F2A30</c> can never
/// be taken for a placeholder. Elements are walked depth-first in document order, children included.
///
/// A displayed text is exactly what the exporter (<c>Ft100SceneExporter.BuildElementContent</c>,
/// <c>ModernTableHtmlRenderer</c>) emits as an HTML text node — the only place the runtime substitutes a text
/// `#N` — including its fallbacks (<c>DisplayName</c>, <c>Placeholder</c>). The markup of a Custom/SVG element
/// is never read, and <c>LegacyPayload.Text</c> is never read either: the exporter never renders it (a
/// converted legacy text renders through <c>Data.Text</c>, which carries the same text), so reading it too
/// would only report the same token twice.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §4.4, §6.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetLoweringTests.cs.
/// </remarks>
public static class QuickWindowPlaceholderUsageCollector
{
    /// <summary>Collects the placeholder usages and invalid text tokens of <paramref name="scene"/>.</summary>
    public static (IReadOnlyList<PlaceholderUsage> Usages, IReadOnlyList<InvalidPlaceholderToken> Invalid) Collect(ScadaScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var usages = new List<PlaceholderUsage>();
        var invalid = new List<InvalidPlaceholderToken>();
        foreach (var element in scene.Elements ?? [])
        {
            Visit(element, usages, invalid);
        }

        return (usages, invalid);
    }

    /// <summary>
    /// Collects every valid `#N` token of <paramref name="scene"/> written in a field the mount-time substitution
    /// never rewrites: a state effect's <c>TextContent</c> (states, default effect, quality fallback), a
    /// read-variable <c>DisplayFormat</c>, a command confirmation message, and the <c>value</c>/<c>placeholder</c>
    /// attributes of an input (element or table cell). Such a token would reach the operator as a raw `#N`, so the
    /// lowering refuses it (fail-closed) rather than exporting it.
    /// </summary>
    public static IReadOnlyList<UnsupportedPlaceholderUse> CollectUnsupported(ScadaScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var result = new List<UnsupportedPlaceholderUse>();
        foreach (var element in scene.Elements ?? [])
        {
            VisitUnsupported(element, result);
        }

        return result;
    }

    /// <summary>
    /// Returns <paramref name="scene"/> with every `\#` of its displayed texts (the same fields
    /// <see cref="Collect"/> reads as <see cref="PlaceholderUsageSite.Text"/>) turned into a literal `#`. Used for a
    /// quick-window page with no `#N` at all: the runtime only substitutes (and unescapes) a page that has
    /// placeholder members, so without this `\#` would be shown as is.
    /// </summary>
    public static ScadaScene UnescapeDisplayedTexts(ScadaScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var elements = scene.Elements ?? [];
        var rewritten = elements.Select(UnescapeDisplayedTexts).ToArray();
        return rewritten.SequenceEqual(elements, ReferenceEqualityComparer.Instance)
            ? scene
            : scene with { Elements = rewritten };
    }

    private static ScadaElement UnescapeDisplayedTexts(ScadaElement element)
    {
        var result = element;
        var data = element.Data;
        switch (element.Kind)
        {
            case ScadaElementKind.Text when data?.Text is not null:
                result = result with { Data = data with { Text = QuickWindowPlaceholder.UnescapeText(data.Text) } };
                break;
            case ScadaElementKind.Text:
                result = result with { DisplayName = QuickWindowPlaceholder.UnescapeText(element.DisplayName) };
                break;
            case ScadaElementKind.Button when data?.Text is not null:
                result = result with { Data = data with { Text = QuickWindowPlaceholder.UnescapeText(data.Text) } };
                break;
            case ScadaElementKind.Button when data?.Placeholder is not null:
                result = result with { Data = data with { Placeholder = QuickWindowPlaceholder.UnescapeText(data.Placeholder) } };
                break;
            case ScadaElementKind.Button:
                result = result with { DisplayName = QuickWindowPlaceholder.UnescapeText(element.DisplayName) };
                break;
            case ScadaElementKind.InputNumeric when data is { IsReadOnly: true, Value: null, DisplayFormat: not null }:
                result = result with { Data = data with { DisplayFormat = QuickWindowPlaceholder.UnescapeText(data.DisplayFormat) } };
                break;
            case ScadaElementKind.InputNumeric when data is { IsReadOnly: true, Value: null, Placeholder: not null }:
                result = result with { Data = data with { Placeholder = QuickWindowPlaceholder.UnescapeText(data.Placeholder) } };
                break;
            case ScadaElementKind.Table when element.Table is { } table:
                var cells = table.EffectiveCells
                    .Select(cell => cell.EffectiveContent.Kind == ScadaTableCellContentKind.Text && cell.Content is { } content
                        ? cell with { Content = content with { Text = QuickWindowPlaceholder.UnescapeText(content.Text) } }
                        : cell)
                    .ToArray();
                result = result with { Table = table with { Cells = cells } };
                break;
        }

        if (element.Children is { Count: > 0 } children)
        {
            result = result with { Children = children.Select(UnescapeDisplayedTexts).ToArray() };
        }

        return result == element ? element : result;
    }

    /// <summary>
    /// The texts <paramref name="element"/> renders as HTML text nodes, mirroring
    /// <c>Ft100SceneExporter.BuildElementContent</c>/<c>BuildButton</c> and <c>ModernTableHtmlRenderer</c> fallbacks.
    /// </summary>
    private static IEnumerable<string?> DisplayedTexts(ScadaElement element)
    {
        var data = element.Data;
        switch (element.Kind)
        {
            case ScadaElementKind.Text:
                yield return data?.Text ?? element.DisplayName;
                break;
            case ScadaElementKind.Button:
                yield return data?.Text ?? data?.Placeholder ?? element.DisplayName;
                break;
            case ScadaElementKind.InputNumeric when data is { IsReadOnly: true, Value: null }:
                yield return data.DisplayFormat ?? data.Placeholder;
                break;
            case ScadaElementKind.Table:
                foreach (var cell in element.Table?.EffectiveCells ?? [])
                {
                    if (cell.EffectiveContent.Kind == ScadaTableCellContentKind.Text) yield return cell.EffectiveContent.Text;
                }

                break;
        }
    }

    private static void VisitUnsupported(ScadaElement element, List<UnsupportedPlaceholderUse> result)
    {
        var id = element.Id;
        void Scan(string? text, string field)
        {
            foreach (var token in QuickWindowPlaceholder.FindInText(text))
            {
                if (token.Number is not null) result.Add(new UnsupportedPlaceholderUse(token.Raw, id, field));
            }
        }

        if (element.StateConfig is { } state)
        {
            foreach (var rule in state.States ?? [])
            {
                Scan(rule.Effect?.TextContent, $"StateConfig.States[{rule.Id}].Effect.TextContent");
            }

            Scan(state.DefaultEffect?.TextContent, "StateConfig.DefaultEffect.TextContent");
            Scan(state.QualityFallback?.TextContent, "StateConfig.QualityFallback.TextContent");
            Scan(state.ReadVariable?.DisplayFormat, "StateConfig.ReadVariable.DisplayFormat");
        }

        foreach (var command in element.CommandConfig?.Commands ?? [])
        {
            Scan(command.Confirmation?.Message, $"CommandConfig.Commands[{command.Id}].Confirmation.Message");
        }

        var data = element.Data;
        switch (element.Kind)
        {
            case ScadaElementKind.InputText:
                Scan(data?.Text, "Data.Text");
                Scan(data?.Placeholder, "Data.Placeholder");
                break;
            case ScadaElementKind.InputNumeric when data is not { IsReadOnly: true }:
                Scan(data?.Placeholder, "Data.Placeholder");
                break;
            case ScadaElementKind.Table:
                foreach (var cell in element.Table?.EffectiveCells ?? [])
                {
                    var content = cell.EffectiveContent;
                    if (content.Kind == ScadaTableCellContentKind.Text) continue;
                    Scan(content.Text, $"Table.Cells[{cell.Row},{cell.Column}].Content.Text");
                    Scan(content.Placeholder, $"Table.Cells[{cell.Row},{cell.Column}].Content.Placeholder");
                }

                break;
        }

        foreach (var child in element.ChildElements)
        {
            VisitUnsupported(child, result);
        }
    }

    private static void Visit(ScadaElement element, List<PlaceholderUsage> usages, List<InvalidPlaceholderToken> invalid)
    {
        var id = element.Id;

        if (element.Data is { } data)
        {
            AddExact(usages, data.ReadTagId, PlaceholderUsageSite.ReadTag, id, null);
            AddExact(usages, data.TagBinding, PlaceholderUsageSite.ReadTag, id, null);
            AddExact(usages, data.WriteTagId, PlaceholderUsageSite.WriteTag, id, null);
        }

        foreach (var text in DisplayedTexts(element))
        {
            AddText(usages, invalid, text, id);
        }

        if (element.StateConfig is { } state)
        {
            foreach (var rule in state.States ?? [])
            {
                if (rule.Expression?.Ast is { } ast)
                {
                    AddExpression(usages, ast, id);
                }
            }

            AddExact(usages, state.ReadVariable?.TagId, PlaceholderUsageSite.ReadVariable, id, null);
        }

        foreach (var command in element.CommandConfig?.Commands ?? [])
        {
            AddExact(usages, command.WriteTagId, PlaceholderUsageSite.CommandWriteTag, id, command.Id);
            AddExact(usages, command.ReadTagId, PlaceholderUsageSite.CommandReadTag, id, command.Id);
        }

        foreach (var cell in element.Table?.EffectiveCells ?? [])
        {
            AddExact(usages, cell.ValueBindings?.ReadTagId, PlaceholderUsageSite.ReadTag, id, null);
            AddExact(usages, cell.ValueBindings?.WriteTagId, PlaceholderUsageSite.WriteTag, id, null);
        }

        foreach (var child in element.ChildElements)
        {
            Visit(child, usages, invalid);
        }
    }

    private static void AddExact(List<PlaceholderUsage> usages, string? value, PlaceholderUsageSite site, string elementId, string? commandId)
    {
        if (QuickWindowPlaceholder.TryParseName(value?.Trim(), out var number))
        {
            usages.Add(new PlaceholderUsage(number, site, elementId, commandId));
        }
    }

    private static void AddText(List<PlaceholderUsage> usages, List<InvalidPlaceholderToken> invalid, string? text, string elementId)
    {
        foreach (var token in QuickWindowPlaceholder.FindInText(text))
        {
            if (token.Number is { } number)
            {
                usages.Add(new PlaceholderUsage(number, PlaceholderUsageSite.Text, elementId, null));
            }
            else
            {
                invalid.Add(new InvalidPlaceholderToken(token.Raw, elementId));
            }
        }
    }

    private static void AddExpression(List<PlaceholderUsage> usages, ScadaExprNode node, string elementId)
    {
        switch (node)
        {
            case ScadaExprTagRef tag:
                AddExact(usages, tag.TagName, PlaceholderUsageSite.StateExpression, elementId, null);
                break;
            case ScadaExprUnary unary:
                AddExpression(usages, unary.Operand, elementId);
                break;
            case ScadaExprBinary binary:
                AddExpression(usages, binary.Left, elementId);
                AddExpression(usages, binary.Right, elementId);
                break;
            case ScadaExprFunc func:
                foreach (var argument in func.Args ?? [])
                {
                    AddExpression(usages, argument, elementId);
                }

                break;
        }
    }
}
