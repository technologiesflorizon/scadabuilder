using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.QuickWindows;

/// <summary>The lowered project, its lowered scenes and every diagnostic found while lowering.</summary>
/// <param name="Project">The project without quick-window pages, carrying the lowered definitions and invocations.</param>
/// <param name="Scenes">The scenes by page key, without quick-window pages, commands carrying their invocation key.</param>
/// <param name="Diagnostics">Per-sheet and per-(sheet, page) diagnostics, in deterministic order.</param>
public sealed record ParameterSheetLoweringResult(
    ScadaProject Project,
    IReadOnlyDictionary<Guid, ScadaScene> Scenes,
    IReadOnlyList<ScadaBuildValidationIssue> Diagnostics);

/// <summary>
/// Lowers the authoring model — quick-window pages using `#N`, parameter sheets and open-quick-window commands
/// pairing a sheet with a page — onto the DEC-0050 structures, and validates every (sheet, page) pair.
/// </summary>
/// <remarks>
/// Pure and deterministic: no I/O, no random key, so two lowerings of the same input are identical. Validation,
/// preview and export all consume the lowered project. Quick-window pages leave the page inventory: they only
/// exist in the result as <see cref="QuickWindowDefinition"/>s keyed by their page key (ruling R4). Definitions
/// and invocations already present in the input are kept as they are. Lowered invocations never carry
/// <see cref="QuickWindowInvocation.OwnerPageKey"/>, because package validation forbids every <c>*PageKey</c>
/// property (KNOWN_GAPS entry 30).
///
/// Decisions: DEC-0050, DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §3 (R1–R4), §4, §6.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetLoweringTests.cs.
/// </remarks>
public static class ParameterSheetLowering
{
    /// <summary>Lowers <paramref name="project"/> and its loaded <paramref name="scenes"/> (keyed by page key); never throws on authoring data.</summary>
    public static ParameterSheetLoweringResult Lower(ScadaProject project, IReadOnlyDictionary<Guid, ScadaScene> scenes)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(scenes);

        var context = new LoweringContext(project, scenes);
        context.ParseSheets();
        context.CollectQuickWindowPages();
        var loweredScenes = context.LowerCommands();
        var definitions = context.BuildDefinitions(loweredScenes);

        var resultScenes = new Dictionary<Guid, ScadaScene>();
        foreach (var (key, scene) in scenes)
        {
            if (context.IsQuickWindowKey(key) || scene.PageType == ScadaPageType.QuickWindow) continue;
            resultScenes[key] = loweredScenes.TryGetValue(key, out var lowered) ? lowered : scene;
        }

        var resultProject = project with
        {
            Scenes = project.Scenes.Where(page => !context.IsQuickWindowKey(page.PageKey)).ToArray(),
            QuickWindows = definitions.Count == 0
                ? project.QuickWindows
                : [.. project.EffectiveQuickWindows, .. definitions],
            QuickWindowInvocations = context.Invocations.Count == 0
                ? project.QuickWindowInvocations
                : [.. project.EffectiveQuickWindowInvocations, .. context.Invocations]
        };

