using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Conversion;
using ScadaBuilderV2.Application.ElementStudio;
using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Application.Selection;
using ScadaBuilderV2.Domain.Editor;
using ScadaBuilderV2.Domain.Elements;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ElementStudio;
using ScadaBuilderV2.Infrastructure.ModernProjects;
using ScadaBuilderV2.Infrastructure.ReferenceProjects;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.App;

public partial class MainWindow : Window
{
    public static readonly RoutedCommand UndoSceneCommand = new(nameof(UndoSceneCommand), typeof(MainWindow));
    public static readonly RoutedCommand RedoSceneCommand = new(nameof(RedoSceneCommand), typeof(MainWindow));
    private const int ShowWindowRestore = 9;
    private const string ElementPlusLibraryDragFormat = "ScadaBuilderV2.ElementPlusLibraryItemPath";
    private const string TagCatalogAllDevicesFilter = "Tous les appareils";
    private const string TagCatalogAllDatatypesFilter = "Tous les types";
    private const string TagCatalogAllAccessFilter = "Tous les acces";
    private const string TagCatalogAllStatesFilter = "Tous les etats";
    private readonly IReferenceScadaProjectReader _referenceReader = new ReferenceScadaProjectReader();
    private readonly ModernProjectStore _modernProjectStore = new();
    private readonly Tf100WebTagCatalogImporter _tagCatalogImporter = new();
    private readonly IElementStudioImportPackageWriter _elementStudioPackageWriter = new ElementStudioImportPackageWriter();
    private readonly ElementStudioComponentPackageStore _elementStudioComponentPackageStore = new();
    private readonly ElementPlusLibraryReader _elementPlusLibraryReader = new();
    private readonly ObservableCollection<ElementPlusLibraryItem> _elementLibraryItems = [];
    private readonly ObservableCollection<TagCatalogListItem> _tagCatalogItems = [];
    private readonly ICollectionView _tagCatalogView;
    private readonly ObservableCollection<SceneWorkspaceTab> _openSceneTabs = [];
    private readonly Dictionary<string, IReadOnlyList<RibbonGroupDefinition>> _ribbonTabs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _hiddenSourceObjectIds = new(StringComparer.Ordinal);
    private readonly List<LegacyElementListItem> _sourceObjects = [];
    private readonly ActiveSelectionState _activeSelection = new();
    private HashSet<string> _selectedSourceObjectIds => _activeSelection.SourceObjectIds;
    private HashSet<string> _selectedSceneObjectIds => _activeSelection.SceneObjectIds;
    private ScadaElement? _selectedSceneObject
    {
        get => _activeSelection.PrimarySceneObject;
        set => _activeSelection.PrimarySceneObject = value;
    }

    private static readonly JsonSerializerOptions WebMessageJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private ReferenceScadaProjectManifest? _referenceProject;
    private ScadaProject? _modernProject;
    private ReferenceScadaPage? _activeReferencePage;
    private ScadaScene? _activeScene;
    private SceneWorkspaceTab? _activeSceneTab;
    private string? _repositoryRoot;
    private int _extractedCandidateCount;
    private bool _webMessageHooked;
    private bool _isUpdatingBackgroundColorControls;
    private bool _isUpdatingPagePropertyControls;
    private bool _isUpdatingElementProperties;
    private bool _isUpdatingSceneObjectList;
    private bool _isDraggingBackgroundColorPicker;
    private double _backgroundHue;
    private double _backgroundSaturation;
    private double _backgroundValue;
    private ScadaElementKind? _pendingInsertKind;
    private ScadaShapeKind? _pendingInsertShapeKind;
    private ScadaButtonKind? _pendingInsertButtonKind;
    private string _activeRibbonKey = "File";
    private string? _activeInsertCommandId;
    private int _nextTextSequence = 1;
    private int _nextInputTextSequence = 1;
    private int _nextInputNumericSequence = 1;
    private int _nextShapeSequence = 1;
    private int _nextButtonSequence = 1;
    private int _nextGroupSequence = 1;
    private bool _activeSceneDirty;
    private bool _isFt100Sb2ExportRunning;
    private bool _isUpdatingPageSelection;
    private bool _isUpdatingSceneTabSelection;
    private bool _isClosingConfirmed;
    private FileSystemWatcher? _elementLibraryWatcher;
    private DispatcherTimer? _elementLibraryRefreshTimer;
    private readonly DispatcherTimer _pageDimensionApplyTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private Point _elementLibraryDragStartPoint;
    private string? _sceneBackgroundDocumentScriptId;
    private ScadaScene? _pageDimensionEditBeforeScene;

    public string StatusText { get; private set; } = "Etat / Warning";

    public ObservableCollection<RibbonGroupViewModel> ActiveRibbonGroups { get; } = [];

    public ObservableCollection<RibbonCommandViewModel> ToolPaletteCommands { get; } = [];

    public bool IsSelectionLocked { get; set; } = false;

    public MainWindow()
    {
        InitializeComponent();
        InitializeRibbonCommandRegistry();
        InitializeToolPaletteCommands();
        DataContext = this;
        SetActiveRibbon("File");
        ElementLibraryListBox.ItemsSource = _elementLibraryItems;
        _tagCatalogView = CollectionViewSource.GetDefaultView(_tagCatalogItems);
        _tagCatalogView.Filter = FilterTagCatalogItem;
        TagCatalogDataGrid.ItemsSource = _tagCatalogView;
        InitializeTagCatalogFilters();
        SceneTabs.ItemsSource = _openSceneTabs;
        _pageDimensionApplyTimer.Tick += OnPageDimensionApplyTimerTick;
        StatusTextBlock.Text = $"Etat / Warning - {LoadVersionText()}";
        SetBackgroundColorControls("#000000");

        var registry = new CommandRegistry();
        registry.Register(new ToggleSelectionLockCommand());

        PreviewWebView.NavigationCompleted += OnPreviewNavigationCompleted;
        Closing += OnMainWindowClosing;
        Closed += (_, _) =>
        {
            foreach (var tab in _openSceneTabs)
            {
                tab.History.Clear();
            }

            StopElementLibraryWatcher();
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await LoadReferenceProjectAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Erreur chargement source legacy: {ex.Message}");
        }
    }

    private async Task LoadReferenceProjectAsync()
    {
        _repositoryRoot = ResolveRepositoryRoot();
        _referenceProject = await _referenceReader.LoadAmrReferenceAsync(_repositoryRoot);
        var sceneReferences = _referenceProject.Pages
            .Select(page => new ScadaSceneReference(page.Id, page.Title, $"scenes/{page.Id}.scene.json"))
            .ToArray();
        _modernProject = await _modernProjectStore.EnsureReferenceModernProjectAsync(_repositoryRoot, sceneReferences);
        StartElementLibraryWatcher();
        await RefreshElementLibraryAsync();

        ProjectNameText.Text = $"{_referenceProject.Name} ({_referenceProject.Pages.Count} pages)";
        RefreshProjectTagSummary();
        PagesListBox.ItemsSource = _referenceProject.Pages;

        var preferredPageId = _modernProject.EffectiveHomePageId;
        var preferredPage = _referenceProject.Pages.FirstOrDefault(page => page.Id == preferredPageId)
            ?? _referenceProject.Pages.FirstOrDefault(page => page.Id == "win00008")
            ?? _referenceProject.Pages.FirstOrDefault();
        if (preferredPage is not null)
        {
            _isUpdatingPageSelection = true;
            try
            {
                PagesListBox.SelectedItem = preferredPage;
            }
            finally
            {
                _isUpdatingPageSelection = false;
            }

            await OpenSceneTabAsync(preferredPage);
        }

        SetStatus($"Source legacy chargee en lecture seule: {_referenceProject.Name}");
    }

