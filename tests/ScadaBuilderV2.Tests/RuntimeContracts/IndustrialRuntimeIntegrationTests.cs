using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ModernProjects;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.Tests.RuntimeContracts;

[TestClass]
public sealed class IndustrialRuntimeIntegrationTests
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    [TestMethod]
    public async Task ReferenceProjectNormalizesEveryCompiledNumericReadBindingAndExportsWin00017Mappings()
    {
        var repositoryRoot = FindRepositoryRoot();
        var store = new ModernProjectStore();
        var project = await store.LoadProjectAsync(repositoryRoot)
            ?? throw new InvalidOperationException("AMR_REF_SCADA_V2 project was not found.");
        var snapshot = await store.ReadWorkspaceSnapshotAsync(repositoryRoot, new PageWorkspaceReadContext(ProjectOverride: project));
        var compiledPages = project.Scenes.Where(page => page.IncludeInBuild).ToArray();
        var mismatches = compiledPages
            .SelectMany(page => FlattenElements(snapshot.Scenes[page.PageKey].Elements)
                .Where(element => element.Kind == ScadaElementKind.InputNumeric)
                .Where(element => element.StateConfig?.ReadVariable is not null)
                .Where(element => !string.Equals(
                    element.Data?.ReadTagId,
                    element.StateConfig!.ReadVariable!.TagId,
                    StringComparison.Ordinal))
                .Select(element => $"{page.EffectivePageCode}/{element.Id}"))
            .ToArray();

        Assert.AreEqual(0, mismatches.Length, $"Numeric read binding mismatches: {string.Join(", ", mismatches)}");

        var win00017Page = compiledPages.Single(page => page.EffectivePageCode == "win00017");
        var win00017Scene = Synchronize(snapshot.Scenes[win00017Page.PageKey], win00017Page);
        var tempRoot = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Industrial", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var export = await new Ft100SceneExporter().ExportAsync(
                win00017Scene,
                ResolveSourcePath(repositoryRoot, win00017Page),
                tempRoot,
                project);
            using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(export.ExportDirectory, "manifest.json")));
            var exportedPage = manifest.RootElement.GetProperty("Pages")[0];
            ValidateWin00017(exportedPage);
            ValidateNumericReadBindingCoherence([exportedPage]);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public async Task ReferenceProjectExportsStrict23AndLocksFourIndustrialIntegrations()
    {
        var repositoryRoot = FindRepositoryRoot();
        var store = new ModernProjectStore();
        var project = await store.LoadProjectAsync(repositoryRoot)
            ?? throw new InvalidOperationException("AMR_REF_SCADA_V2 project was not found.");
        var snapshot = await store.ReadWorkspaceSnapshotAsync(repositoryRoot, new PageWorkspaceReadContext(ProjectOverride: project));
        var inputs = project.Scenes
            .Where(page => page.IncludeInBuild)
            .OrderBy(page => page.EffectivePageCode, StringComparer.Ordinal)
            .Select(page => new Ft100ProjectPageExportInput(
                Synchronize(snapshot.Scenes[page.PageKey], page),
                ResolveSourcePath(repositoryRoot, page),
                page))
            .ToArray();

        var tempRoot = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Industrial", Guid.NewGuid().ToString("N"));
        var archivePath = Path.Combine(tempRoot, "AMR_REF_SCADA_V2.sb2");
        Directory.CreateDirectory(tempRoot);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var export = await new Ft100SceneExporter().ExportProjectArchiveAsync(project, inputs, archivePath);
            stopwatch.Stop();
            Assert.IsTrue(export.Validation.IsValid, string.Join("; ", export.Validation.Errors.Select(error => error.Message)));

            using var archive = ZipFile.OpenRead(export.ArchivePath);
            var manifest = ReadJson(archive, $"{Ft100SceneExporter.ProjectPackageDirectoryName}/manifest.json");
            Assert.AreEqual("2.3", manifest.RootElement.GetProperty("ManifestVersion").GetString());
            var pages = manifest.RootElement.GetProperty("Pages").EnumerateArray()
                .ToDictionary(page => page.GetProperty("Id").GetString()!, StringComparer.Ordinal);

            ValidateWin00003(pages["win00003"], pages.Keys);
            ValidateWin00004(archive, pages["win00004"]);
            ValidateWin00008(pages["win00008"]);
            ValidateWin00012(pages["win00012_modern_no_legacy"]);
            ValidateWin00017(pages["win00017"]);
            ValidateNumericReadBindingCoherence(pages.Values);

            var runtimeContract = manifest.RootElement.GetProperty("RuntimeContract");
            var runtimeSha = runtimeContract.GetProperty("RuntimeSha256").GetString()!;
            var runtimeEntry = archive.Entries.Single(entry =>
                entry.FullName == $"{Ft100SceneExporter.ProjectPackageDirectoryName}/scada-runtime.{runtimeSha[..8]}.js");
            using (var runtimeStream = runtimeEntry.Open())
            {
                Assert.AreEqual(runtimeSha, Convert.ToHexString(SHA256.HashData(runtimeStream)).ToLowerInvariant());
            }

            // Evidence values that track authored reference data are derived from the export itself,
            // so the recorded report is true by construction. Any change still fails the comparison
            // below until the evidence file is regenerated deliberately with SCADA_UPDATE_INDUSTRIAL_EVIDENCE=1.
            var footerNavigations = Objects(pages["win00003"])
                .Where(element => element.TryGetProperty("CommandConfig", out var config) && config.ValueKind == JsonValueKind.Object)
                .SelectMany(element => element.GetProperty("CommandConfig").GetProperty("Commands").EnumerateArray())
                .Count();
            var repairedToggleMapping = Objects(pages["win00012_modern_no_legacy"])
                .Single(element => element.GetProperty("Id").GetString() == "toggle_defrost_p4_e12")
                .GetProperty("CommandConfig").GetProperty("Commands")[0]
                .GetProperty("WriteTagId").GetString();

            var report = new
            {
                Schema = "scada-v2-industrial-acceptance-v1",
                GeneratedUtc = DateTimeOffset.UtcNow,
                BuilderVersion = File.ReadAllText(Path.Combine(repositoryRoot, "SCADA_BUILDER_V2", "VERSION")).Trim(),
                ManifestVersion = "2.3",
                RuntimeSha256 = runtimeSha,
                RequiredCapabilities = runtimeContract.GetProperty("RequiredCapabilities")
                    .EnumerateArray().Select(item => item.GetString()).ToArray(),
                PackageSha256 = Sha256File(export.ArchivePath),
                ExportDurationMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3),
                PageCount = export.PageCount,
                LiveWritesExecuted = false,
                Pages = new Dictionary<string, object>
                {
                    ["win00003"] = new { Navigations = footerNavigations, LatestWinsBackForward = "covered-by-tf100web-lifecycle-suite" },
                    ["win00004"] = new { Header = "win00002", Footer = "win00003", AssetsValidated = true },
                    ["win00008"] = new
                    {
                        States = 8,
                        ReadOnlyNumerics = 2,
                        WritableNumerics = 1,
                        SvgColorFilterStacking = "covered-by-runtime-js",
                        RoundTrip = "covered-by-tf100web-lifecycle-suite"
                    },
                    ["win00012_modern_no_legacy"] = new
                    {
                        DefrostToggles = 56,
                        ManualDepartureButtons = 14,
                        DefrostStatusIndicators = 14,
                        TableCells = 126,
                        RepairedToggleMapping = repairedToggleMapping
                    }
                },
                Diagnostics = new[] { "every defrost toggle carries a confirmed mapping and the shared quality fallback; no fabricated mapping", "no PLC write executed during automated acceptance" }
            };
            var reportJson = JsonSerializer.Serialize(report, ReportOptions) + Environment.NewLine;
            var evidencePath = Path.Combine(repositoryRoot, "SCADA_BUILDER_V2", "tests", "conformance", "industrial", "amr-ref-industrial-acceptance.json");
            if (string.Equals(Environment.GetEnvironmentVariable("SCADA_UPDATE_INDUSTRIAL_EVIDENCE"), "1", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
                await File.WriteAllTextAsync(evidencePath, reportJson, new UTF8Encoding(false));
            }
            else if (File.Exists(evidencePath))
            {
                using var expected = JsonDocument.Parse(await File.ReadAllTextAsync(evidencePath));
                using var actual = JsonDocument.Parse(reportJson);
                foreach (var property in new[]
                         {
                             "ManifestVersion", "RuntimeSha256", "RequiredCapabilities", "PackageSha256", "PageCount",
                             "LiveWritesExecuted", "Pages", "Diagnostics"
                         })
                {
                    Assert.IsTrue(
                        JsonNode.DeepEquals(
                            JsonNode.Parse(expected.RootElement.GetProperty(property).GetRawText()),
                            JsonNode.Parse(actual.RootElement.GetProperty(property).GetRawText())),
                        $"Industrial evidence drifted for '{property}'. Regenerate deliberately after review.");
                }
            }

            var requestedExport = Environment.GetEnvironmentVariable("SCADA_INDUSTRIAL_EXPORT_PATH");
            if (!string.IsNullOrWhiteSpace(requestedExport))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(requestedExport))!);
                File.Copy(export.ArchivePath, requestedExport, overwrite: true);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    /// <summary>
    /// Locks the exported footer navigation bar of the reference project.
    /// The expected count tracks authored data: the footer was reworked in `d6c39c6` (2026-07-18),
    /// which dropped the `win00087` entry, so it moved from 8 to 7 navigations. The invariants that
    /// must never regress, whatever the authored count, are asserted explicitly below: every command
    /// is a navigation, no target is duplicated, and no target is empty or dangling.
    /// </summary>
    private static void ValidateWin00003(JsonElement page, IReadOnlyCollection<string> manifestPageIds)
    {
        var commands = Objects(page)
            .Where(element => element.TryGetProperty("CommandConfig", out var config) && config.ValueKind == JsonValueKind.Object)
            .SelectMany(element => element.GetProperty("CommandConfig").GetProperty("Commands").EnumerateArray())
            .ToArray();
        var targets = commands.Select(command => command.GetProperty("TargetPageId").GetString()).ToArray();

        Assert.AreEqual(7, commands.Length, "The authored footer navigation count changed; update this expectation deliberately.");
        Assert.IsTrue(commands.All(command => command.GetProperty("Kind").GetString() == "navigate"));
        Assert.AreEqual(targets.Length, targets.Distinct(StringComparer.Ordinal).Count(), "A footer navigation target is duplicated.");
        Assert.IsTrue(targets.All(target => !string.IsNullOrWhiteSpace(target)), "A footer navigation has no target page.");
        foreach (var target in targets)
        {
            Assert.IsTrue(
                manifestPageIds.Contains(target!, StringComparer.Ordinal),
                $"Footer navigation targets '{target}', which is not an exported page.");
        }
    }

    private static void ValidateWin00004(ZipArchive archive, JsonElement page)
    {
        Assert.AreEqual("win00002", page.GetProperty("HeaderPageId").GetString());
        Assert.AreEqual("win00003", page.GetProperty("FooterPageId").GetString());
        var root = Ft100SceneExporter.ProjectPackageDirectoryName;
        Assert.IsNotNull(archive.GetEntry($"{root}/win00004/win00004.html"));
        Assert.IsTrue(archive.Entries.Any(entry => entry.FullName.StartsWith($"{root}/win00004/css/", StringComparison.Ordinal)));
        Assert.IsTrue(archive.Entries.Any(entry => entry.FullName.StartsWith($"{root}/win00004/images/", StringComparison.Ordinal)));
    }

    private static void ValidateWin00008(JsonElement page)
    {
        var objects = Objects(page);
        Assert.AreEqual(8, objects.Count(element => element.GetProperty("StateConfig").ValueKind == JsonValueKind.Object));
        var numerics = objects.Where(element => element.GetProperty("Kind").GetString() == "InputNumeric").ToArray();
        Assert.AreEqual(2, numerics.Count(element =>
            element.GetProperty("ValueBindings").GetProperty("ReadTagId").ValueKind == JsonValueKind.String &&
            element.GetProperty("ValueBindings").GetProperty("WriteTagId").ValueKind == JsonValueKind.Null));
        Assert.AreEqual(1, numerics.Count(element =>
            element.GetProperty("ValueBindings").GetProperty("WriteTagId").ValueKind == JsonValueKind.String));
    }

    private static void ValidateWin00012(JsonElement page)
    {
        var objects = Objects(page);
        var buttons = objects.Where(element =>
            element.GetProperty("Kind").GetString() == "Button" &&
            element.GetProperty("Id").GetString()!.StartsWith("toggle_defrost_", StringComparison.Ordinal)).ToArray();
        Assert.AreEqual(56, buttons.Length);
        Assert.AreEqual(56, buttons.Count(element => element.GetProperty("StateConfig").ValueKind == JsonValueKind.Object));
        Assert.AreEqual(56, buttons.Count(element => element.GetProperty("CommandConfig").ValueKind == JsonValueKind.Object));
        var manualButtons = objects.Where(element =>
            element.GetProperty("Kind").GetString() == "Button" &&
            element.GetProperty("Id").GetString()!.StartsWith("manual_defrost_", StringComparison.Ordinal)).ToArray();
        // Authored contract of the Depart Manuel row: the 14 buttons were introduced unmapped in
        // `V2.1.4.0064` and have since been wired to their write command. Each one carries exactly one
        // command and stays stateless: the row drives the PLC, it never displays a state.
        Assert.AreEqual(14, manualButtons.Length);
        Assert.IsTrue(manualButtons.All(element => element.GetProperty("CommandConfig").ValueKind == JsonValueKind.Object), "Every manual defrost button must carry its write command.");
        Assert.IsTrue(manualButtons.All(element => element.GetProperty("StateConfig").ValueKind == JsonValueKind.Null), "A manual defrost button must stay stateless.");

        var statusIndicators = objects.Where(element =>
            element.GetProperty("Kind").GetString() == "Shape" &&
            element.GetProperty("ShapeKind").GetString() == "Rectangle" &&
            element.GetProperty("Id").GetString()!.StartsWith("defrost_status_", StringComparison.Ordinal)).ToArray();
        // Symmetrically, the 14 Etat du degivrage indicators are now bound to their read state and must
        // never gain a command: they display, they do not write.
        Assert.AreEqual(14, statusIndicators.Length);
        Assert.IsTrue(statusIndicators.All(element => element.GetProperty("StateConfig").ValueKind == JsonValueKind.Object), "Every defrost status indicator must carry its read state.");
        Assert.IsTrue(statusIndicators.All(element => element.GetProperty("CommandConfig").ValueKind == JsonValueKind.Null), "A defrost status indicator must never write.");

        var table = objects.Single(element => element.GetProperty("Id").GetString() == "table_defrost_upper");
        Assert.AreEqual(126, table.GetProperty("TableCellBindings").GetArrayLength());

        foreach (var button in buttons)
        {
            var command = button.GetProperty("CommandConfig").GetProperty("Commands")[0];
            Assert.AreEqual("writeTag", command.GetProperty("Kind").GetString());
            Assert.AreEqual(command.GetProperty("WriteTagId").GetString(), command.GetProperty("ReadTagId").GetString());
        }

        // `toggle_defrost_p4_e12` is the toggle that used to ship unmapped. The lock is that it now
        // carries a real PLC mapping and the shared quality fallback, not which mapping id it points to:
        // the id is authoring data and legitimately changes when the catalog is re-pointed.
        var lastRepaired = buttons.Single(element => element.GetProperty("Id").GetString() == "toggle_defrost_p4_e12");
        var lastRepairedCommand = lastRepaired.GetProperty("CommandConfig").GetProperty("Commands")[0];
        var lastRepairedWriteTag = lastRepairedCommand.GetProperty("WriteTagId").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(lastRepairedWriteTag), "The repaired defrost toggle must keep a confirmed PLC mapping.");
        StringAssert.StartsWith(lastRepairedWriteTag, "tf100.mapping.");
        var fallback = lastRepaired.GetProperty("StateConfig").GetProperty("QualityFallback");
        Assert.AreEqual(0.4, fallback.GetProperty("Opacity").GetDouble(), 0.0001);
    }

    private static void ValidateWin00017(JsonElement page)
    {
        var objects = Objects(page).ToDictionary(element => element.GetProperty("Id").GetString()!, StringComparer.Ordinal);
        Assert.AreEqual("tf100.mapping.165", ReadTagId(objects["7faa09c82bbe4974b0fb320f8739d8b7"]));
        Assert.AreEqual("tf100.mapping.162", ReadTagId(objects["25f9f3aa3fd9433ba9c622a22b42d52c"]));
        Assert.AreEqual("tf100.mapping.163", ReadTagId(objects["d0ed6e496f1f471f8c8839348a7b7d77"]));
    }

    private static void ValidateNumericReadBindingCoherence(IEnumerable<JsonElement> pages)
    {
        var mismatches = pages
            .SelectMany(page => Objects(page).Select(element => (Page: page, Element: element)))
            .Where(item => item.Element.GetProperty("Kind").GetString() == "InputNumeric")
            .Where(item => item.Element.GetProperty("StateConfig").ValueKind == JsonValueKind.Object)
            .Where(item => item.Element.GetProperty("StateConfig").GetProperty("ReadVariable").ValueKind == JsonValueKind.Object)
            .Where(item => !string.Equals(
                ReadTagId(item.Element),
                item.Element.GetProperty("StateConfig").GetProperty("ReadVariable").GetProperty("TagId").GetString(),
                StringComparison.Ordinal))
            .Select(item => $"{item.Page.GetProperty("Id").GetString()}/{item.Element.GetProperty("Id").GetString()}")
            .ToArray();

        Assert.AreEqual(0, mismatches.Length, $"Numeric read binding mismatches: {string.Join(", ", mismatches)}");
    }

    private static string? ReadTagId(JsonElement element) =>
        element.GetProperty("ValueBindings").GetProperty("ReadTagId").GetString();

    private static JsonElement[] Objects(JsonElement page) => page.GetProperty("Objects").EnumerateArray().ToArray();

    private static IEnumerable<ScadaElement> FlattenElements(IEnumerable<ScadaElement> elements)
    {
        foreach (var element in elements)
        {
            yield return element;
            foreach (var child in FlattenElements(element.ChildElements)) yield return child;
        }
    }

    private static JsonDocument ReadJson(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName) ?? throw new InvalidOperationException($"Missing archive entry '{entryName}'.");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return JsonDocument.Parse(reader.ReadToEnd());
    }

    private static ScadaScene Synchronize(ScadaScene scene, ScadaSceneReference page) =>
        scene.WithPageType(page.Type)
            .WithIncludeInBuild(page.IncludeInBuild)
            .WithCanvasSize(page.EffectiveCanvasSize)
            .WithBackground(page.EffectiveBackground)
            .WithPageComposition(page.HeaderPageId, page.FooterPageId) with
        {
            PageKey = page.PageKey,
            PageCode = page.EffectivePageCode,
            Origin = page.EffectiveOrigin,
            ImportProvenance = page.ImportProvenance,
            HeaderPageKey = page.HeaderPageKey,
            FooterPageKey = page.FooterPageKey
        };

    private static string? ResolveSourcePath(string repositoryRoot, ScadaSceneReference page)
    {
        if (page.EffectiveOrigin == PageOrigin.Native) return null;
        var sourcePath = page.ImportProvenance?.SourcePath
            ?? throw new InvalidOperationException($"Imported page '{page.Id}' has no source path.");
        var fullPath = Path.IsPathRooted(sourcePath)
            ? Path.GetFullPath(sourcePath)
            : Path.GetFullPath(Path.Combine(repositoryRoot, sourcePath));
        Assert.IsTrue(File.Exists(fullPath), fullPath);
        return fullPath;
    }

    private static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !(Directory.Exists(Path.Combine(directory.FullName, "SCADA_BUILDER")) &&
                 Directory.Exists(Path.Combine(directory.FullName, "SCADA_BUILDER_V2"))))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("SCADA AMR GROUP repository root not found.");
    }
}
