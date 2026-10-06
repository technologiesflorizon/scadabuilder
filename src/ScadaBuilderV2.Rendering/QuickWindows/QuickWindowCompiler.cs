using System.Text.Json;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.Rendering.QuickWindows;

/// <summary>
/// Compiles the quick windows of one project into the deterministic package registries and content files
/// frozen by the package contract.
/// </summary>
/// <remarks>
/// The compiler is <c>internal</c> on purpose. Quick-window capabilities are `Blocked`, so no product path
/// may produce a package carrying them: `Ft100SceneExporter` refuses the export before creating any staging
/// directory. The class is reachable only through `InternalsVisibleTo` so tests can build the non-shippable
/// fixture, and no `allowBlocked` parameter, environment variable, hidden profile, CLI option or conditional
/// branch exists to bypass that gate from the product.
///
/// Determinism: definitions are ordered by `DefinitionKey`, invocations by `InvocationKey`, members and
/// bindings by `MemberKey`, and files by relative path. Two compilations of the same project are identical.
///
/// Statut : DEC-0050 décommissionné comme modèle d'authoring (remplacé par DEC-0052) ; conservé car DEC-0052 en
/// dépend : Ft100SceneExporter compile ici les définitions et invocations abaissées vers le manifeste 2.3 (§12).
/// Decisions: DEC-0047, DEC-0050 (décommissionné), DEC-0052, FR-019, FR-020.
/// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §12.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowExporterTests.cs.
/// </remarks>
internal static class QuickWindowCompiler
{
    /// <summary>Compiles every quick window of the project, or returns the empty compilation.</summary>
    internal static Ft100QuickWindowCompilation Compile(ScadaProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var definitions = project.EffectiveQuickWindows;
        if (definitions.Count == 0)
        {
            return Ft100QuickWindowCompilation.Empty;
        }

        var warnings = new List<string>();
        var entries = new List<Ft100QuickWindowDefinitionEntry>();
        var files = new List<Ft100QuickWindowContentFile>();

        foreach (var definition in definitions.OrderBy(item => item.DefinitionKey))
        {
            var ns = QuickWindowNamespace.ForDefinition(definition.DefinitionKey);
            var relativeHtml = $"{ns}/{ns}.html";
            var relativeCss = $"{ns}/css/{ns}.css";
            var content = definition.EffectiveContent;
            var scene = content.ToScene(ns, definition.EffectiveTitle, definition.DefinitionKey, ns);
            var sceneWarnings = new List<string>();

            // The content reuses the exported Element+ geometry: one semantics for pages and quick windows.
            // No host injects bindings into a quick-window fragment, so it carries its TagBinding and table
            // cell bindings itself (package contract §11b.2).
            var html = Ft100SceneExporter.BuildDocumentHtml(
                scene,
                $"css/{ns}.css",
                sourceContent: string.Empty,
                runtimeScriptSource: null,
                project.TagCatalog,
                sceneWarnings,
                quickWindowFragment: true,
                PlaceholderDatatypes(project, definition));
            var css = Ft100SceneExporter.BuildDocumentCss(scene);
            warnings.AddRange(sceneWarnings);

            files.Add(new Ft100QuickWindowContentFile(relativeHtml, html, relativeCss, css));
            entries.Add(new Ft100QuickWindowDefinitionEntry(
                Key(definition.DefinitionKey),
                definition.EffectiveCode,
                definition.DisplayName,
                definition.InterfaceVersion,
                ns,
                relativeHtml,
                relativeCss,
                content.EffectiveCanvasSize.Width,
                content.EffectiveCanvasSize.Height,
                definition.EffectiveInterfaceMembers
                    .OrderBy(member => member.MemberKey)
                    .Select(ToManifestMember)
                    .ToArray(),
                ToManifestPresentation(definition.EffectivePresentation, definition.DisplayName)));
        }

        var invocations = project.EffectiveQuickWindowInvocations
            .OrderBy(invocation => invocation.InvocationKey)
            .Select(ToManifestInvocation)
            .ToArray();

        return new Ft100QuickWindowCompilation(
            entries,
            invocations,
            files.OrderBy(file => file.RelativeHtmlPath, StringComparer.Ordinal).ToArray(),
            warnings.Distinct(StringComparer.Ordinal).OrderBy(warning => warning, StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Maps each `#N` member of <paramref name="definition"/> to the mapping datatype code
    /// (<see cref="Ft100SceneExporter.NormalizeMappingDatatype"/>) that every invocation's bound tag agrees on. A
    /// member bound by any invocation to a non-tag, to an unknown tag or to a tag of another datatype has none, so
    /// its fragment omits <c>data-scada-datatype</c> exactly as a page whose mapping does not resolve.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0052.
    /// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §11b.2.
    /// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowFragmentValueBindingTests.cs.
    /// </remarks>
    private static IReadOnlyDictionary<string, string>? PlaceholderDatatypes(ScadaProject project, QuickWindowDefinition definition)
    {
        var tags = (project.TagCatalog?.Tags ?? [])
            .GroupBy(tag => tag.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var invocations = project.EffectiveQuickWindowInvocations
            .Where(invocation => invocation.DefinitionKey == definition.DefinitionKey)
            .ToArray();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var member in definition.EffectiveInterfaceMembers)
        {
            if (!QuickWindowPlaceholder.TryParseName(member.Name, out _) || invocations.Length == 0) continue;
            var codes = invocations
                .Select(invocation => (invocation.Bindings ?? []).FirstOrDefault(binding => binding.MemberKey == member.MemberKey))
                .Select(binding => binding is { SourceKind: QuickWindowBindingSourceKind.Tag, TagId: { } tagId } && tags.TryGetValue(tagId, out var tag)
                    ? Ft100SceneExporter.NormalizeMappingDatatype(tag.Datatype)
                    : null)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (codes is [{ } code]) result[member.Name] = code;
        }

        return result.Count == 0 ? null : result;
    }

    private static Ft100QuickWindowMember ToManifestMember(QuickWindowInterfaceMember member) => new(
        Key(member.MemberKey),
        member.Name,
        member.Family.ToString(),
        member.DataType.ToString(),
        member.Access.ToString(),
        member.Required,
        member.DefaultValue,
        member.Description);

    /// <summary>Compiles the bounded presentation defaults of one definition into manifest shape.</summary>
    /// <remarks>
    /// The title is resolved here, not in the host. `FR-UI-03` gives a definition without an explicit
    /// title the `DisplayName` as its title, and that fallback is portable semantics: the deployed host
    /// reads `intent.title || presentation.Title || ""`, so a null here left the title bar empty while
    /// the Builder preview showed the name. The scene built above already used `EffectiveTitle`; only
    /// this projection dropped it.
    ///
    /// Decisions: DEC-0050.
    /// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §12.4.
    /// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowExporterTests.cs.
    /// </remarks>
    private static Ft100QuickWindowPresentation ToManifestPresentation(
        QuickWindowPresentationDefaults presentation,
        string displayName) => new(
        presentation.EffectiveTitle(displayName),
        presentation.Position.ToString(),
        presentation.Backdrop,
        presentation.IsDraggable,
        presentation.IsResizable,
        presentation.IsViewportConstrained,
        presentation.Chrome?.TitleBarColor,
        presentation.Chrome?.BorderColor,
        presentation.Chrome?.Shadow);

    private static Ft100QuickWindowInvocationEntry ToManifestInvocation(QuickWindowInvocation invocation) => new(
        Key(invocation.InvocationKey),
        Key(invocation.DefinitionKey),
        invocation.InterfaceVersion,
        invocation.EffectiveTitleOverride,
        invocation.OwnerElementId,
        invocation.OwnerCommandId,
        (invocation.Bindings ?? [])
            .OrderBy(binding => binding.MemberKey)
            .Select(ToManifestBinding)
            .ToArray());

    private static Ft100QuickWindowBinding ToManifestBinding(QuickWindowBinding binding) => new(
        Key(binding.MemberKey),
        binding.SourceKind.ToString(),
        binding.TagId,
        binding.LiteralValue,
        binding.Expression,
        binding.ParentMemberKey is { } parentKey ? Key(parentKey) : null,
        binding.ExpressionAst is null
            ? null
            : JsonSerializer.SerializeToElement(binding.ExpressionAst, Ft100SceneExporter.StateCommandJsonOptions));

    private static string Key(Guid value) => value.ToString("D");
}
