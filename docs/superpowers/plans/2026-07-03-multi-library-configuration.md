# Multi-Library Configuration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users register multiple Element+ library folders (a locked default plus user-added externals), pick which one SCADA Builder V2 browses, and pick which one Studio Element+ saves a new component into.

**Architecture:** A new `LibraryRegistry` (Application layer, pure in-memory logic) models the ordered list of libraries (default + externals) and the mutations allowed on each. A new `LibraryRegistryStore` (Infrastructure layer) persists only the external entries to a shared `%AppData%/ScadaBuilderV2/libraries.json` file — the default entry's path is never persisted, it is recomputed on every read from the existing reference-project logic so it can never go stale if the repository moves. `ScadaBuilderV2.App` gets a new `ConfigurationWindow` (opened from the now-enabled `tool.settings` ribbon command) to manage the list, and a `ComboBox` in its "Librairie" panel to pick the active one. `ScadaBuilderV2.ElementStudio.App` gets a split-button ("Ajouter à la librairie") whose arrow lists the same registry so a saved component can target any configured library. The two WPF apps are separate processes; each reads `libraries.json` independently when it needs it — no live cross-process sync.

**Tech Stack:** C# / .NET 8, WPF (XAML), MSTest, `System.Text.Json`, `Microsoft.Win32.OpenFolderDialog` (already used elsewhere in this codebase for folder pickers).

## Global Constraints

- The default library entry's path is never written to `libraries.json` — only external entries are persisted. Only the default entry's **name** is user-editable; its path and existence in the list are locked (cannot be changed or removed).
- `libraries.json` lives at `%AppData%/ScadaBuilderV2/libraries.json` (via `Environment.SpecialFolder.ApplicationData`). Missing file = empty external list, not an error.
- `Add` rejects a path that is already registered (comparing full, trailing-separator-trimmed, case-insensitive paths) — no silent duplicates.
- The active library selected in `ScadaBuilderV2.App`'s "Librairie" panel is a session-only choice — it is never persisted and always resets to the default library on next launch.
- In Studio Element+, the main "Ajouter à la librairie" button (the button body, not the arrow) keeps its exact current behavior (opens the existing Save dialog defaulting to `ResolveDefaultSepDirectory()`) — this task must not change that code path. Only the arrow is new.
- No new tabs are added to `ConfigurationWindow` beyond "Librairie" (YAGNI — no speculative empty tabs).
- No confirmation dialog is required before removing an external library (not requested by the spec).

---

## Before You Start

