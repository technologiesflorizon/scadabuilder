using System.Text.Json.Serialization;

namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>Identifies the content rendered inside a modern table cell.</summary>
/// <remarks>Decisions: DEC-0039. Contracts: docs/superpowers/specs/2026-07-14-modern-table-and-insert-ribbon-design.md. Tests: tests/ScadaBuilderV2.Tests/ScadaTableModelTests.cs.</remarks>
public enum ScadaTableCellContentKind
{
    Text,
    InputText,
    InputNumeric
}

/// <summary>Defines horizontal alignment for one inheritable table format value.</summary>
public enum ScadaTableHorizontalAlignment
{
    Left,
    Center,
    Right,
    Justify
}

/// <summary>Defines vertical alignment for one inheritable table format value.</summary>
public enum ScadaTableVerticalAlignment
{
    Top,
    Middle,
    Bottom
}

/// <summary>Defines the CSS-compatible line style used by a table grid.</summary>
public enum ScadaTableGridStyle
{
    None,
    Solid,
    Dashed,
    Dotted,
    Double
}

/// <summary>Stores nullable table formatting overrides; null means inherit.</summary>
/// <remarks>Decisions: DEC-0039. Tests: tests/ScadaBuilderV2.Tests/ScadaTableOperationsTests.cs.</remarks>
public sealed record ScadaTableFormat(
    string? Background = null,
    string? Foreground = null,
    string? GridColor = null,
    double? GridWidth = null,
    ScadaTableGridStyle? GridStyle = null,
    ScadaTableHorizontalAlignment? HorizontalAlignment = null,
    ScadaTableVerticalAlignment? VerticalAlignment = null,
    double? Padding = null,
    string? FontFamily = null,
    double? FontSize = null,
    string? FontWeight = null,
    string? FontStyle = null,
    bool? TextWrap = null,
    double? LineHeight = null);

/// <summary>Defines one effective physical table-border segment.</summary>
public sealed record ScadaTableBorder(ScadaTableGridStyle Style, string Color, double Width);

/// <summary>Identifies the orientation of one physical table grid line.</summary>
public enum ScadaTableBorderOrientation { Horizontal, Vertical }

/// <summary>Overrides one unit segment of a physical table grid line.</summary>
public sealed record ScadaTableBorderOverride(
    ScadaTableBorderOrientation Orientation,
    int GridLine,
    int Segment,
    ScadaTableBorder? Border);

/// <summary>Defines base, header and alternating-row formats for a table.</summary>
public sealed record ScadaTableStyle(
    ScadaTableFormat? Base = null,
    ScadaTableFormat? Header = null,
    ScadaTableFormat? AlternatingRows = null)
{
    /// <summary>Gets the product default modern table style.</summary>
    public static ScadaTableStyle Default { get; } = new(
        new ScadaTableFormat(
            Background: "#FFFFFF",
            Foreground: "#0F2A30",
            GridColor: "#8AA0A6",
            GridWidth: 1,
            GridStyle: ScadaTableGridStyle.Solid,
            HorizontalAlignment: ScadaTableHorizontalAlignment.Left,
            VerticalAlignment: ScadaTableVerticalAlignment.Middle,
            Padding: 4,
            FontFamily: "Segoe UI",
            FontSize: 14,
            FontWeight: "Normal",
            FontStyle: "Normal"),
        new ScadaTableFormat(Background: "#EAF5F7", FontWeight: "Bold"),
        new ScadaTableFormat(Background: "#F6FAFB"));
}

/// <summary>Defines one modern table column.</summary>
public sealed record ScadaTableColumn(double Width, ScadaTableFormat? Style = null);

/// <summary>Defines one modern table row.</summary>
public sealed record ScadaTableRow(double Height, ScadaTableFormat? Style = null, bool IsHeader = false);

/// <summary>Defines the authored content of one table cell anchor.</summary>
public sealed record ScadaTableCellContent(
    ScadaTableCellContentKind Kind = ScadaTableCellContentKind.Text,
    string Text = "",
    string Placeholder = "",
    double? NumericValue = null,
    double? Minimum = null,
    double? Maximum = null,
    double? Step = null,
    bool IsReadOnly = false)
{
    /// <summary>Gets an empty text cell content.</summary>
    public static ScadaTableCellContent EmptyText { get; } = new();
}

