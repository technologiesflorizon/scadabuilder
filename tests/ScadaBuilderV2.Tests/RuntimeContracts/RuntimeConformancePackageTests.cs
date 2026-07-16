using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ScadaBuilderV2.Application.RuntimeContracts;
using ScadaBuilderV2.Domain.RuntimeContracts;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.Tests.RuntimeContracts;

[TestClass]
public sealed class RuntimeConformancePackageTests
{
    private const string ArtifactName = "scada-v2-runtime-conformance.sb2";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [TestMethod]
    public void FactoryCoversEveryStrictSupportedCapabilityAndIndexesEveryBlockedCapability()
    {
        var model = ScadaV2RuntimeConformanceProjectFactory.Create();
        var analysis = ScadaRuntimeCapabilityAnalyzer.Analyze(
            model.Project,
            model.Pages.Select(page => page.Scene).ToArray());
        var actual = analysis.RequiredCapabilities.Select(capability => capability.Id).ToArray();
        var expected = ScadaRuntimeCapabilityCatalog.All
            .Where(capability => capability.Status == ScadaRuntimeCapabilityStatus.Supported)
            .Select(capability => capability.Id)
            .ToArray();

        CollectionAssert.AreEqual(expected, actual,
            "The conformance model must exercise every capability eligible for strict manifest 2.3 export.");
        Assert.AreEqual(0, analysis.BlockedCapabilities.Count);
    }

