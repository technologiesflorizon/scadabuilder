# Versionnement de format et convertisseurs — Plan d'implémentation

Date: 2026-09-08
Status: Plan actif — huit tâches, aucune démarrée
Document version: `V2.1.6.0011`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-09-08 | `V2.1.6.0011` | `PENDING` | Création : huit tâches, du refus vers l'arrière livré seul jusqu'au retrait du second rôle de `ManifestVersion`. |

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Donner à chaque artefact persisté une version de format détectable, refuser d'ouvrir ce qu'un binaire ne comprend pas, et remplacer le reniflage de formes par des convertisseurs explicites que l'opérateur accepte et dont la sauvegarde précède l'écriture.

**Architecture:** Un entier de génération par module, lu du JSON brut avant toute désérialisation. Un registre de convertisseurs chaînés, chacun une fonction pure sur l'arbre désérialisé. Un coordinateur qui refuse vers l'arrière, calcule un plan, obtient le consentement, sauvegarde puis écrit. Le WPF ne possède que le dialogue.

**Tech Stack:** .NET 8, C# 12, System.Text.Json, MSTest, WPF.

**Spec:** `docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md`

**Contexte amont :** `docs/superpowers/specs/2026-09-08-interactive-icons-pre-spec.md` cadre les chantiers A et B, qui consomment ce mécanisme. Aucune tâche de ce plan ne les implémente.

## Global Constraints

- **Branche :** `studio-element-plus-drawing`. Le worktree doit être propre avant de commencer.
- **Aucune régression d'octets.** Les fixtures gelées, les paquets de conformance et les preuves industrielles ne sont pas régénérés par ce chantier. Un `FormatVersion` n'est **jamais sérialisé quand il est null** : utiliser `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` sur un `int?`, jamais un `int` non nullable.
- **`JsonOptions` de `ModernProjectStore` :** `PropertyNameCaseInsensitive = true`, `WriteIndented = true`, `Converters = { new JsonStringEnumConverter() }`. Toute lecture brute doit être insensible à la casse pour rester cohérente.
- **L'absence du champ vaut génération 0.**
- **Politique de version :** bumps `iteration` pendant tout ce chantier. Bump `feature` seulement si une capacité livrable est activée, ce qui n'arrive pas ici. `VERSION` et une ligne de changelog dans le document propriétaire **et** `docs/README.md` au même changement.
- **`verify-docs` doit rester à `Errors: 0` et 121 avertissements.** Un avertissement de plus est une régression à corriger, pas à accepter.
- **Après chaque tâche :** `dotnet test ScadaBuilderV2.sln --no-restore` complet avant le commit. La baseline d'entrée est **912/912 sans skip**.
- **Langue :** code, identifiants et XML docs en anglais; lignes de changelog et texte de décision en français.
- **XML docs obligatoires** sur toute API publique. Le code sensible au contrat cite `Decisions:`, `Contracts:` et `Tests:` dans ses `<remarks>`.

---

### Task 1: Refus vers l'arrière — la sécurité avant tout le reste

Cette tâche est livrable seule et ferme la perte de données décrite au §1.1 de la spec : un binaire antérieur ouvre aujourd'hui un projet contenant des Fenêtres rapides, ignore `QuickWindows` à la désérialisation, et les réécrit absentes à la première sauvegarde. Elle ne dépend d'aucun convertisseur.

**Files:**
- Create: `src/ScadaBuilderV2.Domain/Projects/ScadaFormatGeneration.cs`
- Create: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ArtifactFormatVersionReader.cs`
- Modify: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ProjectWorkspaceRepository.cs` (dans `OpenAsync`, juste après `ValidateOpenPath`)
- Test: `tests/ScadaBuilderV2.Tests/Formats/ArtifactFormatVersionReaderTests.cs`
- Test: `tests/ScadaBuilderV2.Tests/Formats/BackwardRefusalTests.cs`

**Interfaces:**
- Produces: `ScadaFormatGeneration.Project`, `.Scene`, `.TagCatalog`, `.Component` (constantes `int`); `ArtifactFormatVersionReader.ReadFormatVersion(string json)` → `int`; le code de diagnostic `project.format-too-new`.
- Consumes: rien.

- [ ] **Step 1: Écrire le test du lecteur de version**

`tests/ScadaBuilderV2.Tests/Formats/ArtifactFormatVersionReaderTests.cs`

```csharp
using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Locks the raw pre-read of a format version. It must answer without deserialising into a model the
/// binary may not understand, which is the whole point: refusing requires reading a file we cannot parse.
/// </summary>
[TestClass]
public sealed class ArtifactFormatVersionReaderTests
{
    [TestMethod]
    public void AnAbsentFieldIsGenerationZero()
    {
        Assert.AreEqual(0, ArtifactFormatVersionReader.ReadFormatVersion("""{"Name":"Projet"}"""));
    }

    [TestMethod]
    public void ThePresentFieldIsReturned()
    {
        Assert.AreEqual(3, ArtifactFormatVersionReader.ReadFormatVersion("""{"Name":"P","FormatVersion":3}"""));
    }

    [TestMethod]
    public void TheFieldIsReadCaseInsensitivelyLikeTheSerializer()
    {
        Assert.AreEqual(2, ArtifactFormatVersionReader.ReadFormatVersion("""{"formatversion":2}"""));
    }

    /// <summary>A file we cannot parse is generation zero, not a crash: the open pipeline reports it later.</summary>
    [TestMethod]
    public void MalformedJsonIsGenerationZeroRatherThanAnException()
    {
        Assert.AreEqual(0, ArtifactFormatVersionReader.ReadFormatVersion("{ not json"));
        Assert.AreEqual(0, ArtifactFormatVersionReader.ReadFormatVersion(""));
    }

    [TestMethod]
    public void ANonIntegerFieldIsGenerationZero()
    {
        Assert.AreEqual(0, ArtifactFormatVersionReader.ReadFormatVersion("""{"FormatVersion":"trois"}"""));
        Assert.AreEqual(0, ArtifactFormatVersionReader.ReadFormatVersion("""{"FormatVersion":null}"""));
    }

    [TestMethod]
    public void ARootThatIsNotAnObjectIsGenerationZero()
    {
        Assert.AreEqual(0, ArtifactFormatVersionReader.ReadFormatVersion("[1,2,3]"));
    }
}
```

- [ ] **Step 2: Exécuter le test et vérifier qu'il échoue**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ArtifactFormatVersionReaderTests"`
Expected: échec de compilation, `ArtifactFormatVersionReader` n'existe pas.

- [ ] **Step 3: Écrire les constantes de génération**

`src/ScadaBuilderV2.Domain/Projects/ScadaFormatGeneration.cs`

```csharp
namespace ScadaBuilderV2.Domain.Projects;

/// <summary>Current persisted format generation of each module.</summary>
/// <remarks>
/// A generation is a plain incrementing integer. It is deliberately unrelated to <see cref="ScadaVersion"/>,
/// which is the product version that authored a file, and to `ManifestVersion`, which is the export profile
/// negotiated with TF100Web. A format number describes one thing only: the shape of the data.
///
/// The absence of the field in a file means generation zero, so every artifact written before this mechanism
/// existed is generation zero without being rewritten.
///
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C1.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/BackwardRefusalTests.cs.
/// </remarks>
public static class ScadaFormatGeneration
{
    /// <summary>Generation of `project.json` understood by this binary.</summary>
    public const int Project = 0;

    /// <summary>Generation of a persisted scene understood by this binary.</summary>
    public const int Scene = 0;

    /// <summary>Generation of the persisted tag catalog understood by this binary.</summary>
    public const int TagCatalog = 0;

    /// <summary>Generation of a `.sep` component understood by this binary.</summary>
    /// <remarks>
    /// The component module already shipped a version of its own before this mechanism existed:
    /// `ElementStudioComponentMetadata.SchemaVersion`. That field is the component's format version and is
    /// used as-is. Adding a second number to the same file would create two truths about it.
    /// </remarks>
    public const int Component = 1;
}
```

- [ ] **Step 4: Écrire le lecteur brut**

`src/ScadaBuilderV2.Infrastructure/ModernProjects/ArtifactFormatVersionReader.cs`

```csharp
using System.Text.Json;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

/// <summary>Reads a persisted artifact's format generation without deserialising it.</summary>
/// <remarks>
/// Refusing a file the binary does not understand requires reading it first, and deserialising into the
/// current model is exactly what must not happen: unknown properties would be dropped silently and written
/// away on the next save. This reads the one field it needs from the raw document and nothing else.
///
/// Case-insensitivity mirrors `ModernProjectStore.JsonOptions.PropertyNameCaseInsensitive`, so the reader and
/// the serializer never disagree about which field they are looking at.
///
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C1, C2.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ArtifactFormatVersionReaderTests.cs.
/// </remarks>
public static class ArtifactFormatVersionReader
{
    /// <summary>Name of the field carrying the generation in every module but the component.</summary>
    public const string FieldName = "FormatVersion";

    /// <summary>Returns the declared generation, or zero when the field is absent or unusable.</summary>
    /// <remarks>
    /// Malformed input answers zero rather than throwing. A file that cannot be parsed is not a version
    /// problem, and the open pipeline already reports it with its own diagnostic further down.
    /// </remarks>
    public static int ReadFormatVersion(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return 0;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, FieldName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                return property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetInt32(out var generation)
                        ? generation
                        : 0;
            }
        }
        catch (JsonException)
        {
            return 0;
        }

        return 0;
    }
}
```

- [ ] **Step 5: Exécuter le test et vérifier qu'il passe**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ArtifactFormatVersionReaderTests"`
Expected: PASS, 6 tests.

