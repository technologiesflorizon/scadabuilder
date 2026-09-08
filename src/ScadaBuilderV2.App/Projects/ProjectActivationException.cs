namespace ScadaBuilderV2.App.Projects;

/// <summary>Raised when a validated project could not be wired into the editor session.</summary>
/// <remarks>
/// The candidate was already validated by the repository, so this never means the project is malformed. It
/// means the shell could not finish building the session around it, and D8 requires that the editor then sit
/// in a defined state rather than a half-built one: the caller has already fallen back to the welcome screen
/// by the time this reaches it. The type exists so that fallback is distinguishable from an arbitrary crash.
///
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-07-29-project-lifecycle-design.md D8, D10.
/// Tests: tests/ScadaBuilderV2.Tests/ProjectLifecycleShellContractTests.cs.
/// </remarks>
public sealed class ProjectActivationException(string projectFilePath, Exception innerException)
    : Exception($"Activation impossible pour « {projectFilePath} ».", innerException)
{
    /// <summary>Gets the manifest path of the project whose activation failed.</summary>
    public string ProjectFilePath { get; } = projectFilePath;

    /// <summary>Gets the underlying message, without the wrapper's own framing.</summary>
    public string InnerMessage => InnerException?.Message ?? Message;
}
