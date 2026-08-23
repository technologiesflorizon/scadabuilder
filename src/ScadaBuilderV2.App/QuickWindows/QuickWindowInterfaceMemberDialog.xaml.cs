using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// Shared member dialog of the local interface: it authors a new member and edits the advanced
/// properties and constraints of an existing one (FR-UI-16).
/// </summary>
/// <remarks>
/// The dialog never mutates the workspace: it returns a validated candidate member that the workspace
/// controller applies as one undoable transition.
///
/// Decisions: DEC-0050, FR-005, FR-006, FR-UI-16.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceAuthoringTests.cs.
/// </remarks>
public partial class QuickWindowInterfaceMemberDialog : Window
{
    private readonly QuickWindowInterfaceMemberDraft draft;
    private readonly IReadOnlyList<QuickWindowInterfaceMember> siblings;
    private bool isLoading = true;

    /// <summary>Creates the dialog on one draft and the sibling members used for validation.</summary>
    public QuickWindowInterfaceMemberDialog(
        string title,
        QuickWindowInterfaceMemberDraft draft,
        IReadOnlyList<QuickWindowInterfaceMember> siblings)
    {
        ArgumentNullException.ThrowIfNull(draft);
        this.draft = draft;
        this.siblings = siblings ?? Array.Empty<QuickWindowInterfaceMember>();
        InitializeComponent();

        Title = title;
        PromptText.Text = title;
        FamilyComboBox.ItemsSource = QuickWindowInterfaceOptions.Families;
        DataTypeComboBox.ItemsSource = QuickWindowInterfaceOptions.DataTypes;
        AccessComboBox.ItemsSource = QuickWindowInterfaceOptions.Accesses;

        NameTextBox.Text = draft.Name;
        FamilyComboBox.SelectedValue = draft.Family;
        DataTypeComboBox.SelectedValue = draft.DataType;
        RequiredCheckBox.IsChecked = draft.Required;
        DefaultValueTextBox.Text = draft.DefaultValue ?? string.Empty;
        DescriptionTextBox.Text = draft.Description ?? string.Empty;
        isLoading = false;
        RefreshAccessOptions(draft.Access);
        Loaded += (_, _) => NameTextBox.Focus();
    }

    /// <summary>Gets the authored member once the dialog was accepted.</summary>
    public QuickWindowInterfaceMember? AuthoredMember { get; private set; }

    private void OnFamilyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoading) return;
        if (FamilyComboBox.SelectedValue is not QuickWindowInterfaceFamily family) return;
        draft.Family = family;
        RequiredCheckBox.IsChecked = draft.Required;
        RefreshAccessOptions(draft.Access);
    }

    private void RefreshAccessOptions(QuickWindowMemberAccess current)
    {
        if (FamilyComboBox.SelectedValue is not QuickWindowInterfaceFamily family) return;
        var allowed = QuickWindowInterfaceMemberRules.AllowedAccesses(family);
        AccessComboBox.ItemsSource = QuickWindowInterfaceOptions.Accesses
            .Where(option => allowed.Contains((QuickWindowMemberAccess)option.Value))
            .ToArray();
        AccessComboBox.SelectedValue = allowed.Contains(current) ? current : allowed[0];
        AccessComboBox.IsEnabled = allowed.Count > 1;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        draft.Name = NameTextBox.Text;
        if (FamilyComboBox.SelectedValue is QuickWindowInterfaceFamily family) draft.Family = family;
        if (DataTypeComboBox.SelectedValue is QuickWindowDataType dataType) draft.DataType = dataType;
        if (AccessComboBox.SelectedValue is QuickWindowMemberAccess access) draft.Access = access;
        draft.Required = RequiredCheckBox.IsChecked == true;
        draft.DefaultValue = DefaultValueTextBox.Text;
        draft.Description = DescriptionTextBox.Text;

        var issues = draft.Validate(siblings);
        if (issues.Count > 0)
        {
            ErrorText.Text = string.Join(Environment.NewLine, issues);
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        AuthoredMember = draft.ToMember();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
