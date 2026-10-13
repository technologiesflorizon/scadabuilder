# Interverrouillage des commandes Toggle - Plan d'implémentation

Date: 2026-10-13
Status: Active - plan d'implémentation (DEC-0054)
Document version: `V2.1.6.0072`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-10-13 | `V2.1.6.0072` | `PENDING` | Ajout des métadonnées documentaires standard au plan (exigées par `verify-docs.ps1`). |


> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ajouter aux commandes `WriteTag` en mode `Toggle` un champ optionnel `InterlockTagId` : quand le clic HMI fait passer le tag d'écriture à 1, le tag d'interverrouillage est d'abord écrit à 0, et le 1 n'est écrit que si cette première écriture réussit.

**Architecture:** Un champ `string? InterlockTagId` sur `ScadaCommandBinding`, validé au build, édité dans `ElementCommandDialog`, exporté en `interlockTagId` dans `data-scada-command-config` et le manifeste seulement s'il est renseigné, substitué `#N` → tag dans les fenêtres rapides, exécuté par `command-dispatcher.js` (runtime partagé livré dans le paquet). La nouvelle capacité `command.write.toggle-interlock` est exigée seulement si utilisée ; TF100Web l'ajoute à ses capacités supportées et la prouve par une sonde de conformité.

**Tech Stack:** .NET 8 / MSTest (Builder), JavaScript ES5 du runtime + `node:test` (Builder et TF100Web), Django 4 / unittest sous WSL (TF100Web).

**Spec:** `docs/superpowers/specs/2026-10-13-toggle-interlock-design.md`

## Global Constraints

