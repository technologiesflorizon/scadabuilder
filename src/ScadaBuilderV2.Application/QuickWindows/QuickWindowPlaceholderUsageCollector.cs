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

/// <summary>Collects every `#N` placeholder used by a quick-window page.</summary>
/// <remarks>
/// Only binding fields and displayed texts are read; styles and colours never are, so <c>#0F2A30</c> can never
/// be taken for a placeholder. Elements are walked depth-first in document order, children included.
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

    private static void Visit(ScadaElement element, List<PlaceholderUsage> usages, List<InvalidPlaceholderToken> invalid)
    {
        var id = element.Id;

        if (element.Data is { } data)
        {
            AddExact(usages, data.ReadTagId, PlaceholderUsageSite.ReadTag, id, null);
            AddExact(usages, data.TagBinding, PlaceholderUsageSite.ReadTag, id, null);
            AddExact(usages, data.WriteTagId, PlaceholderUsageSite.WriteTag, id, null);
            AddText(usages, invalid, data.Text, id);
        }

        if (element.LegacyPayload is { } legacy)
        {
            AddText(usages, invalid, legacy.Text, id);
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
