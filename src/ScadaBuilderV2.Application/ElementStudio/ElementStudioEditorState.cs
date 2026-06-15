using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.ElementStudio;

public enum ElementStudioSelectionMode
{
    Replace,
    Add,
    Remove
}

public enum ElementStudioHorizontalAlignment
{
    Left,
    Center,
    Right
}

public enum ElementStudioVerticalAlignment
{
    Top,
    Middle,
    Bottom
}

public sealed record ElementStudioEditableItem(
    string Id,
    string Name,
    string Kind,
    SceneBounds Bounds,
    bool IsLocked = false,
    bool IsVisible = true,
    string? GroupId = null);

public sealed class ElementStudioEditorState
{
    private readonly List<ElementStudioEditableItem> items;
    private readonly List<ElementStudioEditableItem> clipboard = [];
    private readonly HashSet<string> selectedIds = new(StringComparer.Ordinal);
    private readonly Stack<EditorSnapshot> undoStack = [];
    private readonly Stack<EditorSnapshot> redoStack = [];
    private int duplicateSequence;
    private int groupSequence;

    public ElementStudioEditorState(IEnumerable<ElementStudioEditableItem> initialItems)
    {
        items = initialItems.ToList();
    }

    public IReadOnlyList<ElementStudioEditableItem> Items => items;

    public IReadOnlyCollection<string> SelectedIds => selectedIds;

    public bool CanUndo => undoStack.Count > 0;

    public bool CanRedo => redoStack.Count > 0;

    public void Select(IEnumerable<string> ids, ElementStudioSelectionMode mode)
    {
        var validIds = ids
            .Where(id => items.Any(item =>
                string.Equals(item.Id, id, StringComparison.Ordinal) &&
                item.IsVisible &&
                !item.IsLocked))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (mode == ElementStudioSelectionMode.Replace)
        {
            selectedIds.Clear();
        }

        foreach (var id in validIds)
        {
            if (mode == ElementStudioSelectionMode.Remove)
            {
                selectedIds.Remove(id);
            }
            else
            {
                selectedIds.Add(id);
            }
        }
    }

    public void SelectIntersecting(SceneBounds rectangle, ElementStudioSelectionMode mode)
    {
        Select(items
            .Where(item => item.IsVisible && !item.IsLocked && Intersects(item.Bounds, rectangle))
            .Select(item => item.Id), mode);
    }

