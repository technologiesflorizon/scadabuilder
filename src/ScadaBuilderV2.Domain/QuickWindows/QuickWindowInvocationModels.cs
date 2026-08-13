using System.Text.Json.Serialization;

namespace ScadaBuilderV2.Domain.QuickWindows;

/// <summary>
/// Source kind for a typed port binding.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-007, FR-009, FR-010.
/// </remarks>
public enum QuickWindowBindingSourceKind
{
    /// <summary>Explicit absence: no subscription nor write.</summary>
    None = 0,
    /// <summary>Bound to a catalog tag id.</summary>
    Tag = 1,
    /// <summary>Bound to a typed literal value.</summary>
    Literal = 2,
    /// <summary>Bound to an authorized expression.</summary>
    Expression = 3,
    /// <summary>Bound to a public port of the parent window.</summary>
    ParentPort = 4
}

/// <summary>
/// One typed port binding for a single interface member.
/// Neutral absence is explicit (None) and creates no runtime subscription.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-009, FR-010.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §8.4.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBindingTests.cs.
/// </remarks>
public sealed record QuickWindowBinding(
    Guid MemberKey,
    QuickWindowBindingSourceKind SourceKind,
    string? TagId = null,
    string? LiteralValue = null,
    string? Expression = null,
    Guid? ParentMemberKey = null)
{
    /// <summary>Gets whether this binding is explicitly absent.</summary>
    [JsonIgnore]
    public bool IsAbsent => SourceKind == QuickWindowBindingSourceKind.None;

    /// <summary>Creates an absent binding.</summary>
    public static QuickWindowBinding Absent(Guid memberKey) => new(memberKey, QuickWindowBindingSourceKind.None);

    /// <summary>Creates a tag binding.</summary>
    public static QuickWindowBinding FromTag(Guid memberKey, string tagId) => new(memberKey, QuickWindowBindingSourceKind.Tag, TagId: tagId);

    /// <summary>Creates a literal binding.</summary>
    public static QuickWindowBinding FromLiteral(Guid memberKey, string literal) => new(memberKey, QuickWindowBindingSourceKind.Literal, LiteralValue: literal);

    /// <summary>Creates an expression binding.</summary>
    public static QuickWindowBinding FromExpression(Guid memberKey, string expression) => new(memberKey, QuickWindowBindingSourceKind.Expression, Expression: expression);

    /// <summary>Creates a parent-port binding.</summary>
    public static QuickWindowBinding FromParentPort(Guid memberKey, Guid parentMemberKey) => new(memberKey, QuickWindowBindingSourceKind.ParentPort, ParentMemberKey: parentMemberKey);
}

/// <summary>
/// Persistent invocation: concrete, typed bindings for one QuickWindowDefinition.
/// Compiled invocation is not yet a runtime instance.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-008, FR-015, FR-027.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBindingTests.cs.
/// </remarks>
public sealed record QuickWindowInvocation(
    Guid InvocationKey,
    Guid DefinitionKey,
    IReadOnlyList<QuickWindowBinding> Bindings,
    string? TitleOverride = null,
    int InterfaceVersion = 1,
    Guid? OwnerPageKey = null,
    string? OwnerElementId = null,
    string? OwnerCommandId = null)
{
    /// <summary>Gets effective title override, null means use definition default.</summary>
    [JsonIgnore]
    public string? EffectiveTitleOverride => string.IsNullOrWhiteSpace(TitleOverride) ? null : TitleOverride;

    /// <summary>Creates an empty invocation for a definition.</summary>
    public static QuickWindowInvocation CreateEmpty(Guid definitionKey, string? titleOverride = null)
    {
        return new QuickWindowInvocation(Guid.NewGuid(), definitionKey, Array.Empty<QuickWindowBinding>(), titleOverride);
    }

    /// <summary>Returns a binding for a member, or null if absent.</summary>
    public QuickWindowBinding? FindBinding(Guid memberKey) => (Bindings ?? Array.Empty<QuickWindowBinding>()).FirstOrDefault(b => b.MemberKey == memberKey);
}
