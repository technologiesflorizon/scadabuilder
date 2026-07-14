using System.Security.Cryptography;
using System.Text;

namespace ScadaBuilderV2.Domain.Projects;

/// <summary>Identifies whether a page is native to Scada+ or backed by an imported source.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/PageIdentityTests.cs.
/// </remarks>
public enum PageOrigin
{
    Native,
    Imported
}

/// <summary>Describes the optional external source from which a modern page was imported.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md, docs/07_legacy_migration/LEGACY_SOURCE_POLICY_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/PageIdentityTests.cs, tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs.
/// </remarks>
public sealed record ImportProvenance(
    string SourceSystem,
    string? SourceProjectId = null,
    string? SourcePageId = null,
    string? SourcePath = null);

/// <summary>Creates immutable logical page keys for new and migrated pages.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/PageIdentityTests.cs.
/// </remarks>
public static class PageKeyFactory
{
    /// <summary>Creates a new non-empty logical page key.</summary>
    public static Guid CreateNew() => Guid.NewGuid();

    /// <summary>Creates a stable logical key for one legacy page identity.</summary>
    public static Guid CreateDeterministic(string projectName, string legacyPageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyPageId);

        var canonical = $"scada-builder-v2-page|{projectName.Trim().ToLowerInvariant()}|{legacyPageId.Trim().ToLowerInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
