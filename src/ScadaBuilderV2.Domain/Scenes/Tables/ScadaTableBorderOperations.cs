namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>Identifies an editor border preset; preset names are never persisted.</summary>
public enum ScadaTableBorderPreset { None, All, Outline, Inside, Top, Right, Bottom, Left }

/// <summary>Expands border presets into canonical physical segment overrides.</summary>
public static class ScadaTableBorderOperations
{
    /// <summary>Applies a UI preset as physical segment overrides.</summary>
    public static ScadaTableDefinition ApplyPreset(ScadaTableDefinition table, ScadaTableRange range, ScadaTableBorderPreset preset, ScadaTableBorder? border)
    {
        if (preset == ScadaTableBorderPreset.None) border = new ScadaTableBorder(ScadaTableGridStyle.None, border?.Color ?? "#000000", 0);
        ValidateBorder(border);
        var keys = Segments(range, preset).ToHashSet();
        var retained = table.EffectiveBorderOverrides.Where(item => !keys.Contains((item.Orientation, item.GridLine, item.Segment))).ToList();
        retained.AddRange(keys.Select(key => new ScadaTableBorderOverride(key.Item1, key.Item2, key.Item3, border)));
        return table with { BorderOverrides = retained.OrderBy(x => x.Orientation).ThenBy(x => x.GridLine).ThenBy(x => x.Segment).ToArray() };
    }

    /// <summary>Validates all persisted physical segments.</summary>
    public static void Validate(ScadaTableDefinition table)
    {
        foreach (var item in table.EffectiveBorderOverrides)
        {
            var valid = item.Orientation == ScadaTableBorderOrientation.Horizontal
                ? item.GridLine >= 0 && item.GridLine <= table.EffectiveRows.Count && item.Segment >= 0 && item.Segment < table.EffectiveColumns.Count
                : item.GridLine >= 0 && item.GridLine <= table.EffectiveColumns.Count && item.Segment >= 0 && item.Segment < table.EffectiveRows.Count;
            if (!valid) throw new InvalidOperationException("Table border segment is outside the grid.");
            ValidateBorder(item.Border);
        }
    }

    private static IEnumerable<(ScadaTableBorderOrientation, int, int)> Segments(ScadaTableRange r, ScadaTableBorderPreset preset)
    {
        bool all = preset == ScadaTableBorderPreset.All;
        if (all || preset is ScadaTableBorderPreset.Outline or ScadaTableBorderPreset.Top)
            for (var c = r.StartColumn; c <= r.EndColumn; c++) yield return (ScadaTableBorderOrientation.Horizontal, r.StartRow, c);
        if (all || preset is ScadaTableBorderPreset.Outline or ScadaTableBorderPreset.Bottom)
            for (var c = r.StartColumn; c <= r.EndColumn; c++) yield return (ScadaTableBorderOrientation.Horizontal, r.EndRow + 1, c);
        if (all || preset is ScadaTableBorderPreset.Outline or ScadaTableBorderPreset.Left)
            for (var row = r.StartRow; row <= r.EndRow; row++) yield return (ScadaTableBorderOrientation.Vertical, r.StartColumn, row);
        if (all || preset is ScadaTableBorderPreset.Outline or ScadaTableBorderPreset.Right)
            for (var row = r.StartRow; row <= r.EndRow; row++) yield return (ScadaTableBorderOrientation.Vertical, r.EndColumn + 1, row);
        if (all || preset == ScadaTableBorderPreset.Inside)
        {
            for (var line = r.StartRow + 1; line <= r.EndRow; line++) for (var c = r.StartColumn; c <= r.EndColumn; c++) yield return (ScadaTableBorderOrientation.Horizontal, line, c);
            for (var line = r.StartColumn + 1; line <= r.EndColumn; line++) for (var row = r.StartRow; row <= r.EndRow; row++) yield return (ScadaTableBorderOrientation.Vertical, line, row);
        }
        if (preset == ScadaTableBorderPreset.None)
        {
            for (var line = r.StartRow; line <= r.EndRow + 1; line++) for (var c = r.StartColumn; c <= r.EndColumn; c++) yield return (ScadaTableBorderOrientation.Horizontal, line, c);
            for (var line = r.StartColumn; line <= r.EndColumn + 1; line++) for (var row = r.StartRow; row <= r.EndRow; row++) yield return (ScadaTableBorderOrientation.Vertical, line, row);
        }
    }

    private static void ValidateBorder(ScadaTableBorder? border)
    {
        if (border is not null && (!double.IsFinite(border.Width) || border.Width < 0 || string.IsNullOrWhiteSpace(border.Color)))
            throw new ArgumentException("A table border requires a finite non-negative width and a color.");
    }
}
