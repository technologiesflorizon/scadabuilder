using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Tables;

/// <summary>Identifies whether an inspected table-format value is inherited, locally overridden, or mixed.</summary>
public enum TablePropertyValueState
{
    Inherited,
    Custom,
    Mixed
}

/// <summary>Describes local and effective formatting for one table authoring scope.</summary>
public sealed record TableFormatInspection(
    ScadaTableFormat EffectiveFormat,
    ScadaTableFormat? LocalFormat,
    TablePropertyValueState State,
    IReadOnlyDictionary<string, TablePropertyValueState> PropertyStates);

/// <summary>Computes property-by-property inherited, custom, and mixed states for the table inspector.</summary>
/// <remarks>Decisions: DEC-0040. Contracts: docs/superpowers/specs/2026-07-15-table-ui-authoring-and-element-lock-design.md. Tests: tests/ScadaBuilderV2.Tests/TablePropertiesInspectorTests.cs.</remarks>
public static class TablePropertiesInspector
{
    private static readonly string[] PropertyNames =
    [
        nameof(ScadaTableFormat.Background),
        nameof(ScadaTableFormat.Foreground),
        nameof(ScadaTableFormat.GridColor),
        nameof(ScadaTableFormat.GridWidth),
        nameof(ScadaTableFormat.GridStyle),
        nameof(ScadaTableFormat.HorizontalAlignment),
        nameof(ScadaTableFormat.VerticalAlignment),
        nameof(ScadaTableFormat.Padding),
        nameof(ScadaTableFormat.FontFamily),
        nameof(ScadaTableFormat.FontSize),
        nameof(ScadaTableFormat.FontWeight),
        nameof(ScadaTableFormat.FontStyle),
        nameof(ScadaTableFormat.TextWrap),
        nameof(ScadaTableFormat.LineHeight)
    ];

    /// <summary>Inspects the selected scope without mutating the table.</summary>
    public static TableFormatInspection Inspect(ScadaTableDefinition table, ScadaTableFormatScope scope)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(scope);

        var coordinates = ResolveCoordinates(table, scope).ToArray();
        var localFormats = ResolveLocalFormats(table, scope).ToArray();
        var effectiveFormats = coordinates
            .Select(coordinate => ScadaTableStyleResolver.Resolve(table, coordinate.Row, coordinate.Column))
            .ToArray();

        if (effectiveFormats.Length == 0)
        {
            effectiveFormats = [ScadaTableStyleResolver.Resolve(table, 0, 0)];
        }

        if (localFormats.Length == 0)
        {
            localFormats = [null];
        }

        var states = PropertyNames.ToDictionary(
            property => property,
            property => ResolveState(localFormats.Select(format => Read(format, property)).ToArray()),
            StringComparer.Ordinal);
        var overall = states.Values.Any(state => state == TablePropertyValueState.Mixed)
            ? TablePropertyValueState.Mixed
            : states.Values.Any(state => state == TablePropertyValueState.Custom)
                ? TablePropertyValueState.Custom
                : TablePropertyValueState.Inherited;

