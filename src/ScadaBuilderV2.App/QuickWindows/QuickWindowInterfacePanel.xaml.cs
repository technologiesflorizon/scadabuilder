using System.Windows.Controls;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// `Interface locale` surface: a read-only list of the `#N` placeholders used by the active quick-window
/// page and their locations (DEC-0052). There is no member to add, edit or delete any more — the page's
/// own content is the only source.
/// </summary>
/// <remarks>
/// The control owns no decision and raises nothing: it is a pure read-only projection of
/// <see cref="QuickWindowInterfacePanelViewModel.LoadPlaceholders"/>.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceViewModelTests.cs.
/// </remarks>
public partial class QuickWindowInterfacePanel : UserControl
{
    private QuickWindowInterfacePanelViewModel? model;

    /// <summary>Creates the panel.</summary>
    public QuickWindowInterfacePanel()
    {
        InitializeComponent();
    }

    /// <summary>Gets the bound panel model, or null before <see cref="Bind"/> is first called.</summary>
    public QuickWindowInterfacePanelViewModel? Model => model;

    /// <summary>Binds the panel to the local interface of the active quick-window page.</summary>
    public void Bind(QuickWindowInterfacePanelViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        model = viewModel;
        DataContext = viewModel;
    }
}
