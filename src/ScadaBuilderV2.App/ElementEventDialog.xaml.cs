using System.Windows;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App;

/// <summary>
/// Modal authoring surface for model-backed Element+ runtime events.
/// </summary>
/// <remarks>
/// Decisions: DEC-0011.
/// Contracts: docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md.
/// </remarks>
public partial class ElementEventDialog : Window
{
    private IReadOnlyList<ScadaActionDefinition> actions = Array.Empty<ScadaActionDefinition>();

    /// <summary>
    /// Initializes the event authoring dialog for one Element+ object.
    /// </summary>
    public ElementEventDialog(
        ScadaElement element,
        IReadOnlyList<ScadaActionDefinition> sceneActions,
        IReadOnlyList<ScadaSceneReference> pageReferences,
        ScadaTagCatalog? tagCatalog)
    {
        ArgumentNullException.ThrowIfNull(element);
        InitializeComponent();

        ElementTitleText.Text = $"{element.UserLabel} ({element.Kind})";
        actions = sceneActions;
        RefreshExistingEvents(element, sceneActions);

        TriggerComboBox.ItemsSource = ScadaEventRegistry.Triggers;
        TriggerComboBox.SelectedItem = ScadaEventRegistry.FindTrigger(ScadaEventRegistry.ClickKey);

        ActionComboBox.ItemsSource = ScadaEventRegistry.Actions.Where(action => action.Implemented).ToArray();
        ActionComboBox.SelectedItem = ScadaEventRegistry.FindAction(ScadaEventRegistry.ChangePageFunction);

        TargetPageComboBox.ItemsSource = pageReferences
            .Where(page => page.IncludeInBuild && page.Type == ScadaPageType.Default)
            .OrderBy(page => page.Id, StringComparer.Ordinal)
            .Select(page => new TargetPageItem(page.Id, $"{page.Id} - {page.Title}"))
            .ToArray();
        TargetPageComboBox.SelectedIndex = TargetPageComboBox.Items.Count > 0 ? 0 : -1;

        TargetTagComboBox.ItemsSource = (tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .Where(tag => tag.Enabled && tag.Writeable)
            .OrderBy(tag => tag.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(tag => new TargetTagItem(tag.Id, $"{tag.DisplayName} [{tag.Id}]"))
            .ToArray();
        TargetTagComboBox.SelectedIndex = TargetTagComboBox.Items.Count > 0 ? 0 : -1;
        UpdateActionArgumentVisibility();
    }

    /// <summary>
    /// Applies an event request while the dialog remains open.
    /// </summary>
    public Func<ElementEventDialogResult, string>? AddEvent { get; set; }

    /// <summary>
    /// Removes an event request while the dialog remains open.
    /// </summary>
    public Func<int, string>? DeleteEvent { get; set; }

    /// <summary>
    /// Refreshes the event list after a model-backed event is added.
    /// </summary>
    public void RefreshExistingEvents(ScadaElement element, IReadOnlyList<ScadaActionDefinition> sceneActions)
    {
        actions = sceneActions;
        ExistingEventsListBox.ItemsSource = element.EventBindings.Count == 0
            ? new[] { new EventListItem(-1, "Aucun evenement configure") }
            : element.EventBindings
                .Select((binding, index) => new EventListItem(index, FormatExistingEvent(binding, sceneActions)))
                .ToArray();
        ExistingEventsSummaryText.Text = $"{element.EventBindings.Count} evenement(s) configure(s)";
        ExistingEventsListBox.SelectedIndex = element.EventBindings.Count == 0 ? -1 : 0;
        UpdateDeleteButtonState();
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = "";

        if (TriggerComboBox.SelectedItem is not ScadaEventTriggerContract trigger)
        {
            ValidationText.Text = "Selectionnez un declencheur.";
            return;
        }

        if (ActionComboBox.SelectedItem is not ScadaActionFunctionContract action)
        {
            ValidationText.Text = "Selectionnez une fonction.";
            return;
        }

        if (string.Equals(action.FunctionName, ScadaEventRegistry.ChangePageFunction, StringComparison.Ordinal))
        {
            if (TargetPageComboBox.SelectedItem is not TargetPageItem targetPage)
            {
                ValidationText.Text = "Selectionnez une page cible compilee de type Defaut.";
                return;
            }

            var pageResult = new ElementEventDialogResult(trigger.RuntimeTrigger, action.FunctionName, TargetPageId: targetPage.PageId);
            ValidationText.Text = AddEvent?.Invoke(pageResult) ?? "Evenement ajoute.";
            return;
        }

        if (!string.Equals(action.FunctionName, ScadaEventRegistry.WriteTagFunction, StringComparison.Ordinal))
        {
            ValidationText.Text = "Fonction d'evenement non implementee.";
            return;
        }

        if (TargetTagComboBox.SelectedItem is not TargetTagItem targetTag)
        {
            ValidationText.Text = "Importez un catalogue contenant au moins un tag ecrivable.";
            return;
        }

        var value = TagValueTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(value))
        {
            ValidationText.Text = "Entrez la valeur a ecrire.";
            return;
        }

        var tagResult = new ElementEventDialogResult(trigger.RuntimeTrigger, action.FunctionName, TagId: targetTag.TagId, Value: value);
        ValidationText.Text = AddEvent?.Invoke(tagResult) ?? "Evenement ajoute.";
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = "";
        if (ExistingEventsListBox.SelectedItem is not EventListItem { Index: >= 0 } selected)
        {
            ValidationText.Text = "Selectionnez un evenement a supprimer.";
            return;
        }

        ValidationText.Text = DeleteEvent?.Invoke(selected.Index) ?? "Evenement supprime.";
    }

    private void OnExistingEventSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateDeleteButtonState();
    }

