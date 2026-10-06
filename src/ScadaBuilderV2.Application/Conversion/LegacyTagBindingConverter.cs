using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Conversion;

/// <summary>What the editor offers for an element's legacy <c>Data.TagBinding</c>.</summary>
public enum LegacyTagBindingState
{
    /// <summary>The element carries no legacy binding (none, Custom library reference, or legacy static).</summary>
    None,

    /// <summary>The element carries a legacy binding and no <c>Data.ReadTagId</c>: it may be converted.</summary>
    Convertible,

    /// <summary>The element already reads through <c>Data.ReadTagId</c>; its legacy binding is ignored residue.</summary>
    Residual
}

/// <summary>Outcome of converting the legacy binding of one element.</summary>
/// <param name="Converted">Whether the scene changed.</param>
/// <param name="Message">French operator message: the conversion done, or why nothing changed.</param>
/// <param name="Scene">The scene after the conversion, or the unchanged input scene.</param>
/// <param name="ReadTagId">The read tag written, when converted.</param>
/// <param name="HistoryAction">The single undoable transition to push, when converted.</param>
public sealed record LegacyTagBindingConversion(
    bool Converted,
    string Message,
    ScadaScene Scene,
    string? ReadTagId,
    IEditorHistoryAction? HistoryAction);

/// <summary>One element converted by <see cref="LegacyTagBindingConverter.ConvertScene"/>.</summary>
public sealed record LegacyTagBindingConvertedElement(string ElementId, string TagBinding, string ReadTagId);

/// <summary>One residual legacy binding (element already reading through <c>ReadTagId</c>) cleared by <see cref="LegacyTagBindingConverter.ConvertScene"/>.</summary>
public sealed record LegacyTagBindingClearedElement(string ElementId, string TagBinding, string ReadTagId);

/// <summary>One element left untouched by <see cref="LegacyTagBindingConverter.ConvertScene"/>, with its reason.</summary>
public sealed record LegacyTagBindingSkippedElement(string ElementId, string TagBinding, string Reason);

