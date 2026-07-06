using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ScadaBuilderV2.Application.ElementStudio;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.ElementStudio.App;

public sealed class ElementStudioWorkspaceViewModel : INotifyPropertyChanged
{
    private ElementStudioItemViewModel? selectedItem;
    private readonly List<ElementStudioItemViewModel> clipboard = [];
    private readonly Stack<WorkspaceSnapshot> undoStack = [];
    private readonly Stack<WorkspaceSnapshot> redoStack = [];
    private int nextElementIndex;
    private int nextGroupIndex;
    private string componentName = ElementStudioComponentNaming.DefaultComponentName;
    private string componentCategory = "General";
    private string componentTags = "";
    private string componentVisualKind = "Source";
    private string activeTool = ElementStudioToolNames.Selection;
    private string savedComponentPath = "";
    private double workzoneWidth;
    private double workzoneHeight;
    private double workzoneZoom = 1.0;

    public ElementStudioWorkspaceViewModel(ElementStudioImportPackage package, IEnumerable<string> diagnostics)
    {
        Package = package;
        workzoneWidth = Math.Max(160, package.Bounds.Width);
        workzoneHeight = Math.Max(120, package.Bounds.Height);
        ImportedItems = new ObservableCollection<ElementStudioItemViewModel>(
            package.Items.Select((item, index) => new ElementStudioItemViewModel(item, index)));
        SelectedItems = new ObservableCollection<ElementStudioItemViewModel>();
        Diagnostics = new ObservableCollection<string>(diagnostics);
        nextElementIndex = ImportedItems.Count + 1;
        componentName = ElementStudioComponentNaming.ResolveDefaultComponentName(
            package.Items.Select(item => item.SourceName).ToArray());
        SetSelectedItems(ImportedItems.Take(1));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ElementStudioImportPackage Package { get; }

    public ObservableCollection<ElementStudioItemViewModel> ImportedItems { get; }

    public ObservableCollection<ElementStudioItemViewModel> SelectedItems { get; }

    public ObservableCollection<string> Diagnostics { get; }

    public IReadOnlyList<string> ToolModes { get; } =
    [
        ElementStudioToolNames.Selection,
        ElementStudioToolNames.Ligne,
        ElementStudioToolNames.Polyline,
        ElementStudioToolNames.Rectangle,
        ElementStudioToolNames.Polygone,
        ElementStudioToolNames.Image
    ];

    public double WorkzoneWidth
    {
        get => workzoneWidth;
        set
        {
            var bounded = Math.Clamp(value, 160, 20000);
            if (SetField(ref workzoneWidth, bounded))
            {
                OnPropertyChanged(nameof(WorkzoneScaledWidth));
                OnPropertyChanged(nameof(WorkzoneSizeText));
            }
        }
    }

    public double WorkzoneHeight
    {
        get => workzoneHeight;
        set
        {
            var bounded = Math.Clamp(value, 120, 20000);
            if (SetField(ref workzoneHeight, bounded))
            {
                OnPropertyChanged(nameof(WorkzoneScaledHeight));
                OnPropertyChanged(nameof(WorkzoneSizeText));
            }
        }
    }

    public double WorkzoneScaledWidth => WorkzoneWidth * WorkzoneZoom;

    public double WorkzoneScaledHeight => WorkzoneHeight * WorkzoneZoom;

    public double WorkzoneZoom
    {
        get => workzoneZoom;
        set
        {
            var bounded = Math.Clamp(value, 0.25, 4.0);
            if (SetField(ref workzoneZoom, bounded))
            {
                OnPropertyChanged(nameof(WorkzoneZoomText));
                OnPropertyChanged(nameof(WorkzoneScaledWidth));
                OnPropertyChanged(nameof(WorkzoneScaledHeight));
            }
        }
    }

    public string WorkzoneZoomText => $"{WorkzoneZoom * 100:0}%";

    public string WorkzoneSizeText => $"{WorkzoneWidth:0} x {WorkzoneHeight:0}";

    public void ResizeWorkzone(double deltaWidth, double deltaHeight)
    {
        WorkzoneWidth += deltaWidth;
        WorkzoneHeight += deltaHeight;
    }

    public void FitWorkzoneToImportedBounds()
    {
        WorkzoneWidth = Math.Max(160, Package.Bounds.Width);
        WorkzoneHeight = Math.Max(120, Package.Bounds.Height);
    }

    public string ActiveTool
    {
        get => activeTool;
        set
        {
            if (!ToolModes.Contains(value))
            {
                return;
            }

            if (SetField(ref activeTool, value))
            {
                OnPropertyChanged(nameof(ActiveToolSummary));
            }
        }
    }

    public string ActiveToolSummary => $"Outil actif: {ActiveTool}";

    public string SavedComponentPath
    {
        get => savedComponentPath;
        set => SetField(ref savedComponentPath, value);
    }

    public string ComponentName
    {
        get => componentName;
        set => SetField(ref componentName, value);
    }

    public string ComponentCategory
    {
        get => componentCategory;
        set => SetField(ref componentCategory, value);
    }

    public string ComponentTags
    {
        get => componentTags;
        set => SetField(ref componentTags, value);
    }

    public IReadOnlyList<string> ComponentTagList => ComponentTags
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public string ComponentVisualKind
    {
        get => componentVisualKind;
        set => SetField(ref componentVisualKind, value);
    }

    public string ComponentSummary =>
        $"{ComponentName} | {ComponentVisualKind} | {ImportedItems.Count} source(s)";

    public bool CanUndo => undoStack.Count > 0;

    public bool CanRedo => redoStack.Count > 0;

    public int VisibleItemCount => ImportedItems.Count(item => item.IsVisible);

    public int HiddenItemCount => ImportedItems.Count(item => !item.IsVisible);

    public int LockedItemCount => ImportedItems.Count(item => item.IsLocked);

    public int GroupCount => ImportedItems
        .Where(item => !string.IsNullOrWhiteSpace(item.GroupId))
        .Select(item => item.GroupId)
        .Distinct(StringComparer.Ordinal)
        .Count();

    public string StructureSummary =>
        $"{VisibleItemCount} visibles | {HiddenItemCount} caches | {LockedItemCount} verrouilles | {GroupCount} groupes";

    public string SelectionStateSummary => SelectedItems.Count == 0
        ? "Aucune selection active"
        : $"{SelectedItems.Count} selectionnes | {SelectedItems.Count(item => !string.IsNullOrWhiteSpace(item.GroupId))} groupes | {SelectedItems.Count(item => item.IsLocked)} verrouilles";

    public void ZoomIn()
    {
        WorkzoneZoom += 0.1;
    }

    public void ZoomOut()
    {
        WorkzoneZoom -= 0.1;
    }

    public void ResetZoom()
    {
        WorkzoneZoom = 1.0;
    }

    public ElementStudioItemViewModel? SelectedItem
    {
        get => selectedItem;
        set
        {
            if (selectedItem == value)
            {
                return;
            }

            selectedItem = value;
            OnPropertyChanged();
            NotifyEditorStateChanged();
        }
    }

    public void SetSelectedItems(IEnumerable<ElementStudioItemViewModel> items)
    {
        var nextItems = items
            .Where(item => ImportedItems.Contains(item) && item.IsVisible && !item.IsLocked)
            .Distinct()
            .ToArray();

        SelectedItems.Clear();
        foreach (var item in nextItems)
        {
            SelectedItems.Add(item);
        }

        SelectedItem = nextItems.LastOrDefault();
        NotifyEditorStateChanged();
    }

    public void SetSelectedItemIds(IEnumerable<string> sourceElementIds)
    {
        var ids = sourceElementIds.ToHashSet(StringComparer.Ordinal);
        SetSelectedItems(ImportedItems.Where(item => ids.Contains(item.SourceElementId)));
    }

    public void SelectAll()
    {
        SetSelectedItems(ImportedItems.Where(item => item.IsVisible && !item.IsLocked));
    }

    public void ClearSelection()
    {
        SetSelectedItems([]);
    }

    public void MoveSelectedItemsBy(double deltaX, double deltaY)
    {
        if (deltaX == 0 && deltaY == 0)
        {
            return;
        }

        var selected = MutableSelectedItems();
        if (selected.Length == 0)
        {
            return;
        }

        PushUndoSnapshot();
        foreach (var item in selected)
        {
            item.MoveBy(deltaX, deltaY);
        }

        NotifyEditorStateChanged();
    }

    public void ResizeSelectedItemsBy(double deltaWidth, double deltaHeight)
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

        PushUndoSnapshot();
        foreach (var item in selected)
        {
            item.ResizeBy(deltaWidth, deltaHeight);
        }

        NotifyEditorStateChanged();
    }

