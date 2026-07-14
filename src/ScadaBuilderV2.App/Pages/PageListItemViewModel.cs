using System.ComponentModel;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App.Pages;

/// <summary>Presentation model for one modern project page; the stable key is routing-only.</summary>
public sealed class PageListItemViewModel : INotifyPropertyChanged
{
    private ScadaSceneReference page;
    private bool isSelected;
    private int errorCount;
    private int warningCount;

    public PageListItemViewModel(ScadaSceneReference page) => this.page = page;

    public event PropertyChangedEventHandler? PropertyChanged;
    public Guid PageKey => page.PageKey;
    public string PageCode => page.EffectivePageCode;
    public string Title => page.Title;
    public string TypeLabel => page.Type.ToString();
    public bool IncludeInBuild => page.IncludeInBuild;
    public bool IsHome { get; private set; }
    public string BuildLabel => IncludeInBuild ? "Build" : "Hors build";
    public string DiagnosticLabel => errorCount > 0 ? $"{errorCount} erreur(s)" : warningCount > 0 ? $"{warningCount} avertissement(s)" : "Valide";
    public string SearchText => $"{PageCode} {Title} {TypeLabel}";

    public bool IsSelected
    {
        get => isSelected;
        set { if (isSelected == value) return; isSelected = value; OnChanged(nameof(IsSelected)); }
    }

    public void Update(ScadaSceneReference updated, bool home, int errors = 0, int warnings = 0)
    {
        page = updated;
        IsHome = home;
        errorCount = errors;
        warningCount = warnings;
        foreach (var name in new[] { nameof(PageCode), nameof(Title), nameof(TypeLabel), nameof(IncludeInBuild), nameof(IsHome), nameof(BuildLabel), nameof(DiagnosticLabel), nameof(SearchText) }) OnChanged(name);
    }

    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