    [TestMethod]
    public async Task CommittedConformancePackageMatchesTwoDeterministicExports()
    {
        var repoRoot = FindRepositoryRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Conformance", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var firstPath = await ExportAsync(Path.Combine(tempRoot, "first"));
            var secondPath = await ExportAsync(Path.Combine(tempRoot, "second"));
            var firstBytes = await File.ReadAllBytesAsync(firstPath);
            var secondBytes = await File.ReadAllBytesAsync(secondPath);
            CollectionAssert.AreEqual(firstBytes, secondBytes,
                "The same conformance model must produce byte-identical .sb2 archives.");

            var packageSha256 = Sha256(firstBytes);
            ValidateArchive(firstPath, packageSha256);
            var expectedIndex = BuildExpectedIndex(packageSha256);

            var artifactDirectory = Path.Combine(repoRoot, "tests", "conformance", "artifacts");
            var committedArtifact = Path.Combine(artifactDirectory, ArtifactName);
            var committedSha = Path.Combine(artifactDirectory, "scada-v2-runtime-conformance.sha256");
            var committedIndex = Path.Combine(repoRoot, "tests", "conformance", "expected-runtime-capabilities.json");
            if (string.Equals(Environment.GetEnvironmentVariable("SCADA_UPDATE_CONFORMANCE"), "1", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(artifactDirectory);
                File.Copy(firstPath, committedArtifact, overwrite: true);
                await File.WriteAllTextAsync(committedSha, $"{packageSha256}  {ArtifactName}\n", new UTF8Encoding(false));
                Directory.CreateDirectory(Path.GetDirectoryName(committedIndex)!);
                await File.WriteAllTextAsync(committedIndex, expectedIndex, new UTF8Encoding(false));
            }

            Assert.IsTrue(File.Exists(committedArtifact), "Run with SCADA_UPDATE_CONFORMANCE=1 to create the committed fixture.");
            CollectionAssert.AreEqual(firstBytes, await File.ReadAllBytesAsync(committedArtifact));
            Assert.AreEqual($"{packageSha256}  {ArtifactName}\n", await File.ReadAllTextAsync(committedSha));
            Assert.AreEqual(expectedIndex, (await File.ReadAllTextAsync(committedIndex)).Replace("\r\n", "\n", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public void ExpectedIndexIsExhaustiveMachineReadableAndSanitized()
    {
        var indexPath = Path.Combine(FindRepositoryRoot(), "tests", "conformance", "expected-runtime-capabilities.json");
        using var document = JsonDocument.Parse(File.ReadAllText(indexPath));
        var root = document.RootElement;
        Assert.AreEqual("scada-v2-runtime-conformance-v1", root.GetProperty("Schema").GetString());
        var fixtures = root.GetProperty("Fixtures").EnumerateArray().ToArray();
        CollectionAssert.AreEqual(
            ScadaRuntimeCapabilityCatalog.All.Select(capability => capability.Id).ToArray(),
            fixtures.Select(fixture => fixture.GetProperty("CapabilityId").GetString()).ToArray());
        Assert.AreEqual(fixtures.Length, fixtures.Select(fixture => fixture.GetProperty("FixtureId").GetString()).Distinct().Count());

        var text = File.ReadAllText(indexPath);
        foreach (var forbidden in new[] { "AMR_REF", "win000", "Consigne_", "YL_", "KIC_", "C:\\Users", "F:\\Projet" })
        {
            Assert.IsFalse(text.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                $"Conformance fixtures must not include client/project token '{forbidden}'.");
        }
    }

    [TestMethod]
    public void EverySupportedFixtureHasModelRuntimeAndHostEvidenceBeforeItCanBeExported()
    {
        var model = ScadaV2RuntimeConformanceProjectFactory.Create();
        var analysis = ScadaRuntimeCapabilityAnalyzer.Analyze(
            model.Project,
            model.Pages.Select(page => page.Scene).ToArray());
        var required = analysis.RequiredCapabilities.ToDictionary(capability => capability.Id, StringComparer.Ordinal);
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "tests", "conformance", "expected-runtime-capabilities.json")));

        foreach (var fixture in index.RootElement.GetProperty("Fixtures").EnumerateArray())
        {
            var capabilityId = fixture.GetProperty("CapabilityId").GetString()!;
            var status = fixture.GetProperty("Status").GetString();
            if (status == nameof(ScadaRuntimeCapabilityStatus.Supported))
            {
                Assert.IsTrue(required.TryGetValue(capabilityId, out var capability), capabilityId);
                Assert.IsTrue(capability!.Evidence.BuilderTests.Count > 0, capabilityId);
                Assert.IsTrue(capability.Evidence.SharedRuntimeTests.Count > 0, capabilityId);
                Assert.IsTrue(capability.Evidence.Tf100WebTests.Count > 0, capabilityId);
                Assert.AreEqual("manifest-declared-and-runtime-initialized", fixture.GetProperty("ExpectedResult").GetString());
            }
            else
            {
                Assert.IsFalse(required.ContainsKey(capabilityId), capabilityId);
                Assert.AreEqual("strict-export-rejected", fixture.GetProperty("ExpectedResult").GetString());
            }
        }
    }

    private static async Task<string> ExportAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var model = ScadaV2RuntimeConformanceProjectFactory.Create();
        var archivePath = Path.Combine(directory, ArtifactName);
        var result = await new Ft100SceneExporter().ExportProjectArchiveAsync(model.Project, model.Pages, archivePath);
        Assert.IsTrue(result.Validation.IsValid, string.Join("; ", result.Validation.Errors.Select(error => error.Message)));
        return result.ArchivePath;
    }

    private static void ValidateArchive(string archivePath, string packageSha256)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entryNames = archive.Entries.Select(entry => entry.FullName).ToArray();
        CollectionAssert.AreEqual(entryNames.OrderBy(name => name, StringComparer.Ordinal).ToArray(), entryNames);
        Assert.IsTrue(entryNames.All(name => name.StartsWith($"{Ft100SceneExporter.ProjectPackageDirectoryName}/", StringComparison.Ordinal)));
        Assert.IsTrue(entryNames.Any(name => name.EndsWith(".html", StringComparison.Ordinal)));
        Assert.IsTrue(entryNames.Any(name => name.EndsWith(".css", StringComparison.Ordinal)));
        Assert.IsFalse(entryNames.Any(name => name.Contains("selection-overlay", StringComparison.OrdinalIgnoreCase)));

