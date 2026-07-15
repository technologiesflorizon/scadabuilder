using System.Text.Json.Nodes;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class Ft100PackageValidatorTests
{
    [TestMethod]
    public void Manifest22AcceptsExactNumericTableCellTarget()
    {
        var (root, package) = CreatePackage(BuildManifest("2.2", includeBinding: true), ValidHtml());
        try
        {
            var result = Ft100PackageValidator.ValidatePackageDirectory(package);
            Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors.Select(issue => $"{issue.Code}: {issue.Message}")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Manifest21RemainsValidWithoutTableCellBindings()
    {
        var (root, package) = CreatePackage(BuildManifest("2.1", includeBinding: false), UnboundHtml());
        try
        {
            var result = Ft100PackageValidator.ValidatePackageDirectory(package);
            Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors.Select(issue => issue.Message)));
            Assert.IsFalse(result.Issues.Any(issue => issue.Code == "manifest-version-legacy"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void InvalidTableCellContractReportsStableBlockingDiagnostics()
    {
        var manifest = BuildManifest("2.1", includeBinding: true);
        var page = manifest["Pages"]!.AsArray()[0]!.AsObject();
        var table = page["Objects"]!.AsArray()[0]!.AsObject();
        var binding = table["TableCellBindings"]!.AsArray()[0]!.AsObject();
        binding["Kind"] = "InputText";
        binding["TargetId"] = "table_001";
        var data = (JsonObject)binding["Data"]!;
        data["IsReadOnly"] = true;
        data["Min"] = 100;
        data["Max"] = 10;
        data["Step"] = 0;
        data["DisplayFormat"] = "not-supported";
        var values = (JsonObject)binding["ValueBindings"]!;
        values["ReadTagId"] = "tag.disabled";
        values["WriteTagId"] = "tag.readonly";
        table["TableCellBindings"]!.AsArray().Add(binding.DeepClone());

        var html = ValidHtml().Replace(
            "id=\"ft100-page__table_001\"",
            "id=\"ft100-page__table_001\" data-scada-role=\"numeric\" data-scada-mapping-id=\"wrong\"",
            StringComparison.Ordinal);
        var (root, package) = CreatePackage(manifest, html);
        try
        {
            var result = Ft100PackageValidator.ValidatePackageDirectory(package);
            var codes = result.Errors.Select(issue => issue.Code).ToHashSet(StringComparer.Ordinal);
            CollectionAssert.IsSubsetOf(new[]
            {
                "table-cell.version",
                "table-cell.wrapper-binding",
                "table-cell.kind",
                "table-cell.target-id",
                "table-cell.target-missing",
                "table-cell.target-duplicate",
                "table-cell.read-tag",
                "table-cell.write-tag-readonly",
                "table-cell.readonly-write",
                "table-cell.range",
                "table-cell.step",
                "table-cell.display-format"
            }, codes.ToArray());
            Assert.IsTrue(result.Errors.Where(issue => issue.Code.StartsWith("table-cell.", StringComparison.Ordinal))
                .All(issue => issue.Message.Contains("Elements[table_001].Table.Cells[1,0]", StringComparison.Ordinal) ||
                              issue.Code is "table-cell.version" or "table-cell.wrapper-binding"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void UnknownManifestVersionIsRejected()
    {
        var (root, package) = CreatePackage(BuildManifest("3.0", includeBinding: false), UnboundHtml());
        try
        {
            var result = Ft100PackageValidator.ValidatePackageDirectory(package);
            Assert.IsTrue(result.Errors.Any(issue => issue.Code == "manifest-version-unsupported"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static JsonObject BuildManifest(string version, bool includeBinding)
    {
        var table = new JsonObject
        {
            ["Id"] = "table_001",
            ["Kind"] = "Table",
            ["ValueBindings"] = new JsonObject { ["ReadTagId"] = null, ["WriteTagId"] = null },
            ["TableCellBindings"] = new JsonArray()
        };
        if (includeBinding)
        {
            table["TableCellBindings"]!.AsArray().Add(new JsonObject
            {
                ["Row"] = 1,
                ["Column"] = 0,
                ["TargetId"] = "table_001__cell-1-0",
                ["Kind"] = "InputNumeric",
                ["Data"] = new JsonObject
                {
                    ["Placeholder"] = "0.0",
                    ["DisplayFormat"] = "##.#",
                    ["IsReadOnly"] = false,
                    ["Min"] = 0,
                    ["Max"] = 100,
                    ["Step"] = 0.1
                },
                ["ValueBindings"] = new JsonObject { ["ReadTagId"] = "tag.read", ["WriteTagId"] = "tag.write" }
            });
        }

        return new JsonObject
        {
            ["Name"] = "Validator",
            ["ManifestVersion"] = version,
            ["HomePageId"] = "page",
            ["Pages"] = new JsonArray(new JsonObject
            {
                ["Id"] = "page",
                ["Name"] = "Page",
                ["Type"] = "default",
                ["IncludeInBuild"] = true,
                ["RelativePath"] = "page/page.html",
                ["Objects"] = new JsonArray(table)
            }),
            ["Actions"] = new JsonArray(),
            ["Tags"] = new JsonArray(
                new JsonObject { ["Id"] = "tag.read", ["Enabled"] = true, ["Writeable"] = false },
                new JsonObject { ["Id"] = "tag.write", ["Enabled"] = true, ["Writeable"] = true },
                new JsonObject { ["Id"] = "tag.disabled", ["Enabled"] = false, ["Writeable"] = true },
                new JsonObject { ["Id"] = "tag.readonly", ["Enabled"] = true, ["Writeable"] = false })
        };
    }

    private static (string Root, string Package) CreatePackage(JsonObject manifest, string html)
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var package = Path.Combine(root, Ft100SceneExporter.ProjectPackageDirectoryName);
        var page = Path.Combine(package, "page");
        Directory.CreateDirectory(page);
        File.WriteAllText(Path.Combine(package, "manifest.json"), manifest.ToJsonString());
        File.WriteAllText(Path.Combine(page, "page.html"), html);
        File.WriteAllText(Path.Combine(package, "scada-runtime.12345678.js"), "window.scadaRuntime = {};");
        return (root, package);
    }

    private static string ValidHtml() => """
        <!doctype html><html><body>
        <div id="ft100-page">
          <div id="ft100-page__table_001">
            <table><tbody><tr><td id="ft100-page__table_001__cell-1-0" data-row="1" data-column="0" data-scada-table-cell-kind="InputNumeric"><input id="ft100-page__table_001__cell-1-0__input" type="number" min="0" max="100" step="0.1"></td></tr></tbody></table>
          </div>
        </div></body></html>
        """;

    private static string UnboundHtml() => """
        <!doctype html><html><body><div id="ft100-page"><div id="ft100-page__table_001"><table></table></div></div></body></html>
        """;
}
