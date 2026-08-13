namespace ScadaBuilderV2.Domain.QuickWindows;

/// <summary>Result of validating a manifest profile against the QuickWindow V1 contract.</summary>
public sealed record QuickWindowProfileCompatibilityResult(bool IsCompatible, string Code, string Category);

/// <summary>Validates whether a manifest profile can carry QuickWindow V1 records.</summary>
/// <remarks>
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
