namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>Provides immutable modern-table grid operations.</summary>
/// <remarks>Decisions: DEC-0039. Contracts: docs/superpowers/specs/2026-07-14-modern-table-and-insert-ribbon-design.md. Tests: tests/ScadaBuilderV2.Tests/ScadaTableOperationsTests.cs.</remarks>
public static class ScadaTableOperations
{
    public static ScadaTableDefinition Merge(ScadaTableDefinition table, ScadaTableRange range)
    {
        Validate(table, range);
        EnsureNoPartialMerge(table, range);

        var anchor = FindCell(table, range.StartRow, range.StartColumn)
            ?? throw new InvalidOperationException("The merge anchor cell does not exist.");
        var cells = table.EffectiveCells
            .Where(cell => !range.Contains(cell.Row, cell.Column))
            .Append(anchor with
            {
                Row = range.StartRow,
                Column = range.StartColumn,
                RowSpan = range.RowCount,
                ColumnSpan = range.ColumnCount
            })
            .OrderBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)
            .ToArray();
        return table with { Cells = cells };
    }

    public static ScadaTableDefinition Unmerge(ScadaTableDefinition table, int row, int column)
    {
        ValidateCoordinate(table, row, column);
        var merged = FindCell(table, row, column)
            ?? throw new InvalidOperationException("The selected cell does not exist.");
        if (merged.RowSpan <= 1 && merged.ColumnSpan <= 1)
        {
            return table;
        }

        var replacements = Enumerable.Range(merged.Row, merged.RowSpan)
            .SelectMany(targetRow => Enumerable.Range(merged.Column, merged.ColumnSpan)
                .Select(targetColumn => targetRow == merged.Row && targetColumn == merged.Column
                    ? merged with { RowSpan = 1, ColumnSpan = 1 }
                    : new ScadaTableCell(targetRow, targetColumn, Content: ScadaTableCellContent.EmptyText)))
            .ToArray();
        return table with
        {
            Cells = table.EffectiveCells
                .Where(cell => !ReferenceEquals(cell, merged) && cell != merged)
                .Concat(replacements)
                .OrderBy(cell => cell.Row)
                .ThenBy(cell => cell.Column)
                .ToArray()
        };
    }

    public static ScadaTableDefinition SetContent(
        ScadaTableDefinition table,
        int row,
        int column,
        ScadaTableCellContent content)
    {
        ValidateCoordinate(table, row, column);
        var target = FindCell(table, row, column)
            ?? throw new InvalidOperationException("The selected cell does not exist.");
        return ReplaceCell(table, target, target with { Content = content });
    }

    public static ScadaTableDefinition ClearContent(ScadaTableDefinition table, ScadaTableRange range)
    {
        Validate(table, range);
        EnsureNoPartialMerge(table, range);
        return table with
        {
            Cells = table.EffectiveCells
                .Select(cell => range.Contains(cell.Row, cell.Column)
                    ? cell with { Content = ScadaTableCellContent.EmptyText }
                    : cell)
                .ToArray()
        };
    }

    public static ScadaTableDefinition SetCellFormat(
        ScadaTableDefinition table,
        ScadaTableRange range,
        ScadaTableFormat? format)
    {
        Validate(table, range);
        EnsureNoPartialMerge(table, range);
        return table with
        {
            Cells = table.EffectiveCells
                .Select(cell => range.Contains(cell.Row, cell.Column) ? cell with { Style = format } : cell)
                .ToArray()
        };
    }

    public static ScadaTableDefinition SetRowFormat(ScadaTableDefinition table, IEnumerable<int> rows, ScadaTableFormat? format)
    {
        var indices = rows.Distinct().ToHashSet();
        if (indices.Any(index => index < 0 || index >= table.EffectiveRows.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(rows));
        }

        return table with
        {
            Rows = table.EffectiveRows.Select((row, index) => indices.Contains(index) ? row with { Style = format } : row).ToArray()
        };
    }

    public static ScadaTableDefinition SetColumnFormat(ScadaTableDefinition table, IEnumerable<int> columns, ScadaTableFormat? format)
    {
        var indices = columns.Distinct().ToHashSet();
        if (indices.Any(index => index < 0 || index >= table.EffectiveColumns.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        return table with
        {
            Columns = table.EffectiveColumns.Select((column, index) => indices.Contains(index) ? column with { Style = format } : column).ToArray()
        };
    }

    public static ScadaTableDefinition SetRowHeight(ScadaTableDefinition table, IEnumerable<int> rows, double height)
    {
        if (height < ScadaTableDefinition.MinimumRowHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        var indices = rows.Distinct().ToHashSet();
        if (indices.Any(index => index < 0 || index >= table.EffectiveRows.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(rows));
        }

        return table with { Rows = table.EffectiveRows.Select((row, index) => indices.Contains(index) ? row with { Height = height } : row).ToArray() };
    }

    public static ScadaTableDefinition SetColumnWidth(ScadaTableDefinition table, IEnumerable<int> columns, double width)
    {
        if (width < ScadaTableDefinition.MinimumColumnWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var indices = columns.Distinct().ToHashSet();
        if (indices.Any(index => index < 0 || index >= table.EffectiveColumns.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        return table with { Columns = table.EffectiveColumns.Select((column, index) => indices.Contains(index) ? column with { Width = width } : column).ToArray() };
    }

    public static ScadaTableDefinition ResizeProportionally(ScadaTableDefinition table, double width, double height)
    {
        var currentWidth = table.Width;
        var currentHeight = table.Height;
        if (currentWidth <= 0 || currentHeight <= 0 ||
            width < table.EffectiveColumns.Count * ScadaTableDefinition.MinimumColumnWidth ||
            height < table.EffectiveRows.Count * ScadaTableDefinition.MinimumRowHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "The requested table size violates track minimums.");
        }

        return table with
        {
            Columns = ScaleColumns(table.EffectiveColumns, width / currentWidth, width),
            Rows = ScaleRows(table.EffectiveRows, height / currentHeight, height)
        };
    }

    public static ScadaTableDefinition InsertRow(ScadaTableDefinition table, int index)
    {
        if (table.EffectiveRows.Count >= ScadaTableDefinition.MaximumTrackCount || index < 0 || index > table.EffectiveRows.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var template = table.EffectiveRows.Count == 0
            ? new ScadaTableRow(ScadaTableDefinition.DefaultRowHeight)
            : table.EffectiveRows[Math.Clamp(index == table.EffectiveRows.Count ? index - 1 : index, 0, table.EffectiveRows.Count - 1)];
        var rows = table.EffectiveRows.ToList();
        rows.Insert(index, template with { IsHeader = false });

        var cells = table.EffectiveCells.Select(cell =>
        {
            if (cell.Row < index)
            {
                return cell.Row + cell.RowSpan > index ? cell with { RowSpan = cell.RowSpan + 1 } : cell;
            }

            return cell with { Row = cell.Row + 1 };
        }).ToList();
        for (var column = 0; column < table.EffectiveColumns.Count; column++)
        {
            if (!cells.Any(cell => cell.Covers(index, column)))
            {
                cells.Add(new ScadaTableCell(index, column, Content: ScadaTableCellContent.EmptyText));
            }
        }

        return table with { Rows = rows, Cells = cells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToArray() };
    }

    public static ScadaTableDefinition InsertColumn(ScadaTableDefinition table, int index)
    {
        if (table.EffectiveColumns.Count >= ScadaTableDefinition.MaximumTrackCount || index < 0 || index > table.EffectiveColumns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var template = table.EffectiveColumns.Count == 0
            ? new ScadaTableColumn(ScadaTableDefinition.DefaultColumnWidth)
            : table.EffectiveColumns[Math.Clamp(index == table.EffectiveColumns.Count ? index - 1 : index, 0, table.EffectiveColumns.Count - 1)];
        var columns = table.EffectiveColumns.ToList();
        columns.Insert(index, template);
        var cells = table.EffectiveCells.Select(cell =>
        {
            if (cell.Column < index)
            {
                return cell.Column + cell.ColumnSpan > index ? cell with { ColumnSpan = cell.ColumnSpan + 1 } : cell;
            }

            return cell with { Column = cell.Column + 1 };
        }).ToList();
        for (var row = 0; row < table.EffectiveRows.Count; row++)
        {
            if (!cells.Any(cell => cell.Covers(row, index)))
            {
                cells.Add(new ScadaTableCell(row, index, Content: ScadaTableCellContent.EmptyText));
            }
        }

        return table with { Columns = columns, Cells = cells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToArray() };
    }

    public static ScadaTableDefinition DeleteRow(ScadaTableDefinition table, int index)
    {
        if (table.EffectiveRows.Count <= 1 || index < 0 || index >= table.EffectiveRows.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var rows = table.EffectiveRows.Where((_, rowIndex) => rowIndex != index).ToArray();
        var cells = new List<ScadaTableCell>();
        foreach (var cell in table.EffectiveCells)
        {
            if (!cell.Covers(index, cell.Column))
            {
                cells.Add(cell.Row > index ? cell with { Row = cell.Row - 1 } : cell);
            }
            else if (cell.RowSpan > 1)
            {
                var newRow = cell.Row == index ? index : cell.Row;
                if (newRow > index)
                {
                    newRow--;
                }
                cells.Add(cell with { Row = Math.Min(newRow, rows.Length - 1), RowSpan = cell.RowSpan - 1 });
            }
        }

        MaterializeMissingCells(rows.Length, table.EffectiveColumns.Count, cells);
        return table with { Rows = rows, Cells = cells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToArray() };
    }

    public static ScadaTableDefinition DeleteColumn(ScadaTableDefinition table, int index)
    {
        if (table.EffectiveColumns.Count <= 1 || index < 0 || index >= table.EffectiveColumns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var columns = table.EffectiveColumns.Where((_, columnIndex) => columnIndex != index).ToArray();
        var cells = new List<ScadaTableCell>();
        foreach (var cell in table.EffectiveCells)
        {
            if (!cell.Covers(cell.Row, index))
            {
                cells.Add(cell.Column > index ? cell with { Column = cell.Column - 1 } : cell);
            }
            else if (cell.ColumnSpan > 1)
            {
                var newColumn = cell.Column == index ? index : cell.Column;
                if (newColumn > index)
                {
                    newColumn--;
                }
                cells.Add(cell with { Column = Math.Min(newColumn, columns.Length - 1), ColumnSpan = cell.ColumnSpan - 1 });
            }
        }

        MaterializeMissingCells(table.EffectiveRows.Count, columns.Length, cells);
        return table with { Columns = columns, Cells = cells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToArray() };
    }

    public static void ValidateDefinition(ScadaTableDefinition table)
    {
        if (table.EffectiveRows.Count is < 1 or > ScadaTableDefinition.MaximumTrackCount ||
            table.EffectiveColumns.Count is < 1 or > ScadaTableDefinition.MaximumTrackCount)
        {
            throw new InvalidOperationException("Table track count is invalid.");
        }

        foreach (var cell in table.EffectiveCells)
        {
            if (cell.Row < 0 || cell.Column < 0 || cell.RowSpan < 1 || cell.ColumnSpan < 1 ||
                cell.Row + cell.RowSpan > table.EffectiveRows.Count ||
                cell.Column + cell.ColumnSpan > table.EffectiveColumns.Count)
            {
                throw new InvalidOperationException("Table cell bounds are invalid.");
            }
        }

        for (var row = 0; row < table.EffectiveRows.Count; row++)
        {
            for (var column = 0; column < table.EffectiveColumns.Count; column++)
            {
                if (table.EffectiveCells.Count(cell => cell.Covers(row, column)) != 1)
                {
                    throw new InvalidOperationException($"Table coordinate {row},{column} must be covered exactly once.");
                }
            }
        }

        ScadaTableBorderOperations.Validate(table);
    }

    private static void Validate(ScadaTableDefinition table, ScadaTableRange range)
    {
        if (range.StartRow < 0 || range.StartColumn < 0 || range.EndRow >= table.EffectiveRows.Count ||
            range.EndColumn >= table.EffectiveColumns.Count || range.StartRow > range.EndRow || range.StartColumn > range.EndColumn)
        {
            throw new ArgumentOutOfRangeException(nameof(range));
        }
    }

    private static void ValidateCoordinate(ScadaTableDefinition table, int row, int column) =>
        Validate(table, new ScadaTableRange(row, column, row, column));

    private static void EnsureNoPartialMerge(ScadaTableDefinition table, ScadaTableRange range)
    {
        foreach (var cell in table.EffectiveCells.Where(cell => cell.RowSpan > 1 || cell.ColumnSpan > 1))
        {
            var coordinates = Enumerable.Range(cell.Row, cell.RowSpan)
                .SelectMany(row => Enumerable.Range(cell.Column, cell.ColumnSpan).Select(column => (row, column)))
                .ToArray();
            var selectedCount = coordinates.Count(coordinate => range.Contains(coordinate.row, coordinate.column));
            if (selectedCount > 0 && selectedCount < coordinates.Length)
            {
                throw new InvalidOperationException("The selection intersects an existing merge only partially.");
            }
        }
    }

    private static ScadaTableCell? FindCell(ScadaTableDefinition table, int row, int column) =>
        table.EffectiveCells.FirstOrDefault(cell => cell.Covers(row, column));

    private static ScadaTableDefinition ReplaceCell(ScadaTableDefinition table, ScadaTableCell before, ScadaTableCell after) =>
        table with { Cells = table.EffectiveCells.Select(cell => cell == before ? after : cell).ToArray() };

    private static ScadaTableColumn[] ScaleColumns(IReadOnlyList<ScadaTableColumn> columns, double scale, double target)
    {
        var result = columns.Select(column => column with { Width = Math.Max(ScadaTableDefinition.MinimumColumnWidth, column.Width * scale) }).ToArray();
        result[^1] = result[^1] with { Width = result[^1].Width + target - result.Sum(column => column.Width) };
        return result;
    }

    private static ScadaTableRow[] ScaleRows(IReadOnlyList<ScadaTableRow> rows, double scale, double target)
    {
        var result = rows.Select(row => row with { Height = Math.Max(ScadaTableDefinition.MinimumRowHeight, row.Height * scale) }).ToArray();
        result[^1] = result[^1] with { Height = result[^1].Height + target - result.Sum(row => row.Height) };
        return result;
    }

    private static void MaterializeMissingCells(int rowCount, int columnCount, ICollection<ScadaTableCell> cells)
    {
        for (var row = 0; row < rowCount; row++)
        {
            for (var column = 0; column < columnCount; column++)
            {
                if (!cells.Any(cell => cell.Covers(row, column)))
                {
                    cells.Add(new ScadaTableCell(row, column, Content: ScadaTableCellContent.EmptyText));
                }
            }
        }
    }
}
