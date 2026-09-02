using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Rendering;
using ScadaBuilderV2.Rendering.QuickWindows;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Generates and locks the quick-window soak package: the endurance workload driven for at least
/// 24 hours on the canary, produced only by the protected internal harness.
/// </summary>
/// <remarks>
/// This package exists because the handshake fixture is too small to be an endurance workload. It carries
/// six definitions and fourteen invocations instead of two and four, exercises every data type and every
/// public interface family, and spreads its bindings over several protocols so the Redis feeder drives more
/// than one snapshot key. What it does not do is change the handshake fixture: that artifact stays frozen,
/// byte for byte, and keeps its own hash and its own cross-repository vendoring.
///
/// Like the handshake package this one is deliberately non shippable. Quick-window capabilities are all
/// `Blocked`, so no product export path can emit it: it is assembled here from <c>QuickWindowCompiler</c>
/// and the shared runtime bundle, and its `RuntimeContract` declares no quick-window capability.
///
/// Bindings use the `tf100.mapping.&lt;id&gt;` form, which the soak feeder resolves against `RegisterMapping`
/// to reach the `protocol_id` and `keyword` behind each Redis snapshot key. The ids live in the 5000 range
/// so a soak deployment can never be confused with the handshake fixture's 200-400 range.
///
/// Regenerate with `SCADA_UPDATE_QUICK_WINDOW_SOAK=1` after a deliberate review of the manifest, compiler
/// or runtime change that caused the drift.
///
/// Decisions: DEC-0047, DEC-0050.
/// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §12.
/// Plan: Task 5.3, soak workload.
/// </remarks>
[TestClass]
public sealed class QuickWindowSoakPackageFixtureTests
{
    private const string FixtureName = "quick-window-soak.sb2";
    private const string HashName = "quick-window-soak.sha256";

    private static readonly Guid PumpDefinition = Guid.Parse("50000001-1111-4111-8111-a00000000001");
    private static readonly Guid MotorDefinition = Guid.Parse("50000002-1111-4111-8111-a00000000002");
    private static readonly Guid ValveDefinition = Guid.Parse("50000003-1111-4111-8111-a00000000003");
    private static readonly Guid SensorDefinition = Guid.Parse("50000004-1111-4111-8111-a00000000004");
    private static readonly Guid AlarmDefinition = Guid.Parse("50000005-1111-4111-8111-a00000000005");
    private static readonly Guid TrendDefinition = Guid.Parse("50000006-1111-4111-8111-a00000000006");

    // Pump: every family and every data type that a public interface may carry.
    private static readonly Guid PumpRunning = Guid.Parse("51000001-2222-4222-8222-b00000000001");
    private static readonly Guid PumpStart = Guid.Parse("51000002-2222-4222-8222-b00000000002");
    private static readonly Guid PumpSetpoint = Guid.Parse("51000003-2222-4222-8222-b00000000003");
    private static readonly Guid PumpFlow = Guid.Parse("51000004-2222-4222-8222-b00000000004");
    private static readonly Guid PumpLabel = Guid.Parse("51000005-2222-4222-8222-b00000000005");
    private static readonly Guid PumpMode = Guid.Parse("51000006-2222-4222-8222-b00000000006");

    private static readonly Guid MotorRunning = Guid.Parse("52000001-2222-4222-8222-b00000000011");
    private static readonly Guid MotorStart = Guid.Parse("52000002-2222-4222-8222-b00000000012");
    private static readonly Guid MotorSpeed = Guid.Parse("52000003-2222-4222-8222-b00000000013");

    private static readonly Guid ValveOpen = Guid.Parse("53000001-2222-4222-8222-b00000000021");
    private static readonly Guid ValvePosition = Guid.Parse("53000002-2222-4222-8222-b00000000022");

    private static readonly Guid SensorValue = Guid.Parse("54000001-2222-4222-8222-b00000000031");
    private static readonly Guid SensorUnit = Guid.Parse("54000002-2222-4222-8222-b00000000032");

    private static readonly Guid AlarmActive = Guid.Parse("55000001-2222-4222-8222-b00000000041");
    private static readonly Guid AlarmCount = Guid.Parse("55000002-2222-4222-8222-b00000000042");
    private static readonly Guid AlarmAcknowledge = Guid.Parse("55000003-2222-4222-8222-b00000000043");

    private static readonly Guid TrendValue = Guid.Parse("56000001-2222-4222-8222-b00000000051");

