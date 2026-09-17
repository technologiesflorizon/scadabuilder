using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

[TestClass]
public sealed class QuickWindowBuildValidationTests
{
    [TestMethod]
    public void ProjectOnlyValidationStillAppliesQuickWindowBuildGate()
    {
        // Phase 6 promoted eleven capabilities, so a project that stays inside them no longer trips the
        // gate. The gate itself must stay armed for what was not promoted, and must name it.
        var member = Member("Run", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read);
        var definition = Definition("motor", members: [member]);
        var page = Page("page");

        var clean = ScadaProjectBuildValidator.Validate(Project(page, [definition], []));
        Assert.IsFalse(
            clean.Any(issue => issue.Code == "quick-window.capability-unsupported"),
            "a project on promoted capabilities alone must not be gated");

        var forwarding = new QuickWindowInvocation(
            Guid.NewGuid(),
            definition.DefinitionKey,
            [QuickWindowBinding.FromParentPort(member.MemberKey, member.MemberKey)],
            InterfaceVersion: 1);
        var gated = ScadaProjectBuildValidator.Validate(Project(page, [definition], [forwarding]));

        var issue = gated.SingleOrDefault(item => item.Code == "quick-window.capability-unsupported");
        Assert.IsNotNull(issue, "a parent-port binding must still be refused at build time");
        StringAssert.Contains(issue!.Message, "quick-window.binding.parent-port");
    }

    [TestMethod]
    public void ContentIsAuthorisedByCapabilityStatusEvenWhenManifestVersionNeverBecame23()
    {
        // ScadaProject.ManifestVersion defaults to "2.0" (ProjectModels.cs) and nothing in src/ ever assigns
        // it "2.3" - that string is only ever written into the exported manifest artifact, a different thing
        // (Ft100SceneExporter.ManifestVersion(Ft100ManifestProfile)). Before Task 8 of the 2026-09-08
        // format-versioning chantier, ValidateQuickWindows compared this field against "2.3" directly, so
        // this exact project - "2.0", one Supported-capability definition, one invocation, real quick-window
        // content - was refused as an Error on every real project the instant it carried quick windows. This
        // exercises the actual authorisation path end to end, not a catalog constant in isolation.
        var member = Member("Run", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read);
        var definition = Definition("motor", members: [member]);
        var invocation = Invocation(Guid.NewGuid(), definition.DefinitionKey);

        // Positive anchor (round 2 of review): the two Assert.IsFalse below are pure absence assertions - if a
        // future edit made Project(...) hand ValidateQuickWindows an empty QuickWindows/QuickWindowInvocations
        // pair (a swapped parameter, say), containsQuickWindows would be false, the capability check would be
        // skipped, both codes would still be absent, and this test would pass while covering nothing. A second
        // invocation whose DefinitionKey matches no definition forces the validator to have actually iterated
        // `EffectiveQuickWindowInvocations` against `EffectiveQuickWindows` (ProjectModels.cs, the
        // `foreach (var inv in invs)` loop: `defs.FirstOrDefault(d => d.DefinitionKey == inv.DefinitionKey)`
        // is null, so it emits `quick-window.invocation-definition-missing`) - a diagnostic no validator that
        // skipped this content, or that never looked at DefinitionKey at all, could produce.
        var orphanInvocation = Invocation(Guid.NewGuid(), Guid.NewGuid());
        var page = Page("page");

        var issues = ScadaProjectBuildValidator.Validate(Project(page, [definition], [invocation, orphanInvocation], manifestVersion: "2.0"));

        Assert.IsTrue(
            issues.Any(issue => issue.Code == "quick-window.invocation-definition-missing"),
            "positive anchor: the validator must have actually iterated invocations against definitions.");
        Assert.IsFalse(
            issues.Any(issue => issue.Code == "quick-window.profile-unsupported"),
            "this error code no longer exists in the product; ManifestVersion does not authorise content.");
        Assert.IsFalse(
            issues.Any(issue => issue.Code == "quick-window.capability-unsupported"),
            "quick-window.definition, OpenQuickWindow and CloseQuickWindow are Supported, so the catalog allows this content regardless of ManifestVersion.");
    }

    [TestMethod]
    public void BuildValidationSeparatesAuthoringWarningsFromErrorsAndNeverCreatesDefaults()
    {
        var member = Member("Run", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read, required: true);
        var definition = Definition("motor", members: [member]);
        var invocation = new QuickWindowInvocation(Guid.NewGuid(), definition.DefinitionKey, [], InterfaceVersion: 1);
        var page = Page("page");
        // Deliberately "2.2": before Task 8 of the 2026-09-08 format-versioning chantier, this alone tripped
        // "quick-window.profile-unsupported". Content authorisation now comes from the capability catalog,
        // not this field, so the same project on a "2.2" manifest still only reports the missing binding.
        var project = Project(page, [definition], [invocation], manifestVersion: "2.2");
        var beforeBindings = invocation.Bindings;

        var buildIssues = ScadaProjectBuildValidator.Validate(project, [page]);
        var snapshot = new PageWorkspaceSnapshot(
            1,
            project,
            new Dictionary<Guid, ScadaScene> { [page.PageKey] = page },
            []);
        var authoringIssues = new QuickWindowDependencyAnalyzer().Analyze(snapshot).Diagnostics;

        Assert.IsTrue(buildIssues.Any(issue => issue.Code == "quick-window.required-missing" && issue.Severity == ScadaBuildValidationSeverity.Error));
        Assert.IsFalse(
            buildIssues.Any(issue => issue.Code == "quick-window.profile-unsupported"),
            "ManifestVersion no longer authorises quick-window content; the capability catalog does.");
        Assert.IsTrue(authoringIssues.Any(issue => issue.Code == "quick-window.binding.required-missing" && issue.Severity == ScadaBuildValidationSeverity.Warning));
        Assert.AreSame(beforeBindings, invocation.Bindings);
        Assert.AreEqual(0, invocation.Bindings.Count);
    }

