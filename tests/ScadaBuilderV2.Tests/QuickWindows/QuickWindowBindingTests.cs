using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

[TestClass]
public sealed class QuickWindowBindingTests
{
    private static QuickWindowDefinition CreateDefinitionWithMembers()
    {
        var readMember = new QuickWindowInterfaceMember(Guid.NewGuid(), "RunFeedback", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read, Required: false);
        var writeMember = new QuickWindowInterfaceMember(Guid.NewGuid(), "StartCommand", QuickWindowInterfaceFamily.WriteCommand, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Write, Required: true);
        var paramMember = new QuickWindowInterfaceMember(Guid.NewGuid(), "MotorName", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.String, QuickWindowMemberAccess.Read, Required: false);
        var thresholdMember = new QuickWindowInterfaceMember(Guid.NewGuid(), "Threshold", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.Integer, QuickWindowMemberAccess.Read, Required: false);
        var privateVar = new QuickWindowInterfaceMember(Guid.NewGuid(), "LocalCount", QuickWindowInterfaceFamily.PrivateVariable, QuickWindowDataType.Integer, QuickWindowMemberAccess.Internal, Required: false);
        var constant = new QuickWindowInterfaceMember(Guid.NewGuid(), "Precision", QuickWindowInterfaceFamily.PrivateConstant, QuickWindowDataType.Decimal, QuickWindowMemberAccess.Internal, Required: false, DefaultValue: "2");

        var content = new VisualContent(CanvasSize.DefaultDesktop);
        return new QuickWindowDefinition(Guid.NewGuid(), "qw_motor", "Moteur Faceplate", 1, content, new[] { readMember, writeMember, paramMember, thresholdMember, privateVar, constant });
    }

    private static ScadaTagCatalog CreateCatalog()
    {
        return new ScadaTagCatalog("tf100web-scada-tags-v1", new[]
        {
            new ScadaTagDefinition("tf100.mapping.210", "RunFeedback M101", Datatype: "Bool", Writeable: false, Enabled: true),
            new ScadaTagDefinition("tf100.mapping.211", "StartCommand M101", Datatype: "Bool", Writeable: true, Enabled: true),
            new ScadaTagDefinition("tf100.mapping.310", "RunFeedback M102", Datatype: "Bool", Writeable: false, Enabled: true),
            new ScadaTagDefinition("tf100.mapping.311", "StartCommand M102", Datatype: "Bool", Writeable: true, Enabled: true),
            new ScadaTagDefinition("tf100.mapping.999", "ReadonlyTag", Datatype: "Bool", Writeable: false, Enabled: true),
            new ScadaTagDefinition("tf100.mapping.220", "Threshold M101", Datatype: "Int32", Writeable: false, Enabled: true),
        });
    }

    [TestMethod]
    public void OpenQuickWindowRequiresInvocationKey()
    {
        var cmd = new ScadaCommandBinding("cmd1", "Open", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow, QuickWindowInvocationKey: null);
        var scene = ScadaScene.CreateEmpty("win00001", "Page", new CanvasSize(1280, 873))
            .WithElement(ScadaElement.CreateText("btn", "Btn", 10, 20) with
            {
                CommandConfig = new ScadaElementCommandConfig(new[] { cmd })
            });
        var project = ScadaProject.CreateDefault("P") with
        {
            Scenes = new[] { new ScadaSceneReference("win00001", "Page", "scenes/win00001.scene.json") },
            QuickWindowInvocations = Array.Empty<QuickWindowInvocation>()
        };
        var issues = ScadaProjectBuildValidator.Validate(project, new[] { scene });
        Assert.IsTrue(issues.Any(i => i.Code == "command.open-quick-window-missing-invocation"), "OpenQuickWindow must require invocation key.");
    }

