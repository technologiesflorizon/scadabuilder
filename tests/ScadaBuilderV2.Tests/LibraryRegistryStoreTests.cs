using ScadaBuilderV2.Application.Libraries;
using ScadaBuilderV2.Infrastructure.Libraries;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class LibraryRegistryStoreTests
{
    private string _tempSettingsDirectory = "";
    private string _tempSettingsPath = "";

    [TestInitialize]
    public void Setup()
    {
        _tempSettingsDirectory = Path.Combine(Path.GetTempPath(), $"scada-builder-v2-libraries-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempSettingsDirectory);
        _tempSettingsPath = Path.Combine(_tempSettingsDirectory, "libraries.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempSettingsDirectory))
        {
            Directory.Delete(_tempSettingsDirectory, recursive: true);
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

    [TestMethod]
    public async Task ReadExternalEntriesAsyncReturnsEmptyListWhenFileIsCorruptJson()
    {
        var store = new LibraryRegistryStore();
        await File.WriteAllTextAsync(_tempSettingsPath, "{ not valid json ]]]");

        var entries = await store.ReadExternalEntriesAsync(_tempSettingsPath);

        Assert.AreEqual(0, entries.Count);
    }

    [TestMethod]
    public async Task ReadDefaultNameAsyncReturnsNullWhenFileMissing()
    {
        var store = new LibraryRegistryStore();

        var name = await store.ReadDefaultNameAsync(_tempSettingsPath);

        Assert.IsNull(name);
    }

    [TestMethod]
    public async Task WriteThenReadRoundTripsDefaultName()
    {
        var store = new LibraryRegistryStore();

        await store.WriteDefaultNameAsync("Ma librairie par defaut", _tempSettingsPath);
        var name = await store.ReadDefaultNameAsync(_tempSettingsPath);

        Assert.AreEqual("Ma librairie par defaut", name);
    }

    [TestMethod]
    public async Task ReadDefaultNameAsyncReturnsNullWhenFileIsCorruptJson()
    {
        var store = new LibraryRegistryStore();
        var defaultNamePath = Path.Combine(_tempSettingsDirectory, "default-library-name.json");
        await File.WriteAllTextAsync(defaultNamePath, "{ not valid json ]]]");

        var name = await store.ReadDefaultNameAsync(_tempSettingsPath);

        Assert.IsNull(name);
    }
}
