using System.IO;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Rendering;
using ScadaBuilderV2.Rendering.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>One materialized editor-only preview of a quick-window instance.</summary>
/// <param name="DefinitionKey">Definition being previewed.</param>
/// <param name="InvocationKey">Invocation whose bindings are previewed.</param>
/// <param name="Generation">Monotonic generation of this preview request.</param>
/// <param name="RuntimeInstanceId">Temporary runtime instance id of the previewed context.</param>
/// <param name="Source">Local URI of the materialized preview document.</param>
public sealed record QuickWindowPreviewSession(
    Guid DefinitionKey,
    Guid InvocationKey,
    long Generation,
    Guid RuntimeInstanceId,
    Uri Source);

/// <summary>
/// Builder-side host adapter of the quick-window preview: it materializes the instance document and
/// hands the shared host manager one monotonic generation per request.
/// </summary>
/// <remarks>
/// The adapter owns no lifecycle policy. `SinglePerDefinition`, focus versus recreate, stale-hydration
/// rejection and cascade dispose belong to <c>ScadaRuntime.QuickWindowHost</c>, the single implementation
/// adapted from the frozen Phase 0 prototype; duplicating them here would create a second semantics.
/// Every artifact produced by this adapter lives under the editor preview root and is never exported.
///
/// Decisions: DEC-0050, FR-021, FR-022, FR-UI-21.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.3.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPreviewTests.cs.
/// </remarks>
public sealed class BuilderQuickWindowHostAdapter(string previewRootPath)
{
    /// <summary>Preview sub-directory hosting every materialized quick-window instance.</summary>
    public const string PreviewDirectoryName = "quick-windows";

    private readonly string previewRoot = string.IsNullOrWhiteSpace(previewRootPath)
        ? throw new ArgumentException("A preview root path is required.", nameof(previewRootPath))
        : previewRootPath;

    private long generation;

    /// <summary>Gets the last generation handed to the host manager.</summary>
    public long Generation => generation;

    /// <summary>Gets the currently previewed instance, or null.</summary>
    public QuickWindowPreviewSession? Current { get; private set; }

    /// <summary>
    /// Materializes and opens one instance preview. Every request receives a strictly monotonic
    /// generation so the host manager can reject a stale hydration deterministically.
    /// </summary>
    public async Task<QuickWindowPreviewSession> OpenAsync(
        QuickWindowDefinition definition,
        Guid invocationKey,
        QuickWindowTestBenchValues? testBench = null,
        ScadaTagCatalog? tagCatalog = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var nextGeneration = ++generation;
        var runtimeInstanceId = Guid.NewGuid();
        var root = Path.Combine(previewRoot, PreviewDirectoryName);
        var document = await PreviewDocument.MaterializeQuickWindowAsync(
            new QuickWindowPreviewInput(definition, invocationKey, testBench, tagCatalog),
            root,
            nextGeneration,
            runtimeInstanceId,
            cancellationToken);
        var session = new QuickWindowPreviewSession(
            definition.DefinitionKey,
            invocationKey,
            nextGeneration,
            runtimeInstanceId,
            document.GetSourceUri(root));
        Current = session;
        return session;
    }

    /// <summary>Closes the previewed instance on the editor side; the host manager disposes its context.</summary>
    public void Close() => Current = null;
}