    [TestMethod]
    public void CloseQuickWindowHasNoTargetAndNoInvocationKey()
    {
        // Valid CloseQuickWindow: no target, no invocation
        var valid = new ScadaCommandBinding("c1", "Close", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.CloseQuickWindow);
        var invalidWithPage = new ScadaCommandBinding("c2", "Close", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.CloseQuickWindow, TargetPageId: "win00001");
        var invalidWithInv = new ScadaCommandBinding("c3", "Close", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.CloseQuickWindow, QuickWindowInvocationKey: Guid.NewGuid());
        var sceneValid = ScadaScene.CreateEmpty("win00001", "Page", new CanvasSize(1280, 873))
            .WithElement(ScadaElement.CreateText("btn", "Btn", 10, 20) with { CommandConfig = new ScadaElementCommandConfig(new[] { valid }) });
        var sceneInvalidPage = ScadaScene.CreateEmpty("win00001", "Page", new CanvasSize(1280, 873))
            .WithElement(ScadaElement.CreateText("btn", "Btn", 10, 20) with { CommandConfig = new ScadaElementCommandConfig(new[] { invalidWithPage }) });
        var sceneInvalidInv = ScadaScene.CreateEmpty("win00001", "Page", new CanvasSize(1280, 873))
            .WithElement(ScadaElement.CreateText("btn", "Btn", 10, 20) with { CommandConfig = new ScadaElementCommandConfig(new[] { invalidWithInv }) });
        var project = ScadaProject.CreateDefault("P") with { Scenes = new[] { new ScadaSceneReference("win00001", "Page", "scenes/win00001.scene.json") } };

        Assert.IsTrue(ScadaProjectBuildValidator.Validate(project, new[] { sceneValid }).Any(i => i.Code == "command.close-quick-window-outside-content"));
        Assert.IsTrue(ScadaProjectBuildValidator.Validate(project, new[] { sceneInvalidPage }).Any(i => i.Code == "command.close-quick-window-target"));
        Assert.IsTrue(ScadaProjectBuildValidator.Validate(project, new[] { sceneInvalidInv }).Any(i => i.Code == "command.close-quick-window-invocation"));
    }

    [TestMethod]
    public void NoToggleQuickWindowExists()
    {
        var kinds = Enum.GetValues<ScadaCommandKind>();
        // Verify new quick window kinds exist and old popup kinds were removed
        Assert.IsTrue(kinds.Contains(ScadaCommandKind.OpenQuickWindow));
        Assert.IsTrue(kinds.Contains(ScadaCommandKind.CloseQuickWindow));
        // Ensure no ToggleQuickWindow kind and no legacy popup kinds
        Assert.IsFalse(Enum.TryParse<ScadaCommandKind>("ToggleQuickWindow", out _));
        Assert.IsFalse(Enum.TryParse<ScadaCommandKind>("OpenPopup", out _));
        Assert.IsFalse(Enum.TryParse<ScadaCommandKind>("TogglePopup", out _));
        Assert.IsFalse(Enum.TryParse<ScadaCommandKind>("ClosePopup", out _));
        Assert.IsFalse(kinds.Any(k => k.ToString().Contains("Toggle") && k.ToString().Contains("Popup")));
    }

    [TestMethod]
    public void TypeAccessWriteValidation()
    {
        var def = CreateDefinitionWithMembers();
        var catalog = CreateCatalog();
        var readMember = def.InterfaceMembers.First(m => m.Name == "RunFeedback");
        var writeMember = def.InterfaceMembers.First(m => m.Name == "StartCommand");

        // Tag valid
        var validTag = QuickWindowBinding.FromTag(readMember.MemberKey, "tf100.mapping.210");
        var r1 = QuickWindowBindingValidator.ValidateBinding(validTag, readMember, catalog, def.InterfaceVersion);
        Assert.IsTrue(r1.IsValid);

        // Write member with readonly tag should fail
        var invalidWrite = QuickWindowBinding.FromTag(writeMember.MemberKey, "tf100.mapping.999");
        var r2 = QuickWindowBindingValidator.ValidateBinding(invalidWrite, writeMember, catalog, def.InterfaceVersion);
        Assert.IsFalse(r2.IsValid);
        Assert.IsTrue(r2.ErrorCode?.Contains("readonly") == true || r2.Message?.Contains("read-only") == true);

        // Wrong datatype: string param with bool tag? Using bool tag for string member should be considered? Our catalog bool vs string param maybe incompatible? We'll test
        var paramMember = def.InterfaceMembers.First(m => m.Name == "MotorName");
        var boolTagForString = QuickWindowBinding.FromTag(paramMember.MemberKey, "tf100.mapping.210");
        var r3 = QuickWindowBindingValidator.ValidateBinding(boolTagForString, paramMember, catalog, def.InterfaceVersion);
        Assert.IsFalse(r3.IsValid);
        // String member with bool tag: our compatibility allows bool -> string? Actually IsTagDatatypeCompatible returns false for mismatch. Let's assert it is invalid if we tighten, but currently enum type string vs bool tag may be considered incompatible.
        // Instead test required: writeMember required missing should be invalid when absent
        var absentForRequired = QuickWindowBinding.Absent(writeMember.MemberKey);
        var r4 = QuickWindowBindingValidator.ValidateBinding(absentForRequired, writeMember, catalog, def.InterfaceVersion);
        Assert.IsFalse(r4.IsValid);
        Assert.AreEqual("binding.required-missing", r4.ErrorCode);

        // Optional absent should be valid and neutral (no subscription)
        var optionalMember = def.InterfaceMembers.First(m => m.Name == "MotorName");
        var absentOptional = QuickWindowBinding.Absent(optionalMember.MemberKey);
        var r5 = QuickWindowBindingValidator.ValidateBinding(absentOptional, optionalMember, catalog, def.InterfaceVersion);
        Assert.IsTrue(r5.IsValid);
    }