        return new ParameterSheetLoweringResult(resultProject, resultScenes, context.Diagnostics);
    }

    /// <summary>One sheet and its single parse.</summary>
    private sealed record ParsedSheet(ParameterSheet Sheet, ParameterSheetParseResult Parse);

    /// <summary>One quick-window page; <c>Usages</c> is null when its scene is not loaded.</summary>
    private sealed record QuickWindowPage(ScadaSceneReference Reference, IReadOnlyList<PlaceholderUsage>? Usages);

    /// <summary>State of one lowering run, in step order.</summary>
    private sealed class LoweringContext(ScadaProject project, IReadOnlyDictionary<Guid, ScadaScene> scenes)
    {
        private readonly Dictionary<Guid, ParsedSheet> _sheets = new();
        private readonly Dictionary<Guid, QuickWindowPage> _quickWindowPages = new();

        /// <summary>Diagnostics in the order they were found.</summary>
        public List<ScadaBuildValidationIssue> Diagnostics { get; } = new();

        /// <summary>Invocations lowered from authored commands, in traversal order.</summary>
        public List<QuickWindowInvocation> Invocations { get; } = new();

        /// <summary>Returns whether <paramref name="pageKey"/> is a quick-window page.</summary>
        public bool IsQuickWindowKey(Guid pageKey) => _quickWindowPages.ContainsKey(pageKey);

        /// <summary>Step 1: parse each sheet once; every parse diagnostic becomes <c>parameter-sheet.syntax</c>.</summary>
        public void ParseSheets()
        {
            foreach (var sheet in project.EffectiveParameterSheets)
            {
                if (_sheets.ContainsKey(sheet.SheetKey)) continue;
                var parse = ParameterSheetParser.Parse(sheet.Text);
                _sheets[sheet.SheetKey] = new ParsedSheet(sheet, parse);
                foreach (var diagnostic in parse.Diagnostics)
                {
                    Diagnostics.Add(new ScadaBuildValidationIssue(
                        ScadaBuildValidationSeverity.Error,
                        "parameter-sheet.syntax",
                        $"Feuille '{sheet.DisplayName}', ligne {diagnostic.Line} : {diagnostic.Message}",
                        PropertyPath: SheetPath(sheet),
                        TargetKey: sheet.SheetKey));
                }
            }
        }

        /// <summary>Step 2 (collection): every quick-window page, its placeholder usages and its invalid tokens.</summary>
        public void CollectQuickWindowPages()
        {
            foreach (var page in project.Scenes)
            {
                scenes.TryGetValue(page.PageKey, out var scene);
                var isQuickWindow = page.Type == ScadaPageType.QuickWindow || scene?.PageType == ScadaPageType.QuickWindow;
                if (!isQuickWindow || _quickWindowPages.ContainsKey(page.PageKey)) continue;

                if (scene is null)
                {
                    _quickWindowPages[page.PageKey] = new QuickWindowPage(page, null);
                    Diagnostics.Add(new ScadaBuildValidationIssue(
                        ScadaBuildValidationSeverity.Error,
                        "parameter-sheet.page-scene-missing",
                        $"Page Fenêtre rapide '{page.EffectivePageCode}' : sa scène n'est pas chargée, ses #N ne peuvent pas être vérifiés.",
                        PageId: page.EffectivePageCode,
                        PageKey: page.PageKey,
                        PropertyPath: $"Scenes[{page.EffectivePageCode}]"));
                    continue;
                }

                var (usages, invalid) = QuickWindowPlaceholderUsageCollector.Collect(scene);
                _quickWindowPages[page.PageKey] = new QuickWindowPage(page, usages);
                foreach (var token in invalid)
                {
                    Diagnostics.Add(new ScadaBuildValidationIssue(
                        ScadaBuildValidationSeverity.Error,
                        "parameter-sheet.token-invalid",
                        $"Page '{page.EffectivePageCode}', élément '{token.ElementId}' : le jeton « {token.Raw} » n'est pas un paramètre valide (#1 à #999 sans zéro de tête ; écrire \\# pour un # littéral).",
                        PageId: page.EffectivePageCode,
                        PageKey: page.PageKey,
                        ElementId: token.ElementId,
                        PropertyPath: ElementPath(page, token.ElementId)));
                }
            }
        }

        /// <summary>Steps 3 to 5: validate each (sheet, page) command, build its invocation and rewrite the command.</summary>
        public Dictionary<Guid, ScadaScene> LowerCommands()
        {
            var lowered = new Dictionary<Guid, ScadaScene>();
            foreach (var page in project.Scenes)
            {
                if (lowered.ContainsKey(page.PageKey) || !scenes.TryGetValue(page.PageKey, out var scene)) continue;
                var elements = scene.Elements ?? [];
                var changed = false;
                var rewritten = new ScadaElement[elements.Count];
                for (var i = 0; i < elements.Count; i++)
                {
                    rewritten[i] = LowerElement(page, elements[i]);
                    changed |= !ReferenceEquals(rewritten[i], elements[i]);
                }

                if (changed) lowered[page.PageKey] = scene with { Elements = rewritten };
            }

            return lowered;
        }

        /// <summary>Step 2 (build): one definition per quick-window page, from its lowered scene.</summary>
        public List<QuickWindowDefinition> BuildDefinitions(IReadOnlyDictionary<Guid, ScadaScene> loweredScenes)
        {
            var definitions = new List<QuickWindowDefinition>();
            foreach (var (pageKey, quickWindow) in _quickWindowPages)
            {
                if (quickWindow.Usages is null) continue;
                var scene = loweredScenes.TryGetValue(pageKey, out var lowered) ? lowered : scenes[pageKey];
                var page = quickWindow.Reference;
                var members = quickWindow.Usages
                    .GroupBy(usage => usage.Number)
                    .OrderBy(group => group.Key)
                    .Select(group =>
                    {
                        var writes = group.Any(usage => IsWrite(usage.Site));
                        return new QuickWindowInterfaceMember(
                            QuickWindowKeys.Member(pageKey, group.Key),
                            $"#{group.Key}",
                            writes ? QuickWindowInterfaceFamily.WriteCommand : QuickWindowInterfaceFamily.ReadState,
                            QuickWindowDataType.String,
                            writes ? QuickWindowMemberAccess.Write : QuickWindowMemberAccess.Read);
                    })
                    .ToArray();

                definitions.Add(new QuickWindowDefinition(
                    pageKey,
                    page.EffectivePageCode,
                    page.Title,
                    InterfaceVersion: 1,
                    VisualContent.FromScene(scene),
                    members,
                    new QuickWindowPresentationDefaults()));
            }

            return definitions;
        }

        private ScadaElement LowerElement(ScadaSceneReference owner, ScadaElement element)
        {
            var result = element;

            var commands = element.CommandConfig?.Commands;
            if (commands is not null && commands.Any(IsAuthoredOpenCommand))
            {
                var rewritten = commands
                    .Select(command => IsAuthoredOpenCommand(command) ? LowerCommand(owner, element, command) : command)
                    .ToArray();
                if (!rewritten.SequenceEqual(commands, ReferenceEqualityComparer.Instance))
                {
                    result = result with { CommandConfig = element.CommandConfig! with { Commands = rewritten } };
                }
            }

            var children = element.Children;
            if (children is { Count: > 0 })
            {
                var rewrittenChildren = children.Select(child => LowerElement(owner, child)).ToArray();
                if (!rewrittenChildren.SequenceEqual(children, ReferenceEqualityComparer.Instance))
                {
                    result = result with { Children = rewrittenChildren };
                }
            }

            return result;
        }

        private static bool IsAuthoredOpenCommand(ScadaCommandBinding command) =>
            command.Kind == ScadaCommandKind.OpenQuickWindow &&
            (command.ParameterSheetKey is not null || command.QuickWindowPageKey is not null);

        private ScadaCommandBinding LowerCommand(ScadaSceneReference owner, ScadaElement element, ScadaCommandBinding command)
        {
            var commandSite = $"La commande '{command.Id}' de l'élément '{element.Id}' (page '{owner.EffectivePageCode}')";

            if (command.ParameterSheetKey is not { } sheetKey || !_sheets.TryGetValue(sheetKey, out var parsed))
            {
                var missing = command.ParameterSheetKey is { } key ? key.ToString("D") : "aucune";
                AddCommandIssue(ScadaBuildValidationSeverity.Error, "parameter-sheet.sheet-missing",
                    $"{commandSite} désigne une feuille de paramètres introuvable ({missing}).",
                    owner, element, command, command.ParameterSheetKey);
                return command;
            }

            if (command.QuickWindowPageKey is not { } pageKey || !_quickWindowPages.TryGetValue(pageKey, out var quickWindow))
            {
                var target = command.QuickWindowPageKey is { } key
                    ? project.Scenes.FirstOrDefault(page => page.PageKey == key)
                    : null;
                var what = target is not null
                    ? $"la page '{target.EffectivePageCode}', qui n'est pas une page Fenêtre rapide"
                    : $"une page introuvable ({(command.QuickWindowPageKey is { } k ? k.ToString("D") : "aucune")})";
                AddCommandIssue(ScadaBuildValidationSeverity.Error, "parameter-sheet.page-not-quick-window",
                    $"{commandSite} désigne {what}.",
                    owner, element, command, command.QuickWindowPageKey);
                return command;
            }

            if (quickWindow.Usages is null) return command;

            var sheet = parsed.Sheet;
            var pagePrefix = $"Feuille '{sheet.DisplayName}', page '{quickWindow.Reference.EffectivePageCode}'";
            var bindings = new List<QuickWindowBinding>();
            var used = quickWindow.Usages.GroupBy(usage => usage.Number).OrderBy(group => group.Key).ToArray();

            foreach (var group in used)
            {
                var number = group.Key;
                if (!parsed.Parse.Entries.TryGetValue(number, out var entry))
                {
                    AddCommandIssue(ScadaBuildValidationSeverity.Error, "parameter-sheet.placeholder-undefined",
                        $"{pagePrefix} : #{number} est utilisé par la page mais n'est pas défini dans la feuille.",
                        owner, element, command, sheet.SheetKey);
                    continue;
                }

                var linePrefix = $"{pagePrefix}, ligne {entry.Line}";
                var valid = true;

                if (group.Any(usage => IsWrite(usage.Site)) && entry.Kind != ParameterValueKind.Tag)
                {
                    valid = false;
                    AddCommandIssue(ScadaBuildValidationSeverity.Error, "parameter-sheet.write-not-tag",
                        $"{linePrefix} : #{number} est une cible d'écriture et doit être lié à un tag seul ({{TAG}}), pas à « {entry.Source} ».",
                        owner, element, command, sheet.SheetKey);
                }

                if (group.Any(usage => usage.Site == PlaceholderUsageSite.Text) && entry.Kind != ParameterValueKind.Literal)
                {
                    valid = false;
                    AddCommandIssue(ScadaBuildValidationSeverity.Error, "parameter-sheet.text-not-literal",
                        $"{linePrefix} : #{number} est affiché dans un texte et doit être lié à un littéral chaîne, pas à « {entry.Source} ».",
                        owner, element, command, sheet.SheetKey);
                }

                string? canonicalId = null;
                if (entry.Kind == ParameterValueKind.Tag)
                {
                    var tagName = ((ScadaExprTagRef)entry.Node).TagName;
                    var resolved = ScadaExpressionValidator.TryResolveTagReference(tagName, project.TagCatalog);
                    switch (resolved.Status)
                    {
                        case TagResolveStatus.Resolved:
                            canonicalId = resolved.CanonicalId;
                            break;
                        case TagResolveStatus.Ambiguous:
                            valid = false;
                            AddCommandIssue(ScadaBuildValidationSeverity.Error, "parameter-sheet.tag-ambiguous",
                                $"{linePrefix} : #{number} référence le tag '{tagName}', ambigu dans le catalogue du projet ({string.Join(", ", resolved.Matches)}).",
                                owner, element, command, sheet.SheetKey);
                            break;
                        default:
                            valid = false;
                            AddCommandIssue(ScadaBuildValidationSeverity.Error, "parameter-sheet.tag-unknown",
                                $"{linePrefix} : #{number} référence le tag '{tagName}', absent du catalogue du projet.",
                                owner, element, command, sheet.SheetKey);
                            break;
                    }
                }

                if (!valid) continue;

                var memberKey = QuickWindowKeys.Member(pageKey, number);
                bindings.Add(entry.Kind switch
                {
                    ParameterValueKind.Tag => new QuickWindowBinding(memberKey, QuickWindowBindingSourceKind.Tag, TagId: canonicalId),
                    ParameterValueKind.Literal => new QuickWindowBinding(memberKey, QuickWindowBindingSourceKind.Literal,
                        LiteralValue: ((ScadaExprLiteralString)entry.Node).Value),
                    _ => new QuickWindowBinding(memberKey, QuickWindowBindingSourceKind.Expression,
                        Expression: entry.Source, ExpressionAst: entry.Node)
                });
            }

            var usedNumbers = used.Select(group => group.Key).ToHashSet();
            foreach (var entry in parsed.Parse.Entries.Values.OrderBy(entry => entry.Number))
            {
                if (usedNumbers.Contains(entry.Number)) continue;
                AddCommandIssue(ScadaBuildValidationSeverity.Warning, "parameter-sheet.placeholder-unused",
                    $"{pagePrefix}, ligne {entry.Line} : #{entry.Number} est défini mais n'est pas utilisé par la page.",
                    owner, element, command, sheet.SheetKey);
            }

            var invocationKey = QuickWindowKeys.Invocation(owner.PageKey, element.Id, command.Id);
            Invocations.Add(new QuickWindowInvocation(
                invocationKey,
                pageKey,
                bindings,
                InterfaceVersion: 1,
                OwnerElementId: element.Id,
                OwnerCommandId: command.Id));

            return command with { QuickWindowInvocationKey = invocationKey };
        }

        private void AddCommandIssue(
            ScadaBuildValidationSeverity severity,
            string code,
            string message,
            ScadaSceneReference owner,
            ScadaElement element,
            ScadaCommandBinding command,
            Guid? targetKey) =>
            Diagnostics.Add(new ScadaBuildValidationIssue(
                severity,
                code,
                message,
                PageId: owner.EffectivePageCode,
                PageKey: owner.PageKey,
                ElementId: element.Id,
                CommandId: command.Id,
                PropertyPath: ElementPath(owner, element.Id),
                TargetKey: targetKey));
    }

    private static bool IsWrite(PlaceholderUsageSite site) =>
        site is PlaceholderUsageSite.WriteTag or PlaceholderUsageSite.CommandWriteTag;

    private static string SheetPath(ParameterSheet sheet) => $"ParameterSheets[{sheet.Code}]";

    private static string ElementPath(ScadaSceneReference page, string elementId) => $"Scenes[{page.EffectivePageCode}].{elementId}";
}
