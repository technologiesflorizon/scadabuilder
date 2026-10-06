using System.Windows;
using ScadaBuilderV2.Application.Conversion;
using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App;

/// <summary>
/// Legacy <c>Data.TagBinding</c> surface of the shell: the read-only field of the « Données » tab, the
/// « Convertir en Lecture de variable » and « Effacer le TagBinding résiduel » actions for one element, the
/// page-wide conversion, and the removal of a read binding shown by the State tab.
/// </summary>
/// <remarks>
/// Every decision lives in <see cref="LegacyTagBindingConverter"/>; this file owns only the WPF wiring and the
/// history push. The conversion resolves against the project tag catalog (never the <c>#N</c> selector projection)
/// and is one <c>SceneSnapshotChangedAction</c>, so one undo restores the page exactly.
/// Decisions: DEC-0053.
/// Contracts: docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md §2 règle 5.
/// Tests: tests/ScadaBuilderV2.Tests/LegacyTagBindingConverterTests.cs,
/// tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs.
/// </remarks>
public partial class MainWindow
{
    /// <summary>Projects the legacy binding of <paramref name="element"/> on the read-only « Données » panel.</summary>
    private void RefreshLegacyTagBindingPanel(ScadaElement? element)
    {
        var state = element is null ? LegacyTagBindingState.None : LegacyTagBindingConverter.Inspect(element);
        ElementLegacyTagBindingPanel.Visibility = state == LegacyTagBindingState.None ? Visibility.Collapsed : Visibility.Visible;
        ElementTagBindingTextBox.Text = element?.Data?.TagBinding ?? "";
        ElementLegacyTagBindingStatusText.Text = LegacyTagBindingConverter.Describe(state, IsActivePageOfQuickWindowType);
        ConvertLegacyTagBindingButton.Visibility = state == LegacyTagBindingState.Convertible ? Visibility.Visible : Visibility.Collapsed;
        ClearResidualTagBindingButton.Visibility = state == LegacyTagBindingState.Residual ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Wires the modal properties dialog to the same legacy conversion, residual clearing and read-binding removal
    /// as the docked panel. Each runs immediately as its own undoable transition, so it persists even if the dialog
    /// is then cancelled (as <c>SaveStateConfig</c> does).
    /// </summary>
    private void ConfigureReadBindingSurfaces(ElementPropertiesDialog dialog, string elementId)
    {
        dialog.IsQuickWindowPage = IsActivePageOfQuickWindowType;
        dialog.ConvertLegacyTagBinding = () => ConvertLegacyTagBinding(elementId);
        dialog.ClearResidualTagBinding = () => ClearResidualTagBinding(elementId);
        dialog.RemoveReadBinding = () => RemoveReadBinding(elementId);
    }

    private void OnClearResidualTagBindingClick(object sender, RoutedEventArgs e)
    {
        if (_selectedSceneObject is null)
        {
            SetStatus("Aucun Element+ selectionne pour l'effacement du TagBinding.");
            return;
        }

        var (_, message) = ClearResidualTagBinding(_selectedSceneObject.Id);
        SetStatus(message);
    }

    /// <summary>Clears the residual legacy binding of <paramref name="elementId"/> and returns the latest element with the operator message.</summary>
    private (ScadaElement Element, string Message) ClearResidualTagBinding(string elementId)
    {
        if (_activeScene is null)
        {
            return (ScadaElement.CreateText(elementId, elementId, 0, 0), "Effacement impossible : aucune page active.");
        }

        var result = LegacyTagBindingConverter.ClearResidual(_activeScene, elementId);
        if (result.HistoryAction is { } action)
        {
            ApplyLegacyTagBindingConversion(result.Scene, action);
        }

        var latest = _activeScene.FindElementRecursive(elementId) ?? ScadaElement.CreateText(elementId, elementId, 0, 0);
        return (latest, result.Message);
    }

    /// <summary>
    /// Removes every read binding of <paramref name="elementId"/> (<c>StateConfig.ReadVariable</c> and
    /// <c>Data.ReadTagId</c>) as one undoable transition and returns the latest element.
    /// </summary>
    private ScadaElement RemoveReadBinding(string elementId)
    {
        if (_activeScene is null)
        {
            return ScadaElement.CreateText(elementId, elementId, 0, 0);
        }

        var before = _activeScene;
        var after = ReadBindingPresentation.WithoutReadBinding(before, elementId);
        if (!ReferenceEquals(before, after))
        {
            ApplyLegacyTagBindingConversion(after, new SceneSnapshotChangedAction(before.Id, before, after, "retrait lecture de variable"));
            SetStatus("Lecture de variable retiree (ReadVariable et liaison ReadTagId).");
        }

        RefreshStateAndCommandTabs();
        return _activeScene.FindElementRecursive(elementId) ?? ScadaElement.CreateText(elementId, elementId, 0, 0);
    }

    private void OnConvertLegacyTagBindingClick(object sender, RoutedEventArgs e)
    {
        if (_selectedSceneObject is null)
        {
            SetStatus("Aucun Element+ selectionne pour la conversion du TagBinding.");
            return;
        }

        var (_, message) = ConvertLegacyTagBinding(_selectedSceneObject.Id);
        SetStatus(message);
    }

    private void OnConvertAllLegacyTagBindingsClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null)
        {
            SetStatus("Aucune page active pour la conversion des TagBinding.");
            return;
        }

        var result = LegacyTagBindingConverter.ConvertScene(_activeScene, _modernProject?.TagCatalog, IsActivePageOfQuickWindowType);
        if (result.HistoryAction is { } action)
        {
            ApplyLegacyTagBindingConversion(result.Scene, action);
        }

        SetStatus(result.Summary.Split(Environment.NewLine)[0]);
        MessageBox.Show(this, result.Summary, "Conversion des TagBinding", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// Converts the legacy binding of <paramref name="elementId"/> on the active page and returns the latest element
    /// with the operator message.
    /// </summary>
    private (ScadaElement Element, string Message) ConvertLegacyTagBinding(string elementId)
    {
        if (_activeScene is null)
        {
            return (ScadaElement.CreateText(elementId, elementId, 0, 0), "Conversion impossible : aucune page active.");
        }

        var result = LegacyTagBindingConverter.ConvertElement(_activeScene, elementId, _modernProject?.TagCatalog, IsActivePageOfQuickWindowType);
        if (result.HistoryAction is { } action)
        {
            ApplyLegacyTagBindingConversion(result.Scene, action);
        }

        var latest = _activeScene.FindElementRecursive(elementId) ?? ScadaElement.CreateText(elementId, elementId, 0, 0);
        return (latest, result.Message);
    }

    private void ApplyLegacyTagBindingConversion(ScadaScene after, IEditorHistoryAction action)
    {
        _activeScene = after;
        if (_selectedSceneObject is not null)
        {
            _selectedSceneObject = _activeScene.FindElementRecursive(_selectedSceneObject.Id) ?? _selectedSceneObject;
        }

        _activeSceneTab?.History.Push(action);
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        _ = RenderModernSceneAsync();
    }
}
