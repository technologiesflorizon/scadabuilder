using System.Text.RegularExpressions;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Domain.QuickWindows;

/// <summary>
/// Validates typed quick window bindings fail-closed before build/export.
/// Covers type, access, write authorization, interface version, required/optional neutrality and anti-injection.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-010.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBindingTests.cs.
/// </remarks>
public static class QuickWindowBindingValidator
{
    // Injection patterns that must be rejected without subscription or write.
    private static readonly string[] InjectionSubstrings =
    [
        "<script",
        "{{",
        "}}",
        "${",
        "javascript:",
        "[data-",
        "url(",
        "../",
        "C:\\",
        "C:/",
        "//",
        "\\\\"
        // Note: "#" and "." intentionally NOT globally blocked to allow numeric literals. See docs.
        // If strict single-char blocking is desired, add them here and adjust callers.
    ];

    // Additional single-char selectors that are blocked only when they appear as selector-like patterns.
    // For transport safety we reject raw "#" prefix or "." prefix typical of CSS selectors, not decimals.
    private static readonly Regex SelectorHashRegex = new(@"(^|\s)#\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SelectorDotRegex = new(@"(^|\s)\.\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Result of a single binding validation.</summary>
    public sealed record BindingValidationResult(bool IsValid, string? ErrorCode = null, string? Message = null, string? Category = null)
    {
        public static BindingValidationResult Valid => new(true);
        public static BindingValidationResult Invalid(string code, string message, string category = "validation") => new(false, code, message, category);
        public static BindingValidationResult InjectionRejected(string message) => new(false, "injection-rejected", message, "injection-rejected");
    }

    /// <summary>Validates one binding against its member and catalog.</summary>
    public static BindingValidationResult ValidateBinding(
        QuickWindowBinding binding,
        QuickWindowInterfaceMember member,
        ScadaTagCatalog? catalog,
        int definitionInterfaceVersion)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(member);

        if (binding.MemberKey != member.MemberKey)
            return BindingValidationResult.Invalid("binding.member-mismatch", $"Binding member key {binding.MemberKey} does not match member {member.MemberKey}.");

        if (definitionInterfaceVersion <= 0)
            return BindingValidationResult.Invalid("binding.interface-version-invalid", "Definition interface version must be greater than zero.", "interface-version");

        var shapeIssue = ValidateBindingShape(binding);
        if (shapeIssue is not null)
            return shapeIssue;

        if (binding.SourceKind == QuickWindowBindingSourceKind.None)
        {
            if (member.Required)
                return BindingValidationResult.Invalid("binding.required-missing", $"Required member '{member.Name}' has no binding.", "required");
            // Optional absent is neutral: no subscription nor write.
            return BindingValidationResult.Valid;
        }

        if (member.IsPrivate)
            return BindingValidationResult.Invalid("binding.private-member", $"Private member '{member.Name}' cannot be bound by an invocation.", "access");

        if (member.Family == QuickWindowInterfaceFamily.WriteCommand
            && binding.SourceKind is not QuickWindowBindingSourceKind.Tag and not QuickWindowBindingSourceKind.ParentPort)
        {
            return BindingValidationResult.Invalid("binding.write-source", $"WriteCommand member '{member.Name}' must bind to a Tag or ParentPort.", "access");
        }

        // Required members must have a concrete binding.
        // Validate source kind-specific rules.

        return binding.SourceKind switch
        {
            QuickWindowBindingSourceKind.Tag => ValidateTagBinding(binding, member, catalog),
            QuickWindowBindingSourceKind.Literal => ValidateLiteralBinding(binding, member),
            QuickWindowBindingSourceKind.Expression => ValidateExpressionBinding(binding, member, catalog),
            QuickWindowBindingSourceKind.ParentPort => ValidateParentPortBinding(binding, member),
            _ => BindingValidationResult.Invalid("binding.unknown-source", $"Unknown source kind {binding.SourceKind}.")
        };
    }

