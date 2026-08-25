using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Rendering;
using ScadaBuilderV2.Rendering.QuickWindows;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Generates and locks the quick-window runtime handshake package: the first real Builder → package →
/// TF100Web round trip, produced only by the protected internal harness.
/// </summary>
/// <remarks>
/// The package is deliberately non shippable. Quick-window capabilities are `Blocked`, so no product export
/// path can emit it: it is assembled here from <c>QuickWindowCompiler</c> and the shared runtime bundle, and
/// its `RuntimeContract` declares no quick-window capability, exactly like a real strict 2.3 package would
/// be forced to today.
///
/// The same bytes and the same SHA-256 are vendored into TF100Web so both repositories execute the identical
/// artifact. Regenerate with `SCADA_UPDATE_QUICK_WINDOW_HANDSHAKE=1` after a deliberate review of the
/// manifest, compiler, runtime or parser change that caused the drift.
///
/// Decisions: DEC-0047, DEC-0050.
/// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §12.
/// Plan: Task 4.4.
/// </remarks>
[TestClass]
public sealed class QuickWindowRuntimeHandshakeFixtureTests
{
    private const string FixtureName = "quick-window-runtime-handshake.sb2";
    private const string HashName = "quick-window-runtime-handshake.sha256";

    private static readonly Guid DefinitionA = Guid.Parse("a1b2c3d4-1111-4222-8333-aaaaaaaaaaaa");
    private static readonly Guid DefinitionB = Guid.Parse("e5f6a7b8-2222-4333-8444-bbbbbbbbbbbb");
    private static readonly Guid RunningKey = Guid.Parse("11112222-3333-4444-5555-666677778888");
    private static readonly Guid StartKey = Guid.Parse("22223333-4444-5555-6666-777788889999");
    private static readonly Guid LabelKey = Guid.Parse("33334444-5555-6666-7777-88889999aaaa");
    private static readonly Guid ChildRunningKey = Guid.Parse("44445555-6666-7777-8888-9999aaaabbbb");
    private static readonly Guid InvocationM101 = Guid.Parse("aaaa1111-2222-3333-4444-555566667777");
    private static readonly Guid InvocationM102 = Guid.Parse("bbbb2222-3333-4444-5555-666677778888");
    private static readonly Guid InvocationChild = Guid.Parse("cccc3333-4444-5555-6666-777788889999");
    private static readonly Guid InvocationUnbound = Guid.Parse("dddd4444-5555-6666-7777-88889999aaaa");
    private static readonly Guid PageKey = Guid.Parse("eeee5555-6666-7777-8888-9999aaaabbbb");

    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    [TestMethod]
    public void TheHandshakePackageIsGeneratedOnlyByTheProtectedHarnessAndStaysLocked()
    {
        var repositoryRoot = FindRepositoryRoot();
        var conformanceDirectory = Path.Combine(repositoryRoot, "tests", "conformance");
        var fixturePath = Path.Combine(conformanceDirectory, FixtureName);
        var hashPath = Path.Combine(conformanceDirectory, HashName);

        var staging = Path.Combine(Path.GetTempPath(), "scada-qw-handshake", Guid.NewGuid().ToString("N"));
        try
        {
            var firstBytes = BuildPackage(Path.Combine(staging, "first"));
            var secondBytes = BuildPackage(Path.Combine(staging, "second"));
            CollectionAssert.AreEqual(firstBytes, secondBytes, "the harness must produce byte-identical packages");

            var sha256 = Convert.ToHexString(SHA256.HashData(firstBytes)).ToLowerInvariant();
            if (string.Equals(Environment.GetEnvironmentVariable("SCADA_UPDATE_QUICK_WINDOW_HANDSHAKE"), "1", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(conformanceDirectory);
                File.WriteAllBytes(fixturePath, firstBytes);
                File.WriteAllText(hashPath, $"{sha256}  {FixtureName}\n", new UTF8Encoding(false));
            }

            Assert.IsTrue(File.Exists(fixturePath), "Run with SCADA_UPDATE_QUICK_WINDOW_HANDSHAKE=1 to create the fixture.");
            CollectionAssert.AreEqual(firstBytes, File.ReadAllBytes(fixturePath), "the committed handshake package drifted");
            Assert.AreEqual(
                $"{sha256}  {FixtureName}\n",
                File.ReadAllText(hashPath).Replace("\r\n", "\n", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    [TestMethod]
    public void TheVendoredTf100WebFixtureIsTheSameArtifact()
    {
        var repositoryRoot = FindRepositoryRoot();
        var builderFixture = Path.Combine(repositoryRoot, "tests", "conformance", FixtureName);
        var vendored = Path.Combine("F:", "Projet", "Git", "TF100Web", "frontend", "test_fixtures", FixtureName);

        Assert.IsTrue(File.Exists(builderFixture), "the Builder fixture must exist");
        if (!File.Exists(vendored))
        {
            Assert.Inconclusive("TF100Web is not available on this machine; the vendored copy cannot be compared.");
            return;
        }

        CollectionAssert.AreEqual(
            File.ReadAllBytes(builderFixture),
            File.ReadAllBytes(vendored),
            "both repositories must execute the identical handshake artifact");
    }

    [TestMethod]
    public void TheHandshakePackageDeclaresNoQuickWindowCapability()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var archive = ZipFile.OpenRead(Path.Combine(repositoryRoot, "tests", "conformance", FixtureName));
        var manifestEntry = archive.GetEntry($"{Ft100SceneExporter.ProjectPackageDirectoryName}/manifest.json");
        Assert.IsNotNull(manifestEntry);

        using var reader = new StreamReader(manifestEntry!.Open());
        using var manifest = JsonDocument.Parse(reader.ReadToEnd());
        var root = manifest.RootElement;

        Assert.AreEqual("2.3", root.GetProperty("ManifestVersion").GetString());
        Assert.AreEqual(4, root.GetProperty("QuickWindowInvocations").GetArrayLength());
        Assert.AreEqual(2, root.GetProperty("QuickWindows").GetArrayLength());

        var required = root.GetProperty("RuntimeContract").GetProperty("RequiredCapabilities")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        Assert.IsFalse(
            required.Any(capability => capability.Contains("quick-window", StringComparison.Ordinal)),
            "no quick-window capability may be declared while every one of them is Blocked");
    }

    private static byte[] BuildPackage(string stagingRoot)
    {
        var packageDirectory = Path.Combine(stagingRoot, Ft100SceneExporter.ProjectPackageDirectoryName);
        Directory.CreateDirectory(packageDirectory);

        var project = Project();
        var compilation = QuickWindowCompiler.Compile(project);
        foreach (var file in compilation.Files)
        {
            var htmlPath = Path.Combine(packageDirectory, file.RelativeHtmlPath.Replace('/', Path.DirectorySeparatorChar));
            var cssPath = Path.Combine(packageDirectory, file.RelativeCssPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(htmlPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(cssPath)!);
            File.WriteAllText(htmlPath, file.Html, new UTF8Encoding(false));
            File.WriteAllText(cssPath, file.Css, new UTF8Encoding(false));
        }

        // The package also carries its caller page, so the TF100Web package validator sees a complete
        // artifact rather than a registry-only fixture.
        var pageDirectory = Path.Combine(packageDirectory, "win00054");
        Directory.CreateDirectory(Path.Combine(pageDirectory, "css"));
        File.WriteAllText(
            Path.Combine(pageDirectory, "win00054.html"),
            """
            <!doctype html>
            <html lang="fr">
            <head>
              <meta charset="utf-8">
              <title>win00054</title>
              <link rel="stylesheet" href="css/win00054.css">
            </head>
            <body style="margin:0;padding:0;">
              <div id="ft100-win00054" class="ft100-scada-scene" data-scada-page-id="win00054" data-scada-page-type="Default" data-scada-width="1920" data-scada-height="1080"></div>
            </body>
            </html>

            """,
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(pageDirectory, "css", "win00054.css"),
            "#ft100-win00054.ft100-scada-scene { position: relative; width: 1920px; height: 1080px; }\n",
            new UTF8Encoding(false));

        var runtimeScript = Ft100SceneExporter.GetRuntimeScript();
        var runtimeBytes = new UTF8Encoding(false).GetBytes(runtimeScript);
        var runtimeSha256 = Convert.ToHexString(SHA256.HashData(runtimeBytes)).ToLowerInvariant();
        File.WriteAllBytes(Path.Combine(packageDirectory, $"scada-runtime.{runtimeSha256[..8]}.js"), runtimeBytes);

        File.WriteAllText(
            Path.Combine(packageDirectory, "manifest.json"),
            BuildManifest(compilation, runtimeSha256),
            new UTF8Encoding(false));

        var archivePath = Path.Combine(stagingRoot, FixtureName);
        using (var output = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8))
        {
            foreach (var filePath in Directory.GetFiles(packageDirectory, "*", SearchOption.AllDirectories)
                         .OrderBy(path => Path.GetRelativePath(stagingRoot, path), StringComparer.Ordinal))
            {
                var entryName = Path.GetRelativePath(stagingRoot, filePath).Replace('\\', '/');
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var source = File.OpenRead(filePath);
                using var destination = entry.Open();
                source.CopyTo(destination);
            }
        }

        return File.ReadAllBytes(archivePath);
    }

    private static string BuildManifest(Ft100QuickWindowCompilation compilation, string runtimeSha256)
    {
        var manifest = new
        {
            Name = "QuickWindowRuntimeHandshake",
            ManifestVersion = "2.3",
            HomePageId = "win00054",
            Pages = new object[]
            {
                new
                {
                    Id = "win00054",
                    Name = "win00054",
                    Type = "Default",
                    IncludeInBuild = true,
                    IsHome = true,
                    RelativePath = "win00054/win00054.html",
                    Width = 1920,
                    Height = 1080
                }
            },
            QuickWindows = compilation.Definitions,
            QuickWindowInvocations = compilation.Invocations,
            RuntimeContract = new
            {
                Version = "1.0",
                RequiredCapabilities = Array.Empty<string>(),
                RuntimeSha256 = runtimeSha256
            }
        };

        return JsonSerializer.Serialize(manifest, ManifestOptions);
    }

    private static ScadaProject Project()
    {
        var motor = new QuickWindowDefinition(
            DefinitionA,
            "moteur",
            "Moteur",
            1,
            new VisualContent(
                new CanvasSize(480, 320),
                Elements: [ScadaElement.CreateText("sensor", "Sensor", 10, 20)]),
            [
                new QuickWindowInterfaceMember(RunningKey, "Running", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read, Required: true),
                new QuickWindowInterfaceMember(StartKey, "Start", QuickWindowInterfaceFamily.WriteCommand, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Write),
                new QuickWindowInterfaceMember(LabelKey, "Label", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.String, QuickWindowMemberAccess.Read)
            ],
            new QuickWindowPresentationDefaults());

        var child = new QuickWindowDefinition(
            DefinitionB,
            "detail",
            "Detail",
            1,
            new VisualContent(
                new CanvasSize(360, 240),
                Elements: [ScadaElement.CreateText("sensor", "Sensor", 5, 5)]),
            [
                new QuickWindowInterfaceMember(ChildRunningKey, "Running", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read, Required: true)
            ],
            new QuickWindowPresentationDefaults());

        return ScadaProject.CreateDefault("QuickWindowRuntimeHandshake") with
        {
            ManifestVersion = "2.3",
            QuickWindows = [motor, child],
            QuickWindowInvocations =
            [
                // M101 and M102 are two invocations of the same definition with independent mappings.
                new QuickWindowInvocation(
                    InvocationM101,
                    DefinitionA,
                    [
                        QuickWindowBinding.FromTag(RunningKey, "tf100.mapping.210"),
                        QuickWindowBinding.FromTag(StartKey, "tf100.mapping.211")
                    ],
                    InterfaceVersion: 1,
                    OwnerPageKey: PageKey,
                    OwnerElementId: "caller-m101",
                    OwnerCommandId: "open"),
                new QuickWindowInvocation(
                    InvocationM102,
                    DefinitionA,
                    [
                        QuickWindowBinding.FromTag(RunningKey, "tf100.mapping.310"),
                        QuickWindowBinding.FromTag(StartKey, "tf100.mapping.311")
                    ],
                    InterfaceVersion: 1,
                    OwnerPageKey: PageKey,
                    OwnerElementId: "caller-m102",
                    OwnerCommandId: "open"),
                // The child proves Page -> A -> B stays reachable and depth 3 stays refused.
                new QuickWindowInvocation(
                    InvocationChild,
                    DefinitionB,
                    [QuickWindowBinding.FromTag(ChildRunningKey, "tf100.mapping.410")],
                    InterfaceVersion: 1,
                    OwnerPageKey: PageKey,
                    OwnerElementId: "caller-child",
                    OwnerCommandId: "open"),
                // The unbound invocation proves a required port refuses the open with no subscription.
                new QuickWindowInvocation(
                    InvocationUnbound,
                    DefinitionA,
                    [QuickWindowBinding.FromTag(StartKey, "tf100.mapping.211")],
                    InterfaceVersion: 1,
                    OwnerPageKey: PageKey,
                    OwnerElementId: "caller-unbound",
                    OwnerCommandId: "open")
            ]
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ScadaBuilderV2.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        Assert.Fail("Unable to locate the repository root.");
        return string.Empty;
    }
}
