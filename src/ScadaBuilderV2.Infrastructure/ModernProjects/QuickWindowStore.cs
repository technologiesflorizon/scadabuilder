using System.Text.Json;
using System.Text.Json.Serialization;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

/// <summary>
/// Persists quick window definitions under a deterministic layout.
/// Each definition is stored as quick-windows/&lt;definitionKey&gt;.quick-window.json
/// with ordered JSON and atomic transaction semantics consistent with scenes.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, FR-001, FR-019.
/// Contracts: docs/superpowers/plans/2026-08-10-parameterized-quick-window-management.md Task 1.4.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowStoreTests.cs.
/// </remarks>
public sealed class QuickWindowStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Gets the relative path for a quick window file.</summary>
    public static string GetRelativePath(Guid definitionKey)
    {
        if (definitionKey == Guid.Empty)
            throw new ArgumentException("DefinitionKey must be non-empty.", nameof(definitionKey));
        return $"quick-windows/{definitionKey:N}.quick-window.json";
    }

    /// <summary>Resolves the contained absolute path and validates it stays under projectRoot.</summary>
    public static string ResolveContainedQuickWindowPath(string projectRoot, string relativePath)
    {
        if (!relativePath.StartsWith("quick-windows/", StringComparison.OrdinalIgnoreCase) || !relativePath.EndsWith(".quick-window.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"QuickWindow path must be under quick-windows/: {relativePath}");
        return ResolveContainedPath(projectRoot, relativePath);
    }

    private static string ResolveContainedPath(string root, string relativePath)
    {
        var normalized = relativePath.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(normalized) || normalized.Contains(':', StringComparison.Ordinal))
            throw new InvalidOperationException($"Workspace path is not portable: {relativePath}");
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(s => s is "." or ".." || s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new InvalidOperationException($"Workspace path is invalid: {relativePath}");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, Path.Combine(segments)));
        if (!fullPath.StartsWith($"{fullRoot}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Workspace path escapes the project root: {relativePath}");
        return fullPath;
    }

    /// <summary>Saves one quick window definition atomically (staging then replace).</summary>
    public async Task SaveAsync(string projectRoot, QuickWindowDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(definition);
        var issues = QuickWindowValidation.ValidateDefinition(definition);
        if (issues.Count > 0)
            throw new InvalidOperationException($"QuickWindow definition invalid: {string.Join("; ", issues)}");

        var relative = GetRelativePath(definition.DefinitionKey);
        var path = ResolveContainedQuickWindowPath(projectRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Deterministic order: serialize with sorted members and ordered JSON
        var deterministic = definition with
        {
            InterfaceMembers = definition.InterfaceMembers.OrderBy(m => m.Name, StringComparer.Ordinal).ThenBy(m => m.MemberKey).ToArray(),
            Content = definition.Content with
            {
                Elements = definition.Content.EffectiveElements.OrderBy(e => e.Id, StringComparer.Ordinal).ToArray()
            }
        };
        await SaveJsonAsync(path, deterministic, cancellationToken);
    }

    /// <summary>Loads one quick window definition.</summary>
    public async Task<QuickWindowDefinition?> LoadAsync(string projectRoot, Guid definitionKey, CancellationToken cancellationToken = default)
    {
        var relative = GetRelativePath(definitionKey);
        var path = ResolveContainedQuickWindowPath(projectRoot, relative);
        if (!File.Exists(path))
            return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<QuickWindowDefinition>(stream, JsonOptions, cancellationToken);
    }

    /// <summary>Loads all quick window definitions under projectRoot.</summary>
    public async Task<IReadOnlyList<QuickWindowDefinition>> LoadAllAsync(string projectRoot, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        projectRoot = Path.GetFullPath(projectRoot);
        var dir = Path.Combine(projectRoot, "quick-windows");
        if (!Directory.Exists(dir))
            return Array.Empty<QuickWindowDefinition>();
        var result = new List<QuickWindowDefinition>();
        foreach (var file in Directory.GetFiles(dir, "*.quick-window.json", SearchOption.TopDirectoryOnly).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            // Path validation
            var relative = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
            _ = ResolveContainedQuickWindowPath(projectRoot, relative);
            await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            var def = await JsonSerializer.DeserializeAsync<QuickWindowDefinition>(stream, JsonOptions, cancellationToken);
            if (def is not null)
                result.Add(def);
        }
        return result.OrderBy(d => d.Code, StringComparer.Ordinal).ThenBy(d => d.DefinitionKey).ToArray();
    }

    /// <summary>Deletes one quick window file.</summary>
    public void Delete(string projectRoot, Guid definitionKey)
    {
        var relative = GetRelativePath(definitionKey);
        var path = ResolveContainedQuickWindowPath(projectRoot, relative);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>Stages quick window definitions for atomic workspace save, mirroring ModernProjectStore staging.</summary>
    internal static async Task<WorkspaceSaveFileEntry> StageJsonAsync(string projectRoot, string transactionRoot, QuickWindowDefinition definition, CancellationToken cancellationToken)
    {
        var relative = GetRelativePath(definition.DefinitionKey);
        var targetPath = ResolveContainedQuickWindowPath(projectRoot, relative);
        var stagedRelative = $"new/{relative}";
        var backupRelative = $"backup/{relative}";
        var stagedPath = ResolveContainedPath(transactionRoot, stagedRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
        // Deterministic
        var deterministic = definition with
        {
            InterfaceMembers = definition.InterfaceMembers.OrderBy(m => m.Name, StringComparer.Ordinal).ThenBy(m => m.MemberKey).ToArray(),
            Content = definition.Content with
            {
                Elements = definition.Content.EffectiveElements.OrderBy(e => e.Id, StringComparer.Ordinal).ToArray()
            }
        };
        await SaveJsonAsync(stagedPath, deterministic, cancellationToken);
        return new WorkspaceSaveFileEntry(relative, stagedRelative, backupRelative, File.Exists(targetPath));
    }

    private static async Task SaveJsonAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        await using var write = File.Create(path);
        await JsonSerializer.SerializeAsync(write, value, JsonOptions, cancellationToken);
    }
}
