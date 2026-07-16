using System.Windows;
using ScadaBuilderV2.Application.Tables;

namespace ScadaBuilderV2.App.TableEditor;

/// <summary>Presents numeric table-cell properties and returns typed edit intentions.</summary>
public partial class TableNumericInputPropertiesDialog : Window
{
    private readonly TableNumericInputPropertiesViewModel viewModel;
    private bool isSynchronizingBindings;

    internal TableNumericInputPropertiesDialog(TableNumericInputPropertiesViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        Title = $"Input numerique — {viewModel.CellAddress}";
        TargetSummaryText.Text = viewModel.TargetSummary;
        InitialValueTextBox.Text = viewModel.InitialValue;
        PlaceholderTextBox.Text = viewModel.Placeholder;
        MinimumTextBox.Text = viewModel.Minimum;
        MaximumTextBox.Text = viewModel.Maximum;
        StepTextBox.Text = viewModel.Step;
        DisplayFormatTextBox.Text = viewModel.DisplayFormat;
        ReadOnlyCheckBox.IsChecked = viewModel.IsReadOnly;
        ReadBindingSummaryText.Text = viewModel.ReadBindingSummary;
        WriteBindingSummaryText.Text = viewModel.WriteBindingSummary;
        isSynchronizingBindings = true;
        ReadTagComboBox.ItemsSource = viewModel.ReadTags;
        WriteTagComboBox.ItemsSource = viewModel.WriteTags;
        ReadTagComboBox.SelectedValue = viewModel.SelectedReadTagId;
        WriteTagComboBox.SelectedValue = viewModel.SelectedWriteTagId;
        isSynchronizingBindings = false;
        ReadDefaultNoticeText.Text = viewModel.ReadDefaultNotice;
    }

    internal IReadOnlyList<TableEditRequest>? Result { get; private set; }

    private void OnChooseReadBinding(object sender, RoutedEventArgs e) => ReadTagComboBox.IsDropDownOpen = true;

    private void OnRemoveReadBinding(object sender, RoutedEventArgs e) => ReadTagComboBox.SelectedItem = null;

    private void OnChooseWriteBinding(object sender, RoutedEventArgs e) => WriteTagComboBox.IsDropDownOpen = true;

    private void OnRemoveWriteBinding(object sender, RoutedEventArgs e) => WriteTagComboBox.SelectedItem = null;

    private void OnWriteTagSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (isSynchronizingBindings) return;
        var bindings = viewModel.UpdateBindingDraft(
            ReadTagComboBox.SelectedValue as string,
            WriteTagComboBox.SelectedValue as string,
            ReadOnlyCheckBox.IsChecked == true);
        isSynchronizingBindings = true;
        ReadTagComboBox.SelectedValue = bindings.ReadTagId;
        isSynchronizingBindings = false;
        ReadDefaultNoticeText.Text = viewModel.ReadDefaultNotice;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        viewModel.UpdateDraft(
            InitialValueTextBox.Text,
            PlaceholderTextBox.Text,
            MinimumTextBox.Text,
            MaximumTextBox.Text,
            StepTextBox.Text,
            DisplayFormatTextBox.Text,
            ReadOnlyCheckBox.IsChecked == true,
            ReadTagComboBox.SelectedValue as string,
            WriteTagComboBox.SelectedValue as string);
        if (!viewModel.TryBuildRequests(out var requests, out var error))
        {
            MessageBox.Show(this, error ?? "Les propriétés sont invalides.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = requests;
        DialogResult = true;
    }
}
