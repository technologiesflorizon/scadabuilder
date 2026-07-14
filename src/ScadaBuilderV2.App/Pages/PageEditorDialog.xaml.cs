using System.Windows;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App.Pages;

public enum PageEditorDialogMode { New, Rename, Duplicate }

/// <summary>Shared modern dialog for page creation, rename and duplication.</summary>
public partial class PageEditorDialog : Window
{
    private readonly PageEditorDialogMode mode;

    public PageEditorDialog(PageEditorDialogMode mode, string pageCode, string pageTitle)
    {
        InitializeComponent();
        this.mode = mode;
        HeadingText.Text = mode switch { PageEditorDialogMode.New => "Nouvelle page", PageEditorDialogMode.Rename => "Renommer la page", _ => "Dupliquer la page" };
        PageCodeTextBox.Text = pageCode;
        PageTitleTextBox.Text = pageTitle;
        PageCodeTextBox.IsEnabled = mode != PageEditorDialogMode.Rename;
        PageCodeTextBox.Focus();
        PageCodeTextBox.SelectAll();
    }

    public string PageCode => PageCodeTextBox.Text.Trim();
    public string PageTitle => PageTitleTextBox.Text.Trim();
    public PageEditorDialogMode Mode => mode;

    public static PageEditorDialog ForNew(string suggestedCode) => new(PageEditorDialogMode.New, suggestedCode, suggestedCode);

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (PageTitle.Length == 0)
        {
            ValidationText.Text = "Le titre est requis.";
            return;
        }
        if (mode != PageEditorDialogMode.Rename)
        {
            var validation = PageCodePolicy.Validate(PageCode);
            if (!validation.IsValid)
            {
                ValidationText.Text = validation.Errors[0];
                return;
            }
        }
        DialogResult = true;
    }
}