        var manifestEntry = archive.GetEntry($"{Ft100SceneExporter.ProjectPackageDirectoryName}/manifest.json")!;
        using var manifest = JsonDocument.Parse(ReadText(manifestEntry));
        var contract = manifest.RootElement.GetProperty("RuntimeContract");
        var required = contract.GetProperty("RequiredCapabilities").EnumerateArray().Select(item => item.GetString()).ToArray();
        CollectionAssert.AreEqual(
            ScadaRuntimeCapabilityCatalog.All
                .Where(capability => capability.Status == ScadaRuntimeCapabilityStatus.Supported)
                .Select(capability => capability.Id)
                .ToArray(),
            required);

        var runtimeEntry = archive.Entries.Single(entry => entry.FullName.Contains("/scada-runtime.", StringComparison.Ordinal));
        using var runtime = runtimeEntry.Open();
        var runtimeSha = Convert.ToHexString(SHA256.HashData(runtime)).ToLowerInvariant();
        Assert.AreEqual(runtimeSha, contract.GetProperty("RuntimeSha256").GetString());
        Assert.AreEqual($"scada-runtime.{runtimeSha[..8]}.js", Path.GetFileName(runtimeEntry.FullName));
        Assert.AreEqual(64, packageSha256.Length);

        foreach (var htmlEntry in archive.Entries.Where(entry => entry.FullName.EndsWith(".html", StringComparison.Ordinal)))
        {
            var html = ReadText(htmlEntry);
            Assert.IsFalse(html.Contains("selection-overlay", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(html.Contains("C:\\Users", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(html.Contains("F:\\Projet", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string BuildExpectedIndex(string packageSha256)
    {
        var fixtures = ScadaRuntimeCapabilityCatalog.All.Select(capability => new
        {
            CapabilityId = capability.Id,
            FixtureId = capability.FixtureId,
            Status = capability.Status.ToString(),
            PageId = capability.Status == ScadaRuntimeCapabilityStatus.Supported ? PageFor(capability.Id) : null,
            InitialValues = capability.Status == ScadaRuntimeCapabilityStatus.Supported
                ? new Dictionary<string, object> { ["conformance.tag.bool"] = false, ["conformance.tag.number"] = 12.5 }
                : null,
            Gesture = GestureFor(capability.Id),
            ExpectedResult = capability.Status == ScadaRuntimeCapabilityStatus.Supported
                ? "manifest-declared-and-runtime-initialized"
                : "strict-export-rejected",
            ExpectedDiagnostic = capability.Status == ScadaRuntimeCapabilityStatus.Blocked ? capability.Id : null
        }).ToArray();
        var index = new
        {
            Schema = "scada-v2-runtime-conformance-v1",
            ContractVersion = ScadaRuntimeCapabilityCatalog.ContractVersion,
            PackageFile = ArtifactName,
            PackageSha256 = packageSha256,
            Fixtures = fixtures
        };
        return JsonSerializer.Serialize(index, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static string PageFor(string capabilityId) => capabilityId switch
    {
        "page.header" => ScadaV2RuntimeConformanceProjectFactory.HeaderPageId,
        "page.footer" => ScadaV2RuntimeConformanceProjectFactory.FooterPageId,
        "page.fragment" => ScadaV2RuntimeConformanceProjectFactory.FragmentPageId,
        _ => ScadaV2RuntimeConformanceProjectFactory.MainPageId
    };

    private static string GestureFor(string capabilityId)
    {
        if (capabilityId.StartsWith("command.trigger.", StringComparison.Ordinal)) return "pointer-trigger";
        if (capabilityId.StartsWith("command.", StringComparison.Ordinal)) return "dispatch-command";
        if (capabilityId.StartsWith("action.", StringComparison.Ordinal)) return "click-action-source";
        if (capabilityId.StartsWith("binding.", StringComparison.Ordinal)) return "hydrate-edit-readback";
        if (capabilityId.StartsWith("state.", StringComparison.Ordinal) ||
            capabilityId.StartsWith("expression.", StringComparison.Ordinal) ||
            capabilityId.StartsWith("effect.", StringComparison.Ordinal)) return "hydrate-state";
        return "load-page";
    }

    private static string ReadText(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ScadaBuilderV2.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("SCADA Builder repository root not found.");
    }
}
