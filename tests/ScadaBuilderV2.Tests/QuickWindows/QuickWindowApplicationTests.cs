using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

[TestClass]
public sealed class QuickWindowApplicationTests
{
    [TestMethod]
    public void DefinitionServiceCreatesUpdatesRoutesAndDeletesOnlyUnreferencedDefinitions()
    {
        var service = new QuickWindowDefinitionService();
        var snapshot = CreateSnapshot();

        var created = service.CreateEmpty(snapshot, "motor_window", "Motor");

        Assert.AreEqual(CommandResultStatus.Succeeded, created.Result.Status);
        Assert.IsTrue(created.Result.Changed);
        Assert.AreEqual(snapshot.Version + 1, created.After.Version);
        var definition = created.After.Project.EffectiveQuickWindows.Single();

        var renamed = service.Update(created.After, definition with { DisplayName = "Motor details" });
        Assert.AreEqual("Motor details", renamed.After.Project.EffectiveQuickWindows.Single().DisplayName);

        var member = new QuickWindowInterfaceMember(
            Guid.NewGuid(),
            "MotorName",
            QuickWindowInterfaceFamily.PublicParameter,
            QuickWindowDataType.String,
            QuickWindowMemberAccess.Read);
        var versionBlocked = service.Update(renamed.After, definition with
        {
            DisplayName = "Motor details",
            InterfaceMembers = [member]
        });
        Assert.AreEqual(CommandResultStatus.Blocked, versionBlocked.Result.Status);
        Assert.IsTrue(versionBlocked.Result.Diagnostics.Any(issue => issue.Code == "quick-window.interface-version-not-incremented"));

        var withInterface = service.Update(renamed.After, definition with
        {
            DisplayName = "Motor details",
            InterfaceVersion = 2,
            InterfaceMembers = [member]
        });
        Assert.AreEqual(CommandResultStatus.Succeeded, withInterface.Result.Status);

        var invocationKey = Guid.NewGuid();
        var caller = CreateCaller("caller", "open", invocationKey);
        var page = CreatePage("page", caller);
        var invocation = new QuickWindowInvocation(
            invocationKey,
            definition.DefinitionKey,
            [QuickWindowBinding.FromLiteral(member.MemberKey, "M101")],
            InterfaceVersion: 2,
            OwnerPageKey: page.PageKey,
            OwnerElementId: caller.Id,
            OwnerCommandId: "open");
        var referenced = AddQuickWindows(CreateSnapshot(page), [withInterface.After.Project.EffectiveQuickWindows.Single()], [invocation]);

        var usages = service.ListUsages(referenced, definition.DefinitionKey);
        Assert.AreEqual(1, usages.Count);
        Assert.AreEqual(QuickWindowUsageKind.PageCommand, usages[0].Kind);

        var navigation = service.NavigateToUsage(referenced, definition.DefinitionKey);
        Assert.AreEqual(page.PageKey, navigation.Result.PageToOpenKey);
        Assert.AreEqual(caller.Id, navigation.UsageToNavigate?.ElementId);

        var deletionBlocked = service.Delete(referenced, definition.DefinitionKey);
        Assert.AreEqual(CommandResultStatus.Blocked, deletionBlocked.Result.Status);
        Assert.IsTrue(deletionBlocked.Result.Diagnostics.All(issue => issue.Code == "quick-window.delete-dependency"));

        var unreferenced = AddQuickWindows(CreateSnapshot(), [definition], []);
        var deleted = service.Delete(unreferenced, definition.DefinitionKey);
        Assert.AreEqual(CommandResultStatus.Succeeded, deleted.Result.Status);
        Assert.AreEqual(0, deleted.After.Project.EffectiveQuickWindows.Count);
    }

    [TestMethod]
    public void DefinitionServiceRequiresConfirmationForReferencedMemberRemovalAndKeepsStableDiagnostic()
    {
        var member = ReadMember("Run", required: false);
        var definition = Definition("motor", members: [member], interfaceVersion: 1);
        var invocation = new QuickWindowInvocation(
            Guid.NewGuid(),
            definition.DefinitionKey,
            [QuickWindowBinding.FromTag(member.MemberKey, "motor.run")],
            InterfaceVersion: 1);
        var snapshot = AddQuickWindows(CreateSnapshot(), [definition], [invocation]);
        var candidate = definition with { InterfaceVersion = 2, InterfaceMembers = [] };
        var service = new QuickWindowDefinitionService();

        var blocked = service.Update(snapshot, candidate);

        Assert.AreEqual(CommandResultStatus.Blocked, blocked.Result.Status);
        Assert.IsTrue(blocked.Result.Diagnostics.Any(issue => issue.Code == "quick-window.interface-member-in-use"));

        var confirmed = service.Update(snapshot, candidate, confirmReferencedMemberRemoval: true);
        Assert.AreEqual(CommandResultStatus.Succeeded, confirmed.Result.Status);
        Assert.IsTrue(confirmed.Result.Diagnostics.Any(issue => issue.Code == "quick-window.port-removed"));
    }

