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
/// Locks the quick-window caller authoring surface: `OpenQuickWindow` with a definition target and no page
/// target, `CloseQuickWindow(Self)` only inside a quick-window content, the conditional `Liaisons` tab, the
/// typed columns and source selectors (FR-UI-18, FR-UI-19), the muted/flagged statuses (FR-UI-20) and the
/// `Outdated` display that keeps build and export blocked (FR-UI-24).
/// Decisions: DEC-0050. Plan: Task 3.3.
/// </summary>
[TestClass]
public sealed class QuickWindowBindingAuthoringTests
{
    private static readonly Guid DefinitionKey = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RunningKey = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid SetpointKey = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid PrivateKey = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid ParentPortKey = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid InvocationKey = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    [TestMethod]
    public void APageOpensADefinitionAndNeverClosesAQuickWindow()
    {
        var page = new QuickWindowCommandAuthoringContext([Definition()], []);

        CollectionAssert.AreEqual(new[] { ScadaCommandKind.OpenQuickWindow }, page.AllowedCommandKinds().ToArray());
        Assert.IsTrue(page.CanOpenQuickWindow);
        Assert.IsFalse(page.CanCloseQuickWindow, "a page can never close a quick window");
        Assert.AreEqual(0, page.EffectiveParentPorts.Count, "a page has no parent port to forward");
        Assert.AreEqual(0, QuickWindowCommandAuthoringContext.Empty.AllowedCommandKinds().Count);
    }

    [TestMethod]
    public void AQuickWindowContentOffersCloseSelfWithoutAnyFreeTarget()
    {
        var content = new QuickWindowCommandAuthoringContext([Definition()], Definition().EffectiveInterfaceMembers, IsQuickWindowContent: true);

        CollectionAssert.AreEqual(
            new[] { ScadaCommandKind.OpenQuickWindow, ScadaCommandKind.CloseQuickWindow },
            content.AllowedCommandKinds().ToArray());
        Assert.AreEqual(2, content.EffectiveParentPorts.Count, "only public ports may be forwarded");
        Assert.IsFalse(content.EffectiveParentPorts.Any(port => port.MemberKey == PrivateKey));

        var xaml = ReadAppFile("ElementCommandDialog.xaml");
        StringAssert.Contains(xaml, "x:Name=\"CloseQuickWindowPanel\"");
        StringAssert.Contains(xaml, "x:Name=\"QuickWindowDefinitionComboBox\"");

        var source = ReadAppFile("ElementCommandDialog.xaml.cs");
        StringAssert.Contains(source, "CloseQuickWindowPanel.Visibility = kind == ScadaCommandKind.CloseQuickWindow");
        StringAssert.Contains(
            source,
            "TargetPageId: kind == ScadaCommandKind.Navigate",
            "a quick-window command must never carry a page target");
        Assert.IsFalse(
            source.Contains("ToggleQuickWindow", StringComparison.Ordinal),
            "no Toggle quick-window kind exists");
    }

    [TestMethod]
    public void TheLiaisonsTabIsShownOnlyForAnOpenQuickWindowCommand()
    {
        var xaml = ReadAppFile("ElementPropertiesDialog.xaml");
        StringAssert.Contains(xaml, "x:Name=\"QuickWindowBindingsTab\"");
        StringAssert.Contains(xaml, "Header=\"Liaisons\"");
        StringAssert.Contains(xaml, "Visibility=\"Collapsed\"");
        StringAssert.Contains(xaml, "x:Name=\"QuickWindowBindingsEditorControl\"");
        StringAssert.Contains(xaml, "SelectionChanged=\"OnCommandSelectionChanged\"");

        var source = ReadAppFile("ElementPropertiesDialog.xaml.cs");
        StringAssert.Contains(source, "selected?.Kind == ScadaCommandKind.OpenQuickWindow");
        StringAssert.Contains(source, "QuickWindowBindingsTab.Visibility = Visibility.Collapsed;");
        StringAssert.Contains(source, "QuickWindowBindingsEditorControl.Clear();");
    }

