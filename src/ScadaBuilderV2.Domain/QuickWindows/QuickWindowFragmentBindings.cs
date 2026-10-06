using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Domain.QuickWindows;

/// <summary>
/// The one rule deciding which legacy value binding a quick-window definition fragment must carry itself,
/// because no host injects it there.
/// </summary>
/// <remarks>
/// On a page, TF100Web reads <c>Data.TagBinding</c> from the page's manifest objects and injects the resolved
/// mapping into the composed fragment server side. A quick-window fragment is served without any injection
/// and its definition has no manifest objects, so the exporter emits that binding as
/// <c>data-scada-read-tag</c> in the fragment, and the capability analyzer declares it. Both read this rule so
/// the emitted markup and the negotiated contract can never disagree. <c>Data.ReadTagId</c> wins when both are
/// set (the page path gives <c>ValueBindings.ReadTagId</c> the same precedence), and a Custom element's
/// <c>TagBinding</c> records the library <c>.sep</c> it was instantiated from, never a tag.
///
/// Decisions: DEC-0050, DEC-0052.
/// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §11b.2.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowFragmentValueBindingTests.cs.
/// </remarks>
public static class QuickWindowFragmentBindings
{
    /// <summary>
    /// Returns the trimmed <c>Data.TagBinding</c> a quick-window fragment must emit as its read tag, or
    /// <see langword="null"/> when there is none, when <c>Data.ReadTagId</c> already provides the read tag, or
    /// when <paramref name="element"/> is a Custom element.
    /// </summary>
    public static string? InlineTagBinding(ScadaElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var data = element.Data;
        if (data is null || element.Kind == ScadaElementKind.Custom || !string.IsNullOrWhiteSpace(data.ReadTagId))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(data.TagBinding) ? null : data.TagBinding.Trim();
    }
}
