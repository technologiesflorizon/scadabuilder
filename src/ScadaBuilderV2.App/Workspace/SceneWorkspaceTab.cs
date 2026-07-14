using System.ComponentModel;
using ScadaBuilderV2.App.Pages;
using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App.Workspace;

/// <summary>One open modern page editor tab; imported source data is optional provenance, never identity.</summary>
public sealed class SceneWorkspaceTab : INotifyPropertyChanged
{
    private ScadaScene scene;
    private bool isDirty;
    private PageWorkspaceEntry entry;

    public SceneWorkspaceTab(PageWorkspaceEntry entry, ScadaScene scene)
    {
        this.entry = entry ?? throw new ArgumentNullException(nameof(entry));
        this.scene = scene ?? throw new ArgumentNullException(nameof(scene));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public PageWorkspaceEntry Entry => entry;
    public ScadaSceneReference Page => entry.Page;
    public Guid PageKey => entry.PageKey;
    public string SceneId => entry.PageCode;
    public string Title => string.IsNullOrWhiteSpace(entry.Title) ? SceneId : entry.Title;
    public string Header => IsDirty ? $"{SceneId}*" : SceneId;

    public ScadaScene Scene
    {
        get => scene;
        set
        {
            if (ReferenceEquals(scene, value)) return;
            scene = value;
            OnPropertyChanged(nameof(Scene));
        }
    }

    public bool IsDirty
    {
        get => isDirty;
        set
        {
            if (isDirty == value) return;
            isDirty = value;
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(Header));
        }
    }

    public EditorHistoryService History { get; } = new();
    public IReadOnlyList<string> SelectedModernElementIds { get; set; } = Array.Empty<string>();
    public string? PrimaryModernElementId { get; set; }

    public void UpdatePage(ScadaSceneReference page)
    {
        entry = new PageWorkspaceEntry(page);
        OnPropertyChanged(nameof(Entry));
        OnPropertyChanged(nameof(Page));
        OnPropertyChanged(nameof(SceneId));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Header));
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