- [ ] **Step 6: Écrire le test du refus, dont la régression de perte de données**

`tests/ScadaBuilderV2.Tests/Formats/BackwardRefusalTests.cs`

```csharp
using System.IO;
using System.Text.Json;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Infrastructure.ModernProjects;
using ScadaBuilderV2.Infrastructure.ReferenceProjects;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Locks the refusal a newer artifact receives from an older binary. DEC-0049 D5 already required it; until
/// this gate existed the pipeline refused only an invalid path and a failed open.
/// </summary>
[TestClass]
public sealed class BackwardRefusalTests
{
    private string root = "";

    [TestInitialize]
    public void CreateWorkspace()
    {
        root = Path.Combine(Path.GetTempPath(), "scada-format-refusal", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void RemoveWorkspace()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public async Task AProjectNewerThanTheBinaryIsRefusedBeforeActivation()
    {
        var projectPath = WriteProject(ScadaFormatGeneration.Project + 1);
        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator());

        var result = await repository.OpenAsync(projectPath);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Candidate, "no candidate may be prepared from a file we cannot read");
        var issue = result.Diagnostics.Single(entry => entry.Code == "project.format-too-new");
        StringAssert.Contains(issue.Message, (ScadaFormatGeneration.Project + 1).ToString());
        StringAssert.Contains(issue.Message, ScadaFormatGeneration.Project.ToString());
    }

    [TestMethod]
    public async Task AProjectAtTheCurrentGenerationIsNotRefusedByThisGate()
    {
        var projectPath = WriteProject(ScadaFormatGeneration.Project);
        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator());

        var result = await repository.OpenAsync(projectPath);

        Assert.IsFalse(
            result.Diagnostics.Any(entry => entry.Code == "project.format-too-new"),
            "the version gate must not fire on a file this binary understands.");
    }

    /// <summary>
    /// The data loss this gate exists to close: an older binary silently dropped quick windows.
    /// </summary>
    /// <remarks>
    /// `ScadaProject.QuickWindows` and `QuickWindowInvocations` are nullable properties added in Phase 1 of
    /// DEC-0050. A binary that predates them ignores the unknown properties on deserialisation and writes
    /// them away on the first save - definitions, local interfaces and every invocation, with no trace. The
    /// refusal is what makes that impossible: a binary that does not understand a file cannot rewrite it.
    /// </remarks>
    [TestMethod]
    public async Task AProjectCarryingUnknownContentIsRefusedRatherThanSilentlyRewritten()
    {
        var projectPath = WriteProject(
            ScadaFormatGeneration.Project + 1,
            extraJson: """"QuickWindowInvocations":[{"InvocationKey":"11111111-1111-1111-1111-111111111111"}],""");
        var before = await File.ReadAllTextAsync(projectPath);

        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator());
        var result = await repository.OpenAsync(projectPath);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(
            before,
            await File.ReadAllTextAsync(projectPath),
            "a refused project must not be touched, let alone rewritten without what it carried.");
    }

    private string WriteProject(int formatVersion, string extraJson = "")
    {
        var projectPath = Path.Combine(root, "project.json");
        var json = $$"""
        {
          "FormatVersion": {{formatVersion}},
          {{extraJson}}
          "Name": "Projet test",
          "Version": { "Production": 2, "Feature": 1, "Iteration": 6 },
          "CanvasSize": { "Width": 1920, "Height": 1080 },
          "ResponsiveMode": "Fixed",
          "AuthoringMode": "DesktopFirst",
          "DevicePresets": [],
          "Scenes": [],
          "ManifestVersion": "2.3"
        }
        """;
        File.WriteAllText(projectPath, json);
        return projectPath;
    }
}
```

- [ ] **Step 7: Exécuter le test et vérifier qu'il échoue**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~BackwardRefusalTests"`
Expected: FAIL — aucun diagnostic `project.format-too-new` n'est produit.

- [ ] **Step 8: Insérer le refus dans `OpenAsync`**

Dans `src/ScadaBuilderV2.Infrastructure/ModernProjects/ProjectWorkspaceRepository.cs`, immédiatement après le bloc `ValidateOpenPath` et **avant** le `try` qui lit le snapshot :

```csharp
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
```

Ajouter `using ScadaBuilderV2.Domain.Projects;` si absent.

- [ ] **Step 9: Exécuter le test et vérifier qu'il passe**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~BackwardRefusalTests"`
Expected: PASS, 3 tests.

- [ ] **Step 10: Exécuter la suite complète**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 921/921, aucun ignoré. Aucun test existant ne doit changer de résultat.

- [ ] **Step 11: Documenter et versionner**

Bump `VERSION` avec `python C:\Users\mathi\.codex\skills\scada-builder-v2-versioning\scripts\bump_scada_v2_version.py <courante> iteration`.

Ajouter une ligne de changelog dans `docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md` et `docs/README.md`, et une section décrivant le refus dans le contrat de modèle projet. Mettre à jour l'entrée `DEC-0049` du registre : D5 est désormais implémentée.

Run: `powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1`
Expected: `Errors: 0`, 121 avertissements.

- [ ] **Step 12: Commit**

```bash
git add -A
git commit -m "feat: refuse a project newer than the binary understands

DEC-0049 D5 required this and the pipeline never implemented it: OpenAsync
refused only an invalid path and a failed open.

Without it an older binary opens a project carrying QuickWindows, drops the
properties it does not know on deserialisation, and writes them away on the
first save - definitions, local interfaces and every invocation, with no
message and no trace. It cannot do better: with no format version it has no
way to know it is reading something newer than itself.

The generation is read from the raw document, never by deserialising into a
model the binary may not understand.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: `FormatVersion` sur le projet, la scène et le catalogue de tags

Le `.sep` est délibérément absent : il porte déjà `ElementStudioComponentMetadata.SchemaVersion`, raccordé en Task 4.

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs` (record `ScadaProject`, record `ScadaTagCatalog`)
- Modify: `src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs` (record `ScadaScene`)
- Test: `tests/ScadaBuilderV2.Tests/Formats/FormatVersionSerializationTests.cs`

**Interfaces:**
- Consumes: `ScadaFormatGeneration` (Task 1).
- Produces: `ScadaProject.FormatVersion` (`int?`), `ScadaProject.EffectiveFormatVersion` (`int`), et les paires équivalentes sur `ScadaScene` et `ScadaTagCatalog`.

- [ ] **Step 1: Écrire le test de sérialisation**