    [TestMethod]
    public void BuildValidationRejectsMappingTypeAccessAndLiteralExpressionInjection()
    {
        var missing = Member("Missing", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read);
        var disabled = Member("Disabled", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read);
        var typed = Member("Typed", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.String, QuickWindowMemberAccess.Read);
        var writable = Member("Writable", QuickWindowInterfaceFamily.WriteCommand, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Write);
        var literal = Member("Literal", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.String, QuickWindowMemberAccess.Read);
        var expression = Member("Expression", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.Integer, QuickWindowMemberAccess.Read);
        var definition = Definition("motor", members: [missing, disabled, typed, writable, literal, expression]);
        var invocation = new QuickWindowInvocation(
            Guid.NewGuid(),
            definition.DefinitionKey,
            [
                QuickWindowBinding.FromTag(missing.MemberKey, "tag.missing"),
                QuickWindowBinding.FromTag(disabled.MemberKey, "tag.disabled"),
                QuickWindowBinding.FromTag(typed.MemberKey, "tag.bool"),
                QuickWindowBinding.FromTag(writable.MemberKey, "tag.readonly"),
                QuickWindowBinding.FromLiteral(literal.MemberKey, "<script>alert(1)</script>"),
                QuickWindowBinding.FromExpression(expression.MemberKey, "${danger}")
            ],
            InterfaceVersion: 1);
        var catalog = new ScadaTagCatalog("test", [
            new ScadaTagDefinition("tag.disabled", "Disabled", Datatype: "Bool", Enabled: false),
            new ScadaTagDefinition("tag.bool", "Bool", Datatype: "Bool"),
            new ScadaTagDefinition("tag.readonly", "Readonly", Datatype: "Bool", Writeable: false)
        ]);
        var page = Page("page");
        var project = Project(page, [definition], [invocation]) with { TagCatalog = catalog };

        var issues = ScadaProjectBuildValidator.Validate(project, [page]);

        Assert.IsTrue(issues.Any(issue => issue.Code == "quick-window.mapping-missing"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "quick-window.mapping-disabled"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "quick-window.type-incompatible"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "quick-window.access-invalid"));
        Assert.AreEqual(2, issues.Count(issue => issue.Code == "quick-window.injection-rejected"));
        Assert.IsTrue(issues.Where(issue => issue.Code.StartsWith("quick-window.", StringComparison.Ordinal)).All(issue => issue.Severity == ScadaBuildValidationSeverity.Error));
    }

    [TestMethod]
    public void BuildValidationRejectsMissingContentPresentationVersionRemovedPortAndDefinition()
    {
        var oldMemberKey = Guid.NewGuid();
        var definition = Definition("motor", interfaceVersion: 2) with
        {
            PresentationDefaults = new QuickWindowPresentationDefaults(IsResizable: true)
        };
        var missingContent = Definition("empty") with { Content = null! };
        var staleInvocation = new QuickWindowInvocation(
            Guid.NewGuid(),
            definition.DefinitionKey,
            [QuickWindowBinding.FromLiteral(oldMemberKey, "stale")],
            InterfaceVersion: 1);
        var missingDefinitionInvocation = new QuickWindowInvocation(Guid.NewGuid(), Guid.NewGuid(), [], InterfaceVersion: 1);
        var page = Page("page");
        var project = Project(page, [definition, missingContent], [staleInvocation, missingDefinitionInvocation]);

        var issues = ScadaProjectBuildValidator.Validate(project, [page]);

        Assert.IsTrue(issues.Any(issue => issue.Code == "quick-window.content-missing"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "quick-window.presentation-invalid"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "quick-window.interface-version-incompatible"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "quick-window.port-removed"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "quick-window.invocation-definition-missing"));
    }

