using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Covers the local-interface versioning transitions and the derived `Outdated` invocation status.
/// Decisions: DEC-0050, FR-032, FR-UI-24. Plan: Task 2.4.
/// </summary>
[TestClass]
public sealed class QuickWindowInterfaceVersioningTests
{
    [TestMethod]
    public void RenamingAMemberNeedsNoVersionIncrementAndKeepsInvocationsCurrent()
    {
        var member = Member("Run");
        var definition = Definition("motor", members: [member]);
        var invocation = Invocation(definition, QuickWindowBinding.FromTag(member.MemberKey, "motor.run"));
        var snapshot = Snapshot(definition, invocation);

        var renamed = definition with { InterfaceMembers = [member with { Name = "RunFeedback" }] };
        var result = new QuickWindowDefinitionService().Update(snapshot, renamed);

        Assert.AreEqual(CommandResultStatus.Succeeded, result.Result.Status);
        var after = result.After.Project.EffectiveQuickWindowInvocations.Single();
        Assert.AreEqual(1, after.InterfaceVersion);
        Assert.AreEqual(
            QuickWindowInvocationStatus.Current,
            QuickWindowInterfaceCompatibility.StatusOf(result.After.Project.EffectiveQuickWindows.Single(), after));
        Assert.AreEqual(QuickWindowBindingSourceKind.Tag, after.Bindings.Single().SourceKind);
    }

    [TestMethod]
    public void PrivateMemberChangeNeedsNoVersionIncrement()
    {
        var privateMember = new QuickWindowInterfaceMember(
            Guid.NewGuid(),
            "Scratch",
            QuickWindowInterfaceFamily.PrivateVariable,
            QuickWindowDataType.Integer,
            QuickWindowMemberAccess.Internal);
        var definition = Definition("motor", members: [privateMember]);
        var invocation = Invocation(definition);
        var snapshot = Snapshot(definition, invocation);

        var changed = definition with
        {
            InterfaceMembers = [privateMember with { DataType = QuickWindowDataType.Decimal }]
        };
        var result = new QuickWindowDefinitionService().Update(snapshot, changed);

        Assert.AreEqual(CommandResultStatus.Succeeded, result.Result.Status);
        Assert.AreEqual(1, result.After.Project.EffectiveQuickWindowInvocations.Single().InterfaceVersion);
    }

    [TestMethod]
    public void AddingAnOptionalPublicMemberRealignsInvocationsAndLeavesTheNewPortUnbound()
    {
        var member = Member("Run");
        var definition = Definition("motor", members: [member]);
        var invocation = Invocation(definition, QuickWindowBinding.FromTag(member.MemberKey, "motor.run"));
        var snapshot = Snapshot(definition, invocation);

        var added = Member("Speed", QuickWindowDataType.Integer);
        var candidate = definition with { InterfaceVersion = 2, InterfaceMembers = [member, added] };
        var result = new QuickWindowDefinitionService().Update(snapshot, candidate);

        Assert.AreEqual(CommandResultStatus.Succeeded, result.Result.Status);
        var after = result.After.Project.EffectiveQuickWindowInvocations.Single();
        Assert.AreEqual(2, after.InterfaceVersion, "a compatible transition realigns the invocation instead of breaking it");
        Assert.AreEqual(
            QuickWindowInvocationStatus.Current,
            QuickWindowInterfaceCompatibility.StatusOf(result.After.Project.EffectiveQuickWindows.Single(), after));
        Assert.IsNull(after.FindBinding(added.MemberKey), "a new port is unbound, never defaulted");
        Assert.IsFalse(result.Result.Diagnostics.Any(issue => issue.Code == "quick-window.invocation-outdated"));
    }

    [TestMethod]
    public void AddingARequiredPublicMemberMarksInvocationsOutdatedAndBlocksBuild()
    {
        var member = Member("Run");
        var definition = Definition("motor", members: [member]);
        var invocation = Invocation(definition, QuickWindowBinding.FromTag(member.MemberKey, "motor.run"));
        var snapshot = Snapshot(definition, invocation);

        var required = Member("Speed", QuickWindowDataType.Integer, required: true);
        var candidate = definition with { InterfaceVersion = 2, InterfaceMembers = [member, required] };
        var result = new QuickWindowDefinitionService().Update(snapshot, candidate);

        Assert.AreEqual(CommandResultStatus.Succeeded, result.Result.Status, "an outdated invocation stays saveable");
        var after = result.After.Project.EffectiveQuickWindowInvocations.Single();
        Assert.AreEqual(1, after.InterfaceVersion, "a breaking transition never realigns silently");
        Assert.AreEqual(
            QuickWindowInvocationStatus.Outdated,
            QuickWindowInterfaceCompatibility.StatusOf(result.After.Project.EffectiveQuickWindows.Single(), after));
        Assert.IsTrue(result.Result.Diagnostics.Any(issue => issue.Code == "quick-window.invocation-outdated"));
        Assert.AreEqual(1, after.Bindings.Count, "existing bindings are preserved");

        var issues = ScadaProjectBuildValidator.Validate(result.After.Project, [Page("page")]);
        Assert.IsTrue(issues.Any(issue =>
            issue.Code == "quick-window.interface-version-incompatible" &&
            issue.Severity == ScadaBuildValidationSeverity.Error));
    }

