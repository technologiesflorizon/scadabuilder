using ScadaBuilderV2.Infrastructure.ReferenceProjects;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ReferenceScadaProjectReaderTests
{
    [TestMethod]
    public async Task LoadAsyncReadsReferenceProjectManifestAndPreservesPageOrder()
    {
        var projectJsonPath = CreateProjectJson("""
            {
              "name": "AMR_REF_SCADA",
              "version": "0.1.0",
              "pages": [
                "pages/win00002.json",
                "pages/win00003.json"
              ]
            }
            """);
        var reader = new ReferenceScadaProjectReader();

        var project = await reader.LoadAsync(projectJsonPath);

        Assert.AreEqual("AMR_REF_SCADA", project.Name);
        Assert.AreEqual("0.1.0", project.Version);
        Assert.AreEqual(2, project.Pages.Count);
        Assert.AreEqual("win00002", project.Pages[0].Id);
        Assert.AreEqual("win00002", project.Pages[0].Title);
        Assert.AreEqual("pages/win00002.json", project.Pages[0].RelativePath);
        Assert.IsTrue(project.Pages[0].AbsolutePath.EndsWith(
            Path.Combine("pages", "win00002.json"),
            StringComparison.Ordinal));
        Assert.AreEqual("win00003", project.Pages[1].Id);
    }

    [TestMethod]
    public async Task LoadAmrReferenceAsyncResolvesExpectedReferenceProjectPath()
    {
        var repositoryRoot = CreateRepositoryRootWithReferenceProject("""
            {
              "name": "AMR_REF_SCADA",
              "version": "0.1.0",
              "pages": [
                "pages/win00096.json"
              ]
            }
            """);
        var reader = new ReferenceScadaProjectReader();

        var project = await reader.LoadAmrReferenceAsync(repositoryRoot);

        Assert.AreEqual("AMR_REF_SCADA", project.Name);
        Assert.AreEqual("win00096", project.Pages.Single().Id);
        Assert.IsTrue(project.ProjectJsonPath.EndsWith(
            Path.Combine("SCADA_BUILDER", "AMR_SCADA", "AMR_REF_SCADA", "project.json"),
            StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ListPagesAsyncReturnsPagesOnly()
    {
        var projectJsonPath = CreateProjectJson("""
            {
              "name": "AMR_REF_SCADA",
              "pages": [
                "pages/win00008.json"
              ]
            }
            """);
        var reader = new ReferenceScadaProjectReader();

        var pages = await reader.ListPagesAsync(projectJsonPath);

        Assert.AreEqual(1, pages.Count);
        Assert.AreEqual("win00008", pages[0].Id);
    }

    [TestMethod]
    public async Task LoadAsyncRejectsMissingPagesArray()
    {
        var projectJsonPath = CreateProjectJson("""
            {
              "name": "AMR_REF_SCADA"
            }
            """);
        var reader = new ReferenceScadaProjectReader();

        await Assert.ThrowsExceptionAsync<InvalidDataException>(
            async () => await reader.LoadAsync(projectJsonPath));
    }

    private static string CreateProjectJson(string content)
    {
        var projectDirectory = Path.Combine(
            Path.GetTempPath(),
            "ScadaBuilderV2.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(projectDirectory);
        var projectJsonPath = Path.Combine(projectDirectory, "project.json");
        File.WriteAllText(projectJsonPath, content);
        return projectJsonPath;
    }

    private static string CreateRepositoryRootWithReferenceProject(string content)
    {
        var repositoryRoot = Path.Combine(
            Path.GetTempPath(),
            "ScadaBuilderV2.Tests",
            Guid.NewGuid().ToString("N"));
        var projectDirectory = Path.Combine(
            repositoryRoot,
            "SCADA_BUILDER",
            "AMR_SCADA",
            "AMR_REF_SCADA");

        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(Path.Combine(projectDirectory, "project.json"), content);
        return repositoryRoot;
    }
}