    public void SetSelectedBounds(double? x, double? y, double? width, double? height)
    {
        var selected = MutableSelectedItems();
        if (selected.Length == 0)
        {
            return;
        }

        PushUndoSnapshot();
        foreach (var item in selected)
        {
            item.SetBounds(x, y, width, height);
        }

        NotifyEditorStateChanged();
    }

    public void EqualizeSelectedWidth()
    {
        var selected = MutableSelectedItems();
        if (selected.Length < 2)
        {
            return;
        }

        PushUndoSnapshot();
        var width = selected[0].Bounds.Width;
        foreach (var item in selected)
        {
            item.SetWidth(width);
        }

        NotifyEditorStateChanged();
    }

    public void EqualizeSelectedHeight()
    {
        var selected = MutableSelectedItems();
        if (selected.Length < 2)
        {
            return;
        }

        PushUndoSnapshot();
        var height = selected[0].Bounds.Height;
        foreach (var item in selected)
        {
            item.SetHeight(height);
        }

        NotifyEditorStateChanged();
    }

    public void DeleteSelectedItems()
    {
        var selected = SelectedItems
            .Where(item => !item.IsLocked && item.IsVisible)
            .ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        PushUndoSnapshot();
        foreach (var item in selected)
        {
            ImportedItems.Remove(item);
        }

        SetSelectedItems([]);
        NotifyEditorStateChanged();
    }