    private async void OnPageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPageSelection)
        {
            return;
        }

        if (PagesListBox.SelectedItem is not ReferenceScadaPage page)
        {
            return;
        }

        try
        {
            await OpenSceneTabAsync(page);
            RightContextTabs.SelectedItem = PageContextTab;
        }
        catch (Exception ex)
        {
            SetStatus($"Erreur preview {page.Id}: {ex.Message}");
        }
    }

    private async Task OpenSceneTabAsync(ReferenceScadaPage page)
    {
        if (_referenceProject is null)
        {
            return;
        }

        var existing = _openSceneTabs.FirstOrDefault(tab => string.Equals(tab.SceneId, page.Id, StringComparison.Ordinal));
        if (existing is not null)
        {
            await ActivateSceneTabAsync(existing);
            return;
        }

        if (_repositoryRoot is null)
        {
            return;
        }

        var scene = await _modernProjectStore.LoadOrCreateSceneAsync(
            _repositoryRoot,
            page.Id,
            page.Title,
            CanvasSize.DefaultDesktop);
        var tab = new SceneWorkspaceTab(page, scene);
        _openSceneTabs.Add(tab);
        await ActivateSceneTabAsync(tab);
    }

    private async Task ActivateSceneTabAsync(SceneWorkspaceTab tab)
    {
        SaveActiveTabTransientState();

        _activeSceneTab = tab;
        _activeScene = tab.Scene;
        _activeReferencePage = tab.ReferencePage;
        _activeSceneDirty = tab.IsDirty;
        RestoreTabSelectionState(tab);

        _isUpdatingSceneTabSelection = true;
        try
        {
            SceneTabs.SelectedItem = tab;
        }
        finally
        {
            _isUpdatingSceneTabSelection = false;
        }

        _isUpdatingPageSelection = true;
        try
        {
            PagesListBox.SelectedItem = tab.ReferencePage;
        }
        finally
        {
            _isUpdatingPageSelection = false;
        }

        ResetElementSequences(_activeScene);
        SetBackgroundColorControls(_activeScene.BackgroundColor);
        LoadPageProperties(_activeScene);
        RefreshModernSceneUi();
        await LoadActiveTabPreviewAsync();
    }

    private async Task LoadActiveTabPreviewAsync()
    {
        if (_activeSceneTab is null || _activeReferencePage is null)
        {
            SetPreviewPlaceholder("Aucune scene active.");
            return;
        }

        var source = await ResolveLegacyViewerSourceAsync(_activeReferencePage);
        if (source is null)
        {
            SetPreviewPlaceholder("Aucune source HTML legacy trouvee pour cette page.");
            return;
        }

        _selectedSourceObjectIds.Clear();
        _hiddenSourceObjectIds.Clear();
        CacheConvertedLegacyIdsFromActiveScene();
        _sourceObjects.Clear();
        RefreshSelectionUi();

        var backgroundColor = _activeScene?.BackgroundColor ?? "#000000";
        UpdatePreviewSurfaceBackground(backgroundColor);

        var preview = new PreviewDocument(_activeReferencePage.Id, _activeReferencePage.Title, source.RelativeHtmlSource);
        var sourceUri = preview.GetSourceUri(source.RootPath);
        await PrepareInitialSceneBackgroundScriptAsync(backgroundColor);

        ActivePageText.Text = _activeReferencePage.Id;
        PreviewSourceText.Text = sourceUri.LocalPath;
        PreviewPlaceholder.Visibility = Visibility.Collapsed;
        if (PreviewWebView.Source is not null && PreviewWebView.Source.Equals(sourceUri))
        {
            PreviewWebView.Visibility = Visibility.Collapsed;
            await RefreshActiveSceneInViewerAsync();
            PreviewWebView.Visibility = Visibility.Visible;
        }
        else
        {
            PreviewWebView.Visibility = Visibility.Collapsed;
            PreviewWebView.Source = sourceUri;
        }

        SetStatus($"Legacy Viewer charge: {_activeReferencePage.Id} ({source.Kind})");
    }

    private void SetPreviewPlaceholder(string message)
    {
        ActivePageText.Text = _activeReferencePage?.Id ?? "-";
        PreviewSourceText.Text = "-";
        PreviewWebView.Visibility = Visibility.Collapsed;
        PreviewPlaceholder.Text = message;
        PreviewPlaceholder.Visibility = Visibility.Visible;
    }

    private async void OnSceneTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSceneTabSelection || !ReferenceEquals(e.OriginalSource, SceneTabs))
        {
            return;
        }

        if (SceneTabs.SelectedItem is not SceneWorkspaceTab tab || ReferenceEquals(tab, _activeSceneTab))
        {
            return;
        }

        try
        {
            await ActivateSceneTabAsync(tab);
        }
        catch (Exception ex)
        {
            SetStatus($"Erreur activation scene {tab.SceneId}: {ex.Message}");
        }
    }

    private async void OnCloseSceneTabClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: SceneWorkspaceTab tab })
        {
            return;
        }

        try
        {
            await CloseSceneTabAsync(tab);
        }
        catch (Exception ex)
        {
            SetStatus($"Fermeture scene impossible: {ex.Message}");
        }
    }

    private async Task<bool> CloseSceneTabAsync(SceneWorkspaceTab tab)
    {
        SaveActiveTabTransientState();
        if (!await ConfirmSaveDirtyTabAsync(tab))
        {
            return false;
        }

        var removedIndex = _openSceneTabs.IndexOf(tab);
        var wasActive = ReferenceEquals(tab, _activeSceneTab);
        _openSceneTabs.Remove(tab);

        if (!wasActive)
        {
            SetStatus($"Scene fermee: {tab.SceneId}");
            return true;
        }

        _activeSceneTab = null;
        _activeScene = null;
        _activeReferencePage = null;
        _activeSceneDirty = false;
        _selectedSceneObject = null;
        _selectedSceneObjectIds.Clear();
        _selectedSourceObjectIds.Clear();
        _hiddenSourceObjectIds.Clear();
        _sourceObjects.Clear();

        if (_openSceneTabs.Count == 0)
        {
            _isUpdatingSceneTabSelection = true;
            try
            {
                SceneTabs.SelectedItem = null;
            }
            finally
            {
                _isUpdatingSceneTabSelection = false;
            }

            RefreshSelectionUi();
            RefreshModernSceneUi();
            SetPreviewPlaceholder("Aucune scene ouverte.");
            SetStatus($"Scene fermee: {tab.SceneId}");
            return true;
        }

        var nextIndex = Math.Clamp(removedIndex, 0, _openSceneTabs.Count - 1);
        await ActivateSceneTabAsync(_openSceneTabs[nextIndex]);
        SetStatus($"Scene fermee: {tab.SceneId}");
        return true;
    }

    private async Task<bool> ConfirmSaveDirtyTabAsync(SceneWorkspaceTab tab)
    {
        if (!tab.IsDirty)
        {
            return true;
        }

        var result = MessageBox.Show(
            this,
            $"La scene {tab.SceneId} contient des modifications non sauvegardees.{Environment.NewLine}{Environment.NewLine}Voulez-vous sauvegarder avant de fermer?",
            "Fermer la scene",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel)
        {
            return false;
        }

        if (result == MessageBoxResult.Yes)
        {
            await SaveSceneTabAsync(tab);
        }

        return true;
    }

    private async Task SaveSceneTabAsync(SceneWorkspaceTab tab)
    {
        if (_repositoryRoot is null)
        {
            SetStatus("Aucun projet actif pour sauvegarder la scene.");
            return;
        }

        if (ReferenceEquals(tab, _activeSceneTab) && _activeScene is not null)
        {
            tab.Scene = _activeScene;
        }

        if (_modernProject is not null)
        {
            await _modernProjectStore.SaveProjectAsync(_repositoryRoot, _modernProject);
        }

        await _modernProjectStore.SaveSceneAsync(_repositoryRoot, tab.Scene);
        _modernProject = await _modernProjectStore.LoadProjectAsync(_repositoryRoot) ?? _modernProject;
        tab.IsDirty = false;
        if (ReferenceEquals(tab, _activeSceneTab))
        {
            _activeSceneDirty = false;
            LoadPageProperties(_activeScene);
            RefreshModernSceneUi();
        }

        SetStatus($"Scene V2 sauvegardee: {tab.SceneId}");
    }

    private async void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isClosingConfirmed)
        {
            return;
        }

        e.Cancel = true;
        SaveActiveTabTransientState();

        foreach (var tab in _openSceneTabs.Where(tab => tab.IsDirty).ToArray())
        {
            if (!await ConfirmSaveDirtyTabAsync(tab))
            {
                SetStatus("Fermeture annulee: sauvegarde des scenes non terminee.");
                return;
            }
        }

        _isClosingConfirmed = true;
        Close();
    }

    private void SaveActiveTabTransientState()
    {
        if (_activeSceneTab is null)
        {
            return;
        }

        if (_activeScene is not null)
        {
            _activeSceneTab.Scene = _activeScene;
        }

        _activeSceneTab.IsDirty = _activeSceneDirty;
        _activeSceneTab.SelectedModernElementIds = _selectedSceneObjectIds.ToArray();
        _activeSceneTab.PrimaryModernElementId = _selectedSceneObject?.Id;
    }

    private void RestoreTabSelectionState(SceneWorkspaceTab tab)
    {
        _selectedSceneObjectIds.Clear();
        foreach (var id in tab.SelectedModernElementIds)
        {
            if (tab.Scene.FindElementRecursive(id) is not null)
            {
                _selectedSceneObjectIds.Add(id);
            }
        }

        _selectedSceneObject =
            (!string.IsNullOrWhiteSpace(tab.PrimaryModernElementId)
                ? tab.Scene.FindElementRecursive(tab.PrimaryModernElementId)
                : null) ??
            (_selectedSceneObjectIds.Count == 0
                ? null
                : tab.Scene.FindElementRecursive(_selectedSceneObjectIds.Last()));

        _selectedSourceObjectIds.Clear();
        _hiddenSourceObjectIds.Clear();
        CacheConvertedLegacyIdsFromActiveScene();
        _sourceObjects.Clear();
    }

    private static async Task<string> ReadLegacyHtmlSourceAsync(string pageJsonPath)
    {
        await using var stream = File.OpenRead(pageJsonPath);
        using var document = await JsonDocument.ParseAsync(stream);

        if (document.RootElement.TryGetProperty("legacy", out var legacy) &&
            legacy.TryGetProperty("source_html", out var sourceHtml))
        {
            return sourceHtml.GetString()?.Trim() ?? "";
        }

        if (document.RootElement.TryGetProperty("layers", out var layers) &&
            layers.ValueKind == JsonValueKind.Array)
        {
            foreach (var layer in layers.EnumerateArray())
            {
                if (layer.TryGetProperty("type", out var type) &&
                    string.Equals(type.GetString(), "legacy_embed", StringComparison.OrdinalIgnoreCase) &&
                    layer.TryGetProperty("src", out var src))
                {
                    return src.GetString()?.Trim().TrimStart('.', '/', '\\') ?? "";
                }
            }
        }

        return "";
    }

    private async Task<LegacyViewerSource?> ResolveLegacyViewerSourceAsync(ReferenceScadaPage page)
    {
        if (_referenceProject is null)
        {
            return null;
        }

        var relativeHtmlSource = await ReadLegacyHtmlSourceAsync(page.AbsolutePath);
        if (!string.IsNullOrWhiteSpace(relativeHtmlSource))
        {
            var referenceSource = new LegacyViewerSource(_referenceProject.ProjectDirectory, relativeHtmlSource, "reference-html");
            if (File.Exists(Path.Combine(referenceSource.RootPath, referenceSource.RelativeHtmlSource)))
            {
                return referenceSource;
            }
        }

        if (_repositoryRoot is not null)
        {
            var rawLegacyHtml = FindRawLegacyHtml(_repositoryRoot, page.Id);
            if (rawLegacyHtml is not null)
            {
                return rawLegacyHtml;
            }
        }

        return string.IsNullOrWhiteSpace(relativeHtmlSource)
            ? null
            : new LegacyViewerSource(_referenceProject.ProjectDirectory, relativeHtmlSource, "reference-html-missing");
    }

    private static LegacyViewerSource? FindRawLegacyHtml(string repositoryRoot, string pageId)
    {
        var rawHtmlRoot = Path.Combine(repositoryRoot, "03_web_legacy", "html_pages");
        if (!Directory.Exists(rawHtmlRoot))
        {
            return null;
        }

        var rawFile = Directory
            .EnumerateFiles(rawHtmlRoot, $"{pageId}_*.html", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).Contains("_updated", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (rawFile is null)
        {
            return null;
        }

        var relativePath = Path.GetRelativePath(repositoryRoot, rawFile);
        return new LegacyViewerSource(repositoryRoot, relativePath, "raw-html");
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "SCADA_BUILDER")) &&
                Directory.Exists(Path.Combine(current.FullName, "SCADA_BUILDER_V2")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to resolve SCADA_AMR_GROUP repository root.");
    }

    private void SetStatus(string text)
    {
        StatusTextBlock.Text = text;
    }

    private void SetFt100ExportProgress(bool isVisible)
    {
        Ft100ExportProgressBar.Visibility = isVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void OnRefreshElementLibraryClick(object sender, RoutedEventArgs e)
    {
        await RefreshElementLibraryAsync();
    }

    private void OnElementLibrarySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ElementLibraryListBox.SelectedItem is ElementPlusLibraryItem item)
        {
            SetStatus($"{item.FileName}: double-cliquez pour instancier dans la scene active.");
        }
    }

    private void OnElementLibraryPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _elementLibraryDragStartPoint = e.GetPosition(null);
    }

    private async void OnElementLibraryMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var item = ResolveElementLibraryItemFromEvent(e.OriginalSource as DependencyObject)
            ?? ElementLibraryListBox.SelectedItem as ElementPlusLibraryItem;
        if (item is null)
        {
            return;
        }

        e.Handled = true;
        var position = await ResolveVisibleSceneCenterAsync();
        await CreateElementPlusLibraryInstanceAsync(item.FilePath, position.X, position.Y, centerOnPoint: true);
    }

    private void OnElementLibraryPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            ElementLibraryListBox.SelectedItem is not ElementPlusLibraryItem item)
        {
            return;
        }

        var current = e.GetPosition(null);
        if (Math.Abs(current.X - _elementLibraryDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _elementLibraryDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(
            ElementLibraryListBox,
            new DataObject(ElementPlusLibraryDragFormat, item.FilePath),
            DragDropEffects.Copy);
    }

    private void OnPreviewWebViewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(ElementPlusLibraryDragFormat)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnPreviewWebViewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(ElementPlusLibraryDragFormat) ||
            e.Data.GetData(ElementPlusLibraryDragFormat) is not string packagePath)
        {
            return;
        }

        var position = e.GetPosition(PreviewWebView);
        await CreateElementPlusLibraryInstanceAsync(packagePath, position.X, position.Y);
        e.Handled = true;
    }

    private static ElementPlusLibraryItem? ResolveElementLibraryItemFromEvent(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ListBoxItem { DataContext: ElementPlusLibraryItem item })
            {
                return item;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private async Task<Point> ResolveVisibleSceneCenterAsync()
    {
        const double fallbackX = 80;
        const double fallbackY = 80;

        if (PreviewWebView.CoreWebView2 is null)
        {
            return new Point(fallbackX, fallbackY);
        }

        try
        {
            var result = await PreviewWebView.ExecuteScriptAsync("""
(() => {
  const surface = document.querySelector('.page') || document.querySelector('#scada-root') || document.body;
  const rect = surface.getBoundingClientRect();
  const x = Math.max(0, (window.innerWidth / 2) - rect.left + surface.scrollLeft);
  const y = Math.max(0, (window.innerHeight / 2) - rect.top + surface.scrollTop);
  return { x, y };
})()
""");

            using var document = JsonDocument.Parse(result);
            var root = document.RootElement;
            return new Point(
                root.TryGetProperty("x", out var x) && x.TryGetDouble(out var parsedX) ? parsedX : fallbackX,
                root.TryGetProperty("y", out var y) && y.TryGetDouble(out var parsedY) ? parsedY : fallbackY);
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException or ArgumentException)
        {
            SetStatus($"Position scene non disponible, insertion par defaut: {ex.Message}");
            return new Point(fallbackX, fallbackY);
        }
    }

    private async Task RefreshElementLibraryAsync()
    {
        var libraryRoot = ResolveElementPlusLibraryRoot(create: true);
        if (libraryRoot is null)
        {
            _elementLibraryItems.Clear();
            ElementLibraryStatusText.Text = "Aucun projet actif pour charger la librairie Element+.";
            return;
        }

        try
        {
            var snapshot = await _elementPlusLibraryReader.ReadAsync(libraryRoot);
            _elementLibraryItems.Clear();
            foreach (var item in snapshot.Items)
            {
                _elementLibraryItems.Add(item);
            }

            var diagnostics = snapshot.Diagnostics.Count == 0
                ? ""
                : $" {snapshot.Diagnostics.Count} fichier(s) ignore(s).";
            ElementLibraryStatusText.Text =
                $"{snapshot.Items.Count} Element+ charge(s) depuis {libraryRoot}.{diagnostics}";
        }
        catch (Exception ex)
        {
            ElementLibraryStatusText.Text = $"Chargement librairie impossible: {ex.Message}";
        }
    }

    private void StartElementLibraryWatcher()
    {
        StopElementLibraryWatcher();

        var libraryRoot = ResolveElementPlusLibraryRoot(create: true);
        if (libraryRoot is null)
        {
            return;
        }

        _elementLibraryRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _elementLibraryRefreshTimer.Tick += async (_, _) =>
        {
            _elementLibraryRefreshTimer?.Stop();
            await RefreshElementLibraryAsync();
        };

        _elementLibraryWatcher = new FileSystemWatcher(libraryRoot, "*.sep")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
        };
        _elementLibraryWatcher.Created += OnElementLibraryChanged;
        _elementLibraryWatcher.Changed += OnElementLibraryChanged;
        _elementLibraryWatcher.Deleted += OnElementLibraryChanged;
        _elementLibraryWatcher.Renamed += OnElementLibraryRenamed;
        _elementLibraryWatcher.EnableRaisingEvents = true;
    }

    private void StopElementLibraryWatcher()
    {
        if (_elementLibraryWatcher is not null)
        {
            _elementLibraryWatcher.EnableRaisingEvents = false;
            _elementLibraryWatcher.Created -= OnElementLibraryChanged;
            _elementLibraryWatcher.Changed -= OnElementLibraryChanged;
            _elementLibraryWatcher.Deleted -= OnElementLibraryChanged;
            _elementLibraryWatcher.Renamed -= OnElementLibraryRenamed;
            _elementLibraryWatcher.Dispose();
            _elementLibraryWatcher = null;
        }

        _elementLibraryRefreshTimer?.Stop();
        _elementLibraryRefreshTimer = null;
    }

    private void OnElementLibraryChanged(object sender, FileSystemEventArgs e)
    {
        ScheduleElementLibraryRefresh();
    }

    private void OnElementLibraryRenamed(object sender, RenamedEventArgs e)
    {
        ScheduleElementLibraryRefresh();
    }

    private void ScheduleElementLibraryRefresh()
    {
        Dispatcher.BeginInvoke(() =>
        {
            _elementLibraryRefreshTimer?.Stop();
            _elementLibraryRefreshTimer?.Start();
        });
    }

    private string? ResolveElementPlusLibraryRoot(bool create)
    {
        if (_repositoryRoot is null)
        {
            return null;
        }

        var libraryRoot = Path.Combine(
            ModernProjectStore.GetReferenceModernProjectRoot(_repositoryRoot),
            "library",
            "elements");

        if (create)
        {
            Directory.CreateDirectory(libraryRoot);
        }

        return libraryRoot;
    }

    private void OnStatusDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        var diagnostics = new List<string>
        {
            $"Status courant: {StatusTextBlock.Text}",
            $"Version: {LoadVersionText()}"
        };

        var studioLaunchLogPath = ResolveElementStudioLaunchLogPath();
        if (studioLaunchLogPath is not null && File.Exists(studioLaunchLogPath))
        {
            diagnostics.Add("");
            diagnostics.Add($"Log Studio Element+: {studioLaunchLogPath}");
            diagnostics.Add("");
            diagnostics.AddRange(ReadLastLines(studioLaunchLogPath, 12));
        }
        else
        {
            diagnostics.Add("");
            diagnostics.Add("Aucun log Studio Element+ trouve pour le projet actif.");
        }

        MessageBox.Show(
            string.Join(Environment.NewLine, diagnostics),
            "Diagnostics SCADA Builder V2",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private string? ResolveElementStudioLaunchLogPath()
    {
        if (_repositoryRoot is null)
        {
            return null;
        }

        return Path.Combine(
            _repositoryRoot,
            "SCADA_BUILDER_V2",
            "projects",
            "AMR_REF_SCADA_V2",
            ".studio",
            "logs",
            "element-studio-launch.log");
    }

    private static IReadOnlyList<string> ReadLastLines(string path, int count)
    {
        try
        {
            return File.ReadLines(path).TakeLast(count).ToArray();
        }
        catch (Exception ex)
        {
            return [$"Lecture du log impossible: {ex.Message}"];
        }
    }

    private void CacheConvertedLegacyIdsFromActiveScene()
    {
        if (_activeScene is null)
        {
            return;
        }

        foreach (var sourceElementId in _activeScene.GetSuppressedSourceElementIds())
        {
            _hiddenSourceObjectIds.Add(sourceElementId);
        }
    }

    private async Task HideConvertedLegacyElementsInViewerAsync()
    {
        if (_hiddenSourceObjectIds.Count == 0)
        {
            return;
        }

        await RemoveLegacyElementsInViewerAsync(_hiddenSourceObjectIds.ToArray());
    }

    private async Task ResetSourceProjectionVisibilityFromActiveSceneAsync()
    {
        if (_activeScene is null)
        {
            return;
        }

        await ExecuteLegacyViewerCommandAsync("restoreHidden");
        _hiddenSourceObjectIds.Clear();
        foreach (var sourceElementId in _activeScene.GetSuppressedSourceElementIds())
        {
            _hiddenSourceObjectIds.Add(sourceElementId);
        }

        await HideConvertedLegacyElementsInViewerAsync();
    }

    private async void OnPreviewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            SetStatus($"Erreur navigation Legacy Viewer: {e.WebErrorStatus}");
            return;
        }

        try
        {
            await EnsureWebMessageHookAsync();
            await PreviewWebView.ExecuteScriptAsync(LegacyExtractionScript);
            await RefreshActiveSceneInViewerAsync();
            PreviewWebView.Visibility = Visibility.Visible;
            SetStatus("Legacy Extraction actif: Ctrl ajoute/retire, Alt retire, drag selectionne, clic droit ouvre les actions.");
        }
        catch (Exception ex)
        {
            SetStatus($"Erreur injection Legacy Extraction: {ex.Message}");
        }
    }

    private async Task RefreshActiveSceneInViewerAsync()
    {
        if (_activeScene is not null)
        {
            await ApplySceneBackgroundColorAsync(_activeScene.BackgroundColor, updateStatus: false);
            await ApplySceneCanvasSizeAsync(_activeScene.CanvasSize);
        }

        await RenderModernSceneAsync();
        await HideConvertedLegacyElementsInViewerAsync();
        await ApplySourceElementBoundsInViewerAsync();
    }

    private async Task EnsureWebMessageHookAsync()
    {
        await PreviewWebView.EnsureCoreWebView2Async();
        if (_webMessageHooked || PreviewWebView.CoreWebView2 is null)
        {
            return;
        }

        PreviewWebView.CoreWebView2.WebMessageReceived += OnLegacyViewerMessageReceived;
        _webMessageHooked = true;
    }

    private void OnLegacyViewerMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var message = JsonSerializer.Deserialize<LegacyViewerMessage>(e.WebMessageAsJson, WebMessageJsonOptions);
            if (message is null)
            {
                return;
            }

            switch (message.Type)
            {
                case "inventory":
                    ApplyLegacyInventory(message.Items ?? []);
                    break;
                case "selection":
                    ApplyLegacySelection(message.Items ?? []);
                    break;
                case "hideSelected":
                    _ = HideSelectedLegacyElementsAsync();
                    break;
                case "deleteSelected":
                    _ = DeleteSelectedSceneObjectsAsync();
                    break;
                case "extractSelected":
                    ExtractSelectedLegacyElements();
                    break;
                case "convertSelectedTextToElementPlus":
                    _ = ConvertSelectedLegacyTextToElementPlusAsync(ElementPlusConversionTarget.Text);
                    break;
                case "clearSelection":
                    _ = ClearSelectionAsync();
                    break;
                case "clearObjectSelection":
                case "clearModernSelection":
                    ClearModernSelection();
                    break;
                case "clearAllSelection":
                    ClearSelection();
                    break;
                case "editBackground":
                    ShowBackgroundCssEditor(message.BackgroundColor);
                    break;
                case "contextMenuRequest":
                    _ = ShowContextMenuForRequestAsync(message);
                    break;
                case "executeCommand":
                    _ = ExecuteEditorCommandAsync(message.CommandId, message);
                    break;
                case "placeElement":
                    PlaceModernElement(message.Kind, message.ShapeKind, message.X, message.Y);
                    break;
                case "placeTwoPointElement":
                    PlaceTwoPointShape(message.ShapeKind, message.X, message.Y, message.X2, message.Y2);
                    break;
                case "cancelPlacement":
                    ResetPendingInsertion();
                    SetStatus("Insertion annulee.");
                    break;
                case "selectSceneObject":
                case "selectModernElement":
                    SelectModernElement(message.Id, message.Additive, message.Toggle);
                    break;
                case "openSceneObjectProperties":
                case "openModernElementProperties":
                    ShowModernElementProperties(message.Id);
                    break;
                case "openSceneObjectEvents":
                case "openModernElementEvents":
                    ShowModernElementEvents(message.Id);
                    break;
                case "updateSceneObjectProperties":
                case "updateModernElementProperties":
                    UpdateModernElementProperties(message);
                    break;
                case "updateSceneObjectGeometry":
                case "updateModernElementGeometry":
                    UpdateModernElementGeometry(
                        message.Id,
                        message.X,
                        message.Y,
                        message.Width,
                        message.Height,
                        message.BeforeX,
                        message.BeforeY,
                        message.BeforeWidth,
                        message.BeforeHeight);
                    break;
                case "moveSelectionBy":
                    _ = MoveSelectionByAsync(message);
                    break;
                case "deleteSceneObject":
                case "deleteModernElement":
                    _ = DeleteSelectedSceneObjectsAsync(message.Id);
                    break;
                case "editLegacyText":
                    EditLegacyText(message.Id, message.Text);
                    break;
                case "previewSceneCanvasResize":
                    PreviewActiveSceneCanvasResize(message);
                    break;
                case "resizeSceneCanvas":
                    _ = ResizeActiveSceneCanvasFromPreviewAsync(message);
                    break;
                case "undo":
                    _ = UndoLastSceneOperationAsync();
                    break;
                case "redo":
                    _ = RedoLastSceneOperationAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Message Legacy Viewer invalide: {ex.Message}");
        }
    }

    private void ApplyLegacyInventory(IReadOnlyList<LegacyViewerElementMessage> items)
    {
        MaterializeLegacyElementsFromInventory(items);

        _sourceObjects.Clear();
        if (_activeScene?.LegacyElementsMaterialized == true)
        {
            foreach (var legacyElement in _activeScene.GetLegacyStaticElements())
            {
                var sourceId = legacyElement.LegacySource?.SourceElementId;
                if (string.IsNullOrWhiteSpace(sourceId) || _hiddenSourceObjectIds.Contains(sourceId))
                {
                    continue;
                }

                _sourceObjects.Add(ToLegacyElementListItem(legacyElement));
            }
        }
        else
        {
            foreach (var item in items.Where(item => !string.IsNullOrWhiteSpace(item.Id)))
            {
                var id = item.Id.Trim();
                if (_hiddenSourceObjectIds.Contains(id))
                {
                    continue;
                }

                _sourceObjects.Add(ToLegacyElementListItem(item));
            }
        }

        RefreshSelectionUi();
    }

    private void ApplyLegacySelection(IReadOnlyList<LegacyViewerElementMessage> items)
    {
        _selectedSourceObjectIds.Clear();

        foreach (var item in items.Where(item => !string.IsNullOrWhiteSpace(item.Id)))
        {
            var id = item.Id.Trim();
            if (_hiddenSourceObjectIds.Contains(id))
            {
                continue;
            }

            _selectedSourceObjectIds.Add(id);
        }

        if (_selectedSourceObjectIds.Count > 0)
        {
            _selectedSceneObject = null;
            _selectedSceneObjectIds.Clear();
        }

        RefreshSelectionUi();
        RefreshModernSceneUi();
    }

    private void RefreshSelectionUi()
    {
        var snapshot = CreateSceneElementInventorySnapshot();
        SelectionSummaryText.Text = snapshot.SelectedKeys.Count == 0
            ? "Aucun element selectionne"
            : $"{snapshot.SelectedKeys.Count} element(s) selectionne(s), {_hiddenSourceObjectIds.Count} masque(s)";
        ExtractionSummaryText.Text = $"{snapshot.Elements.Count} element(s) disponible(s), {_extractedCandidateCount} candidat(s) extrait(s)";

        _isUpdatingSceneObjectList = true;
        try
        {
            LegacyElementsListBox.ItemsSource = null;
            LegacyElementsListBox.ItemsSource = snapshot.Elements;
            LegacyElementsListBox.SelectedItems.Clear();
            foreach (var element in snapshot.Elements.Where(element => snapshot.SelectedKeys.Contains(element.Key)))
            {
                LegacyElementsListBox.SelectedItems.Add(element);
            }
        }
        finally
        {
            _isUpdatingSceneObjectList = false;
        }
    }

    private SceneElementInventorySnapshot CreateSceneElementInventorySnapshot()
    {
        var modernElements = CreateModernSceneElementListItems(
            _activeScene?.Elements ?? Array.Empty<ScadaElement>(),
            "",
            0);

        return SceneElementInventorySnapshot.FromElements(
            _sourceObjects,
            modernElements,
            _selectedSourceObjectIds,
            _selectedSceneObject?.Id,
            _activeScene?.GetSuppressedSourceElementIds() ?? _hiddenSourceObjectIds,
            selectedModernElementIds: _selectedSceneObjectIds);
    }

    private static IEnumerable<SceneElementListItem> CreateModernSceneElementListItems(
        IEnumerable<ScadaElement> elements,
        string parentId,
        int depth)
    {
        foreach (var element in elements)
        {
            if (!element.IsLegacyStatic)
            {
                yield return new SceneElementListItem(
                    $"{SceneElementInventorySnapshot.SceneObjectPrefix}{element.Id}",
                    element.Id,
                    $"{element.UserLabel} [{element.Kind}]",
                    element.Kind.ToString(),
                    SceneElementInventorySnapshot.SceneObjectKind,
                    parentId,
                    depth);
            }

            foreach (var child in CreateModernSceneElementListItems(element.ChildElements, element.Id, depth + 1))
            {
                yield return child;
            }
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

    private async void OnHideSelectedClick(object sender, RoutedEventArgs e)
    {
        await HideSelectedLegacyElementsAsync();
    }

    private async void OnDeleteSelectedLegacyClick(object sender, RoutedEventArgs e)
    {
        await DeleteSelectedSceneObjectsAsync();
    }

    private async void OnRestoreHiddenClick(object sender, RoutedEventArgs e)
    {
        await ExecuteLegacyViewerCommandAsync("restoreHidden");
        _hiddenSourceObjectIds.Clear();
        RefreshSelectionUi();
        SetStatus("Elements masques restaures dans la session.");
    }

    private async void OnLegacyElementListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSceneObjectList)
        {
            return;
        }

        var selectedItems = LegacyElementsListBox.SelectedItems
            .OfType<SceneElementListItem>()
            .ToArray();
        var ids = selectedItems
            .Where(element => element.Source == SceneElementInventorySnapshot.SourceObjectKind)
            .Select(element => element.Id)
            .ToArray();
        var selectedModernItems = selectedItems
            .Where(element => element.Source == SceneElementInventorySnapshot.SceneObjectKind)
            .ToArray();

        _selectedSourceObjectIds.Clear();
        foreach (var id in ids)
        {
            _selectedSourceObjectIds.Add(id);
        }

        _selectedSceneObjectIds.Clear();
        foreach (var id in selectedModernItems.Select(element => element.Id))
        {
            _selectedSceneObjectIds.Add(id);
        }

        _selectedSceneObject = selectedModernItems.Length == 0
            ? null
            : _activeScene?.FindElementRecursive(selectedModernItems[^1].Id);
        RefreshSelectionUi();
        RefreshModernSceneUi();
        await SelectLegacyElementsInViewerAsync(ids);
        if (_selectedSceneObjectIds.Count > 0)
        {
            await ExecuteLegacyViewerCommandAsync(new LegacyViewerCommand("selectObject", Ids: _selectedSceneObjectIds.ToArray()));
        }
    }

    private void OnExtractSelectedClick(object sender, RoutedEventArgs e)
    {
        ExtractSelectedLegacyElements();
    }

    private async void OnGroupSelectedClick(object sender, RoutedEventArgs e)
    {
        await GroupSelectedModernElementsAsync();
    }

    private void OnUngroupSelectedModernClick(object sender, RoutedEventArgs e)
    {
        UngroupSelectedModernElement();
    }

    private async void OnConvertSelectedLegacyTextClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement placementTarget)
        {
            await ConvertSelectedLegacyTextToElementPlusAsync(ElementPlusConversionTarget.Text);
            return;
        }

        var menu = new ContextMenu();
        AddConversionMenuItem(menu, "Texte", ElementPlusConversionTarget.Text);
        AddConversionMenuItem(menu, "Affichage numerique", ElementPlusConversionTarget.NumericReadOnly);
        AddConversionMenuItem(menu, "Champ d'entree texte", ElementPlusConversionTarget.TextInput);
        AddConversionMenuItem(menu, "Champ numerique editable", ElementPlusConversionTarget.NumericEditable);
        AddConversionMenuItem(menu, "Bouton", ElementPlusConversionTarget.Button);
        menu.PlacementTarget = placementTarget;
        menu.IsOpen = true;
    }

    private void AddConversionMenuItem(ContextMenu menu, string header, ElementPlusConversionTarget target)
    {
        var item = new MenuItem
        {
            Header = header,
            Tag = target
        };
        item.Click += async (_, _) => await ConvertSelectedLegacyTextToElementPlusAsync(target);
        menu.Items.Add(item);
    }

    private async void OnClearSelectionClick(object sender, RoutedEventArgs e)
    {
        await ClearSelectionAsync();
    }

    private async void OnApplyBackgroundColorClick(object sender, RoutedEventArgs e)
    {
        var color = BackgroundColorTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(color))
        {
            SetStatus("Couleur de fond vide.");
            return;
        }

        if (!TryParseCssColor(color, out var parsedColor))
        {
            SetStatus($"Couleur CSS non reconnue: {color}");
            return;
        }

        color = ToCssHex(parsedColor);
        SetBackgroundColorControls(color);
        if (_activeScene is not null)
        {
            var previousColor = _activeScene.BackgroundColor;
            if (string.Equals(previousColor, color, StringComparison.OrdinalIgnoreCase))
            {
                SetStatus($"Couleur de fond V2 deja appliquee: {color}.");
                return;
            }

            _activeScene = _activeScene.WithBackgroundColor(color);
            _activeSceneTab?.History.Push(new SceneBackgroundChangedAction(_activeScene.Id, previousColor, color));
            MarkActiveSceneDirty();
        }

        await ApplySceneBackgroundColorAsync(color);
        LoadPageProperties(_activeScene);
    }

    private async void OnApplyPagePropertiesClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null)
        {
            SetStatus("Aucune page active.");
            return;
        }

        if (!TryReadPageDimensions(out var width, out var height))
        {
            SetStatus("Dimensions de page invalides. Largeur et hauteur doivent etre au moins 160x120.");
            return;
        }

        var color = BackgroundColorTextBox.Text.Trim();
        if (!TryParseCssColor(color, out var parsedColor))
        {
            SetStatus($"Couleur CSS non reconnue: {color}");
            return;
        }

        var before = _activeScene;
        var background = new SceneBackgroundStyle(
            ToCssHex(parsedColor),
            string.IsNullOrWhiteSpace(BackgroundImageTextBox.Text) ? null : BackgroundImageTextBox.Text.Trim(),
            string.IsNullOrWhiteSpace(BackgroundSizeTextBox.Text) ? "cover" : BackgroundSizeTextBox.Text.Trim(),
            GetComboBoxText(BackgroundRepeatComboBox, "no-repeat"),
            string.IsNullOrWhiteSpace(BackgroundPositionTextBox.Text) ? "center center" : BackgroundPositionTextBox.Text.Trim(),
            GetComboBoxText(BackgroundAttachmentComboBox, "scroll"),
            GetComboBoxText(BackgroundOriginComboBox, "padding-box"),
            GetComboBoxText(BackgroundClipComboBox, "border-box"),
            GetComboBoxText(BackgroundBlendModeComboBox, "normal"));

        var updated = _activeScene
            .WithPageType(GetSelectedPageType())
            .WithIncludeInBuild(IncludeInBuildCheckBox.IsChecked == true)
            .WithPageComposition(GetSelectedCompositionPageId(HeaderPageComboBox), GetSelectedCompositionPageId(FooterPageComboBox))
            .WithCanvasSize(new CanvasSize(width, height))
            .WithBackground(background);
        if (updated.PageType is ScadaPageType.Header or ScadaPageType.Footer)
        {
            updated = updated.WithPageComposition(null, null);
        }

        if (Equals(before, updated))
        {
            SetStatus("Proprietes page deja appliquees.");
            return;
        }

        _activeScene = updated;
        UpdateModernProjectFromActiveScene();
        EnsureHomePageStillValid();
        _activeSceneTab?.History.Push(new SceneSnapshotChangedAction(
            updated.Id,
            before,
            updated,
            "proprietes page"));
        MarkActiveSceneDirty();
        LoadPageProperties(_activeScene);
        await ApplySceneBackgroundColorAsync(_activeScene.BackgroundColor, updateStatus: false);
        await ApplySceneCanvasSizeAsync(_activeScene.CanvasSize);
        await RenderModernSceneAsync();
        SetStatus($"Proprietes page appliquees: {width}x{height}, {GetPageTypeLabel(_activeScene.PageType)}. Sauvegarde requise.");
    }

    private async Task ResizeActiveSceneCanvasFromPreviewAsync(LegacyViewerMessage message)
    {
        if (_activeScene is null)
        {
            SetStatus("Aucune scene active pour redimensionner la workzone.");
            return;
        }

        await ApplyActiveSceneCanvasSizeAsync(
            Math.Max(160, (int)Math.Round(message.Width)),
            Math.Max(120, (int)Math.Round(message.Height)),
            "redimensionnement workzone",
            _activeScene,
            "Workzone redimensionnee");
    }

    private void PreviewActiveSceneCanvasResize(LegacyViewerMessage message)
    {
        if (_activeScene is null)
        {
            return;
        }

        var width = Math.Max(160, (int)Math.Round(message.Width));
        var height = Math.Max(120, (int)Math.Round(message.Height));
        SetPageDimensionFields(width, height);
        SetStatus($"Workzone: {width}x{height}");
    }

    private void OnPageDimensionTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingPagePropertyControls || !ArePagePropertyControlsReady() || _activeScene is null)
        {
            return;
        }

        _pageDimensionEditBeforeScene ??= _activeScene;
        _pageDimensionApplyTimer.Stop();
        _pageDimensionApplyTimer.Start();
    }

    private async void OnPageDimensionApplyTimerTick(object? sender, EventArgs e)
    {
        _pageDimensionApplyTimer.Stop();
        if (_activeScene is null)
        {
            _pageDimensionEditBeforeScene = null;
            return;
        }

        if (!TryReadPageDimensions(out var width, out var height))
        {
            SetStatus("Dimensions de page invalides. Largeur et hauteur doivent etre au moins 160x120.");
            return;
        }

        var before = _pageDimensionEditBeforeScene ?? _activeScene;
        _pageDimensionEditBeforeScene = null;
        await ApplyActiveSceneCanvasSizeAsync(width, height, "dimensions page", before, "Dimensions page appliquees");
    }

    private async Task ApplyActiveSceneCanvasSizeAsync(
        int width,
        int height,
        string historyLabel,
        ScadaScene before,
        string statusPrefix)
    {
        if (_activeScene is null)
        {
            return;
        }

        var updated = _activeScene.WithCanvasSize(new CanvasSize(width, height));
        if (Equals(before, updated))
        {
            return;
        }

        _activeScene = updated;
        _activeSceneTab?.History.Push(new SceneSnapshotChangedAction(
            updated.Id,
            before,
            updated,
            historyLabel));
        MarkActiveSceneDirty();
        LoadPageProperties(_activeScene);
        await ApplySceneCanvasSizeAsync(_activeScene.CanvasSize);
        await RenderModernSceneAsync();
        SetStatus($"{statusPrefix}: {width}x{height}. Sauvegarde requise.");
    }

    private void OnPageTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPagePropertyControls || !ArePagePropertyControlsReady() || _activeScene is null)
        {
            return;
        }

        ApplyActiveScenePageType(GetSelectedPageType());
    }

    private void ApplyActiveScenePageType(ScadaPageType pageType)
    {
        if (_activeScene is null || _activeScene.PageType == pageType)
        {
            return;
        }

        var before = _activeScene;
        var updated = _activeScene.WithPageType(pageType);
        if (updated.PageType is ScadaPageType.Header or ScadaPageType.Footer)
        {
            updated = updated.WithPageComposition(null, null);
        }

        _activeScene = updated;
        UpdateModernProjectFromActiveScene();
        EnsureHomePageStillValid();
        _activeSceneTab?.History.Push(new SceneSnapshotChangedAction(
            updated.Id,
            before,
            updated,
            "type page"));
        MarkActiveSceneDirty();
        LoadPageProperties(_activeScene);
        RefreshModernSceneUi();
        SetStatus($"Type de page applique: {GetPageTypeLabel(pageType)}. Sauvegarde requise.");
    }

    private void OnIncludeInBuildClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingPagePropertyControls || _activeScene is null)
        {
            return;
        }

        var before = _activeScene;
        var updated = _activeScene.WithIncludeInBuild(IncludeInBuildCheckBox.IsChecked == true);
        if (Equals(before, updated))
        {
            return;
        }

        _activeScene = updated;
        UpdateModernProjectFromActiveScene();
        EnsureHomePageStillValid();
        _activeSceneTab?.History.Push(new SceneSnapshotChangedAction(updated.Id, before, updated, "compilation page"));
        MarkActiveSceneDirty();
        LoadPageProperties(_activeScene);
        SetStatus($"Compilation page {(updated.IncludeInBuild ? "activee" : "desactivee")}: {updated.Id}. Sauvegarde requise.");
    }

    private void OnHomePageClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingPagePropertyControls || _activeScene is null)
        {
            return;
        }

        if (HomePageCheckBox.IsChecked == true)
        {
            if (_activeScene.PageType != ScadaPageType.Default || !_activeScene.IncludeInBuild)
            {
                SetStatus("La page d'accueil doit etre une page Defaut compilee.");
                LoadPageProperties(_activeScene);
                return;
            }

            SetHomePageId(_activeScene.Id);
            MarkActiveSceneDirty();
            LoadPageProperties(_activeScene);
            SetStatus($"Page d'accueil definie: {_activeScene.Id}. Sauvegarde requise.");
            return;
        }

        if (_modernProject is not null && string.Equals(_modernProject.HomePageId, _activeScene.Id, StringComparison.Ordinal))
        {
            SetHomePageId(null);
            MarkActiveSceneDirty();
            LoadPageProperties(_activeScene);
            SetStatus("Page d'accueil retiree. Fallback: premiere page Defaut compilee.");
        }
    }

    private void OnPageCompositionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPagePropertyControls || _activeScene is null)
        {
            return;
        }

        var before = _activeScene;
        var updated = _activeScene.WithPageComposition(
            GetSelectedCompositionPageId(HeaderPageComboBox),
            GetSelectedCompositionPageId(FooterPageComboBox));
        if (updated.PageType is ScadaPageType.Header or ScadaPageType.Footer)
        {
            updated = updated.WithPageComposition(null, null);
        }

        if (Equals(before, updated))
        {
            return;
        }

        _activeScene = updated;
        UpdateModernProjectFromActiveScene();
        _activeSceneTab?.History.Push(new SceneSnapshotChangedAction(updated.Id, before, updated, "composition page"));
        MarkActiveSceneDirty();
        LoadPageProperties(_activeScene);
        SetStatus($"Composition page appliquee: {updated.Id}. Sauvegarde requise.");
    }

    private void OnBackgroundColorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingBackgroundColorControls || !AreBackgroundColorControlsReady())
        {
            return;
        }

        if (TryParseCssColor(BackgroundColorTextBox.Text.Trim(), out var color))
        {
            UpdateBackgroundColorControls(color, updateText: false);
        }
    }

    private void OnBackgroundSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingBackgroundColorControls || !AreBackgroundColorControlsReady())
        {
            return;
        }

        var color = Color.FromRgb(
            (byte)Math.Round(BackgroundRedSlider.Value),
            (byte)Math.Round(BackgroundGreenSlider.Value),
            (byte)Math.Round(BackgroundBlueSlider.Value));
        UpdateBackgroundColorControls(color, updateText: true);
    }

    private void OnBackgroundHueSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingBackgroundColorControls || !AreBackgroundColorControlsReady())
        {
            return;
        }

        _backgroundHue = BackgroundHueSlider.Value;
        UpdateBackgroundColorControls(FromHsv(_backgroundHue, _backgroundSaturation, _backgroundValue), updateText: true);
    }

    private void OnSaturationValuePickerMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!AreBackgroundColorControlsReady())
        {
            return;
        }

        _isDraggingBackgroundColorPicker = true;
        SaturationValuePicker.CaptureMouse();
        UpdateSaturationValueFromPoint(e.GetPosition(SaturationValuePicker));
    }

    private void OnSaturationValuePickerMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingBackgroundColorPicker || !AreBackgroundColorControlsReady())
        {
            return;
        }

        UpdateSaturationValueFromPoint(e.GetPosition(SaturationValuePicker));
    }

    private void OnSaturationValuePickerMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingBackgroundColorPicker)
        {
            return;
        }

        _isDraggingBackgroundColorPicker = false;
        SaturationValuePicker.ReleaseMouseCapture();
        UpdateSaturationValueFromPoint(e.GetPosition(SaturationValuePicker));
    }

    private void OnSaturationValuePickerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (AreBackgroundColorControlsReady())
        {
            UpdateSaturationValueSelector();
        }
    }

    private void OnBackgroundSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string color })
        {
            SetBackgroundColorControls(color);
        }
    }

    private async Task HideSelectedLegacyElementsAsync()
    {
        if (_selectedSourceObjectIds.Count == 0)
        {
            SetStatus("Aucun element legacy a masquer.");
            return;
        }

        var maskedIds = _selectedSourceObjectIds.ToArray();
        foreach (var id in maskedIds)
        {
            _hiddenSourceObjectIds.Add(id);
        }

        await HideLegacyElementsInViewerAsync(maskedIds);
        _selectedSourceObjectIds.Clear();
        SetStatus($"{maskedIds.Length} element(s) masque(s) dans la vue de travail.");
        RefreshSelectionUi();
        RefreshModernSceneUi();
    }

    private async Task DeleteSelectedLegacyElementsAsync()
    {
        await DeleteSelectedSceneObjectsAsync();
    }

    private async Task DeleteSelectedSceneObjectsAsync(string? fallbackElementId = null)
    {
        if (_activeScene is null)
        {
            SetStatus("Aucune scene active pour supprimer la selection.");
            return;
        }

        var selectedSourceIds = _selectedSourceObjectIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var selectedElements = ResolveSelectedSceneObjects(fallbackElementId);
        if (selectedElements.Count == 0 && selectedSourceIds.Length == 0)
        {
            SetStatus("Aucun objet selectionne a supprimer.");
            return;
        }

        var deletedElements = selectedElements
            .Where(element => !selectedElements.Any(candidate =>
                !string.Equals(candidate.Id, element.Id, StringComparison.Ordinal) &&
                ContainsElement(candidate, element.Id)))
            .ToArray();

        var materializedSourceIds = deletedElements
            .Select(element => element.LegacySource?.SourceElementId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToArray();
        var sourceIds = materializedSourceIds
            .Concat(selectedSourceIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var deletedSnapshots = deletedElements
            .Select(element => new DeletedSceneObjectSnapshot(
                element,
                _activeScene.FindParentOf(element.Id)?.Id,
                GetSiblingIndex(_activeScene, element)))
            .ToArray();

        _activeScene = _activeScene
            .WithoutSceneObjects(deletedElements.Select(element => element.Id))
            .WithRemovedSourceElementIds(sourceIds);

        foreach (var sourceId in sourceIds)
        {
            _hiddenSourceObjectIds.Add(sourceId);
        }

        RemoveLegacyElementsFromInventory(sourceIds);
        await RemoveLegacyElementsInViewerAsync(sourceIds);
        _selectedSourceObjectIds.Clear();
        _selectedSceneObject = null;
        _selectedSceneObjectIds.Clear();
        _activeSceneTab?.History.Push(new SceneObjectsDeletedAction(_activeScene.Id, deletedSnapshots, sourceIds));
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        await RenderModernSceneAsync();
        var deletedCount = deletedElements.Length + sourceIds.Except(materializedSourceIds, StringComparer.Ordinal).Count();
        SetStatus($"{deletedCount} objet(s) supprime(s) de la scene active. Undo disponible. Sauvegarde requise.");
    }

    private IReadOnlyList<ScadaElement> ResolveSelectedSceneObjects(string? fallbackElementId = null)
    {
        if (_activeScene is null)
        {
            return Array.Empty<ScadaElement>();
        }

        var selected = new List<ScadaElement>();
        foreach (var sourceId in _selectedSourceObjectIds)
        {
            var element = _activeScene.FindLegacyStaticBySourceElementId(sourceId);
            if (element is not null)
            {
                selected.Add(element);
            }
        }

        var elementIds = _selectedSceneObjectIds.ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(fallbackElementId))
        {
            elementIds.Add(fallbackElementId);
        }

        foreach (var elementId in elementIds)
        {
            var element = _activeScene.FindElementRecursive(elementId);
            if (element is not null)
            {
                selected.Add(element);
            }
        }

        return selected
            .GroupBy(element => element.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static int GetSiblingIndex(ScadaScene scene, ScadaElement element)
    {
        var parent = scene.FindParentOf(element.Id);
        var siblings = parent is null ? scene.Elements : parent.ChildElements;
        for (var index = 0; index < siblings.Count; index++)
        {
            if (string.Equals(siblings[index].Id, element.Id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return siblings.Count;
    }

    private void ExtractSelectedLegacyElements()
    {
        if (_selectedSourceObjectIds.Count == 0)
        {
            SetStatus("Aucun element legacy a extraire.");
            return;
        }

        _extractedCandidateCount++;
        SetStatus($"Candidat V2 cree depuis {_selectedSourceObjectIds.Count} element(s) legacy. Groupement manuel a faire dans V2.");
        RefreshSelectionUi();
    }

    private async Task ConvertSelectedLegacyTextToElementPlusAsync(ElementPlusConversionTarget target)
    {
        if (_activeScene is null)
        {
            SetStatus("Aucune scene V2 active pour convertir en Element+.");
            return;
        }

        if (_selectedSourceObjectIds.Count == 0)
        {
            SetStatus("Aucun element legacy selectionne pour Conversion Element+.");
            return;
        }

        var selectedLegacy = _sourceObjects
            .Where(element => _selectedSourceObjectIds.Contains(element.Id))
            .ToArray();
        var conversionCandidates = selectedLegacy
            .Where(element => ElementPlusLegacyConverter.CanConvert(ToLegacyDetectedObject(element), target))
            .ToArray();

        if (conversionCandidates.Length == 0)
        {
            SetStatus($"Aucun element legacy convertible vers {GetConversionTargetLabel(target)} dans la selection.");
            return;
        }

        var convertedElements = conversionCandidates
            .Select(element => CreateElementPlusFromLegacy(element, target))
            .ToArray();

        var convertedLegacyIds = conversionCandidates
            .Select(element => element.Id)
            .ToArray();
        var sourceSceneElements = convertedLegacyIds
            .Select(id => _activeScene.FindLegacyStaticBySourceElementId(id))
            .OfType<ScadaElement>()
            .ToArray();

        foreach (var convertedElement in convertedElements)
        {
            _activeScene = _activeScene.WithCommittedElementPlusConversion(convertedElement);
            if (!string.IsNullOrWhiteSpace(convertedElement.LegacySource?.SourceElementId))
            {
                _hiddenSourceObjectIds.Add(convertedElement.LegacySource.SourceElementId);
            }
        }

        var sourceSnapshots = conversionCandidates.Select(ToLegacyDetectedObject).ToArray();
        var convertedSnapshots = convertedElements.ToArray();
        var sourceElementSnapshots = sourceSceneElements.ToArray();
        var targetLabel = GetConversionTargetLabel(target);
        _activeSceneTab?.History.Push(new DelegateEditorHistoryAction(
            _activeScene.Id,
            $"Conversion Element+ {targetLabel}",
            _ => UndoLegacyConversionAsync(sourceSnapshots, convertedSnapshots, sourceElementSnapshots),
            _ => RedoLegacyConversionAsync(sourceSnapshots, convertedSnapshots, targetLabel)));

        RemoveLegacyElementsFromInventory(convertedLegacyIds);
        await RemoveLegacyElementsInViewerAsync(convertedLegacyIds);
        _selectedSceneObject = convertedElements[^1];
        _selectedSceneObjectIds.Clear();
        _selectedSceneObjectIds.Add(_selectedSceneObject.Id);
        _selectedSourceObjectIds.Clear();
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        await RenderModernSceneAsync();

        var skippedCount = selectedLegacy.Length - conversionCandidates.Length;
        var skippedMessage = skippedCount > 0 ? $" {skippedCount} element(s) ignore(s): conversion non plausible vers {GetConversionTargetLabel(target)}." : "";
        SetStatus($"{convertedElements.Length} element(s) legacy converti(s) en Element+ ({GetConversionTargetLabel(target)}). Undo disponible jusqu'a fermeture.{skippedMessage}");
    }

    // Scene grouping is Element+ only; legacy source nodes must be converted before they can become group children.
    private async Task GroupSelectedModernElementsAsync()
    {
        if (_activeScene is null)
        {
            SetStatus("Aucune scene V2 active pour grouper.");
            return;
        }

        if (_selectedSourceObjectIds.Count > 0)
        {
            WarnLegacyGroupingRequiresConversion();
            return;
        }

        var selectedModernIds = _selectedSceneObjectIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (selectedModernIds.Length < 2)
        {
            SetStatus("Selectionnez au moins deux Element+ pour grouper.");
            return;
        }

        var sequence = _nextGroupSequence++;
        var groupId = CreateUniqueElementId($"group_{sequence:000}");
        var groupName = $"Group{sequence:000}";
        var beforeScene = _activeScene;
        try
        {
            _activeScene = _activeScene.WithGroupedElements(groupId, groupName, selectedModernIds);
        }
        catch (InvalidOperationException ex)
        {
            SetStatus(ex.Message);
            return;
        }

        _activeSceneTab?.History.Push(new SceneSnapshotChangedAction(
            beforeScene.Id,
            beforeScene,
            _activeScene,
            "groupement Element+"));
        var group = _activeScene.FindElementRecursive(groupId);
        _selectedSceneObject = group;
        _selectedSceneObjectIds.Clear();
        _selectedSceneObjectIds.Add(groupId);
        _selectedSourceObjectIds.Clear();
        await ExecuteLegacyViewerCommandAsync("clearSelection");
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        await RenderModernSceneAsync();
        SetStatus($"{selectedModernIds.Length} Element+ groupe(s) dans {group?.DisplayName ?? groupName}. Sauvegarde requise.");
    }

    private Task GroupSelectedLegacyElementsAsync()
    {
        WarnLegacyGroupingRequiresConversion();
        return Task.CompletedTask;
    }

    private void WarnLegacyGroupingRequiresConversion()
    {
        SetStatus("Le groupement direct des elements legacy est decommissionne. Convertissez d'abord les elements en Element+, puis relancez Grouper.");
    }

    private async Task OpenSelectedLegacyInElementStudioAsync()
    {
        if (_repositoryRoot is null || _activeScene is null || _activeReferencePage is null)
        {
            SetStatus("Studio Element+ indisponible: aucun projet ou scene active.");
            return;
        }

        var selectedLegacy = await CaptureSelectedLegacyElementsForStudioAsync();
        if (selectedLegacy.Length == 0)
        {
            SetStatus("Selectionnez au moins un element legacy pour ouvrir Studio Element+.");
            return;
        }

        try
        {
            var package = CreateElementStudioImportPackage(selectedLegacy);
            var projectsRoot = Path.Combine(_repositoryRoot, "SCADA_BUILDER_V2", "projects");
            var packagePath = await _elementStudioPackageWriter.WriteToProjectAsync(package, projectsRoot);
            var launch = await TryLaunchElementStudioAsync(packagePath);
            AppendElementStudioLaunchLog(packagePath, launch);
            SetStatus(launch.Launched
                ? $"Studio Element+ ouvert avec {selectedLegacy.Length} element(s): {Path.GetFileName(packagePath)}"
                : $"Package Studio Element+ cree: {packagePath}. {launch.Message}");
        }
        catch (Exception ex)
        {
            SetStatus($"Erreur ouverture Studio Element+: {ex.Message}");
        }
    }

    private async Task<LegacyElementListItem[]> CaptureSelectedLegacyElementsForStudioAsync()
    {
        if (PreviewWebView.CoreWebView2 is null)
        {
            return GetSelectedLegacyElements();
        }

        try
        {
            var scriptResult = await PreviewWebView.ExecuteScriptAsync(
                "window.scadaSceneEditor && window.scadaSceneEditor.getSelectedMessagesForStudio ? window.scadaSceneEditor.getSelectedMessagesForStudio() : [];");
            var messages = DeserializeScriptResult<List<LegacyViewerElementMessage>>(scriptResult);
            if (messages is { Count: > 0 })
            {
                return messages
                    .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !_hiddenSourceObjectIds.Contains(item.Id.Trim()))
                    .Select(ToLegacyElementListItem)
                    .ToArray();
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Capture Studio Element+ degradee: {ex.Message}");
        }

        return GetSelectedLegacyElements();
    }

    private static T? DeserializeScriptResult<T>(string? scriptResult)
    {
        if (string.IsNullOrWhiteSpace(scriptResult) ||
            string.Equals(scriptResult, "null", StringComparison.OrdinalIgnoreCase))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(scriptResult, WebMessageJsonOptions);
        }
        catch (JsonException)
        {
            var unwrapped = JsonSerializer.Deserialize<string>(scriptResult, WebMessageJsonOptions);
            return string.IsNullOrWhiteSpace(unwrapped)
                ? default
                : JsonSerializer.Deserialize<T>(unwrapped, WebMessageJsonOptions);
        }
    }

    private LegacyElementListItem[] GetSelectedLegacyElements()
    {
        return _sourceObjects
            .Where(element => _selectedSourceObjectIds.Contains(element.Id))
            .ToArray();
    }

    private ElementStudioImportPackage CreateElementStudioImportPackage(IReadOnlyList<LegacyElementListItem> selectedLegacy)
    {
        var packageId = $"studio_{_activeScene?.Id ?? "scene"}_{DateTimeOffset.Now:yyyyMMdd_HHmmss}";
        var version = LoadVersionText();
        var items = selectedLegacy
            .Select((element, index) => new ElementStudioLegacyItem(
                element.Id,
                element.DisplayName,
                element.ElementType,
                new SceneBounds(
                    element.X,
                    element.Y,
                    Math.Max(1, element.Width),
                    Math.Max(1, element.Height)),
                new SceneBounds(0, 0, 0, 0),
                null,
                string.IsNullOrWhiteSpace(element.LegacyMarkup) ? null : element.LegacyMarkup,
                element.Text,
                new ElementStudioStyleSnapshot(
                    string.IsNullOrWhiteSpace(element.FontFamily) ? "Segoe UI" : element.FontFamily,
                    element.FontSize > 0 ? element.FontSize : 12,
                    string.IsNullOrWhiteSpace(element.Foreground) ? "#000000" : element.Foreground,
                    string.IsNullOrWhiteSpace(element.Background) ? "Transparent" : element.Background,
                    string.IsNullOrWhiteSpace(element.Foreground) ? "Transparent" : element.Foreground,
                    0,
                    "None",
                    1),
                element.ZIndex,
                string.IsNullOrWhiteSpace(element.RawMetadataJson) ? null : element.RawMetadataJson))
            .ToArray();

        return ElementStudioImportPackageFactory.Create(
            packageId,
            "AMR_REF_SCADA_V2",
            _activeScene?.Id ?? "",
            _activeReferencePage?.AbsolutePath ?? "",
            items,
            ElementStudioPackageMetadata.Current(version),
            ResolveElementPlusLibraryRoot(create: true));
    }

    private void AppendElementStudioLaunchLog(string packagePath, ElementStudioLaunchResult launch)
    {
        if (_repositoryRoot is null)
        {
            return;
        }

        try
        {
            var logPath = ResolveElementStudioLaunchLogPath();
            if (logPath is null)
            {
                return;
            }

            var logDirectory = Path.GetDirectoryName(logPath);
            if (string.IsNullOrWhiteSpace(logDirectory))
            {
                return;
            }

            Directory.CreateDirectory(logDirectory);
            var line =
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\tLaunched={launch.Launched}\tMessage={launch.Message}\tPackage={packagePath}{Environment.NewLine}";
            File.AppendAllText(logPath, line);
        }
        catch
        {
            // Launch logging is diagnostic only; it must never block the editor workflow.
        }
    }

    private async Task<ElementStudioLaunchResult> TryLaunchElementStudioAsync(string packagePath)
    {
        var attempts = new List<string>();
        var studioExePath = ResolveElementStudioExecutablePath();
        if (studioExePath is not null)
        {
            var exeLaunch = await LaunchElementStudioExecutableAsync(studioExePath, packagePath);
            if (exeLaunch.Launched)
            {
                return exeLaunch;
            }

            attempts.Add($"exe: {exeLaunch.Message}");
        }
        else
        {
            attempts.Add("exe: introuvable");
        }

        var studioProjectPath = ResolveElementStudioProjectPath();
        if (studioProjectPath is null)
        {
            attempts.Add("dotnet run: projet introuvable");
            return new ElementStudioLaunchResult(false, string.Join(" | ", attempts));
        }

        var projectLaunch = await LaunchElementStudioProjectAsync(studioProjectPath, packagePath);
        if (projectLaunch.Launched)
        {
            return projectLaunch;
        }

        attempts.Add($"dotnet run: {projectLaunch.Message}");
        return new ElementStudioLaunchResult(false, string.Join(" | ", attempts));
    }

    private static async Task<ElementStudioLaunchResult> LaunchElementStudioExecutableAsync(string studioExePath, string packagePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = studioExePath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal,
            WorkingDirectory = Path.GetDirectoryName(studioExePath) ?? ""
        };
        startInfo.ArgumentList.Add(packagePath);

        var process = Process.Start(startInfo);
        if (process is null)
        {
            return new ElementStudioLaunchResult(false, $"Impossible de lancer {studioExePath}.");
        }

        var visibleProcess = await WaitForStudioWindowAsync(process, TimeSpan.FromSeconds(8));
        if (visibleProcess is null)
        {
            var message = process.HasExited
                ? $"Studio Element+ a quitte immediatement (code {process.ExitCode})."
                : "Studio Element+ demarre, mais aucune fenetre WPF visible n'a ete detectee.";
            process.Dispose();
            return new ElementStudioLaunchResult(false, message);
        }

        BringProcessWindowToFront(visibleProcess);
        var messagePath = visibleProcess.Id == process.Id
            ? studioExePath
            : $"{studioExePath} (window process {visibleProcess.Id})";
        visibleProcess.Dispose();
        process.Dispose();
        return new ElementStudioLaunchResult(true, messagePath);
    }

    private static async Task<ElementStudioLaunchResult> LaunchElementStudioProjectAsync(string studioProjectPath, string packagePath)
    {
        var build = await BuildElementStudioProjectAsync(studioProjectPath);
        if (!build.Launched)
        {
            return build;
        }

        var dotnetStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(studioProjectPath) ?? ""
        };
        dotnetStartInfo.ArgumentList.Add("run");
        dotnetStartInfo.ArgumentList.Add("--no-build");
        dotnetStartInfo.ArgumentList.Add("--project");
        dotnetStartInfo.ArgumentList.Add(studioProjectPath);
        dotnetStartInfo.ArgumentList.Add("--");
        dotnetStartInfo.ArgumentList.Add(packagePath);

        var process = Process.Start(dotnetStartInfo);
        if (process is null)
        {
            return new ElementStudioLaunchResult(false, $"Impossible de lancer dotnet run pour {studioProjectPath}.");
        }

        var visibleProcess = await WaitForStudioWindowAsync(process, TimeSpan.FromSeconds(12));
        if (visibleProcess is null)
        {
            var message = process.HasExited
                ? $"Studio Element+ via dotnet run a quitte immediatement (code {process.ExitCode})."
                : "Studio Element+ via dotnet run reste actif, mais aucune fenetre WPF visible n'a ete detectee.";
            TryKillProcess(process);
            process.Dispose();
            return new ElementStudioLaunchResult(false, message);
        }

        BringProcessWindowToFront(visibleProcess);
        var messagePath = $"{studioProjectPath} (window process {visibleProcess.Id})";
        visibleProcess.Dispose();
        process.Dispose();
        return new ElementStudioLaunchResult(true, messagePath);
    }

    private static async Task<ElementStudioLaunchResult> BuildElementStudioProjectAsync(string studioProjectPath)
    {
        var buildStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(studioProjectPath) ?? ""
        };
        buildStartInfo.ArgumentList.Add("build");
        buildStartInfo.ArgumentList.Add(studioProjectPath);
        buildStartInfo.ArgumentList.Add("--nologo");

        var process = Process.Start(buildStartInfo);
        if (process is null)
        {
            return new ElementStudioLaunchResult(false, $"Impossible de compiler Studio Element+ pour {studioProjectPath}.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        var exitCode = process.ExitCode;
        process.Dispose();

        if (exitCode == 0)
        {
            return new ElementStudioLaunchResult(true, studioProjectPath);
        }

        var details = string.Join(" ", new[] { output, error }
            .Where(text => !string.IsNullOrWhiteSpace(text)))
            .ReplaceLineEndings(" ")
            .Trim();
        if (details.Length > 260)
        {
            details = details[^260..];
        }

        return new ElementStudioLaunchResult(
            false,
            string.IsNullOrWhiteSpace(details)
                ? $"Compilation Studio Element+ echouee (code {exitCode})."
                : $"Compilation Studio Element+ echouee (code {exitCode}): {details}");
    }

    private static async Task<Process?> WaitForStudioWindowAsync(Process process, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.Now + timeout;
        while (DateTimeOffset.Now < deadline)
        {
            process.Refresh();
            if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero)
            {
                return process;
            }

            var studioProcess = FindVisibleStudioProcess();
            if (studioProcess is not null)
            {
                return studioProcess;
            }

            await Task.Delay(150);
        }

        return null;
    }

    private static Process? FindVisibleStudioProcess()
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.MainWindowHandle == IntPtr.Zero)
                {
                    process.Dispose();
                    continue;
                }

                if (string.Equals(process.ProcessName, "ScadaBuilderV2.ElementStudio.App", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(process.MainWindowTitle, "Studio Element+", StringComparison.OrdinalIgnoreCase))
                {
                    return process;
                }
            }
            catch
            {
                process.Dispose();
            }
        }

        return null;
    }

    private static void BringProcessWindowToFront(Process process)
    {
        try
        {
            process.Refresh();
            if (process.MainWindowHandle == IntPtr.Zero)
            {
                return;
            }

            ShowWindow(process.MainWindowHandle, ShowWindowRestore);
            SetForegroundWindow(process.MainWindowHandle);
        }
        catch
        {
            // Best-effort UI activation only.
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private string? ResolveElementStudioExecutablePath()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "ScadaBuilderV2.ElementStudio.App.exe"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private string? ResolveElementStudioProjectPath()
    {
        var sourceRoot = ResolveElementStudioSourceRoot();
        if (sourceRoot is null)
        {
            return null;
        }

        var projectPath = Path.Combine(sourceRoot, "ScadaBuilderV2.ElementStudio.App.csproj");
        return File.Exists(projectPath) ? projectPath : null;
    }

    private string? ResolveElementStudioSourceRoot()
    {
        if (_repositoryRoot is not null)
        {
            var fromRepositoryRoot = Path.Combine(
                _repositoryRoot,
                "SCADA_BUILDER_V2",
                "src",
                "ScadaBuilderV2.ElementStudio.App");
            if (Directory.Exists(fromRepositoryRoot))
            {
                return fromRepositoryRoot;
            }
        }

        var fromBaseDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "ScadaBuilderV2.ElementStudio.App"));
        return Directory.Exists(fromBaseDirectory) ? fromBaseDirectory : null;
    }

    private void UngroupSelectedModernElement()
    {
        if (_activeScene is null || _selectedSceneObject is null)
        {
            SetStatus("Aucun groupe V2 selectionne a degrouper.");
            return;
        }

        if (_selectedSceneObject.Kind != ScadaElementKind.Group)
        {
            SetStatus("L'objet selectionne n'est pas un groupe.");
            return;
        }

        var groupId = _selectedSceneObject.Id;
        var beforeScene = _activeScene;
        _activeScene = _activeScene.WithUngroupedElement(groupId);
        if (!Equals(beforeScene, _activeScene))
        {
            _activeSceneTab?.History.Push(new SceneSnapshotChangedAction(
                beforeScene.Id,
                beforeScene,
                _activeScene,
                "degroupement Element+"));
        }

        _selectedSceneObject = null;
        _selectedSceneObjectIds.Clear();
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        _ = RenderModernSceneAsync();
        SetStatus($"{groupId} degroupe. Les enfants conservent leur position visuelle.");
    }

    private void RemoveLegacyElementsFromInventory(IReadOnlyCollection<string> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var idsToRemove = ids.ToHashSet(StringComparer.Ordinal);
        _sourceObjects.RemoveAll(element => idsToRemove.Contains(element.Id));
    }

    private void MaterializeLegacyElementsFromInventory(IReadOnlyList<LegacyViewerElementMessage> items)
    {
        if (_activeScene is null || _activeScene.LegacyElementsMaterialized || items.Count == 0)
        {
            return;
        }

        var suppressedSourceIds = _activeScene.GetSuppressedSourceElementIds();
        foreach (var item in items.Where(item => !string.IsNullOrWhiteSpace(item.Id)))
        {
            var id = item.Id.Trim();
            if (_hiddenSourceObjectIds.Contains(id) ||
                suppressedSourceIds.Contains(id) ||
                _activeScene.FindLegacyStaticBySourceElementId(id) is not null)
            {
                continue;
            }

            _activeScene = _activeScene.WithElement(CreateLegacyStaticElement(ToLegacyElementListItem(item)));
        }

        _activeScene = _activeScene.WithLegacyElementsMaterialized();
    }

    private ScadaElement CreateLegacyStaticElement(LegacyElementListItem legacy)
    {
        var sourceDocumentId = _activeReferencePage?.Id ?? _activeScene?.Id ?? "";
        var sourcePath = PreviewSourceText.Text == "-" ? null : PreviewSourceText.Text;
        var source = new LegacySourceTrace(
            "Wonderware/ArchestrA",
            sourceDocumentId,
            legacy.Id,
            legacy.DisplayName,
            sourcePath);
        var payload = new LegacyElementPayload(
            legacy.ElementType,
            legacy.Text,
            legacy.IsTextLike,
            legacy.FontFamily,
            legacy.FontSize,
            legacy.Foreground,
            legacy.Background,
            string.IsNullOrWhiteSpace(legacy.LegacyMarkup) ? null : legacy.LegacyMarkup,
            string.IsNullOrWhiteSpace(legacy.RawMetadataJson) ? null : legacy.RawMetadataJson);

        return ScadaElement.CreateLegacyStatic(
            CreateUniqueElementId($"legacy_{SanitizeElementIdPart(legacy.Id)}"),
            legacy.DisplayName,
            new SceneBounds(
                legacy.X,
                legacy.Y,
                Math.Max(1, legacy.Width),
                Math.Max(1, legacy.Height)),
            source,
            payload);
    }

    private ScadaElement CreateElementPlusFromLegacy(LegacyElementListItem legacy, ElementPlusConversionTarget target)
    {
        var idPrefix = target switch
        {
            ElementPlusConversionTarget.Text => "elementplus_text",
            ElementPlusConversionTarget.TextInput => "elementplus_input_text",
            ElementPlusConversionTarget.NumericReadOnly => "elementplus_numeric_display",
            ElementPlusConversionTarget.NumericEditable => "elementplus_numeric",
            ElementPlusConversionTarget.Button => "elementplus_button",
            _ => "elementplus"
        };
        var id = CreateUniqueElementId($"{idPrefix}_{SanitizeElementIdPart(legacy.Id)}");
        var displayName = string.IsNullOrWhiteSpace(legacy.DisplayName)
            ? $"Element+ {id}"
            : $"Element+ {legacy.DisplayName}";
        var sourceDocumentId = _activeReferencePage?.Id ?? _activeScene?.Id ?? "";
        var sourcePath = PreviewSourceText.Text == "-" ? null : PreviewSourceText.Text;

        return ElementPlusLegacyConverter.Convert(
            ToLegacyDetectedObject(legacy),
            target,
            new ElementPlusConversionOptions(id, displayName, "Wonderware/ArchestrA", sourceDocumentId, sourcePath));
    }

    private void RestoreLegacyElementInInventory(LegacyDetectedObject legacy)
    {
        RemoveLegacyElementsFromInventory([legacy.RuntimeId]);
        _sourceObjects.Add(ToLegacyElementListItem(legacy));
    }

    private string CreateUniqueElementId(string baseId)
    {
        if (_activeScene is null)
        {
            return baseId;
        }

        var existingIds = FlattenElements(_activeScene.Elements).Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
        if (!existingIds.Contains(baseId))
        {
            return baseId;
        }

        var index = 2;
        string candidate;
        do
        {
            candidate = $"{baseId}_{index}";
            index++;
        }
        while (existingIds.Contains(candidate));

        return candidate;
    }

    private async Task ClearSelectionAsync()
    {
        await ExecuteLegacyViewerCommandAsync("clearSelection");
        ClearSelection();
        SetStatus("Selection effacee.");
    }

    private void ClearSelection()
    {
        _selectedSourceObjectIds.Clear();
        _selectedSceneObject = null;
        _selectedSceneObjectIds.Clear();
        RefreshSelectionUi();
        RefreshModernSceneUi();
    }

    private void ClearModernSelection()
    {
        _selectedSceneObject = null;
        _selectedSceneObjectIds.Clear();
        RefreshSelectionUi();
        RefreshModernSceneUi();
    }

    private async Task ExecuteLegacyViewerCommandAsync(string command)
    {
        if (PreviewWebView.CoreWebView2 is null)
        {
            return;
        }

        var commandJson = JsonSerializer.Serialize(command);
        await PreviewWebView.ExecuteScriptAsync($"window.scadaSceneEditor && window.scadaSceneEditor.command({commandJson});");
    }

    private async Task SelectLegacyElementsInViewerAsync(IReadOnlyList<string> ids)
    {
        if (PreviewWebView.CoreWebView2 is null)
        {
            return;
        }

        var idsJson = JsonSerializer.Serialize(ids);
        await PreviewWebView.ExecuteScriptAsync($"window.scadaSceneEditor && window.scadaSceneEditor.selectLegacyElements({idsJson});");
    }

    private async Task RestoreLegacyElementsInViewerAsync(IReadOnlyList<string> ids)
    {
        if (PreviewWebView.CoreWebView2 is null)
        {
            return;
        }

        var idsJson = JsonSerializer.Serialize(ids);
        await PreviewWebView.ExecuteScriptAsync($"window.scadaSceneEditor && window.scadaSceneEditor.restoreLegacyElements({idsJson});");
    }

    private async Task HideLegacyElementsInViewerAsync(IReadOnlyList<string> ids)
    {
        if (PreviewWebView.CoreWebView2 is null)
        {
            return;
        }

        var idsJson = JsonSerializer.Serialize(ids);
        await PreviewWebView.ExecuteScriptAsync($"window.scadaSceneEditor && window.scadaSceneEditor.hideLegacyElements({idsJson});");
    }

    private async Task RemoveLegacyElementsInViewerAsync(IReadOnlyList<string> ids)
    {
        if (PreviewWebView.CoreWebView2 is null)
        {
            return;
        }

        var idsJson = JsonSerializer.Serialize(ids);
        await PreviewWebView.ExecuteScriptAsync($"window.scadaSceneEditor && window.scadaSceneEditor.removeLegacyElements({idsJson});");
    }

    private async Task PrepareInitialSceneBackgroundScriptAsync(string color)
    {
        await PreviewWebView.EnsureCoreWebView2Async();
        if (PreviewWebView.CoreWebView2 is null)
        {
            return;
        }

        UpdatePreviewNativeBackground(color);
        if (!string.IsNullOrWhiteSpace(_sceneBackgroundDocumentScriptId))
        {
            PreviewWebView.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(_sceneBackgroundDocumentScriptId);
            _sceneBackgroundDocumentScriptId = null;
        }

        var colorJson = JsonSerializer.Serialize(color);
        var script = $$"""
(() => {
  const color = {{colorJson}};
  const styleId = 'scada-initial-scene-background';
  const installStyle = () => {
    let style = document.getElementById(styleId);
    if (!style) {
      style = document.createElement('style');
      style.id = styleId;
      (document.head || document.documentElement).appendChild(style);
    }

    style.textContent = `html, body, .page, #scada-root { background: ${color} !important; } body { min-height: 100vh !important; }`;
  };
  const apply = () => {
    installStyle();
    document.documentElement.style.backgroundColor = color;
    if (document.body) {
      document.body.style.backgroundColor = color;
    }

    const surface = document.querySelector('.page') || document.querySelector('#scada-root');
    if (surface) {
      surface.style.backgroundColor = color;
    }
  };

  apply();
  document.addEventListener('readystatechange', apply);
  document.addEventListener('DOMContentLoaded', apply, { once: true });
  window.addEventListener('load', apply, { once: true });
})();
""";
        _sceneBackgroundDocumentScriptId = await PreviewWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }

    private async Task ApplySceneBackgroundColorAsync(string color, bool updateStatus = true)
    {
        if (PreviewWebView.CoreWebView2 is null)
        {
            return;
        }

        var colorJson = JsonSerializer.Serialize(color);
        UpdatePreviewSurfaceBackground(color);
        UpdatePreviewNativeBackground(color);
        await PreviewWebView.ExecuteScriptAsync($"window.scadaSceneEditor && window.scadaSceneEditor.setBackgroundColor({colorJson});");
        if (updateStatus)
        {
            SetStatus($"Couleur de fond V2 appliquee: {color}. Sauvegarde requise.");
        }
    }

    private async Task ApplySceneCanvasSizeAsync(CanvasSize canvasSize)
    {
        if (PreviewWebView.CoreWebView2 is null)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            width = canvasSize.Width,
            height = canvasSize.Height
        });

