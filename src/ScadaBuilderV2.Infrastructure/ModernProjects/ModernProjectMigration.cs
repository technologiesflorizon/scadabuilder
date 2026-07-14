using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

/// <summary>Migrates legacy page ids to the modern key/code/provenance model.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/PageIdentityTests.cs, tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs.
/// </remarks>
public static class ModernProjectMigration
{
    /// <summary>Returns an idempotently migrated project snapshot.</summary>
    public static ScadaProject MigrateProject(
        ScadaProject project,
        IReadOnlyList<ScadaSceneReference>? importedInventory = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var importedByCode = (importedInventory ?? Array.Empty<ScadaSceneReference>())
            .Where(page => !string.IsNullOrWhiteSpace(page.EffectivePageCode))
            .GroupBy(page => page.EffectivePageCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var normalized = project.Scenes
            .Select(page => NormalizeIdentity(project.Name, page, importedByCode))
            .ToArray();
        var byCode = normalized
            .GroupBy(page => page.EffectivePageCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        normalized = normalized
            .Select(page => page with
            {
                HeaderPageKey = ResolveTargetKey(page.HeaderPageKey, page.HeaderPageId, byCode),
                FooterPageKey = ResolveTargetKey(page.FooterPageKey, page.FooterPageId, byCode)
            })
            .ToArray();

        var homePageKey = ResolveTargetKey(project.HomePageKey, project.HomePageId, byCode);
        return project with
        {
            Scenes = normalized,
            HomePageKey = homePageKey,
            HomePageId = ResolveCode(homePageKey, project.HomePageId, normalized)
        };
    }

    /// <summary>Returns a scene projected onto the migrated project identity model.</summary>
    public static ScadaScene MigrateScene(ScadaScene scene, ScadaProject project)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(project);
        var migratedProject = MigrateProject(project);
        var byCode = migratedProject.Scenes
            .GroupBy(page => page.EffectivePageCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var reference = migratedProject.Scenes.FirstOrDefault(page =>
                scene.PageKey != Guid.Empty && page.PageKey == scene.PageKey)
            ?? migratedProject.Scenes.FirstOrDefault(page =>
                string.Equals(page.EffectivePageCode, scene.EffectivePageCode, StringComparison.OrdinalIgnoreCase));

        var actions = scene.ActionDefinitions
            .Select(action => action with
            {
                TargetPageKey = ResolveTargetKey(action.TargetPageKey, action.TargetPageId, byCode)
            })
            .ToArray();
        var elements = scene.Elements.Select(element => MigrateElement(element, byCode)).ToArray();

        return scene with
        {
            Id = reference?.EffectivePageCode ?? scene.EffectivePageCode,
            PageKey = reference?.PageKey ?? (scene.PageKey == Guid.Empty
                ? PageKeyFactory.CreateDeterministic(project.Name, scene.EffectivePageCode)
                : scene.PageKey),
            PageCode = reference?.EffectivePageCode ?? scene.EffectivePageCode,
            Origin = reference?.EffectiveOrigin ?? scene.EffectiveOrigin,
            ImportProvenance = reference?.ImportProvenance ?? scene.ImportProvenance,
            HeaderPageKey = ResolveTargetKey(scene.HeaderPageKey ?? reference?.HeaderPageKey, scene.HeaderPageId, byCode),
            FooterPageKey = ResolveTargetKey(scene.FooterPageKey ?? reference?.FooterPageKey, scene.FooterPageId, byCode),
            Actions = actions,
            Elements = elements
        };
    }

    private static ScadaSceneReference NormalizeIdentity(
        string projectName,
        ScadaSceneReference page,
        IReadOnlyDictionary<string, ScadaSceneReference> importedByCode)
    {
        var pageCode = page.EffectivePageCode.Trim();
        importedByCode.TryGetValue(pageCode, out var imported);
        var provenance = page.ImportProvenance;
        var origin = page.Origin;
        if (origin is null && provenance is null && imported is not null)
        {
            provenance = imported.ImportProvenance ?? new ImportProvenance(
                "Wonderware",
                projectName,
                imported.EffectivePageCode);
            origin = PageOrigin.Imported;
        }

        origin ??= provenance is null ? PageOrigin.Native : PageOrigin.Imported;
        return page with
        {
            Id = pageCode,
            PageCode = pageCode,
            PageKey = page.PageKey == Guid.Empty
                ? PageKeyFactory.CreateDeterministic(projectName, pageCode)
                : page.PageKey,
            Origin = origin,
            ImportProvenance = provenance
        };
    }

    private static ScadaElement MigrateElement(
        ScadaElement element,
        IReadOnlyDictionary<string, ScadaSceneReference> byCode)
    {
        var commandConfig = element.CommandConfig is null
            ? null
            : new ScadaElementCommandConfig(element.CommandConfig.Commands
                .Select(command => command with
                {
                    TargetPageKey = ResolveTargetKey(command.TargetPageKey, command.TargetPageId, byCode)
                })
                .ToArray());
        return element with
        {
            Children = element.Children is null
                ? null
                : element.ChildElements.Select(child => MigrateElement(child, byCode)).ToArray(),
            CommandConfig = commandConfig
        };
    }

    private static Guid? ResolveTargetKey(
        Guid? currentKey,
        string? legacyCode,
        IReadOnlyDictionary<string, ScadaSceneReference> byCode)
    {
        if (currentKey is { } key && key != Guid.Empty)
        {
            return key;
        }

        return !string.IsNullOrWhiteSpace(legacyCode) && byCode.TryGetValue(legacyCode, out var page)
            ? page.PageKey
            : null;
    }

    private static string? ResolveCode(
        Guid? key,
        string? legacyCode,
        IReadOnlyList<ScadaSceneReference> pages)
    {
        if (key is { } value && value != Guid.Empty)
        {
            return pages.FirstOrDefault(page => page.PageKey == value)?.EffectivePageCode ?? legacyCode;
        }

        return legacyCode;
    }
}
