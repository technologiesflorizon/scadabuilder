using System.IO;
using System.Text.Json;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Infrastructure.ModernProjects;
using ScadaBuilderV2.Infrastructure.ReferenceProjects;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Locks the refusal a newer artifact receives from an older binary. DEC-0049 D5 already required it; until
/// this gate existed the pipeline refused only an invalid path and a failed open.
/// </summary>
[TestClass]
public sealed class BackwardRefusalTests
{
    private string root = "";

    [TestInitialize]
    public void CreateWorkspace()
    {
        root = Path.Combine(Path.GetTempPath(), "scada-format-refusal", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void RemoveWorkspace()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public async Task AProjectNewerThanTheBinaryIsRefusedBeforeActivation()
    {
        var projectPath = WriteProject(ScadaFormatGeneration.Project + 1);
        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator());

        var result = await repository.OpenAsync(projectPath);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Candidate, "no candidate may be prepared from a file we cannot read");
        var issue = result.Diagnostics.Single(entry => entry.Code == "project.format-too-new");
        StringAssert.Contains(issue.Message, (ScadaFormatGeneration.Project + 1).ToString());
        StringAssert.Contains(issue.Message, ScadaFormatGeneration.Project.ToString());
    }

    [TestMethod]
    public async Task AProjectAtTheCurrentGenerationIsNotRefusedByThisGate()
    {
        var projectPath = WriteProject(ScadaFormatGeneration.Project);
        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator());

        var result = await repository.OpenAsync(projectPath);

        Assert.IsFalse(
            result.Diagnostics.Any(entry => entry.Code == "project.format-too-new"),
            "the version gate must not fire on a file this binary understands.");
    }

    /// <summary>
    /// The data loss this gate exists to close: an older binary silently dropped quick windows.
    /// </summary>
    /// <remarks>
    /// `ScadaProject.QuickWindows` and `QuickWindowInvocations` are nullable properties added in Phase 1 of
    /// DEC-0050. A binary that predates them ignores the unknown properties on deserialisation and writes
    /// them away on the first save - definitions, local interfaces and every invocation, with no trace. The
    /// refusal is what makes that impossible: a binary that does not understand a file cannot rewrite it.
    /// </remarks>
    [TestMethod]
    public async Task AProjectCarryingUnknownContentIsRefusedRatherThanSilentlyRewritten()
    {
        var projectPath = WriteProject(
            ScadaFormatGeneration.Project + 1,
            extraJson: "\"QuickWindowInvocations\":[{\"InvocationKey\":\"11111111-1111-1111-1111-111111111111\"}],");
        var before = await File.ReadAllTextAsync(projectPath);

        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator());
        var result = await repository.OpenAsync(projectPath);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(
            before,
            await File.ReadAllTextAsync(projectPath),
            "a refused project must not be touched, let alone rewritten without what it carried.");
    }

    private string WriteProject(int formatVersion, string extraJson = "")
    {
        var projectPath = Path.Combine(root, "project.json");
        var json = $$"""
        {
          "FormatVersion": {{formatVersion}},
          {{extraJson}}
          "Name": "Projet test",
          "Version": { "Production": 2, "Feature": 1, "Iteration": 6 },
          "CanvasSize": { "Width": 1920, "Height": 1080 },
          "ResponsiveMode": "Fixed",
          "AuthoringMode": "DesktopFirst",
          "DevicePresets": [],
          "Scenes": [],
          "ManifestVersion": "2.3"
        }
        """;
        File.WriteAllText(projectPath, json);
        return projectPath;
    }
}
