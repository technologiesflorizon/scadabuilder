using System.IO;
using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Application.Projects;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.App.Projects;

/// <summary>Collects and validates user intent for one native project workspace.</summary>
public partial class CreateProjectDialog : Window
{
    private bool directoryNameWasEdited;

    public CreateProjectDialog(string initialParent)
    {
        InitializeComponent();
        ParentDirectoryTextBox.Text = initialParent;
        PageCodeTextBox.Text = "win00001";
        PageTitleTextBox.Text = "Page principale";
        WidthTextBox.Text = "1280";
        HeightTextBox.Text = "873";
        ResponsiveModeComboBox.ItemsSource = Enum.GetValues<ResponsiveMode>();
        ResponsiveModeComboBox.SelectedItem = ResponsiveMode.Fixed;
        RefreshValidation();
    }

    /// <summary>Gets the validated request after the dialog is accepted.</summary>
    public CreateProjectRequest? Result { get; private set; }

    private void OnInputChanged(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, ProjectDirectoryNameTextBox) && ProjectDirectoryNameTextBox.IsKeyboardFocusWithin)
        {
            directoryNameWasEdited = true;
        }
        if (ReferenceEquals(sender, ProjectNameTextBox) && !directoryNameWasEdited)
        {
            ProjectDirectoryNameTextBox.Text = SanitizeDirectoryName(ProjectNameTextBox.Text);
        }
        RefreshValidation();
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choisir le dossier parent du projet",
            InitialDirectory = Directory.Exists(ParentDirectoryTextBox.Text)
                ? ParentDirectoryTextBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            ParentDirectoryTextBox.Text = dialog.FolderName;
        }
    }

    private void OnCreateClick(object sender, RoutedEventArgs e)
    {
        var request = BuildRequest();
        var validation = ProjectWorkspacePathPolicy.ValidateCreation(request);
        if (validation.ProjectRoot is null || validation.Diagnostics.Count > 0)
        {
            RefreshValidation();
            return;
        }

        Result = request;
        DialogResult = true;
    }

    private void RefreshValidation()
    {
        if (!IsInitialized)
        {
            return;
        }
        var request = BuildRequest();
        var validation = ProjectWorkspacePathPolicy.ValidateCreation(request);
        FinalPathTextBlock.Text = validation.ProjectRoot ??
            (string.IsNullOrWhiteSpace(ParentDirectoryTextBox.Text)
                ? ""
                : Path.Combine(ParentDirectoryTextBox.Text, ProjectDirectoryNameTextBox.Text));
        ValidationTextBlock.Text = validation.Diagnostics.Count == 0
            ? "Le projet est prêt à être créé."
            : string.Join(Environment.NewLine, validation.Diagnostics.Select(issue => $"• {issue.Message}"));
        CreateButton.IsEnabled = validation.ProjectRoot is not null && validation.Diagnostics.Count == 0;
    }

    private CreateProjectRequest BuildRequest()
    {
        _ = int.TryParse(WidthTextBox?.Text, out var width);
        _ = int.TryParse(HeightTextBox?.Text, out var height);
        return new CreateProjectRequest(
            ProjectNameTextBox?.Text ?? "",
            ParentDirectoryTextBox?.Text ?? "",
            ProjectDirectoryNameTextBox?.Text ?? "",
            PageCodeTextBox?.Text ?? "",
            PageTitleTextBox?.Text ?? "",
            new CanvasSize(width, height),
            ResponsiveModeComboBox?.SelectedItem is ResponsiveMode responsive ? responsive : ResponsiveMode.Fixed,
            AuthoringMode.DesktopFirst);
    }

    private static string SanitizeDirectoryName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        return string.Concat(value.Trim().Select(character => invalid.Contains(character) ? '_' : character));
    }
}
