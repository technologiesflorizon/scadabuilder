using System.Windows;

namespace ScadaBuilderV2.App;

public partial class LibraryNameDialog : Window
{
    public LibraryNameDialog(string prompt, string initialName)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        NameTextBox.Text = initialName;
        NameTextBox.SelectAll();
        Loaded += (_, _) => NameTextBox.Focus();
    }

    public string EnteredName { get; private set; } = "";

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var trimmed = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            ErrorText.Text = "Le nom ne peut pas etre vide.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        EnteredName = trimmed;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