    /// <summary>Validates all bindings for an invocation against a definition.</summary>
    public static IReadOnlyList<BindingValidationResult> ValidateInvocation(
        QuickWindowInvocation invocation,
        QuickWindowDefinition definition,
        ScadaTagCatalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(definition);

        var results = new List<BindingValidationResult>();

        if (invocation.InvocationKey == Guid.Empty)
            results.Add(BindingValidationResult.Invalid("invocation.key-empty", "InvocationKey must be a non-empty GUID."));

        if (invocation.DefinitionKey != definition.DefinitionKey)
            results.Add(BindingValidationResult.Invalid("invocation.definition-mismatch", "Invocation definition key does not match definition."));

        if (invocation.InterfaceVersion != definition.InterfaceVersion)
            results.Add(BindingValidationResult.Invalid("invocation.interface-version-mismatch", $"Invocation interface version {invocation.InterfaceVersion} does not match definition version {definition.InterfaceVersion}.", "interface-version"));

        if (invocation.TitleOverride is { Length: > 256 })
            results.Add(BindingValidationResult.Invalid("invocation.title-too-long", "TitleOverride must not exceed 256 characters."));
        else if (!string.IsNullOrWhiteSpace(invocation.TitleOverride))
        {
            var titleInjection = CheckInjection(invocation.TitleOverride);
            if (titleInjection is not null)
                results.Add(titleInjection with { ErrorCode = "invocation.title-injection" });
        }

        if (invocation.Bindings is null)
        {
            results.Add(BindingValidationResult.Invalid("invocation.bindings-null", "Bindings must be an array, even when empty."));
            return results;
        }

        var members = definition.InterfaceMembers ?? Array.Empty<QuickWindowInterfaceMember>();
        var membersByKey = members.GroupBy(member => member.MemberKey).ToDictionary(group => group.Key, group => group.First());
        var bindingsByMember = invocation.Bindings.GroupBy(b => b.MemberKey).ToDictionary(g => g.Key, g => g.First());

        // Validate each member has at most one binding and required handling
        foreach (var member in members)
        {
            bindingsByMember.TryGetValue(member.MemberKey, out var binding);
            binding ??= QuickWindowBinding.Absent(member.MemberKey);
            var result = ValidateBinding(binding, member, catalog, definition.InterfaceVersion);
            if (!result.IsValid)
                results.Add(result);
        }

        // Check for orphan bindings referencing unknown members
        foreach (var binding in invocation.Bindings.Where(b => !membersByKey.ContainsKey(b.MemberKey)))
        {
            results.Add(BindingValidationResult.Invalid("binding.unknown-member", $"Binding references unknown member {binding.MemberKey}."));
        }

        // Check duplicate member keys
        var duplicates = invocation.Bindings.GroupBy(b => b.MemberKey).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var dup in duplicates)
            results.Add(BindingValidationResult.Invalid("binding.duplicate", $"Duplicate binding for member {dup}."));

