using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Pages;

/// <summary>Centralizes every page lifecycle rule independently from WPF and persistence details.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/superpowers/specs/2026-07-14-page-commands-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/PageCommandCoordinatorTests.cs.
/// </remarks>
public sealed class PageCommandCoordinator(PageDependencyAnalyzer? dependencyAnalyzer = null)
{
    private readonly PageDependencyAnalyzer dependencyAnalyzer = dependencyAnalyzer ?? new PageDependencyAnalyzer();

    /// <summary>Prepares a complete workspace transition without mutating the supplied snapshot.</summary>
    public PageWorkspaceMutation Execute(
        PageWorkspaceSnapshot snapshot,
        ProjectWorkspaceUiSnapshot ui,
        PageCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(request);

        return request switch
        {
            NewPageRequest value => NewPage(snapshot, ui, value),
            RenamePageRequest value => Rename(snapshot, ui, value),
            ChangePageCodeRequest value => ChangeCode(snapshot, ui, value),
            DuplicatePageRequest value => Duplicate(snapshot, ui, value),
            DeletePageRequest value => Delete(snapshot, ui, value),
            OpenPageRequest value => Route(snapshot, ui, value.PageKey, open: true, "Page ouverte."),
            ShowPagePropertiesRequest value => Route(snapshot, ui, value.PageKey, open: false, "Propriétés de la page sélectionnées."),
            SetPageBuildInclusionRequest value => SetBuildInclusion(snapshot, ui, value),
            SetHomePageRequest value => SetHome(snapshot, ui, value),
            SetPageTypeRequest value => SetType(snapshot, ui, value),
            SetPageCompositionRequest value => SetComposition(snapshot, ui, value),
            SetPageCanvasRequest value => SetCanvas(snapshot, ui, value),
            SetPageBackgroundRequest value => SetBackground(snapshot, ui, value),
            ValidatePagesRequest => Validate(snapshot, ui),
            _ => NoChange(snapshot, ui, CommandResult.Blocked("Requête de page non prise en charge."), "page")
        };
    }

    private static PageWorkspaceMutation NewPage(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, NewPageRequest request)
    {
        var code = request.PageCode.Trim();
        var validation = PageCodePolicy.Validate(code, snapshot.Project.Scenes.Select(page => page.EffectivePageCode));
        if (!validation.IsValid)
        {
            return InvalidCode(snapshot, ui, code, validation);
        }

        var pageKey = Guid.NewGuid();
        var title = string.IsNullOrWhiteSpace(request.Title) ? code : request.Title.Trim();
        var active = ui.ActivePageKey is { } activeKey
            ? snapshot.Project.Scenes.FirstOrDefault(page => page.PageKey == activeKey)
            : null;
        var headerKey = active?.Type == ScadaPageType.Default ? active.HeaderPageKey : null;
        var footerKey = active?.Type == ScadaPageType.Default ? active.FooterPageKey : null;
        var headerCode = ResolveCode(snapshot.Project, headerKey);
        var footerCode = ResolveCode(snapshot.Project, footerKey);
        var template = request.Template ?? PageTemplate.Blank;
        var scene = template.CreateScene(pageKey, code, title, snapshot.Project.CanvasSize) with
        {
            Id = code,
            Title = title,
            PageKey = pageKey,
            PageCode = code,
            PageType = ScadaPageType.Default,
            IncludeInBuild = false,
            HeaderPageKey = headerKey,
            FooterPageKey = footerKey,
            HeaderPageId = headerCode,
            FooterPageId = footerCode
        };
        var page = new ScadaSceneReference(
            code,
            title,
            $"scenes/{pageKey:N}.scene.json",
            ScadaPageType.Default,
            snapshot.Project.CanvasSize,
            scene.EffectiveBackground,
            IncludeInBuild: false,
            HeaderPageId: headerCode,
            FooterPageId: footerCode,
            PageKey: pageKey,
            PageCode: code,
            Origin: PageOrigin.Native,
            HeaderPageKey: headerKey,
            FooterPageKey: footerKey);
        var pages = InsertAfter(snapshot.Project.Scenes, page, ui.SelectedPageKey);
        var after = NextSnapshot(snapshot, snapshot.Project with { Scenes = pages }, AddScene(snapshot.Scenes, scene));
        var afterUi = OpenAndSelect(ui, pageKey);
        return Changed(snapshot, after, ui, afterUi, pageKey, "Nouvelle page créée.", "créer page", open: true);
    }

