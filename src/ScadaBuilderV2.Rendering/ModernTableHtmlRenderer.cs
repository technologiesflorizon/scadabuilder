using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Rendering;

/// <summary>Renders the persistent modern-table model for preview and FT100 export.</summary>
/// <remarks>Decisions: DEC-0039. Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md. Tests: tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.</remarks>
internal static class ModernTableHtmlRenderer
{
    public static string Render(ScadaElement element, string scopedElementId)
    {
        var table = element.Table;
        if (table is null || table.EffectiveRows.Count == 0 || table.EffectiveColumns.Count == 0)
        {
            return string.Empty;
        }

        var html = new StringBuilder();
        html.Append("<table class=\"scada-modern-table\" role=\"grid\" data-scada-table=\"true\" style=\"position:relative;border-collapse:collapse;");
        html.Append("grid-template-columns:");
        html.Append(string.Join(' ', table.EffectiveColumns.Select(column => $"{Format(column.Width)}px")));
        html.Append(";grid-template-rows:");
        html.Append(string.Join(' ', table.EffectiveRows.Select(row => $"{Format(row.Height)}px")));
        html.Append(";\"><tbody style=\"display:contents\">");

        foreach (var cell in table.EffectiveCells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column))
        {
            if (cell.Row < 0 || cell.Column < 0 || cell.Row >= table.EffectiveRows.Count || cell.Column >= table.EffectiveColumns.Count)
            {
                continue;
            }

            var format = ScadaTableStyleResolver.Resolve(table, cell.Row, cell.Column);
            var cellId = $"{scopedElementId}__cell-{cell.Row}-{cell.Column}";
            var isHeader = table.EffectiveRows[cell.Row].IsHeader;
            html.Append(isHeader ? "<th id=\"" : "<td id=\"");
            html.Append(Html(cellId));
            html.Append("\" class=\"scada-modern-table__cell\" role=\"gridcell\" data-row=\"");
            html.Append(cell.Row.ToString(CultureInfo.InvariantCulture));
            html.Append("\" data-column=\"");
            html.Append(cell.Column.ToString(CultureInfo.InvariantCulture));
            html.Append("\" style=\"");
            AppendCellStyle(html, cell, format);
            html.Append("\">");
            AppendContent(html, cell.EffectiveContent, cellId);
            html.Append(isHeader ? "</th>" : "</td>");
        }
        html.Append("</tbody>");
        AppendPhysicalBorders(html, table);
        html.Append("</table>");
        return html.ToString();
    }

    private static void AppendCellStyle(StringBuilder html, ScadaTableCell cell, ScadaTableFormat format)
    {
        html.Append($"grid-row:{cell.Row + 1}/span {Math.Max(1, cell.RowSpan)};");
        html.Append($"grid-column:{cell.Column + 1}/span {Math.Max(1, cell.ColumnSpan)};");
        html.Append($"background:{Css(format.Background, "#FFFFFF")};color:{Css(format.Foreground, "#0F2A30")};");
        html.Append($"border:{Format(format.GridWidth ?? 1)}px {GridStyle(format.GridStyle)} {Css(format.GridColor, "#8AA0A6")};");
        html.Append($"padding:{Format(format.Padding ?? 4)}px;");
        html.Append($"font-family:{Css(format.FontFamily, "Segoe UI")};font-size:{Format(format.FontSize ?? 14)}px;");
        html.Append($"font-weight:{Css(format.FontWeight, "Normal")};font-style:{Css(format.FontStyle, "Normal")};");
        html.Append($"text-align:{Horizontal(format.HorizontalAlignment)};align-items:{Vertical(format.VerticalAlignment)};");
        html.Append($"white-space:{(format.TextWrap == true ? "normal" : "nowrap")};overflow-wrap:{(format.TextWrap == true ? "anywhere" : "normal")};");
        if (format.LineHeight is double lineHeight) html.Append($"line-height:{Format(lineHeight)}px;");
    }

    private static void AppendPhysicalBorders(StringBuilder html, ScadaTableDefinition table)
    {
        var x = Prefix(table.EffectiveColumns.Select(c => c.Width));
        var y = Prefix(table.EffectiveRows.Select(r => r.Height));
        for (var line = 0; line <= table.EffectiveRows.Count; line++)
        for (var segment = 0; segment < table.EffectiveColumns.Count; segment++)
        {
            if (!ScadaTableBorderResolver.IsVisible(table, ScadaTableBorderOrientation.Horizontal, line, segment)) continue;
            var b = ScadaTableBorderResolver.Resolve(table, ScadaTableBorderOrientation.Horizontal, line, segment);
            AppendBorder(html, b, x[segment], y[line] - b.Width / 2, table.EffectiveColumns[segment].Width, b.Width);
        }
        for (var line = 0; line <= table.EffectiveColumns.Count; line++)
        for (var segment = 0; segment < table.EffectiveRows.Count; segment++)
        {
            if (!ScadaTableBorderResolver.IsVisible(table, ScadaTableBorderOrientation.Vertical, line, segment)) continue;
            var b = ScadaTableBorderResolver.Resolve(table, ScadaTableBorderOrientation.Vertical, line, segment);
            AppendBorder(html, b, x[line] - b.Width / 2, y[segment], b.Width, table.EffectiveRows[segment].Height);
        }
    }

    private static double[] Prefix(IEnumerable<double> sizes)
    {
        var values = new List<double> { 0 }; foreach (var size in sizes) values.Add(values[^1] + size); return values.ToArray();
    }
    private static void AppendBorder(StringBuilder html, ScadaTableBorder border, double left, double top, double width, double height)
    {
        if (border.Style == ScadaTableGridStyle.None || border.Width <= 0) return;
        html.Append($"<span aria-hidden=\"true\" class=\"scada-modern-table__border\" style=\"position:absolute;pointer-events:none;z-index:3;left:{Format(left)}px;top:{Format(top)}px;width:{Format(width)}px;height:{Format(height)}px;background:{Css(border.Color, "transparent")};\"></span>");
    }

    private static void AppendContent(StringBuilder html, ScadaTableCellContent content, string cellId)
    {
        switch (content.Kind)
        {
            case ScadaTableCellContentKind.InputText:
                html.Append($"<input id=\"{Html(cellId)}__input\" type=\"text\" value=\"{Html(content.Text)}\" placeholder=\"{Html(content.Placeholder)}\"");
                if (content.IsReadOnly) html.Append(" readonly");
                html.Append(" />");
                break;
            case ScadaTableCellContentKind.InputNumeric:
                html.Append($"<input id=\"{Html(cellId)}__input\" type=\"number\" value=\"{Html(content.NumericValue?.ToString(CultureInfo.InvariantCulture) ?? content.Text)}\" placeholder=\"{Html(content.Placeholder)}\"");
                AppendNumberAttribute(html, "min", content.Minimum);
                AppendNumberAttribute(html, "max", content.Maximum);
                AppendNumberAttribute(html, "step", content.Step);
                if (content.IsReadOnly) html.Append(" readonly");
                html.Append(" />");
                break;
            default:
                html.Append("<span data-scada-table-text>");
                html.Append(Html(content.Text));
                html.Append("</span>");
                break;
        }
    }

    private static void AppendNumberAttribute(StringBuilder html, string name, double? value)
    {
        if (value.HasValue) html.Append($" {name}=\"{Format(value.Value)}\"");
    }

    private static string GridStyle(ScadaTableGridStyle? value) => (value ?? ScadaTableGridStyle.Solid).ToString().ToLowerInvariant();
    private static string Horizontal(ScadaTableHorizontalAlignment? value) => (value ?? ScadaTableHorizontalAlignment.Left).ToString().ToLowerInvariant();
    private static string Vertical(ScadaTableVerticalAlignment? value) => (value ?? ScadaTableVerticalAlignment.Middle) switch
    {
        ScadaTableVerticalAlignment.Top => "flex-start",
        ScadaTableVerticalAlignment.Bottom => "flex-end",
        _ => "center"
    };
    private static string Css(string? value, string fallback) => Html(string.IsNullOrWhiteSpace(value) ? fallback : value);
    private static string Html(string value) => HtmlEncoder.Default.Encode(value);
    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
