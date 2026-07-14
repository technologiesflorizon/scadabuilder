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
    public IReadOnlyList<string> TypeFilters { get; } = ["Tous", "Default", "Fragment", "Header", "Footer"];
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
        if (typeFilter != "Tous" && !string.Equals(item.TypeLabel, typeFilter, StringComparison.Ordinal)) return false;
        return buildFilter switch { "Inclus" => item.IncludeInBuild, "Exclus" => !item.IncludeInBuild, _ => true };
    }

    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
