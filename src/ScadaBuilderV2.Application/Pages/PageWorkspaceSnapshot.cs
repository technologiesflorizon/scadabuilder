using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Pages;

/// <summary>One project-local scene file scheduled for deletion after the project commit point.</summary>
public sealed record PendingPageDeletion(
    Guid PageKey,
    string RelativePath);

/// <summary>Coherent project and scene state persisted as one transactional workspace unit.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/ModernProjectAtomicSnapshotTests.cs.
/// </remarks>
public sealed record PageWorkspaceSnapshot(
    long Version,
    ScadaProject Project,
    IReadOnlyDictionary<Guid, ScadaScene> Scenes,
    IReadOnlyList<PendingPageDeletion> PendingDeletions);
