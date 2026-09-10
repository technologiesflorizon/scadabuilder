using System.Text.Json;
using System.Text.Json.Nodes;
using ScadaBuilderV2.Application.Formats;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.Projects;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

/// <summary>Creates and opens project workspaces rooted at user-selected locations.</summary>
/// <remarks>
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-07-29-project-lifecycle-design.md,
/// docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C5, C6.
/// Tests: tests/ScadaBuilderV2.Tests/ProjectCreationIntegrationTests.cs, tests/ScadaBuilderV2.Tests/ProjectOpenIntegrationTests.cs,
/// tests/ScadaBuilderV2.Tests/Formats/BackwardRefusalTests.cs.
/// </remarks>
public sealed class ProjectWorkspaceRepository(
    ModernProjectStore store,
    ReferenceProjectCompatibilityLocator compatibilityLocator,
    ArtifactConverterRegistry registry,
    ConversionCoordinator conversions) : IProjectWorkspaceRepository
{
    /// <inheritdoc />
    public IReadOnlyList<ScadaBuildValidationIssue> ValidateCreation(CreateProjectRequest request)
    {
        var validation = ProjectWorkspacePathPolicy.ValidateCreation(request);
        return validation.ProjectRoot is null && validation.Diagnostics.Count == 0
            ? [new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Error,
                "project.create-target-unresolved",
                "Le chemin du projet n'a pas pu être résolu.")]
            : validation.Diagnostics;
    }

    /// <inheritdoc />
    public async Task<ProjectRepositoryResult> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ProjectWorkspacePathPolicy.ValidateCreation(request);
        if (validation.ProjectRoot is null || validation.Diagnostics.Count > 0)
        {
            return new ProjectRepositoryResult(null, validation.Diagnostics);
        }

        var projectRoot = validation.ProjectRoot;
        var parent = Path.GetDirectoryName(projectRoot)!;
        Directory.CreateDirectory(parent);
        var stagingRoot = Path.Combine(parent, $".{Path.GetFileName(projectRoot)}.create-{Guid.NewGuid():N}");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateProjectDirectories(stagingRoot);
            var pageKey = PageKeyFactory.CreateNew();
            var pageCode = request.InitialPageCode.Trim();
            var pageTitle = request.InitialPageTitle.Trim();
            var page = new ScadaSceneReference(
                pageCode,
                pageTitle,
                $"scenes/{pageKey:N}.scene.json",
                ScadaPageType.Default,
                request.CanvasSize,
                SceneBackgroundStyle.Default,
                IncludeInBuild: true,
                PageKey: pageKey,
                PageCode: pageCode,
                Origin: PageOrigin.Native);
            var project = new ScadaProject(
                request.Name.Trim(),
                Domain.Versioning.ScadaVersion.Initial,
                request.CanvasSize,
                request.ResponsiveMode,
                request.AuthoringMode,
                DefaultDevicePresets.All,
                [page],
                HomePageId: pageCode,
                HomePageKey: pageKey,
                // A brand-new project is authored directly in the current shape - it never needs converting,
                // so it is stamped at the current generation instead of being left at the implicit zero that
                // would otherwise send it straight back through the conversion gate on its very next open.
                FormatVersion: ScadaFormatGeneration.Project);
            var scene = ScadaScene.CreateEmpty(pageCode, pageTitle, request.CanvasSize) with
            {
                PageKey = pageKey,
                PageCode = pageCode,
                Origin = PageOrigin.Native,
                IncludeInBuild = true
            };
            var snapshot = new PageWorkspaceSnapshot(
                1,
                project,
                new Dictionary<Guid, ScadaScene> { [pageKey] = scene },
                []);
            await store.SaveWorkspaceSnapshotToProjectRootAsync(stagingRoot, snapshot, cancellationToken);
            _ = await store.ReadWorkspaceSnapshotFromProjectRootAsync(stagingRoot, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(projectRoot) || File.Exists(projectRoot))
            {
                return Failure("project.target-exists", "Le dossier projet a été créé par une autre opération.");
            }

            Directory.Move(stagingRoot, projectRoot);
            var location = new ProjectWorkspaceLocation(projectRoot, Path.Combine(projectRoot, "project.json"));
            return new ProjectRepositoryResult(
                new ProjectLoadCandidate(location, snapshot, WasMigrated: false, []),
                []);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Failure("project.create-failed", $"La création du projet a échoué : {exception.Message}");
        }
        finally
        {
            DeleteVerifiedStaging(parent, stagingRoot);
        }
    }

    /// <inheritdoc />
    public async Task<ProjectRepositoryResult> OpenAsync(
        string projectFilePath,
        CancellationToken cancellationToken = default)
    {
        var validation = ProjectWorkspacePathPolicy.ValidateOpenPath(projectFilePath);
        if (validation.Location is null)
        {
            return new ProjectRepositoryResult(null, validation.Diagnostics);
        }

        try
        {
            var declaredGeneration = ArtifactFormatVersionReader.ReadFormatVersion(
                File.Exists(validation.Location.ProjectFilePath)
                    ? File.ReadAllText(validation.Location.ProjectFilePath)
                    : null);
            if (declaredGeneration > ScadaFormatGeneration.Project)
            {
                // A binary that does not understand a file must never be able to rewrite it. Deserialising here
                // would drop the properties it does not know, and the first save would write them away.
                return new ProjectRepositoryResult(null, [new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "project.format-too-new",
                    $"Ce projet est au format {declaredGeneration}; cette version de SCADA Builder comprend le format "
                    + $"{ScadaFormatGeneration.Project}. Ouvrez-le avec une version plus récente : l'ouvrir ici "
                    + "risquerait d'en supprimer ce qu'elle ne sait pas lire.",
                    SuggestedFix: "Mettre SCADA Builder à jour.")]);
            }

            if (declaredGeneration < ScadaFormatGeneration.Project)
            {
                var outcome = await conversions.PrepareAsync(
                    [new ArtifactToConvert(
                        ArtifactModule.Project,
                        validation.Location.ProjectFilePath,
                        declaredGeneration,
                        ScadaFormatGeneration.Project)],
                    cancellationToken);

                if (!outcome.CanProceed)
                {
                    // C5: convert, or do not open. There is no path to a session on an unconverted artifact.
                    // Nothing above this point has touched disk (PrepareAsync resolves the chain from the
                    // declared integer generation; it never reads project.json), and recovery has not run
                    // yet either -- so a declined conversion, exactly like a too-new refusal, leaves the
                    // project's directory untouched. Spec §6.5: "Annuler" n'ouvre pas le projet, et aucun
                    // artefact n'est touché.
                    return new ProjectRepositoryResult(null, outcome.Diagnostics);
                }

                // Ruling 40 (fix round 2), corrected by Ruling 44 (fix round 3): recovery must run after the
                // CanProceed gate, not merely after the version pre-read. Round 2 moved the call below the
                // pre-read but still above PrepareAsync/CanProceed -- so on the path where the operator
                // *declines* the conversion, AcquireWorkspaceLockAsync had still created `.studio/` and
                // `workspace-save.lock`, and if a transaction was pending, RollbackTransaction had already
                // replaced project.json *before the operator answered*. C2 (a binary that does not understand
                // a file must never rewrite it) and spec §6.5 (a declined conversion touches nothing) both
                // require recovery to wait until the operator has actually consented. Recovery is only needed
                // when a conversion is genuinely about to be written, so it moves here, immediately before the
                // write loop -- C1 (an interrupted save transaction must not undo a consented conversion)
                // stays closed identically, and now both refusal paths (too-new above, declined here) leave
                // the directory untouched. Recovery is idempotent, so `ReadWorkspaceSnapshotFromProjectRootAsync`
                // further down recovering again once nothing is pending remains a no-op.
                await store.RecoverPendingTransactionsAsync(validation.Location.ProjectRoot, cancellationToken);

                string? lastBackupPath = null;
                try
                {
                    foreach (var entry in outcome.Plan.Entries)
                    {
                        // Ruling 38: parsed with a case-insensitive JsonObject so this indexer-based converter
                        // sees the same "FormatVersion" field ArtifactFormatVersionReader matched (it compares
                        // OrdinalIgnoreCase) to reach this branch at all, whatever casing the manifest actually
                        // used. Without this, a manifest carrying e.g. "formatversion" is read as generation 0
                        // by the reader but misses entirely on this converter's plain indexer, so the
                        // converter appends a second, differently-cased "FormatVersion" property -- and the
                        // reader, still matching the first one it finds, keeps reporting generation 0 forever,
                        // re-converting the project on every open.
                        var document = JsonNode.Parse(
                            await File.ReadAllTextAsync(entry.FilePath, cancellationToken),
                            nodeOptions: new JsonNodeOptions { PropertyNameCaseInsensitive = true })
                            ?? throw new InvalidDataException($"Document illisible: {entry.FilePath}");
                        var converted = registry
                            .ResolveChain(entry.Module, entry.FromVersion, entry.ToVersion)
                            .Apply(document);

                        // C6: the backup is the only way back, so it is written immediately before the file on
                        // disk is modified. Converting in memory modifies nothing on disk; if that step throws
                        // (e.g. a page with no identity to settle), no backup is left behind and the file is
                        // untouched, so a retry does not accumulate orphaned numbered backups.
                        lastBackupPath = ArtifactBackupWriter.CreateBackup(entry.FilePath);

                        // Ruling 35: staged beside the target and renamed into place rather than written
                        // directly onto the live file, matching the atomic staging + rename the design spec
                        // (§2.1) justifies the whole conversion approach on. A crash or power loss mid-write
                        // then leaves either the original file (untouched, because the write landed on the
                        // temp path) or the fully-written converted file -- never a half-written
                        // `project.json`. The temp file is cleaned up if anything throws before the rename.
                        var tempPath = entry.FilePath + $".converting-{Guid.NewGuid():N}.tmp";
                        try
                        {
                            await using (var tempStream = new FileStream(
                                tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, FileOptions.WriteThrough))
                            // Fix round 2: a plain System.Text.Encoding.UTF8 StreamWriter emits a UTF-8 BOM
                            // (EF BB BF); the File.WriteAllTextAsync this replaced did not, and no other JSON
                            // writer in this store does either (ModernProjectStore.SaveJsonAsync goes straight
                            // through JsonSerializer.SerializeAsync onto the raw stream). A BOM-prefixed
                            // project.json would be the one file in the project that silently disagreed with
                            // every other artifact's encoding.
                            await using (var writer = new StreamWriter(tempStream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                            {
                                await writer.WriteAsync(converted.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                                await writer.FlushAsync(cancellationToken);
                                tempStream.Flush(flushToDisk: true);
                            }

                            File.Move(tempPath, entry.FilePath, overwrite: true);
                        }
                        catch
                        {
                            // Fix round 2: the cleanup delete must never replace the real failure. A locked or
                            // already-vanished temp file would otherwise throw out of this catch and hide
                            // whatever actually went wrong with the conversion write.
                            try
                            {
                                if (File.Exists(tempPath))
                                {
                                    File.Delete(tempPath);
                                }
                            }
                            catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException)
                            {
                                // Best-effort only: the original exception below is the one that must surface.
                            }
                            throw;
                        }
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException)
                {
                    // Ruling 39: C7 says the backup is the only way back, so an operator told the conversion
                    // failed must also be told where to find it -- capturing the return of CreateBackup instead
                    // of discarding it, as the prior code did, is what makes this possible.
                    var backupHint = lastBackupPath is null
                        ? string.Empty
                        : $" Une sauvegarde de l'original se trouve à '{lastBackupPath}'.";
                    return Failure("project.open-failed", $"L’ouverture du projet a échoué : {exception.Message}{backupHint}");
                }
            }

            var snapshot = await store.ReadWorkspaceSnapshotFromProjectRootAsync(
                validation.Location.ProjectRoot,
                cancellationToken: cancellationToken);
            var diagnostics = ValidateSnapshot(snapshot);
            var sourceBase = compatibilityLocator.FindImportedSourceBase(validation.Location.ProjectRoot, snapshot.Project);
            foreach (var page in snapshot.Project.Scenes.Where(page => page.EffectiveOrigin == PageOrigin.Imported))
            {
                var sourcePath = page.ImportProvenance?.SourcePath;
                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    diagnostics.Add(Error("project.import-source-missing", $"La page importée '{page.EffectivePageCode}' ne définit aucune source."));
                    continue;
                }

                var resolved = ResolveImportedSource(validation.Location.ProjectRoot, sourceBase, sourcePath);
                if (resolved is null)
                {
                    diagnostics.Add(Error("project.import-source-unresolved", $"La source importée de la page '{page.EffectivePageCode}' est introuvable."));
                }
            }

            if (diagnostics.Any(issue => issue.Severity == ScadaBuildValidationSeverity.Error))
            {
                return new ProjectRepositoryResult(null, diagnostics);
            }

            var location = validation.Location with { ImportedSourceBaseRoot = sourceBase };
            return new ProjectRepositoryResult(
                new ProjectLoadCandidate(location, snapshot, WasMigrated: false, diagnostics),
                diagnostics);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException)
        {
            return Failure("project.open-failed", $"L’ouverture du projet a échoué : {exception.Message}");
        }
    }

    private static List<ScadaBuildValidationIssue> ValidateSnapshot(PageWorkspaceSnapshot snapshot)
    {
        var issues = ScadaProjectBuildValidator.Validate(snapshot.Project).ToList();
        if (snapshot.Project.Scenes.Count != snapshot.Scenes.Count)
        {
            issues.Add(Error("project.scene-inventory-mismatch", "L’inventaire des scènes ne correspond pas au manifeste du projet."));
        }
        return issues;
    }

    private static string? ResolveImportedSource(string projectRoot, string? sourceBase, string sourcePath)
    {
        foreach (var root in new[] { projectRoot, sourceBase }.Where(root => !string.IsNullOrWhiteSpace(root)).Cast<string>())
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var candidate = Path.IsPathRooted(sourcePath)
                ? Path.GetFullPath(sourcePath)
                : Path.GetFullPath(Path.Combine(fullRoot, sourcePath));
            if (candidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    private static void CreateProjectDirectories(string root)
    {
        Directory.CreateDirectory(root);
        foreach (var relative in new[]
        {
            "scenes", "assets", Path.Combine("library", "elements"), "libraries",
            Path.Combine("imports", "legacy"), Path.Combine("imports", "tags"),
            "exports", ".studio"
        })
        {
            Directory.CreateDirectory(Path.Combine(root, relative));
        }
    }

    private static ProjectRepositoryResult Failure(string code, string message) =>
        new(null, [Error(code, message)]);

    private static ScadaBuildValidationIssue Error(string code, string message) =>
        new(ScadaBuildValidationSeverity.Error, code, message);

    private static void DeleteVerifiedStaging(string parent, string stagingRoot)
    {
        if (!Directory.Exists(stagingRoot))
        {
            return;
        }

        var fullParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullStaging = Path.GetFullPath(stagingRoot);
        if (!fullStaging.StartsWith(fullParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullStaging).Contains(".create-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Project creation staging cleanup escaped its verified parent.");
        }
        Directory.Delete(fullStaging, recursive: true);
    }
}
