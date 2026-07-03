namespace ScadaBuilderV2.Application.ElementStudio;

/// <summary>
/// Computes a unique "<name>_copieN" candidate for duplicating a Studio Element+ component.
/// </summary>
public static class ElementStudioComponentCopyNaming
{
    public static string GenerateCopyName(string baseName, IEnumerable<string> existingNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        ArgumentNullException.ThrowIfNull(existingNames);

        var existingSet = new HashSet<string>(existingNames, StringComparer.Ordinal);
        var suffixIndex = 1;
        string candidate;
        do
        {
            candidate = $"{baseName}_copie{suffixIndex}";
            suffixIndex++;
        } while (existingSet.Contains(candidate));

        return candidate;
    }
}
