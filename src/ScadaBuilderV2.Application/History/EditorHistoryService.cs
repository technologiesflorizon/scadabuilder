using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.History;

/// <summary>One reversible editor mutation targeting a scene or the project workspace.</summary>
public interface IEditorHistoryAction
{
    /// <summary>Gets the polymorphic target of the mutation.</summary>
    EditorHistoryTarget Target { get; }

    /// <summary>Gets the user-facing action label.</summary>
    string Label { get; }

    /// <summary>Returns whether this action can merge with the next action.</summary>
    bool CanMergeWith(IEditorHistoryAction next);

    /// <summary>Merges this action with a compatible following action.</summary>
    IEditorHistoryAction MergeWith(IEditorHistoryAction next);

    /// <summary>Restores state before this mutation.</summary>
    Task UndoAsync(EditorHistoryContext context);

    /// <summary>Restores state after this mutation.</summary>
    Task RedoAsync(EditorHistoryContext context);
}

/// <summary>Callback boundary used by history actions without referencing WPF or WebView.</summary>
public sealed class EditorHistoryContext
{
    public required string ActiveSceneId { get; init; }

    public Guid? ActivePageKey { get; init; }

    public required Func<ScadaScene?> GetActiveScene { get; init; }

    public required Action<ScadaScene> ReplaceActiveScene { get; init; }

    public Func<Guid, ScadaScene?>? GetSceneByPageKey { get; init; }

    public Func<string, ScadaScene?>? GetSceneById { get; init; }

    public Action<Guid, ScadaScene>? ReplaceSceneByPageKey { get; init; }

    public Action<Guid>? RemoveSceneByPageKey { get; init; }

    public Func<IReadOnlyCollection<Guid>>? GetWorkspaceSceneKeys { get; init; }

    public Func<ScadaProject?>? GetProject { get; init; }

    public Action<ScadaProject>? ReplaceProject { get; init; }

    public Action<ProjectWorkspaceUiSnapshot>? RestoreWorkspaceUi { get; init; }

    public Action<IReadOnlyList<Guid>>? SetPendingDeletedPageKeys { get; init; }

    public Action<bool>? SetWorkspaceDirty { get; init; }

    public required Action MarkDirty { get; init; }

    public required Func<Task> RefreshPreviewAsync { get; init; }

    public Func<EditorHistoryTarget, Task>? RefreshTargetAsync { get; init; }

    public required Action<string> SetStatus { get; init; }

    /// <summary>Returns whether this context can resolve and mutate the requested target.</summary>
    public bool CanResolve(EditorHistoryTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Scope == EditorHistoryScope.Project)
        {
            return GetProject is not null &&
                ReplaceProject is not null &&
                GetWorkspaceSceneKeys is not null &&
                GetSceneByPageKey is not null &&
                ReplaceSceneByPageKey is not null &&
                RemoveSceneByPageKey is not null &&
                RestoreWorkspaceUi is not null &&
                SetPendingDeletedPageKeys is not null &&
                SetWorkspaceDirty is not null;
        }

        if (target.PageKey is { } pageKey)
        {
            return GetSceneByPageKey is not null ||
                (ActivePageKey == pageKey && GetActiveScene() is not null);
        }

