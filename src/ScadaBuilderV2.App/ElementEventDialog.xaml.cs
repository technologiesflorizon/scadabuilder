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
    private IReadOnlyDictionary<string, ScadaTagDefinition> tagsById = new Dictionary<string, ScadaTagDefinition>(StringComparer.Ordinal);

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
        tagsById = (tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .ToDictionary(tag => tag.Id, StringComparer.Ordinal);
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
            .Where(tag => tag.Enabled)
            .OrderBy(tag => tag.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(tag => new TargetTagItem(tag.Id, tag.AuthoringLabel))
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
    public Func<ElementEventDialogDeleteRequest, string>? DeleteEvent { get; set; }

    /// <summary>
    /// Opens the project-level tag creation workflow.
    /// </summary>
    public Func<string>? CreateTag { get; set; }

    /// <summary>
    /// Refreshes the event list after a model-backed event is added.
    /// </summary>
    public void RefreshExistingEvents(ScadaElement element, IReadOnlyList<ScadaActionDefinition> sceneActions)
    {
        actions = sceneActions;
        var items = new List<EventListItem>();
        if (!string.IsNullOrWhiteSpace(element.Data?.ReadTagId))
        {
            items.Add(new EventListItem("read", -1, $"Lire valeur -> {FormatTag(element.Data.ReadTagId)}"));
        }

        if (!string.IsNullOrWhiteSpace(element.Data?.WriteTagId))
        {
            items.Add(new EventListItem("write", -1, $"Ecrire valeur -> {FormatTag(element.Data.WriteTagId)}"));
        }

        items.AddRange(element.EventBindings
            .Select((binding, index) => new EventListItem("event", index, FormatExistingEvent(binding, sceneActions))));

        ExistingEventsListBox.ItemsSource = items.Count == 0
            ? new[] { new EventListItem("none", -1, "Aucun evenement configure") }
            : items.ToArray();
        ExistingEventsSummaryText.Text = $"{items.Count} binding(s)/evenement(s) configure(s)";
        ExistingEventsListBox.SelectedIndex = items.Count == 0 ? -1 : 0;
        UpdateDeleteButtonState();
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = "";

        if (ActionComboBox.SelectedItem is not ScadaActionFunctionContract action)
        {
            ValidationText.Text = "Selectionnez une fonction.";
            return;
        }

        var isValueBinding =
            string.Equals(action.FunctionName, ScadaEventRegistry.ReadValueFunction, StringComparison.Ordinal) ||
            string.Equals(action.FunctionName, ScadaEventRegistry.WriteValueFunction, StringComparison.Ordinal);
        if (isValueBinding)
        {
            if (TargetTagComboBox.SelectedItem is not TargetTagItem targetTag)
            {
                ValidationText.Text = "Importez un catalogue contenant au moins un tag.";
                return;
            }

            var tagResult = new ElementEventDialogResult(action.FunctionName, TagId: targetTag.TagId);
            ValidationText.Text = AddEvent?.Invoke(tagResult) ?? "Binding ajoute.";
            return;
        }

        if (TriggerComboBox.SelectedItem is not ScadaEventTriggerContract trigger)
        {
            ValidationText.Text = "Selectionnez un declencheur.";
            return;
        }

        if (string.Equals(action.FunctionName, ScadaEventRegistry.ChangePageFunction, StringComparison.Ordinal))
        {
            if (TargetPageComboBox.SelectedItem is not TargetPageItem targetPage)
            {
                ValidationText.Text = "Selectionnez une page cible compilee de type Defaut.";
                return;
            }

            var pageResult = new ElementEventDialogResult(action.FunctionName, RuntimeTrigger: trigger.RuntimeTrigger, TargetPageId: targetPage.PageId);
            ValidationText.Text = AddEvent?.Invoke(pageResult) ?? "Evenement ajoute.";
            return;
        }

        ValidationText.Text = "Fonction d'evenement non implementee.";
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = "";
        if (ExistingEventsListBox.SelectedItem is not EventListItem { Index: >= 0 } selected)
        {
            ValidationText.Text = "Selectionnez un evenement a supprimer.";
            return;
        }

        ValidationText.Text = DeleteEvent?.Invoke(new ElementEventDialogDeleteRequest(selected.Kind, selected.Index)) ?? "Binding supprime.";
    }

    private void OnExistingEventSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateDeleteButtonState();
    }

    private void OnActionSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateActionArgumentVisibility();
    }

    private void OnCreateTagClick(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = CreateTag?.Invoke() ??
            "Creation de tags disponible dans une prochaine revision apres import des protocoles projet.";
    }

    private void UpdateActionArgumentVisibility()
    {
        var isChangePage = ActionComboBox.SelectedItem is ScadaActionFunctionContract action &&
            string.Equals(action.FunctionName, ScadaEventRegistry.ChangePageFunction, StringComparison.Ordinal);
        var isValueBinding = ActionComboBox.SelectedItem is ScadaActionFunctionContract tagAction &&
            (string.Equals(tagAction.FunctionName, ScadaEventRegistry.ReadValueFunction, StringComparison.Ordinal) ||
             string.Equals(tagAction.FunctionName, ScadaEventRegistry.WriteValueFunction, StringComparison.Ordinal));

        TriggerLabelText.IsEnabled = !isValueBinding;
        TriggerComboBox.IsEnabled = !isValueBinding;
        TargetPageLabelText.Visibility = isChangePage ? Visibility.Visible : Visibility.Collapsed;
        TargetPageComboBox.Visibility = isChangePage ? Visibility.Visible : Visibility.Collapsed;
        TargetTagLabelText.Visibility = isValueBinding ? Visibility.Visible : Visibility.Collapsed;
        TargetTagPanel.Visibility = isValueBinding ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateDeleteButtonState()
    {
        DeleteEventButton.IsEnabled = ExistingEventsListBox.SelectedItem is EventListItem { Kind: "read" or "write" } or
            EventListItem { Kind: "event", Index: >= 0 };
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

    private string FormatTag(string tagId)
    {
        return tagsById.TryGetValue(tagId, out var tag) ? tag.AuthoringLabel : tagId;
    }

    private sealed record TargetPageItem(string PageId, string DisplayName);

    private sealed record TargetTagItem(string TagId, string DisplayName);

    private sealed record EventListItem(string Kind, int Index, string DisplayName);
}

/// <summary>
/// Result returned by the Element+ event authoring modal.
/// </summary>
/// <param name="RuntimeTrigger">Browser trigger to bind on the Element+ object.</param>
/// <param name="FunctionName">Registered action function name.</param>
/// <param name="TargetPageId">Target page argument for the selected action.</param>
public sealed record ElementEventDialogResult(
    string FunctionName,
    string? RuntimeTrigger = null,
    string? TargetPageId = null,
    string? TagId = null);

/// <summary>
/// Delete request returned by the Element+ event authoring modal.
/// </summary>
public sealed record ElementEventDialogDeleteRequest(string Kind, int EventIndex);
