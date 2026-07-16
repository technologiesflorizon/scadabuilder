namespace ScadaBuilderV2.Rendering;

/// <summary>Controls the FT100 manifest contract emitted by an export operation.</summary>
/// <remarks>
/// Decisions: DEC-0047.
/// Contracts: docs/superpowers/specs/2026-07-16-scada-v2-tf100web-runtime-conformance-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs and Ft100PackageValidatorTests.cs.
/// </remarks>
public enum Ft100ManifestProfile
{
    /// <summary>Emits manifest 2.3 and blocks capabilities without strict runtime evidence.</summary>
    Strict23,

    /// <summary>Emits manifest 2.2 only for explicit compatibility fixtures.</summary>
    Compatibility22,

    /// <summary>Emits manifest 2.1 only for explicit compatibility fixtures.</summary>
    Compatibility21
}

/// <summary>Serialized runtime requirements carried by a strict FT100 manifest 2.3 package.</summary>
/// <param name="Version">Runtime capability contract version.</param>
/// <param name="RequiredCapabilities">Sorted, unique capability ids required by the package.</param>
/// <param name="RuntimeSha256">Lowercase SHA-256 of the runtime file packaged beside the manifest.</param>
public sealed record Ft100RuntimeContractManifest(
    string Version,
    IReadOnlyList<string> RequiredCapabilities,
    string RuntimeSha256);
