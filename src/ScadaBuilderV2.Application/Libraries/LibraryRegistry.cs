namespace ScadaBuilderV2.Application.Libraries;

/// <summary>
/// One Element+ library location: the locked default, or a user-registered external folder.
/// </summary>
public sealed record LibraryEntry(string Name, string Path, bool IsDefault);

/// <summary>
/// In-memory registry of Element+ library locations: exactly one locked default entry
/// plus zero or more user-managed external entries.
/// </summary>
public sealed class LibraryRegistry
{
    private LibraryEntry _defaultEntry;
    private readonly List<LibraryEntry> _externalEntries;

    public LibraryRegistry(LibraryEntry defaultEntry, IEnumerable<LibraryEntry> externalEntries)
    {
        ArgumentNullException.ThrowIfNull(defaultEntry);
        ArgumentNullException.ThrowIfNull(externalEntries);
        if (!defaultEntry.IsDefault)
        {
            throw new ArgumentException("The default entry must have IsDefault set to true.", nameof(defaultEntry));
        }

        _defaultEntry = defaultEntry;
        _externalEntries = externalEntries.Select(entry => entry with { IsDefault = false }).ToList();
    }

    public IReadOnlyList<LibraryEntry> Entries =>
        new[] { _defaultEntry }.Concat(_externalEntries).ToArray();

    public IReadOnlyList<LibraryEntry> ExternalEntries => _externalEntries;

    public void Add(string name, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalizedPath = NormalizePath(path);
        if (Entries.Any(entry => NormalizePath(entry.Path) == normalizedPath))
        {
            throw new InvalidOperationException($"Une librairie avec le chemin '{path}' est deja enregistree.");
        }

        _externalEntries.Add(new LibraryEntry(name, path, IsDefault: false));
    }

    public void Rename(string currentName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        if (string.Equals(_defaultEntry.Name, currentName, StringComparison.Ordinal))
        {
            _defaultEntry = _defaultEntry with { Name = newName };
            return;
        }

        var index = FindExternalIndex(currentName, "renommer");
        _externalEntries[index] = _externalEntries[index] with { Name = newName };
    }

    public void UpdatePath(string name, string newPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);

        var index = FindExternalIndex(name, "modifier le chemin de");
        _externalEntries[index] = _externalEntries[index] with { Path = newPath };
    }

    public void Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var index = FindExternalIndex(name, "supprimer");
        _externalEntries.RemoveAt(index);
    }

    private int FindExternalIndex(string name, string action)
    {
        var index = _externalEntries.FindIndex(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
        if (index < 0)
        {
            if (string.Equals(_defaultEntry.Name, name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Impossible de {action} la librairie par defaut.");
            }

            throw new InvalidOperationException($"Aucune librairie externe nommee '{name}' n'est enregistree.");
        }

        return index;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
    }
}
