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
    private int _nextTextSequence = 1;
    private int _nextInputTextSequence = 1;
    private int _nextInputNumericSequence = 1;
    private int _nextGroupSequence = 1;
    private bool _activeSceneDirty;
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

    public bool IsSelectionLocked { get; set; } = false;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
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
                    PlaceModernElement(message.Kind, message.X, message.Y);
                    break;
                case "selectSceneObject":
                case "selectModernElement":
                    SelectModernElement(message.Id, message.Additive, message.Toggle);
                    break;
                case "openSceneObjectProperties":
                case "openModernElementProperties":
                    SelectModernElement(message.Id);
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
        var dotnetStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(studioProjectPath) ?? ""
        };
        dotnetStartInfo.ArgumentList.Add("run");
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

    private static IReadOnlyList<ElementPlusConversionTarget> GetPlausibleConversionTargets(LegacyElementListItem element)
    {
        return ElementPlusLegacyConverter.GetPlausibleTargets(ToLegacyDetectedObject(element));
    }

    private static IReadOnlyList<ElementPlusConversionTarget> GetPlausibleConversionTargets(LegacyViewerElementMessage element)
    {
        return ElementPlusLegacyConverter.GetPlausibleTargets(ToLegacyDetectedObject(ToLegacyElementListItem(element)));
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

    private static LegacyDetectedObject ToLegacyDetectedObject(LegacyElementListItem legacy)
    {
        return new LegacyDetectedObject(
            legacy.Id,
            legacy.DisplayName,
            legacy.ElementType,
            legacy.Text,
            legacy.IsTextLike,
            new SceneBounds(legacy.X, legacy.Y, legacy.Width, legacy.Height),
            new LegacyObjectStyle(legacy.FontFamily, legacy.FontSize, legacy.Foreground, legacy.Background));
    }

    private static LegacyElementListItem ToLegacyElementListItem(LegacyDetectedObject legacy)
    {
        return new LegacyElementListItem(
            legacy.RuntimeId,
            legacy.DisplayName,
            legacy.LegacyType,
            legacy.Bounds.X,
            legacy.Bounds.Y,
            legacy.Bounds.Width,
            legacy.Bounds.Height,
            legacy.Text,
            legacy.IsTextLike,
            legacy.Style.FontFamily,
            legacy.Style.FontSize,
            legacy.Style.Foreground,
            legacy.Style.Background,
            "",
            "");
    }

    private static LegacyElementListItem ToLegacyElementListItem(LegacyViewerElementMessage item)
    {
        var id = item.Id.Trim();
        return new LegacyElementListItem(
            id,
            string.IsNullOrWhiteSpace(item.Name) ? id : item.Name.Trim(),
            string.IsNullOrWhiteSpace(item.ElementType) ? "Legacy" : item.ElementType.Trim(),
            item.X,
            item.Y,
            item.Width,
            item.Height,
            item.Text ?? "",
            item.IsTextLike,
            item.FontFamily ?? "",
            item.FontSize,
            item.Foreground ?? "",
            item.Background ?? "",
            item.LegacyMarkup ?? "",
            item.RawMetadataJson ?? "",
            item.RenderOrder);
    }

    private static LegacyElementListItem ToLegacyElementListItem(ScadaElement element)
    {
        var payload = element.LegacyPayload;
        var sourceId = element.LegacySource?.SourceElementId ?? element.Id;
        return new LegacyElementListItem(
            sourceId,
            string.IsNullOrWhiteSpace(element.LegacySource?.SourceElementName)
                ? element.DisplayName
                : element.LegacySource.SourceElementName,
            payload?.LegacyType ?? "Legacy",
            element.Bounds.X,
            element.Bounds.Y,
            element.Bounds.Width,
            element.Bounds.Height,
            payload?.Text ?? element.Data?.Text ?? "",
            payload?.IsTextLike ?? false,
            payload?.FontFamily ?? element.Style?.FontFamily ?? "",
            payload?.FontSize ?? element.Style?.FontSize ?? 0,
            payload?.Foreground ?? element.Style?.Foreground ?? "",
            payload?.Background ?? element.Style?.Background ?? "",
            payload?.LegacyMarkup ?? "",
            payload?.RawMetadataJson ?? "");
    }

    private void RestoreLegacyElementInInventory(LegacyDetectedObject legacy)
    {
        RemoveLegacyElementsFromInventory([legacy.RuntimeId]);
        _sourceObjects.Add(ToLegacyElementListItem(legacy));
    }

    private static string GetConversionTargetLabel(ElementPlusConversionTarget target)
    {
        return target switch
        {
            ElementPlusConversionTarget.Text => "Texte",
            ElementPlusConversionTarget.TextInput => "Champ d'entree texte",
            ElementPlusConversionTarget.NumericReadOnly => "Affichage numerique",
            ElementPlusConversionTarget.NumericEditable => "Champ numerique editable",
            ElementPlusConversionTarget.Button => "Bouton",
            _ => target.ToString()
        };
    }

    private static EditorCommandDescriptor CreateConversionCommandDescriptor(ElementPlusConversionTarget target)
    {
        return new EditorCommandDescriptor(
            $"source.convert-to-element-plus.{GetConversionTargetCommandSuffix(target)}",
            GetConversionTargetLabel(target),
            "conversion");
    }

    private static string GetConversionTargetCommandSuffix(ElementPlusConversionTarget target)
    {
        return target switch
        {
            ElementPlusConversionTarget.Text => "text",
            ElementPlusConversionTarget.TextInput => "input-text",
            ElementPlusConversionTarget.NumericReadOnly => "numeric-readonly",
            ElementPlusConversionTarget.NumericEditable => "numeric-editable",
            ElementPlusConversionTarget.Button => "button",
            _ => target.ToString().ToLowerInvariant()
        };
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

    private static string SanitizeElementIdPart(string value)
    {
        var chars = value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '_')
            .ToArray();
        var sanitized = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "legacy" : sanitized;
    }

    private static string NormalizeTransparentCssColor(string value)
    {
        return string.IsNullOrWhiteSpace(value) ||
            value.Equals("rgba(0, 0, 0, 0)", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("transparent", StringComparison.OrdinalIgnoreCase)
                ? "Transparent"
                : value;
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
        if (string.IsNullOrWhiteSpace(targetId))
        {
            SetStatus("Aucun Element+ selectionne pour les proprietes.");
            return;
        }

        SelectModernElement(targetId);
        RightContextTabs.SelectedItem = PropertiesContextTab;
        SetStatus("Proprietes Element+ ouvertes.");
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

    private async void BeginModernElementPlacement(ScadaElementKind kind)
    {
        _pendingInsertKind = kind;
        await ExecuteLegacyViewerCommandAsync(new LegacyViewerCommand("beginPlacement", kind.ToString()));
        var label = kind switch
        {
            ScadaElementKind.InputText => "champ d'entree texte",
            ScadaElementKind.InputNumeric => "champ d'entree numerique",
            _ => "champ texte"
        };
        SetStatus($"Insertion active: cliquez dans la scene pour placer un {label}.");
    }

    private void PlaceModernElement(string? kind, double x, double y)
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

        var element = CreateModernElement(elementKind.Value, x, y);

        var beforeScene = _activeScene;
        _activeScene = _activeScene.WithElement(element);
        _activeSceneTab?.History.Push(new SceneSnapshotChangedAction(
            beforeScene.Id,
            beforeScene,
            _activeScene,
            "insertion Element+"));
        _selectedSceneObject = element;
        _selectedSceneObjectIds.Clear();
        _selectedSceneObjectIds.Add(element.Id);
        _selectedSourceObjectIds.Clear();
        _pendingInsertKind = null;
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        _ = RenderModernSceneAsync();
        SetStatus($"{element.UserLabel} ajoute a la scene V2. Sauvegarde requise.");
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
            (parsed == ScadaElementKind.Text || parsed == ScadaElementKind.InputText || parsed == ScadaElementKind.InputNumeric)
                ? parsed
                : null;
    }

    private ScadaElement CreateModernElement(ScadaElementKind kind, double x, double y)
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

        var textSequence = _nextInputTextSequence++;
        var inputTextId = CreateUniqueElementId($"input_text_{textSequence:000}");
        return ScadaElement.CreateInputText(inputTextId, $"InputText{textSequence:000}", x, y);
    }

    private void ResetElementSequences(ScadaScene scene)
    {
        var elements = FlattenElements(scene.Elements).ToArray();
        _nextTextSequence = elements.Count(element => element.Kind == ScadaElementKind.Text && !element.IsImportedFromLegacy) + 1;
        _nextInputTextSequence = elements.Count(element => element.Kind == ScadaElementKind.InputText) + 1;
        _nextInputNumericSequence = elements.Count(element => element.Kind == ScadaElementKind.InputNumeric) + 1;
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
                        string.IsNullOrWhiteSpace(message.ButtonHoverBorderColor) ? ScadaButtonHoverStyle.Default.BorderColor : message.ButtonHoverBorderColor))
                : current.ButtonBehavior
        };

        if (Equals(current, updated))
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
            ElementBackgroundComboBox.IsEnabled = isEnabled;
            ElementBorderStyleComboBox.IsEnabled = isEnabled;
            ElementBorderWidthTextBox.IsEnabled = isEnabled;
            ElementAdvancedCssTextBox.IsEnabled = isEnabled;
            ButtonContextTab.Visibility = element?.Kind == ScadaElementKind.Button ? Visibility.Visible : Visibility.Collapsed;
            ButtonDisabledCheckBox.IsEnabled = element?.Kind == ScadaElementKind.Button;
            ButtonHoverEnabledCheckBox.IsEnabled = element?.Kind == ScadaElementKind.Button;
            ButtonHoverBackgroundComboBox.IsEnabled = element?.Kind == ScadaElementKind.Button;
            ButtonHoverForegroundComboBox.IsEnabled = element?.Kind == ScadaElementKind.Button;
            ButtonHoverBorderColorComboBox.IsEnabled = element?.Kind == ScadaElementKind.Button;
            ElementPlaceholderTextBox.IsEnabled = isEnabled;
            ElementValueTextBox.IsEnabled = isEnabled;
            ElementMinTextBox.IsEnabled = isEnabled;
            ElementMaxTextBox.IsEnabled = isEnabled;
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
                ElementBackgroundComboBox.SelectedIndex = 0;
                ElementBorderStyleComboBox.SelectedIndex = 0;
                ElementBorderWidthTextBox.Text = "";
                ShadowNoneRadio.IsChecked = true;
                ElementAdvancedCssTextBox.Text = "";
                ButtonDisabledCheckBox.IsChecked = false;
                ButtonHoverEnabledCheckBox.IsChecked = true;
                SelectComboBoxText(ButtonHoverBackgroundComboBox, ScadaButtonHoverStyle.Default.Background);
                SelectComboBoxText(ButtonHoverForegroundComboBox, ScadaButtonHoverStyle.Default.Foreground);
                SelectComboBoxText(ButtonHoverBorderColorComboBox, ScadaButtonHoverStyle.Default.BorderColor);
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
            SelectComboBoxText(ElementBackgroundComboBox, style.Background);
            SelectComboBoxText(ElementBorderStyleComboBox, style.BorderStyle);
            ElementBorderWidthTextBox.Text = style.BorderWidth.ToString("0.##");
            ShadowNoneRadio.IsChecked = style.ShadowPreset == "None";
            ShadowSoftRadio.IsChecked = style.ShadowPreset == "Soft";
            ShadowRaisedRadio.IsChecked = style.ShadowPreset == "Raised";
            ShadowInsetRadio.IsChecked = style.ShadowPreset == "Inset";
            ElementAdvancedCssTextBox.Text = style.AdvancedCss ?? "";
            var buttonBehavior = element.EffectiveButtonBehavior;
            var hoverStyle = buttonBehavior.EffectiveHover;
            ButtonDisabledCheckBox.IsChecked = buttonBehavior.IsDisabled;
            ButtonHoverEnabledCheckBox.IsChecked = hoverStyle.Enabled;
            SelectComboBoxText(ButtonHoverBackgroundComboBox, hoverStyle.Background);
            SelectComboBoxText(ButtonHoverForegroundComboBox, hoverStyle.Foreground);
            SelectComboBoxText(ButtonHoverBorderColorComboBox, hoverStyle.BorderColor);
            ElementPlaceholderTextBox.Text = data.Placeholder ?? "";
            ElementValueTextBox.Text = data.Value?.ToString("0.##") ?? data.Text ?? "";
            ElementMinTextBox.Text = data.Minimum?.ToString("0.##") ?? "";
            ElementMaxTextBox.Text = data.Maximum?.ToString("0.##") ?? "";
            ElementDecimalsTextBox.Text = data.Decimals?.ToString() ?? "";
            ElementUnitTextBox.Text = data.Unit ?? "";
            ElementFormatTextBox.Text = data.DisplayFormat ?? "";
            ElementTagBindingTextBox.Text = data.TagBinding ?? "";
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
            ElementEventsSummaryText.Text = bindingSummaries.Count == 0
                ? "Aucun evenement"
                : string.Join(", ", bindingSummaries);
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

    private void OpenElementEventDialog(string? elementId)
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
            Owner = this
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
                Background = GetComboBoxText(ElementBackgroundComboBox, style.Background),
                BorderStyle = GetComboBoxText(ElementBorderStyleComboBox, style.BorderStyle),
                BorderWidth = Math.Max(0, ParseDoubleOrDefault(ElementBorderWidthTextBox.Text, style.BorderWidth)),
                ShadowPreset = GetSelectedShadowPreset(),
                AdvancedCss = string.IsNullOrWhiteSpace(ElementAdvancedCssTextBox.Text) ? null : ElementAdvancedCssTextBox.Text
            },
            ButtonBehavior = current.Kind == ScadaElementKind.Button
                ? new ScadaButtonBehavior(
                    ButtonDisabledCheckBox.IsChecked == true,
                    new ScadaButtonHoverStyle(
                        ButtonHoverEnabledCheckBox.IsChecked == true,
                        GetComboBoxText(ButtonHoverBackgroundComboBox, ScadaButtonHoverStyle.Default.Background),
                        GetComboBoxText(ButtonHoverForegroundComboBox, ScadaButtonHoverStyle.Default.Foreground),
                        GetComboBoxText(ButtonHoverBorderColorComboBox, ScadaButtonHoverStyle.Default.BorderColor)))
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

    private static bool TryParseCssColor(string cssColor, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(cssColor))
        {
            return false;
        }

        var value = cssColor.Trim();
        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var start = value.IndexOf('(');
            var end = value.IndexOf(')');
            if (start >= 0 && end > start)
            {
                var parts = value[(start + 1)..end]
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 &&
                    TryParseByte(parts[0], out var r) &&
                    TryParseByte(parts[1], out var g) &&
                    TryParseByte(parts[2], out var b))
                {
                    color = Color.FromRgb(r, g, b);
                    return true;
                }
            }
        }

        try
        {
            var converted = ColorConverter.ConvertFromString(value);
            if (converted is Color parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch (FormatException)
        {
            return false;
        }

        return false;
    }

    private static bool TryParseByte(string raw, out byte value)
    {
        value = 0;
        var normalized = raw.Trim();
        if (normalized.EndsWith('%'))
        {
            if (double.TryParse(normalized.TrimEnd('%'), out var percent))
            {
                value = (byte)Math.Clamp(Math.Round(percent / 100 * 255), 0, 255);
                return true;
            }

            return false;
        }

        if (double.TryParse(normalized, out var number))
        {
            value = (byte)Math.Clamp(Math.Round(number), 0, 255);
            return true;
        }

        return false;
    }

    private static string ToCssHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
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

    private static Color FromHsv(double hue, double saturation, double value)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        value = Math.Clamp(value, 0, 1);

        var chroma = value * saturation;
        var x = chroma * (1 - Math.Abs((hue / 60 % 2) - 1));
        var m = value - chroma;

        (var r1, var g1, var b1) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };

        return Color.FromRgb(
            ToColorByte((r1 + m) * 255),
            ToColorByte((g1 + m) * 255),
            ToColorByte((b1 + m) * 255));
    }

    private static (double Hue, double Saturation, double Value) ToHsv(Color color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var hue = delta == 0
            ? 0
            : max == r
                ? 60 * (((g - b) / delta) % 6)
                : max == g
                    ? 60 * (((b - r) / delta) + 2)
                    : 60 * (((r - g) / delta) + 4);
        if (hue < 0)
        {
            hue += 360;
        }

        var saturation = max == 0 ? 0 : delta / max;
        return (hue, saturation, max);
    }

    private static byte ToColorByte(double value)
    {
        return (byte)Math.Clamp(Math.Round(value), 0, 255);
    }

    private static string LoadVersionText()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var versionPath = Path.Combine(repositoryRoot, "SCADA_BUILDER_V2", "VERSION");
        return File.Exists(versionPath)
            ? File.ReadAllText(versionPath).Trim()
            : "V2.0.0.0000";
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string ribbonKey })
        {
            return;
        }

        FileRibbon.Visibility = ribbonKey == "File" ? Visibility.Visible : Visibility.Collapsed;
        EditRibbon.Visibility = ribbonKey == "Edit" ? Visibility.Visible : Visibility.Collapsed;
        InsertRibbon.Visibility = ribbonKey == "Insert" ? Visibility.Visible : Visibility.Collapsed;
        ScreenRibbon.Visibility = ribbonKey == "Screen" ? Visibility.Visible : Visibility.Collapsed;
        SelectionRibbon.Visibility = ribbonKey == "Selection" ? Visibility.Visible : Visibility.Collapsed;
        ToolsRibbon.Visibility = ribbonKey == "Tools" ? Visibility.Visible : Visibility.Collapsed;
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
        IReadOnlyList<string>? Ids = null);

    private sealed class LegacyViewerMessage
    {
        public string Type { get; set; } = "";

        public List<LegacyViewerElementMessage>? Items { get; set; }

        public string? BackgroundColor { get; set; }

        public string? Text { get; set; }

        public string? Id { get; set; }

        public List<string>? Ids { get; set; }

        public string? Kind { get; set; }

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

        public bool IsReadOnly { get; set; }

        public bool Additive { get; set; }

        public bool Toggle { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

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

        public IReadOnlyList<ModernElementRenderPayload> Children { get; set; } = [];
    }

    private const string LegacyExtractionScript = """
(() => {
  if (window.scadaSceneEditor || window.scadaLegacyExtraction) {
    const api = window.scadaSceneEditor || window.scadaLegacyExtraction;
    api.refresh();
    return;
  }

  const selectableSelector = '[data-id]:not(.scada-modern-element)';
  const inventorySelector = '.layer[data-id]:not(.scada-modern-element), .shape-layer [data-id]';
  const selected = new Set();
  const hidden = new Set();
  const removedNodes = new Map();

  const style = document.createElement('style');
  style.textContent = `
    [data-scada-selected="true"] {
      outline: 2px solid #2090a0 !important;
      outline-offset: 2px !important;
      filter: drop-shadow(0 0 5px rgba(32,144,160,.55));
    }
    #scada-extract-marquee {
      position: fixed;
      z-index: 2147483645;
      border: 1px solid #2090a0;
      background: rgba(32,144,160,.14);
      pointer-events: none;
      display: none;
    }
    #scada-extract-menu {
      position: fixed;
      z-index: 2147483647;
      min-width: 168px;
      padding: 6px;
      border: 1px solid rgba(15,42,48,.18);
      background: #fff;
      box-shadow: 0 10px 28px rgba(15,42,48,.18);
      display: none;
      font: 13px Segoe UI, Arial, sans-serif;
      pointer-events: auto;
    }
    #scada-extract-menu button {
      width: 100%;
      display: block;
      margin: 0;
      padding: 7px 9px;
      border: 0;
      background: transparent;
      color: #0f2a30;
      text-align: left;
      cursor: pointer;
    }
    #scada-extract-menu button:hover { background: #e0f2d0; }
    #scada-extract-menu button:disabled,
    #scada-extract-menu button[aria-disabled="true"] {
      color: #8a9aa0;
      cursor: not-allowed;
      background: transparent;
    }
    #scada-extract-menu .submenu {
      position: relative;
    }
    #scada-extract-menu .submenu::after {
      content: '';
      position: absolute;
      left: 100%;
      top: -8px;
      width: 14px;
      height: calc(100% + 16px);
      background: transparent;
    }
    #scada-extract-menu .submenu[data-submenu-x="left"]::after {
      left: auto;
      right: 100%;
    }
    #scada-extract-menu .submenu > button {
      padding-right: 24px;
    }
    #scada-extract-menu .submenu > button::after {
      content: '>';
      position: absolute;
      right: 10px;
      color: #5f747a;
    }
    #scada-extract-menu .submenu[data-submenu-x="left"] > button::after {
      content: '<';
    }
    #scada-extract-menu .submenu-panel {
      position: absolute;
      left: calc(100% - 1px);
      top: -4px;
      min-width: 190px;
      padding: 6px;
      border: 1px solid rgba(15,42,48,.18);
      background: #fff;
      box-shadow: 0 10px 28px rgba(15,42,48,.18);
      display: none;
    }
    #scada-extract-menu .submenu[data-submenu-x="left"] .submenu-panel {
      left: auto;
      right: calc(100% - 1px);
    }
    #scada-extract-menu .submenu:hover > .submenu-panel,
    #scada-extract-menu .submenu:focus-within > .submenu-panel {
      display: block;
    }
    #scada-modern-layer {
      position: absolute;
      inset: 0;
      z-index: 2147483000;
      pointer-events: none;
    }
    .scada-modern-element {
      position: absolute;
      box-sizing: border-box;
      pointer-events: auto;
      display: flex;
      align-items: center;
      padding: 0 8px;
      color: #0f2a30;
      background: #fff;
      border: 1px solid #8aa0a6;
      font: 14px Segoe UI, Arial, sans-serif;
      user-select: none;
      cursor: pointer;
    }
    body.scada-placement-active .scada-modern-element {
      pointer-events: none;
    }
    .scada-modern-element[data-selected="true"] {
      outline: 2px solid #2090a0;
      outline-offset: 2px;
      box-shadow: 0 0 0 4px rgba(32,144,160,.20), 0 8px 22px rgba(15,42,48,.18);
    }
    .scada-modern-group {
      align-items: stretch;
      padding: 0;
      background: transparent !important;
      border: 1px dashed transparent !important;
    }
    .scada-modern-group[data-selected="true"] {
      border-color: rgba(32,144,160,.85) !important;
    }
    .scada-modern-group[data-group-context="true"] {
      outline: 2px solid #2090a0;
      outline-offset: 3px;
      border-color: rgba(32,144,160,.55) !important;
      box-shadow: 0 0 0 4px rgba(32,144,160,.16);
    }
    .scada-modern-child[data-selected="true"] {
      outline: 2px solid #f2c230;
      outline-offset: 2px;
      box-shadow: 0 0 0 4px rgba(242,194,48,.24), 0 6px 18px rgba(15,42,48,.16);
    }
    .scada-modern-element input {
      width: 100%;
      height: 100%;
      border: 0;
      outline: 0;
      background: transparent;
      color: inherit;
      font: inherit;
      pointer-events: none;
    }
    .scada-modern-badge {
      position: absolute;
      left: -2px;
      top: -24px;
      height: 20px;
      max-width: 240px;
      padding: 2px 7px;
      border-radius: 4px;
      background: #2090a0;
      color: #fff;
      font: 12px Segoe UI, Arial, sans-serif;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      display: none;
      pointer-events: none;
    }
    .scada-modern-element[data-selected="true"] > .scada-modern-badge {
      display: block;
    }
    .scada-modern-handle {
      position: absolute;
      width: 9px;
      height: 9px;
      border: 1px solid #ffffff;
      background: #2090a0;
      box-shadow: 0 1px 3px rgba(15,42,48,.25);
      display: none;
    }
    .scada-modern-element[data-selected="true"] > .scada-modern-handle {
      display: block;
    }
    .scada-modern-handle[data-handle="nw"] { left: -6px; top: -6px; cursor: nwse-resize; }
    .scada-modern-handle[data-handle="ne"] { right: -6px; top: -6px; cursor: nesw-resize; }
    .scada-modern-handle[data-handle="sw"] { left: -6px; bottom: -6px; cursor: nesw-resize; }
    .scada-modern-handle[data-handle="se"] { right: -6px; bottom: -6px; cursor: nwse-resize; }
    body.scada-placement-active,
    body.scada-placement-active * {
      cursor: text !important;
    }
    #scada-text-editor {
      position: fixed;
      z-index: 2147483647;
      min-width: 80px;
      padding: 4px 6px;
      border: 2px solid #2090a0;
      background: #ffffff;
      color: #0f2a30;
      box-shadow: 0 10px 28px rgba(15,42,48,.20);
      font: 14px Segoe UI, Arial, sans-serif;
      outline: 0;
    }
    #scada-modern-editor {
      position: fixed;
      z-index: 2147483647;
      width: 300px;
      max-height: min(430px, calc(100vh - 24px));
      overflow: hidden;
      padding: 0;
      border: 1px solid rgba(15,42,48,.20);
      background: #ffffff;
      color: #0f2a30;
      box-shadow: 0 18px 42px rgba(15,42,48,.24);
      font: 13px Segoe UI, Arial, sans-serif;
    }
    #scada-modern-editor .editor-titlebar {
      display: flex;
      align-items: center;
      gap: 8px;
      min-height: 36px;
      padding: 6px 8px 6px 10px;
      border-bottom: 1px solid rgba(15,42,48,.14);
      background: #f2f8ef;
    }
    #scada-modern-editor h3 {
      flex: 1;
      margin: 0;
      font-size: 14px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    #scada-modern-editor .close {
      width: 32px;
      height: 28px;
      min-height: 28px;
      padding: 0;
      border: 0;
      background: transparent;
      color: #0f2a30;
      font: 16px "Segoe UI Symbol", "Segoe UI", Arial, sans-serif;
      line-height: 28px;
      text-align: center;
      border-radius: 2px;
    }
    #scada-modern-editor .close:hover {
      background: #c42b1c;
      color: #ffffff;
    }
    #scada-modern-editor label {
      display: block;
      margin: 7px 0 3px;
      color: #4e6a71;
      font-size: 12px;
    }
    #scada-modern-editor input,
    #scada-modern-editor select {
      width: 100%;
      box-sizing: border-box;
      min-height: 28px;
      border: 1px solid rgba(15,42,48,.24);
      padding: 4px 6px;
      font: 13px Segoe UI, Arial, sans-serif;
    }
    #scada-modern-editor .row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px;
    }
    #scada-modern-editor .tabs {
      display: flex;
      padding: 8px 8px 0;
      gap: 4px;
      border-bottom: 1px solid rgba(15,42,48,.14);
    }
    #scada-modern-editor .tab {
      min-height: 28px;
      padding: 4px 9px;
      border: 1px solid rgba(15,42,48,.16);
      border-bottom: 0;
      background: #ffffff;
      color: #4e6a71;
    }
    #scada-modern-editor .tab.active {
      background: #2090a0;
      border-color: #2090a0;
      color: #ffffff;
    }
    #scada-modern-editor .panel {
      display: none;
      max-height: 270px;
      overflow: auto;
      padding: 8px 10px 0;
    }
    #scada-modern-editor .panel.active {
      display: block;
    }
    #scada-modern-editor .actions {
      display: flex;
      justify-content: flex-end;
      gap: 6px;
      padding: 10px;
      border-top: 1px solid rgba(15,42,48,.12);
      margin-top: 0;
    }
    #scada-modern-editor button {
      min-height: 30px;
      border: 1px solid rgba(15,42,48,.20);
      background: #ffffff;
      color: #0f2a30;
      padding: 5px 10px;
      cursor: pointer;
    }
    #scada-modern-editor button.primary {
      background: #2090a0;
      border-color: #2090a0;
      color: #ffffff;
    }
    #scada-scene-resize-handle {
      position: absolute;
      right: -1px;
      bottom: -1px;
      width: 18px;
      height: 18px;
      z-index: 2147483200;
      box-sizing: border-box;
      border-left: 1px solid rgba(15,42,48,.35);
      border-top: 1px solid rgba(15,42,48,.35);
      background:
        linear-gradient(135deg, transparent 0 45%, rgba(15,42,48,.18) 45% 55%, transparent 55%),
        linear-gradient(135deg, transparent 0 62%, rgba(15,42,48,.28) 62% 70%, transparent 70%),
        rgba(255,255,255,.92);
      cursor: nwse-resize;
      pointer-events: auto;
      touch-action: none;
    }
  `;
  document.head.appendChild(style);

  const marquee = document.createElement('div');
  marquee.id = 'scada-extract-marquee';
  document.body.appendChild(marquee);

  const menu = document.createElement('div');
  menu.id = 'scada-extract-menu';
  document.body.appendChild(menu);

  let modernElements = [];
  let selectedModernId = null;
  let sceneCanvasResize = null;
  const selectedModernIds = new Set();
  let placementKind = null;
  let sourceDrag = null;
  let modernDrag = null;
  let activeTextEditor = null;
  let activeModernEditor = null;

  document.querySelectorAll('button.layer[disabled], input.layer[disabled], select.layer[disabled], textarea.layer[disabled]')
    .forEach(el => {
      el.setAttribute('data-scada-was-disabled', 'true');
      el.disabled = false;
      el.setAttribute('aria-disabled', 'true');
    });

  function getId(el) {
    return el && el.getAttribute ? (el.getAttribute('data-id') || '') : '';
  }

  function selectableSelectorForId(id) {
    const escaped = CSS.escape(id || '');
    return `[data-id="${escaped}"]:not(.scada-modern-element)`;
  }

  function getSelectableElementById(id) {
    return id ? document.querySelector(selectableSelectorForId(id)) : null;
  }

  function getSelectableElements() {
    return Array.from(document.querySelectorAll(selectableSelector))
      .filter(el => getId(el) && !hidden.has(getId(el)));
  }

  function getInventoryElements() {
    return Array.from(document.querySelectorAll(inventorySelector))
      .filter(el => getId(el) && !hidden.has(getId(el)));
  }

  function rememberRemovedSourceElement(el, id) {
    if (!el || !id || !el.parentNode || removedNodes.has(id)) return;
    removedNodes.set(id, {
      node: el,
      parent: el.parentNode,
      nextSibling: el.nextSibling
    });
  }

  function removeSourceElement(el) {
    const id = getId(el);
    if (!id) return;
    hidden.add(id);
    selected.delete(id);
    el.removeAttribute('data-scada-selected');
    el.removeAttribute('data-scada-deleted');
    rememberRemovedSourceElement(el, id);
    if (el.parentNode) {
      el.remove();
    }
  }

  function removeSourceElements(ids) {
    const removeIds = new Set(Array.isArray(ids) ? ids : []);
    if (!removeIds.size) return;
    removeIds.forEach(id => {
      const normalizedId = `${id || ''}`.trim();
      if (!normalizedId) return;
      const el = getSelectableElementById(normalizedId);
      if (el) {
        removeSourceElement(el);
      } else {
        hidden.add(normalizedId);
        selected.delete(normalizedId);
      }
    });
    postInventory();
    postSelection();
  }

  function restoreSourceElement(id, shouldSelect = false) {
    const normalizedId = `${id || ''}`.trim();
    if (!normalizedId) return;
    const entry = removedNodes.get(normalizedId);
    if (entry && entry.node && entry.parent && entry.parent.isConnected) {
      if (entry.nextSibling && entry.nextSibling.parentNode === entry.parent) {
        entry.parent.insertBefore(entry.node, entry.nextSibling);
      } else {
        entry.parent.appendChild(entry.node);
      }
    }
    removedNodes.delete(normalizedId);

    const el = getSelectableElementById(normalizedId);
    hidden.delete(normalizedId);
    selected.delete(normalizedId);
    if (!el) return;
    el.style.display = '';
    el.removeAttribute('data-scada-selected');
    el.removeAttribute('data-scada-deleted');
    if (shouldSelect) {
      selected.add(normalizedId);
      el.setAttribute('data-scada-selected', 'true');
    }
  }

  function getRenderOrder(el) {
    return getSelectableElements().indexOf(el);
  }

  function getElementBounds(el) {
    const rect = el.getBoundingClientRect();
    const surface = getPageSurface();
    const surfaceRect = surface.getBoundingClientRect();
    return {
      x: rect.left - surfaceRect.left + surface.scrollLeft,
      y: rect.top - surfaceRect.top + surface.scrollTop,
      width: rect.width,
      height: rect.height
    };
  }

  function setSourceElementGeometry(el, geometry) {
    if (!el || !geometry) return;
    if (isSvgSourceShape(el)) {
      setSvgSourceElementGeometry(el, geometry);
      return;
    }
    el.style.position = window.getComputedStyle(el).position === 'static' ? 'absolute' : el.style.position;
    el.style.left = `${Math.max(0, Math.round(geometry.x))}px`;
    el.style.top = `${Math.max(0, Math.round(geometry.y))}px`;
    if (Number.isFinite(geometry.width) && geometry.width > 0) {
      el.style.width = `${Math.max(1, Math.round(geometry.width))}px`;
    }
    if (Number.isFinite(geometry.height) && geometry.height > 0) {
      el.style.height = `${Math.max(1, Math.round(geometry.height))}px`;
    }
    el.style.transform = '';
  }

  function isSvgSourceShape(el) {
    return !!(el && el.ownerSVGElement && el !== el.ownerSVGElement);
  }

  function setSvgSourceElementGeometry(el, geometry) {
    const surface = getPageSurface();
    const surfaceRect = surface.getBoundingClientRect();
    const svg = el.ownerSVGElement;
    const svgRect = svg.getBoundingClientRect();
    const viewBox = svg.viewBox && svg.viewBox.baseVal ? svg.viewBox.baseVal : null;
    const scaleX = viewBox && svgRect.width ? viewBox.width / svgRect.width : 1;
    const scaleY = viewBox && svgRect.height ? viewBox.height / svgRect.height : 1;
    const originX = svgRect.left - surfaceRect.left + surface.scrollLeft;
    const originY = svgRect.top - surfaceRect.top + surface.scrollTop;
    const x = Math.max(0, Math.round(((geometry.x || 0) - originX) * scaleX + (viewBox ? viewBox.x : 0)));
    const y = Math.max(0, Math.round(((geometry.y || 0) - originY) * scaleY + (viewBox ? viewBox.y : 0)));
    const width = Number.isFinite(geometry.width) && geometry.width > 0
      ? Math.max(1, Math.round(geometry.width * scaleX))
      : null;
    const height = Number.isFinite(geometry.height) && geometry.height > 0
      ? Math.max(1, Math.round(geometry.height * scaleY))
      : null;

    const tag = (el.tagName || '').toLowerCase();
    if (tag === 'rect' || tag === 'image' || tag === 'foreignobject' || el.hasAttribute('x')) {
      el.setAttribute('x', `${x}`);
      el.setAttribute('y', `${y}`);
      if (width !== null) el.setAttribute('width', `${width}`);
      if (height !== null) el.setAttribute('height', `${height}`);
      el.removeAttribute('transform');
      return;
    }

    try {
      const box = el.getBBox();
      el.setAttribute('transform', `translate(${x - Math.round(box.x)} ${y - Math.round(box.y)})`);
    } catch {
    }
  }

  function selectedSourceElements() {
    return getSelectableElements().filter(el => selected.has(getId(el)));
  }

  function applySourceElementBounds(bounds) {
    if (!Array.isArray(bounds)) return;
    bounds.forEach(item => {
      const id = item?.Id || item?.id;
      if (!id) return;
      const el = getSelectableElementById(id);
      if (!el) return;
      setSourceElementGeometry(el, {
        x: item.X ?? item.x ?? 0,
        y: item.Y ?? item.y ?? 0,
        width: item.Width ?? item.width ?? el.offsetWidth,
        height: item.Height ?? item.height ?? el.offsetHeight
      });
    });
    postInventory();
    postSelection();
  }

  function toElementMessage(el, options = {}) {
    const bounds = getElementBounds(el);
    const computed = window.getComputedStyle(el);
    const includeLegacyMarkup = options.includeLegacyMarkup === true;
    let computedStyleText = '';
    let legacyMarkup = '';
    if (includeLegacyMarkup) {
      const clone = el.cloneNode(true);
      clone.removeAttribute('data-scada-selected');
      computedStyleText = Array.from(computed)
        .filter(name => !['outline', 'outline-color', 'outline-style', 'outline-width', 'outline-offset', 'box-shadow', 'cursor'].includes(name))
        .map(name => `${name}: ${computed.getPropertyValue(name)};`)
        .join(' ');
      clone.removeAttribute('class');
      clone.setAttribute('style', `${computedStyleText} ${clone.getAttribute('style') || ''}`.trim());
      legacyMarkup = clone.outerHTML || '';
    }
    const rawMetadata = {
      tagName: el.tagName.toLowerCase(),
      computedStyleText,
      transform: computed.transform || '',
      opacity: computed.opacity || '',
      display: computed.display || '',
      position: computed.position || '',
      left: computed.left || '',
      top: computed.top || '',
      fill: computed.fill || '',
      stroke: computed.stroke || '',
      strokeWidth: computed.strokeWidth || '',
      zIndex: computed.zIndex || ''
    };
    return {
      id: getId(el),
      name: el.getAttribute('data-name') || getId(el),
      elementType: el.getAttribute('data-type') || el.tagName.toLowerCase(),
      text: getEditableText(el),
      isTextLike: isTextLikeElement(el),
      x: bounds.x,
      y: bounds.y,
      width: bounds.width,
      height: bounds.height,
      fontFamily: computed.fontFamily || '',
      fontSize: parseFloat(computed.fontSize || '0') || 0,
      foreground: computed.color || '',
      background: computed.backgroundColor || '',
      legacyMarkup,
      rawMetadataJson: includeLegacyMarkup ? JSON.stringify(rawMetadata) : '',
      renderOrder: getRenderOrder(el)
    };
  }

  function findSelectable(target) {
    return target && target.closest ? target.closest(selectableSelector) : null;
  }

  function getEditableText(el) {
    if (!el) return '';
    if ((el.tagName || '').toLowerCase() === 'button') return el.textContent || el.value || '';
    if ('value' in el && typeof el.value === 'string') return el.value;
    return el.textContent || '';
  }

  function setEditableText(el, text) {
    if (!el) return;
    if ((el.tagName || '').toLowerCase() === 'button') {
      el.textContent = text;
      return;
    }
    if ('value' in el && typeof el.value === 'string') {
      el.value = text;
      return;
    }
    el.textContent = text;
  }

  function isTextLikeElement(el) {
    const type = (el?.getAttribute?.('data-type') || '').toLowerCase();
    const tag = (el?.tagName || '').toLowerCase();
    return type.includes('text') ||
      tag === 'text' ||
      tag === 'span' ||
      tag === 'label' ||
      tag === 'button' ||
      tag === 'input' ||
      tag === 'textarea' ||
      tag === 'div';
  }

  function closeTextEditor(commit) {
    if (!activeTextEditor) return;
    const { editor, target, id, originalText } = activeTextEditor;
    const newText = editor.value;
    editor.remove();
    activeTextEditor = null;

    if (!commit) {
      setEditableText(target, originalText);
      return;
    }

    setEditableText(target, newText);
    window.chrome?.webview?.postMessage({ type: 'editLegacyText', id, text: newText });
    postInventory();
  }

  function beginLegacyTextEdit(target) {
    if (!target || !isTextLikeElement(target)) return false;
    closeTextEditor(false);
    const id = getId(target);
    if (!id) return false;

    const rect = target.getBoundingClientRect();
    const originalText = getEditableText(target);
    const editor = document.createElement('input');
    editor.id = 'scada-text-editor';
    editor.type = 'text';
    editor.value = originalText;
    editor.style.left = `${Math.max(0, rect.left)}px`;
    editor.style.top = `${Math.max(0, rect.top)}px`;
    editor.style.width = `${Math.max(90, rect.width)}px`;
    editor.style.height = `${Math.max(28, rect.height)}px`;
    document.body.appendChild(editor);
    activeTextEditor = { editor, target, id, originalText };
    editor.focus();
    editor.select();

    editor.addEventListener('keydown', event => {
      if (event.key === 'Enter') {
        closeTextEditor(true);
        event.preventDefault();
      }
      if (event.key === 'Escape') {
        closeTextEditor(false);
        event.preventDefault();
      }
    });
    editor.addEventListener('blur', () => closeTextEditor(true));
    return true;
  }

  function setSelected(el, value) {
    const id = getId(el);
    if (!id) return;
    if (value) {
      selected.add(id);
      el.setAttribute('data-scada-selected', 'true');
    } else {
      selected.delete(id);
      el.removeAttribute('data-scada-selected');
    }
  }

  function clearSelection() {
    getSelectableElements().forEach(el => el.removeAttribute('data-scada-selected'));
    selected.clear();
    postSelection();
  }

  function clearModernSelection(post = true) {
    selectedModernId = null;
    selectedModernIds.clear();
    document.querySelectorAll('.scada-modern-element').forEach(element => {
      element.dataset.selected = 'false';
    });
    closeModernEditor();
    if (post) {
      window.chrome?.webview?.postMessage({ type: 'clearObjectSelection' });
    }
  }

  function clearAllSelection(post = true) {
    getSelectableElements().forEach(el => el.removeAttribute('data-scada-selected'));
    selected.clear();
    clearModernSelection(false);
    postSelection();
    if (post) {
      window.chrome?.webview?.postMessage({ type: 'clearAllSelection' });
    }
  }

  function selectLegacyElements(ids) {
    const requested = new Set(Array.isArray(ids) ? ids : []);
    getSelectableElements().forEach(el => {
      setSelected(el, requested.has(getId(el)));
    });
    if (requested.size > 0) {
      clearModernSelection(false);
    }
    postSelection();
  }

  function postSelection() {
    const items = getSelectableElements()
      .filter(el => selected.has(getId(el)))
      .map(toElementMessage);
    window.chrome?.webview?.postMessage({ type: 'selection', items });
  }

  function getSelectedMessages() {
    return getSelectableElements()
      .filter(el => selected.has(getId(el)))
      .map(toElementMessage);
  }

  function getSelectedMessagesForStudio() {
    return getSelectableElements()
      .filter(el => selected.has(getId(el)))
      .map(el => toElementMessage(el, { includeLegacyMarkup: true }));
  }

  function postInventory() {
    const items = getInventoryElements().map(toElementMessage);
    window.chrome?.webview?.postMessage({ type: 'inventory', items });
  }

  function hideMenu() {
    menu.style.display = 'none';
  }

  function renderContextMenuCommands(commands) {
    menu.textContent = '';
    const createCommandButton = command => {
      const button = document.createElement('button');
      button.type = 'button';
      button.dataset.commandId = command.Id;
      button.textContent = command.Label || command.Id;
      if (command.IsEnabled === false || command.isEnabled === false) {
        button.disabled = true;
        button.setAttribute('aria-disabled', 'true');
        const reason = command.DisabledReason || command.disabledReason || '';
        if (reason) {
          button.title = reason;
        }
      }
      return button;
    };

    const createCommandNode = command => {
      const children = command.Children || command.children || [];
      if (Array.isArray(children) && children.length) {
        const wrapper = document.createElement('div');
        wrapper.className = 'submenu';
        const parent = document.createElement('button');
        parent.type = 'button';
        parent.textContent = command.Label || command.Id;
        parent.setAttribute('aria-haspopup', 'true');
        wrapper.appendChild(parent);
        const panel = document.createElement('div');
        panel.className = 'submenu-panel';
        children
          .filter(child => child && child.Id)
          .forEach(child => panel.appendChild(createCommandButton(child)));
        wrapper.appendChild(panel);
        wrapper.addEventListener('mouseenter', () => positionSubmenuPanel(wrapper));
        wrapper.addEventListener('focusin', () => positionSubmenuPanel(wrapper));
        return wrapper;
      }

      return createCommandButton(command);
    };

    (Array.isArray(commands) ? commands : [])
      .filter(command => command && command.Id)
      .forEach(command => menu.appendChild(createCommandNode(command)));
  }

  function getContextMenuBounds() {
    const margin = 8;
    return {
      left: margin,
      top: margin,
      right: Math.max(margin, window.innerWidth - margin),
      bottom: Math.max(margin, window.innerHeight - margin)
    };
  }

  function clampContextCoordinate(value, size, min, max) {
    if (!Number.isFinite(value)) {
      return min;
    }

    return Math.max(min, Math.min(value, max - size));
  }

  function positionSubmenuPanel(wrapper) {
    const panel = wrapper.querySelector(':scope > .submenu-panel');
    if (!panel) {
      return;
    }

    const bounds = getContextMenuBounds();
    delete wrapper.dataset.submenuX;
    panel.style.left = '';
    panel.style.right = '';
    panel.style.top = '-4px';
    panel.style.maxHeight = '';
    panel.style.overflowY = '';
    panel.style.visibility = 'hidden';
    panel.style.display = 'block';

    const wrapperRect = wrapper.getBoundingClientRect();
    const panelRect = panel.getBoundingClientRect();
    const opensRight = wrapperRect.right + panelRect.width <= bounds.right;
    const opensLeft = wrapperRect.left - panelRect.width >= bounds.left;
    if (!opensRight && opensLeft) {
      wrapper.dataset.submenuX = 'left';
      panel.style.left = 'auto';
      panel.style.right = 'calc(100% - 1px)';
    }

    const maxPanelHeight = Math.max(42, window.innerHeight - 16);
    const panelHeight = Math.min(panelRect.height, maxPanelHeight);
    const desiredTop = -4;
    const viewportTop = wrapperRect.top + desiredTop;
    const adjustedViewportTop = clampContextCoordinate(viewportTop, panelHeight, bounds.top, bounds.bottom);
    panel.style.top = `${adjustedViewportTop - wrapperRect.top}px`;
    if (panelRect.height > maxPanelHeight) {
      panel.style.maxHeight = `${maxPanelHeight}px`;
      panel.style.overflowY = 'auto';
    }

    panel.style.display = '';
    panel.style.visibility = '';
  }

  function showContextMenu(payload) {
    const commands = payload?.commands || payload?.Commands || [];
    renderContextMenuCommands(commands);
    if (!menu.children.length) {
      hideMenu();
      return;
    }
    const x = payload?.x ?? payload?.X ?? 0;
    const y = payload?.y ?? payload?.Y ?? 0;
    const bounds = getContextMenuBounds();
    menu.style.maxHeight = '';
    menu.style.overflowY = '';
    menu.style.visibility = 'hidden';
    menu.style.display = 'block';
    menu.style.left = `${bounds.left}px`;
    menu.style.top = `${bounds.top}px`;
    const menuRect = menu.getBoundingClientRect();
    const maxMenuHeight = Math.max(42, window.innerHeight - 16);
    const menuWidth = Math.max(180, menuRect.width || 180);
    const menuHeight = Math.min(Math.max(42, menuRect.height || 42), maxMenuHeight);
    if (menuRect.height > maxMenuHeight) {
      menu.style.maxHeight = `${maxMenuHeight}px`;
      menu.style.overflowY = 'auto';
    }
    const left = clampContextCoordinate(x, menuWidth, bounds.left, bounds.right);
    const top = clampContextCoordinate(y, menuHeight, bounds.top, bounds.bottom);
    menu.style.left = `${left}px`;
    menu.style.top = `${top}px`;
    menu.style.visibility = '';
  }

  function getPageSurface() {
    return document.querySelector('.page') || document.querySelector('#scada-root') || document.body;
  }

  function ensureModernLayer() {
    const surface = getPageSurface();
    let layer = document.getElementById('scada-modern-layer');
    if (!layer) {
      layer = document.createElement('div');
      layer.id = 'scada-modern-layer';
      surface.appendChild(layer);
    }
    const position = window.getComputedStyle(surface).position;
    if (position === 'static') {
      surface.style.position = 'relative';
    }
    return layer;
  }

  function setSceneSurfaceSize(width, height) {
    const boundedWidth = Math.max(160, Math.round(width || 0));
    const boundedHeight = Math.max(120, Math.round(height || 0));
    const surface = getPageSurface();
    document.documentElement.style.setProperty('--page-w', `${boundedWidth}px`);
    document.documentElement.style.setProperty('--page-h', `${boundedHeight}px`);
    surface.style.setProperty('--page-w', `${boundedWidth}px`);
    surface.style.setProperty('--page-h', `${boundedHeight}px`);
    surface.style.width = `${boundedWidth}px`;
    surface.style.height = `${boundedHeight}px`;
    surface.style.minWidth = `${boundedWidth}px`;
    surface.style.minHeight = `${boundedHeight}px`;
    surface.style.overflow = 'hidden';

    const layer = document.getElementById('scada-modern-layer');
    if (layer && layer.parentElement === surface) {
      layer.style.width = `${boundedWidth}px`;
      layer.style.height = `${boundedHeight}px`;
    }

    return { width: boundedWidth, height: boundedHeight };
  }

  function ensureSceneResizeHandle() {
    const surface = getPageSurface();
    let handle = document.getElementById('scada-scene-resize-handle');
    if (!handle) {
      handle = document.createElement('div');
      handle.id = 'scada-scene-resize-handle';
      handle.setAttribute('aria-label', 'Redimensionner la workzone');
      surface.appendChild(handle);
    } else if (handle.parentElement !== surface) {
      surface.appendChild(handle);
    }

    const position = window.getComputedStyle(surface).position;
    if (position === 'static') {
      surface.style.position = 'relative';
    }

    return handle;
  }

  function cssText(value, fallback) {
    return value === undefined || value === null || value === '' ? fallback : value;
  }

  function shadowCss(preset) {
    if (preset === 'Soft') return '0 8px 18px rgba(15,42,48,.16)';
    if (preset === 'Raised') return '0 12px 26px rgba(15,42,48,.24)';
    if (preset === 'Inset') return 'inset 0 2px 8px rgba(15,42,48,.22)';
    return 'none';
  }

  function getModernElementById(id) {
    return modernElements.find(element => element.Id === id) || null;
  }

  function postModernGeometry(id, before, after) {
    window.chrome?.webview?.postMessage({
      type: 'updateSceneObjectGeometry',
      id,
      beforeX: Math.max(0, Math.round(before.x)),
      beforeY: Math.max(0, Math.round(before.y)),
      beforeWidth: Math.max(8, Math.round(before.width)),
      beforeHeight: Math.max(8, Math.round(before.height)),
      x: Math.max(0, Math.round(after.x)),
      y: Math.max(0, Math.round(after.y)),
      width: Math.max(8, Math.round(after.width)),
      height: Math.max(8, Math.round(after.height))
    });
  }

  function postSelectionMove(targetKind, ids, deltaX, deltaY, items = null) {
    window.chrome?.webview?.postMessage({
      type: 'moveSelectionBy',
      targetKind,
      ids,
      items,
      deltaX: Math.round(deltaX),
      deltaY: Math.round(deltaY)
    });
  }

  function closeModernEditor() {
    activeModernEditor?.remove?.();
    activeModernEditor = null;
  }

  function appendField(container, labelText, value, name, type = 'text') {
    const label = document.createElement('label');
    label.textContent = labelText;
    const input = document.createElement('input');
    input.name = name;
    input.type = type;
    input.value = value ?? '';
    container.appendChild(label);
    container.appendChild(input);
    return input;
  }

  function appendSelect(container, labelText, value, name, options) {
    const label = document.createElement('label');
    label.textContent = labelText;
    const select = document.createElement('select');
    select.name = name;
    options.forEach(option => {
      const item = document.createElement('option');
      item.value = option;
      item.textContent = option;
      if (String(option).toLowerCase() === String(value || '').toLowerCase()) {
        item.selected = true;
      }
      select.appendChild(item);
    });
    container.appendChild(label);
    container.appendChild(select);
    return select;
  }

  function appendRow(container, fields) {
    const row = document.createElement('div');
    row.className = 'row';
    fields.forEach(field => {
      const cell = document.createElement('div');
      field(cell);
      row.appendChild(cell);
    });
    container.appendChild(row);
  }

  function openModernEditor(element, clientX, clientY) {
    if (!element) return;
    closeModernEditor();
    const isTextObject = element.Kind === 'Text' || element.Kind === 'Button';
    const isNumeric = element.Kind === 'InputNumeric';
    const style = element.Style || {};
    const data = element.Data || {};
    const isReadOnlyNumeric = isNumeric && data.IsReadOnly === true;
    const editor = document.createElement('div');
    editor.id = 'scada-modern-editor';
    editor.style.left = `${Math.min(clientX, Math.max(12, window.innerWidth - 320))}px`;
    editor.style.top = `${Math.min(clientY, Math.max(12, window.innerHeight - 450))}px`;

    const titlebar = document.createElement('div');
    titlebar.className = 'editor-titlebar';
    const title = document.createElement('h3');
    title.textContent = isTextObject
      ? `${element.DisplayName || element.Id} - champ texte`
      : isNumeric
        ? `${element.DisplayName || element.Id} - ${isReadOnlyNumeric ? 'affichage numerique' : 'entree numerique'}`
        : `${element.DisplayName || element.Id} - entree texte`;
    const close = document.createElement('button');
    close.type = 'button';
    close.className = 'close';
    close.textContent = 'X';
    close.title = 'Fermer';
    close.setAttribute('aria-label', 'Fermer');
    titlebar.appendChild(title);
    titlebar.appendChild(close);
    editor.appendChild(titlebar);

    const tabs = document.createElement('div');
    tabs.className = 'tabs';
    editor.appendChild(tabs);

    function createPanel(id, label, active = false) {
      const tab = document.createElement('button');
      tab.type = 'button';
      tab.className = `tab${active ? ' active' : ''}`;
      tab.textContent = label;
      const panel = document.createElement('div');
      panel.className = `panel${active ? ' active' : ''}`;
      panel.dataset.panel = id;
      tab.addEventListener('click', () => {
        editor.querySelectorAll('.tab').forEach(item => item.classList.remove('active'));
        editor.querySelectorAll('.panel').forEach(item => item.classList.remove('active'));
        tab.classList.add('active');
        panel.classList.add('active');
      });
      tabs.appendChild(tab);
      editor.appendChild(panel);
      return panel;
    }

    const generalPanel = createPanel('general', 'General', true);
    const dataPanel = createPanel('data', 'Donnees');
    const stylePanel = createPanel('style', 'Style');
    const buttonBehavior = element.ButtonBehavior || {};
    const buttonHover = buttonBehavior.Hover || {};
    let buttonPanel = null;
    if (element.Kind === 'Button') {
      buttonPanel = createPanel('button', 'Bouton');
    }
    const eventButton = document.createElement('button');
    eventButton.type = 'button';
    eventButton.className = 'tab';
    eventButton.textContent = 'Evenement';
    eventButton.addEventListener('click', () => {
      window.chrome?.webview?.postMessage({ type: 'openSceneObjectEvents', id: element.Id });
    });
    tabs.appendChild(eventButton);

    appendField(generalPanel, 'Nom', element.DisplayName || element.Id, 'DisplayName');
    appendRow(generalPanel, [
      cell => appendField(cell, 'X', element.X, 'X', 'number'),
      cell => appendField(cell, 'Y', element.Y, 'Y', 'number')
    ]);
    appendRow(generalPanel, [
      cell => appendField(cell, 'Largeur', element.Width, 'Width', 'number'),
      cell => appendField(cell, 'Hauteur', element.Height, 'Height', 'number')
    ]);

    if (!isTextObject) {
      appendField(dataPanel, 'Placeholder', data.Placeholder || '', 'Placeholder');
    }
    if (isNumeric) {
      appendField(dataPanel, 'Valeur', data.Value ?? '', 'ValueText', 'number');
      appendRow(dataPanel, [
        cell => appendField(cell, 'Min', data.Minimum ?? '', 'MinimumText', 'number'),
        cell => appendField(cell, 'Max', data.Maximum ?? '', 'MaximumText', 'number')
      ]);
      appendRow(dataPanel, [
        cell => appendField(cell, 'Decimales', data.Decimals ?? '', 'DecimalsText', 'number'),
        cell => appendField(cell, 'Unite', data.Unit || '', 'Unit')
      ]);
      appendField(dataPanel, 'Format affichage', data.DisplayFormat || '', 'DisplayFormat');
    } else {
      appendField(dataPanel, 'Texte', data.Text ?? '', 'Text');
    }
    if (!isTextObject) {
      appendField(dataPanel, 'Mapping / Tag', data.TagBinding || '', 'TagBinding');
    }

    appendRow(stylePanel, [
      cell => appendField(cell, 'Fond', style.Background || '#FFFFFF', 'Background'),
      cell => appendField(cell, 'Taille police', style.FontSize || 14, 'FontSize', 'number')
    ]);
    appendRow(stylePanel, [
      cell => appendSelect(cell, 'Bordure', style.BorderStyle || 'Solid', 'BorderStyle', ['Solid', 'Dashed', 'Dotted', 'None']),
      cell => appendField(cell, 'Largeur bordure', style.BorderWidth ?? 1, 'BorderWidth', 'number')
    ]);
    if (buttonPanel) {
      const disabledLabel = document.createElement('label');
      const disabled = document.createElement('input');
      disabled.type = 'checkbox';
      disabled.name = 'ButtonDisabled';
      disabled.checked = buttonBehavior.IsDisabled === true;
      disabledLabel.appendChild(disabled);
      disabledLabel.appendChild(document.createTextNode(' Bouton desactive'));
      buttonPanel.appendChild(disabledLabel);

      const hoverLabel = document.createElement('label');
      const hoverEnabled = document.createElement('input');
      hoverEnabled.type = 'checkbox';
      hoverEnabled.name = 'ButtonHoverEnabled';
      hoverEnabled.checked = buttonHover.Enabled !== false;
      hoverLabel.appendChild(hoverEnabled);
      hoverLabel.appendChild(document.createTextNode(' Survol automatique'));
      buttonPanel.appendChild(hoverLabel);
      appendField(buttonPanel, 'Fond survol', buttonHover.Background || '#EAF5F7', 'ButtonHoverBackground');
      appendField(buttonPanel, 'Texte survol', buttonHover.Foreground || '#0F2A30', 'ButtonHoverForeground');
      appendField(buttonPanel, 'Bordure survol', buttonHover.BorderColor || '#2090A0', 'ButtonHoverBorderColor');
    }

    const readOnlyLabel = document.createElement('label');
    const readOnly = document.createElement('input');
    readOnly.type = 'checkbox';
    readOnly.name = 'IsReadOnly';
    readOnly.checked = data.IsReadOnly === true;
    readOnlyLabel.appendChild(readOnly);
    readOnlyLabel.appendChild(document.createTextNode(' Lecture seulement'));
    if (!isTextObject) {
      dataPanel.appendChild(readOnlyLabel);
    }

    const actions = document.createElement('div');
    actions.className = 'actions';
    const cancel = document.createElement('button');
    cancel.type = 'button';
    cancel.textContent = 'Annuler';
    const apply = document.createElement('button');
    apply.type = 'button';
    apply.className = 'primary';
    apply.textContent = 'Appliquer';
    actions.appendChild(cancel);
    actions.appendChild(apply);
    editor.appendChild(actions);

    close.addEventListener('click', closeModernEditor);
    cancel.addEventListener('click', closeModernEditor);
    editor.addEventListener('keydown', event => {
      if (event.key === 'Escape') {
        closeModernEditor();
        event.preventDefault();
        event.stopPropagation();
      }
    });
    apply.addEventListener('click', () => {
      const field = name => editor.querySelector(`[name="${name}"]`);
      window.chrome?.webview?.postMessage({
        type: 'updateSceneObjectProperties',
        id: element.Id,
        displayName: field('DisplayName')?.value || '',
        placeholder: field('Placeholder')?.value || '',
        text: field('Text')?.value || '',
        valueText: field('ValueText')?.value || '',
        x: Number(field('X')?.value || element.X),
        y: Number(field('Y')?.value || element.Y),
        width: Number(field('Width')?.value || element.Width),
        height: Number(field('Height')?.value || element.Height),
        minimumText: field('MinimumText')?.value || '',
        maximumText: field('MaximumText')?.value || '',
        decimalsText: field('DecimalsText')?.value || '',
        unit: field('Unit')?.value || '',
        displayFormat: field('DisplayFormat')?.value || '',
        tagBinding: field('TagBinding')?.value || '',
        background: field('Background')?.value || '',
        fontSize: Number(field('FontSize')?.value || style.FontSize || 14),
        borderStyle: field('BorderStyle')?.value || '',
        borderWidth: Number(field('BorderWidth')?.value || style.BorderWidth || 0),
        buttonDisabled: field('ButtonDisabled')?.checked === true,
        buttonHoverEnabled: field('ButtonHoverEnabled')?.checked !== false,
        buttonHoverBackground: field('ButtonHoverBackground')?.value || '',
        buttonHoverForeground: field('ButtonHoverForeground')?.value || '',
        buttonHoverBorderColor: field('ButtonHoverBorderColor')?.value || '',
        isReadOnly: field('IsReadOnly')?.checked === true
      });
      closeModernEditor();
    });

    document.body.appendChild(editor);
    activeModernEditor = editor;
    editor.tabIndex = -1;
    editor.focus();
  }

  function selectModernElementInDom(id) {
    selectedModernIds.clear();
    if (id) selectedModernIds.add(id);
    selectedModernId = id || null;
    document.querySelectorAll('.scada-modern-element').forEach(element => {
      element.dataset.selected = selectedModernIds.has(element.dataset.id) ? 'true' : 'false';
    });
    const selectedElement = selectedModernId
      ? document.querySelector(`.scada-modern-element[data-id="${CSS.escape(selectedModernId)}"]`)
      : null;
    selectedElement?.focus?.({ preventScroll: true });
  }

  function syncModernSelectionInDom() {
    document.querySelectorAll('.scada-modern-element').forEach(element => {
      element.dataset.selected = selectedModernIds.has(element.dataset.id) ? 'true' : 'false';
    });
    const selectedElement = selectedModernId
      ? document.querySelector(`.scada-modern-element[data-id="${CSS.escape(selectedModernId)}"]`)
      : null;
    selectedElement?.focus?.({ preventScroll: true });
  }

  function addModernElementToSelection(id) {
    if (!id) return;
    selectedModernIds.add(id);
    selectedModernId = id;
    syncModernSelectionInDom();
  }

  function toggleModernElementInSelection(id) {
    if (!id) return;
    if (selectedModernIds.has(id)) {
      selectedModernIds.delete(id);
      selectedModernId = selectedModernIds.size ? Array.from(selectedModernIds).at(-1) : null;
    } else {
      selectedModernIds.add(id);
      selectedModernId = id;
    }
    syncModernSelectionInDom();
  }

  function readWrapperGeometry(wrapper) {
    return {
      x: parseFloat(wrapper.style.left) || 0,
      y: parseFloat(wrapper.style.top) || 0,
      width: parseFloat(wrapper.style.width) || wrapper.offsetWidth,
      height: parseFloat(wrapper.style.height) || wrapper.offsetHeight
    };
  }

  function setWrapperGeometry(wrapper, geometry) {
    wrapper.style.left = `${Math.max(0, geometry.x)}px`;
    wrapper.style.top = `${Math.max(0, geometry.y)}px`;
    wrapper.style.width = `${Math.max(8, geometry.width)}px`;
    wrapper.style.height = `${Math.max(8, geometry.height)}px`;
  }

  function getSceneMoveWrapper(wrapper) {
    if (!wrapper?.classList?.contains('scada-modern-child')) {
      return wrapper;
    }
    return wrapper.parentElement?.closest?.('.scada-modern-group') || wrapper;
  }

  function renderModernElements(elements) {
    modernElements = Array.isArray(elements) ? elements : [];
    const layer = ensureModernLayer();
    layer.innerHTML = '';
    const renderElement = (element, parentWrapper = null) => {
      const style = element.Style || {};
      const data = element.Data || {};
      const buttonBehavior = element.ButtonBehavior || {};
      const wrapper = document.createElement('div');
      const isGroup = element.Kind === 'Group';
      wrapper.className = `scada-modern-element${isGroup ? ' scada-modern-group' : ''}${parentWrapper ? ' scada-modern-child' : ''}`;
      wrapper.tabIndex = 0;
      wrapper.dataset.id = element.Id;
      wrapper.dataset.selected = element.IsSelected ? 'true' : 'false';
      wrapper.dataset.groupContext = element.IsGroupContextSelected ? 'true' : 'false';
      if (parentWrapper?.dataset?.id) {
        wrapper.dataset.parentGroupId = parentWrapper.dataset.id;
      }
      if (element.IsSelected) {
        selectedModernIds.add(element.Id);
        selectedModernId = element.Id;
      }
      wrapper.style.left = `${element.X}px`;
      wrapper.style.top = `${element.Y}px`;
      wrapper.style.width = `${element.Width}px`;
      wrapper.style.height = `${element.Height}px`;
      wrapper.style.zIndex = `${Number(element.RenderIndex ?? element.renderIndex ?? 0)}`;
      wrapper.style.fontFamily = cssText(style.FontFamily, 'Segoe UI');
      wrapper.style.fontSize = `${cssText(style.FontSize, 14)}px`;
      wrapper.style.color = cssText(style.Foreground, '#0f2a30');
      wrapper.style.background = cssText(style.Background, '#ffffff');
      wrapper.style.borderStyle = cssText(style.BorderStyle, 'solid').toLowerCase();
      wrapper.style.borderWidth = `${cssText(style.BorderWidth, 1)}px`;
      wrapper.style.borderColor = cssText(style.BorderColor, '#8aa0a6');
      wrapper.style.boxShadow = shadowCss(style.ShadowPreset);
      if (style.AdvancedCss) {
        wrapper.style.cssText += ';' + style.AdvancedCss;
      }
      if (isGroup) {
        (element.Children || element.children || []).forEach(child => {
          wrapper.appendChild(renderElement(child, wrapper));
        });
      } else if (element.Kind === 'Shape') {
        const shape = document.createElement('div');
        shape.style.width = '100%';
        shape.style.height = '100%';
        shape.style.background = cssText(style.Background, 'transparent');
        shape.style.border = '0';
        shape.style.pointerEvents = 'none';
        wrapper.appendChild(shape);
      } else if (element.Kind === 'Text') {
        const text = document.createElement('span');
        text.textContent = data.Text || element.DisplayName || 'Texte';
        text.style.width = '100%';
        text.style.overflow = 'hidden';
        text.style.textOverflow = 'ellipsis';
        text.style.whiteSpace = 'nowrap';
        wrapper.appendChild(text);
      } else if (element.Kind === 'Button') {
        const button = document.createElement('button');
        button.type = 'button';
        button.dataset.scadaButtonBehavior = JSON.stringify(buttonBehavior || {});
        button.textContent = data.Text || data.Placeholder || element.DisplayName || 'Bouton';
        button.style.width = '100%';
        button.style.height = '100%';
        button.style.boxSizing = 'border-box';
        button.style.font = 'inherit';
        button.style.color = 'inherit';
        button.style.background = 'transparent';
        button.style.border = '0';
        button.style.overflow = 'hidden';
        button.style.textOverflow = 'ellipsis';
        button.style.whiteSpace = 'nowrap';
        button.style.pointerEvents = 'none';
        wrapper.appendChild(button);
      } else if (element.Kind === 'InputNumeric' && data.IsReadOnly === true) {
        const value = document.createElement('span');
        value.textContent = data.Value ?? data.DisplayFormat ?? data.Placeholder ?? '';
        value.style.width = '100%';
        value.style.overflow = 'hidden';
        value.style.textOverflow = 'ellipsis';
        value.style.whiteSpace = 'nowrap';
        wrapper.appendChild(value);
      } else if (element.Kind === 'Custom') {
        wrapper.style.padding = '0';
        wrapper.style.alignItems = 'stretch';
        wrapper.style.justifyContent = 'stretch';
        const custom = document.createElement('div');
        custom.className = 'scada-modern-custom-content';
        custom.style.width = '100%';
        custom.style.height = '100%';
        custom.style.pointerEvents = 'none';
        custom.style.overflow = 'visible';
        custom.innerHTML = data.Text || '';
        custom.querySelectorAll('svg').forEach(svg => {
          svg.style.width = '100%';
          svg.style.height = '100%';
          svg.style.display = 'block';
          svg.style.overflow = 'visible';
        });
        wrapper.appendChild(custom);
      } else {
        const input = document.createElement('input');
        input.type = element.Kind === 'InputNumeric' ? 'number' : 'text';
        input.readOnly = data.IsReadOnly === true;
        input.placeholder = data.Placeholder || '';
        input.value = element.Kind === 'InputNumeric'
          ? (data.Value ?? '')
          : (data.Text ?? '');
        wrapper.appendChild(input);
      }

      const badge = document.createElement('div');
      badge.className = 'scada-modern-badge';
      badge.textContent = `${element.DisplayName || element.Id} - ${element.Kind}`;
      wrapper.appendChild(badge);

      ['nw', 'ne', 'sw', 'se'].forEach(handle => {
        const grip = document.createElement('span');
        grip.className = 'scada-modern-handle';
        grip.dataset.handle = handle;
        wrapper.appendChild(grip);
      });

      wrapper.addEventListener('pointerdown', event => {
        if (placementKind) {
          return;
        }
        if (event.target?.closest?.('.scada-modern-element') !== wrapper) {
          return;
        }
        event.preventDefault();
        event.stopPropagation();
        const sceneMoveWrapper = getSceneMoveWrapper(wrapper);
        const sceneMoveId = sceneMoveWrapper?.dataset?.id || element.Id;
        const preserveModernSelection = !event.ctrlKey && !event.shiftKey && selectedModernIds.has(sceneMoveId);
        if (event.ctrlKey || event.shiftKey) {
          toggleModernElementInSelection(sceneMoveId);
        } else {
          clearSelection();
          if (!preserveModernSelection) {
            selectModernElementInDom(sceneMoveId);
          }
        }
        if (!preserveModernSelection) {
          window.chrome?.webview?.postMessage({
            type: 'selectSceneObject',
            id: sceneMoveId,
            additive: event.ctrlKey || event.shiftKey,
            toggle: event.ctrlKey || event.shiftKey
          });
        }
        const geometry = readWrapperGeometry(sceneMoveWrapper);
        const movingWrappers = event.target?.classList?.contains('scada-modern-handle')
          ? [sceneMoveWrapper]
          : Array.from(document.querySelectorAll('.scada-modern-element'))
              .filter(item => selectedModernIds.has(item.dataset.id))
              .map(item => getSceneMoveWrapper(item))
              .filter((item, index, items) => item && items.indexOf(item) === index);
        modernDrag = {
          id: sceneMoveId,
          wrapper: sceneMoveWrapper,
          mode: event.target?.classList?.contains('scada-modern-handle') ? 'resize' : 'move',
          handle: event.target?.dataset?.handle || '',
          startClientX: event.clientX,
          startClientY: event.clientY,
          startX: geometry.x,
          startY: geometry.y,
          startWidth: geometry.width,
          startHeight: geometry.height,
          items: movingWrappers.map(item => ({
            id: item.dataset.id,
            wrapper: item,
            geometry: readWrapperGeometry(item)
          }))
        };
        sceneMoveWrapper.setPointerCapture?.(event.pointerId);
      }, true);

      wrapper.addEventListener('dblclick', event => {
        if (placementKind) {
          return;
        }
        if (event.target?.closest?.('.scada-modern-element') !== wrapper) {
          return;
        }
        event.preventDefault();
        event.stopPropagation();
        if (!event.ctrlKey && !event.shiftKey) {
          clearSelection();
        }
        selectedModernId = element.Id;
        selectModernElementInDom(element.Id);
        window.chrome?.webview?.postMessage({ type: 'openSceneObjectProperties', id: element.Id });
        openModernEditor(element, event.clientX, event.clientY);
      }, true);

      wrapper.addEventListener('contextmenu', event => {
        if (placementKind) {
          return;
        }
        if (event.target?.closest?.('.scada-modern-element') !== wrapper) {
          return;
        }
        event.preventDefault();
        event.stopPropagation();
        const sceneContextWrapper = getSceneMoveWrapper(wrapper);
        const sceneContextId = sceneContextWrapper?.dataset?.id || element.Id;
        if (!selectedModernIds.has(sceneContextId) && !event.ctrlKey && !event.shiftKey) {
          clearSelection();
          selectModernElementInDom(sceneContextId);
        } else if (event.ctrlKey || event.shiftKey) {
          toggleModernElementInSelection(sceneContextId);
        }
        window.chrome?.webview?.postMessage({
          type: 'contextMenuRequest',
          targetKind: 'object',
          id: sceneContextId,
          x: event.clientX,
          y: event.clientY,
          backgroundColor: getBackgroundColor()
        });
      }, true);

      return wrapper;
    };
    modernElements.forEach(element => layer.appendChild(renderElement(element)));
  }

  function applyTextOverrides(overrides) {
    if (!Array.isArray(overrides)) return;
    overrides.forEach(overrideItem => {
      if (!overrideItem || !overrideItem.Id) return;
      const target = getSelectableElementById(overrideItem.Id);
      if (target) {
        setEditableText(target, overrideItem.Text || '');
      }
    });
    postInventory();
  }

  function getBackgroundColor() {
    const surface = getPageSurface();
    return window.getComputedStyle(surface).backgroundColor || surface.style.backgroundColor || '#000000';
  }

  function rectsIntersect(a, b) {
    return !(a.right < b.left || a.left > b.right || a.bottom < b.top || a.top > b.bottom);
  }

  function isEditableKeyboardTarget(target) {
    if (!target) return false;
    if (activeTextEditor?.editor === target) return true;
    if (activeModernEditor?.contains?.(target)) return true;
    const editable = target.closest?.('input, textarea, select, [contenteditable]');
    if (!editable) return false;
    const tag = (editable.tagName || '').toLowerCase();
    if (tag === 'input' || tag === 'textarea' || tag === 'select') {
      return editable.disabled !== true && editable.readOnly !== true;
    }
    return editable.getAttribute('contenteditable') !== 'false';
  }

  let drag = null;

  document.addEventListener('click', event => {
    if (!menu.contains(event.target)) {
      hideMenu();
    }
  }, true);

  document.addEventListener('dblclick', event => {
    const target = findSelectable(event.target);
    if (!target) return;
    if (beginLegacyTextEdit(target)) {
      event.preventDefault();
      event.stopPropagation();
    }
  }, true);

  function openContextMenu(event) {
    if (event.__scadaContextMenuHandled) return;
    if (event.target?.closest?.('.scada-modern-element')) return;
    event.__scadaContextMenuHandled = true;
    hideMenu();
    const target = findSelectable(event.target);
    if (target && !selected.has(getId(target))) {
      if (!event.ctrlKey && !event.shiftKey) {
        clearSelection();
        clearModernSelection();
      }
      setSelected(target, true);
      postSelection();
    } else if (!target && selected.size === 0) {
      clearAllSelection(false);
    }

    event.preventDefault();
    event.stopPropagation();
    const hasLegacySelection = target || selected.size > 0;
    window.chrome?.webview?.postMessage({
      type: 'contextMenuRequest',
      targetKind: hasLegacySelection ? 'source' : 'background',
      items: hasLegacySelection ? getSelectedMessages() : [],
      x: event.clientX,
      y: event.clientY,
      backgroundColor: getBackgroundColor()
    });
  }

  document.addEventListener('pointerdown', event => {
    if (activeTextEditor && event.target !== activeTextEditor.editor) {
      closeTextEditor(true);
    }
    if (event.button === 2) {
      return;
    }
    if (event.button !== 0) return;
    if (activeModernEditor && activeModernEditor.contains(event.target)) {
      event.stopPropagation();
      return;
    }
    if (event.target?.closest?.('#scada-scene-resize-handle')) {
      const surface = getPageSurface();
      sceneCanvasResize = {
        pointerId: event.pointerId,
        startClientX: event.clientX,
        startClientY: event.clientY,
        startWidth: surface.offsetWidth || surface.getBoundingClientRect().width || 160,
        startHeight: surface.offsetHeight || surface.getBoundingClientRect().height || 120
      };
      event.target.setPointerCapture?.(event.pointerId);
      event.preventDefault();
      event.stopPropagation();
      return;
    }
    if (event.target && menu.contains(event.target)) {
      event.stopPropagation();
      return;
    }
    hideMenu();

    if (placementKind) {
      const surface = getPageSurface();
      const rect = surface.getBoundingClientRect();
      const x = Math.max(0, event.clientX - rect.left + surface.scrollLeft);
      const y = Math.max(0, event.clientY - rect.top + surface.scrollTop);
      const kind = placementKind;
      placementKind = null;
      document.body.classList.remove('scada-placement-active');
      window.chrome?.webview?.postMessage({ type: 'placeElement', kind, x, y });
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (event.target?.closest?.('.scada-modern-element')) {
      return;
    }

    const target = findSelectable(event.target);

    if (target) {
      const targetId = getId(target);
      if (event.altKey) {
        setSelected(target, false);
      } else if (event.ctrlKey || event.shiftKey) {
        setSelected(target, !selected.has(getId(target)));
      } else {
        if (!selected.has(targetId)) {
          clearSelection();
        }
        clearModernSelection();
        setSelected(target, true);
      }
      postSelection();
      if (!event.altKey && !event.ctrlKey && !event.shiftKey && !event.metaKey && selected.has(targetId)) {
        sourceDrag = {
          pointerId: event.pointerId,
          captureTarget: target,
          startClientX: event.clientX,
          startClientY: event.clientY,
          didDrag: false,
          items: selectedSourceElements().map(el => ({
            id: getId(el),
            el,
            geometry: getElementBounds(el)
          }))
        };
        target.setPointerCapture?.(event.pointerId);
      }
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    drag = {
      startX: event.clientX,
      startY: event.clientY,
      remove: event.altKey
    };
    marquee.style.left = `${drag.startX}px`;
    marquee.style.top = `${drag.startY}px`;
    marquee.style.width = '0px';
    marquee.style.height = '0px';
    marquee.style.display = 'block';
  }, true);

  document.addEventListener('pointermove', event => {
    if (sceneCanvasResize) {
      const width = sceneCanvasResize.startWidth + event.clientX - sceneCanvasResize.startClientX;
      const height = sceneCanvasResize.startHeight + event.clientY - sceneCanvasResize.startClientY;
      const previewSize = setSceneSurfaceSize(width, height);
      window.chrome?.webview?.postMessage({
        type: 'previewSceneCanvasResize',
        width: previewSize.width,
        height: previewSize.height
      });
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (modernDrag) {
      const dx = event.clientX - modernDrag.startClientX;
      const dy = event.clientY - modernDrag.startClientY;
      const geometry = {
        x: modernDrag.startX,
        y: modernDrag.startY,
        width: modernDrag.startWidth,
        height: modernDrag.startHeight
      };

      if (modernDrag.mode === 'move') {
        (modernDrag.items || []).forEach(item => {
          setWrapperGeometry(item.wrapper, {
            x: item.geometry.x + dx,
            y: item.geometry.y + dy,
            width: item.geometry.width,
            height: item.geometry.height
          });
        });
      } else {
        if (modernDrag.handle.includes('e')) geometry.width = modernDrag.startWidth + dx;
        if (modernDrag.handle.includes('s')) geometry.height = modernDrag.startHeight + dy;
        if (modernDrag.handle.includes('w')) {
          geometry.x = modernDrag.startX + dx;
          geometry.width = modernDrag.startWidth - dx;
        }
        if (modernDrag.handle.includes('n')) {
          geometry.y = modernDrag.startY + dy;
          geometry.height = modernDrag.startHeight - dy;
        }
        setWrapperGeometry(modernDrag.wrapper, geometry);
      }

      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (sourceDrag) {
      const dx = event.clientX - sourceDrag.startClientX;
      const dy = event.clientY - sourceDrag.startClientY;
      sourceDrag.didDrag = sourceDrag.didDrag || Math.abs(dx) > 3 || Math.abs(dy) > 3;
      if (sourceDrag.didDrag) {
        sourceDrag.items.forEach(item => setSourceElementGeometry(item.el, {
          x: item.geometry.x + dx,
          y: item.geometry.y + dy,
          width: item.geometry.width,
          height: item.geometry.height
        }));
      }
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (!drag) return;
    const left = Math.min(drag.startX, event.clientX);
    const top = Math.min(drag.startY, event.clientY);
    const width = Math.abs(event.clientX - drag.startX);
    const height = Math.abs(event.clientY - drag.startY);
    marquee.style.left = `${left}px`;
    marquee.style.top = `${top}px`;
    marquee.style.width = `${width}px`;
    marquee.style.height = `${height}px`;
  }, true);

  document.addEventListener('pointerup', event => {
    if (sceneCanvasResize) {
      const width = sceneCanvasResize.startWidth + event.clientX - sceneCanvasResize.startClientX;
      const height = sceneCanvasResize.startHeight + event.clientY - sceneCanvasResize.startClientY;
      const finalSize = setSceneSurfaceSize(width, height);
      window.chrome?.webview?.postMessage({
        type: 'resizeSceneCanvas',
        beforeWidth: sceneCanvasResize.startWidth,
        beforeHeight: sceneCanvasResize.startHeight,
        width: finalSize.width,
        height: finalSize.height
      });
      sceneCanvasResize = null;
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (modernDrag) {
      const geometry = readWrapperGeometry(modernDrag.wrapper);
      if (modernDrag.mode === 'move' && (modernDrag.items || []).length > 1) {
        postSelectionMove(
          'object',
          modernDrag.items.map(item => item.id).filter(Boolean),
          event.clientX - modernDrag.startClientX,
          event.clientY - modernDrag.startClientY);
      } else {
        postModernGeometry(
          modernDrag.id,
          {
            x: modernDrag.startX,
            y: modernDrag.startY,
            width: modernDrag.startWidth,
            height: modernDrag.startHeight
          },
          geometry);
      }
      modernDrag = null;
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (sourceDrag) {
      const deltaX = event.clientX - sourceDrag.startClientX;
      const deltaY = event.clientY - sourceDrag.startClientY;
      if (sourceDrag.didDrag) {
        postSelectionMove(
          'source',
          sourceDrag.items.map(item => item.id).filter(Boolean),
          deltaX,
          deltaY,
          sourceDrag.items.map(item => toElementMessage(item.el)));
        postInventory();
        postSelection();
      }
      try {
        sourceDrag.captureTarget?.releasePointerCapture?.(sourceDrag.pointerId);
      } catch {
      }
      sourceDrag = null;
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (!drag) return;
    const box = marquee.getBoundingClientRect();
    marquee.style.display = 'none';

    if (box.width > 3 && box.height > 3) {
      getSelectableElements()
        .filter(el => rectsIntersect(box, el.getBoundingClientRect()))
        .forEach(el => setSelected(el, !drag.remove));
      postSelection();
    } else if (!drag.remove && !event.ctrlKey && !event.shiftKey) {
      clearAllSelection();
    }

    drag = null;
    event.preventDefault();
  }, true);

  document.addEventListener('keydown', event => {
    if (isEditableKeyboardTarget(event.target)) {
      return;
    }
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') {
      window.chrome?.webview?.postMessage({ type: event.shiftKey ? 'redo' : 'undo' });
      event.preventDefault();
      event.stopPropagation();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'y') {
      window.chrome?.webview?.postMessage({ type: 'redo' });
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (!selectedModernId) return;

    const wrapper = document.querySelector(`.scada-modern-element[data-id="${CSS.escape(selectedModernId)}"]`);
    if (!wrapper) return;

    if (event.key === 'Backspace') {
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (event.key === 'Delete') {
      window.chrome?.webview?.postMessage({ type: 'deleteSceneObject', id: selectedModernId });
      selectedModernIds.delete(selectedModernId);
      selectedModernId = null;
      syncModernSelectionInDom();
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    const arrows = {
      ArrowLeft: [-1, 0],
      ArrowRight: [1, 0],
      ArrowUp: [0, -1],
      ArrowDown: [0, 1]
    };
    const delta = arrows[event.key];
    if (!delta) return;

    const step = event.shiftKey ? 10 : 1;
    const geometry = readWrapperGeometry(wrapper);
    const before = { ...geometry };
    geometry.x += delta[0] * step;
    geometry.y += delta[1] * step;
    setWrapperGeometry(wrapper, geometry);
    postModernGeometry(selectedModernId, before, geometry);
    event.preventDefault();
    event.stopPropagation();
  }, true);

  document.addEventListener('contextmenu', openContextMenu, true);
  menu.addEventListener('click', event => {
    if (event.target?.disabled || event.target?.getAttribute?.('aria-disabled') === 'true') {
      event.preventDefault();
      event.stopPropagation();
      return;
    }
    const commandId = event.target?.getAttribute?.('data-command-id');
    if (!commandId) return;
    event.preventDefault();
    event.stopPropagation();
    hideMenu();
    window.chrome?.webview?.postMessage({
      type: 'executeCommand',
      commandId,
      items: getSelectedMessages(),
      backgroundColor: getBackgroundColor()
    });
  });

  function hideSelected() {
    getSelectableElements()
      .filter(el => selected.has(getId(el)))
      .forEach(removeSourceElement);
    selected.clear();
    postInventory();
    postSelection();
  }

  function hideLegacyElements(ids) {
    removeSourceElements(ids);
  }

  function removeLegacyElements(ids) {
    removeSourceElements(ids);
  }

  function deleteSelected() {
    getSelectableElements()
      .filter(el => selected.has(getId(el)))
      .forEach(removeSourceElement);
    selected.clear();
    postInventory();
    postSelection();
  }

  function restoreHidden() {
    const restoreIds = new Set([...hidden, ...removedNodes.keys()]);
    restoreIds.forEach(id => restoreSourceElement(id, false));
    hidden.clear();
    document.querySelectorAll(selectableSelector).forEach(el => {
      el.style.display = '';
      el.removeAttribute('data-scada-selected');
      el.removeAttribute('data-scada-deleted');
    });
    selected.clear();
    postInventory();
    postSelection();
  }

  function restoreLegacyElements(ids) {
    const restoreIds = new Set(Array.isArray(ids) ? ids : []);
    if (!restoreIds.size) return;
    restoreIds.forEach(id => restoreSourceElement(id, true));
    postInventory();
    postSelection();
  }

  const sceneEditorApi = {
    refresh() {
      ensureSceneResizeHandle();
      postInventory();
      postSelection();
    },
    command(command) {
      const action = typeof command === 'string' ? command : command?.Action;
      if (action === 'beginPlacement') {
        closeModernEditor();
        clearModernSelection(false);
        placementKind = command?.Kind || null;
        document.body.classList.toggle('scada-placement-active', !!placementKind);
        return;
      }
      if (action === 'selectObject' || action === 'selectModern') {
        const ids = command?.Ids || command?.ids || [];
        selectedModernIds.clear();
        if (Array.isArray(ids) && ids.length) {
          ids.forEach(id => { if (id) selectedModernIds.add(id); });
          selectedModernId = ids.at(-1) || null;
          syncModernSelectionInDom();
        } else {
          selectModernElementInDom(command?.Id || null);
        }
        return;
      }
      if (action === 'hideSelected') hideSelected();
      if (action === 'deleteSelected') deleteSelected();
      if (action === 'restoreHidden') restoreHidden();
      if (action === 'clearSelection') clearAllSelection();
    },
    showContextMenu,
    getSelectedMessagesForStudio,
    selectLegacyElements,
    hideLegacyElements,
    removeLegacyElements,
    restoreLegacyElements,
    setBackgroundColor(color) {
      const surface = getPageSurface();
      surface.style.backgroundColor = color;
      document.body.style.backgroundColor = color;
    },
    setCanvasSize(size) {
      setSceneSurfaceSize(size?.width ?? size?.Width, size?.height ?? size?.Height);
      ensureSceneResizeHandle();
    },
    applyTextOverrides,
    applySourceElementBounds,
    renderModernElements
  };
  window.scadaSceneEditor = sceneEditorApi;
  window.scadaLegacyExtraction = sceneEditorApi;

  ensureSceneResizeHandle();
  postInventory();
  postSelection();
})();
""";
}
