namespace ScadaBuilderV2.Application.Projects;

/// <summary>Identifies one editable project by its exact durable root and manifest path.</summary>
/// <remarks>
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-07-29-project-lifecycle-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ProjectOpenIntegrationTests.cs.
/// </remarks>
public sealed record ProjectWorkspaceLocation(
    string ProjectRoot,
    string ProjectFilePath,
    string? ImportedSourceBaseRoot = null);
