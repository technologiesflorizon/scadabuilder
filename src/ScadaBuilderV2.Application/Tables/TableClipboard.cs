using ScadaBuilderV2.Domain.Scenes;
using System.Text;

namespace ScadaBuilderV2.Application.Tables;

/// <summary>Stores a rectangular table clipboard payload.</summary>
public sealed record TableClipboardPayload(
    int Rows,
    int Columns,
    IReadOnlyList<ScadaTableCell> Cells,
    string Tsv);

/// <summary>Copies and pastes table cells without depending on the platform clipboard.</summary>
/// <remarks>Decisions: DEC-0039. Tests: tests/ScadaBuilderV2.Tests/TableClipboardTests.cs.</remarks>
public static class TableClipboard
{
    public static TableClipboardPayload Copy(ScadaTableDefinition table, ScadaTableRange range)
    {
        EnsureRange(table, range);
        var cells = table.EffectiveCells
            .Where(cell => range.Contains(cell.Row, cell.Column))
            .Select(cell => cell with { Row = cell.Row - range.StartRow, Column = cell.Column - range.StartColumn })
            .ToArray();
        foreach (var cell in table.EffectiveCells.Where(cell => cell.RowSpan > 1 || cell.ColumnSpan > 1))
        {
            var selected = range.Contains(cell.Row, cell.Column);
            var fullySelected = selected && range.Contains(cell.Row + cell.RowSpan - 1, cell.Column + cell.ColumnSpan - 1);
            if (selected != fullySelected || (!selected && CoversAny(cell, range)))
            {
                throw new InvalidOperationException("La selection coupe une cellule fusionnee.");
            }
        }

        return new TableClipboardPayload(range.RowCount, range.ColumnCount, cells, ToTsv(table, range));
    }

    public static ScadaTableDefinition Paste(ScadaTableDefinition table, int startRow, int startColumn, TableClipboardPayload payload)
    {
        var target = new ScadaTableRange(startRow, startColumn, startRow + payload.Rows - 1, startColumn + payload.Columns - 1);
        EnsureRange(table, target);
        var cleared = ScadaTableOperations.ClearContent(table, target);
        var destination = cleared.EffectiveCells.Where(cell => !target.Contains(cell.Row, cell.Column)).ToList();
        destination.AddRange(payload.Cells.Select(cell => cell with { Row = cell.Row + startRow, Column = cell.Column + startColumn }));
        var result = cleared with { Cells = destination.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToArray() };
        ScadaTableOperations.ValidateDefinition(result);
        return result;
    }

    public static TableClipboardPayload ParseTsv(string text)
    {
        var lines = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length > 1 && lines[^1].Length == 0)
        {
            lines = lines[..^1];
        }

        var values = lines.Select(line => line.Split('\t')).ToArray();
        var columns = Math.Max(1, values.Max(row => row.Length));
        var cells = values.SelectMany((row, rowIndex) => Enumerable.Range(0, columns).Select(columnIndex =>
            new ScadaTableCell(
                rowIndex,
                columnIndex,
                Content: new ScadaTableCellContent(Text: columnIndex < row.Length ? row[columnIndex] : string.Empty))))
            .ToArray();
        return new TableClipboardPayload(Math.Max(1, values.Length), columns, cells, text ?? string.Empty);
    }

    private static string ToTsv(ScadaTableDefinition table, ScadaTableRange range)
    {
        var builder = new StringBuilder();
        for (var row = range.StartRow; row <= range.EndRow; row++)
        {
            if (row > range.StartRow)
            {
                builder.AppendLine();
            }
            for (var column = range.StartColumn; column <= range.EndColumn; column++)
            {
                if (column > range.StartColumn)
                {
                    builder.Append('\t');
                }
                var cell = table.EffectiveCells.First(candidate => candidate.Covers(row, column));
                if (cell.Row == row && cell.Column == column)
                {
                    builder.Append(cell.EffectiveContent.Kind == ScadaTableCellContentKind.InputNumeric
                        ? cell.EffectiveContent.NumericValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? cell.EffectiveContent.Text
                        : cell.EffectiveContent.Text);
                }
            }
        }
        return builder.ToString();
    }

    private static bool CoversAny(ScadaTableCell cell, ScadaTableRange range)
    {
        for (var row = range.StartRow; row <= range.EndRow; row++)
        {
            for (var column = range.StartColumn; column <= range.EndColumn; column++)
            {
                if (cell.Covers(row, column)) return true;
            }
        }
        return false;
    }

    private static void EnsureRange(ScadaTableDefinition table, ScadaTableRange range)
    {
        if (range.StartRow < 0 || range.StartColumn < 0 || range.EndRow >= table.EffectiveRows.Count || range.EndColumn >= table.EffectiveColumns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(range), "La plage depasse les limites du tableau.");
        }
    }
}
