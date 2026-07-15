using System.ComponentModel;
using System.Runtime.CompilerServices;
using ScadaBuilderV2.Application.Selection;

namespace ScadaBuilderV2.App.ElementLock;

/// <summary>Projects one application-derived Element+ lock state to every WPF lock surface.</summary>
/// <remarks>Decisions: DEC-0040. Tests: tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs.</remarks>
public sealed class ElementLockStateViewModel : INotifyPropertyChanged
{
    private bool isEnabled;
    private bool isToggleChecked;
    private bool isMixed;
    private bool? isPropertyChecked;
    private string summary = "Aucun Element+ selectionne";
    private string actionLabel = "Verrouiller";
    private string indicatorLabel = "Aucune selection";

    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsEnabled { get => isEnabled; private set => Set(ref isEnabled, value); }
    public bool IsToggleChecked { get => isToggleChecked; private set => Set(ref isToggleChecked, value); }
    public bool IsMixed { get => isMixed; private set => Set(ref isMixed, value); }
    public bool? IsPropertyChecked { get => isPropertyChecked; private set => Set(ref isPropertyChecked, value); }
    public string Summary { get => summary; private set => Set(ref summary, value); }
    public string ActionLabel { get => actionLabel; private set => Set(ref actionLabel, value); }
    public string IndicatorLabel { get => indicatorLabel; private set => Set(ref indicatorLabel, value); }

    /// <summary>Refreshes all surface values from one aggregated Application state.</summary>
    public void Update(ElementLockSelectionState state)
    {
        IsEnabled = state.HasSelection;
        IsToggleChecked = state.HasSelection && state.AllLocked;
        IsMixed = state.HasSelection && state.IsMixed;
        IsPropertyChecked = !state.HasSelection ? false : state.IsMixed ? null : state.AllLocked;
        Summary = !state.HasSelection ? "Aucun Element+ selectionne" : state.IsMixed ? "Selection partiellement verrouillee" : state.AllLocked ? "Selection verrouillee" : "Selection deverrouillee";
        ActionLabel = state.AllLocked ? "Deverrouiller" : "Verrouiller";
        IndicatorLabel = !state.HasSelection ? "Aucune selection" : state.IsMixed ? "Verrou mixte" : state.AllLocked ? "Verrouille" : "Deverrouille";
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
