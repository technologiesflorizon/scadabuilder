using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.QuickWindows;
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
/// since `DEC-0050` removed the popup command kinds. Choosing (or already editing) the `OpenQuickWindow` type
/// opens the `QuickWindowConfigurationDialog` sub-dialog - a parameter sheet and a page of type Fenêtre
/// rapide - rather than the former single "fenêtre rapide cible" combo box; the pairing is stored on the
/// command as `ParameterSheetKey`/`QuickWindowPageKey` and compiled at build time by `ParameterSheetLowering`.
/// A pre-existing DEC-0050 command that only ever carried a `QuickWindowInvocationKey` (authored before this
/// pairing existed) keeps loading and saving unchanged as long as its sheet/page selection is left untouched
/// in this editing session - see `QuickWindowConfigurationRules.IsUnmodifiedLegacyInvocation`.
///
/// Decisions: DEC-0036, DEC-0050, DEC-0052, FR-011, FR-013.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBindingAuthoringTests.cs,
/// tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowConfigurationRulesTests.cs.
/// </remarks>
public partial class ElementCommandDialog : Window
{
    private sealed record TagItem(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly IReadOnlyList<ScadaSceneReference> _pageReferences;
    private readonly IReadOnlyList<ScadaSceneReference> _quickWindowPages;
    private readonly IReadOnlyList<ParameterSheet> _parameterSheets;
    private readonly string _commandId;
    private readonly ScadaCommandBinding? _existingCommand;

    private readonly QuickWindowCommandAuthoringContext _quickWindowContext;

    private Guid? _parameterSheetKey;
    private Guid? _quickWindowPageKey;

    public ElementCommandDialog(
        ScadaCommandBinding? existingCommand,
        IReadOnlyList<ScadaSceneReference> pageReferences,
        ScadaTagCatalog? tagCatalog,
        IReadOnlyCollection<ScadaCommandKind> usedKinds,
        IReadOnlyList<ParameterSheet> parameterSheets,
        QuickWindowCommandAuthoringContext quickWindowContext)
    {
        InitializeComponent();
        _pageReferences = pageReferences;
        _quickWindowPages = pageReferences.Where(page => page.Type == ScadaPageType.QuickWindow).ToArray();
        _parameterSheets = parameterSheets;
        _commandId = existingCommand?.Id ?? Guid.NewGuid().ToString("n");
        _existingCommand = existingCommand;
        _quickWindowContext = quickWindowContext;

        if (existingCommand?.Kind == ScadaCommandKind.OpenQuickWindow)
        {
            _parameterSheetKey = existingCommand.ParameterSheetKey;
            _quickWindowPageKey = existingCommand.QuickWindowPageKey;
        }

        TriggerComboBox.ItemsSource = Enum.GetValues<ScadaCommandTrigger>();
        var kindOptions = ElementCommandKindCatalog.CreateOptions(
            usedKinds,
            existingCommand?.Kind,
            _quickWindowPages.Count > 0,
            _parameterSheets.Count > 0,
            _quickWindowContext.IsQuickWindowContent);
        KindComboBox.ItemsSource = kindOptions;
        WriteModeComboBox.ItemsSource = Enum.GetValues<ScadaWriteMode>();
        var navigateTargets = QuickWindowConfigurationRules.NavigateTargetPages(pageReferences);
        TargetPageComboBox.ItemsSource = navigateTargets;

        var enabledTags = (tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .Where(tag => tag.Enabled)
            .ToArray();
        // Shared guard (fix round 1, DEC-0052): a QuickWindow page's `#N` catalog is already numerically
        // ordered and must not be re-sorted by DisplayName, which would scramble it back to the lexical
        // #1, #10, #100... order this fixes.
        var orderedTags = QuickWindowPagePlaceholderCatalog.OrderForSelector(enabledTags);
        var tagItems = orderedTags
            .Select(tag => new TagItem(tag.Id, tag.AuthoringLabel))
            .ToArray();
        WriteTagComboBox.ItemsSource = tagItems;
        ReadTagComboBox.ItemsSource = tagItems;
        InterlockTagComboBox.ItemsSource = tagItems;

        if (existingCommand is not null)
        {
            NameTextBox.Text = existingCommand.Name;
            TriggerComboBox.SelectedItem = existingCommand.Trigger;
            KindComboBox.SelectedItem = kindOptions.First(option => option.Kind == existingCommand.Kind);
            WriteTagComboBox.SelectedItem = tagItems.FirstOrDefault(t => t.Id == existingCommand.WriteTagId);
            ReadTagComboBox.SelectedItem = tagItems.FirstOrDefault(t => t.Id == existingCommand.ReadTagId);
            InterlockTagComboBox.SelectedItem = tagItems.FirstOrDefault(t => t.Id == existingCommand.InterlockTagId);
            WriteModeComboBox.SelectedItem = existingCommand.WriteMode;
            OnValueTextBox.Text = existingCommand.OnValue ?? string.Empty;
            OffValueTextBox.Text = existingCommand.OffValue ?? string.Empty;
            FixedValueTextBox.Text = existingCommand.FixedValue ?? string.Empty;
            TargetPageComboBox.SelectedItem = navigateTargets.FirstOrDefault(p => p.Id == existingCommand.TargetPageId);
            UrlTextBox.Text = existingCommand.Url ?? string.Empty;
            NewTabCheckBox.IsChecked = existingCommand.NewTab;
            ConfirmationCheckBox.IsChecked = existingCommand.Confirmation is not null;
            ConfirmationMessageTextBox.Text = existingCommand.Confirmation?.Message ?? string.Empty;
        }
        else
        {
            TriggerComboBox.SelectedIndex = 0;
            var firstEnabledIndex = kindOptions.ToList().FindIndex(option => option.IsEnabled);
            KindComboBox.SelectedIndex = firstEnabledIndex >= 0 ? firstEnabledIndex : -1;
        }

        UpdateKindPanels();
        UpdateWriteModePanels();
        UpdateQuickWindowSummary();
    }

    public ScadaCommandBinding? Result { get; private set; }

    private void OnDialogLoaded(object sender, RoutedEventArgs e)
    {
        // Only a new command opens the sub-dialog by itself; editing an existing one goes through "Configurer…".
        if (QuickWindowConfigurationRules.ShouldOpenConfigurationOnLoad(
                _existingCommand is null,
                (KindComboBox.SelectedItem as ElementCommandKindOption)?.Kind))
        {
            OpenQuickWindowConfiguration();
        }
    }

    private void OnKindChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateKindPanels();
        if (IsLoaded && (KindComboBox.SelectedItem as ElementCommandKindOption)?.Kind == ScadaCommandKind.OpenQuickWindow)
        {
            OpenQuickWindowConfiguration();
        }
    }

    private void OnWriteModeChanged(object sender, SelectionChangedEventArgs e) => UpdateWriteModePanels();

    private void OnConfigureQuickWindowClick(object sender, RoutedEventArgs e) => OpenQuickWindowConfiguration();

    /// <summary>
    /// Opens the "Configuration de la fenêtre rapide" sub-dialog preselected on the dialog's current
    /// choice, and applies its result - or keeps the previous choice unchanged on "Annuler".
    /// </summary>
    private void OpenQuickWindowConfiguration()
    {
        var dialog = new QuickWindowConfigurationDialog(_parameterSheets, _quickWindowPages, _parameterSheetKey, _quickWindowPageKey)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true)
        {
            _parameterSheetKey = dialog.SelectedParameterSheetKey;
            _quickWindowPageKey = dialog.SelectedQuickWindowPageKey;
        }

        UpdateQuickWindowSummary();
    }

