using System.IO;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

/// <summary>Copies an artifact aside before it is converted.</summary>
/// <remarks>
/// A conversion does not undo, so this copy is the only way back and it is written first: its failure aborts
/// the conversion. An existing backup is never replaced, because the file it holds may be the only surviving
/// copy of an earlier generation.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C6, C7.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ArtifactBackupWriterTests.cs.
/// </remarks>
public static class ArtifactBackupWriter
{
    /// <summary>Copies the file to `<name>.bak`, numbering the suffix rather than replacing an earlier backup.</summary>
    /// <returns>The path actually written.</returns>
    public static string CreateBackup(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var candidate = filePath + ".bak";
        var index = 1;
        while (File.Exists(candidate))
        {
            candidate = $"{filePath}.bak.{index++}";
        }
        File.Copy(filePath, candidate);
        return candidate;
    }
}
