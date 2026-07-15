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
using ScadaBuilderV2.Application.Clipboard;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Commands.Pages;
using ScadaBuilderV2.Application.Conversion;
using ScadaBuilderV2.Application.ElementStudio;
using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Application.Selection;
using ScadaBuilderV2.Domain.Editor;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Elements;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ElementStudio;
using ScadaBuilderV2.Application.Libraries;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Infrastructure.Libraries;
using ScadaBuilderV2.Infrastructure.ModernProjects;
using ScadaBuilderV2.Infrastructure.ReferenceProjects;
using ScadaBuilderV2.Rendering;
using ScadaBuilderV2.App.Pages;
using ScadaBuilderV2.App.Diagnostics;
using ScadaBuilderV2.App.Workspace;
using ScadaBuilderV2.App.TableEditor;
using ScadaBuilderV2.App.Ribbon;
using ScadaBuilderV2.App.EditorBridge;
using ScadaBuilderV2.Application.Tables;

namespace ScadaBuilderV2.App;

public partial class MainWindow : Window, IPageWorkspaceHost
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
    private readonly PageSourceProjectionResolver _pageSourceProjectionResolver = new();
    private readonly PageWorkspaceController _pageWorkspaceController;
    private readonly PageExportInputBuilder _pageExportInputBuilder;
    private readonly CommandRegistry _applicationCommandRegistry = new();
    private readonly PageCommandCoordinator _pageCommandCoordinator = new();
    private readonly PagesPanelViewModel _pagesPanel = new();
    private readonly PagePropertiesViewModel _pageProperties = new();
    private readonly DiagnosticsPanelViewModel _diagnosticsPanel = new();
    private readonly PageCommandController _pageCommandController;
    private readonly TableEditorController _tableEditorController;
    private readonly TableRibbonViewModel _tableRibbonViewModel;
    private readonly Tf100WebTagCatalogImporter _tagCatalogImporter = new();
    private readonly IElementStudioImportPackageWriter _elementStudioPackageWriter = new ElementStudioImportPackageWriter();
    private readonly ElementStudioComponentPackageStore _elementStudioComponentPackageStore = new();
    private readonly LibraryRegistryStore _libraryRegistryStore = new();
    private readonly ScadaBuilderV2.Infrastructure.Shell.DockLayoutStore _dockLayoutStore = new();
    private string? _defaultLayoutXml;
    private bool _layoutLoaded;
    private IReadOnlyList<LibraryEntry> _libraryEntries = [];
    private LibraryEntry? _selectedLibraryEntry;
    private readonly ElementPlusLibraryReader _elementPlusLibraryReader = new();
    private readonly ObservableCollection<ElementLibraryTileViewModel> _elementLibraryItems = [];
    private readonly ObservableCollection<TagCatalogListItem> _tagCatalogItems = [];
    private readonly ICollectionView _tagCatalogView;
    private readonly Dictionary<string, IReadOnlyList<RibbonGroupDefinition>> _ribbonTabs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _hiddenSourceObjectIds = new(StringComparer.Ordinal);
    private readonly List<LegacyElementListItem> _sourceObjects = [];
    private readonly ActiveSelectionState _activeSelection = new();
    private readonly ShortcutRegistry _shortcutRegistry = new();
    private readonly SceneClipboard _sceneClipboard = new();
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
    private readonly TableAuthoringSession _tableAuthoringSession = new();
    private string _activeRibbonKey = "File";
    private string _activeInsertFamilyId = "text-values";
    private string? _activeInsertCommandId;
    private int _nextTextSequence = 1;
    private int _nextInputTextSequence = 1;
    private int _nextInputNumericSequence = 1;
    private int _nextShapeSequence = 1;
    private int _nextButtonSequence = 1;
    private int _nextTableSequence = 1;
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

    public string StatusText { get; private set; } = "Etat / Warning";

    public ObservableCollection<RibbonGroupViewModel> ActiveRibbonGroups { get; } = [];

    public ObservableCollection<RibbonCommandViewModel> ToolPaletteCommands { get; } = [];

    public ObservableCollection<RibbonFamilyViewModel> InsertFamilies { get; } = [];

    public PagesPanelViewModel PagesPanel => _pagesPanel;

    public PagePropertiesViewModel PageProperties => _pageProperties;

    public DiagnosticsPanelViewModel DiagnosticsPanel => _diagnosticsPanel;

    public MainWindow()
    {
        _tableRibbonViewModel = new TableRibbonViewModel(_tableAuthoringSession);
        _tableEditorController = new TableEditorController(
            this,
            CommitTableElement,
            CanCommitTableTransform,
            () => _modernProject?.TagCatalog);
        _pageWorkspaceController = new PageWorkspaceController(_modernProjectStore, this);
        _pageExportInputBuilder = new PageExportInputBuilder(_modernProjectStore, _pageSourceProjectionResolver);
        RegisterPageApplicationCommands();
        _pageCommandController = new PageCommandController(
            this,
            _applicationCommandRegistry,
            _pageWorkspaceController,
            OnPageCommandCompleted,
            () => ShowDiagnosticsPanel());
        InitializeComponent();
        CaptureDefaultLayout();
        InitializeRibbonCommandRegistry();
        InitializeToolPaletteCommands();
        DataContext = this;
        SetActiveRibbon("File");
        ElementLibraryListBox.ItemsSource = _elementLibraryItems;
        _tagCatalogView = CollectionViewSource.GetDefaultView(_tagCatalogItems);
        _tagCatalogView.Filter = FilterTagCatalogItem;
        TagCatalogDataGrid.ItemsSource = _tagCatalogView;
        InitializeTagCatalogFilters();
        SceneTabs.ItemsSource = _pageWorkspaceController.OpenTabs;
        _pageDimensionApplyTimer.Tick += OnPageDimensionApplyTimerTick;
        StatusTextBlock.Text = $"Etat / Warning - {LoadVersionText()}";
        SetBackgroundColorControls("#000000");

        _applicationCommandRegistry.Register(new ToggleElementLockCommand());

        PreviewWebView.NavigationCompleted += OnPreviewNavigationCompleted;
        Closing += OnMainWindowClosing;
        Loaded += async (_, _) => await LoadDockLayoutAsync();
        ToolAnchorable.Closing += OnAnchorableClosing;
        ProjectAnchorable.Closing += OnAnchorableClosing;
        TagCatalogAnchorable.Closing += OnAnchorableClosing;
        PageAnchorable.Closing += OnAnchorableClosing;
        ElementAnchorable.Closing += OnAnchorableClosing;
        PropertiesAnchorable.Closing += OnAnchorableClosing;
        LibraryAnchorable.Closing += OnAnchorableClosing;
        DiagnosticsAnchorable.Closing += OnAnchorableClosing;
        Closed += (_, _) =>
        {
            foreach (var tab in _pageWorkspaceController.OpenTabs)
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
            if (!_diagnosticsPanel.HasIssues) DiagnosticsAnchorable.Hide();
        }
        catch (Exception ex)
        {
            SetStatus($"Erreur chargement source legacy: {ex.Message}");
        }
    }

    private async Task LoadReferenceProjectAsync()
    {
        _diagnosticsPanel.Clear();
        _repositoryRoot = ResolveRepositoryRoot();
        _referenceProject = await _referenceReader.LoadAmrReferenceAsync(_repositoryRoot);
        var importedPages = new List<ImportedPageDescriptor>();
        foreach (var page in _referenceProject.Pages)
        {
            var source = await ResolveLegacyViewerSourceAsync(page.AbsolutePath, page.Id);
            var sourcePath = source is null
                ? null
                : Path.GetRelativePath(
                    _repositoryRoot,
                    Path.Combine(source.RootPath, source.RelativeHtmlSource));
            importedPages.Add(new ImportedPageDescriptor(page.Id, page.Title, sourcePath));
        }
        var sceneReferences = PageWorkspaceController.CreateImportedPageReferences("AMR_REF_SCADA_V2", importedPages);
        _modernProject = await _modernProjectStore.EnsureReferenceModernProjectAsync(_repositoryRoot, sceneReferences);
        _pageWorkspaceController.Initialize(_repositoryRoot, _modernProject);
        await RefreshLibrarySelectorAsync();

        ProjectNameText.Text = $"{_referenceProject.Name} ({_referenceProject.Pages.Count} pages)";
        RefreshProjectTagSummary();
        _pagesPanel.Load(_modernProject);
        PagesListBox.ItemsSource = _pagesPanel.View;

        var preferredPage = _modernProject.Scenes.FirstOrDefault(page => page.PageKey == _modernProject.EffectiveHomePageKey)
            ?? _modernProject.Scenes.FirstOrDefault(page => page.EffectivePageCode == "win00008")
            ?? _modernProject.Scenes.FirstOrDefault();
        if (preferredPage is not null)
        {
            _isUpdatingPageSelection = true;
            try
            {
                PagesListBox.SelectedItem = _pagesPanel.Items.FirstOrDefault(item => item.PageKey == preferredPage.PageKey);
            }
            finally
            {
                _isUpdatingPageSelection = false;
            }

            await _pageWorkspaceController.OpenAsync(preferredPage.PageKey);
        }

        SetStatus($"Source legacy chargee en lecture seule: {_referenceProject.Name}");
    }

    private void OnPageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPageSelection)
        {
            return;
        }

        if (PagesListBox.SelectedItem is not PageListItemViewModel item)
        {
            _pagesPanel.SelectedPage = null;
            RefreshActiveRibbonCommandStates();
            return;
        }

        _pagesPanel.SelectedPage = item;
        RefreshActiveRibbonCommandStates();
    }

    public async Task ActivatePageAsync(SceneWorkspaceTab tab)
    {
        if (!ReferenceEquals(_activeSceneTab, tab)) SaveActiveTabTransientState();

        _activeSceneTab = tab;
        _activeScene = tab.Scene;
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
            PagesListBox.SelectedItem = _pagesPanel.Items.FirstOrDefault(page => page.PageKey == tab.PageKey);
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
        if (_activeSceneTab is null || _activeScene is null || _repositoryRoot is null)
        {
            SetPreviewPlaceholder("Aucune scene active.");
            return;
        }

        var pageReference = _activeSceneTab.Page;
        var source = _pageSourceProjectionResolver.Resolve(pageReference, _repositoryRoot);
        PreviewDocument preview;
        string sourceKind;
        string previewRoot;
        if (source is null)
        {
            previewRoot = Path.Combine(
                ModernProjectStore.GetReferenceModernProjectRoot(_repositoryRoot),
                ".studio",
                "preview");
            preview = await PreviewDocument.MaterializeNativeAsync(
                new PageDocumentInput(pageReference, _activeScene),
                previewRoot);
            sourceKind = "native";
        }
        else
        {
            previewRoot = source.RootPath;
            preview = new PreviewDocument(
                pageReference.EffectivePageCode,
                pageReference.Title,
                source.RelativeHtmlSource);
            sourceKind = source.Kind;
        }

        _selectedSourceObjectIds.Clear();
        _hiddenSourceObjectIds.Clear();
        CacheConvertedLegacyIdsFromActiveScene();
        _sourceObjects.Clear();
        RefreshSelectionUi();

        var backgroundColor = _activeScene?.BackgroundColor ?? "#000000";
        UpdatePreviewSurfaceBackground(backgroundColor);

        var sourceUri = preview.GetSourceUri(previewRoot);
        await PrepareInitialSceneBackgroundScriptAsync(backgroundColor);

        ActivePageText.Text = pageReference.EffectivePageCode;
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

        SetStatus($"Document page charge: {pageReference.EffectivePageCode} ({sourceKind})");
    }

    private void SetPreviewPlaceholder(string message)
    {
        ActivePageText.Text = _activeSceneTab?.Page.EffectivePageCode ?? "-";
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
            await _pageWorkspaceController.ActivateAsync(tab);
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
            SaveActiveTabTransientState();
            await _pageWorkspaceController.CloseAsync(tab);
        }
        catch (Exception ex)
        {
            SetStatus($"Fermeture scene impossible: {ex.Message}");
        }
    }

    public void ClearActivePage(SceneWorkspaceTab tab)
    {
        _activeSceneTab = null;
        _activeScene = null;
        _activeSceneDirty = false;
        _selectedSceneObject = null;
        _selectedSceneObjectIds.Clear();
        _selectedSourceObjectIds.Clear();
        _hiddenSourceObjectIds.Clear();
        _sourceObjects.Clear();
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
    }

    public async Task<bool> ConfirmCloseDirtyPageAsync(SceneWorkspaceTab tab)
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
        if (_repositoryRoot is null || _modernProject is null)
        {
            SetStatus("Aucun projet actif pour sauvegarder la scene.");
            return;
        }

        if (ReferenceEquals(tab, _activeSceneTab) && _activeScene is not null)
        {
            tab.Scene = _activeScene;
        }

        _pageWorkspaceController.ReplaceProject(_modernProject ?? throw new InvalidOperationException("Aucun projet moderne actif."));
        await _pageWorkspaceController.SaveAsync();
        _modernProject = _pageWorkspaceController.Project ?? _modernProject;
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

        foreach (var tab in _pageWorkspaceController.OpenTabs.Where(tab => tab.IsDirty).ToArray())
        {
            if (!await ConfirmCloseDirtyPageAsync(tab))
            {
                SetStatus("Fermeture annulee: sauvegarde des scenes non terminee.");
                return;
            }
        }

        await SaveDockLayoutAsync();
        _isClosingConfirmed = true;
        Close();
    }

    public void ReportPageWorkspaceStatus(string message) => SetStatus(message);

    /// <summary>
    /// Prevents AvalonDock's default "close" behavior (which detaches the anchorable from
    /// the layout permanently) from firing when the user clicks a side panel's close button.
    /// Instead the panel is hidden and remains reachable from the "Fenêtres" menu.
    /// </summary>
    private void OnAnchorableClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (sender is not AvalonDock.Layout.LayoutAnchorable anchorable)
        {
            return;
        }

        e.Cancel = true;
        anchorable.Hide();
    }

    /// <summary>
    /// Serializes the current AvalonDock layout to an XML string.
    /// </summary>
    private string SerializeLayout()
    {
        var serializer = new AvalonDock.Layout.Serialization.XmlLayoutSerializer(MainDockingManager);
        using var writer = new System.IO.StringWriter();
        serializer.Serialize(writer);
        return writer.ToString();
    }

    /// <summary>
    /// Restores an AvalonDock layout previously produced by <see cref="SerializeLayout"/>.
    /// Throws on malformed or incompatible layout XML; callers decide how to handle failure.
    /// </summary>
    private void DeserializeLayout(string layoutXml)
    {
        var serializer = new AvalonDock.Layout.Serialization.XmlLayoutSerializer(MainDockingManager);
        using var reader = new System.IO.StringReader(layoutXml);
        serializer.Deserialize(reader);
    }

    /// <summary>
    /// Snapshots the AvalonDock layout as defined in XAML, before any saved layout is
    /// restored, so "Reinitialiser la disposition" has a default to return to.
    /// </summary>
    private void CaptureDefaultLayout()
    {
        _defaultLayoutXml = SerializeLayout();
    }

    /// <summary>
    /// Restores the previously saved AvalonDock layout, if any. Falls back silently to the
    /// XAML-defined default layout when no saved layout exists or it fails to parse.
    /// </summary>
    private async Task LoadDockLayoutAsync()
    {
        if (_layoutLoaded)
        {
            return;
        }

        _layoutLoaded = true;

        var path = _dockLayoutStore.GetDefaultLayoutPath();
        var layoutXml = await _dockLayoutStore.ReadLayoutXmlAsync(path);
        if (string.IsNullOrEmpty(layoutXml))
        {
            return;
        }

        try
        {
            DeserializeLayout(layoutXml);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException or NullReferenceException)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore dock layout, keeping default: {ex.Message}");
        }
    }

    /// <summary>
    /// Serializes the current AvalonDock layout and persists it so it can be restored on
    /// the next launch. Failures are logged and swallowed so a layout-save problem never
    /// blocks the window from closing.
    /// </summary>
    private async Task SaveDockLayoutAsync()
    {
        try
        {
            var layoutXml = SerializeLayout();
            await _dockLayoutStore.WriteLayoutXmlAsync(_dockLayoutStore.GetDefaultLayoutPath(), layoutXml);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save dock layout: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores the AvalonDock layout captured from XAML at startup, discarding any
    /// manual rearrangement made during the current or previous sessions.
    /// </summary>
    private void OnResetLayoutClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_defaultLayoutXml))
        {
            return;
        }

        try
        {
            DeserializeLayout(_defaultLayoutXml);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException or NullReferenceException)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to reset dock layout: {ex.Message}");
        }
    }

    private void OnShowToolAnchorableClick(object sender, RoutedEventArgs e) => ToolAnchorable.Show();
    private void OnShowProjectAnchorableClick(object sender, RoutedEventArgs e) => ProjectAnchorable.Show();
    private void OnShowTagCatalogAnchorableClick(object sender, RoutedEventArgs e) => TagCatalogAnchorable.Show();
    private void OnShowPageAnchorableClick(object sender, RoutedEventArgs e) => PageAnchorable.Show();
    private void OnShowElementAnchorableClick(object sender, RoutedEventArgs e) => ElementAnchorable.Show();
    private void OnShowPropertiesAnchorableClick(object sender, RoutedEventArgs e) => PropertiesAnchorable.Show();
    private void OnShowLibraryAnchorableClick(object sender, RoutedEventArgs e) => LibraryAnchorable.Show();

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

    private async Task<LegacyViewerSource?> ResolveLegacyViewerSourceAsync(string pageManifestPath, string pageCode)
    {
        if (_referenceProject is null)
        {
            return null;
        }

        var relativeHtmlSource = await ReadLegacyHtmlSourceAsync(pageManifestPath);
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
            var rawLegacyHtml = FindRawLegacyHtml(_repositoryRoot, pageCode);
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
        if (ElementLibraryListBox.SelectedItem is ElementLibraryTileViewModel tile)
        {
            SetStatus($"{tile.Item.FileName}: double-cliquez pour instancier dans la scene active.");
        }
    }

    private void OnElementLibraryPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _elementLibraryDragStartPoint = e.GetPosition(null);
    }

    private async void OnElementLibraryMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var tile = ResolveElementLibraryTileFromEvent(e.OriginalSource as DependencyObject)
            ?? ElementLibraryListBox.SelectedItem as ElementLibraryTileViewModel;
        if (tile is null)
        {
            return;
        }

        e.Handled = true;
        var position = await ResolveVisibleSceneCenterAsync();
        await CreateElementPlusLibraryInstanceAsync(tile.Item.FilePath, position.X, position.Y, centerOnPoint: true);
    }

    private void OnElementLibraryPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            ElementLibraryListBox.SelectedItem is not ElementLibraryTileViewModel tile)
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
            new DataObject(ElementPlusLibraryDragFormat, tile.Item.FilePath),
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

    private static ElementLibraryTileViewModel? ResolveElementLibraryTileFromEvent(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ListBoxItem { DataContext: ElementLibraryTileViewModel tile })
            {
                return tile;
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
        var libraryRoot = ResolveActiveLibraryRoot(create: true);
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
                _elementLibraryItems.Add(new ElementLibraryTileViewModel(item));
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

        var libraryRoot = ResolveActiveLibraryRoot(create: true);
        if (libraryRoot is null)
        {
            return;
        }

        try
        {
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
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            StopElementLibraryWatcher();
        }
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

    private string? ResolveActiveLibraryRoot(bool create)
    {
        var selectedEntry = _selectedLibraryEntry;
        if (selectedEntry is null)
        {
            return null;
        }

        if (create && selectedEntry.IsDefault)
        {
            Directory.CreateDirectory(selectedEntry.Path);
        }

        return selectedEntry.Path;
    }

    private async Task RefreshLibrarySelectorAsync()
    {
        var registry = await BuildLibraryRegistryAsync();
        var previousName = _selectedLibraryEntry?.Name;
        _libraryEntries = registry.Entries;
        LibrarySelectorComboBox.ItemsSource = _libraryEntries;
        var toSelect = _libraryEntries.FirstOrDefault(entry => entry.Name == previousName) ?? _libraryEntries[0];
        LibrarySelectorComboBox.SelectedItem = toSelect;
    }

    private async void OnLibrarySelectorSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedLibraryEntry = LibrarySelectorComboBox.SelectedItem as LibraryEntry;
        StartElementLibraryWatcher();
        await RefreshElementLibraryAsync();
    }

    private async Task<LibraryRegistry> BuildLibraryRegistryAsync()
    {
        var defaultPath = ResolveElementPlusLibraryRoot(create: true)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SCADA_BUILDER_V2", "library", "elements");
        var defaultName = await _libraryRegistryStore.ReadDefaultNameAsync() ?? "Defaut";
        var defaultEntry = new LibraryEntry(defaultName, defaultPath, IsDefault: true);
        var externalEntries = await _libraryRegistryStore.ReadExternalEntriesAsync();
        return new LibraryRegistry(defaultEntry, externalEntries);
    }

    private async Task OpenConfigurationWindowAsync()
    {
        var registry = await BuildLibraryRegistryAsync();
        var dialog = new ConfigurationWindow(registry) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            await _libraryRegistryStore.WriteExternalEntriesAsync(dialog.Registry.ExternalEntries);
            await _libraryRegistryStore.WriteDefaultNameAsync(dialog.Registry.Entries[0].Name);
            await RefreshLibrarySelectorAsync();
        }
    }

    private void OnStatusDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        ShowDiagnosticsPanel();
    }

    private void ShowDiagnosticsPanel(bool selectErrors = true)
    {
        DiagnosticsAnchorable.Show();
        DiagnosticsAnchorable.IsActive = true;
        if (selectErrors) DiagnosticsTabControl.SelectedIndex = 0;
    }

    private async void OnDiagnosticIssueMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid { SelectedItem: DiagnosticIssueViewModel issue } || issue.PageRouteKey is not { } pageKey)
        {
            return;
        }

        await _pageWorkspaceController.OpenAsync(pageKey);
        if (!string.IsNullOrWhiteSpace(issue.ElementId))
        {
            SelectModernElement(issue.ElementId);
        }
        if (!string.IsNullOrWhiteSpace(issue.PropertyPath))
        {
            PageAnchorable.Show();
            PageAnchorable.IsActive = true;
        }
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
            if (ForwardTableWebViewMessage(e.WebMessageAsJson)) return;
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
                    if (message.TargetKind == "table" && !string.IsNullOrWhiteSpace(message.Id))
                    {
                        _tableEditorController.Select(message.Id, message.Row, message.Column, message.EndRow, message.EndColumn);
                        _tableEditorController.FormatScopeKind = message.Scope switch
                        {
                            "table" => ScadaTableFormatScopeKind.Table,
                            "row" => ScadaTableFormatScopeKind.Rows,
                            "column" => ScadaTableFormatScopeKind.Columns,
                            _ => message.Row == message.EndRow && message.Column == message.EndColumn ? ScadaTableFormatScopeKind.Cells : ScadaTableFormatScopeKind.Cells
                        };
                    }
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
                    RejectDecommissionedElementEvents(message.Id);
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
                case "updateSceneObjectRotation":
                    UpdateModernElementRotation(message.Id, message.Rotation);
                    break;
                case "resizeSceneGroupWithChildren":
                    UpdateModernGroupGeometryWithChildren(message);
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
                case "shortcut":
                    HandleShortcut(message.Key, message.CtrlKey, message.ShiftKey, message.AltKey);
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
        RefreshTablePropertiesPanel();
        RefreshElementLockState();
        if (_activeRibbonKey == "Selection") RefreshActiveRibbonCommandStates();
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
        if (_activeScene is null || _activeSceneTab is null)
        {
            SetStatus("Aucune page active.");
            return;
        }

        var background = _activeScene.EffectiveBackground with { Color = color };
        var result = await ExecutePagePropertyCommandAsync(
            "page.set-background",
            new SetPageBackgroundRequest(_activeSceneTab.PageKey, background));
        if (result.Status == CommandResultStatus.Succeeded)
        {
            await ApplySceneBackgroundColorAsync(color);
        }
    }

    private async void OnApplyPagePropertiesClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null || _activeSceneTab is null)
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
        var pageKey = _activeSceneTab.PageKey;
        var pageType = GetSelectedPageType();
        var headerKey = pageType == ScadaPageType.Default ? GetSelectedCompositionPageKey(HeaderPageComboBox) : null;
        var footerKey = pageType == ScadaPageType.Default ? GetSelectedCompositionPageKey(FooterPageComboBox) : null;
        var requests = new (string Id, PageCommandRequest Request)[]
        {
            ("page.change-code", new ChangePageCodeRequest(pageKey, PageNameTextBox.Text.Trim())),
            ("page.rename", new RenamePageRequest(pageKey, PageTitleTextBox.Text.Trim())),
            ("page.set-type", new SetPageTypeRequest(pageKey, pageType)),
            ("page.set-build-inclusion", new SetPageBuildInclusionRequest(pageKey, IncludeInBuildCheckBox.IsChecked == true)),
            ("page.set-composition", new SetPageCompositionRequest(pageKey, headerKey, footerKey)),
            ("page.set-canvas", new SetPageCanvasRequest(pageKey, new CanvasSize(width, height))),
            ("page.set-background", new SetPageBackgroundRequest(pageKey, background))
        };

        foreach (var (commandId, request) in requests)
        {
            if (commandId == "page.set-composition" && pageType != ScadaPageType.Default) continue;
            var result = await ExecutePagePropertyCommandAsync(commandId, request);
            if (result.Status is CommandResultStatus.Blocked or CommandResultStatus.Failed) return;
        }

        await ApplySceneBackgroundColorAsync(background.Color, updateStatus: false);
        await ApplySceneCanvasSizeAsync(new CanvasSize(width, height));
        SetStatus($"Proprietes page appliquees: {width}x{height}. Sauvegarde requise.");
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

        _pageDimensionApplyTimer.Stop();
        _pageDimensionApplyTimer.Start();
    }

    private async void OnPageDimensionApplyTimerTick(object? sender, EventArgs e)
    {
        _pageDimensionApplyTimer.Stop();
        if (_activeScene is null)
        {
            return;
        }

        if (!TryReadPageDimensions(out var width, out var height))
        {
            SetStatus("Dimensions de page invalides. Largeur et hauteur doivent etre au moins 160x120.");
            return;
        }

        await ApplyActiveSceneCanvasSizeAsync(width, height, "Dimensions page appliquees");
    }

    private async Task ApplyActiveSceneCanvasSizeAsync(
        int width,
        int height,
        string statusPrefix)
    {
        if (_activeScene is null || _activeSceneTab is null)
        {
            return;
        }

        var size = new CanvasSize(width, height);
        var result = await ExecutePagePropertyCommandAsync(
            "page.set-canvas",
            new SetPageCanvasRequest(_activeSceneTab.PageKey, size));
        if (result.Status == CommandResultStatus.Succeeded)
        {
            await ApplySceneCanvasSizeAsync(size);
            SetStatus($"{statusPrefix}: {width}x{height}. Sauvegarde requise.");
        }
    }

    private async void OnPageTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPagePropertyControls || !ArePagePropertyControlsReady() || _activeSceneTab is null)
        {
            return;
        }

        await ExecutePagePropertyCommandAsync(
            "page.set-type",
            new SetPageTypeRequest(_activeSceneTab.PageKey, GetSelectedPageType()));
    }

    private async void OnPageBuildInclusionClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingPagePropertyControls || _activeSceneTab is null)
        {
            return;
        }

        await ExecutePagePropertyCommandAsync(
            "page.set-build-inclusion",
            new SetPageBuildInclusionRequest(_activeSceneTab.PageKey, IncludeInBuildCheckBox.IsChecked == true));
    }

    private async void OnHomePageClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingPagePropertyControls || _activeSceneTab is null)
        {
            return;
        }

        await ExecutePagePropertyCommandAsync(
            "page.set-home",
            new SetHomePageRequest(HomePageCheckBox.IsChecked == true ? _activeSceneTab.PageKey : null));
    }

    private async void OnPageCompositionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPagePropertyControls || _activeSceneTab is null)
        {
            return;
        }

        await ExecutePagePropertyCommandAsync(
            "page.set-composition",
            new SetPageCompositionRequest(
                _activeSceneTab.PageKey,
                GetSelectedCompositionPageKey(HeaderPageComboBox),
                GetSelectedCompositionPageKey(FooterPageComboBox)));
    }

    private async void OnPageCodeLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_isUpdatingPagePropertyControls || _activeSceneTab is null) return;
        await ExecutePagePropertyCommandAsync(
            "page.change-code",
            new ChangePageCodeRequest(_activeSceneTab.PageKey, PageNameTextBox.Text.Trim()));
    }

    private async void OnPageTitleLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_isUpdatingPagePropertyControls || _activeSceneTab is null) return;
        await ExecutePagePropertyCommandAsync(
            "page.rename",
            new RenamePageRequest(_activeSceneTab.PageKey, PageTitleTextBox.Text.Trim()));
    }

    private async Task<CommandResult> ExecutePagePropertyCommandAsync(string commandId, PageCommandRequest request)
    {
        var pageKey = _activeSceneTab?.PageKey ?? _pageProperties.PageRouteKey;
        var result = await _pageCommandController.ExecuteAsync(commandId, request, pageKey);
        if (result.Diagnostics.Count > 0)
        {
            _diagnosticsPanel.Load(result.Diagnostics, _modernProject, $"Commande {commandId}");
        }
        if (result.Status is CommandResultStatus.Blocked or CommandResultStatus.Failed)
        {
            _pageCommandController.PresentFailure(result);
            LoadPageProperties(_activeScene);
        }
        return result;
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

    private async Task CutSelectionAsync()
    {
        if (_activeScene is null)
        {
            SetStatus("Aucune scene active pour couper la selection.");
            return;
        }

        var selectedElements = ResolveTopLevelSelectedElements();
        if (selectedElements.Count == 0)
        {
            SetStatus("Aucun objet selectionne a couper.");
            return;
        }

        _sceneClipboard.Copy(selectedElements);

        var deletedSnapshots = selectedElements
            .Select(element => new DeletedSceneObjectSnapshot(
                element,
                _activeScene.FindParentOf(element.Id)?.Id,
                GetSiblingIndex(_activeScene, element)))
            .ToArray();

        _activeScene = _activeScene.WithoutSceneObjects(selectedElements.Select(element => element.Id));

        _selectedSourceObjectIds.Clear();
        _selectedSceneObject = null;
        _selectedSceneObjectIds.Clear();
        _activeSceneTab?.History.Push(new SceneObjectsDeletedAction(_activeScene.Id, deletedSnapshots));
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        await RenderModernSceneAsync();
        SetStatus($"{deletedSnapshots.Length} objet(s) coupe(s). Undo disponible. Presse-papier mis a jour.");
    }

    private void PasteClipboard()
    {
        if (_activeScene is null || !_sceneClipboard.HasContent)
        {
            SetStatus("Presse-papier vide ou aucune scene active.");
            return;
        }

        var pasted = _sceneClipboard.Content!
            .Select(element => CloneWithNewIds(element, 20, 20))
            .ToArray();

        var scene = _activeScene;
        foreach (var element in pasted)
        {
            scene = scene.WithElement(element);
        }

        var addedSnapshots = pasted
            .Select(element => new DeletedSceneObjectSnapshot(element, null, GetSiblingIndex(scene, element)))
            .ToArray();

        _activeScene = scene;
        _activeSceneTab?.History.Push(new SceneObjectsAddedAction(_activeScene.Id, addedSnapshots));

        _selectedSourceObjectIds.Clear();
        _selectedSceneObjectIds.Clear();
        foreach (var element in pasted)
        {
            _selectedSceneObjectIds.Add(element.Id);
        }

        _selectedSceneObject = pasted.Length > 0 ? pasted[^1] : null;

        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        _ = RenderModernSceneAsync();
        _ = ExecuteLegacyViewerCommandAsync(new LegacyViewerCommand("selectObject", Ids: _selectedSceneObjectIds.ToArray()));
        SetStatus($"{pasted.Length} objet(s) colle(s). Undo disponible.");
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
        if (_repositoryRoot is null || _activeScene is null || _activeSceneTab is null)
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

    private async Task OpenSelectedModernComponentInElementStudioAsync()
    {
        if (_repositoryRoot is null || _activeScene is null)
        {
            SetStatus("Studio Element+ indisponible: aucun projet ou scene active.");
            return;
        }

        var selected = _selectedSceneObjectIds.Count == 1
            ? _activeScene.FindElementRecursive(_selectedSceneObjectIds.Single())
            : null;
        var sourceSepFileName = selected?.Kind == ScadaElementKind.Custom ? selected.Data?.TagBinding : null;
        if (selected is null || string.IsNullOrWhiteSpace(sourceSepFileName))
        {
            SetStatus("Selectionnez un objet Element+ instancie depuis la bibliotheque pour ouvrir Studio Element+.");
            return;
        }

        var sepFilePath = await ResolveLibrarySepFilePathAsync(sourceSepFileName);
        if (sepFilePath is null)
        {
            SetStatus($"Composant source '{sourceSepFileName}' introuvable dans les bibliotheques Element+ configurees.");
            return;
        }

        try
        {
            var sepPackage = await _elementStudioComponentPackageStore.ReadFromPathAsync(sepFilePath);
            var version = LoadVersionText();
            var editPackage = ElementStudioComponentToImportPackageMapper.ToEditablePackage(sepPackage, sepFilePath, version);
            var projectsRoot = Path.Combine(_repositoryRoot, "SCADA_BUILDER_V2", "projects");
            var packagePath = await _elementStudioPackageWriter.WriteToProjectAsync(editPackage, projectsRoot);
            var launch = await TryLaunchElementStudioAsync(packagePath);
            AppendElementStudioLaunchLog(packagePath, launch);
            SetStatus(launch.Launched
                ? $"Studio Element+ ouvert pour edition: {Path.GetFileName(sepFilePath)}"
                : $"Package Studio Element+ cree: {packagePath}. {launch.Message}");
        }
        catch (Exception ex)
        {
            SetStatus($"Erreur ouverture Studio Element+: {ex.Message}");
        }
    }

    private async Task<string?> ResolveLibrarySepFilePathAsync(string fileName)
    {
        var registry = await BuildLibraryRegistryAsync();
        foreach (var entry in registry.Entries)
        {
            var candidatePath = Path.Combine(entry.Path, fileName);
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        return null;
    }

    private async Task OpenElementStudioFromToolPaletteAsync()
    {
        var hasEditingContext = _repositoryRoot is not null && _activeScene is not null && _activeSceneTab is not null;
        if (hasEditingContext)
        {
            var selectedLegacy = await CaptureSelectedLegacyElementsForStudioAsync();
            if (selectedLegacy.Length > 0)
            {
                await OpenSelectedLegacyInElementStudioAsync();
                return;
            }
        }

        try
        {
            SetStatus("Ouverture de Studio Element+...");
            var launch = await TryLaunchElementStudioAsync(packagePath: null);
            AppendElementStudioLaunchLog("(aucun package)", launch);
            SetStatus(launch.Launched
                ? "Studio Element+ ouvert."
                : $"Ouverture Studio Element+ echouee: {launch.Message}");
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
            GetActiveImportedSourcePath() ?? "",
            items,
            ElementStudioPackageMetadata.Current(version),
            ResolveElementPlusLibraryRoot(create: true));
    }

    private string? GetActiveImportedSourcePath()
    {
        if (_repositoryRoot is null || _activeSceneTab is null)
        {
            return null;
        }

        return _pageSourceProjectionResolver.Resolve(_activeSceneTab.Page, _repositoryRoot)?.GetSourcePath();
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

    private async Task<ElementStudioLaunchResult> TryLaunchElementStudioAsync(string? packagePath)
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

    private static async Task<ElementStudioLaunchResult> LaunchElementStudioExecutableAsync(string studioExePath, string? packagePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = studioExePath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal,
            WorkingDirectory = Path.GetDirectoryName(studioExePath) ?? ""
        };
        if (!string.IsNullOrWhiteSpace(packagePath))
        {
            startInfo.ArgumentList.Add(packagePath);
        }

        var process = Process.Start(startInfo);
        if (process is null)
        {
            return new ElementStudioLaunchResult(false, $"Impossible de lancer {studioExePath}.");
        }

        var visibleProcess = await WaitForStudioWindowAsync(process, TimeSpan.FromSeconds(15));
        if (visibleProcess is null)
        {
            if (process.HasExited)
            {
                var exitMessage = $"Studio Element+ a quitte immediatement (code {process.ExitCode}).";
                process.Dispose();
                return new ElementStudioLaunchResult(false, exitMessage);
            }

            // Still starting up (first WebView2 startup can exceed the wait window); leave it alive.
            process.Dispose();
            return new ElementStudioLaunchResult(
                true,
                $"{studioExePath} (fenetre non detectee dans le delai; le studio peut prendre quelques secondes de plus).");
        }

        BringProcessWindowToFront(visibleProcess);
        var messagePath = visibleProcess.Id == process.Id
            ? studioExePath
            : $"{studioExePath} (window process {visibleProcess.Id})";
        visibleProcess.Dispose();
        process.Dispose();
        return new ElementStudioLaunchResult(true, messagePath);
    }

    private static async Task<ElementStudioLaunchResult> LaunchElementStudioProjectAsync(string studioProjectPath, string? packagePath)
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
        if (!string.IsNullOrWhiteSpace(packagePath))
        {
            dotnetStartInfo.ArgumentList.Add("--");
            dotnetStartInfo.ArgumentList.Add(packagePath);
        }

        var process = Process.Start(dotnetStartInfo);
        if (process is null)
        {
            return new ElementStudioLaunchResult(false, $"Impossible de lancer dotnet run pour {studioProjectPath}.");
        }

        var visibleProcess = await WaitForStudioWindowAsync(process, TimeSpan.FromSeconds(30));
        if (visibleProcess is null)
        {
            if (process.HasExited)
            {
                var exitMessage = $"Studio Element+ via dotnet run a quitte immediatement (code {process.ExitCode}).";
                process.Dispose();
                return new ElementStudioLaunchResult(false, exitMessage);
            }

            // The studio may still be initializing (first WebView2 startup can exceed the wait window).
            // Leave the process alive instead of killing a window that is about to appear.
            process.Dispose();
            return new ElementStudioLaunchResult(
                true,
                $"{studioProjectPath} (fenetre non detectee dans le delai; le studio peut prendre quelques secondes de plus).");
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

        var sourceRoot = ResolveElementStudioSourceRoot();
        if (sourceRoot is not null)
        {
            candidates.Add(Path.Combine(sourceRoot, "bin", "Release", "net8.0-windows", "ScadaBuilderV2.ElementStudio.App.exe"));
            candidates.Add(Path.Combine(sourceRoot, "bin", "Debug", "net8.0-windows", "ScadaBuilderV2.ElementStudio.App.exe"));
        }

        return candidates
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
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

    private void ReorderSelectedElement(string operation)
    {
        if (_activeScene is null || _selectedSceneObjectIds.Count != 1)
        {
            SetStatus("Ordre: selection unique requise.");
            return;
        }

        var elementId = _selectedSceneObjectIds.First();
        if (!_activeScene.Elements.Any(e => e.Id == elementId))
        {
            SetStatus("Ordre: disponible pour les elements de premier niveau uniquement.");
            return;
        }

        var beforeScene = _activeScene;
        _activeScene = operation switch
        {
            "bring-to-front" => beforeScene.WithElementBroughtToFront(elementId),
            "bring-forward" => beforeScene.WithElementBroughtForward(elementId),
            "send-backward" => beforeScene.WithElementSentBackward(elementId),
            "send-to-back" => beforeScene.WithElementSentToBack(elementId),
            _ => beforeScene
        };

        if (Equals(beforeScene, _activeScene)) return;

        var label = operation switch
        {
            "bring-to-front" => "Mettre a l'avant",
            "bring-forward" => "Avancer",
            "send-backward" => "Reculer",
            "send-to-back" => "Mettre a l'arriere",
            _ => "Ordre"
        };

        _activeSceneTab?.History.Push(new SceneSnapshotChangedAction(
            beforeScene.Id,
            beforeScene,
            _activeScene,
            label));
        MarkActiveSceneDirty();
        RefreshSelectionUi();
        RefreshModernSceneUi();
        _ = RenderModernSceneAsync();
        SetStatus($"{label} applique.");
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
        var sourceDocumentId = _activeSceneTab?.SceneId ?? _activeScene?.Id ?? "";
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
        var sourceDocumentId = _activeSceneTab?.SceneId ?? _activeScene?.Id ?? "";
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

        var page = _modernProject?.Scenes.FirstOrDefault(candidate => candidate.PageKey == scene.PageKey)
            ?? _modernProject?.Scenes.FirstOrDefault(candidate => string.Equals(candidate.EffectivePageCode, scene.EffectivePageCode, StringComparison.OrdinalIgnoreCase));
        if (page is null) return;

        _isUpdatingPagePropertyControls = true;
        try
        {
            _pageProperties.Load(page, scene, _modernProject?.EffectiveHomePageKey);
            SelectPageType(page.Type);
            IncludeInBuildCheckBox.IsChecked = page.IncludeInBuild;
            HomePageCheckBox.IsChecked = _modernProject?.EffectiveHomePageKey == page.PageKey;
            HomePageCheckBox.IsEnabled = page.Type == ScadaPageType.Default && page.IncludeInBuild;
            RefreshCompositionComboBox(HeaderPageComboBox, ScadaPageType.Header, page.HeaderPageKey);
            RefreshCompositionComboBox(FooterPageComboBox, ScadaPageType.Footer, page.FooterPageKey);
            var canCompose = page.Type is not (ScadaPageType.Header or ScadaPageType.Footer);
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
            PageTitleTextBox is not null &&
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

    private void RefreshCompositionComboBox(ComboBox comboBox, ScadaPageType pageType, Guid? selectedPageKey)
    {
        comboBox.Items.Clear();
        comboBox.Items.Add(new ComboBoxItem { Content = "Aucun", Tag = "" });

        foreach (var page in (_modernProject?.Scenes ?? [])
            .Where(page => page.Type == pageType)
            .OrderBy(page => page.Id, StringComparer.Ordinal))
        {
            comboBox.Items.Add(new ComboBoxItem
            {
                Content = $"{page.EffectivePageCode} - {page.Title}",
                Tag = page.PageKey
            });
        }

        comboBox.SelectedIndex = 0;
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is Guid pageKey && pageKey == selectedPageKey)
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private static Guid? GetSelectedCompositionPageKey(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag is Guid pageKey ? pageKey : null;
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
        else if ((message.TargetKind == "object" || message.TargetKind == "modern" || message.TargetKind == "table") && !string.IsNullOrWhiteSpace(message.Id))
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
        if (message.TargetKind == "table")
        {
            var tableElement = _activeScene?.FindElementRecursive(message.Id ?? "");
            return tableElement is null ? [] : _tableEditorController.BuildContextMenu(tableElement);
        }

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

            var lockState = _elementLockCoordinator.BuildState(_activeScene!, _selectedSceneObjectIds);
            modernCommands.Insert(1, new EditorCommandDescriptor(
                "object.lock",
                lockState.AllLocked ? "Deverrouiller" : "Verrouiller",
                "lock",
                IsChecked: lockState.AllLocked));

            if (_selectedSceneObjectIds.Count == 1)
            {
                var sourceSepFileName = selected.Kind == ScadaElementKind.Custom
                    ? selected.Data?.TagBinding
                    : null;
                modernCommands.Insert(0, new EditorCommandDescriptor(
                    "object.open-in-element-studio",
                    "Ouvrir dans Studio Element+",
                    "element-studio",
                    IsEnabled: !string.IsNullOrWhiteSpace(sourceSepFileName),
                    DisabledReason: "Cet objet n'a pas ete instancie depuis la bibliotheque Element+."));
            }

            if (_selectedSceneObjectIds.Count > 1)
            {
                modernCommands.Insert(0, new EditorCommandDescriptor("object.group", "Grouper", "group"));
            }

            if (selected.Kind == ScadaElementKind.Group)
            {
                modernCommands.Insert(0, new EditorCommandDescriptor("object.ungroup", "Degrouper", "group"));
            }

            if (_selectedSceneObjectIds.Count == 1 && (_activeScene?.Elements.Any(e => e.Id == selected.Id) ?? false))
            {
                var elementCount = _activeScene!.Elements.Count;
                var elementIdx = _activeScene.Elements.ToList().FindIndex(e => e.Id == selected.Id);
                modernCommands.Add(new EditorCommandDescriptor(
                    "object.order",
                    "Ordre",
                    "order",
                    Children:
                    [
                        new EditorCommandDescriptor("object.order.bring-to-front", "Mettre a l'avant", "order",
                            IsEnabled: elementIdx < elementCount - 1),
                        new EditorCommandDescriptor("object.order.bring-forward", "Avancer", "order",
                            IsEnabled: elementIdx < elementCount - 1),
                        new EditorCommandDescriptor("object.order.send-backward", "Reculer", "order",
                            IsEnabled: elementIdx > 0),
                        new EditorCommandDescriptor("object.order.send-to-back", "Mettre a l'arriere", "order",
                            IsEnabled: elementIdx > 0),
                    ]));
                modernCommands.Add(new EditorCommandDescriptor(
                    "object.rotation",
                    "Rotation",
                    "rotation",
                    Children:
                    [
                        new EditorCommandDescriptor("object.rotation.0", "0°", "rotation"),
                        new EditorCommandDescriptor("object.rotation.90", "90°", "rotation"),
                        new EditorCommandDescriptor("object.rotation.180", "180°", "rotation"),
                        new EditorCommandDescriptor("object.rotation.270", "270°", "rotation"),
                        new EditorCommandDescriptor("object.rotation.custom", "Personnalisé...", "rotation"),
                    ]));
                modernCommands.Add(new EditorCommandDescriptor(
                    "object.mirror",
                    "Miroir",
                    "mirror",
                    Children:
                    [
                        new EditorCommandDescriptor("object.mirror.horizontal", "Horizontale", "mirror",
                            IsChecked: selected.Style.FlipHorizontally),
                        new EditorCommandDescriptor("object.mirror.vertical", "Verticale", "mirror",
                            IsChecked: selected.Style.FlipVertically),
                    ]));

                if (selected.Kind != ScadaElementKind.Group && selected.ChildElements.Count == 0)
                {
                    modernCommands.Add(new EditorCommandDescriptor(
                        "object.resize",
                        "Redimensionner",
                        "resize"));
                }
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

        if (commandId.StartsWith("table.", StringComparison.Ordinal))
        {
            var tableElement = _activeScene?.FindElementRecursive(message.Id ?? _tableEditorController.ElementId ?? "");
            if (tableElement is not null && _tableEditorController.Execute(commandId, tableElement))
            {
                SetStatus($"Commande tableau executee: {commandId}.");
            }
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
            case "object.open-in-element-studio":
                await OpenSelectedModernComponentInElementStudioAsync();
                break;
            case "object.ungroup":
            case "element-plus.ungroup":
                UngroupSelectedModernElement();
                break;
            case "object.lock":
                await ToggleSelectedElementLockAsync();
                break;
            case "object.order.bring-to-front":
                ReorderSelectedElement("bring-to-front");
                break;
            case "object.order.bring-forward":
                ReorderSelectedElement("bring-forward");
                break;
            case "object.order.send-backward":
                ReorderSelectedElement("send-backward");
                break;
            case "object.order.send-to-back":
                ReorderSelectedElement("send-to-back");
                break;
            case "object.rotation.0":
                UpdateModernElementRotation(message.Id, 0);
                break;
            case "object.rotation.90":
                UpdateModernElementRotation(message.Id, 90);
                break;
            case "object.rotation.180":
                UpdateModernElementRotation(message.Id, 180);
                break;
            case "object.rotation.270":
                UpdateModernElementRotation(message.Id, 270);
                break;
            case "object.mirror.horizontal":
                ToggleModernElementMirror(message.Id, vertical: false);
                break;
            case "object.mirror.vertical":
                ToggleModernElementMirror(message.Id, vertical: true);
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

        PageAnchorable.IsActive = true;
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

        if (current.Kind == ScadaElementKind.Table)
        {
            _tableEditorController.Execute("table.properties", current);
            return;
        }

        var dialog = new ElementPropertiesDialog(current, _modernProject?.Scenes ?? [], _modernProject?.TagCatalog)
        {
            Owner = this
        };
        dialog.SaveStateConfig = config => SaveElementStateConfigFromDialog(current.Id, config);
        dialog.SaveCommandConfig = config => SaveElementCommandConfigFromDialog(current.Id, config);
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

    private ScadaElement SaveElementStateConfigFromDialog(string elementId, ScadaElementStateConfig config)
    {
        if (_activeScene is null)
        {
            return ScadaElement.CreateInputText(elementId, elementId, 0, 0);
        }

        _activeScene = _activeScene.WithElementStateConfig(elementId, config);
        MarkActiveSceneDirty();
        RefreshModernSceneUi();
        return _activeScene.FindElementRecursive(elementId) ?? ScadaElement.CreateInputText(elementId, elementId, 0, 0);
    }

    private ScadaElement SaveElementCommandConfigFromDialog(string elementId, ScadaElementCommandConfig config)
    {
        if (_activeScene is null)
        {
            return ScadaElement.CreateInputText(elementId, elementId, 0, 0);
        }

        _activeScene = _activeScene.WithElementCommandConfig(elementId, config);
        MarkActiveSceneDirty();
        RefreshModernSceneUi();
        return _activeScene.FindElementRecursive(elementId) ?? ScadaElement.CreateInputText(elementId, elementId, 0, 0);
    }

    // DECOMMISSIONED: retained temporarily for legacy source compatibility; no active UI or bridge route may call it.
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
        PropertiesAnchorable.IsActive = true;
        OpenElementEventDialog(targetId);
    }

    private void RejectDecommissionedElementEvents(string? elementId)
    {
        _ = elementId;
        SetStatus("EventBindings decommissionnes. Utilisez StateConfig ou CommandConfig.");
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
        if (_repositoryRoot is null || _activeScene is null || _activeSceneTab is null)
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
            var workspaceSnapshot = await _pageWorkspaceController.CaptureSnapshotAsync();
            var projectSnapshot = workspaceSnapshot.Project;
            _modernProject = projectSnapshot;
            var validationIssues = ScadaProjectBuildValidator.Validate(projectSnapshot, workspaceSnapshot.Scenes.Values.ToArray()).ToArray();
            _diagnosticsPanel.Load(validationIssues, projectSnapshot, "Validation export FT100");
            var errors = validationIssues
                .Where(issue => issue.Severity == ScadaBuildValidationSeverity.Error)
                .ToArray();
            if (errors.Length > 0)
            {
                SetStatus($"Export FT100 bloque: {errors[0].Message}");
                _pageCommandController.PresentFailure("L'export FT100 est bloqué par des erreurs de validation.", validationIssues);
                return;
            }

            var inputs = await BuildFt100ProjectExportInputsAsync(projectSnapshot);
            var result = await exporter.ExportProjectAsync(projectSnapshot, inputs, dialog.FolderName);
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

        if (_repositoryRoot is null || _activeScene is null || _activeSceneTab is null)
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
            var workspaceSnapshot = await _pageWorkspaceController.CaptureSnapshotAsync();
            var projectSnapshot = workspaceSnapshot.Project;
            _modernProject = projectSnapshot;
            var validationIssues = ScadaProjectBuildValidator.Validate(projectSnapshot, workspaceSnapshot.Scenes.Values.ToArray()).ToArray();
            _diagnosticsPanel.Load(validationIssues, projectSnapshot, "Validation export FT100 .sb2");
            var errors = validationIssues
                .Where(issue => issue.Severity == ScadaBuildValidationSeverity.Error)
                .ToArray();
            if (errors.Length > 0)
            {
                SetStatus($"Export FT100 .sb2 bloque: {errors[0].Message}");
                _pageCommandController.PresentFailure("L'export .sb2 est bloqué par des erreurs de validation.", validationIssues);
                return;
            }

            SetStatus("Export FT100 .sb2 en cours: resolution des sources...");
            var inputs = await BuildFt100ProjectExportInputsAsync(projectSnapshot);
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
            AppendExportLog(ex);
        }
        finally
        {
            _isFt100Sb2ExportRunning = false;
            SetFt100ExportProgress(false);
        }
    }

    private static void AppendExportLog(Exception ex)
    {
        try
        {
            var logDir = Path.Combine(Path.GetTempPath(), "scada-builder-v2-logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "export-errors.log");
            var entry = $"{DateTime.UtcNow:O} | {ex.GetType().Name}: {ex.Message}{Environment.NewLine}";
            if (ex is AggregateException agg)
            {
                foreach (var inner in agg.InnerExceptions)
                    entry += $"  Inner: {inner.GetType().Name}: {inner.Message}{Environment.NewLine}";
            }
            else if (ex.InnerException is not null)
            {
                entry += $"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}{Environment.NewLine}";
            }
            entry += Environment.NewLine;
            File.AppendAllText(logPath, entry);
        }
        catch
        {
            // Logging must never throw.
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
        if (_repositoryRoot is null)
        {
            throw new InvalidOperationException("Aucun depot actif.");
        }

        return await _pageExportInputBuilder.BuildAsync(
            _repositoryRoot,
            project,
            _pageWorkspaceController.OpenTabs,
            _activeSceneTab,
            _activeScene);
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
            ScadaElementKind.Table => "tableau moderne",
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
        if (element.Kind == ScadaElementKind.Table)
        {
            _tableAuthoringSession.CompletePlacement(element.Id);
            _ = SyncTableEditorStateInWebViewAsync();
            RefreshTableRibbonSurface();
        }
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

    private void SelectAllSceneObjects()
    {
        if (_activeScene is null)
        {
            return;
        }

        _selectedSourceObjectIds.Clear();
        _selectedSceneObjectIds.Clear();
        foreach (var element in _activeScene.Elements)
        {
            _selectedSceneObjectIds.Add(element.Id);
        }

        _selectedSceneObject = _activeScene.Elements.Count > 0 ? _activeScene.Elements[^1] : null;

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

        var proposedScene = _activeScene.WithReplacedElementRecursive(current with { Bounds = afterBounds });
        if (!CanApplyElementTransform(proposedScene, [current.Id]))
        {
            return;
        }

        if (current.Kind == ScadaElementKind.Table && current.Table is not null)
        {
            _tableEditorController.ResizeAndMove(current, afterBounds.X, afterBounds.Y, afterBounds.Width, afterBounds.Height);
            return;
        }

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
        SetStatus($"{updated.UserLabel}: position {updated.Bounds.X:0},{updated.Bounds.Y:0}, taille {updated.Bounds.Width:0}x{updated.Bounds.Height:0}.");
    }

    private void UpdateModernElementRotation(string? id, double rotation)
    {
        var targetId = string.IsNullOrWhiteSpace(id)
            ? _selectedSceneObject?.Id
            : id;
        if (_activeScene is null || string.IsNullOrWhiteSpace(targetId))
        {
            return;
        }

        var current = _activeScene.FindElementRecursive(targetId);
        if (current is null)
        {
            return;
        }

        var normalized = NormalizeRotation(rotation);
        if (Math.Abs(current.Style.Rotation - normalized) < 0.05)
        {
            return;
        }

        var updated = current with { Style = current.Style with { Rotation = normalized } };
        CommitModernElementProperties(current, updated);
    }

    private void ToggleModernElementMirror(string? id, bool vertical)
    {
        var targetId = string.IsNullOrWhiteSpace(id)
            ? _selectedSceneObject?.Id
            : id;
        if (_activeScene is null || string.IsNullOrWhiteSpace(targetId))
        {
            return;
        }

        var current = _activeScene.FindElementRecursive(targetId);
        if (current is null)
        {
            return;
        }

        var updated = vertical
            ? current with { Style = current.Style with { FlipVertically = !current.Style.FlipVertically } }
            : current with { Style = current.Style with { FlipHorizontally = !current.Style.FlipHorizontally } };
        CommitModernElementProperties(current, updated);
    }

    private static double NormalizeRotation(double degrees)
    {
        var normalized = degrees % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }

        normalized = Math.Round(normalized, 1);
        if (normalized >= 360)
        {
            normalized -= 360;
        }

        return normalized;
    }

    private void UpdateModernGroupGeometryWithChildren(LegacyViewerMessage message)
    {
        if (_activeScene is null || string.IsNullOrWhiteSpace(message.Id))
        {
            return;
        }

        var group = _activeScene.FindElementRecursive(message.Id);
        if (group is null)
        {
            return;
        }

        var beforeGroupBounds = new SceneBounds(
            Math.Max(0, Math.Round(message.BeforeX)),
            Math.Max(0, Math.Round(message.BeforeY)),
            Math.Max(8, Math.Round(message.BeforeWidth)),
            Math.Max(8, Math.Round(message.BeforeHeight)));
        if (!HasUsableGeometrySnapshot(beforeGroupBounds))
        {
            beforeGroupBounds = group.Bounds;
        }

        var afterGroupBounds = new SceneBounds(
            Math.Max(0, Math.Round(message.X)),
            Math.Max(0, Math.Round(message.Y)),
            Math.Max(8, Math.Round(message.Width)),
            Math.Max(8, Math.Round(message.Height)));

        var elementBounds = new List<MovedSceneElementBounds>
        {
            new(group.Id, beforeGroupBounds, afterGroupBounds)
        };

        foreach (var child in message.Children ?? Enumerable.Empty<LegacyViewerChildBoundsMessage>())
        {
            if (string.IsNullOrWhiteSpace(child.Id) || _activeScene.FindElementRecursive(child.Id) is null)
            {
                continue;
            }

            var beforeChildBounds = new SceneBounds(
                Math.Round(child.BeforeX),
                Math.Round(child.BeforeY),
                Math.Max(1, Math.Round(child.BeforeWidth)),
                Math.Max(1, Math.Round(child.BeforeHeight)));
            var afterChildBounds = new SceneBounds(
                Math.Round(child.X),
                Math.Round(child.Y),
                Math.Max(1, Math.Round(child.Width)),
                Math.Max(1, Math.Round(child.Height)));

            elementBounds.Add(new MovedSceneElementBounds(child.Id, beforeChildBounds, afterChildBounds));
        }

        if (elementBounds.All(item => BoundsEqual(item.BeforeBounds, item.AfterBounds)))
        {
            return;
        }

        var updatedScene = _activeScene;
        foreach (var item in elementBounds)
        {
            var current = updatedScene.FindElementRecursive(item.ElementId);
            if (current is null)
            {
                continue;
            }

            updatedScene = updatedScene.WithReplacedElementRecursive(current with { Bounds = item.AfterBounds });
        }

        if (!CanApplyElementTransform(updatedScene, [group.Id]))
        {
            return;
        }

        _activeScene = updatedScene;
        _selectedSceneObject = updatedScene.FindElementRecursive(group.Id);
        _selectedSceneObjectIds.Add(group.Id);

        _activeSceneTab?.History.Push(new SceneSelectionMovedAction(
            updatedScene.Id,
            elementBounds,
            "resize de groupe"));

        MarkActiveSceneDirty();
        RefreshModernSceneUi();
        SetStatus($"{group.UserLabel}: groupe redimensionne {afterGroupBounds.Width:0}x{afterGroupBounds.Height:0}.");
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

        if (!CanApplyElementTransform(updatedScene, selectedIds))
        {
            return;
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
                RefreshStateAndCommandTabs();
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
            ElementForegroundColorPicker.SetColor(style.Foreground);
            ElementBoldToggle.IsChecked = string.Equals(style.FontWeight, "Bold", StringComparison.OrdinalIgnoreCase);
            ElementItalicToggle.IsChecked = !string.Equals(style.FontStyle, "Normal", StringComparison.OrdinalIgnoreCase);
            ElementUnderlineToggle.IsChecked = style.TextDecoration?.Any(value => string.Equals(value, "Underline", StringComparison.OrdinalIgnoreCase)) == true;
            ElementStrikethroughToggle.IsChecked = style.TextDecoration?.Any(value => string.Equals(value, "LineThrough", StringComparison.OrdinalIgnoreCase)) == true;
            SelectComboBoxTag(ElementTextTransformComboBox, style.TextTransform);
            ElementAlignLeftRadio.IsChecked = string.Equals(style.TextAlign, "Left", StringComparison.OrdinalIgnoreCase);
            ElementAlignCenterRadio.IsChecked = string.Equals(style.TextAlign, "Center", StringComparison.OrdinalIgnoreCase);
            ElementAlignRightRadio.IsChecked = string.Equals(style.TextAlign, "Right", StringComparison.OrdinalIgnoreCase);
            ElementAlignJustifyRadio.IsChecked = string.Equals(style.TextAlign, "Justify", StringComparison.OrdinalIgnoreCase);
            ElementLetterSpacingTextBox.Text = style.LetterSpacing.ToString("0.##");
            ElementLineHeightTextBox.Text = style.LineHeight.ToString("0.##");
            ElementBackgroundColorPicker.SetColor(style.Background);
            ElementBorderColorPicker.SetColor(style.BorderColor);
            SelectComboBoxText(ElementBorderStyleComboBox, style.BorderStyle);
            ElementBorderWidthTextBox.Text = style.BorderWidth.ToString("0.##");
            ElementBorderRadiusTextBox.Text = style.BorderRadius?.Normalized().TopLeft.ToString("0.##") ?? "0";
            ShadowNoneRadio.IsChecked = style.ShadowPreset == "None";
            ShadowSoftRadio.IsChecked = style.ShadowPreset == "Soft";
            ShadowRaisedRadio.IsChecked = style.ShadowPreset == "Raised";
            ShadowInsetRadio.IsChecked = style.ShadowPreset == "Inset";
            ElementOpacityTextBox.Text = style.Opacity.ToString("0.##");
            ElementRotationTextBox.Text = style.Rotation.ToString("0.##");
            ElementAdvancedCssTextBox.Text = style.AdvancedCss ?? "";
            UpdateElementStylePreview(style);
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
            RefreshStateAndCommandTabs();
        }
        finally
        {
            _isUpdatingElementProperties = false;
        }
    }

    private ScadaStateRule? _testedStateRule;

    private void RefreshStateAndCommandTabs()
    {
        var element = _activeScene?.FindElementRecursive(_selectedSceneObject?.Id ?? string.Empty);
        StateRulesListBox.ItemsSource = element?.EffectiveStateConfig.States;
        CommandsListBox.ItemsSource = element?.EffectiveCommandConfig.Commands;

        var readVariable = element?.EffectiveStateConfig.ReadVariable;
        var hasReadVariable = readVariable is not null;
        ReadVariableSummaryText.Text = hasReadVariable
            ? $"Lecture: {FormatProjectTag(readVariable!.TagId)}{(string.IsNullOrWhiteSpace(readVariable.DisplayFormat) ? "" : $" -> {readVariable.DisplayFormat}")}"
            : "Aucune lecture de variable configuree.";
        AddReadVariableButton.Visibility = hasReadVariable ? Visibility.Collapsed : Visibility.Visible;
        EditReadVariableButton.Visibility = hasReadVariable ? Visibility.Visible : Visibility.Collapsed;
        RemoveReadVariableButton.Visibility = hasReadVariable ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnEditReadVariableClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null || _selectedSceneObject is null)
        {
            return;
        }

        var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
        if (element is null)
        {
            return;
        }

        var dialog = new ElementReadVariableDialog(element.EffectiveStateConfig.ReadVariable, _modernProject?.TagCatalog) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        var config = element.EffectiveStateConfig with { ReadVariable = dialog.Result };
        _activeScene = _activeScene.WithElementStateConfig(_selectedSceneObject.Id, config);
        RefreshStateAndCommandTabs();
    }

    private void OnRemoveReadVariableClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null || _selectedSceneObject is null)
        {
            return;
        }

        var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
        if (element is null)
        {
            return;
        }

        var config = element.EffectiveStateConfig with { ReadVariable = null };
        _activeScene = _activeScene.WithElementStateConfig(_selectedSceneObject.Id, config);
        RefreshStateAndCommandTabs();
    }

    private void OnAddStateRuleClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null || _selectedSceneObject is null)
        {
            return;
        }

        var dialog = new ElementStateRuleDialog(null, _modernProject?.TagCatalog) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
        if (element is null)
        {
            return;
        }

        var config = element.EffectiveStateConfig with
        {
            States = element.EffectiveStateConfig.States.Append(dialog.Result).ToArray()
        };
        _activeScene = _activeScene.WithElementStateConfig(_selectedSceneObject.Id, config);
        RefreshStateAndCommandTabs();
    }

    private void OnStateRuleDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OnEditStateRuleClick(sender, e);
    }

    private void OnEditStateRuleClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null || _selectedSceneObject is null)
        {
            return;
        }

        if (StateRulesListBox.SelectedItem is not ScadaStateRule selected)
        {
            return;
        }

        var dialog = new ElementStateRuleDialog(selected, _modernProject?.TagCatalog) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
        if (element is null)
        {
            return;
        }

        var states = element.EffectiveStateConfig.States
            .Select(rule => rule.Id == dialog.Result.Id ? dialog.Result : rule)
            .ToArray();
        _activeScene = _activeScene.WithElementStateConfig(_selectedSceneObject.Id, element.EffectiveStateConfig with { States = states });
        RefreshStateAndCommandTabs();
    }

    private void OnDeleteStateRuleClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null || _selectedSceneObject is null)
        {
            return;
        }

        if (StateRulesListBox.SelectedItem is not ScadaStateRule selected)
        {
            return;
        }

        var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
        if (element is null)
        {
            return;
        }

        var states = element.EffectiveStateConfig.States.Where(rule => rule.Id != selected.Id).ToArray();
        _activeScene = _activeScene.WithElementStateConfig(_selectedSceneObject.Id, element.EffectiveStateConfig with { States = states });
        RefreshStateAndCommandTabs();
    }

    private void OnMoveStateRuleUpClick(object sender, RoutedEventArgs e) => MoveSelectedStateRule(-1);

    private void OnMoveStateRuleDownClick(object sender, RoutedEventArgs e) => MoveSelectedStateRule(1);

    private void MoveSelectedStateRule(int offset)
    {
        if (_activeScene is null || _selectedSceneObject is null)
        {
            return;
        }

        if (StateRulesListBox.SelectedItem is not ScadaStateRule selected)
        {
            return;
        }

        var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
        if (element is null)
        {
            return;
        }

        var states = element.EffectiveStateConfig.States.ToList();
        var index = states.FindIndex(rule => rule.Id == selected.Id);
        var newIndex = index + offset;
        if (index < 0 || newIndex < 0 || newIndex >= states.Count)
        {
            return;
        }

        (states[index], states[newIndex]) = (states[newIndex], states[index]);
        _activeScene = _activeScene.WithElementStateConfig(_selectedSceneObject.Id, element.EffectiveStateConfig with { States = states });
        RefreshStateAndCommandTabs();
    }

    private void OnTestStateRuleToggleClick(object sender, RoutedEventArgs e)
    {
        if (StateRulesListBox.SelectedItem is not ScadaStateRule selected)
        {
            TestStateRuleToggle.IsChecked = false;
            return;
        }

        _testedStateRule = TestStateRuleToggle.IsChecked == true ? selected : null;
    }

    private void OnAddCommandClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null || _selectedSceneObject is null)
        {
            return;
        }

        var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
        if (element is null)
        {
            return;
        }

        var usedKinds = element.EffectiveCommandConfig.Commands.Select(c => c.Kind).ToArray();
        var dialog = new ElementCommandDialog(null, _modernProject?.Scenes ?? [], _modernProject?.TagCatalog, usedKinds) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        var config = element.EffectiveCommandConfig with
        {
            Commands = element.EffectiveCommandConfig.Commands.Append(dialog.Result).ToArray()
        };
        _activeScene = _activeScene.WithElementCommandConfig(_selectedSceneObject.Id, config);
        RefreshStateAndCommandTabs();
    }

    private void OnCommandDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OnEditCommandClick(sender, e);
    }

    private void OnEditCommandClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null || _selectedSceneObject is null)
        {
            return;
        }

        if (CommandsListBox.SelectedItem is not ScadaCommandBinding selected)
        {
            return;
        }

        var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
        if (element is null)
        {
            return;
        }

        var usedKinds = element.EffectiveCommandConfig.Commands
            .Where(c => c.Id != selected.Id)
            .Select(c => c.Kind)
            .ToArray();
        var dialog = new ElementCommandDialog(selected, _modernProject?.Scenes ?? [], _modernProject?.TagCatalog, usedKinds) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null)
        {
            return;
        }

        var commands = element.EffectiveCommandConfig.Commands
            .Select(command => command.Id == dialog.Result.Id ? dialog.Result : command)
            .ToArray();
        _activeScene = _activeScene.WithElementCommandConfig(_selectedSceneObject.Id, element.EffectiveCommandConfig with { Commands = commands });
        RefreshStateAndCommandTabs();
    }

    private void OnDeleteCommandClick(object sender, RoutedEventArgs e)
    {
        if (_activeScene is null || _selectedSceneObject is null)
        {
            return;
        }

        if (CommandsListBox.SelectedItem is not ScadaCommandBinding selected)
        {
            return;
        }

        var element = _activeScene.FindElementRecursive(_selectedSceneObject.Id);
        if (element is null)
        {
            return;
        }

        var commands = element.EffectiveCommandConfig.Commands.Where(command => command.Id != selected.Id).ToArray();
        _activeScene = _activeScene.WithElementCommandConfig(_selectedSceneObject.Id, element.EffectiveCommandConfig with { Commands = commands });
        RefreshStateAndCommandTabs();
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
                Foreground = result.Foreground,
                Background = result.Background,
                BorderColor = result.BorderColor,
                BorderStyle = result.BorderStyle,
                BorderWidth = result.BorderWidth,
                ShadowPreset = result.ShadowPreset,
                Opacity = result.Opacity,
                Rotation = result.Rotation,
                AdvancedCss = result.AdvancedCss,
                FontWeight = result.FontWeight,
                FontStyle = result.FontStyle,
                TextDecoration = result.TextDecoration,
                TextAlign = result.TextAlign,
                TextTransform = result.TextTransform,
                LetterSpacing = result.LetterSpacing,
                LineHeight = result.LineHeight,
                BorderRadius = result.BorderRadius
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

        var proposedScene = _activeScene.WithReplacedElementRecursive(updated);
        if (!CanApplyElementTransform(proposedScene, [updated.Id]))
        {
            return;
        }

        _activeScene = proposedScene;
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

    // DECOMMISSIONED: retained temporarily for legacy source compatibility; authoring uses StateConfig/CommandConfig.
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
            _modernProject?.Scenes ?? [],
            _modernProject?.TagCatalog)
        {
            Owner = owner ?? this
        };

        dialog.AddEvent = result => AddElementEventFromDialog(current.Id, result, dialog);
        dialog.DeleteEvent = request => DeleteElementEventFromDialog(current.Id, request, dialog);
        dialog.CreateTag = () => "Creation de tags disponible dans une prochaine revision apres import des protocoles projet.";
        dialog.ShowDialog();
    }

    // DECOMMISSIONED: retained temporarily for migration support; new authoring must not invoke this path.
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

    // DECOMMISSIONED: retained temporarily for migration support; new authoring must not invoke this path.
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
                Foreground = GetColorPickerValue(ElementForegroundColorPicker, style.Foreground),
                Background = GetColorPickerValue(ElementBackgroundColorPicker, style.Background),
                BorderColor = GetColorPickerValue(ElementBorderColorPicker, style.BorderColor),
                BorderStyle = GetComboBoxText(ElementBorderStyleComboBox, style.BorderStyle),
                BorderWidth = GetEffectiveBorderWidth(
                    GetComboBoxText(ElementBorderStyleComboBox, style.BorderStyle),
                    ParseDoubleOrDefault(ElementBorderWidthTextBox.Text, style.BorderWidth)),
                ShadowPreset = GetSelectedShadowPreset(),
                Opacity = Math.Clamp(ParseDoubleOrDefault(ElementOpacityTextBox.Text, style.Opacity), 0, 1),
                Rotation = ParseDoubleOrDefault(ElementRotationTextBox.Text, style.Rotation),
                AdvancedCss = string.IsNullOrWhiteSpace(ElementAdvancedCssTextBox.Text) ? null : ElementAdvancedCssTextBox.Text,
                FontWeight = ElementBoldToggle.IsChecked == true ? "Bold" : "Normal",
                FontStyle = ElementItalicToggle.IsChecked == true ? "Italic" : "Normal",
                TextDecoration = GetTextDecorationDock(),
                TextAlign = GetSelectedTextAlignDock(),
                TextTransform = (ElementTextTransformComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "None",
                LetterSpacing = Math.Clamp(ParseDoubleOrDefault(ElementLetterSpacingTextBox.Text, 0), -10, 50),
                LineHeight = Math.Max(0, ParseDoubleOrDefault(ElementLineHeightTextBox.Text, 0)),
                BorderRadius = GetBorderRadiusDock()
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

        UpdateElementStylePreview(updated.Style ?? style);
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
            .Select((element, index) => ModernElementRenderPayloadFactory.Create(element, selectedIds, index));
        var json = JsonSerializer.Serialize(payload);
        await PreviewWebView.ExecuteScriptAsync($"window.scadaSceneEditor && window.scadaSceneEditor.renderModernElements({json});");
        await SyncTableEditorStateInWebViewAsync();
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

    private static bool ContainsElement(ScadaElement element, string elementId)
    {
        return element.Id == elementId || element.ChildElements.Any(child => ContainsElement(child, elementId));
    }

    private IReadOnlyList<ScadaElement> ResolveTopLevelSelectedElements()
    {
        if (_activeScene is null)
        {
            return Array.Empty<ScadaElement>();
        }

        var selected = _selectedSceneObjectIds
            .Select(id => _activeScene.FindElementRecursive(id))
            .Where(element => element is not null)
            .Select(element => element!)
            .ToArray();

        return selected
            .Where(element => !selected.Any(candidate =>
                !string.Equals(candidate.Id, element.Id, StringComparison.Ordinal) &&
                ContainsElement(candidate, element.Id)))
            .ToArray();
    }

    private void CopySelectionToClipboard()
    {
        var selectedElements = ResolveTopLevelSelectedElements();
        if (selectedElements.Count == 0)
        {
            SetStatus("Aucun objet selectionne a copier.");
            return;
        }

        _sceneClipboard.Copy(selectedElements);
        SetStatus($"{selectedElements.Count} objet(s) copie(s).");
    }

    private static ScadaElement CloneWithNewIds(ScadaElement element, double offsetX, double offsetY)
    {
        var clonedChildren = element.Children?
            .Select(child => CloneWithNewIds(child, 0, 0))
            .ToArray();

        return element with
        {
            Id = Guid.NewGuid().ToString("N"),
            Bounds = element.Bounds with { X = element.Bounds.X + offsetX, Y = element.Bounds.Y + offsetY },
            Children = clonedChildren
        };
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
            ActivePageKey = _activeSceneTab?.PageKey,
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
            GetSceneByPageKey = _pageWorkspaceController.GetScene,
            ReplaceSceneByPageKey = _pageWorkspaceController.ReplaceScene,
            RemoveSceneByPageKey = _pageWorkspaceController.RemoveScene,
            GetWorkspaceSceneKeys = _pageWorkspaceController.GetWorkspaceSceneKeys,
            GetProject = () => _modernProject,
            ReplaceProject = project =>
            {
                _modernProject = project;
                _pageWorkspaceController.ReplaceProject(project);
                _pagesPanel.Load(project);
                PagesListBox.ItemsSource = _pagesPanel.View;
            },
            RestoreWorkspaceUi = _pageWorkspaceController.RestoreUi,
            SetPendingDeletedPageKeys = _pageWorkspaceController.SetPendingDeletedPageKeys,
            SetWorkspaceDirty = _pageWorkspaceController.SetWorkspaceDirty,
            MarkDirty = MarkActiveSceneDirty,
            RefreshPreviewAsync = RefreshPreviewAfterHistoryAsync,
            RefreshTargetAsync = async target =>
            {
                await _pageWorkspaceController.RefreshHistoryTargetAsync(target);
                if (target.Scope == EditorHistoryScope.Project)
                {
                    var selectedKey = _pageWorkspaceController.HistorySelectedPageKey;
                    _pagesPanel.SelectedPage = _pagesPanel.Items.FirstOrDefault(item => item.PageKey == selectedKey);
                    PagesListBox.SelectedItem = _pagesPanel.SelectedPage;
                }
            },
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

    private void UpdateElementStylePreview(ScadaElementStyle style)
    {
        if (ElementStylePreviewText is null || ElementStylePreviewBorder is null)
        {
            return;
        }

        ElementStylePreviewText.FontFamily = new FontFamily(style.FontFamily);
        ElementStylePreviewText.FontSize = Math.Max(6, style.FontSize);
        ElementStylePreviewText.FontWeight = string.Equals(style.FontWeight, "Bold", StringComparison.OrdinalIgnoreCase) ? FontWeights.Bold : FontWeights.Normal;
        ElementStylePreviewText.FontStyle = string.Equals(style.FontStyle, "Normal", StringComparison.OrdinalIgnoreCase) ? FontStyles.Normal : FontStyles.Italic;
        ElementStylePreviewText.Text = ApplyPreviewTextTransform("Aperçu Element+", style.TextTransform);
        ElementStylePreviewText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        ElementStylePreviewText.LineHeight = style.LineHeight > 0 ? style.LineHeight : double.NaN;
        ElementStylePreviewText.TextAlignment = style.TextAlign switch
        {
            "Center" => TextAlignment.Center,
            "Right" => TextAlignment.Right,
            "Justify" => TextAlignment.Justify,
            _ => TextAlignment.Left
        };
        var decorations = new TextDecorationCollection();
        if (style.TextDecoration?.Any(value => string.Equals(value, "Underline", StringComparison.OrdinalIgnoreCase)) == true)
        {
            decorations.Add(TextDecorations.Underline[0]);
        }
        if (style.TextDecoration?.Any(value => string.Equals(value, "LineThrough", StringComparison.OrdinalIgnoreCase)) == true)
        {
            decorations.Add(TextDecorations.Strikethrough[0]);
        }
        ElementStylePreviewText.TextDecorations = decorations.Count == 0 ? null : decorations;
        ElementStylePreviewText.Foreground = ToPreviewBrush(style.Foreground);
        ElementStylePreviewBorder.Background = ToPreviewBrush(style.Background);
        ElementStylePreviewBorder.BorderBrush = ToPreviewBrush(style.BorderColor);
        var borderStyle = style.BorderStyle?.Trim().ToLowerInvariant() ?? "none";
        var borderWidth = GetEffectiveBorderWidth(borderStyle, style.BorderWidth);
        ElementStylePreviewBorder.BorderThickness = new Thickness(borderWidth);
        var radius = style.BorderRadius?.Normalized().TopLeft ?? 0;
        ElementStylePreviewBorder.CornerRadius = new CornerRadius(radius);
        ElementStylePreviewBorder.Effect = borderStyle switch
        {
            "inset" => new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 315,
                ShadowDepth = 0,
                BlurRadius = 4,
                Opacity = 0.35
            },
            "outset" => new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.White,
                Direction = 135,
                ShadowDepth = 1,
                BlurRadius = 3,
                Opacity = 0.75
            },
            _ => null
        };
        ElementStylePreviewBorder.Opacity = Math.Clamp(style.Opacity, 0, 1);
        ElementStylePreviewBorder.RenderTransform = new RotateTransform(style.Rotation);
    }

    private static double GetEffectiveBorderWidth(string? borderStyle, double width)
    {
        var normalizedStyle = borderStyle?.Trim() ?? "None";
        return !string.Equals(normalizedStyle, "None", StringComparison.OrdinalIgnoreCase) && width <= 0
            ? 1
            : Math.Max(0, width);
    }

    private static string ApplyPreviewTextTransform(string text, string? transform)
    {
        return transform?.ToLowerInvariant() switch
        {
            "uppercase" => text.ToUpperInvariant(),
            "lowercase" => text.ToLowerInvariant(),
            "capitalize" => string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant())),
            _ => text
        };
    }

    private static Brush ToPreviewBrush(string value)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }
        catch
        {
            return Brushes.Transparent;
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

    private static void SelectComboBoxTag(ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private IReadOnlyList<string>? GetTextDecorationDock()
    {
        var values = new List<string>();
        if (ElementUnderlineToggle.IsChecked == true) values.Add("Underline");
        if (ElementStrikethroughToggle.IsChecked == true) values.Add("LineThrough");
        return values.Count == 0 ? null : values;
    }

    private string GetSelectedTextAlignDock()
    {
        if (ElementAlignCenterRadio.IsChecked == true) return "Center";
        if (ElementAlignRightRadio.IsChecked == true) return "Right";
        if (ElementAlignJustifyRadio.IsChecked == true) return "Justify";
        return "Left";
    }

    private ScadaBorderRadius? GetBorderRadiusDock()
    {
        var radius = Math.Max(0, ParseDoubleOrDefault(ElementBorderRadiusTextBox.Text, 0));
        return radius <= 0 ? null : new ScadaBorderRadius(radius, radius, radius, radius);
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

    private void RegisterPageApplicationCommands()
    {
        _applicationCommandRegistry.Register(new NewPageCommand(_pageCommandCoordinator));
        _applicationCommandRegistry.Register(new RenamePageCommand(_pageCommandCoordinator));
        _applicationCommandRegistry.Register(new DuplicatePageCommand(_pageCommandCoordinator));
        _applicationCommandRegistry.Register(new DeletePageCommand(_pageCommandCoordinator));
        _applicationCommandRegistry.Register(new ChangePageCodeCommand(_pageCommandCoordinator));
        _applicationCommandRegistry.Register(new OpenPageCommand(_pageCommandCoordinator));
        _applicationCommandRegistry.Register(new ShowPagePropertiesCommand(_pageCommandCoordinator));
        _applicationCommandRegistry.Register(new SetPageBuildInclusionCommand(_pageCommandCoordinator));
        _applicationCommandRegistry.Register(new SetHomePageCommand(_pageCommandCoordinator));
        _applicationCommandRegistry.Register(new SetPageTypeCommand(_pageCommandCoordinator));
        _applicationCommandRegistry.Register(new SetPageCompositionCommand(_pageCommandCoordinator));
        _applicationCommandRegistry.Register(new SetPageCanvasCommand(_pageCommandCoordinator));
        _applicationCommandRegistry.Register(new SetPageBackgroundCommand(_pageCommandCoordinator));
        _applicationCommandRegistry.Register(new ValidatePagesCommand(_pageCommandCoordinator));
    }

    private void OnPageCommandCompleted(ScadaProject project, CommandResult result)
    {
        _modernProject = project;
        _pageWorkspaceController.ReplaceProject(project, result.WorkspaceDirty);
        var selectedKey = result.PageToSelectKey ?? _pagesPanel.SelectedPage?.PageKey;
        _pagesPanel.Load(project, result.Diagnostics);
        _pagesPanel.SelectedPage = _pagesPanel.Items.FirstOrDefault(item => item.PageKey == selectedKey);
        _isUpdatingPageSelection = true;
        try
        {
            PagesListBox.SelectedItem = _pagesPanel.SelectedPage;
        }
        finally
        {
            _isUpdatingPageSelection = false;
        }

        RefreshActiveRibbonCommandStates();

        if (result.Message.Length > 0) SetStatus(result.Message);
        if (result.PageToOpenKey is not null) PageAnchorable.IsActive = true;
    }

    private ScadaSceneReference? GetSelectedModernPage()
    {
        var key = (PagesListBox.SelectedItem as PageListItemViewModel)?.PageKey ?? _pagesPanel.SelectedPage?.PageKey;
        return key is { } pageKey ? _modernProject?.Scenes.FirstOrDefault(page => page.PageKey == pageKey) : null;
    }

    private async void OnPageCommandClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string commandId } source) return;
        if (source.DataContext is PageListItemViewModel item)
        {
            _pagesPanel.SelectedPage = item;
            PagesListBox.SelectedItem = item;
        }
        await ExecutePageSurfaceCommandAsync(commandId);
        e.Handled = true;
    }

    private async Task ExecutePageSurfaceCommandAsync(string commandId)
    {
        var page = GetSelectedModernPage();
        CommandResult result;
        if (commandId == "page.set-home" && page is not null)
        {
            result = await _pageCommandController.ExecuteAsync(commandId, new SetHomePageRequest(page.PageKey), page.PageKey);
        }
        else if (commandId == "page.set-build-inclusion" && page is not null)
        {
            result = await _pageCommandController.ExecuteAsync(commandId, new SetPageBuildInclusionRequest(page.PageKey, !page.IncludeInBuild), page.PageKey);
        }
        else
        {
            result = await _pageCommandController.ExecuteInteractiveAsync(commandId, page);
        }

        if (result.Status == CommandResultStatus.Succeeded && commandId == "page.properties")
        {
            PageAnchorable.Show();
            PageAnchorable.IsActive = true;
        }

        if (commandId == "page.validate" || result.Diagnostics.Count > 0)
        {
            _diagnosticsPanel.Load(result.Diagnostics, _modernProject, $"Commande {commandId}");
        }

        if (result.Status is CommandResultStatus.Blocked or CommandResultStatus.Failed)
        {
            _pageCommandController.PresentFailure(result);
        }
    }

    private async void OnPagesListMouseDoubleClick(object sender, MouseButtonEventArgs e) =>
        await ExecutePageSurfaceCommandAsync("page.open");

    private void OnPagesSearchTextChanged(object sender, TextChangedEventArgs e) =>
        _pagesPanel.SearchText = PagesSearchTextBox.Text;

    private void OnPagesFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PagesTypeFilterComboBox.SelectedItem is string type) _pagesPanel.TypeFilter = type;
        if (PagesBuildFilterComboBox.SelectedItem is string build) _pagesPanel.BuildFilter = build;
    }

    private async void OnPagesListPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var commandId = e.Key switch
        {
            Key.Enter => "page.open",
            Key.F2 => "page.rename",
            Key.Delete => "page.delete",
            Key.D when Keyboard.Modifiers == ModifierKeys.Control => "page.duplicate",
            _ => null
        };
        if (commandId is null) return;
        e.Handled = true;
        await ExecutePageSurfaceCommandAsync(commandId);
    }

    private void OnPagesListPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source is not null && source is not ListBoxItem)
        {
            source = source switch
            {
                FrameworkContentElement content => ContentOperations.GetParent(content),
                Visual visual => VisualTreeHelper.GetParent(visual),
                _ => LogicalTreeHelper.GetParent(source)
            };
        }
        if (source is ListBoxItem item)
        {
            _isUpdatingPageSelection = true;
            try
            {
                item.IsSelected = true;
                item.Focus();
            }
            finally
            {
                _isUpdatingPageSelection = false;
            }
            _pagesPanel.SelectedPage = item.DataContext as PageListItemViewModel;
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
        var isElementLockCommand = string.Equals(definition.Id, "object.lock", StringComparison.Ordinal);
        var isTableStateActive = definition.Id switch
        {
            "table.mode.object" => _tableAuthoringSession.Mode == TableInteractionMode.Object,
            "table.mode.cells" => _tableAuthoringSession.Mode == TableInteractionMode.Cells,
            "table.editor-guides" => _tableAuthoringSession.EditorGuidesVisible,
            "table.merge-toggle" => _tableAuthoringSession.SelectionContainsMergedCells,
            _ => false
        };
        var requiresPageSelection = definition.Id is
            "page.rename" or
            "page.duplicate" or
            "page.delete" or
            "page.properties";
        var hasPageSelection = GetSelectedModernPage() is not null;
        var hasRequiredElementSelection = !isElementLockCommand || ElementLockState.IsEnabled;
        var isEnabled = definition.IsEnabled && (!requiresPageSelection || hasPageSelection) && hasRequiredElementSelection;
        var disabledReason = !definition.IsEnabled
            ? definition.DisabledReason
            : requiresPageSelection && !hasPageSelection
                ? "Selectionnez une page dans le panneau Projet > Pages."
                : isElementLockCommand && !ElementLockState.IsEnabled
                    ? "Selectionnez au moins un Element+."
                : null;
        var toolTip = disabledReason is null
            ? definition.ToolTip
            : $"{definition.ToolTip} — Indisponible: {disabledReason}";
        var command = isEnabled
            ? new RibbonRelayCommand(() => ExecuteRibbonCommand(definition.Id), () => true)
            : new RibbonRelayCommand(() => SetStatus($"{definition.Label}: {disabledReason}"), () => false);

        return new RibbonCommandViewModel(
            definition.Id,
            isElementLockCommand ? ElementLockState.ActionLabel : definition.Label,
            toolTip,
            definition.IconKey,
            ResolveRibbonIcon(definition.IconKey),
            isEnabled,
            isElementLockCommand
                ? ElementLockState.IsToggleChecked
                : isTableStateActive || string.Equals(definition.Id, _activeInsertCommandId, StringComparison.Ordinal),
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
        InsertFamilies.Clear();
        InsertFamilySurface.Visibility = ribbonKey == "Insert" ? Visibility.Visible : Visibility.Collapsed;
        TableCreationConfigurationSurface.Visibility = Visibility.Collapsed;
        if (ribbonKey == "Insert")
        {
            foreach (var family in RibbonCommandCatalog.CreateInsertFamilies())
            {
                var familyId = family.Id;
                InsertFamilies.Add(new RibbonFamilyViewModel(
                    family.Id,
                    family.Label,
                    ResolveRibbonIcon(family.IconKey),
                    string.Equals(family.Id, _activeInsertFamilyId, StringComparison.Ordinal),
                    new RibbonRelayCommand(() => SetActiveInsertFamily(familyId), () => true)));
            }
            if (_tableAuthoringSession.IsSurfaceOpen && _activeInsertFamilyId == "data")
            {
                RefreshTableRibbonSurface();
            }
            else
            {
            var activeFamily = RibbonCommandCatalog.CreateInsertFamilies().FirstOrDefault(family => family.Id == _activeInsertFamilyId)
                ?? RibbonCommandCatalog.CreateInsertFamilies()[0];
            foreach (var group in activeFamily.Tools.GroupBy(tool => tool.GroupLabel, StringComparer.Ordinal))
            {
                ActiveRibbonGroups.Add(CreateRibbonGroupViewModel(new RibbonGroupDefinition(
                    group.Key,
                    group.Select(tool => new RibbonCommandDefinition(tool.Id, tool.Label, tool.IconKey, tool.ToolTip, tool.IsEnabled, tool.DisabledReason)).ToArray())));
            }
            }
        }
        else if (_ribbonTabs.TryGetValue(ribbonKey, out var groups))
        {
            foreach (var group in groups)
            {
                ActiveRibbonGroups.Add(CreateRibbonGroupViewModel(group));
            }
        }

        SetRibbonMenuButtonStyle(FileMenuButton, ribbonKey == "File");
        SetRibbonMenuButtonStyle(EditMenuButton, ribbonKey == "Edit");
        SetRibbonMenuButtonStyle(PagesMenuButton, ribbonKey == "Pages");
        SetRibbonMenuButtonStyle(InsertMenuButton, ribbonKey == "Insert");
        SetRibbonMenuButtonStyle(ScreenMenuButton, ribbonKey == "Screen");
        SetRibbonMenuButtonStyle(SelectionMenuButton, ribbonKey == "Selection");
        SetRibbonMenuButtonStyle(ToolsMenuButton, ribbonKey == "Tools");
    }

    private void SetActiveInsertFamily(string familyId)
    {
        if (familyId != "data")
        {
            _tableAuthoringSession.CloseSurface();
            _ = SyncTableEditorStateInWebViewAsync();
        }
        _activeInsertFamilyId = familyId;
        SetActiveRibbon("Insert");
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
        var insertTool = InsertToolCatalog.Find(commandId);
        if (insertTool is { IsEnabled: true })
        {
            if (insertTool.PlacementMode == InsertPlacementMode.ContextualSurface)
            {
                OpenTableAuthoringSurface();
                return;
            }
            BeginInsertToolPlacement(insertTool);
            return;
        }

        switch (commandId)
        {
            case "page.new":
            case "page.rename":
            case "page.duplicate":
            case "page.delete":
            case "page.properties":
            case "page.validate":
                await ExecutePageSurfaceCommandAsync(commandId);
                break;
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
            case "tool.element-studio":
                await OpenElementStudioFromToolPaletteAsync();
                break;
            case "tool.settings":
                await OpenConfigurationWindowAsync();
                break;
            case "object.ungroup":
                UngroupSelectedModernElement();
                break;
            case "object.lock":
                await ToggleSelectedElementLockAsync();
                break;
            case "table.add":
                BeginConfiguredTablePlacement();
                break;
            case "table.autofit":
                await RequestTableAutoFitAsync();
                break;
            case "table.back":
                CloseTableAuthoringSurface();
                break;
            case "table.mode.object":
                await SetTableModeAsync(TableInteractionMode.Object);
                break;
            case "table.mode.cells":
                await SetTableModeAsync(TableInteractionMode.Cells);
                break;
            case "table.editor-guides":
                await ToggleTableEditorGuidesAsync();
                break;
            case "table.content.text":
                if (_selectedSceneObject is not null) _tableEditorController.ConvertContent(_selectedSceneObject, ScadaTableCellContentKind.Text);
                break;
            case "table.content.input-text":
                if (_selectedSceneObject is not null) _tableEditorController.ConvertContent(_selectedSceneObject, ScadaTableCellContentKind.InputText);
                break;
            case "table.content.input-numeric":
                if (_selectedSceneObject is not null) _tableEditorController.ConvertContent(_selectedSceneObject, ScadaTableCellContentKind.InputNumeric);
                break;
            case "table.select.all":
                if (_selectedSceneObject is not null)
                {
                    _tableEditorController.SelectAll(_selectedSceneObject);
                    _tableAuthoringSession.SetSelection(_tableEditorController.Selection, _tableEditorController.SelectionContainsMergedCells(_selectedSceneObject));
                    RefreshTablePropertiesPanel();
                    RefreshTableRibbonSurface();
                }
                break;
            case "table.merge-toggle": case "table.merge": case "table.unmerge": case "table.format": case "table.row.height": case "table.column.width": case "table.properties": case "table.content.properties": case "table.numeric.properties": case "table.binding.read": case "table.binding.write": case "table.borders": case "table.headers": case "table.header.mark": case "table.header.unmark": case "table.equalize": case "table.distribute.rows": case "table.distribute.columns": case "table.format.reset": case "table.row.insert": case "table.column.insert": case "table.row.delete": case "table.column.delete":
                if (_selectedSceneObject is not null) _tableEditorController.Execute(commandId, _selectedSceneObject);
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
        var context = CreateEditorHistoryContext();
        if (_pageWorkspaceController.History.UndoCount > 0 && await _pageWorkspaceController.History.UndoAsync(context))
        {
            return;
        }

        if (_activeSceneTab is null || _activeScene is null)
        {
            SetStatus("Undo ignore: aucune scene active.");
            return;
        }

        if (!await _activeSceneTab.History.UndoAsync(context))
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
        var context = CreateEditorHistoryContext();
        if (_pageWorkspaceController.History.RedoCount > 0 && await _pageWorkspaceController.History.RedoAsync(context))
        {
            return;
        }

        if (_activeSceneTab is null || _activeScene is null)
        {
            SetStatus("Redo ignore: aucune scene active.");
            return;
        }

        if (!await _activeSceneTab.History.RedoAsync(context))
        {
            SetStatus("Aucune operation a retablir dans cette scene.");
        }
    }

    private void HandleShortcut(string? key, bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (!ctrlKey || !TryParseShortcutKey(key, out var shortcutKey))
        {
            return;
        }

        var modifiers = ShortcutModifiers.Control
            | (shiftKey ? ShortcutModifiers.Shift : ShortcutModifiers.None)
            | (altKey ? ShortcutModifiers.Alt : ShortcutModifiers.None);

        switch (_shortcutRegistry.Resolve(shortcutKey, modifiers))
        {
            case "selection.select-all":
                SelectAllSceneObjects();
                break;
            case "clipboard.copy":
                CopySelectionToClipboard();
                break;
            case "clipboard.cut":
                _ = CutSelectionAsync();
                break;
            case "clipboard.paste":
                PasteClipboard();
                break;
            case "history.undo":
                _ = UndoLastSceneOperationAsync();
                break;
            case "history.redo":
                _ = RedoLastSceneOperationAsync();
                break;
        }
    }

    private static bool TryParseShortcutKey(string? key, out ShortcutKey shortcutKey)
    {
        switch ((key ?? "").ToLowerInvariant())
        {
            case "a":
                shortcutKey = ShortcutKey.A;
                return true;
            case "c":
                shortcutKey = ShortcutKey.C;
                return true;
            case "v":
                shortcutKey = ShortcutKey.V;
                return true;
            case "x":
                shortcutKey = ShortcutKey.X;
                return true;
            case "y":
                shortcutKey = ShortcutKey.Y;
                return true;
            case "z":
                shortcutKey = ShortcutKey.Z;
                return true;
            default:
                shortcutKey = default;
                return false;
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


}
