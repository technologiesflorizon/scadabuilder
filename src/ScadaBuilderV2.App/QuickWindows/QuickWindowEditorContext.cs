namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// The authoring surface currently shown by the shared editor: one page, or one quick-window definition.
/// </summary>
/// <remarks>
/// Quick windows reuse the page editor and its canvas, but they are not pages: navigation, route,
/// header/footer composition, home, build inclusion and legacy import are page responsibilities and are
/// hidden rather than left active and ambiguous. The context also carries the badge and title that make
/// the active surface unambiguous in the shell.
///
/// Decisions: DEC-0050, FR-001, FR-019, FR-UI-12, FR-UI-13, FR-UI-14.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.1.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowShellContractTests.cs.
/// </remarks>
public sealed class QuickWindowEditorContext
{
    /// <summary>Command id prefixes that only ever apply to a page.</summary>
    private static readonly string[] PageOnlyPrefixes = ["page.", "import."];

    /// <summary>Command ids that only ever apply to a page even though they carry another prefix.</summary>
    private static readonly HashSet<string> PageOnlyCommands = new(StringComparer.Ordinal)
    {
        "view.mobile",
        "view.tablet",
        "view.desktop"
    };

    private QuickWindowEditorContext(
        Guid? pageKey,
        Guid? quickWindowDefinitionKey,
        string code,
        string displayName)
    {
        PageKey = pageKey;
        QuickWindowDefinitionKey = quickWindowDefinitionKey;
        Code = code;
        DisplayName = displayName;
    }

    /// <summary>Gets the edited page key when the active surface is a page.</summary>
    public Guid? PageKey { get; }

    /// <summary>Gets the edited definition key when the active surface is a quick window.</summary>
    public Guid? QuickWindowDefinitionKey { get; }

    /// <summary>Gets the portable code of the active surface.</summary>
    public string Code { get; }

    /// <summary>Gets the display name of the active surface.</summary>
    public string DisplayName { get; }

    /// <summary>Gets whether the active surface is a page.</summary>
    public bool IsPage => PageKey is not null;

    /// <summary>Gets whether the active surface is a quick-window definition.</summary>
    public bool IsQuickWindow => QuickWindowDefinitionKey is not null;

    /// <summary>Gets the short badge shown next to the editor title.</summary>
    public string ContextBadge => IsQuickWindow ? "Fenêtre rapide" : "Page";

    /// <summary>Gets the unambiguous editor title for the active surface.</summary>
    public string ContextTitle => string.IsNullOrWhiteSpace(DisplayName)
        ? $"{ContextBadge} · {Code}"
        : $"{ContextBadge} · {DisplayName}";

    /// <summary>Creates the context for one edited page.</summary>
    public static QuickWindowEditorContext ForPage(Guid pageKey, string code, string? displayName = null)
    {
        if (pageKey == Guid.Empty)
            throw new ArgumentException("PageKey must be a non-empty GUID.", nameof(pageKey));
        return new QuickWindowEditorContext(pageKey, null, code ?? string.Empty, displayName ?? code ?? string.Empty);
    }

    /// <summary>Creates the context for one edited quick-window definition.</summary>
    public static QuickWindowEditorContext ForQuickWindow(Guid definitionKey, string code, string? displayName = null)
    {
        if (definitionKey == Guid.Empty)
            throw new ArgumentException("DefinitionKey must be a non-empty GUID.", nameof(definitionKey));
        return new QuickWindowEditorContext(null, definitionKey, code ?? string.Empty, displayName ?? code ?? string.Empty);
    }

    /// <summary>
    /// Returns whether one ribbon, menu or panel command applies to the active surface.
    /// Page-only commands are hidden on a quick window instead of being shown disabled.
    /// </summary>
    public bool IsCommandVisible(string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId)) return false;
        if (!IsQuickWindow) return true;
        if (PageOnlyCommands.Contains(commandId)) return false;
        return !PageOnlyPrefixes.Any(prefix => commandId.StartsWith(prefix, StringComparison.Ordinal));
    }
}