`tests/ScadaBuilderV2.Tests/Formats/FormatVersionSerializationTests.cs`

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Locks the one property of the new field that matters for existing evidence: it is invisible until it is
/// set. Frozen fixtures, conformance packages and industrial proofs depend on byte-stable artifacts, and a
/// field written as `null` would change every one of them.
/// </summary>
[TestClass]
public sealed class FormatVersionSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [TestMethod]
    public void AnUnsetFormatVersionIsNotSerialised()
    {
        var json = JsonSerializer.Serialize(ScadaProject.CreateDefault("Projet"), Options);

        Assert.IsFalse(
            json.Contains("FormatVersion", StringComparison.OrdinalIgnoreCase),
            "an unset generation must leave the document byte-identical to what it was before this field existed.");
    }

    [TestMethod]
    public void ASetFormatVersionIsSerialisedAndReadBack()
    {
        var project = ScadaProject.CreateDefault("Projet") with { FormatVersion = 1 };

        var json = JsonSerializer.Serialize(project, Options);
        var round = JsonSerializer.Deserialize<ScadaProject>(json, Options);

        StringAssert.Contains(json, "\"FormatVersion\": 1");
        Assert.AreEqual(1, round!.FormatVersion);
    }

    [TestMethod]
    public void AnAbsentFormatVersionReadsBackAsGenerationZero()
    {
        var project = JsonSerializer.Deserialize<ScadaProject>(
            JsonSerializer.Serialize(ScadaProject.CreateDefault("Projet"), Options), Options);

        Assert.IsNull(project!.FormatVersion);
        Assert.AreEqual(0, project.EffectiveFormatVersion);
    }

    [TestMethod]
    public void TheSceneCarriesTheSameContract()
    {
        var scene = ScadaScene.CreateEmpty("win00001", "win00001", CanvasSize.DefaultDesktop);

        var json = JsonSerializer.Serialize(scene, Options);
        Assert.IsFalse(json.Contains("FormatVersion", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(0, scene.EffectiveFormatVersion);

        var versioned = JsonSerializer.Serialize(scene with { FormatVersion = 2 }, Options);
        StringAssert.Contains(versioned, "\"FormatVersion\": 2");
    }

    [TestMethod]
    public void TheTagCatalogCarriesTheSameContract()
    {
        var catalog = new ScadaTagCatalog("tf100web-scada-tags-v1", []);

        var json = JsonSerializer.Serialize(catalog, Options);
        Assert.IsFalse(json.Contains("FormatVersion", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(0, catalog.EffectiveFormatVersion);

        var versioned = JsonSerializer.Serialize(catalog with { FormatVersion = 1 }, Options);
        StringAssert.Contains(versioned, "\"FormatVersion\": 1");
    }
}
```

- [ ] **Step 2: Exécuter le test et vérifier qu'il échoue**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~FormatVersionSerializationTests"`
Expected: échec de compilation, `FormatVersion` n'existe pas.

- [ ] **Step 3: Ajouter le champ aux trois records**

Dans `ScadaProject`, ajouter en **dernier paramètre positionnel** pour ne pas déplacer les existants :

```csharp
    IReadOnlyList<QuickWindowInvocation>? QuickWindowInvocations = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? FormatVersion = null)
```

et dans le corps :

```csharp
    /// <summary>Gets the persisted format generation; an absent field means generation zero.</summary>
    /// <remarks>
    /// Never serialised while null, so every project written before this mechanism existed keeps its exact
    /// bytes. Contracts: 2026-09-08-project-format-versioning-and-converters-design.md C1, C10.
    /// </remarks>
    [JsonIgnore]
    public int EffectiveFormatVersion => FormatVersion ?? 0;
```

Répéter à l'identique sur `ScadaScene` (dernier paramètre après `FooterPageKey`) et sur `ScadaTagCatalog` (après `ImportedAtUtc`). Ajouter `using System.Text.Json.Serialization;` où il manque.

- [ ] **Step 4: Exécuter le test et vérifier qu'il passe**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~FormatVersionSerializationTests"`
Expected: PASS, 5 tests.

- [ ] **Step 5: Vérifier qu'aucun artefact gelé n'a bougé**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 926/926. Une dérive de SHA sur une fixture gelée signifie qu'un `FormatVersion` a été sérialisé alors qu'il était null : corriger, ne jamais régénérer la fixture.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: carry a format generation on project, scene and tag catalog

A nullable integer, never serialised while null, so every artifact written
before this mechanism keeps its exact bytes and the frozen fixtures do not
move. An absent field means generation zero.

The component module is deliberately absent: a .sep already ships
ElementStudioComponentMetadata.SchemaVersion, and a second number on the
same file would create two truths about it.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: Contrat de convertisseur, registre et validation de chaîne

**Files:**
- Create: `src/ScadaBuilderV2.Application/Formats/ArtifactModule.cs`
- Create: `src/ScadaBuilderV2.Application/Formats/IArtifactConverter.cs`
- Create: `src/ScadaBuilderV2.Application/Formats/ArtifactConverterRegistry.cs`
- Test: `tests/ScadaBuilderV2.Tests/Formats/ArtifactConverterRegistryTests.cs`

**Interfaces:**
- Consumes: `ScadaFormatGeneration` (Task 1).
- Produces: `enum ArtifactModule { Project, Scene, TagCatalog, Component }`; `IArtifactConverter` avec `Module`, `FromVersion`, `ToVersion`, `StepDescription` et `JsonNode Convert(JsonNode document)`; `ArtifactConverterRegistry.Register(IArtifactConverter)`, `.ResolveChain(ArtifactModule, int from, int to)` → `ConversionChain`.

- [ ] **Step 1: Écrire le test du registre**

`tests/ScadaBuilderV2.Tests/Formats/ArtifactConverterRegistryTests.cs`

```csharp
using System.Text.Json.Nodes;
using ScadaBuilderV2.Application.Formats;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Locks chain composition. A converter is written one step at a time and the registry composes them, so an
/// incomplete or ambiguous chain must be a refusal before any write, never a partial conversion.
/// </summary>
[TestClass]
public sealed class ArtifactConverterRegistryTests
{
    private sealed class Step(ArtifactModule module, int from, int to) : IArtifactConverter
    {
        public ArtifactModule Module => module;
        public int FromVersion => from;
        public int ToVersion => to;
        public string StepDescription => $"{module} {from} vers {to}";
        public JsonNode Convert(JsonNode document)
        {
            document["Steps"] = (document["Steps"]?.GetValue<string>() ?? "") + $"[{from}->{to}]";
            return document;
        }
    }

    [TestMethod]
    public void ACompleteChainIsResolvedInOrder()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(ArtifactModule.Project, 1, 2));
        registry.Register(new Step(ArtifactModule.Project, 0, 1));

        var chain = registry.ResolveChain(ArtifactModule.Project, 0, 2);

        Assert.IsTrue(chain.IsComplete);
        CollectionAssert.AreEqual(
            new[] { 0, 1 },
            chain.Steps.Select(step => step.FromVersion).ToArray(),
            "the chain must run in ascending order regardless of registration order.");
    }

    [TestMethod]
    public void AChainWithAGapIsRefusedAndNamesTheMissingStep()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(ArtifactModule.Project, 0, 1));
        // 1 -> 2 is missing.
        registry.Register(new Step(ArtifactModule.Project, 2, 3));

        var chain = registry.ResolveChain(ArtifactModule.Project, 0, 3);

        Assert.IsFalse(chain.IsComplete);
        Assert.AreEqual(1, chain.MissingFromVersion);
    }

    [TestMethod]
    public void AConverterMaySpanSeveralStepsWhenNoOtherClaimsThem()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(ArtifactModule.Project, 0, 3));

        var chain = registry.ResolveChain(ArtifactModule.Project, 0, 3);

        Assert.IsTrue(chain.IsComplete);
        Assert.AreEqual(1, chain.Steps.Count);
    }

    /// <summary>Two converters claiming the same step is a registration error, not a runtime coin toss.</summary>
    [TestMethod]
    public void TwoConvertersClaimingTheSameStepIsRejectedAtRegistration()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(ArtifactModule.Project, 0, 1));

        var thrown = Assert.ThrowsException<InvalidOperationException>(
            () => registry.Register(new Step(ArtifactModule.Project, 0, 2)));

        StringAssert.Contains(thrown.Message, "Project");
        StringAssert.Contains(thrown.Message, "0");
    }

    [TestMethod]
    public void ModulesDoNotInterfereWithEachOther()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(ArtifactModule.Project, 0, 1));
        registry.Register(new Step(ArtifactModule.Scene, 0, 1));

        Assert.IsTrue(registry.ResolveChain(ArtifactModule.Scene, 0, 1).IsComplete);
        Assert.AreEqual(1, registry.ResolveChain(ArtifactModule.Scene, 0, 1).Steps.Count);
    }

    [TestMethod]
    public void AnArtifactAlreadyCurrentResolvesToAnEmptyCompleteChain()
    {
        var registry = new ArtifactConverterRegistry();

        var chain = registry.ResolveChain(ArtifactModule.Project, 2, 2);

        Assert.IsTrue(chain.IsComplete);
        Assert.AreEqual(0, chain.Steps.Count);
    }

    [TestMethod]
    public void ApplyingAChainRunsEveryStepInOrder()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(ArtifactModule.Project, 0, 1));
        registry.Register(new Step(ArtifactModule.Project, 1, 2));

        var document = registry.ResolveChain(ArtifactModule.Project, 0, 2).Apply(new JsonObject());

        Assert.AreEqual("[0->1][1->2]", document["Steps"]!.GetValue<string>());
    }
}
```

- [ ] **Step 2: Exécuter le test et vérifier qu'il échoue**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ArtifactConverterRegistryTests"`
Expected: échec de compilation.

- [ ] **Step 3: Écrire le module et le contrat**

`src/ScadaBuilderV2.Application/Formats/ArtifactModule.cs`

```csharp
namespace ScadaBuilderV2.Application.Formats;

/// <summary>One independently versioned persisted artifact family.</summary>
/// <remarks>
/// Each module carries its own generation so one can move without forcing the others, and a converter only
/// ever knows its own module.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C1.
/// </remarks>
public enum ArtifactModule
{
    /// <summary>`project.json`.</summary>
    Project,

    /// <summary>One persisted scene under `scenes/`.</summary>
    Scene,

    /// <summary>The imported tag catalog.</summary>
    TagCatalog,

    /// <summary>One `.sep` component package.</summary>
    Component
}
```

`src/ScadaBuilderV2.Application/Formats/IArtifactConverter.cs`

```csharp
using System.Text.Json.Nodes;

namespace ScadaBuilderV2.Application.Formats;

/// <summary>Converts one artifact from one format generation to a later one.</summary>
/// <remarks>
/// A converter is a pure function on the deserialised tree: no disk, no network, no clock. That is what makes
/// it testable from a frozen input and what makes a conversion deterministic, so converting the same file
/// twice or on two machines produces identical bytes.
///
/// Converters are written one step at a time; the registry composes the chain. A converter may span several
/// steps when the change is trivial, but never over a step another converter claims.
///
/// No descending converter exists, today or later: it would have to invent the data it does not hold.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C3, C7.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ArtifactConverterRegistryTests.cs.
/// </remarks>
public interface IArtifactConverter
{
    /// <summary>Module this converter operates on.</summary>
    ArtifactModule Module { get; }

    /// <summary>Generation this converter accepts.</summary>
    int FromVersion { get; }

    /// <summary>Generation this converter produces.</summary>
    int ToVersion { get; }

    /// <summary>One sentence naming what this step changes, shown to the operator in the conversion plan.</summary>
    string StepDescription { get; }

    /// <summary>Returns the converted document. The input may be mutated and returned.</summary>
    JsonNode Convert(JsonNode document);
}
```

- [ ] **Step 4: Écrire le registre et la chaîne**

`src/ScadaBuilderV2.Application/Formats/ArtifactConverterRegistry.cs`

```csharp
using System.Text.Json.Nodes;

namespace ScadaBuilderV2.Application.Formats;

/// <summary>An ordered, validated sequence of converters from one generation to another.</summary>
/// <param name="Module">Module the chain belongs to.</param>
/// <param name="FromVersion">Generation the artifact declares.</param>
/// <param name="ToVersion">Generation this binary understands.</param>
/// <param name="Steps">Converters to run, in ascending order.</param>
/// <param name="MissingFromVersion">Generation no converter accepts, or null when the chain is complete.</param>
public sealed record ConversionChain(
    ArtifactModule Module,
    int FromVersion,
    int ToVersion,
    IReadOnlyList<IArtifactConverter> Steps,
    int? MissingFromVersion)
{
    /// <summary>Gets whether every generation between the two ends is covered.</summary>
    public bool IsComplete => MissingFromVersion is null;

    /// <summary>Runs every step in order and returns the converted document.</summary>
    /// <exception cref="InvalidOperationException">The chain is incomplete.</exception>
    public JsonNode Apply(JsonNode document)
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException(
                $"La chaîne de conversion {Module} {FromVersion}->{ToVersion} est incomplète à {MissingFromVersion}.");
        }

        var current = document;
        foreach (var step in Steps)
        {
            current = step.Convert(current);
        }
        return current;
    }
}

/// <summary>Holds the converters of every module and composes their chains.</summary>
/// <remarks>
/// Two converters claiming the same starting generation is a registration error rather than a runtime one:
/// the ambiguity has no correct resolution, and discovering it at start-up is the only honest moment.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C3, C4.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ArtifactConverterRegistryTests.cs.
/// </remarks>
public sealed class ArtifactConverterRegistry
{
    private readonly Dictionary<(ArtifactModule Module, int From), IArtifactConverter> converters = [];

    /// <summary>Registers one converter.</summary>
    /// <exception cref="InvalidOperationException">Another converter already claims this starting generation.</exception>
    public void Register(IArtifactConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        if (converter.ToVersion <= converter.FromVersion)
        {
            throw new InvalidOperationException(
                $"Un convertisseur ne descend jamais: {converter.Module} {converter.FromVersion}->{converter.ToVersion}.");
        }

        var key = (converter.Module, converter.FromVersion);
        if (converters.TryGetValue(key, out var existing))
        {
            throw new InvalidOperationException(
                $"Deux convertisseurs revendiquent {converter.Module} depuis {converter.FromVersion}: "
                + $"{existing.StepDescription} et {converter.StepDescription}.");
        }
        converters[key] = converter;
    }

    /// <summary>Composes the chain between two generations, without running anything.</summary>
    public ConversionChain ResolveChain(ArtifactModule module, int fromVersion, int toVersion)
    {
        var steps = new List<IArtifactConverter>();
        var current = fromVersion;
        while (current < toVersion)
        {
            if (!converters.TryGetValue((module, current), out var step))
            {
                return new ConversionChain(module, fromVersion, toVersion, steps, current);
            }
            steps.Add(step);
            current = step.ToVersion;
        }
        return new ConversionChain(module, fromVersion, toVersion, steps, null);
    }
}
```

- [ ] **Step 5: Exécuter le test et vérifier qu'il passe**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ArtifactConverterRegistryTests"`
Expected: PASS, 7 tests.

- [ ] **Step 6: Exécuter la suite complète et committer**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 933/933.

```bash
git add -A
git commit -m "feat: add the artifact converter contract and its registry

A converter declares its module, the generation it accepts and the one it
produces, and is a pure function on the deserialised tree: no disk, no
network, no clock. That is what makes a conversion deterministic and
testable from a frozen input.

The registry composes chains. An incomplete chain names the generation no
converter accepts and refuses rather than converting partially; two
converters claiming the same starting generation is a registration error,
because the ambiguity has no correct resolution.

No descending converter exists, today or later: it would have to invent the
data it does not hold.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: Raccorder le module `.sep` à son `SchemaVersion` existant

**Files:**
- Create: `src/ScadaBuilderV2.Application/Formats/ComponentFormatVersionAccessor.cs`
- Test: `tests/ScadaBuilderV2.Tests/Formats/ComponentFormatVersionAccessorTests.cs`

**Interfaces:**
- Consumes: `ArtifactModule` (Task 3), `ElementStudioComponentMetadata.CurrentSchemaVersion`.
- Produces: `ComponentFormatVersionAccessor.ReadGeneration(JsonNode)` → `int`, `.WriteGeneration(JsonNode, int)`.

- [ ] **Step 1: Écrire le test**

`tests/ScadaBuilderV2.Tests/Formats/ComponentFormatVersionAccessorTests.cs`

```csharp
using System.Text.Json.Nodes;
using ScadaBuilderV2.Application.ElementStudio;
using ScadaBuilderV2.Application.Formats;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// The component module already had a version before this mechanism existed. It is wired in rather than
/// doubled: two numbers on one file would raise a question - which one is authoritative - that has no good
/// answer.
/// </summary>
[TestClass]
public sealed class ComponentFormatVersionAccessorTests
{
    [TestMethod]
    public void TheGenerationIsReadFromTheExistingMetadataField()
    {
        var document = JsonNode.Parse("""{"Metadata":{"Schema":"s","SchemaVersion":1,"Format":"json.sep"}}""")!;

        Assert.AreEqual(1, ComponentFormatVersionAccessor.ReadGeneration(document));
    }

    [TestMethod]
    public void AComponentWithoutMetadataIsGenerationZero()
    {
        Assert.AreEqual(0, ComponentFormatVersionAccessor.ReadGeneration(JsonNode.Parse("{}")!));
        Assert.AreEqual(0, ComponentFormatVersionAccessor.ReadGeneration(JsonNode.Parse("""{"Metadata":{}}""")!));
    }

    [TestMethod]
    public void WritingTheGenerationUpdatesTheSameFieldTheReaderUses()
    {
        var document = JsonNode.Parse("""{"Metadata":{"Schema":"s","SchemaVersion":1}}""")!;

        ComponentFormatVersionAccessor.WriteGeneration(document, 2);

        Assert.AreEqual(2, ComponentFormatVersionAccessor.ReadGeneration(document));
        Assert.AreEqual(2, document["Metadata"]!["SchemaVersion"]!.GetValue<int>());
    }

    [TestMethod]
    public void TheShippedGenerationMatchesTheComponentMetadataConstant()
    {
        Assert.AreEqual(
            ElementStudioComponentMetadata.CurrentSchemaVersion,
            ScadaBuilderV2.Domain.Projects.ScadaFormatGeneration.Component,
            "the two constants describe the same thing and must never drift.");
    }
}
```

- [ ] **Step 2: Exécuter le test et vérifier qu'il échoue**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ComponentFormatVersionAccessorTests"`
Expected: échec de compilation.

- [ ] **Step 3: Écrire l'accesseur**

`src/ScadaBuilderV2.Application/Formats/ComponentFormatVersionAccessor.cs`

```csharp
using System.Text.Json.Nodes;

namespace ScadaBuilderV2.Application.Formats;

/// <summary>Reads and writes the `.sep` generation through the field the format already shipped.</summary>
/// <remarks>
/// `ElementStudioComponentMetadata.SchemaVersion` predates this mechanism and already describes the shape of
/// a component file. It is therefore the component module's format version, used as-is. Adding a parallel
/// `FormatVersion` beside it would create two numbers for one file and a question about which is
/// authoritative that has no good answer.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C1.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ComponentFormatVersionAccessorTests.cs.
/// </remarks>
public static class ComponentFormatVersionAccessor
{
    private const string MetadataField = "Metadata";
    private const string VersionField = "SchemaVersion";

    /// <summary>Returns the component's generation, or zero when the metadata block is absent.</summary>
    public static int ReadGeneration(JsonNode document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var version = document[MetadataField]?[VersionField];
        return version is not null && version.GetValueKind() == System.Text.Json.JsonValueKind.Number
            ? version.GetValue<int>()
            : 0;
    }

    /// <summary>Writes the component's generation, creating the metadata block when it is absent.</summary>
    public static void WriteGeneration(JsonNode document, int generation)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document[MetadataField] is not JsonObject metadata)
        {
            metadata = [];
            document[MetadataField] = metadata;
        }
        metadata[VersionField] = generation;
    }
}
```

- [ ] **Step 4: Exécuter le test et vérifier qu'il passe**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ComponentFormatVersionAccessorTests"`
Expected: PASS, 4 tests.

- [ ] **Step 5: Exécuter la suite complète et committer**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 937/937.

```bash
git add -A
git commit -m "feat: wire the .sep module to the version it already carried

ElementStudioComponentMetadata.SchemaVersion predates this mechanism and
already describes the shape of a component file, so it is the component
module's format version and is used as-is. A parallel field beside it would
create two numbers for one file and a question about which is authoritative
that has no good answer.

A test pins the two constants together so they cannot drift.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: Plan de conversion, sauvegarde et coordinateur

**Files:**
- Create: `src/ScadaBuilderV2.Application/Formats/ConversionPlan.cs`
- Create: `src/ScadaBuilderV2.Application/Formats/IConversionConsent.cs`
- Create: `src/ScadaBuilderV2.Application/Formats/ConversionCoordinator.cs`
- Create: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ArtifactBackupWriter.cs`
- Test: `tests/ScadaBuilderV2.Tests/Formats/ConversionCoordinatorTests.cs`
- Test: `tests/ScadaBuilderV2.Tests/Formats/ArtifactBackupWriterTests.cs`

**Interfaces:**
- Consumes: `ArtifactConverterRegistry`, `ConversionChain` (Task 3).
- Produces: `ArtifactToConvert(ArtifactModule Module, string FilePath, int FromVersion, int ToVersion)`; `ConversionPlanEntry(ArtifactModule Module, string FilePath, int FromVersion, int ToVersion, IReadOnlyList<string> StepDescriptions)`; `ConversionOutcome(bool CanProceed, ConversionPlan Plan, IReadOnlyList<ScadaBuildValidationIssue> Diagnostics)`; `ConversionPlan(IReadOnlyList<ConversionPlanEntry> Entries)` avec `.IsEmpty`; `enum ConversionDecision { Convert, Cancel }`; `IConversionConsent.RequestAsync(ConversionPlan, CancellationToken)` → `Task<ConversionDecision>`; `ArtifactBackupWriter.CreateBackup(string filePath)` → `string`.

- [ ] **Step 1: Écrire le test de la sauvegarde**

`tests/ScadaBuilderV2.Tests/Formats/ArtifactBackupWriterTests.cs`

```csharp
using System.IO;
using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// The backup is the only way back from a conversion, so it is written before the conversion and never
/// silently replaces an earlier one.
/// </summary>
[TestClass]
public sealed class ArtifactBackupWriterTests
{
    private string root = "";

    [TestInitialize]
    public void CreateWorkspace()
    {
        root = Path.Combine(Path.GetTempPath(), "scada-backup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void RemoveWorkspace()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public void TheBackupKeepsTheOriginalBytes()
    {
        var path = Path.Combine(root, "project.json");
        File.WriteAllText(path, """{"Name":"avant"}""");

        var backup = ArtifactBackupWriter.CreateBackup(path);

        Assert.AreEqual(Path.Combine(root, "project.json.bak"), backup);
        Assert.AreEqual("""{"Name":"avant"}""", File.ReadAllText(backup));
    }

    [TestMethod]
    public void AnExistingBackupIsNeverOverwritten()
    {
        var path = Path.Combine(root, "project.json");
        File.WriteAllText(path, "second");
        File.WriteAllText(Path.Combine(root, "project.json.bak"), "premier");

        var backup = ArtifactBackupWriter.CreateBackup(path);

        Assert.AreEqual(Path.Combine(root, "project.json.bak.1"), backup);
        Assert.AreEqual("premier", File.ReadAllText(Path.Combine(root, "project.json.bak")));
        Assert.AreEqual("second", File.ReadAllText(backup));
    }

    [TestMethod]
    public void NumberingContinuesPastTheFirstCollision()
    {
        var path = Path.Combine(root, "a.sep");
        File.WriteAllText(path, "courant");
        File.WriteAllText(Path.Combine(root, "a.sep.bak"), "un");
        File.WriteAllText(Path.Combine(root, "a.sep.bak.1"), "deux");

        Assert.AreEqual(Path.Combine(root, "a.sep.bak.2"), ArtifactBackupWriter.CreateBackup(path));
    }
}
```

- [ ] **Step 2: Exécuter le test et vérifier qu'il échoue**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ArtifactBackupWriterTests"`
Expected: échec de compilation.

- [ ] **Step 3: Écrire la sauvegarde**

`src/ScadaBuilderV2.Infrastructure/ModernProjects/ArtifactBackupWriter.cs`

```csharp
using System.IO;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

/// <summary>Copies an artifact aside before it is converted.</summary>
/// <remarks>
/// A conversion does not undo, so this copy is the only way back and it is written first: its failure aborts
/// the conversion. An existing backup is never replaced, because the file it holds may be the only surviving
/// copy of an earlier generation.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C6, C7.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ArtifactBackupWriterTests.cs.
/// </remarks>
public static class ArtifactBackupWriter
{
    /// <summary>Copies the file to `<name>.bak`, numbering the suffix rather than replacing an earlier backup.</summary>
    /// <returns>The path actually written.</returns>
    public static string CreateBackup(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var candidate = filePath + ".bak";
        var index = 1;
        while (File.Exists(candidate))
        {
            candidate = $"{filePath}.bak.{index++}";
        }
        File.Copy(filePath, candidate);
        return candidate;
    }
}
```

- [ ] **Step 4: Exécuter le test et vérifier qu'il passe**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ArtifactBackupWriterTests"`
Expected: PASS, 3 tests.

- [ ] **Step 5: Écrire le test du coordinateur**

`tests/ScadaBuilderV2.Tests/Formats/ConversionCoordinatorTests.cs`

```csharp
using System.Text.Json.Nodes;
using ScadaBuilderV2.Application.Formats;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Locks the two outcomes C5 allows: convert, or do not open. There is no read-only consultation mode, so
/// there is no path by which an unconverted artifact becomes active.
/// </summary>
[TestClass]
public sealed class ConversionCoordinatorTests
{
    private sealed class Step(int from, int to) : IArtifactConverter
    {
        public ArtifactModule Module => ArtifactModule.Project;
        public int FromVersion => from;
        public int ToVersion => to;
        public string StepDescription => $"étape {from} vers {to}";
        public JsonNode Convert(JsonNode document)
        {
            document["Converted"] = to;
            return document;
        }
    }

    private sealed class Consent(ConversionDecision decision) : IConversionConsent
    {
        public int RequestCount { get; private set; }
        public ConversionPlan? SeenPlan { get; private set; }

        public Task<ConversionDecision> RequestAsync(ConversionPlan plan, CancellationToken cancellationToken)
        {
            RequestCount++;
            SeenPlan = plan;
            return Task.FromResult(decision);
        }
    }

    [TestMethod]
    public async Task AnArtifactAlreadyCurrentIsNotConvertedAndTheOperatorIsNotAsked()
    {
        var consent = new Consent(ConversionDecision.Convert);
        var coordinator = new ConversionCoordinator(new ArtifactConverterRegistry(), consent);

        var outcome = await coordinator.PrepareAsync(
            [new ArtifactToConvert(ArtifactModule.Project, "project.json", 0, 0)],
            CancellationToken.None);

        Assert.IsTrue(outcome.CanProceed);
        Assert.IsTrue(outcome.Plan.IsEmpty);
        Assert.AreEqual(0, consent.RequestCount, "there is nothing to consent to.");
    }

    [TestMethod]
    public async Task RefusingTheConversionStopsTheTransition()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(0, 1));
        var consent = new Consent(ConversionDecision.Cancel);
        var coordinator = new ConversionCoordinator(registry, consent);

        var outcome = await coordinator.PrepareAsync(
            [new ArtifactToConvert(ArtifactModule.Project, "project.json", 0, 1)],
            CancellationToken.None);

        Assert.IsFalse(outcome.CanProceed);
        Assert.AreEqual(1, consent.RequestCount);
        Assert.AreEqual("project.conversion-declined", outcome.Diagnostics.Single().Code);
    }

    [TestMethod]
    public async Task AcceptingTheConversionProducesAnExecutablePlan()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(0, 1));
        registry.Register(new Step(1, 2));
        var consent = new Consent(ConversionDecision.Convert);
        var coordinator = new ConversionCoordinator(registry, consent);

        var outcome = await coordinator.PrepareAsync(
            [new ArtifactToConvert(ArtifactModule.Project, "project.json", 0, 2)],
            CancellationToken.None);

        Assert.IsTrue(outcome.CanProceed);
        var entry = outcome.Plan.Entries.Single();
        Assert.AreEqual(0, entry.FromVersion);
        Assert.AreEqual(2, entry.ToVersion);
        CollectionAssert.AreEqual(
            new[] { "étape 0 vers 1", "étape 1 vers 2" },
            entry.StepDescriptions.ToArray(),
            "the operator is shown what each step changes, not just that something will.");
    }

    [TestMethod]
    public async Task AnIncompleteChainIsRefusedBeforeTheOperatorIsAsked()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(0, 1));
        var consent = new Consent(ConversionDecision.Convert);
        var coordinator = new ConversionCoordinator(registry, consent);

        var outcome = await coordinator.PrepareAsync(
            [new ArtifactToConvert(ArtifactModule.Project, "project.json", 0, 3)],
            CancellationToken.None);

        Assert.IsFalse(outcome.CanProceed);
        Assert.AreEqual(0, consent.RequestCount, "there is no point asking to run a chain that cannot run.");
        var issue = outcome.Diagnostics.Single();
        Assert.AreEqual("project.conversion-chain-incomplete", issue.Code);
        StringAssert.Contains(issue.Message, "1");
    }
}
```

- [ ] **Step 6: Exécuter le test et vérifier qu'il échoue**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ConversionCoordinatorTests"`
Expected: échec de compilation.

- [ ] **Step 7: Écrire le plan et le consentement**

`src/ScadaBuilderV2.Application/Formats/ConversionPlan.cs`

```csharp
namespace ScadaBuilderV2.Application.Formats;

/// <summary>One artifact found to be behind the generation this binary understands.</summary>
public sealed record ArtifactToConvert(
    ArtifactModule Module,
    string FilePath,
    int FromVersion,
    int ToVersion);

/// <summary>What one artifact's conversion will do, as shown to the operator.</summary>
public sealed record ConversionPlanEntry(
    ArtifactModule Module,
    string FilePath,
    int FromVersion,
    int ToVersion,
    IReadOnlyList<string> StepDescriptions);

/// <summary>Everything a single conversion will touch.</summary>
public sealed record ConversionPlan(IReadOnlyList<ConversionPlanEntry> Entries)
{
    /// <summary>Gets whether there is nothing to convert.</summary>
    public bool IsEmpty => Entries.Count == 0;
}

/// <summary>The operator's answer. C5 allows two, and read-only consultation is not one of them.</summary>
public enum ConversionDecision
{
    /// <summary>Convert, back up first, and open.</summary>
    Convert,

    /// <summary>Do not convert, and do not open.</summary>
    Cancel
}
```

`src/ScadaBuilderV2.Application/Formats/IConversionConsent.cs`

```csharp
namespace ScadaBuilderV2.Application.Formats;

/// <summary>Asks the operator to accept a conversion plan.</summary>
/// <remarks>
/// A conversion does not undo, so it is never implicit. The implementation owns the presentation and nothing
/// else; the coordinator owns whether the question is worth asking.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C5, C7.
/// </remarks>
public interface IConversionConsent
{
    /// <summary>Returns the operator's answer for one plan.</summary>
    Task<ConversionDecision> RequestAsync(ConversionPlan plan, CancellationToken cancellationToken);
}
```

- [ ] **Step 8: Écrire le coordinateur**

`src/ScadaBuilderV2.Application/Formats/ConversionCoordinator.cs`

```csharp
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Application.Formats;

/// <summary>Result of preparing a conversion, before anything is written.</summary>
public sealed record ConversionOutcome(
    bool CanProceed,
    ConversionPlan Plan,
    IReadOnlyList<ScadaBuildValidationIssue> Diagnostics);

/// <summary>Decides whether a set of artifacts can be opened, and on what terms.</summary>
/// <remarks>
/// The order matters and is the point of this type. A chain that cannot run is refused before the operator is
/// asked, because there is nothing to consent to. An artifact already current asks nothing. And a refusal
/// stops the transition outright: C5 allows convert or do not open, so nothing here can produce a session on
/// an unconverted artifact.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C4, C5.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ConversionCoordinatorTests.cs.
/// </remarks>
public sealed class ConversionCoordinator(ArtifactConverterRegistry registry, IConversionConsent consent)
{
    /// <summary>Builds the plan, validates every chain and obtains consent when there is something to convert.</summary>
    public async Task<ConversionOutcome> PrepareAsync(
        IReadOnlyList<ArtifactToConvert> artifacts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifacts);

        var entries = new List<ConversionPlanEntry>();
        var diagnostics = new List<ScadaBuildValidationIssue>();

        foreach (var artifact in artifacts.Where(item => item.FromVersion < item.ToVersion))
        {
            var chain = registry.ResolveChain(artifact.Module, artifact.FromVersion, artifact.ToVersion);
            if (!chain.IsComplete)
            {
                diagnostics.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Error,
                    "project.conversion-chain-incomplete",
                    $"Aucun convertisseur ne prend le format {chain.MissingFromVersion} du module "
                    + $"{artifact.Module}. La conversion est refusée plutôt qu'exécutée à moitié.",
                    PropertyPath: artifact.FilePath));
                continue;
            }

            entries.Add(new ConversionPlanEntry(
                artifact.Module,
                artifact.FilePath,
                artifact.FromVersion,
                artifact.ToVersion,
                chain.Steps.Select(step => step.StepDescription).ToArray()));
        }

        var plan = new ConversionPlan(entries);
        if (diagnostics.Count > 0)
        {
            return new ConversionOutcome(false, plan, diagnostics);
        }
        if (plan.IsEmpty)
        {
            return new ConversionOutcome(true, plan, []);
        }

        var decision = await consent.RequestAsync(plan, cancellationToken);
        if (decision == ConversionDecision.Cancel)
        {
            return new ConversionOutcome(false, plan, [new ScadaBuildValidationIssue(
                ScadaBuildValidationSeverity.Warning,
                "project.conversion-declined",
                "La conversion a été refusée; le projet n'est pas ouvert.")]);
        }

        return new ConversionOutcome(true, plan, []);
    }
}
```

- [ ] **Step 9: Exécuter le test et vérifier qu'il passe**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ConversionCoordinatorTests"`
Expected: PASS, 4 tests.

- [ ] **Step 10: Exécuter la suite complète et committer**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 944/944.

```bash
git add -A
git commit -m "feat: plan a conversion, back it up, and ask before running it

The coordinator's order is the point. A chain that cannot run is refused
before the operator is asked, because there is nothing to consent to. An
artifact already current asks nothing. A refusal stops the transition
outright: C5 allows convert or do not open, so nothing here can produce a
session on an unconverted artifact.

The backup is written before the conversion because it is the only way back,
and an existing one is never replaced: the file it holds may be the only
surviving copy of an earlier generation.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: Dialogue de conversion, deux issues

**Files:**
- Create: `src/ScadaBuilderV2.App/Projects/ConversionPlanDialog.xaml`
- Create: `src/ScadaBuilderV2.App/Projects/ConversionPlanDialog.xaml.cs`
- Create: `src/ScadaBuilderV2.App/Projects/WpfConversionConsent.cs`
- Test: `tests/ScadaBuilderV2.Tests/Formats/ConversionDialogContractTests.cs`

**Interfaces:**
- Consumes: `ConversionPlan`, `ConversionDecision`, `IConversionConsent` (Task 5).
- Produces: `ConversionPlanDialog.Decision`; `WpfConversionConsent(Window owner)` implémentant `IConversionConsent`.

- [ ] **Step 1: Écrire le test de contrat**

`tests/ScadaBuilderV2.Tests/Formats/ConversionDialogContractTests.cs`

```csharp
using System.IO;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Source contract for a surface no unit test can drive. Each assertion encodes a decision of the spec that
/// would otherwise be silently lost in a later edit.
/// </summary>
[TestClass]
public sealed class ConversionDialogContractTests
{
    [TestMethod]
    public void TheDialogOffersExactlyTwoOutcomes()
    {
        var xaml = ReadAppFile(Path.Combine("Projects", "ConversionPlanDialog.xaml"));

        StringAssert.Contains(xaml, "Content=\"Convertir\"");
        StringAssert.Contains(xaml, "Content=\"Ne pas ouvrir\"");
        Assert.IsFalse(
            xaml.Contains("sans convertir", StringComparison.OrdinalIgnoreCase),
            "C5 removed the read-only consultation mode: the generations diverge too much for a "
            + "half-migrated session to be faithful.");
    }

    [TestMethod]
    public void TheDialogSaysTheOperationDoesNotUndo()
    {
        var xaml = ReadAppFile(Path.Combine("Projects", "ConversionPlanDialog.xaml"));

        StringAssert.Contains(xaml, "ne se défait pas");
        StringAssert.Contains(xaml, "sauvegarde");
    }

    [TestMethod]
    public void CancelIsTheDefaultDecision()
    {
        var code = ReadAppFile(Path.Combine("Projects", "ConversionPlanDialog.xaml.cs"));

        StringAssert.Contains(
            code,
            "ConversionDecision Decision { get; private set; } = ConversionDecision.Cancel;",
            "closing the dialog by any other route must not convert.");
    }

    [TestMethod]
    public void ThePlanIsShownStepByStep()
    {
        var xaml = ReadAppFile(Path.Combine("Projects", "ConversionPlanDialog.xaml"));

        StringAssert.Contains(xaml, "StepDescriptions");
        StringAssert.Contains(xaml, "FilePath");
    }

    private static string ReadAppFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.App", relativePath);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }
        Assert.Fail($"Unable to locate src/ScadaBuilderV2.App/{relativePath}.");
        return string.Empty;
    }
}
```

- [ ] **Step 2: Exécuter le test et vérifier qu'il échoue**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ConversionDialogContractTests"`
Expected: FAIL, `Assert.Fail` — le fichier XAML n'existe pas.

- [ ] **Step 3: Écrire le XAML**

`src/ScadaBuilderV2.App/Projects/ConversionPlanDialog.xaml`

```xml
<Window x:Class="ScadaBuilderV2.App.Projects.ConversionPlanDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Conversion de format requise"
        Width="640"
        SizeToContent="Height"
        MaxHeight="620"
        ResizeMode="NoResize"
        WindowStartupLocation="CenterOwner"
        ShowInTaskbar="False">
    <Window.Resources>
        <SolidColorBrush x:Key="InkBrush" Color="#0F2A30"/>
        <SolidColorBrush x:Key="MutedBrush" Color="#4E6A71"/>
        <SolidColorBrush x:Key="PanelBrush" Color="#FFFFFF"/>
        <SolidColorBrush x:Key="SurfaceBrush" Color="#F4F8F6"/>
        <SolidColorBrush x:Key="BorderBrushSoft" Color="#1A0F2A30"/>
        <SolidColorBrush x:Key="WarningBrush" Color="#6A4300"/>
    </Window.Resources>
    <DockPanel Background="{StaticResource PanelBrush}">
        <StackPanel DockPanel.Dock="Top" Margin="26,24,26,12">
            <TextBlock Text="Ce projet doit être converti avant d'être ouvert."
                       FontSize="18" FontWeight="SemiBold"
                       TextWrapping="Wrap"
                       Foreground="{StaticResource InkBrush}"/>
            <TextBlock Margin="0,10,0,0"
                       TextWrapping="Wrap"
                       Foreground="{StaticResource WarningBrush}"
                       Text="La conversion ne se défait pas. Une sauvegarde de chaque fichier est écrite avant toute modification; c'est le seul retour possible."/>
        </StackPanel>

        <Border DockPanel.Dock="Bottom"
                Background="{StaticResource SurfaceBrush}"
                BorderBrush="{StaticResource BorderBrushSoft}"
                BorderThickness="0,1,0,0"
                Padding="20,16">
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                <Button x:Name="ConvertButton"
                        Content="Convertir"
                        MinWidth="140" MinHeight="34" Margin="6,0"
                        IsDefault="True"
                        Click="OnConvertClick"/>
                <Button x:Name="CancelButton"
                        Content="Ne pas ouvrir"
                        MinWidth="140" MinHeight="34" Margin="6,0"
                        IsCancel="True"
                        Click="OnCancelClick"/>
            </StackPanel>
        </Border>

        <ScrollViewer VerticalScrollBarVisibility="Auto" Margin="26,0,26,12">
            <ItemsControl x:Name="PlanItems">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border BorderBrush="{StaticResource BorderBrushSoft}"
                                BorderThickness="1" CornerRadius="4"
                                Padding="12" Margin="0,4">
                            <StackPanel>
                                <TextBlock FontWeight="SemiBold" Foreground="{StaticResource InkBrush}">
                                    <Run Text="{Binding Module, Mode=OneWay}"/>
                                    <Run Text=" — format "/>
                                    <Run Text="{Binding FromVersion, Mode=OneWay}"/>
                                    <Run Text=" vers "/>
                                    <Run Text="{Binding ToVersion, Mode=OneWay}"/>
                                </TextBlock>
                                <TextBlock Text="{Binding FilePath}"
                                           FontSize="11"
                                           TextTrimming="CharacterEllipsis"
                                           ToolTip="{Binding FilePath}"
                                           Foreground="{StaticResource MutedBrush}"/>
                                <ItemsControl ItemsSource="{Binding StepDescriptions}" Margin="0,6,0,0">
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate>
                                            <TextBlock Text="{Binding}" FontSize="12"
                                                       Foreground="{StaticResource InkBrush}"/>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </StackPanel>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>
    </DockPanel>
</Window>
```

- [ ] **Step 4: Écrire le code-behind et l'adaptateur de consentement**

`src/ScadaBuilderV2.App/Projects/ConversionPlanDialog.xaml.cs`

```csharp
using System.Windows;
using ScadaBuilderV2.Application.Formats;

namespace ScadaBuilderV2.App.Projects;

/// <summary>Shows what a conversion will do and collects the operator's answer.</summary>
/// <remarks>
/// C5 allows two outcomes: convert, or do not open. There is no read-only consultation mode - the generations
/// diverge too much for a half-migrated session to be faithful to what the operator believes they see.
///
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C5, C7.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ConversionDialogContractTests.cs.
/// </remarks>
public partial class ConversionPlanDialog : Window
{
    /// <summary>Gets the operator's answer; `Cancel` unless a button says otherwise.</summary>
    public ConversionDecision Decision { get; private set; } = ConversionDecision.Cancel;

    /// <summary>Creates the dialog for one plan.</summary>
    public ConversionPlanDialog(ConversionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        InitializeComponent();
        PlanItems.ItemsSource = plan.Entries;
    }

    private void OnConvertClick(object sender, RoutedEventArgs e) => Complete(ConversionDecision.Convert);

    private void OnCancelClick(object sender, RoutedEventArgs e) => Complete(ConversionDecision.Cancel);

    private void Complete(ConversionDecision decision)
    {
        Decision = decision;
        DialogResult = decision == ConversionDecision.Convert;
        Close();
    }
}
```

`src/ScadaBuilderV2.App/Projects/WpfConversionConsent.cs`

```csharp
using System.Windows;
using ScadaBuilderV2.Application.Formats;

namespace ScadaBuilderV2.App.Projects;

/// <summary>Presents a conversion plan through the WPF dialog.</summary>
/// <remarks>
/// WPF owns the presentation and nothing else. Whether the question is worth asking belongs to
/// <see cref="ConversionCoordinator"/>.
/// </remarks>
public sealed class WpfConversionConsent(Window owner) : IConversionConsent
{
    /// <inheritdoc />
    public Task<ConversionDecision> RequestAsync(ConversionPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dialog = new ConversionPlanDialog(plan) { Owner = owner };
        dialog.ShowDialog();
        return Task.FromResult(dialog.Decision);
    }
}
```

- [ ] **Step 5: Exécuter le test et vérifier qu'il passe**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ConversionDialogContractTests"`
Expected: PASS, 4 tests.

- [ ] **Step 6: Exécuter la suite complète et committer**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 948/948.

```bash
git add -A
git commit -m "feat: show the conversion plan and offer two outcomes

Convertir, or Ne pas ouvrir. The dialog names each file, its two
generations and what each step changes, and states that the operation does
not undo and that the backup is the only way back.

Cancel is the default decision, so closing the dialog by any other route
does not convert.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: Convertisseur projet 0 → 1, absorbant et supprimant le reniflage

C'est la tâche qui paie le chantier : chaque règle reprise est **retirée** dans le même commit.

**Files:**
- Create: `src/ScadaBuilderV2.Infrastructure/ModernProjects/Converters/ProjectGeneration1Converter.cs`
- Modify: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectMigration.cs` (retrait des règles reprises)
- Modify: `src/ScadaBuilderV2.Domain/Projects/ScadaFormatGeneration.cs` (`Project = 1`)
- Modify: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ProjectWorkspaceRepository.cs` (brancher le coordinateur)
- Test: `tests/ScadaBuilderV2.Tests/Formats/ProjectGeneration1ConverterTests.cs`

**Interfaces:**
- Consumes: `IArtifactConverter`, `ArtifactConverterRegistry` (Task 3), `ConversionCoordinator` (Task 5).
- Produces: `ProjectGeneration1Converter` enregistré pour `(Project, 0 → 1)`.

- [ ] **Step 1: Recenser les règles reprises**

Lire `ModernProjectMigration.cs` et lister, dans le message de commit **et** dans le rapport, chaque règle avec son sort : reprise par le convertisseur et supprimée, ou conservée avec sa raison. Une règle conservée sans raison écrite est un échec de cette tâche.

Point d'attention : `NormalizeIdentity` et `ResolveTargetKey` traitent l'identité de page (`PageKey`, `PageCode`, `Origin`, `ImportProvenance`, `HeaderPageKey`, `FooterPageKey`). Ce sont les règles à reprendre en priorité, parce qu'elles ne s'appliquent qu'aux projets antérieurs à `DEC-0038`.

- [ ] **Step 2: Écrire le test du convertisseur**

`tests/ScadaBuilderV2.Tests/Formats/ProjectGeneration1ConverterTests.cs`

```csharp
using System.Text.Json.Nodes;
using ScadaBuilderV2.Application.Formats;
using ScadaBuilderV2.Infrastructure.ModernProjects.Converters;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Locks the converter that absorbs the page-identity rules `ModernProjectMigration` used to re-derive on
/// every load. Once a project is converted and saved, those rules are dead code and are deleted; that is the
/// gain shape-sniffing can never deliver.
/// </summary>
[TestClass]
public sealed class ProjectGeneration1ConverterTests
{
    private static readonly ProjectGeneration1Converter Converter = new();

    [TestMethod]
    public void ItDeclaresTheStepItCovers()
    {
        Assert.AreEqual(ArtifactModule.Project, Converter.Module);
        Assert.AreEqual(0, Converter.FromVersion);
        Assert.AreEqual(1, Converter.ToVersion);
        Assert.IsFalse(string.IsNullOrWhiteSpace(Converter.StepDescription));
    }

    [TestMethod]
    public void ItStampsTheGenerationItProduces()
    {
        var document = Converter.Convert(JsonNode.Parse("""{"Name":"P","Scenes":[]}""")!);

        Assert.AreEqual(1, document["FormatVersion"]!.GetValue<int>());
    }

    [TestMethod]
    public void APageWithoutAKeyReceivesAStableOne()
    {
        var document = Converter.Convert(JsonNode.Parse("""
        {"Name":"P","Scenes":[{"Id":"win00001","Title":"Accueil","PageCode":"win00001"}]}
        """)!);

        var key = document["Scenes"]![0]!["PageKey"]!.GetValue<string>();
        Assert.AreNotEqual(Guid.Empty.ToString(), key);
        Assert.IsTrue(Guid.TryParse(key, out _));
    }

    /// <summary>Converting twice produces identical bytes: a key derived once is derived the same way again.</summary>
    [TestMethod]
    public void ConversionIsDeterministicAcrossRuns()
    {
        const string source = """{"Name":"P","Scenes":[{"Id":"win00001","PageCode":"win00001"}]}""";

        var first = Converter.Convert(JsonNode.Parse(source)!).ToJsonString();
        var second = Converter.Convert(JsonNode.Parse(source)!).ToJsonString();

        Assert.AreEqual(first, second, "a converted project must not depend on when or where it was converted.");
    }

    [TestMethod]
    public void AnExistingPageKeyIsPreserved()
    {
        const string key = "11111111-2222-3333-4444-555555555555";
        var document = Converter.Convert(JsonNode.Parse($$"""
        {"Name":"P","Scenes":[{"Id":"win00001","PageKey":"{{key}}"}]}
        """)!);

        Assert.AreEqual(key, document["Scenes"]![0]!["PageKey"]!.GetValue<string>());
    }
}
```

- [ ] **Step 3: Exécuter le test et vérifier qu'il échoue**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ProjectGeneration1ConverterTests"`
Expected: échec de compilation.

- [ ] **Step 4: Écrire le convertisseur**

`src/ScadaBuilderV2.Infrastructure/ModernProjects/Converters/ProjectGeneration1Converter.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ScadaBuilderV2.Application.Formats;

namespace ScadaBuilderV2.Infrastructure.ModernProjects.Converters;

/// <summary>Converts `project.json` from generation 0 to 1 by settling page identity once.</summary>
/// <remarks>
/// Before this converter, `ModernProjectMigration` re-derived page identity by sniffing the shape of the data
/// on every load of every project, and could never be retired because nothing recorded that a project had
/// already been migrated. This runs once, stamps the generation, and lets those rules be deleted.
///
/// The key is derived from the page code rather than randomly generated, so converting the same project twice
/// or on two machines produces identical bytes.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C3, C9.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ProjectGeneration1ConverterTests.cs.
/// </remarks>
public sealed class ProjectGeneration1Converter : IArtifactConverter
{
    /// <inheritdoc />
    public ArtifactModule Module => ArtifactModule.Project;

    /// <inheritdoc />
    public int FromVersion => 0;

    /// <inheritdoc />
    public int ToVersion => 1;

    /// <inheritdoc />
    public string StepDescription =>
        "Fixe l'identité durable de chaque page, jusqu'ici recalculée à chaque ouverture.";

    /// <inheritdoc />
    public JsonNode Convert(JsonNode document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document["Scenes"] is JsonArray scenes)
        {
            foreach (var scene in scenes.OfType<JsonObject>())
            {
                var existing = scene["PageKey"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(existing)
                    && Guid.TryParse(existing, out var parsed)
                    && parsed != Guid.Empty)
                {
                    continue;
                }

                var code = scene["PageCode"]?.GetValue<string>()
                    ?? scene["Id"]?.GetValue<string>()
                    ?? "";
                scene["PageKey"] = DeriveKey(code).ToString("D");
            }
        }

        document["FormatVersion"] = ToVersion;
        return document;
    }

    /// <summary>Derives a stable key from a page code so conversion is reproducible.</summary>
    private static Guid DeriveKey(string pageCode)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("scada-v2-page-identity:" + pageCode));
        return new Guid(hash.AsSpan(0, 16));
    }
}
```

- [ ] **Step 5: Exécuter le test et vérifier qu'il passe**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ProjectGeneration1ConverterTests"`
Expected: PASS, 5 tests.

- [ ] **Step 6: Passer la génération courante à 1, brancher le coordinateur, supprimer les règles reprises**

Dans `ScadaFormatGeneration`, `Project = 1`.

`ProjectWorkspaceRepository` reçoit le registre et le coordinateur par constructeur. Dans `OpenAsync`, après le refus vers l'arrière et **avant** `ReadWorkspaceSnapshotFromProjectRootAsync` :

```csharp
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

            foreach (var entry in outcome.Plan.Entries)
            {
                // C6: the backup is the only way back, so it is written before anything is changed.
                ArtifactBackupWriter.CreateBackup(entry.FilePath);

                var document = JsonNode.Parse(await File.ReadAllTextAsync(entry.FilePath, cancellationToken))
                    ?? throw new InvalidDataException($"Document illisible: {entry.FilePath}");
                var converted = registry
                    .ResolveChain(entry.Module, entry.FromVersion, entry.ToVersion)
                    .Apply(document);
                await File.WriteAllTextAsync(
                    entry.FilePath,
                    converted.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                    cancellationToken);
            }
        }
```

Ajouter `using System.Text.Json;`, `using System.Text.Json.Nodes;` et `using ScadaBuilderV2.Application.Formats;`.

Dans `ModernProjectMigration`, **supprimer** `NormalizeIdentity`, `ResolveTargetKey` et `ResolveCode` ainsi que leurs appels dans `MigrateProject`, puisque le convertisseur les a repris. Toute règle conservée reçoit un commentaire disant pourquoi elle l'est.

- [ ] **Step 7: Verrouiller qu'aucun chemin d'activation n'accepte un artefact non converti**

Ajouter à `tests/ScadaBuilderV2.Tests/Formats/BackwardRefusalTests.cs` :

```csharp
    /// <summary>Contract 6.5: refusing the conversion leaves the project closed and untouched.</summary>
    [TestMethod]
    public async Task ARefusedConversionNeitherOpensTheProjectNorTouchesIt()
    {
        var projectPath = WriteProject(formatVersion: 0);
        var before = await File.ReadAllTextAsync(projectPath);
        var registry = new ArtifactConverterRegistry();
        registry.Register(new ProjectGeneration1Converter());
        var repository = new ProjectWorkspaceRepository(
            new ModernProjectStore(),
            new ReferenceProjectCompatibilityLocator(),
            registry,
            new ConversionCoordinator(registry, new DecliningConsent()));

        var result = await repository.OpenAsync(projectPath);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Candidate, "no candidate may be prepared from an unconverted artifact.");
        Assert.AreEqual("project.conversion-declined", result.Diagnostics.Single().Code);
        Assert.AreEqual(before, await File.ReadAllTextAsync(projectPath));
        Assert.IsFalse(File.Exists(projectPath + ".bak"), "a refused conversion writes no backup either.");
    }

    private sealed class DecliningConsent : IConversionConsent
    {
        public Task<ConversionDecision> RequestAsync(ConversionPlan plan, CancellationToken cancellationToken)
            => Task.FromResult(ConversionDecision.Cancel);
    }
```

Ajouter `using ScadaBuilderV2.Application.Formats;` et `using ScadaBuilderV2.Infrastructure.ModernProjects.Converters;`.

- [ ] **Step 8: Exécuter la suite complète**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 954/954. Les tests de `ModernProjectStoreTests` et `PageIdentityTests` qui couvraient les règles supprimées doivent être **déplacés** vers les tests du convertisseur, jamais supprimés sans remplacement.

- [ ] **Step 9: Documenter et committer**

Mettre à jour `PROJECT_MODEL_CONTRACT_V2.md`, `KNOWN_GAPS_V2.md`, `REGRESSION_COVERAGE_V2.md`, `DECISION_REGISTER_V2.md`, `docs/README.md` et `VERSION`.

Run: `powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1`
Expected: `Errors: 0`, 121 avertissements.

```bash
git add -A
git commit -m "feat: convert project.json to generation 1 and delete the rules it absorbs

The page-identity rules ModernProjectMigration re-derived on every load of
every project now run once, at conversion, and are deleted here. That is the
gain shape-sniffing can never deliver: a rule that runs once can be retired,
a rule that guesses cannot.

The key is derived from the page code rather than generated, so converting
the same project twice or on two machines produces identical bytes.

Rules kept rather than absorbed are listed with their reason.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 8: Retirer à `ManifestVersion` son second rôle

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs` (validation de contenu)
- Modify: `docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md`
- Test: `tests/ScadaBuilderV2.Tests/Formats/ManifestVersionResponsibilityTests.cs`

**Interfaces:**
- Consumes: `ScadaRuntimeCapabilityCatalog` (existant).
- Produces: aucun type nouveau. Cette tâche **retire** une responsabilité.

- [ ] **Step 1: Écrire le test**

`tests/ScadaBuilderV2.Tests/Formats/ManifestVersionResponsibilityTests.cs`

```csharp
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.RuntimeContracts;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// `ManifestVersion` served two roles: the export profile negotiated with TF100Web, and a declaration of what
/// a project was allowed to contain. The second is removed. The capability catalog is already the authority
/// on allowed content - it carries status, three-layer evidence and the fail-closed gate - and a string
/// comparison was never going to say the same thing.
/// </summary>
[TestClass]
public sealed class ManifestVersionResponsibilityTests
{
    [TestMethod]
    public void QuickWindowsAreAllowedBecauseTheirCapabilityIsSupported()
    {
        Assert.AreEqual(
            ScadaRuntimeCapabilityStatus.Supported,
            ScadaRuntimeCapabilityCatalog.QuickWindowDefinition.Status,
            "this is what authorises quick-window content, not the string \"2.3\".");
    }

    [TestMethod]
    public void ContentValidationNoLongerReadsTheManifestVersionString()
    {
        var source = ReadSource("src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs");
        var validation = Between(source, "ValidateQuickWindows(", "private static void Validate");

        Assert.IsFalse(
            validation.Contains("ManifestVersion", StringComparison.Ordinal),
            "content validation belongs to the capability catalog.");
    }

    private static string Between(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        Assert.IsTrue(from >= 0, $"'{start}' not found.");
        var to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        return to > from ? source[from..to] : source[from..];
    }

    private static string ReadSource(string relativePath)
    {
        var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = System.IO.Path.Combine(directory.FullName, relativePath);
            if (System.IO.File.Exists(candidate)) return System.IO.File.ReadAllText(candidate);
            directory = directory.Parent;
        }
        Assert.Fail($"Unable to locate {relativePath}.");
        return string.Empty;
    }
}
```

- [ ] **Step 2: Exécuter le test et vérifier qu'il échoue**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ManifestVersionResponsibilityTests"`
Expected: FAIL sur le second test — `ValidateQuickWindows` lit encore `ManifestVersion`.

- [ ] **Step 3: Remplacer le contrôle de chaîne par un contrôle de capacité**

Dans `ProjectModels.cs`, remplacer toute comparaison de `ManifestVersion` servant à autoriser du contenu par une interrogation du catalogue. `ManifestVersion` conserve son nom et son rôle de profil d'export; seule l'autorisation de contenu change de source.

- [ ] **Step 4: Exécuter le test et vérifier qu'il passe**

Run: `dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ManifestVersionResponsibilityTests"`
Expected: PASS, 2 tests.

- [ ] **Step 5: Suite complète, documentation, commit**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: 955/955.

Run: `powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1`
Expected: `Errors: 0`, 121 avertissements.

```bash
git add -A
git commit -m "refactor: take the content role away from ManifestVersion

The field served two purposes: the export profile negotiated with TF100Web,
and a declaration of what a project was allowed to contain. It keeps the
first and loses the second.

The capability catalog is already the authority on allowed content - it
carries the status, the three-layer evidence and the fail-closed gate - and
a string comparison against \"2.3\" was never going to say the same thing.
Quick-window content is authorised because quick-window.definition is
Supported.

No new field is introduced: this removes a responsibility.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Clôture du chantier

- [ ] Rapport `docs/superpowers/reports/<date>-format-versioning-audit.md` : ce qui a été absorbé et supprimé de `ModernProjectMigration`, ce qui a été conservé et pourquoi, l'état des générations par module.
- [ ] Vérifier que les fixtures gelées, les paquets de conformance et les preuves industrielles sont **inchangés**. Une dérive est un échec, jamais un effet attendu.
- [ ] `verify-docs` à `Errors: 0` et 121 avertissements.
- [ ] Suite complète verte, sans skip.
- [ ] `python tools/docs/resolve-pending-commits.py --check` à zéro.
