using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App;

/// <summary>
/// Modal editor of one Element+ command.
/// </summary>
/// <remarks>
/// Quick-window command kinds are offered by the bounded authoring context of the caller surface: a page may
/// open a definition, only a quick-window content may close itself as `Self`, and no Toggle kind exists
/// since `DEC-0050` removed the popup command kinds.
///
/// Decisions: DEC-0036, DEC-0050, FR-011, FR-013.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBindingAuthoringTests.cs.
/// </remarks>
public partial class ElementCommandDialog : Window
{
    private sealed record TagItem(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly IReadOnlyList<ScadaSceneReference> _pageReferences;
    private readonly string _commandId;
    private readonly ScadaCommandBinding? _existingCommand;

    private readonly IReadOnlyCollection<ScadaCommandKind> _usedKinds;
    private readonly QuickWindowCommandAuthoringContext _quickWindowContext;

    public ElementCommandDialog(
        ScadaCommandBinding? existingCommand,
        IReadOnlyList<ScadaSceneReference> pageReferences,
        ScadaTagCatalog? tagCatalog,
        IReadOnlyCollection<ScadaCommandKind> usedKinds,
        QuickWindowCommandAuthoringContext? quickWindowContext = null)
    {
        InitializeComponent();
        _pageReferences = pageReferences;
        _commandId = existingCommand?.Id ?? Guid.NewGuid().ToString("n");
        _existingCommand = existingCommand;
        _usedKinds = usedKinds;
        _quickWindowContext = quickWindowContext ?? QuickWindowCommandAuthoringContext.Empty;

        TriggerComboBox.ItemsSource = Enum.GetValues<ScadaCommandTrigger>();
        var quickWindowKinds = _quickWindowContext.AllowedCommandKinds();
        KindComboBox.ItemsSource = Enum.GetValues<ScadaCommandKind>()
            .Where(kind => kind is not ScadaCommandKind.OpenQuickWindow and not ScadaCommandKind.CloseQuickWindow || quickWindowKinds.Contains(kind))
            .Where(kind => !usedKinds.Contains(kind) || kind == existingCommand?.Kind)
            .ToArray();
        QuickWindowDefinitionComboBox.ItemsSource = _quickWindowContext.EffectiveDefinitions;
        WriteModeComboBox.ItemsSource = Enum.GetValues<ScadaWriteMode>();
        TargetPageComboBox.ItemsSource = pageReferences;

        var tagItems = (tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .Where(tag => tag.Enabled)
            .OrderBy(tag => tag.DisplayName)
            .Select(tag => new TagItem(tag.Id, tag.AuthoringLabel))
            .ToArray();
        WriteTagComboBox.ItemsSource = tagItems;
        ReadTagComboBox.ItemsSource = tagItems;

        if (existingCommand is not null)
        {
            NameTextBox.Text = existingCommand.Name;
            TriggerComboBox.SelectedItem = existingCommand.Trigger;
            KindComboBox.SelectedItem = existingCommand.Kind;
            WriteTagComboBox.SelectedItem = tagItems.FirstOrDefault(t => t.Id == existingCommand.WriteTagId);
            ReadTagComboBox.SelectedItem = tagItems.FirstOrDefault(t => t.Id == existingCommand.ReadTagId);
            WriteModeComboBox.SelectedItem = existingCommand.WriteMode;
            OnValueTextBox.Text = existingCommand.OnValue ?? string.Empty;
            OffValueTextBox.Text = existingCommand.OffValue ?? string.Empty;
            FixedValueTextBox.Text = existingCommand.FixedValue ?? string.Empty;
            TargetPageComboBox.SelectedItem = pageReferences.FirstOrDefault(p => p.Id == existingCommand.TargetPageId);
            QuickWindowDefinitionComboBox.SelectedItem = _quickWindowContext.ResolveDefinitionOfInvocation(existingCommand.QuickWindowInvocationKey);
            UrlTextBox.Text = existingCommand.Url ?? string.Empty;
            NewTabCheckBox.IsChecked = existingCommand.NewTab;
            ConfirmationCheckBox.IsChecked = existingCommand.Confirmation is not null;
            ConfirmationMessageTextBox.Text = existingCommand.Confirmation?.Message ?? string.Empty;
        }
        else
        {
            TriggerComboBox.SelectedIndex = 0;
            KindComboBox.SelectedIndex = 0;
        }

        UpdateKindPanels();
        UpdateWriteModePanels();
    }

    public ScadaCommandBinding? Result { get; private set; }

    /// <summary>
    /// Gets the definition targeted by an authored `OpenQuickWindow` command. The command itself only ever
    /// carries an `InvocationKey`; the shell creates or updates the invocation resolving this target.
    /// </summary>
    public Guid? SelectedQuickWindowDefinitionKey { get; private set; }

    private void OnKindChanged(object sender, SelectionChangedEventArgs e) => UpdateKindPanels();

    private void OnWriteModeChanged(object sender, SelectionChangedEventArgs e) => UpdateWriteModePanels();

    private void UpdateKindPanels()
    {
        var kind = (ScadaCommandKind?)KindComboBox.SelectedItem ?? ScadaCommandKind.WriteTag;
        WriteTagPanel.Visibility = kind == ScadaCommandKind.WriteTag ? Visibility.Visible : Visibility.Collapsed;
        PagePanel.Visibility = kind is ScadaCommandKind.Navigate
            ? Visibility.Visible
            : Visibility.Collapsed;
        UrlPanel.Visibility = kind == ScadaCommandKind.OpenUrl ? Visibility.Visible : Visibility.Collapsed;
        QuickWindowPanel.Visibility = kind == ScadaCommandKind.OpenQuickWindow ? Visibility.Visible : Visibility.Collapsed;
        CloseQuickWindowPanel.Visibility = kind == ScadaCommandKind.CloseQuickWindow ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateWriteModePanels()
    {
        var mode = (ScadaWriteMode?)WriteModeComboBox.SelectedItem;
        MomentaryValuesPanel.Visibility = mode == ScadaWriteMode.Momentary ? Visibility.Visible : Visibility.Collapsed;
        FixedValueTextBox.Visibility = mode == ScadaWriteMode.SetFixed ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text) || TriggerComboBox.SelectedItem is null || KindComboBox.SelectedItem is null)
        {
            return;
        }

        var kind = (ScadaCommandKind)KindComboBox.SelectedItem;
        if (_usedKinds.Contains(kind))
        {
            ValidationText.Text = $"Une commande '{kind}' existe deja pour cet Element+.";
            return;
        }

        if (kind == ScadaCommandKind.OpenQuickWindow && QuickWindowDefinitionComboBox.SelectedItem is not QuickWindowDefinition)
        {
            ValidationText.Text = "Selectionnez la fenetre rapide cible.";
            return;
        }

        SelectedQuickWindowDefinitionKey = kind == ScadaCommandKind.OpenQuickWindow
            ? (QuickWindowDefinitionComboBox.SelectedItem as QuickWindowDefinition)?.DefinitionKey
            : null;
        var existingInvocationKey = _existingCommand?.Kind == ScadaCommandKind.OpenQuickWindow
            ? _existingCommand.QuickWindowInvocationKey
            : null;

        Result = new ScadaCommandBinding(
            _commandId,
            NameTextBox.Text.Trim(),
            Enabled: true,
            Trigger: (ScadaCommandTrigger)TriggerComboBox.SelectedItem,
            Kind: kind,
            Confirmation: ConfirmationCheckBox.IsChecked == true
                ? new ScadaConfirmation(ConfirmationMessageTextBox.Text.Trim())
                : null,
            WriteTagId: (WriteTagComboBox.SelectedItem as TagItem)?.Id,
            ReadTagId: (ReadTagComboBox.SelectedItem as TagItem)?.Id,
            WriteMode: (ScadaWriteMode?)WriteModeComboBox.SelectedItem,
            OnValue: string.IsNullOrWhiteSpace(OnValueTextBox.Text) ? null : OnValueTextBox.Text,
            OffValue: string.IsNullOrWhiteSpace(OffValueTextBox.Text) ? null : OffValueTextBox.Text,
            FixedValue: string.IsNullOrWhiteSpace(FixedValueTextBox.Text) ? null : FixedValueTextBox.Text,
            TargetPageId: kind == ScadaCommandKind.Navigate ? (TargetPageComboBox.SelectedItem as ScadaSceneReference)?.Id : null,
            Url: string.IsNullOrWhiteSpace(UrlTextBox.Text) ? null : UrlTextBox.Text,
            NewTab: NewTabCheckBox.IsChecked == true,
            QuickWindowInvocationKey: kind == ScadaCommandKind.OpenQuickWindow ? existingInvocationKey : null);

        DialogResult = true;
    }
}
