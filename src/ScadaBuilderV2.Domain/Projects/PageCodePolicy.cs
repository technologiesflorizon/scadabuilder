using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ScadaBuilderV2.Domain.Projects;

/// <summary>Result returned when validating one human-visible page code.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/PageIdentityTests.cs.
/// </remarks>
public sealed record PageCodeValidationResult(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>Validates and proposes portable human-visible page codes.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/PageIdentityTests.cs.
/// </remarks>
public static partial class PageCodePolicy
{
    private static readonly HashSet<string> ReservedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "con", "prn", "aux", "nul", "clock$",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
    };

    /// <summary>Validates format, portability and case-insensitive uniqueness.</summary>
    public static PageCodeValidationResult Validate(
        string? pageCode,
        IEnumerable<string>? existingCodes = null,
        string? currentCode = null)
    {
        var errors = new List<string>();
        var normalized = pageCode?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            errors.Add("Le code de page est requis.");
        }
        else
        {
            if (!PageCodeRegex().IsMatch(normalized))
            {
                errors.Add("Le code doit commencer par une lettre minuscule et contenir seulement a-z, 0-9, _ ou - (64 caractères maximum).");
            }

            if (ReservedFileNames.Contains(normalized))
            {
                errors.Add("Le code de page est réservé par le système de fichiers.");
            }

            if ((existingCodes ?? Array.Empty<string>()).Any(existing =>
                !string.Equals(existing, currentCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("Le code de page existe déjà.");
            }
        }

        return new PageCodeValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Derives a portable, valid page code from a free-form human-readable display name (accents, spaces
    /// and capitals allowed in the input). The returned code always satisfies <see cref="Validate"/> in
    /// isolation (no existing-codes/current-code check); callers that need project-wide uniqueness still
    /// run the result through their own suffixing (see `QuickWindowWorkspaceController.ProposeCode`).
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0050, FR-UI-12.
    /// Contracts: lower-case; diacritics stripped via Unicode `FormD` + `NonSpacingMark` filtering (not a
    /// hand-written lookup table); any run of characters outside `[a-z0-9]` becomes a single `_`; leading
    /// and trailing `_`/`-` are trimmed; a result that does not start with `a`-`z` (empty, or starting with
    /// a digit) is prefixed with `fenetre`/`fenetre_`; the result is truncated to 64 characters with no
    /// trailing `_` left by the cut.
    /// Tests: tests/ScadaBuilderV2.Tests/PageIdentityTests.cs.
    /// </remarks>
    public static string DeriveCodeFromDisplayName(string? displayName)
    {
        const string FallbackPrefix = "fenetre";

        var normalized = StripDiacritics((displayName ?? string.Empty).Trim().ToLowerInvariant());
        normalized = NonPortableRunRegex().Replace(normalized, "_").Trim('_', '-');

        if (normalized.Length == 0)
        {
            normalized = FallbackPrefix;
        }
        else if (normalized[0] < 'a' || normalized[0] > 'z')
        {
            normalized = $"{FallbackPrefix}_{normalized}";
        }

        if (normalized.Length > 64)
        {
            normalized = normalized[..64].TrimEnd('_', '-');
        }

        return normalized;
    }

    private static string StripDiacritics(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Proposes a deterministic unique code for a duplicated page.</summary>
    public static string SuggestDuplicateCode(string sourceCode, IEnumerable<string>? existingCodes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCode);
        var used = (existingCodes ?? Array.Empty<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var source = sourceCode.Trim();
        for (var suffix = 1; suffix < int.MaxValue; suffix++)
        {
            var suffixText = suffix == 1 ? "_copy" : $"_copy{suffix}";
            var prefixLength = Math.Min(source.Length, 64 - suffixText.Length);
            var candidate = $"{source[..prefixLength]}{suffixText}";
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Impossible de proposer un code de page unique.");
    }

    [GeneratedRegex("^[a-z][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex PageCodeRegex();

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonPortableRunRegex();
}