    [TestMethod]
    public void BuildValidationRejectsCycleAndDepthThreeButAllowsPageToAToB()
    {
        var aKey = Guid.NewGuid();
        var bKey = Guid.NewGuid();
        var cKey = Guid.NewGuid();
        var ab = Invocation(Guid.NewGuid(), bKey);
        var definitionA = Definition("a", aKey, elements: [Caller("open-b", ab.InvocationKey)]);
        var definitionB = Definition("b", bKey);
        var page = Page("page", Caller("open-a", Guid.NewGuid()));
        var depthTwo = Project(page, [definitionA, definitionB], [ab]);

        Assert.IsFalse(ScadaProjectBuildValidator.Validate(depthTwo, [page]).Any(issue => issue.Code == "cycle/depth-exceeded"));

        var bc = Invocation(Guid.NewGuid(), cKey);
        definitionB = definitionB with
        {
            Content = definitionB.EffectiveContent with { Elements = [Caller("open-c", bc.InvocationKey)] }
        };
        var definitionC = Definition("c", cKey);
        var depthThree = Project(page, [definitionA, definitionB, definitionC], [ab, bc]);
        var depthIssues = ScadaProjectBuildValidator.Validate(depthThree, [page]).Where(issue => issue.Code == "cycle/depth-exceeded").ToArray();
        Assert.AreEqual(1, depthIssues.Length);
        StringAssert.Contains(depthIssues[0].Message, "exceeds two");

        var ba = Invocation(Guid.NewGuid(), aKey);
        definitionB = definitionB with
        {
            Content = definitionB.EffectiveContent with { Elements = [Caller("open-a", ba.InvocationKey)] }
        };
        var cycle = Project(page, [definitionA, definitionB], [ab, ba]);
        var cycleIssues = ScadaProjectBuildValidator.Validate(cycle, [page]).Where(issue => issue.Code == "cycle/depth-exceeded").ToArray();
        Assert.AreEqual(1, cycleIssues.Length);
        StringAssert.Contains(cycleIssues[0].Message, "cycle");
    }

    [TestMethod]
    public void BuildValidationRejectsNonSelfCloseAndMissingNestedInvocation()
    {
        var close = new ScadaCommandBinding(
            "close",
            "Close",
            true,
            ScadaCommandTrigger.OnClick,
            ScadaCommandKind.CloseQuickWindow,
            TargetPageId: "page");
        var open = new ScadaCommandBinding(
            "open",
            "Open",
            true,
            ScadaCommandTrigger.OnClick,
            ScadaCommandKind.OpenQuickWindow,
            QuickWindowInvocationKey: Guid.NewGuid());
        var definition = Definition("motor", elements: [Element("commands", close, open)]);
        var page = Page("page");
        var project = Project(page, [definition], []);

        var issues = ScadaProjectBuildValidator.Validate(project, [page]);

        Assert.IsTrue(issues.Any(issue => issue.Code == "quick-window.close-selector-invalid"));
        Assert.IsTrue(issues.Any(issue => issue.Code == "quick-window.invocation-missing"));
    }

    private static ScadaProject Project(
        ScadaScene page,
        IReadOnlyList<QuickWindowDefinition> definitions,
        IReadOnlyList<QuickWindowInvocation> invocations,
        string manifestVersion = "2.3") =>
        ScadaProject.CreateDefault("BuildValidation") with
        {
            ManifestVersion = manifestVersion,
            Scenes = [new ScadaSceneReference(
                page.Id,
                page.Title,
                $"scenes/{page.PageKey:N}.scene.json",
                PageKey: page.PageKey,
                PageCode: page.EffectivePageCode)],
            QuickWindows = definitions,
            QuickWindowInvocations = invocations
        };

    private static ScadaScene Page(string code, params ScadaElement[] elements)
    {
        var page = ScadaScene.CreateEmpty(code, code, CanvasSize.DefaultDesktop) with
        {
            PageKey = Guid.NewGuid(),
            PageCode = code
        };
        return elements.Aggregate(page, (current, element) => current.WithElement(element));
    }

    private static QuickWindowDefinition Definition(
        string code,
        Guid? key = null,
        IReadOnlyList<QuickWindowInterfaceMember>? members = null,
        int interfaceVersion = 1,
        IReadOnlyList<ScadaElement>? elements = null) =>
        new(
            key ?? Guid.NewGuid(),
            code,
            code,
            interfaceVersion,
            new VisualContent(new CanvasSize(480, 320), Elements: elements ?? []),
            members ?? [],
            new QuickWindowPresentationDefaults());

    private static QuickWindowInterfaceMember Member(
        string name,
        QuickWindowInterfaceFamily family,
        QuickWindowDataType dataType,
        QuickWindowMemberAccess access,
        bool required = false) =>
        new(Guid.NewGuid(), name, family, dataType, access, required);

    private static QuickWindowInvocation Invocation(Guid invocationKey, Guid definitionKey) =>
        new(invocationKey, definitionKey, [], InterfaceVersion: 1);

    private static ScadaElement Caller(string commandId, Guid invocationKey) => Element(
        $"element-{commandId}",
        new ScadaCommandBinding(
            commandId,
            commandId,
            true,
            ScadaCommandTrigger.OnClick,
            ScadaCommandKind.OpenQuickWindow,
            QuickWindowInvocationKey: invocationKey));

    private static ScadaElement Element(string elementId, params ScadaCommandBinding[] commands) =>
        ScadaElement.CreateButton(elementId, elementId, 10, 20, ScadaButtonKind.Command) with
        {
            CommandConfig = new ScadaElementCommandConfig(commands)
        };
}