    [TestMethod]
    public void RemovingABoundMemberOutdatesOnlyTheInvocationsThatBoundIt()
    {
        var kept = Member("Run");
        var removed = Member("Speed", QuickWindowDataType.Integer);
        var definition = Definition("motor", members: [kept, removed]);
        var binding = Invocation(definition, QuickWindowBinding.FromTag(removed.MemberKey, "motor.speed"));
        var unrelated = Invocation(definition, QuickWindowBinding.FromTag(kept.MemberKey, "motor.run"));
        var snapshot = Snapshot(definition, binding, unrelated);

        var candidate = definition with { InterfaceVersion = 2, InterfaceMembers = [kept] };
        var service = new QuickWindowDefinitionService();

        var blocked = service.Update(snapshot, candidate);
        Assert.AreEqual(CommandResultStatus.Blocked, blocked.Result.Status);
        Assert.IsTrue(blocked.Result.Diagnostics.Any(issue => issue.Code == "quick-window.interface-member-in-use"));

        var confirmed = service.Update(snapshot, candidate, confirmReferencedMemberRemoval: true);
        Assert.AreEqual(CommandResultStatus.Succeeded, confirmed.Result.Status);
        var afterDefinition = confirmed.After.Project.EffectiveQuickWindows.Single();
        var afterBinding = confirmed.After.Project.EffectiveQuickWindowInvocations.Single(item => item.InvocationKey == binding.InvocationKey);
        var afterUnrelated = confirmed.After.Project.EffectiveQuickWindowInvocations.Single(item => item.InvocationKey == unrelated.InvocationKey);

        Assert.AreEqual(QuickWindowInvocationStatus.Outdated, QuickWindowInterfaceCompatibility.StatusOf(afterDefinition, afterBinding));
        Assert.AreEqual(QuickWindowInvocationStatus.Current, QuickWindowInterfaceCompatibility.StatusOf(afterDefinition, afterUnrelated));
        Assert.AreEqual(1, afterBinding.Bindings.Count, "the orphan binding is reported, never deleted");
    }

    [TestMethod]
    public void ChangingTheTypeOfABoundMemberOutdatesThatInvocation()
    {
        var member = Member("Speed", QuickWindowDataType.Integer);
        var definition = Definition("motor", members: [member]);
        var invocation = Invocation(definition, QuickWindowBinding.FromTag(member.MemberKey, "motor.speed"));
        var snapshot = Snapshot(definition, invocation);

        var candidate = definition with
        {
            InterfaceVersion = 2,
            InterfaceMembers = [member with { DataType = QuickWindowDataType.String }]
        };
        var result = new QuickWindowDefinitionService().Update(snapshot, candidate, confirmReferencedMemberRemoval: true);

        Assert.AreEqual(CommandResultStatus.Succeeded, result.Result.Status);
        var after = result.After.Project.EffectiveQuickWindowInvocations.Single();
        Assert.AreEqual(
            QuickWindowInvocationStatus.Outdated,
            QuickWindowInterfaceCompatibility.StatusOf(result.After.Project.EffectiveQuickWindows.Single(), after));
    }

    [TestMethod]
    public void RepairRebindsRealignsAndClearsTheBuildGate()
    {
        var member = Member("Run");
        var required = Member("Speed", QuickWindowDataType.Integer, required: true);
        var definition = Definition("motor", members: [member, required], interfaceVersion: 2);
        var invocation = Invocation(definition, QuickWindowBinding.FromTag(member.MemberKey, "motor.run")) with
        {
            InterfaceVersion = 1
        };
        var snapshot = Snapshot(definition, invocation);
        var service = new QuickWindowInvocationService();

        var repaired = service.Repair(
            snapshot,
            invocation.InvocationKey,
            [
                QuickWindowBinding.FromTag(member.MemberKey, "motor.run"),
                QuickWindowBinding.FromLiteral(required.MemberKey, "12")
            ]);

        Assert.AreEqual(CommandResultStatus.Succeeded, repaired.Result.Status);
        var after = repaired.After.Project.EffectiveQuickWindowInvocations.Single();
        Assert.AreEqual(2, after.InterfaceVersion);
        Assert.AreEqual(QuickWindowInvocationStatus.Current, QuickWindowInterfaceCompatibility.StatusOf(definition, after));
        Assert.IsFalse(ScadaProjectBuildValidator.Validate(repaired.After.Project, [Page("page")])
            .Any(issue => issue.Code == "quick-window.interface-version-incompatible"));
    }

