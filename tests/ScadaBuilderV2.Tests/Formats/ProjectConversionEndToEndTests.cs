using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ScadaBuilderV2.Application.Formats;
using ScadaBuilderV2.Application.Projects;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Infrastructure.ModernProjects;
using ScadaBuilderV2.Infrastructure.ModernProjects.Converters;
using ScadaBuilderV2.Infrastructure.ReferenceProjects;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Ruling 36: before this file, no test anywhere actually converted a generation-0 project on disk and opened
/// it. Every generation-0 fixture in <see cref="BackwardRefusalTests"/> used <c>DecliningConsent</c>; every
/// <c>AcceptingConsent</c> test wrote a current-or-newer generation. The parse -> <c>Apply</c> ->
/// <c>CreateBackup</c> -> write path -- the branch's only irreversible operation -- never ran end to end in
/// the suite. These tests build a genuinely valid generation-0 project (via <c>CreateAsync</c>, which stamps
/// the current generation, then hand-downgraded to zero) so the fixture is what a real project looks like, not
/// a synthetic shape that would fail closed for an unrelated reason.
/// </summary>
[TestClass]
public sealed class ProjectConversionEndToEndTests
{
    private string root = "";

    [TestInitialize]
    public void CreateWorkspace()
    {
        root = Path.Combine(Path.GetTempPath(), "scada-format-conversion-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void RemoveWorkspace()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    /// <summary>
    /// The consented conversion actually runs: the file is genuinely at generation 0 with real scenes, the
    /// open succeeds, the file on disk now carries the current generation, the original bytes survive under
    /// <c>.bak</c>, and a second open is a pure read -- no consent prompt, no rewrite. This is the end-to-end
    /// path fixes 4-6 (page-code fallback, case-insensitive read, backup-path reporting) all sit on; if it did
    /// not exist none of those fixes would be provably exercised by anything.
    /// </summary>
    [TestMethod]
    public async Task AGenerationZeroProjectConvertsOpensAndStaysStableOnASecondOpen()
    {
        var projectPath = await CreateGenerationZeroProject();
        var generationZeroBytes = await File.ReadAllTextAsync(projectPath);
        Assert.AreEqual(0, ArtifactFormatVersionReader.ReadFormatVersion(generationZeroBytes),
            "test setup must actually produce a generation-0 file, or nothing below exercises conversion.");

        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator(),
            CreateRegistry(),
            new ConversionCoordinator(CreateRegistry(), new AcceptingConsent()));

        var firstOpen = await repository.OpenAsync(projectPath);

        Assert.IsTrue(firstOpen.IsSuccess, string.Join(Environment.NewLine, firstOpen.Diagnostics.Select(i => i.Message)));
        Assert.AreEqual(1, firstOpen.Candidate!.Snapshot.Project.Scenes.Count,
            "positive anchor: the opened project actually carries the real page this test wrote, not an empty shell.");
        Assert.AreEqual("win00001", firstOpen.Candidate.Snapshot.Project.Scenes[0].EffectivePageCode);

        var convertedBytes = await File.ReadAllTextAsync(projectPath);
        Assert.AreEqual(
            ScadaFormatGeneration.Project,
            ArtifactFormatVersionReader.ReadFormatVersion(convertedBytes),
            "the file on disk must now declare the current generation, not the generation it arrived at.");

        // Fix round 2: File.ReadAllText[Async] silently strips a UTF-8 BOM, so nothing that reads the file
        // back through it (including every other assertion in this test) can see one. Reading the raw bytes is
        // the only way to catch a converted file gaining a BOM none of the store's other JSON writers emit.
        var rawConvertedBytes = await File.ReadAllBytesAsync(projectPath);
        CollectionAssert.AreNotEqual(
            new byte[] { 0xEF, 0xBB, 0xBF },
            rawConvertedBytes.Take(3).ToArray(),
            "a converted project.json must not gain a UTF-8 BOM: no other JSON writer in this store emits one.");
        Assert.AreEqual('{', (char)rawConvertedBytes[0], "positive anchor: the file starts with the JSON object itself, not a byte-order mark.");

        var backupPath = projectPath + ".bak";
        Assert.IsTrue(File.Exists(backupPath), "C7: the backup is the only way back.");
        Assert.AreEqual(generationZeroBytes, await File.ReadAllTextAsync(backupPath),
            "the backup must hold the original, pre-conversion bytes exactly.");

        // A second open must not prompt for consent (ThrowingConsent fails the test if it is asked) and must
        // not rewrite a file that is already at the current generation.
        var secondRepository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator(),
            CreateRegistry(),
            new ConversionCoordinator(CreateRegistry(), new ThrowingConsent()));

        var secondOpen = await secondRepository.OpenAsync(projectPath);

        Assert.IsTrue(secondOpen.IsSuccess, string.Join(Environment.NewLine, secondOpen.Diagnostics.Select(i => i.Message)));
        Assert.AreEqual(convertedBytes, await File.ReadAllTextAsync(projectPath),
            "a second open of an already-current file must not rewrite it.");
        Assert.IsFalse(File.Exists(projectPath + ".bak.1"),
            "no second backup may appear: nothing was converted on the second open.");
    }

