using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Application.Projects;

/// <summary>Requests creation of one native V2 project workspace.</summary>
public sealed record CreateProjectRequest(
    string Name,
    string ParentDirectory,
    string ProjectDirectoryName,
    string InitialPageCode,
    string InitialPageTitle,
    CanvasSize CanvasSize,
    ResponsiveMode ResponsiveMode,
    AuthoringMode AuthoringMode);

/// <summary>A fully validated project that can replace the active editor session.</summary>
public sealed record ProjectLoadCandidate(
    ProjectWorkspaceLocation Location,
    PageWorkspaceSnapshot Snapshot,
    bool WasMigrated,
    IReadOnlyList<ScadaBuildValidationIssue> Diagnostics);

/// <summary>One project remembered in the per-user recent-project registry.</summary>
public sealed record RecentProjectEntry(
    string DisplayName,
    string ProjectFilePath,
    DateTimeOffset LastOpenedUtc,
    bool IsAvailable = true);

/// <summary>Outcome of a project repository operation.</summary>
public sealed record ProjectRepositoryResult(
    ProjectLoadCandidate? Candidate,
    IReadOnlyList<ScadaBuildValidationIssue> Diagnostics)
{
    /// <summary>Gets whether the operation produced an activatable project.</summary>
    public bool IsSuccess => Candidate is not null && !HasBlockingError;

    /// <summary>Gets whether the operation stopped on something the operator has to be told about.</summary>
    /// <remarks>
    /// A close produces no candidate, so `IsSuccess` cannot describe it. This separates the outcomes that
    /// need an error dialog from a cancellation, which the operator already knows they chose.
    /// </remarks>
    public bool HasBlockingError => Diagnostics.Any(issue => issue.Severity == ScadaBuildValidationSeverity.Error);
}

/// <summary>Choice made when a project with unsaved changes is about to be replaced or closed.</summary>
public enum ProjectCloseDecision
{
    Save,
    Discard,
    Cancel
}
