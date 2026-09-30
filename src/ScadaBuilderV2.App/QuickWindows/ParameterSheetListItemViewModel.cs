using System.Collections.ObjectModel;
using System.ComponentModel;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>Presentation model for one parameter sheet of the project group (DEC-0052).</summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetEditorViewModelTests.cs.
/// </remarks>
public sealed class ParameterSheetListItemViewModel : INotifyPropertyChanged
{
    private ParameterSheet sheet;
    private bool isSelected;

    /// <summary>Creates the presentation model for one sheet.</summary>
    public ParameterSheetListItemViewModel(ParameterSheet sheet) => this.sheet = sheet;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the durable sheet key used for routing only.</summary>
    public Guid SheetKey => sheet.SheetKey;

    /// <summary>Gets the portable project-local code.</summary>
    public string Code => sheet.Code;

    /// <summary>Gets the authoring display name.</summary>
    public string DisplayName => sheet.DisplayName;

    /// <summary>Gets the secondary label shown under the name.</summary>
    public string SubtitleLabel => Code;

    /// <summary>Gets the searchable text of this entry.</summary>
    public string SearchText => $"{Code} {DisplayName}";

    /// <summary>Gets or sets whether this entry is selected in the panel.</summary>
    public bool IsSelected
    {
        get => isSelected;
        set { if (isSelected == value) return; isSelected = value; OnChanged(nameof(IsSelected)); }
    }

    /// <summary>Refreshes this entry from the current sheet.</summary>
    public void Update(ParameterSheet updated)
    {
        sheet = updated;
        foreach (var name in new[] { nameof(Code), nameof(DisplayName), nameof(SubtitleLabel), nameof(SearchText) })
        {
            OnChanged(name);
        }
    }

    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Parameter-sheet inventory of the project, listed by the "Fenêtres rapides" panel (DEC-0052).</summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetEditorViewModelTests.cs.
/// </remarks>
public sealed class ParameterSheetsPanelViewModel : INotifyPropertyChanged
{
    private ParameterSheetListItemViewModel? selected;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the sheets of the project, in durable order.</summary>
    public ObservableCollection<ParameterSheetListItemViewModel> Items { get; } = [];

    /// <summary>Gets or sets the selected sheet.</summary>
    public ParameterSheetListItemViewModel? Selected
    {
        get => selected;
        set
        {
            if (ReferenceEquals(selected, value)) return;
            if (selected is not null) selected.IsSelected = false;
            selected = value;
            if (selected is not null) selected.IsSelected = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
        }
    }

    /// <summary>Reloads the inventory from the project, preserving selection by key.</summary>
    public void Load(IReadOnlyList<ParameterSheet> sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        var previousKey = Selected?.SheetKey;
        Items.Clear();
        foreach (var sheet in sheets)
        {
            var item = new ParameterSheetListItemViewModel(sheet);
            Items.Add(item);
        }

        Selected = previousKey is { } key ? Items.FirstOrDefault(item => item.SheetKey == key) : null;
    }
}