    /// <summary>
    /// Ruling 34: proves the fix for the branch's most serious finding. A workspace-save transaction left
    /// pending (as an interrupted save would leave one) must not be rolled back onto a conversion the operator
    /// just consented to. Before the fix, <c>RecoverIncompleteTransactionsAsync</c> only ran from
    /// <c>ModernProjectStore.LoadProjectFromRootAsync</c>, which <c>ProjectWorkspaceRepository.OpenAsync</c>
    /// reaches only *after* it has already converted and overwritten <c>project.json</c> in place; the pending
    /// transaction's backup (captured while the file was still generation 0) would then be moved back over the
    /// freshly-converted file, silently reverting it and reactivating the session on unconverted data -- the
    /// exact path spec §6.5 declares cannot exist. This test fails before the fix (the file ends back at
    /// generation 0) and passes after it (recovery consumes the pending transaction before the version
    /// pre-read, so it can no longer interfere with the conversion write that follows).
    /// </summary>
    [TestMethod]
    public async Task APendingSaveTransactionDoesNotUndoAConsentedConversion()
    {
        var projectPath = await CreateGenerationZeroProject();
        var generationZeroBytes = await File.ReadAllTextAsync(projectPath);
        var projectRoot = Path.GetDirectoryName(projectPath)!;

        PlacePendingSaveTransaction(projectRoot, generationZeroBytes);

        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator(),
            CreateRegistry(),
            new ConversionCoordinator(CreateRegistry(), new AcceptingConsent()));

