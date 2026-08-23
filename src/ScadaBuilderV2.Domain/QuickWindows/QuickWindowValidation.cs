using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Scenes;
using System.Text.RegularExpressions;

namespace ScadaBuilderV2.Domain.QuickWindows;

/// <summary>
/// Validates persistent quick window contracts fail-closed before build/export.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-005, FR-006, FR-017, FR-018, FR-019, FR-020, FR-027.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowDomainTests.cs.
/// </remarks>
public static class QuickWindowValidation
{
    /// <summary>Validates a definition and returns all issues. Empty list means valid.</summary>
    public static IReadOnlyList<string> ValidateDefinition(QuickWindowDefinition definition, IReadOnlySet<string>? existingCodes = null, string? currentCode = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var issues = new List<string>();

        if (definition.DefinitionKey == Guid.Empty)
            issues.Add("QuickWindow DefinitionKey must be a non-empty GUID.");

        var codeResult = PageCodePolicy.Validate(definition.Code, existingCodes, currentCode);
        if (!codeResult.IsValid)
            issues.AddRange(codeResult.Errors.Select(e => $"Code: {e}"));

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
            issues.Add("DisplayName is required.");

        if (definition.InterfaceVersion <= 0)
            issues.Add("InterfaceVersion must be greater than zero.");

        if (definition.Content is null)
            issues.Add("VisualContent is required.");
        else
            ValidateVisualContent(definition.Content, issues);

        ValidatePresentation(definition.PresentationDefaults, issues);

        ValidateMembers(definition.InterfaceMembers, issues);
        ValidateQuickWindowCommands(definition.Content?.EffectiveElements ?? Array.Empty<ScadaElement>(), issues);

        // Ensure no fourth identifier is introduced.
        // DefinitionKey, InvocationKey and RuntimeInstanceId are the only identities.
        // This is a structural guard: QuickWindowDefinition must not expose InstanceKey.
        var type = typeof(QuickWindowDefinition);
        if (type.GetProperties().Any(p => string.Equals(p.Name, "InstanceKey", StringComparison.OrdinalIgnoreCase)))
            issues.Add("QuickWindow must not introduce InstanceKey; only DefinitionKey, InvocationKey and RuntimeInstanceId are allowed.");

        return issues;
    }

    /// <summary>
    /// Validates one interface member in isolation and against its siblings, without requiring a full definition.
    /// Authoring surfaces use it to refuse an invalid member before any workspace mutation is prepared.
    /// </summary>
    /// <param name="member">The candidate member.</param>
    /// <param name="siblings">The other members of the same definition; the candidate itself is ignored when present.</param>
    /// <returns>Every issue found; an empty list means the member is valid.</returns>
    /// <remarks>
    /// Decisions: DEC-0050, FR-005, FR-006.
    /// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceAuthoringTests.cs.
    /// </remarks>
    public static IReadOnlyList<string> ValidateMember(
        QuickWindowInterfaceMember member,
        IEnumerable<QuickWindowInterfaceMember>? siblings = null)
    {
        ArgumentNullException.ThrowIfNull(member);
        var issues = new List<string>();
        ValidateMembers([member], issues);

        foreach (var sibling in (siblings ?? Array.Empty<QuickWindowInterfaceMember>())
                     .Where(candidate => candidate.MemberKey != member.MemberKey))
        {
            if (sibling.MemberKey == member.MemberKey)
                continue;

            if (string.Equals(sibling.Name, member.Name, StringComparison.OrdinalIgnoreCase))
                issues.Add($"Duplicate member Name '{member.Name}'.");
        }

        return issues;
    }

    /// <summary>Validates presentation defaults. Any extra placement per invocation must be rejected elsewhere.</summary>
    public static IReadOnlyList<string> ValidatePresentation(QuickWindowPresentationDefaults? presentation)
    {
        var issues = new List<string>();
        ValidatePresentation(presentation, issues);
        return issues;
    }

