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
/// Locks the `Interface locale` authoring surface: single grouped table with family filters (FR-UI-15),
/// inline edition of the common properties with a shared dialog for the advanced ones (FR-UI-16),
/// per-member usage counters with navigation and confirmed referenced deletion (FR-UI-17), muted/flagged
/// unbound ports (FR-UI-20) and selectors restricted to local members (FR-031).
/// Decisions: DEC-0050. Plan: Task 3.2.
/// </summary>
[TestClass]
public sealed class QuickWindowInterfaceAuthoringTests
{
    private static readonly Guid DefinitionKey = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid RunningKey = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid SetpointKey = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid CounterKey = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid InvocationKey = Guid.Parse("88888888-8888-8888-8888-888888888888");

    [TestMethod]
    public void SingleTableGroupsPublicInterfaceAndPrivateDataWithCounters()
    {
        var panel = new QuickWindowInterfacePanelViewModel();

        panel.Load(Definition());

        Assert.AreEqual("Interface locale", panel.Title);
        Assert.AreEqual(3, panel.Members.Count);
        Assert.AreEqual(2, panel.PublicCount);
        Assert.AreEqual(1, panel.PrivateCount);
        CollectionAssert.AreEqual(
            new[]
            {
                QuickWindowInterfaceMemberViewModel.PublicGroupLabel,
                QuickWindowInterfaceMemberViewModel.PublicGroupLabel,
                QuickWindowInterfaceMemberViewModel.PrivateGroupLabel
            },
            panel.Members.Select(member => member.GroupLabel).ToArray(),
            "the public interface must be grouped first in the single table");
        StringAssert.Contains(panel.DefinitionLabel, "interface v1");
    }

    [TestMethod]
    public void FamilyAndTextFiltersNarrowTheSameTable()
    {
        var panel = new QuickWindowInterfacePanelViewModel();
        panel.Load(Definition());

        Assert.AreEqual(QuickWindowInterfacePanelViewModel.AllFamiliesLabel, panel.FamilyFilters[0].Label);
        Assert.IsNull(panel.FamilyFilters[0].Family);
        Assert.AreEqual(6, panel.FamilyFilters.Count, "every family plus the unfiltered option must be offered");

        panel.SelectedFamilyFilter = panel.FamilyFilters.Single(filter => filter.Family == QuickWindowInterfaceFamily.ReadState);
        Assert.AreEqual(1, panel.Members.Count);
        Assert.AreEqual("Running", panel.Members[0].Name);

        panel.SelectedFamilyFilter = panel.FamilyFilters[0];
        panel.SearchText = "setp";
        Assert.AreEqual(1, panel.Members.Count);
        Assert.AreEqual("Setpoint", panel.Members[0].Name);

        panel.SearchText = string.Empty;
        Assert.AreEqual(3, panel.Members.Count);
    }

    [TestMethod]
    public void CommonPropertiesAreInlineAndAdvancedOnesUseTheSharedDialog()
    {
        CollectionAssert.AreEqual(
            new[] { "Name", "Family", "DataType", "Access", "Required" },
            QuickWindowInterfaceMemberViewModel.InlineEditableProperties.ToArray());
        CollectionAssert.AreEqual(
            new[] { "DefaultValue", "Description" },
            QuickWindowInterfaceMemberViewModel.AdvancedProperties.ToArray());

        var panelXaml = ReadAppFile(Path.Combine("QuickWindows", "QuickWindowInterfacePanel.xaml"));
        foreach (var column in new[] { "Nom", "Famille", "Type", "Accès", "Requis", "Statut", "Utilisations" })
        {
            StringAssert.Contains(panelXaml, $"Header=\"{column}\"");
        }

        StringAssert.Contains(panelXaml, "PropertyGroupDescription PropertyName=\"GroupLabel\"");
        StringAssert.Contains(panelXaml, "x:Name=\"MemberFamilyFilterComboBox\"");
        StringAssert.Contains(panelXaml, "x:Name=\"MemberSearchTextBox\"");
        StringAssert.Contains(panelXaml, "Click=\"OnNavigateToUsageClick\"");

        var dialogXaml = ReadAppFile(Path.Combine("QuickWindows", "QuickWindowInterfaceMemberDialog.xaml"));
        foreach (var field in new[] { "NameTextBox", "FamilyComboBox", "DataTypeComboBox", "AccessComboBox", "RequiredCheckBox", "DefaultValueTextBox", "DescriptionTextBox" })
        {
            StringAssert.Contains(dialogXaml, $"x:Name=\"{field}\"", $"the shared member dialog must expose '{field}'.");
        }
    }

