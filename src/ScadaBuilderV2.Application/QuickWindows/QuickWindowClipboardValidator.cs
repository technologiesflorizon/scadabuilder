using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.QuickWindows;

/// <summary>Authoring surface receiving a paste, a duplication or a library instantiation.</summary>
public enum QuickWindowClipboardTargetKind
{
    /// <summary>The content is dropped on a page scene.</summary>
    Page = 0,

    /// <summary>The content is dropped on the canvas of a quick-window definition.</summary>
    QuickWindow = 1
}

/// <summary>
/// The bounded target context of one clipboard operation: a page, or one quick-window definition and
/// its local interface.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-031, FR-034.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowClipboardTests.cs.
/// </remarks>
public sealed record QuickWindowClipboardTarget(
    QuickWindowClipboardTargetKind Kind,
    Guid? DefinitionKey = null,
    IReadOnlyList<QuickWindowInterfaceMember>? InterfaceMembers = null)
{
    /// <summary>Gets the local interface members of the target definition, empty on a page.</summary>
    public IReadOnlyList<QuickWindowInterfaceMember> EffectiveMembers => InterfaceMembers ?? [];

    /// <summary>Creates the target context of a page scene.</summary>
    public static QuickWindowClipboardTarget ForPage() => new(QuickWindowClipboardTargetKind.Page);

    /// <summary>Creates the target context of one quick-window definition.</summary>
    public static QuickWindowClipboardTarget ForQuickWindow(QuickWindowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new QuickWindowClipboardTarget(
            QuickWindowClipboardTargetKind.QuickWindow,
            definition.DefinitionKey,
            definition.EffectiveInterfaceMembers);
    }
}

/// <summary>Why one reference cannot be resolved in the target context.</summary>
public enum QuickWindowClipboardIssueKind
{
    /// <summary>A physical project tag pasted inside a quick-window content.</summary>
    ProjectTagInQuickWindow = 0,

    /// <summary>A local interface member pasted outside its own definition.</summary>
    LocalMemberOutsideDefinition = 1,

    /// <summary>A port belonging to another quick-window definition.</summary>
    ForeignDefinitionPort = 2,

    /// <summary>A reference that resolves nowhere in the target context.</summary>
    UnresolvedReference = 3,

    /// <summary>An `OpenQuickWindow` command whose invocation or definition no longer exists.</summary>
    MissingInvocationTarget = 4,

    /// <summary>An invocation already owned by another caller and therefore not duplicable.</summary>
    DuplicatedInvocation = 5
}

/// <summary>One reference that the target context cannot honour, named by object and property.</summary>
/// <param name="Kind">Why the reference is refused.</param>
/// <param name="ElementId">Scene object carrying the reference.</param>
/// <param name="ElementName">Authoring label of that object.</param>
/// <param name="PropertyPath">Exact property carrying the reference.</param>
/// <param name="Reference">The refused reference itself.</param>
/// <param name="Message">Operator-facing explanation.</param>
public sealed record QuickWindowClipboardIssue(
    QuickWindowClipboardIssueKind Kind,
    string ElementId,
    string ElementName,
    string PropertyPath,
    string Reference,
    string Message);

/// <summary>Result of one boundary analysis.</summary>
/// <param name="Issues">Every refused reference, in stable order.</param>
public sealed record QuickWindowClipboardAnalysis(IReadOnlyList<QuickWindowClipboardIssue> Issues)
{
    /// <summary>Gets whether the operation may be applied as is.</summary>
    public bool IsAllowed => Issues.Count == 0;

    /// <summary>Gets the analysis of an operation that crosses no boundary.</summary>
    public static QuickWindowClipboardAnalysis Allowed { get; } = new([]);
}

