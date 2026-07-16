using ScadaBuilderV2.Application.Tables;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App.TableEditor;

/// <summary>Supplies the modal and docked table inspectors with one contextual state and typed requests.</summary>
internal sealed class TablePropertiesViewModel
{
    public ScadaElement? Element { get; private set; }
    public ScadaTableRange Range { get; private set; } = new(0, 0, 0, 0);
    public ScadaTableFormatScopeKind ScopeKind { get; private set; } = ScadaTableFormatScopeKind.Cells;
    public TableFormatInspection? Format { get; private set; }
    public TableCellNumericInputInspection? NumericInput { get; private set; }

    public string StateLabel => Format?.State switch
    {
        TablePropertyValueState.Inherited => "Format de la portée : hérité (aucune surcharge locale)",
        TablePropertyValueState.Custom => "Format de la portée : personnalisé",
        TablePropertyValueState.Mixed => "Format de la portée : mixte",
        _ => "Format de la portée : —"
    };

    public void Load(ScadaElement element, ScadaTableRange range, ScadaTableFormatScopeKind scopeKind)
    {
        if (element.Table is null) throw new ArgumentException("A table Element+ is required.", nameof(element));
        Element = element;
        Range = range;
        ScopeKind = scopeKind;
        Format = TablePropertiesInspector.Inspect(element.Table, CreateScope());
    }

    public TableCellNumericInputInspection LoadNumericInput(
        ScadaElement element,
        string? selectionElementId,
        ScadaTableRange range,
        ScadaTagCatalog? tagCatalog)
    {
        NumericInput = TableCellNumericInputInspector.Inspect(element, selectionElementId, range, tagCatalog);
        return NumericInput;
    }

    public TableEditRequest ApplyFormat(ScadaTableFormat format) =>
        new(TableEditKind.ApplyFormatScope, Format: format, FormatScope: CreateScope());

    public TableEditRequest ResetProperty(string propertyName) =>
        new(TableEditKind.ResetFormatProperty, FormatScope: CreateScope(), PropertyName: propertyName);

    public TableEditRequest ResetScope() =>
        new(TableEditKind.ResetFormatScope, FormatScope: CreateScope());

    public TableEditRequest ApplyBorders(ScadaTableBorderPreset preset, ScadaTableBorder border) =>
        new(TableEditKind.ApplyBorderPreset, Range, BorderPreset: preset, Border: border);

    public TableEditRequest ApplyDimensions(double width, double height, ScadaTableStyle style) =>
        new(TableEditKind.SetTableProperties, Width: width, Height: height, TableStyle: style);

    private ScadaTableFormatScope CreateScope() => new(
        ScopeKind,
        ScopeKind is ScadaTableFormatScopeKind.Table or ScadaTableFormatScopeKind.HeaderRows or ScadaTableFormatScopeKind.AlternatingRows
            ? null
            : Range);
}
