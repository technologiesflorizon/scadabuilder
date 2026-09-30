using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App.Pages;

/// <summary>Searchable and filterable modern page inventory preserving durable project order.</summary>
public sealed class PagesPanelViewModel : INotifyPropertyChanged
{
    private string searchText = string.Empty;
    private string typeFilter = "Default";
    private string buildFilter = "Tous";
    private PageListItemViewModel? selectedPage;

    public PagesPanelViewModel()
    {
        View = CollectionViewSource.GetDefaultView(Items);
        View.Filter = Filter;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<PageListItemViewModel> Items { get; } = [];
    public ICollectionView View { get; }
    public IReadOnlyList<string> TypeFilters { get; } = ["Tous", "Default", "Fenêtre rapide", "Header", "Footer"];
    public IReadOnlyList<string> BuildFilters { get; } = ["Tous", "Inclus", "Exclus"];

    public string SearchText
    {
        get => searchText;
        set { if (searchText == value) return; searchText = value ?? string.Empty; Changed(nameof(SearchText)); View.Refresh(); }
    }

    public string TypeFilter
    {
        get => typeFilter;
        set { if (typeFilter == value) return; typeFilter = value ?? "Tous"; Changed(nameof(TypeFilter)); View.Refresh(); }
    }

    public string BuildFilter
    {
        get => buildFilter;
        set { if (buildFilter == value) return; buildFilter = value ?? "Tous"; Changed(nameof(BuildFilter)); View.Refresh(); }
    }

    public PageListItemViewModel? SelectedPage
    {
        get => selectedPage;
        set
        {
            if (ReferenceEquals(selectedPage, value)) return;
            if (selectedPage is not null) selectedPage.IsSelected = false;
            selectedPage = value;
            if (selectedPage is not null) selectedPage.IsSelected = true;
            Changed(nameof(SelectedPage));
        }
    }

    public void Load(ScadaProject project, IReadOnlyList<ScadaBuildValidationIssue>? diagnostics = null)
    {
        var selectedKey = SelectedPage?.PageKey;
        var issues = diagnostics ?? Array.Empty<ScadaBuildValidationIssue>();
        Items.Clear();
        foreach (var page in project.Scenes)
        {
            var item = new PageListItemViewModel(page);
            var pageIssues = issues.Where(issue => issue.PageKey == page.PageKey || string.Equals(issue.PageId, page.EffectivePageCode, StringComparison.OrdinalIgnoreCase)).ToArray();
            item.Update(page, project.EffectiveHomePageKey == page.PageKey,
                pageIssues.Count(issue => issue.Severity == ScadaBuildValidationSeverity.Error),
                pageIssues.Count(issue => issue.Severity == ScadaBuildValidationSeverity.Warning));
            Items.Add(item);
        }
        SelectedPage = Items.FirstOrDefault(item => item.PageKey == selectedKey) ?? Items.FirstOrDefault();
        View.Refresh();
    }

    private bool Filter(object candidate)
    {
        if (candidate is not PageListItemViewModel item) return false;
        if (searchText.Length > 0 && !item.SearchText.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)) return false;
        if (typeFilter != "Tous" && !string.Equals(TypeFilterLabel(item.TypeLabel), typeFilter, StringComparison.Ordinal)) return false;
        return buildFilter switch { "Inclus" => item.IncludeInBuild, "Exclus" => !item.IncludeInBuild, _ => true };
    }

    /// <summary>
    /// Maps one page's raw <see cref="TypeLabel"/> (the enum name) to the value it must match in
    /// <see cref="TypeFilters"/>. A legacy Fragment page (retired from the page-type selector, DEC-0052)
    /// never matches a specific filter here and stays findable only under "Tous".
    /// </summary>
    private static string TypeFilterLabel(string typeLabel) =>
        Enum.TryParse<ScadaPageType>(typeLabel, out var type) && type == ScadaPageType.QuickWindow
            ? "Fenêtre rapide"
            : typeLabel;

    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
