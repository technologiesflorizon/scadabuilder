using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Application.Clipboard;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Locks the fail-closed page ↔ quick-window clipboard boundary: refusal by default with an object and
/// property diagnostic, only `Annuler` or `Coller sans liaisons`, stripped references left unbound and
/// never promoted, and the same rule for a duplicated Element+ or a library component.
/// Decisions: DEC-0050, FR-031, FR-034, FR-036, FR-UI-23. Plan: Task 3.5.
/// </summary>
[TestClass]
public sealed class QuickWindowClipboardTests
{
    private static readonly Guid DefinitionKey = Guid.Parse("11112222-3333-4444-5555-666677778888");
    private static readonly Guid OtherDefinitionKey = Guid.Parse("99990000-1111-2222-3333-444455556666");
    private static readonly Guid SpeedKey = Guid.Parse("aaaa1111-bbbb-2222-cccc-333344445555");
    private static readonly Guid ForeignPortKey = Guid.Parse("bbbb2222-cccc-3333-dddd-444455556666");
    private static readonly Guid InvocationKey = Guid.Parse("cccc3333-dddd-4444-eeee-555566667777");

    [TestMethod]
    public void APageTagPastedIntoAQuickWindowIsRefusedWithItsObjectAndProperty()
    {
        var element = ScadaElement.CreateText("gauge", "Jauge", 10, 20) with
        {
            StateConfig = ScadaElementStateConfig.Default with
            {
                ReadVariable = new ScadaReadVariableRule("motor.speed", DisplayFormat: "0.0")
            }
        };

        var analysis = QuickWindowClipboardValidator.Analyze(
            [element],
            QuickWindowClipboardTarget.ForQuickWindow(Definition()),
            Project());

        Assert.IsFalse(analysis.IsAllowed);
        var issue = analysis.Issues.Single();
        Assert.AreEqual(QuickWindowClipboardIssueKind.ProjectTagInQuickWindow, issue.Kind);
        Assert.AreEqual("gauge", issue.ElementId);
        Assert.AreEqual("Jauge", issue.ElementName);
        Assert.AreEqual("StateConfig.ReadVariable.TagId", issue.PropertyPath);
        Assert.AreEqual("motor.speed", issue.Reference);
        StringAssert.Contains(issue.Message, "tag physique");
    }

    [TestMethod]
    public void ALocalInterfaceMemberPastedOnAPageIsRefused()
    {
        var element = ScadaElement.CreateText("label", "Etiquette", 0, 0) with
        {
            StateConfig = ScadaElementStateConfig.Default with
            {
                States =
                [
                    new ScadaStateRule("rule-1", "Marche", true, ScadaExpression.FromSource("{Speed} > 10"), ScadaEffectBlock.Empty)
                ]
            }
        };

        var analysis = QuickWindowClipboardValidator.Analyze(
            [element],
            QuickWindowClipboardTarget.ForPage(),
            Project());

        Assert.IsFalse(analysis.IsAllowed);
        var issue = analysis.Issues.Single();
        Assert.AreEqual(QuickWindowClipboardIssueKind.LocalMemberOutsideDefinition, issue.Kind);
        Assert.AreEqual("StateConfig.States[rule-1].Expression", issue.PropertyPath);
        Assert.AreEqual("Speed", issue.Reference);
        StringAssert.Contains(issue.Message, "Interface locale");
    }

    [TestMethod]
    public void APortOfAnotherDefinitionIsRefusedInTheTargetDefinition()
    {
        var element = ScadaElement.CreateText("value", "Valeur", 0, 0) with
        {
            StateConfig = ScadaElementStateConfig.Default with
            {
                ReadVariable = new ScadaReadVariableRule("Pressure")
            }
        };

        var analysis = QuickWindowClipboardValidator.Analyze(
            [element],
            QuickWindowClipboardTarget.ForQuickWindow(Definition()),
            Project());

        var issue = analysis.Issues.Single();
        Assert.AreEqual(QuickWindowClipboardIssueKind.ForeignDefinitionPort, issue.Kind);
        StringAssert.Contains(issue.Message, "Pompe");
    }

