using System.Text.Json;
using ScadaBuilderV2.Application.Libraries;

namespace ScadaBuilderV2.Infrastructure.Libraries;

/// <summary>
/// Persists user-registered external Element+ library locations. The locked default
/// library entry is never read from or written to this store.
/// </summary>
public sealed class LibraryRegistryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static string GetDefaultSettingsPath()
    {
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataRoot, "ScadaBuilderV2", "libraries.json");
    }

    public async Task<IReadOnlyList<LibraryEntry>> ReadExternalEntriesAsync(
        string? settingsPath = null,
        CancellationToken cancellationToken = default)
    {
        var path = settingsPath ?? GetDefaultSettingsPath();
        if (!File.Exists(path))
        {
            return Array.Empty<LibraryEntry>();
        }

        try
        {
            await using var read = File.OpenRead(path);
            var records = await JsonSerializer.DeserializeAsync<List<LibraryEntryRecord>>(read, SerializerOptions, cancellationToken)
                ?? [];

            return records
                .Select(record => new LibraryEntry(record.Name, record.Path, IsDefault: false))
                .ToArray();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return Array.Empty<LibraryEntry>();
        }
    }

    public async Task<string?> ReadDefaultNameAsync(
        string? settingsPath = null,
        CancellationToken cancellationToken = default)
    {
        var path = ResolveDefaultNamePath(settingsPath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var read = File.OpenRead(path);
            var record = await JsonSerializer.DeserializeAsync<DefaultNameRecord>(read, SerializerOptions, cancellationToken);
            return string.IsNullOrWhiteSpace(record?.Name) ? null : record.Name;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    public async Task WriteDefaultNameAsync(
        string name,
        string? settingsPath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var path = ResolveDefaultNamePath(settingsPath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var write = File.Create(path);
        await JsonSerializer.SerializeAsync(write, new DefaultNameRecord(name), SerializerOptions, cancellationToken);
    }

    private static string ResolveDefaultNamePath(string? settingsPath)
    {
        var externalEntriesPath = settingsPath ?? GetDefaultSettingsPath();
        var directory = Path.GetDirectoryName(externalEntriesPath) ?? "";
        return Path.Combine(directory, "default-library-name.json");
    }

    public async Task WriteExternalEntriesAsync(
        IReadOnlyList<LibraryEntry> externalEntries,
        string? settingsPath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(externalEntries);
        if (externalEntries.Any(entry => entry.IsDefault))
        {
            throw new ArgumentException("The default library entry must not be persisted to the library registry file.", nameof(externalEntries));
        }

        var path = settingsPath ?? GetDefaultSettingsPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var records = externalEntries
            .Select(entry => new LibraryEntryRecord(entry.Name, entry.Path))
            .ToList();

        await using var write = File.Create(path);
        await JsonSerializer.SerializeAsync(write, records, SerializerOptions, cancellationToken);
    }

    private sealed record LibraryEntryRecord(string Name, string Path);

    private sealed record DefaultNameRecord(string Name);
}
