using System.IO.Compression;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Commands.Pages;
using ScadaBuilderV2.Application.History;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ModernProjects;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class PageLifecycleIntegrationTests
{
    [TestMethod]
    public async Task ImportedProjectSupportsCompleteModernPageLifecycleAndSb2Contract()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var archivePath = Path.Combine(root, "exports", "lifecycle.sb2");
        var store = new ModernProjectStore();
        try
        {
            var imported = new[]
            {
                Imported("header_main", ScadaPageType.Header),
                Imported("footer_main", ScadaPageType.Footer),
                Imported("home", ScadaPageType.Default),
                Imported("legacy_detail", ScadaPageType.Default)
            };
            var project = await store.EnsureReferenceModernProjectAsync(root, imported);
            var initial = await store.ReadWorkspaceSnapshotAsync(root);
            var header = initial.Project.Scenes.Single(page => page.EffectivePageCode == "header_main");
            var footer = initial.Project.Scenes.Single(page => page.EffectivePageCode == "footer_main");
            var home = initial.Project.Scenes.Single(page => page.EffectivePageCode == "home");
            var detail = initial.Project.Scenes.Single(page => page.EffectivePageCode == "legacy_detail");
            var homePage = home with
            {
                HeaderPageKey = header.PageKey,
                FooterPageKey = footer.PageKey,
                HeaderPageId = header.EffectivePageCode,
                FooterPageId = footer.EffectivePageCode
            };
            var homeScene = initial.Scenes[home.PageKey]
                .WithPageComposition(header.EffectivePageCode, footer.EffectivePageCode)
                .WithAction(new ScadaActionDefinition(
                    "navigate_detail",
                    ScadaActionKind.Navigate,
                    TargetPageId: detail.EffectivePageCode,
                    TargetPageKey: detail.PageKey)) with
            {
                HeaderPageKey = header.PageKey,
                FooterPageKey = footer.PageKey
            };
            var initialPages = initial.Project.Scenes.Select(page => page.PageKey == home.PageKey ? homePage : page).ToArray();
            var initialScenes = initial.Scenes.ToDictionary(pair => pair.Key, pair => pair.Value);
            initialScenes[home.PageKey] = homeScene;
            initial = initial with
            {
                Project = initial.Project with
                {
                    Scenes = initialPages,
                    HomePageKey = home.PageKey,
                    HomePageId = home.EffectivePageCode
                },
                Scenes = initialScenes
            };
            await store.SaveWorkspaceSnapshotAsync(root, initial);

            var stableKeys = initial.Project.Scenes.ToDictionary(page => page.EffectivePageCode, page => page.PageKey);
            var history = new EditorHistoryService();
            var context = CreateContext(initial, home.PageKey, history);
            var registry = CreateRegistry();

            var created = await ExecuteAsync(registry, context, "page.new", new NewPageRequest("native_blank", "Nouvelle page"));
            var nativeKey = created.PageToOpenKey!.Value;
            Assert.IsFalse(context.PageWorkspace!.Project.Scenes.Single(page => page.PageKey == nativeKey).IncludeInBuild);
            Assert.AreEqual(PageOrigin.Native, context.PageWorkspace.Project.Scenes.Single(page => page.PageKey == nativeKey).EffectiveOrigin);

            await ExecuteAsync(registry, context, "page.rename", new RenamePageRequest(nativeKey, "Réglages"));
            await ExecuteAsync(registry, context, "page.change-code", new ChangePageCodeRequest(nativeKey, "settings_page"));
            var duplicated = await ExecuteAsync(registry, context, "page.duplicate", new DuplicatePageRequest(detail.PageKey, "legacy_detail_copy"));
            var duplicate = context.PageWorkspace.Project.Scenes.Single(page => page.PageKey == duplicated.PageToOpenKey);
            Assert.AreEqual(PageOrigin.Imported, duplicate.EffectiveOrigin);
            Assert.AreEqual("Wonderware", duplicate.ImportProvenance?.SourceSystem);
            Assert.AreEqual(detail.ImportProvenance?.SourcePageId, duplicate.ImportProvenance?.SourcePageId);

            context.PageWorkspaceUi = new ProjectWorkspaceUiSnapshot(
                [home.PageKey, detail.PageKey],
                detail.PageKey,
                detail.PageKey,
                new Dictionary<Guid, EditorPageSelectionSnapshot>());
            var blockedDelete = await ExecuteAsync(registry, context, "page.delete", new DeletePageRequest(detail.PageKey));
            Assert.AreEqual(CommandResultStatus.Blocked, blockedDelete.Status);
            Assert.IsTrue(blockedDelete.Diagnostics.Any(issue => issue.Code == "page.delete-dependency"));

            var correctedScenes = context.PageWorkspace.Scenes.ToDictionary(pair => pair.Key, pair => pair.Value);
            correctedScenes[home.PageKey] = correctedScenes[home.PageKey] with { Actions = [] };
            context.PageWorkspace = context.PageWorkspace with { Scenes = correctedScenes };
            var deleted = await ExecuteAsync(registry, context, "page.delete", new DeletePageRequest(detail.PageKey));
            Assert.AreEqual(CommandResultStatus.Succeeded, deleted.Status);
            Assert.IsFalse(context.PageWorkspace.Project.Scenes.Any(page => page.PageKey == detail.PageKey));

            await store.SaveWorkspaceSnapshotAsync(root, context.PageWorkspace);
            var workspace = new MutableHistoryWorkspace(context.PageWorkspace, context.PageWorkspaceUi!, context.IsPageWorkspaceDirty);
            workspace.Ui = workspace.Ui with { OpenPageKeys = [], SelectedPageKey = null, ActivePageKey = null };
            Assert.IsTrue(await history.UndoAsync(workspace.CreateContext()));
            Assert.IsTrue(workspace.Project.Scenes.Any(page => page.PageKey == detail.PageKey));
            Assert.IsTrue(workspace.Scenes.ContainsKey(detail.PageKey));
            Assert.IsTrue(workspace.Ui.OpenPageKeys.Contains(detail.PageKey));

            var restored = workspace.ToSnapshot(context.PageWorkspace.Version + 1);
            await store.SaveWorkspaceSnapshotAsync(root, restored);
            var reloaded = await store.ReadWorkspaceSnapshotAsync(root);
            foreach (var (code, key) in stableKeys)
            {
                Assert.AreEqual(key, reloaded.Project.Scenes.Single(page => page.EffectivePageCode == code).PageKey);
            }
            Assert.AreEqual(nativeKey, reloaded.Project.Scenes.Single(page => page.EffectivePageCode == "settings_page").PageKey);
            Assert.IsFalse(reloaded.Project.Scenes.Single(page => page.PageKey == nativeKey).IncludeInBuild);

            Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
            var sourceHtml = Path.Combine(root, "wonderware-projection.html");
            await File.WriteAllTextAsync(sourceHtml, "<!doctype html><html><body><div class=\"page\"></div></body></html>");
            var inputs = reloaded.Project.Scenes
                .Where(page => page.IncludeInBuild)
                .Select(page => new Ft100ProjectPageExportInput(
                    reloaded.Scenes[page.PageKey],
                    page.EffectiveOrigin == PageOrigin.Imported ? sourceHtml : null,
                    page))
                .ToArray();
            var export = await new Ft100SceneExporter().ExportProjectArchiveAsync(reloaded.Project, inputs, archivePath);
            Assert.IsTrue(export.Validation.IsValid);

            using var archive = ZipFile.OpenRead(archivePath);
            var text = string.Join("\n", archive.Entries
                .Where(entry => entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || entry.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                .Select(ReadEntry));
            foreach (var page in reloaded.Project.Scenes)
            {
                Assert.IsFalse(text.Contains(page.PageKey.ToString("D"), StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(text.Contains(page.PageKey.ToString("N"), StringComparison.OrdinalIgnoreCase));
            }
            Assert.IsFalse(text.Contains("data-scada-selected", StringComparison.Ordinal));
            Assert.IsFalse(text.Contains("scada-placement-preview", StringComparison.Ordinal));
            Assert.IsTrue(archive.Entries.Any(entry => entry.FullName.Contains("legacy_detail_copy/legacy_detail_copy.html", StringComparison.Ordinal)));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static ScadaSceneReference Imported(string code, ScadaPageType type) => new(
        code,
        code,
        $"scenes/{code}.scene.json",
        type,
        IncludeInBuild: true,
        Origin: PageOrigin.Imported,
        ImportProvenance: new ImportProvenance("Wonderware", "LegacyProject", code));

    private static CommandRegistry CreateRegistry()
    {
        var coordinator = new PageCommandCoordinator();
        var registry = new CommandRegistry();
        foreach (var command in new IApplicationCommand[]
        {
            new NewPageCommand(coordinator),
            new RenamePageCommand(coordinator),
            new ChangePageCodeCommand(coordinator),
            new DuplicatePageCommand(coordinator),
            new DeletePageCommand(coordinator)
        }) registry.Register(command);
        return registry;
    }

    private static ApplicationContext CreateContext(PageWorkspaceSnapshot snapshot, Guid activeKey, EditorHistoryService history) => new()
    {
        CurrentProject = snapshot.Project,
        PageWorkspace = snapshot,
        PageWorkspaceUi = new ProjectWorkspaceUiSnapshot(
            [activeKey],
            activeKey,
            activeKey,
            new Dictionary<Guid, EditorPageSelectionSnapshot>()),
        SelectedPageKey = activeKey,
        ActiveEditorPageKey = activeKey,
        HomePageKey = snapshot.Project.EffectiveHomePageKey,
        WorkspaceHistory = history,
        ApplyPageWorkspaceMutation = _ => { }
    };

    private static async Task<CommandResult> ExecuteAsync(
        CommandRegistry registry,
        ApplicationContext context,
        string commandId,
        PageCommandRequest request)
    {
        context.PageCommandRequest = request;
        return await registry.ExecuteAsync(commandId, context);
    }

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private sealed class MutableHistoryWorkspace(PageWorkspaceSnapshot snapshot, ProjectWorkspaceUiSnapshot ui, bool isDirty)
    {
        public ScadaProject Project { get; private set; } = snapshot.Project;
        public Dictionary<Guid, ScadaScene> Scenes { get; } = snapshot.Scenes.ToDictionary(pair => pair.Key, pair => pair.Value);
        public ProjectWorkspaceUiSnapshot Ui { get; set; } = ui;
        public bool IsDirty { get; private set; } = isDirty;
        public IReadOnlyList<Guid> PendingDeletedPageKeys { get; private set; } = snapshot.PendingDeletions.Select(item => item.PageKey).ToArray();

        public EditorHistoryContext CreateContext() => new()
        {
            ActiveSceneId = string.Empty,
            GetActiveScene = () => null,
            ReplaceActiveScene = _ => throw new AssertFailedException("Project history must not require an active scene."),
            GetSceneByPageKey = key => Scenes.GetValueOrDefault(key),
            ReplaceSceneByPageKey = (key, scene) => Scenes[key] = scene,
            RemoveSceneByPageKey = key => Scenes.Remove(key),
            GetWorkspaceSceneKeys = () => Scenes.Keys.ToArray(),
            GetProject = () => Project,
            ReplaceProject = project => Project = project,
            RestoreWorkspaceUi = value => Ui = value,
            SetPendingDeletedPageKeys = keys => PendingDeletedPageKeys = keys.ToArray(),
            SetWorkspaceDirty = value => IsDirty = value,
            MarkDirty = () => IsDirty = true,
            RefreshPreviewAsync = () => Task.CompletedTask,
            RefreshTargetAsync = _ => Task.CompletedTask,
            SetStatus = _ => { }
        };

        public PageWorkspaceSnapshot ToSnapshot(long version)
        {
            var deletions = PendingDeletedPageKeys.Select(key => new PendingPageDeletion(
                key,
                Project.Scenes.FirstOrDefault(page => page.PageKey == key)?.RelativePath ?? $"scenes/{key:N}.scene.json")).ToArray();
            return new PageWorkspaceSnapshot(version, Project, Scenes, deletions);
        }
    }
}
