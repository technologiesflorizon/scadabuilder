namespace ScadaBuilderV2.Domain.RuntimeContracts;

/// <summary>Identifies the component that owns execution of one runtime capability.</summary>
/// <remarks>
/// Decisions: DEC-0047.
/// Contracts: docs/superpowers/specs/2026-07-16-scada-v2-tf100web-runtime-conformance-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/RuntimeContracts/ScadaRuntimeCapabilityCatalogTests.cs.
/// </remarks>
public enum ScadaRuntimeCapabilityOwner
{
    PackageTransport,
    SharedRuntime,
    Tf100WebHost
}

/// <summary>Describes whether one declared capability is currently eligible for strict export.</summary>
public enum ScadaRuntimeCapabilityStatus
{
    Supported,
    Blocked,
    Deprecated
}

/// <summary>
/// Identifies the executable tests that prove one capability in each required layer.
/// Empty evidence means the capability cannot be promoted to strict 2.3 support.
/// </summary>
/// <param name="BuilderTests">Builder model, renderer, exporter, or validator tests.</param>
/// <param name="SharedRuntimeTests">Portable runtime tests.</param>
/// <param name="Tf100WebTests">TF100Web intake, host, or end-to-end tests.</param>
public sealed record ScadaRuntimeCapabilityEvidence(
    IReadOnlyList<string> BuilderTests,
    IReadOnlyList<string> SharedRuntimeTests,
    IReadOnlyList<string> Tf100WebTests)
{
    /// <summary>Gets evidence with no executable test claims.</summary>
    public static ScadaRuntimeCapabilityEvidence Pending { get; } = new([], [], []);

    /// <summary>Gets whether all three required execution layers have at least one test.</summary>
    public bool IsComplete =>
        BuilderTests.Count > 0 &&
        SharedRuntimeTests.Count > 0 &&
        Tf100WebTests.Count > 0;
}

/// <summary>One stable, versioned capability in the SCADA Builder V2 runtime contract.</summary>
/// <param name="Id">Stable lowercase dotted capability identifier.</param>
/// <param name="MinimumContractVersion">Minimum runtime-contract version that understands the capability.</param>
/// <param name="Owner">Single execution owner for the capability.</param>
/// <param name="Status">Current strict-export disposition.</param>
/// <param name="Artifacts">Manifest, DOM, CSS, asset, or runtime artifacts consumed by the capability.</param>
/// <param name="FixtureId">Stable conformance fixture identifier.</param>
/// <param name="Evidence">Executable evidence required before strict support.</param>
public sealed record ScadaRuntimeCapability(
    string Id,
    string MinimumContractVersion,
    ScadaRuntimeCapabilityOwner Owner,
    ScadaRuntimeCapabilityStatus Status,
    IReadOnlyList<string> Artifacts,
    string FixtureId,
    ScadaRuntimeCapabilityEvidence Evidence);