    /// <summary>Validates visual content bounded contract.</summary>
    public static void ValidateVisualContent(VisualContent content, List<string> issues)
    {
        if (content.CanvasSize is null)
            issues.Add("VisualContent.CanvasSize is required.");
        else if (content.CanvasSize.Width <= 0 || content.CanvasSize.Height <= 0)
            issues.Add("VisualContent.CanvasSize must have positive dimensions.");

        ValidateReferences(content.EffectiveStyleSheets, "StyleSheets", issues);
        ValidateReferences(content.EffectiveAssetReferences, "AssetReferences", issues);

        // VisualContent must be bounded: only canvas, background, elements, styles, assets.
        // No navigation, route, header/footer, interface, presentation, lifecycle.
        // This is ensured by type composition: VisualContent has no such properties.
    }

    private static void ValidatePresentation(QuickWindowPresentationDefaults? presentation, List<string> issues)
    {
        if (presentation is null)
            return;

        if (presentation.Position != QuickWindowPosition.Center)
            issues.Add("QuickWindowPosition must be Center in V1; free placement per invocation is not allowed.");

        if (!presentation.IsDraggable)
            issues.Add("IsDraggable must be true in V1 (FR-UI-05).");

        if (presentation.IsResizable)
            issues.Add("IsResizable must be false in V1 (FR-UI-05).");

        if (!presentation.IsViewportConstrained)
            issues.Add("IsViewportConstrained must be true in V1 (FR-UI-06).");

        if (presentation.Title is { Length: > 256 })
            issues.Add("Presentation title must not exceed 256 characters.");

        if (presentation.Chrome is { } chrome)
        {
            ValidateColor(chrome.TitleBarColor, "Chrome.TitleBarColor", issues);
            ValidateColor(chrome.BorderColor, "Chrome.BorderColor", issues);
            if (chrome.Shadow is { Length: > 256 } || (chrome.Shadow is not null && ContainsUnsafeTransportValue(chrome.Shadow)))
                issues.Add("Chrome.Shadow contains an invalid or unsafe value.");
        }
    }

