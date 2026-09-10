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
            var json = await File.ReadAllTextAsync(path);
            ThrowIfRetiredPopupCommandKind(json, $"scene '{sceneId}'");
            await using var read = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
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
        return await LoadOrCreateSceneFromProjectRootAsync(
            GetReferenceModernProjectRoot(repositoryRoot),
            page,
            cancellationToken);
    }

    /// <summary>Loads one scene from an exact project root without applying reference-project path conventions.</summary>
    /// <remarks>
    /// Decisions: DEC-0049.
    /// Contracts: docs/superpowers/specs/2026-07-29-project-lifecycle-design.md.
    /// Tests: tests/ScadaBuilderV2.Tests/ProjectLifecycleIntegrationTests.cs.
    /// </remarks>
    public async Task<ScadaScene> LoadOrCreateSceneFromProjectRootAsync(
        string projectRoot,
        ScadaSceneReference page,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(page);
        projectRoot = Path.GetFullPath(projectRoot);
        await RecoverIncompleteTransactionsAsync(projectRoot, cancellationToken);
        var path = ResolveContainedScenePath(projectRoot, page.RelativePath);
        if (File.Exists(path))
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            ThrowIfRetiredPopupCommandKind(json, $"scene '{page.EffectivePageCode}' ({page.RelativePath})");
            await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            var scene = await JsonSerializer.DeserializeAsync<ScadaScene>(stream, JsonOptions, cancellationToken);
            if (scene is not null)
            {
                var project = await LoadProjectFromRootAsync(projectRoot, cancellationToken);
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

    /// <summary>Saves one scene file without mutating the authoritative project page inventory.</summary>
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
    }

    public async Task<ScadaProject?> LoadProjectAsync(string repositoryRoot)
    {
        return await LoadProjectFromRootAsync(GetReferenceModernProjectRoot(repositoryRoot));
    }

    /// <summary>Loads a project manifest from an exact project root.</summary>
    public async Task<ScadaProject?> LoadProjectFromRootAsync(
        string projectRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        projectRoot = Path.GetFullPath(projectRoot);
        await RecoverIncompleteTransactionsAsync(projectRoot);
        var projectPath = Path.Combine(projectRoot, "project.json");
        var project = File.Exists(projectPath)
            ? await LoadProjectFileAsync(projectPath, cancellationToken)
            : null;
        if (project is null)
            return null;
        project = ModernProjectMigration.MigrateProject(project);
        // QuickWindow definition files are authoritative. project.json never embeds full definitions.
        var qwStore = new QuickWindowStore();
        var qwFromFiles = await qwStore.LoadAllAsync(projectRoot, cancellationToken);
        if (qwFromFiles.Count > 0)
        {
            project = project with { QuickWindows = qwFromFiles };
        }
        else if (project.EffectiveQuickWindows.Count > 0)
        {
            throw new InvalidDataException("project.json contains inline QuickWindow definitions without authoritative quick-windows/*.quick-window.json files. The file remains unchanged; no implicit migration is performed.");
        }
        return ModernProjectMigration.MigrateProject(project);
    }

    /// <inheritdoc />
    public async Task<PageWorkspaceSnapshot> ReadWorkspaceSnapshotAsync(
        string repositoryRoot,
        PageWorkspaceReadContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        return await ReadWorkspaceSnapshotFromProjectRootAsync(
            GetReferenceModernProjectRoot(repositoryRoot),
            context,
            cancellationToken);
    }

    /// <summary>Reads a coherent workspace snapshot from an exact project root.</summary>
    public async Task<PageWorkspaceSnapshot> ReadWorkspaceSnapshotFromProjectRootAsync(
        string projectRoot,
        PageWorkspaceReadContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        projectRoot = Path.GetFullPath(projectRoot);
        var project = context?.ProjectOverride ?? await LoadProjectFromRootAsync(projectRoot, cancellationToken)
            ?? throw new InvalidOperationException("No modern SCADA project exists at the requested repository root.");
        project = ModernProjectMigration.MigrateProject(project);
        var overrides = context?.OpenOrDirtyScenes ?? new Dictionary<Guid, ScadaScene>();
        var scenes = new Dictionary<Guid, ScadaScene>();

        foreach (var page in project.Scenes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scene = overrides.TryGetValue(page.PageKey, out var openOrDirtyScene)
                ? openOrDirtyScene
                : await LoadOrCreateSceneFromProjectRootAsync(projectRoot, page, cancellationToken);
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
        await SaveProjectToRootAsync(GetReferenceModernProjectRoot(repositoryRoot), project);
    }

    /// <summary>Saves a project manifest under an exact project root.</summary>
    public async Task SaveProjectToRootAsync(
        string projectRoot,
        ScadaProject project,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(project);
        projectRoot = Path.GetFullPath(projectRoot);
        var normalized = ModernProjectMigration.MigrateProject(project);
        var quickWindowStore = new QuickWindowStore();
        foreach (var definition in normalized.EffectiveQuickWindows.OrderBy(item => item.Code, StringComparer.Ordinal).ThenBy(item => item.DefinitionKey))
            await quickWindowStore.SaveAsync(projectRoot, definition, cancellationToken);
        var projectPath = Path.Combine(Path.GetFullPath(projectRoot), "project.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        await SaveJsonAsync(projectPath, normalized with { QuickWindows = null }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveWorkspaceSnapshotAsync(
        string repositoryRoot,
        PageWorkspaceSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        await SaveWorkspaceSnapshotToProjectRootAsync(
            GetReferenceModernProjectRoot(repositoryRoot),
            snapshot,
            cancellationToken);
    }

    /// <summary>Atomically saves a workspace snapshot under an exact project root.</summary>
    public async Task SaveWorkspaceSnapshotToProjectRootAsync(
        string projectRoot,
        PageWorkspaceSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(snapshot);

        projectRoot = Path.GetFullPath(projectRoot);
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

            // Stage quick window definitions deterministically
            foreach (var qw in normalized.Project.EffectiveQuickWindows.OrderBy(qw => qw.Code, StringComparer.Ordinal).ThenBy(qw => qw.DefinitionKey))
            {
                cancellationToken.ThrowIfCancellationRequested();
                entries.Add(await QuickWindowStore.StageJsonAsync(projectRoot, transactionRoot, qw, cancellationToken));
            }

            entries.Add(await StageJsonAsync(
                projectRoot,
                transactionRoot,
                "project.json",
                normalized.Project with { QuickWindows = null },
                cancellationToken));
            await ValidateStagedSnapshotAsync(transactionRoot, entries, normalized.Project, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Compute quick-window deletions: any file under quick-windows/ not in normalized project
            var existingQwRelative = new List<string>();
            var qwDir = Path.Combine(projectRoot, "quick-windows");
            if (Directory.Exists(qwDir))
            {
                foreach (var file in Directory.GetFiles(qwDir, "*.quick-window.json", SearchOption.TopDirectoryOnly))
                {
                    var rel = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
                    existingQwRelative.Add(rel);
                }
            }
            var normalizedQwRelative = normalized.Project.EffectiveQuickWindows.Select(qw => QuickWindowStore.GetRelativePath(qw.DefinitionKey)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var qwDeletions = existingQwRelative.Where(rel => !normalizedQwRelative.Contains(rel)).ToArray();
            var allDeletions = normalized.PendingDeletions.Select(item => NormalizeRelativePath(item.RelativePath)).Concat(qwDeletions).ToArray();

            journal = new WorkspaceSaveJournal(
                transactionId,
                normalized.Version,
                WorkspaceSavePhase.Prepared,
                entries,
                allDeletions);
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

    /// <summary>Replays or rolls back any incomplete workspace-save transaction left under a project root.</summary>
    /// <remarks>
    /// Public entry point onto the store's own recovery so a caller that must read a consistent
    /// <c>project.json</c> before this store's normal load path would run recovery itself — such as
    /// <see cref="ProjectWorkspaceRepository"/>'s format-version pre-read, which runs before any conversion
    /// decision and therefore before <see cref="LoadProjectFromRootAsync"/> ever gets a chance to recover — can
    /// force it deterministically first. Recovery is idempotent: an interrupted transaction is fully replayed
    /// or rolled back and its directory deleted, so calling this again from the normal load path once nothing
    /// is pending is a no-op.
    ///
    /// Decisions: DEC-0049.
    /// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md §6.5.
    /// Tests: tests/ScadaBuilderV2.Tests/Formats/ProjectConversionEndToEndTests.cs.
    /// </remarks>
    public async Task RecoverPendingTransactionsAsync(string projectRoot, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        await RecoverIncompleteTransactionsAsync(Path.GetFullPath(projectRoot), cancellationToken);
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

        // Validate quick windows definitions
        var quickWindows = project.EffectiveQuickWindows;
        if (quickWindows.Count == 0 && project.EffectiveQuickWindowInvocations.Count > 0)
            throw new InvalidOperationException("QuickWindow invocations cannot be saved without authoritative definitions.");
        if (quickWindows.Count > 0)
        {
            var qwKeys = new HashSet<Guid>();
            var qwCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var qw in quickWindows)
            {
                if (qw.DefinitionKey == Guid.Empty)
                    throw new InvalidOperationException("QuickWindow DefinitionKey must be non-empty.");
                if (!qwKeys.Add(qw.DefinitionKey))
                    throw new InvalidOperationException($"Duplicate QuickWindow DefinitionKey '{qw.DefinitionKey}'.");
                if (!qwCodes.Add(qw.Code))
                    throw new InvalidOperationException($"Duplicate QuickWindow code '{qw.Code}'.");
                var codeValidation = PageCodePolicy.Validate(qw.Code);
                if (!codeValidation.IsValid)
                    throw new InvalidOperationException(codeValidation.Errors[0]);
                var relative = QuickWindowStore.GetRelativePath(qw.DefinitionKey);
                _ = QuickWindowStore.ResolveContainedQuickWindowPath(projectRoot, relative);
                var issues = ScadaBuilderV2.Domain.QuickWindows.QuickWindowValidation.ValidateDefinition(qw);
                if (issues.Count > 0)
                    throw new InvalidOperationException($"QuickWindow '{qw.Code}' invalid: {string.Join("; ", issues)}");
            }
            // Deterministic order
            var ordered = quickWindows.OrderBy(qw => qw.Code, StringComparer.Ordinal).ThenBy(qw => qw.DefinitionKey).ToArray();
            project = project with { QuickWindows = ordered };
            // Also validate invocations
            var invs = project.EffectiveQuickWindowInvocations;
            var invKeys = new HashSet<Guid>();
            var qwKeySet = qwKeys;
            foreach (var inv in invs)
            {
                if (inv.InvocationKey == Guid.Empty)
                    throw new InvalidOperationException("QuickWindow InvocationKey must be non-empty.");
                if (!invKeys.Add(inv.InvocationKey))
                    throw new InvalidOperationException($"Duplicate InvocationKey '{inv.InvocationKey}'.");
                if (!qwKeySet.Contains(inv.DefinitionKey))
                    throw new InvalidOperationException($"Invocation '{inv.InvocationKey}' references missing QuickWindow '{inv.DefinitionKey}'.");
                // Validate binding member keys exist
                var def = ordered.First(d => d.DefinitionKey == inv.DefinitionKey);
                var memberKeys = def.InterfaceMembers.Select(m => m.MemberKey).ToHashSet();
                foreach (var b in inv.Bindings)
                {
                    if (!memberKeys.Contains(b.MemberKey))
                        throw new InvalidOperationException($"Invocation '{inv.InvocationKey}' references unknown member '{b.MemberKey}'.");
                }
            }
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
            var stagedJson = await File.ReadAllTextAsync(stagedPath, cancellationToken);
            ThrowIfRetiredPopupCommandKind(stagedJson, $"staged {entry.TargetRelativePath}");
            await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(stagedJson));
            if (string.Equals(entry.TargetRelativePath, "project.json", StringComparison.OrdinalIgnoreCase))
            {
                var project = await JsonSerializer.DeserializeAsync<ScadaProject>(stream, JsonOptions, cancellationToken);
                if (project is null || project.Scenes.Count != expectedProject.Scenes.Count)
                {
                    throw new InvalidDataException("Staged project.json did not pass workspace validation.");
                }
            }
            else if (entry.TargetRelativePath.StartsWith("quick-windows/", StringComparison.OrdinalIgnoreCase))
            {
                var qw = await JsonSerializer.DeserializeAsync<ScadaBuilderV2.Domain.QuickWindows.QuickWindowDefinition>(stream, JsonOptions, cancellationToken);
                if (qw is null || qw.DefinitionKey == Guid.Empty)
                {
                    throw new InvalidDataException($"Staged quick-window '{entry.TargetRelativePath}' did not pass validation.");
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
            string path;
            try
            {
                path = ResolveContainedScenePath(projectRoot, relativePath);
            }
            catch
            {
                try
                {
                    path = QuickWindowStore.ResolveContainedQuickWindowPath(projectRoot, relativePath);
                }
                catch
                {
                    path = ResolveContainedPath(projectRoot, relativePath);
                }
            }
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

    /// <summary>Gets the tag import directory under an exact project root.</summary>
    public static string GetTagImportDirectoryFromProjectRoot(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        return Path.Combine(Path.GetFullPath(projectRoot), "imports", "tags");
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

    private static async Task<ScadaProject?> LoadProjectFileAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        await using var read = File.OpenRead(projectPath);
        return await JsonSerializer.DeserializeAsync<ScadaProject>(read, JsonOptions, cancellationToken);
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

    /// <summary>Throws if the json contains a retired popup command kind without migration.</summary>
    /// <remarks>DEC-0050: OpenPopup/TogglePopup/ClosePopup are retired modern command kinds. Any persisted occurrence must be diagnosed with project/scene/element/command location and refuse to save without overwriting the original bytes.</remarks>
    private static void ThrowIfRetiredPopupCommandKind(string json, string context)
    {
        if (string.IsNullOrWhiteSpace(json) || !json.Contains("CommandConfig", StringComparison.Ordinal))
            return;

        // Precise detection of retired ScadaCommandKind values inside CommandConfig.Commands.
        // Legacy ScadaActionKind values (MountFragment etc. inside Actions) are allowlisted and ignored.
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Elements", out var elements) && elements.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var retired = new HashSet<string>(StringComparer.Ordinal) { "OpenPopup", "TogglePopup", "ClosePopup" };
                void CheckElements(System.Text.Json.JsonElement arr)
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        if (el.TryGetProperty("CommandConfig", out var cmdCfg) && cmdCfg.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            if (cmdCfg.TryGetProperty("Commands", out var cmds) && cmds.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var cmd in cmds.EnumerateArray())
                                {
                                    if (cmd.TryGetProperty("Kind", out var kindProp) && kindProp.ValueKind == System.Text.Json.JsonValueKind.String)
                                    {
                                        var kindStr = kindProp.GetString();
                                        if (kindStr != null && retired.Contains(kindStr))
                                            throw new InvalidDataException($"Retired popup command kind \"{kindStr}\" detected in {context}. DEC-0050: ScadaCommandKind {kindStr} was never completed end-to-end and is refused fail-closed. File remains unchanged; no migration to QuickWindow is performed.");
                                    }
                                }
                            }
                        }
                        if (el.TryGetProperty("Children", out var children) && children.ValueKind == System.Text.Json.JsonValueKind.Array)
                            CheckElements(children);
                        if (el.TryGetProperty("ChildElements", out var child2) && child2.ValueKind == System.Text.Json.JsonValueKind.Array)
                            CheckElements(child2);
                    }
                }
                CheckElements(elements);
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // If not valid JSON or not a scene, fall back to simple string check only for CommandConfig
            if (json.Contains("\"CommandConfig\"", StringComparison.Ordinal) && (json.Contains("\"OpenPopup\"", StringComparison.Ordinal) || json.Contains("\"TogglePopup\"", StringComparison.Ordinal) || json.Contains("\"ClosePopup\"", StringComparison.Ordinal)))
            {
                // Check if it's likely a command (contains Commands)
                if (json.Contains("\"Commands\"", StringComparison.Ordinal))
                    throw new InvalidDataException($"Retired popup command kind detected in {context} (fallback). DEC-0050 refused fail-closed.");
            }
        }
    }
}
