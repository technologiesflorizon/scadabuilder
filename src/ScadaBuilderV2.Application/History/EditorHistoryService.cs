using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.History;

public interface IEditorHistoryAction
{
    string SceneId { get; }

    string Label { get; }

    bool CanMergeWith(IEditorHistoryAction next);

    IEditorHistoryAction MergeWith(IEditorHistoryAction next);

    Task UndoAsync(EditorHistoryContext context);

    Task RedoAsync(EditorHistoryContext context);
}

public sealed class EditorHistoryContext
{
    public required string ActiveSceneId { get; init; }

    public required Func<ScadaScene?> GetActiveScene { get; init; }

    public required Action<ScadaScene> ReplaceActiveScene { get; init; }

    public required Action MarkDirty { get; init; }

    public required Func<Task> RefreshPreviewAsync { get; init; }

    public required Action<string> SetStatus { get; init; }
}

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
            string.Equals(previous.SceneId, action.SceneId, StringComparison.Ordinal) &&
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

        if (!undoStack.TryPeek(out var action))
        {
            return false;
        }

        if (!string.Equals(action.SceneId, context.ActiveSceneId, StringComparison.Ordinal))
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

        if (!redoStack.TryPeek(out var action))
        {
            return false;
        }

        if (!string.Equals(action.SceneId, context.ActiveSceneId, StringComparison.Ordinal))
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

public sealed class DelegateEditorHistoryAction : IEditorHistoryAction
{
    private readonly Func<EditorHistoryContext, Task> undo;
    private readonly Func<EditorHistoryContext, Task> redo;

    public DelegateEditorHistoryAction(
        string sceneId,
        string label,
        Func<EditorHistoryContext, Task> undo,
        Func<EditorHistoryContext, Task> redo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        SceneId = sceneId;
        Label = label;
        this.undo = undo ?? throw new ArgumentNullException(nameof(undo));
        this.redo = redo ?? throw new ArgumentNullException(nameof(redo));
    }

    public string SceneId { get; }

    public string Label { get; }

    public bool CanMergeWith(IEditorHistoryAction next)
    {
        return false;
    }

    public IEditorHistoryAction MergeWith(IEditorHistoryAction next)
    {
        throw new InvalidOperationException("Delegate history actions do not support merge.");
    }

    public Task UndoAsync(EditorHistoryContext context)
    {
        return undo(context);
    }

    public Task RedoAsync(EditorHistoryContext context)
    {
        return redo(context);
    }
}
