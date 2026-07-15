using ScadaBuilderV2.App.ElementLock;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Selection;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App;

// High-level WPF adaptation only; lock aggregation, recursive mutation and transform validation live in Application/Domain.
public partial class MainWindow
{
    private readonly ElementLockCoordinator _elementLockCoordinator = new();
    private readonly ElementTransformGuard _elementTransformGuard = new();

    /// <summary>Gets the shared state bound by the Selection ribbon, Properties checkbox and top Lock indicator.</summary>
    public ElementLockStateViewModel ElementLockState { get; } = new();

    private void RefreshElementLockState()
    {
        if (_activeScene is null)
        {
            ElementLockState.Update(ElementLockSelectionState.Empty);
            return;
        }
        ElementLockState.Update(_elementLockCoordinator.BuildState(_activeScene, _selectedSceneObjectIds));
    }

    private async void OnElementLockToggleClick(object sender, System.Windows.RoutedEventArgs e) => await ToggleSelectedElementLockAsync();

    private async Task ToggleSelectedElementLockAsync()
    {
        if (_activeScene is null || _selectedSceneObjectIds.Count == 0)
        {
            SetStatus("Selectionnez au moins un Element+.");
            return;
        }
        var context = new ApplicationContext
        {
            ActiveSceneSnapshot = _activeScene,
            ActiveSceneHistory = _activeSceneTab?.History,
            ApplyActiveSceneMutation = scene => _activeScene = scene
        };
        context.Selection.SetSelection(_selectedSceneObjectIds, _selectedSceneObject?.Id);
        var result = await _applicationCommandRegistry.ExecuteAsync("object.lock", context);
        if (result.Changed && context.ActiveSceneSnapshot is not null)
        {
            _activeScene = context.ActiveSceneSnapshot;
            _selectedSceneObject = _selectedSceneObject is null ? null : _activeScene.FindElementRecursive(_selectedSceneObject.Id);
            MarkActiveSceneDirty();
            RefreshSelectionUi();
            RefreshModernSceneUi();
            await RenderModernSceneAsync();
        }
        SetStatus(result.Message);
    }

    private bool CanApplyElementTransform(ScadaScene proposed, IEnumerable<string> targetIds)
    {
        if (_activeScene is null || _elementTransformGuard.CanApply(_activeScene, proposed, targetIds, out var reason)) return true;
        SetStatus(reason ?? "Transformation refusee: Element+ verrouille.");
        _ = RenderModernSceneAsync();
        return false;
    }
}