    public void CopySelectedItems()
    {
        clipboard.Clear();
        clipboard.AddRange(SelectedItems);
    }

    public void DuplicateSelectedItems()
    {
        CopySelectedItems();
        PasteCopiedItems();
    }

    public void PasteCopiedItems()
    {
        if (clipboard.Count == 0)
        {
            return;
        }

        PushUndoSnapshot();
        var copies = new List<ElementStudioItemViewModel>();
        foreach (var item in clipboard)
        {
            var copy = item.CreateCopy(nextElementIndex++);
            copy.MoveBy(16, 16);
            ImportedItems.Add(copy);
            copies.Add(copy);
        }

        SetSelectedItems(copies);
        NotifyEditorStateChanged();
    }

    public void AlignLeft()
    {
        AlignHorizontal(item => item.Bounds.X, (item, target) => item.SetX(target));
    }

    public void AlignCenter()
    {
        var selected = MutableSelectedItems();
        if (selected.Length < 2)
        {
            return;
        }

        PushUndoSnapshot();
        var left = selected.Min(item => item.Bounds.X);
        var right = selected.Max(item => item.Bounds.X + item.Bounds.Width);
        var center = (left + right) / 2;
        foreach (var item in selected)
        {
            item.SetX(center - item.Bounds.Width / 2);
        }

        NotifyEditorStateChanged();
    }

    public void AlignRight()
    {
        var selected = MutableSelectedItems();
        if (selected.Length < 2)
        {
            return;
        }

        PushUndoSnapshot();
        var right = selected.Max(item => item.Bounds.X + item.Bounds.Width);
        foreach (var item in selected)
        {
            item.SetX(right - item.Bounds.Width);
        }

        NotifyEditorStateChanged();
    }

    public void AlignTop()
    {
        AlignVertical(item => item.Bounds.Y, (item, target) => item.SetY(target));
    }

    public void AlignMiddle()
    {
        var selected = MutableSelectedItems();
        if (selected.Length < 2)
        {
            return;
        }

        PushUndoSnapshot();
        var top = selected.Min(item => item.Bounds.Y);
        var bottom = selected.Max(item => item.Bounds.Y + item.Bounds.Height);
        var middle = (top + bottom) / 2;
        foreach (var item in selected)
        {
            item.SetY(middle - item.Bounds.Height / 2);
        }

        NotifyEditorStateChanged();
    }

    public void AlignBottom()
    {
        var selected = MutableSelectedItems();
        if (selected.Length < 2)
        {
            return;
        }

        PushUndoSnapshot();
        var bottom = selected.Max(item => item.Bounds.Y + item.Bounds.Height);
        foreach (var item in selected)
        {
            item.SetY(bottom - item.Bounds.Height);
        }

        NotifyEditorStateChanged();
    }

