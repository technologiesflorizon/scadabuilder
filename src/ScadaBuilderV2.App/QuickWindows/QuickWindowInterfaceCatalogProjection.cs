using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// Projects the local interface of one definition into the selector catalog consumed by the state,
/// command, binding and expression authoring surfaces while a quick window is the active context.
/// </summary>
/// <remarks>
/// The content of a quick window never references a physical project tag (FR-031), so those selectors are
/// fed with the local members instead of the project `Catalogue Tags`. The projection is editor-only: it is
/// rebuilt from the definition on every activation and is never persisted nor exported.
///
/// Decisions: DEC-0050, FR-007, FR-031, FR-UI-15.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.1.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceAuthoringTests.cs.
/// </remarks>
public static class QuickWindowInterfaceCatalogProjection
{
    /// <summary>Schema marking a selector catalog that only carries local interface members.</summary>
    public const string Schema = "quick-window-local-interface/1.0";

    /// <summary>Device label shown by the selectors instead of a physical device.</summary>
    public const string DeviceLabel = "Interface locale";

    /// <summary>Projects every member of one definition into an editor-only selector catalog.</summary>
    public static ScadaTagCatalog Create(QuickWindowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var tags = definition.EffectiveInterfaceMembers
            .Select(member => new ScadaTagDefinition(
                member.Name,
                member.Name,
                KeywordLabel: member.Description,
                KeywordType: QuickWindowInterfaceLabels.For(member.Family),
                Device: DeviceLabel,
                Protocol: null,
                AddressUri: null,
                Datatype: QuickWindowInterfaceLabels.For(member.DataType),
                Writeable: member.Access is QuickWindowMemberAccess.Write or QuickWindowMemberAccess.ReadWrite,
                Enabled: true))
            .ToArray();
        return new ScadaTagCatalog(Schema, tags);
    }
}
