using System.IO.Compression;
using System.Text;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ModernProjects;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class AdvancedTableAuthoringIntegrationTests
{
    [TestMethod]
    public async Task Win00012RepresentativeTableSurvivesEditReloadPreviewAndSb2Export()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var previewRoot = Path.Combine(root, "preview");
        var archivePath = Path.Combine(root, "exports", "win00012-table.sb2");
        var store = new ModernProjectStore();
        try
        {
            var table = BuildRepresentativeTable();
            var element = ScadaElement.CreateTable("table_win00012", "Rapport", 48, 72, 16, 10) with
            {
                Table = table,
                Bounds = new SceneBounds(48, 72, table.Width, table.Height),
                IsLocked = true
            };
            var scene = ScadaScene.CreateEmpty("win00012-modern", "Rapport moderne", new(1600, 1000)).WithElement(element);
            await store.SaveSceneAsync(root, scene);

            var reloaded = await store.LoadOrCreateSceneAsync(root, scene.Id, scene.Title, scene.CanvasSize);
            var loadedElement = reloaded.Elements.Single();
            var loadedTable = loadedElement.Table!;
            Assert.IsTrue(loadedElement.IsLocked);
            Assert.AreEqual(16, loadedTable.EffectiveRows.Count);
            Assert.AreEqual(10, loadedTable.EffectiveColumns.Count);
            Assert.AreEqual(2, loadedTable.EffectiveRows.TakeWhile(row => row.IsHeader).Count());
            Assert.AreEqual(10, loadedTable.EffectiveCells.Single(cell => cell.Row == 0).ColumnSpan);
            Assert.IsTrue(loadedTable.EffectiveBorderOverrides.Count > 100);

            var page = new ScadaSceneReference(reloaded.Id, reloaded.Title, $"scenes/{reloaded.Id}.scene.json", CanvasSize: reloaded.CanvasSize, Origin: PageOrigin.Native);
            var preview = await PreviewDocument.MaterializeNativeAsync(new PageDocumentInput(page, reloaded), previewRoot);
            var previewHtml = await File.ReadAllTextAsync(preview.GetSourcePath(previewRoot));
            StringAssert.Contains(previewHtml, "<th id=");
            StringAssert.Contains(previewHtml, "<tr style=\"display:contents\">");
            StringAssert.Contains(previewHtml, "colspan=\"10\"");
            StringAssert.Contains(previewHtml, "type=\"text\"");
            StringAssert.Contains(previewHtml, "type=\"number\"");
            StringAssert.Contains(previewHtml, "Section production");

            var project = ScadaProject.CreateDefault("Win00012Table") with
            {
                HomePageId = page.EffectivePageCode,
                Scenes = [page]
            };
            Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
            var export = await new Ft100SceneExporter().ExportProjectArchiveAsync(
                project,
                [new Ft100ProjectPageExportInput(reloaded, null, page)],
                archivePath);
            Assert.IsTrue(export.Validation.IsValid);

            using var archive = ZipFile.OpenRead(archivePath);
            var entry = archive.GetEntry($"{Ft100SceneExporter.ProjectPackageDirectoryName}/{page.EffectivePageCode}/{page.EffectivePageCode}.html");
            Assert.IsNotNull(entry);
            using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
            var html = await reader.ReadToEndAsync();
            StringAssert.Contains(html, "grid-template-columns:82px 104px 126px");
            StringAssert.Contains(html, "white-space:normal");
            StringAssert.Contains(html, "border-top:3px double #245766");
            Assert.IsFalse(html.Contains("IsLocked", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("data-editor-locked", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("scada-editor-table-header", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("scada-editor-table-track", StringComparison.Ordinal));
            Assert.IsFalse(html.Contains("ValueBindings", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static ScadaTableDefinition BuildRepresentativeTable()
    {
        var table = ScadaTableDefinition.CreateDefault(16, 10, firstRowIsHeader: false) with
        {
            Columns = Enumerable.Range(0, 10).Select(index => new ScadaTableColumn(82 + index % 4 * 22, index == 0 ? new(Foreground: "#123D48") : null)).ToArray(),
            Rows = Enumerable.Range(0, 16).Select(index => new ScadaTableRow(28 + index % 3 * 6, IsHeader: index < 2)).ToArray(),
            Style = new ScadaTableStyle(
                new(Background: "#FFFFFF", Foreground: "#17343B", GridColor: "#8AA0A6", GridWidth: 1, GridStyle: ScadaTableGridStyle.Solid, Padding: 5, FontFamily: "Segoe UI", FontSize: 13, TextWrap: true, LineHeight: 18),
                new(Background: "#DDEFF2", Foreground: "#102D34", FontWeight: "Bold", HorizontalAlignment: ScadaTableHorizontalAlignment.Center),
                new(Background: "#F2F8F9"))
        };
        table = ScadaTableStructureOperations.Merge(table, new(0, 0, 0, 9));
        table = ScadaTableContentOperations.SetContent(table, 0, 0, new(Text: "Section production"));
        table = ScadaTableContentOperations.SetContent(table, 2, 1, new(ScadaTableCellContentKind.InputText, "Lot A", "Numéro de lot"));
        table = ScadaTableContentOperations.SetContent(table, 2, 2, new(ScadaTableCellContentKind.InputNumeric, NumericValue: 42.5, Minimum: 0, Maximum: 100, Step: 0.5));
        table = ScadaTableFormatOperations.ApplyFormat(table, new(ScadaTableFormatScopeKind.Rows, new(5, 0, 5, 9)), new(Background: "#FFF5DA", FontStyle: "Italic"));
        table = ScadaTableFormatOperations.ApplyFormat(table, new(ScadaTableFormatScopeKind.Cells, new(8, 4, 8, 4)), new(Background: "#FBD4D4", Foreground: "#781D1D", FontWeight: "Bold"));
        table = ScadaTableBorderOperations.ApplyPreset(table, new(0, 0, 15, 9), ScadaTableBorderPreset.Outline, new(ScadaTableGridStyle.Double, "#245766", 3));
        table = ScadaTableBorderOperations.ApplyPreset(table, new(0, 0, 15, 9), ScadaTableBorderPreset.Inside, new(ScadaTableGridStyle.Solid, "#9DB2B8", 1));
        table = ScadaTableBorderOperations.ApplyPreset(table, new(7, 0, 7, 9), ScadaTableBorderPreset.Bottom, new(ScadaTableGridStyle.Dashed, "#B45A1A", 2));
        ScadaTableOperations.ValidateDefinition(table);
        return table;
    }
}
