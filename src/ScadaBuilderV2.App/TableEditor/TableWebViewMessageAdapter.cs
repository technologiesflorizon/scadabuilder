using System.Text.Json;

namespace ScadaBuilderV2.App.TableEditor;

/// <summary>Validates the table-specific JSON bridge before it reaches editor coordination.</summary>
internal static class TableWebViewMessageAdapter
{
    public static bool TryParse(string json, out ITableWebViewRequest? request)
    {
        request = null;
        try
        {
            using var document = JsonDocument.Parse(json); var root = document.RootElement;
            var type = Text(root, "type"); var id = Text(root, "id"); if (string.IsNullOrWhiteSpace(id)) return false;
            request = type switch
            {
                "tableSelection" => new TableSelectionRequest(id, Int(root,"row"), Int(root,"column"), Int(root,"endRow"), Int(root,"endColumn"), OptionalText(root,"scope")),
                "tableCellEdit" => new TableCellEditRequest(id, Int(root,"row"), Int(root,"column"), Text(root,"contentKind"), OptionalText(root,"text") ?? ""),
                "tableTrackResize" => ParseTrack(root,id),
                "tableAutoFitMeasured" => new TableAutoFitRequest(id, Numbers(root,"columnSizes"), Numbers(root,"rowSizes")),
                _ => null
            };
            return request is not null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentOutOfRangeException) { return false; }
    }
    private static TableTrackResizeRequest ParseTrack(JsonElement root,string id)
    {
        var orientation=Text(root,"orientation"); var size=Number(root,"trackSize"); var index=Int(root,"trackIndex");
        if (orientation is not ("row" or "column") || index<0 || !double.IsFinite(size) || size<=0) throw new InvalidOperationException();
        return new(id,orientation,index,size);
    }
    private static string Text(JsonElement root,string name) => root.GetProperty(name).GetString() ?? throw new InvalidOperationException();
    private static string? OptionalText(JsonElement root,string name) => root.TryGetProperty(name,out var x) ? x.GetString() : null;
    private static int Int(JsonElement root,string name) { var value=root.GetProperty(name).GetInt32(); if(value<0) throw new ArgumentOutOfRangeException(name); return value; }
    private static double Number(JsonElement root,string name) => root.GetProperty(name).GetDouble();
    private static double[] Numbers(JsonElement root,string name) { var values=root.GetProperty(name).EnumerateArray().Select(x=>x.GetDouble()).ToArray(); if(values.Any(x=>!double.IsFinite(x))) throw new InvalidOperationException(); return values; }
}