    private static void ValidateMembers(IReadOnlyList<QuickWindowInterfaceMember>? members, List<string> issues)
    {
        if (members is null)
        {
            issues.Add("InterfaceMembers is required and must be an array.");
            return;
        }

        var seenKeys = new HashSet<Guid>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in members)
        {
            if (member.MemberKey == Guid.Empty)
                issues.Add($"Member '{member.Name}' MemberKey must be non-empty.");
            else if (!seenKeys.Add(member.MemberKey))
                issues.Add($"Duplicate MemberKey '{member.MemberKey}'.");

            if (string.IsNullOrWhiteSpace(member.Name))
                issues.Add($"Member '{member.MemberKey}' Name is required.");
            else if (!seenNames.Add(member.Name))
                issues.Add($"Duplicate member Name '{member.Name}'.");

            // Required only for public ports; private variables/constants are always optional.
            if (member.IsPrivate && member.Required)
                issues.Add($"Private member '{member.Name}' cannot be Required.");

            if (member.Family == QuickWindowInterfaceFamily.ReadState && member.Access != QuickWindowMemberAccess.Read)
                issues.Add($"ReadState member '{member.Name}' must have Read access.");

            if (member.Family == QuickWindowInterfaceFamily.WriteCommand && member.Access != QuickWindowMemberAccess.Write)
                issues.Add($"WriteCommand member '{member.Name}' must have Write access.");

            if (member.Family == QuickWindowInterfaceFamily.PublicParameter && member.Access is not QuickWindowMemberAccess.Read and not QuickWindowMemberAccess.ReadWrite)
                issues.Add($"PublicParameter member '{member.Name}' must have Read or ReadWrite access.");

            if (member.IsPrivate && member.Access != QuickWindowMemberAccess.Internal)
                issues.Add($"Private member '{member.Name}' must have Internal access.");

            if (member.Family == QuickWindowInterfaceFamily.PrivateConstant && string.IsNullOrWhiteSpace(member.DefaultValue))
                issues.Add($"PrivateConstant member '{member.Name}' requires a fixed DefaultValue.");

            if (member.DefaultValue is not null && !IsLiteralCompatible(member.DefaultValue, member.DataType))
                issues.Add($"DefaultValue for member '{member.Name}' is not compatible with {member.DataType}.");
        }
    }

    /// <summary>Validates that a fourth identifier is not introduced in QuickWindow domain.</summary>
    public static bool HasFourthIdentifierViolation()
    {
        var forbidden = new[] { "InstanceKey", "WindowInstanceKey", "QuickWindowInstanceKey" };
        var types = typeof(QuickWindowDefinition).Assembly.GetTypes()
            .Where(type => string.Equals(type.Namespace, typeof(QuickWindowDefinition).Namespace, StringComparison.Ordinal))
            .ToArray();
        foreach (var t in types)
        {
            foreach (var prop in t.GetProperties())
            {
                if (forbidden.Any(f => string.Equals(f, prop.Name, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }
        return false;
    }

    private static void ValidateQuickWindowCommands(IEnumerable<ScadaElement> elements, List<string> issues)
    {
        foreach (var element in FlattenElements(elements))
        {
            foreach (var command in element.EffectiveCommandConfig.Commands.Where(command => command.Kind == ScadaCommandKind.CloseQuickWindow))
            {
                if (command.TargetPageKey is not null || !string.IsNullOrWhiteSpace(command.TargetPageId) || command.QuickWindowInvocationKey is not null)
                    issues.Add($"CloseQuickWindow command '{command.Id}' inside quick-window content must target Self implicitly and carry no page or invocation target.");
            }
        }
    }

    private static IEnumerable<ScadaElement> FlattenElements(IEnumerable<ScadaElement> elements)
    {
        foreach (var element in elements)
        {
            yield return element;
            foreach (var child in FlattenElements(element.ChildElements))
                yield return child;
        }
    }

    private static void ValidateReferences(IEnumerable<string> references, string property, List<string> issues)
    {
        foreach (var reference in references)
        {
            if (string.IsNullOrWhiteSpace(reference)
                || Path.IsPathRooted(reference)
                || reference.Contains("..", StringComparison.Ordinal)
                || reference.Contains(':', StringComparison.Ordinal)
                || ContainsUnsafeTransportValue(reference))
            {
                issues.Add($"VisualContent.{property} contains an invalid project-relative reference '{reference}'.");
            }
        }
    }

    private static void ValidateColor(string? color, string property, List<string> issues)
    {
        if (color is not null && !Regex.IsMatch(color, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$", RegexOptions.CultureInvariant))
            issues.Add($"{property} must be #RRGGBB or #RRGGBBAA.");
    }

    private static bool ContainsUnsafeTransportValue(string value) =>
        value.Contains("<script", StringComparison.OrdinalIgnoreCase)
        || value.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
        || value.Contains("url(", StringComparison.OrdinalIgnoreCase)
        || value.Contains("{{", StringComparison.Ordinal)
        || value.Contains("${", StringComparison.Ordinal);

    private static bool IsLiteralCompatible(string literal, QuickWindowDataType type)
    {
        var trimmed = literal.Trim();
        return type switch
        {
            QuickWindowDataType.Boolean => bool.TryParse(trimmed, out _) || trimmed is "0" or "1",
            QuickWindowDataType.Integer => long.TryParse(trimmed, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _),
            QuickWindowDataType.Decimal => double.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) && double.IsFinite(value),
            QuickWindowDataType.String => !ContainsUnsafeTransportValue(literal),
            QuickWindowDataType.Enum => !string.IsNullOrWhiteSpace(trimmed),
            _ => false
        };
    }
}