await PreviewWebView.ExecuteScriptAsync($$"""
(function(size) {
  if (window.scadaSceneEditor && typeof window.scadaSceneEditor.setCanvasSize === 'function') {
    window.scadaSceneEditor.setCanvasSize(size);
    return;
  }

  const surface = document.querySelector('.page') || document.querySelector('#scada-root') || document.querySelector('[id^="ft100-"]');
  if (!surface) {
    return;
  }

  document.documentElement.style.setProperty('--page-w', size.width + 'px');
  document.documentElement.style.setProperty('--page-h', size.height + 'px');
  surface.style.setProperty('--page-w', size.width + 'px');
  surface.style.setProperty('--page-h', size.height + 'px');
  surface.style.width = size.width + 'px';
  surface.style.height = size.height + 'px';
  surface.style.minWidth = size.width + 'px';
  surface.style.minHeight = size.height + 'px';
  surface.style.overflow = 'hidden';

  const modernLayer = document.getElementById('scada-modern-layer');
  if (modernLayer && modernLayer.parentElement === surface) {
    modernLayer.style.width = size.width + 'px';
    modernLayer.style.height = size.height + 'px';
  }
})( {{payload}} );
""");
    }

    private void LoadPageProperties(ScadaScene? scene)
    {
        if (scene is null || !ArePagePropertyControlsReady())
        {
            return;
        }

        _isUpdatingPagePropertyControls = true;
        try
        {
            PageNameTextBox.Text = scene.Id;
            SelectPageType(scene.PageType);
            IncludeInBuildCheckBox.IsChecked = scene.IncludeInBuild;
            HomePageCheckBox.IsChecked = string.Equals(_modernProject?.HomePageId, scene.Id, StringComparison.Ordinal);
            HomePageCheckBox.IsEnabled = scene.PageType == ScadaPageType.Default && scene.IncludeInBuild;
            RefreshCompositionComboBox(HeaderPageComboBox, ScadaPageType.Header, scene.HeaderPageId);
            RefreshCompositionComboBox(FooterPageComboBox, ScadaPageType.Footer, scene.FooterPageId);
            var canCompose = scene.PageType is not (ScadaPageType.Header or ScadaPageType.Footer);
            HeaderPageComboBox.IsEnabled = canCompose;
            FooterPageComboBox.IsEnabled = canCompose;
            PageWidthTextBox.Text = scene.CanvasSize.Width.ToString();
            PageHeightTextBox.Text = scene.CanvasSize.Height.ToString();
            var background = scene.EffectiveBackground;
            SetBackgroundColorControls(background.Color);
            BackgroundImageTextBox.Text = background.Image ?? "";
            BackgroundSizeTextBox.Text = background.Size;
            SelectComboBoxText(BackgroundRepeatComboBox, background.Repeat);
            BackgroundPositionTextBox.Text = background.Position;
            SelectComboBoxText(BackgroundAttachmentComboBox, background.Attachment);
            SelectComboBoxText(BackgroundOriginComboBox, background.Origin);
            SelectComboBoxText(BackgroundClipComboBox, background.Clip);
            SelectComboBoxText(BackgroundBlendModeComboBox, background.BlendMode);
        }
        finally
        {
            _isUpdatingPagePropertyControls = false;
        }
    }

    private void SetPageDimensionFields(int width, int height)
    {
        if (!ArePagePropertyControlsReady())
        {
            return;
        }

        _isUpdatingPagePropertyControls = true;
        try
        {
            PageWidthTextBox.Text = width.ToString();
            PageHeightTextBox.Text = height.ToString();
        }
        finally
        {
            _isUpdatingPagePropertyControls = false;
        }
    }

    private bool TryReadPageDimensions(out int width, out int height)
    {
        width = 0;
        height = 0;
        return ArePagePropertyControlsReady() &&
            int.TryParse(PageWidthTextBox.Text.Trim(), out width) &&
            int.TryParse(PageHeightTextBox.Text.Trim(), out height) &&
            width >= 160 &&
            height >= 120;
    }

    private bool ArePagePropertyControlsReady()
    {
        return PageNameTextBox is not null &&
            PageTypeComboBox is not null &&
            IncludeInBuildCheckBox is not null &&
            HomePageCheckBox is not null &&
            HeaderPageComboBox is not null &&
            FooterPageComboBox is not null &&
            PageWidthTextBox is not null &&
            PageHeightTextBox is not null &&
            BackgroundImageTextBox is not null &&
            BackgroundSizeTextBox is not null &&
            BackgroundRepeatComboBox is not null &&
            BackgroundPositionTextBox is not null &&
            BackgroundAttachmentComboBox is not null &&
            BackgroundOriginComboBox is not null &&
            BackgroundClipComboBox is not null &&
            BackgroundBlendModeComboBox is not null;
    }

    private void RefreshCompositionComboBox(ComboBox comboBox, ScadaPageType pageType, string? selectedPageId)
    {
        comboBox.Items.Clear();
        comboBox.Items.Add(new ComboBoxItem { Content = "Aucun", Tag = "" });

        foreach (var page in GetCurrentSceneReferences()
            .Where(page => page.Type == pageType)
            .OrderBy(page => page.Id, StringComparer.Ordinal))
        {
            comboBox.Items.Add(new ComboBoxItem
            {
                Content = $"{page.Id} - {page.Title}",
                Tag = page.Id
            });
        }

        comboBox.SelectedIndex = 0;
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), selectedPageId, StringComparison.Ordinal))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private static string? GetSelectedCompositionPageId(ComboBox comboBox)
    {
        var value = (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private IReadOnlyList<ScadaSceneReference> GetCurrentSceneReferences()
    {
        var references = (_modernProject?.Scenes ?? Array.Empty<ScadaSceneReference>())
            .ToDictionary(scene => scene.Id, StringComparer.Ordinal);

        foreach (var tab in _openSceneTabs)
        {
            var scene = ReferenceEquals(tab, _activeSceneTab) && _activeScene is not null
                ? _activeScene
                : tab.Scene;
            references[scene.Id] = ToSceneReference(scene);
        }

        return references.Values
            .OrderBy(scene => scene.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private void UpdateModernProjectFromActiveScene()
    {
        if (_modernProject is null || _activeScene is null)
        {
            return;
        }

        var reference = ToSceneReference(_activeScene);
        var scenes = _modernProject.Scenes
            .Where(scene => !string.Equals(scene.Id, reference.Id, StringComparison.Ordinal))
            .Append(reference)
            .OrderBy(scene => scene.Id, StringComparer.Ordinal)
            .ToArray();

        _modernProject = _modernProject with { Scenes = scenes };
    }

    private void RefreshProjectTagSummary()
    {
        _tagCatalogItems.Clear();
        if (_modernProject?.TagCatalog is not { Count: > 0 } catalog)
        {
            ProjectTagsSummaryText.Text = "Aucun catalogue importe";
            RefreshTagCatalogFilterOptions();
            _tagCatalogView.Refresh();
            UpdateTagCatalogFilteredSummary();
            return;
        }

        foreach (var tag in catalog.Tags.OrderBy(tag => tag.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            _tagCatalogItems.Add(new TagCatalogListItem(
                tag.Id,
                tag.DisplayName,
                tag.Datatype ?? "",
                tag.Device ?? "",
                tag.AddressUri ?? "",
                tag.Writeable ? "Lecture/Ecriture" : "Lecture",
                tag.Enabled ? "Actif" : "Desactive"));
        }

        var source = string.IsNullOrWhiteSpace(catalog.SourceFileName)
            ? "source inconnue"
            : catalog.SourceFileName;
        ProjectTagsSummaryText.Text = $"{catalog.Count} tag(s) importes - {source}";
        RefreshTagCatalogFilterOptions();
        _tagCatalogView.Refresh();
        UpdateTagCatalogFilteredSummary();
    }

    private void InitializeTagCatalogFilters()
    {
        TagCatalogDeviceFilterComboBox.ItemsSource = new[] { TagCatalogAllDevicesFilter };
        TagCatalogDatatypeFilterComboBox.ItemsSource = new[] { TagCatalogAllDatatypesFilter };
        TagCatalogAccessFilterComboBox.ItemsSource = new[] { TagCatalogAllAccessFilter };
        TagCatalogStateFilterComboBox.ItemsSource = new[] { TagCatalogAllStatesFilter };
        TagCatalogDeviceFilterComboBox.SelectedIndex = 0;
        TagCatalogDatatypeFilterComboBox.SelectedIndex = 0;
        TagCatalogAccessFilterComboBox.SelectedIndex = 0;
        TagCatalogStateFilterComboBox.SelectedIndex = 0;
    }

    private void RefreshTagCatalogFilterOptions()
    {
        SetFilterItems(
            TagCatalogDeviceFilterComboBox,
            TagCatalogAllDevicesFilter,
            _tagCatalogItems.Select(item => item.Device));
        SetFilterItems(
            TagCatalogDatatypeFilterComboBox,
            TagCatalogAllDatatypesFilter,
            _tagCatalogItems.Select(item => item.Datatype));
        SetFilterItems(
            TagCatalogAccessFilterComboBox,
            TagCatalogAllAccessFilter,
            _tagCatalogItems.Select(item => item.Access));
        SetFilterItems(
            TagCatalogStateFilterComboBox,
            TagCatalogAllStatesFilter,
            _tagCatalogItems.Select(item => item.State));
    }

    private static void SetFilterItems(ComboBox comboBox, string allLabel, IEnumerable<string> values)
    {
        var previous = comboBox.SelectedItem as string;
        var items = new[] { allLabel }
            .Concat(values.Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase))
            .ToArray();
        comboBox.ItemsSource = items;
        comboBox.SelectedItem = !string.IsNullOrWhiteSpace(previous) && items.Contains(previous)
            ? previous
            : allLabel;
    }

    private void OnTagCatalogFilterChanged(object sender, RoutedEventArgs e)
    {
        _tagCatalogView.Refresh();
        UpdateTagCatalogFilteredSummary();
    }

    private bool FilterTagCatalogItem(object candidate)
    {
        return candidate is TagCatalogListItem item &&
            TagCatalogFilterMatches(
                item,
                TagCatalogSearchTextBox?.Text,
                TagCatalogDeviceFilterComboBox?.SelectedItem as string,
                TagCatalogDatatypeFilterComboBox?.SelectedItem as string,
                TagCatalogAccessFilterComboBox?.SelectedItem as string,
                TagCatalogStateFilterComboBox?.SelectedItem as string);
    }

    private static bool TagCatalogFilterMatches(
        TagCatalogListItem item,
        string? searchText,
        string? deviceFilter,
        string? datatypeFilter,
        string? accessFilter,
        string? stateFilter)
    {
        if (!MatchesTextSearch(item, searchText))
        {
            return false;
        }

        return MatchesExactFilter(item.Device, deviceFilter, TagCatalogAllDevicesFilter) &&
            MatchesExactFilter(item.Datatype, datatypeFilter, TagCatalogAllDatatypesFilter) &&
            MatchesExactFilter(item.Access, accessFilter, TagCatalogAllAccessFilter) &&
            MatchesExactFilter(item.State, stateFilter, TagCatalogAllStatesFilter);
    }

    private static bool MatchesTextSearch(TagCatalogListItem item, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return item.SearchText.Contains(searchText.Trim(), StringComparison.CurrentCultureIgnoreCase);
    }

    private static bool MatchesExactFilter(string value, string? filter, string allLabel)
    {
        return string.IsNullOrWhiteSpace(filter) ||
            string.Equals(filter, allLabel, StringComparison.Ordinal) ||
            string.Equals(value, filter, StringComparison.CurrentCultureIgnoreCase);
    }

    private void UpdateTagCatalogFilteredSummary()
    {
        var visibleItems = _tagCatalogItems.Where(item => FilterTagCatalogItem(item)).ToArray();
        var total = _tagCatalogItems.Count;
        var writeable = visibleItems.Count(item => item.Access == "Lecture/Ecriture");
        var readOnly = visibleItems.Count(item => item.Access == "Lecture");
        var disabled = visibleItems.Count(item => item.State == "Desactive");
        TagCatalogFilteredSummaryText.Text = total == 0
            ? "Aucun tag affiche"
            : $"{visibleItems.Length}/{total} tag(s) affiche(s) - {writeable} ecriture(s), {readOnly} lecture seule, {disabled} desactive(s)";
    }

    private string FormatProjectTag(string tagId)
    {
        var tag = _modernProject?.TagCatalog?.Tags.FirstOrDefault(tag => string.Equals(tag.Id, tagId, StringComparison.Ordinal));
        return tag?.AuthoringLabel ?? tagId;
    }

    private string FormatElementEventsSummary(ScadaElement element)
    {
        var data = element.Data ?? new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
        var bindingSummaries = new List<string>();
        if (!string.IsNullOrWhiteSpace(data.ReadTagId))
        {
            bindingSummaries.Add($"Lire valeur: {FormatProjectTag(data.ReadTagId)}");
        }

        if (!string.IsNullOrWhiteSpace(data.WriteTagId))
        {
            bindingSummaries.Add($"Ecrire valeur: {FormatProjectTag(data.WriteTagId)}");
        }

        bindingSummaries.AddRange(element.EventBindings
            .Select(binding => ScadaEventRegistry.FindTrigger(binding.Trigger)?.FrenchLabel ?? binding.Trigger));
        return bindingSummaries.Count == 0
            ? "Aucun evenement"
            : string.Join(", ", bindingSummaries);
    }

    private void OnCreateProjectTagClick(object sender, RoutedEventArgs e)
    {
        SetStatus("Creation de tags disponible dans une prochaine revision apres import des protocoles projet.");
    }

    private void SetHomePageId(string? pageId)
    {
        if (_modernProject is null)
        {
            return;
        }

        _modernProject = _modernProject with
        {
            HomePageId = string.IsNullOrWhiteSpace(pageId) ? null : pageId
        };
    }

    private void EnsureHomePageStillValid()
    {
        if (_modernProject is null)
        {
            return;
        }

        var currentHome = _modernProject.HomePageId;
        if (string.IsNullOrWhiteSpace(currentHome))
        {
            return;
        }

        var homePage = GetCurrentSceneReferences()
            .FirstOrDefault(scene => string.Equals(scene.Id, currentHome, StringComparison.Ordinal));
        if (homePage is null || homePage.Type != ScadaPageType.Default || !homePage.IncludeInBuild)
        {
            _modernProject = _modernProject with { HomePageId = null };
        }
    }

    private static ScadaSceneReference ToSceneReference(ScadaScene scene)
    {
        return new ScadaSceneReference(
            scene.Id,
            scene.Title,
            $"scenes/{scene.Id}.scene.json",
            scene.PageType,
            scene.CanvasSize,
            scene.EffectiveBackground,
            scene.IncludeInBuild,
            scene.HeaderPageId,
            scene.FooterPageId);
    }

    private ScadaPageType GetSelectedPageType()
    {
        return PageTypeComboBox.SelectedIndex switch
        {
            1 => ScadaPageType.Fragment,
            2 => ScadaPageType.Header,
            3 => ScadaPageType.Footer,
            _ => ScadaPageType.Default
        };
    }

    private void SelectPageType(ScadaPageType pageType)
    {
        PageTypeComboBox.SelectedIndex = pageType switch
        {
            ScadaPageType.Fragment => 1,
            ScadaPageType.Header => 2,
            ScadaPageType.Footer => 3,
            _ => 0
        };
    }

    private static string GetPageTypeLabel(ScadaPageType pageType)
    {
        return pageType switch
        {
            ScadaPageType.Fragment => "Fragment",
            ScadaPageType.Header => "Entete",
            ScadaPageType.Footer => "Pied-de-page",
            _ => "Defaut"
        };
    }

    private async Task ShowContextMenuForRequestAsync(LegacyViewerMessage message)
    {
        if (message.Items is { Count: > 0 })
        {
            ApplyLegacySelection(message.Items);
        }
        else if ((message.TargetKind == "object" || message.TargetKind == "modern") && !string.IsNullOrWhiteSpace(message.Id))
        {
            var selected = _activeScene?.FindElementRecursive(message.Id);
            if (selected is not null && !_selectedSceneObjectIds.Contains(selected.Id))
            {
                _selectedSceneObjectIds.Clear();
                _selectedSceneObjectIds.Add(selected.Id);
            }

            _selectedSceneObject = selected ?? _selectedSceneObject;
            _selectedSourceObjectIds.Clear();
            RefreshSelectionUi();
            RefreshModernSceneUi();
        }
        else if (message.TargetKind == "background")
        {
            ClearSelection();
        }

        var commands = BuildContextMenuCommands(message);
        var payload = new
        {
            commands,
            x = message.X,
            y = message.Y
        };
        var json = JsonSerializer.Serialize(payload);
        await PreviewWebView.ExecuteScriptAsync($"window.scadaSceneEditor && window.scadaSceneEditor.showContextMenu({json});");
    }

    private IReadOnlyList<EditorCommandDescriptor> BuildContextMenuCommands(LegacyViewerMessage message)
    {
        if (message.TargetKind == "background")
        {
            return
            [
                new EditorCommandDescriptor("scene.background.edit", "CSS fond", "scene")
            ];
        }

        if (message.TargetKind == "object" || message.TargetKind == "modern")
        {
            var selected = string.IsNullOrWhiteSpace(message.Id)
                ? _selectedSceneObject
                : _activeScene?.FindElementRecursive(message.Id);
            if (selected is null)
            {
                return [];
            }

            var modernCommands = new List<EditorCommandDescriptor>
            {
                new("object.properties", "Propriete", "object"),
                new("object.delete", "Supprimer la selection", "object")
            };
            if (_selectedSceneObjectIds.Count > 1)
            {
                modernCommands.Insert(0, new EditorCommandDescriptor("object.group", "Grouper", "group"));
            }

            if (selected.Kind == ScadaElementKind.Group)
            {
                modernCommands.Insert(0, new EditorCommandDescriptor("object.ungroup", "Degrouper", "group"));
            }

            return modernCommands;
        }

        var selectedLegacy = (message.Items ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToArray();
        if (selectedLegacy.Length == 0)
        {
            return [];
        }

        var commands = new List<EditorCommandDescriptor>
        {
            new("source.open-in-element-studio", "Ouvrir dans Studio Element+", "element-studio")
        };
        commands.Add(new EditorCommandDescriptor(
            "source.properties",
            "Propriete",
            "source",
            IsEnabled: false,
            DisabledReason: "Convertir l'element en Element+ avant d'ouvrir ses proprietes."));

        var targets = selectedLegacy
            .SelectMany(GetPlausibleConversionTargets)
            .Distinct()
            .ToArray();
        if (targets.Length > 0)
        {
            commands.Add(new EditorCommandDescriptor(
                "source.convert-to-element-plus",
                "Conversion Element+",
                "conversion",
                Children: targets
                    .Select(CreateConversionCommandDescriptor)
                    .ToArray()));
        }

        commands.Add(new EditorCommandDescriptor("source.mask", "Masquer la selection", "source"));
        if (selectedLegacy.Length > 1)
        {
            commands.Add(new EditorCommandDescriptor(
                "source.group-requires-conversion",
                "Grouper",
                "group",
                IsEnabled: false,
                DisabledReason: "Convertir les elements legacy en Element+ avant de les grouper."));
        }

        commands.Add(new EditorCommandDescriptor("selection.delete", "Supprimer la selection", "selection"));
        return commands;
    }

    private async Task ExecuteEditorCommandAsync(string? commandId, LegacyViewerMessage message)
    {
        if (string.IsNullOrWhiteSpace(commandId))
        {
            SetStatus("Commande ignoree: id manquant.");
            return;
        }

        if (message.Items is { Count: > 0 })
        {
            ApplyLegacySelection(message.Items);
        }

        switch (commandId)
        {
            case "scene.background.edit":
                ShowBackgroundCssEditor(message.BackgroundColor);
                break;
            case "source.convert-to-element-plus.text":
            case "legacy.convert-to-element-plus.text":
                await ConvertSelectedLegacyTextToElementPlusAsync(ElementPlusConversionTarget.Text);
                break;
            case "source.convert-to-element-plus.numeric-readonly":
            case "legacy.convert-to-element-plus.numeric-readonly":
                await ConvertSelectedLegacyTextToElementPlusAsync(ElementPlusConversionTarget.NumericReadOnly);
                break;
            case "source.convert-to-element-plus.input-text":
            case "legacy.convert-to-element-plus.input-text":
                await ConvertSelectedLegacyTextToElementPlusAsync(ElementPlusConversionTarget.TextInput);
                break;
            case "source.convert-to-element-plus.numeric-editable":
            case "legacy.convert-to-element-plus.numeric-editable":
                await ConvertSelectedLegacyTextToElementPlusAsync(ElementPlusConversionTarget.NumericEditable);
                break;
            case "source.convert-to-element-plus.button":
            case "legacy.convert-to-element-plus.button":
                await ConvertSelectedLegacyTextToElementPlusAsync(ElementPlusConversionTarget.Button);
                break;
            case "object.properties":
            case "element-plus.properties":
                ShowModernElementProperties(message.Id);
                break;
            case "source.properties":
            case "legacy.properties":
                SetStatus("Proprietes indisponibles: convertir l'element en Element+ avant d'ouvrir ses proprietes.");
                break;
            case "source.mask":
            case "legacy.mask":
                await HideSelectedLegacyElementsAsync();
                break;
            case "source.group-to-element-plus":
            case "legacy.group-to-element-plus":
            case "source.group-requires-conversion":
            case "legacy.group-requires-conversion":
                await GroupSelectedLegacyElementsAsync();
                break;
            case "object.group":
            case "element-plus.group":
                await GroupSelectedModernElementsAsync();
                break;
            case "source.open-in-element-studio":
            case "legacy.open-in-element-studio":
                await OpenSelectedLegacyInElementStudioAsync();
                break;
            case "object.ungroup":
            case "element-plus.ungroup":
                UngroupSelectedModernElement();
                break;
            case "object.delete":
            case "element-plus.delete":
                await DeleteSelectedSceneObjectsAsync(message.Id);
                break;
            case "selection.delete":
                await DeleteSelectedSceneObjectsAsync();
                break;
            default:
                SetStatus($"Commande non supportee: {commandId}");
                break;
        }
    }

    private void EditLegacyText(string? id, string? text)
    {
        if (_activeScene is null || string.IsNullOrWhiteSpace(id))
        {
            SetStatus("Edition texte legacy ignoree: aucun element actif.");
            return;
        }

        var newText = text ?? "";
        var beforeScene = _activeScene;
        _activeScene = _activeScene.WithLegacyTextOverride(id, newText);
        if (!Equals(beforeScene, _activeScene))
        {
            _activeSceneTab?.History.Push(new SceneSnapshotChangedAction(
                beforeScene.Id,
                beforeScene,
                _activeScene,
                "edition texte legacy"));
        }

        MarkActiveSceneDirty();
        SetStatus($"Texte legacy modifie pour {id}. Sauvegarde requise.");
    }

    private void ShowBackgroundCssEditor(string? backgroundColor)
    {
        if (!string.IsNullOrWhiteSpace(backgroundColor))
        {
            SetBackgroundColorControls(backgroundColor);
        }

        RightContextTabs.SelectedItem = PageContextTab;
        SetStatus("Edition CSS du fond active. La modification reste en session legacy.");
    }

    private void ShowModernElementProperties(string? elementId)
    {
        var targetId = string.IsNullOrWhiteSpace(elementId)
            ? _selectedSceneObject?.Id
            : elementId;
        if (_activeScene is null || string.IsNullOrWhiteSpace(targetId))
        {
            SetStatus("Aucun Element+ selectionne pour les proprietes.");
            return;
        }

        SelectModernElement(targetId);
        var current = _activeScene.FindElementRecursive(targetId);
        if (current is null || current.IsLegacyStatic)
        {
            SetStatus("Les proprietes modales sont disponibles sur les objets Element+ convertis.");
            return;
        }

        var dialog = new ElementPropertiesDialog(current, FormatElementEventsSummary(current))
        {
            Owner = this
        };
        dialog.OpenEvents = () =>
        {
            OpenElementEventDialog(current.Id, dialog);
            var latestWithEvents = _activeScene?.FindElementRecursive(current.Id);
            if (latestWithEvents is not null)
            {
                dialog.SetEventSummary(FormatElementEventsSummary(latestWithEvents));
            }
        };
        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        var latest = _activeScene.FindElementRecursive(current.Id);
        if (latest is null)
        {
            SetStatus("Element+ introuvable pour appliquer les proprietes.");
            return;
        }

        var updated = BuildUpdatedElementFromDialog(latest, dialog.Result);
        CommitModernElementProperties(latest, updated);
    }

    private void ShowModernElementEvents(string? elementId)
    {
        var targetId = string.IsNullOrWhiteSpace(elementId)
            ? _selectedSceneObject?.Id
            : elementId;
        if (string.IsNullOrWhiteSpace(targetId))
        {
            SetStatus("Aucun Element+ selectionne pour les evenements.");
            return;
        }

        SelectModernElement(targetId);
        RightContextTabs.SelectedItem = PropertiesContextTab;
        OpenElementEventDialog(targetId);
    }

    private async void OnSaveSceneClick(object sender, RoutedEventArgs e)
    {
        if (_repositoryRoot is null || _activeScene is null || _activeSceneTab is null)
        {
            SetStatus("Aucune scene V2 active a sauvegarder.");
            return;
        }

        await SaveSceneTabAsync(_activeSceneTab);
    }

    private async void OnImportTagsClick(object sender, RoutedEventArgs e)
    {
        if (_repositoryRoot is null || _modernProject is null)
        {
            SetStatus("Aucun projet V2 actif pour importer les tags.");
            return;
        }

        try
        {
            var importDirectory = ModernProjectStore.GetTagImportDirectory(_repositoryRoot);
            Directory.CreateDirectory(importDirectory);
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importer les tags SCADA",
                InitialDirectory = importDirectory,
                Filter = "Fichier tags SCADA (*.json)|*.json|Tous les fichiers (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
            {
                SetStatus("Import des tags annule.");
                return;
            }

            var catalog = await _tagCatalogImporter.ImportAsync(dialog.FileName);
            var snapshotPath = Path.Combine(
                importDirectory,
                $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Path.GetFileName(dialog.FileName)}");
            File.Copy(dialog.FileName, snapshotPath, overwrite: true);

            _modernProject = _modernProject with { TagCatalog = catalog with { SourceFileName = Path.GetFileName(snapshotPath) } };
            await _modernProjectStore.SaveProjectAsync(_repositoryRoot, _modernProject);
            RefreshProjectTagSummary();
            SetStatus($"Tags SCADA importes: {_modernProject.TagCatalog.Count} tag(s).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            SetStatus($"Import des tags impossible: {ex.Message}");
        }
    }

    private async void OnExportFt100Click(object sender, RoutedEventArgs e)
    {
        if (_repositoryRoot is null || _activeScene is null || _activeReferencePage is null)
        {
            SetStatus("Aucune scene active a exporter vers FT100.");
            return;
        }

        try
        {
            var defaultExportRoot = Path.Combine(
                ModernProjectStore.GetReferenceModernProjectRoot(_repositoryRoot),
                "exports");
            Directory.CreateDirectory(defaultExportRoot);

            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Selectionner le dossier de destination FT100",
                InitialDirectory = defaultExportRoot,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
            {
                SetStatus("Export FT100 annule.");
                return;
            }

            var exporter = new Ft100SceneExporter();
            UpdateModernProjectFromActiveScene();
            EnsureHomePageStillValid();
            if (_modernProject is null)
            {
                SetStatus("Export FT100 impossible: projet V2 introuvable.");
                return;
            }

            _modernProject = _modernProject with { Scenes = GetCurrentSceneReferences() };
            var errors = ScadaProjectBuildValidator.Validate(_modernProject)
                .Where(issue => issue.Severity == ScadaBuildValidationSeverity.Error)
                .ToArray();
            if (errors.Length > 0)
            {
                SetStatus($"Export FT100 bloque: {errors[0].Message}");
                return;
            }

            var inputs = await BuildFt100ProjectExportInputsAsync(_modernProject);
            var result = await exporter.ExportProjectAsync(_modernProject, inputs, dialog.FolderName);
            SetStatus($"Export FT100 paquet cree: {result.ExportDirectory} ({result.PageResults.Count} page(s), {result.CopiedImageCount} image(s)).");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        {
            SetStatus($"Export FT100 impossible: {ex.Message}");
        }
    }

    private async void OnExportFt100Sb2Click(object sender, RoutedEventArgs e)
    {
        if (_isFt100Sb2ExportRunning)
        {
            SetStatus("Export FT100 .sb2 deja en cours.");
            return;
        }

        if (_repositoryRoot is null || _activeScene is null || _activeReferencePage is null)
        {
            SetStatus("Aucune scene active a exporter vers FT100.");
            return;
        }

        try
        {
            SetStatus("Export FT100 .sb2: choix du fichier de destination...");
            var defaultExportRoot = Path.Combine(
                ModernProjectStore.GetReferenceModernProjectRoot(_repositoryRoot),
                "exports");
            Directory.CreateDirectory(defaultExportRoot);

            var projectName = _modernProject?.Name ?? "scada-builder-v2";
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exporter le package FT100 .sb2",
                InitialDirectory = defaultExportRoot,
                FileName = $"{SanitizeFileName(projectName)}.sb2",
                Filter = "Package SCADA Builder FT100 (*.sb2)|*.sb2|Tous les fichiers (*.*)|*.*",
                AddExtension = true,
                DefaultExt = ".sb2",
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                SetStatus("Export FT100 .sb2 annule.");
                return;
            }

            _isFt100Sb2ExportRunning = true;
            SetFt100ExportProgress(true);
            SetStatus("Export FT100 .sb2 en cours: preparation des pages...");
            await Dispatcher.Yield(DispatcherPriority.Background);

            var exporter = new Ft100SceneExporter();
            UpdateModernProjectFromActiveScene();
            EnsureHomePageStillValid();
            if (_modernProject is null)
            {
                SetStatus("Export FT100 .sb2 impossible: projet V2 introuvable.");
                return;
            }

            _modernProject = _modernProject with { Scenes = GetCurrentSceneReferences() };
            var errors = ScadaProjectBuildValidator.Validate(_modernProject)
                .Where(issue => issue.Severity == ScadaBuildValidationSeverity.Error)
                .ToArray();
            if (errors.Length > 0)
            {
                SetStatus($"Export FT100 .sb2 bloque: {errors[0].Message}");
                return;
            }

            SetStatus("Export FT100 .sb2 en cours: resolution des sources...");
            var inputs = await BuildFt100ProjectExportInputsAsync(_modernProject);
            var projectSnapshot = _modernProject;
            var archivePath = dialog.FileName;
            SetStatus("Export FT100 .sb2 en cours: generation et compression...");
            var result = await Task.Run(() => exporter.ExportProjectArchiveAsync(projectSnapshot, inputs, archivePath));
            var warningText = result.Validation.Warnings.Count == 0
                ? ""
                : $" {result.Validation.Warnings.Count} warning(s) compatibilite FT100.";
            SetStatus($"Export FT100 .sb2 cree: {result.ArchivePath} ({result.PageCount} page(s), {result.CopiedImageCount} image(s)).{warningText}");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        {
            SetStatus($"Export FT100 .sb2 impossible: {ex.Message}");
        }
        finally
        {
            _isFt100Sb2ExportRunning = false;
            SetFt100ExportProgress(false);
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized)
            ? "scada-builder-v2"
            : sanitized;
    }

    private async Task<IReadOnlyList<Ft100ProjectPageExportInput>> BuildFt100ProjectExportInputsAsync(ScadaProject project)
    {
        if (_repositoryRoot is null || _referenceProject is null)
        {
            throw new InvalidOperationException("Aucun projet de reference actif.");
        }

        var referencePagesById = _referenceProject.Pages.ToDictionary(page => page.Id, StringComparer.Ordinal);
        var inputs = new List<Ft100ProjectPageExportInput>();
        foreach (var pageReference in project.Scenes.Where(page => page.IncludeInBuild).OrderBy(page => page.Id, StringComparer.Ordinal))
        {
            if (!referencePagesById.TryGetValue(pageReference.Id, out var referencePage))
            {
                throw new InvalidOperationException($"Reference page '{pageReference.Id}' introuvable.");
            }

            var source = await ResolveLegacyViewerSourceAsync(referencePage);
            if (source is null)
            {
                throw new InvalidOperationException($"Source HTML legacy introuvable pour {pageReference.Id}.");
            }

            var scene = await LoadSceneForProjectExportAsync(pageReference, referencePage);
            inputs.Add(new Ft100ProjectPageExportInput(
                scene,
                Path.Combine(source.RootPath, source.RelativeHtmlSource)));
        }

        return inputs;
    }

    private async Task<ScadaScene> LoadSceneForProjectExportAsync(
        ScadaSceneReference pageReference,
        ReferenceScadaPage referencePage)
    {
        if (_repositoryRoot is null)
        {
            throw new InvalidOperationException("Aucun depot actif.");
        }

        var openTab = _openSceneTabs.FirstOrDefault(tab => string.Equals(tab.SceneId, pageReference.Id, StringComparison.Ordinal));
        var scene = openTab is null
            ? await _modernProjectStore.LoadOrCreateSceneAsync(
                _repositoryRoot,
                pageReference.Id,
                referencePage.Title,
                pageReference.EffectiveCanvasSize)
            : ReferenceEquals(openTab, _activeSceneTab) && _activeScene is not null
                ? _activeScene
                : openTab.Scene;

        return scene
            .WithPageType(pageReference.Type)
            .WithIncludeInBuild(pageReference.IncludeInBuild)
            .WithCanvasSize(pageReference.EffectiveCanvasSize)
            .WithBackground(pageReference.EffectiveBackground)
            .WithPageComposition(pageReference.HeaderPageId, pageReference.FooterPageId);
    }

    private void OnInsertInputTextClick(object sender, RoutedEventArgs e)
    {
        BeginModernElementPlacement(ScadaElementKind.InputText);
    }

    private void OnInsertTextClick(object sender, RoutedEventArgs e)
    {
        BeginModernElementPlacement(ScadaElementKind.Text);
    }

    private void OnInsertInputNumericClick(object sender, RoutedEventArgs e)
    {
        BeginModernElementPlacement(ScadaElementKind.InputNumeric);
    }

    private void OnInsertRectangleClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.Rectangle);
    }

    private void OnInsertEllipseClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.Ellipse);
    }

    private void OnInsertLineClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.Line);
    }

    private void OnInsertArrowClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.Arrow);
    }

    private void OnInsertIndicatorLampClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.IndicatorLamp);
    }

    private void OnInsertHorizontalBarClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.HorizontalBar);
    }

    private void OnInsertVerticalBarClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.VerticalBar);
    }

    private void OnInsertTankClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.Tank);
    }

    private void OnInsertPipeHorizontalClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.PipeHorizontal);
    }

    private void OnInsertPipeVerticalClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.PipeVertical);
    }

    private void OnInsertValveClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.Valve);
    }

    private void OnInsertPumpClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.Pump);
    }

    private void OnInsertMotorClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.Motor);
    }

    private void OnInsertFanClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.Fan);
    }

    private void OnInsertConveyorClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.Conveyor);
    }

    private void OnInsertGaugeClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.Gauge);
    }

    private void OnInsertSwitchClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.Switch);
    }

    private void OnInsertBreakerClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.Breaker);
    }

    private void OnInsertTransformerClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.Transformer);
    }

    private void OnInsertAlarmBeaconClick(object sender, RoutedEventArgs e)
    {
        BeginShapePlacement(ScadaShapeKind.AlarmBeacon);
    }

    private void OnInsertButtonClick(object sender, RoutedEventArgs e)
    {
        BeginButtonPlacement(ScadaButtonKind.Command);
    }

    private void OnInsertToggleButtonClick(object sender, RoutedEventArgs e)
    {
        BeginButtonPlacement(ScadaButtonKind.Toggle);
    }

    private void OnInsertNavigationButtonClick(object sender, RoutedEventArgs e)
    {
        BeginButtonPlacement(ScadaButtonKind.Navigation);
    }

    private void OnInsertAlarmAckButtonClick(object sender, RoutedEventArgs e)
    {
        BeginButtonPlacement(ScadaButtonKind.AlarmAcknowledge);
    }

    private void OnInsertEmergencyStopButtonClick(object sender, RoutedEventArgs e)
    {
        BeginButtonPlacement(ScadaButtonKind.EmergencyStop);
    }

    private void BeginShapePlacement(ScadaShapeKind shapeKind, string? commandId = null)
    {
        _pendingInsertShapeKind = shapeKind;
        BeginModernElementPlacement(ScadaElementKind.Shape, commandId ?? CommandIdForShape(shapeKind));
    }

    private void BeginButtonPlacement(ScadaButtonKind buttonKind, string? commandId = null)
    {
        _pendingInsertButtonKind = buttonKind;
        BeginModernElementPlacement(ScadaElementKind.Button, commandId ?? CommandIdForButton(buttonKind));
    }

    private async void BeginModernElementPlacement(ScadaElementKind kind, string? commandId = null)
    {
        _pendingInsertKind = kind;
        if (kind != ScadaElementKind.Shape)
        {
            _pendingInsertShapeKind = null;
        }
        if (kind != ScadaElementKind.Button)
        {
            _pendingInsertButtonKind = null;
        }

        _activeInsertCommandId = commandId ?? CommandIdForElementKind(kind);
        RefreshActiveRibbonCommandStates();

        var shapeKind = _pendingInsertShapeKind ?? ScadaShapeKind.Rectangle;
        var isTwoPointShape = kind == ScadaElementKind.Shape && IsTwoPointShape(shapeKind);
        await ExecuteLegacyViewerCommandAsync(new LegacyViewerCommand(
            "beginPlacement",
            kind.ToString(),
            ShapeKind: kind == ScadaElementKind.Shape ? shapeKind.ToString() : null,
            IsTwoPoint: isTwoPointShape));
        var label = kind switch
        {
            ScadaElementKind.InputText => "champ d'entree texte",
            ScadaElementKind.InputNumeric => "champ d'entree numerique",
            ScadaElementKind.Shape => FormatShapeLabel(_pendingInsertShapeKind ?? ScadaShapeKind.Rectangle),
            ScadaElementKind.Button => FormatButtonLabel(_pendingInsertButtonKind ?? ScadaButtonKind.Command),
            _ => "champ texte"
        };
        var instruction = isTwoPointShape
            ? "cliquez le premier point, puis le second point"
            : "cliquez dans la scene";
        SetStatus($"Insertion active: {instruction} pour placer un {label}.");
    }

    private void PlaceModernElement(string? kind, string? shapeKindText, double x, double y)
    {
        if (_activeScene is null)
        {
            SetStatus("Aucune scene V2 active.");
            return;
        }

        var elementKind = ParseInsertKind(kind) ?? _pendingInsertKind;
        if (elementKind is null)
        {
            SetStatus("Aucun outil d'insertion actif.");
            return;
        }

        var shapeKind = elementKind == ScadaElementKind.Shape
            ? ParseShapeKind(shapeKindText) ?? _pendingInsertShapeKind
            : null;
        if (elementKind == ScadaElementKind.Shape && shapeKind is null)
        {
            SetStatus("Aucune forme d'insertion active.");
            return;
        }

        var element = CreateModernElement(elementKind.Value, x, y, shapeKind);
        AddModernElementToScene(element, "insertion Element+");
    }

    private void PlaceTwoPointShape(string? shapeKindText, double x, double y, double x2, double y2)
    {
        if (_activeScene is null)
        {
            SetStatus("Aucune scene V2 active.");
            return;
        }

        var shapeKind = ParseShapeKind(shapeKindText) ?? _pendingInsertShapeKind;
        if (shapeKind is null || !IsTwoPointShape(shapeKind.Value))
        {
            SetStatus("Aucun outil ligne/fleche actif.");
            return;
        }

        var left = Math.Min(x, x2);
        var top = Math.Min(y, y2);
        var width = Math.Max(8, Math.Abs(x2 - x));
        var height = Math.Max(8, Math.Abs(y2 - y));
        var startX = Math.Abs(x2 - x) < 8 ? width / 2 : x - left;
        var startY = Math.Abs(y2 - y) < 8 ? height / 2 : y - top;
        var endX = Math.Abs(x2 - x) < 8 ? width / 2 : x2 - left;
        var endY = Math.Abs(y2 - y) < 8 ? height / 2 : y2 - top;

        var sequence = _nextShapeSequence++;
        var id = CreateUniqueElementId($"shape_{sequence:000}");
        var element = ScadaElement.CreateShape(id, $"{FormatShapeName(shapeKind.Value)}{sequence:000}", shapeKind.Value, left, top);
        var data = element.Data ?? new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
        element = element with
        {
            Bounds = new SceneBounds(left, top, width, height),
            Data = data with
            {
                ShapeStartX = startX,
                ShapeStartY = startY,
                ShapeEndX = endX,
                ShapeEndY = endY
            }
        };

        AddModernElementToScene(element, "insertion ligne Element+");
    }

    private void AddModernElementToScene(ScadaElement element, string historyLabel)
    {
        if (_activeScene is null)
        {
            SetStatus("Aucune scene V2 active.");
            return;
        }

        var beforeScene = _activeScene;
        _activeScene = _activeScene.WithElement(element);
        _activeSceneTab?.History.Push(new SceneSnapshotChangedAction(
            beforeScene.Id,
            beforeScene,
            _activeScene,
            historyLabel));
        _selectedSceneObject = element;
        _selectedSceneObjectIds.Clear();
        _selectedSceneObjectIds.Add(element.Id);
        _selectedSourceObjectIds.Clear();
        ResetPendingInsertion();
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        _ = RenderModernSceneAsync();
        SetStatus($"{element.UserLabel} ajoute a la scene V2. Sauvegarde requise.");
    }

    private void ResetPendingInsertion()
    {
        _pendingInsertKind = null;
        _pendingInsertShapeKind = null;
        _pendingInsertButtonKind = null;
        _activeInsertCommandId = null;
        RefreshActiveRibbonCommandStates();
    }

    private async Task CreateElementPlusLibraryInstanceAsync(
        string packagePath,
        double x,
        double y,
        bool centerOnPoint = false)
    {
        if (_activeScene is null)
        {
            SetStatus("Aucune scene V2 active pour instancier Element+.");
            return;
        }

        try
        {
            var package = await _elementStudioComponentPackageStore.ReadFromPathAsync(packagePath);
            var component = package.Component;
            var markup = component.Visual.Kind switch
            {
                ElementStudioComponentVisualKind.Svg => ElementStudioSvgMarkupNormalizer.NormalizeSvgMarkup(component.Visual.SvgMarkup),
                ElementStudioComponentVisualKind.Html => component.Visual.HtmlMarkup,
                _ => null
            };

            if (string.IsNullOrWhiteSpace(markup))
            {
                markup = $"""<div class="element-plus-placeholder">{System.Net.WebUtility.HtmlEncode(component.Name)}</div>""";
            }

            var bounds = component.Bounds.HasPositiveSize
                ? component.Bounds
                : new SceneBounds(0, 0, 120, 80);
            var width = Math.Max(24, bounds.Width);
            var height = Math.Max(24, bounds.Height);
            var targetX = centerOnPoint ? Math.Max(0, x - (width / 2)) : x;
            var targetY = centerOnPoint ? Math.Max(0, y - (height / 2)) : y;
            var id = CreateUniqueElementId($"lib_{SanitizeElementIdPart(component.ComponentId)}");
            var element = new ScadaElement(
                id,
                component.Name,
                ScadaElementKind.Custom,
                new SceneBounds(targetX, targetY, width, height),
                null,
                ScadaElementLayout.Absolute,
                ScadaElementStyle.DefaultText with
                {
                    Background = "Transparent",
                    BorderColor = "Transparent",
                    BorderWidth = 0,
                    BorderStyle = "None"
                },
                new ScadaElementData(
                    markup,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    component.Visual.Kind.ToString(),
                    Path.GetFileName(packagePath),
                    false));

            var beforeScene = _activeScene;
            _activeScene = _activeScene.WithElement(element);
            _activeSceneTab?.History.Push(new SceneSnapshotChangedAction(
                beforeScene.Id,
                beforeScene,
                _activeScene,
                "instanciation librairie Element+"));
            _selectedSceneObject = element;
            _selectedSceneObjectIds.Clear();
            _selectedSceneObjectIds.Add(element.Id);
            _selectedSourceObjectIds.Clear();
            MarkActiveSceneDirty();
            RefreshSelectionUi();
            RefreshModernSceneUi();
            await RenderModernSceneAsync();
            SetStatus($"{element.UserLabel} instancie depuis la librairie Element+. Sauvegarde requise.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException)
        {
            SetStatus($"Instantiation Element+ impossible: {ex.Message}");
        }
    }

    private static ScadaElementKind? ParseInsertKind(string? kind)
    {
        return Enum.TryParse<ScadaElementKind>(kind, ignoreCase: true, out var parsed) &&
            (parsed == ScadaElementKind.Text ||
                parsed == ScadaElementKind.InputText ||
                parsed == ScadaElementKind.InputNumeric ||
                parsed == ScadaElementKind.Shape ||
                parsed == ScadaElementKind.Button)
                ? parsed
                : null;
    }

    private static ScadaShapeKind? ParseShapeKind(string? shapeKind)
    {
        return Enum.TryParse<ScadaShapeKind>(shapeKind, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static bool IsTwoPointShape(ScadaShapeKind shapeKind)
    {
        return shapeKind is ScadaShapeKind.Line or ScadaShapeKind.Arrow;
    }

    private static string? CommandIdForElementKind(ScadaElementKind kind)
    {
        return kind switch
        {
            ScadaElementKind.Text => "insert.text",
            ScadaElementKind.InputText => "insert.input-text",
            ScadaElementKind.InputNumeric => "insert.input-numeric",
            _ => null
        };
    }

    private static string? CommandIdForShape(ScadaShapeKind shapeKind)
    {
        return shapeKind switch
        {
            ScadaShapeKind.Rectangle => "insert.shape.rectangle",
            ScadaShapeKind.Ellipse => "insert.shape.ellipse",
            ScadaShapeKind.Circle => "insert.shape.circle",
            ScadaShapeKind.Triangle => "insert.shape.triangle",
            ScadaShapeKind.Star => "insert.shape.star",
            ScadaShapeKind.Line => "insert.shape.line",
            ScadaShapeKind.Arrow => "insert.shape.arrow",
            ScadaShapeKind.IndicatorLamp => "insert.hmi.indicator-lamp",
            ScadaShapeKind.HorizontalBar => "insert.hmi.bar-horizontal",
            ScadaShapeKind.VerticalBar => "insert.hmi.bar-vertical",
            ScadaShapeKind.Tank => "insert.hmi.tank",
            ScadaShapeKind.PipeHorizontal => "insert.hmi.pipe-horizontal",
            ScadaShapeKind.PipeVertical => "insert.hmi.pipe-vertical",
            ScadaShapeKind.Valve => "insert.hmi.valve",
            ScadaShapeKind.Pump => "insert.hmi.pump",
            ScadaShapeKind.Motor => "insert.hmi.motor",
            ScadaShapeKind.Fan => "insert.hmi.fan",
            ScadaShapeKind.Conveyor => "insert.hmi.conveyor",
            ScadaShapeKind.Gauge => "insert.hmi.gauge",
            ScadaShapeKind.Switch => "insert.hmi.switch",
            ScadaShapeKind.Breaker => "insert.hmi.breaker",
            ScadaShapeKind.Transformer => "insert.hmi.transformer",
            ScadaShapeKind.AlarmBeacon => "insert.hmi.alarm-beacon",
            _ => null
        };
    }

    private static string? CommandIdForButton(ScadaButtonKind buttonKind)
    {
        return buttonKind switch
        {
            ScadaButtonKind.Command => "insert.button.command",
            ScadaButtonKind.Toggle => "insert.button.toggle",
            ScadaButtonKind.Navigation => "insert.button.navigation",
            ScadaButtonKind.AlarmAcknowledge => "insert.button.alarm-ack",
            ScadaButtonKind.EmergencyStop => "insert.button.emergency-stop",
            _ => null
        };
    }

    private ScadaElement CreateModernElement(ScadaElementKind kind, double x, double y, ScadaShapeKind? shapeKindOverride = null)
    {
        if (kind == ScadaElementKind.Text)
        {
            var sequence = _nextTextSequence++;
            var id = CreateUniqueElementId($"text_{sequence:000}");
            return ScadaElement.CreateText(id, $"Text{sequence:000}", x, y);
        }

        if (kind == ScadaElementKind.InputNumeric)
        {
            var sequence = _nextInputNumericSequence++;
            var id = CreateUniqueElementId($"input_numeric_{sequence:000}");
            return ScadaElement.CreateInputNumeric(id, $"InputNumeric{sequence:000}", x, y);
        }

        if (kind == ScadaElementKind.Shape)
        {
            var sequence = _nextShapeSequence++;
            var shapeKind = shapeKindOverride ?? _pendingInsertShapeKind ?? ScadaShapeKind.Rectangle;
            var id = CreateUniqueElementId($"shape_{sequence:000}");
            return ScadaElement.CreateShape(id, $"{FormatShapeName(shapeKind)}{sequence:000}", shapeKind, x, y);
        }

        if (kind == ScadaElementKind.Button)
        {
            var sequence = _nextButtonSequence++;
            var buttonKind = _pendingInsertButtonKind ?? ScadaButtonKind.Command;
            var id = CreateUniqueElementId($"button_{sequence:000}");
            return ScadaElement.CreateButton(id, $"{FormatButtonName(buttonKind)}{sequence:000}", x, y, buttonKind);
        }

        var textSequence = _nextInputTextSequence++;
        var inputTextId = CreateUniqueElementId($"input_text_{textSequence:000}");
        return ScadaElement.CreateInputText(inputTextId, $"InputText{textSequence:000}", x, y);
    }

    private static string FormatShapeName(ScadaShapeKind shapeKind)
    {
        return shapeKind switch
        {
            ScadaShapeKind.RoundedRectangle => "RectangleArrondi",
            ScadaShapeKind.Ellipse => "Ellipse",
            ScadaShapeKind.Circle => "Cercle",
            ScadaShapeKind.Triangle => "Triangle",
            ScadaShapeKind.Star => "Etoile",
            ScadaShapeKind.Line => "Ligne",
            ScadaShapeKind.Arrow => "Fleche",
            ScadaShapeKind.IndicatorLamp => "Voyant",
            ScadaShapeKind.HorizontalBar => "BarreHorizontale",
            ScadaShapeKind.VerticalBar => "BarreVerticale",
            ScadaShapeKind.Tank => "Reservoir",
            ScadaShapeKind.PipeHorizontal => "TuyauHorizontal",
            ScadaShapeKind.PipeVertical => "TuyauVertical",
            ScadaShapeKind.Valve => "Vanne",
            ScadaShapeKind.Pump => "Pompe",
            ScadaShapeKind.Motor => "Moteur",
            ScadaShapeKind.Fan => "Ventilateur",
            ScadaShapeKind.Conveyor => "Convoyeur",
            ScadaShapeKind.Gauge => "Jauge",
            ScadaShapeKind.Switch => "Interrupteur",
            ScadaShapeKind.Breaker => "Disjoncteur",
            ScadaShapeKind.Transformer => "Transformateur",
            ScadaShapeKind.AlarmBeacon => "BaliseAlarme",
            _ => "Rectangle"
        };
    }

    private static string FormatShapeLabel(ScadaShapeKind shapeKind)
    {
        return shapeKind switch
        {
            ScadaShapeKind.RoundedRectangle => "rectangle arrondi",
            ScadaShapeKind.Ellipse => "ellipse",
            ScadaShapeKind.Circle => "cercle",
            ScadaShapeKind.Triangle => "triangle",
            ScadaShapeKind.Star => "etoile",
            ScadaShapeKind.Line => "ligne",
            ScadaShapeKind.Arrow => "fleche",
            ScadaShapeKind.IndicatorLamp => "voyant HMI",
            ScadaShapeKind.HorizontalBar => "barre horizontale HMI",
            ScadaShapeKind.VerticalBar => "barre verticale HMI",
            ScadaShapeKind.Tank => "reservoir HMI",
            ScadaShapeKind.PipeHorizontal => "tuyau horizontal HMI",
            ScadaShapeKind.PipeVertical => "tuyau vertical HMI",
            ScadaShapeKind.Valve => "vanne HMI",
            ScadaShapeKind.Pump => "pompe HMI",
            ScadaShapeKind.Motor => "moteur HMI",
            ScadaShapeKind.Fan => "ventilateur HMI",
            ScadaShapeKind.Conveyor => "convoyeur HMI",
            ScadaShapeKind.Gauge => "jauge HMI",
            ScadaShapeKind.Switch => "interrupteur electrique HMI",
            ScadaShapeKind.Breaker => "disjoncteur HMI",
            ScadaShapeKind.Transformer => "transformateur HMI",
            ScadaShapeKind.AlarmBeacon => "balise alarme HMI",
            _ => "rectangle"
        };
    }

    private static string FormatButtonName(ScadaButtonKind buttonKind)
    {
        return buttonKind switch
        {
            ScadaButtonKind.Toggle => "Toggle",
            ScadaButtonKind.Navigation => "Navigation",
            ScadaButtonKind.AlarmAcknowledge => "Acquitter",
            ScadaButtonKind.EmergencyStop => "ArretUrgence",
            _ => "Bouton"
        };
    }

    private static string FormatButtonLabel(ScadaButtonKind buttonKind)
    {
        return buttonKind switch
        {
            ScadaButtonKind.Toggle => "bouton bascule",
            ScadaButtonKind.Navigation => "bouton navigation",
            ScadaButtonKind.AlarmAcknowledge => "bouton acquittement alarme",
            ScadaButtonKind.EmergencyStop => "bouton arret d'urgence",
            _ => "bouton"
        };
    }

    private void ResetElementSequences(ScadaScene scene)
    {
        var elements = FlattenElements(scene.Elements).ToArray();
        _nextTextSequence = elements.Count(element => element.Kind == ScadaElementKind.Text && !element.IsImportedFromLegacy) + 1;
        _nextInputTextSequence = elements.Count(element => element.Kind == ScadaElementKind.InputText) + 1;
        _nextInputNumericSequence = elements.Count(element => element.Kind == ScadaElementKind.InputNumeric) + 1;
        _nextShapeSequence = elements.Count(element => element.Kind == ScadaElementKind.Shape && !element.IsImportedFromLegacy) + 1;
        _nextButtonSequence = elements.Count(element => element.Kind == ScadaElementKind.Button && !element.IsImportedFromLegacy) + 1;
        _nextGroupSequence = elements.Count(element => element.Kind == ScadaElementKind.Group) + 1;
    }

    private void SelectModernElement(string? id, bool additive = false, bool toggle = false)
    {
        if (_activeScene is null || string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var selected = _activeScene.FindElementRecursive(id);
        if (selected is null)
        {
            return;
        }

        if (!additive && !toggle)
        {
            _selectedSceneObjectIds.Clear();
        }

        if (toggle && _selectedSceneObjectIds.Contains(id))
        {
            _selectedSceneObjectIds.Remove(id);
            _selectedSceneObject = _selectedSceneObjectIds.Count == 0
                ? null
                : _activeScene.FindElementRecursive(_selectedSceneObjectIds.Last());
        }
        else
        {
            _selectedSceneObjectIds.Add(id);
            _selectedSceneObject = selected;
        }

        _selectedSourceObjectIds.Clear();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        _ = ExecuteLegacyViewerCommandAsync(new LegacyViewerCommand("selectObject", Ids: _selectedSceneObjectIds.ToArray()));
    }

    private void UpdateModernElementGeometry(
        string? id,
        double x,
        double y,
        double width,
        double height,
        double beforeX,
        double beforeY,
        double beforeWidth,
        double beforeHeight)
    {
        if (_activeScene is null || string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var current = _activeScene.FindElementRecursive(id);
        if (current is null)
        {
            return;
        }

        var beforeBounds = new SceneBounds(
            Math.Max(0, Math.Round(beforeX)),
            Math.Max(0, Math.Round(beforeY)),
            Math.Max(8, Math.Round(beforeWidth)),
            Math.Max(8, Math.Round(beforeHeight)));
        if (!HasUsableGeometrySnapshot(beforeBounds))
        {
            beforeBounds = current.Bounds;
        }

        var afterBounds = new SceneBounds(
            Math.Max(0, Math.Round(x)),
            Math.Max(0, Math.Round(y)),
            Math.Max(8, Math.Round(width)),
            Math.Max(8, Math.Round(height)));

        if (BoundsEqual(current.Bounds, afterBounds))
        {
            return;
        }

        var updated = current with
        {
            Bounds = afterBounds
        };

        _activeScene = _activeScene.WithReplacedElementRecursive(updated);
        _selectedSceneObject = updated;
        _selectedSceneObjectIds.Add(updated.Id);
        if (!BoundsEqual(beforeBounds, afterBounds))
        {
            _activeSceneTab?.History.Push(new ModernElementBoundsChangedAction(
                _activeScene.Id,
                updated.Id,
                beforeBounds,
                afterBounds));
        }

        MarkActiveSceneDirty();
        RefreshModernSceneUi();
        _ = RenderModernSceneAsync();
        SetStatus($"{updated.UserLabel}: position {updated.Bounds.X:0},{updated.Bounds.Y:0}, taille {updated.Bounds.Width:0}x{updated.Bounds.Height:0}.");
    }

    private async Task MoveSelectionByAsync(LegacyViewerMessage message)
    {
        if (string.Equals(message.TargetKind, "source", StringComparison.OrdinalIgnoreCase))
        {
            await MoveSourceSelectionByAsync(message);
            return;
        }

        await MoveSceneObjectSelectionByAsync(message);
    }

    private async Task MoveSourceSelectionByAsync(LegacyViewerMessage message)
    {
        if (_activeScene is null)
        {
            return;
        }

        var deltaX = Math.Round(message.DeltaX);
        var deltaY = Math.Round(message.DeltaY);
        if (deltaX == 0 && deltaY == 0)
        {
            return;
        }

        var selectedSourceIds = ResolveSourceSelectionIds(message).ToArray();
        var selectedElements = selectedSourceIds
            .Select(id => _activeScene.FindLegacyStaticBySourceElementId(id))
            .Where(element => element is not null)
            .Select(element => element!)
            .ToArray();
        if (selectedElements.Length == 0)
        {
            return;
        }

        var updatedScene = _activeScene;
        var updatedElements = new List<ScadaElement>();
        var movedBounds = new List<MovedSceneElementBounds>();
        foreach (var element in selectedElements)
        {
            var updated = element with
            {
                Bounds = new SceneBounds(
                    Math.Max(0, Math.Round(element.Bounds.X + deltaX)),
                    Math.Max(0, Math.Round(element.Bounds.Y + deltaY)),
                    Math.Max(1, Math.Round(element.Bounds.Width)),
                    Math.Max(1, Math.Round(element.Bounds.Height)))
            };
            updatedScene = updatedScene.WithReplacedElementRecursive(updated);
            updatedElements.Add(updated);
            movedBounds.Add(new MovedSceneElementBounds(element.Id, element.Bounds, updated.Bounds));
        }

        _activeScene = updatedScene;
        _selectedSourceObjectIds.Clear();
        foreach (var sourceId in selectedSourceIds)
        {
            _selectedSourceObjectIds.Add(sourceId);
        }

        _selectedSceneObject = null;
        _selectedSceneObjectIds.Clear();
        _activeSceneTab?.History.Push(new SceneSelectionMovedAction(
            updatedScene.Id,
            movedBounds,
            "deplacement selection"));
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        await ApplySourceElementBoundsInViewerAsync(updatedElements);
        SetStatus($"{updatedElements.Count} element(s) deplace(s): {deltaX:0},{deltaY:0}. Sauvegarde requise.");
    }

    private async Task MoveSceneObjectSelectionByAsync(LegacyViewerMessage message)
    {
        if (_activeScene is null)
        {
            return;
        }

        var deltaX = Math.Round(message.DeltaX);
        var deltaY = Math.Round(message.DeltaY);
        if (deltaX == 0 && deltaY == 0)
        {
            return;
        }

        var selectedIds = NormalizeSceneObjectIdsForMove(ResolveSceneObjectSelectionIds(message)).ToArray();
        var selectedElements = selectedIds
            .Select(id => _activeScene.FindElementRecursive(id))
            .Where(element => element is not null)
            .Select(element => element!)
            .Where(element => !selectedIds.Any(id =>
                !string.Equals(id, element.Id, StringComparison.Ordinal) &&
                _activeScene.FindElementRecursive(id) is { } selectedParent &&
                ContainsElement(selectedParent, element.Id)))
            .ToArray();
        if (selectedElements.Length == 0)
        {
            return;
        }

        var updatedScene = _activeScene;
        var movedBounds = new List<MovedSceneElementBounds>();
        foreach (var element in selectedElements)
        {
            var updated = element with
            {
                Bounds = new SceneBounds(
                    Math.Max(0, Math.Round(element.Bounds.X + deltaX)),
                    Math.Max(0, Math.Round(element.Bounds.Y + deltaY)),
                    Math.Max(1, Math.Round(element.Bounds.Width)),
                    Math.Max(1, Math.Round(element.Bounds.Height)))
            };
            updatedScene = updatedScene.WithReplacedElementRecursive(updated);
            movedBounds.Add(new MovedSceneElementBounds(element.Id, element.Bounds, updated.Bounds));
        }

        _activeScene = updatedScene;
        _selectedSceneObjectIds.Clear();
        foreach (var id in selectedIds)
        {
            if (_activeScene.FindElementRecursive(id) is not null)
            {
                _selectedSceneObjectIds.Add(id);
            }
        }

        _selectedSceneObject = _selectedSceneObjectIds.Count == 0
            ? null
            : _activeScene.FindElementRecursive(_selectedSceneObjectIds.Last());
        _selectedSourceObjectIds.Clear();
        _activeSceneTab?.History.Push(new SceneSelectionMovedAction(
            updatedScene.Id,
            movedBounds,
            "deplacement selection"));
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        await RenderModernSceneAsync();
        SetStatus($"{selectedElements.Length} objet(s) deplace(s): {deltaX:0},{deltaY:0}. Sauvegarde requise.");
    }

    private IReadOnlyList<string> ResolveSourceSelectionIds(LegacyViewerMessage message)
    {
        var ids = (message.Ids ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToArray();
        if (ids.Length > 0)
        {
            return ids;
        }

        ids = (message.Items ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .Select(item => item.Id.Trim())
            .ToArray();
        return ids.Length > 0
            ? ids
            : _selectedSourceObjectIds.ToArray();
    }

    private IReadOnlyList<string> ResolveSceneObjectSelectionIds(LegacyViewerMessage message)
    {
        var ids = (message.Ids ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToArray();
        if (ids.Length > 0)
        {
            return ids;
        }

        if (!string.IsNullOrWhiteSpace(message.Id))
        {
            return [message.Id.Trim()];
        }

        return _selectedSceneObjectIds.ToArray();
    }

    private IReadOnlyList<string> NormalizeSceneObjectIdsForMove(IEnumerable<string> ids)
    {
        if (_activeScene is null)
        {
            return [];
        }

        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => NormalizeSceneObjectIdForMove(id.Trim()))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private string NormalizeSceneObjectIdForMove(string id)
    {
        if (_activeScene is null)
        {
            return id;
        }

        var element = _activeScene.FindElementRecursive(id);
        if (element is null || element.Kind == ScadaElementKind.Group)
        {
            return id;
        }

        var parent = _activeScene.FindParentOf(id);
        return parent?.Kind == ScadaElementKind.Group ? parent.Id : id;
    }

    private static bool HasUsableGeometrySnapshot(SceneBounds bounds)
    {
        return !double.IsNaN(bounds.X) &&
            !double.IsNaN(bounds.Y) &&
            !double.IsNaN(bounds.Width) &&
            !double.IsNaN(bounds.Height) &&
            bounds.Width > 0 &&
            bounds.Height > 0;
    }

    private static bool BoundsEqual(SceneBounds left, SceneBounds right)
    {
        return left.X.Equals(right.X) &&
            left.Y.Equals(right.Y) &&
            left.Width.Equals(right.Width) &&
            left.Height.Equals(right.Height);
    }

    private void UpdateModernElementProperties(LegacyViewerMessage message)
    {
        if (_activeScene is null || string.IsNullOrWhiteSpace(message.Id))
        {
            return;
        }

        var current = _activeScene.FindElementRecursive(message.Id);
        if (current is null)
        {
            return;
        }

        var style = current.Style ?? (current.Kind == ScadaElementKind.Text ? ScadaElementStyle.DefaultText : ScadaElementStyle.DefaultInput);
        var data = current.Data ?? new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
        var updated = current with
        {
            DisplayName = string.IsNullOrWhiteSpace(message.DisplayName) ? current.DisplayName : message.DisplayName.Trim(),
            Bounds = new SceneBounds(
                Math.Max(0, message.X),
                Math.Max(0, message.Y),
                Math.Max(8, message.Width),
                Math.Max(8, message.Height)),
            Style = style with
            {
                Background = string.IsNullOrWhiteSpace(message.Background) ? style.Background : message.Background,
                FontSize = message.FontSize > 0 ? message.FontSize : style.FontSize,
                BorderWidth = message.BorderWidth >= 0 ? message.BorderWidth : style.BorderWidth,
                BorderStyle = string.IsNullOrWhiteSpace(message.BorderStyle) ? style.BorderStyle : message.BorderStyle
            },
            Data = data with
            {
                Placeholder = string.IsNullOrWhiteSpace(message.Placeholder) ? data.Placeholder : message.Placeholder,
                Text = current.Kind is ScadaElementKind.InputText or ScadaElementKind.Text or ScadaElementKind.Button ? message.Text ?? data.Text : data.Text,
                Value = current.Kind == ScadaElementKind.InputNumeric ? ParseNullableDouble(message.ValueText ?? "") : data.Value,
                Minimum = ParseNullableDouble(message.MinimumText ?? ""),
                Maximum = ParseNullableDouble(message.MaximumText ?? ""),
                Decimals = ParseNullableInt(message.DecimalsText ?? ""),
                Unit = string.IsNullOrWhiteSpace(message.Unit) ? null : message.Unit,
                DisplayFormat = string.IsNullOrWhiteSpace(message.DisplayFormat) ? null : message.DisplayFormat,
                TagBinding = string.IsNullOrWhiteSpace(message.TagBinding) ? null : message.TagBinding,
                IsReadOnly = message.IsReadOnly
            },
            ButtonBehavior = current.Kind == ScadaElementKind.Button
                ? new ScadaButtonBehavior(
                    message.ButtonDisabled,
                    new ScadaButtonHoverStyle(
                        message.ButtonHoverEnabled,
                        string.IsNullOrWhiteSpace(message.ButtonHoverBackground) ? ScadaButtonHoverStyle.Default.Background : message.ButtonHoverBackground,
                        string.IsNullOrWhiteSpace(message.ButtonHoverForeground) ? ScadaButtonHoverStyle.Default.Foreground : message.ButtonHoverForeground,
                        string.IsNullOrWhiteSpace(message.ButtonHoverBorderColor) ? ScadaButtonHoverStyle.Default.BorderColor : message.ButtonHoverBorderColor),
                    new ScadaButtonPressedStyle(
                        message.ButtonPressedEnabled,
                        string.IsNullOrWhiteSpace(message.ButtonPressedBackground) ? ScadaButtonPressedStyle.Default.Background : message.ButtonPressedBackground,
                        string.IsNullOrWhiteSpace(message.ButtonPressedForeground) ? ScadaButtonPressedStyle.Default.Foreground : message.ButtonPressedForeground,
                        string.IsNullOrWhiteSpace(message.ButtonPressedBorderColor) ? ScadaButtonPressedStyle.Default.BorderColor : message.ButtonPressedBorderColor))
                : current.ButtonBehavior
        };

        if (Equals(current, updated))
        {
            return;
        }

        CommitModernElementProperties(current, updated);
    }

    private void OnDeleteSelectedModernElementClick(object sender, RoutedEventArgs e)
    {
        _ = DeleteSelectedSceneObjectsAsync(_selectedSceneObject?.Id);
    }

    private async Task DeleteModernElement(string? id)
    {
        await DeleteSelectedSceneObjectsAsync(id);
    }

    private async Task DeleteSelectedModernElements(string? fallbackId = null)
    {
        await DeleteSelectedSceneObjectsAsync(fallbackId);
    }

    private async Task DeleteModernElements(IReadOnlyCollection<string> ids)
    {
        foreach (var id in ids.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            _selectedSceneObjectIds.Add(id);
        }

        await DeleteSelectedSceneObjectsAsync();
    }

    private void RefreshModernSceneUi()
    {
        if (_activeScene is null)
        {
            ModernSelectionSummaryText.Text = "Aucun objet V2 selectionne";
            SelectedObjectPropertiesPanel.Visibility = Visibility.Collapsed;
            return;
        }

        if (_activeSceneTab is not null)
        {
            _activeSceneTab.IsDirty = _activeSceneDirty;
        }

        if (_selectedSourceObjectIds.Count > 0)
        {
            var snapshot = LegacyElementSelectionSnapshot.FromInventory(_sourceObjects, _selectedSourceObjectIds);
            ModernSelectionSummaryText.Text = $"Legacy: {snapshot.PropertySummary}";
            LoadElementProperties(null);
            return;
        }

        if (_selectedSceneObject is null)
        {
            ModernSelectionSummaryText.Text = $"{FlattenElements(_activeScene.Elements).Count()} objet(s) V2 dans la scene";
            LoadElementProperties(null);
            return;
        }

        ModernSelectionSummaryText.Text = _selectedSceneObjectIds.Count > 1
            ? $"{_selectedSceneObjectIds.Count} objets V2 selectionnes - actif: {_selectedSceneObject.UserLabel} ({_selectedSceneObject.Kind})"
            : $"{_selectedSceneObject.UserLabel} ({_selectedSceneObject.Kind})";
        LoadElementProperties(_selectedSceneObject);
    }

    private void LoadElementProperties(ScadaElement? element)
    {
        _isUpdatingElementProperties = true;
        try
        {
            var isEnabled = element is not null;
            SelectedObjectPropertiesPanel.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
            ElementNameTextBox.IsEnabled = isEnabled;
            ElementReadOnlyCheckBox.IsEnabled = isEnabled;
            ElementPositionModeComboBox.IsEnabled = isEnabled;
            ElementXTextBox.IsEnabled = isEnabled;
            ElementYTextBox.IsEnabled = isEnabled;
            ElementWidthTextBox.IsEnabled = isEnabled;
            ElementHeightTextBox.IsEnabled = isEnabled;
            ElementFontFamilyComboBox.IsEnabled = isEnabled;
            ElementFontSizeTextBox.IsEnabled = isEnabled;
            ElementBackgroundColorPicker.IsEnabled = isEnabled;
            ElementBorderColorPicker.IsEnabled = isEnabled;
            ElementBorderStyleComboBox.IsEnabled = isEnabled;
            ElementBorderWidthTextBox.IsEnabled = isEnabled;
            ElementOpacityTextBox.IsEnabled = isEnabled;
            ElementRotationTextBox.IsEnabled = isEnabled;
            ElementAdvancedCssTextBox.IsEnabled = isEnabled;
            ButtonContextTab.Visibility = element?.Kind == ScadaElementKind.Button ? Visibility.Visible : Visibility.Collapsed;
            ButtonDisabledCheckBox.IsEnabled = element?.Kind == ScadaElementKind.Button;
            ButtonHoverEnabledCheckBox.IsEnabled = element?.Kind == ScadaElementKind.Button;
            ButtonHoverBackgroundColorPicker.IsEnabled = element?.Kind == ScadaElementKind.Button;
            ButtonHoverForegroundColorPicker.IsEnabled = element?.Kind == ScadaElementKind.Button;
            ButtonHoverBorderColorPicker.IsEnabled = element?.Kind == ScadaElementKind.Button;
            ButtonPressedEnabledCheckBox.IsEnabled = element?.Kind == ScadaElementKind.Button;
            ButtonPressedBackgroundColorPicker.IsEnabled = element?.Kind == ScadaElementKind.Button;
            ButtonPressedForegroundColorPicker.IsEnabled = element?.Kind == ScadaElementKind.Button;
            ButtonPressedBorderColorPicker.IsEnabled = element?.Kind == ScadaElementKind.Button;
            ElementPlaceholderTextBox.IsEnabled = isEnabled;
            ElementValueTextBox.IsEnabled = isEnabled;
            var canEditNumericInputConstraints = isEnabled
                && element?.Kind == ScadaElementKind.InputNumeric
                && element.Data?.IsReadOnly != true;
            ElementMinTextBox.IsEnabled = canEditNumericInputConstraints;
            ElementMaxTextBox.IsEnabled = canEditNumericInputConstraints;
            ElementDecimalsTextBox.IsEnabled = isEnabled;
            ElementUnitTextBox.IsEnabled = isEnabled;
            ElementFormatTextBox.IsEnabled = isEnabled;
            ElementTagBindingTextBox.IsEnabled = isEnabled;
            OpenElementEventsButton.IsEnabled = isEnabled;

            if (element is null)
            {
                ElementNameTextBox.Text = "";
                ElementReadOnlyCheckBox.IsChecked = false;
                ElementPositionModeComboBox.SelectedIndex = 0;
                ElementXTextBox.Text = "";
                ElementYTextBox.Text = "";
                ElementWidthTextBox.Text = "";
                ElementHeightTextBox.Text = "";
                ElementFontFamilyComboBox.SelectedIndex = 0;
                ElementFontSizeTextBox.Text = "";
                ElementBackgroundColorPicker.SetColor("#FFFFFF");
                ElementBorderColorPicker.SetColor("#8AA0A6");
                ElementBorderStyleComboBox.SelectedIndex = 0;
                ElementBorderWidthTextBox.Text = "";
                ShadowNoneRadio.IsChecked = true;
                ElementOpacityTextBox.Text = "";
                ElementRotationTextBox.Text = "";
                ElementAdvancedCssTextBox.Text = "";
                ButtonDisabledCheckBox.IsChecked = false;
                ButtonHoverEnabledCheckBox.IsChecked = true;
                ButtonHoverBackgroundColorPicker.SetColor(ScadaButtonHoverStyle.Default.Background);
                ButtonHoverForegroundColorPicker.SetColor(ScadaButtonHoverStyle.Default.Foreground);
                ButtonHoverBorderColorPicker.SetColor(ScadaButtonHoverStyle.Default.BorderColor);
                ButtonPressedEnabledCheckBox.IsChecked = true;
                ButtonPressedBackgroundColorPicker.SetColor(ScadaButtonPressedStyle.Default.Background);
                ButtonPressedForegroundColorPicker.SetColor(ScadaButtonPressedStyle.Default.Foreground);
                ButtonPressedBorderColorPicker.SetColor(ScadaButtonPressedStyle.Default.BorderColor);
                ElementPlaceholderTextBox.Text = "";
                ElementValueTextBox.Text = "";
                ElementMinTextBox.Text = "";
                ElementMaxTextBox.Text = "";
                ElementDecimalsTextBox.Text = "";
                ElementUnitTextBox.Text = "";
                ElementFormatTextBox.Text = "";
                ElementTagBindingTextBox.Text = "";
                ElementEventsSummaryText.Text = "Aucun evenement";
                return;
            }

            var style = element.Style ?? (element.Kind == ScadaElementKind.Text ? ScadaElementStyle.DefaultText : ScadaElementStyle.DefaultInput);
            var data = element.Data ?? new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
            ElementNameTextBox.Text = element.DisplayName;
            ElementReadOnlyCheckBox.IsChecked = data.IsReadOnly;
            ElementPositionModeComboBox.SelectedIndex = element.Layout?.PositionMode == ElementPositionMode.Relative ? 1 : 0;
            ElementXTextBox.Text = element.Bounds.X.ToString("0.##");
            ElementYTextBox.Text = element.Bounds.Y.ToString("0.##");
            ElementWidthTextBox.Text = element.Bounds.Width.ToString("0.##");
            ElementHeightTextBox.Text = element.Bounds.Height.ToString("0.##");
            SelectComboBoxText(ElementFontFamilyComboBox, style.FontFamily);
            ElementFontSizeTextBox.Text = style.FontSize.ToString("0.##");
            ElementBackgroundColorPicker.SetColor(style.Background);
            ElementBorderColorPicker.SetColor(style.BorderColor);
            SelectComboBoxText(ElementBorderStyleComboBox, style.BorderStyle);
            ElementBorderWidthTextBox.Text = style.BorderWidth.ToString("0.##");
            ShadowNoneRadio.IsChecked = style.ShadowPreset == "None";
            ShadowSoftRadio.IsChecked = style.ShadowPreset == "Soft";
            ShadowRaisedRadio.IsChecked = style.ShadowPreset == "Raised";
            ShadowInsetRadio.IsChecked = style.ShadowPreset == "Inset";
            ElementOpacityTextBox.Text = style.Opacity.ToString("0.##");
            ElementRotationTextBox.Text = style.Rotation.ToString("0.##");
            ElementAdvancedCssTextBox.Text = style.AdvancedCss ?? "";
            var buttonBehavior = element.EffectiveButtonBehavior;
            var hoverStyle = buttonBehavior.EffectiveHover;
            var pressedStyle = buttonBehavior.EffectivePressed;
            ButtonDisabledCheckBox.IsChecked = buttonBehavior.IsDisabled;
            ButtonHoverEnabledCheckBox.IsChecked = hoverStyle.Enabled;
            ButtonHoverBackgroundColorPicker.SetColor(hoverStyle.Background);
            ButtonHoverForegroundColorPicker.SetColor(hoverStyle.Foreground);
            ButtonHoverBorderColorPicker.SetColor(hoverStyle.BorderColor);
            ButtonPressedEnabledCheckBox.IsChecked = pressedStyle.Enabled;
            ButtonPressedBackgroundColorPicker.SetColor(pressedStyle.Background);
            ButtonPressedForegroundColorPicker.SetColor(pressedStyle.Foreground);
            ButtonPressedBorderColorPicker.SetColor(pressedStyle.BorderColor);
            ElementPlaceholderTextBox.Text = data.Placeholder ?? "";
            ElementValueTextBox.Text = data.Value?.ToString("0.##") ?? data.Text ?? "";
            ElementMinTextBox.Text = data.Minimum?.ToString("0.##") ?? "";
            ElementMaxTextBox.Text = data.Maximum?.ToString("0.##") ?? "";
            ElementDecimalsTextBox.Text = data.Decimals?.ToString() ?? "";
            ElementUnitTextBox.Text = data.Unit ?? "";
            ElementFormatTextBox.Text = data.DisplayFormat ?? "";
            ElementTagBindingTextBox.Text = data.TagBinding ?? "";
            ElementEventsSummaryText.Text = FormatElementEventsSummary(element);
        }
        finally
        {
            _isUpdatingElementProperties = false;
        }
    }

    private void OnOpenSelectedElementEventsClick(object sender, RoutedEventArgs e)
    {
        OpenElementEventDialog(_selectedSceneObject?.Id);
    }

    private static ScadaElement BuildUpdatedElementFromDialog(
        ScadaElement current,
        ElementPropertiesDialogResult result)
    {
        var style = current.Style ?? (current.Kind == ScadaElementKind.Text ? ScadaElementStyle.DefaultText : ScadaElementStyle.DefaultInput);
        var data = current.Data ?? new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
        return current with
        {
            DisplayName = result.DisplayName,
            Bounds = result.Bounds,
            Layout = new ScadaElementLayout(result.PositionMode, current.Layout?.RelativeToElementId),
            Style = style with
            {
                FontFamily = result.FontFamily,
                FontSize = result.FontSize,
                Background = result.Background,
                BorderColor = result.BorderColor,
                BorderStyle = result.BorderStyle,
                BorderWidth = result.BorderWidth,
                ShadowPreset = result.ShadowPreset,
                Opacity = result.Opacity,
                Rotation = result.Rotation,
                AdvancedCss = result.AdvancedCss
            },
            ButtonBehavior = current.Kind == ScadaElementKind.Button
                ? new ScadaButtonBehavior(
                    result.ButtonDisabled,
                    new ScadaButtonHoverStyle(
                        result.ButtonHoverEnabled,
                        result.ButtonHoverBackground,
                        result.ButtonHoverForeground,
                        result.ButtonHoverBorderColor),
                    new ScadaButtonPressedStyle(
                        result.ButtonPressedEnabled,
                        result.ButtonPressedBackground,
                        result.ButtonPressedForeground,
                        result.ButtonPressedBorderColor))
                : current.ButtonBehavior,
            Data = data with
            {
                Placeholder = result.Placeholder,
                Text = current.Kind is ScadaElementKind.InputText or ScadaElementKind.Text or ScadaElementKind.Button
                    ? result.Text
                    : data.Text,
                Value = current.Kind == ScadaElementKind.InputNumeric ? result.Value : data.Value,
                Minimum = result.Minimum,
                Maximum = result.Maximum,
                Decimals = result.Decimals,
                Unit = result.Unit,
                DisplayFormat = result.DisplayFormat,
                TagBinding = result.TagBinding,
                IsReadOnly = result.IsReadOnly
            }
        };
    }

    private void CommitModernElementProperties(ScadaElement current, ScadaElement updated)
    {
        if (_activeScene is null || Equals(current, updated))
        {
            return;
        }

        _activeScene = _activeScene.WithReplacedElementRecursive(updated);
        _selectedSceneObject = updated;
        _selectedSceneObjectIds.Add(updated.Id);
        _activeSceneTab?.History.Push(new ModernElementChangedAction(
            _activeScene.Id,
            current,
            updated,
            "proprietes Element+"));
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        _ = RenderModernSceneAsync();
        SetStatus($"{updated.UserLabel} mis a jour. Sauvegarde requise.");
    }

    private void OpenElementEventDialog(string? elementId, Window? owner = null)
    {
        if (_activeScene is null || string.IsNullOrWhiteSpace(elementId))
        {
            SetStatus("Aucun Element+ selectionne pour les evenements.");
            return;
        }

        var current = _activeScene.FindElementRecursive(elementId);
        if (current is null || current.IsLegacyStatic)
        {
            SetStatus("Les evenements runtime sont disponibles sur les objets Element+ convertis.");
            return;
        }

        var dialog = new ElementEventDialog(
            current,
            _activeScene.ActionDefinitions,
            _activeScene.Elements,
            GetCurrentSceneReferences(),
            _modernProject?.TagCatalog)
        {
            Owner = owner ?? this
        };

        dialog.AddEvent = result => AddElementEventFromDialog(current.Id, result, dialog);
        dialog.DeleteEvent = request => DeleteElementEventFromDialog(current.Id, request, dialog);
        dialog.CreateTag = () => "Creation de tags disponible dans une prochaine revision apres import des protocoles projet.";
        dialog.ShowDialog();
    }

    private string AddElementEventFromDialog(
        string elementId,
        ElementEventDialogResult result,
        ElementEventDialog? dialog)
    {
        if (_activeScene is null)
        {
            SetStatus("Aucune scene active pour ajouter l'evenement.");
            return "Aucune scene active.";
        }

        var current = _activeScene.FindElementRecursive(elementId);
        if (current is null || current.IsLegacyStatic)
        {
            SetStatus("Element+ introuvable pour ajouter l'evenement.");
            return "Element+ introuvable.";
        }

        var beforeScene = _activeScene;
        var changeLabel = "evenement Element+";
        var updated = result.FunctionName switch
        {
            var functionName when string.Equals(functionName, ScadaEventRegistry.ChangePageFunction, StringComparison.Ordinal) =>
                _activeScene.WithChangePageEvent(
                    current.Id,
                    result.RuntimeTrigger ?? "",
                    result.TargetPageId ?? ""),
            var functionName when string.Equals(functionName, ScadaEventRegistry.OpenPopupFunction, StringComparison.Ordinal) =>
                _activeScene.WithOpenPopupEvent(
                    current.Id,
                    result.RuntimeTrigger ?? "",
                    result.TargetPageId ?? ""),
            var functionName when string.Equals(functionName, ScadaEventRegistry.ClosePopupFunction, StringComparison.Ordinal) =>
                _activeScene.WithClosePopupEvent(
                    current.Id,
                    result.RuntimeTrigger ?? "",
                    result.TargetPageId ?? ""),
            var functionName when string.Equals(functionName, ScadaEventRegistry.TogglePopupFunction, StringComparison.Ordinal) =>
                _activeScene.WithTogglePopupEvent(
                    current.Id,
                    result.RuntimeTrigger ?? "",
                    result.TargetPageId ?? ""),
            var functionName when string.Equals(functionName, ScadaEventRegistry.ReadValueFunction, StringComparison.Ordinal) =>
                _activeScene.WithValueBinding(
                    current.Id,
                    readTagId: result.TagId ?? ""),
            var functionName when string.Equals(functionName, ScadaEventRegistry.WriteValueFunction, StringComparison.Ordinal) =>
                _activeScene.WithValueBinding(
                    current.Id,
                    writeTagId: result.TagId ?? ""),
            var functionName when string.Equals(functionName, ScadaEventRegistry.ShowFunction, StringComparison.Ordinal) =>
                _activeScene.WithObjectVisibilityEvent(
                    current.Id,
                    result.RuntimeTrigger ?? "",
                    ScadaActionKind.Show,
                    result.TargetElementId ?? "",
                    result.Condition),
            var functionName when string.Equals(functionName, ScadaEventRegistry.HideFunction, StringComparison.Ordinal) =>
                _activeScene.WithObjectVisibilityEvent(
                    current.Id,
                    result.RuntimeTrigger ?? "",
                    ScadaActionKind.Hide,
                    result.TargetElementId ?? "",
                    result.Condition),
            var functionName when string.Equals(functionName, ScadaEventRegistry.ToggleVisibilityFunction, StringComparison.Ordinal) =>
                _activeScene.WithObjectVisibilityEvent(
                    current.Id,
                    result.RuntimeTrigger ?? "",
                    ScadaActionKind.ToggleVisibility,
                    result.TargetElementId ?? "",
                    result.Condition),
            var functionName when string.Equals(functionName, ScadaEventRegistry.ShowBorderFunction, StringComparison.Ordinal) =>
                _activeScene.WithObjectBorderEvent(
                    current.Id,
                    result.RuntimeTrigger ?? "",
                    ScadaActionKind.SetClass,
                    result.TargetElementId ?? ""),
            var functionName when string.Equals(functionName, ScadaEventRegistry.HideBorderFunction, StringComparison.Ordinal) =>
                _activeScene.WithObjectBorderEvent(
                    current.Id,
                    result.RuntimeTrigger ?? "",
                    ScadaActionKind.RemoveClass,
                    result.TargetElementId ?? ""),
            var functionName when string.Equals(functionName, ScadaEventRegistry.ToggleBorderFunction, StringComparison.Ordinal) =>
                _activeScene.WithObjectBorderEvent(
                    current.Id,
                    result.RuntimeTrigger ?? "",
                    ScadaActionKind.ToggleClass,
                    result.TargetElementId ?? ""),
            var functionName when ScadaEventRegistry.IsVisualEffectFunction(functionName) =>
                _activeScene.WithVisualEffectEvent(
                    current.Id,
                    result.RuntimeTrigger ?? "",
                    result.FunctionName,
                    result.TargetElementId ?? ""),
            _ => throw new InvalidOperationException("Fonction d'evenement non implementee dans cette tranche.")
        };
        if (string.Equals(result.FunctionName, ScadaEventRegistry.ReadValueFunction, StringComparison.Ordinal) ||
            string.Equals(result.FunctionName, ScadaEventRegistry.WriteValueFunction, StringComparison.Ordinal))
        {
            changeLabel = "binding valeur Element+";
        }

        if (Equals(beforeScene, updated))
        {
            SetStatus("Binding deja configure.");
            return "Binding deja configure.";
        }

        _activeScene = updated;
        _selectedSceneObject = _activeScene.FindElementRecursive(current.Id);
        _selectedSceneObjectIds.Add(current.Id);
        _activeSceneTab?.History.Push(new SceneSnapshotChangedAction(
            updated.Id,
            beforeScene,
            updated,
            changeLabel));
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        _ = RenderModernSceneAsync();
        dialog?.RefreshExistingEvents(
            _activeScene.FindElementRecursive(current.Id) ?? current,
            _activeScene.ActionDefinitions);
        SetStatus($"Binding Element+ ajoute sur {_selectedSceneObject?.UserLabel ?? current.UserLabel}. Sauvegarde requise.");
        return "Binding ajoute. Vous pouvez en ajouter un autre ou fermer la fenetre.";
    }

    private string DeleteElementEventFromDialog(
        string elementId,
        ElementEventDialogDeleteRequest request,
        ElementEventDialog dialog)
    {
        if (_activeScene is null)
        {
            SetStatus("Aucune scene active pour supprimer l'evenement.");
            return "Aucune scene active.";
        }

        var current = _activeScene.FindElementRecursive(elementId);
        if (current is null || current.IsLegacyStatic)
        {
            SetStatus("Element+ introuvable pour supprimer l'evenement.");
            return "Element+ introuvable.";
        }

        var beforeScene = _activeScene;
        var updated = request.Kind switch
        {
            "read" => _activeScene.WithoutValueBinding(current.Id, ScadaValueBindingKind.Read),
            "write" => _activeScene.WithoutValueBinding(current.Id, ScadaValueBindingKind.Write),
            "event" => _activeScene.WithoutObjectEventAt(current.Id, request.EventIndex),
            _ => _activeScene
        };
        if (Equals(beforeScene, updated))
        {
            SetStatus("Aucun binding selectionne a supprimer.");
            return "Aucun binding selectionne a supprimer.";
        }

        _activeScene = updated;
        _selectedSceneObject = _activeScene.FindElementRecursive(current.Id);
        _selectedSceneObjectIds.Add(current.Id);
        _activeSceneTab?.History.Push(new SceneSnapshotChangedAction(
            updated.Id,
            beforeScene,
            updated,
            "suppression binding Element+"));
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        _ = RenderModernSceneAsync();
        dialog.RefreshExistingEvents(
            _activeScene.FindElementRecursive(current.Id) ?? current,
            _activeScene.ActionDefinitions);
        SetStatus($"Binding supprime sur {_selectedSceneObject?.UserLabel ?? current.UserLabel}. Sauvegarde requise.");
        return "Binding supprime. Vous pouvez continuer ou fermer la fenetre.";
    }

    private void OnElementPropertyChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingElementProperties || _activeScene is null || _selectedSceneObject is null)
        {
            return;
        }

        var current = _selectedSceneObject;
        var style = current.Style ?? (current.Kind == ScadaElementKind.Text ? ScadaElementStyle.DefaultText : ScadaElementStyle.DefaultInput);
        var data = current.Data ?? new ScadaElementData(null, null, null, null, null, null, null, null, null, false);
        var updated = current with
        {
            DisplayName = string.IsNullOrWhiteSpace(ElementNameTextBox.Text) ? current.Id : ElementNameTextBox.Text.Trim(),
            Bounds = new SceneBounds(
                ParseDoubleOrDefault(ElementXTextBox.Text, current.Bounds.X),
                ParseDoubleOrDefault(ElementYTextBox.Text, current.Bounds.Y),
                Math.Max(4, ParseDoubleOrDefault(ElementWidthTextBox.Text, current.Bounds.Width)),
                Math.Max(4, ParseDoubleOrDefault(ElementHeightTextBox.Text, current.Bounds.Height))),
            Layout = new ScadaElementLayout(
                ElementPositionModeComboBox.SelectedIndex == 1 ? ElementPositionMode.Relative : ElementPositionMode.Absolute,
                current.Layout?.RelativeToElementId),
            Style = style with
            {
                FontFamily = GetComboBoxText(ElementFontFamilyComboBox, style.FontFamily),
                FontSize = Math.Max(6, ParseDoubleOrDefault(ElementFontSizeTextBox.Text, style.FontSize)),
                Background = GetColorPickerValue(ElementBackgroundColorPicker, style.Background),
                BorderColor = GetColorPickerValue(ElementBorderColorPicker, style.BorderColor),
                BorderStyle = GetComboBoxText(ElementBorderStyleComboBox, style.BorderStyle),
                BorderWidth = Math.Max(0, ParseDoubleOrDefault(ElementBorderWidthTextBox.Text, style.BorderWidth)),
                ShadowPreset = GetSelectedShadowPreset(),
                Opacity = Math.Clamp(ParseDoubleOrDefault(ElementOpacityTextBox.Text, style.Opacity), 0, 1),
                Rotation = ParseDoubleOrDefault(ElementRotationTextBox.Text, style.Rotation),
                AdvancedCss = string.IsNullOrWhiteSpace(ElementAdvancedCssTextBox.Text) ? null : ElementAdvancedCssTextBox.Text
            },
            ButtonBehavior = current.Kind == ScadaElementKind.Button
                ? new ScadaButtonBehavior(
                    ButtonDisabledCheckBox.IsChecked == true,
                    new ScadaButtonHoverStyle(
                        ButtonHoverEnabledCheckBox.IsChecked == true,
                        GetColorPickerValue(ButtonHoverBackgroundColorPicker, ScadaButtonHoverStyle.Default.Background),
                        GetColorPickerValue(ButtonHoverForegroundColorPicker, ScadaButtonHoverStyle.Default.Foreground),
                        GetColorPickerValue(ButtonHoverBorderColorPicker, ScadaButtonHoverStyle.Default.BorderColor)),
                    new ScadaButtonPressedStyle(
                        ButtonPressedEnabledCheckBox.IsChecked == true,
                        GetColorPickerValue(ButtonPressedBackgroundColorPicker, ScadaButtonPressedStyle.Default.Background),
                        GetColorPickerValue(ButtonPressedForegroundColorPicker, ScadaButtonPressedStyle.Default.Foreground),
                        GetColorPickerValue(ButtonPressedBorderColorPicker, ScadaButtonPressedStyle.Default.BorderColor)))
                : current.ButtonBehavior,
            Data = data with
            {
                Placeholder = string.IsNullOrWhiteSpace(ElementPlaceholderTextBox.Text) ? null : ElementPlaceholderTextBox.Text,
                Text = (current.Kind is ScadaElementKind.InputText or ScadaElementKind.Text or ScadaElementKind.Button) && !string.IsNullOrWhiteSpace(ElementValueTextBox.Text) ? ElementValueTextBox.Text : data.Text,
                Value = current.Kind == ScadaElementKind.InputNumeric ? ParseNullableDouble(ElementValueTextBox.Text) : data.Value,
                Minimum = ParseNullableDouble(ElementMinTextBox.Text),
                Maximum = ParseNullableDouble(ElementMaxTextBox.Text),
                Decimals = ParseNullableInt(ElementDecimalsTextBox.Text),
                Unit = string.IsNullOrWhiteSpace(ElementUnitTextBox.Text) ? null : ElementUnitTextBox.Text,
                DisplayFormat = string.IsNullOrWhiteSpace(ElementFormatTextBox.Text) ? null : ElementFormatTextBox.Text,
                TagBinding = string.IsNullOrWhiteSpace(ElementTagBindingTextBox.Text) ? null : ElementTagBindingTextBox.Text,
                IsReadOnly = ElementReadOnlyCheckBox.IsChecked == true
            }
        };

        if (Equals(current, updated))
        {
            return;
        }

        CommitModernElementProperties(current, updated);
    }

    private async Task RenderModernSceneAsync()
    {
        if (PreviewWebView.CoreWebView2 is null || _activeScene is null)
        {
            return;
        }

        var selectedIds = _selectedSceneObjectIds.ToHashSet(StringComparer.Ordinal);
        if (_selectedSceneObject is not null)
        {
            selectedIds.Add(_selectedSceneObject.Id);
        }

        var payload = _activeScene.Elements
            .Where(element => !element.IsLegacyStatic)
            .Select((element, index) => ToRenderPayload(element, selectedIds, index));
        var json = JsonSerializer.Serialize(payload);
        await PreviewWebView.ExecuteScriptAsync($"window.scadaSceneEditor && window.scadaSceneEditor.renderModernElements({json});");
        await ApplyLegacyTextOverridesAsync(_activeScene.TextOverrides);
    }

    private async Task ApplySourceElementBoundsInViewerAsync(IReadOnlyList<ScadaElement>? elements = null)
    {
        if (PreviewWebView.CoreWebView2 is null || _activeScene is null)
        {
            return;
        }

        var payload = (elements ?? _activeScene.GetLegacyStaticElements())
            .Select(element => new
            {
                Id = element.LegacySource?.SourceElementId,
                element.Bounds.X,
                element.Bounds.Y,
                element.Bounds.Width,
                element.Bounds.Height
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToArray();
        if (payload.Length == 0)
        {
            return;
        }

        var json = JsonSerializer.Serialize(payload);
        await PreviewWebView.ExecuteScriptAsync($"window.scadaSceneEditor && window.scadaSceneEditor.applySourceElementBounds({json});");
    }

    private static ModernElementRenderPayload ToRenderPayload(ScadaElement element, IReadOnlySet<string> selectedIds, int renderIndex)
    {
        return new ModernElementRenderPayload
        {
            Id = element.Id,
            DisplayName = element.DisplayName,
            Kind = element.Kind.ToString(),
            X = element.Bounds.X,
            Y = element.Bounds.Y,
            Width = element.Bounds.Width,
            Height = element.Bounds.Height,
            IsSelected = selectedIds.Contains(element.Id),
            IsGroupContextSelected = element.Kind == ScadaElementKind.Group &&
                element.ChildElements.Any(child => selectedIds.Any(selectedId => ContainsElement(child, selectedId))),
            RenderIndex = renderIndex,
            Style = element.Style,
            Data = element.Data,
            ButtonBehavior = element.ButtonBehavior,
            ShapeKind = element.Kind == ScadaElementKind.Shape ? element.EffectiveShapeKind.ToString() : null,
            ButtonKind = element.Kind == ScadaElementKind.Button ? element.EffectiveButtonKind.ToString() : null,
            Children = element.ChildElements
                .Select((child, childIndex) => ToRenderPayload(child, selectedIds, childIndex))
                .ToArray()
        };
    }

    private static bool ContainsElement(ScadaElement element, string elementId)
    {
        return element.Id == elementId || element.ChildElements.Any(child => ContainsElement(child, elementId));
    }

    private async Task ApplyLegacyTextOverridesAsync(IReadOnlyList<LegacyTextOverride> overrides)
    {
        if (PreviewWebView.CoreWebView2 is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(overrides.Select(overrideItem => new
        {
            Id = overrideItem.SourceElementId,
            overrideItem.Text
        }));
        await PreviewWebView.ExecuteScriptAsync($"window.scadaSceneEditor && window.scadaSceneEditor.applyTextOverrides({json});");
    }

    private async Task ExecuteLegacyViewerCommandAsync(LegacyViewerCommand command)
    {
        if (PreviewWebView.CoreWebView2 is null)
        {
            return;
        }

        var commandJson = JsonSerializer.Serialize(command);
        await PreviewWebView.ExecuteScriptAsync($"window.scadaSceneEditor && window.scadaSceneEditor.command({commandJson});");
    }

    private EditorHistoryContext CreateEditorHistoryContext()
    {
        var sceneId = _activeScene?.Id ?? "";
        return new EditorHistoryContext
        {
            ActiveSceneId = sceneId,
            GetActiveScene = () => _activeScene,
            ReplaceActiveScene = scene =>
            {
                _activeScene = scene;
                if (_activeSceneTab is not null)
                {
                    _activeSceneTab.Scene = scene;
                }

                if (_selectedSceneObject is not null)
                {
                    _selectedSceneObject = scene.FindElementRecursive(_selectedSceneObject.Id);
                }

                var existingSelectedIds = _selectedSceneObjectIds
                    .Where(id => scene.FindElementRecursive(id) is not null)
                    .ToArray();
                _selectedSceneObjectIds.Clear();
                foreach (var id in existingSelectedIds)
                {
                    _selectedSceneObjectIds.Add(id);
                }
            },
            MarkDirty = MarkActiveSceneDirty,
            RefreshPreviewAsync = RefreshPreviewAfterHistoryAsync,
            SetStatus = SetStatus
        };
    }

    private async Task RefreshPreviewAfterHistoryAsync()
    {
        if (_activeScene is null)
        {
            return;
        }

        LoadPageProperties(_activeScene);
        RefreshSelectionUi();
        RefreshModernSceneUi();
        await ApplySceneBackgroundColorAsync(_activeScene.BackgroundColor, updateStatus: false);
        await ResetSourceProjectionVisibilityFromActiveSceneAsync();
        await RenderModernSceneAsync();
        await ApplySourceElementBoundsInViewerAsync();
    }

    private void MarkActiveSceneDirty()
    {
        _activeSceneDirty = true;
        if (_activeSceneTab is not null)
        {
            if (_activeScene is not null)
            {
                _activeSceneTab.Scene = _activeScene;
            }

            _activeSceneTab.IsDirty = true;
        }
    }

    private static double ParseDoubleOrDefault(string value, double fallback)
    {
        return double.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static double? ParseNullableDouble(string value)
    {
        return double.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int? ParseNullableInt(string value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string GetComboBoxText(ComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem is ComboBoxItem item && item.Content is string text
            ? text
            : fallback;
    }

    private static string GetColorPickerValue(ColorPickerField colorPicker, string fallback)
    {
        return string.IsNullOrWhiteSpace(colorPicker.Value) ? fallback : colorPicker.Value.Trim();
    }

    private static void SelectComboBoxText(ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
    }

    private string GetSelectedShadowPreset()
    {
        if (ShadowSoftRadio.IsChecked == true)
        {
            return "Soft";
        }

        if (ShadowRaisedRadio.IsChecked == true)
        {
            return "Raised";
        }

        if (ShadowInsetRadio.IsChecked == true)
        {
            return "Inset";
        }

        return "None";
    }

    private void SetBackgroundColorControls(string cssColor)
    {
        if (!AreBackgroundColorControlsReady())
        {
            return;
        }

        UpdatePreviewSurfaceBackground(cssColor);
        if (TryParseCssColor(cssColor, out var color))
        {
            UpdateBackgroundColorControls(color, updateText: true);
            return;
        }

        BackgroundColorTextBox.Text = cssColor;
    }

    private void UpdatePreviewSurfaceBackground(string cssColor)
    {
        if (PreviewSurfaceBorder is null)
        {
            return;
        }

        if (TryParseCssColor(cssColor, out var color))
        {
            PreviewSurfaceBorder.Background = new SolidColorBrush(color);
        }
    }

    private void UpdatePreviewNativeBackground(string cssColor)
    {
        if (TryParseCssColor(cssColor, out var color))
        {
            PreviewWebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }
    }

    private void UpdateBackgroundColorControls(Color color, bool updateText)
    {
        if (!AreBackgroundColorControlsReady())
        {
            return;
        }

        _isUpdatingBackgroundColorControls = true;
        try
        {
            BackgroundColorPreview.Background = new SolidColorBrush(color);
            (var hue, var saturation, var value) = ToHsv(color);
            _backgroundHue = hue;
            _backgroundSaturation = saturation;
            _backgroundValue = value;
            BackgroundHueSlider.Value = hue;
            HueColorLayer.Fill = new SolidColorBrush(FromHsv(hue, 1, 1));
            UpdateSaturationValueSelector();
            BackgroundRedSlider.Value = color.R;
            BackgroundGreenSlider.Value = color.G;
            BackgroundBlueSlider.Value = color.B;
            BackgroundRedValueText.Text = color.R.ToString();
            BackgroundGreenValueText.Text = color.G.ToString();
            BackgroundBlueValueText.Text = color.B.ToString();
            if (updateText)
            {
                BackgroundColorTextBox.Text = ToCssHex(color);
            }
        }
        finally
        {
            _isUpdatingBackgroundColorControls = false;
        }
    }

    private bool AreBackgroundColorControlsReady()
    {
        return BackgroundColorPreview is not null &&
            BackgroundColorTextBox is not null &&
            SaturationValuePicker is not null &&
            SaturationValueSelectorTransform is not null &&
            HueColorLayer is not null &&
            BackgroundHueSlider is not null &&
            BackgroundRedSlider is not null &&
            BackgroundGreenSlider is not null &&
            BackgroundBlueSlider is not null &&
            BackgroundRedValueText is not null &&
            BackgroundGreenValueText is not null &&
            BackgroundBlueValueText is not null;
    }

    private void UpdateSaturationValueFromPoint(Point point)
    {
        var width = Math.Max(1, SaturationValuePicker.ActualWidth);
        var height = Math.Max(1, SaturationValuePicker.ActualHeight);
        _backgroundSaturation = Math.Clamp(point.X / width, 0, 1);
        _backgroundValue = 1 - Math.Clamp(point.Y / height, 0, 1);
        UpdateBackgroundColorControls(FromHsv(_backgroundHue, _backgroundSaturation, _backgroundValue), updateText: true);
    }

    private void UpdateSaturationValueSelector()
    {
        var width = Math.Max(1, SaturationValuePicker.ActualWidth);
        var height = Math.Max(1, SaturationValuePicker.ActualHeight);
        SaturationValueSelectorTransform.X = Math.Clamp(_backgroundSaturation * width - 7, -7, width - 7);
        SaturationValueSelectorTransform.Y = Math.Clamp((1 - _backgroundValue) * height - 7, -7, height - 7);
    }

    private static string LoadVersionText()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var versionPath = Path.Combine(repositoryRoot, "SCADA_BUILDER_V2", "VERSION");
        return File.Exists(versionPath)
            ? File.ReadAllText(versionPath).Trim()
            : "V2.0.0.0000";
    }

    private void InitializeRibbonCommandRegistry()
    {
        _ribbonTabs.Clear();
        foreach (var (tabKey, groups) in RibbonCommandCatalog.CreateDefault())
        {
            _ribbonTabs[tabKey] = groups;
        }
    }

    private void InitializeToolPaletteCommands()
    {
        ToolPaletteCommands.Clear();
        foreach (var definition in RibbonCommandCatalog.CreateToolPalette())
        {
            ToolPaletteCommands.Add(CreateRibbonCommandViewModel(definition));
        }
    }

    private RibbonGroupViewModel CreateRibbonGroupViewModel(RibbonGroupDefinition group)
    {
        var commands = group.Commands.Select(CreateRibbonCommandViewModel).ToArray();
        return new RibbonGroupViewModel(group.Label, commands);
    }

    private RibbonCommandViewModel CreateRibbonCommandViewModel(RibbonCommandDefinition definition)
    {
        var command = definition.IsEnabled
            ? new RibbonRelayCommand(() => ExecuteRibbonCommand(definition.Id), () => true)
            : new RibbonRelayCommand(() => SetStatus($"{definition.Label}: {definition.DisabledReason}"), () => false);

        return new RibbonCommandViewModel(
            definition.Id,
            definition.Label,
            definition.ToolTip,
            definition.IconKey,
            ResolveRibbonIcon(definition.IconKey),
            definition.IsEnabled,
            string.Equals(definition.Id, _activeInsertCommandId, StringComparison.Ordinal),
            command);
    }

    private ImageSource? ResolveRibbonIcon(string iconKey)
    {
        return TryFindResource(iconKey) as ImageSource;
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string ribbonKey })
        {
            return;
        }

        SetActiveRibbon(ribbonKey);
    }

    private void SetActiveRibbon(string ribbonKey)
    {
        _activeRibbonKey = ribbonKey;
        ActiveRibbonGroups.Clear();
        if (_ribbonTabs.TryGetValue(ribbonKey, out var groups))
        {
            foreach (var group in groups)
            {
                ActiveRibbonGroups.Add(CreateRibbonGroupViewModel(group));
            }
        }

        SetRibbonMenuButtonStyle(FileMenuButton, ribbonKey == "File");
        SetRibbonMenuButtonStyle(EditMenuButton, ribbonKey == "Edit");
        SetRibbonMenuButtonStyle(InsertMenuButton, ribbonKey == "Insert");
        SetRibbonMenuButtonStyle(ScreenMenuButton, ribbonKey == "Screen");
        SetRibbonMenuButtonStyle(SelectionMenuButton, ribbonKey == "Selection");
        SetRibbonMenuButtonStyle(ToolsMenuButton, ribbonKey == "Tools");
    }

    private void RefreshActiveRibbonCommandStates()
    {
        SetActiveRibbon(_activeRibbonKey);
    }

    private void SetRibbonMenuButtonStyle(Button button, bool isActive)
    {
        button.Style = (Style)FindResource(isActive ? "ActiveMenuButtonStyle" : "MenuButtonStyle");
    }

    private async void ExecuteRibbonCommand(string commandId)
    {
        switch (commandId)
        {
            case "project.save":
                OnSaveSceneClick(this, new RoutedEventArgs());
                break;
            case "import.tags":
                OnImportTagsClick(this, new RoutedEventArgs());
                break;
            case "export.ft100.folder":
                OnExportFt100Click(this, new RoutedEventArgs());
                break;
            case "export.ft100.sb2":
                OnExportFt100Sb2Click(this, new RoutedEventArgs());
                break;
            case "edit.undo":
                await UndoLastSceneOperationAsync();
                break;
            case "edit.redo":
                await RedoLastSceneOperationAsync();
                break;
            case "object.group":
                await GroupSelectedModernElementsAsync();
                break;
            case "object.ungroup":
                UngroupSelectedModernElement();
                break;
            case "insert.text":
                BeginModernElementPlacement(ScadaElementKind.Text, commandId);
                break;
            case "insert.input-text":
                BeginModernElementPlacement(ScadaElementKind.InputText, commandId);
                break;
            case "insert.input-numeric":
                BeginModernElementPlacement(ScadaElementKind.InputNumeric, commandId);
                break;
            case "insert.shape.rectangle":
                BeginShapePlacement(ScadaShapeKind.Rectangle, commandId);
                break;
            case "insert.shape.ellipse":
                BeginShapePlacement(ScadaShapeKind.Ellipse, commandId);
                break;
            case "insert.shape.circle":
                BeginShapePlacement(ScadaShapeKind.Circle, commandId);
                break;
            case "insert.shape.triangle":
                BeginShapePlacement(ScadaShapeKind.Triangle, commandId);
                break;
            case "insert.shape.star":
                BeginShapePlacement(ScadaShapeKind.Star, commandId);
                break;
            case "insert.shape.line":
                BeginShapePlacement(ScadaShapeKind.Line, commandId);
                break;
            case "insert.shape.arrow":
                BeginShapePlacement(ScadaShapeKind.Arrow, commandId);
                break;
            case "insert.hmi.indicator-lamp":
                BeginShapePlacement(ScadaShapeKind.IndicatorLamp, commandId);
                break;
            case "insert.hmi.bar-horizontal":
                BeginShapePlacement(ScadaShapeKind.HorizontalBar, commandId);
                break;
            case "insert.hmi.bar-vertical":
                BeginShapePlacement(ScadaShapeKind.VerticalBar, commandId);
                break;
            case "insert.hmi.tank":
                BeginShapePlacement(ScadaShapeKind.Tank, commandId);
                break;
            case "insert.hmi.pipe-horizontal":
                BeginShapePlacement(ScadaShapeKind.PipeHorizontal, commandId);
                break;
            case "insert.hmi.pipe-vertical":
                BeginShapePlacement(ScadaShapeKind.PipeVertical, commandId);
                break;
            case "insert.hmi.valve":
                BeginShapePlacement(ScadaShapeKind.Valve, commandId);
                break;
            case "insert.hmi.pump":
                BeginShapePlacement(ScadaShapeKind.Pump, commandId);
                break;
            case "insert.hmi.motor":
                BeginShapePlacement(ScadaShapeKind.Motor, commandId);
                break;
            case "insert.hmi.fan":
                BeginShapePlacement(ScadaShapeKind.Fan, commandId);
                break;
            case "insert.hmi.conveyor":
                BeginShapePlacement(ScadaShapeKind.Conveyor, commandId);
                break;
            case "insert.hmi.gauge":
                BeginShapePlacement(ScadaShapeKind.Gauge, commandId);
                break;
            case "insert.hmi.switch":
                BeginShapePlacement(ScadaShapeKind.Switch, commandId);
                break;
            case "insert.hmi.breaker":
                BeginShapePlacement(ScadaShapeKind.Breaker, commandId);
                break;
            case "insert.hmi.transformer":
                BeginShapePlacement(ScadaShapeKind.Transformer, commandId);
                break;
            case "insert.hmi.alarm-beacon":
                BeginShapePlacement(ScadaShapeKind.AlarmBeacon, commandId);
                break;
            case "insert.button.command":
                BeginButtonPlacement(ScadaButtonKind.Command, commandId);
                break;
            case "insert.button.toggle":
                BeginButtonPlacement(ScadaButtonKind.Toggle, commandId);
                break;
            case "insert.button.navigation":
                BeginButtonPlacement(ScadaButtonKind.Navigation, commandId);
                break;
            case "insert.button.alarm-ack":
                BeginButtonPlacement(ScadaButtonKind.AlarmAcknowledge, commandId);
                break;
            case "insert.button.emergency-stop":
                BeginButtonPlacement(ScadaButtonKind.EmergencyStop, commandId);
                break;
            default:
                SetStatus($"Commande ruban inconnue: {commandId}");
                break;
        }
    }

    private async void OnUndoClick(object sender, RoutedEventArgs e)
    {
        await UndoLastSceneOperationAsync();
    }

    private async void OnUndoCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        await UndoLastSceneOperationAsync();
        e.Handled = true;
    }

    private async Task UndoLastSceneOperationAsync()
    {
        if (_activeSceneTab is null || _activeScene is null)
        {
            SetStatus("Undo ignore: aucune scene active.");
            return;
        }

        if (!await _activeSceneTab.History.UndoAsync(CreateEditorHistoryContext()))
        {
            SetStatus("Aucune operation a annuler dans cette scene.");
        }
    }

    private async void OnRedoClick(object sender, RoutedEventArgs e)
    {
        await RedoLastSceneOperationAsync();
    }

    private async void OnRedoCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        await RedoLastSceneOperationAsync();
        e.Handled = true;
    }

    private async Task RedoLastSceneOperationAsync()
    {
        if (_activeSceneTab is null || _activeScene is null)
        {
            SetStatus("Redo ignore: aucune scene active.");
            return;
        }

        if (!await _activeSceneTab.History.RedoAsync(CreateEditorHistoryContext()))
        {
            SetStatus("Aucune operation a retablir dans cette scene.");
        }
    }

    private async Task RestoreLegacyDeletionAsync(
        IReadOnlyList<ScadaElement> deletedElements,
        IReadOnlyList<LegacyElementListItem> deletedInventoryItems)
    {
        if (_activeScene is null)
        {
            SetStatus("Undo suppression ignore: aucune scene active.");
            return;
        }

        foreach (var element in deletedElements)
        {
            if (_activeScene.FindElementRecursive(element.Id) is null)
            {
                _activeScene = _activeScene.WithElement(element);
            }

            var sourceId = element.LegacySource?.SourceElementId;
            if (!string.IsNullOrWhiteSpace(sourceId))
            {
                _hiddenSourceObjectIds.Remove(sourceId);
            }
        }

        _selectedSourceObjectIds.Clear();
        foreach (var item in deletedInventoryItems)
        {
            RemoveLegacyElementsFromInventory([item.Id]);
            _sourceObjects.Add(item);
            _selectedSourceObjectIds.Add(item.Id);
        }

        await RestoreLegacyElementsInViewerAsync(deletedInventoryItems.Select(item => item.Id).ToArray());
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        await RenderModernSceneAsync();
        SetStatus($"{deletedElements.Count} suppression(s) legacy annulee(s). Sauvegarde requise.");
    }

    private async Task ApplyLegacyDeletionSnapshotAsync(
        IReadOnlyList<ScadaElement> deletedElements,
        IReadOnlyList<LegacyElementListItem> deletedInventoryItems)
    {
        if (_activeScene is null)
        {
            SetStatus("Redo suppression ignore: aucune scene active.");
            return;
        }

        foreach (var element in deletedElements)
        {
            var sourceId = element.LegacySource?.SourceElementId;
            if (!string.IsNullOrWhiteSpace(sourceId))
            {
                _hiddenSourceObjectIds.Add(sourceId);
            }

            _activeScene = _activeScene.WithoutElementRecursive(element.Id);
        }

        var ids = deletedInventoryItems.Select(item => item.Id).ToArray();
        RemoveLegacyElementsFromInventory(ids);
        await RemoveLegacyElementsInViewerAsync(ids);
        _selectedSourceObjectIds.Clear();
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        await RenderModernSceneAsync();
        SetStatus($"{deletedElements.Count} suppression(s) legacy retablie(s). Sauvegarde requise.");
    }

    private async Task UndoLegacyConversionAsync(
        IReadOnlyList<LegacyDetectedObject> sources,
        IReadOnlyList<ScadaElement> convertedElements,
        IReadOnlyList<ScadaElement> sourceElements)
    {
        if (_activeScene is null)
        {
            SetStatus("Undo conversion ignore: aucune scene active.");
            return;
        }

        foreach (var convertedElement in convertedElements)
        {
            _activeScene = _activeScene.WithoutElement(convertedElement.Id);
        }

        foreach (var sourceElement in sourceElements)
        {
            if (_activeScene.FindElementRecursive(sourceElement.Id) is null)
            {
                _activeScene = _activeScene.WithElement(sourceElement);
            }
        }

        _selectedSceneObject = null;
        _selectedSceneObjectIds.Clear();
        _selectedSourceObjectIds.Clear();
        foreach (var source in sources)
        {
            _selectedSourceObjectIds.Add(source.RuntimeId);
            _hiddenSourceObjectIds.Remove(source.RuntimeId);
            RemoveLegacyElementsFromInventory([source.RuntimeId]);
        }

        foreach (var sourceElement in sourceElements)
        {
            _sourceObjects.Add(ToLegacyElementListItem(sourceElement));
        }

        var restoredSourceIds = sourceElements
            .Select(element => element.LegacySource?.SourceElementId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var source in sources.Where(source => !restoredSourceIds.Contains(source.RuntimeId)))
        {
            RestoreLegacyElementInInventory(source);
        }

        await RestoreLegacyElementsInViewerAsync(sources.Select(source => source.RuntimeId).ToArray());
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        await RenderModernSceneAsync();
        SetStatus($"{convertedElements.Count} conversion(s) Element+ annulee(s). Sauvegarde requise.");
    }

    private async Task RedoLegacyConversionAsync(
        IReadOnlyList<LegacyDetectedObject> sources,
        IReadOnlyList<ScadaElement> convertedElements,
        string targetLabel)
    {
        if (_activeScene is null)
        {
            SetStatus("Redo conversion ignore: aucune scene active.");
            return;
        }

        foreach (var convertedElement in convertedElements)
        {
            _activeScene = _activeScene.WithCommittedElementPlusConversion(convertedElement);
            if (!string.IsNullOrWhiteSpace(convertedElement.LegacySource?.SourceElementId))
            {
                _hiddenSourceObjectIds.Add(convertedElement.LegacySource.SourceElementId);
            }
        }

        var convertedLegacyIds = sources.Select(source => source.RuntimeId).ToArray();
        RemoveLegacyElementsFromInventory(convertedLegacyIds);
        await RemoveLegacyElementsInViewerAsync(convertedLegacyIds);

        _selectedSceneObject = convertedElements[^1];
        _selectedSceneObjectIds.Clear();
        _selectedSceneObjectIds.Add(_selectedSceneObject.Id);
        _selectedSourceObjectIds.Clear();
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        await RenderModernSceneAsync();
        SetStatus($"{convertedElements.Count} conversion(s) Element+ retablie(s) ({targetLabel}). Sauvegarde requise.");
    }

    /// <summary>
    /// View model for a top-ribbon command group rendered from the shell command registry.
    /// </summary>
    public sealed class RibbonGroupViewModel
    {
        public RibbonGroupViewModel(string label, IReadOnlyList<RibbonCommandViewModel> commands)
        {
            Label = label;
            Commands = commands;
        }

        /// <summary>
        /// Visible group label.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Whether the group uses the large two-row shape gallery layout.
        /// </summary>
        public bool IsShapeGallery => string.Equals(Label, "Formes", StringComparison.Ordinal);

        /// <summary>
        /// Ordered command list displayed in the group.
        /// </summary>
        public IReadOnlyList<RibbonCommandViewModel> Commands { get; }
    }

    /// <summary>
    /// View model for one top-ribbon command button.
    /// </summary>
    public sealed class RibbonCommandViewModel
    {
        public RibbonCommandViewModel(
            string id,
            string label,
            string toolTip,
            string iconKey,
            ImageSource? icon,
            bool isEnabled,
            bool isActive,
            ICommand command)
        {
            Id = id;
            Label = label;
            ToolTip = toolTip;
            IconKey = iconKey;
            Icon = icon;
            IsEnabled = isEnabled;
            IsActive = isActive;
            Command = command;
        }

        /// <summary>
        /// Stable command id used by ribbon dispatch and documentation.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Visible button label.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Tooltip or disabled reason shown by the command surface.
        /// </summary>
        public string ToolTip { get; }

        /// <summary>
        /// Semantic icon resource key.
        /// </summary>
        public string IconKey { get; }

        /// <summary>
        /// Resolved WPF icon resource.
        /// </summary>
        public ImageSource? Icon { get; }

        /// <summary>
        /// Whether the command is executable in the current implementation slice.
        /// </summary>
        public bool IsEnabled { get; }

        /// <summary>
        /// Whether this command is the current active insertion command.
        /// </summary>
        public bool IsActive { get; }

        /// <summary>
        /// WPF command invoked by the ribbon button.
        /// </summary>
        public ICommand Command { get; }
    }

    private sealed class RibbonRelayCommand : ICommand
    {
        private readonly Action execute;
        private readonly Func<bool> canExecute;

        public RibbonRelayCommand(Action execute, Func<bool> canExecute)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            return canExecute();
        }

        public void Execute(object? parameter)
        {
            execute();
        }
    }

    private sealed class SceneWorkspaceTab : INotifyPropertyChanged
    {
        private ScadaScene scene;
        private bool isDirty;

        public SceneWorkspaceTab(ReferenceScadaPage referencePage, ScadaScene scene)
        {
            ReferencePage = referencePage;
            this.scene = scene;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ReferenceScadaPage ReferencePage { get; }

        public string SceneId => ReferencePage.Id;

        public string Title => string.IsNullOrWhiteSpace(ReferencePage.Title) ? ReferencePage.Id : ReferencePage.Title;

        public string Header => IsDirty ? $"{SceneId}*" : SceneId;

        public ScadaScene Scene
        {
            get => scene;
            set
            {
                if (ReferenceEquals(scene, value))
                {
                    return;
                }

                scene = value;
                OnPropertyChanged(nameof(Scene));
            }
        }

        public bool IsDirty
        {
            get => isDirty;
            set
            {
                if (isDirty == value)
                {
                    return;
                }

                isDirty = value;
                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(Header));
            }
        }

        public EditorHistoryService History { get; } = new();

        public IReadOnlyList<string> SelectedModernElementIds { get; set; } = Array.Empty<string>();

        public string? PrimaryModernElementId { get; set; }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private sealed class ActiveSelectionState
    {
        public HashSet<string> SourceObjectIds { get; } = new(StringComparer.Ordinal);

        public HashSet<string> SceneObjectIds { get; } = new(StringComparer.Ordinal);

        public ScadaElement? PrimarySceneObject { get; set; }

        public void Clear()
        {
            SourceObjectIds.Clear();
            SceneObjectIds.Clear();
            PrimarySceneObject = null;
        }
    }

    private sealed record LegacyViewerSource(string RootPath, string RelativeHtmlSource, string Kind);

    private sealed record ElementStudioLaunchResult(bool Launched, string Message);

    private sealed record TagCatalogListItem(
        string Id,
        string Name,
        string Datatype,
        string Device,
        string Address,
        string Access,
        string State)
    {
        public string SearchText => string.Join(" ", Id, Name, Datatype, Device, Address, Access, State);
    }

    private sealed record LegacyViewerCommand(
        string Action,
        string? Kind = null,
        string? Id = null,
        IReadOnlyList<string>? Ids = null,
        string? ShapeKind = null,
        bool IsTwoPoint = false);

    private sealed class LegacyViewerMessage
    {
        public string Type { get; set; } = "";

        public List<LegacyViewerElementMessage>? Items { get; set; }

        public string? BackgroundColor { get; set; }

        public string? Text { get; set; }

        public string? Id { get; set; }

        public List<string>? Ids { get; set; }

        public string? Kind { get; set; }

        public string? ShapeKind { get; set; }

        public string? CommandId { get; set; }

        public string? TargetKind { get; set; }

        public string? DisplayName { get; set; }

        public string? Placeholder { get; set; }

        public string? ValueText { get; set; }

        public string? MinimumText { get; set; }

        public string? MaximumText { get; set; }

        public string? DecimalsText { get; set; }

        public string? Unit { get; set; }

        public string? DisplayFormat { get; set; }

        public string? TagBinding { get; set; }

        public string? Background { get; set; }

        public string? BorderStyle { get; set; }

        public bool ButtonDisabled { get; set; }

        public bool ButtonHoverEnabled { get; set; } = true;

        public string? ButtonHoverBackground { get; set; }

        public string? ButtonHoverForeground { get; set; }

        public string? ButtonHoverBorderColor { get; set; }

        public bool ButtonPressedEnabled { get; set; } = true;

        public string? ButtonPressedBackground { get; set; }

        public string? ButtonPressedForeground { get; set; }

        public string? ButtonPressedBorderColor { get; set; }

        public bool IsReadOnly { get; set; }

        public bool Additive { get; set; }

        public bool Toggle { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double X2 { get; set; }

        public double Y2 { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double BeforeX { get; set; }

        public double BeforeY { get; set; }

        public double BeforeWidth { get; set; }

        public double BeforeHeight { get; set; }

        public double DeltaX { get; set; }

        public double DeltaY { get; set; }

        public double FontSize { get; set; }

        public double BorderWidth { get; set; }
    }

    private sealed class LegacyViewerElementMessage
    {
        public string Id { get; set; } = "";

        public string Name { get; set; } = "";

        public string ElementType { get; set; } = "";

        public string? Text { get; set; }

        public bool IsTextLike { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public string? FontFamily { get; set; }

        public double FontSize { get; set; }

        public string? Foreground { get; set; }

        public string? Background { get; set; }

        public string? LegacyMarkup { get; set; }

        public string? RawMetadataJson { get; set; }

        public int RenderOrder { get; set; }
    }

    private sealed class ModernElementRenderPayload
    {
        public string Id { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public string Kind { get; set; } = "";

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public bool IsSelected { get; set; }

        public bool IsGroupContextSelected { get; set; }

        public int RenderIndex { get; set; }

        public ScadaElementStyle? Style { get; set; }

        public ScadaElementData? Data { get; set; }

        public ScadaButtonBehavior? ButtonBehavior { get; set; }

        public string? ShapeKind { get; set; }

        public string? ButtonKind { get; set; }

        public IReadOnlyList<ModernElementRenderPayload> Children { get; set; } = [];
    }

}