    [TestMethod]
    public void RepairIsBlockedWhileTheInvocationStaysIncompatible()
    {
        var member = Member("Run");
        var required = Member("Speed", QuickWindowDataType.Integer, required: true);
        var definition = Definition("motor", members: [member, required], interfaceVersion: 2);
        var invocation = Invocation(definition, QuickWindowBinding.FromTag(member.MemberKey, "motor.run")) with
        {
            InterfaceVersion = 1
        };
        var snapshot = Snapshot(definition, invocation);

        var repaired = new QuickWindowInvocationService().Repair(
            snapshot,
            invocation.InvocationKey,
            [QuickWindowBinding.FromTag(member.MemberKey, "motor.run")]);

        Assert.AreEqual(CommandResultStatus.Blocked, repaired.Result.Status);
        Assert.IsTrue(repaired.Result.Diagnostics.Any(issue => issue.Code == "quick-window.invocation-outdated"));
        Assert.AreEqual(1, repaired.After.Project.EffectiveQuickWindowInvocations.Single().InterfaceVersion);
    }

    [TestMethod]
    public void OutdatedInvocationsAreListedWithTheirReasonForTheRepairSurface()
    {
        var member = Member("Run");
        var required = Member("Speed", QuickWindowDataType.Integer, required: true);
        var definition = Definition("motor", members: [member, required], interfaceVersion: 2);
        var invocation = Invocation(definition, QuickWindowBinding.FromTag(member.MemberKey, "motor.run")) with
        {
            InterfaceVersion = 1
        };
        var snapshot = Snapshot(definition, invocation);

        var outdated = new QuickWindowDefinitionService().ListOutdatedInvocations(snapshot, definition.DefinitionKey);

        Assert.AreEqual(1, outdated.Count);
        Assert.AreEqual(invocation.InvocationKey, outdated[0].InvocationKey);
        Assert.AreEqual(1, outdated[0].InvocationInterfaceVersion);
        Assert.AreEqual(2, outdated[0].DefinitionInterfaceVersion);
        Assert.IsTrue(outdated[0].Reasons.Count > 0);
    }

    private static PageWorkspaceSnapshot Snapshot(QuickWindowDefinition definition, params QuickWindowInvocation[] invocations)
    {
        var page = Page("page");
        var project = ScadaProject.CreateDefault("QuickWindowVersioningTests") with
        {
            ManifestVersion = "2.3",
            Scenes =
            [
                new ScadaSceneReference(page.Id, page.Title, page.Id, PageKey: page.PageKey, PageCode: page.EffectivePageCode)
            ],
            QuickWindows = [definition],
            QuickWindowInvocations = invocations
        };
        return new PageWorkspaceSnapshot(1, project, new Dictionary<Guid, ScadaScene> { [page.PageKey] = page }, []);
    }

    private static ScadaScene Page(string code) =>
        ScadaScene.CreateEmpty(code, code, CanvasSize.DefaultDesktop) with
        {
            PageKey = PageKeyFor(code),
            PageCode = code
        };

    private static Guid PageKeyFor(string code) => new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(code)));

    private static QuickWindowDefinition Definition(
        string code,
        IReadOnlyList<QuickWindowInterfaceMember>? members = null,
        int interfaceVersion = 1) =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            code,
            code,
            interfaceVersion,
            new VisualContent(new CanvasSize(480, 320), Elements: []),
            members ?? [],
            new QuickWindowPresentationDefaults());

    private static QuickWindowInvocation Invocation(QuickWindowDefinition definition, params QuickWindowBinding[] bindings) =>
        new(
            Guid.NewGuid(),
            definition.DefinitionKey,
            bindings,
            InterfaceVersion: definition.InterfaceVersion);

    private static QuickWindowInterfaceMember Member(
        string name,
        QuickWindowDataType dataType = QuickWindowDataType.Boolean,
        bool required = false) =>
        new(
            Guid.NewGuid(),
            name,
            QuickWindowInterfaceFamily.ReadState,
            dataType,
            QuickWindowMemberAccess.Read,
            required);
}