    [TestMethod]
    public void TheGridExposesTheTypedColumnsOfEveryPublicPort()
    {
        var editor = new QuickWindowBindingsEditorViewModel();

        editor.Load(Definition(), null);

        Assert.AreEqual(2, editor.Rows.Count, "private members are never bindable by an invocation");
        var running = editor.Rows.Single(row => row.MemberKey == RunningKey);
        Assert.AreEqual("Running", running.Name);
        Assert.AreEqual("État lu", running.FamilyLabel);
        Assert.AreEqual("Booléen", running.DataTypeLabel);
        Assert.AreEqual("Aucune", running.SourceLabel);
        Assert.AreEqual(string.Empty, running.ValueOrReference);
        Assert.AreEqual("Non lié", running.StatusLabel);

        var xaml = ReadAppFile(Path.Combine("QuickWindows", "QuickWindowBindingsEditor.xaml"));
        foreach (var column in new[] { "Nom", "Famille", "Type", "Source", "Valeur / Référence", "Statut" })
        {
            StringAssert.Contains(xaml, $"Header=\"{column}\"");
        }
    }

    [TestMethod]
    public void ASourceKindIsChosenFirstAndItsContextualValueIsTypedNotFreeText()
    {
        var editor = new QuickWindowBindingsEditorViewModel();
        editor.Load(Definition(), null, Catalog(), [ParentPort()]);
        var setpoint = editor.Rows.Single(row => row.MemberKey == SetpointKey);

        CollectionAssert.AreEqual(
            new[] { "Aucune", "Tag", "Littéral", "Expression", "Port parent" },
            setpoint.SourceOptions.Select(option => option.Label).ToArray());

        setpoint.SourceKind = QuickWindowBindingSourceKind.Tag;
        setpoint.ValueOrReference = "motor.speed";
        Assert.IsTrue(setpoint.UsesTagSelector);
        Assert.IsFalse(setpoint.UsesTextValue);
        Assert.AreEqual("motor.speed", setpoint.ToBinding().TagId);

        setpoint.SourceKind = QuickWindowBindingSourceKind.Literal;
        Assert.AreEqual(string.Empty, setpoint.ValueOrReference, "changing the source clears the previous typed value");
        Assert.IsNull(setpoint.ToBinding().TagId);
        Assert.IsTrue(setpoint.UsesTextValue);

        setpoint.SourceKind = QuickWindowBindingSourceKind.ParentPort;
        setpoint.SelectedParentPort = ParentPort();
        Assert.IsTrue(setpoint.UsesParentPortSelector);
        Assert.AreEqual(ParentPortKey, setpoint.ToBinding().ParentMemberKey);
        Assert.AreEqual("ParentSpeed", setpoint.ValueOrReference);

        var pageRow = new QuickWindowBindingRowViewModel(Definition().EffectiveInterfaceMembers[1], null);
        Assert.IsFalse(
            pageRow.SourceOptions.Any(option => option.Kind == QuickWindowBindingSourceKind.ParentPort),
            "a page caller has no parent port to forward");
    }

    [TestMethod]
    public void AnUnboundOptionalPortIsMutedAndAnUnboundRequiredPortIsFlagged()
    {
        var editor = new QuickWindowBindingsEditorViewModel();
        editor.Load(Definition(), null, Catalog());

        var required = editor.Rows.Single(row => row.MemberKey == RunningKey);
        var optional = editor.Rows.Single(row => row.MemberKey == SetpointKey);

        Assert.AreEqual(QuickWindowBindingRowViewModel.UnboundRequiredToken, required.StatusToken);
        Assert.IsTrue(required.IsBlocking);
        Assert.AreEqual(QuickWindowBindingRowViewModel.UnboundOptionalToken, optional.StatusToken);
        Assert.IsTrue(optional.IsUnboundOptional);
        Assert.IsFalse(optional.IsBlocking);
        Assert.AreEqual(1, editor.UnboundRequiredCount);

        optional.SourceKind = QuickWindowBindingSourceKind.Literal;
        optional.ValueOrReference = "12";
        Assert.AreEqual(QuickWindowBindingRowViewModel.BoundToken, optional.StatusToken);
        Assert.AreEqual("Lié", optional.StatusLabel);
    }

