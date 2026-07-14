using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Application.Pages;

/// <summary>Describes the durable relationship that makes one page depend on another page.</summary>
public enum PageDependencyKind
{
    Home,
    Header,
    Footer,
    ActionNavigate,
    ActionPopup,
    CommandNavigate,
    CommandPopup,
    OpenWorkspaceTab
}

/// <summary>One resolved or unresolved page relationship with enough location data for diagnostics and editing.</summary>
public sealed record PageDependency(
    PageDependencyKind Kind,
    Guid? SourcePageKey,
    string? SourcePageCode,
    Guid? TargetPageKey,
    string? TargetPageCode,
    string PropertyPath,
    string? ElementId = null,
    string? CommandId = null,
    string? ActionId = null,
    bool IsResolved = true)
{
    /// <summary>Gets whether this relationship must be resolved before deleting its target page.</summary>
    public bool BlocksDeletion => Kind != PageDependencyKind.OpenWorkspaceTab;
}

/// <summary>Complete dependency graph and diagnostics computed from one coherent workspace snapshot.</summary>
public sealed record PageDependencyAnalysis(
    IReadOnlyList<PageDependency> Dependencies,
    IReadOnlyList<ScadaBuildValidationIssue> Diagnostics)
{
    /// <summary>Returns every relationship that currently targets the supplied stable page key.</summary>
    public IReadOnlyList<PageDependency> GetInbound(Guid pageKey) =>
        Dependencies.Where(dependency => dependency.TargetPageKey == pageKey).ToArray();
}

/// <summary>Workspace state that can override durable scenes and contribute editor-only page dependencies.</summary>
public sealed record PageWorkspaceReadContext(
    IReadOnlyDictionary<Guid, ScadaBuilderV2.Domain.Scenes.ScadaScene>? OpenOrDirtyScenes = null,
    IReadOnlyCollection<Guid>? OpenPageKeys = null,
    ScadaProject? ProjectOverride = null,
    IReadOnlyList<PendingPageDeletion>? PendingDeletions = null,
    long Version = 1);
