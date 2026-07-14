namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>Resolves effective table formatting according to DEC-0039 precedence.</summary>
public static class ScadaTableStyleResolver
{
    /// <summary>Resolves one cell format property by property.</summary>
    public static ScadaTableFormat Resolve(ScadaTableDefinition table, int row, int column)
    {
        var cell = table.EffectiveCells.FirstOrDefault(candidate => candidate.Covers(row, column));
        var rowDefinition = table.EffectiveRows.ElementAtOrDefault(row);
        var columnDefinition = table.EffectiveColumns.ElementAtOrDefault(column);
        var style = table.EffectiveStyle;
        var band = rowDefinition?.IsHeader == true
            ? style.Header
            : row % 2 == 1 ? style.AlternatingRows : null;
        var fallback = ScadaTableStyle.Default.Base ?? new ScadaTableFormat();

        return Merge(fallback, style.Base, columnDefinition?.Style, band, rowDefinition?.Style, cell?.Style);
    }

    /// <summary>Merges formats from lowest to highest precedence.</summary>
    public static ScadaTableFormat Merge(params ScadaTableFormat?[] formats)
    {
        var result = new ScadaTableFormat();
        foreach (var format in formats.Where(format => format is not null).Cast<ScadaTableFormat>())
        {
            result = new ScadaTableFormat(
                format.Background ?? result.Background,
                format.Foreground ?? result.Foreground,
                format.GridColor ?? result.GridColor,
                format.GridWidth ?? result.GridWidth,
                format.GridStyle ?? result.GridStyle,
                format.HorizontalAlignment ?? result.HorizontalAlignment,
                format.VerticalAlignment ?? result.VerticalAlignment,
                format.Padding ?? result.Padding,
                format.FontFamily ?? result.FontFamily,
                format.FontSize ?? result.FontSize,
                format.FontWeight ?? result.FontWeight,
                format.FontStyle ?? result.FontStyle);
        }

        return result;
    }
}