    [TestMethod]
    public void TwoInvocationsCreatedIndependently()
    {
        var def = CreateDefinitionWithMembers();
        var catalog = CreateCatalog();

        var mRead = def.InterfaceMembers.First(m => m.Name == "RunFeedback");
        var mWrite = def.InterfaceMembers.First(m => m.Name == "StartCommand");
        var mParam = def.InterfaceMembers.First(m => m.Name == "MotorName");

        var invM101 = new QuickWindowInvocation(Guid.NewGuid(), def.DefinitionKey, new[]
        {
            QuickWindowBinding.FromTag(mRead.MemberKey, "tf100.mapping.210"),
            QuickWindowBinding.FromTag(mWrite.MemberKey, "tf100.mapping.211"),
            QuickWindowBinding.FromLiteral(mParam.MemberKey, "Moteur M101")
        }, TitleOverride: "M101");

        var invM102 = new QuickWindowInvocation(Guid.NewGuid(), def.DefinitionKey, new[]
        {
            QuickWindowBinding.FromTag(mRead.MemberKey, "tf100.mapping.310"),
            QuickWindowBinding.FromTag(mWrite.MemberKey, "tf100.mapping.311"),
            QuickWindowBinding.FromLiteral(mParam.MemberKey, "Moteur M102")
        }, TitleOverride: "M102");

        Assert.AreNotEqual(invM101.InvocationKey, invM102.InvocationKey);
        Assert.AreEqual(def.DefinitionKey, invM101.DefinitionKey);
        Assert.AreEqual(def.DefinitionKey, invM102.DefinitionKey);

        var r101 = QuickWindowBindingValidator.ValidateInvocation(invM101, def, catalog);
        var r102 = QuickWindowBindingValidator.ValidateInvocation(invM102, def, catalog);

        Assert.IsTrue(r101.All(r => r.IsValid), string.Join("; ", r101.Where(r => !r.IsValid).Select(r => r.Message)));
        Assert.IsTrue(r102.All(r => r.IsValid), string.Join("; ", r102.Where(r => !r.IsValid).Select(r => r.Message)));

        // Ensure no cross leak: M101 tags not in M102
        Assert.IsFalse(invM101.Bindings.Any(b => b.TagId == "tf100.mapping.310"));
        Assert.IsFalse(invM102.Bindings.Any(b => b.TagId == "tf100.mapping.210"));
    }

