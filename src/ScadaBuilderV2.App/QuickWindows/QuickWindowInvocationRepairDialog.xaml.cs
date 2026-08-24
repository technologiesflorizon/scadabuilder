using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// Repair surface of the invocations left `Outdated` by a local-interface change: page, caller element and
/// incompatibility reason, navigation to the caller and explicit port-by-port relinking (FR-UI-24).
/// </summary>
/// <remarks>
/// The dialog never repairs automatically nor in bulk: the operator relinks one invocation at a time and the
/// realignment happens only when nothing breaks any more. Build and export stay blocked until every
/// invocation is repaired.
///
/// Decisions: DEC-0050, FR-032, FR-UI-24.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceVersioningTests.cs.
/// </remarks>
public partial class QuickWindowInvocationRepairDialog : Window
{
    private readonly QuickWindowDefinition definition;
    private readonly IReadOnlyList<QuickWindowInvocation> invocations;
    private readonly ScadaTagCatalog? tagCatalog;

    /// <summary>Creates the repair surface of one definition.</summary>
    public QuickWindowInvocationRepairDialog(
        QuickWindowDefinition definition,
        IReadOnlyList<QuickWindowRepairRowViewModel> rows,
        IReadOnlyList<QuickWindowInvocation> invocations,
        ScadaTagCatalog? tagCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(rows);
        this.definition = definition;
        this.invocations = invocations ?? [];
        this.tagCatalog = tagCatalog;
        InitializeComponent();

        DefinitionLabelText.Text = $"{definition.DisplayName} · interface v{definition.InterfaceVersion}";
        OutdatedSummaryText.Text = rows.Count == 1
            ? "1 invocation à réparer."
            : $"{rows.Count} invocations à réparer.";
        OutdatedInvocationsDataGrid.ItemsSource = rows;
        if (rows.Count > 0) OutdatedInvocationsDataGrid.SelectedIndex = 0;
    }

    /// <summary>Gets the invocation the operator asked to repair or navigate to, when there is one.</summary>
    public Guid? SelectedInvocationKey => (OutdatedInvocationsDataGrid.SelectedItem as QuickWindowRepairRowViewModel)?.InvocationKey;

    /// <summary>Gets the bindings relinked port by port for <see cref="SelectedInvocationKey"/>.</summary>
    public IReadOnlyList<QuickWindowBinding> RepairedBindings { get; private set; } = [];

    /// <summary>Gets whether the operator asked to navigate to the caller instead of repairing.</summary>
    public bool NavigationRequested { get; private set; }

    private void OnSelectedInvocationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutdatedInvocationsDataGrid.SelectedItem is not QuickWindowRepairRowViewModel row)
        {
            RepairBindingsEditor.Clear();
            return;
        }

        var invocation = invocations.FirstOrDefault(item => item.InvocationKey == row.InvocationKey);
        RepairBindingsEditor.Load(
            row.Outdated.OwnerCommandId ?? string.Empty,
            definition,
            invocation,
            tagCatalog,
            []);
    }

    private void OnRepairClick(object sender, RoutedEventArgs e)
    {
        if (SelectedInvocationKey is null) return;
        if (RepairBindingsEditor.Model.Validate().Count > 0) return;
        RepairedBindings = RepairBindingsEditor.Model.ToBindings();
        NavigationRequested = false;
        DialogResult = true;
    }

    private void OnNavigateToCallerClick(object sender, RoutedEventArgs e)
    {
        if (SelectedInvocationKey is null) return;
        NavigationRequested = true;
        DialogResult = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
