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
