using System.Windows;
using System.Windows.Controls;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// `Interface locale` authoring surface: one single table grouped in `Interface publique` and
/// `Données privées`, filtered by family and free text, with inline edition of the common properties,
/// a shared dialog for the advanced ones and a usage counter per member.
/// </summary>
/// <remarks>
/// The control owns no decision: every edit is raised to the shell, which delegates to
/// <see cref="QuickWindowWorkspaceController"/> so the mutation stays undoable on the single workspace stack.
///
/// Decisions: DEC-0050, FR-005, FR-UI-15, FR-UI-16, FR-UI-17, FR-UI-20.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.1.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceAuthoringTests.cs.
/// </remarks>
public partial class QuickWindowInterfacePanel : UserControl
{
    private QuickWindowInterfacePanelViewModel? model;

    /// <summary>Creates the panel with its labelled inline selectors.</summary>
    public QuickWindowInterfacePanel()
    {
        InitializeComponent();
        FamilyColumn.ItemsSource = QuickWindowInterfaceOptions.Families;
        DataTypeColumn.ItemsSource = QuickWindowInterfaceOptions.DataTypes;
        AccessColumn.ItemsSource = QuickWindowInterfaceOptions.Accesses;
    }

    /// <summary>Raised when the operator asks for a new member.</summary>
    public event EventHandler? AddMemberRequested;

    /// <summary>Raised when the operator asks for the advanced properties of one member.</summary>
    public event EventHandler<QuickWindowInterfaceMemberViewModel>? EditMemberRequested;

    /// <summary>Raised when the operator asks to delete one member.</summary>
    public event EventHandler<QuickWindowInterfaceMemberViewModel>? DeleteMemberRequested;

    /// <summary>Raised when the operator asks to navigate to the usages of one member.</summary>
    public event EventHandler<QuickWindowInterfaceMemberViewModel>? NavigateToUsageRequested;

    /// <summary>Raised when one inline table edit produced a new candidate member.</summary>
    public event EventHandler<QuickWindowInterfaceMemberViewModel>? MemberInlineEdited;

    /// <summary>Binds the panel to the local interface of the active definition.</summary>
    public void Bind(QuickWindowInterfacePanelViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (model is not null) model.MemberInlineEdited -= OnMemberInlineEdited;
        model = viewModel;
        model.MemberInlineEdited += OnMemberInlineEdited;
        DataContext = viewModel;
    }

    private void OnMemberInlineEdited(object? sender, QuickWindowInterfaceMemberViewModel member) =>
        MemberInlineEdited?.Invoke(this, member);

    private void OnAddMemberClick(object sender, RoutedEventArgs e)
    {
        AddMemberRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnEditMemberClick(object sender, RoutedEventArgs e)
    {
        if (model?.Selected is { } selected) EditMemberRequested?.Invoke(this, selected);
        e.Handled = true;
    }

    private void OnDeleteMemberClick(object sender, RoutedEventArgs e)
    {
        if (model?.Selected is { } selected) DeleteMemberRequested?.Invoke(this, selected);
        e.Handled = true;
    }

    private void OnNavigateToUsageClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: QuickWindowInterfaceMemberViewModel member })
        {
            NavigateToUsageRequested?.Invoke(this, member);
        }

        e.Handled = true;
    }

    private void OnMembersDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (model?.Selected is not { } selected) return;
        EditMemberRequested?.Invoke(this, selected);
        e.Handled = true;
    }
}
