using ScadaBuilderV2.Domain.Projects;

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

    /// <summary>Gets the authorization policy applied by the command registry.</summary>
    public ICommandAuthorizationPolicy AuthorizationPolicy { get; }

    /// <summary>Gets the non-reentrant execution gate shared by command surfaces for this context.</summary>
    public CommandExecutionGate ExecutionGate { get; }

    /// <summary>Gets whether an application command currently owns the execution gate.</summary>
    public bool IsBusy => ExecutionGate.IsBusy;

    public SelectionState Selection { get; } = new();
}

public sealed class SelectionState
{
    private readonly List<string> _selectedElementIds = [];

    public IReadOnlyList<string> SelectedElementIds => _selectedElementIds;

    public string? PrimaryElementId { get; private set; }

    public bool IsSelectionLocked { get; private set; }

    public void SetSelection(IEnumerable<string> elementIds, string? primaryElementId, bool force = false)
    {
        if (IsSelectionLocked && !force)
        {
            return;
        }

        _selectedElementIds.Clear();
        foreach (var elementId in elementIds.Select(k => k.Trim()).Where(k => k.Length > 0).Distinct(StringComparer.Ordinal))
        {
            _selectedElementIds.Add(elementId);
        }

        PrimaryElementId = primaryElementId is { Length: > 0 } && _selectedElementIds.Contains(primaryElementId)
            ? primaryElementId
            : _selectedElementIds.FirstOrDefault();
    }

    public void SetSelectionLocked(bool locked)
    {
        IsSelectionLocked = locked;
    }
}