    public void DistributeHorizontally()
    {
        var selected = MutableSelectedItems().OrderBy(item => item.Bounds.X).ToArray();
        if (selected.Length < 3)
        {
            return;
        }

        PushUndoSnapshot();
        var left = selected.First().Bounds.X;
        var right = selected.Last().Bounds.X;
        var step = (right - left) / (selected.Length - 1);
        for (var index = 0; index < selected.Length; index++)
        {
            selected[index].SetX(left + step * index);
        }

        NotifyEditorStateChanged();
    }

    public void DistributeVertically()
    {
        var selected = MutableSelectedItems().OrderBy(item => item.Bounds.Y).ToArray();
        if (selected.Length < 3)
        {
            return;
        }

        PushUndoSnapshot();
        var top = selected.First().Bounds.Y;
        var bottom = selected.Last().Bounds.Y;
        var step = (bottom - top) / (selected.Length - 1);
        for (var index = 0; index < selected.Length; index++)
        {
            selected[index].SetY(top + step * index);
        }

        NotifyEditorStateChanged();
    }

    public void GroupSelectedItems()
    {
        var selected = MutableSelectedItems();
        if (selected.Length < 2)
        {
            return;
        }

        PushUndoSnapshot();
        nextGroupIndex++;
        var groupId = $"group-{nextGroupIndex:000}";
        foreach (var item in selected)
        {
            item.GroupId = groupId;
        }

        NotifyEditorStateChanged();
    }

    public void UngroupSelectedItems()
    {
        var selected = MutableSelectedItems().Where(item => !string.IsNullOrWhiteSpace(item.GroupId)).ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        PushUndoSnapshot();
        foreach (var item in selected)
        {
            item.GroupId = null;
        }

        NotifyEditorStateChanged();
    }

    public void LockSelectedItems()
    {
        var selected = SelectedItems.Where(item => item.IsVisible && !item.IsLocked).ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        PushUndoSnapshot();
        foreach (var item in selected)
        {
            item.IsLocked = true;
        }

        ClearSelection();
    }

    public void UnlockAllItems()
    {
        var locked = ImportedItems.Where(item => item.IsLocked).ToArray();
        if (locked.Length == 0)
        {
            return;
        }

        PushUndoSnapshot();
        foreach (var item in locked)
        {
            item.IsLocked = false;
        }

        NotifyEditorStateChanged();
    }

    public void HideSelectedItems()
    {
        var selected = SelectedItems.Where(item => item.IsVisible).ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        PushUndoSnapshot();
        foreach (var item in selected)
        {
            item.IsVisible = false;
        }

        ClearSelection();
    }

    public void ShowAllItems()
    {
        var hidden = ImportedItems.Where(item => !item.IsVisible).ToArray();
        if (hidden.Length == 0)
        {
            return;
        }

        PushUndoSnapshot();
        foreach (var item in hidden)
        {
            item.IsVisible = true;
        }

        NotifyEditorStateChanged();
    }

    public void Undo()
    {
        if (undoStack.Count == 0)
        {
            return;
        }

        redoStack.Push(CreateSnapshot());
        RestoreSnapshot(undoStack.Pop());
    }

    public void Redo()
    {
        if (redoStack.Count == 0)
        {
            return;
        }

        undoStack.Push(CreateSnapshot());
        RestoreSnapshot(redoStack.Pop());
    }

    public ElementStudioImportPackage CreatePackageSnapshot()
    {
        return Package with
        {
            Items = ImportedItems
                .Where(item => item.IsVisible)
                .Select(item => item.ToLegacyItem())
                .ToArray()
        };
    }

    public string PackageSummary =>
        $"{Package.PackageId} | {Package.SourceProjectId} | {Package.SourceSceneId}";

    public string SelectionSummary => SelectedItems.Count switch
    {
        0 => "Aucun element selectionne",
        1 => $"{SelectedItem?.ElementName} ({SelectedItem?.LegacyType})",
        _ => $"{SelectedItems.Count} elements selectionnes"
    };

    private ElementStudioItemViewModel[] MutableSelectedItems()
    {
        return SelectedItems.Where(item => item.IsVisible && !item.IsLocked).ToArray();
    }

    private void AlignHorizontal(
        Func<ElementStudioItemViewModel, double> targetSelector,
        Action<ElementStudioItemViewModel, double> apply)
    {
        var selected = MutableSelectedItems();
        if (selected.Length < 2)
        {
            return;
        }

        PushUndoSnapshot();
        var target = selected.Min(targetSelector);
        foreach (var item in selected)
        {
            apply(item, target);
        }

        NotifyEditorStateChanged();
    }

