namespace ScadaBuilderV2.Rendering.QuickWindows;

/// <summary>One compiled interface member of a quick-window definition, in manifest PascalCase.</summary>
/// <remarks>
/// The manifest keeps the .NET PascalCase field contract; the runtime JSON embedded in HTML attributes keeps
/// the Builder camelCase contract. The two casings are never collapsed into one rule.
///
/// Decisions: DEC-0050.
/// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §12.4.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowExporterTests.cs.
/// </remarks>
public sealed record Ft100QuickWindowMember(
    string MemberKey,
    string Name,
    string Family,
    string DataType,
    string Access,
    bool Required,
    string? DefaultValue,
    string? Description);

/// <summary>Bounded presentation defaults compiled for one definition.</summary>
public sealed record Ft100QuickWindowPresentation(
    string? Title,
    string Position,
    bool Backdrop,
    bool IsDraggable,
    bool IsResizable,
    bool IsViewportConstrained,
    string? TitleBarColor,
    string? BorderColor,
    string? Shadow);

/// <summary>One compiled quick-window definition entry of the manifest `QuickWindows[]` registry.</summary>
/// <param name="DefinitionKey">Durable identity of the definition.</param>
/// <param name="Code">Portable project-local code.</param>
/// <param name="DisplayName">Authoring display name.</param>
/// <param name="InterfaceVersion">Current local-interface version.</param>
/// <param name="Namespace">Deterministic DOM/CSS namespace `qw-&lt;key8&gt;`.</param>
/// <param name="RelativePath">Package-relative HTML path of the definition content.</param>
/// <param name="CssRelativePath">Package-relative CSS path of the definition content.</param>
/// <param name="Width">Canvas width of the definition content.</param>
/// <param name="Height">Canvas height of the definition content.</param>
/// <param name="InterfaceMembers">Local interface, ordered by member key.</param>
/// <param name="PresentationDefaults">Bounded presentation defaults.</param>
public sealed record Ft100QuickWindowDefinitionEntry(
    string DefinitionKey,
    string Code,
    string DisplayName,
    int InterfaceVersion,
    string Namespace,
    string RelativePath,
    string CssRelativePath,
    double Width,
    double Height,
    IReadOnlyList<Ft100QuickWindowMember> InterfaceMembers,
    Ft100QuickWindowPresentation PresentationDefaults);

/// <summary>One compiled typed binding of an invocation.</summary>
public sealed record Ft100QuickWindowBinding(
    string MemberKey,
    string SourceKind,
    string? TagId,
    string? LiteralValue,
    string? Expression,
    string? ParentMemberKey);

/// <summary>One compiled invocation entry of the manifest `QuickWindowInvocations[]` registry.</summary>
/// <param name="InvocationKey">Durable identity referenced by the caller command.</param>
/// <param name="DefinitionKey">Target definition.</param>
/// <param name="InterfaceVersion">Interface version this invocation was aligned on.</param>
/// <param name="TitleOverride">Optional per-invocation title.</param>
/// <param name="OwnerPageKey">Caller page, when the invocation is attached.</param>
/// <param name="OwnerElementId">Caller element id.</param>
/// <param name="OwnerCommandId">Caller command id.</param>
/// <param name="Bindings">Typed bindings, ordered by member key.</param>
public sealed record Ft100QuickWindowInvocationEntry(
    string InvocationKey,
    string DefinitionKey,
    int InterfaceVersion,
    string? TitleOverride,
    string? OwnerPageKey,
    string? OwnerElementId,
    string? OwnerCommandId,
    IReadOnlyList<Ft100QuickWindowBinding> Bindings);

/// <summary>One compiled quick-window content file pair, ready to be written under the package root.</summary>
/// <param name="RelativeHtmlPath">Package-relative HTML path.</param>
/// <param name="Html">HTML document of the definition content.</param>
/// <param name="RelativeCssPath">Package-relative CSS path.</param>
/// <param name="Css">Stylesheet of the definition content.</param>
public sealed record Ft100QuickWindowContentFile(
    string RelativeHtmlPath,
    string Html,
    string RelativeCssPath,
    string Css);

/// <summary>Complete deterministic compilation of the quick windows of one project.</summary>
/// <param name="Definitions">`QuickWindows[]` registry, ordered by definition key.</param>
/// <param name="Invocations">`QuickWindowInvocations[]` registry, ordered by invocation key.</param>
/// <param name="Files">Content files to write, ordered by relative path.</param>
/// <param name="Warnings">Non blocking rendering warnings.</param>
public sealed record Ft100QuickWindowCompilation(
    IReadOnlyList<Ft100QuickWindowDefinitionEntry> Definitions,
    IReadOnlyList<Ft100QuickWindowInvocationEntry> Invocations,
    IReadOnlyList<Ft100QuickWindowContentFile> Files,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Gets the compilation of a project without any quick window.</summary>
    public static Ft100QuickWindowCompilation Empty { get; } = new([], [], [], []);

    /// <summary>Gets whether the project carries any quick window at all.</summary>
    public bool IsEmpty => Definitions.Count == 0 && Invocations.Count == 0;
}