/// <summary>Defines one table cell anchor and its optional row/column span.</summary>
public sealed record ScadaTableCell(
    int Row,
    int Column,
    int RowSpan = 1,
    int ColumnSpan = 1,
    ScadaTableCellContent? Content = null,
    ScadaTableFormat? Style = null)
{
    /// <summary>Gets the effective content including the empty-text compatibility fallback.</summary>
    [JsonIgnore]
    public ScadaTableCellContent EffectiveContent => Content ?? ScadaTableCellContent.EmptyText;

    /// <summary>Returns true when this anchor covers the supplied coordinate.</summary>
    public bool Covers(int row, int column) =>
        row >= Row && row < Row + Math.Max(1, RowSpan) &&
        column >= Column && column < Column + Math.Max(1, ColumnSpan);
}

/// <summary>Defines an inclusive rectangular cell range.</summary>
public sealed record ScadaTableRange(int StartRow, int StartColumn, int EndRow, int EndColumn)
{
    [JsonIgnore]
    public int RowCount => EndRow - StartRow + 1;

    [JsonIgnore]
    public int ColumnCount => EndColumn - StartColumn + 1;

    /// <summary>Creates a normalized range whose start is above and left of its end.</summary>
    public static ScadaTableRange Normalize(int row1, int column1, int row2, int column2) =>
        new(Math.Min(row1, row2), Math.Min(column1, column2), Math.Max(row1, row2), Math.Max(column1, column2));

    /// <summary>Returns true when the range contains the supplied coordinate.</summary>
    public bool Contains(int row, int column) =>
        row >= StartRow && row <= EndRow && column >= StartColumn && column <= EndColumn;
}

/// <summary>Defines the complete persistent model for one Element+ table.</summary>
/// <remarks>Decisions: DEC-0039. Contracts: docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md. Tests: tests/ScadaBuilderV2.Tests/ScadaTableModelTests.cs.</remarks>
public sealed record ScadaTableDefinition(
    IReadOnlyList<ScadaTableColumn>? Columns,
    IReadOnlyList<ScadaTableRow>? Rows,
    IReadOnlyList<ScadaTableCell>? Cells,
    ScadaTableStyle? Style = null,
    IReadOnlyList<ScadaTableBorderOverride>? BorderOverrides = null)
{
    public const int MinimumTrackCount = 1;
    public const int MaximumTrackCount = 64;
    public const double MinimumColumnWidth = 24;
    public const double MinimumRowHeight = 20;
    public const double DefaultColumnWidth = 96;
    public const double DefaultRowHeight = 32;

    [JsonIgnore]
    public IReadOnlyList<ScadaTableColumn> EffectiveColumns => Columns ?? Array.Empty<ScadaTableColumn>();

    [JsonIgnore]
    public IReadOnlyList<ScadaTableRow> EffectiveRows => Rows ?? Array.Empty<ScadaTableRow>();

    [JsonIgnore]
    public IReadOnlyList<ScadaTableCell> EffectiveCells => Cells ?? Array.Empty<ScadaTableCell>();

    [JsonIgnore]
    public IReadOnlyList<ScadaTableBorderOverride> EffectiveBorderOverrides => BorderOverrides ?? Array.Empty<ScadaTableBorderOverride>();

    [JsonIgnore]
    public ScadaTableStyle EffectiveStyle => Style ?? ScadaTableStyle.Default;

    [JsonIgnore]
    public double Width => EffectiveColumns.Sum(column => column.Width);

    [JsonIgnore]
    public double Height => EffectiveRows.Sum(row => row.Height);

    /// <summary>Creates a fully materialized table using the approved 6x8 preset defaults.</summary>
    public static ScadaTableDefinition CreateDefault(int rows = 6, int columns = 8, bool firstRowIsHeader = true)
    {
        ValidateTrackCount(rows, nameof(rows));
        ValidateTrackCount(columns, nameof(columns));

        var columnDefinitions = Enumerable.Range(0, columns)
            .Select(_ => new ScadaTableColumn(DefaultColumnWidth))
            .ToArray();
        var rowDefinitions = Enumerable.Range(0, rows)
            .Select(index => new ScadaTableRow(DefaultRowHeight, IsHeader: firstRowIsHeader && index == 0))
            .ToArray();
        var cellDefinitions = Enumerable.Range(0, rows)
            .SelectMany(row => Enumerable.Range(0, columns)
                .Select(column => new ScadaTableCell(row, column, Content: ScadaTableCellContent.EmptyText)))
            .ToArray();

        return new ScadaTableDefinition(columnDefinitions, rowDefinitions, cellDefinitions, ScadaTableStyle.Default);
    }

    private static void ValidateTrackCount(int value, string parameterName)
    {
        if (value is < MinimumTrackCount or > MaximumTrackCount)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Table track count must be between {MinimumTrackCount} and {MaximumTrackCount}.");
        }
    }
}
