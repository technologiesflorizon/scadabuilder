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
            // Ruling 34: recovery must run before the version pre-read, not merely before the store's own
            // normal load path. `ReadWorkspaceSnapshotFromProjectRootAsync` below also recovers on its way in
            // (it always has), but by then a conversion may already have overwritten `project.json` in this
            // method. An incomplete workspace-save transaction left over from an earlier interrupted save
            // would then be rolled back onto that freshly-converted file, silently reverting the conversion
            // the operator just consented to and reactivating the session on unconverted data -- exactly what
            // spec §6.5 declares cannot exist. Recovery is idempotent, so running it here and again inside the
            // later read is safe; it is a no-op the second time once nothing is pending.
            await store.RecoverPendingTransactionsAsync(validation.Location.ProjectRoot, cancellationToken);

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
                    return new ProjectRepositoryResult(null, outcome.Diagnostics);
                }

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
                            await using (var writer = new StreamWriter(tempStream, System.Text.Encoding.UTF8))
                            {
                                await writer.WriteAsync(converted.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                                await writer.FlushAsync(cancellationToken);
                                tempStream.Flush(flushToDisk: true);
                            }

                            File.Move(tempPath, entry.FilePath, overwrite: true);
                        }
                        catch
                        {
                            if (File.Exists(tempPath))
                            {
                                File.Delete(tempPath);
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
