using System.Globalization;
using ScadaBuilderV2.Application.Tables;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App.TableEditor;

/// <summary>Represents one selectable tag in the table numeric-input authoring surfaces.</summary>
internal sealed record TableNumericTagOption(string Id, string Label, ScadaTagDefinition Tag);

/// <summary>Collects and validates numeric table-cell property and binding intentions.</summary>
/// <remarks>Decisions: DEC-0042, DEC-0043. Contracts: docs/superpowers/specs/2026-07-15-table-numeric-cell-authoring-correction-design.md. Tests: tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs.</remarks>
internal sealed class TableNumericInputPropertiesViewModel
{
    private readonly TableCellNumericInputInspection inspection;

    public TableNumericInputPropertiesViewModel(TableCellNumericInputInspection inspection)
    {
        this.inspection = inspection;
        var content = inspection.Content ?? new ScadaTableCellContent(ScadaTableCellContentKind.InputNumeric);
        InitialValue = Format(content.NumericValue);
        Placeholder = content.Placeholder;
        Minimum = Format(content.Minimum);
        Maximum = Format(content.Maximum);
        Step = Format(content.Step);
        DisplayFormat = content.DisplayFormat ?? string.Empty;
        IsReadOnly = content.IsReadOnly;
        var bindings = TableNumericBindingAuthoringPolicy.Normalize(
            inspection.ValueBindings?.ReadTagId,
            inspection.ValueBindings?.WriteTagId,
            content.IsReadOnly);
        SelectedReadTagId = bindings.ReadTagId;
        SelectedWriteTagId = bindings.WriteTagId;
        ReadDefaultedFromWrite = bindings.ReadDefaultedFromWrite;
        ReadTags = CreateOptions(inspection.ReadTags);
        WriteTags = CreateOptions(inspection.WriteTags);
    }

    public string InitialValue { get; private set; }
    public string Placeholder { get; private set; }
    public string Minimum { get; private set; }
    public string Maximum { get; private set; }
    public string Step { get; private set; }
    public string DisplayFormat { get; private set; }
    public bool IsReadOnly { get; private set; }
    public string TableElementId => inspection.TableElementId ?? "—";
    public string CellAddress => inspection.CellAddress ?? "—";
    public string TargetSummary => $"Tableau : {TableElementId}  |  Cellule : {CellAddress}";
    public string? SelectedReadTagId { get; private set; }
    public string? SelectedWriteTagId { get; private set; }
    public bool ReadDefaultedFromWrite { get; private set; }
    public string ReadDefaultNotice => ReadDefaultedFromWrite
        ? "Lecture automatiquement alignee sur Ecrire."
        : string.Empty;
    public string ReadBindingSummary => inspection.ReadBindingSummary;
    public string WriteBindingSummary => inspection.WriteBindingSummary;
    public IReadOnlyList<TableNumericTagOption> ReadTags { get; }
    public IReadOnlyList<TableNumericTagOption> WriteTags { get; }

    public void UpdateDraft(
        string initialValue,
        string placeholder,
        string minimum,
        string maximum,
        string step,
        string displayFormat,
        bool isReadOnly,
        string? readTagId,
        string? writeTagId)
    {
        InitialValue = initialValue;
        Placeholder = placeholder;
        Minimum = minimum;
        Maximum = maximum;
        Step = step;
        DisplayFormat = displayFormat;
        IsReadOnly = isReadOnly;
        UpdateBindingDraft(readTagId, writeTagId, isReadOnly);
    }

    public TableNumericBindingDraft UpdateBindingDraft(string? readTagId, string? writeTagId, bool isReadOnly)
    {
        var bindings = TableNumericBindingAuthoringPolicy.Normalize(readTagId, writeTagId, isReadOnly);
        SelectedReadTagId = bindings.ReadTagId;
        SelectedWriteTagId = bindings.WriteTagId;
        ReadDefaultedFromWrite = bindings.ReadDefaultedFromWrite;
        return bindings;
    }

    public bool TryBuildRequests(out IReadOnlyList<TableEditRequest> requests, out string? error)
    {
        requests = [];
        if (!inspection.HasSingleAnchor || !inspection.IsNumericInput ||
            inspection.AnchorRow is not { } row || inspection.AnchorColumn is not { } column)
        {
            error = inspection.Diagnostic ?? "Selectionnez une cellule InputNumeric unique.";
            return false;
        }

        if (!TryOptionalNumber(InitialValue, "valeur initiale", out var initial, out error) ||
            !TryOptionalNumber(Minimum, "minimum", out var minimum, out error) ||
            !TryOptionalNumber(Maximum, "maximum", out var maximum, out error) ||
            !TryOptionalNumber(Step, "pas", out var step, out error))
        {
            return false;
        }

        var content = new ScadaTableCellContent(
            ScadaTableCellContentKind.InputNumeric,
            Text: inspection.Content?.Text ?? string.Empty,
            Placeholder: Placeholder.Trim(),
            NumericValue: initial,
            Minimum: minimum,
            Maximum: maximum,
            Step: step,
            IsReadOnly: IsReadOnly,
            DisplayFormat: Normalize(DisplayFormat));
        error = TableCellNumericInputInspector.ValidateContent(content);
        if (error is not null)
        {
            return false;
        }

        var result = new List<TableEditRequest>
        {
            new(TableEditKind.SetNumericInputProperties, Row: row, Column: column, Content: content)
        };
        AddBindingRequest(result, row, column, TableCellBindingKind.Read, inspection.ValueBindings?.ReadTagId, SelectedReadTagId);
        AddBindingRequest(result, row, column, TableCellBindingKind.Write, inspection.ValueBindings?.WriteTagId, IsReadOnly ? null : SelectedWriteTagId);
        requests = result;
        return true;
    }

    private static void AddBindingRequest(
        ICollection<TableEditRequest> requests,
        int row,
        int column,
        TableCellBindingKind kind,
        string? current,
        string? requested)
    {
        if (string.Equals(Normalize(current), Normalize(requested), StringComparison.Ordinal))
        {
            return;
        }

        requests.Add(requested is null
            ? new TableEditRequest(TableEditKind.RemoveCellValueBinding, Row: row, Column: column, BindingKind: kind)
            : new TableEditRequest(TableEditKind.SetCellValueBinding, Row: row, Column: column, BindingKind: kind, TagId: requested));
    }

    private static IReadOnlyList<TableNumericTagOption> CreateOptions(IEnumerable<ScadaTagDefinition> tags) =>
        tags.Select(tag => new TableNumericTagOption(tag.Id, tag.AuthoringLabel, tag)).ToArray();

    private static bool TryOptionalNumber(string text, string label, out double? value, out string? error)
    {
        value = null;
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed) || !double.IsFinite(parsed))
        {
            error = $"La {label} doit etre un nombre valide.";
            return false;
        }

        value = parsed;
        return true;
    }

    private static string Format(double? value) => value?.ToString("0.################", CultureInfo.CurrentCulture) ?? string.Empty;

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
