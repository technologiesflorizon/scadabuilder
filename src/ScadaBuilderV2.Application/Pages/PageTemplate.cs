using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Pages;

/// <summary>Application-level page template contract prepared for reusable page models.</summary>
public sealed record PageTemplate(
    string Id,
    string DisplayName,
    Func<Guid, string, string, CanvasSize, ScadaScene> CreateScene)
{
    /// <summary>Canonical empty native page template.</summary>
    public static PageTemplate Blank { get; } = new(
        "blank",
        "Blank",
        (pageKey, pageCode, title, canvas) => ScadaScene.CreateEmpty(pageCode, title, canvas) with
        {
            PageKey = pageKey,
            PageCode = pageCode,
            Origin = PageOrigin.Native,
            IncludeInBuild = false
        });
}
