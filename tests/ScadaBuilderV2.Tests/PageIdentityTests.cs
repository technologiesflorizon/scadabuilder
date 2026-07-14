using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class PageIdentityTests
{
    [TestMethod]
    public void DeterministicPageKeyIsStableAndNonEmpty()
    {
        var first = PageKeyFactory.CreateDeterministic("AMR_REF_SCADA_V2", "win00012");
        var second = PageKeyFactory.CreateDeterministic("amr_ref_scada_v2", "WIN00012");

        Assert.AreNotEqual(Guid.Empty, first);
        Assert.AreEqual(first, second);
        Assert.AreNotEqual(first, PageKeyFactory.CreateDeterministic("AMR_REF_SCADA_V2", "win00013"));
    }

    [TestMethod]
    public void PageCodePolicyValidatesPortabilityAndCaseInsensitiveUniqueness()
    {
        Assert.IsTrue(PageCodePolicy.Validate("win00012_1").IsValid);
        Assert.IsFalse(PageCodePolicy.Validate("Win00012").IsValid);
        Assert.IsFalse(PageCodePolicy.Validate("con").IsValid);
        Assert.IsFalse(PageCodePolicy.Validate("win00012", ["WIN00012"]).IsValid);
        Assert.AreEqual("win00012_copy3", PageCodePolicy.SuggestDuplicateCode(
            "win00012",
            ["win00012", "win00012_copy", "win00012_copy2"]));

        var maximumLengthCode = $"p{new string('a', 63)}";
        var duplicate = PageCodePolicy.SuggestDuplicateCode(maximumLengthCode);
        Assert.AreEqual(64, duplicate.Length);
        Assert.IsTrue(PageCodePolicy.Validate(duplicate).IsValid);
    }

    [TestMethod]
    public void ProjectMigrationIsIdempotentAndPreservesWonderwareProvenance()
    {
        var imported = new ScadaSceneReference("win00012", "Source", "scenes/win00012.scene.json");
        var project = ScadaProject.CreateDefault("AMR_REF_SCADA_V2") with
        {
            HomePageId = "win00012",
            Scenes = [imported]
        };

        var migrated = ModernProjectMigration.MigrateProject(project, [imported]);
        var secondPass = ModernProjectMigration.MigrateProject(migrated, [imported]);
        var page = migrated.Scenes.Single();

        Assert.AreNotEqual(Guid.Empty, page.PageKey);
        Assert.AreEqual("win00012", page.PageCode);
        Assert.AreEqual(PageOrigin.Imported, page.Origin);
        Assert.AreEqual("Wonderware", page.ImportProvenance?.SourceSystem);
        Assert.AreEqual(page.PageKey, migrated.HomePageKey);
        Assert.AreEqual(migrated.HomePageKey, secondPass.HomePageKey);
        CollectionAssert.AreEqual(migrated.Scenes.ToArray(), secondPass.Scenes.ToArray());
    }

    [TestMethod]
    public void SceneMigrationResolvesActionCommandAndCompositionTargetsToKeys()
    {
        var header = new ScadaSceneReference("header", "Header", "scenes/header.scene.json", ScadaPageType.Header);
        var home = new ScadaSceneReference("home", "Home", "scenes/home.scene.json", HeaderPageId: "header");
        var target = new ScadaSceneReference("target", "Target", "scenes/target.scene.json");
        var project = ModernProjectMigration.MigrateProject(ScadaProject.CreateDefault("test") with
        {
            Scenes = [header, home, target]
        });
        var element = ScadaElement.CreateText("button", "Button", 10, 10) with
        {
            CommandConfig = new ScadaElementCommandConfig([
                new ScadaCommandBinding(
                    "navigate",
                    "Navigate",
                    true,
                    ScadaCommandTrigger.OnClick,
                    ScadaCommandKind.Navigate,
                    TargetPageId: "target")
            ])
        };
        var scene = ScadaScene.CreateEmpty("home", "Home", CanvasSize.DefaultDesktop)
            .WithPageComposition("header", null)
            .WithAction(new ScadaActionDefinition("action", ScadaActionKind.Navigate, TargetPageId: "target"))
            .WithElement(element);

        var migrated = ModernProjectMigration.MigrateScene(scene, project);
        var headerKey = project.Scenes.Single(page => page.PageCode == "header").PageKey;
        var targetKey = project.Scenes.Single(page => page.PageCode == "target").PageKey;

        Assert.AreEqual(project.Scenes.Single(page => page.PageCode == "home").PageKey, migrated.PageKey);
        Assert.AreEqual(headerKey, migrated.HeaderPageKey);
        Assert.AreEqual(targetKey, migrated.ActionDefinitions.Single().TargetPageKey);
        Assert.AreEqual(targetKey, migrated.Elements.Single().EffectiveCommandConfig.Commands.Single().TargetPageKey);
    }

    [TestMethod]
    public void ProjectMigrationEnrichesExistingWonderwareProvenanceWithProjectionPath()
    {
        var existing = new ScadaSceneReference(
            "win00009",
            "Imported",
            "scenes/win00009.scene.json",
            PageKey: Guid.NewGuid(),
            PageCode: "win00009",
            Origin: PageOrigin.Imported,
            ImportProvenance: new ImportProvenance("Wonderware", "Legacy", "win00009"));
        var inventory = existing with
        {
            ImportProvenance = existing.ImportProvenance! with
            {
                SourcePath = "SCADA_BUILDER/AMR_SCADA/html/win00009.html"
            }
        };
        var project = ScadaProject.CreateDefault("AMR_REF_SCADA_V2") with { Scenes = [existing] };

        var migrated = ModernProjectMigration.MigrateProject(project, [inventory]);

        Assert.AreEqual(
            "SCADA_BUILDER/AMR_SCADA/html/win00009.html",
            migrated.Scenes.Single().ImportProvenance?.SourcePath);
    }
}
