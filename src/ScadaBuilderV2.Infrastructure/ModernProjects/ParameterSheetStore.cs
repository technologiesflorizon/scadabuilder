using System.Text.Json;
using System.Text.Json.Serialization;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

/// <summary>
/// Persists project parameter sheets under a deterministic layout.
/// Each sheet is stored as parameter-sheets/&lt;sheetKey&gt;.parameter-sheet.json
/// with ordered JSON and atomic transaction semantics consistent with quick windows.
/// </summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/sdd/2026-09-30-quick-window-parameter-sheets/task-4-brief.md.
/// Tests: tests/ScadaBuilderV2.Tests/ParameterSheetStoreTests.cs.
/// </remarks>
public sealed class ParameterSheetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Gets the relative path for a parameter sheet file.</summary>
    public static string GetRelativePath(Guid sheetKey)
    {
        if (sheetKey == Guid.Empty)
            throw new ArgumentException("SheetKey must be non-empty.", nameof(sheetKey));
        return $"parameter-sheets/{sheetKey:N}.parameter-sheet.json";
    }

    /// <summary>Resolves the contained absolute path and validates it stays under projectRoot.</summary>
    public static string ResolveContainedParameterSheetPath(string projectRoot, string relativePath)
    {
        if (!relativePath.StartsWith("parameter-sheets/", StringComparison.OrdinalIgnoreCase) || !relativePath.EndsWith(".parameter-sheet.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"ParameterSheet path must be under parameter-sheets/: {relativePath}");
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

    /// <summary>Saves one parameter sheet atomically (staging then replace).</summary>
    public async Task SaveAsync(string projectRoot, ParameterSheet sheet, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(sheet);

        var relative = GetRelativePath(sheet.SheetKey);
        var path = ResolveContainedParameterSheetPath(projectRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await SaveJsonAsync(path, sheet, cancellationToken);
    }

    /// <summary>Loads one parameter sheet.</summary>
    public async Task<ParameterSheet?> LoadAsync(string projectRoot, Guid sheetKey, CancellationToken cancellationToken = default)
    {
        var relative = GetRelativePath(sheetKey);
        var path = ResolveContainedParameterSheetPath(projectRoot, relative);
        if (!File.Exists(path))
            return null;
        await using var stream = File.OpenRead(path);
        var sheet = await JsonSerializer.DeserializeAsync<ParameterSheet>(stream, JsonOptions, cancellationToken);
        ValidateLoadedSheet(path, sheetKey, sheet);
        return sheet;
    }

    /// <summary>Loads all parameter sheets under projectRoot.</summary>
    public async Task<IReadOnlyList<ParameterSheet>> LoadAllAsync(string projectRoot, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        projectRoot = Path.GetFullPath(projectRoot);
        var dir = Path.Combine(projectRoot, "parameter-sheets");
        if (!Directory.Exists(dir))
            return Array.Empty<ParameterSheet>();
        var result = new List<ParameterSheet>();
        foreach (var file in Directory.GetFiles(dir, "*.parameter-sheet.json", SearchOption.TopDirectoryOnly).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            var relative = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
            _ = ResolveContainedParameterSheetPath(projectRoot, relative);
            await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            var sheet = await JsonSerializer.DeserializeAsync<ParameterSheet>(stream, JsonOptions, cancellationToken);
            var fileName = Path.GetFileName(file);
            var keyText = fileName[..^".parameter-sheet.json".Length];
            if (!Guid.TryParseExact(keyText, "N", out var expectedKey))
                throw new InvalidDataException($"ParameterSheet file name is not a canonical SheetKey: {fileName}");
            ValidateLoadedSheet(file, expectedKey, sheet);
            result.Add(sheet!);
        }
        return result.OrderBy(s => s.Code, StringComparer.Ordinal).ThenBy(s => s.SheetKey).ToArray();
    }

    /// <summary>Deletes one parameter sheet file.</summary>
    public void Delete(string projectRoot, Guid sheetKey)
    {
        var relative = GetRelativePath(sheetKey);
        var path = ResolveContainedParameterSheetPath(projectRoot, relative);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>Stages parameter sheets for atomic workspace save, mirroring ModernProjectStore staging.</summary>
    internal static async Task<WorkspaceSaveFileEntry> StageJsonAsync(string projectRoot, string transactionRoot, ParameterSheet sheet, CancellationToken cancellationToken)
    {
        var relative = GetRelativePath(sheet.SheetKey);
        var targetPath = ResolveContainedParameterSheetPath(projectRoot, relative);
        var stagedRelative = $"new/{relative}";
        var backupRelative = $"backup/{relative}";
        var stagedPath = ResolveContainedPath(transactionRoot, stagedRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
        await SaveJsonAsync(stagedPath, sheet, cancellationToken);
        return new WorkspaceSaveFileEntry(relative, stagedRelative, backupRelative, File.Exists(targetPath));
    }

    private static async Task SaveJsonAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var write = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(write, value, JsonOptions, cancellationToken);
                await write.FlushAsync(cancellationToken);
                write.Flush(flushToDisk: true);
            }
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static void ValidateLoadedSheet(string path, Guid expectedKey, ParameterSheet? sheet)
    {
        if (sheet is null)
            throw new InvalidDataException($"ParameterSheet file '{path}' is empty or invalid.");
        if (sheet.SheetKey != expectedKey)
            throw new InvalidDataException($"ParameterSheet file '{path}' contains SheetKey '{sheet.SheetKey}' instead of '{expectedKey}'.");
    }
}