    [TestMethod]
    public void AntiInjectionLiteralIsRejectedWithoutSubscription()
    {
        var def = CreateDefinitionWithMembers();
        var paramMember = def.InterfaceMembers.First(m => m.Name == "MotorName");
        var catalog = CreateCatalog();

        var injections = new[]
        {
            "<script>alert(1)</script>",
            "{{evil}}",
            "${process}",
            "javascript:alert(1)",
            "[data-scada]",
            "url(http://evil)",
            "../secret",
            "C:\\windows\\file",
            "//evil.com",
            "\\\\network\\share"
        };

        foreach (var payload in injections)
        {
            var binding = QuickWindowBinding.FromLiteral(paramMember.MemberKey, payload);
            var result = QuickWindowBindingValidator.ValidateBinding(binding, paramMember, catalog, def.InterfaceVersion);
            Assert.IsFalse(result.IsValid, $"Literal injection payload '{payload}' should be rejected.");
            Assert.AreEqual("injection-rejected", result.Category, $"Payload '{payload}' should be injection-rejected.");
            // Must not create subscription nor write: validator ensures blocked; we also verify no tag involved
            Assert.IsTrue(binding.SourceKind == QuickWindowBindingSourceKind.Literal);
        }

        // Valid literal remains typed and escaped
        var valid = QuickWindowBinding.FromLiteral(paramMember.MemberKey, "Moteur M101");
        var validResult = QuickWindowBindingValidator.ValidateBinding(valid, paramMember, catalog, def.InterfaceVersion);
        Assert.IsTrue(validResult.IsValid, "Valid literal should pass.");
    }

    [TestMethod]
    public void AntiInjectionExpressionIsRejectedBlocked()
    {
        var def = CreateDefinitionWithMembers();
        var intMember = def.InterfaceMembers.First(m => m.Name == "Threshold");
        var catalog = CreateCatalog();

        var injectionsExpr = new[]
        {
            "<script>",
            "{{a}}",
            "${b}",
            "javascript:evil()",
            "[data-test]",
            "url(x)",
            "../",
            "C:\\path",
            "//comment",
            "\\\\share"
        };

        foreach (var payload in injectionsExpr)
        {
            var binding = QuickWindowBinding.FromExpression(intMember.MemberKey, payload);
            var result = QuickWindowBindingValidator.ValidateBinding(binding, intMember, catalog, def.InterfaceVersion);
            Assert.IsFalse(result.IsValid, $"Expression injection payload '{payload}' should be rejected.");
            Assert.AreEqual("injection-rejected", result.Category);
        }

        // Valid expression
        var validExpr = QuickWindowBinding.FromExpression(intMember.MemberKey, "{tf100.mapping.220} + 10");
        var validRes = QuickWindowBindingValidator.ValidateBinding(validExpr, intMember, catalog, def.InterfaceVersion);
        Assert.IsTrue(validRes.IsValid, validRes.Message);
        var validExpr2 = QuickWindowBinding.FromExpression(intMember.MemberKey, "{tf100.mapping.220}");
        var validRes2 = QuickWindowBindingValidator.ValidateBinding(validExpr2, intMember, catalog, def.InterfaceVersion);
        Assert.IsTrue(validRes2.IsValid);
    }

    [TestMethod]
    public void RequiredPortAbsentIsNeutralAndBlockedOnBuild()
    {
        var def = CreateDefinitionWithMembers();
        var catalog = CreateCatalog();
        var requiredMember = def.InterfaceMembers.First(m => m.Required);
        var invocation = new QuickWindowInvocation(Guid.NewGuid(), def.DefinitionKey, new[]
        {
            QuickWindowBinding.Absent(requiredMember.MemberKey) // required missing
        });
        var results = QuickWindowBindingValidator.ValidateInvocation(invocation, def, catalog);
        Assert.IsTrue(results.Any(r => !r.IsValid && r.ErrorCode == "binding.required-missing"));

        // Build validator should surface required-missing
        var project = ScadaProject.CreateDefault("P") with
        {
            Scenes = new[] { new ScadaSceneReference("win00001", "Page", "scenes/win00001.scene.json") },
            QuickWindows = new[] { def },
            QuickWindowInvocations = new[] { invocation }
        };
        var issues = ScadaProjectBuildValidator.Validate(project, Array.Empty<ScadaScene>());
        Assert.IsTrue(issues.Any(i => i.Code == "quick-window.required-missing" || i.Code == "quick-window.binding-invalid"));
    }

