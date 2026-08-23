using System.Windows;
using System.Windows.Controls;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// Editor-only test bench of a quick-window definition: temporary values, temporary bindings and the
/// preview instance they feed (FR-UI-21).
/// </summary>
/// <remarks>
/// The control owns no lifecycle policy and no persistence: it raises the operator intent, the shell
/// materializes the preview through <see cref="BuilderQuickWindowHostAdapter"/> and the shared
/// <c>ScadaRuntime.QuickWindowHost</c> owns the instance semantics.
///
/// Decisions: DEC-0050, FR-UI-21.
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

    /// <summary>Raised when the operator asks to open a preview instance with the current values.</summary>
    public event EventHandler? PreviewRequested;

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

    private void OnOpenPreviewClick(object sender, RoutedEventArgs e)
    {
        PreviewRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnClosePreviewClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