    private void AlignVertical(
        Func<ElementStudioItemViewModel, double> targetSelector,
        Action<ElementStudioItemViewModel, double> apply)
    {
        var selected = MutableSelectedItems();
        if (selected.Length < 2)
        {
            return;
        }

        PushUndoSnapshot();
        var target = selected.Min(targetSelector);
        foreach (var item in selected)
        {
            apply(item, target);
        }

        NotifyEditorStateChanged();
    }

    private void PushUndoSnapshot()
    {
        undoStack.Push(CreateSnapshot());
        redoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void NotifyEditorStateChanged()
    {
        OnPropertyChanged(nameof(ComponentSummary));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(SelectionStateSummary));
        OnPropertyChanged(nameof(StructureSummary));
        OnPropertyChanged(nameof(VisibleItemCount));
        OnPropertyChanged(nameof(HiddenItemCount));
        OnPropertyChanged(nameof(LockedItemCount));
        OnPropertyChanged(nameof(GroupCount));
    }

    private WorkspaceSnapshot CreateSnapshot()
    {
        return new WorkspaceSnapshot(
            ImportedItems.Select(item => item.CreateSnapshot()).ToArray(),
            SelectedItems.Select(item => item.SourceElementId).ToArray(),
            nextElementIndex,
            nextGroupIndex);
    }

    private void RestoreSnapshot(WorkspaceSnapshot snapshot)
    {
        ImportedItems.Clear();
        foreach (var item in snapshot.Items.Select(ElementStudioItemViewModel.FromSnapshot))
        {
            ImportedItems.Add(item);
        }

        nextElementIndex = snapshot.NextElementIndex;
        nextGroupIndex = snapshot.NextGroupIndex;
        SetSelectedItemIds(snapshot.SelectedIds);
        NotifyEditorStateChanged();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(ComponentSummary));
        return true;
    }
}

internal sealed record ElementStudioItemSnapshot(
    ElementStudioLegacyItem Item,
    string ElementName,
    SceneBounds Bounds,
    bool IsLocked,
    bool IsVisible,
    string? GroupId);

internal sealed record WorkspaceSnapshot(
    IReadOnlyList<ElementStudioItemSnapshot> Items,
    IReadOnlyList<string> SelectedIds,
    int NextElementIndex,
    int NextGroupIndex);

public static class ElementStudioToolNames
{
    public const string Selection = "Selection";
    public const string Ligne = "Ligne";
    public const string Polyline = "Polyline";
    public const string Rectangle = "Rectangle";
    public const string Polygone = "Polygone";
    public const string Image = "Image";
}

public sealed class ElementStudioItemViewModel : INotifyPropertyChanged
{
    private SceneBounds bounds;
    private bool isLocked;
    private bool isVisible = true;
    private string? groupId;

    public ElementStudioItemViewModel(ElementStudioLegacyItem item, int index)
        : this(item, CreateElementPlusName(index + 1))
    {
    }

    private ElementStudioItemViewModel(ElementStudioLegacyItem item, string elementName)
    {
        Item = item;
        ElementName = elementName;
        bounds = item.BoundsRelativeToPackage;
    }