    [TestMethod]
    public void RequiredPortsAreValidatedBeforeTheBindingsAreSaved()
    {
        var editor = new QuickWindowBindingsEditorViewModel();
        editor.Load(Definition(), null, Catalog());

        var issues = editor.Validate();

        Assert.IsTrue(issues.Any(issue => issue.Contains("Running", StringComparison.Ordinal)));
        StringAssert.Contains(editor.ValidationMessage, "Running");

        editor.Rows.Single(row => row.MemberKey == RunningKey).SourceKind = QuickWindowBindingSourceKind.Tag;
        editor.Rows.Single(row => row.MemberKey == RunningKey).ValueOrReference = "motor.run";
        Assert.AreEqual(0, editor.Validate().Count);

        var request = editor.ToRequest("open");
        Assert.IsNotNull(request);
        Assert.AreEqual(1, request!.Bindings.Count, "a neutral absence is never persisted as a binding");
        Assert.AreEqual(DefinitionKey, request.DefinitionKey);
        Assert.AreEqual("open", request.CommandId);
    }

    [TestMethod]
    public void AnOutdatedInvocationIsFlaggedInTheGridAndKeepsBuildAndExportBlocked()
    {
        var definition = Definition() with { InterfaceVersion = 2 };
        var stale = new QuickWindowInvocation(
            InvocationKey,
            DefinitionKey,
            [QuickWindowBinding.FromTag(Guid.Parse("12345678-1234-1234-1234-123456789012"), "ghost.tag")],
            InterfaceVersion: 1);
        var editor = new QuickWindowBindingsEditorViewModel();

        editor.Load(definition, stale, Catalog());

        Assert.IsTrue(editor.IsOutdated);
        StringAssert.Contains(editor.OutdatedLabel, "réparer");
        var orphan = editor.Rows.Single(row => row.IsOrphan);
        Assert.AreEqual(QuickWindowBindingRowViewModel.OutdatedToken, orphan.StatusToken);
        Assert.AreEqual("Port retiré", orphan.StatusLabel);
        Assert.IsTrue(orphan.IsBlocking);
        Assert.AreEqual(
            QuickWindowBindingRowViewModel.OutdatedToken,
            editor.Rows.Single(row => row.MemberKey == RunningKey).StatusToken,
            "an unbound required port of an outdated invocation is shown as to repair");

        var project = ScadaProject.CreateDefault("QuickWindowBindings") with
        {
            ManifestVersion = "2.3",
            QuickWindows = [definition],
            QuickWindowInvocations = [stale]
        };
        var issues = ScadaProjectBuildValidator.Validate(project, []);
        Assert.IsTrue(
            issues.Any(issue => issue.Code == "quick-window.interface-version-incompatible" && issue.Severity == ScadaBuildValidationSeverity.Error),
            "export stays blocked while an invocation is outdated");
    }

