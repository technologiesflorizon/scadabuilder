namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>Identifies a persistent table-format scope.</summary>
public enum ScadaTableFormatScopeKind { Table, HeaderRows, AlternatingRows, Rows, Columns, Cells }

/// <summary>Describes a format scope while preserving the physical selection.</summary>
public sealed record ScadaTableFormatScope(ScadaTableFormatScopeKind Kind, ScadaTableRange? Range = null);

/// <summary>Applies and resets nullable table formatting overrides.</summary>
public static class ScadaTableFormatOperations
{
    /// <summary>Applies a nullable local override to an explicit scope.</summary>
    public static ScadaTableDefinition ApplyFormat(ScadaTableDefinition table, ScadaTableFormatScope scope, ScadaTableFormat? format) => scope.Kind switch
    {
        ScadaTableFormatScopeKind.Table => table with { Style = table.EffectiveStyle with { Base = format } },
        ScadaTableFormatScopeKind.HeaderRows => table with { Style = table.EffectiveStyle with { Header = format } },
        ScadaTableFormatScopeKind.AlternatingRows => table with { Style = table.EffectiveStyle with { AlternatingRows = format } },
        ScadaTableFormatScopeKind.Rows => ScadaTableOperations.SetRowFormat(table, Rows(Require(scope)), format),
        ScadaTableFormatScopeKind.Columns => ScadaTableOperations.SetColumnFormat(table, Columns(Require(scope)), format),
        ScadaTableFormatScopeKind.Cells => ScadaTableOperations.SetCellFormat(table, Require(scope), format),
        _ => table
    };

    /// <summary>Clears every local override on a scope.</summary>
    public static ScadaTableDefinition ResetScope(ScadaTableDefinition table, ScadaTableFormatScope scope) => ApplyFormat(table, scope, null);

    /// <summary>Clears one named local property while retaining other overrides.</summary>
    public static ScadaTableDefinition ResetProperty(ScadaTableDefinition table, ScadaTableFormatScope scope, string property)
    {
        var current = ResolveLocal(table, scope);
        if (current is null) return table;
        var reset = property switch
        {
            nameof(ScadaTableFormat.Background) => current with { Background = null },
            nameof(ScadaTableFormat.Foreground) => current with { Foreground = null },
            nameof(ScadaTableFormat.GridColor) => current with { GridColor = null },
            nameof(ScadaTableFormat.GridWidth) => current with { GridWidth = null },
            nameof(ScadaTableFormat.GridStyle) => current with { GridStyle = null },
            nameof(ScadaTableFormat.HorizontalAlignment) => current with { HorizontalAlignment = null },
            nameof(ScadaTableFormat.VerticalAlignment) => current with { VerticalAlignment = null },
            nameof(ScadaTableFormat.Padding) => current with { Padding = null },
            nameof(ScadaTableFormat.FontFamily) => current with { FontFamily = null },
            nameof(ScadaTableFormat.FontSize) => current with { FontSize = null },
            nameof(ScadaTableFormat.FontWeight) => current with { FontWeight = null },
            nameof(ScadaTableFormat.FontStyle) => current with { FontStyle = null },
            nameof(ScadaTableFormat.TextWrap) => current with { TextWrap = null },
            nameof(ScadaTableFormat.LineHeight) => current with { LineHeight = null },
            _ => throw new ArgumentException("Unknown table format property.", nameof(property))
        };
        return ApplyFormat(table, scope, reset);
    }

    private static ScadaTableFormat? ResolveLocal(ScadaTableDefinition table, ScadaTableFormatScope scope) => scope.Kind switch
    {
        ScadaTableFormatScopeKind.Table => table.EffectiveStyle.Base,
        ScadaTableFormatScopeKind.HeaderRows => table.EffectiveStyle.Header,
        ScadaTableFormatScopeKind.AlternatingRows => table.EffectiveStyle.AlternatingRows,
        ScadaTableFormatScopeKind.Rows => table.EffectiveRows[Require(scope).StartRow].Style,
        ScadaTableFormatScopeKind.Columns => table.EffectiveColumns[Require(scope).StartColumn].Style,
        ScadaTableFormatScopeKind.Cells => table.EffectiveCells.FirstOrDefault(c => c.Covers(Require(scope).StartRow, Require(scope).StartColumn))?.Style,
        _ => null
    };
    private static ScadaTableRange Require(ScadaTableFormatScope scope) => scope.Range ?? throw new ArgumentException("A range is required.");
    private static IEnumerable<int> Rows(ScadaTableRange range) => Enumerable.Range(range.StartRow, range.RowCount);
    private static IEnumerable<int> Columns(ScadaTableRange range) => Enumerable.Range(range.StartColumn, range.ColumnCount);
}
