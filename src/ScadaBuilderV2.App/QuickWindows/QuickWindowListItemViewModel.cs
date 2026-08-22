using System.Collections.ObjectModel;
using System.ComponentModel;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>Presentation model for one quick-window definition of the project group.</summary>
/// <remarks>
/// Decisions: DEC-0050, FR-032, FR-UI-12, FR-UI-24.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowShellContractTests.cs.
/// </remarks>
public sealed class QuickWindowListItemViewModel : INotifyPropertyChanged
{
    private QuickWindowDefinition definition;
    private bool isSelected;
    private int usageCount;
    private int outdatedInvocationCount;

    /// <summary>Creates the presentation model for one definition.</summary>
    public QuickWindowListItemViewModel(QuickWindowDefinition definition) => this.definition = definition;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the durable definition key used for routing only.</summary>
    public Guid DefinitionKey => definition.DefinitionKey;

    /// <summary>Gets the portable project-local code.</summary>
    public string Code => definition.EffectiveCode;

    /// <summary>Gets the authoring display name.</summary>
    public string DisplayName => definition.DisplayName;

    /// <summary>Gets the current local-interface version.</summary>
    public int InterfaceVersion => definition.InterfaceVersion;

    /// <summary>Gets the secondary label: code and interface version.</summary>
    public string SubtitleLabel => $"{Code} · interface v{InterfaceVersion}";

    /// <summary>Gets the number of invocations pointing at this definition.</summary>
    public int UsageCount => usageCount;

    /// <summary>Gets the usage label shown under the name.</summary>
    public string UsageLabel => usageCount switch
    {
        0 => "Aucun appelant",
        1 => "1 appelant",
        _ => $"{usageCount} appelants"
    };

    /// <summary>Gets whether at least one invocation is outdated and blocks build/export.</summary>
    public bool HasOutdatedInvocations => outdatedInvocationCount > 0;

    /// <summary>Gets the repair label for outdated invocations.</summary>
    public string OutdatedLabel => outdatedInvocationCount switch
    {
        0 => string.Empty,
        1 => "1 invocation à réparer",
        _ => $"{outdatedInvocationCount} invocations à réparer"
    };

    /// <summary>Gets the searchable text of this entry.</summary>
    public string SearchText => $"{Code} {DisplayName}";

    /// <summary>Gets or sets whether this entry is selected in the panel.</summary>
    public bool IsSelected
    {
        get => isSelected;
        set { if (isSelected == value) return; isSelected = value; OnChanged(nameof(IsSelected)); }
    }

    /// <summary>Refreshes this entry from the current definition and its analysis counters.</summary>
    public void Update(QuickWindowDefinition updated, int usages = 0, int outdatedInvocations = 0)
    {
        definition = updated;
        usageCount = usages;
        outdatedInvocationCount = outdatedInvocations;
        foreach (var name in new[]
                 {
                     nameof(Code), nameof(DisplayName), nameof(InterfaceVersion), nameof(SubtitleLabel),
                     nameof(UsageCount), nameof(UsageLabel), nameof(HasOutdatedInvocations),
                     nameof(OutdatedLabel), nameof(SearchText)
                 })
        {
            OnChanged(name);
        }
    }

    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Quick-window inventory of the project, kept separate from pages and from the Element+ library.</summary>
/// <remarks>
/// Decisions: DEC-0050, FR-001, FR-UI-12.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowShellContractTests.cs.
/// </remarks>
public sealed class QuickWindowsPanelViewModel : INotifyPropertyChanged
{
    private QuickWindowListItemViewModel? selected;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the definitions of the project, in durable order.</summary>
    public ObservableCollection<QuickWindowListItemViewModel> Items { get; } = [];

    /// <summary>Gets or sets the selected definition.</summary>
    public QuickWindowListItemViewModel? Selected
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
    public void Load(
        IReadOnlyList<QuickWindowDefinition> definitions,
        IReadOnlyDictionary<Guid, int>? usagesByDefinition = null,
        IReadOnlyDictionary<Guid, int>? outdatedByDefinition = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var previousKey = Selected?.DefinitionKey;
        Items.Clear();
        foreach (var definition in definitions)
        {
            var item = new QuickWindowListItemViewModel(definition);
            item.Update(
                definition,
                usagesByDefinition is not null && usagesByDefinition.TryGetValue(definition.DefinitionKey, out var usages) ? usages : 0,
                outdatedByDefinition is not null && outdatedByDefinition.TryGetValue(definition.DefinitionKey, out var outdated) ? outdated : 0);
            Items.Add(item);
        }

        Selected = previousKey is { } key
            ? Items.FirstOrDefault(item => item.DefinitionKey == key)
            : null;
    }
}