        var result = await repository.OpenAsync(projectPath);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(i => i.Message)));

        var finalBytes = await File.ReadAllTextAsync(projectPath);
        Assert.AreEqual(
            ScadaFormatGeneration.Project,
            ArtifactFormatVersionReader.ReadFormatVersion(finalBytes),
            "the pending transaction's stale backup must not win over the conversion the operator just "
            + "consented to: the file must end at the current generation, not be silently rolled back to zero.");
        Assert.AreEqual(1, result.Candidate!.Snapshot.Project.Scenes.Count,
            "positive anchor: the session that activates is the real, converted project -- not a fallback.");

        var transactionsRoot = Path.Combine(projectRoot, ".studio", "transactions");
        Assert.IsFalse(Directory.Exists(transactionsRoot) && Directory.GetDirectories(transactionsRoot).Length > 0,
            "the pending transaction must have been consumed, not left to fire again on the next open.");
    }

    /// <summary>
    /// Ruling 38: a manifest that carries the field under a differently-cased key still declares generation
    /// zero to <see cref="ArtifactFormatVersionReader"/> (it matches case-insensitively already), so it must
    /// still convert -- and, critically, convert to a single, correctly-cased <c>FormatVersion</c> property
    /// rather than appending a second one beside the original. The positive anchor is the second open: if the
    /// converter's write missed the lowercase key, a second .bak would appear because the project would
    /// re-convert on every open.
    /// </summary>
    [TestMethod]
    public async Task ALowercaseFormatVersionKeyStillConvertsAndDoesNotReconvertOnASecondOpen()
    {
        var projectPath = await CreateGenerationZeroProject();
        var node = JsonNode.Parse(await File.ReadAllTextAsync(projectPath))!.AsObject();
        node.Remove("FormatVersion");
        node["formatversion"] = 0;
        await File.WriteAllTextAsync(projectPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator(),
            CreateRegistry(),
            new ConversionCoordinator(CreateRegistry(), new AcceptingConsent()));

        var firstOpen = await repository.OpenAsync(projectPath);
        Assert.IsTrue(firstOpen.IsSuccess, string.Join(Environment.NewLine, firstOpen.Diagnostics.Select(i => i.Message)));

        var convertedNode = JsonNode.Parse(await File.ReadAllTextAsync(projectPath))!.AsObject();
        var formatVersionProperties = convertedNode
            .Where(kvp => string.Equals(kvp.Key, ArtifactFormatVersionReader.FieldName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.AreEqual(1, formatVersionProperties.Length,
            "exactly one FormatVersion-shaped property must survive conversion, not the original plus a new one.");
        Assert.AreEqual(ScadaFormatGeneration.Project, formatVersionProperties[0].Value!.GetValue<int>());

        var convertedBytes = await File.ReadAllTextAsync(projectPath);
        var secondRepository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator(),
            CreateRegistry(),
            new ConversionCoordinator(CreateRegistry(), new ThrowingConsent()));

        var secondOpen = await secondRepository.OpenAsync(projectPath);

        Assert.IsTrue(secondOpen.IsSuccess, string.Join(Environment.NewLine, secondOpen.Diagnostics.Select(i => i.Message)));
        Assert.AreEqual(convertedBytes, await File.ReadAllTextAsync(projectPath),
            "an already-converted project must not be rewritten again on the next open.");
        Assert.IsFalse(File.Exists(projectPath + ".bak.1"),
            "unbounded .bak.N accumulation is exactly the symptom of a converter that keeps missing the field.");
    }

    /// <summary>
    /// Fix round 2 / item 3: the C3 residue. All three tests above build fixtures via <c>CreateAsync</c>, so
    /// every scene already carries a <c>PageKey</c> and the converter's key-settling branch is <c>continue</c>d
    /// every time -- the branch that actually writes a permanent page identity to disk was covered only at the
    /// <c>JsonNode</c> unit level in <c>ProjectGeneration1ConverterTests</c>, never through the real open path.
    /// This test strips <c>PageKey</c> from the scene (and the now-dangling <c>HomePageKey</c>, so the missing
    /// key is the only unsettled identity in the fixture) before downgrading, so the converter's derivation
    /// branch is the one that actually runs on this open.
    /// </summary>
    [TestMethod]
    public async Task AProjectWithNoPageKeyGetsTheDeterministicKeySettledAndItSurvivesASaveAndReopen()
    {
        var projectPath = await CreateGenerationZeroProject();
        var node = JsonNode.Parse(await File.ReadAllTextAsync(projectPath))!.AsObject();
        var scene = node["Scenes"]!.AsArray()[0]!.AsObject();
        scene.Remove("PageKey");
        node.Remove("HomePageKey");
        await File.WriteAllTextAsync(projectPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var store = new ModernProjectStore();
        var repository = new ProjectWorkspaceRepository(
            store,
            new ReferenceProjectCompatibilityLocator(),
            CreateRegistry(),
            new ConversionCoordinator(CreateRegistry(), new AcceptingConsent()));

        var opened = await repository.OpenAsync(projectPath);
        Assert.IsTrue(opened.IsSuccess, string.Join(Environment.NewLine, opened.Diagnostics.Select(i => i.Message)));

        var page = opened.Candidate!.Snapshot.Project.Scenes.Single();
        var expectedKey = PageKeyFactory.CreateDeterministic("Projet conversion e2e", "win00001");
        Assert.AreEqual(expectedKey, page.PageKey,
            "the settled key must be the same deterministic derivation PageKeyFactory produces everywhere else in the product, not an arbitrary new Guid.");
        Assert.AreNotEqual(Guid.Empty, page.PageKey, "positive anchor: a real, non-empty key was actually settled.");

        // Ruling 47 (fix round 3): the two assertions above do not discriminate the converter from the
        // in-memory normalizer it exists to make unnecessary. If the converter wrote nothing at all, PageKey
        // would deserialize as Guid.Empty, and ModernProjectMigration.NormalizeIdentity -- exercised on every
        // load, per Ruling 16 -- would derive the identical CreateDeterministic(name, code) key in memory
        // before the snapshot is ever returned. Both assertions above would pass regardless of whether the
        // converter ran. The converter's whole stated justification (ProjectGeneration1Converter's own
        // remarks) is settling identity *in the file*, so that is what must be checked: read project.json off
        // disk directly, after this first open and before the save/reopen below, and assert the key the
        // converter wrote is the settled one.
        var convertedNode = JsonNode.Parse(await File.ReadAllTextAsync(projectPath))!.AsObject();
        var onDiskKey = convertedNode["Scenes"]!.AsArray()[0]!["PageKey"]!.GetValue<string>();
        Assert.AreEqual(expectedKey.ToString("D"), onDiskKey,
            "the converter must have settled the key in the file itself, not only in the loaded snapshot -- the normalizer alone would leave PageKey absent from disk.");

        // Survives a save and reopen: the settled identity must not be an artifact of the open-time snapshot
        // alone -- it has to be what gets persisted and read back, exactly as `MigrateProject` would treat any
        // other already-keyed page.
        var projectRoot = Path.GetDirectoryName(projectPath)!;
        await store.SaveWorkspaceSnapshotToProjectRootAsync(projectRoot, opened.Candidate.Snapshot);

        var reopened = await repository.OpenAsync(projectPath);
        Assert.IsTrue(reopened.IsSuccess, string.Join(Environment.NewLine, reopened.Diagnostics.Select(i => i.Message)));
        Assert.AreEqual(expectedKey, reopened.Candidate!.Snapshot.Project.Scenes.Single().PageKey,
            "the settled key must survive a save and a fresh reopen unchanged.");
    }

    /// <summary>Creates a genuinely valid project via the real creation path, then downgrades it to generation zero on disk.</summary>
    private async Task<string> CreateGenerationZeroProject()
    {
        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator(),
            CreateRegistry(),
            new ConversionCoordinator(CreateRegistry(), new AcceptingConsent()));

        var created = await repository.CreateAsync(new CreateProjectRequest(
            "Projet conversion e2e",
            root,
            "ProjetConversionE2e",
            "win00001",
            "Page principale",
            CanvasSize.DefaultDesktop,
            ResponsiveMode.Fixed,
            AuthoringMode.DesktopFirst));
        Assert.IsTrue(created.IsSuccess, string.Join(Environment.NewLine, created.Diagnostics.Select(i => i.Message)));
        var projectPath = created.Candidate!.Location.ProjectFilePath;

        var node = JsonNode.Parse(await File.ReadAllTextAsync(projectPath))!.AsObject();
        node["FormatVersion"] = 0;
        await File.WriteAllTextAsync(projectPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        return projectPath;
    }

    /// <summary>
    /// Hand-builds an incomplete workspace-save transaction exactly as <c>ModernProjectStore</c>'s internal
    /// journal shape requires -- <c>WorkspaceSaveJournal</c> is internal to Infrastructure, so this writes the
    /// same JSON shape by hand rather than reaching for the type. Phase is left below <c>ProjectCommitted</c>
    /// (<c>Prepared</c>), and the write entry's backup file carries the pre-conversion bytes, exactly what an
    /// interrupted save that captured a backup before crashing would leave behind.
    /// </summary>
    private static void PlacePendingSaveTransaction(string projectRoot, string backupContent)
    {
        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = Path.Combine(projectRoot, ".studio", "transactions", transactionId);
        Directory.CreateDirectory(Path.Combine(transactionRoot, "backup"));
        File.WriteAllText(Path.Combine(transactionRoot, "backup", "project.json"), backupContent);

        var journal = $$"""
        {
          "TransactionId": "{{transactionId}}",
          "SnapshotVersion": 1,
          "Phase": "Prepared",
          "Writes": [
            { "TargetRelativePath": "project.json", "StagedRelativePath": "new/project.json", "BackupRelativePath": "backup/project.json", "TargetExisted": true }
          ],
          "Deletions": []
        }
        """;
        File.WriteAllText(Path.Combine(transactionRoot, "journal.json"), journal);
    }

    private static ArtifactConverterRegistry CreateRegistry()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new ProjectGeneration1Converter());
        return registry;
    }

    /// <summary>Always convert. Used where consent must be asked and granted.</summary>
    private sealed class AcceptingConsent : IConversionConsent
    {
        public Task<ConversionDecision> RequestAsync(ConversionPlan plan, CancellationToken cancellationToken)
            => Task.FromResult(ConversionDecision.Convert);
    }

    /// <summary>Fails the test if asked. Used to prove a second open on an already-current file never prompts.</summary>
    private sealed class ThrowingConsent : IConversionConsent
    {
        public Task<ConversionDecision> RequestAsync(ConversionPlan plan, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Conversion consent must not be requested for a project already at the current generation.");
    }
}
