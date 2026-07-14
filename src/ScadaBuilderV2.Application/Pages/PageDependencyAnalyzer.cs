using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Pages;

/// <summary>Builds a complete page dependency graph without depending on WPF or the legacy import inventory.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/superpowers/specs/2026-07-14-page-commands-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/PageDependencyAnalyzerTests.cs.
/// </remarks>
public sealed class PageDependencyAnalyzer
{
    /// <summary>Analyzes all pages, including pages excluded from build, from one coherent workspace snapshot.</summary>
    public PageDependencyAnalysis Analyze(
        PageWorkspaceSnapshot snapshot,
        IReadOnlyCollection<Guid>? openPageKeys = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var project = snapshot.Project;
        var pagesByKey = project.Scenes
            .Where(page => page.PageKey != Guid.Empty)
            .GroupBy(page => page.PageKey)
            .ToDictionary(group => group.Key, group => group.First());
        var pagesByCode = project.Scenes
            .Where(page => !string.IsNullOrWhiteSpace(page.EffectivePageCode))
            .GroupBy(page => page.EffectivePageCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var dependencies = new List<PageDependency>();
        var diagnostics = new List<ScadaBuildValidationIssue>();

        AddDependency(
            PageDependencyKind.Home,
            null,
            null,
            project.HomePageKey is { } configuredHomeKey && configuredHomeKey != Guid.Empty
                ? project.HomePageKey
                : project.EffectiveHomePageKey,
            !string.IsNullOrWhiteSpace(project.HomePageId)
                ? project.HomePageId
                : project.EffectiveHomePageId,
            "Project.HomePageKey");

        foreach (var page in project.Scenes)
        {
            AddDependency(PageDependencyKind.Header, page.PageKey, page.EffectivePageCode,
                page.HeaderPageKey, page.HeaderPageId, "Page.HeaderPageKey");
            AddDependency(PageDependencyKind.Footer, page.PageKey, page.EffectivePageCode,
                page.FooterPageKey, page.FooterPageId, "Page.FooterPageKey");

            if (!snapshot.Scenes.TryGetValue(page.PageKey, out var scene))
            {
                diagnostics.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "page.scene-snapshot-missing",
                    $"Page '{page.EffectivePageCode}' has no scene in the coherent workspace snapshot.",
                    page.EffectivePageCode,
                    page.PageKey,
                    PropertyPath: "Workspace.Scenes",
                    SuggestedFix: "Reload the page workspace or restore the missing scene file."));
                continue;
            }

            foreach (var action in scene.ActionDefinitions)
            {
                var kind = action.Kind switch
                {
                    ScadaActionKind.Navigate => PageDependencyKind.ActionNavigate,
                    ScadaActionKind.MountFragment or ScadaActionKind.TogglePopup or ScadaActionKind.ClosePopup => PageDependencyKind.ActionPopup,
                    _ => (PageDependencyKind?)null
                };
                if (kind is not null)
                {
                    AddDependency(kind.Value, page.PageKey, page.EffectivePageCode,
                        action.TargetPageKey, action.TargetPageId,
                        $"Scene.Actions[{action.Id}].TargetPageKey", actionId: action.Id);
                }
            }

            foreach (var element in Flatten(scene.Elements))
            {
                foreach (var command in element.EffectiveCommandConfig.Commands)
                {
                    var kind = command.Kind switch
                    {
                        ScadaCommandKind.Navigate => PageDependencyKind.CommandNavigate,
                        ScadaCommandKind.OpenPopup or ScadaCommandKind.TogglePopup or ScadaCommandKind.ClosePopup => PageDependencyKind.CommandPopup,
                        _ => (PageDependencyKind?)null
                    };
                    if (kind is not null)
                    {
                        AddDependency(kind.Value, page.PageKey, page.EffectivePageCode,
                            command.TargetPageKey, command.TargetPageId,
                            $"Scene.Elements[{element.Id}].CommandConfig.Commands[{command.Id}].TargetPageKey",
                            element.Id, command.Id);
                    }
                }
            }
        }

        foreach (var openPageKey in openPageKeys ?? Array.Empty<Guid>())
        {
            var target = pagesByKey.GetValueOrDefault(openPageKey);
            dependencies.Add(new PageDependency(
                PageDependencyKind.OpenWorkspaceTab,
                null,
                null,
                target?.PageKey ?? openPageKey,
                target?.EffectivePageCode,
                "Workspace.OpenPageKeys",
                IsResolved: target is not null));
        }

        return new PageDependencyAnalysis(dependencies, diagnostics);

        void AddDependency(
            PageDependencyKind kind,
            Guid? sourceKey,
            string? sourceCode,
            Guid? targetKey,
            string? targetCode,
            string propertyPath,
            string? elementId = null,
            string? commandId = null,
            string? actionId = null)
        {
            if ((targetKey is null || targetKey == Guid.Empty) && string.IsNullOrWhiteSpace(targetCode))
            {
                return;
            }

            ScadaSceneReference? target = null;
            if (targetKey is { } key && key != Guid.Empty)
            {
                target = pagesByKey.GetValueOrDefault(key);
            }
            else if (!string.IsNullOrWhiteSpace(targetCode))
            {
                pagesByCode.TryGetValue(targetCode, out target);
            }

            var dependency = new PageDependency(
                kind,
                sourceKey is { } source && source != Guid.Empty ? source : null,
                sourceCode,
                target?.PageKey ?? (targetKey is { } unresolved && unresolved != Guid.Empty ? unresolved : null),
                target?.EffectivePageCode ?? targetCode,
                propertyPath,
                elementId,
                commandId,
                actionId,
                target is not null);
            dependencies.Add(dependency);

            if (target is null)
            {
                var location = actionId is not null ? $"action '{actionId}'" : commandId is not null ? $"command '{commandId}'" : propertyPath;
                diagnostics.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "page.reference-missing",
                    $"Page '{sourceCode ?? "project"}' {location} references a missing page '{targetCode ?? targetKey?.ToString()}'.",
                    sourceCode,
                    sourceKey is { } validSource && validSource != Guid.Empty ? validSource : null,
                    elementId,
                    commandId ?? actionId,
                    propertyPath,
                    targetKey,
                    "Select an existing page or remove the stale reference."));
            }
        }
    }

    private static IEnumerable<ScadaElement> Flatten(IEnumerable<ScadaElement> elements)
    {
        foreach (var element in elements)
        {
            yield return element;
            foreach (var child in Flatten(element.ChildElements))
            {
                yield return child;
            }
        }
    }
}