/// <summary>
/// Validates fail-closed every paste, duplication or library instantiation crossing the page ↔ quick-window
/// boundary, and produces the `Coller sans liaisons` variant.
/// </summary>
/// <remarks>
/// A quick-window canvas never references a physical project tag and a page never references a local
/// interface member. Anything else — a port of another definition, an unresolvable reference, an invocation
/// whose target is gone or one already owned by another caller — is refused too. Nothing is ever promoted
/// automatically into the local interface: stripping leaves the property unbound.
///
/// Decisions: DEC-0050, FR-031, FR-034, FR-UI-23.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.5.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowClipboardTests.cs.
/// </remarks>
public static class QuickWindowClipboardValidator
{
    /// <summary>Analyzes one clipboard payload against its target context.</summary>
    /// <param name="elements">The pasted, duplicated or instantiated objects.</param>
    /// <param name="target">The receiving context.</param>
    /// <param name="project">The project owning tags, definitions and invocations.</param>
    /// <param name="sourceElementIds">Ids already present in the target scene, used to detect a duplication.</param>
    public static QuickWindowClipboardAnalysis Analyze(
        IReadOnlyList<ScadaElement> elements,
        QuickWindowClipboardTarget target,
        ScadaProject? project,
        IReadOnlySet<string>? sourceElementIds = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (elements is null || elements.Count == 0) return QuickWindowClipboardAnalysis.Allowed;

        var catalogTags = (project?.TagCatalog?.Tags ?? [])
            .Select(tag => tag.Id)
            .ToHashSet(StringComparer.Ordinal);
        var definitions = project?.EffectiveQuickWindows ?? [];
        var localMembers = target.EffectiveMembers
            .Select(member => member.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var foreignMembers = definitions
            .Where(definition => definition.DefinitionKey != target.DefinitionKey)
            .SelectMany(definition => definition.EffectiveInterfaceMembers.Select(member => (definition, member)))
            .ToArray();
        var invocations = (project?.EffectiveQuickWindowInvocations ?? [])
            .ToDictionary(invocation => invocation.InvocationKey, invocation => invocation);
        var issues = new List<QuickWindowClipboardIssue>();

        foreach (var element in Flatten(elements))
        {
            foreach (var (path, reference) in CollectTagReferences(element))
            {
                var issue = ClassifyTagReference(element, path, reference, target, catalogTags, localMembers, foreignMembers);
                if (issue is not null) issues.Add(issue);
            }

            foreach (var command in element.EffectiveCommandConfig.Commands
                         .Where(command => command.Kind == ScadaCommandKind.OpenQuickWindow))
            {
                var path = $"CommandConfig.Commands[{command.Id}].QuickWindowInvocationKey";
                if (command.QuickWindowInvocationKey is not { } invocationKey || invocationKey == Guid.Empty)
                {
                    issues.Add(Issue(
                        QuickWindowClipboardIssueKind.MissingInvocationTarget,
                        element,
                        path,
                        string.Empty,
                        "La commande 'Ouvrir une fenêtre rapide' collée n'a aucune invocation."));
                    continue;
                }

                if (!invocations.TryGetValue(invocationKey, out var invocation))
                {
                    issues.Add(Issue(
                        QuickWindowClipboardIssueKind.MissingInvocationTarget,
                        element,
                        path,
                        invocationKey.ToString(),
                        "L'invocation référencée par la commande collée n'existe pas dans ce projet."));
                    continue;
                }

                if (!definitions.Any(definition => definition.DefinitionKey == invocation.DefinitionKey))
                {
                    issues.Add(Issue(
                        QuickWindowClipboardIssueKind.MissingInvocationTarget,
                        element,
                        path,
                        invocationKey.ToString(),
                        "La fenêtre rapide ciblée par l'invocation collée n'existe pas dans ce projet."));
                    continue;
                }

                var alreadyOwnedByAnother = sourceElementIds?.Contains(element.Id) != true &&
                    !string.IsNullOrWhiteSpace(invocation.OwnerElementId) &&
                    !string.Equals(invocation.OwnerElementId, element.Id, StringComparison.Ordinal);
                if (alreadyOwnedByAnother)
                {
                    issues.Add(Issue(
                        QuickWindowClipboardIssueKind.DuplicatedInvocation,
                        element,
                        path,
                        invocationKey.ToString(),
                        $"L'invocation collée appartient déjà à l'appelant '{invocation.OwnerElementId}'; une invocation ne peut pas être partagée."));
                }
            }
        }

        var stable = issues
            .OrderBy(issue => issue.ElementId, StringComparer.Ordinal)
            .ThenBy(issue => issue.PropertyPath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Reference, StringComparer.Ordinal)
            .ToArray();
        return new QuickWindowClipboardAnalysis(stable);
    }

    /// <summary>
    /// Returns the payload of the `Coller sans liaisons` variant: every refused reference is removed and its
    /// property is left unbound. No reference is rewritten, remapped nor promoted into a local interface.
    /// </summary>
    public static IReadOnlyList<ScadaElement> StripRefusedReferences(
        IReadOnlyList<ScadaElement> elements,
        QuickWindowClipboardAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        if (elements is null || elements.Count == 0 || analysis.IsAllowed) return elements ?? [];

        var refusedByElement = analysis.Issues
            .GroupBy(issue => issue.ElementId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        return elements.Select(element => Strip(element, refusedByElement)).ToArray();
    }

    private static ScadaElement Strip(ScadaElement element, IReadOnlyDictionary<string, QuickWindowClipboardIssue[]> refusedByElement)
    {
        var children = element.ChildElements.Count == 0
            ? element.Children
            : element.ChildElements.Select(child => Strip(child, refusedByElement)).ToArray();
        var updated = element with { Children = children };
        if (!refusedByElement.TryGetValue(element.Id, out var refused)) return updated;

        var paths = refused.Select(issue => issue.PropertyPath).ToHashSet(StringComparer.Ordinal);

        if (updated.Data is { } data)
        {
            updated = updated with
            {
                Data = data with
                {
                    ReadTagId = paths.Contains("Data.ReadTagId") ? null : data.ReadTagId,
                    WriteTagId = paths.Contains("Data.WriteTagId") ? null : data.WriteTagId,
                    TagBinding = paths.Contains("Data.TagBinding") ? null : data.TagBinding
                }
            };
        }

        if (updated.StateConfig is { } state)
        {
            var readVariable = state.ReadVariable is not null && paths.Contains("StateConfig.ReadVariable.TagId")
                ? null
                : state.ReadVariable;
            var states = state.States
                .Where(rule => !paths.Contains($"StateConfig.States[{rule.Id}].Expression"))
                .ToArray();
            updated = updated with { StateConfig = state with { ReadVariable = readVariable, States = states } };
        }

        if (updated.CommandConfig is { } commandConfig)
        {
            var commands = commandConfig.Commands.Select(command =>
            {
                var writePath = $"CommandConfig.Commands[{command.Id}].WriteTagId";
                var readPath = $"CommandConfig.Commands[{command.Id}].ReadTagId";
                var invocationPath = $"CommandConfig.Commands[{command.Id}].QuickWindowInvocationKey";
                return command with
                {
                    WriteTagId = paths.Contains(writePath) ? null : command.WriteTagId,
                    ReadTagId = paths.Contains(readPath) ? null : command.ReadTagId,
                    QuickWindowInvocationKey = paths.Contains(invocationPath) ? null : command.QuickWindowInvocationKey
                };
            }).ToArray();
            updated = updated with { CommandConfig = commandConfig with { Commands = commands } };
        }

        if (updated.Table is { Cells: not null } table)
        {
            var cells = table.Cells!.Select(cell =>
            {
                var readPath = $"Table.Cells[{cell.Row},{cell.Column}].ReadTagId";
                var writePath = $"Table.Cells[{cell.Row},{cell.Column}].WriteTagId";
                if (cell.ValueBindings is not { } bindings) return cell;
                return cell with
                {
                    ValueBindings = bindings with
                    {
                        ReadTagId = paths.Contains(readPath) ? null : bindings.ReadTagId,
                        WriteTagId = paths.Contains(writePath) ? null : bindings.WriteTagId
                    }
                };
            }).ToArray();
            updated = updated with { Table = table with { Cells = cells } };
        }

        return updated;
    }

    private static QuickWindowClipboardIssue? ClassifyTagReference(
        ScadaElement element,
        string propertyPath,
        string reference,
        QuickWindowClipboardTarget target,
        IReadOnlySet<string> catalogTags,
        IReadOnlySet<string> localMembers,
        IReadOnlyList<(QuickWindowDefinition Definition, QuickWindowInterfaceMember Member)> foreignMembers)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;

        var foreign = foreignMembers.FirstOrDefault(entry =>
            string.Equals(entry.Member.Name, reference, StringComparison.OrdinalIgnoreCase));

        if (target.Kind == QuickWindowClipboardTargetKind.Page)
        {
            // A page keeps its physical tags; only a local interface member is a boundary crossing.
            if (catalogTags.Contains(reference) || foreign.Definition is null) return null;
            return Issue(
                QuickWindowClipboardIssueKind.LocalMemberOutsideDefinition,
                element,
                propertyPath,
                reference,
                $"'{reference}' est un membre de l'Interface locale de '{foreign.Definition.DisplayName}' et n'existe pas sur une page.");
        }

        if (localMembers.Contains(reference)) return null;

        if (catalogTags.Contains(reference))
        {
            return Issue(
                QuickWindowClipboardIssueKind.ProjectTagInQuickWindow,
                element,
                propertyPath,
                reference,
                $"'{reference}' est un tag physique du projet; le contenu d'une fenêtre rapide ne référence que son Interface locale.");
        }

        if (foreign.Definition is not null)
        {
            return Issue(
                QuickWindowClipboardIssueKind.ForeignDefinitionPort,
                element,
                propertyPath,
                reference,
                $"'{reference}' est un port de la fenêtre rapide '{foreign.Definition.DisplayName}' et n'existe pas dans cette Interface locale.");
        }

        return Issue(
            QuickWindowClipboardIssueKind.UnresolvedReference,
            element,
            propertyPath,
            reference,
            $"'{reference}' ne correspond à aucun membre de l'Interface locale de la fenêtre rapide cible.");
    }

    private static IEnumerable<(string PropertyPath, string Reference)> CollectTagReferences(ScadaElement element)
    {
        if (element.Data is { } data)
        {
            if (!string.IsNullOrWhiteSpace(data.ReadTagId)) yield return ("Data.ReadTagId", data.ReadTagId!);
            if (!string.IsNullOrWhiteSpace(data.WriteTagId)) yield return ("Data.WriteTagId", data.WriteTagId!);
            if (!string.IsNullOrWhiteSpace(data.TagBinding)) yield return ("Data.TagBinding", data.TagBinding!);
        }

        if (element.StateConfig is { } state)
        {
            if (state.ReadVariable is { } readVariable && !string.IsNullOrWhiteSpace(readVariable.TagId))
                yield return ("StateConfig.ReadVariable.TagId", readVariable.TagId);

            foreach (var rule in state.States)
            {
                foreach (var tag in rule.Expression?.ReferencedTags ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(tag))
                        yield return ($"StateConfig.States[{rule.Id}].Expression", tag);
                }
            }
        }

        foreach (var command in element.EffectiveCommandConfig.Commands)
        {
            if (!string.IsNullOrWhiteSpace(command.WriteTagId))
                yield return ($"CommandConfig.Commands[{command.Id}].WriteTagId", command.WriteTagId!);
            if (!string.IsNullOrWhiteSpace(command.ReadTagId))
                yield return ($"CommandConfig.Commands[{command.Id}].ReadTagId", command.ReadTagId!);
        }

        foreach (var cell in element.Table?.Cells ?? [])
        {
            if (cell.ValueBindings is not { } bindings) continue;
            if (!string.IsNullOrWhiteSpace(bindings.ReadTagId))
                yield return ($"Table.Cells[{cell.Row},{cell.Column}].ReadTagId", bindings.ReadTagId!);
            if (!string.IsNullOrWhiteSpace(bindings.WriteTagId))
                yield return ($"Table.Cells[{cell.Row},{cell.Column}].WriteTagId", bindings.WriteTagId!);
        }
    }

    private static QuickWindowClipboardIssue Issue(
        QuickWindowClipboardIssueKind kind,
        ScadaElement element,
        string propertyPath,
        string reference,
        string message) =>
        new(kind, element.Id, element.UserLabel, propertyPath, reference, message);

    private static IEnumerable<ScadaElement> Flatten(IEnumerable<ScadaElement> elements)
    {
        foreach (var element in elements)
        {
            yield return element;
            foreach (var child in Flatten(element.ChildElements))
                yield return child;
        }
    }
}
