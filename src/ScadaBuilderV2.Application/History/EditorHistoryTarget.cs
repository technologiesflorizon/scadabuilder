namespace ScadaBuilderV2.Application.History;

/// <summary>Scope of one reversible editor history action.</summary>
public enum EditorHistoryScope
{
    /// <summary>The action mutates one page scene.</summary>
    Scene,

    /// <summary>The action mutates the project workspace and can affect multiple pages.</summary>
    Project,

    /// <summary>The action mutates the visual content of one quick-window definition.</summary>
    QuickWindow
}

/// <summary>Polymorphic target used to route scene and project history actions.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/04_editor/STATE_MANAGEMENT_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs,
/// tests/ScadaBuilderV2.Tests/ProjectWorkspaceHistoryTests.cs.
/// </remarks>
public sealed record EditorHistoryTarget(
    EditorHistoryScope Scope,
    Guid? PageKey = null,
    string? LegacySceneId = null,
    Guid? QuickWindowDefinitionKey = null)
{
    /// <summary>Creates a target for one scene, with legacy id fallback during migration.</summary>
    public static EditorHistoryTarget ForScene(string sceneId, Guid? pageKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneId);
        return new EditorHistoryTarget(
            EditorHistoryScope.Scene,
            pageKey is { } key && key != Guid.Empty ? key : null,
            sceneId);
    }

    /// <summary>Gets the singleton project-workspace target.</summary>
    public static EditorHistoryTarget Project { get; } = new(EditorHistoryScope.Project);

    /// <summary>
    /// Creates a target for the visual content of one quick-window definition.
    /// Quick windows share the single workspace stack; only their scope differs (FR-035).
    /// </summary>
    public static EditorHistoryTarget ForQuickWindow(Guid definitionKey)
    {
        if (definitionKey == Guid.Empty)
            throw new ArgumentException("DefinitionKey must be a non-empty GUID.", nameof(definitionKey));
        return new EditorHistoryTarget(EditorHistoryScope.QuickWindow, QuickWindowDefinitionKey: definitionKey);
    }

    /// <summary>Returns whether two targets refer to the same durable history scope.</summary>
    public bool RefersToSameTarget(EditorHistoryTarget other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Scope != other.Scope)
        {
            return false;
        }

        if (Scope == EditorHistoryScope.Project)
        {
            return true;
        }

        if (Scope == EditorHistoryScope.QuickWindow)
        {
            return QuickWindowDefinitionKey == other.QuickWindowDefinitionKey;
        }

        if (PageKey is { } pageKey && other.PageKey is { } otherPageKey)
        {
            return pageKey == otherPageKey;
        }

        return string.Equals(LegacySceneId, other.LegacySceneId, StringComparison.Ordinal);
    }
}
