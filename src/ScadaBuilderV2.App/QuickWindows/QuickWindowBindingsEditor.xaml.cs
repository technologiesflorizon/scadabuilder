using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// `Liaisons` authoring surface of one `OpenQuickWindow` command: a typed grid where every public port
/// chooses its source kind first, then its contextual value.
/// </summary>
/// <remarks>
/// The control owns no decision: it validates the authored bindings against the domain and raises one save
/// request that the shell applies through the quick-window invocation service.
///
/// Decisions: DEC-0050, FR-009, FR-010, FR-UI-18, FR-UI-19, FR-UI-20, FR-UI-24.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBindingAuthoringTests.cs.
/// </remarks>
public partial class QuickWindowBindingsEditor : UserControl
{
    /// <summary>Creates the bindings grid bound to its own editor model.</summary>
    public QuickWindowBindingsEditor()
    {
        InitializeComponent();
        DataContext = Model;
    }

    /// <summary>Raised when the operator saves the authored bindings.</summary>
    public event EventHandler<QuickWindowInvocationAuthoringRequest>? SaveRequested;

    /// <summary>Gets the editor model of the shown invocation.</summary>
    public QuickWindowBindingsEditorViewModel Model { get; } = new();

    /// <summary>Gets or sets the caller command id whose invocation is authored.</summary>
    public string? CommandId { get; set; }

    /// <summary>Loads one definition and its persisted invocation into the grid.</summary>
    public void Load(
        string commandId,
        QuickWindowDefinition definition,
        QuickWindowInvocation? invocation,
        ScadaTagCatalog? tagCatalog,
        IReadOnlyList<QuickWindowInterfaceMember>? parentPorts)
    {
        CommandId = commandId;
        Model.Load(definition, invocation, tagCatalog, parentPorts);
    }

    /// <summary>Clears the grid when the selected command is not an `OpenQuickWindow` command.</summary>
    public void Clear()
    {
        CommandId = null;
        Model.Clear();
    }

    private void OnSaveBindingsClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (string.IsNullOrWhiteSpace(CommandId)) return;
        if (Model.Validate().Count > 0) return;
        if (Model.ToRequest(CommandId!) is { } request) SaveRequested?.Invoke(this, request);
    }
}
