using System.Text.Json;

namespace ScadaBuilderV2.App.TableEditor;

/// <summary>Validates the table-specific JSON bridge before it reaches editor coordination.</summary>
internal static class TableWebViewMessageAdapter
{
    public static bool IsTableMessage(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("type", out var type) &&
                   type.GetString()?.StartsWith("table", StringComparison.Ordinal) == true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryParse(string json, out ITableWebViewRequest? request)
        => TryParse(json, out request, out _);

    public static bool TryParse(string json, out ITableWebViewRequest? request, out string? error)
    {
        request = null;
        error = null;
        try
        {
            using var document = JsonDocument.Parse(json); var root = document.RootElement;
            var type = Text(root, "type"); var id = Text(root, "id"); if (string.IsNullOrWhiteSpace(id)) return false;
            request = type switch
            {
                "tableSelection" => ParseSelection(root, id),
                "tableCellEdit" => ParseCellEdit(root, id),
                "tableTrackResize" => ParseTrack(root,id),
                "tableAutoFitMeasured" => new TableAutoFitRequest(id, Numbers(root,"columnSizes"), Numbers(root,"rowSizes")),
                "tableInteractionModeChanged" => ParseInteractionMode(root, id),
                _ => null
            };
            if (request is null) error = $"Unsupported table message type '{type}'.";
            return request is not null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentOutOfRangeException or KeyNotFoundException)
        {
            error = $"Invalid table bridge message: {ex.Message}";
            return false;
        }
    }

    private static TableSelectionRequest ParseSelection(JsonElement root, string id)
    {
        var row = Int(root, "row"); var column = Int(root, "column"); var endRow = Int(root, "endRow"); var endColumn = Int(root, "endColumn");
        if (endRow < row || endColumn < column) throw new InvalidOperationException("The table selection range is not normalized.");
        var scope = OptionalText(root, "scope");
        if (scope is not null && scope is not ("table" or "row" or "column" or "cells")) throw new InvalidOperationException("The table selection scope is invalid.");
        return new(id, row, column, endRow, endColumn, scope);
    }

    private static TableCellEditRequest ParseCellEdit(JsonElement root, string id)
    {
        var kind = Text(root, "contentKind");
        if (kind is not ("Text" or "InputText" or "InputNumeric")) throw new InvalidOperationException("The table content kind is invalid.");
        return new(id, Int(root, "row"), Int(root, "column"), kind, OptionalText(root, "text") ?? "");
    }
    private static TableTrackResizeRequest ParseTrack(JsonElement root,string id)
    {
        var orientation=Text(root,"orientation"); var size=Number(root,"trackSize"); var index=Int(root,"trackIndex");
        if (orientation is not ("row" or "column") || index<0 || !double.IsFinite(size) || size<=0) throw new InvalidOperationException();
        return new(id,orientation,index,size);
    }
    private static TableInteractionModeChangedRequest ParseInteractionMode(JsonElement root, string id)
    {
        var mode = Text(root, "mode");
        if (mode is not ("object" or "cells")) throw new InvalidOperationException("The table interaction mode is invalid.");
        return new(id, mode);
    }
    private static string Text(JsonElement root,string name) => root.GetProperty(name).GetString() ?? throw new InvalidOperationException();
    private static string? OptionalText(JsonElement root,string name) => root.TryGetProperty(name,out var x) ? x.GetString() : null;
    private static int Int(JsonElement root,string name) { var value=root.GetProperty(name).GetInt32(); if(value<0) throw new ArgumentOutOfRangeException(name); return value; }
    private static double Number(JsonElement root,string name) => root.GetProperty(name).GetDouble();
    private static double[] Numbers(JsonElement root,string name) { var values=root.GetProperty(name).EnumerateArray().Select(x=>x.GetDouble()).ToArray(); if(values.Any(x=>!double.IsFinite(x))) throw new InvalidOperationException(); return values; }
}