    [TestMethod]
    public void AReferenceThatResolvesNowhereIsRefusedInAQuickWindow()
    {
        var element = ScadaElement.CreateText("ghost", "Fantome", 0, 0) with
        {
            StateConfig = ScadaElementStateConfig.Default with { ReadVariable = new ScadaReadVariableRule("unknown.tag") }
        };

        var analysis = QuickWindowClipboardValidator.Analyze(
            [element],
            QuickWindowClipboardTarget.ForQuickWindow(Definition()),
            Project());

        Assert.AreEqual(QuickWindowClipboardIssueKind.UnresolvedReference, analysis.Issues.Single().Kind);
    }

    [TestMethod]
    public void AMemberOfTheTargetInterfaceAndAPageTagOnAPageAreBothAccepted()
    {
        var local = ScadaElement.CreateText("ok", "Ok", 0, 0) with
        {
            StateConfig = ScadaElementStateConfig.Default with { ReadVariable = new ScadaReadVariableRule("Speed") }
        };
        var page = ScadaElement.CreateText("ok2", "Ok2", 0, 0) with
        {
            StateConfig = ScadaElementStateConfig.Default with { ReadVariable = new ScadaReadVariableRule("motor.speed") }
        };

        Assert.IsTrue(QuickWindowClipboardValidator
            .Analyze([local], QuickWindowClipboardTarget.ForQuickWindow(Definition()), Project()).IsAllowed);
        Assert.IsTrue(QuickWindowClipboardValidator
            .Analyze([page], QuickWindowClipboardTarget.ForPage(), Project()).IsAllowed);
    }

    [TestMethod]
    public void ACallerCopiedElsewhereNeverSharesItsInvocation()
    {
        var caller = Caller("copy-of-caller");

        var analysis = QuickWindowClipboardValidator.Analyze(
            [caller],
            QuickWindowClipboardTarget.ForPage(),
            Project());

        var issue = analysis.Issues.Single();
        Assert.AreEqual(QuickWindowClipboardIssueKind.DuplicatedInvocation, issue.Kind);
        Assert.AreEqual("CommandConfig.Commands[open].QuickWindowInvocationKey", issue.PropertyPath);
        StringAssert.Contains(issue.Message, "caller");

        var stripped = QuickWindowClipboardValidator.StripRefusedReferences([caller], analysis).Single();
        Assert.IsNull(
            stripped.EffectiveCommandConfig.Commands.Single().QuickWindowInvocationKey,
            "the pasted caller keeps its command but loses the shared invocation");
    }

    [TestMethod]
    public void AnInvocationWhoseTargetIsGoneIsRefused()
    {
        var caller = Caller("caller");
        var project = Project() with { QuickWindowInvocations = [] };

        var analysis = QuickWindowClipboardValidator.Analyze(
            [caller],
            QuickWindowClipboardTarget.ForPage(),
            project,
            new HashSet<string>(StringComparer.Ordinal) { "caller" });

        Assert.AreEqual(QuickWindowClipboardIssueKind.MissingInvocationTarget, analysis.Issues.Single().Kind);
    }

