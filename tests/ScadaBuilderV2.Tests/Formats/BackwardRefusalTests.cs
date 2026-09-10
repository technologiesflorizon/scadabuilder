using System.IO;
using System.Text.Json;
using ScadaBuilderV2.Application.Formats;
using ScadaBuilderV2.Application.Projects;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Infrastructure.ModernProjects;
using ScadaBuilderV2.Infrastructure.ModernProjects.Converters;
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
            new ReferenceProjectCompatibilityLocator(),
            CreateRegistry(),
            new ConversionCoordinator(CreateRegistry(), new AcceptingConsent()));

        var result = await repository.OpenAsync(projectPath);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Candidate, "no candidate may be prepared from a file we cannot read");
        var issue = result.Diagnostics.Single(entry => entry.Code == "project.format-too-new");
        StringAssert.Contains(issue.Message, (ScadaFormatGeneration.Project + 1).ToString());
        StringAssert.Contains(issue.Message, ScadaFormatGeneration.Project.ToString());

        // Ruling 40: a binary refusing to open a project must not touch its directory at all, not merely
        // leave `project.json` byte-identical. `AcquireWorkspaceLockAsync` creates `.studio/` and
        // `workspace-save.lock` *before* it even checks whether a pending transaction exists, so calling
        // recovery unconditionally on this path (as an earlier fix-round regression did) wrote both into a
        // project this gate had just refused -- invisible to an assertion that only ever compared
        // `project.json`. The positive anchor is the exact single-entry listing, not merely an absence of
        // `.studio`: a weaker "no .studio" assertion would still pass if refusal wrote some other file.
        CollectionAssert.AreEquivalent(
            new[] { projectPath },
            Directory.GetFileSystemEntries(root),
            "a refused project's directory must contain exactly what it did before the refusal -- no lock file, no .studio/, nothing.");
    }

    [TestMethod]
    public async Task AProjectAtTheCurrentGenerationIsNotRefusedByThisGate()
    {
        var projectPath = WriteProject(ScadaFormatGeneration.Project);
        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator(),
            CreateRegistry(),
            new ConversionCoordinator(CreateRegistry(), new AcceptingConsent()));

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
            new ReferenceProjectCompatibilityLocator(),
            CreateRegistry(),
            new ConversionCoordinator(CreateRegistry(), new AcceptingConsent()));

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
            new ReferenceProjectCompatibilityLocator(),
            CreateRegistry(),
            new ConversionCoordinator(CreateRegistry(), new AcceptingConsent()));

        using (new FileStream(projectPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = await repository.OpenAsync(projectPath);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Candidate);
            Assert.IsTrue(result.Diagnostics.Any(entry => entry.Code == "project.open-failed"));
        }
    }

    /// <summary>Contract 6.5: refusing the conversion leaves the project closed and untouched.</summary>
    [TestMethod]
    public async Task ARefusedConversionNeitherOpensTheProjectNorTouchesIt()
    {
        var projectPath = WriteProject(formatVersion: 0);
        var before = await File.ReadAllTextAsync(projectPath);
        var registry = new ArtifactConverterRegistry();
        registry.Register(new ProjectGeneration1Converter());
        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator(),
            registry,
            new ConversionCoordinator(registry, new DecliningConsent()));

        var result = await repository.OpenAsync(projectPath);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Candidate, "no candidate may be prepared from an unconverted artifact.");
        Assert.AreEqual("project.conversion-declined", result.Diagnostics.Single().Code);
        Assert.AreEqual(before, await File.ReadAllTextAsync(projectPath));
        Assert.IsFalse(File.Exists(projectPath + ".bak"), "a refused conversion writes no backup either.");

        // Ruling 46 (fix round 3): the byte-identical check on project.json above, and the .bak-absence check,
        // both pass even if AcquireWorkspaceLockAsync had already created `.studio/` and `workspace-save.lock`
        // before the operator answered -- exactly the regression this test's own name declares closed
        // ("NorTouchesIt") but did not actually guard. The positive anchor is the exact single-entry listing,
        // the same one BackwardRefusalTests already uses for the too-new refusal.
        CollectionAssert.AreEquivalent(
            new[] { projectPath },
            Directory.GetFileSystemEntries(root),
            "a declined conversion's directory must contain exactly what it did before the decline -- no lock file, no .studio/, nothing.");
    }

    private sealed class DecliningConsent : IConversionConsent
    {
        public Task<ConversionDecision> RequestAsync(ConversionPlan plan, CancellationToken cancellationToken)
            => Task.FromResult(ConversionDecision.Cancel);
    }

    /// <summary>Always convert. The tests above exercise the version gate itself, not conversion refusal.</summary>
    private sealed class AcceptingConsent : IConversionConsent
    {
        public Task<ConversionDecision> RequestAsync(ConversionPlan plan, CancellationToken cancellationToken)
            => Task.FromResult(ConversionDecision.Convert);
    }

    private static ArtifactConverterRegistry CreateRegistry()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new ProjectGeneration1Converter());
        return registry;
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
