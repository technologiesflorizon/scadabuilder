namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>Resolves physical table-border segments and merge visibility.</summary>
public static class ScadaTableBorderResolver
{
    /// <summary>Resolves one override or its effective base-format fallback.</summary>
    public static ScadaTableBorder Resolve(ScadaTableDefinition table, ScadaTableBorderOrientation orientation, int gridLine, int segment)
    {
        var value = table.EffectiveBorderOverrides.LastOrDefault(x => x.Orientation == orientation && x.GridLine == gridLine && x.Segment == segment)?.Border;
        if (value is not null) return value;
        var format = table.EffectiveStyle.Base ?? ScadaTableStyle.Default.Base!;
        return new(format.GridStyle ?? ScadaTableGridStyle.Solid, format.GridColor ?? "#8AA0A6", format.GridWidth ?? 1);
    }

    /// <summary>Returns false for segments hidden inside a merged cell.</summary>
    public static bool IsVisible(ScadaTableDefinition table, ScadaTableBorderOrientation orientation, int gridLine, int segment)
    {
        foreach (var cell in table.EffectiveCells.Where(c => c.RowSpan > 1 || c.ColumnSpan > 1))
        {
            if (orientation == ScadaTableBorderOrientation.Horizontal && gridLine > cell.Row && gridLine < cell.Row + cell.RowSpan && segment >= cell.Column && segment < cell.Column + cell.ColumnSpan) return false;
            if (orientation == ScadaTableBorderOrientation.Vertical && gridLine > cell.Column && gridLine < cell.Column + cell.ColumnSpan && segment >= cell.Row && segment < cell.Row + cell.RowSpan) return false;
        }
        return true;
    }
}