    private static PageWorkspaceMutation Rename(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, RenamePageRequest request)
    {
        if (!TryFind(snapshot, request.PageKey, out var page, out var scene, out var blocked)) return NoChange(snapshot, ui, blocked!, "renommer page");
        var title = request.Title.Trim();
        if (title.Length == 0) return Block(snapshot, ui, page, "Le titre de page est requis.", "Page.Title", "Saisir un titre.");
        if (string.Equals(page.Title, title, StringComparison.Ordinal)) return NoChange(snapshot, ui, CommandResult.NoChange("Le titre est inchangé."), "renommer page");
        return ReplacePageAndScene(snapshot, ui, page with { Title = title }, scene with { Title = title }, "Page renommée.", "renommer page");
    }

    private static PageWorkspaceMutation ChangeCode(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, ChangePageCodeRequest request)
    {
        if (!TryFind(snapshot, request.PageKey, out var page, out var scene, out var blocked)) return NoChange(snapshot, ui, blocked!, "modifier code page");
        var code = request.PageCode.Trim();
        var validation = PageCodePolicy.Validate(code, snapshot.Project.Scenes.Select(item => item.EffectivePageCode), page.EffectivePageCode);
        if (!validation.IsValid) return InvalidCode(snapshot, ui, code, validation, page);
        if (string.Equals(page.EffectivePageCode, code, StringComparison.Ordinal)) return NoChange(snapshot, ui, CommandResult.NoChange("Le code est inchangé."), "modifier code page");
        var pages = snapshot.Project.Scenes.Select(item => item with
        {
            Id = item.PageKey == page.PageKey ? code : item.Id,
            PageCode = item.PageKey == page.PageKey ? code : item.PageCode,
            HeaderPageId = item.HeaderPageKey == page.PageKey ? code : item.HeaderPageId,
            FooterPageId = item.FooterPageKey == page.PageKey ? code : item.FooterPageId
        }).ToArray();
        var scenes = snapshot.Scenes.ToDictionary(
            pair => pair.Key,
            pair => RewriteCompatibilityTargetCodes(
                pair.Value with
                {
                    Id = pair.Key == page.PageKey ? code : pair.Value.Id,
                    PageCode = pair.Key == page.PageKey ? code : pair.Value.PageCode,
                    HeaderPageId = pair.Value.HeaderPageKey == page.PageKey ? code : pair.Value.HeaderPageId,
                    FooterPageId = pair.Value.FooterPageKey == page.PageKey ? code : pair.Value.FooterPageId
                },
                page.PageKey,
                code));
        var project = snapshot.Project with
        {
            Scenes = pages,
            HomePageId = snapshot.Project.HomePageKey == page.PageKey ? code : snapshot.Project.HomePageId
        };
        var after = NextSnapshot(snapshot, project, scenes);
        return Changed(snapshot, after, ui, ui with { SelectedPageKey = page.PageKey }, page.PageKey,
            "Code de page modifié; les identifiants .sb2 exportés seront mis à jour.", "modifier code page");
    }

