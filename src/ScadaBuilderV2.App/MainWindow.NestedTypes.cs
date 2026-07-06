using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using ScadaBuilderV2.Domain.Elements;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App;

// Nested view-model / message / payload types used by MainWindow, isolated from
// MainWindow.xaml.cs as a behavior-preserving split. SceneWorkspaceTab remains in
// the main file (pinned by SceneWorkspaceTabContractTests).
public partial class MainWindow
{
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

        public List<LegacyViewerChildBoundsMessage>? Children { get; set; }
    }

    private sealed class LegacyViewerChildBoundsMessage
    {
        public string Id { get; set; } = "";

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double BeforeX { get; set; }

        public double BeforeY { get; set; }

        public double BeforeWidth { get; set; }

        public double BeforeHeight { get; set; }
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
