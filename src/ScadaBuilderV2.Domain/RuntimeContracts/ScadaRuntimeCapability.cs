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

/// <summary>One stable, versioned capability in the SCADA Builder V2 runtime contract.</summary>
/// <param name="Id">Stable lowercase dotted capability identifier.</param>
/// <param name="MinimumContractVersion">Minimum runtime-contract version that understands the capability.</param>
/// <param name="Owner">Single execution owner for the capability.</param>
/// <param name="Status">Current strict-export disposition.</param>
public sealed record ScadaRuntimeCapability(
    string Id,
    string MinimumContractVersion,
    ScadaRuntimeCapabilityOwner Owner,
    ScadaRuntimeCapabilityStatus Status);
