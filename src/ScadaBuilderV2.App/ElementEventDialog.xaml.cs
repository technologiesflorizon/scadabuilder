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
    private readonly IReadOnlyList<ScadaSceneReference> pageReferences;
    private IReadOnlyDictionary<string, ScadaTagDefinition> tagsById = new Dictionary<string, ScadaTagDefinition>(StringComparer.Ordinal);

    /// <summary>
    /// Initializes the event authoring dialog for one Element+ object.
    /// </summary>
    public ElementEventDialog(
        ScadaElement element,
        IReadOnlyList<ScadaActionDefinition> sceneActions,
        IReadOnlyList<ScadaElement> sceneElements,
        IReadOnlyList<ScadaSceneReference> pageReferences,
        ScadaTagCatalog? tagCatalog)
    {
        ArgumentNullException.ThrowIfNull(element);
        InitializeComponent();

        this.pageReferences = pageReferences;
        ElementTitleText.Text = $"{element.UserLabel} ({element.Kind})";
        actions = sceneActions;
        tagsById = (tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .ToDictionary(tag => tag.Id, StringComparer.Ordinal);
        RefreshExistingEvents(element, sceneActions);

        TriggerComboBox.ItemsSource = ScadaEventRegistry.Triggers;
        TriggerComboBox.SelectedItem = ScadaEventRegistry.FindTrigger(ScadaEventRegistry.ClickKey);

        ActionComboBox.ItemsSource = ScadaEventRegistry.Actions.Where(action => action.Implemented).ToArray();
        ActionComboBox.SelectedItem = ScadaEventRegistry.FindAction(ScadaEventRegistry.ChangePageFunction);

        TargetElementComboBox.ItemsSource = FlattenElements(sceneElements)
            .Where(target => !target.IsLegacyStatic)
            .OrderBy(target => target.UserLabel, StringComparer.CurrentCultureIgnoreCase)
            .Select(target => new TargetElementItem(target.Id, $"{target.UserLabel} | {target.Kind}"))
            .ToArray();
        TargetElementComboBox.SelectedIndex = TargetElementComboBox.Items.Count > 0 ? 0 : -1;

        var tagItems = (tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
            .Where(tag => tag.Enabled)
            .OrderBy(tag => tag.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(tag => new TargetTagItem(tag.Id, tag.AuthoringLabel))
            .ToArray();
        TargetTagComboBox.ItemsSource = tagItems;
        TargetTagComboBox.SelectedIndex = TargetTagComboBox.Items.Count > 0 ? 0 : -1;
        ConditionTagComboBox.ItemsSource = tagItems;
        ConditionTagComboBox.SelectedIndex = ConditionTagComboBox.Items.Count > 0 ? 0 : -1;
        ConditionOperatorComboBox.ItemsSource = new[]
        {
            new ConditionOperatorItem(ScadaConditionOperator.True, "Vrai"),
            new ConditionOperatorItem(ScadaConditionOperator.False, "Faux"),
            new ConditionOperatorItem(ScadaConditionOperator.Equals, "="),
            new ConditionOperatorItem(ScadaConditionOperator.NotEquals, "<>"),
            new ConditionOperatorItem(ScadaConditionOperator.GreaterThan, ">"),
            new ConditionOperatorItem(ScadaConditionOperator.GreaterThanOrEqual, ">="),
            new ConditionOperatorItem(ScadaConditionOperator.LessThan, "<"),
            new ConditionOperatorItem(ScadaConditionOperator.LessThanOrEqual, "<=")
        };
        ConditionOperatorComboBox.SelectedIndex = 0;
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

        var isPageTargetAction =
            string.Equals(action.FunctionName, ScadaEventRegistry.ChangePageFunction, StringComparison.Ordinal) ||
            IsPopupFunction(action.FunctionName);
        if (isPageTargetAction)
        {
            if (TargetPageComboBox.SelectedItem is not TargetPageItem targetPage)
            {
                ValidationText.Text = IsPopupFunction(action.FunctionName)
                    ? "Selectionnez un fragment compile pour la popup."
                    : "Selectionnez une page cible compilee de type Defaut.";
                return;
            }

            var pageResult = new ElementEventDialogResult(action.FunctionName, RuntimeTrigger: trigger.RuntimeTrigger, TargetPageId: targetPage.PageId);
            ValidationText.Text = AddEvent?.Invoke(pageResult) ?? "Evenement ajoute.";
            return;
        }

        var isObjectTargetAction = IsObjectVisibilityFunction(action.FunctionName) || IsObjectBorderFunction(action.FunctionName);
        if (isObjectTargetAction)
        {
            if (TargetElementComboBox.SelectedItem is not TargetElementItem targetElement)
            {
                ValidationText.Text = "Selectionnez un objet cible.";
                return;
            }

            var condition = IsObjectVisibilityFunction(action.FunctionName) ? BuildConditionFromUi() : null;
            if (IsObjectVisibilityFunction(action.FunctionName) && ConditionCheckBox.IsChecked == true && condition is null)
            {
                return;
            }

            var objectResult = new ElementEventDialogResult(
                action.FunctionName,
                RuntimeTrigger: trigger.RuntimeTrigger,
                TargetElementId: targetElement.ElementId,
                Condition: condition);
            ValidationText.Text = AddEvent?.Invoke(objectResult) ?? "Evenement ajoute.";
            return;
        }

        ValidationText.Text = "Fonction d'evenement non implementee.";
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = "";
        if (ExistingEventsListBox.SelectedItem is not EventListItem selected ||
            selected is not ({ Kind: "read" or "write" } or { Kind: "event", Index: >= 0 }))
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

    private void OnConditionChanged(object sender, RoutedEventArgs e)
    {
        UpdateActionArgumentVisibility();
    }

    private void OnConditionOperatorChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateConditionValueVisibility();
    }

    private void UpdateActionArgumentVisibility()
    {
        var isChangePage = ActionComboBox.SelectedItem is ScadaActionFunctionContract action &&
            string.Equals(action.FunctionName, ScadaEventRegistry.ChangePageFunction, StringComparison.Ordinal);
        var isOpenPopup = ActionComboBox.SelectedItem is ScadaActionFunctionContract popupAction &&
            IsPopupFunction(popupAction.FunctionName);
        var isObjectVisibilityAction = ActionComboBox.SelectedItem is ScadaActionFunctionContract objectVisibilityAction &&
            IsObjectVisibilityFunction(objectVisibilityAction.FunctionName);
        var isObjectTargetAction = ActionComboBox.SelectedItem is ScadaActionFunctionContract objectAction &&
            (IsObjectVisibilityFunction(objectAction.FunctionName) || IsObjectBorderFunction(objectAction.FunctionName));
        var isValueBinding = ActionComboBox.SelectedItem is ScadaActionFunctionContract tagAction &&
            (string.Equals(tagAction.FunctionName, ScadaEventRegistry.ReadValueFunction, StringComparison.Ordinal) ||
             string.Equals(tagAction.FunctionName, ScadaEventRegistry.WriteValueFunction, StringComparison.Ordinal));

        TriggerLabelText.IsEnabled = !isValueBinding;
        TriggerComboBox.IsEnabled = !isValueBinding;
        TargetPageLabelText.Text = isOpenPopup ? "Fragment popup" : "Page cible";
        TargetPageLabelText.Visibility = isChangePage || isOpenPopup ? Visibility.Visible : Visibility.Collapsed;
        TargetPageComboBox.Visibility = isChangePage || isOpenPopup ? Visibility.Visible : Visibility.Collapsed;
        if (isChangePage || isOpenPopup)
        {
            RefreshTargetPageOptions(isOpenPopup ? ScadaPageType.Fragment : ScadaPageType.Default);
        }

        TargetElementLabelText.Visibility = isObjectTargetAction ? Visibility.Visible : Visibility.Collapsed;
        TargetElementComboBox.Visibility = isObjectTargetAction ? Visibility.Visible : Visibility.Collapsed;
        TargetTagLabelText.Visibility = isValueBinding ? Visibility.Visible : Visibility.Collapsed;
        TargetTagPanel.Visibility = isValueBinding ? Visibility.Visible : Visibility.Collapsed;
        ConditionCheckBox.Visibility = isObjectVisibilityAction ? Visibility.Visible : Visibility.Collapsed;
        ConditionPanel.Visibility = isObjectVisibilityAction && ConditionCheckBox.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateConditionValueVisibility();
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
            ScadaActionKind.MountFragment or ScadaActionKind.ClosePopup or ScadaActionKind.TogglePopup when !string.IsNullOrWhiteSpace(action.TargetPageId) =>
                $" -> {action.TargetPageId}",
            ScadaActionKind.Show or ScadaActionKind.Hide or ScadaActionKind.ToggleVisibility when !string.IsNullOrWhiteSpace(action.TargetElementId) =>
                $" -> {action.TargetElementId}{FormatCondition(action.Condition)}",
            ScadaActionKind.SetClass or ScadaActionKind.RemoveClass or ScadaActionKind.ToggleClass when !string.IsNullOrWhiteSpace(action.TargetElementId) =>
                $" -> {action.TargetElementId}",
            ScadaActionKind.WriteTag when !string.IsNullOrWhiteSpace(action.TagId) => $" -> {action.TagId} = {action.Value}",
            _ => ""
        };
        return $"{trigger?.FrenchLabel ?? binding.Trigger} | {actionContract?.FrenchLabel ?? action?.Kind.ToString() ?? binding.ActionId}{target}";
    }

    private ScadaActionCondition? BuildConditionFromUi()
    {
        if (ConditionCheckBox.IsChecked != true)
        {
            return null;
        }

        if (ConditionTagComboBox.SelectedItem is not TargetTagItem tag)
        {
            ValidationText.Text = "Selectionnez un tag de condition.";
            return null;
        }

        if (ConditionOperatorComboBox.SelectedItem is not ConditionOperatorItem selectedOperator)
        {
            ValidationText.Text = "Selectionnez un operateur de condition.";
            return null;
        }

        var compareValue = selectedOperator.Operator is ScadaConditionOperator.True or ScadaConditionOperator.False
            ? null
            : ConditionValueTextBox.Text?.Trim();
        if (selectedOperator.Operator is not (ScadaConditionOperator.True or ScadaConditionOperator.False) &&
            string.IsNullOrWhiteSpace(compareValue))
        {
            ValidationText.Text = "Entrez la valeur de comparaison.";
            return null;
        }

        return new ScadaActionCondition(tag.TagId, selectedOperator.Operator, compareValue);
    }

    private void UpdateConditionValueVisibility()
    {
        var usesValue = ConditionOperatorComboBox.SelectedItem is not ConditionOperatorItem selectedOperator ||
            selectedOperator.Operator is not (ScadaConditionOperator.True or ScadaConditionOperator.False);
        ConditionValueLabelText.Visibility = usesValue ? Visibility.Visible : Visibility.Collapsed;
        ConditionValueTextBox.Visibility = usesValue ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string FormatCondition(ScadaActionCondition? condition)
    {
        return condition is null
            ? ""
            : $" si {condition.TagId} {condition.Operator} {condition.CompareValue}".TrimEnd();
    }

    private string FormatTag(string tagId)
    {
        return tagsById.TryGetValue(tagId, out var tag) ? tag.AuthoringLabel : tagId;
    }

    // Groups popup functions so authoring keeps the same fragment target selector for each popup action.
    private static bool IsPopupFunction(string? functionName)
    {
        return string.Equals(functionName, ScadaEventRegistry.OpenPopupFunction, StringComparison.Ordinal) ||
            string.Equals(functionName, ScadaEventRegistry.ClosePopupFunction, StringComparison.Ordinal) ||
            string.Equals(functionName, ScadaEventRegistry.TogglePopupFunction, StringComparison.Ordinal);
    }

    // Groups visibility functions because only these object-target actions support tag conditions.
    private static bool IsObjectVisibilityFunction(string? functionName)
    {
        return string.Equals(functionName, ScadaEventRegistry.ShowFunction, StringComparison.Ordinal) ||
            string.Equals(functionName, ScadaEventRegistry.HideFunction, StringComparison.Ordinal) ||
            string.Equals(functionName, ScadaEventRegistry.ToggleVisibilityFunction, StringComparison.Ordinal);
    }

    // Groups runtime border functions so they reuse the Element+ target selector without condition UI.
    private static bool IsObjectBorderFunction(string? functionName)
    {
        return string.Equals(functionName, ScadaEventRegistry.ShowBorderFunction, StringComparison.Ordinal) ||
            string.Equals(functionName, ScadaEventRegistry.HideBorderFunction, StringComparison.Ordinal) ||
            string.Equals(functionName, ScadaEventRegistry.ToggleBorderFunction, StringComparison.Ordinal);
    }

    // Keeps page target choices aligned with the selected runtime function contract.
    private void RefreshTargetPageOptions(ScadaPageType pageType)
    {
        var previous = (TargetPageComboBox.SelectedItem as TargetPageItem)?.PageId;
        var items = pageReferences
            .Where(page => page.IncludeInBuild && page.Type == pageType)
            .OrderBy(page => page.Id, StringComparer.Ordinal)
            .Select(page => new TargetPageItem(page.Id, $"{page.Id} - {page.Title}"))
            .ToArray();
        TargetPageComboBox.ItemsSource = items;
        TargetPageComboBox.SelectedItem = !string.IsNullOrWhiteSpace(previous)
            ? items.FirstOrDefault(item => string.Equals(item.PageId, previous, StringComparison.Ordinal))
            : null;
        if (TargetPageComboBox.SelectedItem is null)
        {
            TargetPageComboBox.SelectedIndex = TargetPageComboBox.Items.Count > 0 ? 0 : -1;
        }
    }

    private static IEnumerable<ScadaElement> FlattenElements(IEnumerable<ScadaElement> elements)
    {
        foreach (var element in elements)
        {
            yield return element;
            foreach (var child in FlattenElements(element.ChildElements))
            {
                yield return child;
            }
        }
    }

    private sealed record TargetPageItem(string PageId, string DisplayName);

    private sealed record TargetElementItem(string ElementId, string DisplayName);

    private sealed record TargetTagItem(string TagId, string DisplayName);

    private sealed record ConditionOperatorItem(ScadaConditionOperator Operator, string DisplayName);

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
    string? TargetElementId = null,
    string? TagId = null,
    ScadaActionCondition? Condition = null);

/// <summary>
/// Delete request returned by the Element+ event authoring modal.
/// </summary>
public sealed record ElementEventDialogDeleteRequest(string Kind, int EventIndex);
