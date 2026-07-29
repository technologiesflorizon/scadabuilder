using System.Text.Json;
using ScadaBuilderV2.Application.Projects;

namespace ScadaBuilderV2.Infrastructure.Shell;

/// <summary>Persists recent projects and the last creation parent in per-user application data.</summary>
/// <remarks>
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-07-29-project-lifecycle-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/RecentProjectStoreTests.cs.
/// </remarks>
public sealed class RecentProjectStore(string? settingsRoot = null) : IRecentProjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string root = settingsRoot ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ScadaBuilderV2");

    /// <inheritdoc />
    public bool IsInitialized => File.Exists(Path.Combine(root, "recent-projects.json"));

    public async Task<IReadOnlyList<RecentProjectEntry>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(root, "recent-projects.json");
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var entries = await JsonSerializer.DeserializeAsync<List<RecentProjectRecord>>(stream, JsonOptions, cancellationToken) ?? [];
            return entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.ProjectFilePath))
                .GroupBy(entry => Path.GetFullPath(entry.ProjectFilePath), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(entry => entry.LastOpenedUtc).First())
                .OrderByDescending(entry => entry.LastOpenedUtc)
                .Take(12)
                .Select(entry => new RecentProjectEntry(
                    entry.DisplayName,
                    Path.GetFullPath(entry.ProjectFilePath),
                    entry.LastOpenedUtc,
                    File.Exists(entry.ProjectFilePath)))
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return [];
        }
    }

    public async Task RecordAsync(
        ProjectWorkspaceLocation location,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        var fullPath = Path.GetFullPath(location.ProjectFilePath);
        var entries = (await ReadAsync(cancellationToken))
            .Where(entry => !string.Equals(entry.ProjectFilePath, fullPath, StringComparison.OrdinalIgnoreCase))
            .Select(entry => new RecentProjectRecord(entry.DisplayName, entry.ProjectFilePath, entry.LastOpenedUtc))
            .Prepend(new RecentProjectRecord(displayName.Trim(), fullPath, DateTimeOffset.UtcNow))
            .Take(12)
            .ToArray();
        await WriteRecordsAsync(entries, cancellationToken);
    }

    public async Task RemoveAsync(string projectFilePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(projectFilePath);
        var entries = (await ReadAsync(cancellationToken))
            .Where(entry => !string.Equals(entry.ProjectFilePath, fullPath, StringComparison.OrdinalIgnoreCase))
            .Select(entry => new RecentProjectRecord(entry.DisplayName, entry.ProjectFilePath, entry.LastOpenedUtc))
            .ToArray();
        await WriteRecordsAsync(entries, cancellationToken);
    }

    public string GetDefaultCreationParent() =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public async Task<string> ReadCreationParentAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(root, "project-creation-settings.json");
        if (!File.Exists(path))
        {
            return GetDefaultCreationParent();
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var record = await JsonSerializer.DeserializeAsync<CreationSettingsRecord>(stream, JsonOptions, cancellationToken);
            return string.IsNullOrWhiteSpace(record?.ParentDirectory)
                ? GetDefaultCreationParent()
                : record.ParentDirectory;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return GetDefaultCreationParent();
        }
    }

    public async Task WriteCreationParentAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(root);
        await WriteAtomicAsync(
            Path.Combine(root, "project-creation-settings.json"),
            new CreationSettingsRecord(Path.GetFullPath(path)),
            cancellationToken);
    }

    private async Task WriteRecordsAsync(
        IReadOnlyList<RecentProjectRecord> entries,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        await WriteAtomicAsync(Path.Combine(root, "recent-projects.json"), entries, cancellationToken);
    }

    private static async Task WriteAtomicAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var pending = path + ".pending";
        await using (var stream = File.Create(pending))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
        }
        File.Move(pending, path, overwrite: true);
    }

    private sealed record RecentProjectRecord(
        string DisplayName,
        string ProjectFilePath,
        DateTimeOffset LastOpenedUtc);

    private sealed record CreationSettingsRecord(string ParentDirectory);
}
