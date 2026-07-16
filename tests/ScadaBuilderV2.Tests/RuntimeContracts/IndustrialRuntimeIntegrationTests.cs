using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

            ValidateWin00003(pages["win00003"]);
            ValidateWin00004(archive, pages["win00004"]);
            ValidateWin00008(pages["win00008"]);
            ValidateWin00012(pages["win00012_modern_no_legacy"]);

            var runtimeContract = manifest.RootElement.GetProperty("RuntimeContract");
            var runtimeSha = runtimeContract.GetProperty("RuntimeSha256").GetString()!;
            var runtimeEntry = archive.Entries.Single(entry =>
                entry.FullName == $"{Ft100SceneExporter.ProjectPackageDirectoryName}/scada-runtime.{runtimeSha[..8]}.js");
            using (var runtimeStream = runtimeEntry.Open())
            {
                Assert.AreEqual(runtimeSha, Convert.ToHexString(SHA256.HashData(runtimeStream)).ToLowerInvariant());
            }

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
                    ["win00003"] = new { Navigations = 8, LatestWinsBackForward = "covered-by-tf100web-lifecycle-suite" },
                    ["win00004"] = new { Header = "win00002", Footer = "win00003", AssetsValidated = true },
                    ["win00008"] = new { States = 8, ReadOnlyNumerics = 2, WritableNumerics = 1, RoundTrip = "covered-by-tf100web-lifecycle-suite" },
                    ["win00012_modern_no_legacy"] = new { Buttons = 56, TableCells = 126, ExpectedMissingMapping = 615 }
                },
                Diagnostics = new[] { "mapping 615 expected quality fallback; no fabricated mapping", "no PLC write executed during automated acceptance" }
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
                    Assert.AreEqual(
                        expected.RootElement.GetProperty(property).GetRawText(),
                        actual.RootElement.GetProperty(property).GetRawText(),
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

    private static void ValidateWin00003(JsonElement page)
    {
        var commands = Objects(page)
            .Where(element => element.TryGetProperty("CommandConfig", out var config) && config.ValueKind == JsonValueKind.Object)
            .SelectMany(element => element.GetProperty("CommandConfig").GetProperty("Commands").EnumerateArray())
            .ToArray();
        Assert.AreEqual(8, commands.Length);
        Assert.IsTrue(commands.All(command => command.GetProperty("Kind").GetString() == "navigate"));
        Assert.AreEqual(8, commands.Select(command => command.GetProperty("TargetPageId").GetString()).Distinct().Count());
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
        var table = objects.Single(element => element.GetProperty("Id").GetString() == "table_defrost_upper");
        Assert.AreEqual(126, table.GetProperty("TableCellBindings").GetArrayLength());

        foreach (var button in buttons)
        {
            var command = button.GetProperty("CommandConfig").GetProperty("Commands")[0];
            Assert.AreEqual("writeTag", command.GetProperty("Kind").GetString());
            Assert.AreEqual(command.GetProperty("WriteTagId").GetString(), command.GetProperty("ReadTagId").GetString());
        }

        var missing = buttons.Single(element => element.GetProperty("Id").GetString() == "toggle_defrost_p4_e12");
        var missingCommand = missing.GetProperty("CommandConfig").GetProperty("Commands")[0];
        Assert.AreEqual("tf100.mapping.615", missingCommand.GetProperty("WriteTagId").GetString());
        var fallback = missing.GetProperty("StateConfig").GetProperty("QualityFallback");
        Assert.AreEqual(0.4, fallback.GetProperty("Opacity").GetDouble(), 0.0001);
    }

    private static JsonElement[] Objects(JsonElement page) => page.GetProperty("Objects").EnumerateArray().ToArray();

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
