using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

[TestClass]
public sealed class QuickWindowContractHandshakeTests
{
    private static readonly JsonSerializerOptions RecordOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Guid DefinitionKey = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid LocalCountKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MotorNameKey = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RunFeedbackKey = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid StartCommandKey = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ThresholdKey = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ScadaBuilderV2.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("ScadaBuilderV2.sln not found.");
    }

    private static QuickWindowDefinition CreateDefinition()
    {
        var members = new[]
        {
            new QuickWindowInterfaceMember(LocalCountKey, "LocalCount", QuickWindowInterfaceFamily.PrivateVariable, QuickWindowDataType.Integer, QuickWindowMemberAccess.Internal),
            new QuickWindowInterfaceMember(MotorNameKey, "MotorName", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.String, QuickWindowMemberAccess.Read),
            new QuickWindowInterfaceMember(RunFeedbackKey, "RunFeedback", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read),
            new QuickWindowInterfaceMember(StartCommandKey, "StartCommand", QuickWindowInterfaceFamily.WriteCommand, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Write, Required: true),
            new QuickWindowInterfaceMember(ThresholdKey, "Threshold", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.Integer, QuickWindowMemberAccess.Read)
        };
        return new QuickWindowDefinition(
            DefinitionKey,
            "qw_motor",
            "Moteur Faceplate",
            1,
            new VisualContent(new CanvasSize(400, 300), SceneBackgroundStyle.Default, Array.Empty<ScadaElement>(), Array.Empty<string>(), Array.Empty<string>()),
            members,
            new QuickWindowPresentationDefaults());
    }

    private static QuickWindowInvocation CreateInvocation(Guid key, string suffix, int readTag, int writeTag)
    {
        var bindings = new[]
        {
            QuickWindowBinding.FromLiteral(MotorNameKey, $"Moteur {suffix}"),
            QuickWindowBinding.FromTag(RunFeedbackKey, $"tf100.mapping.{readTag}"),
            QuickWindowBinding.FromTag(StartCommandKey, $"tf100.mapping.{writeTag}"),
            QuickWindowBinding.Absent(ThresholdKey)
        }.OrderBy(binding => binding.MemberKey).ToArray();
        return new QuickWindowInvocation(
            key,
            DefinitionKey,
            bindings,
            TitleOverride: suffix,
            InterfaceVersion: 1,
            OwnerPageKey: Guid.Parse("99999999-9999-4999-8999-999999999999"),
            OwnerElementId: $"open-{suffix.ToLowerInvariant()}",
            OwnerCommandId: $"cmd-{suffix.ToLowerInvariant()}");
    }

    private static ScadaTagCatalog CreateCatalog() => new("tf100web-scada-tags-v1", new[]
    {
        new ScadaTagDefinition("tf100.mapping.210", "RunFeedback M101", Datatype: "Bool", Writeable: false),
        new ScadaTagDefinition("tf100.mapping.211", "StartCommand M101", Datatype: "Bool", Writeable: true),
        new ScadaTagDefinition("tf100.mapping.310", "RunFeedback M102", Datatype: "Bool", Writeable: false),
        new ScadaTagDefinition("tf100.mapping.311", "StartCommand M102", Datatype: "Bool", Writeable: true)
    });

    private static (JsonObject Manifest, JsonObject Expectations, string Sha) GenerateHandshake()
    {
        var root = FindRepoRoot();
        var prototypeHash = File.ReadAllText(Path.Combine(root, "tools", "prototypes", "quick-window-dom-css-isolation", "prototype.sha256")).Trim();
        var definition = CreateDefinition() with { InterfaceMembers = CreateDefinition().InterfaceMembers.OrderBy(member => member.Name, StringComparer.Ordinal).ToArray() };
        var invocations = new[]
        {
            CreateInvocation(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "M101", 210, 211),
            CreateInvocation(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "M102", 310, 311)
        }.OrderBy(invocation => invocation.InvocationKey).ToArray();

        var manifest = new JsonObject
        {
            ["schema"] = "scada-builder-v2-quick-window-contract-handshake-v1",
            ["manifestVersion"] = "2.3",
            ["contractVersion"] = "1.0",
            ["prototypeRevision"] = "1.0.2",
            ["prototypeHash"] = prototypeHash,
            ["project"] = "Handshake",
            ["generatedFrom"] = "ScadaBuilderV2.Domain.QuickWindows records",
            ["quickWindows"] = new JsonArray(JsonSerializer.SerializeToNode(definition, RecordOptions)),
            ["quickWindowInvocations"] = new JsonArray(invocations.Select(invocation => JsonSerializer.SerializeToNode(invocation, RecordOptions)).ToArray()),
            ["capabilities"] = new JsonArray("command.close-quick-window", "command.open-quick-window"),
            ["profile"] = "2.3"
        };
        var canonicalPayload = manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload))).ToLowerInvariant();
        manifest["sha256"] = sha;

        var expectations = new JsonObject
        {
            ["schema"] = "scada-builder-v2-quick-window-contract-handshake-expectations-v1",
            ["generatedFrom"] = "manifest.json",
            ["sha256"] = sha,
            ["cases"] = new JsonArray(
                Case("valid-two-invocations", "accept", "validation"),
                Case("optional-absent-neutral", "accept", "neutral"),
                Case("required-missing", "reject", "required", "binding.required-missing"),
                Case("interface-version-mismatch", "reject", "interface-version", "invocation.interface-version-mismatch"),
                Case("retired-command-kind", "reject", "retired", "retired-popup-command"),
                Case("fourth-identifier-absent", "accept", "identity", "no-instance-key"),
                Case("injection-literal-rejected", "reject", "injection-rejected", "injection-rejected"),
                Case("type-mismatch", "reject", "validation", "binding.type-mismatch"),
                Case("profile-2.1-rejected", "reject", "capability", "profile.quick-window-unsupported"),
                Case("profile-2.2-rejected", "reject", "capability", "profile.quick-window-unsupported"),
                Case("profile-2.3-accepted", "accept", "capability", "profile.compatible"),
                Case("order-determinism", "accept", "determinism"),
                Case("duplicate-invocation-key", "reject", "validation", "quick-window.duplicate-invocation")),
            ["mutationMatrix"] = new JsonArray("keys", "types", "order", "InvocationKey", "InterfaceVersion", "capability", "manifest profile")
        };
        return (manifest, expectations, sha);
    }

    private static JsonObject Case(string id, string expected, string category, string? code = null)
    {
        var value = new JsonObject { ["id"] = id, ["expected"] = expected, ["category"] = category };
        if (code is not null)
            value["code"] = code;
        return value;
    }

    private static string Formatted(JsonNode node) => node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;

    private static (string ManifestPath, string ExpectationsPath) FixturePaths()
    {
        var directory = Path.Combine(FindRepoRoot(), "tests", "conformance", "quick-window-contract-handshake");
        return (Path.Combine(directory, "manifest.json"), Path.Combine(directory, "expectations.json"));
    }

    [TestMethod]
    public void CommittedHandshakeIsExactDeterministicOutputFromDomainRecords()
    {
        var generated = GenerateHandshake();
        var paths = FixturePaths();
        if (string.Equals(Environment.GetEnvironmentVariable("UPDATE_QUICK_WINDOW_HANDSHAKE"), "1", StringComparison.Ordinal))
        {
            File.WriteAllText(paths.ManifestPath, Formatted(generated.Manifest), new UTF8Encoding(false));
            File.WriteAllText(paths.ExpectationsPath, Formatted(generated.Expectations), new UTF8Encoding(false));
        }

        Assert.AreEqual(Formatted(generated.Manifest), File.ReadAllText(paths.ManifestPath), "Regenerate the handshake from domain records.");
        Assert.AreEqual(Formatted(generated.Expectations), File.ReadAllText(paths.ExpectationsPath), "Regenerate handshake expectations.");
        Assert.AreNotEqual(new string('0', 64), generated.Sha);
        Assert.AreNotEqual("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", generated.Sha);
    }

    [TestMethod]
    public void GeneratedRecordsValidateAndKeepInvocationsIsolated()
    {
        var generated = GenerateHandshake();
        var definitions = generated.Manifest["quickWindows"]!.AsArray();
        var invocations = generated.Manifest["quickWindowInvocations"]!.AsArray();
        var definition = JsonSerializer.Deserialize<QuickWindowDefinition>(definitions[0]!.ToJsonString(), RecordOptions)!;
        Assert.AreEqual(0, QuickWindowValidation.ValidateDefinition(definition).Count);
        Assert.AreEqual(2, invocations.Count);
        var parsedInvocations = invocations.Select(node => JsonSerializer.Deserialize<QuickWindowInvocation>(node!.ToJsonString(), RecordOptions)!).ToArray();
        Assert.IsTrue(parsedInvocations.SelectMany(invocation => QuickWindowBindingValidator.ValidateInvocation(invocation, definition, CreateCatalog())).All(result => result.IsValid));
        CollectionAssert.AreEqual(new[] { "tf100.mapping.210", "tf100.mapping.211" }, parsedInvocations[0].Bindings.Where(binding => binding.SourceKind == QuickWindowBindingSourceKind.Tag).Select(binding => binding.TagId).OrderBy(value => value).ToArray());
        CollectionAssert.AreEqual(new[] { "tf100.mapping.310", "tf100.mapping.311" }, parsedInvocations[1].Bindings.Where(binding => binding.SourceKind == QuickWindowBindingSourceKind.Tag).Select(binding => binding.TagId).OrderBy(value => value).ToArray());
        Assert.IsFalse(parsedInvocations.SelectMany(invocation => invocation.Bindings).Any(binding => binding.MemberKey == LocalCountKey), "Private members must never be invocation-bound.");
    }

    [TestMethod]
    public void MutationsAreRejectedWithStableCategories()
    {
        var definition = CreateDefinition();
        var catalog = CreateCatalog();
        var valid = CreateInvocation(Guid.NewGuid(), "M101", 210, 211);

        var wrongDefinition = valid with { DefinitionKey = Guid.NewGuid() };
        Assert.IsTrue(QuickWindowBindingValidator.ValidateInvocation(wrongDefinition, definition, catalog).Any(result => result.ErrorCode == "invocation.definition-mismatch"));

        var wrongVersion = valid with { InterfaceVersion = 2 };
        Assert.IsTrue(QuickWindowBindingValidator.ValidateInvocation(wrongVersion, definition, catalog).Any(result => result.Category == "interface-version"));

        var requiredMissing = QuickWindowBinding.Absent(StartCommandKey);
        var startMember = definition.InterfaceMembers.Single(member => member.MemberKey == StartCommandKey);
        Assert.AreEqual("binding.required-missing", QuickWindowBindingValidator.ValidateBinding(requiredMissing, startMember, catalog, 1).ErrorCode);

        var motorNameMember = definition.InterfaceMembers.Single(member => member.MemberKey == MotorNameKey);
        Assert.AreEqual("injection-rejected", QuickWindowBindingValidator.ValidateBinding(QuickWindowBinding.FromLiteral(MotorNameKey, "<script>"), motorNameMember, catalog, 1).Category);
        Assert.AreEqual("binding.type-mismatch", QuickWindowBindingValidator.ValidateBinding(QuickWindowBinding.FromTag(MotorNameKey, "tf100.mapping.210"), motorNameMember, catalog, 1).ErrorCode);

        foreach (var profile in new[] { "2.1", "2.2" })
        {
            var result = QuickWindowProfileCompatibility.Validate(profile, containsQuickWindows: true);
            Assert.IsFalse(result.IsCompatible);
            Assert.AreEqual("profile.quick-window-unsupported", result.Code);
        }
        Assert.IsTrue(QuickWindowProfileCompatibility.Validate("2.3", containsQuickWindows: true).IsCompatible);

        var duplicate = valid with { InvocationKey = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") };
        var project = ScadaProject.CreateDefault("Handshake") with
        {
            Scenes = new[] { new ScadaSceneReference("win00001", "Page", "scenes/win00001.scene.json", PageKey: Guid.NewGuid(), PageCode: "win00001") },
            QuickWindows = new[] { definition },
            QuickWindowInvocations = new[] { duplicate, duplicate }
        };
        Assert.IsTrue(ScadaProjectBuildValidator.Validate(project, Array.Empty<ScadaScene>()).Any(issue => issue.Code == "quick-window.duplicate-invocation"));
    }

    [TestMethod]
    public void HandshakeHasOnlyCanonicalIdentitiesAndOrdinalOrder()
    {
        var manifest = GenerateHandshake().Manifest;
        var text = manifest.ToJsonString();
        Assert.IsFalse(text.Contains("\"InstanceKey\"", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("\"RuntimeInstanceId\"", StringComparison.Ordinal));
        var members = manifest["quickWindows"]![0]!["InterfaceMembers"]!.AsArray().Select(item => item!["Name"]!.GetValue<string>()).ToArray();
        CollectionAssert.AreEqual(members.OrderBy(value => value, StringComparer.Ordinal).ToArray(), members);
        var capabilities = manifest["capabilities"]!.AsArray().Select(item => item!.GetValue<string>()).ToArray();
        CollectionAssert.AreEqual(capabilities.OrderBy(value => value, StringComparer.Ordinal).ToArray(), capabilities);
        var invocations = manifest["quickWindowInvocations"]!.AsArray().Select(item => item!["InvocationKey"]!.GetValue<string>()).ToArray();
        CollectionAssert.AreEqual(invocations.OrderBy(value => value, StringComparer.Ordinal).ToArray(), invocations);
    }
}
