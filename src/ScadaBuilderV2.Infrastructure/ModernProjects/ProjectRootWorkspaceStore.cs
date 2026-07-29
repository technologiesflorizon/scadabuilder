using ScadaBuilderV2.Application.Pages;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

/// <summary>Adapts page workspace persistence to an exact project root.</summary>
/// <remarks>
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-07-29-project-lifecycle-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ProjectLifecycleIntegrationTests.cs.
/// </remarks>
public sealed class ProjectRootWorkspaceStore(ModernProjectStore store) : IPageWorkspaceStore, IPageWorkspaceReader
{
    public Task<PageWorkspaceSnapshot> ReadWorkspaceSnapshotAsync(
        string projectRoot,
        PageWorkspaceReadContext? context = null,
        CancellationToken cancellationToken = default) =>
        store.ReadWorkspaceSnapshotFromProjectRootAsync(projectRoot, context, cancellationToken);

    public Task SaveWorkspaceSnapshotAsync(
        string projectRoot,
        PageWorkspaceSnapshot snapshot,
        CancellationToken cancellationToken = default) =>
        store.SaveWorkspaceSnapshotToProjectRootAsync(projectRoot, snapshot, cancellationToken);
}
