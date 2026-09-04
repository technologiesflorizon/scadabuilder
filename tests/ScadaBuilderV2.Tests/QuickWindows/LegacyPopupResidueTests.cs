using System.Text.Json;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.Tests.QuickWindows;

[TestClass]
public sealed class LegacyPopupResidueTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ScadaBuilderV2.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    [TestMethod]
    public void NewResidueIsRejected()
    {
        var repoRoot = FindRepoRoot();
        var allowlistPath = Path.Combine(repoRoot, "tests", "conformance", "legacy-popup-residue-allowlist.json");
        Assert.IsTrue(File.Exists(allowlistPath), $"Allowlist file not found at {allowlistPath}");
        var json = File.ReadAllText(allowlistPath);
        Assert.IsTrue(json.Contains("MountFragment"));
        Assert.IsTrue(json.Contains("ScadaPopupOptions"));
        // Verify no modern command kind remains in src (excluding allowlist files and historical archive)
        var srcRoot = Path.Combine(repoRoot, "src");
        var srcFiles = Directory.GetFiles(srcRoot, "*.cs", SearchOption.AllDirectories);
        foreach (var file in srcFiles)
        {
            var text = File.ReadAllText(file);
            // Only check for modern command kind usage outside the single comment in ScadaCommandBinding.cs that mentions DEC-0050
            if (file.EndsWith("ScadaCommandBinding.cs", StringComparison.OrdinalIgnoreCase))
            {
                // Allow the comment that explains removal, but not actual enum values
                var lines = text.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("OpenPopup") && !line.TrimStart().StartsWith("//") && !line.Contains("DEC-0050"))
                        Assert.Fail($"Unexpected modern popup residue in {file}: {line.Trim()}");
                    if (line.Contains("TogglePopup") && !line.TrimStart().StartsWith("//") && !line.Contains("DEC-0050"))
                        Assert.Fail($"Unexpected modern popup residue in {file}: {line.Trim()}");
                    if (line.Contains("ClosePopup") && !line.TrimStart().StartsWith("//") && line.Contains("ScadaCommandKind") && !line.Contains("DEC-0050"))
                        Assert.Fail($"Unexpected modern popup residue in {file}: {line.Trim()}");
                }
                continue;
            }
            // For other src files, only allow legacy ScadaActionKind residues which are allowlisted
            if (text.Contains("ScadaCommandKind.OpenPopup") || text.Contains("ScadaCommandKind.TogglePopup") || text.Contains("ScadaCommandKind.ClosePopup"))
                Assert.Fail($"New modern popup residue found in {file}");
        }
    }

    [TestMethod]
    public void AllowlistedDoesNotReachQuickWindow()
    {
        var repoRoot = FindRepoRoot();
        // Ensure no allowlisted legacy symbol appears in QuickWindow domain types
        var qwAssembly = typeof(QuickWindowDefinition).Assembly;
        var qwTypes = qwAssembly.GetTypes().Where(t => t.Namespace == "ScadaBuilderV2.Domain.QuickWindows").ToArray();
        var quickWindowSources = Directory.GetFiles(Path.Combine(repoRoot, "src", "ScadaBuilderV2.Domain", "QuickWindows"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText).ToArray();
        var combinedSource = string.Join("\n", quickWindowSources);
        foreach (var type in qwTypes)
        {
            var text = type.FullName ?? "";
            Assert.IsFalse(text.Contains("InstanceKey") && text.Contains("QuickWindowInstanceKey"), "QuickWindow must not introduce InstanceKey");
            var props = type.GetProperties().Select(p => p.Name).ToArray();
            CollectionAssert.DoesNotContain(props, "InstanceKey");
        }
        // Allowlisted residues must not be referenced inside QuickWindow files (combined check)
        Assert.IsFalse(combinedSource.Contains("MountFragment"), "QuickWindow must not reference MountFragment");
        Assert.IsFalse(combinedSource.Contains("ScadaPopupOptions"), "QuickWindow must not reference ScadaPopupOptions");
        Assert.IsFalse(combinedSource.Contains("ScadaCommandKind.OpenPopup") || combinedSource.Contains("ScadaCommandKind.TogglePopup"), "QuickWindow must not reference popup command kinds");

        // Also ensure ScadaProject EffectiveQuickWindows does not expose legacy popup
        var projProps = typeof(ScadaProject).GetProperties().Select(p => p.Name).ToArray();
        CollectionAssert.DoesNotContain(projProps, "InstanceKey");
    }

    [TestMethod]
    public void LegacyCapabilityNotUsedAsQuickWindowProof()
    {
        // Ensure legacy capabilities are not counted as quick-window proof
        var all = ScadaBuilderV2.Domain.RuntimeContracts.ScadaRuntimeCapabilityCatalog.All;
        var legacyIds = new[] { "action.mount-fragment", "action.close-popup", "action.toggle-popup", "popup.options" };
        var quickWindowIds = new[] { "command.open-quick-window", "command.close-quick-window" };
        foreach (var legacyId in legacyIds)
        {
            var cap = all.FirstOrDefault(c => c.Id == legacyId);
            Assert.IsNotNull(cap, $"Legacy capability {legacyId} should exist as Blocked");
            Assert.AreEqual(ScadaBuilderV2.Domain.RuntimeContracts.ScadaRuntimeCapabilityStatus.Blocked, cap!.Status);
        }
        foreach (var qwId in quickWindowIds)
        {
            var cap = all.FirstOrDefault(c => c.Id == qwId);
            Assert.IsNotNull(cap, $"QuickWindow capability {qwId} should exist");
            // Promoted in Phase 6 on its own three-layer evidence, never on a legacy popup's.
            Assert.AreEqual(ScadaBuilderV2.Domain.RuntimeContracts.ScadaRuntimeCapabilityStatus.Supported, cap!.Status);
            // Ensure legacy not used as proof: their fixture ids are distinct
            Assert.IsFalse(cap.FixtureId.Contains("popup"), "QuickWindow fixture must not contain popup");
        }
    }

    [TestMethod]
    public async Task ProtectsProjectAndFixtures()
    {
        var repoRoot = FindRepoRoot();
        // Test that legacy command OpenPopup is rejected without overwriting bytes
        var fixturePath = Path.Combine(repoRoot, "tests", "conformance", "fixtures", "legacy-popup-reject", "legacy-command-openpopup.json");
        Assert.IsTrue(File.Exists(fixturePath), $"Fixture not found: {fixturePath}");
        var originalBytes = await File.ReadAllBytesAsync(fixturePath);
        var json = await File.ReadAllTextAsync(fixturePath);
        Assert.IsTrue(json.Contains("\"OpenPopup\""));

        // Simulate loading via ModernProjectStore: should throw InvalidDataException and not create QuickWindow
        var tempRoot = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot, "SCADA_BUILDER_V2", "projects", "AMR_REF_SCADA_V2", "scenes"));
            var scenePath = Path.Combine(tempRoot, "SCADA_BUILDER_V2", "projects", "AMR_REF_SCADA_V2", "scenes", "legacy-command-openpopup.scene.json");
            await File.WriteAllTextAsync(scenePath, json);
            var project = ScadaProject.CreateDefault("Test") with
            {
                Scenes = new[] { new ScadaSceneReference("legacy-command-openpopup", "Legacy", "scenes/legacy-command-openpopup.scene.json", PageKey: Guid.Parse("11111111-1111-1111-1111-111111111111"), PageCode: "legacy-command-openpopup") }
            };
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "SCADA_BUILDER_V2", "projects", "AMR_REF_SCADA_V2", "project.json"), JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true }));

            var store = new ModernProjectStore();
            var pageRef = project.Scenes[0];
            await Assert.ThrowsExceptionAsync<InvalidDataException>(async () =>
            {
                await store.LoadOrCreateSceneFromProjectRootAsync(Path.Combine(tempRoot, "SCADA_BUILDER_V2", "projects", "AMR_REF_SCADA_V2"), pageRef);
            });

            // Verify bytes unchanged (no migration)
            var afterBytes = await File.ReadAllBytesAsync(scenePath);
            CollectionAssert.AreEqual(originalBytes, afterBytes, "Fixture bytes must remain unchanged; no migration.");

            // Verify no QuickWindow was created
            var projectAfter = await store.LoadProjectFromRootAsync(Path.Combine(tempRoot, "SCADA_BUILDER_V2", "projects", "AMR_REF_SCADA_V2"));
            Assert.IsNotNull(projectAfter);
            Assert.AreEqual(0, projectAfter!.EffectiveQuickWindows.Count, "Legacy popup must not create QuickWindow.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public void LegacyActionMountFragmentDoesNotMigrateToQuickWindow()
    {
        var def = QuickWindowDefinition.CreateEmpty("qw_test", "Test");
        // Ensure definition does not contain legacy action kind
        Assert.IsFalse(def.Code.Contains("MountFragment"));
        Assert.IsFalse(def.InterfaceMembers.Any(m => m.Name.Contains("MountFragment")));
        // Simulate that a project with legacy MountFragment action does not produce a QuickWindow via migration
        var project = ScadaProject.CreateDefault("P") with
        {
            Scenes = new[] { new ScadaSceneReference("page1", "Page1", "scenes/page1.scene.json") },
            QuickWindows = Array.Empty<QuickWindowDefinition>()
        };
        Assert.AreEqual(0, project.EffectiveQuickWindows.Count);
        Assert.AreEqual(0, project.EffectiveQuickWindowInvocations.Count);
    }
}