    private static PageWorkspaceMutation Duplicate(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, DuplicatePageRequest request)
    {
        if (!TryFind(snapshot, request.PageKey, out var source, out var sourceScene, out var blocked)) return NoChange(snapshot, ui, blocked!, "dupliquer page");
        var code = request.PageCode.Trim();
        var validation = PageCodePolicy.Validate(code, snapshot.Project.Scenes.Select(page => page.EffectivePageCode));
        if (!validation.IsValid) return InvalidCode(snapshot, ui, code, validation, source);

        var pageKey = Guid.NewGuid();
        var title = string.IsNullOrWhiteSpace(request.Title) ? $"{source.Title} - copie" : request.Title.Trim();
        var actionIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var actions = sourceScene.ActionDefinitions.Select(action =>
        {
            var selfReference = IsSelfReference(action.TargetPageKey, action.TargetPageId, source);
            if (!selfReference) return action;
            var newActionId = $"{action.Id}__copy_{pageKey:N}";
            actionIds[action.Id] = newActionId;
            return action with { Id = newActionId, TargetPageKey = pageKey, TargetPageId = code };
        }).ToArray();
        var elements = sourceScene.Elements.Select(element => RewriteElement(element, source, pageKey, code, actionIds)).ToArray();
        var scene = sourceScene with
        {
            Id = code,
            Title = title,
            PageKey = pageKey,
            PageCode = code,
            Elements = elements,
            Actions = actions
        };
        var page = source with
        {
            Id = code,
            Title = title,
            RelativePath = $"scenes/{pageKey:N}.scene.json",
            PageKey = pageKey,
            PageCode = code
        };
        var pages = InsertAfter(snapshot.Project.Scenes, page, source.PageKey);
        var after = NextSnapshot(snapshot, snapshot.Project with { Scenes = pages }, AddScene(snapshot.Scenes, scene));
        var afterUi = OpenAndSelect(ui, pageKey);
        return Changed(snapshot, after, ui, afterUi, pageKey, "Page dupliquée.", "dupliquer page", open: true);
    }

    private PageWorkspaceMutation Delete(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, DeletePageRequest request)
    {
        if (!TryFind(snapshot, request.PageKey, out var page, out _, out var blocked)) return NoChange(snapshot, ui, blocked!, "supprimer page");
        var inbound = dependencyAnalyzer.Analyze(snapshot, ui.OpenPageKeys)
            .GetInbound(page.PageKey)
            .Where(item => item.BlocksDeletion)
            .ToArray();
        if (inbound.Length > 0)
        {
            var diagnostics = inbound.Select(item => new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                "page.delete-dependency",
                $"La page '{page.EffectivePageCode}' est encore référencée par {Describe(item.Kind)}.",
                item.SourcePageCode,
                item.SourcePageKey,
                item.ElementId,
                item.CommandId ?? item.ActionId,
                item.PropertyPath,
                page.PageKey,
                "Retirer ou remplacer cette référence avant de supprimer la page.")).ToArray();
            return NoChange(snapshot, ui, CommandResult.Blocked("La page ne peut pas être supprimée tant que ses dépendances existent.", diagnostics), "supprimer page");
        }

