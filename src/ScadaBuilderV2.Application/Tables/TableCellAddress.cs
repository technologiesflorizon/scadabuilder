namespace ScadaBuilderV2.Application.Tables;

/// <summary>Formats zero-based table coordinates as spreadsheet-style A1 addresses.</summary>
/// <remarks>
/// Decisions: DEC-0043.
/// Contracts: docs/superpowers/specs/2026-07-15-table-numeric-cell-authoring-correction-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/TableCellAddressTests.cs.
/// </remarks>
public static class TableCellAddress
{
    /// <summary>Returns the A1 address for a zero-based row and column.</summary>
    public static string FromZeroBased(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);

        var letters = new Stack<char>();
        var value = column + 1;
        while (value > 0)
        {
            value--;
            letters.Push((char)('A' + value % 26));
            value /= 26;
        }

        return $"{new string(letters.ToArray())}{row + 1}";
    }
}
