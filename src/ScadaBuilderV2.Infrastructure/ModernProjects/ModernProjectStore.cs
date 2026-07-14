using System.Text.Json;
using System.Text.Json.Serialization;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Domain.Versioning;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

public sealed class ModernProjectStore : IPageWorkspaceStore, IPageWorkspaceReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<ScadaProject> EnsureReferenceModernProjectAsync(string repositoryRoot, IReadOnlyList<ScadaSceneReference> scenes)
    {
        var projectRoot = GetReferenceModernProjectRoot(repositoryRoot);
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(Path.Combine(projectRoot, "scenes"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "assets"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "library", "elements"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "libraries"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "imports", "legacy"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "imports", "tags"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "exports"));
        await RecoverIncompleteTransactionsAsync(projectRoot);

        var projectPath = Path.Combine(projectRoot, "project.json");
        var project = ModernProjectMigration.MigrateProject(new ScadaProject(
            "AMR_REF_SCADA_V2",
            ScadaVersion.Initial,
            CanvasSize.DefaultDesktop,
            ResponsiveMode.Fixed,
            AuthoringMode.DesktopFirst,
            DefaultDevicePresets.All,
            scenes), scenes);
        var originalJson = string.Empty;

        if (File.Exists(projectPath))
        {
            var existing = await LoadProjectFileAsync(projectPath);
            if (existing is not null)
            {
                originalJson = JsonSerializer.Serialize(existing, JsonOptions);
                var migratedExisting = ModernProjectMigration.MigrateProject(existing, scenes);
                project = ModernProjectMigration.MigrateProject(migratedExisting with
                {
                    ManifestVersion = string.IsNullOrWhiteSpace(existing.ManifestVersion) ? "2.0" : existing.ManifestVersion,
                    Scenes = MergeSceneReferences(migratedExisting.Scenes, project.Scenes)
                }, scenes);
            }
        }

        var migratedJson = JsonSerializer.Serialize(project, JsonOptions);
        if (!File.Exists(projectPath) || !string.Equals(originalJson, migratedJson, StringComparison.Ordinal))
        {
            await SaveJsonAsync(projectPath, project);
        }

        return project;
    }

    public async Task<ScadaScene> LoadOrCreateSceneAsync(string repositoryRoot, string sceneId, string title, CanvasSize canvasSize)
    {
        await RecoverIncompleteTransactionsAsync(GetReferenceModernProjectRoot(repositoryRoot));
        var path = GetScenePath(repositoryRoot, sceneId);
        if (File.Exists(path))
        {
            await using var read = File.OpenRead(path);
            var scene = await JsonSerializer.DeserializeAsync<ScadaScene>(read, JsonOptions);
            if (scene is not null)
            {
                var normalized = scene.WithoutConvertedLegacyTextOverrides();
                var project = await LoadProjectAsync(repositoryRoot);
                return project is null ? normalized : ModernProjectMigration.MigrateScene(normalized, project);
            }
        }

        return ScadaScene.CreateEmpty(sceneId, title, canvasSize);
    }

    /// <summary>Loads a page scene from its durable project-relative path or creates its modern native snapshot.</summary>
    public async Task<ScadaScene> LoadOrCreateSceneAsync(
        string repositoryRoot,
        ScadaSceneReference page,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(page);
        var projectRoot = GetReferenceModernProjectRoot(repositoryRoot);
        await RecoverIncompleteTransactionsAsync(projectRoot, cancellationToken);
        var path = ResolveContainedScenePath(projectRoot, page.RelativePath);
        if (File.Exists(path))
        {
            await using var stream = File.OpenRead(path);
            var scene = await JsonSerializer.DeserializeAsync<ScadaScene>(stream, JsonOptions, cancellationToken);
            if (scene is not null)
            {
                var project = await LoadProjectAsync(repositoryRoot);
                return project is null
                    ? scene.WithoutConvertedLegacyTextOverrides()
                    : ModernProjectMigration.MigrateScene(scene.WithoutConvertedLegacyTextOverrides(), project);
            }
        }

        return ScadaScene.CreateEmpty(page.EffectivePageCode, page.Title, page.EffectiveCanvasSize) with
        {
            PageKey = page.PageKey,
            PageCode = page.EffectivePageCode,
            Origin = page.EffectiveOrigin,
            ImportProvenance = page.ImportProvenance,
            PageType = page.Type,
            Background = page.EffectiveBackground,
            BackgroundColor = page.EffectiveBackground.Color,
            IncludeInBuild = page.IncludeInBuild,
            HeaderPageId = page.HeaderPageId,
            FooterPageId = page.FooterPageId,
            HeaderPageKey = page.HeaderPageKey,
            FooterPageKey = page.FooterPageKey
        };
    }

    public async Task SaveSceneAsync(string repositoryRoot, ScadaScene scene)
    {
        var project = await LoadProjectAsync(repositoryRoot);
        var normalized = project is null ? scene : ModernProjectMigration.MigrateScene(scene, project);
        var projectRoot = GetReferenceModernProjectRoot(repositoryRoot);
        var existingReference = project?.Scenes.FirstOrDefault(reference =>
            (normalized.PageKey != Guid.Empty && reference.PageKey == normalized.PageKey) ||
            string.Equals(reference.EffectivePageCode, normalized.EffectivePageCode, StringComparison.OrdinalIgnoreCase));
        var path = existingReference is null
            ? GetScenePath(repositoryRoot, normalized.Id)
            : ResolveContainedScenePath(projectRoot, existingReference.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await SaveJsonAsync(path, normalized);
        await UpsertSceneReferenceAsync(repositoryRoot, normalized);
    }

    public async Task<ScadaProject?> LoadProjectAsync(string repositoryRoot)
    {
        var projectRoot = GetReferenceModernProjectRoot(repositoryRoot);
        await RecoverIncompleteTransactionsAsync(projectRoot);
        var projectPath = Path.Combine(projectRoot, "project.json");
        var project = File.Exists(projectPath)
            ? await LoadProjectFileAsync(projectPath)
            : null;
        return project is null ? null : ModernProjectMigration.MigrateProject(project);
    }

    /// <inheritdoc />
    public async Task<PageWorkspaceSnapshot> ReadWorkspaceSnapshotAsync(
        string repositoryRoot,
        PageWorkspaceReadContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var project = context?.ProjectOverride ?? await LoadProjectAsync(repositoryRoot)
            ?? throw new InvalidOperationException("No modern SCADA project exists at the requested repository root.");
        project = ModernProjectMigration.MigrateProject(project);
        var overrides = context?.OpenOrDirtyScenes ?? new Dictionary<Guid, ScadaScene>();
        var scenes = new Dictionary<Guid, ScadaScene>();

        foreach (var page in project.Scenes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scene = overrides.TryGetValue(page.PageKey, out var openOrDirtyScene)
                ? openOrDirtyScene
                : await LoadOrCreateSceneAsync(repositoryRoot, page, cancellationToken);
            scenes[page.PageKey] = ModernProjectMigration.MigrateScene(scene, project);
        }

        return new PageWorkspaceSnapshot(
            Math.Max(1, context?.Version ?? 1),
            project,
            scenes,
            context?.PendingDeletions ?? Array.Empty<PendingPageDeletion>());
    }

    public async Task SaveProjectAsync(string repositoryRoot, ScadaProject project)
    {
        var projectPath = Path.Combine(GetReferenceModernProjectRoot(repositoryRoot), "project.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        await SaveJsonAsync(projectPath, ModernProjectMigration.MigrateProject(project));
    }

    /// <inheritdoc />
    public async Task SaveWorkspaceSnapshotAsync(
        string repositoryRoot,
        PageWorkspaceSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(snapshot);

        var projectRoot = GetReferenceModernProjectRoot(repositoryRoot);
        Directory.CreateDirectory(projectRoot);
        await using var workspaceLock = await AcquireWorkspaceLockAsync(projectRoot, cancellationToken);
        await RecoverIncompleteTransactionsAsync(projectRoot, cancellationToken, lockAlreadyHeld: true);
        var normalized = ValidateAndNormalizeSnapshot(projectRoot, snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        var transactionsRoot = Path.Combine(projectRoot, ".studio", "transactions");
        Directory.CreateDirectory(transactionsRoot);
        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = ResolveContainedPath(transactionsRoot, transactionId);
        Directory.CreateDirectory(transactionRoot);

        WorkspaceSaveJournal? journal = null;
        try
        {
            var entries = new List<WorkspaceSaveFileEntry>();
            foreach (var reference in normalized.Project.Scenes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scene = normalized.Scenes[reference.PageKey];
                entries.Add(await StageJsonAsync(
                    projectRoot,
                    transactionRoot,
                    reference.RelativePath,
                    scene,
                    cancellationToken));
            }

            entries.Add(await StageJsonAsync(
                projectRoot,
                transactionRoot,
                "project.json",
                normalized.Project,
                cancellationToken));
            await ValidateStagedSnapshotAsync(transactionRoot, entries, normalized.Project, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            journal = new WorkspaceSaveJournal(
                transactionId,
                normalized.Version,
                WorkspaceSavePhase.Prepared,
                entries,
                normalized.PendingDeletions.Select(item => NormalizeRelativePath(item.RelativePath)).ToArray());
            await SaveJournalAsync(transactionRoot, journal);

            journal = journal with { Phase = WorkspaceSavePhase.WritingScenes };
            await SaveJournalAsync(transactionRoot, journal);
            foreach (var entry in entries.Where(entry => !string.Equals(entry.TargetRelativePath, "project.json", StringComparison.OrdinalIgnoreCase)))
            {
                ReplaceStagedFile(projectRoot, transactionRoot, entry);
            }

            journal = journal with { Phase = WorkspaceSavePhase.ProjectCommitting };
            await SaveJournalAsync(transactionRoot, journal);
            ReplaceStagedFile(
                projectRoot,
                transactionRoot,
                entries.Single(entry => string.Equals(entry.TargetRelativePath, "project.json", StringComparison.OrdinalIgnoreCase)));

            journal = journal with { Phase = WorkspaceSavePhase.ProjectCommitted };
            await SaveJournalAsync(transactionRoot, journal);
            ApplyPendingDeletions(projectRoot, journal.Deletions);

            journal = journal with { Phase = WorkspaceSavePhase.Completed };
            await SaveJournalAsync(transactionRoot, journal);
            DeleteTransactionDirectory(transactionsRoot, transactionRoot);
        }
        catch
        {
            if (journal is not null && journal.Phase < WorkspaceSavePhase.ProjectCommitted)
            {
                RollbackTransaction(projectRoot, transactionRoot, journal);
            }

            if (journal is null || journal.Phase < WorkspaceSavePhase.ProjectCommitted)
            {
                DeleteTransactionDirectory(transactionsRoot, transactionRoot);
            }

            throw;
        }
    }

    public static string GetReferenceModernProjectRoot(string repositoryRoot)
    {
        return Path.Combine(repositoryRoot, "SCADA_BUILDER_V2", "projects", "AMR_REF_SCADA_V2");
    }

    private static PageWorkspaceSnapshot ValidateAndNormalizeSnapshot(
        string projectRoot,
        PageWorkspaceSnapshot snapshot)
    {
        if (snapshot.Version <= 0)
        {
            throw new InvalidOperationException("Workspace snapshot version must be greater than zero.");
        }

        var project = ModernProjectMigration.MigrateProject(snapshot.Project);
        var duplicateKey = project.Scenes
            .Where(page => page.PageKey != Guid.Empty)
            .GroupBy(page => page.PageKey)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateKey is not null)
        {
            throw new InvalidOperationException($"Duplicate PageKey '{duplicateKey.Key}' cannot be saved.");
        }

        if (project.Scenes.Any(page => page.PageKey == Guid.Empty))
        {
            throw new InvalidOperationException("Every page must have a PageKey before saving a workspace snapshot.");
        }

        var duplicateCode = project.Scenes
            .GroupBy(page => page.EffectivePageCode, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCode is not null)
        {
            throw new InvalidOperationException($"Duplicate PageCode '{duplicateCode.Key}' cannot be saved.");
        }

        var normalizedReferences = project.Scenes.Select(reference =>
        {
            var validation = PageCodePolicy.Validate(reference.EffectivePageCode);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.Errors[0]);
            }

            var relativePath = NormalizeRelativePath(reference.RelativePath);
            _ = ResolveContainedScenePath(projectRoot, relativePath);
            return reference with { RelativePath = relativePath };
        }).ToArray();
        var duplicatePath = normalizedReferences
            .GroupBy(reference => reference.RelativePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePath is not null)
        {
            throw new InvalidOperationException($"Scene path '{duplicatePath.Key}' is used by more than one page.");
        }

        project = ModernProjectMigration.MigrateProject(project with { Scenes = normalizedReferences });
        var activeKeys = project.Scenes.Select(page => page.PageKey).ToHashSet();
        if (snapshot.Scenes.Count != activeKeys.Count || snapshot.Scenes.Keys.Any(key => !activeKeys.Contains(key)))
        {
            throw new InvalidOperationException("Workspace scenes must match the project page inventory exactly.");
        }

        var scenes = new Dictionary<Guid, ScadaScene>();
        foreach (var reference in project.Scenes)
        {
            if (!snapshot.Scenes.TryGetValue(reference.PageKey, out var scene))
            {
                throw new InvalidOperationException($"Page '{reference.EffectivePageCode}' has no scene snapshot.");
            }

            var normalizedScene = ModernProjectMigration.MigrateScene(scene, project);
            if (normalizedScene.PageKey != reference.PageKey)
            {
                throw new InvalidOperationException($"Scene '{scene.Id}' does not match PageKey '{reference.PageKey}'.");
            }

            scenes[reference.PageKey] = normalizedScene;
        }

        var activePaths = project.Scenes
            .Select(page => page.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pendingDeletions = snapshot.PendingDeletions.Select(deletion =>
        {
            if (deletion.PageKey == Guid.Empty || activeKeys.Contains(deletion.PageKey))
            {
                throw new InvalidOperationException("A pending deletion must identify a page absent from the saved project.");
            }

            var relativePath = NormalizeRelativePath(deletion.RelativePath);
            _ = ResolveContainedScenePath(projectRoot, relativePath);
            if (activePaths.Contains(relativePath))
            {
                throw new InvalidOperationException($"Active scene path '{relativePath}' cannot be deleted.");
            }

            return deletion with { RelativePath = relativePath };
        }).ToArray();
        if (pendingDeletions.GroupBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("Pending workspace deletions contain duplicate scene paths.");
        }

        return new PageWorkspaceSnapshot(snapshot.Version, project, scenes, pendingDeletions);
    }

    private static async Task<WorkspaceSaveFileEntry> StageJsonAsync<T>(
        string projectRoot,
        string transactionRoot,
        string targetRelativePath,
        T value,
        CancellationToken cancellationToken)
    {
        var normalizedTarget = NormalizeRelativePath(targetRelativePath);
        var targetPath = string.Equals(normalizedTarget, "project.json", StringComparison.OrdinalIgnoreCase)
            ? ResolveContainedPath(projectRoot, normalizedTarget)
            : ResolveContainedScenePath(projectRoot, normalizedTarget);
        var stagedRelativePath = NormalizeRelativePath(Path.Combine("new", normalizedTarget));
        var backupRelativePath = NormalizeRelativePath(Path.Combine("backup", normalizedTarget));
        var stagedPath = ResolveContainedPath(transactionRoot, stagedRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
        await SaveJsonAsync(stagedPath, value, cancellationToken);
        return new WorkspaceSaveFileEntry(
            normalizedTarget,
            stagedRelativePath,
            backupRelativePath,
            File.Exists(targetPath));
    }

    private static async Task ValidateStagedSnapshotAsync(
        string transactionRoot,
        IReadOnlyList<WorkspaceSaveFileEntry> entries,
        ScadaProject expectedProject,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stagedPath = ResolveContainedPath(transactionRoot, entry.StagedRelativePath);
            await using var stream = File.OpenRead(stagedPath);
            if (string.Equals(entry.TargetRelativePath, "project.json", StringComparison.OrdinalIgnoreCase))
            {
                var project = await JsonSerializer.DeserializeAsync<ScadaProject>(stream, JsonOptions, cancellationToken);
                if (project is null || project.Scenes.Count != expectedProject.Scenes.Count)
                {
                    throw new InvalidDataException("Staged project.json did not pass workspace validation.");
                }
            }
            else
            {
                var scene = await JsonSerializer.DeserializeAsync<ScadaScene>(stream, JsonOptions, cancellationToken);
                if (scene is null || scene.PageKey == Guid.Empty)
                {
                    throw new InvalidDataException($"Staged scene '{entry.TargetRelativePath}' did not pass validation.");
                }
            }
        }
    }

    private static void ReplaceStagedFile(
        string projectRoot,
        string transactionRoot,
        WorkspaceSaveFileEntry entry)
    {
        var targetPath = ResolveContainedPath(projectRoot, entry.TargetRelativePath);
        var stagedPath = ResolveContainedPath(transactionRoot, entry.StagedRelativePath);
        var backupPath = ResolveContainedPath(transactionRoot, entry.BackupRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

        if (entry.TargetExisted)
        {
            if (!File.Exists(targetPath))
            {
                throw new IOException($"Workspace target disappeared during save: {entry.TargetRelativePath}");
            }

            File.Replace(stagedPath, targetPath, backupPath, ignoreMetadataErrors: true);
            return;
        }

        File.Move(stagedPath, targetPath);
    }

    private static void RollbackTransaction(
        string projectRoot,
        string transactionRoot,
        WorkspaceSaveJournal journal)
    {
        foreach (var entry in journal.Writes.Reverse())
        {
            var targetPath = ResolveContainedPath(projectRoot, entry.TargetRelativePath);
            var stagedPath = ResolveContainedPath(transactionRoot, entry.StagedRelativePath);
            var backupPath = ResolveContainedPath(transactionRoot, entry.BackupRelativePath);
            if (entry.TargetExisted && File.Exists(backupPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Move(backupPath, targetPath, overwrite: true);
            }
            else if (!entry.TargetExisted && !File.Exists(stagedPath) && File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    private static void ApplyPendingDeletions(string projectRoot, IEnumerable<string> relativePaths)
    {
        foreach (var relativePath in relativePaths)
        {
            var path = ResolveContainedScenePath(projectRoot, relativePath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static async Task RecoverIncompleteTransactionsAsync(
        string projectRoot,
        CancellationToken cancellationToken = default,
        bool lockAlreadyHeld = false)
    {
        FileStream? workspaceLock = null;
        if (!lockAlreadyHeld)
        {
            workspaceLock = await AcquireWorkspaceLockAsync(projectRoot, cancellationToken);
        }

        try
        {
            var transactionsRoot = Path.Combine(projectRoot, ".studio", "transactions");
            if (!Directory.Exists(transactionsRoot))
            {
                return;
            }

            foreach (var transactionRoot in Directory.GetDirectories(transactionsRoot).OrderBy(path => path, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var journalPath = ResolveContainedPath(transactionRoot, "journal.json");
                if (!File.Exists(journalPath))
                {
                    DeleteTransactionDirectory(transactionsRoot, transactionRoot);
                    continue;
                }

                WorkspaceSaveJournal journal;
                await using (var stream = File.OpenRead(journalPath))
                {
                    journal = await JsonSerializer.DeserializeAsync<WorkspaceSaveJournal>(stream, JsonOptions, cancellationToken)
                        ?? throw new InvalidDataException($"Workspace recovery journal is invalid: {journalPath}");
                }
                if (journal.Phase >= WorkspaceSavePhase.ProjectCommitted)
                {
                    ApplyPendingDeletions(projectRoot, journal.Deletions);
                }
                else
                {
                    RollbackTransaction(projectRoot, transactionRoot, journal);
                }

                DeleteTransactionDirectory(transactionsRoot, transactionRoot);
            }
        }
        finally
        {
            if (workspaceLock is not null)
            {
                await workspaceLock.DisposeAsync();
            }
        }
    }

    private static async Task<FileStream> AcquireWorkspaceLockAsync(
        string projectRoot,
        CancellationToken cancellationToken)
    {
        var studioRoot = Path.Combine(projectRoot, ".studio");
        Directory.CreateDirectory(studioRoot);
        var lockPath = Path.Combine(studioRoot, "workspace-save.lock");
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous);
            }
            catch (IOException)
            {
                await Task.Delay(50, cancellationToken);
            }
        }
    }

    private static async Task SaveJournalAsync(string transactionRoot, WorkspaceSaveJournal journal)
    {
        var journalPath = ResolveContainedPath(transactionRoot, "journal.json");
        var pendingPath = ResolveContainedPath(transactionRoot, "journal.pending.json");
        await SaveJsonAsync(pendingPath, journal);
        File.Move(pendingPath, journalPath, overwrite: true);
    }

    private static string ResolveContainedScenePath(string projectRoot, string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        if (!normalized.StartsWith("scenes/", StringComparison.OrdinalIgnoreCase) ||
            !normalized.EndsWith(".scene.json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Scene path must remain under scenes/: {relativePath}");
        }

        return ResolveContainedPath(projectRoot, normalized);
    }

    private static string ResolveContainedPath(string root, string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        if (Path.IsPathRooted(normalized) || normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Workspace path is not portable: {relativePath}");
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new InvalidOperationException($"Workspace path is invalid: {relativePath}");
        }

        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, Path.Combine(segments)));
        if (!fullPath.StartsWith($"{fullRoot}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Workspace path escapes the project root: {relativePath}");
        }

        return fullPath;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return relativePath.Trim().Replace('\\', '/');
    }

    private static void DeleteTransactionDirectory(string transactionsRoot, string transactionRoot)
    {
        var verifiedRoot = Path.GetFullPath(transactionsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var verifiedTransaction = Path.GetFullPath(transactionRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!verifiedTransaction.StartsWith($"{verifiedRoot}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Transaction cleanup path escapes the workspace transaction root.");
        }

        if (Directory.Exists(verifiedTransaction))
        {
            Directory.Delete(verifiedTransaction, recursive: true);
        }
    }

    /// <summary>
    /// Gets the project-local directory where imported tag export snapshots are stored.
    /// </summary>
    public static string GetTagImportDirectory(string repositoryRoot)
    {
        return Path.Combine(GetReferenceModernProjectRoot(repositoryRoot), "imports", "tags");
    }

    private static string GetScenePath(string repositoryRoot, string sceneId)
    {
        return Path.Combine(GetReferenceModernProjectRoot(repositoryRoot), "scenes", $"{sceneId}.scene.json");
    }

    private static async Task SaveJsonAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        await using var write = File.Create(path);
        await JsonSerializer.SerializeAsync(write, value, JsonOptions, cancellationToken);
    }

    private static async Task<ScadaProject?> LoadProjectFileAsync(string projectPath)
    {
        await using var read = File.OpenRead(projectPath);
        return await JsonSerializer.DeserializeAsync<ScadaProject>(read, JsonOptions);
    }

    private static async Task UpsertSceneReferenceAsync(string repositoryRoot, ScadaScene scene)
    {
        var projectRoot = GetReferenceModernProjectRoot(repositoryRoot);
        var projectPath = Path.Combine(projectRoot, "project.json");
        if (!File.Exists(projectPath))
        {
            return;
        }

        var project = await LoadProjectFileAsync(projectPath);
        if (project is null)
        {
            return;
        }

        var existingReference = project.Scenes.FirstOrDefault(existing =>
            (scene.PageKey != Guid.Empty && existing.PageKey == scene.PageKey) ||
            string.Equals(existing.EffectivePageCode, scene.EffectivePageCode, StringComparison.OrdinalIgnoreCase));
        var reference = new ScadaSceneReference(
            scene.EffectivePageCode,
            scene.Title,
            existingReference?.RelativePath ?? $"scenes/{scene.Id}.scene.json",
            scene.PageType,
            scene.CanvasSize,
            scene.EffectiveBackground,
            scene.IncludeInBuild,
            scene.HeaderPageId,
            scene.FooterPageId,
            scene.PageKey,
            scene.EffectivePageCode,
            scene.EffectiveOrigin,
            scene.ImportProvenance,
            scene.HeaderPageKey,
            scene.FooterPageKey);

        var replaced = false;
        var scenes = project.Scenes.Select(existing =>
        {
            if ((scene.PageKey != Guid.Empty && existing.PageKey == scene.PageKey) ||
                string.Equals(existing.EffectivePageCode, scene.EffectivePageCode, StringComparison.OrdinalIgnoreCase))
            {
                replaced = true;
                return reference;
            }

            return existing;
        }).ToList();
        if (!replaced)
        {
            scenes.Add(reference);
        }

        var homePageId = project.HomePageId;
        if (!string.IsNullOrWhiteSpace(homePageId) &&
            string.Equals(homePageId, scene.Id, StringComparison.Ordinal) &&
            (scene.PageType != ScadaPageType.Default || !scene.IncludeInBuild))
        {
            homePageId = null;
        }

        await SaveJsonAsync(projectPath, ModernProjectMigration.MigrateProject(project with
        {
            Scenes = scenes,
            ManifestVersion = "2.0",
            HomePageId = homePageId
        }));
    }

    private static IReadOnlyList<ScadaSceneReference> MergeSceneReferences(
        IReadOnlyList<ScadaSceneReference> existing,
        IReadOnlyList<ScadaSceneReference> incoming)
    {
        var merged = existing.ToList();
        var existingCodes = existing.Select(scene => scene.EffectivePageCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        merged.AddRange(incoming.Where(scene => !existingCodes.Contains(scene.EffectivePageCode)));
        return merged;
    }
}
