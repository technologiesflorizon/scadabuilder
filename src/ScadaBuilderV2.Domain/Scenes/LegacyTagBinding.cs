namespace ScadaBuilderV2.Domain.Scenes;

/// <summary>
/// The one rule deciding whether an element still carries a legacy <c>Data.TagBinding</c> value binding.
/// </summary>
/// <remarks>
/// <c>TagBinding</c> is a legacy, read-only field (DEC-0053): it is preserved by save/reload for existing
/// projects and still emitted as the read tag of a quick-window fragment (contract §11b.2), but the Builder never
/// exports it on a normal page, so it is inert there. The modern equivalent is the read binding
/// <c>Data.ReadTagId</c> (« Lecture de variable »). A Custom element's <c>TagBinding</c> records the library
/// <c>.sep</c> it was instantiated from, never a tag, and a legacy static element is not an Element+ object, so
/// neither carries a legacy binding.
///
/// Decisions: DEC-0053, DEC-0052.
/// Contracts: docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md §2 règle 5,
/// docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md §11b.2.
/// Tests: tests/ScadaBuilderV2.Tests/LegacyTagBindingValidationTests.cs,
/// tests/ScadaBuilderV2.Tests/LegacyTagBindingConverterTests.cs.
/// </remarks>
public static class LegacyTagBinding
{
    /// <summary>
    /// Returns the trimmed legacy <c>Data.TagBinding</c> of <paramref name="element"/>, or <see langword="null"/>
    /// when it has none, when it is a Custom element (library reference) or a legacy static element.
    /// </summary>
    public static string? Of(ScadaElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.Kind == ScadaElementKind.Custom || element.IsLegacyStatic)
        {
            return null;
        }

        var tagBinding = element.Data?.TagBinding;
        return string.IsNullOrWhiteSpace(tagBinding) ? null : tagBinding.Trim();
    }

    /// <summary>
    /// Returns whether <paramref name="element"/> already reads a tag through <c>Data.ReadTagId</c>, which wins
    /// over any legacy <c>TagBinding</c> it still carries.
    /// </summary>
    public static bool HasReadTagId(ScadaElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return !string.IsNullOrWhiteSpace(element.Data?.ReadTagId);
    }
}
