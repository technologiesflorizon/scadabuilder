using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Application.Selection;

namespace ScadaBuilderV2.Application.Commands;

/// <summary>Toggles persistent position lock for the active Element+ selection closure.</summary>
/// <remarks>Decisions: DEC-0040. Tests: tests/ScadaBuilderV2.Tests/ApplicationCommandTests.cs.</remarks>
public sealed class ToggleElementLockCommand : IApplicationCommand
{
    private readonly ElementLockCoordinator coordinator = new();
    public string Id => "object.lock";
    public string DisplayName => "Verrouiller";

    public bool CanExecute(ApplicationContext context) =>
        context.ActiveSceneSnapshot is not null && context.Selection.SelectedElementIds.Count > 0;

    public Task<CommandResult> ExecuteAsync(ApplicationContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scene = context.ActiveSceneSnapshot!;
        var mutation = coordinator.Toggle(scene, context.Selection.SelectedElementIds);
        if (!mutation.Changed) return Task.FromResult(CommandResult.NoChange("Aucun Element+ a verrouiller."));
        context.ApplyActiveSceneMutation?.Invoke(mutation.AfterScene);
        context.ActiveSceneSnapshot = mutation.AfterScene;
        context.ActiveSceneHistory?.Push(new ElementLockChangedAction(scene.Id, mutation.Items, scene.PageKey == Guid.Empty ? null : scene.PageKey));
        return Task.FromResult(CommandResult.Success(mutation.Items.All(item => item.After) ? "Selection verrouillee." : "Selection deverrouillee.", workspaceDirty: true));
    }
}