        return results;
    }

    private static BindingValidationResult ValidateTagBinding(QuickWindowBinding binding, QuickWindowInterfaceMember member, ScadaTagCatalog? catalog)
    {
        var tagId = binding.TagId;
        if (string.IsNullOrWhiteSpace(tagId))
            return BindingValidationResult.Invalid("binding.tag-empty", $"Tag binding for '{member.Name}' is empty.");

        // Anti-injection also applies to tag ids? Tag ids are from catalog, not user free text, but we still guard.
        var injection = CheckInjection(tagId);
        if (injection is not null) return injection;

        if (catalog is null || catalog.Tags.Count == 0)
            return BindingValidationResult.Invalid("binding.catalog-missing", $"Tag '{tagId}' cannot be validated without a catalog.");

        var tag = catalog.Tags.FirstOrDefault(t => string.Equals(t.Id, tagId, StringComparison.Ordinal));
        if (tag is null)
            return BindingValidationResult.Invalid("binding.tag-missing", $"Tag '{tagId}' not found in catalog.");

        if (!tag.Enabled)
            return BindingValidationResult.Invalid("binding.tag-disabled", $"Tag '{tagId}' is disabled.");

        // Access check: member requiring write needs writeable tag.
        if (member.Access is QuickWindowMemberAccess.Write or QuickWindowMemberAccess.ReadWrite)
        {
            if (!tag.Writeable)
                return BindingValidationResult.Invalid("binding.tag-readonly", $"Member '{member.Name}' requires write access but tag '{tagId}' is read-only.");
        }

        // Type compatibility: crude mapping datatype string vs member DataType
        if (!IsTagDatatypeCompatible(tag.Datatype, member.DataType))
            return BindingValidationResult.Invalid("binding.type-mismatch", $"Tag '{tagId}' datatype '{tag.Datatype}' is not compatible with member '{member.Name}' type {member.DataType}.");

        return BindingValidationResult.Valid;
    }

    private static BindingValidationResult ValidateLiteralBinding(QuickWindowBinding binding, QuickWindowInterfaceMember member)
    {
        var literal = binding.LiteralValue;
        if (literal is null)
            return BindingValidationResult.Invalid("binding.literal-empty", $"Literal binding for '{member.Name}' is empty.");

        var injection = CheckInjection(literal);
        if (injection is not null) return injection;
        // Also check selector-like hashes/dots
        if (SelectorHashRegex.IsMatch(literal) || SelectorDotRegex.IsMatch(literal))
            return BindingValidationResult.InjectionRejected($"Literal '{literal}' contains selector-like pattern.");

        // Type validation
        if (!IsLiteralCompatible(literal, member.DataType))
            return BindingValidationResult.Invalid("binding.literal-type", $"Literal '{literal}' is not compatible with type {member.DataType}.");

        return BindingValidationResult.Valid;
    }

    private static BindingValidationResult ValidateExpressionBinding(QuickWindowBinding binding, QuickWindowInterfaceMember member, ScadaTagCatalog? catalog)
    {
        var expr = binding.Expression;
        if (string.IsNullOrWhiteSpace(expr))
            return BindingValidationResult.Invalid("binding.expression-empty", $"Expression binding for '{member.Name}' is empty.");

        var injection = CheckInjection(expr);
        if (injection is not null) return injection;
        if (SelectorHashRegex.IsMatch(expr) || SelectorDotRegex.IsMatch(expr))
            return BindingValidationResult.InjectionRejected($"Expression '{expr}' contains selector-like pattern.");

        var parsed = ScadaExpressionParser.Parse(expr);
        if (parsed.Root is null)
            return BindingValidationResult.Invalid("binding.expression-syntax", $"Expression for '{member.Name}' is invalid: {string.Join("; ", parsed.Errors)}.", "syntax");

        var expression = ScadaExpression.FromSource(expr);
        if (catalog is null || catalog.Tags.Count == 0)
            return BindingValidationResult.Invalid("binding.catalog-missing", $"Expression for '{member.Name}' cannot be validated without a tag catalog.");

        foreach (var tagRef in expression.ReferencedTags)
        {
            var resolved = ScadaExpressionValidator.TryResolveTagReference(tagRef, catalog);
            if (resolved.Status != TagResolveStatus.Resolved)
                return BindingValidationResult.Invalid("binding.expression-tag-missing", $"Expression references missing or ambiguous tag '{tagRef}'.");
        }

        return BindingValidationResult.Valid;
    }

    private static BindingValidationResult ValidateParentPortBinding(QuickWindowBinding binding, QuickWindowInterfaceMember member)
    {
        if (binding.ParentMemberKey is null || binding.ParentMemberKey == Guid.Empty)
            return BindingValidationResult.Invalid("binding.parent-empty", $"ParentPort binding for '{member.Name}' has no parent member key.");

        // Parent port is only valid for child windows; depth handling is done at dependency analyzer.
        // No injection check for GUID.

        return BindingValidationResult.Valid;
    }

    private static BindingValidationResult? CheckInjection(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var lower = value.ToLowerInvariant();
        foreach (var pattern in InjectionSubstrings)
        {
            if (lower.Contains(pattern.ToLowerInvariant()))
                return BindingValidationResult.InjectionRejected($"Value contains forbidden pattern '{pattern}'.");
        }

        // Also check HTML tag injection < and > balanced?
        if (lower.Contains("<script") || lower.Contains("javascript:"))
            return BindingValidationResult.InjectionRejected("Value contains HTML/JS injection.");

        return null;
    }

    private static bool IsTagDatatypeCompatible(string? tagDatatype, QuickWindowDataType memberType)
    {
        if (string.IsNullOrWhiteSpace(tagDatatype))
            return false;

        var dt = tagDatatype.Trim().ToLowerInvariant();
        return memberType switch
        {
            QuickWindowDataType.Boolean => dt is "bool" or "boolean" or "booléen" or "bit",
            QuickWindowDataType.Integer => dt is "int" or "int16" or "int32" or "int64" or "integer" or "dint" or "word",
            QuickWindowDataType.Decimal => dt is "float" or "float32" or "float64" or "double" or "decimal" or "real" or "analog",
            QuickWindowDataType.String => dt is "string" or "text" or "char",
            QuickWindowDataType.Enum => dt is "int" or "int16" or "int32" or "int64" or "integer" or "dint" or "word" or "string" or "text",
            _ => false
        };
    }

    private static bool IsLiteralCompatible(string literal, QuickWindowDataType type)
    {
        var trimmed = literal.Trim();
        return type switch
        {
            QuickWindowDataType.Boolean => bool.TryParse(trimmed, out _) || trimmed.Equals("vrai", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("faux", StringComparison.OrdinalIgnoreCase) || trimmed is "0" or "1",
            QuickWindowDataType.Integer => long.TryParse(trimmed, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _),
            QuickWindowDataType.Decimal => double.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) && double.IsFinite(d),
            QuickWindowDataType.String => true, // any string allowed after injection check; escaped downstream
            QuickWindowDataType.Enum => !string.IsNullOrWhiteSpace(trimmed),
            _ => false
        };
    }

    private static BindingValidationResult? ValidateBindingShape(QuickWindowBinding binding)
    {
        var populated = new[]
        {
            binding.TagId is not null,
            binding.LiteralValue is not null,
            binding.Expression is not null,
            binding.ParentMemberKey is not null
        };
        var expectedIndex = binding.SourceKind switch
        {
            QuickWindowBindingSourceKind.Tag => 0,
            QuickWindowBindingSourceKind.Literal => 1,
            QuickWindowBindingSourceKind.Expression => 2,
            QuickWindowBindingSourceKind.ParentPort => 3,
            QuickWindowBindingSourceKind.None => -1,
            _ => -2
        };
        if (expectedIndex == -2)
            return BindingValidationResult.Invalid("binding.unknown-source", $"Unknown source kind {binding.SourceKind}.");
        if (expectedIndex == -1 && populated.Any(value => value))
            return BindingValidationResult.Invalid("binding.ambiguous-payload", "An absent binding must not carry source data.", "shape");
        if (expectedIndex >= 0 && populated.Where((value, index) => value && index != expectedIndex).Any())
            return BindingValidationResult.Invalid("binding.ambiguous-payload", $"Binding source {binding.SourceKind} carries fields for another source kind.", "shape");
        return null;
    }
}