        var oldIndex = snapshot.Project.Scenes.ToList().IndexOf(page);
        var pages = snapshot.Project.Scenes.Where(item => item.PageKey != page.PageKey).ToArray();
        var scenes = snapshot.Scenes.Where(pair => pair.Key != page.PageKey).ToDictionary();
        var deletions = snapshot.PendingDeletions
            .Where(item => item.PageKey != page.PageKey)
            .Append(new PendingPageDeletion(page.PageKey, page.RelativePath))
            .ToArray();
        var project = snapshot.Project with { Scenes = pages };
        var after = new PageWorkspaceSnapshot(snapshot.Version + 1, project, scenes, deletions);
        var neighbor = pages.Length == 0 ? (Guid?)null : pages[Math.Min(oldIndex, pages.Length - 1)].PageKey;
        var openKeys = ui.OpenPageKeys.Where(key => key != page.PageKey).ToArray();
        var afterUi = ui with
        {
            OpenPageKeys = openKeys,
            SelectedPageKey = ui.SelectedPageKey == page.PageKey ? neighbor : ui.SelectedPageKey,
            ActivePageKey = ui.ActivePageKey == page.PageKey ? neighbor : ui.ActivePageKey,
            PageSelections = ui.PageSelections.Where(pair => pair.Key != page.PageKey).ToDictionary()
        };
        return Changed(snapshot, after, ui, afterUi, page.PageKey, "Page supprimée; suppression du fichier en attente d'enregistrement.", "supprimer page");
    }

    private static PageWorkspaceMutation SetBuildInclusion(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, SetPageBuildInclusionRequest request)
    {
        if (!TryFind(snapshot, request.PageKey, out var page, out var scene, out var blocked)) return NoChange(snapshot, ui, blocked!, "compiler page");
        if (!request.IncludeInBuild && snapshot.Project.EffectiveHomePageKey == page.PageKey)
            return Block(snapshot, ui, page, "La page d'accueil doit rester incluse dans le build.", "Page.IncludeInBuild", "Choisir d'abord une autre page d'accueil.");
        if (page.IncludeInBuild == request.IncludeInBuild) return NoChange(snapshot, ui, CommandResult.NoChange("L'inclusion dans le build est inchangée."), "compiler page");
        return ReplacePageAndScene(snapshot, ui, page with { IncludeInBuild = request.IncludeInBuild }, scene with { IncludeInBuild = request.IncludeInBuild }, "Inclusion dans le build modifiée.", "compiler page");
    }

    private static PageWorkspaceMutation SetHome(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, SetHomePageRequest request)
    {
        if (request.PageKey is null)
        {
            if (snapshot.Project.EffectiveHomePageKey is not { } previousKey)
                return NoChange(snapshot, ui, CommandResult.NoChange("Aucune page d'accueil n'est définie."), "retirer accueil");
            var cleared = NextSnapshot(snapshot, snapshot.Project with { HomePageKey = null, HomePageId = null }, snapshot.Scenes);
            return Changed(snapshot, cleared, ui, ui, previousKey, "Page d'accueil retirée.", "retirer accueil");
        }

        if (!TryFind(snapshot, request.PageKey.Value, out var page, out _, out var blocked)) return NoChange(snapshot, ui, blocked!, "définir accueil");
        if (page.Type != ScadaPageType.Default || !page.IncludeInBuild)
            return Block(snapshot, ui, page, "La page d'accueil doit être une page Default incluse dans le build.", "Project.HomePageKey", "Changer le type et inclure la page dans le build.");
        if (snapshot.Project.HomePageKey == page.PageKey) return NoChange(snapshot, ui, CommandResult.NoChange("Cette page est déjà la page d'accueil."), "définir accueil");
        var project = snapshot.Project with { HomePageKey = page.PageKey, HomePageId = page.EffectivePageCode };
        var after = NextSnapshot(snapshot, project, snapshot.Scenes);
        return Changed(snapshot, after, ui, ui with { SelectedPageKey = page.PageKey }, page.PageKey, "Page d'accueil modifiée.", "définir accueil");
    }

    private static PageWorkspaceMutation SetType(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, SetPageTypeRequest request)
    {
        if (!TryFind(snapshot, request.PageKey, out var page, out var scene, out var blocked)) return NoChange(snapshot, ui, blocked!, "modifier type page");
        if (snapshot.Project.EffectiveHomePageKey == page.PageKey && request.PageType != ScadaPageType.Default)
            return Block(snapshot, ui, page, "La page d'accueil doit conserver le type Default.", "Page.Type", "Choisir d'abord une autre page d'accueil.");
        if (page.Type == request.PageType) return NoChange(snapshot, ui, CommandResult.NoChange("Le type est inchangé."), "modifier type page");
        var clearComposition = request.PageType != ScadaPageType.Default;
        var updatedPage = page with
        {
            Type = request.PageType,
            HeaderPageKey = clearComposition ? null : page.HeaderPageKey,
            FooterPageKey = clearComposition ? null : page.FooterPageKey,
            HeaderPageId = clearComposition ? null : page.HeaderPageId,
            FooterPageId = clearComposition ? null : page.FooterPageId
        };
        var updatedScene = scene with
        {
            PageType = request.PageType,
            HeaderPageKey = clearComposition ? null : scene.HeaderPageKey,
            FooterPageKey = clearComposition ? null : scene.FooterPageKey,
            HeaderPageId = clearComposition ? null : scene.HeaderPageId,
            FooterPageId = clearComposition ? null : scene.FooterPageId
        };
        return ReplacePageAndScene(snapshot, ui, updatedPage, updatedScene, "Type de page modifié.", "modifier type page");
    }

    private static PageWorkspaceMutation SetComposition(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, SetPageCompositionRequest request)
    {
        if (!TryFind(snapshot, request.PageKey, out var page, out var scene, out var blocked)) return NoChange(snapshot, ui, blocked!, "modifier composition");
        if (page.Type != ScadaPageType.Default)
            return Block(snapshot, ui, page, "Seule une page Default peut composer un header et un footer.", "Page.Type", "Changer le type de page à Default.");
        if (!ValidateCompositionTarget(snapshot.Project, request.HeaderPageKey, ScadaPageType.Header, out var header, out var error) ||
            !ValidateCompositionTarget(snapshot.Project, request.FooterPageKey, ScadaPageType.Footer, out var footer, out error))
            return Block(snapshot, ui, page, error!, "Page.Composition", "Choisir une page compilée du type attendu.");
        var updatedPage = page with
        {
            HeaderPageKey = header?.PageKey,
            FooterPageKey = footer?.PageKey,
            HeaderPageId = header?.EffectivePageCode,
            FooterPageId = footer?.EffectivePageCode
        };
        var updatedScene = scene with
        {
            HeaderPageKey = header?.PageKey,
            FooterPageKey = footer?.PageKey,
            HeaderPageId = header?.EffectivePageCode,
            FooterPageId = footer?.EffectivePageCode
        };
        return ReplacePageAndScene(snapshot, ui, updatedPage, updatedScene, "Composition de page modifiée.", "modifier composition");
    }

    private static PageWorkspaceMutation SetCanvas(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, SetPageCanvasRequest request)
    {
        if (!TryFind(snapshot, request.PageKey, out var page, out var scene, out var blocked))
            return NoChange(snapshot, ui, blocked!, "modifier dimensions page");
        if (request.CanvasSize.Width < 160 || request.CanvasSize.Height < 120)
            return Block(snapshot, ui, page, "Les dimensions de page doivent être au moins 160 x 120.", "Page.CanvasSize", "Saisir une largeur et une hauteur valides.");
        if (page.EffectiveCanvasSize == request.CanvasSize && scene.CanvasSize == request.CanvasSize)
            return NoChange(snapshot, ui, CommandResult.NoChange("Les dimensions sont inchangées."), "modifier dimensions page");
        return ReplacePageAndScene(
            snapshot,
            ui,
            page with { CanvasSize = request.CanvasSize },
            scene.WithCanvasSize(request.CanvasSize),
            "Dimensions de page modifiées.",
            "modifier dimensions page");
    }

    private static PageWorkspaceMutation SetBackground(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, SetPageBackgroundRequest request)
    {
        if (!TryFind(snapshot, request.PageKey, out var page, out var scene, out var blocked))
            return NoChange(snapshot, ui, blocked!, "modifier arrière-plan page");
        if (page.EffectiveBackground == request.Background && scene.EffectiveBackground == request.Background)
            return NoChange(snapshot, ui, CommandResult.NoChange("L'arrière-plan est inchangé."), "modifier arrière-plan page");
        return ReplacePageAndScene(
            snapshot,
            ui,
            page with { Background = request.Background },
            scene.WithBackground(request.Background),
            "Arrière-plan de page modifié.",
            "modifier arrière-plan page");
    }

    private PageWorkspaceMutation Validate(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui)
    {
        var diagnostics = ScadaProjectBuildValidator.Validate(snapshot.Project, snapshot.Scenes.Values.ToArray())
            .Concat(dependencyAnalyzer.Analyze(snapshot, ui.OpenPageKeys).Diagnostics)
            .Distinct()
            .ToArray();
        var errors = diagnostics.Count(item => item.Severity == ScadaBuildValidationSeverity.Error);
        return NoChange(snapshot, ui, CommandResult.NoChange(
            errors == 0 ? "Validation terminée sans erreur bloquante." : $"Validation terminée : {errors} erreur(s).",
            diagnostics), "valider pages");
    }

    private static PageWorkspaceMutation Route(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, Guid pageKey, bool open, string message)
    {
        var page = snapshot.Project.Scenes.FirstOrDefault(item => item.PageKey == pageKey);
        if (page is null) return NoChange(snapshot, ui, CommandResult.Blocked("La page demandée n'existe pas."), "ouvrir page");
        var afterUi = open ? OpenAndSelect(ui, pageKey) : ui with { SelectedPageKey = pageKey };
        var result = CommandResult.NoChange(message, pageToSelectKey: pageKey, pageToOpenKey: open ? pageKey : null);
        return new PageWorkspaceMutation(snapshot, snapshot, ui, afterUi, result, open ? "ouvrir page" : "propriétés page");
    }

    private static PageWorkspaceMutation ReplacePageAndScene(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, ScadaSceneReference page, ScadaScene scene, string message, string label)
    {
        var pages = snapshot.Project.Scenes.Select(item => item.PageKey == page.PageKey ? page : item).ToArray();
        var scenes = new Dictionary<Guid, ScadaScene>(snapshot.Scenes) { [page.PageKey] = scene };
        var after = NextSnapshot(snapshot, snapshot.Project with { Scenes = pages }, scenes);
        return Changed(snapshot, after, ui, ui with { SelectedPageKey = page.PageKey }, page.PageKey, message, label);
    }

    private static PageWorkspaceMutation Changed(PageWorkspaceSnapshot before, PageWorkspaceSnapshot after, ProjectWorkspaceUiSnapshot beforeUi, ProjectWorkspaceUiSnapshot afterUi, Guid pageKey, string message, string label, bool open = false) =>
        new(before, after, beforeUi, afterUi,
            CommandResult.Success(message, [pageKey], pageKey, open ? pageKey : null, workspaceDirty: true), label);

    private static PageWorkspaceMutation NoChange(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, CommandResult result, string label) =>
        new(snapshot, snapshot, ui, ui, result, label);

    private static PageWorkspaceMutation InvalidCode(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, string code, PageCodeValidationResult validation, ScadaSceneReference? page = null)
    {
        var issue = new ScadaBuildValidationIssue(
            ScadaBuildValidationSeverity.Error,
            "page.code-invalid",
            validation.Errors[0],
            page?.EffectivePageCode ?? code,
            page?.PageKey,
            PropertyPath: "Page.PageCode",
            SuggestedFix: "Utiliser un code portable unique commençant par une lettre minuscule.");
        return NoChange(snapshot, ui, CommandResult.Blocked(validation.Errors[0], [issue]), "modifier code page");
    }

    private static PageWorkspaceMutation Block(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, ScadaSceneReference page, string message, string path, string fix)
    {
        var issue = new ScadaBuildValidationIssue(ScadaBuildValidationSeverity.Error, "page.command-blocked", message,
            page.EffectivePageCode, page.PageKey, PropertyPath: path, SuggestedFix: fix);
        return NoChange(snapshot, ui, CommandResult.Blocked(message, [issue]), "page");
    }

    private static bool TryFind(PageWorkspaceSnapshot snapshot, Guid pageKey, out ScadaSceneReference page, out ScadaScene scene, out CommandResult? blocked)
    {
        page = snapshot.Project.Scenes.FirstOrDefault(item => item.PageKey == pageKey)!;
        if (page is null || !snapshot.Scenes.TryGetValue(pageKey, out scene!))
        {
            scene = null!;
            blocked = CommandResult.Blocked("La page demandée ou sa scène n'existe pas dans le snapshot courant.");
            return false;
        }
        blocked = null;
        return true;
    }

    private static PageWorkspaceSnapshot NextSnapshot(PageWorkspaceSnapshot snapshot, ScadaProject project, IReadOnlyDictionary<Guid, ScadaScene> scenes) =>
        new(snapshot.Version + 1, project, scenes, snapshot.PendingDeletions);

    private static IReadOnlyDictionary<Guid, ScadaScene> AddScene(IReadOnlyDictionary<Guid, ScadaScene> scenes, ScadaScene scene)
    {
        var result = new Dictionary<Guid, ScadaScene>(scenes) { [scene.PageKey] = scene };
        return result;
    }

    private static IReadOnlyList<ScadaSceneReference> InsertAfter(IReadOnlyList<ScadaSceneReference> pages, ScadaSceneReference page, Guid? afterKey)
    {
        var result = pages.ToList();
        var index = afterKey is { } key ? result.FindIndex(item => item.PageKey == key) : -1;
        result.Insert(index < 0 ? result.Count : index + 1, page);
        return result;
    }

    private static ProjectWorkspaceUiSnapshot OpenAndSelect(ProjectWorkspaceUiSnapshot ui, Guid pageKey) => ui with
    {
        OpenPageKeys = ui.OpenPageKeys.Contains(pageKey) ? ui.OpenPageKeys : ui.OpenPageKeys.Append(pageKey).ToArray(),
        SelectedPageKey = pageKey,
        ActivePageKey = pageKey
    };

    private static string? ResolveCode(ScadaProject project, Guid? key) => key is { } value
        ? project.Scenes.FirstOrDefault(page => page.PageKey == value)?.EffectivePageCode
        : null;

    private static bool IsSelfReference(Guid? targetKey, string? targetCode, ScadaSceneReference source) =>
        (targetKey is { } key && key != Guid.Empty && key == source.PageKey) ||
        ((targetKey is null || targetKey == Guid.Empty) && string.Equals(targetCode, source.EffectivePageCode, StringComparison.OrdinalIgnoreCase));

    private static ScadaElement RewriteElement(ScadaElement element, ScadaSceneReference source, Guid newPageKey, string newPageCode, IReadOnlyDictionary<string, string> actionIds)
    {
        var commands = element.EffectiveCommandConfig.Commands.Select(command =>
            IsSelfReference(command.TargetPageKey, command.TargetPageId, source)
                ? command with { TargetPageKey = newPageKey, TargetPageId = newPageCode }
                : command).ToArray();
        return element with
        {
            Children = element.Children is null
                ? null
                : element.ChildElements.Select(child => RewriteElement(child, source, newPageKey, newPageCode, actionIds)).ToArray(),
            Events = element.Events is null
                ? null
                : element.EventBindings.Select(binding => actionIds.TryGetValue(binding.ActionId, out var replacement)
                    ? binding with { ActionId = replacement }
                    : binding).ToArray(),
            CommandConfig = element.CommandConfig is null ? null : new ScadaElementCommandConfig(commands)
        };
    }

    private static ScadaScene RewriteCompatibilityTargetCodes(ScadaScene scene, Guid targetPageKey, string targetPageCode) => scene with
    {
        Actions = scene.ActionDefinitions.Select(action => action.TargetPageKey == targetPageKey
            ? action with { TargetPageId = targetPageCode }
            : action).ToArray(),
        Elements = scene.Elements.Select(element => RewriteCompatibilityTargetCodes(element, targetPageKey, targetPageCode)).ToArray()
    };

    private static ScadaElement RewriteCompatibilityTargetCodes(ScadaElement element, Guid targetPageKey, string targetPageCode)
    {
        var commands = element.EffectiveCommandConfig.Commands.Select(command => command.TargetPageKey == targetPageKey
            ? command with { TargetPageId = targetPageCode }
            : command).ToArray();
        return element with
        {
            Children = element.Children is null
                ? null
                : element.ChildElements.Select(child => RewriteCompatibilityTargetCodes(child, targetPageKey, targetPageCode)).ToArray(),
            CommandConfig = element.CommandConfig is null ? null : new ScadaElementCommandConfig(commands)
        };
    }

    private static bool ValidateCompositionTarget(ScadaProject project, Guid? key, ScadaPageType expectedType, out ScadaSceneReference? target, out string? error)
    {
        target = null;
        error = null;
        if (key is null || key == Guid.Empty) return true;
        target = project.Scenes.FirstOrDefault(page => page.PageKey == key);
        if (target is null) error = "La page de composition sélectionnée n'existe pas.";
        else if (target.Type != expectedType) error = $"La page de composition doit être de type {expectedType}.";
        else if (!target.IncludeInBuild) error = "La page de composition doit être incluse dans le build.";
        return error is null;
    }

    private static string Describe(PageDependencyKind kind) => kind switch
    {
        PageDependencyKind.Home => "la page d'accueil",
        PageDependencyKind.Header => "une composition header",
        PageDependencyKind.Footer => "une composition footer",
        PageDependencyKind.ActionNavigate or PageDependencyKind.ActionPopup => "une action",
        PageDependencyKind.CommandNavigate or PageDependencyKind.CommandPopup => "une commande Element+",
        _ => "le workspace"
    };
}