This plan continues directly on branch `master` in `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2` (per the user's prior choice to work without a feature branch). There may still be a pre-existing unrelated uncommitted change to `projects/AMR_REF_SCADA_V2/scenes/win00008.scene.json` in the working tree — do not touch, stage, or commit that file in any task below.

---

### Task 1: `LibraryEntry` + `LibraryRegistry` (Application layer)

**Files:**
- Create: `src/ScadaBuilderV2.Application/Libraries/LibraryRegistry.cs`
- Test: `tests/ScadaBuilderV2.Tests/LibraryRegistryTests.cs`

**Interfaces:**
- Produces: `namespace ScadaBuilderV2.Application.Libraries;` containing `public sealed record LibraryEntry(string Name, string Path, bool IsDefault);` and `public sealed class LibraryRegistry` with:
  - `LibraryRegistry(LibraryEntry defaultEntry, IEnumerable<LibraryEntry> externalEntries)` — throws `ArgumentException` if `defaultEntry.IsDefault` is `false`.
  - `IReadOnlyList<LibraryEntry> Entries { get; }` — default entry first, then externals, in insertion order.
  - `IReadOnlyList<LibraryEntry> ExternalEntries { get; }`
  - `void Add(string name, string path)` — appends an external entry; throws `InvalidOperationException` if the (normalized) path already exists anywhere in `Entries`.
  - `void Rename(string currentName, string newName)` — works for the default entry (matched by name) and for external entries; throws `InvalidOperationException` if no entry (default or external) has `currentName`.
  - `void UpdatePath(string name, string newPath)` — external entries only; throws `InvalidOperationException` if `name` matches the default entry's name or no external entry matches.
  - `void Remove(string name)` — external entries only; same throw rule as `UpdatePath`.
- Consumes: nothing from other tasks (foundational).

- [ ] **Step 1: Write the failing tests**

Create `tests/ScadaBuilderV2.Tests/LibraryRegistryTests.cs`:

```csharp
using ScadaBuilderV2.Application.Libraries;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class LibraryRegistryTests
{
    private static LibraryEntry CreateDefaultEntry(string path = @"C:\libs\default")
        => new("Defaut", path, IsDefault: true);

    [TestMethod]
    public void EntriesReturnsDefaultFirstThenExternalEntries()
    {
        var registry = new LibraryRegistry(
            CreateDefaultEntry(),
            new[] { new LibraryEntry("Externe", @"C:\libs\externe", IsDefault: false) });

        var entries = registry.Entries;

        Assert.AreEqual(2, entries.Count);
        Assert.IsTrue(entries[0].IsDefault);
        Assert.AreEqual("Defaut", entries[0].Name);
        Assert.AreEqual("Externe", entries[1].Name);
    }

    [TestMethod]
    public void ConstructorRejectsDefaultEntryWithIsDefaultFalse()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            new LibraryRegistry(new LibraryEntry("Defaut", @"C:\libs\default", IsDefault: false), []));
    }

    [TestMethod]
    public void AddAppendsExternalEntry()
    {
        var registry = new LibraryRegistry(CreateDefaultEntry(), []);

        registry.Add("Externe", @"C:\libs\externe");

        Assert.AreEqual(2, registry.Entries.Count);
        Assert.AreEqual("Externe", registry.ExternalEntries[0].Name);
        Assert.IsFalse(registry.ExternalEntries[0].IsDefault);
    }

    [TestMethod]
    public void AddRejectsDuplicatePath()
    {
        var registry = new LibraryRegistry(CreateDefaultEntry(@"C:\libs\shared"), []);
        registry.Add("Externe", @"C:\libs\externe");

        Assert.ThrowsException<InvalidOperationException>(() => registry.Add("Autre", @"C:\libs\externe"));
        Assert.ThrowsException<InvalidOperationException>(() => registry.Add("Doublon defaut", @"C:\libs\shared"));
    }

    [TestMethod]
    public void RenameUpdatesDefaultEntryName()
    {
        var registry = new LibraryRegistry(CreateDefaultEntry(), []);

        registry.Rename("Defaut", "Librairie principale");

        Assert.AreEqual("Librairie principale", registry.Entries[0].Name);
        Assert.IsTrue(registry.Entries[0].IsDefault);
    }

    [TestMethod]
    public void RenameUpdatesExternalEntryName()
    {
        var registry = new LibraryRegistry(CreateDefaultEntry(), []);
        registry.Add("Externe", @"C:\libs\externe");

        registry.Rename("Externe", "Externe renommee");

        Assert.AreEqual("Externe renommee", registry.ExternalEntries[0].Name);
    }

    [TestMethod]
    public void RenameThrowsWhenNameNotFound()
    {
        var registry = new LibraryRegistry(CreateDefaultEntry(), []);

        Assert.ThrowsException<InvalidOperationException>(() => registry.Rename("Introuvable", "Nouveau nom"));
    }

    [TestMethod]
    public void UpdatePathRejectsDefaultEntry()
    {
        var registry = new LibraryRegistry(CreateDefaultEntry(), []);

        Assert.ThrowsException<InvalidOperationException>(() => registry.UpdatePath("Defaut", @"C:\libs\new-path"));
    }

    [TestMethod]
    public void UpdatePathChangesExternalEntryPath()
    {
        var registry = new LibraryRegistry(CreateDefaultEntry(), []);
        registry.Add("Externe", @"C:\libs\externe");

        registry.UpdatePath("Externe", @"C:\libs\externe-v2");

        Assert.AreEqual(@"C:\libs\externe-v2", registry.ExternalEntries[0].Path);
    }

    [TestMethod]
    public void RemoveRejectsDefaultEntry()
    {
        var registry = new LibraryRegistry(CreateDefaultEntry(), []);

        Assert.ThrowsException<InvalidOperationException>(() => registry.Remove("Defaut"));
    }

    [TestMethod]
    public void RemoveDeletesExternalEntry()
    {
        var registry = new LibraryRegistry(CreateDefaultEntry(), []);
        registry.Add("Externe", @"C:\libs\externe");

        registry.Remove("Externe");

        Assert.AreEqual(0, registry.ExternalEntries.Count);
        Assert.AreEqual(1, registry.Entries.Count);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~LibraryRegistryTests"`
Expected: build error / FAIL — `ScadaBuilderV2.Application.Libraries` namespace does not exist yet.

- [ ] **Step 3: Implement `LibraryEntry` and `LibraryRegistry`**

Create `src/ScadaBuilderV2.Application/Libraries/LibraryRegistry.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~LibraryRegistryTests"`
Expected: all `LibraryRegistryTests` PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Application/Libraries/LibraryRegistry.cs tests/ScadaBuilderV2.Tests/LibraryRegistryTests.cs
git commit -m "feat: add LibraryEntry/LibraryRegistry for multi-library configuration"
```

---

### Task 2: `LibraryRegistryStore` (Infrastructure persistence)

**Files:**
- Create: `src/ScadaBuilderV2.Infrastructure/Libraries/LibraryRegistryStore.cs`
- Test: `tests/ScadaBuilderV2.Tests/LibraryRegistryStoreTests.cs`

**Interfaces:**
- Consumes: `ScadaBuilderV2.Application.Libraries.LibraryEntry` from Task 1.
- Produces: `namespace ScadaBuilderV2.Infrastructure.Libraries;` containing `public sealed class LibraryRegistryStore` with:
  - `public static string GetDefaultSettingsPath()` — returns `%AppData%/ScadaBuilderV2/libraries.json`.
  - `public Task<IReadOnlyList<LibraryEntry>> ReadExternalEntriesAsync(string? settingsPath = null, CancellationToken cancellationToken = default)` — returns an empty list if the file does not exist; every returned entry has `IsDefault == false`.
  - `public Task WriteExternalEntriesAsync(IReadOnlyList<LibraryEntry> externalEntries, string? settingsPath = null, CancellationToken cancellationToken = default)` — throws `ArgumentException` if any entry has `IsDefault == true`; creates the parent directory if missing.

- [ ] **Step 1: Write the failing tests**

Create `tests/ScadaBuilderV2.Tests/LibraryRegistryStoreTests.cs`:

```csharp
using ScadaBuilderV2.Application.Libraries;
using ScadaBuilderV2.Infrastructure.Libraries;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class LibraryRegistryStoreTests
{
    private string _tempSettingsPath = "";

    [TestInitialize]
    public void Setup()
    {
        _tempSettingsPath = Path.Combine(Path.GetTempPath(), $"scada-builder-v2-libraries-test-{Guid.NewGuid():N}.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(_tempSettingsPath))
        {
            File.Delete(_tempSettingsPath);
        }
    }

    [TestMethod]
    public async Task ReadExternalEntriesAsyncReturnsEmptyListWhenFileMissing()
    {
        var store = new LibraryRegistryStore();

        var entries = await store.ReadExternalEntriesAsync(_tempSettingsPath);

        Assert.AreEqual(0, entries.Count);
    }

    [TestMethod]
    public async Task WriteThenReadRoundTripsExternalEntries()
    {
        var store = new LibraryRegistryStore();
        var entries = new[]
        {
            new LibraryEntry("Externe A", @"C:\libs\a", IsDefault: false),
            new LibraryEntry("Externe B", @"C:\libs\b", IsDefault: false)
        };

        await store.WriteExternalEntriesAsync(entries, _tempSettingsPath);
        var reloaded = await store.ReadExternalEntriesAsync(_tempSettingsPath);

        Assert.AreEqual(2, reloaded.Count);
        Assert.AreEqual("Externe A", reloaded[0].Name);
        Assert.AreEqual(@"C:\libs\a", reloaded[0].Path);
        Assert.IsFalse(reloaded[0].IsDefault);
        Assert.AreEqual("Externe B", reloaded[1].Name);
    }

    [TestMethod]
    public async Task WriteExternalEntriesAsyncRejectsDefaultEntry()
    {
        var store = new LibraryRegistryStore();
        var entries = new[] { new LibraryEntry("Defaut", @"C:\libs\default", IsDefault: true) };

        await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.WriteExternalEntriesAsync(entries, _tempSettingsPath));
    }

    [TestMethod]
    public async Task WriteExternalEntriesAsyncCreatesParentDirectory()
    {
        var nestedDirectory = Path.Combine(Path.GetTempPath(), $"scada-builder-v2-libraries-test-{Guid.NewGuid():N}");
        var nestedPath = Path.Combine(nestedDirectory, "libraries.json");
        var store = new LibraryRegistryStore();

        try
        {
            await store.WriteExternalEntriesAsync(Array.Empty<LibraryEntry>(), nestedPath);

            Assert.IsTrue(File.Exists(nestedPath));
        }
        finally
        {
            if (Directory.Exists(nestedDirectory))
            {
                Directory.Delete(nestedDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void GetDefaultSettingsPathEndsWithExpectedRelativePath()
    {
        var path = LibraryRegistryStore.GetDefaultSettingsPath();

        StringAssert.EndsWith(path, Path.Combine("ScadaBuilderV2", "libraries.json"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~LibraryRegistryStoreTests"`
Expected: build error / FAIL — `ScadaBuilderV2.Infrastructure.Libraries` namespace does not exist yet.

- [ ] **Step 3: Implement `LibraryRegistryStore`**

Create `src/ScadaBuilderV2.Infrastructure/Libraries/LibraryRegistryStore.cs`:

```csharp
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

        await using var read = File.OpenRead(path);
        var records = await JsonSerializer.DeserializeAsync<List<LibraryEntryRecord>>(read, SerializerOptions, cancellationToken)
            ?? [];

        return records
            .Select(record => new LibraryEntry(record.Name, record.Path, IsDefault: false))
            .ToArray();
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
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~LibraryRegistryStoreTests"`
Expected: all `LibraryRegistryStoreTests` PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Infrastructure/Libraries/LibraryRegistryStore.cs tests/ScadaBuilderV2.Tests/LibraryRegistryStoreTests.cs
git commit -m "feat: add LibraryRegistryStore for persisting external library paths"
```

---

### Task 3: `ConfigurationWindow` + enable `tool.settings`

**Files:**
- Create: `src/ScadaBuilderV2.App/LibraryNameDialog.xaml`
- Create: `src/ScadaBuilderV2.App/LibraryNameDialog.xaml.cs`
- Create: `src/ScadaBuilderV2.App/ConfigurationWindow.xaml`
- Create: `src/ScadaBuilderV2.App/ConfigurationWindow.xaml.cs`
- Modify: `src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs:85` (enable `tool.settings`)
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs` (add `case "tool.settings":`, `OpenConfigurationWindowAsync`, `BuildLibraryRegistryAsync`)
- Test: `tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs` (extend)
- Test: `tests/ScadaBuilderV2.Tests/ConfigurationWindowContractTests.cs` (new)

**Interfaces:**
- Consumes: `LibraryEntry`/`LibraryRegistry` (Task 1), `LibraryRegistryStore` (Task 2). `RibbonCommandCatalog.Enabled(string id, string label, string iconKey, string toolTip)` helper (existing, in `RibbonCommandCatalog.cs`). `MainWindow`'s existing `ResolveElementPlusLibraryRoot(bool create)` (`MainWindow.xaml.cs:889-907`) and `ExecuteRibbonCommand(string commandId)` switch (`MainWindow.xaml.cs:5821` onward).
- Produces: `ScadaBuilderV2.App.ConfigurationWindow` — public constructor `ConfigurationWindow(LibraryRegistry registry)`, public property `LibraryRegistry Registry { get; }`, standard `Window.ShowDialog()` returns `true` on OK. `ScadaBuilderV2.App.LibraryNameDialog` — public constructor `LibraryNameDialog(string prompt, string initialName)`, public property `string EnteredName { get; private set; }`. `MainWindow`'s new `private async Task<LibraryRegistry> BuildLibraryRegistryAsync()` and `private async Task OpenConfigurationWindowAsync()` — Task 4 will extend `OpenConfigurationWindowAsync`'s body.

Because this repo's test project cannot reference the WPF `ScadaBuilderV2.App` project (it's a `net8.0-windows` WPF app, not a class library the tests link against), verification for the WPF pieces of this task follows the existing pattern in `RibbonCommandCatalogTests.cs`: read the `.xaml`/`.xaml.cs` files as text and assert on their contents. Full interactive verification (opening the window, clicking through it) is manual — there is no WPF UI test harness in this codebase.

- [ ] **Step 1: Write the failing tests**

Add to `tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs` (after `ToolsTabExposesElementStudioCommand`):

```csharp
    [TestMethod]
    public void ToolsTabExposesEnabledSettingsCommand()
    {
        var tabs = RibbonCommandCatalog.CreateDefault();
        var toolsCommands = tabs["Tools"].SelectMany(group => group.Commands).ToArray();
        var settingsCommand = toolsCommands.SingleOrDefault(command => command.Id == "tool.settings");

        Assert.IsNotNull(settingsCommand, "Tools tab should expose the tool.settings command.");
        Assert.IsTrue(settingsCommand!.IsEnabled);
        Assert.IsNull(settingsCommand.DisabledReason);
    }
```

Create `tests/ScadaBuilderV2.Tests/ConfigurationWindowContractTests.cs`:

```csharp
namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ConfigurationWindowContractTests
{
    [TestMethod]
    public void ConfigurationWindowXamlExposesLibraryTabControls()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "ConfigurationWindow.xaml");

        StringAssert.Contains(xaml, "Header=\"Librairie\"");
        StringAssert.Contains(xaml, "x:Name=\"LibraryListView\"");
        StringAssert.Contains(xaml, "x:Name=\"AddLibraryButton\"");
        StringAssert.Contains(xaml, "x:Name=\"RenameLibraryButton\"");
        StringAssert.Contains(xaml, "x:Name=\"ChangePathLibraryButton\"");
        StringAssert.Contains(xaml, "x:Name=\"RemoveLibraryButton\"");
    }

    [TestMethod]
    public void ConfigurationWindowCodeGuardsDefaultEntryAndUsesRegistryMutations()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.App", "ConfigurationWindow.xaml.cs");

        StringAssert.Contains(code, "selected.IsDefault");
        StringAssert.Contains(code, "Registry.Add(");
        StringAssert.Contains(code, "Registry.Rename(");
        StringAssert.Contains(code, "Registry.UpdatePath(");
        StringAssert.Contains(code, "Registry.Remove(");
    }

    [TestMethod]
    public void MainWindowOpensConfigurationWindowFromToolSettingsCommand()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");

        StringAssert.Contains(code, "case \"tool.settings\":");
        StringAssert.Contains(code, "OpenConfigurationWindowAsync");
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate project file: {Path.Combine(parts)}");
        return "";
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ToolsTabExposesEnabledSettingsCommand|FullyQualifiedName~ConfigurationWindowContractTests"`
Expected: `ToolsTabExposesEnabledSettingsCommand` FAILs (`tool.settings` still `Disabled`); all three `ConfigurationWindowContractTests` FAIL with `Assert.Fail("Unable to locate project file: ...ConfigurationWindow.xaml")` (Task-2 assertions) or a missing-string failure (Task-3 assertion, since `case "tool.settings":` doesn't exist yet).

- [ ] **Step 3: Enable `tool.settings` in the catalog**

In `src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs`, change:

```csharp
                Group("Configuration",
                    Disabled("tool.settings", "Configurer", "Icon.Tool.Settings", "Configurer les outils a venir"),
                    Enabled("tool.element-studio", "Studio E+", "Icon.Tool.ElementStudio", "Ouvrir Studio Element+ (editeur de composants Element+)"))
```

to:

```csharp
                Group("Configuration",
                    Enabled("tool.settings", "Configurer", "Icon.Tool.Settings", "Ouvrir la configuration (librairies)"),
                    Enabled("tool.element-studio", "Studio E+", "Icon.Tool.ElementStudio", "Ouvrir Studio Element+ (editeur de composants Element+)"))
```

- [ ] **Step 4: Create `LibraryNameDialog`**

Create `src/ScadaBuilderV2.App/LibraryNameDialog.xaml`:

```xml
<Window x:Class="ScadaBuilderV2.App.LibraryNameDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Nom de la librairie"
        Width="360"
        Height="180"
        MinWidth="320"
        MinHeight="180"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize"
        ShowInTaskbar="False">
    <Window.Resources>
        <SolidColorBrush x:Key="InkBrush" Color="#0F2A30"/>
        <SolidColorBrush x:Key="BorderBrushSoft" Color="#DCE8DD"/>
        <Style TargetType="Button">
            <Setter Property="MinHeight" Value="32"/>
            <Setter Property="Padding" Value="12,6"/>
            <Setter Property="Margin" Value="4"/>
            <Setter Property="BorderBrush" Value="{StaticResource BorderBrushSoft}"/>
        </Style>
        <Style x:Key="PrimaryButtonStyle" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
            <Setter Property="Background" Value="#2090A0"/>
            <Setter Property="BorderBrush" Value="#0F7280"/>
            <Setter Property="Foreground" Value="#FFFFFF"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
        </Style>
    </Window.Resources>
    <DockPanel Margin="16">
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
            <Button Content="OK" Style="{StaticResource PrimaryButtonStyle}" Click="OnOkClick" IsDefault="True"/>
            <Button Content="Annuler" Click="OnCancelClick" IsCancel="True"/>
        </StackPanel>
        <StackPanel>
            <TextBlock x:Name="PromptText" Text="Nom de la librairie" Foreground="{StaticResource InkBrush}" Margin="0,0,0,6"/>
            <TextBox x:Name="NameTextBox" MinHeight="30"/>
            <TextBlock x:Name="ErrorText" Foreground="Red" Margin="0,6,0,0" TextWrapping="Wrap" Visibility="Collapsed"/>
        </StackPanel>
    </DockPanel>
</Window>
```

Create `src/ScadaBuilderV2.App/LibraryNameDialog.xaml.cs`:

```csharp
using System.Windows;

namespace ScadaBuilderV2.App;

public partial class LibraryNameDialog : Window
{
    public LibraryNameDialog(string prompt, string initialName)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        NameTextBox.Text = initialName;
        NameTextBox.SelectAll();
        Loaded += (_, _) => NameTextBox.Focus();
    }

    public string EnteredName { get; private set; } = "";

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        var trimmed = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            ErrorText.Text = "Le nom ne peut pas etre vide.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        EnteredName = trimmed;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
```

- [ ] **Step 5: Create `ConfigurationWindow`**

Create `src/ScadaBuilderV2.App/ConfigurationWindow.xaml`:

```xml
<Window x:Class="ScadaBuilderV2.App.ConfigurationWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Configuration"
        Width="560"
        Height="440"
        MinWidth="480"
        MinHeight="360"
        WindowStartupLocation="CenterOwner"
        ResizeMode="CanResize">
    <Window.Resources>
        <SolidColorBrush x:Key="InkBrush" Color="#0F2A30"/>
        <SolidColorBrush x:Key="MutedBrush" Color="#5E7A82"/>
        <SolidColorBrush x:Key="BorderBrushSoft" Color="#DCE8DD"/>
        <Style TargetType="Button">
            <Setter Property="MinHeight" Value="32"/>
            <Setter Property="Padding" Value="12,6"/>
            <Setter Property="Margin" Value="4,4,0,4"/>
            <Setter Property="BorderBrush" Value="{StaticResource BorderBrushSoft}"/>
        </Style>
        <Style x:Key="PrimaryButtonStyle" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
            <Setter Property="Background" Value="#2090A0"/>
            <Setter Property="BorderBrush" Value="#0F7280"/>
            <Setter Property="Foreground" Value="#FFFFFF"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
        </Style>
    </Window.Resources>
    <DockPanel Margin="16">
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
            <Button Content="OK" Style="{StaticResource PrimaryButtonStyle}" Click="OnOkClick" IsDefault="True"/>
            <Button Content="Annuler" Click="OnCancelClick" IsCancel="True"/>
        </StackPanel>
        <TabControl>
            <TabItem Header="Librairie">
                <DockPanel Margin="8">
                    <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="0,8,0,0">
                        <Button x:Name="AddLibraryButton" Content="Ajouter" Click="OnAddLibraryClick"/>
                        <Button x:Name="RenameLibraryButton" Content="Renommer" Click="OnRenameLibraryClick"/>
                        <Button x:Name="ChangePathLibraryButton" Content="Modifier le chemin" Click="OnChangePathLibraryClick"/>
                        <Button x:Name="RemoveLibraryButton" Content="Supprimer" Click="OnRemoveLibraryClick"/>
                    </StackPanel>
                    <ListView x:Name="LibraryListView"
                              BorderBrush="{StaticResource BorderBrushSoft}"
                              BorderThickness="1"
                              SelectionChanged="OnLibraryListViewSelectionChanged">
                        <ListView.View>
                            <GridView>
                                <GridViewColumn Header="Nom" DisplayMemberBinding="{Binding Name}" Width="200"/>
                                <GridViewColumn Header="Chemin" DisplayMemberBinding="{Binding Path}" Width="280"/>
                            </GridView>
                        </ListView.View>
                    </ListView>
                </DockPanel>
            </TabItem>
        </TabControl>
    </DockPanel>
</Window>
```

Create `src/ScadaBuilderV2.App/ConfigurationWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Application.Libraries;

namespace ScadaBuilderV2.App;

public partial class ConfigurationWindow : Window
{
    public ConfigurationWindow(LibraryRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Registry = registry;
        InitializeComponent();
        RefreshLibraryListView();
    }

    public LibraryRegistry Registry { get; }

    private void RefreshLibraryListView()
    {
        LibraryListView.ItemsSource = Registry.Entries;
        UpdateButtonStates();
    }

    private void OnLibraryListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var selected = LibraryListView.SelectedItem as LibraryEntry;
        RenameLibraryButton.IsEnabled = selected is not null;
        var isExternalSelected = selected is not null && !selected.IsDefault;
        ChangePathLibraryButton.IsEnabled = isExternalSelected;
        RemoveLibraryButton.IsEnabled = isExternalSelected;
    }

    private void OnAddLibraryClick(object sender, RoutedEventArgs e)
    {
        var folderDialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Selectionner le dossier de la librairie"
        };

        if (folderDialog.ShowDialog(this) != true)
        {
            return;
        }

        var defaultName = System.IO.Path.GetFileName(folderDialog.FolderName.TrimEnd('\\', '/'));
        var nameDialog = new LibraryNameDialog("Nom de la nouvelle librairie", defaultName) { Owner = this };
        if (nameDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            Registry.Add(nameDialog.EnteredName, folderDialog.FolderName);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, "Ajouter une librairie", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RefreshLibraryListView();
    }

    private void OnRenameLibraryClick(object sender, RoutedEventArgs e)
    {
        if (LibraryListView.SelectedItem is not LibraryEntry selected)
        {
            return;
        }

        var nameDialog = new LibraryNameDialog("Nouveau nom de la librairie", selected.Name) { Owner = this };
        if (nameDialog.ShowDialog() != true)
        {
            return;
        }

        Registry.Rename(selected.Name, nameDialog.EnteredName);
        RefreshLibraryListView();
    }

    private void OnChangePathLibraryClick(object sender, RoutedEventArgs e)
    {
        if (LibraryListView.SelectedItem is not LibraryEntry selected || selected.IsDefault)
        {
            return;
        }

        var folderDialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Selectionner le nouveau dossier de la librairie",
            InitialDirectory = selected.Path
        };

        if (folderDialog.ShowDialog(this) != true)
        {
            return;
        }

        Registry.UpdatePath(selected.Name, folderDialog.FolderName);
        RefreshLibraryListView();
    }

    private void OnRemoveLibraryClick(object sender, RoutedEventArgs e)
    {
        if (LibraryListView.SelectedItem is not LibraryEntry selected || selected.IsDefault)
        {
            return;
        }

        Registry.Remove(selected.Name);
        RefreshLibraryListView();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
```

- [ ] **Step 6: Wire `tool.settings` in `MainWindow`**

In `src/ScadaBuilderV2.App/MainWindow.xaml.cs`, add these two usings alongside the existing `using ScadaBuilderV2.Infrastructure.ElementStudio;` line (around line 23):

```csharp
using ScadaBuilderV2.Application.Libraries;
using ScadaBuilderV2.Infrastructure.Libraries;
```

Add a new field next to the existing `_elementStudioComponentPackageStore` field (around line 44):

```csharp
    private readonly LibraryRegistryStore _libraryRegistryStore = new();
```

Add these two methods near `ResolveElementPlusLibraryRoot` (after its closing brace, around line 907):

```csharp
    private async Task<LibraryRegistry> BuildLibraryRegistryAsync()
    {
        var defaultPath = ResolveElementPlusLibraryRoot(create: true)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SCADA_BUILDER_V2", "library", "elements");
        var defaultEntry = new LibraryEntry("Defaut", defaultPath, IsDefault: true);
        var externalEntries = await _libraryRegistryStore.ReadExternalEntriesAsync();
        return new LibraryRegistry(defaultEntry, externalEntries);
    }

    private async Task OpenConfigurationWindowAsync()
    {
        var registry = await BuildLibraryRegistryAsync();
        var dialog = new ConfigurationWindow(registry) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            await _libraryRegistryStore.WriteExternalEntriesAsync(dialog.Registry.ExternalEntries);
        }
    }
```

In `ExecuteRibbonCommand`'s switch (`MainWindow.xaml.cs:5821` onward), add a case right after the existing `tool.element-studio` case:

```csharp
            case "tool.element-studio":
                await OpenElementStudioFromToolPaletteAsync();
                break;
            case "tool.settings":
                await OpenConfigurationWindowAsync();
                break;
```

- [ ] **Step 7: Build and run the tests to verify they pass**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: `Build succeeded.`

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ToolsTabExposesEnabledSettingsCommand|FullyQualifiedName~ConfigurationWindowContractTests"`
Expected: all 4 tests PASS.

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: same pass/fail counts as before this task, plus the new tests passing (the two pre-existing unrelated failures from before this plan — `ScadaBuilderLaunchesStudioFromProjectInDevelopmentToAvoidStaleBinaries` and `LegacyContextMenuExposesElementStudioCommand` — are still expected to fail; they are untouched by this task).

- [ ] **Step 8: Commit**

```bash
git add src/ScadaBuilderV2.App/LibraryNameDialog.xaml src/ScadaBuilderV2.App/LibraryNameDialog.xaml.cs src/ScadaBuilderV2.App/ConfigurationWindow.xaml src/ScadaBuilderV2.App/ConfigurationWindow.xaml.cs src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs tests/ScadaBuilderV2.Tests/ConfigurationWindowContractTests.cs
git commit -m "feat: add ConfigurationWindow with library management, enable tool.settings"
```

---

### Task 4: Active library selector in SCADA Builder V2's "Librairie" panel

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml:1082-1084`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Test: `tests/ScadaBuilderV2.Tests/ConfigurationWindowContractTests.cs` (extend)

**Interfaces:**
- Consumes: `LibraryEntry`/`LibraryRegistry` (Task 1), `BuildLibraryRegistryAsync()` and `OpenConfigurationWindowAsync()` (Task 3, both in `MainWindow.xaml.cs`), existing `RefreshElementLibraryAsync()` (`MainWindow.xaml.cs:790-819`) and `StartElementLibraryWatcher()` (`MainWindow.xaml.cs:821-851`).
- Produces: `MainWindow`'s new `private string? ResolveActiveLibraryRoot(bool create)` and `private async Task RefreshLibrarySelectorAsync()`, used by later maintenance of this panel.

- [ ] **Step 1: Write the failing tests**

Add to `tests/ScadaBuilderV2.Tests/ConfigurationWindowContractTests.cs`:

```csharp
    [TestMethod]
    public void MainWindowXamlReplacesLibraryLabelWithSelectorComboBox()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml");

        StringAssert.Contains(xaml, "x:Name=\"LibrarySelectorComboBox\"");
        StringAssert.Contains(xaml, "SelectionChanged=\"OnLibrarySelectorSelectionChanged\"");
        Assert.IsFalse(xaml.Contains("Text=\"Element+ disponibles\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MainWindowUsesActiveLibraryRootForRefreshAndWatcher()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");

        StringAssert.Contains(code, "private string? ResolveActiveLibraryRoot(bool create)");
        StringAssert.Contains(code, "private async Task RefreshLibrarySelectorAsync()");
        StringAssert.Contains(code, "OnLibrarySelectorSelectionChanged");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~MainWindowXamlReplacesLibraryLabelWithSelectorComboBox|FullyQualifiedName~MainWindowUsesActiveLibraryRootForRefreshAndWatcher"`
Expected: both FAIL (the ComboBox, `ResolveActiveLibraryRoot`, and `RefreshLibrarySelectorAsync` don't exist yet).

- [ ] **Step 3: Replace the label with a ComboBox in `MainWindow.xaml`**

In `src/ScadaBuilderV2.App/MainWindow.xaml`, change:

```xml
                                    <TextBlock Text="Element+ disponibles"
                                               FontWeight="SemiBold"
                                               Foreground="{StaticResource InkBrush}"/>
```

to:

```xml
                                    <ComboBox x:Name="LibrarySelectorComboBox"
                                              DisplayMemberPath="Name"
                                              Margin="0,0,0,4"
                                              SelectionChanged="OnLibrarySelectorSelectionChanged"/>
```

- [ ] **Step 4: Route library loading through the active selection**

In `src/ScadaBuilderV2.App/MainWindow.xaml.cs`, add two fields next to `_libraryRegistryStore` (from Task 3):

```csharp
    private IReadOnlyList<LibraryEntry> _libraryEntries = [];
    private LibraryEntry? _selectedLibraryEntry;
```

Change `RefreshElementLibraryAsync()`'s first line (`MainWindow.xaml.cs:792`) from:

```csharp
        var libraryRoot = ResolveElementPlusLibraryRoot(create: true);
```

to:

```csharp
        var libraryRoot = ResolveActiveLibraryRoot(create: true);
```

Change `StartElementLibraryWatcher()`'s library-root line (`MainWindow.xaml.cs:825`) from:

```csharp
        var libraryRoot = ResolveElementPlusLibraryRoot(create: true);
```

to:

```csharp
        var libraryRoot = ResolveActiveLibraryRoot(create: true);
```

Add a new `ResolveActiveLibraryRoot` method right after `ResolveElementPlusLibraryRoot`'s closing brace (leave `ResolveElementPlusLibraryRoot` itself unchanged — it is still used by `BuildLibraryRegistryAsync` to compute the default entry's path):

```csharp
    private string? ResolveActiveLibraryRoot(bool create)
    {
        var path = _selectedLibraryEntry?.Path;
        if (path is null)
        {
            return null;
        }

        if (create)
        {
            Directory.CreateDirectory(path);
        }

        return path;
    }
```

Add the selector refresh and selection-changed handler, near `RefreshElementLibraryAsync`:

```csharp
    private async Task RefreshLibrarySelectorAsync()
    {
        var registry = await BuildLibraryRegistryAsync();
        var previousName = _selectedLibraryEntry?.Name;
        _libraryEntries = registry.Entries;
        LibrarySelectorComboBox.ItemsSource = _libraryEntries;
        var toSelect = _libraryEntries.FirstOrDefault(entry => entry.Name == previousName) ?? _libraryEntries[0];
        LibrarySelectorComboBox.SelectedItem = toSelect;
    }

    private async void OnLibrarySelectorSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedLibraryEntry = LibrarySelectorComboBox.SelectedItem as LibraryEntry;
        StartElementLibraryWatcher();
        await RefreshElementLibraryAsync();
    }
```

Find the existing initialization call (`MainWindow.xaml.cs:166-167`):

```csharp
        StartElementLibraryWatcher();
        await RefreshElementLibraryAsync();
```

Replace it with:

```csharp
        await RefreshLibrarySelectorAsync();
```

(Setting `LibrarySelectorComboBox.SelectedItem` inside `RefreshLibrarySelectorAsync` triggers `OnLibrarySelectorSelectionChanged`, which starts the watcher and refreshes the list box — so this single call replaces both original lines.)

Finally, update `OpenConfigurationWindowAsync` (added in Task 3) so closing the Configuration window with OK refreshes the selector's available entries:

```csharp
    private async Task OpenConfigurationWindowAsync()
    {
        var registry = await BuildLibraryRegistryAsync();
        var dialog = new ConfigurationWindow(registry) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            await _libraryRegistryStore.WriteExternalEntriesAsync(dialog.Registry.ExternalEntries);
            await RefreshLibrarySelectorAsync();
        }
    }
```

- [ ] **Step 5: Build and run the tests to verify they pass**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: `Build succeeded.`

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~MainWindowXamlReplacesLibraryLabelWithSelectorComboBox|FullyQualifiedName~MainWindowUsesActiveLibraryRootForRefreshAndWatcher"`
Expected: both PASS.

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: same result profile as the end of Task 3 (all new/updated tests passing; the same 2 pre-existing unrelated failures still present).

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/ConfigurationWindowContractTests.cs
git commit -m "feat: add active library selector to SCADA Builder V2's Librairie panel"
```

---

### Task 5: "Ajouter à la librairie" split-button in Studio Element+

**Files:**
- Modify: `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml:109`
- Modify: `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml.cs`
- Test: `tests/ScadaBuilderV2.Tests/ConfigurationWindowContractTests.cs` (extend)

**Interfaces:**
- Consumes: `LibraryEntry` (Task 1), `LibraryRegistryStore` (Task 2). Existing `ResolveDefaultSepDirectory()` (`MainWindow.xaml.cs:814-845`), `CreateCurrentComponentPackage()` (`MainWindow.xaml.cs:561`), `componentPackageStore.WriteToLibraryAsync(ElementStudioComponentPackage, string, CancellationToken)` (`ElementStudioComponentPackageStore.cs:13-23`), existing field `currentSepPath`, existing `OnSaveComponentAsClick`/`SaveComponentAsAsync()` (`MainWindow.xaml.cs:92-116`, unchanged by this task).
- Produces: nothing consumed by a later task (final task in this plan).

- [ ] **Step 1: Write the failing tests**

Add to `tests/ScadaBuilderV2.Tests/ConfigurationWindowContractTests.cs`:

```csharp
    [TestMethod]
    public void ElementStudioXamlReplacesSaveAsSepButtonWithSplitButton()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml");

        StringAssert.Contains(xaml, "x:Name=\"AddToLibraryButton\"");
        StringAssert.Contains(xaml, "x:Name=\"AddToLibraryArrowButton\"");
        StringAssert.Contains(xaml, "Click=\"OnAddToLibraryArrowClick\"");
        Assert.IsFalse(xaml.Contains("Content=\"Save as .sep\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ElementStudioCodeSavesDirectlyToChosenLibraryFromArrowMenu()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "MainWindow.xaml.cs");

        StringAssert.Contains(code, "OnAddToLibraryArrowClick");
        StringAssert.Contains(code, "componentPackageStore.WriteToLibraryAsync(");
        StringAssert.Contains(code, "OnSaveComponentAsClick");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ElementStudioXamlReplacesSaveAsSepButtonWithSplitButton|FullyQualifiedName~ElementStudioCodeSavesDirectlyToChosenLibraryFromArrowMenu"`
Expected: `ElementStudioXamlReplacesSaveAsSepButtonWithSplitButton` FAILs (split-button controls don't exist yet). `ElementStudioCodeSavesDirectlyToChosenLibraryFromArrowMenu` FAILs on the `OnAddToLibraryArrowClick` assertion (the other two strings already exist today, so that alone would not fail — the missing method name is what fails it).

- [ ] **Step 3: Replace the "Save as .sep" button with a split-button**

In `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml`, change:

```xml
                            <Button Style="{StaticResource RibbonCommandButtonStyle}" Content="Save as .sep" Click="OnSaveComponentAsClick"/>
```

to:

```xml
                            <StackPanel Orientation="Horizontal" Margin="3,3,0,3">
                                <Button x:Name="AddToLibraryButton" Style="{StaticResource RibbonCommandButtonStyle}" Width="84" Margin="0" Content="Ajouter a la librairie" Click="OnSaveComponentAsClick"/>
                                <Button x:Name="AddToLibraryArrowButton" Style="{StaticResource RibbonCommandButtonStyle}" Width="20" Margin="0" Content="&#9662;" Click="OnAddToLibraryArrowClick"/>
                            </StackPanel>
```

- [ ] **Step 4: Add the library field, arrow handler, and menu-item handler**

In `src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml.cs`, add these usings alongside the existing `using ScadaBuilderV2.Infrastructure.ElementStudio;` line (around line 12):

```csharp
using ScadaBuilderV2.Application.Libraries;
using ScadaBuilderV2.Infrastructure.Libraries;
```

Add a new field next to `componentPackageStore` (around line 19):

```csharp
    private readonly LibraryRegistryStore libraryRegistryStore = new();
```

Add these two handlers right after `SaveComponentAsync` (after its closing brace, around line 134):

```csharp
    private async void OnAddToLibraryArrowClick(object sender, RoutedEventArgs e)
    {
        var externalEntries = await libraryRegistryStore.ReadExternalEntriesAsync();
        var defaultEntry = new LibraryEntry("Defaut", ResolveDefaultSepDirectory(), IsDefault: true);
        var entries = new[] { defaultEntry }.Concat(externalEntries).ToArray();

        var menu = new ContextMenu();
        foreach (var entry in entries)
        {
            var menuItem = new MenuItem { Header = entry.Name, Tag = entry };
            menuItem.Click += OnLibraryMenuItemClick;
            menu.Items.Add(menuItem);
        }

        menu.PlacementTarget = AddToLibraryArrowButton;
        menu.IsOpen = true;
    }

    private async void OnLibraryMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: LibraryEntry entry })
        {
            return;
        }

        try
        {
            var package = CreateCurrentComponentPackage();
            var savedPath = await componentPackageStore.WriteToLibraryAsync(package, entry.Path);
            currentSepPath = savedPath;
            workspace.SavedComponentPath = savedPath;
            workspace.ComponentVisualKind = package.Component.Visual.Kind.ToString();
            workspace.Diagnostics.Add($"Composant ajoute a la librairie '{entry.Name}': {savedPath}");
        }
        catch (Exception ex)
        {
            workspace.Diagnostics.Add($"Ajout a la librairie impossible: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Ajouter a la librairie", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
```

- [ ] **Step 5: Build and run the tests to verify they pass**

Run: `dotnet build ScadaBuilderV2.sln`
Expected: `Build succeeded.`

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ElementStudioXamlReplacesSaveAsSepButtonWithSplitButton|FullyQualifiedName~ElementStudioCodeSavesDirectlyToChosenLibraryFromArrowMenu"`
Expected: both PASS.

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: same result profile as the end of Task 4 (all new/updated tests passing; the same 2 pre-existing unrelated failures still present — neither touches `ScadaBuilderV2.ElementStudio.App`).

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml src/ScadaBuilderV2.ElementStudio.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/ConfigurationWindowContractTests.cs
git commit -m "feat: add Ajouter a la librairie split-button to Studio Element+"
```

---

## Self-Review Notes

- **Spec coverage:** `LibraryEntry`/`LibraryRegistry` semantics (Task 1) match the spec's corrected Rename/UpdatePath/Remove rules exactly. Persistence to `%AppData%/ScadaBuilderV2/libraries.json`, default-entry exclusion, and missing-file handling (Task 2) match. `tool.settings` enablement and `ConfigurationWindow`'s single "Librairie" tab with Add/Rename/Modifier-chemin/Supprimer (Task 3) match — no speculative extra tabs. Active-library `ComboBox` replacing the static label, non-persisted selection (Task 4) matches. Split-button in Studio Element+ with unchanged main-button behavior and a new arrow menu that saves directly via `WriteToLibraryAsync` (Task 5) matches; no confirmation dialog was added for delete, per the spec's explicit out-of-scope note.
- **Placeholder scan:** none — every step carries complete, concrete code and exact commands.
- **Type consistency:** `LibraryEntry(string Name, string Path, bool IsDefault)` is used identically across all five tasks (Application, Infrastructure, both WPF apps). `LibraryRegistry.Entries`/`ExternalEntries`/`Add`/`Rename`/`UpdatePath`/`Remove` signatures from Task 1 are the exact signatures consumed in Tasks 3-4. `LibraryRegistryStore.ReadExternalEntriesAsync`/`WriteExternalEntriesAsync` signatures from Task 2 are the exact signatures consumed in Tasks 3 and 5. `BuildLibraryRegistryAsync`/`OpenConfigurationWindowAsync` introduced in Task 3 are extended (not redefined) in Task 4.