- Dépôt Builder : `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`, branche `feature/toggle-interlock` (déjà créée, contient la spec `0cfa009`).
- Dépôt TF100Web : worktree `F:\Projet\Git\TF100Web-qw`, nouvelle branche `feature/toggle-interlock-capability` partie de `main` (`7543cd3`).
- Nom du champ : `InterlockTagId` (C#, manifeste PascalCase), `interlockTagId` (JSON runtime camelCase dans les attributs HTML).
- Nom de la capacité : `command.write.toggle-interlock`, propriétaire `SharedRuntime`, statut `Supported` (preuves Builder + runtime JS + TF100Web, Tasks 3, 4, 6, 7).
- Diagnostic runtime d'échec de l'interverrouillage : `interlock-write-failed`. Métadonnées de l'écriture d'interverrouillage : `mode: 'ToggleInterlock'`.
- Un projet sans interverrouillage doit produire un manifeste et un DOM identiques octet pour octet à aujourd'hui (seul `RuntimeSha256` dérive, car le runtime change).
- Pas de changement de `ManifestVersion` ni de version de contrat runtime (`1.0`).
- Documentation : changelogs et décisions en **français**, code/identifiants/XML docs en anglais. `VERSION` passe à `V2.1.6.0072`. Hashes de commit inconnus notés `PENDING`, résolus par `python tools/docs/resolve-pending-commits.py --apply`. `tools/docs/verify-docs.ps1` doit rester à 0 erreur.
- Fixtures gelées régénérées avec `SCADA_UPDATE_CONFORMANCE`, `SCADA_UPDATE_PARAMETER_SHEET_E2E`, `SCADA_UPDATE_QUICK_WINDOW_PARAMETER_SHEET`, `SCADA_UPDATE_QUICK_WINDOW_HANDSHAKE`, `SCADA_UPDATE_QUICK_WINDOW_SOAK` ; **jamais** `SCADA_UPDATE_INDUSTRIAL_EVIDENCE`.
- Suites Django TF100Web : sous WSL, venv `/home/florizon/.venvs/tf100web`, réglages SQLite hors dépôt et `SCADA_BUILDER_PROJECTS_ROOT` vide (sinon 7 faux échecs). Tests JS TF100Web depuis Windows : `node --test frontend/tests_runtime_js/*.test.mjs`.
- Commits : un par tâche, message en anglais, se terminant par
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_012JgfWRuzEkPYzJQfGw3HfA
  ```

## Review Focus

1. **Écriture d'interverrouillage asynchrone qui échoue** (promesse rejetée ou résolue à `false`, le cas réel de TF100Web) : le `1` ne doit jamais partir. Testé Task 4.
2. **Double clic pendant la séquence** (TF100Web met ~100 ms par écriture) : aucune seconde séquence, aucune écriture supplémentaire. Testé Task 4.
3. **`#N` d'interverrouillage lié à un littéral ou une expression dans une feuille de paramètres** : refusé au build (`parameter-sheet.write-not-tag`) et au montage (`placeholder-write-not-tag`), jamais un bouton à moitié configuré. Testé Tasks 2 et 4.
4. **Copier-coller d'un bouton de fenêtre rapide vers une page** : l'`InterlockTagId` `#N` est signalé et retiré comme le `WriteTagId`, sinon il reste un `#N` orphelin sur une page normale. Testé Task 2.
5. **Passage du mode Toggle à un autre mode dans l'éditeur** : l'interverrouillage est vidé, et un projet modifié à la main qui garde un `InterlockTagId` hors Toggle est une erreur de build. Testé Tasks 1 et 5.

---

## File Structure

**Builder — modifiés**
- `src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBinding.cs` — nouveau paramètre `InterlockTagId`.
- `src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBindingValidator.cs` — contrôle libellé/Id canonique étendu.
- `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs` — `ScadaProjectBuildValidator.ValidateSceneCommandBindings` : nouvelles erreurs `command.interlock-*`.
- `src/ScadaBuilderV2.Application/QuickWindows/QuickWindowPlaceholderUsageCollector.cs` — site `CommandInterlockTag`.
- `src/ScadaBuilderV2.Application/QuickWindows/ParameterSheetLowering.cs` — `IsWrite` inclut le nouveau site.
- `src/ScadaBuilderV2.Application/QuickWindows/QuickWindowClipboardValidator.cs` — collecte et retrait de l'`InterlockTagId`.
- `src/ScadaBuilderV2.App/QuickWindows/QuickWindowInterfaceViewModel.cs` — libellé du site.
- `src/ScadaBuilderV2.Domain/RuntimeContracts/ScadaRuntimeCapabilityCatalog.cs` — capacité `CommandToggleInterlock`.
- `src/ScadaBuilderV2.Application/RuntimeContracts/ScadaRuntimeCapabilityAnalyzer.cs` — exige la capacité si utilisée.
- `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs` — projection typée de la commande, `InterlockTagId` omis si nul.
- `src/ScadaBuilderV2.Rendering/Runtime/command-dispatcher.js` — séquence interverrouillage → écriture.
- `src/ScadaBuilderV2.Rendering/Runtime/quick-window-runtime.js` — substitution `#N` de `interlockTagId`.
- `src/ScadaBuilderV2.App/ElementCommandDialog.xaml` et `.xaml.cs` — liste « Interverrouillage (optionnel) ».
- `tests/ScadaBuilderV2.Tests/RuntimeContracts/ScadaV2RuntimeConformanceProjectFactory.cs` — commande `write-toggle-interlock`.
- `tests/ScadaBuilderV2.Tests/RuntimeContracts/RuntimeConformancePackageTests.cs` — 126 → 127.
- Docs : `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`, `docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md`, `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`, `docs/00_governance/DECISION_REGISTER_V2.md`, `docs/README.md`, `VERSION`, la spec, `docs/10_generated/` (régénéré).

**Builder — créés**
- `tests/ScadaBuilderV2.Tests/ElementEvents/ToggleInterlockValidationTests.cs`
- `tests/ScadaBuilderV2.Tests/ToggleInterlockExportTests.cs`
- `tests/runtime-js/command-dispatcher-interlock.test.mjs`

**TF100Web — modifiés**
- `frontend/scada_package.py` — `SUPPORTED_SCADA_RUNTIME_CAPABILITIES`.
- `frontend/tests_runtime_conformance.py` — 126 → 127.
- `frontend/tests_runtime_js/runtime-conformance-harness.mjs` — sonde.
- `frontend/test_fixtures/*` — fixtures re-vendorisées.

---

### Task 0: Contrats et décision avant le code

**Files:**
- Modify: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`
- Modify: `docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md:142-146`
- Modify: `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md` (règle 16)
- Modify: `docs/00_governance/DECISION_REGISTER_V2.md`
- Modify: `docs/superpowers/specs/2026-10-13-toggle-interlock-design.md` (statut, historique)
- Modify: `docs/README.md`, `VERSION`

**Interfaces:**
- Produces: décision `DEC-0054` citée dans les `<remarks>` de toutes les tâches suivantes.

- [ ] **Step 1: Vérifier le prochain numéro de décision**

Run: `grep -n "^### DEC-" docs/00_governance/DECISION_REGISTER_V2.md | tail -3`
Expected: la dernière entrée est `DEC-0053`. Si ce n'est pas le cas, utiliser le numéro suivant partout où ce plan écrit `DEC-0054`.

- [ ] **Step 2: Ajouter `DEC-0054` au registre**

Ajouter à la fin de `docs/00_governance/DECISION_REGISTER_V2.md`, en suivant le format de `DEC-0053` (titre `###`, puis statut, date, contexte, décision, conséquences) :

```markdown
### DEC-0054 - Interverrouillage des commandes Toggle

- Statut : Accepted
- Date : 2026-10-13
- Spec : `docs/superpowers/specs/2026-10-13-toggle-interlock-design.md`

Contexte : sur le site AMR, Départ/Arrêt et Auto/Man sont des paires de tags distincts pilotées par des boutons Toggle ; activer l'un ne désactive pas l'autre.

Décision : une commande `WriteTag` en mode `Toggle` peut porter un `InterlockTagId` optionnel. Quand le clic HMI fait passer le tag d'écriture à 1, le runtime écrit d'abord 0 sur le tag d'interverrouillage, puis 1 sur le tag d'écriture seulement si la première écriture est acceptée. Seul le clic déclenche l'interverrouillage (jamais un changement venu de l'automate). Le comportement est négocié par la capacité `command.write.toggle-interlock`, exigée seulement si une commande l'utilise.

Conséquences : TF100Web doit accepter la capacité avant tout paquet qui l'utilise ; un paquet sans interverrouillage reste identique et accepté par un TF100Web antérieur.
```

- [ ] **Step 3: Mettre à jour les trois contrats**

Dans `docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md`, remplacer la puce `WriteTag` + `Toggle` (lignes 142-143) par :

```markdown
- `WriteTag` + `Toggle`: read current value of `readTagId` (fallback: `writeTagId`), write
  its logical negation to `writeTagId`. When the negation is `1` and `interlockTagId` is set
  (`DEC-0054`), first write `0` to `interlockTagId` (metadata `mode: 'ToggleInterlock'`), then
  write `1` to `writeTagId` only if that write was accepted; otherwise report
  `interlock-write-failed` and write nothing else. The whole sequence is one in-flight command.
```

Dans `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`, compléter la règle 16 par la phrase : « Une commande Toggle peut porter un `InterlockTagId` (`DEC-0054`) : réservé au mode Toggle, tag existant, actif, accessible en écriture, booléen, distinct des tags d'écriture et de lecture ; un `#N` est accepté dans une page Fenêtre rapide et compte comme un site d'écriture. »

Dans `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`, ajouter une règle numérotée après la dernière règle de la section des commandes runtime :

```markdown
NN. `data-scada-command-config` and manifest `CommandConfig` carry `interlockTagId` / `InterlockTagId` only on a Toggle command that sets it (`DEC-0054`); the property is absent otherwise, so packages without an interlock are byte-identical to earlier exports. A package that uses it requires runtime capability `command.write.toggle-interlock`, which TF100Web must list in `SUPPORTED_SCADA_RUNTIME_CAPABILITIES` before such a package is deployed. Quick-window `#N` substitution treats `interlockTagId` as a write site.
```

(`NN` = numéro suivant dans la liste existante.)

- [ ] **Step 4: Versions et changelogs**

Écrire `V2.1.6.0072` dans `VERSION`. Dans chacun des fichiers touchés (les trois contrats, le registre, la spec, `docs/README.md`), passer `Document version:` à `` `V2.1.6.0072` `` et ajouter en tête de la table « Historique des changements » :

```markdown
| 2026-10-13 | `V2.1.6.0072` | `PENDING` | `DEC-0054` : interverrouillage optionnel des commandes Toggle (`InterlockTagId`, capacité `command.write.toggle-interlock`). Contrat mis à jour avant l'implémentation. |
```

Dans la spec, passer `Status:` à `Approved - DEC-0054`.

- [ ] **Step 5: Vérifier la documentation**

Run (PowerShell): `powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1 2>&1 | Select-String "Errors:"`
Expected: `Errors: 0`

- [ ] **Step 6: Commit, puis résoudre les PENDING**

```bash
git add VERSION docs
git commit -m "docs: DEC-0054 toggle interlock contract before implementation" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -m "Claude-Session: https://claude.ai/code/session_012JgfWRuzEkPYzJQfGw3HfA"
python tools/docs/resolve-pending-commits.py --apply
git add docs && git commit -m "docs: resolve PENDING placeholders for DEC-0054" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -m "Claude-Session: https://claude.ai/code/session_012JgfWRuzEkPYzJQfGw3HfA"
```

---

### Task 1: Modèle et validation de build

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBinding.cs`
- Modify: `src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBindingValidator.cs`
- Modify: `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs` (`ValidateSceneCommandBindings`)
- Create: `tests/ScadaBuilderV2.Tests/ElementEvents/ToggleInterlockValidationTests.cs`

**Interfaces:**
- Produces: `ScadaCommandBinding.InterlockTagId` (`string?`, dernier paramètre, défaut `null`) ; codes d'erreur `command.interlock-requires-toggle`, `command.interlock-tag-missing`, `command.interlock-tag-readonly`, `command.interlock-tag-not-boolean`, `command.interlock-same-tag` (sévérité `Error`, `PropertyPath` = `Scene.Elements[{elementId}].CommandConfig.Commands[{commandId}].InterlockTagId`).

- [ ] **Step 1: Écrire les tests qui échouent**

Créer `tests/ScadaBuilderV2.Tests/ElementEvents/ToggleInterlockValidationTests.cs` :

```csharp
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.ElementEvents;

/// <summary>
/// Locks the build validation of a Toggle command interlock (DEC-0054): reserved to Toggle, an existing, enabled,
/// writeable, boolean tag, distinct from the write and read tags; a #N is exempt from catalog checks.
/// </summary>
[TestClass]
public sealed class ToggleInterlockValidationTests
{
    private const string PageId = "win00001";

    private static ScadaTagCatalog Catalog() => new("tf100web-scada-tags-v1", new[]
    {
        new ScadaTagDefinition("tag.start", "Depart", Datatype: "Booléen", Writeable: true),
        new ScadaTagDefinition("tag.stop", "Arret", Datatype: "Booléen", Writeable: true),
        new ScadaTagDefinition("tag.feedback", "Retour", Datatype: "Booléen", Writeable: false),
        new ScadaTagDefinition("tag.speed", "Vitesse", Datatype: "Float", Writeable: true),
        new ScadaTagDefinition("tag.disabled", "Inactif", Datatype: "Booléen", Writeable: true, Enabled: false),
    });

    private static ScadaCommandBinding Toggle(string? interlock, string write = "tag.start", string? read = null,
        ScadaWriteMode mode = ScadaWriteMode.Toggle) =>
        new("cmd", "Depart", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
            WriteTagId: write, ReadTagId: read, WriteMode: mode, FixedValue: mode == ScadaWriteMode.SetFixed ? "1" : null,
            InterlockTagId: interlock);

    private static IReadOnlyList<ScadaBuildValidationIssue> Validate(ScadaCommandBinding command)
    {
        var scene = ScadaScene.CreateEmpty(PageId, "Page", new CanvasSize(400, 200))
            .WithElement(ScadaElement.CreateButton("btn", "Depart", 0, 0) with
            {
                CommandConfig = new ScadaElementCommandConfig(new[] { command })
            });
        var project = ScadaProject.CreateDefault("P") with
        {
            Scenes = new[] { new ScadaSceneReference(PageId, "Page", $"scenes/{PageId}.scene.json") },
            TagCatalog = Catalog()
        };
        return ScadaProjectBuildValidator.Validate(project, new[] { scene })
            .Where(issue => issue.Code.StartsWith("command.interlock-", StringComparison.Ordinal))
            .ToArray();
    }

    [TestMethod]
    public void AValidInterlockRaisesNoIssue()
    {
        Assert.AreEqual(0, Validate(Toggle("tag.stop")).Count);
    }

    [TestMethod]
    public void NoInterlockRaisesNoIssue()
    {
        Assert.AreEqual(0, Validate(Toggle(null)).Count);
    }

    [TestMethod]
    public void AnInterlockOutsideToggleIsAnError()
    {
        var issue = Validate(Toggle("tag.stop", mode: ScadaWriteMode.SetFixed)).Single();
        Assert.AreEqual("command.interlock-requires-toggle", issue.Code);
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        Assert.AreEqual("Scene.Elements[btn].CommandConfig.Commands[cmd].InterlockTagId", issue.PropertyPath);
    }

    [TestMethod]
    public void AnUnknownOrDisabledInterlockTagIsAnError()
    {
        Assert.AreEqual("command.interlock-tag-missing", Validate(Toggle("tag.unknown")).Single().Code);
        Assert.AreEqual("command.interlock-tag-missing", Validate(Toggle("tag.disabled")).Single().Code);
    }

    [TestMethod]
    public void AReadOnlyInterlockTagIsAnError()
    {
        Assert.AreEqual("command.interlock-tag-readonly", Validate(Toggle("tag.feedback")).Single().Code);
    }

    [TestMethod]
    public void ANonBooleanInterlockTagIsAnError()
    {
        Assert.AreEqual("command.interlock-tag-not-boolean", Validate(Toggle("tag.speed")).Single().Code);
    }

    [TestMethod]
    public void AnInterlockEqualToTheWriteOrReadTagIsAnError()
    {
        Assert.AreEqual("command.interlock-same-tag", Validate(Toggle("tag.start")).Single().Code);
        Assert.AreEqual("command.interlock-same-tag", Validate(Toggle("tag.stop", read: "tag.stop")).Single().Code);
    }

    [TestMethod]
    public void APlaceholderInterlockIsExemptFromCatalogChecksButNotFromTheSameTagRule()
    {
        Assert.AreEqual(0, Validate(Toggle("#4", write: "#3")).Count);
        Assert.AreEqual("command.interlock-same-tag", Validate(Toggle("#3", write: "#3")).Single().Code);
    }

    [TestMethod]
    public void AHumanLabelAsInterlockIsReportedLikeAWriteTag()
    {
        var issues = ScadaCommandBindingValidator.ValidateCommandBinding(Toggle("Arret"), Catalog());
        Assert.AreEqual(1, issues.Count);
        StringAssert.Contains(issues[0], "InterlockTagId");
        StringAssert.Contains(issues[0], "tag.stop");
    }
}
```

- [ ] **Step 2: Vérifier l'échec**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ToggleInterlockValidationTests"`
Expected: échec de compilation, `ScadaCommandBinding` n'a pas de paramètre `InterlockTagId`.

- [ ] **Step 3: Ajouter le champ au modèle**

Dans `ScadaCommandBinding.cs`, ajouter le paramètre en dernière position, après `Guid? QuickWindowPageKey = null` :

```csharp
    Guid? QuickWindowPageKey = null,
    string? InterlockTagId = null)
```

Et compléter le `<remarks>` du record : `Decisions: DEC-0036, DEC-0054.` et une ligne `InterlockTagId: Toggle only; written to 0 before WriteTagId is written to 1 (docs/superpowers/specs/2026-10-13-toggle-interlock-design.md).`

- [ ] **Step 4: Étendre le contrôle libellé/Id canonique**

Dans `ScadaCommandBindingValidator.ValidateCommandBinding`, après la ligne `CheckTagId(command.ReadTagId, ...)` :

```csharp
        CheckTagId(command.InterlockTagId, "InterlockTagId", command.Name, catalog, tagIds, issues);
```

et ajouter `DEC-0054` à la ligne `Decisions:` de la classe.

- [ ] **Step 5: Ajouter la validation de build**

Dans `ProjectModels.cs`, `ValidateSceneCommandBindings`, juste après l'en-tête `foreach (var cmd in element.EffectiveCommandConfig.Commands)` et avant `if (cmd.Kind == ...Navigate)`, ajouter :

```csharp
                ValidateCommandInterlock(issues, scene, element, cmd, catalog);
```

Puis ajouter la méthode juste après `ValidateSceneCommandBindings` :

```csharp
    /// <summary>
    /// Validates the optional interlock of a Toggle command: reserved to Toggle, an existing, enabled, writeable,
    /// boolean tag, distinct from the write and read tags. A quick-window #N is exempt from the catalog checks
    /// (its tag is only known once a parameter sheet binds it) but not from the same-tag rule.
    /// </summary>
    /// <remarks>
    /// Decisions: DEC-0054.
    /// Contracts: docs/superpowers/specs/2026-10-13-toggle-interlock-design.md §6.
    /// Tests: tests/ScadaBuilderV2.Tests/ElementEvents/ToggleInterlockValidationTests.cs.
    /// </remarks>
    private static void ValidateCommandInterlock(
        List<ScadaBuildValidationIssue> issues,
        ScadaScene scene,
        ScadaElement element,
        ElementEvents.Command.ScadaCommandBinding command,
        ScadaTagCatalog? catalog)
    {
        var interlock = command.InterlockTagId;
        if (string.IsNullOrWhiteSpace(interlock))
            return;

        void Add(string code, string message, string fix) => issues.Add(new ScadaBuildValidationIssue(
            ScadaBuildValidationSeverity.Error,
            code,
            $"Scene '{scene.Id}', element '{element.Id}', commande '{command.Name}' : {message}",
            scene.EffectivePageCode,
            scene.PageKey,
            element.Id,
            command.Id,
            $"Scene.Elements[{element.Id}].CommandConfig.Commands[{command.Id}].InterlockTagId",
            SuggestedFix: fix));

        if (command.Kind != ElementEvents.Command.ScadaCommandKind.WriteTag ||
            command.WriteMode != ElementEvents.Command.ScadaWriteMode.Toggle)
        {
            Add("command.interlock-requires-toggle",
                "l'interverrouillage n'est valable qu'en mode Toggle.",
                "Passez la commande en mode Toggle ou retirez l'interverrouillage.");
            return;
        }

        if (string.Equals(interlock, command.WriteTagId, StringComparison.Ordinal) ||
            string.Equals(interlock, command.ReadTagId, StringComparison.Ordinal))
        {
            Add("command.interlock-same-tag",
                $"le tag d'interverrouillage '{interlock}' est aussi le tag d'écriture ou de lecture : la commande s'annulerait elle-même.",
                "Choisissez le tag de la commande opposée (Arrêt pour Départ, Man pour Auto).");
            return;
        }

        if (QuickWindows.QuickWindowPlaceholder.TryParseName(interlock, out _))
            return;

        var tag = catalog?.Tags?.FirstOrDefault(t => string.Equals(t.Id, interlock, StringComparison.Ordinal));
        if (tag is null || !tag.Enabled)
        {
            Add("command.interlock-tag-missing",
                $"le tag d'interverrouillage '{interlock}' est absent du catalogue ou désactivé.",
                "Sélectionnez un tag actif du catalogue.");
            return;
        }

        if (!tag.Writeable)
        {
            Add("command.interlock-tag-readonly",
                $"le tag d'interverrouillage '{interlock}' n'est pas accessible en écriture.",
                "Sélectionnez un tag accessible en écriture.");
            return;
        }

        if (!IsBooleanDatatype(tag.Datatype))
        {
            Add("command.interlock-tag-not-boolean",
                $"le tag d'interverrouillage '{interlock}' n'est pas booléen ({tag.Datatype}).",
                "Sélectionnez un tag booléen.");
        }
    }
```

Si `QuickWindows.QuickWindowPlaceholder` ne se résout pas depuis ce fichier, ajouter `using ScadaBuilderV2.Domain.QuickWindows;` en tête et écrire `QuickWindowPlaceholder.TryParseName(...)`.

- [ ] **Step 6: Vérifier**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ToggleInterlockValidationTests"`
Expected: 9 tests PASS.

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~QuickWindowBindingTests|FullyQualifiedName~ScadaElementCommandConfigTests"`
Expected: PASS (aucune régression sur la validation des commandes).

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.Domain tests/ScadaBuilderV2.Tests/ElementEvents/ToggleInterlockValidationTests.cs
git commit -m "feat(commands): optional InterlockTagId on Toggle commands, validated at build" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -m "Claude-Session: https://claude.ai/code/session_012JgfWRuzEkPYzJQfGw3HfA"
```

---

### Task 2: Fenêtres rapides — site d'écriture `CommandInterlockTag` et presse-papiers

**Files:**
- Modify: `src/ScadaBuilderV2.Application/QuickWindows/QuickWindowPlaceholderUsageCollector.cs:8-24,266-270`
- Modify: `src/ScadaBuilderV2.Application/QuickWindows/ParameterSheetLowering.cs:710-711`
- Modify: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowInterfaceViewModel.cs:543-544`
- Modify: `src/ScadaBuilderV2.Application/QuickWindows/QuickWindowClipboardValidator.cs:273-283,390-396`
- Test: `tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetLoweringTests.cs`, `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowClipboardTests.cs`

**Interfaces:**
- Consumes: `ScadaCommandBinding.InterlockTagId` (Task 1).
- Produces: `PlaceholderUsageSite.CommandInterlockTag`.

- [ ] **Step 1: Écrire les tests qui échouent**

Dans `ParameterSheetLoweringTests.cs`, ajouter (à côté de `AWriteToANonTagIsAnError`) :

```csharp
    [TestMethod]
    public void AnInterlockPlaceholderIsCollectedAsACommandInterlockWriteSite()
    {
        var scene = ScadaScene.CreateEmpty("qw", "qw", new CanvasSize(400, 200)) with
        {
            Elements = [InterlockCommandElement("start", "#2", "#5")]
        };

        var (usages, invalid) = QuickWindowPlaceholderUsageCollector.Collect(scene);

        Assert.AreEqual(0, invalid.Count);
        CollectionAssert.Contains(usages.ToArray(), new PlaceholderUsage(5, PlaceholderUsageSite.CommandInterlockTag, "start", "start-cmd"));
    }

    [TestMethod]
    public void AnInterlockBoundToANonTagIsAWriteNotTagError()
    {
        var result = Lower(
            "#1 := {FAN_03_RUN}\n#2 := {FAN_03_START}\n#3 := {FAN_03_SPEED}\n#4 := \"Ventilateur 3\"\n#5 := \"pas un tag\"",
            [.. FanPageElements(), InterlockCommandElement("stop", "#1", "#5")]);

        var issue = Single(result, "parameter-sheet.write-not-tag");
        StringAssert.Contains(issue.Message, "#5");
    }
```

et l'aide, à côté de `WriteCommandElement` :

```csharp
    private static ScadaElement InterlockCommandElement(string id, string writeTagId, string interlockTagId) =>
        ScadaElement.CreateButton(id, "Marche", 0, 0) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding($"{id}-cmd", "Marche", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
                    WriteTagId: writeTagId, WriteMode: ScadaWriteMode.Toggle, InterlockTagId: interlockTagId)
            ])
        };
```

Dans `QuickWindowClipboardTests.cs`, ajouter :

```csharp
    [TestMethod]
    public void AnInterlockPlaceholderPastedOnAPageIsRefusedAndStrippedLikeTheWriteTag()
    {
        var element = ScadaElement.CreateButton("start", "Depart", 0, 0) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding("cmd", "Depart", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
                    WriteTagId: "motor.run", WriteMode: ScadaWriteMode.Toggle, InterlockTagId: "#4")
            ])
        };

        var target = QuickWindowClipboardTarget.ForPage();
        var analysis = QuickWindowClipboardValidator.Analyze([element], target, Project());

        Assert.IsTrue(analysis.Issues.Any(issue => issue.PropertyPath == "CommandConfig.Commands[cmd].InterlockTagId"),
            string.Join("; ", analysis.Issues.Select(issue => issue.PropertyPath)));
        var stripped = QuickWindowClipboardValidator.StripRefusedReferences([element], analysis).Single();
        Assert.IsNull(stripped.EffectiveCommandConfig.Commands.Single().InterlockTagId);
    }
```

Avant d'écrire ce test, lire `QuickWindowClipboardTests.cs` lignes 28-120 pour confirmer : le nom de la propriété du chemin dans `QuickWindowClipboardIssue` (ici supposé `PropertyPath`), qu'un `#N` collé sur une page est bien refusé, et que `Project()` existe (utilisé ligne 223). Si le chemin s'appelle autrement, adapter les deux assertions sans changer leur intention.

- [ ] **Step 2: Vérifier l'échec**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ParameterSheetLoweringTests|FullyQualifiedName~QuickWindowClipboardTests"`
Expected: échec de compilation, `PlaceholderUsageSite.CommandInterlockTag` n'existe pas.

- [ ] **Step 3: Ajouter le site et le collecter**

Dans `QuickWindowPlaceholderUsageCollector.cs`, enum `PlaceholderUsageSite`, après `CommandReadTag` :

```csharp
    /// <summary>The tag a Toggle command writes to 0 before writing its own tag to 1 (DEC-0054).</summary>
    CommandInterlockTag,
```

et dans la boucle des commandes, après la ligne `CommandReadTag` :

```csharp
            AddExact(usages, command.InterlockTagId, PlaceholderUsageSite.CommandInterlockTag, id, command.Id);
```

- [ ] **Step 4: Le traiter comme une écriture**

Dans `ParameterSheetLowering.cs` :

```csharp
    private static bool IsWrite(PlaceholderUsageSite site) =>
        site is PlaceholderUsageSite.WriteTag or PlaceholderUsageSite.CommandWriteTag or PlaceholderUsageSite.CommandInterlockTag;
```

Dans `QuickWindowInterfaceViewModel.cs`, dans le `switch` des libellés, après `CommandReadTag` :

```csharp
        PlaceholderUsageSite.CommandInterlockTag => "Commande (interverrouillage)",
```

- [ ] **Step 5: Presse-papiers**

Dans `QuickWindowClipboardValidator.cs`, `CollectTagReferences`, dans la boucle des commandes, après le `yield return` du `ReadTagId` :

```csharp
            if (!string.IsNullOrWhiteSpace(command.InterlockTagId))
                yield return ($"CommandConfig.Commands[{command.Id}].InterlockTagId", command.InterlockTagId!);
```

et dans `StripRefusedReferences`, projection des commandes :

```csharp
                var interlockPath = $"CommandConfig.Commands[{command.Id}].InterlockTagId";
                return command with
                {
                    WriteTagId = paths.Contains(writePath) ? null : command.WriteTagId,
                    ReadTagId = paths.Contains(readPath) ? null : command.ReadTagId,
                    InterlockTagId = paths.Contains(interlockPath) ? null : command.InterlockTagId,
                    QuickWindowInvocationKey = paths.Contains(invocationPath) ? null : command.QuickWindowInvocationKey
                };
```

- [ ] **Step 6: Vérifier**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~QuickWindows"`
Expected: PASS, y compris les trois nouveaux tests. Si des tests `QuickWindowIsolation*` échouent avec un artefact manquant, vérifier que `artifacts/quick-window-isolation` existe dans ce checkout (problème connu des worktrees neufs, sans lien avec cette tâche).

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.Application/QuickWindows src/ScadaBuilderV2.App/QuickWindows/QuickWindowInterfaceViewModel.cs tests/ScadaBuilderV2.Tests/QuickWindows
git commit -m "feat(quick-windows): a Toggle interlock #N is a write site, stripped on paste" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -m "Claude-Session: https://claude.ai/code/session_012JgfWRuzEkPYzJQfGw3HfA"
```

---

### Task 3: Export et capacité `command.write.toggle-interlock`

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/RuntimeContracts/ScadaRuntimeCapabilityCatalog.cs:221,302`
- Modify: `src/ScadaBuilderV2.Application/RuntimeContracts/ScadaRuntimeCapabilityAnalyzer.cs:371-394`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs:2349-2406`
- Create: `tests/ScadaBuilderV2.Tests/ToggleInterlockExportTests.cs`

**Interfaces:**
- Consumes: `ScadaCommandBinding.InterlockTagId` (Task 1).
- Produces: `ScadaRuntimeCapabilityCatalog.CommandToggleInterlock` (id `command.write.toggle-interlock`) ; JSON runtime `interlockTagId` placé juste après `readTagId`, absent si nul.

- [ ] **Step 1: Écrire les tests qui échouent**

Créer `tests/ScadaBuilderV2.Tests/ToggleInterlockExportTests.cs` :

```csharp
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
```

Avant de lancer, vérifier dans `ScadaRuntimeCapability` (Domain/RuntimeContracts) les noms exacts des propriétés `Owner`, `Status`, `Evidence` et du record d'évidence ; les adapter si besoin. Vérifier aussi que la sérialisation existante écrit bien `"readTagId":null` (c'est le cas aujourd'hui : le HTML déployé du popup condenseur contient `"readTagId":null`).

- [ ] **Step 2: Vérifier l'échec**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ToggleInterlockExportTests"`
Expected: échec de compilation, `ScadaRuntimeCapabilityCatalog.CommandToggleInterlock` n'existe pas.

- [ ] **Step 3: Déclarer la capacité**

Dans `ScadaRuntimeCapabilityCatalog.cs`, après `CommandConfirmation` :

```csharp
    /// <summary>Gets the Toggle interlock capability: the interlock tag is written to 0 before the toggle writes 1.</summary>
    /// <remarks>
    /// Decisions: DEC-0054.
    /// Contracts: docs/superpowers/specs/2026-10-13-toggle-interlock-design.md §8-§9.
    /// Tests: tests/ScadaBuilderV2.Tests/ToggleInterlockExportTests.cs, tests/runtime-js/command-dispatcher-interlock.test.mjs.
    /// </remarks>
    public static ScadaRuntimeCapability CommandToggleInterlock { get; } = Capability("command.write.toggle-interlock", ScadaRuntimeCapabilityOwner.SharedRuntime);
```

et dans `CollectAll`, ajouter `CommandToggleInterlock` juste après `CommandConfirmation` dans le tableau.

- [ ] **Step 4: L'exiger quand elle est utilisée**

Dans `ScadaRuntimeCapabilityAnalyzer.AnalyzeCommands`, après le bloc `WriteModes` :

```csharp
            if (command.WriteMode == ScadaWriteMode.Toggle && !string.IsNullOrWhiteSpace(command.InterlockTagId))
            {
                Add(capabilities, ScadaRuntimeCapabilityCatalog.CommandToggleInterlock);
            }
```

(ajouter `using ScadaBuilderV2.Domain.ElementEvents.Command;` si `ScadaWriteMode` n'est pas déjà importé.)

- [ ] **Step 5: Projeter la commande avec un type qui omet le champ nul**

Dans `Ft100SceneExporter.cs`, remplacer le corps de `BuildRuntimeCommandConfig` par :

```csharp
    private static object BuildRuntimeCommandConfig(ScadaElementCommandConfig config)
    {
        return new
        {
            Commands = config.Commands.Select(command => new RuntimeCommandProjection(
                command.Id,
                command.Name,
                command.Enabled,
                command.Trigger,
                command.Kind,
                command.Confirmation,
                command.WriteTagId,
                command.ReadTagId,
                string.IsNullOrWhiteSpace(command.InterlockTagId) ? null : command.InterlockTagId,
                command.WriteMode,
                command.OnValue,
                command.OffValue,
                command.FixedValue,
                command.TargetPageId,
                command.Url,
                command.NewTab,
                command.QuickWindowInvocationKey?.ToString("D"))).ToArray()
        };
    }

    /// <summary>
    /// Runtime shape of one command. Property order is the serialized order and must not change: it keeps every
    /// package without an interlock or an invocation byte-identical to earlier exports.
    /// </summary>
    /// <remarks>
    /// `InterlockTagId` (DEC-0054) and `QuickWindowInvocationKey` are omitted when null rather than written as
    /// `null`, for the reason given on <see cref="BuildRuntimeCommandConfig"/>.
    /// </remarks>
    private sealed record RuntimeCommandProjection(
        string Id,
        string Name,
        bool Enabled,
        ScadaCommandTrigger Trigger,
        ScadaCommandKind Kind,
        ScadaConfirmation? Confirmation,
        string? WriteTagId,
        string? ReadTagId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? InterlockTagId,
        ScadaWriteMode? WriteMode,
        string? OnValue,
        string? OffValue,
        string? FixedValue,
        string? TargetPageId,
        string? Url,
        bool NewTab,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? QuickWindowInvocationKey);
```

Ajouter `using System.Text.Json.Serialization;` si absent. Compléter le `<remarks>` de `BuildRuntimeCommandConfig` par `InterlockTagId follows the same rule (DEC-0054).`

- [ ] **Step 6: Vérifier, y compris la non-régression octet pour octet**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ToggleInterlockExportTests|FullyQualifiedName~Ft100SceneExporterTests|FullyQualifiedName~IndustrialRuntimeIntegrationTests|FullyQualifiedName~ScadaRuntimeCapability"`
Expected: PASS. `IndustrialRuntimeIntegrationTests` vérifie les octets gelés d'un vrai paquet sans interverrouillage : s'il échoue sur autre chose que `RuntimeSha256`, la projection a changé l'ordre ou la casse d'une propriété — corriger la projection, ne jamais régénérer l'évidence industrielle. `RuntimeConformancePackageTests` échouera tant que la Task 6 n'est pas faite (126 → 127) : c'est attendu ici.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.Domain/RuntimeContracts src/ScadaBuilderV2.Application/RuntimeContracts src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs tests/ScadaBuilderV2.Tests/ToggleInterlockExportTests.cs
git commit -m "feat(export): emit interlockTagId only when set and require command.write.toggle-interlock" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -m "Claude-Session: https://claude.ai/code/session_012JgfWRuzEkPYzJQfGw3HfA"
```

---

### Task 4: Runtime — séquence d'écriture et substitution `#N`

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/command-dispatcher.js:91-161`
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/quick-window-runtime.js:512-523`
- Create: `tests/runtime-js/command-dispatcher-interlock.test.mjs`
- Modify: `tests/runtime-js/quick-window-placeholder-substitution.test.mjs`

**Interfaces:**
- Consumes: JSON runtime `interlockTagId` (Task 3).
- Produces: diagnostic `interlock-write-failed` ; métadonnées `mode: 'ToggleInterlock'` sur l'écriture d'interverrouillage.

- [ ] **Step 1: Écrire les tests du dispatcher qui échouent**

Créer `tests/runtime-js/command-dispatcher-interlock.test.mjs` :

```js
import test from 'node:test';
import assert from 'node:assert/strict';
import { loadRuntime } from './harness.mjs';

/** Toggle interlock (DEC-0054): interlock written to 0 first, then the write tag to 1 only if accepted. */

const element = { querySelector: () => null };
const command = {
  id: 'start', kind: 'writeTag', writeMode: 'toggle', writeTagId: 'start', interlockTagId: 'stop',
};

function setup(values, writeTag) {
  const window = loadRuntime(['tag-bridge.js', 'command-dispatcher.js']);
  const writes = [];
  const warnings = [];
  window.console = { warn: (code) => warnings.push(code) };
  window.tf100webScadaBuilder = {
    getTagValue: (tagId) => values[tagId] ?? null,
    writeTag: (tagId, value, payload) => { writes.push({ tagId, value, mode: payload.mode }); return writeTag(tagId, value); },
  };
  return { dispatcher: window.ScadaRuntime.CommandDispatcher, writes, warnings };
}

test('target 1: the interlock is written to 0 before the write tag is written to 1', () => {
  const { dispatcher, writes } = setup({ start: false }, () => true);

  assert.notEqual(dispatcher.execute(element, command), false);

  assert.deepEqual(writes, [
    { tagId: 'stop', value: '0', mode: 'ToggleInterlock' },
    { tagId: 'start', value: '1', mode: 'Toggle' },
  ]);
});

test('target 0: the interlock is never written', () => {
  const { dispatcher, writes } = setup({ start: true }, () => true);

  dispatcher.execute(element, command);

  assert.deepEqual(writes, [{ tagId: 'start', value: '0', mode: 'Toggle' }]);
});

test('a synchronously refused interlock never writes 1', () => {
  const { dispatcher, writes, warnings } = setup({ start: false }, (tagId) => tagId !== 'stop');

  assert.equal(dispatcher.execute(element, command), false);

  assert.deepEqual(writes.map((w) => w.tagId), ['stop']);
  assert.ok(warnings.includes('SCADA command interlock-write-failed'), warnings.join(','));
});

test('an asynchronously refused or failed interlock never writes 1', async () => {
  for (const outcome of ['resolve-false', 'reject']) {
    const { dispatcher, writes, warnings } = setup({ start: false }, (tagId) => (tagId === 'stop'
      ? (outcome === 'reject' ? Promise.reject(new Error('HTTP 502')) : Promise.resolve(false))
      : Promise.resolve(true)));

    assert.equal(await dispatcher.execute(element, command), false, outcome);

    assert.deepEqual(writes.map((w) => w.tagId), ['stop'], outcome);
    assert.ok(warnings.includes('SCADA command interlock-write-failed'), `${outcome}: ${warnings.join(',')}`);
  }
});

test('an asynchronously accepted interlock writes 1 and resolves to the write result', async () => {
  const { dispatcher, writes } = setup({ start: false }, () => Promise.resolve(true));

  assert.equal(await dispatcher.execute(element, command), true);

  assert.deepEqual(writes.map((w) => [w.tagId, w.value]), [['stop', '0'], ['start', '1']]);
});

test('a click during the sequence is refused and writes nothing', async () => {
  let release;
  const { dispatcher, writes, warnings } = setup({ start: false }, (tagId) => (tagId === 'stop'
    ? new Promise((resolve) => { release = resolve; })
    : Promise.resolve(true)));

  const first = dispatcher.execute(element, command);
  assert.equal(dispatcher.execute(element, command), false);
  assert.ok(warnings.includes('SCADA command write-in-flight'));
  assert.equal(writes.length, 1);

  release(true);
  assert.equal(await first, true);
  assert.deepEqual(writes.map((w) => w.tagId), ['stop', 'start']);
});
```

- [ ] **Step 2: Vérifier l'échec**

Run: `node --test tests/runtime-js/command-dispatcher-interlock.test.mjs`
Expected: FAIL — le premier test ne voit qu'une écriture (`start`), l'interverrouillage n'existe pas.

- [ ] **Step 3: Implémenter la séquence**

Dans `command-dispatcher.js`, remplacer `_write` par une version découpée, et ajouter la séquence :

```js
  function _send(cmd, tagId, value, payload) {
    var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
    var metadata = payload || {};
    metadata.commandId = cmd.id || '';
    metadata.contractVersion = INTENT_VERSION;
    return bridge.writeTag(tagId, value, metadata);
  }

  function _canWrite(cmd, value) {
    if (!cmd.writeTagId || value === null || value === undefined || value === '') {
      _diagnose('write-data-missing', cmd);
      return false;
    }
    if (_pendingCommands.has(cmd)) {
      _diagnose('write-in-flight', cmd);
      return false;
    }
    var bridge = window.ScadaRuntime && window.ScadaRuntime.TagBridge;
    if (!bridge || typeof bridge.writeTag !== 'function') {
      _diagnose('tag-bridge-unavailable', cmd);
      return false;
    }
    return true;
  }

  function _write(cmd, value, payload) {
    if (!_canWrite(cmd, value)) return false;
    return _trackWrite(cmd, _send(cmd, cmd.writeTagId, value, payload));
  }

  /**
   * Toggle interlock (DEC-0054): writes 0 to `interlockTagId`, then 1 to `writeTagId` only if the first write
   * was accepted. The whole sequence is one in-flight command, so a second click during it is refused.
   */
  function _writeWithInterlock(cmd) {
    if (!_canWrite(cmd, '1')) return false;
    var proceed = function (accepted) {
      if (accepted === false) {
        _diagnose('interlock-write-failed', cmd);
        return false;
      }
      return _send(cmd, cmd.writeTagId, '1', { mode: 'Toggle' });
    };
    var first = _send(cmd, cmd.interlockTagId, '0', { mode: 'ToggleInterlock' });
    if (!first || typeof first.then !== 'function') {
      return proceed(first);
    }
    return _trackWrite(cmd, first.then(proceed, function (error) {
      _diagnose('interlock-write-failed', cmd, error && error.message ? error.message : String(error));
      return false;
    }));
  }
```

et dans `_writeTagCommand`, cas `toggle`, remplacer la dernière ligne par :

```js
        var target = booleanValue ? '0' : '1';
        if (target === '1' && cmd.interlockTagId) return _writeWithInterlock(cmd);
        return _write(cmd, target, { mode: 'Toggle' });
```

Note : avec une première écriture asynchrone, `_trackWrite` reçoit la promesse de toute la séquence ; si `proceed` renvoie la promesse de la seconde écriture, elle est chaînée et son résultat devient celui de la commande. Un refus déclenche aussi le diagnostic existant `write-rejected` de `_trackWrite` en plus de `interlock-write-failed` : c'est voulu, la commande a bien été refusée.

- [ ] **Step 4: Vérifier le dispatcher**

Run: `node --test tests/runtime-js/command-dispatcher-interlock.test.mjs tests/runtime-js/command-dispatcher.test.mjs`
Expected: PASS (6 nouveaux tests, aucun ancien en échec).

- [ ] **Step 5: Écrire les tests de substitution qui échouent**

Dans `tests/runtime-js/quick-window-placeholder-substitution.test.mjs`, ajouter le site à `writeSiteRoots` (il devient couvert par le test existant « every write site bound to a Literal or an Expression keeps refusing placeholder-write-not-tag ») :

```js
  const commandInterlock = make();
  commandInterlock.setAttribute('data-scada-command-config', JSON.stringify({ commands: [{ id: 'cmd', writeTagId: '#1', readTagId: null, interlockTagId: placeholder }] }));
  return { 'data-scada-write-tag': attr, 'command writeTagId': commandWrite, 'command interlockTagId': commandInterlock };
```

(remplacer la ligne `return` existante de `writeSiteRoots` par ces trois lignes), et ajouter :

```js
test('a command config interlockTagId #5 becomes the tag id, and an unbound one refuses the mount', () => {
  const window = loadQuickWindow();
  const root = window.document.createElement('div');
  const el = window.document.createElement('div');
  el.setAttribute('data-scada-command-config', JSON.stringify({
    commands: [{ id: 'cmd-1', name: 'Start', writeTagId: '#1', readTagId: null, interlockTagId: '#5' }]
  }));
  root.appendChild(el);

  assert.equal(window.ScadaRuntime.QuickWindow.substituteInstance(root, PORTS).ok, true);
  assert.equal(JSON.parse(el.getAttribute('data-scada-command-config')).commands[0].interlockTagId, 'tf100.mapping.777');

  const unboundRoot = window.document.createElement('div');
  const unbound = window.document.createElement('div');
  unbound.setAttribute('data-scada-command-config', JSON.stringify({
    commands: [{ id: 'cmd-2', writeTagId: '#1', readTagId: null, interlockTagId: '#9' }]
  }));
  unboundRoot.appendChild(unbound);
  const refused = window.ScadaRuntime.QuickWindow.substituteInstance(unboundRoot, PORTS);
  assert.equal(refused.ok, false);
  assert.equal(refused.code, 'placeholder-unbound');
});
```

Run: `node --test tests/runtime-js/quick-window-placeholder-substitution.test.mjs`
Expected: FAIL — `interlockTagId` reste `#5`, et le site interverrouillage lié à un littéral n'est pas refusé.

- [ ] **Step 6: Implémenter la substitution**

Dans `quick-window-runtime.js`, `substituteInstance`, boucle des commandes, après le bloc `readTagId` :

```js
          if (typeof command.interlockTagId === 'string' && PLACEHOLDER_EXACT.test(command.interlockTagId)) {
            // DEC-0054: the interlock is written, so it is a write site like writeTagId.
            var interlockResolved = _tagFor(ports, command.interlockTagId, 'write');
            if (interlockResolved.error) return _refuse(interlockResolved.error, command.interlockTagId);
            command.interlockTagId = interlockResolved.tagId;
          }
```

Mettre à jour la JSDoc de `substituteInstance` (ligne ~356) pour citer `interlockTagId` parmi les sites d'écriture.

- [ ] **Step 7: Vérifier toute la suite runtime JS**

Run: `node --test tests/runtime-js/`
Expected: PASS. Si `node --test` sur le dossier ne trouve pas les fichiers, lancer `node --test tests/runtime-js/*.test.mjs`.

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~RuntimeJsModulesTests"`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Runtime tests/runtime-js
git commit -m "feat(runtime): toggle interlock writes 0 first and substitutes #N as a write site" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -m "Claude-Session: https://claude.ai/code/session_012JgfWRuzEkPYzJQfGw3HfA"
```

---

### Task 5: Éditeur — liste « Interverrouillage (optionnel) »

**Files:**
- Modify: `src/ScadaBuilderV2.App/ElementCommandDialog.xaml:40-45`
- Modify: `src/ScadaBuilderV2.App/ElementCommandDialog.xaml.cs:93-101,190-195,236-238`
- Modify: `tests/ScadaBuilderV2.Tests/ElementCommandDialogKindListSourceContractTests.cs`

**Interfaces:**
- Consumes: `ScadaCommandBinding.InterlockTagId` (Task 1).

- [ ] **Step 1: Écrire le test de contrat source qui échoue**

Dans `ElementCommandDialogKindListSourceContractTests.cs`, ajouter :

```csharp
    [TestMethod]
    public void TheInterlockListExistsIsEnabledOnlyInToggleAndIsSavedOnlyInToggle()
    {
        var xaml = ReadAppFile("ElementCommandDialog.xaml");
        var code = ReadAppFile("ElementCommandDialog.xaml.cs");

        StringAssert.Contains(xaml, "x:Name=\"InterlockTagComboBox\"");
        StringAssert.Contains(xaml, "Interverrouillage (optionnel)");
        StringAssert.Contains(code, "InterlockTagComboBox.ItemsSource = tagItems;");
        StringAssert.Contains(code, "InterlockTagComboBox.IsEnabled = mode == ScadaWriteMode.Toggle;");
        StringAssert.Contains(code,
            "InterlockTagId: (ScadaWriteMode?)WriteModeComboBox.SelectedItem == ScadaWriteMode.Toggle ? (InterlockTagComboBox.SelectedItem as TagItem)?.Id : null");
    }
```

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ElementCommandDialogKindListSourceContractTests"`
Expected: FAIL sur `InterlockTagComboBox`.

- [ ] **Step 2: XAML**

Dans `ElementCommandDialog.xaml`, dans `WriteTagPanel`, après la `ComboBox x:Name="ReadTagComboBox"` :

```xml
                <TextBlock Text="Interverrouillage (optionnel)" Foreground="{StaticResource MutedBrush}"
                           ToolTip="Mode Toggle uniquement : quand ce bouton passe le tag d'ecriture a 1, ce tag est d'abord mis a 0 (ex. Arret pour Depart)."/>
                <ComboBox x:Name="InterlockTagComboBox" Margin="0,2,0,8" IsEnabled="False"/>
```

- [ ] **Step 3: Code-behind**

Dans le constructeur, après `ReadTagComboBox.ItemsSource = tagItems;` :

```csharp
        InterlockTagComboBox.ItemsSource = tagItems;
```

après `ReadTagComboBox.SelectedItem = ...` (édition d'une commande existante) :

```csharp
            InterlockTagComboBox.SelectedItem = tagItems.FirstOrDefault(t => t.Id == existingCommand.InterlockTagId);
```

dans `UpdateWriteModePanels`, ajouter en fin de méthode :

```csharp
        InterlockTagComboBox.IsEnabled = mode == ScadaWriteMode.Toggle;
        if (mode != ScadaWriteMode.Toggle)
        {
            InterlockTagComboBox.SelectedItem = null;
        }
```

et dans `OnSaveClick`, dans la construction de `Result`, après `QuickWindowPageKey: ...` (dernier argument, donc remplacer la `)` finale) :

```csharp
            QuickWindowPageKey: kind == ScadaCommandKind.OpenQuickWindow ? _quickWindowPageKey : null,
            InterlockTagId: (ScadaWriteMode?)WriteModeComboBox.SelectedItem == ScadaWriteMode.Toggle ? (InterlockTagComboBox.SelectedItem as TagItem)?.Id : null);
```

Si le type de commande n'est pas `WriteTag`, `WriteModeComboBox.SelectedItem` peut rester à Toggle d'une édition précédente : vérifier que la validation de build (Task 1, `command.interlock-requires-toggle`) le signale, et ne rien ajouter ici.

- [ ] **Step 4: Compiler et vérifier**

Run: `dotnet build ScadaBuilderV2.sln` puis `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ElementCommandDialog"`
Expected: build sans erreur, tests PASS.

- [ ] **Step 5: Contrôle manuel**

Lancer `dotnet run --project src/ScadaBuilderV2.App`, ouvrir `AMR_REF_SCADA_V2`, ouvrir la page Fenêtre rapide du condenseur (`win00034`), éditer la commande du bouton Départ : la liste « Interverrouillage (optionnel) » est active en Toggle et propose les `#N` ; la passer en SetFixed la grise et la vide. **Ne pas enregistrer le projet** : la configuration réelle sera faite par l'utilisateur.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/ElementCommandDialog.xaml src/ScadaBuilderV2.App/ElementCommandDialog.xaml.cs tests/ScadaBuilderV2.Tests/ElementCommandDialogKindListSourceContractTests.cs
git commit -m "feat(editor): Toggle interlock selector in the command dialog" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -m "Claude-Session: https://claude.ai/code/session_012JgfWRuzEkPYzJQfGw3HfA"
```

---

### Task 6: Conformance Builder, fixtures régénérées, matrice

**Files:**
- Modify: `tests/ScadaBuilderV2.Tests/RuntimeContracts/ScadaV2RuntimeConformanceProjectFactory.cs:352-370`
- Modify: `tests/ScadaBuilderV2.Tests/RuntimeContracts/RuntimeConformancePackageTests.cs:95-96`
- Regenerate: `tests/conformance/artifacts/*`, `tests/conformance/expected-runtime-capabilities.json`, `tests/conformance/quick-window-*.sb2/.sha256`, `tests/conformance/quick-window-parameter-sheet-e2e/*`, `docs/10_generated/*`

**Interfaces:**
- Produces: `tests/conformance/artifacts/scada-v2-runtime-conformance.sb2` contenant la commande `write-toggle-interlock` (`writeTagId: conformance.tag.write`, `interlockTagId: conformance.tag.bool`, mode toggle), consommée par TF100Web en Task 7.

- [ ] **Step 1: Ajouter la commande au modèle de conformité**

Dans `ScadaV2RuntimeConformanceProjectFactory.cs`, dans `commands.AddRange([...])`, après `write-input` :

```csharp
            new ScadaCommandBinding("write-toggle-interlock", "Toggle interlock", true, ScadaCommandTrigger.OnClick,
                ScadaCommandKind.WriteTag, WriteTagId: "conformance.tag.write", WriteMode: ScadaWriteMode.Toggle,
                InterlockTagId: "conformance.tag.bool"),
```

Dans `RuntimeConformancePackageTests.cs` :

```csharp
        // 115 before Phase 6, the eleven quick-window capabilities promoted there, and DEC-0054's toggle interlock.
        Assert.AreEqual(127, supportedFixtures.Length);
```

- [ ] **Step 2: Vérifier l'échec attendu**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~RuntimeConformancePackageTests"`
Expected: `FactoryCoversEveryStrictSupportedCapability...` PASS ; `CommittedConformancePackageMatchesTwoDeterministicExports` et `ExpectedIndexIsExhaustive...` FAIL (fixture gelée périmée).

- [ ] **Step 3: Régénérer les fixtures gelées (jamais l'évidence industrielle)**

Run (PowerShell, depuis la racine du dépôt) :

```powershell
$env:SCADA_UPDATE_CONFORMANCE="1"; $env:SCADA_UPDATE_PARAMETER_SHEET_E2E="1"; $env:SCADA_UPDATE_QUICK_WINDOW_PARAMETER_SHEET="1"; $env:SCADA_UPDATE_QUICK_WINDOW_HANDSHAKE="1"; $env:SCADA_UPDATE_QUICK_WINDOW_SOAK="1"
dotnet test ScadaBuilderV2.sln --no-restore
Remove-Item Env:SCADA_UPDATE_CONFORMANCE, Env:SCADA_UPDATE_PARAMETER_SHEET_E2E, Env:SCADA_UPDATE_QUICK_WINDOW_PARAMETER_SHEET, Env:SCADA_UPDATE_QUICK_WINDOW_HANDSHAKE, Env:SCADA_UPDATE_QUICK_WINDOW_SOAK
```

Puis `git diff --stat tests/conformance` : seuls doivent changer les `.sb2`, `.sha256`, `expected-runtime-capabilities.json` (une fixture `command.write.toggle-interlock` ajoutée, `PackageSha256` changé) et les fichiers de l'E2E feuille de paramètres. Pour les paquets quick-window, vérifier avec `git diff` du manifeste extrait que seul `RuntimeSha256` a changé. Rien sous `tests/conformance/industrial` ne doit apparaître.

- [ ] **Step 4: Régénérer la matrice des capacités**

Run (PowerShell): `powershell -ExecutionPolicy Bypass -File tools/docs/generate-runtime-capability-matrix.ps1`
Expected: `docs/10_generated/` mis à jour avec `command.write.toggle-interlock` en `Supported`.

- [ ] **Step 5: Suite complète et docs**

Run: `dotnet test ScadaBuilderV2.sln --no-restore`
Expected: tout PASS.

Run: `node --test tests/runtime-js/*.test.mjs`
Expected: tout PASS.

Run (PowerShell): `powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1 2>&1 | Select-String "Errors:"`
Expected: `Errors: 0`.

- [ ] **Step 6: Commit**

```bash
git add tests/ScadaBuilderV2.Tests/RuntimeContracts tests/conformance docs/10_generated
git commit -m "test(conformance): exercise command.write.toggle-interlock and regenerate frozen fixtures" -m "The shared runtime bytes change, so every frozen package drifts by RuntimeSha256 only; the industrial evidence is untouched." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -m "Claude-Session: https://claude.ai/code/session_012JgfWRuzEkPYzJQfGw3HfA"
```

---

### Task 7: TF100Web — accepter et prouver la capacité

**Files:**
- Modify: `F:\Projet\Git\TF100Web-qw\frontend\scada_package.py:14-60`
- Modify: `F:\Projet\Git\TF100Web-qw\frontend\tests_runtime_conformance.py:165-166,193-194`
- Modify: `F:\Projet\Git\TF100Web-qw\frontend\tests_runtime_js\runtime-conformance-harness.mjs:~418`
- Modify: `F:\Projet\Git\TF100Web-qw\frontend\test_fixtures\runtime-conformance-expectations.json`
- Re-vendor: `frontend/test_fixtures/scada-v2-runtime-conformance.sb2/.sha256`, `expected-runtime-capabilities.json`, `quick-window-parameter-sheet.sb2/.sha256`, `quick-window-runtime-handshake.sb2/.sha256`, `quick-window-soak-manifest.json`

**Interfaces:**
- Consumes: fixtures Builder régénérées (Task 6) ; commande `write-toggle-interlock` du paquet de conformité.

- [ ] **Step 1: Créer la branche**

```bash
cd /f/Projet/Git/TF100Web-qw
git status --porcelain   # doit être vide
git fetch -q origin && git switch -c feature/toggle-interlock-capability origin/main
```

- [ ] **Step 2: Re-vendoriser les fixtures**

```bash
B="/f/Groupe AMR/SCADA_AMR_GROUP/SCADA_BUILDER_V2/tests/conformance"
F=frontend/test_fixtures
cp "$B/artifacts/scada-v2-runtime-conformance.sb2" "$B/artifacts/scada-v2-runtime-conformance.sha256" "$B/expected-runtime-capabilities.json" $F/
cp "$B/quick-window-parameter-sheet.sb2" "$B/quick-window-parameter-sheet.sha256" "$B/quick-window-runtime-handshake.sb2" "$B/quick-window-runtime-handshake.sha256" $F/
```

Pour `quick-window-soak-manifest.json`, recopier uniquement la nouvelle valeur `RuntimeSha256` depuis le manifeste du `quick-window-soak.sb2` régénéré du Builder (même procédure que le commit `dcb0c02`). Dans `runtime-conformance-expectations.json`, remplacer `builderPackageSha256` par la première valeur de `$F/scada-v2-runtime-conformance.sha256`.

- [ ] **Step 3: Écrire la sonde (elle échoue tant que la capacité n'est pas acceptée)**

Dans `runtime-conformance-harness.mjs`, dans `probeCommand`, juste avant `const writeSpecs = {` :

```js
  if (capabilityId === 'command.write.toggle-interlock') {
    const command = commandById.get('write-toggle-interlock');
    requireProbe(command && command.kind === 'writeTag' && command.writeMode === 'toggle'
      && command.interlockTagId === 'conformance.tag.bool', 'write-toggle-interlock contract mismatch');
    hostValues['conformance.tag.write'] = 0;
    executeCommand(command, capabilityId);
    requireProbe(hostWrites.length === 2
      && hostWrites[0].tagId === 'conformance.tag.bool' && hostWrites[0].value === '0'
      && hostWrites[1].tagId === 'conformance.tag.write' && hostWrites[1].value === '1',
      'write-toggle-interlock did not clear the interlock before writing 1');
    return 'interlock cleared to 0 before the toggle wrote 1';
  }
```

Dans `tests_runtime_conformance.py`, remplacer les trois `126` par `127` et le commentaire par `# 115 before Phase 6, the eleven quick-window capabilities promoted there, and DEC-0054's toggle interlock.`

Run (WSL, recette Django, voir Global Constraints). Créer d'abord, hors dépôt, le module de réglages SQLite et le dossier de projets vide (depuis Git Bash, préfixer `MSYS_NO_PATHCONV=1`) :

```bash
MSYS_NO_PATHCONV=1 wsl.exe -e bash -lc 'mkdir -p /tmp/tf100_test_settings /tmp/tf100_empty_projects && printf "%s\n" "from tf100web.settings import *  # noqa" "DATABASES[\"default\"] = {\"ENGINE\": \"django.db.backends.sqlite3\", \"NAME\": \"/tmp/tf100_test.sqlite3\"}" > /tmp/tf100_test_settings/tf100_test_sqlite.py'
MSYS_NO_PATHCONV=1 wsl.exe -e bash -lc "cd /mnt/f/Projet/Git/TF100Web-qw && SCADA_BUILDER_PROJECTS_ROOT=/tmp/tf100_empty_projects PYTHONPATH=/tmp/tf100_test_settings DJANGO_SETTINGS_MODULE=tf100_test_sqlite /home/florizon/.venvs/tf100web/bin/python manage.py test frontend.tests_runtime_conformance --noinput -v 1"
```

Expected: FAIL avec `unsupported-runtime-capabilities:command.write.toggle-interlock`. Node n'existant que côté Windows, si le test signale Node absent sous WSL, lancer la vérification de la sonde depuis Windows (voir mémoire projet) et noter le résultat.

- [ ] **Step 4: Accepter la capacité**

Dans `frontend/scada_package.py`, `SUPPORTED_SCADA_RUNTIME_CAPABILITIES`, insérer à sa place alphabétique (après `"command.write.toggle",`) :

```python
    "command.write.toggle-interlock",
```

- [ ] **Step 5: Vérifier**

Run: la même commande Django qu'au Step 3, puis la suite complète `manage.py test --noinput` avec la même recette.
Expected: PASS, aucune régression (référence : 1077 tests OK au 2026-10-13).

Run (Windows): `cd /f/Projet/Git/TF100Web-qw && node --test frontend/tests_runtime_js/*.test.mjs`
Expected: PASS (référence : 126/126).

- [ ] **Step 6: Commit et push de la branche**

```bash
git add frontend/scada_package.py frontend/tests_runtime_conformance.py frontend/tests_runtime_js/runtime-conformance-harness.mjs frontend/test_fixtures
git commit -m "feat(scada): accept and prove command.write.toggle-interlock (DEC-0054)" -m "Re-vendors the Builder fixtures regenerated for the toggle interlock; the quick-window packages drift by RuntimeSha256 only." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -m "Claude-Session: https://claude.ai/code/session_012JgfWRuzEkPYzJQfGw3HfA"
git push -u origin feature/toggle-interlock-capability
```

Ne pas merger dans `main` sans l'accord de l'utilisateur.

---

### Task 8: Vérification finale et passage à l'utilisateur

**Files:** aucun fichier de produit. Harnais de vérification dans le scratchpad de session, jamais commité.

- [ ] **Step 1: Suites complètes des deux dépôts**

Builder : `dotnet test ScadaBuilderV2.sln --no-restore`, `node --test tests/runtime-js/*.test.mjs`, `verify-docs.ps1` → 0 erreur.
TF100Web : suite Django complète (recette SQLite) et `node --test frontend/tests_runtime_js/*.test.mjs`.
Noter les nombres de tests dans le rapport.

- [ ] **Step 2: Push de la branche Builder**

```bash
cd "/f/Groupe AMR/SCADA_AMR_GROUP/SCADA_BUILDER_V2"
git push -u origin feature/toggle-interlock
```

(Si le remote SSH `github-scadabuilder` est injoignable, le signaler et ne pas insister.)

- [ ] **Step 3: Rapport et suite**

Rapporter à l'utilisateur : commits des deux dépôts, résultats des suites, ordre de déploiement (TF100Web d'abord — merge de `feature/toggle-interlock-capability` dans `main` puis redéploiement de l'unité — puis le `.sb2`). Lui demander de configurer dans Builder l'interverrouillage sur Départ/Arrêt (`#3` ↔ `#4`) et Auto/Man (`#1` ↔ `#2`) du popup condenseur `win00034`, puis d'exporter le `.sb2`.

- [ ] **Step 4: Vérification Edge sur le `.sb2` de l'utilisateur**

Une fois le `.sb2` exporté : extraire le paquet dans un dossier du scratchpad ; construire une page qui charge le `quick-window-host.js` de TF100Web, le `scada-runtime.*.js` du paquet, le fragment `qw-698f3b13` et les CSS réels (même méthode que la vérification des barres de défilement du 2026-10-13), avec un `tf100webScadaBuilder` factice qui enregistre les écritures et renvoie des promesses résolues à `true`. Ouvrir le popup avec les liaisons de l'invocation `shape_008` (condenseur 1B), cliquer sur Départ avec `BP_HMI_Depart_Manuel_CE_1B` à 0 dans le cache factice, et vérifier dans Edge headless (`--dump-dom`) que les écritures sont, dans l'ordre, `BP_HMI_Arret_Manuel_CE_1B` ← `'0'` puis `BP_HMI_Depart_Manuel_CE_1B` ← `'1'`. Recommencer pour Man ↔ Auto.

- [ ] **Step 5: Essai sur site**

Seulement avec l'accord explicite de l'utilisateur au moment de l'essai (vraies commandes) : après déploiement, cliquer Départ puis Arrêt dans le popup 1B et vérifier dans Sysmac que chaque clic met l'autre bit à 0.
