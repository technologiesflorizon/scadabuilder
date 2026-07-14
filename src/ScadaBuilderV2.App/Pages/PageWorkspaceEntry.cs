using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App.Pages;

/// <summary>Modern page inventory entry used by the project tree and editor tabs.</summary>
public sealed record PageWorkspaceEntry(ScadaSceneReference Page)
{
    public Guid PageKey => Page.PageKey;
    public string PageCode => Page.EffectivePageCode;
    public string Title => Page.Title;
    public bool IsImported => Page.EffectiveOrigin == PageOrigin.Imported;
    public ImportProvenance? ImportProvenance => Page.ImportProvenance;
}

/// <summary>Import inventory data converted to a modern page reference outside MainWindow.</summary>
public sealed record ImportedPageDescriptor(string PageCode, string Title, string? SourcePath);
