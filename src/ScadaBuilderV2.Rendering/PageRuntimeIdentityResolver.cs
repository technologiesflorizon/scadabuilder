using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Rendering;

/// <summary>Runtime-only project and scene snapshot whose page relationships use human page codes.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md,
/// docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
/// </remarks>
public sealed record PageRuntimeIdentityProjection(
    ScadaProject Project,
    IReadOnlyList<ScadaScene> Scenes);

/// <summary>Resolves stable internal page keys to the human page codes required by the .sb2 contract.</summary>
/// <remarks>
/// The resolver never generates an identity. Fully migrated projects must provide one unique,
/// non-empty <see cref="ScadaSceneReference.PageKey"/> for every page. Projects containing no
/// page keys remain supported as a transitional legacy-id input.
/// </remarks>
public sealed class PageRuntimeIdentityResolver
{
    private readonly IReadOnlyDictionary<Guid, ScadaSceneReference> _pagesByKey;
    private readonly IReadOnlyDictionary<string, ScadaSceneReference> _pagesByCode;

    /// <summary>Creates and validates a resolver for one coherent project snapshot.</summary>
    public PageRuntimeIdentityResolver(ScadaProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var keyedPages = project.Scenes.Where(page => page.PageKey != Guid.Empty).ToArray();
        if (keyedPages.Length > 0 && keyedPages.Length != project.Scenes.Count)
        {
            throw new InvalidOperationException("Every page must have a PageKey before exporting a migrated project.");
        }

        var duplicateKey = keyedPages
            .GroupBy(page => page.PageKey)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateKey is not null)
        {
            throw new InvalidOperationException($"Duplicate PageKey '{duplicateKey.Key}' cannot be exported.");
        }

        var duplicateCode = project.Scenes
            .GroupBy(page => page.EffectivePageCode, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCode is not null)
        {
            throw new InvalidOperationException($"Duplicate PageCode '{duplicateCode.Key}' cannot be exported.");
        }

        _pagesByKey = keyedPages.ToDictionary(page => page.PageKey);
        _pagesByCode = project.Scenes.ToDictionary(
            page => page.EffectivePageCode,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Resolves one required stable page key to its human runtime code.</summary>
    public string ResolveCode(Guid pageKey)
    {
        if (pageKey == Guid.Empty || !_pagesByKey.TryGetValue(pageKey, out var page))
        {
            throw new InvalidOperationException($"PageKey '{pageKey}' does not resolve to a project page.");
        }

        return page.EffectivePageCode;
    }

    /// <summary>Builds a detached runtime projection without persisting or exposing page keys.</summary>
    public static PageRuntimeIdentityProjection Project(
        ScadaProject project,
        IReadOnlyList<ScadaScene> scenes)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(scenes);

        var resolver = new PageRuntimeIdentityResolver(project);
        var runtimeReferences = project.Scenes
            .Select(reference => reference with
            {
                Id = reference.EffectivePageCode,
                PageCode = reference.EffectivePageCode,
                HeaderPageId = resolver.ResolveOptionalCode(reference.HeaderPageKey, reference.HeaderPageId),
                FooterPageId = resolver.ResolveOptionalCode(reference.FooterPageKey, reference.FooterPageId),
                PageKey = Guid.Empty,
                HeaderPageKey = null,
                FooterPageKey = null
            })
            .ToArray();
        var runtimeProject = project with
        {
            Scenes = runtimeReferences,
            HomePageId = resolver.ResolveOptionalCode(project.HomePageKey, project.HomePageId),
            HomePageKey = null
        };
        var runtimeScenes = scenes.Select(scene => resolver.ProjectScene(scene)).ToArray();
        return new PageRuntimeIdentityProjection(runtimeProject, runtimeScenes);
    }

    private ScadaScene ProjectScene(ScadaScene scene)
    {
        var reference = scene.PageKey != Guid.Empty && _pagesByKey.TryGetValue(scene.PageKey, out var keyedReference)
            ? keyedReference
            : _pagesByCode.GetValueOrDefault(scene.EffectivePageCode);
        if (reference is null)
        {
            throw new InvalidOperationException($"Scene '{scene.EffectivePageCode}' does not resolve to a project page.");
        }

        return scene with
        {
            Id = reference.EffectivePageCode,
            PageCode = reference.EffectivePageCode,
            PageKey = Guid.Empty,
            HeaderPageId = ResolveOptionalCode(scene.HeaderPageKey ?? reference.HeaderPageKey, scene.HeaderPageId ?? reference.HeaderPageId),
            FooterPageId = ResolveOptionalCode(scene.FooterPageKey ?? reference.FooterPageKey, scene.FooterPageId ?? reference.FooterPageId),
            HeaderPageKey = null,
            FooterPageKey = null,
            Actions = scene.ActionDefinitions.Select(ProjectAction).ToArray(),
            Elements = scene.Elements.Select(ProjectElement).ToArray()
        };
    }

    private ScadaActionDefinition ProjectAction(ScadaActionDefinition action)
    {
        return action with
        {
            TargetPageId = ResolveOptionalCode(action.TargetPageKey, action.TargetPageId),
            TargetPageKey = null
        };
    }

    private ScadaElement ProjectElement(ScadaElement element)
    {
        var commandConfig = element.CommandConfig is null
            ? null
            : new ScadaElementCommandConfig(element.CommandConfig.Commands
                .Select(command => command with
                {
                    TargetPageId = ResolveOptionalCode(command.TargetPageKey, command.TargetPageId),
                    TargetPageKey = null
                })
                .ToArray());
        return element with
        {
            Children = element.Children is null
                ? null
                : element.ChildElements.Select(ProjectElement).ToArray(),
            CommandConfig = commandConfig
        };
    }

    private string? ResolveOptionalCode(Guid? pageKey, string? legacyPageId)
    {
        if (pageKey is { } key && key != Guid.Empty)
        {
            return ResolveCode(key);
        }

        if (string.IsNullOrWhiteSpace(legacyPageId))
        {
            return null;
        }

        return _pagesByCode.TryGetValue(legacyPageId, out var page)
            ? page.EffectivePageCode
            : legacyPageId.Trim();
    }
}
