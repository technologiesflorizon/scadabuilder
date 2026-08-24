using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Clipboard;

/// <summary>
/// The authoring context a clipboard payload was copied from: a page scene, or the canvas of one
/// quick-window definition.
/// </summary>
/// <remarks>
/// The origin is what makes a boundary crossing detectable: copying from a page and pasting into a quick
/// window, or the reverse, cannot carry its references over.
///
/// Decisions: DEC-0050, FR-031, FR-034.
/// Tests: tests/ScadaBuilderV2.Tests/SceneClipboardTests.cs.
/// </remarks>
/// <param name="IsQuickWindow">Whether the payload was copied from a quick-window canvas.</param>
/// <param name="ContextKey">Page key or quick-window definition key of the origin.</param>
public sealed record SceneClipboardOrigin(bool IsQuickWindow, Guid? ContextKey = null)
{
    /// <summary>Gets the origin of a payload copied from a page scene.</summary>
    public static SceneClipboardOrigin ForPage(Guid? pageKey = null) => new(false, pageKey);

    /// <summary>Gets the origin of a payload copied from one quick-window canvas.</summary>
    public static SceneClipboardOrigin ForQuickWindow(Guid definitionKey) => new(true, definitionKey);
}

/// <summary>Single editor clipboard shared by pages and quick windows.</summary>
/// <remarks>
/// Quick windows never create a second clipboard: they extend this one with the origin context needed by
/// the fail-closed boundary validation (`FR-036`).
///
/// Decisions: DEC-0050, FR-031, FR-036.
/// Tests: tests/ScadaBuilderV2.Tests/SceneClipboardTests.cs.
/// </remarks>
public sealed class SceneClipboard
{
    /// <summary>Gets the copied objects, or null before the first copy.</summary>
    public IReadOnlyList<ScadaElement>? Content { get; private set; }

    /// <summary>Gets the context the current payload was copied from, when it was supplied.</summary>
    public SceneClipboardOrigin? Origin { get; private set; }

    /// <summary>Gets whether the clipboard carries at least one object.</summary>
    public bool HasContent => Content is { Count: > 0 };

    /// <summary>Replaces the clipboard payload and records the context it came from.</summary>
    public void Copy(IReadOnlyList<ScadaElement> elements, SceneClipboardOrigin? origin = null)
    {
        Content = elements;
        Origin = origin;
    }

    /// <summary>
    /// Returns whether pasting the current payload into the given context crosses the page ↔ quick-window
    /// boundary, or moves between two different quick-window definitions.
    /// </summary>
    public bool CrossesQuickWindowBoundary(bool targetIsQuickWindow, Guid? targetContextKey)
    {
        if (Origin is not { } origin) return targetIsQuickWindow;
        if (origin.IsQuickWindow != targetIsQuickWindow) return true;
        return origin.IsQuickWindow && origin.ContextKey != targetContextKey;
    }
}
