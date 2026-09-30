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
    }

    /// <summary>Raised when the operator picks a sheet in the sheet selector.</summary>
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

    private void OnSheetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SheetSelectorComboBox.SelectedItem is ParameterSheet sheet)
        {
            SheetSelected?.Invoke(this, sheet.SheetKey);
        }
    }

    private void OnClosePreviewClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
