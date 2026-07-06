namespace ScadaBuilderV2.Application.ElementStudio;

/// <summary>
/// Resolves the default display name Element Studio should seed a new component with.
/// </summary>
/// <remarks>
/// Decisions: DEC-0035. Contracts: none (Application-only helper, no persisted contract).
/// Tests: ElementStudioComponentNamingTests.cs.
/// </remarks>
public static class ElementStudioComponentNaming
{
    public const string DefaultComponentName = "Nouveau composant";

    public static string ResolveDefaultComponentName(IReadOnlyList<string> importedSourceNames)
    {
        ArgumentNullException.ThrowIfNull(importedSourceNames);

        var firstNonBlank = importedSourceNames.FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        return string.IsNullOrWhiteSpace(firstNonBlank) ? DefaultComponentName : firstNonBlank;
    }
}
