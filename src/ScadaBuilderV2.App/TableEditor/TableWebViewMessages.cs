namespace ScadaBuilderV2.App.TableEditor;

internal interface ITableWebViewRequest { string ElementId { get; } }
internal sealed record TableSelectionRequest(string ElementId, int Row, int Column, int EndRow, int EndColumn, string? Scope) : ITableWebViewRequest;
internal sealed record TableCellEditRequest(string ElementId, int Row, int Column, string ContentKind, string Text) : ITableWebViewRequest;
internal sealed record TableTrackResizeRequest(string ElementId, string Orientation, int TrackIndex, double TrackSize) : ITableWebViewRequest;
internal sealed record TableAutoFitRequest(string ElementId, IReadOnlyList<double> ColumnSizes, IReadOnlyList<double> RowSizes) : ITableWebViewRequest;
internal sealed record TableInteractionModeChangedRequest(string ElementId, string Mode) : ITableWebViewRequest;