/// <summary>Outcome of converting every convertible legacy binding of one page.</summary>
/// <param name="Scene">The scene after the conversion, or the unchanged input scene.</param>
/// <param name="Converted">The converted elements.</param>
/// <param name="Skipped">The elements left untouched, with their reason.</param>
/// <param name="Cleared">The residual legacy bindings cleared (their <c>ReadTagId</c> already won).</param>
/// <param name="HistoryAction">The single undoable transition covering every conversion, when any.</param>
public sealed record LegacyTagBindingSceneConversion(
    ScadaScene Scene,
    IReadOnlyList<LegacyTagBindingConvertedElement> Converted,
    IReadOnlyList<LegacyTagBindingSkippedElement> Skipped,
    IEditorHistoryAction? HistoryAction,
    IReadOnlyList<LegacyTagBindingClearedElement>? Cleared = null)
{
    /// <summary>Gets the residual legacy bindings cleared, empty when none.</summary>
    public IReadOnlyList<LegacyTagBindingClearedElement> EffectiveCleared => Cleared ?? Array.Empty<LegacyTagBindingClearedElement>();

    /// <summary>Gets the French summary shown to the operator: counts, then one line per skipped element.</summary>
    public string Summary
    {
        get
        {
            if (Converted.Count == 0 && Skipped.Count == 0 && EffectiveCleared.Count == 0)
            {
                return "Aucun TagBinding legacy sur cette page.";
            }

            var lines = new List<string>
            {
                $"{Converted.Count} TagBinding converti(s) en Lecture de variable, {EffectiveCleared.Count} résiduel(s) effacé(s), {Skipped.Count} ignoré(s)."
            };
            lines.AddRange(Converted.Select(item => $"Converti — élément '{item.ElementId}' : « {item.TagBinding} » → ReadTagId « {item.ReadTagId} »."));
            lines.AddRange(EffectiveCleared.Select(item => $"Effacé — élément '{item.ElementId}' : TagBinding résiduel « {item.TagBinding} », la Lecture de variable (ReadTagId) « {item.ReadTagId} » reste seule lue."));
            lines.AddRange(Skipped.Select(item => $"Ignoré — élément '{item.ElementId}' : {item.Reason}"));
            if (Converted.Count > 0)
            {
                lines.Add(LegacyTagBindingConverter.ResultLocation);
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}

/// <summary>
/// Assisted conversion of the legacy <c>Data.TagBinding</c> field to the read binding <c>Data.ReadTagId</c>
/// (« Lecture de variable »), one element or one whole page at a time, as one undoable transition.
/// </summary>
/// <remarks>
/// The value is resolved like the read-variable selector would store it: a <c>#N</c> parameter stays as is on a
/// page of type Fenêtre rapide (and is refused elsewhere), anything else must name exactly one enabled tag of the
/// project catalog (id, display name or keyword label) and becomes its canonical id, the form the runtime mapping
/// cache is keyed by. On a Fenêtre rapide page a project tag is accepted too, exactly as the fragment exporter
/// resolves a TagBinding there, although that page's selector offers only <c>#N</c>. An unresolved value is never
/// converted: the binding is kept and the operator is told why. An element that already has a <c>ReadTagId</c> is
/// never converted either (it already wins); its legacy binding is residual and is cleared instead
/// (<see cref="ClearResidual"/>, and by <see cref="ConvertScene"/>). The transition is a <see cref="SceneSnapshotChangedAction"/>, which never
/// merges with a neighbouring property edit, so one undo restores exactly the scene before the conversion.
///
/// Decisions: DEC-0053.
/// Contracts: docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md §2 règle 5.
/// Tests: tests/ScadaBuilderV2.Tests/LegacyTagBindingConverterTests.cs.
/// </remarks>
public static class LegacyTagBindingConverter
{
    /// <summary>History label of a conversion transition.</summary>
    public const string HistoryLabel = "conversion TagBinding en Lecture de variable";

    /// <summary>History label of a residual-clearing transition.</summary>
    public const string ClearHistoryLabel = "effacement TagBinding residuel";

    /// <summary>Tells the operator where a converted read binding is shown.</summary>
    public const string ResultLocation = "Résultat visible dans l'onglet État, section « Lecture de variable » (liaison ReadTagId).";

    /// <summary>Returns what the editor offers for the legacy binding of <paramref name="element"/>.</summary>
    public static LegacyTagBindingState Inspect(ScadaElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (LegacyTagBinding.Of(element) is null)
        {
            return LegacyTagBindingState.None;
        }

        return LegacyTagBinding.HasReadTagId(element) ? LegacyTagBindingState.Residual : LegacyTagBindingState.Convertible;
    }

    /// <summary>Returns the French hint the property surfaces show under the read-only legacy field.</summary>
    public static string Describe(LegacyTagBindingState state, bool isQuickWindowPage) => state switch
    {
        LegacyTagBindingState.Convertible when isQuickWindowPage =>
            "Encore lu par la fenêtre rapide, mais legacy : convertissez-le en Lecture de variable.",
        LegacyTagBindingState.Convertible =>
            "Inerte sur cette page : le Builder ne l'exporte pas. Convertissez-le en Lecture de variable.",
        LegacyTagBindingState.Residual =>
            "Résiduel : la Lecture de variable (ReadTagId) déjà définie a priorité, ce TagBinding est ignoré. Vous pouvez l'effacer.",
        _ => string.Empty
    };

    /// <summary>
    /// Converts the legacy binding of element <paramref name="elementId"/> of <paramref name="scene"/>, or explains
    /// why nothing changed.
    /// </summary>
    /// <param name="scene">The active page scene.</param>
    /// <param name="elementId">The element to convert (searched recursively).</param>
    /// <param name="projectTagCatalog">The project tag catalog (not a <c>#N</c> selector projection).</param>
    /// <param name="isQuickWindowPage">Whether the page is of type <see cref="ScadaPageType.QuickWindow"/>.</param>
    public static LegacyTagBindingConversion ConvertElement(
        ScadaScene scene,
        string elementId,
        ScadaTagCatalog? projectTagCatalog,
        bool isQuickWindowPage)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);

        var element = scene.FindElementRecursive(elementId);
        if (element is null)
        {
            return Unchanged(scene, $"Conversion impossible : élément '{elementId}' introuvable.");
        }

        var tagBinding = LegacyTagBinding.Of(element);
        if (tagBinding is null)
        {
            return Unchanged(scene, "Aucun TagBinding legacy à convertir sur cet élément.");
        }

        if (LegacyTagBinding.HasReadTagId(element))
        {
            return Unchanged(scene, ResidualReason(tagBinding));
        }

        if (!TryResolve(tagBinding, projectTagCatalog, isQuickWindowPage, out var readTagId, out var refusal))
        {
            return Unchanged(scene, $"Conversion impossible : {refusal} Le TagBinding est conservé.");
        }

        var after = scene.WithReplacedElementRecursive(Converted(element, readTagId));
        return new LegacyTagBindingConversion(
            true,
            $"TagBinding « {tagBinding} » converti en Lecture de variable : ReadTagId « {readTagId} ». {ResultLocation}",
            after,
            readTagId,
            new SceneSnapshotChangedAction(scene.Id, scene, after, HistoryLabel));
    }

    /// <summary>
    /// Converts every convertible legacy binding of <paramref name="scene"/> as one transition, and reports every
    /// element left untouched with its reason.
    /// </summary>
    /// <param name="scene">The active page scene.</param>
    /// <param name="projectTagCatalog">The project tag catalog (not a <c>#N</c> selector projection).</param>
    /// <param name="isQuickWindowPage">Whether the page is of type <see cref="ScadaPageType.QuickWindow"/>.</param>
    public static LegacyTagBindingSceneConversion ConvertScene(
        ScadaScene scene,
        ScadaTagCatalog? projectTagCatalog,
        bool isQuickWindowPage)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var converted = new List<LegacyTagBindingConvertedElement>();
        var skipped = new List<LegacyTagBindingSkippedElement>();
        var cleared = new List<LegacyTagBindingClearedElement>();
        var after = scene;
        foreach (var element in Flatten(scene.Elements))
        {
            if (LegacyTagBinding.Of(element) is not { } tagBinding)
            {
                continue;
            }

            if (LegacyTagBinding.HasReadTagId(element))
            {
                var residual = after.FindElementRecursive(element.Id) ?? element;
                after = after.WithReplacedElementRecursive(Cleared(residual));
                cleared.Add(new LegacyTagBindingClearedElement(element.Id, tagBinding, element.Data!.ReadTagId!.Trim()));
                continue;
            }

            if (!TryResolve(tagBinding, projectTagCatalog, isQuickWindowPage, out var readTagId, out var refusal))
            {
                skipped.Add(new LegacyTagBindingSkippedElement(element.Id, tagBinding, refusal));
                continue;
            }

            var current = after.FindElementRecursive(element.Id) ?? element;
            after = after.WithReplacedElementRecursive(Converted(current, readTagId));
            converted.Add(new LegacyTagBindingConvertedElement(element.Id, tagBinding, readTagId));
        }

        var changed = converted.Count > 0 || cleared.Count > 0;
        return new LegacyTagBindingSceneConversion(
            changed ? after : scene,
            converted,
            skipped,
            changed ? new SceneSnapshotChangedAction(scene.Id, scene, after, HistoryLabel) : null,
            cleared);
    }

    /// <summary>
    /// Clears the residual legacy binding of element <paramref name="elementId"/>, which already reads through
    /// <c>Data.ReadTagId</c>, as one undoable transition; anything else is left unchanged with the reason.
    /// </summary>
    /// <remarks>
    /// The way out of a residual binding: the field is read-only and the conversion is not offered once a
    /// <c>ReadTagId</c> wins, so without this action its validation warning would stay forever. Never applies to a
    /// Custom element (<see cref="LegacyTagBinding.Of"/> returns null for its library reference).
    /// </remarks>
    public static LegacyTagBindingConversion ClearResidual(ScadaScene scene, string elementId)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);

        var element = scene.FindElementRecursive(elementId);
        if (element is null)
        {
            return Unchanged(scene, $"Effacement impossible : élément '{elementId}' introuvable.");
        }

        if (LegacyTagBinding.Of(element) is not { } tagBinding)
        {
            return Unchanged(scene, "Aucun TagBinding legacy à effacer sur cet élément.");
        }

        if (!LegacyTagBinding.HasReadTagId(element))
        {
            return Unchanged(scene, $"Effacement refusé : le TagBinding « {tagBinding} » n'est pas résiduel (aucune Lecture de variable ReadTagId). Convertissez-le plutôt en Lecture de variable.");
        }

        var readTagId = element.Data!.ReadTagId!.Trim();
        var after = scene.WithReplacedElementRecursive(Cleared(element));
        return new LegacyTagBindingConversion(
            true,
            $"TagBinding résiduel « {tagBinding} » effacé : la Lecture de variable (ReadTagId) « {readTagId} » reste seule lue.",
            after,
            readTagId,
            new SceneSnapshotChangedAction(scene.Id, scene, after, ClearHistoryLabel));
    }

    /// <summary>
    /// Resolves <paramref name="tagBinding"/> to the read tag the read-variable selector would store, or returns
    /// the French reason it cannot.
    /// </summary>
    public static bool TryResolve(
        string tagBinding,
        ScadaTagCatalog? projectTagCatalog,
        bool isQuickWindowPage,
        out string readTagId,
        out string refusal)
    {
        readTagId = string.Empty;
        refusal = string.Empty;
        var value = tagBinding.Trim();

        if (QuickWindowPlaceholder.TryParseName(value, out _))
        {
            if (!isQuickWindowPage)
            {
                refusal = $"« {value} » est un paramètre #N, qui n'a de sens que sur une page Fenêtre rapide.";
                return false;
            }

            readTagId = value;
            return true;
        }

        if (projectTagCatalog?.Tags is not { Count: > 0 })
        {
            refusal = $"aucun catalogue de tags n'est importé dans le projet, « {value} » ne peut pas être résolu.";
            return false;
        }

        var resolved = ScadaExpressionValidator.TryResolveTagReference(value, projectTagCatalog);
        switch (resolved.Status)
        {
            case TagResolveStatus.Resolved when resolved.CanonicalId is { } canonicalId:
                var tag = projectTagCatalog.Tags.First(item => string.Equals(item.Id, canonicalId, StringComparison.Ordinal));
                if (!tag.Enabled)
                {
                    refusal = $"le tag « {canonicalId} » désigné par « {value} » est désactivé dans le catalogue.";
                    return false;
                }

                readTagId = canonicalId;
                return true;
            case TagResolveStatus.Ambiguous:
                refusal = $"« {value} » désigne plusieurs tags du catalogue ({string.Join(", ", resolved.Matches)}).";
                return false;
            default:
                refusal = $"« {value} » ne désigne aucun tag du catalogue du projet.";
                return false;
        }
    }

    private static string ResidualReason(string tagBinding) =>
        $"Lecture de variable (ReadTagId) déjà définie : le TagBinding « {tagBinding} » est résiduel et n'est pas converti ; utilisez « Effacer le TagBinding résiduel ».";

    private static ScadaElement Converted(ScadaElement element, string readTagId) =>
        element with { Data = element.Data! with { ReadTagId = readTagId, TagBinding = null } };

    private static ScadaElement Cleared(ScadaElement element) =>
        element with { Data = element.Data! with { TagBinding = null } };

    private static LegacyTagBindingConversion Unchanged(ScadaScene scene, string message) =>
        new(false, message, scene, null, null);

    private static IEnumerable<ScadaElement> Flatten(IEnumerable<ScadaElement> elements)
    {
        foreach (var element in elements)
        {
            yield return element;
            foreach (var child in Flatten(element.ChildElements))
            {
                yield return child;
            }
        }
    }
}