    [TestMethod]
    public async Task InlineEditRaisesTheCandidateMemberAndKeepsTypedRulesCoherent()
    {
        var host = new RecordingHost();
        var controller = new QuickWindowWorkspaceController(host);
        var snapshot = Snapshot();
        await controller.OpenAsync(snapshot, DefinitionKey);

        QuickWindowInterfaceMemberViewModel? edited = null;
        controller.InterfacePanel.MemberInlineEdited += (_, member) => edited = member;
        var running = controller.InterfacePanel.Members.Single(member => member.MemberKey == RunningKey);

        running.Family = QuickWindowInterfaceFamily.WriteCommand;

        Assert.IsNotNull(edited);
        Assert.AreEqual(RunningKey, edited!.MemberKey);
        Assert.AreEqual(QuickWindowMemberAccess.Write, edited.Access, "the family owns the access mode");
        Assert.AreEqual("Commande écrite", edited.FamilyLabel);
    }

    [TestMethod]
    public async Task InlineEditBumpsTheInterfaceVersionOnlyWhenThePublicContractChanges()
    {
        var host = new RecordingHost();
        var controller = new QuickWindowWorkspaceController(host);
        var snapshot = Snapshot();

        var renamed = controller.ApplyInlineInterfaceEdit(
            snapshot,
            DefinitionKey,
            Member(RunningKey, "RunningState", QuickWindowInterfaceFamily.ReadState, QuickWindowMemberAccess.Read, required: true));

        Assert.IsNotNull(renamed);
        Assert.AreEqual(CommandResultStatus.Succeeded, renamed!.Result.Status);
        var afterRename = Definition(renamed.After);
        Assert.AreEqual(1, afterRename.InterfaceVersion, "renaming a member never changes the public contract");
        Assert.AreEqual("RunningState", afterRename.EffectiveInterfaceMembers.Single(member => member.MemberKey == RunningKey).Name);

        var retyped = controller.ApplyInlineInterfaceEdit(
            renamed.After,
            DefinitionKey,
            Member(RunningKey, "RunningState", QuickWindowInterfaceFamily.ReadState, QuickWindowMemberAccess.Read, required: true) with
            {
                DataType = QuickWindowDataType.Integer
            });

        Assert.IsNotNull(retyped);
        Assert.AreEqual(CommandResultStatus.Succeeded, retyped!.Result.Status);
        Assert.AreEqual(2, Definition(retyped.After).InterfaceVersion, "a typed contract change requires a higher interface version");
    }

