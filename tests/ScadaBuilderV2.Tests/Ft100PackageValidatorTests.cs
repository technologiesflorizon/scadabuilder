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
    public void Manifest23AcceptsKnownSortedCapabilitiesAndMatchingRuntimeHash()
    {
        var (root, package) = CreatePackage(BuildManifest("2.3", includeBinding: false), UnboundHtml());
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
    public void Manifest23RejectsUnknownDuplicateUnsortedAndBlockedCapabilities()
    {
        var manifest = BuildManifest("2.3", includeBinding: false);
        manifest["RuntimeContract"] = new JsonObject
        {
            ["Version"] = "1.0",
            ["RequiredCapabilities"] = new JsonArray("page.default", "action.show", "page.default", "aaa.unknown")
        };
        var (root, package) = CreatePackage(manifest, UnboundHtml());
        try
        {
            var codes = Ft100PackageValidator.ValidatePackageDirectory(package).Errors
                .Select(issue => issue.Code)
                .ToHashSet(StringComparer.Ordinal);
            CollectionAssert.IsSubsetOf(new[]
            {
                "runtime-contract.capability-duplicate",
                "runtime-contract.capability-order",
                "runtime-contract.capability-blocked",
                "runtime-contract.capability-unknown"
            }, codes.ToArray());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Manifest23RejectsUnsupportedRuntimeContractVersionAndMissingHash()
    {
        var manifest = BuildManifest("2.3", includeBinding: false);
        manifest["RuntimeContract"] = new JsonObject
        {
            ["Version"] = "9.0",
            ["RequiredCapabilities"] = new JsonArray("page.default")
        };
        var (root, package) = CreatePackage(manifest, UnboundHtml(), completeRuntimeContract: false);
        try
        {
            var codes = Ft100PackageValidator.ValidatePackageDirectory(package).Errors
                .Select(issue => issue.Code)
                .ToHashSet(StringComparer.Ordinal);
            Assert.IsTrue(codes.Contains("runtime-contract.version"));
            Assert.IsTrue(codes.Contains("runtime-contract.hash"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Manifest23RejectsMissingRuntimeContract()
    {
        var (root, package) = CreatePackage(
            BuildManifest("2.3", includeBinding: false),
            UnboundHtml(),
            completeRuntimeContract: false);
        try
        {
            var result = Ft100PackageValidator.ValidatePackageDirectory(package);
            Assert.IsTrue(result.Errors.Any(issue => issue.Code == "runtime-contract.missing"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Manifest23RejectsAlteredPackagedRuntime()
    {
        var (root, package) = CreatePackage(BuildManifest("2.3", includeBinding: false), UnboundHtml());
        try
        {
            File.AppendAllText(Directory.GetFiles(package, "scada-runtime.*.js").Single(), "// tampered");
            var codes = Ft100PackageValidator.ValidatePackageDirectory(package).Errors
                .Select(issue => issue.Code)
                .ToHashSet(StringComparer.Ordinal);
            Assert.IsTrue(codes.Contains("runtime-contract.hash-mismatch"));
            Assert.IsTrue(codes.Contains("runtime-contract.runtime-filename"));
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

    private static (string Root, string Package) CreatePackage(
        JsonObject manifest,
        string html,
        bool completeRuntimeContract = true)
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var package = Path.Combine(root, Ft100SceneExporter.ProjectPackageDirectoryName);
        var page = Path.Combine(package, "page");
        Directory.CreateDirectory(page);
        File.WriteAllText(Path.Combine(page, "page.html"), html);
        var runtimePath = Path.Combine(package, "scada-runtime.12345678.js");
        File.WriteAllText(runtimePath, "window.scadaRuntime = {};");
        if (completeRuntimeContract && string.Equals(manifest["ManifestVersion"]?.GetValue<string>(), "2.3", StringComparison.Ordinal))
        {
            var contract = manifest["RuntimeContract"] as JsonObject;
            if (contract is null)
            {
                contract = new JsonObject
                {
                    ["Version"] = "1.0",
                    ["RequiredCapabilities"] = new JsonArray("page.default")
                };
                manifest["RuntimeContract"] = contract;
            }
            contract["RuntimeSha256"] = Ft100SceneExporter.Sha256Hash(runtimePath);
            var runtimeHash = contract["RuntimeSha256"]!.GetValue<string>();
            var hashedRuntimePath = Path.Combine(package, $"scada-runtime.{runtimeHash[..8]}.js");
            File.Move(runtimePath, hashedRuntimePath);
        }
        File.WriteAllText(Path.Combine(package, "manifest.json"), manifest.ToJsonString());
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

    [TestMethod]
    public void QuickWindowRegistriesAreValidatedAgainstTheirContentFiles()
    {
        var root = QuickWindowPackage(out var packageDirectory);
        try
        {
            var result = Ft100PackageValidator.ValidatePackageDirectory(packageDirectory);

            Assert.IsTrue(
                result.Errors.All(issue => !issue.Code.StartsWith("quick-window", StringComparison.Ordinal)),
                "A contractual quick-window package must raise no quick-window error: " +
                string.Join(", ", result.Errors.Select(issue => issue.Code)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void AQuickWindowWithoutItsContentFilesIsRejected()
    {
        var root = QuickWindowPackage(out var packageDirectory);
        try
        {
            File.Delete(Path.Combine(packageDirectory, "qw-a1b2c3d4", "qw-a1b2c3d4.html"));

            var result = Ft100PackageValidator.ValidatePackageDirectory(packageDirectory);

            Assert.IsTrue(result.Errors.Any(issue => issue.Code == "quick-window-missing-html"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void AnInvocationTargetingAnUndeclaredDefinitionIsRejected()
    {
        var root = QuickWindowPackage(out var packageDirectory, invocationDefinitionKey: "00000000-0000-0000-0000-000000000000");
        try
        {
            var result = Ft100PackageValidator.ValidatePackageDirectory(packageDirectory);

            Assert.IsTrue(result.Errors.Any(issue => issue.Code == "quick-window-invocation-target-missing"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void AQuickWindowShippedOutsideItsContractualPathIsRejected()
    {
        var root = QuickWindowPackage(out var packageDirectory, relativePath: "quick-windows/qw-a1b2c3d4.html");
        try
        {
            var result = Ft100PackageValidator.ValidatePackageDirectory(packageDirectory);

            Assert.IsTrue(
                result.Errors.Any(issue => issue.Code == "quick-window-path-not-contractual"),
                "Nesting under a grouping directory collapses at deployment and must be refused.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string QuickWindowPackage(
        out string packageDirectory,
        string? invocationDefinitionKey = null,
        string? relativePath = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "scada-quick-window-package", Guid.NewGuid().ToString("N"));
        packageDirectory = Path.Combine(root, Ft100SceneExporter.ProjectPackageDirectoryName);
        Directory.CreateDirectory(packageDirectory);

        const string ns = "qw-a1b2c3d4";
        const string definitionKey = "a1b2c3d4-1111-4222-8333-aaaaaaaaaaaa";
        var html = relativePath ?? $"{ns}/{ns}.html";
        var css = $"{ns}/css/{ns}.css";

        Directory.CreateDirectory(Path.Combine(packageDirectory, "win00003"));
        File.WriteAllText(
            Path.Combine(packageDirectory, "win00003", "win00003.html"),
            "<!doctype html><html><body><div id=\"ft100-win00003\" data-scada-width=\"1920\" data-scada-height=\"1080\"></div></body></html>");
        Directory.CreateDirectory(Path.Combine(packageDirectory, "win00003", "css"));
        File.WriteAllText(Path.Combine(packageDirectory, "win00003", "css", "win00003.css"), "#ft100-win00003 { position: relative; }");

        Directory.CreateDirectory(Path.Combine(packageDirectory, ns));
        Directory.CreateDirectory(Path.Combine(packageDirectory, ns, "css"));
        File.WriteAllText(
            Path.Combine(packageDirectory, ns, $"{ns}.html"),
            $"<!doctype html><html><body><div id=\"ft100-{ns}\"></div></body></html>");
        File.WriteAllText(Path.Combine(packageDirectory, ns, "css", $"{ns}.css"), $"#ft100-{ns} {{ position: relative; }}");

        var manifest = $$"""
{
  "Name": "QuickWindowPackage",
  "ManifestVersion": "2.3",
  "HomePageId": "win00003",
  "Pages": [
    {
      "Id": "win00003",
      "Name": "win00003",
      "Type": "Default",
      "IncludeInBuild": true,
      "IsHome": true,
      "RelativePath": "win00003/win00003.html",
      "Width": 1920,
      "Height": 1080
    }
  ],
  "QuickWindows": [
    {
      "DefinitionKey": "{{definitionKey}}",
      "Code": "moteur",
      "DisplayName": "Moteur",
      "InterfaceVersion": 1,
      "Namespace": "{{ns}}",
      "RelativePath": "{{html}}",
      "CssRelativePath": "{{css}}",
      "Width": 480,
      "Height": 320,
      "InterfaceMembers": [],
      "PresentationDefaults": { "Position": "Center", "Backdrop": true }
    }
  ],
  "QuickWindowInvocations": [
    {
      "InvocationKey": "cccccccc-dddd-eeee-ffff-000011112222",
      "DefinitionKey": "{{invocationDefinitionKey ?? definitionKey}}",
      "InterfaceVersion": 1,
      "Bindings": []
    }
  ]
}
""";
        File.WriteAllText(Path.Combine(packageDirectory, "manifest.json"), manifest);
        return root;
    }
}
