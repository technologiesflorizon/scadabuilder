using System.IO;
using System.Text.Json;
using ScadaBuilderV2.Application.Projects;
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
    /// A genuinely valid project is created first so the gate is exercised on the one field that changes,
    /// not on a synthetic fixture that would already fail closed for an unrelated reason (an earlier version
    /// of this test used <c>"Scenes": []</c>, which trips <c>build.no-default-page</c> regardless of whether
    /// the version gate exists at all - it passed with the gate deleted). Opening the same project once
    /// before and once after patching <c>FormatVersion</c> in place is what makes the outcome attributable
    /// to the gate: same file, one field different, opposite result.
    ///
    /// `ScadaProject.QuickWindows` and `QuickWindowInvocations` are nullable properties added in Phase 1 of
    /// DEC-0050. A binary that predates them ignores the unknown properties on deserialisation and writes
    /// them away on the first save - definitions, local interfaces and every invocation, with no trace. The
    /// refusal is what makes that impossible: a binary that does not understand a file cannot rewrite it.
    /// </remarks>
    [TestMethod]
    public async Task AProjectCarryingUnknownContentIsRefusedRatherThanSilentlyRewritten()
    {
        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator());

        var created = await repository.CreateAsync(new CreateProjectRequest(
            "Projet test",
            root,
            "ProjetTest",
            "win00001",
            "Page principale",
            CanvasSize.DefaultDesktop,
            ResponsiveMode.Fixed,
            AuthoringMode.DesktopFirst));
        Assert.IsTrue(created.IsSuccess, string.Join(Environment.NewLine, created.Diagnostics.Select(issue => issue.Message)));
        var projectPath = created.Candidate!.Location.ProjectFilePath;

        var openedAtCurrentGeneration = await repository.OpenAsync(projectPath);
        Assert.IsTrue(
            openedAtCurrentGeneration.IsSuccess,
            string.Join(Environment.NewLine, openedAtCurrentGeneration.Diagnostics.Select(issue => issue.Message)));
        Assert.IsFalse(openedAtCurrentGeneration.Diagnostics.Any(entry => entry.Code == "project.format-too-new"));
        Assert.IsNotNull(openedAtCurrentGeneration.Candidate);

        var original = await File.ReadAllTextAsync(projectPath);
        var insertAt = original.IndexOf('{') + 1;
        var patched = original.Insert(insertAt, $"\"FormatVersion\":{ScadaFormatGeneration.Project + 1},");
        await File.WriteAllTextAsync(projectPath, patched);

        var result = await repository.OpenAsync(projectPath);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Candidate);
        var issue = result.Diagnostics.Single();
        Assert.AreEqual("project.format-too-new", issue.Code);
        Assert.AreEqual(
            patched,
            await File.ReadAllTextAsync(projectPath),
            "a refused project must not be touched, let alone rewritten without what it carried.");
    }

    /// <summary>
    /// The pre-read must stay behind the same error boundary as the rest of the open pipeline: a locked or
    /// permission-denied `project.json` is a reported diagnostic, not an unhandled exception.
    /// </summary>
    [TestMethod]
    public async Task AProjectFileThatCannotBeReadProducesADiagnosticRatherThanAnException()
    {
        var projectPath = WriteProject(ScadaFormatGeneration.Project);
        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator());

        using (new FileStream(projectPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = await repository.OpenAsync(projectPath);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Candidate);
            Assert.IsTrue(result.Diagnostics.Any(entry => entry.Code == "project.open-failed"));
        }
    }

    private string WriteProject(int formatVersion)
    {
        var projectPath = Path.Combine(root, "project.json");
        var json = $$"""
        {
          "FormatVersion": {{formatVersion}},
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
