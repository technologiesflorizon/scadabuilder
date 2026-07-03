using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Application.Libraries;

namespace ScadaBuilderV2.App;

public partial class ConfigurationWindow : Window
{
    public ConfigurationWindow(LibraryRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Registry = registry;
        InitializeComponent();
        RefreshLibraryListView();
    }

    public LibraryRegistry Registry { get; }

    private void RefreshLibraryListView()
    {
        LibraryListView.ItemsSource = Registry.Entries;
        UpdateButtonStates();
    }

    private void OnLibraryListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var selected = LibraryListView.SelectedItem as LibraryEntry;
        RenameLibraryButton.IsEnabled = selected is not null;
        var isExternalSelected = selected is not null && !selected.IsDefault;
        ChangePathLibraryButton.IsEnabled = isExternalSelected;
        RemoveLibraryButton.IsEnabled = isExternalSelected;
    }

    private void OnAddLibraryClick(object sender, RoutedEventArgs e)
    {
        var folderDialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Selectionner le dossier de la librairie"
        };

        if (folderDialog.ShowDialog(this) != true)
        {
            return;
        }

        var defaultName = System.IO.Path.GetFileName(folderDialog.FolderName.TrimEnd('\\', '/'));
        var nameDialog = new LibraryNameDialog("Nom de la nouvelle librairie", defaultName) { Owner = this };
        if (nameDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            Registry.Add(nameDialog.EnteredName, folderDialog.FolderName);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, "Ajouter une librairie", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RefreshLibraryListView();
    }

    private void OnRenameLibraryClick(object sender, RoutedEventArgs e)
    {
        if (LibraryListView.SelectedItem is not LibraryEntry selected)
        {
            return;
        }

        var nameDialog = new LibraryNameDialog("Nouveau nom de la librairie", selected.Name) { Owner = this };
        if (nameDialog.ShowDialog() != true)
        {
            return;
        }

        Registry.Rename(selected.Name, nameDialog.EnteredName);
        RefreshLibraryListView();
    }

    private void OnChangePathLibraryClick(object sender, RoutedEventArgs e)
    {
        if (LibraryListView.SelectedItem is not LibraryEntry selected || selected.IsDefault)
        {
            return;
        }

        var folderDialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Selectionner le nouveau dossier de la librairie",
            InitialDirectory = selected.Path
        };

        if (folderDialog.ShowDialog(this) != true)
        {
            return;
        }

        Registry.UpdatePath(selected.Name, folderDialog.FolderName);
        RefreshLibraryListView();
    }

    private void OnRemoveLibraryClick(object sender, RoutedEventArgs e)
    {
        if (LibraryListView.SelectedItem is not LibraryEntry selected || selected.IsDefault)
        {
            return;
        }

        Registry.Remove(selected.Name);
        RefreshLibraryListView();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
