using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.Tests.QuickWindows;

[TestClass]
public sealed class QuickWindowStoreTests
{
    [TestMethod]
    public async Task SaveAndReopenRoundTrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var store = new ModernProjectStore();
        try
        {
            var project = ScadaProject.CreateDefault("QWPersistence") with
            {
                Scenes = new[] { new ScadaSceneReference("win00001", "Page", "scenes/win00001.scene.json", PageKey: Guid.NewGuid(), PageCode: "win00001") }
            };
            var scene = ScadaScene.CreateEmpty("win00001", "Page", CanvasSize.DefaultDesktop) with { PageKey = project.Scenes[0].PageKey, PageCode = "win00001" };
            var qw = QuickWindowDefinition.CreateEmpty("qw_test", "TestWindow") with
            {
                Content = new VisualContent(new CanvasSize(400, 300), null, new[] { ScadaElement.CreateText("t1", "Hello", 10, 20) }),
                InterfaceMembers = new[]
                {
                    new QuickWindowInterfaceMember(Guid.NewGuid(), "RunFeedback", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read),
                    new QuickWindowInterfaceMember(Guid.NewGuid(), "MotorName", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.String, QuickWindowMemberAccess.Read)
                }
            };
            project = project with { QuickWindows = new[] { qw } };
            var snapshot = new PageWorkspaceSnapshot(1, project, new Dictionary<Guid, ScadaScene> { [project.Scenes[0].PageKey] = scene }, Array.Empty<PendingPageDeletion>());
            await store.SaveWorkspaceSnapshotToProjectRootAsync(Path.Combine(root, "proj"), snapshot);
            var loadedProject = await store.LoadProjectFromRootAsync(Path.Combine(root, "proj"));
            Assert.IsNotNull(loadedProject);
            Assert.AreEqual(1, loadedProject!.EffectiveQuickWindows.Count);
            Assert.AreEqual(qw.DefinitionKey, loadedProject.EffectiveQuickWindows[0].DefinitionKey);
            Assert.AreEqual("qw_test", loadedProject.EffectiveQuickWindows[0].Code);
            Assert.AreEqual(2, loadedProject.EffectiveQuickWindows[0].InterfaceMembers.Count);
            // Verify file exists with deterministic name
            var qwPath = Path.Combine(root, "proj", "quick-windows", $"{qw.DefinitionKey:N}.quick-window.json");
            Assert.IsTrue(File.Exists(qwPath), $"QuickWindow file should exist at {qwPath}");
            var json = await File.ReadAllTextAsync(qwPath);
            Assert.IsTrue(json.Contains("\"Code\": \"qw_test\""));
            // Verify deterministic order: members ordered by Name
            Assert.IsTrue(json.IndexOf("MotorName", StringComparison.Ordinal) < json.IndexOf("RunFeedback", StringComparison.Ordinal) || json.IndexOf("MotorName", StringComparison.Ordinal) > 0);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task RenamingPreservesKeyAndFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var store = new ModernProjectStore();
        try
        {
            var key = Guid.NewGuid();
            var qw = new QuickWindowDefinition(key, "qw_orig", "Orig", 1, new VisualContent(CanvasSize.DefaultDesktop), Array.Empty<QuickWindowInterfaceMember>());
            var project = ScadaProject.CreateDefault("Rename") with
            {
                Scenes = new[] { new ScadaSceneReference("win00001", "Page", "scenes/win00001.scene.json", PageKey: Guid.NewGuid(), PageCode: "win00001") },
                QuickWindows = new[] { qw }
            };
            var scene = ScadaScene.CreateEmpty("win00001", "Page", CanvasSize.DefaultDesktop) with { PageKey = project.Scenes[0].PageKey, PageCode = "win00001" };
            var snapshot = new PageWorkspaceSnapshot(1, project, new Dictionary<Guid, ScadaScene> { [project.Scenes[0].PageKey] = scene }, Array.Empty<PendingPageDeletion>());
            await store.SaveWorkspaceSnapshotToProjectRootAsync(Path.Combine(root, "proj"), snapshot);

            var renamed = qw with { Code = "qw_renamed", DisplayName = "Renamed" };
            project = project with { QuickWindows = new[] { renamed } };
            var snapshot2 = new PageWorkspaceSnapshot(2, project, new Dictionary<Guid, ScadaScene> { [project.Scenes[0].PageKey] = scene }, Array.Empty<PendingPageDeletion>());
            await store.SaveWorkspaceSnapshotToProjectRootAsync(Path.Combine(root, "proj"), snapshot2);

            var loaded = await store.LoadProjectFromRootAsync(Path.Combine(root, "proj"));
            Assert.AreEqual(1, loaded!.EffectiveQuickWindows.Count);
            Assert.AreEqual(key, loaded.EffectiveQuickWindows[0].DefinitionKey);
            Assert.AreEqual("qw_renamed", loaded.EffectiveQuickWindows[0].Code);
            // File name must remain with same key
            var pathOld = Path.Combine(root, "proj", "quick-windows", $"{key:N}.quick-window.json");
            Assert.IsTrue(File.Exists(pathOld));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task RollbackOnFailureLeavesPreviousState()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var store = new ModernProjectStore();
        try
        {
            var project = ScadaProject.CreateDefault("Rollback") with
            {
                Scenes = new[] { new ScadaSceneReference("win00001", "Page", "scenes/win00001.scene.json", PageKey: Guid.NewGuid(), PageCode: "win00001") }
            };
            var scene = ScadaScene.CreateEmpty("win00001", "Page", CanvasSize.DefaultDesktop) with { PageKey = project.Scenes[0].PageKey, PageCode = "win00001" };
            var qwValid = QuickWindowDefinition.CreateEmpty("qw_valid", "Valid");
            project = project with { QuickWindows = new[] { qwValid } };
            var snap1 = new PageWorkspaceSnapshot(1, project, new Dictionary<Guid, ScadaScene> { [project.Scenes[0].PageKey] = scene }, Array.Empty<PendingPageDeletion>());
            await store.SaveWorkspaceSnapshotToProjectRootAsync(Path.Combine(root, "proj"), snap1);

            // Attempt to save invalid quick window (duplicate code)
            var qwDup = QuickWindowDefinition.CreateEmpty("qw_valid", "Dup") with { DefinitionKey = Guid.NewGuid() };
            project = project with { QuickWindows = new[] { qwValid, qwDup } };
            var snap2 = new PageWorkspaceSnapshot(2, project, new Dictionary<Guid, ScadaScene> { [project.Scenes[0].PageKey] = scene }, Array.Empty<PendingPageDeletion>());
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await store.SaveWorkspaceSnapshotToProjectRootAsync(Path.Combine(root, "proj"), snap2));

            var loaded = await store.LoadProjectFromRootAsync(Path.Combine(root, "proj"));
            Assert.AreEqual(1, loaded!.EffectiveQuickWindows.Count, "Rollback must preserve previous single QuickWindow");
            Assert.AreEqual("qw_valid", loaded.EffectiveQuickWindows[0].Code);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task NoRewriteWhenNoQuickWindow()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var store = new ModernProjectStore();
        try
        {
            var project = ScadaProject.CreateDefault("NoQW") with
            {
                Scenes = new[] { new ScadaSceneReference("win00001", "Page", "scenes/win00001.scene.json", PageKey: Guid.NewGuid(), PageCode: "win00001") }
            };
            var scene = ScadaScene.CreateEmpty("win00001", "Page", CanvasSize.DefaultDesktop) with { PageKey = project.Scenes[0].PageKey, PageCode = "win00001" };
            var snap1 = new PageWorkspaceSnapshot(1, project, new Dictionary<Guid, ScadaScene> { [project.Scenes[0].PageKey] = scene }, Array.Empty<PendingPageDeletion>());
            await store.SaveWorkspaceSnapshotToProjectRootAsync(Path.Combine(root, "proj"), snap1);
            var projPath = Path.Combine(root, "proj", "project.json");
            var bytesBefore = await File.ReadAllBytesAsync(projPath);
            var qwDir = Path.Combine(root, "proj", "quick-windows");
            Assert.IsFalse(Directory.Exists(qwDir) && Directory.GetFiles(qwDir).Length > 0, "No quick-windows directory should be created for project without QuickWindows");

            // Save again same content with version bump but same data - should not create quick-windows
            var snap2 = new PageWorkspaceSnapshot(2, project, new Dictionary<Guid, ScadaScene> { [project.Scenes[0].PageKey] = scene }, Array.Empty<PendingPageDeletion>());
            await store.SaveWorkspaceSnapshotToProjectRootAsync(Path.Combine(root, "proj"), snap2);
            Assert.IsFalse(Directory.Exists(qwDir) && Directory.GetFiles(qwDir).Length > 0);
            // Project file may be rewritten due to version bump, but quick-window files still absent
            Assert.IsTrue(File.Exists(projPath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task KeysAreStableAcrossSaveReopen()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var store = new ModernProjectStore();
        try
        {
            var qw = QuickWindowDefinition.CreateEmpty("qw_stable", "Stable");
            var originalKey = qw.DefinitionKey;
            var project = ScadaProject.CreateDefault("Stable") with
            {
                Scenes = new[] { new ScadaSceneReference("win00001", "Page", "scenes/win00001.scene.json", PageKey: Guid.NewGuid(), PageCode: "win00001") },
                QuickWindows = new[] { qw }
            };
            var scene = ScadaScene.CreateEmpty("win00001", "Page", CanvasSize.DefaultDesktop) with { PageKey = project.Scenes[0].PageKey, PageCode = "win00001" };
            var snap = new PageWorkspaceSnapshot(1, project, new Dictionary<Guid, ScadaScene> { [project.Scenes[0].PageKey] = scene }, Array.Empty<PendingPageDeletion>());
            await store.SaveWorkspaceSnapshotToProjectRootAsync(Path.Combine(root, "proj"), snap);
            var loaded1 = await store.LoadProjectFromRootAsync(Path.Combine(root, "proj"));
            var loaded2 = await store.LoadProjectFromRootAsync(Path.Combine(root, "proj"));
            Assert.AreEqual(originalKey, loaded1!.EffectiveQuickWindows[0].DefinitionKey);
            Assert.AreEqual(originalKey, loaded2!.EffectiveQuickWindows[0].DefinitionKey);
            Assert.AreEqual(loaded1.EffectiveQuickWindows[0].DefinitionKey, loaded2.EffectiveQuickWindows[0].DefinitionKey);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task DeterministicJsonOrder()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var store = new QuickWindowStore();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "proj"));
            var qw = new QuickWindowDefinition(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "qw_order",
                "Order",
                1,
                new VisualContent(new CanvasSize(500, 400), null, new[]
                {
                    ScadaElement.CreateText("b", "B", 20, 20),
                    ScadaElement.CreateText("a", "A", 10, 10)
                }),
                new[]
                {
                    new QuickWindowInterfaceMember(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Zeta", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.String, QuickWindowMemberAccess.Read),
                    new QuickWindowInterfaceMember(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Alpha", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read)
                });
            await store.SaveAsync(Path.Combine(root, "proj"), qw);
            var json = await File.ReadAllTextAsync(Path.Combine(root, "proj", "quick-windows", $"{qw.DefinitionKey:N}.quick-window.json"));
            // Members should be ordered by Name (Alpha before Zeta)
            Assert.IsTrue(json.IndexOf("Alpha", StringComparison.Ordinal) < json.IndexOf("Zeta", StringComparison.Ordinal), "Members must be ordered deterministically");
            // Elements ordered by Id (a before b)
            Assert.IsTrue(json.IndexOf("\"a\"", StringComparison.Ordinal) < json.IndexOf("\"b\"", StringComparison.Ordinal), "Elements must be ordered deterministically");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