    [TestMethod]
    public void DependencyAnalyzerRejectsCycleAndDepthThreeButAllowsPageToAToB()
    {
        var aKey = Guid.NewGuid();
        var bKey = Guid.NewGuid();
        var cKey = Guid.NewGuid();
        var pageInvocation = Invocation(Guid.NewGuid(), aKey);
        var ab = Invocation(Guid.NewGuid(), bKey);
        var bc = Invocation(Guid.NewGuid(), cKey);
        var page = CreatePage("page", CreateCaller("page-caller", "open-a", pageInvocation.InvocationKey));
        var definitionA = Definition("a", aKey, elements: [CreateCaller("a-caller", "open-b", ab.InvocationKey)]);
        var definitionB = Definition("b", bKey, elements: []);
        var depthTwo = AddQuickWindows(CreateSnapshot(page), [definitionA, definitionB], [pageInvocation, ab]);
        var analyzer = new QuickWindowDependencyAnalyzer();

        Assert.IsFalse(analyzer.Analyze(depthTwo).Diagnostics.Any(issue => issue.Code == "cycle/depth-exceeded"));

        definitionB = definitionB with
        {
            Content = definitionB.EffectiveContent with { Elements = [CreateCaller("b-caller", "open-c", bc.InvocationKey)] }
        };
        var definitionC = Definition("c", cKey);
        var depthThree = AddQuickWindows(CreateSnapshot(page), [definitionA, definitionB, definitionC], [pageInvocation, ab, bc]);
        var depthDiagnostics = analyzer.Analyze(depthThree).Diagnostics.Where(issue => issue.Code == "cycle/depth-exceeded").ToArray();
        Assert.AreEqual(1, depthDiagnostics.Length);
        StringAssert.Contains(depthDiagnostics[0].Message, "exceeds two");

        var ba = Invocation(Guid.NewGuid(), aKey);
        definitionB = definitionB with
        {
            Content = definitionB.EffectiveContent with { Elements = [CreateCaller("b-caller", "open-a", ba.InvocationKey)] }
        };
        var cycle = AddQuickWindows(CreateSnapshot(page), [definitionA, definitionB], [pageInvocation, ab, ba]);
        var cycleDiagnostics = analyzer.Analyze(cycle).Diagnostics.Where(issue => issue.Code == "cycle/depth-exceeded").ToArray();
        Assert.AreEqual(1, cycleDiagnostics.Length);
        StringAssert.Contains(cycleDiagnostics[0].Message, "cycle");
    }

    [TestMethod]
    public void DependencyAnalyzerReportsMissingDefinitionVersionAndRemovedPortDeterministically()
    {
        var removedMemberKey = Guid.NewGuid();
        var definition = Definition("motor", members: [], interfaceVersion: 2);
        var stale = new QuickWindowInvocation(
            Guid.NewGuid(),
            definition.DefinitionKey,
            [QuickWindowBinding.FromLiteral(removedMemberKey, "M101")],
            InterfaceVersion: 1);
        var missing = Invocation(Guid.NewGuid(), Guid.NewGuid());
        var snapshot = AddQuickWindows(CreateSnapshot(), [definition], [stale, missing]);
        var analyzer = new QuickWindowDependencyAnalyzer();

        var first = analyzer.Analyze(snapshot);
        var second = analyzer.Analyze(snapshot);

        CollectionAssert.AreEqual(first.Diagnostics.Select(issue => issue.Code).ToArray(), second.Diagnostics.Select(issue => issue.Code).ToArray());
        Assert.IsTrue(first.Diagnostics.Any(issue => issue.Code == "quick-window.definition-missing"));
        Assert.IsTrue(first.Diagnostics.Any(issue => issue.Code == "quick-window.interface-version-incompatible"));
        Assert.IsTrue(first.Diagnostics.Any(issue => issue.Code == "quick-window.port-removed"));
    }

