using System.IO;
using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// The backup is the only way back from a conversion, so it is written before the conversion and never
/// silently replaces an earlier one.
/// </summary>
[TestClass]
public sealed class ArtifactBackupWriterTests
{
    private string root = "";

    [TestInitialize]
    public void CreateWorkspace()
    {
        root = Path.Combine(Path.GetTempPath(), "scada-backup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void RemoveWorkspace()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public void TheBackupKeepsTheOriginalBytes()
    {
        var path = Path.Combine(root, "project.json");
        File.WriteAllText(path, """{"Name":"avant"}""");

        var backup = ArtifactBackupWriter.CreateBackup(path);

        Assert.AreEqual(Path.Combine(root, "project.json.bak"), backup);
        Assert.AreEqual("""{"Name":"avant"}""", File.ReadAllText(backup));
    }

    [TestMethod]
    public void AnExistingBackupIsNeverOverwritten()
    {
        var path = Path.Combine(root, "project.json");
        File.WriteAllText(path, "second");
        File.WriteAllText(Path.Combine(root, "project.json.bak"), "premier");

        var backup = ArtifactBackupWriter.CreateBackup(path);

        Assert.AreEqual(Path.Combine(root, "project.json.bak.1"), backup);
        Assert.AreEqual("premier", File.ReadAllText(Path.Combine(root, "project.json.bak")));
        Assert.AreEqual("second", File.ReadAllText(backup));
    }

    [TestMethod]
    public void NumberingContinuesPastTheFirstCollision()
    {
        var path = Path.Combine(root, "a.sep");
        File.WriteAllText(path, "courant");
        File.WriteAllText(Path.Combine(root, "a.sep.bak"), "un");
        File.WriteAllText(Path.Combine(root, "a.sep.bak.1"), "deux");

        Assert.AreEqual(Path.Combine(root, "a.sep.bak.2"), ArtifactBackupWriter.CreateBackup(path));
    }
}
