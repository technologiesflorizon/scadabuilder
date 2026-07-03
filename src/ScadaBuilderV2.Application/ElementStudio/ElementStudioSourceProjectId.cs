namespace ScadaBuilderV2.Application.ElementStudio;

/// <summary>
/// Resolves the project id Studio Element+ should use to locate its default Element+
/// library folder when the loaded workspace has no real source project.
/// </summary>
public static class ElementStudioSourceProjectId
{
    /// <summary>
    /// The reference project whose library backs the default Element+ library across
    /// both SCADA Builder V2 and Studio Element+.
    /// </summary>
    public const string ReferenceProjectId = "AMR_REF_SCADA_V2";

    private static readonly string[] UnknownPlaceholders = ["Projet non charge", "Projet inconnu"];

    /// <summary>
    /// Returns <paramref name="sourceProjectId"/> unchanged when it identifies a real
    /// project, or <see cref="ReferenceProjectId"/> when it is null/blank or one of the
    /// placeholder values <see cref="ElementStudioPackageLoader"/>/its loader assigns to
    /// a workspace that has no real source project.
    /// </summary>
    public static string ResolveEffectiveProjectId(string? sourceProjectId)
    {
        if (string.IsNullOrWhiteSpace(sourceProjectId) ||
            UnknownPlaceholders.Contains(sourceProjectId, StringComparer.Ordinal))
        {
            return ReferenceProjectId;
        }

        return sourceProjectId;
    }
}