    [TestMethod]
    public void StrippingRemovesEveryRefusedReferenceAndPromotesNothing()
    {
        var definition = Definition();
        var element = ScadaElement.CreateText("mixed", "Mixte", 0, 0) with
        {
            Data = new ScadaElementData(null, null, null, null, null, null, null, null, null, false, ReadTagId: "motor.speed"),
            StateConfig = ScadaElementStateConfig.Default with
            {
                ReadVariable = new ScadaReadVariableRule("motor.speed"),
                States =
                [
                    new ScadaStateRule("rule-1", "Marche", true, ScadaExpression.FromSource("{Pressure} > 2"), ScadaEffectBlock.Empty)
                ]
            },
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding("write", "Ecrire", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag, WriteTagId: "motor.run")
            ])
        };

        var target = QuickWindowClipboardTarget.ForQuickWindow(definition);
        var analysis = QuickWindowClipboardValidator.Analyze([element], target, Project());
        Assert.AreEqual(4, analysis.Issues.Count);

        var stripped = QuickWindowClipboardValidator.StripRefusedReferences([element], analysis).Single();

        Assert.IsNull(stripped.Data!.ReadTagId);
        Assert.IsNull(stripped.StateConfig!.ReadVariable, "a refused read variable is left unbound");
        Assert.AreEqual(0, stripped.StateConfig.States.Count, "a rule whose expression is refused is removed, never rewritten");
        Assert.IsNull(stripped.EffectiveCommandConfig.Commands.Single().WriteTagId);
        Assert.AreEqual(
            definition.EffectiveInterfaceMembers.Count,
            Definition().EffectiveInterfaceMembers.Count,
            "no refused reference is promoted into the local interface");
        Assert.IsTrue(
            QuickWindowClipboardValidator.Analyze([stripped], target, Project()).IsAllowed,
            "the stripped payload leaves no orphan reference behind");
    }

    [TestMethod]
    public void ChildrenOfAPastedGroupAreValidatedToo()
    {
        var child = ScadaElement.CreateText("child", "Enfant", 0, 0) with
        {
            StateConfig = ScadaElementStateConfig.Default with { ReadVariable = new ScadaReadVariableRule("motor.speed") }
        };
        var group = ScadaElement.CreateText("group", "Groupe", 0, 0) with { Children = [child] };

        var analysis = QuickWindowClipboardValidator.Analyze(
            [group],
            QuickWindowClipboardTarget.ForQuickWindow(Definition()),
            Project());

        Assert.AreEqual("child", analysis.Issues.Single().ElementId);
        var stripped = QuickWindowClipboardValidator.StripRefusedReferences([group], analysis).Single();
        Assert.IsNull(stripped.ChildElements.Single().StateConfig!.ReadVariable);
    }

    [TestMethod]
    public async Task TheOperatorOnlyEverGetsCancelOrPasteWithoutBindings()
    {
        var element = ScadaElement.CreateText("gauge", "Jauge", 0, 0) with
        {
            StateConfig = ScadaElementStateConfig.Default with { ReadVariable = new ScadaReadVariableRule("motor.speed") }
        };
        var target = QuickWindowClipboardTarget.ForQuickWindow(Definition());

        var cancelling = new RecordingHost { PasteDecision = QuickWindowPasteDecision.Cancel };
        var cancelled = await new QuickWindowWorkspaceController(cancelling).PreparePasteAsync([element], target, Project());

        Assert.IsFalse(cancelled.IsAllowed);
        Assert.AreEqual(0, cancelled.Elements.Count);
        Assert.AreEqual(1, cancelling.PasteAnalyses.Count, "the refused references are shown before anything is inserted");
        Assert.IsTrue(cancelling.Statuses.Any(status => status.Contains("annulé", StringComparison.Ordinal)));

        var accepting = new RecordingHost { PasteDecision = QuickWindowPasteDecision.PasteWithoutBindings };
        var pasted = await new QuickWindowWorkspaceController(accepting).PreparePasteAsync([element], target, Project());

        Assert.IsTrue(pasted.IsAllowed);
        Assert.IsTrue(pasted.WasStripped);
        Assert.IsNull(pasted.Elements.Single().StateConfig!.ReadVariable);
        Assert.IsTrue(accepting.Statuses.Any(status => status.Contains("sans liaisons", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task APayloadThatCrossesNoBoundaryIsPastedWithoutAnyDialog()
    {
        var host = new RecordingHost { PasteDecision = QuickWindowPasteDecision.Cancel };
        var element = ScadaElement.CreateText("plain", "Simple", 0, 0);

        var plan = await new QuickWindowWorkspaceController(host)
            .PreparePasteAsync([element], QuickWindowClipboardTarget.ForPage(), Project());

        Assert.IsTrue(plan.IsAllowed);
        Assert.IsFalse(plan.WasStripped);
        Assert.AreEqual(0, host.PasteAnalyses.Count, "an operation that honours the boundary never interrupts the operator");
    }

    [TestMethod]
    public void TheSharedClipboardCarriesItsOriginAndDetectsEveryCrossing()
    {
        var clipboard = new SceneClipboard();
        var pageKey = Guid.NewGuid();
        var element = ScadaElement.CreateText("text-1", "Texte", 0, 0);

        clipboard.Copy([element], SceneClipboardOrigin.ForPage(pageKey));
        Assert.IsTrue(clipboard.HasContent);
        Assert.IsFalse(clipboard.CrossesQuickWindowBoundary(targetIsQuickWindow: false, pageKey));
        Assert.IsTrue(clipboard.CrossesQuickWindowBoundary(targetIsQuickWindow: true, DefinitionKey));

        clipboard.Copy([element], SceneClipboardOrigin.ForQuickWindow(DefinitionKey));
        Assert.IsFalse(clipboard.CrossesQuickWindowBoundary(targetIsQuickWindow: true, DefinitionKey));
        Assert.IsTrue(clipboard.CrossesQuickWindowBoundary(targetIsQuickWindow: true, OtherDefinitionKey), "two definitions are two contexts");
        Assert.IsTrue(clipboard.CrossesQuickWindowBoundary(targetIsQuickWindow: false, pageKey));
    }

    [TestMethod]
    public void ALibraryComponentInstantiatedOnAQuickWindowCanvasFollowsTheSameRule()
    {
        // A .sep component reaches the canvas as plain Element+ objects, so it goes through the same gate.
        var component = ScadaElement.CreateText("lib-root", "Composant", 0, 0) with
        {
            Children =
            [
                ScadaElement.CreateText("lib-value", "Valeur", 0, 0) with
                {
                    Data = new ScadaElementData(null, null, null, null, null, null, null, null, null, false, ReadTagId: "motor.speed")
                }
            ]
        };

        var analysis = QuickWindowClipboardValidator.Analyze(
            [component],
            QuickWindowClipboardTarget.ForQuickWindow(Definition()),
            Project());

        Assert.AreEqual(QuickWindowClipboardIssueKind.ProjectTagInQuickWindow, analysis.Issues.Single().Kind);
        Assert.AreEqual("Data.ReadTagId", analysis.Issues.Single().PropertyPath);
    }

    private sealed class RecordingHost : IQuickWindowWorkspaceHost
    {
        public List<string> Statuses { get; } = [];

        public List<QuickWindowClipboardAnalysis> PasteAnalyses { get; } = [];

        public QuickWindowPasteDecision PasteDecision { get; init; }

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

        public Task<QuickWindowPasteDecision> ResolveQuickWindowPasteAsync(QuickWindowClipboardAnalysis analysis)
        {
            PasteAnalyses.Add(analysis);
            return Task.FromResult(PasteDecision);
        }

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

    private static ScadaElement Caller(string elementId) =>
        ScadaElement.CreateButton(elementId, elementId, 0, 0, ScadaButtonKind.Command) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding(
                    "open",
                    "Ouvrir",
                    true,
                    ScadaCommandTrigger.OnClick,
                    ScadaCommandKind.OpenQuickWindow,
                    QuickWindowInvocationKey: InvocationKey)
            ])
        };

    private static QuickWindowDefinition Definition() =>
        new(
            DefinitionKey,
            "moteur",
            "Moteur",
            1,
            new VisualContent(new CanvasSize(480, 320), Elements: []),
            [
                new QuickWindowInterfaceMember(SpeedKey, "Speed", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.Decimal, QuickWindowMemberAccess.Read)
            ],
            new QuickWindowPresentationDefaults());

    private static QuickWindowDefinition OtherDefinition() =>
        new(
            OtherDefinitionKey,
            "pompe",
            "Pompe",
            1,
            new VisualContent(new CanvasSize(480, 320), Elements: []),
            [
                new QuickWindowInterfaceMember(ForeignPortKey, "Pressure", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Decimal, QuickWindowMemberAccess.Read)
            ],
            new QuickWindowPresentationDefaults());

    private static ScadaProject Project() =>
        ScadaProject.CreateDefault("QuickWindowClipboard") with
        {
            ManifestVersion = "2.3",
            TagCatalog = new ScadaTagCatalog(
                "tf100web/1.0",
                [
                    new ScadaTagDefinition("motor.speed", "Motor speed", Datatype: "Decimal"),
                    new ScadaTagDefinition("motor.run", "Motor run", Datatype: "Boolean", Writeable: true)
                ]),
            QuickWindows = [Definition(), OtherDefinition()],
            QuickWindowInvocations =
            [
                new QuickWindowInvocation(
                    InvocationKey,
                    DefinitionKey,
                    [],
                    InterfaceVersion: 1,
                    OwnerPageKey: Guid.NewGuid(),
                    OwnerElementId: "caller",
                    OwnerCommandId: "open")
            ]
        };
}
