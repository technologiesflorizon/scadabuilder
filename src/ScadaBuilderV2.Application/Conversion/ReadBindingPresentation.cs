using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Conversion;

/// <summary>What the « Lecture de variable » section of the State tab shows for one element.</summary>
/// <param name="Text">The French summary line.</param>
/// <param name="HasReadVariable">Whether <c>StateConfig.ReadVariable</c> is configured (editable through its dialog).</param>
/// <param name="HasReadBinding">Whether the element reads a tag at all (<c>ReadVariable</c> or <c>Data.ReadTagId</c>), so it can be removed.</param>
public sealed record ReadBindingSummary(string Text, bool HasReadVariable, bool HasReadBinding);

/// <summary>
/// The one rule the State tab of both property surfaces uses to show and remove an element's read binding, whether
/// it is a <c>StateConfig.ReadVariable</c> or only a <c>Data.ReadTagId</c> value binding.
/// </summary>
/// <remarks>
/// A converted legacy <c>TagBinding</c> lands in <c>Data.ReadTagId</c> only: writing <c>StateConfig.ReadVariable</c>
/// would declare the <c>state.read-variable</c> capability, which the legacy binding never did. Without this rule
/// the section would keep saying « aucune » right after a conversion. Removing the read binding clears both, so a
/// read binding the section shows can always be taken away.
///
/// Decisions: DEC-0053.
/// Contracts: docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md §2 règle 21.
/// Tests: tests/ScadaBuilderV2.Tests/LegacyTagBindingConverterTests.cs.
/// </remarks>
public static class ReadBindingPresentation
{
    /// <summary>The summary shown when the element reads no tag.</summary>
    public const string NoneText = "Aucune lecture de variable configuree.";

    /// <summary>Describes the read binding of <paramref name="element"/>, labelling tags with <paramref name="formatTag"/>.</summary>
    public static ReadBindingSummary Describe(ScadaElement? element, Func<string, string> formatTag)
    {
        ArgumentNullException.ThrowIfNull(formatTag);
        var readVariable = element?.EffectiveStateConfig.ReadVariable;
        if (readVariable is not null)
        {
            var format = string.IsNullOrWhiteSpace(readVariable.DisplayFormat) ? "" : $" -> {readVariable.DisplayFormat}";
            return new ReadBindingSummary($"Lecture: {formatTag(readVariable.TagId)}{format}", true, true);
        }

        var readTagId = element?.Data?.ReadTagId;
        if (!string.IsNullOrWhiteSpace(readTagId))
        {
            return new ReadBindingSummary($"Lecture de variable : {formatTag(readTagId.Trim())} (liaison ReadTagId)", false, true);
        }

        return new ReadBindingSummary(NoneText, false, false);
    }

    /// <summary>
    /// Returns <paramref name="scene"/> without any read binding on element <paramref name="elementId"/>: no
    /// <c>StateConfig.ReadVariable</c> and no <c>Data.ReadTagId</c>.
    /// </summary>
    public static ScadaScene WithoutReadBinding(ScadaScene scene, string elementId)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);

        var element = scene.FindElementRecursive(elementId);
        if (element is null)
        {
            return scene;
        }

        var updated = element.EffectiveStateConfig.ReadVariable is null
            ? element
            : element with { StateConfig = element.EffectiveStateConfig with { ReadVariable = null } };
        if (updated.Data is { ReadTagId: not null } data)
        {
            updated = updated with { Data = data with { ReadTagId = null } };
        }

        return Equals(updated, element) ? scene : scene.WithReplacedElementRecursive(updated);
    }
}
