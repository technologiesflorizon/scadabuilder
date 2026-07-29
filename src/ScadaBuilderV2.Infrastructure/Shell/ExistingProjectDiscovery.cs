namespace ScadaBuilderV2.Infrastructure.Shell;

/// <summary>Discovers editable project manifests shipped beside, or retained under, the application tree.</summary>
/// <remarks>
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-07-29-project-lifecycle-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ProjectLifecycleInfrastructureTests.cs.
/// </remarks>
public sealed class ExistingProjectDiscovery
{
    /// <summary>Finds immediate project children under the nearest existing <c>projects</c> directory.</summary>
    public IReadOnlyList<string> Discover(params string[] startPaths)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var startPath in startPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            DirectoryInfo? current;
            try
            {
                current = new DirectoryInfo(Path.GetFullPath(startPath));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            while (current is not null)
            {
                var projectsRoot = Path.Combine(current.FullName, "projects");
                if (visited.Add(projectsRoot) && Directory.Exists(projectsRoot))
                {
                    try
                    {
                        var manifests = Directory
                            .EnumerateDirectories(projectsRoot, "*", SearchOption.TopDirectoryOnly)
                            .Select(directory => Path.Combine(directory, "project.json"))
                            .Where(File.Exists)
                            .Select(Path.GetFullPath)
                            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        if (manifests.Length > 0)
                        {
                            return manifests;
                        }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        // Continue toward another approved application ancestor.
                    }
                }

                current = current.Parent;
            }
        }

        return [];
    }
}