    [TestMethod]
    public void InlineEditIsRefusedWhenItBreaksADomainRule()
    {
        var host = new RecordingHost();
        var controller = new QuickWindowWorkspaceController(host);

        var refused = controller.ApplyInlineInterfaceEdit(
            Snapshot(),
            DefinitionKey,
            Member(RunningKey, "Setpoint", QuickWindowInterfaceFamily.ReadState, QuickWindowMemberAccess.Read));

        Assert.IsNull(refused, "a duplicate member name must never reach the workspace");
        Assert.IsTrue(host.Statuses.Any(status => status.Contains("refusée", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task EachMemberCarriesItsUsageCountAndNavigatesToEveryUsage()
    {
        var host = new RecordingHost();
        var controller = new QuickWindowWorkspaceController(host);
        var (snapshot, pageKey) = SnapshotWithCaller();
        await controller.OpenAsync(snapshot, DefinitionKey);

        var bound = controller.InterfacePanel.Members.Single(member => member.MemberKey == SetpointKey);
        var unbound = controller.InterfacePanel.Members.Single(member => member.MemberKey == RunningKey);

        Assert.AreEqual(1, bound.UsageCount);
        Assert.AreEqual("1 utilisation", bound.UsageLabel);
        Assert.IsTrue(bound.HasUsages);
        Assert.AreEqual(0, unbound.UsageCount);
        Assert.AreEqual("Aucune utilisation", unbound.UsageLabel);

        var navigation = controller.NavigateToMemberUsage(snapshot, DefinitionKey, SetpointKey);

        Assert.IsNotNull(navigation);
        Assert.IsFalse(navigation!.Result.Changed, "navigating must never mutate the workspace");
        Assert.AreEqual(pageKey, navigation.Result.PageToOpenKey);
        Assert.AreEqual(InvocationKey, navigation.UsageToNavigate?.InvocationKey);
        Assert.AreEqual(SetpointKey, navigation.UsageToNavigate?.MemberKey);

        Assert.IsNull(controller.NavigateToMemberUsage(snapshot, DefinitionKey, RunningKey));
    }

    [TestMethod]
    public async Task DeletingAReferencedMemberRequiresAnExplicitConfirmation()
    {
        var (snapshot, _) = SnapshotWithCaller();
        var declining = new RecordingHost { ConfirmMemberDeletion = false };
        var controller = new QuickWindowWorkspaceController(declining);

        var cancelled = await controller.DeleteInterfaceMemberAsync(snapshot, DefinitionKey, SetpointKey);

        Assert.IsNull(cancelled, "a referenced member is never removed without an explicit confirmation");
        Assert.AreEqual(1, declining.MemberDeletionConfirmations.Count);
        Assert.AreEqual(1, declining.MemberDeletionConfirmations[0].Usages.Count);

        var accepting = new RecordingHost { ConfirmMemberDeletion = true };
        var confirmedController = new QuickWindowWorkspaceController(accepting);
        var deleted = await confirmedController.DeleteInterfaceMemberAsync(snapshot, DefinitionKey, SetpointKey);

        Assert.IsNotNull(deleted);
        Assert.AreEqual(CommandResultStatus.Succeeded, deleted!.Result.Status);
        var after = Definition(deleted.After);
        Assert.IsFalse(after.EffectiveInterfaceMembers.Any(member => member.MemberKey == SetpointKey));
        Assert.AreEqual(2, after.InterfaceVersion, "removing a public port changes the contract");
        Assert.IsTrue(
            deleted.Result.Diagnostics.Any(issue => issue.Code == "quick-window.invocation-outdated"),
            "the invocation that bound the removed port must be reported as outdated");
    }

    [TestMethod]
    public async Task UnboundOptionalPortIsMutedAndUnboundRequiredPortIsFlagged()
    {
        var host = new RecordingHost();
        var controller = new QuickWindowWorkspaceController(host);
        var (snapshot, _) = SnapshotWithCaller();
        await controller.OpenAsync(snapshot, DefinitionKey);

        var boundOptional = controller.InterfacePanel.Members.Single(member => member.MemberKey == SetpointKey);
        var unboundRequired = controller.InterfacePanel.Members.Single(member => member.MemberKey == RunningKey);
        var privateMember = controller.InterfacePanel.Members.Single(member => member.MemberKey == CounterKey);

        Assert.AreEqual(QuickWindowInterfaceMemberViewModel.BoundToken, boundOptional.BindingStatusToken);
        Assert.AreEqual("Lié", boundOptional.BindingStatusLabel);
        Assert.AreEqual(QuickWindowInterfaceMemberViewModel.UnboundRequiredToken, unboundRequired.BindingStatusToken);
        Assert.IsTrue(unboundRequired.IsUnboundRequired);
        Assert.AreEqual("Non lié", unboundRequired.BindingStatusLabel);
        Assert.AreEqual(QuickWindowInterfaceMemberViewModel.UnboundOptionalToken, privateMember.BindingStatusToken);
        Assert.IsTrue(privateMember.IsUnboundOptional);
        Assert.AreEqual(1, controller.InterfacePanel.UnboundRequiredCount);
    }

    [TestMethod]
    public async Task AddingAMemberGoesThroughTheSharedDialogAndTheSingleWorkspaceStack()
    {
        var authored = new QuickWindowInterfaceMember(
            Guid.NewGuid(),
            "Pressure",
            QuickWindowInterfaceFamily.PublicParameter,
            QuickWindowDataType.Decimal,
            QuickWindowMemberAccess.Read);
        var host = new RecordingHost { MemberToAuthor = authored };
        var controller = new QuickWindowWorkspaceController(host);
        var snapshot = Snapshot();

        var mutation = await controller.AddInterfaceMemberAsync(snapshot, DefinitionKey);

        Assert.IsNotNull(mutation);
        Assert.AreEqual(CommandResultStatus.Succeeded, mutation!.Result.Status);
        Assert.AreEqual(DefinitionKey, mutation.AffectedDefinitionKey);
        Assert.AreSame(snapshot, mutation.Before, "the mutation must stay one reversible workspace transition");
        var after = Definition(mutation.After);
        Assert.AreEqual(4, after.EffectiveInterfaceMembers.Count);
        Assert.AreEqual(2, after.InterfaceVersion, "adding a public port changes the contract");
        Assert.AreEqual(1, host.MemberRequests.Count);
        StringAssert.StartsWith(host.MemberRequests[0].Draft.Name, "membre");
        Assert.AreEqual(3, host.MemberRequests[0].Siblings.Count);
    }

    [TestMethod]
    public void TheMemberDraftKeepsTypedRulesAndValidatesAgainstItsSiblings()
    {
        var draft = QuickWindowInterfaceMemberDraft.ForNew("Speed");
        Assert.IsTrue(draft.IsNew);

        draft.Family = QuickWindowInterfaceFamily.PrivateConstant;
        draft.Required = true;
        Assert.AreEqual(QuickWindowMemberAccess.Internal, draft.Access);
        Assert.IsFalse(draft.Required, "a private member can never be required");

        Assert.IsTrue(draft.Validate().Any(issue => issue.Contains("DefaultValue", StringComparison.Ordinal)));
        draft.DataType = QuickWindowDataType.Integer;
        draft.DefaultValue = "12";
        Assert.AreEqual(0, draft.Validate().Count);

        draft.Name = "Setpoint";
        var issues = draft.Validate(Definition().EffectiveInterfaceMembers);
        Assert.IsTrue(issues.Any(issue => issue.Contains("Duplicate member Name", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void SelectorsOfAQuickWindowExposeOnlyItsLocalInterfaceMembers()
    {
        var catalog = QuickWindowInterfaceCatalogProjection.Create(Definition());

        Assert.AreEqual(QuickWindowInterfaceCatalogProjection.Schema, catalog.Schema);
        Assert.AreEqual(3, catalog.Count);
        CollectionAssert.AreEquivalent(
            new[] { "Running", "Setpoint", "Counter" },
            catalog.Tags.Select(tag => tag.Id).ToArray());
        Assert.IsTrue(catalog.Tags.All(tag => tag.Device == QuickWindowInterfaceCatalogProjection.DeviceLabel));
        Assert.IsTrue(catalog.Tags.All(tag => string.IsNullOrEmpty(tag.AddressUri)), "no physical address can leak into a quick window");
        Assert.IsFalse(catalog.Tags.Single(tag => tag.Id == "Running").Writeable);
    }

    [TestMethod]
    public void TheShellReplacesTheProjectTagCatalogueByTheLocalInterfaceInAQuickWindowContext()
    {
        var xaml = ReadAppFile("MainWindow.xaml");
        StringAssert.Contains(xaml, "x:Name=\"QuickWindowInterfaceAnchorable\"");
        StringAssert.Contains(xaml, "Title=\"Interface locale\"");
        StringAssert.Contains(xaml, "x:Name=\"QuickWindowInterfacePanelControl\"");

        var wiring = ReadAppFile("MainWindow.QuickWindows.cs");
        StringAssert.Contains(wiring, "TagCatalogAnchorable.Hide();");
        StringAssert.Contains(wiring, "QuickWindowInterfaceAnchorable.Show();");
        StringAssert.Contains(wiring, "TagCatalogAnchorable.Show();");
        StringAssert.Contains(wiring, "QuickWindowInterfaceCatalogProjection.Create(definition)");

        var shell = ReadAppFile("MainWindow.xaml.cs");
        foreach (var selector in new[]
                 {
                     "new ElementStateRuleDialog(null, ActiveSelectorTagCatalog)",
                     "new ElementStateRuleDialog(selected, ActiveSelectorTagCatalog)",
                     "new ElementReadVariableDialog(element.EffectiveStateConfig.ReadVariable, ActiveSelectorTagCatalog)",
                     "ActiveSelectorTagCatalog, usedKinds)"
                 })
        {
            StringAssert.Contains(shell, selector, $"'{selector}' must consume the active context catalogue.");
        }

        Assert.IsFalse(
            shell.Contains("Dialog(null, _modernProject?.TagCatalog)", StringComparison.Ordinal),
            "no selector may bypass the active context catalogue");
    }

    private sealed record MemberRequest(string Title, QuickWindowInterfaceMemberDraft Draft, IReadOnlyList<QuickWindowInterfaceMember> Siblings);

    private sealed record MemberDeletionRequest(QuickWindowInterfaceMember Member, IReadOnlyList<QuickWindowUsage> Usages);

    private sealed class RecordingHost : IQuickWindowWorkspaceHost
    {
        public List<string> Statuses { get; } = [];

        public List<MemberRequest> MemberRequests { get; } = [];

        public List<MemberDeletionRequest> MemberDeletionConfirmations { get; } = [];

        public QuickWindowInterfaceMember? MemberToAuthor { get; init; }

        public bool ConfirmMemberDeletion { get; init; }

        public Task ActivateQuickWindowAsync(QuickWindowEditorContext context, QuickWindowDefinition definition) => Task.CompletedTask;

        public Task<string?> RequestQuickWindowNameAsync(string title, string proposedName) => Task.FromResult<string?>(proposedName);

        public Task<bool> ConfirmQuickWindowDeletionAsync(QuickWindowDefinition definition, IReadOnlyList<QuickWindowUsage> usages) =>
            Task.FromResult(usages.Count == 0);

        public Task<QuickWindowInterfaceMember?> RequestInterfaceMemberAsync(
            string title,
            QuickWindowInterfaceMemberDraft draft,
            IReadOnlyList<QuickWindowInterfaceMember> siblings)
        {
            MemberRequests.Add(new MemberRequest(title, draft, siblings));
            return Task.FromResult(MemberToAuthor);
        }

        public Task<bool> ConfirmInterfaceMemberDeletionAsync(
            QuickWindowInterfaceMember member,
            IReadOnlyList<QuickWindowUsage> usages)
        {
            MemberDeletionConfirmations.Add(new MemberDeletionRequest(member, usages));
            return Task.FromResult(ConfirmMemberDeletion);
        }

        public void ReportQuickWindowStatus(string message) => Statuses.Add(message);
    }

    private static QuickWindowInterfaceMember Member(
        Guid memberKey,
        string name,
        QuickWindowInterfaceFamily family,
        QuickWindowMemberAccess access,
        bool required = false,
        QuickWindowDataType dataType = QuickWindowDataType.Boolean,
        string? defaultValue = null) =>
        new(memberKey, name, family, dataType, access, required, defaultValue);

    private static QuickWindowDefinition Definition() =>
        new(
            DefinitionKey,
            "motor",
            "Motor",
            1,
            new VisualContent(new CanvasSize(480, 320), Elements: []),
            [
                Member(RunningKey, "Running", QuickWindowInterfaceFamily.ReadState, QuickWindowMemberAccess.Read, required: true),
                Member(SetpointKey, "Setpoint", QuickWindowInterfaceFamily.PublicParameter, QuickWindowMemberAccess.Read, dataType: QuickWindowDataType.Decimal),
                Member(CounterKey, "Counter", QuickWindowInterfaceFamily.PrivateVariable, QuickWindowMemberAccess.Internal, dataType: QuickWindowDataType.Integer)
            ],
            new QuickWindowPresentationDefaults());

    private static QuickWindowDefinition Definition(PageWorkspaceSnapshot snapshot) =>
        snapshot.Project.EffectiveQuickWindows.Single(definition => definition.DefinitionKey == DefinitionKey);

    private static PageWorkspaceSnapshot Snapshot(
        IReadOnlyList<QuickWindowInvocation>? invocations = null,
        params ScadaScene[] scenes)
    {
        var references = scenes.Select(scene => new ScadaSceneReference(
            scene.Id,
            scene.Title,
            $"scenes/{scene.PageKey:N}.scene.json",
            PageKey: scene.PageKey,
            PageCode: scene.EffectivePageCode)).ToArray();
        var project = ScadaProject.CreateDefault("QuickWindowInterfaceTests") with
        {
            ManifestVersion = "2.3",
            Scenes = references,
            QuickWindows = [Definition()],
            QuickWindowInvocations = invocations ?? []
        };
        return new PageWorkspaceSnapshot(1, project, scenes.ToDictionary(scene => scene.PageKey), []);
    }

    private static (PageWorkspaceSnapshot Snapshot, Guid PageKey) SnapshotWithCaller()
    {
        var command = new ScadaCommandBinding(
            "open",
            "open",
            true,
            ScadaCommandTrigger.OnClick,
            ScadaCommandKind.OpenQuickWindow,
            QuickWindowInvocationKey: InvocationKey);
        var caller = ScadaElement.CreateButton("caller", "caller", 10, 20, ScadaButtonKind.Command) with
        {
            CommandConfig = new ScadaElementCommandConfig([command])
        };
        var pageKey = Guid.NewGuid();
        var page = ScadaScene.CreateEmpty("win00003", "win00003", CanvasSize.DefaultDesktop) with
        {
            PageKey = pageKey,
            PageCode = "win00003"
        };
        page = page.WithElement(caller);
        var invocation = new QuickWindowInvocation(
            InvocationKey,
            DefinitionKey,
            [QuickWindowBinding.FromLiteral(SetpointKey, "42")],
            InterfaceVersion: 1,
            OwnerPageKey: pageKey,
            OwnerElementId: "caller",
            OwnerCommandId: "open");
        return (Snapshot([invocation], page), pageKey);
    }

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
}
