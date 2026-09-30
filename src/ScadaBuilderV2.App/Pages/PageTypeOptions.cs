using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App.Pages;

/// <summary>One page type as offered by the page-type selector, carrying the enum value it maps to.</summary>
/// <remarks>
/// The selector binds by value (<see cref="Type"/>), never by list position: removing an item from
/// <see cref="PageTypeOptions.Selectable"/> must never shift what another item means.
/// Decisions: DEC-0052.
/// </remarks>
public sealed record PageTypeOption(ScadaPageType Type, string Label);

/// <summary>
/// Source of truth for the page-type selector: which types can be picked and what each one is called.
/// </summary>
/// <remarks>
/// <see cref="ScadaPageType.Fragment"/> is retired from selection (DEC-0052): a page still carrying that
/// legacy type shows the selector empty until the user picks a new type, which converts it like any other
/// type change. WPF-free so it can be linked into the test project.
/// Decisions: DEC-0052.
/// Tests: tests/ScadaBuilderV2.Tests/PageTypeOptionsTests.cs.
/// </remarks>
public static class PageTypeOptions
{
    /// <summary>The page types the user can pick from the selector, in display order.</summary>
    public static IReadOnlyList<PageTypeOption> Selectable { get; } =
    [
        new PageTypeOption(ScadaPageType.Default, "Défaut"),
        new PageTypeOption(ScadaPageType.QuickWindow, "Fenêtre rapide"),
        new PageTypeOption(ScadaPageType.Header, "En-tête"),
        new PageTypeOption(ScadaPageType.Footer, "Pied de page")
    ];

    /// <summary>Returns the display label for any page type, including the retired <c>Fragment</c> one.</summary>
    public static string Label(ScadaPageType type) => type switch
    {
        ScadaPageType.QuickWindow => "Fenêtre rapide",
        ScadaPageType.Header => "En-tête",
        ScadaPageType.Footer => "Pied de page",
        ScadaPageType.Fragment => "Fragment (obsolète)",
        _ => "Défaut"
    };
}
