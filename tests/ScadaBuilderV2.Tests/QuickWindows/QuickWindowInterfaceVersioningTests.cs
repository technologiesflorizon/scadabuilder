using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Covers the local-interface versioning transitions, the derived `Outdated` invocation status and the
/// repair surface: listing with page, caller and reason, navigation, explicit port-by-port relinking, the
/// confirmation shown before an interface change outdates invocations, and the build gate that clears only
/// once every invocation is repaired.
/// Decisions: DEC-0050, FR-032, FR-UI-24. Plan: Tasks 2.4 and 3.6.
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

    [TestMethod]
    public void TheRepairSurfaceListsEachOutdatedInvocationWithItsCallerAndReason()
    {
        var member = Member("Run");
        var required = Member("Speed", QuickWindowDataType.Integer, required: true);
        var definition = Definition("motor", members: [member, required], interfaceVersion: 2);
        var invocation = Caller(definition, QuickWindowBinding.FromTag(member.MemberKey, "motor.run")) with
        {
            InterfaceVersion = 1
        };
        var snapshot = Snapshot(definition, invocation);
        var controller = new QuickWindowWorkspaceController(new RepairHost());

        var outdated = controller.ListOutdatedInvocations(snapshot, definition.DefinitionKey);

        Assert.AreEqual(1, outdated.Count);
        var row = new QuickWindowRepairRowViewModel(outdated[0], "page");
        Assert.AreEqual("page", row.PageLabel);
        Assert.AreEqual("caller", row.OwnerElementId);
        Assert.AreEqual("open", row.OwnerCommandId);
        Assert.AreEqual("v1 → v2", row.VersionLabel);
        StringAssert.Contains(row.ReasonLabel, "Speed");
    }

    [TestMethod]
    public void NavigatingToACallerSelectsItsPageWithoutMutatingAnything()
    {
        var member = Member("Run");
        var definition = Definition("motor", members: [member], interfaceVersion: 2);
        var invocation = Caller(definition, QuickWindowBinding.FromTag(member.MemberKey, "motor.run")) with
        {
            InterfaceVersion = 1
        };
        var snapshot = Snapshot(definition, invocation);
        var controller = new QuickWindowWorkspaceController(new RepairHost());

        var navigation = controller.NavigateToInvocation(snapshot, invocation.InvocationKey);

        Assert.IsNotNull(navigation);
        Assert.IsFalse(navigation!.Result.Changed);
        Assert.AreEqual(PageKeyFor("page"), navigation.Result.PageToOpenKey);
        Assert.AreEqual("caller", navigation.UsageToNavigate?.ElementId);
        Assert.IsNull(controller.NavigateToInvocation(snapshot, Guid.NewGuid()));
    }

    [TestMethod]
    public void TheBuildGateClearsOnlyWhenEveryInvocationIsRepaired()
    {
        var member = Member("Run");
        var required = Member("Speed", QuickWindowDataType.Integer, required: true);
        var definition = Definition("motor", members: [member, required], interfaceVersion: 2);
        var first = Caller(definition, QuickWindowBinding.FromTag(member.MemberKey, "motor.run")) with
        {
            InterfaceVersion = 1
        };
        var second = Caller(definition, QuickWindowBinding.FromTag(member.MemberKey, "motor.run")) with
        {
            InterfaceVersion = 1,
            OwnerElementId = "caller-2"
        };
        var snapshot = Snapshot(definition, first, second);
        var controller = new QuickWindowWorkspaceController(new RepairHost());
        var bindings = new[]
        {
            QuickWindowBinding.FromTag(member.MemberKey, "motor.run"),
            QuickWindowBinding.FromLiteral(required.MemberKey, "12")
        };

        var afterFirst = controller.RepairInvocation(snapshot, first.InvocationKey, bindings);
        Assert.AreEqual(CommandResultStatus.Succeeded, afterFirst.Result.Status);
        Assert.AreEqual(
            1,
            controller.ListOutdatedInvocations(afterFirst.After, definition.DefinitionKey).Count,
            "repairing one invocation never repairs the others in bulk");
        Assert.IsTrue(
            ScadaProjectBuildValidator.Validate(afterFirst.After.Project, [Page("page")])
                .Any(issue => issue.Code == "quick-window.interface-version-incompatible"),
            "the build gate stays closed while one invocation is still outdated");

        var afterSecond = controller.RepairInvocation(afterFirst.After, second.InvocationKey, bindings);

        Assert.AreEqual(CommandResultStatus.Succeeded, afterSecond.Result.Status);
        Assert.AreEqual(0, controller.ListOutdatedInvocations(afterSecond.After, definition.DefinitionKey).Count);
        Assert.IsFalse(
            ScadaProjectBuildValidator.Validate(afterSecond.After.Project, [Page("page")])
                .Any(issue => issue.Code == "quick-window.interface-version-incompatible"),
            "the build gate clears only once every invocation is repaired");
    }

    [TestMethod]
    public void ARepairIsOneUndoableTransitionOfTheSingleWorkspaceStack()
    {
        var member = Member("Run");
        var required = Member("Speed", QuickWindowDataType.Integer, required: true);
        var definition = Definition("motor", members: [member, required], interfaceVersion: 2);
        var invocation = Caller(definition, QuickWindowBinding.FromTag(member.MemberKey, "motor.run")) with
        {
            InterfaceVersion = 1
        };
        var snapshot = Snapshot(definition, invocation);
        var controller = new QuickWindowWorkspaceController(new RepairHost());

        var repaired = controller.RepairInvocation(
            snapshot,
            invocation.InvocationKey,
            [
                QuickWindowBinding.FromTag(member.MemberKey, "motor.run"),
                QuickWindowBinding.FromLiteral(required.MemberKey, "12")
            ]);

        Assert.AreSame(snapshot, repaired.Before, "the before snapshot stays the reversible baseline");
        Assert.AreEqual(1, repaired.Before.Project.EffectiveQuickWindowInvocations.First().InterfaceVersion);
        Assert.AreEqual(2, repaired.After.Project.EffectiveQuickWindowInvocations.First().InterfaceVersion);
    }

    [TestMethod]
    public async Task AnInterfaceChangeThatOutdatesInvocationsIsConfirmedWithTheirCountFirst()
    {
        var member = Member("Run");
        var definition = Definition("motor", members: [member]);
        var invocation = Caller(definition, QuickWindowBinding.FromTag(member.MemberKey, "motor.run"));
        var snapshot = Snapshot(definition, invocation);

        var declining = new RepairHost { ConfirmInterfaceVersionImpact = false };
        var cancelled = await new QuickWindowWorkspaceController(declining).ApplyInlineInterfaceEditAsync(
            snapshot,
            definition.DefinitionKey,
            member with { DataType = QuickWindowDataType.Integer });

        Assert.IsNull(cancelled, "an interface change is never applied when the operator refuses its impact");
        Assert.AreEqual(1, declining.ImpactConfirmations.Count);
        Assert.AreEqual(1, declining.ImpactConfirmations[0].Count, "the impacted invocation count is shown before applying");

        var accepting = new RepairHost { ConfirmInterfaceVersionImpact = true };
        var applied = await new QuickWindowWorkspaceController(accepting).ApplyInlineInterfaceEditAsync(
            snapshot,
            definition.DefinitionKey,
            member with { DataType = QuickWindowDataType.Integer });

        Assert.IsNotNull(applied);
        Assert.AreEqual(CommandResultStatus.Succeeded, applied!.Result.Status);
        Assert.AreEqual(2, applied.After.Project.EffectiveQuickWindows.Single().InterfaceVersion);
    }

    [TestMethod]
    public void AnInterfaceChangeThatBreaksNothingNeverInterruptsTheOperator()
    {
        var member = Member("Run");
        var definition = Definition("motor", members: [member]);
        var invocation = Caller(definition, QuickWindowBinding.FromTag(member.MemberKey, "motor.run"));
        var snapshot = Snapshot(definition, invocation);

        var impact = QuickWindowWorkspaceController.PreviewOutdatedImpact(
            snapshot,
            definition,
            definition with { InterfaceMembers = [member with { Name = "RunFeedback" }] });

        Assert.AreEqual(0, impact.Count, "a rename breaks no invocation");
    }

    [TestMethod]
    public void TheRepairSurfaceExposesItsColumnsAndItsExplicitActions()
    {
        var xaml = ReadAppFile(Path.Combine("QuickWindows", "QuickWindowInvocationRepairDialog.xaml"));

        foreach (var column in new[] { "Page", "Élément appelant", "Commande", "Interface", "Motif" })
        {
            StringAssert.Contains(xaml, $"Header=\"{column}\"");
        }

        StringAssert.Contains(xaml, "x:Name=\"NavigateToCallerButton\"");
        StringAssert.Contains(xaml, "x:Name=\"RepairInvocationButton\"");
        StringAssert.Contains(xaml, "QuickWindowBindingsEditor x:Name=\"RepairBindingsEditor\"");

        var panel = ReadAppFile(Path.Combine("QuickWindows", "QuickWindowInterfacePanel.xaml"));
        StringAssert.Contains(panel, "x:Name=\"RepairInvocationsButton\"");

        var dialog = ReadAppFile(Path.Combine("QuickWindows", "QuickWindowInvocationRepairDialog.xaml.cs"));
        Assert.IsFalse(
            dialog.Contains("RepairAll", StringComparison.Ordinal),
            "no automatic nor silent bulk repair may exist");
    }

    private sealed class RepairHost : IQuickWindowWorkspaceHost
    {
        public List<string> Statuses { get; } = [];

        public List<IReadOnlyList<QuickWindowOutdatedInvocation>> ImpactConfirmations { get; } = [];

        public bool ConfirmInterfaceVersionImpact { get; init; } = true;

        public Task ActivateQuickWindowAsync(QuickWindowEditorContext context, QuickWindowDefinition definition) => Task.CompletedTask;

        public Task<string?> RequestQuickWindowNameAsync(string title, string proposedName) => Task.FromResult<string?>(proposedName);

        public Task<bool> ConfirmQuickWindowDeletionAsync(QuickWindowDefinition definition, IReadOnlyList<QuickWindowUsage> usages) =>
            Task.FromResult(usages.Count == 0);

        public Task<QuickWindowInterfaceMember?> RequestInterfaceMemberAsync(
            string title,
            QuickWindowInterfaceMemberDraft draft,
            IReadOnlyList<QuickWindowInterfaceMember> siblings) => Task.FromResult<QuickWindowInterfaceMember?>(null);

        public Task<bool> ConfirmInterfaceMemberDeletionAsync(
            QuickWindowInterfaceMember member,
            IReadOnlyList<QuickWindowUsage> usages) => Task.FromResult(false);

        public Task<QuickWindowPasteDecision> ResolveQuickWindowPasteAsync(QuickWindowClipboardAnalysis analysis) =>
            Task.FromResult(QuickWindowPasteDecision.Cancel);

        public Task<bool> ConfirmInterfaceVersionImpactAsync(
            QuickWindowDefinition definition,
            IReadOnlyList<QuickWindowOutdatedInvocation> impacted)
        {
            ImpactConfirmations.Add(impacted);
            return Task.FromResult(ConfirmInterfaceVersionImpact);
        }

        public void ReportQuickWindowStatus(string message) => Statuses.Add(message);
    }

    private static QuickWindowInvocation Caller(QuickWindowDefinition definition, params QuickWindowBinding[] bindings) =>
        new(
            Guid.NewGuid(),
            definition.DefinitionKey,
            bindings,
            InterfaceVersion: definition.InterfaceVersion,
            OwnerPageKey: PageKeyFor("page"),
            OwnerElementId: "caller",
            OwnerCommandId: "open");

    private static string ReadAppFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.App", relativePath);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate src/ScadaBuilderV2.App/{relativePath}.");
        return string.Empty;
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