    public void MoveSelectionBy(double deltaX, double deltaY)
    {
        if (deltaX == 0 && deltaY == 0)
        {
            return;
        }

        RecordUndo();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (!CanMutate(item))
            {
                continue;
            }

            items[index] = item with
            {
                Bounds = new SceneBounds(
                    item.Bounds.X + deltaX,
                    item.Bounds.Y + deltaY,
                    item.Bounds.Width,
                    item.Bounds.Height)
            };
        }
    }

    public void ResizeSelectionBy(double deltaWidth, double deltaHeight)
    {
        if (deltaWidth == 0 && deltaHeight == 0)
        {
            return;
        }

        var selected = MutableSelectedItems();
        if (selected.Length == 0)
        {
            return;
        }

        RecordUndo();
        ReplaceSelected(item => item with
        {
            Bounds = item.Bounds with
            {
                Width = Math.Max(1, item.Bounds.Width + deltaWidth),
                Height = Math.Max(1, item.Bounds.Height + deltaHeight)
            }
        });
    }

    public void SetSelectionBounds(double? x, double? y, double? width, double? height)
    {
        var selected = MutableSelectedItems();
        if (selected.Length == 0)
        {
            return;
        }

        RecordUndo();
        ReplaceSelected(item => item with
        {
            Bounds = new SceneBounds(
                x ?? item.Bounds.X,
                y ?? item.Bounds.Y,
                Math.Max(1, width ?? item.Bounds.Width),
                Math.Max(1, height ?? item.Bounds.Height))
        });
    }

    public void EqualizeWidth()
    {
        var selected = MutableSelectedItems();
        if (selected.Length < 2)
        {
            return;
        }

        RecordUndo();
        var width = selected[0].Bounds.Width;
        ReplaceSelected(item => item with { Bounds = item.Bounds with { Width = width } });
    }

    public void EqualizeHeight()
    {
        var selected = MutableSelectedItems();
        if (selected.Length < 2)
        {
            return;
        }

        RecordUndo();
        var height = selected[0].Bounds.Height;
        ReplaceSelected(item => item with { Bounds = item.Bounds with { Height = height } });
    }

    public void DeleteSelection()
    {
        if (!items.Any(CanMutate))
        {
            return;
        }

        RecordUndo();
        items.RemoveAll(CanMutate);
        selectedIds.Clear();
    }

    public void CopySelection()
    {
        clipboard.Clear();
        clipboard.AddRange(items.Where(item => selectedIds.Contains(item.Id) && item.IsVisible));
    }

    public void DuplicateSelection(double offsetX = 16, double offsetY = 16)
    {
        CopySelection();
        PasteClipboard(offsetX, offsetY);
    }

    public void PasteClipboard(double offsetX = 16, double offsetY = 16)
    {
        if (clipboard.Count == 0)
        {
            return;
        }

        RecordUndo();
        selectedIds.Clear();
        foreach (var item in clipboard)
        {
            duplicateSequence++;
            var id = $"{item.Id}-copy-{duplicateSequence}";
            var copy = item with
            {
                Id = id,
                Name = $"{item.Name} Copy {duplicateSequence}",
                IsLocked = false,
                IsVisible = true,
                GroupId = null,
                Bounds = new SceneBounds(
                    item.Bounds.X + offsetX,
                    item.Bounds.Y + offsetY,
                    item.Bounds.Width,
                    item.Bounds.Height)
            };
            items.Add(copy);
            selectedIds.Add(id);
        }
    }

    public void AlignHorizontal(ElementStudioHorizontalAlignment alignment)
    {
        var selected = MutableSelectedItems();
        if (selected.Length < 2)
        {
            return;
        }

        RecordUndo();
        var minLeft = selected.Min(item => item.Bounds.X);
        var maxRight = selected.Max(item => item.Bounds.X + item.Bounds.Width);
        var center = (minLeft + maxRight) / 2;

        ReplaceSelected(item =>
        {
            var x = alignment switch
            {
                ElementStudioHorizontalAlignment.Left => minLeft,
                ElementStudioHorizontalAlignment.Center => center - item.Bounds.Width / 2,
                ElementStudioHorizontalAlignment.Right => maxRight - item.Bounds.Width,
                _ => item.Bounds.X
            };
            return item with { Bounds = item.Bounds with { X = x } };
        });
    }

    public void AlignVertical(ElementStudioVerticalAlignment alignment)
    {
        var selected = MutableSelectedItems();
        if (selected.Length < 2)
        {
            return;
        }

        RecordUndo();
        var minTop = selected.Min(item => item.Bounds.Y);
        var maxBottom = selected.Max(item => item.Bounds.Y + item.Bounds.Height);
        var middle = (minTop + maxBottom) / 2;

        ReplaceSelected(item =>
        {
            var y = alignment switch
            {
                ElementStudioVerticalAlignment.Top => minTop,
                ElementStudioVerticalAlignment.Middle => middle - item.Bounds.Height / 2,
                ElementStudioVerticalAlignment.Bottom => maxBottom - item.Bounds.Height,
                _ => item.Bounds.Y
            };
            return item with { Bounds = item.Bounds with { Y = y } };
        });
    }

    public void AlignLeft()
    {
        AlignHorizontal(ElementStudioHorizontalAlignment.Left);
    }

    public void DistributeHorizontally()
    {
        var selected = MutableSelectedItems().OrderBy(item => item.Bounds.X).ToArray();
        if (selected.Length < 3)
        {
            return;
        }

        RecordUndo();
        var minLeft = selected.First().Bounds.X;
        var maxRight = selected.Last().Bounds.X;
        var step = (maxRight - minLeft) / (selected.Length - 1);
        var positions = selected
            .Select((item, index) => new { item.Id, X = minLeft + step * index })
            .ToDictionary(entry => entry.Id, entry => entry.X, StringComparer.Ordinal);

        ReplaceSelected(item => positions.TryGetValue(item.Id, out var x)
            ? item with { Bounds = item.Bounds with { X = x } }
            : item);
    }

    public void DistributeVertically()
    {
        var selected = MutableSelectedItems().OrderBy(item => item.Bounds.Y).ToArray();
        if (selected.Length < 3)
        {
            return;
        }

        RecordUndo();
        var minTop = selected.First().Bounds.Y;
        var maxBottom = selected.Last().Bounds.Y;
        var step = (maxBottom - minTop) / (selected.Length - 1);
        var positions = selected
            .Select((item, index) => new { item.Id, Y = minTop + step * index })
            .ToDictionary(entry => entry.Id, entry => entry.Y, StringComparer.Ordinal);

        ReplaceSelected(item => positions.TryGetValue(item.Id, out var y)
            ? item with { Bounds = item.Bounds with { Y = y } }
            : item);
    }

    public string? GroupSelection()
    {
        var selected = MutableSelectedItems();
        if (selected.Length < 2)
        {
            return null;
        }

        RecordUndo();
        groupSequence++;
        var groupId = $"group-{groupSequence:000}";
        ReplaceSelected(item => item with { GroupId = groupId });
        return groupId;
    }

    public void UngroupSelection()
    {
        if (!MutableSelectedItems().Any(item => !string.IsNullOrWhiteSpace(item.GroupId)))
        {
            return;
        }

        RecordUndo();
        ReplaceSelected(item => item with { GroupId = null });
    }

    public void SetSelectionLocked(bool locked)
    {
        var selected = items.Where(item => selectedIds.Contains(item.Id)).ToArray();
        if (selected.Length == 0 || selected.All(item => item.IsLocked == locked))
        {
            return;
        }

        RecordUndo();
        ReplaceAny(item => selectedIds.Contains(item.Id), item => item with { IsLocked = locked });
        if (locked)
        {
            selectedIds.Clear();
        }
    }

    public void SetSelectionVisible(bool visible)
    {
        var selected = items.Where(item => selectedIds.Contains(item.Id)).ToArray();
        if (selected.Length == 0 || selected.All(item => item.IsVisible == visible))
        {
            return;
        }

        RecordUndo();
        ReplaceAny(item => selectedIds.Contains(item.Id), item => item with { IsVisible = visible });
        if (!visible)
        {
            selectedIds.Clear();
        }
    }

    public void Undo()
    {
        if (undoStack.Count == 0)
        {
            return;
        }

        redoStack.Push(CaptureSnapshot());
        RestoreSnapshot(undoStack.Pop());
    }

    public void Redo()
    {
        if (redoStack.Count == 0)
        {
            return;
        }

        undoStack.Push(CaptureSnapshot());
        RestoreSnapshot(redoStack.Pop());
    }

    private ElementStudioEditableItem[] MutableSelectedItems()
    {
        return items.Where(CanMutate).ToArray();
    }

    private bool CanMutate(ElementStudioEditableItem item)
    {
        return selectedIds.Contains(item.Id) && item.IsVisible && !item.IsLocked;
    }

    private void ReplaceSelected(Func<ElementStudioEditableItem, ElementStudioEditableItem> update)
    {
        ReplaceAny(CanMutate, update);
    }

    private void ReplaceAny(
        Func<ElementStudioEditableItem, bool> predicate,
        Func<ElementStudioEditableItem, ElementStudioEditableItem> update)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (predicate(items[index]))
            {
                items[index] = update(items[index]);
            }
        }
    }

    private void RecordUndo()
    {
        undoStack.Push(CaptureSnapshot());
        redoStack.Clear();
    }

    private EditorSnapshot CaptureSnapshot()
    {
        return new EditorSnapshot(
            items.ToArray(),
            selectedIds.ToArray(),
            duplicateSequence,
            groupSequence);
    }

    private void RestoreSnapshot(EditorSnapshot snapshot)
    {
        items.Clear();
        items.AddRange(snapshot.Items);
        selectedIds.Clear();
        foreach (var selectedId in snapshot.SelectedIds)
        {
            selectedIds.Add(selectedId);
        }

        duplicateSequence = snapshot.DuplicateSequence;
        groupSequence = snapshot.GroupSequence;
    }

    private static bool Intersects(SceneBounds a, SceneBounds b)
    {
        return a.X <= b.X + b.Width &&
            a.X + a.Width >= b.X &&
            a.Y <= b.Y + b.Height &&
            a.Y + a.Height >= b.Y;
    }

    private sealed record EditorSnapshot(
        IReadOnlyList<ElementStudioEditableItem> Items,
        IReadOnlyList<string> SelectedIds,
        int DuplicateSequence,
        int GroupSequence);
}
