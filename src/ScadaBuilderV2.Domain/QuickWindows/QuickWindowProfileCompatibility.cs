namespace ScadaBuilderV2.Domain.QuickWindows;

/// <summary>Result of validating a manifest profile against the QuickWindow V1 contract.</summary>
public sealed record QuickWindowProfileCompatibilityResult(bool IsCompatible, string Code, string Category);

/// <summary>Validates whether a manifest profile can carry QuickWindow V1 records.</summary>
/// <remarks>
/// No longer called from project build validation: content authorisation moved to
/// <c>ScadaRuntimeCapabilityCatalog</c> (Task 8 of the 2026-09-08 format-versioning chantier), which is
/// authoritative regardless of the negotiated manifest profile. This type is kept for the narrower structural
/// fact it still states truthfully - a 2.1/2.2 manifest schema carries no QuickWindows section - and is
/// exercised directly by its own tests, not through <c>ValidateQuickWindows</c> any more.
///
/// Decisions: DEC-0050.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowContractHandshakeTests.cs.
/// </remarks>
public static class QuickWindowProfileCompatibility
{
    /// <summary>Validates one manifest profile; profiles 2.1 and 2.2 reject QuickWindow records fail-closed.</summary>
    public static QuickWindowProfileCompatibilityResult Validate(string? profile, bool containsQuickWindows)
    {
        if (!containsQuickWindows)
            return new QuickWindowProfileCompatibilityResult(true, "profile.compatible", "capability");
        return string.Equals(profile, "2.3", StringComparison.Ordinal)
            ? new QuickWindowProfileCompatibilityResult(true, "profile.compatible", "capability")
            : new QuickWindowProfileCompatibilityResult(false, "profile.quick-window-unsupported", "capability");
    }
}
