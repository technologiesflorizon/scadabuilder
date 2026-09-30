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
        context.DetectPageReferencedAsPage(loweredScenes);

        var resultScenes = new Dictionary<Guid, ScadaScene>();
        foreach (var (key, scene) in scenes)
        {
            if (context.IsQuickWindowKey(key) || scene.PageType == ScadaPageType.QuickWindow) continue;
            resultScenes[key] = loweredScenes.TryGetValue(key, out var lowered) ? lowered : scene;
        }

        // Defence in depth (controller fix round 1): a pre-existing invocation that no live command's
        // QuickWindowInvocationKey references any more (e.g. its caller command was migrated to a
        // ParameterSheetKey/QuickWindowPageKey pair and its old invocation was never retired at authoring
        // time) must not reach the lowered project - ValidateQuickWindowInvocations validates every
        // invocation unconditionally, and an orphan pins a build/export error to a command that looks
        // correctly configured, with no UI left to fix it.
        //
        // Fix round 2: a command can also live INSIDE quick-window content - a QuickWindow page's own
        // content (which becomes a definition's content by this same lowering) or a pre-existing DEC-0050
        // definition's content - and OpenQuickWindow/CloseQuickWindow are both offered there too. The
        // liveness set must therefore be collected from every place a command can live in the LOWERED
        // project: the lowered page scenes AND every definition's content (both freshly built here and
        // pre-existing), never from resultScenes alone (which excludes every QuickWindow page).
        var allDefinitions = definitions.Count == 0
            ? project.EffectiveQuickWindows
            : (IReadOnlyList<QuickWindowDefinition>)[.. project.EffectiveQuickWindows, .. definitions];
        var liveInvocationKeys = CollectReferencedInvocationKeys(resultScenes, allDefinitions);
        var keptPreexisting = project.EffectiveQuickWindowInvocations
            .Where(invocation => liveInvocationKeys.Contains(invocation.InvocationKey))
            .ToArray();
        var invocationsChanged = keptPreexisting.Length != project.EffectiveQuickWindowInvocations.Count || context.Invocations.Count > 0;

        var resultProject = project with
        {
            Scenes = project.Scenes.Where(page => !context.IsQuickWindowKey(page.PageKey)).ToArray(),
            QuickWindows = definitions.Count == 0
                ? project.QuickWindows
                : [.. project.EffectiveQuickWindows, .. definitions],
            QuickWindowInvocations = invocationsChanged
                ? [.. keptPreexisting, .. context.Invocations]
                : project.QuickWindowInvocations
        };

        return new ParameterSheetLoweringResult(resultProject, resultScenes, context.Diagnostics);
    }

    /// <summary>
    /// Collects every `QuickWindowInvocationKey` referenced by a command anywhere in <paramref name="scenes"/>
    /// or in any of <paramref name="definitions"/>' own content, deterministically (scenes ordered by page
    /// key, definitions in their given order).
    /// </summary>
    private static HashSet<Guid> CollectReferencedInvocationKeys(
        IReadOnlyDictionary<Guid, ScadaScene> scenes,
        IReadOnlyList<QuickWindowDefinition> definitions)
    {
        var keys = new HashSet<Guid>();
        foreach (var scene in scenes.OrderBy(pair => pair.Key).Select(pair => pair.Value))
        {
            foreach (var element in scene.Elements ?? [])
            {
                CollectReferencedInvocationKeys(element, keys);
            }
        }

        foreach (var definition in definitions)
        {
            foreach (var element in definition.EffectiveContent.EffectiveElements)
            {
                CollectReferencedInvocationKeys(element, keys);
            }
        }

        return keys;
    }

    private static void CollectReferencedInvocationKeys(ScadaElement element, HashSet<Guid> keys)
    {
        foreach (var command in element.CommandConfig?.Commands ?? [])
        {
            if (command.QuickWindowInvocationKey is { } key) keys.Add(key);
        }

        foreach (var child in element.Children ?? [])
        {
            CollectReferencedInvocationKeys(child, keys);
        }
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

        /// <summary>
        /// Per-(page, #N) type implied by every invocation's binding, in traversal order (spec §6: "il est
        /// déduit quand toutes les invocations concordent et vaut String sinon"). Only a resolved Tag binding
        /// (via the tag's catalog <c>Datatype</c>) or a string Literal binding contributes an observation; an
        /// Expression binding is untyped in this codebase (<c>QuickWindowBindingValidator.ValidateExpressionBinding</c>
        /// never compares it against a member type) and is treated as compatible with any inferred type, so it
        /// contributes none. Grouped by <c>SheetKey</c>, not display name: two distinct sheets may share a
        /// display name, and collapsing on it would hide a real conflict or misattribute one.
        /// </summary>
        private readonly Dictionary<(Guid PageKey, int Number), List<(Guid SheetKey, string SheetDisplayName, QuickWindowDataType DataType)>> _placeholderTypeObservations = new();

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
                            ResolvePlaceholderDataType(pageKey, group.Key, page),
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

        /// <summary>
        /// Spec §6: a `#N` member's <see cref="QuickWindowDataType"/> is deduced when every invocation's
        /// binding for that member agrees, and stays <see cref="QuickWindowDataType.String"/> otherwise — in
        /// which case a <c>parameter-sheet.placeholder-type-conflict</c> error names every disagreeing sheet.
        /// No observation at all (the page is never opened) also keeps <see cref="QuickWindowDataType.String"/>.
        /// </summary>
        private QuickWindowDataType ResolvePlaceholderDataType(Guid pageKey, int number, ScadaSceneReference page)
        {
            if (!_placeholderTypeObservations.TryGetValue((pageKey, number), out var observations) || observations.Count == 0)
            {
                return QuickWindowDataType.String;
            }

            var distinctTypes = observations.Select(observation => observation.DataType).Distinct().ToArray();
            if (distinctTypes.Length == 1)
            {
                return distinctTypes[0];
            }

            var bySheet = string.Join(", ", observations
                .GroupBy(observation => observation.SheetKey)
                .OrderBy(group => group.First().SheetDisplayName, StringComparer.Ordinal)
                .ThenBy(group => group.Key)
                .Select(group => $"'{group.First().SheetDisplayName}' ({group.First().DataType})"));
            Diagnostics.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                "parameter-sheet.placeholder-type-conflict",
                $"Page '{page.EffectivePageCode}', #{number} : les feuilles ne concordent pas sur le type de #{number} ({bySheet}) ; " +
                "le membre reste String.",
                PageId: page.EffectivePageCode,
                PageKey: pageKey,
                PropertyPath: $"Scenes[{page.EffectivePageCode}].#{number}",
                TargetKey: QuickWindowKeys.Member(pageKey, number)));
            return QuickWindowDataType.String;
        }

        /// <summary>Records the type one binding implies for its `#N`, per <see cref="_placeholderTypeObservations"/>.</summary>
        private void RecordPlaceholderTypeObservation(Guid pageKey, int number, ParameterSheet sheet, QuickWindowDataType? impliedType)
        {
            if (impliedType is not { } type) return;

            var key = (pageKey, number);
            if (!_placeholderTypeObservations.TryGetValue(key, out var list))
            {
                list = new List<(Guid SheetKey, string SheetDisplayName, QuickWindowDataType DataType)>();
                _placeholderTypeObservations[key] = list;
            }

            list.Add((sheet.SheetKey, sheet.DisplayName, type));
        }

        /// <summary>
        /// Controller ruling: after lowering removes quick-window pages from the page inventory, any
        /// remaining page's <c>HeaderPageKey</c>/<c>FooterPageKey</c>, any <see cref="ScadaCommandKind.Navigate"/>
        /// command's <c>TargetPageKey</c>, and any legacy <see cref="ScadaActionDefinition.TargetPageKey"/> that
        /// still points at a removed quick-window page would otherwise reach
        /// <c>ScadaBuilderV2.Rendering.PageRuntimeIdentityResolver</c> unexamined and throw a raw
        /// <see cref="InvalidOperationException"/> at export time. Surfacing it here, as a named diagnostic,
        /// keeps every authoring-data problem a diagnostic and no product path throwing on it (contract R1).
        /// </summary>
        public void DetectPageReferencedAsPage(IReadOnlyDictionary<Guid, ScadaScene> loweredScenes)
        {
            foreach (var page in project.Scenes)
            {
                if (IsQuickWindowKey(page.PageKey)) continue;

                CheckPageReference(page, page.HeaderPageKey, "en-tête", "HeaderPageKey");
                CheckPageReference(page, page.FooterPageKey, "pied de page", "FooterPageKey");

                if (!scenes.TryGetValue(page.PageKey, out var originalScene)) continue;
                var scene = loweredScenes.TryGetValue(page.PageKey, out var lowered) ? lowered : originalScene;

                foreach (var action in scene.ActionDefinitions)
                {
                    if (action.TargetPageKey is not { } actionTargetKey || !_quickWindowPages.TryGetValue(actionTargetKey, out var actionTarget))
                    {
                        continue;
                    }

                    Diagnostics.Add(new ScadaBuildValidationIssue(
                        ScadaBuildValidationSeverity.Error,
                        "parameter-sheet.page-referenced-as-page",
                        $"L'action '{action.Id}' de la page '{page.EffectivePageCode}' cible la page '{actionTarget.Reference.EffectivePageCode}', " +
                        "une page Fenêtre rapide qui n'existe plus comme page après l'abaissement.",
                        PageId: page.EffectivePageCode,
                        PageKey: page.PageKey,
                        PropertyPath: $"Scenes[{page.EffectivePageCode}].Actions[{action.Id}].TargetPageKey",
                        TargetKey: actionTargetKey));
                }

                foreach (var element in scene.Elements ?? [])
                {
                    CheckElementNavigateCommands(page, element);
                }
            }
        }

        private void CheckPageReference(ScadaSceneReference page, Guid? referencedKey, string role, string propertyName)
        {
            if (referencedKey is not { } key || !_quickWindowPages.TryGetValue(key, out var target)) return;

            Diagnostics.Add(new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                "parameter-sheet.page-referenced-as-page",
                $"La page '{page.EffectivePageCode}' référence la page '{target.Reference.EffectivePageCode}' comme {role}, " +
                "mais celle-ci est une page Fenêtre rapide et n'existe plus comme page après l'abaissement.",
                PageId: page.EffectivePageCode,
                PageKey: page.PageKey,
                PropertyPath: $"Scenes[{page.EffectivePageCode}].{propertyName}",
                TargetKey: key));
        }

        private void CheckElementNavigateCommands(ScadaSceneReference page, ScadaElement element)
        {
            var commands = element.CommandConfig?.Commands;
            if (commands is not null)
            {
                foreach (var command in commands)
                {
                    if (command.Kind != ScadaCommandKind.Navigate ||
                        command.TargetPageKey is not { } targetKey ||
                        !_quickWindowPages.TryGetValue(targetKey, out var target))
                    {
                        continue;
                    }

                    Diagnostics.Add(new ScadaBuildValidationIssue(
                        ScadaBuildValidationSeverity.Error,
                        "parameter-sheet.page-referenced-as-page",
                        $"La commande '{command.Id}' de l'élément '{element.Id}' (page '{page.EffectivePageCode}') cible la page " +
                        $"'{target.Reference.EffectivePageCode}', une page Fenêtre rapide qui n'existe plus comme page après l'abaissement.",
                        PageId: page.EffectivePageCode,
                        PageKey: page.PageKey,
                        ElementId: element.Id,
                        CommandId: command.Id,
                        PropertyPath: $"{ElementPath(page, element.Id)}.CommandConfig.Commands[{command.Id}].TargetPageKey",
                        TargetKey: targetKey));
                }
            }

            foreach (var child in element.Children ?? [])
            {
                CheckElementNavigateCommands(page, child);
            }
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

                // Expression bindings are untyped in this codebase (see the remarks on
                // _placeholderTypeObservations) and contribute no observation.
                var impliedType = entry.Kind switch
                {
                    ParameterValueKind.Tag => QuickWindowBindingValidator.InferDataTypeFromTagDatatype(
                        project.TagCatalog?.Tags.FirstOrDefault(tag => string.Equals(tag.Id, canonicalId, StringComparison.Ordinal))?.Datatype),
                    ParameterValueKind.Literal => QuickWindowDataType.String,
                    _ => null
                };
                RecordPlaceholderTypeObservation(pageKey, number, sheet, impliedType);
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
