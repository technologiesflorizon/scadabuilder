using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Commands;

public sealed class ApplicationContext
{
    /// <summary>Creates an application context with permissive authorization and a local execution gate.</summary>
    public ApplicationContext(
        ICommandAuthorizationPolicy? authorizationPolicy = null,
        CommandExecutionGate? executionGate = null)
    {
        AuthorizationPolicy = authorizationPolicy ?? AllowAllCommandAuthorizationPolicy.Instance;
        ExecutionGate = executionGate ?? new CommandExecutionGate();
    }

    public ScadaProject? CurrentProject { get; set; }

    public string? ActiveSceneId { get; set; }

    /// <summary>Gets or sets the page selected in the project surface.</summary>
    public Guid? SelectedPageKey { get; set; }

    /// <summary>Gets or sets the page currently active in the editor.</summary>
    public Guid? ActiveEditorPageKey { get; set; }

    /// <summary>Gets or sets the logical home page key in the current workspace.</summary>
    public Guid? HomePageKey { get; set; }

    /// <summary>Gets or sets the coherent in-memory page workspace consumed by page commands.</summary>
    public PageWorkspaceSnapshot? PageWorkspace { get; set; }

    /// <summary>Gets or sets the typed request prepared by the invoking page-command surface.</summary>
    public PageCommandRequest? PageCommandRequest { get; set; }

    /// <summary>Gets or sets the restorable UI state associated with the page workspace.</summary>
    public ProjectWorkspaceUiSnapshot? PageWorkspaceUi { get; set; }

    /// <summary>Gets or sets the shared polymorphic workspace history.</summary>
    public EditorHistoryService? WorkspaceHistory { get; set; }

    /// <summary>Gets or sets the callback that atomically applies a successful in-memory page mutation.</summary>
    public Action<PageWorkspaceMutation>? ApplyPageWorkspaceMutation { get; set; }

    /// <summary>Gets or sets whether project-level page metadata or inventory has unsaved changes.</summary>
    public bool IsPageWorkspaceDirty { get; set; }

    /// <summary>Gets the authorization policy applied by the command registry.</summary>
    public ICommandAuthorizationPolicy AuthorizationPolicy { get; }

    /// <summary>Gets the non-reentrant execution gate shared by command surfaces for this context.</summary>
    public CommandExecutionGate ExecutionGate { get; }

    /// <summary>Gets whether an application command currently owns the execution gate.</summary>
    public bool IsBusy => ExecutionGate.IsBusy;

    /// <summary>Gets or sets the active scene snapshot consumed by scene-object commands.</summary>
    public ScadaScene? ActiveSceneSnapshot { get; set; }

    /// <summary>Gets or sets the callback that commits an immutable active-scene mutation.</summary>
    public Action<ScadaScene>? ApplyActiveSceneMutation { get; set; }

    /// <summary>Gets or sets the active scene history used by scene-object commands.</summary>
    public EditorHistoryService? ActiveSceneHistory { get; set; }

    public SelectionState Selection { get; } = new();
}

public sealed class SelectionState
{
    private readonly List<string> _selectedElementIds = [];

    public IReadOnlyList<string> SelectedElementIds => _selectedElementIds;

    public string? PrimaryElementId { get; private set; }

    public void SetSelection(IEnumerable<string> elementIds, string? primaryElementId)
    {
        _selectedElementIds.Clear();
        foreach (var elementId in elementIds.Select(k => k.Trim()).Where(k => k.Length > 0).Distinct(StringComparer.Ordinal))
        {
            _selectedElementIds.Add(elementId);
        }

        PrimaryElementId = primaryElementId is { Length: > 0 } && _selectedElementIds.Contains(primaryElementId)
            ? primaryElementId
            : _selectedElementIds.FirstOrDefault();
    }

}
