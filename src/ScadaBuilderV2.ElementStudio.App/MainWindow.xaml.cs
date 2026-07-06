using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using ScadaBuilderV2.Application.ElementStudio;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ElementStudio;
using ScadaBuilderV2.Application.Libraries;
using ScadaBuilderV2.Infrastructure.Libraries;

namespace ScadaBuilderV2.ElementStudio.App;

public partial class MainWindow : Window
{
    private readonly IReadOnlyDictionary<string, FrameworkElement> ribbons;
    private readonly ElementStudioComponentPackageStore componentPackageStore = new();
    private readonly LibraryRegistryStore libraryRegistryStore = new();
    private readonly ElementPlusLibraryReader elementPlusLibraryReader = new();
    private readonly ObservableCollection<ElementPlusLibraryItem> libraryItems = [];
    private readonly ElementStudioWorkspaceViewModel workspace;
    private readonly string? sourcePackagePath;
    private bool isSynchronizingElementSelection;
    private string? currentSepPath;

    public MainWindow(string? packagePath)
    {
        InitializeComponent();
        sourcePackagePath = string.IsNullOrWhiteSpace(packagePath) ? null : Path.GetFullPath(packagePath);

        var loadResult = ElementStudioPackageLoader.Load(packagePath);
        workspace = new ElementStudioWorkspaceViewModel(loadResult.Package, loadResult.Diagnostics);
        DataContext = workspace;

        ribbons = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal)
        {
            ["File"] = FileRibbon,
            ["Edit"] = EditRibbon,
            ["View"] = ViewRibbon,
            ["ElementPlus"] = ElementPlusRibbon,
            ["Export"] = ExportRibbon
        };

