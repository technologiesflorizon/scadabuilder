using ScadaBuilderV2.Application.RuntimeContracts;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.RuntimeContracts;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// Locks the export of a Toggle interlock (DEC-0054): `interlockTagId` only when set, and capability
/// `command.write.toggle-interlock` required only when a command uses it.
/// </summary>
[TestClass]
public sealed class ToggleInterlockExportTests
{
    private static ScadaScene SceneWith(string? interlock)
    {
        var command = new ScadaCommandBinding("start", "Depart", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
            WriteTagId: "tf100.mapping.912", WriteMode: ScadaWriteMode.Toggle, InterlockTagId: interlock);
        return ScadaScene.CreateEmpty("win00008", "Test", new(400, 400))
            .WithElement(ScadaElement.CreateButton("btn_start", "Depart", 10, 10) with
            {
                CommandConfig = new ScadaElementCommandConfig(new[] { command })
            });
    }

    private static async Task<string> ExportHtmlAsync(ScadaScene scene)
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2InterlockExport", Guid.NewGuid().ToString("N"));
        var sourceHtmlPath = Path.Combine(root, "source", "page.html");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceHtmlPath)!);
        await File.WriteAllTextAsync(sourceHtmlPath, "<!doctype html><html><body><div class=\"page\"></div></body></html>");
        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, Path.Combine(root, "export"));
            return (await File.ReadAllTextAsync(result.HtmlPath)).Replace("&quot;", "\"");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task AnInterlockIsExportedRightAfterTheReadTag()
    {
        var html = await ExportHtmlAsync(SceneWith("tf100.mapping.913"));

        StringAssert.Contains(html, "\"readTagId\":null,\"interlockTagId\":\"tf100.mapping.913\",\"writeMode\":\"toggle\"");
    }

    [TestMethod]
    public async Task NoInterlockMeansNoInterlockProperty()
    {
        var html = await ExportHtmlAsync(SceneWith(null));

        Assert.IsFalse(html.Contains("interlockTagId"), "a command without an interlock must keep its exact former bytes");
        StringAssert.Contains(html, "\"readTagId\":null,\"writeMode\":\"toggle\"");
    }

    [TestMethod]
    public void TheCapabilityIsRequiredOnlyWhenACommandUsesIt()
    {
        var project = ScadaProject.CreateDefault("P") with
        {
            Scenes = new[] { new ScadaSceneReference("win00008", "Test", "scenes/win00008.scene.json") }
        };

        string[] Required(string? interlock) => ScadaRuntimeCapabilityAnalyzer
            .Analyze(project, new[] { SceneWith(interlock) })
            .RequiredCapabilities.Select(capability => capability.Id).ToArray();

        CollectionAssert.Contains(Required("tf100.mapping.913"), "command.write.toggle-interlock");
        CollectionAssert.DoesNotContain(Required(null), "command.write.toggle-interlock");
        CollectionAssert.Contains(Required(null), "command.write.toggle");
    }

    [TestMethod]
    public void TheCapabilityIsASupportedSharedRuntimeCapabilityWithThreeLayerEvidence()
    {
        var capability = ScadaRuntimeCapabilityCatalog.All.Single(c => c.Id == "command.write.toggle-interlock");

        Assert.AreSame(ScadaRuntimeCapabilityCatalog.CommandToggleInterlock, capability);
        Assert.AreEqual(ScadaRuntimeCapabilityStatus.Supported, capability.Status);
        Assert.AreEqual(ScadaRuntimeCapabilityOwner.SharedRuntime, capability.Owner);
        Assert.IsTrue(capability.Evidence.BuilderTests.Count > 0);
        Assert.IsTrue(capability.Evidence.SharedRuntimeTests.Count > 0);
        Assert.IsTrue(capability.Evidence.Tf100WebTests.Count > 0);
    }
}
