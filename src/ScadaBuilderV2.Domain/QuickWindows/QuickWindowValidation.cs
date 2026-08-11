using ScadaBuilderV2.Domain.Projects;

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

        // Ensure no fourth identifier is introduced.
        // DefinitionKey, InvocationKey and RuntimeInstanceId are the only identities.
        // This is a structural guard: QuickWindowDefinition must not expose InstanceKey.
        var type = typeof(QuickWindowDefinition);
        if (type.GetProperties().Any(p => string.Equals(p.Name, "InstanceKey", StringComparison.OrdinalIgnoreCase)))
            issues.Add("QuickWindow must not introduce InstanceKey; only DefinitionKey, InvocationKey and RuntimeInstanceId are allowed.");

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

        // Chrome is bounded: only TitleBarColor, BorderColor, Shadow. X/geometry/behaviors/backdrop remain theme/host.
        // No validation of colors here; host validates format later.
    }

    private static void ValidateMembers(IReadOnlyList<QuickWindowInterfaceMember>? members, List<string> issues)
    {
        if (members is null)
            return;

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

            // Access validation: ReadState expects Read, WriteCommand expects Write etc. Allow flexible but ensure coherence.
            if (member.Family == QuickWindowInterfaceFamily.ReadState && member.Access != QuickWindowMemberAccess.Read && member.Access != QuickWindowMemberAccess.ReadWrite)
                issues.Add($"ReadState member '{member.Name}' must have Read or ReadWrite access.");

            if (member.Family == QuickWindowInterfaceFamily.WriteCommand && member.Access != QuickWindowMemberAccess.Write && member.Access != QuickWindowMemberAccess.ReadWrite)
                issues.Add($"WriteCommand member '{member.Name}' must have Write or ReadWrite access.");

            if (member.Family == QuickWindowInterfaceFamily.PrivateConstant && !string.IsNullOrWhiteSpace(member.DefaultValue) == false)
            {
                // Private constants are fixed by definition; they may have DefaultValue but are not bound by caller.
                // No extra validation here.
            }
        }
    }

    /// <summary>Validates that a fourth identifier is not introduced in QuickWindow domain.</summary>
    public static bool HasFourthIdentifierViolation()
    {
        var forbidden = new[] { "InstanceKey", "WindowInstanceKey", "QuickWindowInstanceKey" };
        var types = new[] { typeof(QuickWindowDefinition) };
        foreach (var t in types)
        {
            foreach (var prop in t.GetProperties())
            {
                if (forbidden.Any(f => string.Equals(f, prop.Name, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }
        // Also check invocation if loaded
        var invocationType = Type.GetType("ScadaBuilderV2.Domain.QuickWindows.QuickWindowInvocation, ScadaBuilderV2.Domain");
        if (invocationType != null)
        {
            foreach (var prop in invocationType.GetProperties())
            {
                if (forbidden.Any(f => string.Equals(f, prop.Name, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }
        return false;
    }
}