        return new TableFormatInspection(
            Collapse(effectiveFormats),
            CollapseLocal(localFormats),
            overall,
            states);
    }

    private static IEnumerable<(int Row, int Column)> ResolveCoordinates(ScadaTableDefinition table, ScadaTableFormatScope scope)
    {
        var range = scope.Range ?? new ScadaTableRange(0, 0, table.EffectiveRows.Count - 1, table.EffectiveColumns.Count - 1);
        for (var row = 0; row < table.EffectiveRows.Count; row++)
        {
            for (var column = 0; column < table.EffectiveColumns.Count; column++)
            {
                var include = scope.Kind switch
                {
                    ScadaTableFormatScopeKind.Table => true,
                    ScadaTableFormatScopeKind.HeaderRows => table.EffectiveRows[row].IsHeader,
                    ScadaTableFormatScopeKind.AlternatingRows => !table.EffectiveRows[row].IsHeader && row % 2 == 1,
                    ScadaTableFormatScopeKind.Rows => row >= range.StartRow && row <= range.EndRow,
                    ScadaTableFormatScopeKind.Columns => column >= range.StartColumn && column <= range.EndColumn,
                    ScadaTableFormatScopeKind.Cells => range.Contains(row, column),
                    _ => false
                };
                if (include) yield return (row, column);
            }
        }
    }

    private static IEnumerable<ScadaTableFormat?> ResolveLocalFormats(ScadaTableDefinition table, ScadaTableFormatScope scope)
    {
        var range = scope.Range ?? new ScadaTableRange(0, 0, table.EffectiveRows.Count - 1, table.EffectiveColumns.Count - 1);
        return scope.Kind switch
        {
            ScadaTableFormatScopeKind.Table => [table.EffectiveStyle.Base],
            ScadaTableFormatScopeKind.HeaderRows => [table.EffectiveStyle.Header],
            ScadaTableFormatScopeKind.AlternatingRows => [table.EffectiveStyle.AlternatingRows],
            ScadaTableFormatScopeKind.Rows => table.EffectiveRows
                .Skip(range.StartRow).Take(range.RowCount).Select(row => row.Style),
            ScadaTableFormatScopeKind.Columns => table.EffectiveColumns
                .Skip(range.StartColumn).Take(range.ColumnCount).Select(column => column.Style),
            ScadaTableFormatScopeKind.Cells => table.EffectiveCells
                .Where(cell => range.Contains(cell.Row, cell.Column)).Select(cell => cell.Style),
            _ => []
        };
    }

    private static TablePropertyValueState ResolveState(IReadOnlyList<object?> values)
    {
        if (values.All(value => value is null)) return TablePropertyValueState.Inherited;
        var first = values[0];
        return first is not null && values.All(value => Equals(value, first))
            ? TablePropertyValueState.Custom
            : TablePropertyValueState.Mixed;
    }

    private static ScadaTableFormat Collapse(IReadOnlyList<ScadaTableFormat> formats) => new(
        Common(formats, format => format.Background),
        Common(formats, format => format.Foreground),
        Common(formats, format => format.GridColor),
        Common(formats, format => format.GridWidth),
        Common(formats, format => format.GridStyle),
        Common(formats, format => format.HorizontalAlignment),
        Common(formats, format => format.VerticalAlignment),
        Common(formats, format => format.Padding),
        Common(formats, format => format.FontFamily),
        Common(formats, format => format.FontSize),
        Common(formats, format => format.FontWeight),
        Common(formats, format => format.FontStyle),
        Common(formats, format => format.TextWrap),
        Common(formats, format => format.LineHeight));

    private static ScadaTableFormat? CollapseLocal(IReadOnlyList<ScadaTableFormat?> formats)
    {
        if (formats.All(format => format is null)) return null;
        return new ScadaTableFormat(
            Common(formats, format => format?.Background),
            Common(formats, format => format?.Foreground),
            Common(formats, format => format?.GridColor),
            Common(formats, format => format?.GridWidth),
            Common(formats, format => format?.GridStyle),
            Common(formats, format => format?.HorizontalAlignment),
            Common(formats, format => format?.VerticalAlignment),
            Common(formats, format => format?.Padding),
            Common(formats, format => format?.FontFamily),
            Common(formats, format => format?.FontSize),
            Common(formats, format => format?.FontWeight),
            Common(formats, format => format?.FontStyle),
            Common(formats, format => format?.TextWrap),
            Common(formats, format => format?.LineHeight));
    }

    private static T? Common<TSource, T>(IReadOnlyList<TSource> source, Func<TSource, T?> selector)
    {
        var first = selector(source[0]);
        return source.Skip(1).All(item => EqualityComparer<T?>.Default.Equals(selector(item), first)) ? first : default;
    }

    private static object? Read(ScadaTableFormat? format, string property) => property switch
    {
        nameof(ScadaTableFormat.Background) => format?.Background,
        nameof(ScadaTableFormat.Foreground) => format?.Foreground,
        nameof(ScadaTableFormat.GridColor) => format?.GridColor,
        nameof(ScadaTableFormat.GridWidth) => format?.GridWidth,
        nameof(ScadaTableFormat.GridStyle) => format?.GridStyle,
        nameof(ScadaTableFormat.HorizontalAlignment) => format?.HorizontalAlignment,
        nameof(ScadaTableFormat.VerticalAlignment) => format?.VerticalAlignment,
        nameof(ScadaTableFormat.Padding) => format?.Padding,
        nameof(ScadaTableFormat.FontFamily) => format?.FontFamily,
        nameof(ScadaTableFormat.FontSize) => format?.FontSize,
        nameof(ScadaTableFormat.FontWeight) => format?.FontWeight,
        nameof(ScadaTableFormat.FontStyle) => format?.FontStyle,
        nameof(ScadaTableFormat.TextWrap) => format?.TextWrap,
        nameof(ScadaTableFormat.LineHeight) => format?.LineHeight,
        _ => throw new ArgumentOutOfRangeException(nameof(property))
    };
}
