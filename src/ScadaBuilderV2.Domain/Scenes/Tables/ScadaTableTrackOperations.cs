namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>Applies precise deterministic table track dimensions.</summary>
public static class ScadaTableTrackOperations
{
    public static ScadaTableDefinition SetRowHeight(ScadaTableDefinition table, IEnumerable<int> indexes, double height) => ScadaTableOperations.SetRowHeight(table, indexes, height);
    public static ScadaTableDefinition SetColumnWidth(ScadaTableDefinition table, IEnumerable<int> indexes, double width) => ScadaTableOperations.SetColumnWidth(table, indexes, width);
    public static ScadaTableDefinition ResizeProportionally(ScadaTableDefinition table, double width, double height) => ScadaTableOperations.ResizeProportionally(table, width, height);
    public static ScadaTableDefinition EqualizeColumns(ScadaTableDefinition table, IEnumerable<int> indexes) => SetColumns(table, indexes, null, false);
    public static ScadaTableDefinition EqualizeRows(ScadaTableDefinition table, IEnumerable<int> indexes) => SetRows(table, indexes, null, false);
    public static ScadaTableDefinition DistributeColumns(ScadaTableDefinition table, IEnumerable<int> indexes, double total) => SetColumns(table, indexes, total, true);
    public static ScadaTableDefinition DistributeRows(ScadaTableDefinition table, IEnumerable<int> indexes, double total) => SetRows(table, indexes, total, true);

    public static ScadaTableDefinition ApplySizes(ScadaTableDefinition table, IReadOnlyList<double> widths, IReadOnlyList<double> heights)
    {
        if (widths.Count != table.EffectiveColumns.Count || heights.Count != table.EffectiveRows.Count || widths.Any(x => !double.IsFinite(x) || x < ScadaTableDefinition.MinimumColumnWidth) || heights.Any(x => !double.IsFinite(x) || x < ScadaTableDefinition.MinimumRowHeight))
            throw new ArgumentException("Auto-fit track measurements are invalid.");
        return table with { Columns = table.EffectiveColumns.Select((x, i) => x with { Width = widths[i] }).ToArray(), Rows = table.EffectiveRows.Select((x, i) => x with { Height = heights[i] }).ToArray() };
    }

    private static ScadaTableDefinition SetColumns(ScadaTableDefinition table, IEnumerable<int> raw, double? total, bool proportional)
    {
        var indexes = raw.Distinct().Order().ToArray(); Validate(indexes, table.EffectiveColumns.Count);
        var current = indexes.Sum(i => table.EffectiveColumns[i].Width); var target = total ?? current;
        if (!double.IsFinite(target) || target < indexes.Length * ScadaTableDefinition.MinimumColumnWidth) throw new ArgumentOutOfRangeException(nameof(total));
        var columns = table.EffectiveColumns.ToArray();
        foreach (var i in indexes) columns[i] = columns[i] with { Width = proportional ? target * columns[i].Width / current : target / indexes.Length };
        return table with { Columns = columns };
    }
    private static ScadaTableDefinition SetRows(ScadaTableDefinition table, IEnumerable<int> raw, double? total, bool proportional)
    {
        var indexes = raw.Distinct().Order().ToArray(); Validate(indexes, table.EffectiveRows.Count);
        var current = indexes.Sum(i => table.EffectiveRows[i].Height); var target = total ?? current;
        if (!double.IsFinite(target) || target < indexes.Length * ScadaTableDefinition.MinimumRowHeight) throw new ArgumentOutOfRangeException(nameof(total));
        var rows = table.EffectiveRows.ToArray();
        foreach (var i in indexes) rows[i] = rows[i] with { Height = proportional ? target * rows[i].Height / current : target / indexes.Length };
        return table with { Rows = rows };
    }
    private static void Validate(int[] indexes, int count) { if (indexes.Length == 0 || indexes.Any(i => i < 0 || i >= count)) throw new ArgumentOutOfRangeException(nameof(indexes)); }
}
