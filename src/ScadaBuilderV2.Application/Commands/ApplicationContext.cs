using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Application.Commands;

public sealed class ApplicationContext
{
    public ScadaProject? CurrentProject { get; set; }

    public string? ActiveSceneId { get; set; }

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