    [TestMethod]
    public void SavingBindingsPersistsTheInvocationAndItsCallerCommandInOneTransition()
    {
        var host = new RecordingHost();
        var controller = new QuickWindowWorkspaceController(host);
        var (snapshot, pageKey) = SnapshotWithCaller();

        var mutation = controller.SaveInvocation(snapshot, pageKey, "caller", new QuickWindowInvocationAuthoringRequest(
            "open",
            DefinitionKey,
            [QuickWindowBinding.FromTag(RunningKey, "motor.run"), QuickWindowBinding.FromLiteral(SetpointKey, "42")],
            TitleOverride: "Moteur 1"));

        Assert.AreEqual(CommandResultStatus.Succeeded, mutation.Result.Status);
        Assert.AreSame(snapshot, mutation.Before, "the before snapshot stays the reversible baseline");
        var invocation = mutation.After.Project.EffectiveQuickWindowInvocations.Single();
        Assert.AreEqual(DefinitionKey, invocation.DefinitionKey);
        Assert.AreEqual(2, invocation.Bindings.Count);
        Assert.AreEqual("Moteur 1", invocation.EffectiveTitleOverride);
        Assert.AreEqual(1, invocation.InterfaceVersion);
        Assert.AreEqual(pageKey, invocation.OwnerPageKey);
        Assert.AreEqual("caller", invocation.OwnerElementId);
        Assert.AreEqual("open", invocation.OwnerCommandId);

        var command = mutation.After.Scenes[pageKey].FindElementRecursive("caller")!.EffectiveCommandConfig.Commands.Single();
        Assert.AreEqual(invocation.InvocationKey, command.QuickWindowInvocationKey, "the caller command carries the invocation key");
        Assert.AreEqual(0, snapshot.Project.EffectiveQuickWindowInvocations.Count, "the source snapshot is never mutated");
        Assert.IsTrue(host.Statuses.Any(status => status.Contains("enregistrées", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TwoInvocationsOfTheSameDefinitionKeepIndependentBindings()
    {
        var controller = new QuickWindowWorkspaceController(new RecordingHost());
        var (snapshot, pageKey) = SnapshotWithCaller("first", "second");

        var first = controller.SaveInvocation(snapshot, pageKey, "first", new QuickWindowInvocationAuthoringRequest(
            "open",
            DefinitionKey,
            [QuickWindowBinding.FromTag(RunningKey, "motor1.run")]));
        var second = controller.SaveInvocation(first.After, pageKey, "second", new QuickWindowInvocationAuthoringRequest(
            "open",
            DefinitionKey,
            [QuickWindowBinding.FromTag(RunningKey, "motor2.run")]));

        Assert.AreEqual(CommandResultStatus.Succeeded, second.Result.Status);
        var invocations = second.After.Project.EffectiveQuickWindowInvocations;
        Assert.AreEqual(2, invocations.Count);
        Assert.AreNotEqual(invocations[0].InvocationKey, invocations[1].InvocationKey);
        CollectionAssert.AreEquivalent(
            new[] { "motor1.run", "motor2.run" },
            invocations.Select(invocation => invocation.Bindings.Single().TagId).ToArray());
    }

    [TestMethod]
    public void DeletingTheCallerRemovesItsInvocationAtomically()
    {
        var controller = new QuickWindowWorkspaceController(new RecordingHost());
        var (snapshot, pageKey) = SnapshotWithCaller();
        var saved = controller.SaveInvocation(snapshot, pageKey, "caller", new QuickWindowInvocationAuthoringRequest(
            "open",
            DefinitionKey,
            [QuickWindowBinding.FromTag(RunningKey, "motor.run")]));

        var deleted = controller.DeleteCaller(saved.After, pageKey, "caller");

        Assert.AreEqual(CommandResultStatus.Succeeded, deleted.Result.Status);
        Assert.AreEqual(0, deleted.After.Project.EffectiveQuickWindowInvocations.Count);
        Assert.IsNull(deleted.After.Scenes[pageKey].FindElementRecursive("caller"));

        var removed = controller.RemoveInvocation(saved.After, saved.AffectedInvocationKey!.Value);
        Assert.AreEqual(CommandResultStatus.Succeeded, removed.Result.Status);
        Assert.AreEqual(0, removed.After.Project.EffectiveQuickWindowInvocations.Count);
        Assert.IsNull(
            removed.After.Scenes[pageKey].FindElementRecursive("caller")!.EffectiveCommandConfig.Commands.Single().QuickWindowInvocationKey,
            "removing an invocation clears exactly the command that owned it");
    }

    [TestMethod]
    public void TheEditorNeverWritesToTheWorkspaceItself()
    {
        var source = ReadAppFile(Path.Combine("QuickWindows", "QuickWindowBindingsEditor.xaml.cs"));

        foreach (var forbidden in new[] { "QuickWindowInvocationService", "PageWorkspaceSnapshot", "ModernProjectStore" })
        {
            Assert.IsFalse(
                source.Contains(forbidden, StringComparison.Ordinal),
                $"'{forbidden}' is workspace orchestration and must stay outside the bindings editor.");
        }

        StringAssert.Contains(source, "SaveRequested?.Invoke(this, request)");
    }

    private sealed class RecordingHost : IQuickWindowWorkspaceHost
    {
        public List<string> Statuses { get; } = [];

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

        public List<IReadOnlyList<QuickWindowOutdatedInvocation>> ImpactConfirmations { get; } = [];

        public bool ConfirmInterfaceVersionImpact { get; init; } = true;

        public Task<bool> ConfirmInterfaceVersionImpactAsync(
            QuickWindowDefinition definition,
            IReadOnlyList<QuickWindowOutdatedInvocation> impacted)
        {
            ImpactConfirmations.Add(impacted);
            return Task.FromResult(ConfirmInterfaceVersionImpact);
        }

        public void ReportQuickWindowStatus(string message) => Statuses.Add(message);
    }

    private static QuickWindowInterfaceMember ParentPort() =>
        new(ParentPortKey, "ParentSpeed", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.Decimal, QuickWindowMemberAccess.Read);

    private static QuickWindowDefinition Definition() =>
        new(
            DefinitionKey,
            "motor",
            "Motor",
            1,
            new VisualContent(new CanvasSize(480, 320), Elements: []),
            [
                new QuickWindowInterfaceMember(RunningKey, "Running", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read, Required: true),
                new QuickWindowInterfaceMember(SetpointKey, "Setpoint", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.Decimal, QuickWindowMemberAccess.Read),
                new QuickWindowInterfaceMember(PrivateKey, "Counter", QuickWindowInterfaceFamily.PrivateVariable, QuickWindowDataType.Integer, QuickWindowMemberAccess.Internal)
            ],
            new QuickWindowPresentationDefaults());

    private static ScadaTagCatalog Catalog() =>
        new(
            "tf100web/1.0",
            [
                new ScadaTagDefinition("motor.run", "Motor run", Datatype: "Boolean", Writeable: true),
                new ScadaTagDefinition("motor.speed", "Motor speed", Datatype: "Decimal")
            ]);

    private static (PageWorkspaceSnapshot Snapshot, Guid PageKey) SnapshotWithCaller(params string[] callerIds)
    {
        var ids = callerIds.Length == 0 ? ["caller"] : callerIds;
        var pageKey = Guid.NewGuid();
        var page = ScadaScene.CreateEmpty("win00003", "win00003", CanvasSize.DefaultDesktop) with
        {
            PageKey = pageKey,
            PageCode = "win00003"
        };
        foreach (var id in ids)
        {
            var command = new ScadaCommandBinding(
                "open",
                "open",
                true,
                ScadaCommandTrigger.OnClick,
                ScadaCommandKind.OpenQuickWindow);
            page = page.WithElement(ScadaElement.CreateButton(id, id, 10, 20, ScadaButtonKind.Command) with
            {
                CommandConfig = new ScadaElementCommandConfig([command])
            });
        }

        var project = ScadaProject.CreateDefault("QuickWindowBindings") with
        {
            ManifestVersion = "2.3",
            Scenes =
            [
                new ScadaSceneReference(page.Id, page.Title, $"scenes/{pageKey:N}.scene.json", PageKey: pageKey, PageCode: page.EffectivePageCode)
            ],
            QuickWindows = [Definition()],
            QuickWindowInvocations = []
        };
        return (new PageWorkspaceSnapshot(1, project, new Dictionary<Guid, ScadaScene> { [pageKey] = page }, []), pageKey);
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