        return string.Equals(target.LegacySceneId, ActiveSceneId, StringComparison.Ordinal) || GetSceneById is not null;
    }

    /// <summary>Gets the scene identified by a scene target.</summary>
    public ScadaScene? ResolveScene(EditorHistoryTarget target)
    {
        if (target.Scope != EditorHistoryScope.Scene)
        {
            throw new InvalidOperationException("A project history target does not identify one scene.");
        }

        if (target.PageKey is { } pageKey)
        {
            var keyedScene = GetSceneByPageKey?.Invoke(pageKey);
            if (keyedScene is not null)
            {
                return keyedScene;
            }

            if (ActivePageKey == pageKey)
            {
                return GetActiveScene();
            }
        }

        if (!string.IsNullOrWhiteSpace(target.LegacySceneId))
        {
            var identifiedScene = GetSceneById?.Invoke(target.LegacySceneId);
            if (identifiedScene is not null)
            {
                return identifiedScene;
            }

            if (string.Equals(target.LegacySceneId, ActiveSceneId, StringComparison.Ordinal))
            {
                return GetActiveScene();
            }
        }

        return null;
    }

    /// <summary>Replaces the scene identified by a scene target.</summary>
    public void ReplaceScene(EditorHistoryTarget target, ScadaScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (target.PageKey is { } pageKey && ReplaceSceneByPageKey is not null)
        {
            ReplaceSceneByPageKey(pageKey, scene);
            return;
        }

        ReplaceActiveScene(scene);
    }

    /// <summary>Marks a scene or project mutation dirty.</summary>
    public void MarkTargetDirty(EditorHistoryTarget target)
    {
        if (target.Scope == EditorHistoryScope.Project && SetWorkspaceDirty is not null)
        {
            SetWorkspaceDirty(true);
            return;
        }

        MarkDirty();
    }

    /// <summary>Refreshes the view affected by a history action.</summary>
    public Task RefreshAsync(EditorHistoryTarget target)
    {
        return RefreshTargetAsync?.Invoke(target) ?? RefreshPreviewAsync();
    }
}

/// <summary>Workspace-level undo/redo stack supporting both scene and project actions.</summary>
public sealed class EditorHistoryService
{
    private readonly Stack<IEditorHistoryAction> undoStack = new();
    private readonly Stack<IEditorHistoryAction> redoStack = new();

    public bool CanUndo => undoStack.Count > 0;

    public bool CanRedo => redoStack.Count > 0;

    public int UndoCount => undoStack.Count;

    public int RedoCount => redoStack.Count;

    public void Push(IEditorHistoryAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (undoStack.TryPeek(out var previous) &&
            previous.Target.RefersToSameTarget(action.Target) &&
            previous.CanMergeWith(action))
        {
            undoStack.Pop();
            undoStack.Push(previous.MergeWith(action));
        }
        else
        {
            undoStack.Push(action);
        }

        redoStack.Clear();
    }

    public async Task<bool> UndoAsync(EditorHistoryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!undoStack.TryPeek(out var action) || !context.CanResolve(action.Target))
        {
            return false;
        }

        undoStack.Pop();
        await action.UndoAsync(context);
        redoStack.Push(action);
        return true;
    }

    public async Task<bool> RedoAsync(EditorHistoryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!redoStack.TryPeek(out var action) || !context.CanResolve(action.Target))
        {
            return false;
        }

        redoStack.Pop();
        await action.RedoAsync(context);
        undoStack.Push(action);
        return true;
    }

    public void Clear()
    {
        undoStack.Clear();
        redoStack.Clear();
    }
}

/// <summary>History action adapter for mutations implemented by injected callbacks.</summary>
public sealed class DelegateEditorHistoryAction : IEditorHistoryAction
{
    private readonly Func<EditorHistoryContext, Task> undo;
    private readonly Func<EditorHistoryContext, Task> redo;

    public DelegateEditorHistoryAction(
        string sceneId,
        string label,
        Func<EditorHistoryContext, Task> undo,
        Func<EditorHistoryContext, Task> redo,
        Guid? pageKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        SceneId = sceneId;
        Label = label;
        Target = EditorHistoryTarget.ForScene(sceneId, pageKey);
        this.undo = undo ?? throw new ArgumentNullException(nameof(undo));
        this.redo = redo ?? throw new ArgumentNullException(nameof(redo));
    }

    public string SceneId { get; }

    public EditorHistoryTarget Target { get; }

    public string Label { get; }

    public bool CanMergeWith(IEditorHistoryAction next) => false;

    public IEditorHistoryAction MergeWith(IEditorHistoryAction next)
    {
        throw new InvalidOperationException("Delegate history actions do not support merge.");
    }

    public Task UndoAsync(EditorHistoryContext context) => undo(context);

    public Task RedoAsync(EditorHistoryContext context) => redo(context);
}
