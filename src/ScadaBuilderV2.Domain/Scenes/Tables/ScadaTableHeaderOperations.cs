namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>Maintains the consecutive leading table-header row contract.</summary>
public static class ScadaTableHeaderOperations
{
    /// <summary>Marks exactly the leading row prefix as headers.</summary>
    public static ScadaTableDefinition SetHeaderRowCount(ScadaTableDefinition table, int count)
    {
        if (count < 0 || count > table.EffectiveRows.Count) throw new ArgumentOutOfRangeException(nameof(count));
        return table with { Rows = table.EffectiveRows.Select((row, index) => row with { IsHeader = index < count }).ToArray() };
    }
}
