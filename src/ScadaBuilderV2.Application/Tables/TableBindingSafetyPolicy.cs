using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Tables;

/// <summary>Identifies whether a table edit is safe, requires confirmation, or is blocked.</summary>
public enum TableBindingSafetyDisposition
{
    Allowed,
    RequiresConfirmation,
    Blocked
}

/// <summary>Describes the binding impact of one table edit without invoking UI.</summary>
public sealed record TableBindingSafetyResult(
    TableBindingSafetyDisposition Disposition,
    int BindingCount = 0,
    string? Reason = null);

/// <summary>Evaluates binding-sensitive table mutations before they are committed.</summary>
/// <remarks>Decisions: DEC-0042. Contracts: docs/superpowers/specs/2026-07-15-table-cell-numeric-input-tf100web-design.md. Tests: tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs.</remarks>
public static class TableBindingSafetyPolicy
{
    /// <summary>Returns the impact and required authorization for an edit request.</summary>
    public static TableBindingSafetyResult Evaluate(TableEditRequest request, ScadaTableDefinition table)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(table);

        if (request.Kind == TableEditKind.DeleteRow && request.Row is { } row)
        {
            return ConfirmationFor(ScadaTableCellBindingOperations.CountBindings(
                table,
                new ScadaTableRange(row, 0, row, table.EffectiveColumns.Count - 1)),
                "La rangee contient {0} binding(s) cellule qui seront supprimes.");
        }

        if (request.Kind == TableEditKind.DeleteColumn && request.Column is { } column)
        {
            return ConfirmationFor(ScadaTableCellBindingOperations.CountBindings(
                table,
                new ScadaTableRange(0, column, table.EffectiveRows.Count - 1, column)),
                "La colonne contient {0} binding(s) cellule qui seront supprimes.");
        }

        if (request.Kind == TableEditKind.ConvertContentKind &&
            request.ContentKind is not null and not ScadaTableCellContentKind.InputNumeric &&
            request.Range is { } conversionRange)
        {
            return ConfirmationFor(
                ScadaTableCellBindingOperations.CountBindings(table, conversionRange),
                "La conversion supprimera {0} binding(s) cellule.");
        }

        if (request.Kind == TableEditKind.SetNumericInputProperties &&
            request.Content?.IsReadOnly == true &&
            TryResolveAnchor(table, request, out var anchor) &&
            !string.IsNullOrWhiteSpace(anchor.ValueBindings?.WriteTagId))
        {
            return new TableBindingSafetyResult(
                TableBindingSafetyDisposition.RequiresConfirmation,
                1,
                "Activer Lecture seule supprimera le binding d'ecriture de cette cellule.");
        }

        if (request.Kind is TableEditKind.Merge or TableEditKind.ToggleMerge && request.Range is { } mergeRange)
        {
            var mergeAnchor = table.EffectiveCells.FirstOrDefault(cell => cell.Covers(mergeRange.StartRow, mergeRange.StartColumn));
            var absorbed = table.EffectiveCells.Count(cell =>
                cell != mergeAnchor && mergeRange.Contains(cell.Row, cell.Column) && cell.ValueBindings is not null);
            if (absorbed > 0)
            {
                return new TableBindingSafetyResult(
                    TableBindingSafetyDisposition.Blocked,
                    absorbed,
                    "La fusion absorberait une cellule liee autre que l'ancre superieure gauche.");
            }
        }

        return new TableBindingSafetyResult(TableBindingSafetyDisposition.Allowed);
    }

    private static TableBindingSafetyResult ConfirmationFor(int count, string reason) => count == 0
        ? new TableBindingSafetyResult(TableBindingSafetyDisposition.Allowed)
        : new TableBindingSafetyResult(
            TableBindingSafetyDisposition.RequiresConfirmation,
            count,
            string.Format(System.Globalization.CultureInfo.InvariantCulture, reason, count));

    private static bool TryResolveAnchor(ScadaTableDefinition table, TableEditRequest request, out ScadaTableCell anchor)
    {
        var row = request.Row ?? request.Range?.StartRow;
        var column = request.Column ?? request.Range?.StartColumn;
        anchor = row.HasValue && column.HasValue
            ? table.EffectiveCells.FirstOrDefault(cell => cell.Covers(row.Value, column.Value))!
            : null!;
        return anchor is not null;
    }
}
