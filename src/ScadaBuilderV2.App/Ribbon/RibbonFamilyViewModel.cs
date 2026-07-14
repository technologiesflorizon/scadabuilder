using System.Windows.Input;
using System.Windows.Media;

namespace ScadaBuilderV2.App.Ribbon;

/// <summary>View model for a level-one Insert ribbon family.</summary>
public sealed record RibbonFamilyViewModel(
    string Id,
    string Label,
    ImageSource? Icon,
    bool IsActive,
    ICommand Command);