    private static readonly Guid PageKey = Guid.Parse("5f000000-3333-4333-8333-c00000000001");

    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    [TestMethod]
    public void TheSoakPackageIsGeneratedOnlyByTheProtectedHarnessAndStaysLocked()
    {
        var repositoryRoot = FindRepositoryRoot();
        var fixturePath = Path.Combine(repositoryRoot, "tests", "conformance", FixtureName);
        var hashPath = Path.Combine(repositoryRoot, "tests", "conformance", HashName);

        byte[] firstBytes;
        using (var staging = new TemporaryDirectory())
        {
            firstBytes = BuildPackage(staging.Path);
        }

        // Determinism is the property that makes a locked hash meaningful: two builds of the same
        // sources must produce the same archive, or the lock would only be recording build noise.
        byte[] secondBytes;
        using (var staging = new TemporaryDirectory())
        {
            secondBytes = BuildPackage(staging.Path);
        }

        CollectionAssert.AreEqual(firstBytes, secondBytes, "the soak package must be byte-for-byte deterministic");

        var sha256 = Convert.ToHexString(SHA256.HashData(firstBytes)).ToLowerInvariant();

        if (!File.Exists(fixturePath) || !File.Exists(hashPath))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("SCADA_UPDATE_QUICK_WINDOW_SOAK"), "1", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
                File.WriteAllBytes(fixturePath, firstBytes);
                File.WriteAllText(hashPath, $"{sha256}  {FixtureName}\n", new UTF8Encoding(false));
            }
            else
            {
                Assert.Fail($"{FixtureName} is missing; regenerate with SCADA_UPDATE_QUICK_WINDOW_SOAK=1 after review.");
            }
        }

        var recordedHash = File.ReadAllText(hashPath).Split(' ')[0].Trim();
        if (!string.Equals(recordedHash, sha256, StringComparison.Ordinal))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("SCADA_UPDATE_QUICK_WINDOW_SOAK"), "1", StringComparison.Ordinal))
            {
                File.WriteAllBytes(fixturePath, firstBytes);
                File.WriteAllText(hashPath, $"{sha256}  {FixtureName}\n", new UTF8Encoding(false));
            }
            else
            {
                Assert.Fail(
                    $"the soak package drifted from its locked hash ({recordedHash} -> {sha256}); "
                    + "regenerate with SCADA_UPDATE_QUICK_WINDOW_SOAK=1 after review.");
            }
        }

        CollectionAssert.AreEqual(
            File.ReadAllBytes(fixturePath),
            firstBytes,
            "the committed soak package must be the artifact this harness produces");
    }

    [TestMethod]
    public void TheSoakPackageDeclaresNoQuickWindowCapability()
    {
        using var staging = new TemporaryDirectory();
        BuildPackage(staging.Path);

        var manifestPath = Path.Combine(
            staging.Path,
            Ft100SceneExporter.ProjectPackageDirectoryName,
            "manifest.json");
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));

        var required = document.RootElement
            .GetProperty("RuntimeContract")
            .GetProperty("RequiredCapabilities")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();

        Assert.IsFalse(
            required.Any(capability => capability.StartsWith("quick-window", StringComparison.OrdinalIgnoreCase)),
            "no quick-window capability may be declared while every one of them is Blocked");
    }

    [TestMethod]
    public void TheSoakPackageIsAnEnduranceWorkloadNotAHandshake()
    {
        // The whole reason this package exists is breadth. If it ever shrinks back to handshake size,
        // the soak stops proving anything the handshake fixture did not already prove.
        var project = Project();

        Assert.IsTrue(project.QuickWindows.Count >= 6, "the soak workload needs at least six definitions");
        Assert.IsTrue(project.QuickWindowInvocations.Count >= 14, "the soak workload needs at least fourteen invocations");

        var families = project.QuickWindows
            .SelectMany(definition => definition.InterfaceMembers)
            .Select(member => member.Family)
            .Distinct()
            .ToArray();
        CollectionAssert.Contains(families, QuickWindowInterfaceFamily.ReadState);
        CollectionAssert.Contains(families, QuickWindowInterfaceFamily.WriteCommand);
        CollectionAssert.Contains(families, QuickWindowInterfaceFamily.PublicParameter);

        var dataTypes = project.QuickWindows
            .SelectMany(definition => definition.InterfaceMembers)
            .Select(member => member.DataType)
            .Distinct()
            .ToArray();
        Assert.IsTrue(dataTypes.Length >= 4, "the soak workload must exercise more than one data type");

        // Several distinct mapping ids, so the feeder drives several Redis snapshot keys rather than one.
        var tagIds = project.QuickWindowInvocations
            .SelectMany(invocation => invocation.Bindings)
            .Select(binding => binding.TagId)
            .Where(tagId => !string.IsNullOrEmpty(tagId))
            .Distinct()
            .ToArray();
        Assert.IsTrue(tagIds.Length >= 20, "the soak workload must spread over at least twenty mappings");
    }

    [TestMethod]
    public void NoMappingIsBoundToTwoDifferentDataTypes()
    {
        // A RegisterMapping carries exactly one datatype. If two invocations bind the same mapping to
        // members of different types, the canary cannot seed a row that satisfies both, and whichever
        // type is written last silently wins. Caught here rather than downstream in the seed.
        var project = Project();
        var memberTypes = project.QuickWindows
            .SelectMany(definition => definition.InterfaceMembers)
            .ToDictionary(member => member.MemberKey, member => member.DataType);

        var typesByTag = new Dictionary<string, QuickWindowDataType>(StringComparer.Ordinal);
        foreach (var invocation in project.QuickWindowInvocations)
        {
            foreach (var binding in invocation.Bindings)
            {
                if (string.IsNullOrEmpty(binding.TagId)) continue;
                if (!memberTypes.TryGetValue(binding.MemberKey, out var dataType)) continue;

                if (typesByTag.TryGetValue(binding.TagId, out var already))
                {
                    Assert.AreEqual(
                        already,
                        dataType,
                        $"{binding.TagId} is bound to both {already} and {dataType}; one mapping carries one type");
                }
                else
                {
                    typesByTag[binding.TagId] = dataType;
                }
            }
        }

        Assert.IsTrue(typesByTag.Count > 0, "the workload must bind at least one tag");
    }

    [TestMethod]
    public void TheSoakPackageDoesNotDisturbTheFrozenHandshakeFixture()
    {
        // The frozen Phase 0 chain depends on the handshake artifact staying exactly as it is. Adding a
        // soak package must never be a reason to regenerate it, so this test states the boundary.
        var repositoryRoot = FindRepositoryRoot();
        var handshake = Path.Combine(repositoryRoot, "tests", "conformance", "quick-window-runtime-handshake.sb2");
        var handshakeHash = Path.Combine(repositoryRoot, "tests", "conformance", "quick-window-runtime-handshake.sha256");

        Assert.IsTrue(File.Exists(handshake), "the handshake fixture must still exist");

        var recorded = File.ReadAllText(handshakeHash).Split(' ')[0].Trim();
        var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(handshake))).ToLowerInvariant();
        Assert.AreEqual(recorded, actual, "the handshake fixture must stay byte-for-byte frozen");

        Assert.AreNotEqual(
            FixtureName,
            "quick-window-runtime-handshake.sb2",
            "the soak package must be a separate artifact");
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

        // The soak package carries two caller pages, not one: the driver alternates between them so that
        // navigation invalidation is exercised on every lap instead of only at start and end.
        WriteCallerPage(packageDirectory, "win00054");
        WriteCallerPage(packageDirectory, "win00055");

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

    private static void WriteCallerPage(string packageDirectory, string pageId)
    {
        var pageDirectory = Path.Combine(packageDirectory, pageId);
        Directory.CreateDirectory(Path.Combine(pageDirectory, "css"));
        File.WriteAllText(
            Path.Combine(pageDirectory, $"{pageId}.html"),
            $"""
            <!doctype html>
            <html lang="fr">
            <head>
              <meta charset="utf-8">
              <title>{pageId}</title>
              <link rel="stylesheet" href="css/{pageId}.css">
            </head>
            <body style="margin:0;padding:0;">
              <div id="ft100-{pageId}" class="ft100-scada-scene" data-scada-page-id="{pageId}" data-scada-page-type="Default" data-scada-width="1920" data-scada-height="1080"></div>
            </body>
            </html>

            """,
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(pageDirectory, "css", $"{pageId}.css"),
            $"#ft100-{pageId}.ft100-scada-scene {{ position: relative; width: 1920px; height: 1080px; }}\n",
            new UTF8Encoding(false));
    }

    private static string BuildManifest(Ft100QuickWindowCompilation compilation, string runtimeSha256)
    {
        var manifest = new
        {
            Name = "QuickWindowSoak",
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
                },
                new
                {
                    Id = "win00055",
                    Name = "win00055",
                    Type = "Default",
                    IncludeInBuild = true,
                    IsHome = false,
                    RelativePath = "win00055/win00055.html",
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
        var pump = new QuickWindowDefinition(
            PumpDefinition,
            "pompe",
            "Pompe",
            1,
            new VisualContent(
                new CanvasSize(520, 380),
                Elements:
                [
                    ScadaElement.CreateText("titre", "Titre", 12, 12),
                    ScadaElement.CreateText("etat", "Etat", 12, 48),
                    ScadaElement.CreateInputNumeric("consigne", "Consigne", 12, 90),
                    ScadaElement.CreateInputNumeric("debit", "Debit", 12, 140, isReadOnly: true),
                    ScadaElement.CreateShape("voyant", "Voyant", ScadaShapeKind.Rectangle, 320, 48),
                    ScadaElement.CreateButton("demarrer", "Demarrer", 12, 300)
                ]),
            [
                new QuickWindowInterfaceMember(PumpRunning, "Running", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read, Required: true),
                new QuickWindowInterfaceMember(PumpStart, "Start", QuickWindowInterfaceFamily.WriteCommand, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Write),
                new QuickWindowInterfaceMember(PumpSetpoint, "Setpoint", QuickWindowInterfaceFamily.WriteCommand, QuickWindowDataType.Decimal, QuickWindowMemberAccess.Write),
                new QuickWindowInterfaceMember(PumpFlow, "Flow", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Decimal, QuickWindowMemberAccess.Read, Required: true),
                new QuickWindowInterfaceMember(PumpLabel, "Label", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.String, QuickWindowMemberAccess.Read),
                new QuickWindowInterfaceMember(PumpMode, "Mode", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Integer, QuickWindowMemberAccess.Read)
            ],
            new QuickWindowPresentationDefaults());

        var motor = new QuickWindowDefinition(
            MotorDefinition,
            "moteur",
            "Moteur",
            1,
            new VisualContent(
                new CanvasSize(480, 320),
                Elements:
                [
                    ScadaElement.CreateText("titre", "Titre", 10, 10),
                    ScadaElement.CreateInputNumeric("vitesse", "Vitesse", 10, 60, isReadOnly: true),
                    ScadaElement.CreateButton("demarrer", "Demarrer", 10, 250)
                ]),
            [
                new QuickWindowInterfaceMember(MotorRunning, "Running", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read, Required: true),
                new QuickWindowInterfaceMember(MotorStart, "Start", QuickWindowInterfaceFamily.WriteCommand, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Write),
                new QuickWindowInterfaceMember(MotorSpeed, "Speed", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Integer, QuickWindowMemberAccess.Read)
            ],
            new QuickWindowPresentationDefaults());

        var valve = new QuickWindowDefinition(
            ValveDefinition,
            "vanne",
            "Vanne",
            1,
            new VisualContent(
                new CanvasSize(420, 300),
                Elements:
                [
                    ScadaElement.CreateText("titre", "Titre", 8, 8),
                    ScadaElement.CreateInputNumeric("position", "Position", 8, 60, isReadOnly: true),
                    ScadaElement.CreateShape("corps", "Corps", ScadaShapeKind.Rectangle, 220, 60)
                ]),
            [
                new QuickWindowInterfaceMember(ValveOpen, "Open", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read, Required: true),
                new QuickWindowInterfaceMember(ValvePosition, "Position", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Decimal, QuickWindowMemberAccess.Read)
            ],
            new QuickWindowPresentationDefaults());

        var sensor = new QuickWindowDefinition(
            SensorDefinition,
            "capteur",
            "Capteur",
            1,
            new VisualContent(
                new CanvasSize(360, 240),
                Elements:
                [
                    ScadaElement.CreateText("titre", "Titre", 5, 5),
                    ScadaElement.CreateInputNumeric("valeur", "Valeur", 5, 50, isReadOnly: true)
                ]),
            [
                new QuickWindowInterfaceMember(SensorValue, "Value", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Decimal, QuickWindowMemberAccess.Read, Required: true),
                new QuickWindowInterfaceMember(SensorUnit, "Unit", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.String, QuickWindowMemberAccess.Read)
            ],
            new QuickWindowPresentationDefaults());

        var alarm = new QuickWindowDefinition(
            AlarmDefinition,
            "alarme",
            "Alarme",
            1,
            new VisualContent(
                new CanvasSize(560, 400),
                Elements:
                [
                    ScadaElement.CreateText("titre", "Titre", 10, 10),
                    ScadaElement.CreateText("message", "Message", 10, 50),
                    ScadaElement.CreateInputNumeric("compte", "Compte", 10, 100, isReadOnly: true),
                    ScadaElement.CreateButton("acquitter", "Acquitter", 10, 330)
                ]),
            [
                new QuickWindowInterfaceMember(AlarmActive, "Active", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read, Required: true),
                new QuickWindowInterfaceMember(AlarmCount, "Count", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Integer, QuickWindowMemberAccess.Read),
                new QuickWindowInterfaceMember(AlarmAcknowledge, "Acknowledge", QuickWindowInterfaceFamily.WriteCommand, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Write)
            ],
            new QuickWindowPresentationDefaults());

        var trend = new QuickWindowDefinition(
            TrendDefinition,
            "tendance",
            "Tendance",
            1,
            new VisualContent(
                new CanvasSize(640, 420),
                Elements:
                [
                    ScadaElement.CreateText("titre", "Titre", 10, 10),
                    ScadaElement.CreateInputNumeric("courant", "Courant", 10, 60, isReadOnly: true)
                ]),
            [
                new QuickWindowInterfaceMember(TrendValue, "Value", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Decimal, QuickWindowMemberAccess.Read, Required: true)
            ],
            new QuickWindowPresentationDefaults());

        return ScadaProject.CreateDefault("QuickWindowSoak") with
        {
            ManifestVersion = "2.3",
            QuickWindows = [pump, motor, valve, sensor, alarm, trend],
            QuickWindowInvocations =
            [
                // Three invocations of the same pump definition, each with its own mappings. Any write that
                // reaches a sibling's mapping during the soak is the leak this arrangement is here to catch.
                Invocation("60000001", PumpDefinition, "pompe-1",
                    (PumpRunning, 5010), (PumpStart, 5011), (PumpSetpoint, 5012), (PumpFlow, 5013), (PumpMode, 5014)),
                Invocation("60000002", PumpDefinition, "pompe-2",
                    (PumpRunning, 5020), (PumpStart, 5021), (PumpSetpoint, 5022), (PumpFlow, 5023), (PumpMode, 5024)),
                Invocation("60000003", PumpDefinition, "pompe-3",
                    (PumpRunning, 5030), (PumpStart, 5031), (PumpSetpoint, 5032), (PumpFlow, 5033), (PumpMode, 5034)),

                Invocation("60000004", MotorDefinition, "moteur-1",
                    (MotorRunning, 5110), (MotorStart, 5111), (MotorSpeed, 5112)),
                Invocation("60000005", MotorDefinition, "moteur-2",
                    (MotorRunning, 5120), (MotorStart, 5121), (MotorSpeed, 5122)),
                Invocation("60000006", MotorDefinition, "moteur-3",
                    (MotorRunning, 5130), (MotorStart, 5131), (MotorSpeed, 5132)),

                Invocation("60000007", ValveDefinition, "vanne-1",
                    (ValveOpen, 5210), (ValvePosition, 5211)),
                Invocation("60000008", ValveDefinition, "vanne-2",
                    (ValveOpen, 5220), (ValvePosition, 5221)),

                Invocation("60000009", SensorDefinition, "capteur-1",
                    (SensorValue, 5310)),
                Invocation("6000000a", SensorDefinition, "capteur-2",
                    (SensorValue, 5320)),

                Invocation("6000000b", AlarmDefinition, "alarme-1",
                    (AlarmActive, 5410), (AlarmCount, 5411), (AlarmAcknowledge, 5412)),
                Invocation("6000000c", AlarmDefinition, "alarme-2",
                    (AlarmActive, 5420), (AlarmCount, 5421), (AlarmAcknowledge, 5422)),

                // Reached from a pump window, so Page -> A -> B stays exercised for 24 hours and a third
                // level stays refused for just as long.
                Invocation("6000000d", TrendDefinition, "tendance-1",
                    (TrendValue, 5510)),

                // Required port deliberately left unbound: every lap must refuse this open and subscribe
                // nothing. A slow leak on the refusal path is exactly what a short test would miss.
                //
                // The bound member here gets its own mapping id rather than borrowing capteur-2's. A
                // mapping carries exactly one datatype in production, and 5320 is already the sensor's
                // Decimal value: binding a String member to it would have asked the canary to seed one
                // row as two types at once.
                Invocation("6000000e", SensorDefinition, "capteur-non-lie",
                    (SensorUnit, 5321))
            ]
        };
    }

    private static QuickWindowInvocation Invocation(
        string keyPrefix,
        Guid definitionKey,
        string callerElementId,
        params (Guid Member, int MappingId)[] bindings)
    {
        return new QuickWindowInvocation(
            Guid.Parse($"{keyPrefix}-4444-4444-8444-d00000000001"),
            definitionKey,
            bindings
                .Select(binding => QuickWindowBinding.FromTag(binding.Member, $"tf100.mapping.{binding.MappingId}"))
                .ToArray(),
            InterfaceVersion: 1,
            OwnerPageKey: PageKey,
            OwnerElementId: callerElementId,
            OwnerCommandId: "open");
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

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"qw-soak-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A leftover scratch directory is not worth failing a generation over.
            }
        }
    }
}
