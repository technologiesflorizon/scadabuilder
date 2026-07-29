using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

/// <summary>Locates a confined legacy-source base for existing imported V2 projects.</summary>
public sealed class ReferenceProjectCompatibilityLocator
{
    /// <summary>Finds one ancestor that resolves every relative imported source path.</summary>
    public string? FindImportedSourceBase(string projectRoot, ScadaProject project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(project);
        var relativePaths = project.Scenes
            .Where(page => page.EffectiveOrigin == PageOrigin.Imported)
            .Select(page => page.ImportProvenance?.SourcePath)
            .Where(path => !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (relativePaths.Length == 0)
        {
            return null;
        }

        for (var current = new DirectoryInfo(Path.GetFullPath(projectRoot)); current is not null; current = current.Parent)
        {
            var baseRoot = current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var prefix = baseRoot + Path.DirectorySeparatorChar;
            var allResolve = relativePaths.All(relativePath =>
            {
                var candidate = Path.GetFullPath(Path.Combine(baseRoot, relativePath));
                return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate);
            });
            if (allResolve)
            {
                return baseRoot;
            }
        }

        return null;
    }
}