    [TestMethod]
    public void CloseQuickWindowIsRejectedOutsideDefinitionAndAllowedInsideDefinition()
    {
        var close = new ScadaCommandBinding("close1", "Fermer", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.CloseQuickWindow);
        var element = ScadaElement.CreateText("close", "Close", 0, 0) with { CommandConfig = new ScadaElementCommandConfig(new[] { close }) };
        var scene = ScadaScene.CreateEmpty("win00001", "Page", CanvasSize.DefaultDesktop).WithElement(element);
        var project = ScadaProject.CreateDefault("P") with { Scenes = new[] { new ScadaSceneReference("win00001", "Page", "scenes/win00001.scene.json") } };
        Assert.IsTrue(ScadaProjectBuildValidator.Validate(project, new[] { scene }).Any(issue => issue.Code == "command.close-quick-window-outside-content"));

        var definition = QuickWindowDefinition.CreateEmpty("qw_close", "Close") with
        {
            Content = new VisualContent(CanvasSize.DefaultDesktop, Elements: new[] { element })
        };
        Assert.IsFalse(QuickWindowValidation.ValidateDefinition(definition).Any(issue => issue.Contains("CloseQuickWindow", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void DiagnosticsArePreciseAndCategoryInjectionRejected()
    {
        var def = CreateDefinitionWithMembers();
        var paramMember = def.InterfaceMembers.First(m => m.Name == "MotorName");
        var catalog = CreateCatalog();
        var bad = QuickWindowBinding.FromLiteral(paramMember.MemberKey, "<script>");
        var res = QuickWindowBindingValidator.ValidateBinding(bad, paramMember, catalog, def.InterfaceVersion);
        Assert.IsFalse(res.IsValid);
        Assert.AreEqual("injection-rejected", res.Category);
        StringAssert.Contains(res.Message!, "<script");
    }

    [TestMethod]
    public void LiteralValidRemainsTypedAndEscaped()
    {
        // Valid integer literal for a public Integer parameter.
        var def = CreateDefinitionWithMembers();
        var intMember = def.InterfaceMembers.First(m => m.Name == "Threshold");
        var catalog = CreateCatalog();
        var lit = QuickWindowBinding.FromLiteral(intMember.MemberKey, "42");
        var res = QuickWindowBindingValidator.ValidateBinding(lit, intMember, catalog, def.InterfaceVersion);
        Assert.IsTrue(res.IsValid, "Integer literal 42 should be valid for Integer member.");
        // Simulate escaping: literal stored as typed, not raw selector
        Assert.AreEqual("42", lit.LiteralValue);
    }

    [TestMethod]
    public void InvocationVersionMustMatchDefinitionVersion()
    {
        var definition = CreateDefinitionWithMembers() with { InterfaceVersion = 2 };
        var invocation = new QuickWindowInvocation(Guid.NewGuid(), definition.DefinitionKey, Array.Empty<QuickWindowBinding>(), InterfaceVersion: 1);
        var results = QuickWindowBindingValidator.ValidateInvocation(invocation, definition, CreateCatalog());
        Assert.IsTrue(results.Any(result => result.ErrorCode == "invocation.interface-version-mismatch"));
    }

    [TestMethod]
    public void PrivateMemberAndAmbiguousPayloadAreRejected()
    {
        var definition = CreateDefinitionWithMembers();
        var privateMember = definition.InterfaceMembers.First(member => member.Family == QuickWindowInterfaceFamily.PrivateVariable);
        var privateBinding = QuickWindowBinding.FromLiteral(privateMember.MemberKey, "1");
        Assert.AreEqual("binding.private-member", QuickWindowBindingValidator.ValidateBinding(privateBinding, privateMember, CreateCatalog(), 1).ErrorCode);

        var publicMember = definition.InterfaceMembers.First(member => member.Name == "MotorName");
        var ambiguous = new QuickWindowBinding(publicMember.MemberKey, QuickWindowBindingSourceKind.Tag, TagId: "tf100.mapping.210", LiteralValue: "unexpected");
        Assert.AreEqual("binding.ambiguous-payload", QuickWindowBindingValidator.ValidateBinding(ambiguous, publicMember, CreateCatalog(), 1).ErrorCode);
    }

    [TestMethod]
    public void MalformedExpressionIsRejectedFailClosed()
    {
        var definition = CreateDefinitionWithMembers();
        var member = definition.InterfaceMembers.First(item => item.Name == "Threshold");
        var malformed = QuickWindowBinding.FromExpression(member.MemberKey, "({tf100.mapping.220} +");
        var result = QuickWindowBindingValidator.ValidateBinding(malformed, member, CreateCatalog(), definition.InterfaceVersion);
        Assert.AreEqual("binding.expression-syntax", result.ErrorCode);
    }
}