    private ElementStudioItemViewModel(ElementStudioItemSnapshot snapshot)
    {
        Item = snapshot.Item;
        ElementName = snapshot.ElementName;
        bounds = snapshot.Bounds;
        isLocked = snapshot.IsLocked;
        isVisible = snapshot.IsVisible;
        groupId = snapshot.GroupId;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ElementStudioLegacyItem Item { get; }

    public string ElementName { get; }

    public string SourceElementId => Item.SourceElementId;

    public string SourceName => Item.SourceName;

    public string LegacyType => Item.LegacyType;

    public SceneBounds Bounds
    {
        get => bounds;
        private set
        {
            if (EqualityComparer<SceneBounds>.Default.Equals(bounds, value))
            {
                return;
            }

            bounds = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string DisplayName
    {
        get
        {
            var states = new List<string>();
            if (!IsVisible)
            {
                states.Add("Hidden");
            }

            if (IsLocked)
            {
                states.Add("Lock");
            }

            if (!string.IsNullOrWhiteSpace(GroupId))
            {
                states.Add(GroupId);
            }

            var suffix = states.Count == 0
                ? ""
                : $"  ({string.Join(", ", states)})";
            return $"{ElementName}  [{LegacyType}]{suffix}";
        }
    }

    public bool IsLocked
    {
        get => isLocked;
        set
        {
            if (isLocked == value)
            {
                return;
            }

            isLocked = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public bool IsVisible
    {
        get => isVisible;
        set
        {
            if (isVisible == value)
            {
                return;
            }

            isVisible = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string? GroupId
    {
        get => groupId;
        set
        {
            if (string.Equals(groupId, value, StringComparison.Ordinal))
            {
                return;
            }

            groupId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string Fill => string.IsNullOrWhiteSpace(Item.Style.Background) ||
        Item.Style.Background.Equals("transparent", StringComparison.OrdinalIgnoreCase)
            ? "#EAF5F7"
            : Item.Style.Background;

    public string Stroke => string.IsNullOrWhiteSpace(Item.Style.BorderColor) ||
        Item.Style.BorderColor.Equals("transparent", StringComparison.OrdinalIgnoreCase)
            ? "#2090A0"
            : Item.Style.BorderColor;

    public double StrokeWidth => Item.Style.BorderWidth > 0 ? Item.Style.BorderWidth : 1;

    public string Text => string.IsNullOrWhiteSpace(Item.Text) ? SourceName : Item.Text!;

    public string GeometrySummary => string.IsNullOrWhiteSpace(Item.Geometry)
        ? "Aucune geometrie detaillee"
        : Item.Geometry.Length <= 140
            ? Item.Geometry
            : string.Concat(Item.Geometry.AsSpan(0, 140), "...");

    public void MoveBy(double deltaX, double deltaY)
    {
        Bounds = new SceneBounds(
            Bounds.X + deltaX,
            Bounds.Y + deltaY,
            Bounds.Width,
            Bounds.Height);
    }

    public void SetX(double x)
    {
        Bounds = Bounds with { X = x };
    }

    public void SetY(double y)
    {
        Bounds = Bounds with { Y = y };
    }

    public void SetWidth(double width)
    {
        Bounds = Bounds with { Width = Math.Max(1, width) };
    }

    public void SetHeight(double height)
    {
        Bounds = Bounds with { Height = Math.Max(1, height) };
    }

    public void ResizeBy(double deltaWidth, double deltaHeight)
    {
        Bounds = Bounds with
        {
            Width = Math.Max(1, Bounds.Width + deltaWidth),
            Height = Math.Max(1, Bounds.Height + deltaHeight)
        };
    }

    public void SetBounds(double? x, double? y, double? width, double? height)
    {
        Bounds = new SceneBounds(
            x ?? Bounds.X,
            y ?? Bounds.Y,
            Math.Max(1, width ?? Bounds.Width),
            Math.Max(1, height ?? Bounds.Height));
    }

    public ElementStudioItemViewModel CreateCopy(int index)
    {
        var sourceElementId = $"{SourceElementId}-copy-{index:000}";
        var item = Item with
        {
            SourceElementId = sourceElementId,
            SourceName = $"{SourceName} Copy",
            BoundsRelativeToPackage = Bounds
        };

        return new ElementStudioItemViewModel(item, CreateElementPlusName(index));
    }

    public ElementStudioLegacyItem ToLegacyItem()
    {
        return Item with
        {
            BoundsRelativeToPackage = Bounds,
            RawMetadataJson = CreateStateMetadataJson()
        };
    }

    private string? CreateStateMetadataJson()
    {
        if (string.IsNullOrWhiteSpace(GroupId) && !IsLocked)
        {
            return Item.RawMetadataJson;
        }

        var metadata = new List<string>();
        if (!string.IsNullOrWhiteSpace(GroupId))
        {
            metadata.Add($"\"groupId\":\"{GroupId}\"");
        }

        if (IsLocked)
        {
            metadata.Add("\"isLocked\":true");
        }

        return $"{{{string.Join(",", metadata)}}}";
    }

    internal ElementStudioItemSnapshot CreateSnapshot()
    {
        return new ElementStudioItemSnapshot(
            Item,
            ElementName,
            Bounds,
            IsLocked,
            IsVisible,
            GroupId);
    }

    internal static ElementStudioItemViewModel FromSnapshot(ElementStudioItemSnapshot snapshot)
    {
        return new ElementStudioItemViewModel(snapshot);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string CreateElementPlusName(int index)
    {
        return $"Element{index:000}";
    }
}
