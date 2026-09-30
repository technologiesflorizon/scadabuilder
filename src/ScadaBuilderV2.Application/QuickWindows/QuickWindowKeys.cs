using System.Security.Cryptography;
using System.Text;

namespace ScadaBuilderV2.Application.QuickWindows;

/// <summary>Deterministic keys of lowered quick-window structures (ruling R4).</summary>
/// <remarks>
/// Same derivation as <c>PageIdentityModels.CreateDeterministic</c>: SHA-256 of a canonical string, first
/// 16 bytes, version 5 and RFC 4122 variant bits. Two lowerings of the same project therefore produce the
/// same keys.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §3 (R4), §6.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetLoweringTests.cs.
/// </remarks>
public static class QuickWindowKeys
{
    /// <summary>Key of the interface member lowered from placeholder <paramref name="number"/> of a page.</summary>
    public static Guid Member(Guid pageKey, int number) =>
        Create($"scada-builder-v2-qw-member|{pageKey:N}|{number}");

    /// <summary>Key of the invocation lowered from one open-quick-window command.</summary>
    public static Guid Invocation(Guid ownerPageKey, string elementId, string commandId) =>
        Create($"scada-builder-v2-qw-invocation|{ownerPageKey:N}|{elementId}|{commandId}");

    private static Guid Create(string canonical)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
