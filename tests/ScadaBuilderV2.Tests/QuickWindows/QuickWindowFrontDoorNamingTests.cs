using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Locks the quick-window naming front door: a free, human-readable name (accents, spaces, capitals) is
/// always accepted for creation/duplication/rename, the technical `Code` is derived and never re-typed by
/// the operator, a rename never touches the durable `Code`, and every refusal carries its diagnostic reason
/// instead of a bare "refused" line.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-UI-12. Reproduces the bug reported at the keyboard: naming a quick window with a
/// capital letter, a space or an accent was silently refused by <c>PageCodePolicy.Validate</c> because the
/// same typed text served as both the technical code and the display name.
/// Tests: this file exercises <see cref="QuickWindowWorkspaceController"/> directly, which is
/// `Compile Include`d into this test project precisely so this bug can be pinned with a real unit test
/// instead of a source-reading contract test.
/// </remarks>
[TestClass]
public sealed class QuickWindowFrontDoorNamingTests
{
    [TestMethod]
    public async Task CreateAsyncWithAnAccentedSpacedNameSucceedsAndDerivesTheCode()
    {
        var host = new RecordingHost { NameToReturn = "Fenêtre Condenseur" };
        var controller = new QuickWindowWorkspaceController(host);
        var snapshot = CreateSnapshot();

        var mutation = await controller.CreateAsync(snapshot);

        Assert.IsNotNull(mutation);
        Assert.AreEqual(CommandResultStatus.Succeeded, mutation!.Result.Status,
            "a human-readable name must not be refused: this is the bug reported at the keyboard");
        var definition = mutation.After.Project.EffectiveQuickWindows.Single();
        Assert.AreEqual("Fenêtre Condenseur", definition.DisplayName);
        Assert.AreEqual("fenetre_condenseur", definition.Code);
        Assert.IsFalse(host.Statuses.Any(status => status.Contains("refus", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task TwoCreationsWithTheSameReadableNameProduceTwoDefinitionsWithASuffixedCode()
    {
        var host = new RecordingHost { NameToReturn = "Fenêtre Condenseur" };
        var controller = new QuickWindowWorkspaceController(host);
        var snapshot = CreateSnapshot();

        var first = await controller.CreateAsync(snapshot);
        Assert.AreEqual(CommandResultStatus.Succeeded, first!.Result.Status);

        var second = await controller.CreateAsync(first.After);
        Assert.AreEqual(CommandResultStatus.Succeeded, second!.Result.Status);

        var definitions = second.After.Project.EffectiveQuickWindows;
        Assert.AreEqual(2, definitions.Count);
        Assert.IsTrue(definitions.All(definition => definition.DisplayName == "Fenêtre Condenseur"));
        var codes = definitions.Select(definition => definition.Code).OrderBy(code => code).ToArray();
        CollectionAssert.AreEqual(new[] { "fenetre_condenseur", "fenetre_condenseur_2" }, codes);
    }

    [TestMethod]
    public async Task RenameAsyncChangesOnlyTheDisplayNameAndKeepsTheTechnicalCode()
    {
        var host = new RecordingHost { NameToReturn = "Fenêtre Condenseur" };
        var controller = new QuickWindowWorkspaceController(host);
        var created = await controller.CreateAsync(CreateSnapshot());
        var definitionKey = created!.After.Project.EffectiveQuickWindows.Single().DefinitionKey;
        var codeBeforeRename = created.After.Project.EffectiveQuickWindows.Single().Code;

        host.NameToReturn = "Fenêtre Condenseur Halle B";
        var renamed = await controller.RenameAsync(created.After, definitionKey);

        Assert.IsNotNull(renamed);
        Assert.AreEqual(CommandResultStatus.Succeeded, renamed!.Result.Status);
        var definition = renamed.After.Project.EffectiveQuickWindows.Single();
        Assert.AreEqual("Fenêtre Condenseur Halle B", definition.DisplayName, "rename must change the display name");
        Assert.AreEqual(codeBeforeRename, definition.Code, "rename must NOT change the durable technical code");
        Assert.AreEqual("fenetre_condenseur", definition.Code);
    }

    [TestMethod]
    public async Task ARefusalMessageContainsTheDiagnosticReasonNotOnlyThePrefix()
    {
        // "con" is a display name that survives derivation unchanged (already lower-case, already a-z) and
        // collides with the reserved-Windows-device-name rule in PageCodePolicy.Validate. This drives a real
        // `Blocked` result out of the controller's own CreateAsync path, with a real diagnostic message, so
        // the assertion below cannot drift from what QuickWindowDefinitionService actually reports.
        var host = new RecordingHost { NameToReturn = "con" };
        var controller = new QuickWindowWorkspaceController(host);

        var mutation = await controller.CreateAsync(CreateSnapshot());

        Assert.IsNotNull(mutation);
        Assert.AreEqual(CommandResultStatus.Blocked, mutation!.Result.Status);
        var reason = mutation.Result.Diagnostics.First().Message;
        StringAssert.Contains(reason, "réservé", "the diagnostic itself must name the actual rule that failed");

        var reported = host.Statuses.Single();
        StringAssert.StartsWith(reported, "Création de la fenêtre rapide refusée", "the French prefix must be kept");
        StringAssert.Contains(reported, reason, "the reported status must carry the diagnostic reason, not just the prefix");
    }

    private sealed class RecordingHost : IQuickWindowWorkspaceHost
    {
        public string? NameToReturn { get; set; }

        public List<string> Statuses { get; } = [];

        public Task ActivateQuickWindowAsync(QuickWindowEditorContext context, QuickWindowDefinition definition) => Task.CompletedTask;

        public Task<string?> RequestQuickWindowNameAsync(string title, string proposedName) => Task.FromResult(NameToReturn);

        public Task<bool> ConfirmQuickWindowDeletionAsync(QuickWindowDefinition definition, IReadOnlyList<QuickWindowUsage> usages) =>
            Task.FromResult(true);

        public Task<bool> ConfirmParameterSheetDeletionAsync(ParameterSheet sheet, IReadOnlyList<ParameterSheetUsage> usages) =>
            Task.FromResult(true);

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
            IReadOnlyList<QuickWindowOutdatedInvocation> impacted) => Task.FromResult(true);

        public void ReportQuickWindowStatus(string message) => Statuses.Add(message);
    }

    private static PageWorkspaceSnapshot CreateSnapshot(params ScadaScene[] scenes)
    {
        var references = scenes.Select(scene => new ScadaSceneReference(
            scene.Id,
            scene.Title,
            $"scenes/{scene.PageKey:N}.scene.json",
            PageKey: scene.PageKey,
            PageCode: scene.EffectivePageCode)).ToArray();
        var project = ScadaProject.CreateDefault("QuickWindowFrontDoorNamingTests") with
        {
            ManifestVersion = "2.3",
            Scenes = references
        };
        return new PageWorkspaceSnapshot(1, project, scenes.ToDictionary(scene => scene.PageKey), []);
    }
}