        Loaded += OnLoaded;
        StudioLibraryListBox.ItemsSource = libraryItems;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LegacySourceWebView.EnsureCoreWebView2Async();
        if (LegacySourceWebView.CoreWebView2 is not null)
        {
            LegacySourceWebView.CoreWebView2.WebMessageReceived += OnLegacySourceWebMessageReceived;
            var packageDirectory = string.IsNullOrWhiteSpace(sourcePackagePath)
                ? null
                : Path.GetDirectoryName(sourcePackagePath);
            if (!string.IsNullOrWhiteSpace(packageDirectory) && Directory.Exists(packageDirectory))
            {
                LegacySourceWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "studio-import.local",
                    packageDirectory,
                    CoreWebView2HostResourceAccessKind.Allow);
            }
        }

        LegacySourceWebView.NavigationCompleted += OnLegacySourceNavigationCompleted;
        LegacySourceWebView.NavigateToString(BuildLegacySourceDocument(workspace));
        SynchronizeElementListSelection(workspace.SelectedItems);
        UpdateSelectionGeometryFields();
        ApplyWorkzoneZoom();
        UpdateToolButtonStates();
        try
        {
            await RefreshLibrarySelectorAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Librairie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnCreateSvgComponentClick(object sender, RoutedEventArgs e)
    {
        workspace.ComponentVisualKind = "Svg";
        workspace.Diagnostics.Add($"Composant SVG Element+ cree en memoire: {workspace.ComponentName} ({workspace.ImportedItems.Count} source(s)).");
        workspace.Diagnostics.Add("Prochaine etape: sauvegarde .sep de composant reutilisable dans la librairie.");

        await HighlightSelectedSourcesInWebViewAsync(workspace.SelectedItems.Select(item => item.SourceElementId));
    }

    private async void OnSaveComponentClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(currentSepPath))
        {
            await SaveComponentAsAsync();
            return;
        }

        await SaveComponentAsync(currentSepPath);
    }

    private async void OnSaveComponentAsClick(object sender, RoutedEventArgs e)
    {
        await SaveComponentAsAsync();
    }

    private async void OnAddToLibraryArrowClick(object sender, RoutedEventArgs e)
    {
        var entries = await BuildLibraryEntriesAsync();

        var menu = new ContextMenu();
        foreach (var entry in entries)
        {
            var menuItem = new MenuItem { Header = entry.Name, Tag = entry };
            menuItem.Click += OnLibraryMenuItemClick;
            menu.Items.Add(menuItem);
        }

        menu.PlacementTarget = AddToLibraryArrowButton;
        menu.IsOpen = true;
    }

    private async Task<IReadOnlyList<LibraryEntry>> BuildLibraryEntriesAsync()
    {
        var externalEntries = await libraryRegistryStore.ReadExternalEntriesAsync();
        var defaultName = await libraryRegistryStore.ReadDefaultNameAsync() ?? "Defaut";
        var defaultEntry = new LibraryEntry(defaultName, ResolveDefaultSepDirectory(), IsDefault: true);
        return new[] { defaultEntry }.Concat(externalEntries).ToArray();
    }

    private async Task RefreshLibrarySelectorAsync()
    {
        var entries = await BuildLibraryEntriesAsync();
        var previousName = (StudioLibrarySelectorComboBox.SelectedItem as LibraryEntry)?.Name;
        StudioLibrarySelectorComboBox.ItemsSource = entries;
        var toSelect = entries.FirstOrDefault(entry => entry.Name == previousName) ?? entries[0];
        StudioLibrarySelectorComboBox.SelectedItem = toSelect;
    }

    private async void OnStudioLibrarySelectorSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await RefreshLibraryItemsAsync();
    }

    private async Task RefreshLibraryItemsAsync()
    {
        libraryItems.Clear();
        if (StudioLibrarySelectorComboBox.SelectedItem is not LibraryEntry selected)
        {
            return;
        }

        try
        {
            var snapshot = await elementPlusLibraryReader.ReadAsync(selected.Path);
            foreach (var item in snapshot.Items)
            {
                libraryItems.Add(item);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Librairie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnRenameLibraryComponentClick(object sender, RoutedEventArgs e)
    {
        if (StudioLibraryListBox.SelectedItem is not ElementPlusLibraryItem selected)
        {
            return;
        }

        var nameDialog = new ComponentNameDialog("Nouveau nom du composant", selected.Name) { Owner = this };
        if (nameDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var package = await componentPackageStore.ReadFromPathAsync(selected.FilePath);
            var renamedComponent = package.Component with { Name = nameDialog.EnteredName };
            var renamedPackage = package with { Component = renamedComponent };
            var libraryRoot = Path.GetDirectoryName(selected.FilePath) ?? "";
            var newPath = ElementStudioComponentPackageStore.GetDefaultComponentPath(libraryRoot, renamedPackage);

            await componentPackageStore.WriteToPathAsync(renamedPackage, newPath);
            if (!string.Equals(Path.GetFullPath(newPath), Path.GetFullPath(selected.FilePath), StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(selected.FilePath);
            }

            await RefreshLibraryItemsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Renommer le composant", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnCopyLibraryComponentClick(object sender, RoutedEventArgs e)
    {
        if (StudioLibraryListBox.SelectedItem is not ElementPlusLibraryItem selected)
        {
            return;
        }

        try
        {
            var package = await componentPackageStore.ReadFromPathAsync(selected.FilePath);
            var existingNames = libraryItems.Select(item => item.Name);
            var copyName = ElementStudioComponentCopyNaming.GenerateCopyName(selected.Name, existingNames);
            var copiedComponent = package.Component with
            {
                ComponentId = ToComponentId(copyName),
                Name = copyName
            };
            var copiedPackage = package with { Component = copiedComponent };
            var libraryRoot = Path.GetDirectoryName(selected.FilePath) ?? "";

            await componentPackageStore.WriteToLibraryAsync(copiedPackage, libraryRoot);
            await RefreshLibraryItemsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Copier le composant", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnDeleteLibraryComponentClick(object sender, RoutedEventArgs e)
    {
        if (StudioLibraryListBox.SelectedItem is not ElementPlusLibraryItem selected)
        {
            return;
        }

        var confirmed = MessageBox.Show(
            this,
            $"Supprimer definitivement le composant '{selected.Name}' ?",
            "Supprimer le composant",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmed != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            File.Delete(selected.FilePath);
            await RefreshLibraryItemsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Supprimer le composant", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnLibraryMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: LibraryEntry entry })
        {
            return;
        }

        try
        {
            var package = CreateCurrentComponentPackage();
            var savedPath = await componentPackageStore.WriteToLibraryAsync(package, entry.Path);
            currentSepPath = savedPath;
            workspace.SavedComponentPath = savedPath;
            workspace.ComponentVisualKind = package.Component.Visual.Kind.ToString();
            workspace.Diagnostics.Add($"Composant ajoute a la librairie '{entry.Name}': {savedPath}");
        }
        catch (Exception ex)
        {
            workspace.Diagnostics.Add($"Ajout a la librairie impossible: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Ajouter a la librairie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SaveComponentAsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Enregistrer composant Element+",
            AddExtension = true,
            DefaultExt = ".sep",
            Filter = "Studio Element+ (*.sep)|*.sep",
            FileName = $"{ToSafeFileName(workspace.ComponentName)}.sep",
            InitialDirectory = ResolveDefaultSepDirectory()
        };

        if (dialog.ShowDialog(this) != true)
        {
            workspace.Diagnostics.Add("Sauvegarde .sep annulee.");
            return;
        }

        await SaveComponentAsync(dialog.FileName);
    }

    private async Task SaveComponentAsync(string path)
    {
        try
        {
            var package = CreateCurrentComponentPackage();
            var savedPath = await componentPackageStore.WriteToPathAsync(package, path);
            currentSepPath = savedPath;
            workspace.SavedComponentPath = savedPath;
            workspace.ComponentVisualKind = package.Component.Visual.Kind.ToString();
            workspace.Diagnostics.Add($"Composant .sep sauvegarde: {savedPath}");
        }
        catch (Exception ex)
        {
            workspace.Diagnostics.Add($"Sauvegarde .sep impossible: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Sauvegarde .sep", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnZoomOutClick(object sender, RoutedEventArgs e)
    {
        workspace.ZoomOut();
        ApplyWorkzoneZoom();
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e)
    {
        workspace.ZoomIn();
        ApplyWorkzoneZoom();
    }

    private void OnResetZoomClick(object sender, RoutedEventArgs e)
    {
        workspace.ResetZoom();
        ApplyWorkzoneZoom();
    }

    private void OnShrinkWorkzoneClick(object sender, RoutedEventArgs e)
    {
        workspace.ResizeWorkzone(-100, -100);
        ApplyWorkzoneZoom();
    }

    private void OnGrowWorkzoneClick(object sender, RoutedEventArgs e)
    {
        workspace.ResizeWorkzone(100, 100);
        ApplyWorkzoneZoom();
    }

    private void OnFitWorkzoneClick(object sender, RoutedEventArgs e)
    {
        workspace.FitWorkzoneToImportedBounds();
        ApplyWorkzoneZoom();
    }

    private async void OnElementSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isSynchronizingElementSelection)
        {
            return;
        }

        if (sender is not ListBox listBox)
        {
            return;
        }

        var selectedItems = listBox.SelectedItems
            .OfType<ElementStudioItemViewModel>()
            .ToArray();
        workspace.SetSelectedItems(selectedItems);
        UpdateSelectionGeometryFields();
        await HighlightSelectedSourcesInWebViewAsync(selectedItems.Select(item => item.SourceElementId));
    }

    private void OnToolClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string toolName })
        {
            return;
        }

        workspace.ActiveTool = toolName;
        workspace.Diagnostics.Add($"Outil actif: {workspace.ActiveTool}");
        UpdateToolButtonStates();
    }

    private void OnUnavailableCommandClick(object sender, RoutedEventArgs e)
    {
        var commandName = sender is FrameworkElement { Tag: string tag }
            ? tag
            : "Commande";
        workspace.Diagnostics.Add($"{commandName}: handler UI connecte, implementation metier a venir.");
    }

    private async void OnDeleteSelectionClick(object sender, RoutedEventArgs e)
    {
        workspace.DeleteSelectedItems();
        workspace.Diagnostics.Add("Selection supprimee.");
        await RefreshWorkzoneAsync();
    }

    private async void OnUndoClick(object sender, RoutedEventArgs e)
    {
        workspace.Undo();
        workspace.Diagnostics.Add("Undo Studio Element+.");
        await RefreshWorkzoneAsync();
    }

    private async void OnRedoClick(object sender, RoutedEventArgs e)
    {
        workspace.Redo();
        workspace.Diagnostics.Add("Redo Studio Element+.");
        await RefreshWorkzoneAsync();
    }

    private void OnCopySelectionClick(object sender, RoutedEventArgs e)
    {
        workspace.CopySelectedItems();
        workspace.Diagnostics.Add("Selection copiee.");
    }

    private async void OnPasteSelectionClick(object sender, RoutedEventArgs e)
    {
        workspace.PasteCopiedItems();
        workspace.Diagnostics.Add("Selection collee.");
        await RefreshWorkzoneAsync();
    }

    private async void OnDuplicateSelectionClick(object sender, RoutedEventArgs e)
    {
        workspace.DuplicateSelectedItems();
        workspace.Diagnostics.Add("Selection dupliquee.");
        await RefreshWorkzoneAsync();
    }

    private async void OnHideSelectionClick(object sender, RoutedEventArgs e)
    {
        workspace.HideSelectedItems();
        workspace.Diagnostics.Add("Selection cachee.");
        await RefreshWorkzoneAsync();
    }

    private async void OnShowAllClick(object sender, RoutedEventArgs e)
    {
        workspace.ShowAllItems();
        workspace.Diagnostics.Add("Elements caches affiches.");
        await RefreshWorkzoneAsync();
    }

    private async void OnGroupSelectionClick(object sender, RoutedEventArgs e)
    {
        workspace.GroupSelectedItems();
        workspace.Diagnostics.Add("Selection groupee.");
        await RefreshWorkzoneAsync();
    }

    private async void OnUngroupSelectionClick(object sender, RoutedEventArgs e)
    {
        workspace.UngroupSelectedItems();
        workspace.Diagnostics.Add("Selection degroupee.");
        await RefreshWorkzoneAsync();
    }

    private async void OnLockSelectionClick(object sender, RoutedEventArgs e)
    {
        workspace.LockSelectedItems();
        workspace.Diagnostics.Add("Selection verrouillee.");
        await RefreshWorkzoneAsync();
    }

    private async void OnUnlockAllClick(object sender, RoutedEventArgs e)
    {
        workspace.UnlockAllItems();
        workspace.Diagnostics.Add("Elements deverrouilles.");
        await RefreshWorkzoneAsync();
    }

    private async void OnAlignLeftClick(object sender, RoutedEventArgs e)
    {
        workspace.AlignLeft();
        workspace.Diagnostics.Add("Selection alignee a gauche.");
        await RefreshWorkzoneAsync();
    }

    private async void OnAlignCenterClick(object sender, RoutedEventArgs e)
    {
        workspace.AlignCenter();
        workspace.Diagnostics.Add("Selection alignee au centre.");
        await RefreshWorkzoneAsync();
    }

    private async void OnAlignRightClick(object sender, RoutedEventArgs e)
    {
        workspace.AlignRight();
        workspace.Diagnostics.Add("Selection alignee a droite.");
        await RefreshWorkzoneAsync();
    }

    private async void OnAlignTopClick(object sender, RoutedEventArgs e)
    {
        workspace.AlignTop();
        workspace.Diagnostics.Add("Selection alignee en haut.");
        await RefreshWorkzoneAsync();
    }

    private async void OnAlignMiddleClick(object sender, RoutedEventArgs e)
    {
        workspace.AlignMiddle();
        workspace.Diagnostics.Add("Selection alignee au milieu.");
        await RefreshWorkzoneAsync();
    }

    private async void OnAlignBottomClick(object sender, RoutedEventArgs e)
    {
        workspace.AlignBottom();
        workspace.Diagnostics.Add("Selection alignee en bas.");
        await RefreshWorkzoneAsync();
    }

    private async void OnDistributeHorizontalClick(object sender, RoutedEventArgs e)
    {
        workspace.DistributeHorizontally();
        workspace.Diagnostics.Add("Selection distribuee horizontalement.");
        await RefreshWorkzoneAsync();
    }

    private async void OnDistributeVerticalClick(object sender, RoutedEventArgs e)
    {
        workspace.DistributeVertically();
        workspace.Diagnostics.Add("Selection distribuee verticalement.");
        await RefreshWorkzoneAsync();
    }

    private async void OnEqualizeWidthClick(object sender, RoutedEventArgs e)
    {
        workspace.EqualizeSelectedWidth();
        workspace.Diagnostics.Add("Largeurs egalisees.");
        await RefreshWorkzoneAsync();
    }

    private async void OnEqualizeHeightClick(object sender, RoutedEventArgs e)
    {
        workspace.EqualizeSelectedHeight();
        workspace.Diagnostics.Add("Hauteurs egalisees.");
        await RefreshWorkzoneAsync();
    }

    private async void OnApplySelectionGeometryClick(object sender, RoutedEventArgs e)
    {
        workspace.SetSelectedBounds(
            TryParseNullableDouble(SelectionXTextBox.Text),
            TryParseNullableDouble(SelectionYTextBox.Text),
            TryParseNullableDouble(SelectionWidthTextBox.Text),
            TryParseNullableDouble(SelectionHeightTextBox.Text));
        workspace.Diagnostics.Add("Geometrie commune appliquee.");
        await RefreshWorkzoneAsync();
    }

    private void OnRibbonTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string selectedRibbon })
        {
            return;
        }

        foreach (var ribbon in ribbons)
        {
            ribbon.Value.Visibility = ribbon.Key == selectedRibbon
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void OnLegacySourceWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var message = System.Text.Json.JsonSerializer.Deserialize<StudioWebMessage>(e.WebMessageAsJson);
            if (message is null)
            {
                return;
            }

            switch (message.Type)
            {
                case "selectSources":
                    var sourceElementIds = message.SourceElementIds.Length > 0
                        ? message.SourceElementIds
                        : string.IsNullOrWhiteSpace(message.SourceElementId)
                            ? []
                            : [message.SourceElementId];
                    workspace.SetSelectedItemIds(sourceElementIds);
                    SynchronizeElementListSelection(workspace.SelectedItems);
                    UpdateSelectionGeometryFields();
                    workspace.Diagnostics.Add($"Selection workzone: {workspace.SelectionSummary}");
                    break;
                case "clearSelection":
                    workspace.ClearSelection();
                    SynchronizeElementListSelection(workspace.SelectedItems);
                    UpdateSelectionGeometryFields();
                    _ = HighlightSelectedSourcesInWebViewAsync(workspace.SelectedItems.Select(item => item.SourceElementId));
                    workspace.Diagnostics.Add("Selection videe.");
                    break;
                case "moveSources":
                    workspace.MoveSelectedItemsBy(message.DeltaX, message.DeltaY);
                    workspace.Diagnostics.Add($"Selection deplacee: {message.DeltaX:0.##}, {message.DeltaY:0.##}");
                    _ = RefreshWorkzoneAsync();
                    break;
                case "resizeSources":
                    workspace.ResizeSelectedItemsBy(message.DeltaWidth, message.DeltaHeight);
                    workspace.Diagnostics.Add($"Selection redimensionnee: {message.DeltaWidth:0.##}, {message.DeltaHeight:0.##}");
                    _ = RefreshWorkzoneAsync();
                    break;
                case "deleteSelection":
                    workspace.DeleteSelectedItems();
                    workspace.Diagnostics.Add("Selection supprimee.");
                    _ = RefreshWorkzoneAsync();
                    break;
                case "copySelection":
                    workspace.CopySelectedItems();
                    workspace.Diagnostics.Add("Selection copiee.");
                    break;
                case "pasteSelection":
                    workspace.PasteCopiedItems();
                    workspace.Diagnostics.Add("Selection collee.");
                    _ = RefreshWorkzoneAsync();
                    break;
                case "duplicateSelection":
                    workspace.DuplicateSelectedItems();
                    workspace.Diagnostics.Add("Selection dupliquee.");
                    _ = RefreshWorkzoneAsync();
                    break;
                case "undo":
                    workspace.Undo();
                    workspace.Diagnostics.Add("Undo Studio Element+.");
                    _ = RefreshWorkzoneAsync();
                    break;
                case "redo":
                    workspace.Redo();
                    workspace.Diagnostics.Add("Redo Studio Element+.");
                    _ = RefreshWorkzoneAsync();
                    break;
                case "hideSelection":
                    workspace.HideSelectedItems();
                    workspace.Diagnostics.Add("Selection cachee.");
                    _ = RefreshWorkzoneAsync();
                    break;
                case "selectAll":
                    workspace.SelectAll();
                    SynchronizeElementListSelection(workspace.SelectedItems);
                    UpdateSelectionGeometryFields();
                    _ = HighlightSelectedSourcesInWebViewAsync(workspace.SelectedItems.Select(item => item.SourceElementId));
                    break;
            }
        }
        catch (Exception ex)
        {
            workspace.Diagnostics.Add($"Message Studio invalide: {ex.Message}");
        }
    }

    private async Task HighlightSelectedSourcesInWebViewAsync(IEnumerable<string> sourceElementIds)
    {
        if (LegacySourceWebView.CoreWebView2 is null)
        {
            return;
        }

        var idsJson = System.Text.Json.JsonSerializer.Serialize(sourceElementIds.Where(id => !string.IsNullOrWhiteSpace(id)));
        await LegacySourceWebView.ExecuteScriptAsync($"window.elementStudio && window.elementStudio.selectSources({idsJson});");
    }

    private async void OnLegacySourceNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        ApplyWorkzoneZoom();
        UpdateSelectionGeometryFields();
        await HighlightSelectedSourcesInWebViewAsync(workspace.SelectedItems.Select(item => item.SourceElementId));
    }

    private async Task RefreshWorkzoneAsync()
    {
        SynchronizeElementListSelection(workspace.SelectedItems);
        UpdateSelectionGeometryFields();
        if (LegacySourceWebView.CoreWebView2 is null)
        {
            return;
        }

        LegacySourceWebView.NavigateToString(BuildLegacySourceDocument(workspace));
        await Task.CompletedTask;
    }

    private void ApplyWorkzoneZoom()
    {
        LegacySourceWebView.ZoomFactor = workspace.WorkzoneZoom;
    }

    private void UpdateSelectionGeometryFields()
    {
        var selected = workspace.SelectedItems.ToArray();
        SelectionXTextBox.Text = FormatCommonSelectionValue(selected, item => item.Bounds.X);
        SelectionYTextBox.Text = FormatCommonSelectionValue(selected, item => item.Bounds.Y);
        SelectionWidthTextBox.Text = FormatCommonSelectionValue(selected, item => item.Bounds.Width);
        SelectionHeightTextBox.Text = FormatCommonSelectionValue(selected, item => item.Bounds.Height);
    }

    private static string FormatCommonSelectionValue(
        IReadOnlyList<ElementStudioItemViewModel> selected,
        Func<ElementStudioItemViewModel, double> valueSelector)
    {
        if (selected.Count == 0)
        {
            return "";
        }

        var first = valueSelector(selected[0]);
        return selected.All(item => Math.Abs(valueSelector(item) - first) < 0.001)
            ? first.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture)
            : "";
    }

    private static double? TryParseNullableDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.CurrentCulture,
            out var parsed)
            ? parsed
            : double.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out parsed)
                ? parsed
                : null;
    }

    private ElementStudioComponentPackage CreateCurrentComponentPackage()
    {
        var items = workspace.ImportedItems.ToArray();
        var bounds = CalculateComponentBounds(items);
        var assetMap = CreateEmbeddedAssetMap(items);
        var parts = items.Select(item => CreateComponentPart(item, assetMap)).ToArray();
        var svgMarkup = BuildComponentSvgMarkup(items, bounds, assetMap);
        var sourceTrace = new ElementStudioComponentSourceTrace(
            workspace.Package.SourceProjectId,
            workspace.Package.SourceSceneId,
            workspace.Package.SourcePagePath,
            items.Select(item => item.SourceElementId).ToArray(),
            "Created from Studio Element+ import package.");

        return ElementStudioComponentPackageFactory.Create(
            ToComponentId(workspace.ComponentName),
            workspace.ComponentName,
            bounds,
            new ElementStudioComponentVisual(ElementStudioComponentVisualKind.Svg, SvgMarkup: svgMarkup),
            parts,
            assetMap.Values,
            ElementStudioComponentMetadata.Current(ReadCurrentVersion()),
            sourceTrace,
            workspace.ComponentCategory,
            null,
            workspace.ComponentTagList);
    }

    private ElementStudioComponentPart CreateComponentPart(
        ElementStudioItemViewModel item,
        IReadOnlyDictionary<string, ElementStudioEmbeddedAsset> assetMap)
    {
        assetMap.TryGetValue(item.SourceElementId, out var imageAsset);
        return new ElementStudioComponentPart(
            item.ElementName,
            item.ElementName,
            ToComponentPartKind(item.LegacyType, item.Item.LegacyMarkup, imageAsset is not null),
            item.Bounds,
            item.Item.Style,
            item.Item.Geometry,
            item.Item.Text,
            ImageAssetId: imageAsset?.AssetId,
            HtmlMarkup: imageAsset is null ? item.Item.LegacyMarkup : null,
            SourceTrace: new ElementStudioComponentSourceTrace(
                workspace.Package.SourceProjectId,
                workspace.Package.SourceSceneId,
                workspace.Package.SourcePagePath,
                [item.SourceElementId],
                item.SourceName));
    }

    private static ElementStudioComponentPartKind ToComponentPartKind(string legacyType, string? markup, bool hasImageAsset = false)
    {
        if (!string.IsNullOrWhiteSpace(markup))
        {
            var trimmed = markup.TrimStart();
            if (trimmed.StartsWith("<img", StringComparison.OrdinalIgnoreCase))
            {
                return hasImageAsset
                    ? ElementStudioComponentPartKind.Image
                    : ElementStudioComponentPartKind.Html;
            }

            if (trimmed.StartsWith("<line", StringComparison.OrdinalIgnoreCase))
            {
                return ElementStudioComponentPartKind.Line;
            }

            if (trimmed.StartsWith("<polyline", StringComparison.OrdinalIgnoreCase))
            {
                return ElementStudioComponentPartKind.Polyline;
            }

            if (trimmed.StartsWith("<polygon", StringComparison.OrdinalIgnoreCase))
            {
                return ElementStudioComponentPartKind.Polygon;
            }

            if (trimmed.StartsWith("<rect", StringComparison.OrdinalIgnoreCase))
            {
                return ElementStudioComponentPartKind.Rectangle;
            }

            if (trimmed.StartsWith("<path", StringComparison.OrdinalIgnoreCase))
            {
                return ElementStudioComponentPartKind.Path;
            }

            if (trimmed.StartsWith("<text", StringComparison.OrdinalIgnoreCase))
            {
                return ElementStudioComponentPartKind.Text;
            }
        }

        return legacyType.Contains("text", StringComparison.OrdinalIgnoreCase)
            ? ElementStudioComponentPartKind.Text
            : ElementStudioComponentPartKind.Custom;
    }

    private static SceneBounds CalculateComponentBounds(IReadOnlyList<ElementStudioItemViewModel> items)
    {
        if (items.Count == 0)
        {
            return new SceneBounds(0, 0, 160, 120);
        }

        var left = items.Min(item => item.Bounds.X);
        var top = items.Min(item => item.Bounds.Y);
        var right = items.Max(item => item.Bounds.X + item.Bounds.Width);
        var bottom = items.Max(item => item.Bounds.Y + item.Bounds.Height);

        return new SceneBounds(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    private static string BuildComponentSvgMarkup(
        IReadOnlyList<ElementStudioItemViewModel> items,
        SceneBounds bounds,
        IReadOnlyDictionary<string, ElementStudioEmbeddedAsset> assetMap)
    {
        var viewBoxX = bounds.X.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var viewBoxY = bounds.Y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var width = bounds.Width.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var height = bounds.Height.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var content = string.Concat(items.Select(item =>
        {
            var name = HtmlEncoder.Default.Encode(item.ElementName);
            var legacyId = HtmlEncoder.Default.Encode(item.SourceElementId);
            var legacyName = HtmlEncoder.Default.Encode(item.SourceName);
            var markup = BuildComponentItemSvgMarkup(item, assetMap);

            if (string.IsNullOrWhiteSpace(markup))
            {
                var x = item.Bounds.X.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var y = item.Bounds.Y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var text = HtmlEncoder.Default.Encode(item.Text);
                markup = $"""<text x="{x}" y="{y}">{text}</text>""";
            }

            return $"""
              <g id="{name}" data-source-id="{legacyId}" data-source-name="{legacyName}">
                {markup}
              </g>
            """;
        }));

        return ElementStudioSvgMarkupNormalizer.NormalizeSvgMarkup($"""
        <svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="{viewBoxX} {viewBoxY} {width} {height}">
        {content}
        </svg>
        """);
    }

    private static string BuildComponentItemSvgMarkup(
        ElementStudioItemViewModel item,
        IReadOnlyDictionary<string, ElementStudioEmbeddedAsset> assetMap)
    {
        if (assetMap.TryGetValue(item.SourceElementId, out var asset))
        {
            var x = item.Bounds.X.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var y = item.Bounds.Y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var width = Math.Max(1, item.Bounds.Width).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var height = Math.Max(1, item.Bounds.Height).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var href = $"data:{asset.MediaType};base64,{asset.Base64Data}";
            return $"""<image x="{x}" y="{y}" width="{width}" height="{height}" href="{href}" preserveAspectRatio="none" />""";
        }

        return SanitizeLegacyMarkup(item.Item.LegacyMarkup);
    }

    private IReadOnlyDictionary<string, ElementStudioEmbeddedAsset> CreateEmbeddedAssetMap(
        IReadOnlyList<ElementStudioItemViewModel> items)
    {
        var assets = new Dictionary<string, ElementStudioEmbeddedAsset>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var src = ExtractImageSource(item.Item.LegacyMarkup);
            if (string.IsNullOrWhiteSpace(src))
            {
                continue;
            }

            var path = ResolveImportAssetPath(src);
            if (path is null || !File.Exists(path))
            {
                continue;
            }

            var bytes = File.ReadAllBytes(path);
            var fileName = Path.GetFileName(path);
            var assetId = $"asset-{item.SourceElementId}";
            assets[item.SourceElementId] = new ElementStudioEmbeddedAsset(
                assetId,
                fileName,
                GetImageMediaType(path),
                Convert.ToBase64String(bytes),
                bytes.LongLength,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }

        return assets;
    }

    private string? ResolveImportAssetPath(string src)
    {
        if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            src.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            (src.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !src.StartsWith("https://studio-import.local/", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var normalized = src.StartsWith("https://studio-import.local/", StringComparison.OrdinalIgnoreCase)
            ? src["https://studio-import.local/".Length..]
            : src;
        normalized = System.Net.WebUtility.HtmlDecode(normalized).Replace('/', Path.DirectorySeparatorChar);

        var packageDirectory = string.IsNullOrWhiteSpace(sourcePackagePath)
            ? null
            : Path.GetDirectoryName(sourcePackagePath);
        if (string.IsNullOrWhiteSpace(packageDirectory))
        {
            return null;
        }

        return Path.GetFullPath(Path.Combine(packageDirectory, normalized));
    }

    private static string? ExtractImageSource(string? markup)
    {
        if (string.IsNullOrWhiteSpace(markup))
        {
            return null;
        }

        var match = Regex.Match(markup, """src=(?<quote>["'])(?<value>[^"']+)\k<quote>""", RegexOptions.IgnoreCase);
        return match.Success
            ? match.Groups["value"].Value
            : null;
    }

    private static string GetImageMediaType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "image/png"
        };
    }

    private string ResolveDefaultSepDirectory()
    {
        var explicitLibrary = workspace.Package.TargetLibraryPath;
        if (!string.IsNullOrWhiteSpace(explicitLibrary))
        {
            Directory.CreateDirectory(explicitLibrary);
            return explicitLibrary;
        }

        var importPackageDirectory = string.IsNullOrWhiteSpace(sourcePackagePath)
            ? null
            : Path.GetDirectoryName(sourcePackagePath);
        var importProjectLibrary = ResolveProjectLibraryFromDirectory(importPackageDirectory);
        if (importProjectLibrary is not null)
        {
            return importProjectLibrary;
        }

        var effectiveProjectId = ElementStudioSourceProjectId.ResolveEffectiveProjectId(workspace.Package.SourceProjectId);
        var repositoryProjectLibrary = ResolveProjectLibraryFromRepository(effectiveProjectId);
        if (repositoryProjectLibrary is not null)
        {
            return repositoryProjectLibrary;
        }

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SCADA_BUILDER_V2",
            "library",
            "elements");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static string? ResolveProjectLibraryFromDirectory(string? directory)
    {
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var projectJson = Path.Combine(directory, "project.json");
            if (File.Exists(projectJson))
            {
                var library = Path.Combine(directory, "library", "elements");
                Directory.CreateDirectory(library);
                return library;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }

    private static string? ResolveProjectLibraryFromRepository(string? sourceProjectId)
    {
        if (string.IsNullOrWhiteSpace(sourceProjectId))
        {
            return null;
        }

        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var scadaBuilderV2 = Path.Combine(directory.FullName, "SCADA_BUILDER_V2");
            if (Directory.Exists(scadaBuilderV2))
            {
                var library = Path.Combine(scadaBuilderV2, "projects", sourceProjectId, "library", "elements");
                Directory.CreateDirectory(library);
                return library;
            }

            directory = directory.Parent;
        }

        directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (string.Equals(directory.Name, "SCADA_BUILDER_V2", StringComparison.OrdinalIgnoreCase))
            {
                var library = Path.Combine(directory.FullName, "projects", sourceProjectId, "library", "elements");
                Directory.CreateDirectory(library);
                return library;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string ReadCurrentVersion()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "VERSION");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate).Trim();
            }

            directory = directory.Parent;
        }

        return "V2.0.0.0000";
    }

    private static string ToComponentId(string value)
    {
        var safe = ToSafeFileName(value).Trim();
        return string.IsNullOrWhiteSpace(safe)
            ? $"element-plus-{Guid.NewGuid():N}"
            : safe;
    }

    private static string ToSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().Append('/').Append('\\').ToHashSet();
        var safe = new string((string.IsNullOrWhiteSpace(value) ? ElementStudioComponentNaming.DefaultComponentName : value)
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? ElementStudioComponentNaming.DefaultComponentName : safe;
    }

    private void SynchronizeElementListSelection(IEnumerable<ElementStudioItemViewModel> selectedItems)
    {
        isSynchronizingElementSelection = true;
        try
        {
            ElementListBox.SelectedItems.Clear();
            ElementStudioItemViewModel? lastSelected = null;
            foreach (var item in selectedItems)
            {
                ElementListBox.SelectedItems.Add(item);
                lastSelected = item;
            }

            if (lastSelected is not null)
            {
                ElementListBox.ScrollIntoView(lastSelected);
            }
        }
        finally
        {
            isSynchronizingElementSelection = false;
        }
    }

    private void UpdateToolButtonStates()
    {
        foreach (var button in ToolButtonPanel.Children.OfType<Button>())
        {
            var isActive = button.Tag is string toolName &&
                string.Equals(toolName, workspace.ActiveTool, StringComparison.Ordinal);
            button.Background = isActive
                ? new SolidColorBrush(Color.FromRgb(32, 144, 160))
                : Brushes.White;
            button.Foreground = isActive
                ? Brushes.White
                : new SolidColorBrush(Color.FromRgb(15, 42, 48));
            button.BorderBrush = isActive
                ? new SolidColorBrush(Color.FromRgb(15, 114, 128))
                : new SolidColorBrush(Color.FromArgb(0x26, 0x0F, 0x2A, 0x30));
        }
    }

    private static string BuildLegacySourceDocument(ElementStudioWorkspaceViewModel workspace)
    {
        var lockedIds = workspace.ImportedItems
            .Where(item => item.IsLocked)
            .Select(item => item.SourceElementId)
            .ToHashSet(StringComparer.Ordinal);
        var groupFramesHtml = BuildGroupFramesHtml(workspace);
        return BuildLegacySourceDocument(workspace.CreatePackageSnapshot(), lockedIds, groupFramesHtml);
    }

    private static string BuildLegacySourceDocument(
        ElementStudioImportPackage package,
        IReadOnlySet<string>? lockedIds = null,
        string groupFramesHtml = "")
    {
        var width = Math.Max(320, package.Bounds.Width);
        var height = Math.Max(240, package.Bounds.Height);
        var html = string.Concat(package.Items
            .OrderBy(item => item.ZIndex)
            .Select(item => BuildLegacyItemHtml(item, lockedIds ?? new HashSet<string>(StringComparer.Ordinal))));
        return $$"""
        <!doctype html>
        <html>
        <head>
          <meta charset="utf-8">
          <style>
            html, body {
              margin: 0;
              padding: 0;
              width: {{width.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}}px;
              height: {{height.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}}px;
              overflow: hidden;
              background: transparent;
              font-family: Segoe UI, Arial, sans-serif;
            }
            #legacy-source-layer {
              position: relative;
              width: {{width.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}}px;
              height: {{height.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}}px;
            }
            #selection-marquee-layer {
              position: absolute;
              inset: 0;
              width: 100%;
              height: 100%;
              overflow: visible;
            }
            #selection-marquee-layer {
              pointer-events: none;
              z-index: 100000;
            }
            .legacy-source-item {
              position: absolute;
              overflow: visible;
              transform-origin: top left;
              pointer-events: auto;
              cursor: pointer;
            }
            .legacy-source-item svg {
              display: block;
              width: 100%;
              height: 100%;
              overflow: visible;
            }
            .legacy-source-item[data-selected="true"] {
              outline: 2px solid #2090A0;
              outline-offset: 2px;
              filter: drop-shadow(0 0 3px rgba(32,144,160,.65));
            }
            .legacy-source-item[data-locked="true"] {
              cursor: not-allowed;
              opacity: .74;
            }
            .group-frame {
              position: absolute;
              box-sizing: border-box;
              border: 1px dashed rgba(144, 192, 48, .95);
              outline: 1px solid rgba(255, 255, 255, .8);
              background: rgba(144, 192, 48, .05);
              pointer-events: none;
              z-index: 10;
            }
            .resize-handle {
              position: absolute;
              right: -6px;
              bottom: -6px;
              width: 10px;
              height: 10px;
              display: none;
              box-sizing: border-box;
              border: 1px solid #0F7280;
              background: #FFFFFF;
              cursor: nwse-resize;
              z-index: 30;
            }
            .legacy-source-item[data-selected="true"][data-locked="false"] > .resize-handle {
              display: block;
            }
            .legacy-source-fallback {
              box-sizing: border-box;
              width: 100%;
              height: 100%;
              border: 1px solid rgba(32,144,160,.55);
              background: rgba(32,144,160,.10);
              color: #0f2a30;
              font-size: 12px;
              padding: 4px;
            }
            #selection-marquee {
              position: absolute;
              display: none;
              box-sizing: border-box;
              border: 1px dashed rgba(0, 120, 212, .95);
              background: rgba(0, 120, 212, .07);
              box-shadow: inset 0 0 0 1px rgba(255,255,255,.65);
              pointer-events: none;
              z-index: 20;
            }
          </style>
        </head>
        <body>
          <div id="legacy-source-layer">
            {{html}}
            {{groupFramesHtml}}
            <div id="selection-marquee-layer">
              <div id="selection-marquee"></div>
            </div>
          </div>
          <script>
            const sourceItems = Array.from(document.querySelectorAll('[data-source-id]'));
            const sourceLayer = document.getElementById('legacy-source-layer');
            const marquee = document.getElementById('selection-marquee');
            let selectedIds = new Set();
            let dragStart = null;
            let dragMode = 'marquee';
            let didDrag = false;
            function selectSources(sourceElementIds) {
              selectedIds = new Set(sourceElementIds || []);
              sourceItems.forEach(item => {
                item.dataset.selected = selectedIds.has(item.dataset.sourceId) ? 'true' : 'false';
              });
            }
            function notifySelection() {
              window.chrome?.webview?.postMessage({ type: 'selectSources', sourceElementIds: Array.from(selectedIds) });
            }
            function getPoint(event) {
              const rect = sourceLayer.getBoundingClientRect();
              return {
                x: event.clientX - rect.left,
                y: event.clientY - rect.top
              };
            }
            function normalizeRect(a, b) {
              const left = Math.min(a.x, b.x);
              const top = Math.min(a.y, b.y);
              const right = Math.max(a.x, b.x);
              const bottom = Math.max(a.y, b.y);
              return { left, top, right, bottom, width: right - left, height: bottom - top };
            }
            function intersects(a, b) {
              return a.left <= b.right && a.right >= b.left && a.top <= b.bottom && a.bottom >= b.top;
            }
            function itemRect(item) {
              const layerRect = sourceLayer.getBoundingClientRect();
              const rect = item.getBoundingClientRect();
              return {
                left: rect.left - layerRect.left,
                top: rect.top - layerRect.top,
                right: rect.right - layerRect.left,
                bottom: rect.bottom - layerRect.top
              };
            }
            function setMarquee(rect) {
              marquee.style.display = 'block';
              marquee.style.left = `${rect.left}px`;
              marquee.style.top = `${rect.top}px`;
              marquee.style.width = `${rect.width}px`;
              marquee.style.height = `${rect.height}px`;
            }
            function hideMarquee() {
              marquee.style.display = 'none';
            }
            function selectedItems() {
              return sourceItems.filter(item => item.dataset.locked !== 'true' && selectedIds.has(item.dataset.sourceId || ''));
            }
            function applyMovePreview(deltaX, deltaY) {
              selectedItems().forEach(item => {
                item.style.transform = `translate(${deltaX}px, ${deltaY}px)`;
              });
            }
            function applyResizePreview(deltaWidth, deltaHeight) {
              selectedItems().forEach(item => {
                const baseWidth = Number(item.dataset.baseWidth || item.offsetWidth || 1);
                const baseHeight = Number(item.dataset.baseHeight || item.offsetHeight || 1);
                item.style.width = `${Math.max(1, baseWidth + deltaWidth)}px`;
                item.style.height = `${Math.max(1, baseHeight + deltaHeight)}px`;
              });
            }
            function clearMovePreview() {
              sourceItems.forEach(item => {
                item.style.transform = '';
                item.style.width = '';
                item.style.height = '';
                delete item.dataset.baseWidth;
                delete item.dataset.baseHeight;
              });
            }
            function postCommand(type, extra) {
              window.chrome?.webview?.postMessage(Object.assign({ type }, extra || {}));
            }
            sourceItems.forEach(item => {
              item.addEventListener('click', event => {
                if (didDrag) {
                  return;
                }

                event.preventDefault();
                event.stopPropagation();
                if (item.dataset.locked === 'true') {
                  return;
                }

                const sourceElementId = item.dataset.sourceId || '';
                if (event.altKey) {
                  selectedIds.delete(sourceElementId);
                  selectSources(Array.from(selectedIds));
                } else if (event.shiftKey) {
                  selectedIds.add(sourceElementId);
                  selectSources(Array.from(selectedIds));
                } else if (event.ctrlKey || event.metaKey) {
                  if (selectedIds.has(sourceElementId)) {
                    selectedIds.delete(sourceElementId);
                  } else {
                    selectedIds.add(sourceElementId);
                  }
                  selectSources(Array.from(selectedIds));
                } else {
                  selectSources([sourceElementId]);
                }
                notifySelection();
              }, true);
            });
            sourceLayer.addEventListener('pointerdown', event => {
              if (event.button !== 0) {
                return;
              }

              const resizeHandle = event.target.closest('.resize-handle');
              const sourceItem = event.target.closest('.legacy-source-item');
              const sourceElementId = sourceItem?.dataset.sourceId || '';
              if (resizeHandle && sourceItem && sourceItem.dataset.locked !== 'true') {
                event.preventDefault();
                event.stopPropagation();
                if (!selectedIds.has(sourceElementId)) {
                  selectSources([sourceElementId]);
                  notifySelection();
                }
                selectedItems().forEach(item => {
                  item.dataset.baseWidth = String(item.offsetWidth || 1);
                  item.dataset.baseHeight = String(item.offsetHeight || 1);
                });
                dragMode = 'resize';
              } else if (sourceItem && sourceItem.dataset.locked !== 'true' && !event.altKey && !event.shiftKey && !event.ctrlKey && !event.metaKey) {
                if (!selectedIds.has(sourceElementId)) {
                  selectSources([sourceElementId]);
                  notifySelection();
                }
                dragMode = 'move';
              } else {
                dragMode = 'marquee';
              }

              dragStart = getPoint(event);
              didDrag = false;
              sourceLayer.setPointerCapture(event.pointerId);
            });
            sourceLayer.addEventListener('pointermove', event => {
              if (!dragStart) {
                return;
              }

              const current = getPoint(event);
              const rect = normalizeRect(dragStart, current);
              didDrag = rect.width > 3 || rect.height > 3;
              if (didDrag && dragMode === 'resize') {
                applyResizePreview(current.x - dragStart.x, current.y - dragStart.y);
              } else if (didDrag && dragMode === 'move') {
                applyMovePreview(current.x - dragStart.x, current.y - dragStart.y);
              } else if (didDrag) {
                setMarquee(rect);
              }
            });
            sourceLayer.addEventListener('pointerup', event => {
              if (!dragStart) {
                return;
              }

              const current = getPoint(event);
              const rect = normalizeRect(dragStart, current);
              const wasDrag = didDrag;
              const wasMove = dragMode === 'move';
              const wasResize = dragMode === 'resize';
              const deltaX = current.x - dragStart.x;
              const deltaY = current.y - dragStart.y;
              dragStart = null;
              dragMode = 'marquee';
              hideMarquee();
              try {
                sourceLayer.releasePointerCapture(event.pointerId);
              } catch {
              }

              if (wasDrag && wasResize) {
                clearMovePreview();
                postCommand('resizeSources', { deltaWidth: deltaX, deltaHeight: deltaY });
              } else if (wasDrag && wasMove) {
                clearMovePreview();
                postCommand('moveSources', { deltaX, deltaY });
              } else if (wasDrag) {
                const idsInRect = sourceItems
                  .filter(item => item.dataset.locked !== 'true' && intersects(itemRect(item), rect))
                  .map(item => item.dataset.sourceId || '')
                  .filter(Boolean);
                if (event.altKey) {
                  idsInRect.forEach(id => selectedIds.delete(id));
                  selectSources(Array.from(selectedIds));
                } else if (event.shiftKey || event.ctrlKey || event.metaKey) {
                  idsInRect.forEach(id => selectedIds.add(id));
                  selectSources(Array.from(selectedIds));
                } else {
                  selectSources(idsInRect);
                }
                notifySelection();
              } else if (event.target === sourceLayer) {
                selectSources([]);
                notifySelection();
              }
            });
            document.addEventListener('keydown', event => {
              if (event.key === 'Escape') {
                event.preventDefault();
                selectSources([]);
                postCommand('clearSelection');
              } else if (event.key === 'Delete') {
                event.preventDefault();
                postCommand('deleteSelection');
              } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'a') {
                event.preventDefault();
                postCommand('selectAll');
              } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'c') {
                event.preventDefault();
                postCommand('copySelection');
              } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'v') {
                event.preventDefault();
                postCommand('pasteSelection');
              } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'd') {
                event.preventDefault();
                postCommand('duplicateSelection');
              } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') {
                event.preventDefault();
                postCommand(event.shiftKey ? 'redo' : 'undo');
              } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'y') {
                event.preventDefault();
                postCommand('redo');
              } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'h') {
                event.preventDefault();
                postCommand('hideSelection');
              } else if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(event.key)) {
                event.preventDefault();
                const step = event.shiftKey ? 10 : 1;
                const deltaX = event.key === 'ArrowLeft' ? -step : event.key === 'ArrowRight' ? step : 0;
                const deltaY = event.key === 'ArrowUp' ? -step : event.key === 'ArrowDown' ? step : 0;
                postCommand('moveSources', { deltaX, deltaY });
              }
            });
            window.elementStudio = { selectSources };
          </script>
        </body>
        </html>
        """;
    }

    private static string BuildGroupFramesHtml(ElementStudioWorkspaceViewModel workspace)
    {
        return string.Concat(workspace.ImportedItems
            .Where(item => item.IsVisible && !string.IsNullOrWhiteSpace(item.GroupId))
            .GroupBy(item => item.GroupId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group =>
            {
                var leftValue = group.Min(item => item.Bounds.X);
                var topValue = group.Min(item => item.Bounds.Y);
                var rightValue = group.Max(item => item.Bounds.X + item.Bounds.Width);
                var bottomValue = group.Max(item => item.Bounds.Y + item.Bounds.Height);
                var left = leftValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var top = topValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var width = Math.Max(1, rightValue - leftValue).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var height = Math.Max(1, bottomValue - topValue).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var groupId = HtmlEncoder.Default.Encode(group.Key ?? "");
                return $$"""
                <div class="group-frame" data-group-id="{{groupId}}" style="left: {{left}}px; top: {{top}}px; width: {{width}}px; height: {{height}}px;"></div>
                """;
            }));
    }

    private static string BuildLegacyItemHtml(ElementStudioLegacyItem item, IReadOnlySet<string> lockedIds)
    {
        var bounds = item.BoundsRelativeToPackage;
        var left = bounds.X.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var top = bounds.Y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var width = Math.Max(1, bounds.Width).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var height = Math.Max(1, bounds.Height).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var zIndex = item.ZIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var locked = lockedIds.Contains(item.SourceElementId) ? "true" : "false";
        var content = BuildLegacyItemContent(item);

        return $$"""
        <div class="legacy-source-item" data-source-id="{{HtmlEncoder.Default.Encode(item.SourceElementId)}}" data-locked="{{locked}}" style="left: {{left}}px; top: {{top}}px; width: {{width}}px; height: {{height}}px; z-index: {{zIndex}};">
          {{content}}
          <span class="resize-handle" aria-hidden="true"></span>
        </div>
        """;
    }

    private static string BuildLegacyItemContent(ElementStudioLegacyItem item)
    {
        if (string.IsNullOrWhiteSpace(item.LegacyMarkup))
        {
            return $"""<div class="legacy-source-fallback">{HtmlEncoder.Default.Encode(item.SourceName)}</div>""";
        }

        if (IsSvgLegacyMarkup(item.LegacyMarkup))
        {
            var bounds = item.BoundsAbsolute;
            var viewBoxX = bounds.X.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var viewBoxY = bounds.Y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var width = Math.Max(1, bounds.Width).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var height = Math.Max(1, bounds.Height).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            return $$"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="{{viewBoxX}} {{viewBoxY}} {{width}} {{height}}" preserveAspectRatio="none">
              {{SanitizeLegacyMarkup(item.LegacyMarkup)}}
            </svg>
            """;
        }

        return NormalizeHtmlLegacyMarkupForStudio(item);
    }

    private static bool IsSvgLegacyMarkup(string? markup)
    {
        if (string.IsNullOrWhiteSpace(markup))
        {
            return false;
        }

        var trimmed = markup.TrimStart();
        return trimmed.StartsWith("<polygon", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<polyline", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<line", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<path", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<rect", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<circle", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<ellipse", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<text", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeLegacyMarkup(string? markup)
    {
        if (string.IsNullOrWhiteSpace(markup))
        {
            return "";
        }

        var cleaned = Regex.Replace(markup, @"\sdata-scada-selected=""[^""]*""", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\sdata-selected=""[^""]*""", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\sclass=""[^""]*(scada-modern|scada-extract|selected)[^""]*""", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"outline\s*:\s*[^;""']+;?", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"box-shadow\s*:\s*[^;""']+;?", "", RegexOptions.IgnoreCase);
        return cleaned;
    }

    private static string NormalizeHtmlLegacyMarkupForStudio(ElementStudioLegacyItem item)
    {
        var cleaned = RewriteLocalAssetSources(SanitizeLegacyMarkup(item.LegacyMarkup));
        cleaned = Regex.Replace(
            cleaned,
            @"\sstyle=""(?<style>[^""]*)""",
            match =>
            {
                var style = NormalizeHtmlStyleForStudio(match.Groups["style"].Value, item);
                return $" style=\"{HtmlEncoder.Default.Encode(style)}\"";
            },
            RegexOptions.IgnoreCase);

        if (!Regex.IsMatch(cleaned, @"\sstyle=""", RegexOptions.IgnoreCase))
        {
            cleaned = Regex.Replace(
                cleaned,
                @"^(<\w+)",
                $"$1 style=\"{HtmlEncoder.Default.Encode(NormalizeHtmlStyleForStudio("", item))}\"",
                RegexOptions.IgnoreCase);
        }

        return cleaned;
    }

    private static string NormalizeHtmlStyleForStudio(string style, ElementStudioLegacyItem item)
    {
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "position",
            "left",
            "top",
            "right",
            "bottom",
            "inset",
            "inset-block-start",
            "inset-block-end",
            "inset-inline-start",
            "inset-inline-end",
            "width",
            "height",
            "inline-size",
            "block-size",
            "z-index"
        };

        var declarations = style
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(declaration =>
            {
                var separator = declaration.IndexOf(':');
                if (separator <= 0)
                {
                    return false;
                }

                var property = declaration[..separator].Trim();
                return !blocked.Contains(property);
            })
            .ToList();

        declarations.Add("position: static");
        declarations.Add("left: auto");
        declarations.Add("top: auto");
        declarations.Add("width: 100%");
        declarations.Add("height: 100%");
        declarations.Add("box-sizing: border-box");

        if (item.LegacyType.Contains("image", StringComparison.OrdinalIgnoreCase) ||
            item.LegacyMarkup?.TrimStart().StartsWith("<img", StringComparison.OrdinalIgnoreCase) == true)
        {
            declarations.Add("display: block");
            declarations.Add("object-fit: fill");
        }

        return string.Join("; ", declarations) + ";";
    }

    private static string RewriteLocalAssetSources(string markup)
    {
        return Regex.Replace(
            markup,
            """src=(?<quote>["'])(?<value>[^"']+)\k<quote>""",
            match =>
            {
                var quote = match.Groups["quote"].Value;
                var value = System.Net.WebUtility.HtmlDecode(match.Groups["value"].Value);
                if (value.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
                {
                    return $"src={quote}https://studio-import.local/{value}{quote}";
                }

                return match.Value;
            },
            RegexOptions.IgnoreCase);
    }

    private sealed class StudioWebMessage
    {
        public string Type { get; set; } = "";

        public string SourceElementId { get; set; } = "";

        public string[] SourceElementIds { get; set; } = [];

        public double DeltaX { get; set; }

        public double DeltaY { get; set; }

        public double DeltaWidth { get; set; }

        public double DeltaHeight { get; set; }
    }
}