    private void UpdateQuickWindowSummary()
    {
        var sheetName = _parameterSheets.FirstOrDefault(sheet => sheet.SheetKey == _parameterSheetKey)?.DisplayName;
        var pageName = _quickWindowPages.FirstOrDefault(page => page.PageKey == _quickWindowPageKey)?.Title;
        QuickWindowSummaryText.Text = sheetName is not null && pageName is not null
            ? $"Feuille : {sheetName} — Page : {pageName}"
            : "Non configurée.";
    }

    private void UpdateKindPanels()
    {
        var kind = (KindComboBox.SelectedItem as ElementCommandKindOption)?.Kind ?? ScadaCommandKind.WriteTag;
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
        InterlockTagComboBox.IsEnabled = mode == ScadaWriteMode.Toggle;
        if (mode != ScadaWriteMode.Toggle)
        {
            InterlockTagComboBox.SelectedItem = null;
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text) || TriggerComboBox.SelectedItem is null || KindComboBox.SelectedItem is not ElementCommandKindOption selectedKindOption)
        {
            return;
        }

        if (!selectedKindOption.IsEnabled)
        {
            // Defensive: WPF already refuses to select a disabled ComboBoxItem, but this must not rely on it.
            ValidationText.Text = selectedKindOption.DisabledReason ?? "Ce type n'est pas disponible.";
            return;
        }

        var kind = selectedKindOption.Kind;

        var isUnmodifiedLegacyInvocation = QuickWindowConfigurationRules.IsUnmodifiedLegacyInvocation(
            _existingCommand?.Kind == ScadaCommandKind.OpenQuickWindow,
            _existingCommand?.QuickWindowInvocationKey,
            _existingCommand?.ParameterSheetKey,
            _existingCommand?.QuickWindowPageKey,
            _parameterSheetKey,
            _quickWindowPageKey);

        if (kind == ScadaCommandKind.OpenQuickWindow &&
            !QuickWindowConfigurationRules.CanSaveOpenQuickWindowCommand(_parameterSheetKey, _quickWindowPageKey, isUnmodifiedLegacyInvocation))
        {
            ValidationText.Text = "Sélectionnez la feuille de paramètres et la page Fenêtre rapide cible.";
            return;
        }

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
            QuickWindowInvocationKey: kind == ScadaCommandKind.OpenQuickWindow && isUnmodifiedLegacyInvocation
                ? _existingCommand?.QuickWindowInvocationKey
                : null,
            ParameterSheetKey: kind == ScadaCommandKind.OpenQuickWindow ? _parameterSheetKey : null,
            QuickWindowPageKey: kind == ScadaCommandKind.OpenQuickWindow ? _quickWindowPageKey : null,
            InterlockTagId: kind == ScadaCommandKind.WriteTag && (ScadaWriteMode?)WriteModeComboBox.SelectedItem == ScadaWriteMode.Toggle ? (InterlockTagComboBox.SelectedItem as TagItem)?.Id : null);

        DialogResult = true;
    }
}