    [TestMethod]
    public void InvocationServiceKeepsTwoCallersIndependentAndDeletesCallerAtomically()
    {
        var member = new QuickWindowInterfaceMember(
            Guid.NewGuid(),
            "MotorName",
            QuickWindowInterfaceFamily.PublicParameter,
            QuickWindowDataType.String,
            QuickWindowMemberAccess.Read);
        var definition = Definition("motor", members: [member]);
        var callerA = CreateCaller("caller-a", "open-a", null);
        var callerB = CreateCaller("caller-b", "open-b", null);
        var page = CreatePage("page", callerA, callerB);
        var snapshot = AddQuickWindows(CreateSnapshot(page), [definition], []);
        var service = new QuickWindowInvocationService();

        var first = service.Upsert(snapshot, new UpsertQuickWindowInvocationRequest(
            page.PageKey,
            callerA.Id,
            "open-a",
            definition.DefinitionKey,
            [QuickWindowBinding.FromLiteral(member.MemberKey, "M101")]));
        var second = service.Upsert(first.After, new UpsertQuickWindowInvocationRequest(
            page.PageKey,
            callerB.Id,
            "open-b",
            definition.DefinitionKey,
            [QuickWindowBinding.FromLiteral(member.MemberKey, "M102")]));

        Assert.AreEqual(2, second.After.Project.EffectiveQuickWindowInvocations.Count);
        var invocationA = second.After.Project.EffectiveQuickWindowInvocations.Single(item => item.OwnerElementId == callerA.Id);
        var invocationB = second.After.Project.EffectiveQuickWindowInvocations.Single(item => item.OwnerElementId == callerB.Id);
        Assert.AreNotEqual(invocationA.InvocationKey, invocationB.InvocationKey);
        Assert.AreNotSame(invocationA.Bindings, invocationB.Bindings);
        Assert.AreEqual("M101", invocationA.Bindings.Single().LiteralValue);
        Assert.AreEqual("M102", invocationB.Bindings.Single().LiteralValue);
        Assert.AreEqual(invocationA.InvocationKey, second.After.Scenes[page.PageKey].FindElementRecursive(callerA.Id)!
            .EffectiveCommandConfig.Commands.Single().QuickWindowInvocationKey);

        var updatedA = service.Upsert(second.After, new UpsertQuickWindowInvocationRequest(
            page.PageKey,
            callerA.Id,
            "open-a",
            definition.DefinitionKey,
            [QuickWindowBinding.FromLiteral(member.MemberKey, "M101-updated")],
            invocationA.InvocationKey));
        Assert.AreEqual("M102", updatedA.After.Project.EffectiveQuickWindowInvocations.Single(item => item.InvocationKey == invocationB.InvocationKey).Bindings.Single().LiteralValue);

        var deleted = service.DeleteCaller(updatedA.After, page.PageKey, callerA.Id);
        Assert.IsNull(deleted.After.Scenes[page.PageKey].FindElementRecursive(callerA.Id));
        Assert.IsNotNull(deleted.After.Scenes[page.PageKey].FindElementRecursive(callerB.Id));
        Assert.IsFalse(deleted.After.Project.EffectiveQuickWindowInvocations.Any(item => item.InvocationKey == invocationA.InvocationKey));
        Assert.IsTrue(deleted.After.Project.EffectiveQuickWindowInvocations.Any(item => item.InvocationKey == invocationB.InvocationKey));
    }

    private static PageWorkspaceSnapshot CreateSnapshot(params ScadaScene[] scenes)
    {
        var references = scenes.Select(scene => new ScadaSceneReference(
            scene.Id,
            scene.Title,
            $"scenes/{scene.PageKey:N}.scene.json",
            PageKey: scene.PageKey,
            PageCode: scene.EffectivePageCode)).ToArray();
        var project = ScadaProject.CreateDefault("QuickWindowTests") with
        {
            ManifestVersion = "2.3",
            Scenes = references
        };
        return new PageWorkspaceSnapshot(1, project, scenes.ToDictionary(scene => scene.PageKey), []);
    }

    private static PageWorkspaceSnapshot AddQuickWindows(
        PageWorkspaceSnapshot snapshot,
        IReadOnlyList<QuickWindowDefinition> definitions,
        IReadOnlyList<QuickWindowInvocation> invocations) =>
        snapshot with
        {
            Project = snapshot.Project with
            {
                QuickWindows = definitions,
                QuickWindowInvocations = invocations
            }
        };

    private static ScadaScene CreatePage(string code, params ScadaElement[] elements)
    {
        var pageKey = Guid.NewGuid();
        return elements.Aggregate(
            ScadaScene.CreateEmpty(code, code, CanvasSize.DefaultDesktop) with
            {
                PageKey = pageKey,
                PageCode = code
            },
            (scene, element) => scene.WithElement(element));
    }

    private static ScadaElement CreateCaller(string elementId, string commandId, Guid? invocationKey)
    {
        var command = new ScadaCommandBinding(
            commandId,
            commandId,
            true,
            ScadaCommandTrigger.OnClick,
            ScadaCommandKind.OpenQuickWindow,
            QuickWindowInvocationKey: invocationKey);
        return ScadaElement.CreateButton(elementId, elementId, 10, 20, ScadaButtonKind.Command) with
        {
            CommandConfig = new ScadaElementCommandConfig([command])
        };
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

    private static QuickWindowInvocation Invocation(Guid invocationKey, Guid definitionKey) =>
        new(invocationKey, definitionKey, [], InterfaceVersion: 1);

    private static QuickWindowInterfaceMember ReadMember(string name, bool required) =>
        new(
            Guid.NewGuid(),
            name,
            QuickWindowInterfaceFamily.ReadState,
            QuickWindowDataType.Boolean,
            QuickWindowMemberAccess.Read,
            required);
}