    private void OnActionSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateActionArgumentVisibility();
    }

    private void UpdateActionArgumentVisibility()
    {
        var isChangePage = ActionComboBox.SelectedItem is ScadaActionFunctionContract action &&
            string.Equals(action.FunctionName, ScadaEventRegistry.ChangePageFunction, StringComparison.Ordinal);
        var isWriteTag = ActionComboBox.SelectedItem is ScadaActionFunctionContract tagAction &&
            string.Equals(tagAction.FunctionName, ScadaEventRegistry.WriteTagFunction, StringComparison.Ordinal);

        TargetPageLabelText.Visibility = isChangePage ? Visibility.Visible : Visibility.Collapsed;
        TargetPageComboBox.Visibility = isChangePage ? Visibility.Visible : Visibility.Collapsed;
        TargetTagLabelText.Visibility = isWriteTag ? Visibility.Visible : Visibility.Collapsed;
        TargetTagComboBox.Visibility = isWriteTag ? Visibility.Visible : Visibility.Collapsed;
        TagValueLabelText.Visibility = isWriteTag ? Visibility.Visible : Visibility.Collapsed;
        TagValueTextBox.Visibility = isWriteTag ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateDeleteButtonState()
    {
        DeleteEventButton.IsEnabled = ExistingEventsListBox.SelectedItem is EventListItem { Index: >= 0 };
    }

    private string FormatExistingEvent(ScadaObjectEventBinding binding)
    {
        return FormatExistingEvent(binding, actions);
    }

    private static string FormatExistingEvent(
        ScadaObjectEventBinding binding,
        IReadOnlyList<ScadaActionDefinition> sceneActions)
    {
        var trigger = ScadaEventRegistry.FindTrigger(binding.Trigger);
        var action = sceneActions.FirstOrDefault(action => string.Equals(action.Id, binding.ActionId, StringComparison.Ordinal));
        var actionContract = ScadaEventRegistry.FindAction(action?.Kind.ToString());
        var target = action?.Kind switch
        {
            ScadaActionKind.Navigate when !string.IsNullOrWhiteSpace(action.TargetPageId) => $" -> {action.TargetPageId}",
            ScadaActionKind.WriteTag when !string.IsNullOrWhiteSpace(action.TagId) => $" -> {action.TagId} = {action.Value}",
            _ => ""
        };
        return $"{trigger?.FrenchLabel ?? binding.Trigger} | {actionContract?.FrenchLabel ?? action?.Kind.ToString() ?? binding.ActionId}{target}";
    }

    private sealed record TargetPageItem(string PageId, string DisplayName);

    private sealed record TargetTagItem(string TagId, string DisplayName);

    private sealed record EventListItem(int Index, string DisplayName);
}

/// <summary>
/// Result returned by the Element+ event authoring modal.
/// </summary>
/// <param name="RuntimeTrigger">Browser trigger to bind on the Element+ object.</param>
/// <param name="FunctionName">Registered action function name.</param>
/// <param name="TargetPageId">Target page argument for the selected action.</param>
public sealed record ElementEventDialogResult(
    string RuntimeTrigger,
    string FunctionName,
    string? TargetPageId = null,
    string? TagId = null,
    string? Value = null);
