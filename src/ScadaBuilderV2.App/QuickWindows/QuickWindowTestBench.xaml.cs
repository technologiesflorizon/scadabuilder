using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// `Banc d'essai`: a sheet selector previewing the active quick-window page with one parameter sheet's
/// values (DEC-0052), through the real mount-time substitution.
/// </summary>
/// <remarks>
/// The control owns no lifecycle policy and no persistence: it raises the operator's sheet choice, the
/// shell lowers the (sheet, page) pair on a snapshot and materializes the preview through
/// <see cref="BuilderQuickWindowHostAdapter"/>; the shared <c>ScadaRuntime.QuickWindow</c> module owns the
/// instance semantics.
///
/// Decisions: DEC-0050, DEC-0052, FR-UI-21.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPreviewTests.cs.
/// </remarks>
public partial class QuickWindowTestBench : UserControl
{
    /// <summary>Creates the bench bound to its own editor-only model.</summary>
    public QuickWindowTestBench()
    {
        InitializeComponent();
        DataContext = Model;
        Model.SheetSelected += (_, sheetKey) => SheetSelected?.Invoke(this, sheetKey);
    }

    /// <summary>
    /// Raised when the operator genuinely picks a sheet in the sheet selector — a pure relay of
    /// <see cref="QuickWindowTestBenchViewModel.SheetSelected"/>, which is the single place a programmatic
    /// resync (<see cref="QuickWindowTestBenchViewModel.LoadPage"/>) is told apart from a real pick
    /// (controller fix round 2).
    /// </summary>
    public event EventHandler<Guid>? SheetSelected;

    /// <summary>Raised when the operator closes the preview instance.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Gets the editor-only bench model.</summary>
    public QuickWindowTestBenchViewModel Model { get; } = new();

    /// <summary>Shows one materialized preview instance in the bench.</summary>
    public void ShowPreview(Uri source)
    {
        ArgumentNullException.ThrowIfNull(source);
        PreviewPlaceholderText.Visibility = Visibility.Collapsed;
        PreviewInstanceWebView.Visibility = Visibility.Visible;
        PreviewInstanceWebView.Source = source;
    }

    /// <summary>Hides the preview instance without touching any durable state.</summary>
    public void HidePreview(string? message = null)
    {
        PreviewInstanceWebView.Visibility = Visibility.Collapsed;
        PreviewPlaceholderText.Visibility = Visibility.Visible;
        if (!string.IsNullOrWhiteSpace(message)) Model.Status = message!;
    }

    /// <summary>
    /// Forwards every `SelectionChanged` unconditionally, including one the `SelectedValue` binding
    /// itself triggered while <see cref="QuickWindowTestBenchViewModel.LoadPage"/> rebuilds `Sheets`: the
    /// guard lives entirely in the view model's <see cref="QuickWindowTestBenchViewModel.SelectSheet"/>
    /// (an unchanged key is a no-op there), never here (controller fix round 2).
    /// </summary>
    private void OnSheetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SheetSelectorComboBox.SelectedItem is ParameterSheet sheet)
        {
            Model.SelectSheet(sheet.SheetKey);
        }
    }

    private void OnClosePreviewClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
