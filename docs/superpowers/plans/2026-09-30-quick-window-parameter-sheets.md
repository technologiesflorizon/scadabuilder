# Feuilles de paramètres des fenêtres rapides - Plan d'implémentation

Date: 2026-09-30
Status: Draft - en attente de validation et du choix du mode d'exécution
Document version: `V2.1.6.0033`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-09-30 | `V2.1.6.0033` | `PENDING` | Création du plan dérivé de `docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md` : treize tâches, rulings R1 à R4. |

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remplacer l'authoring des fenêtres rapides par des pages de type Fenêtre rapide paramétrées par `#N` et des feuilles de paramètres, compilées vers les structures `DEC-0050` et substituées par le runtime partagé à l'ouverture de l'instance.

**Architecture:** Une couche d'authoring (feuille, page `QuickWindow`, commande (feuille, page)) est *abaissée* par une fonction pure de `Application` vers `QuickWindowDefinition` + `QuickWindowInvocation` juste avant validation, aperçu et export. Le runtime partagé embarqué dans le paquet réécrit les `#N` d'une instance dans `mountInstance`, avant `initPage`. Aucun code de l'hôte TF100Web ne change ; sa branche `codex/quick-window-v1` rejoue sa conformance.

**Tech Stack:** C# / .NET 8, WPF, MSTest, JavaScript ES5 du runtime partagé testé sous Node `24.15.x` (`node --test`), TF100Web Django sous WSL.

**Spec:** `docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md`

## Global Constraints

- **`DEC-PARAMSHEETS`** désigne la décision enregistrée en Task 1, qui prend le numéro suivant `DEC-0051` au registre. Chaque implémenteur remplace ce jeton par le numéro réel dans le code et la documentation qu'il écrit.
- Code, identifiants et XML docs en anglais ; textes affichés et documentation en français.
- Chaque tâche qui touche le code : incrémenter `VERSION` (valeur courante `V2.1.6.0033`) et ajouter une ligne de changelog dans le document propriétaire trouvé via `docs/README.md` **et** dans `docs/README.md`, avec `PENDING` comme hash. Ne jamais lancer `resolve-pending-commits.py` : le contrôleur le fait après revue.
- `powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1` reste à `Errors: 0` (les `PENDING` de son propre commit sont attendus).
- Fichiers C# : UTF-8 **sans BOM**, fins de ligne **CRLF**. Vérifier par `git diff` qu'aucun fichier n'a été réécrit entier.
- Aucune dépendance NuGet ni npm nouvelle.
- Ne jamais lancer deux compilations de `ScadaBuilderV2.App` en parallèle ; ne jamais tuer l'application de l'utilisateur. Rafale `CS2001 GeneratedInternalTypeHelper.g.cs introuvable` ou `BG1002` sans application en cours : `dotnet build-server shutdown`, supprimer `src/ScadaBuilderV2.App/obj/Debug/net8.0-windows`, recompiler.
- Le projet `tests/ScadaBuilderV2.Tests` ne référence pas `ScadaBuilderV2.App` ; il **lie** des fichiers d'`App` un par un (`Compile Include` dans son `.csproj`). Toute logique d'`App` à tester vit dans un fichier sans WPF, ajouté à cette liste.
- Tout test doit **échouer avant** le code qu'il couvre ; la sortie de l'échec figure dans le rapport de tâche.
- Jeton de paramètre : `#` suivi de 1 à 3 chiffres, non suivi d'un chiffre ; valeur valide de 1 à 999 sans zéro de tête ; `\#` dans un texte produit un `#` littéral.
- Les `#N` ne sont reconnus que dans les **textes** et les **champs de liaison** (tags, expressions, cibles d'écriture) — **jamais** dans les styles. `#0F2A30` est une couleur.
- Baseline : build 0 erreur ; `dotnet test ScadaBuilderV2.sln` = 1020 (`ScadaBuilderV2.Tests`) + 7 (`ScadaBuilderV2.ShellTests`), 0 échec.
- Attribution des commits :
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01QTVr3PtkPJQ7oN5DPf5k47
  ```

## Rulings pris en écrivant le plan

Précisions de la spec rendues nécessaires par le code réel. Elles ne changent rien à ce que l'utilisateur écrit dans une feuille.

- **R1 — Nature d'un littéral.** Seul un littéral **texte** (`"Ventilateur 3"`) est de nature `Literal`. Un nombre ou un booléen seul (`1`, `true`) est de nature `Expression` à un seul nœud. Raison : le runtime évalue des AST typés ; un `LiteralValue` texte ne dit pas si `"1"` est un nombre.
- **R2 — `#N` dans un texte affiché exige un littéral texte.** Un `#N` lié à un tag ou à une expression et utilisé dans un texte est une **erreur** de compilation : la substitution textuelle afficherait un identifiant technique (`tf100.mapping.196`). Une valeur vivante s'affiche par la lecture de variable existante, qui, elle, accepte un `#N` lié à un tag.
- **R3 — AST embarqué.** Le runtime n'a pas d'analyseur d'expressions, seulement un évaluateur d'AST. Une liaison `Expression` porte donc, en plus de sa source, un champ manifeste optionnel `ExpressionAst` : l'AST sérialisé au format runtime (discriminant `type`, camelCase). Changement de contrat, documenté en Task 1.
- **R4 — Clés déterministes.** `DefinitionKey` = `PageKey` de la page Fenêtre rapide ; `MemberKey` et `InvocationKey` dérivés par SHA-256 de chaînes canoniques. Deux compilations du même projet restent identiques, comme l'exige `QuickWindowCompiler`.

## Review Focus

1. **Couleurs hexadécimales prises pour des paramètres.** `Popup_Fan_Condenseur` porte 69 couleurs `#0F2A30`, `#1B7A1B`… et aucun `#N` voulu. Attendu : aucun paramètre détecté, la page s'ouvre. Test en Task 5 (collecteur) et Task 7 (runtime).
2. **Feuille ou page supprimée, ou page retypée, alors qu'une commande la référence.** Attendu : erreur bloquante qui nomme l'élément et la commande, jamais une exception. Test en Task 5.
3. **Même page ouverte deux fois de suite avec deux feuilles.** Attendu : la seconde instance affiche ses propres valeurs, sans reste de la première. Test en Task 7.
4. **`//` à l'intérieur d'un littéral** (`#4 := "http://x"`). Attendu : pas de coupure de commentaire dans une chaîne. Test en Task 2.
5. **Feuille de 999 lignes sur une page de 91 éléments.** Attendu : analyse et montage sans dégradation visible ; un seul parcours du DOM. Test en Task 7 (999 paramètres) et Task 2 (999 lignes).

---

## File Structure

| Fichier | Responsabilité |
| --- | --- |
| `src/ScadaBuilderV2.Domain/QuickWindows/ParameterSheetModels.cs` (créé) | `ParameterSheet`, `ParameterValueKind`, `ParameterEntry`, `ParameterSheetDiagnostic`, `ParameterSheetParseResult`. |
| `src/ScadaBuilderV2.Domain/QuickWindows/ParameterSheetParser.cs` (créé) | Analyse totale et pure d'un texte de feuille, classement R1. |
| `src/ScadaBuilderV2.Domain/QuickWindows/QuickWindowPlaceholder.cs` (créé) | Reconnaissance des jetons `#N`, échappement `\#`. |
| `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs` (modifié) | `ScadaPageType.QuickWindow`, `ScadaProject.ParameterSheets`. |
| `src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBinding.cs` (modifié) | `ParameterSheetKey`, `QuickWindowPageKey`. |
| `src/ScadaBuilderV2.Domain/QuickWindows/QuickWindowInvocationModels.cs` (modifié) | `QuickWindowBinding.ExpressionAst`. |
| `src/ScadaBuilderV2.Infrastructure/ModernProjects/ParameterSheetStore.cs` (créé) | Persistance `parameter-sheets/<key>.parameter-sheet.json`. |
| `src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectStore.cs` (modifié) | Chargement, écriture atomique et suppression des feuilles. |
| `src/ScadaBuilderV2.Application/QuickWindows/QuickWindowKeys.cs` (créé) | Clés déterministes R4. |
| `src/ScadaBuilderV2.Application/QuickWindows/QuickWindowPlaceholderUsageCollector.cs` (créé) | Inventaire des `#N` d'une scène, par emplacement. |
| `src/ScadaBuilderV2.Application/QuickWindows/ParameterSheetLowering.cs` (créé) | Abaissement vers `DEC-0050` + validation par couple. |
| `src/ScadaBuilderV2.Rendering/QuickWindows/Ft100QuickWindowManifestModels.cs` et `QuickWindowCompiler.cs` (modifiés) | Champ manifeste `ExpressionAst`. |
| `src/ScadaBuilderV2.Rendering/Runtime/quick-window-runtime.js` (modifié) | Substitution au montage ; `readPort` sur `ExpressionAst`. |
| `src/ScadaBuilderV2.App/MainWindow.xaml(.cs)` (modifiés) | Sélecteur de type par valeur, appel de l'abaissement avant validation et export. |
| `src/ScadaBuilderV2.App/QuickWindows/ParameterSheetEditorViewModel.cs` (créé, lié aux tests) | Logique de l'éditeur de feuille et du dialogue de règle. |
| `src/ScadaBuilderV2.App/QuickWindows/ParameterSheetEditorDialog.xaml(.cs)`, `ParameterRuleDialog.xaml(.cs)`, `QuickWindowConfigurationDialog.xaml(.cs)` (créés) | Surfaces WPF. |
| `src/ScadaBuilderV2.Application/Commands/ElementCommandKindCatalog.cs` (modifié) | Grisage (page Fenêtre rapide **et** feuille requises). |

---

### Task 1: Gouvernance et contrat

**Files:**
- Modify: `docs/08_implementation_status/KNOWN_GAPS_V2.md`
- Modify: `docs/00_governance/DECISION_REGISTER_V2.md`
- Modify: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md` (§11b, §12.4)
- Modify: `docs/superpowers/specs/2026-09-24-fragment-to-quick-window-conversion-design.md` (en-tête)
- Modify: `docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md` (en-tête, R1–R4 en §3)
- Modify: `VERSION`, `docs/README.md`

**Interfaces:**
- Produces: la décision `DEC-PARAMSHEETS`, citable par toutes les tâches suivantes ; la définition contractuelle de `ExpressionAst` et de la substitution au montage.

- [ ] **Step 1: Consigner l'écart** — ajouter à `KNOWN_GAPS_V2.md` une entrée numérotée à la suite des existantes :

```markdown
33. **Aucun élément de fenêtre rapide n'est animé par un port** (constaté le 2026-09-30). `readPort` et `writePort` de `quick-window-runtime.js` ne sont appelés que par des tests : `tests/runtime-js/quick-window-runtime.test.mjs` (Builder) et `frontend/tests_runtime_js/quick-window-runtime-handshake.test.mjs` (TF100Web, branche `codex/quick-window-v1`); `frontend/tests_scada_quick_window_host.py` interdit même à l'hôte de les appeler. Le compilateur ne réécrit pas les références des éléments. Les sondes de la Phase 6 prouvent l'API de résolution, pas l'animation d'un élément : `quick-window.port-binding` est promue sur cette seule base. Traité par `DEC-PARAMSHEETS` (`docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md`).
```

- [ ] **Step 2: Enregistrer la décision** — dans `DECISION_REGISTER_V2.md`, une ligne d'historique et une section `### DEC-PARAMSHEETS - Fenêtres rapides paramétrées par feuilles de paramètres`, en suivant la forme de `DEC-0051` : contexte (l'écart de l'étape 1, le retour utilisateur), décision (D1 à D7 de la spec et R1 à R4 de ce plan), conséquences (spec du 2026-09-24 supersédée ; contrat §11b/§12.4 modifié), statut `Accepted`.

- [ ] **Step 3: Contrat de paquet** — dans `FT100_TF100WEB_PACKAGE_CONTRACT_V2.md` :
  - §12.4, entrée de liaison de `QuickWindowInvocations[].Bindings[]` : ajouter `ExpressionAst` (objet JSON optionnel, AST au format runtime : discriminant `type` parmi `literalNumber`, `literalBool`, `literalString`, `tagRef`, `unary`, `binary`, `func`, propriétés camelCase ; présent seulement quand `SourceKind` vaut `Expression`).
  - §11b : décrire la substitution au montage — emplacements (`data-scada-read-tag`, `data-scada-write-tag`, `data-scada-state-config`, `data-scada-command-config`, nœuds texte), règles de la spec §7 et de R2, refus diagnostiqués (`placeholder-unbound`, `placeholder-write-not-tag`, `placeholder-text-not-literal`), et l'interdiction de toucher aux attributs `style`.

- [ ] **Step 4: Spécs** — la spec du 2026-09-24 passe `Status: Superseded by docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md (DEC-PARAMSHEETS)` ; la spec du 2026-09-30 passe `Status: Approved - DEC-PARAMSHEETS` et reçoit R1 à R4 en fin de §3. Une ligne d'historique chacune.

- [ ] **Step 5: Version et index** — `VERSION` → valeur suivante ; ligne de changelog dans `docs/README.md`.

- [ ] **Step 6: Vérifier**

Run: `powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1`
Expected: `Errors: 0`

- [ ] **Step 7: Commit**

```bash
git add VERSION docs/
git commit -m "docs: record DEC-PARAMSHEETS and the quick-window port-binding gap"
```

---

### Task 2: Analyseur de feuille de paramètres

**Files:**
- Create: `src/ScadaBuilderV2.Domain/QuickWindows/ParameterSheetModels.cs`
- Create: `src/ScadaBuilderV2.Domain/QuickWindows/ParameterSheetParser.cs`
- Test: `tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetParserTests.cs`

**Interfaces:**
- Consumes: `ScadaExpressionParser.Parse(string) : ScadaExprParseResult` ; nœuds `ScadaExprTagRef`, `ScadaExprLiteralString`, `ScadaExprLiteralNumber`, `ScadaExprLiteralBool`, `ScadaExprUnary`, `ScadaExprBinary`, `ScadaExprFunc`.
- Produces:
  - `record ParameterSheet(Guid SheetKey, string Code, string DisplayName, string Text)`
  - `enum ParameterValueKind { Tag, Literal, Expression }`
  - `record ParameterEntry(int Number, int Line, ParameterValueKind Kind, string Source, ScadaExprNode Node)`
  - `record ParameterSheetDiagnostic(int Line, string Message)`
  - `record ParameterSheetParseResult(IReadOnlyDictionary<int, ParameterEntry> Entries, IReadOnlyList<ParameterSheetDiagnostic> Diagnostics)` avec `bool IsValid`
  - `static ParameterSheetParseResult ParameterSheetParser.Parse(string? text)`
  - `static ParameterValueKind ParameterSheetParser.Classify(ScadaExprNode node)`

- [ ] **Step 1: Write the failing tests**

```csharp
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.Tests.QuickWindows;

[TestClass]
public sealed class ParameterSheetParserTests
{
    private const string Example =
        "// Ventilateur condenseur 3\r\n" +
        "#1 := {FAN_03_RUN}\r\n" +
        "#2 := {FAN_03_FAULT}\r\n" +
        "#3 := {FAN_03_SPEED} * 0.1\r\n" +
        "#4 := \"Ventilateur 3\"\r\n" +
        "#5 := 1\r\n";

    [TestMethod]
    public void TheSpecExampleParsesIntoFiveClassifiedEntries()
    {
        var result = ParameterSheetParser.Parse(Example);

        Assert.IsTrue(result.IsValid, string.Join(" | ", result.Diagnostics.Select(d => $"{d.Line}:{d.Message}")));
        Assert.AreEqual(5, result.Entries.Count);
        Assert.AreEqual(ParameterValueKind.Tag, result.Entries[1].Kind);
        Assert.AreEqual(ParameterValueKind.Tag, result.Entries[2].Kind);
        Assert.AreEqual(ParameterValueKind.Expression, result.Entries[3].Kind);
        Assert.AreEqual(ParameterValueKind.Literal, result.Entries[4].Kind);
        Assert.AreEqual(ParameterValueKind.Expression, result.Entries[5].Kind, "R1: a numeric literal is a one-node Expression");
        Assert.AreEqual(2, result.Entries[1].Line);
        Assert.AreEqual("{FAN_03_SPEED} * 0.1", result.Entries[3].Source);
    }

    [TestMethod]
    public void ATrailingCommentIsStrippedButNotInsideAStringLiteral()
    {
        var result = ParameterSheetParser.Parse("#1 := {A} // commentaire\n#4 := \"http://x\"");

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("{A}", result.Entries[1].Source);
        Assert.AreEqual(ParameterValueKind.Literal, result.Entries[4].Kind);
        Assert.AreEqual("http://x", ((ScadaExprLiteralString)result.Entries[4].Node).Value);
    }

    [TestMethod]
    public void BlankAndWhitespaceLinesAreIgnored()
    {
        Assert.IsTrue(ParameterSheetParser.Parse("\n   \n\t\n").IsValid);
        Assert.AreEqual(0, ParameterSheetParser.Parse("\n   \n").Entries.Count);
    }

    [DataTestMethod]
    [DataRow("#0 := 1")]
    [DataRow("#1000 := 1")]
    [DataRow("#01 := 1")]
    [DataRow("1 := 1")]
    [DataRow("#1 = 1")]
    [DataRow("#1 :=")]
    public void AMalformedLineIsReportedWithItsLineNumber(string line)
    {
        var result = ParameterSheetParser.Parse("// en-tete\n" + line);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(2, result.Diagnostics.Single().Line);
    }

    [TestMethod]
    public void ADuplicatedNumberIsAnErrorNamingBothLines()
    {
        var result = ParameterSheetParser.Parse("#1 := {A}\n#2 := {B}\n#1 := {C}");

        Assert.IsFalse(result.IsValid);
        var message = result.Diagnostics.Single().Message;
        StringAssert.Contains(message, "#1");
        StringAssert.Contains(message, "1");
        Assert.AreEqual(3, result.Diagnostics.Single().Line);
    }

    [TestMethod]
    public void ARightHandSideMayNotReferenceAnotherPlaceholder()
    {
        var result = ParameterSheetParser.Parse("#1 := {#2}");

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Diagnostics.Single().Message, "#2");
    }

    [TestMethod]
    public void AnExpressionSyntaxErrorIsReportedOnItsLine()
    {
        var result = ParameterSheetParser.Parse("#1 := {A} +");

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(1, result.Diagnostics.Single().Line);
    }

    [TestMethod]
    public void ParsingIsDeterministic()
    {
        var first = ParameterSheetParser.Parse(Example);
        var second = ParameterSheetParser.Parse(Example);

        CollectionAssert.AreEqual(first.Entries.Keys.OrderBy(k => k).ToArray(), second.Entries.Keys.OrderBy(k => k).ToArray());
        Assert.AreEqual(first.Entries[3], second.Entries[3]);
    }

    [TestMethod]
    public void NineHundredNinetyNineLinesParse()
    {
        var text = string.Join("\n", Enumerable.Range(1, 999).Select(n => $"#{n} := {{T{n}}}"));

        var result = ParameterSheetParser.Parse(text);

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(999, result.Entries.Count);
    }

    [DataTestMethod]
    [DataRow("{A}", ParameterValueKind.Tag)]
    [DataRow("\"x\"", ParameterValueKind.Literal)]
    [DataRow("1", ParameterValueKind.Expression)]
    [DataRow("true", ParameterValueKind.Expression)]
    [DataRow("{A} * 0.1", ParameterValueKind.Expression)]
    [DataRow("{A} && {B}", ParameterValueKind.Expression)]
    public void ClassifyFollowsRulingR1(string source, ParameterValueKind expected)
    {
        var node = ScadaExpressionParser.Parse(source).Root!;

        Assert.AreEqual(expected, ParameterSheetParser.Classify(node));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ParameterSheetParserTests"`
Expected: FAIL — compilation error, `ParameterSheetParser` introuvable.

- [ ] **Step 3: Write the models**

```csharp
using ScadaBuilderV2.Domain.ElementEvents.Expressions;

namespace ScadaBuilderV2.Domain.QuickWindows;

/// <summary>
/// One project parameter sheet: a declarative text mapping each `#N` placeholder of a quick-window page to a
/// tag, a literal or an expression.
/// </summary>
/// <remarks>
/// <see cref="Text"/> is the single source of truth; every entry is derived from it by
/// <see cref="ParameterSheetParser"/>.
///
/// Decisions: DEC-PARAMSHEETS.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §4.2, §4.3.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetParserTests.cs.
/// </remarks>
public sealed record ParameterSheet(Guid SheetKey, string Code, string DisplayName, string Text);

/// <summary>Nature of one parameter value, derived from its parsed right-hand side (ruling R1).</summary>
public enum ParameterValueKind
{
    /// <summary>Exactly one tag reference.</summary>
    Tag,
    /// <summary>Exactly one string literal.</summary>
    Literal,
    /// <summary>Any other expression, including a lone number or boolean.</summary>
    Expression
}

/// <summary>One valid `#N := …` line of a parameter sheet.</summary>
public sealed record ParameterEntry(int Number, int Line, ParameterValueKind Kind, string Source, ScadaExprNode Node);

/// <summary>One problem found on a parameter-sheet line, 1-based.</summary>
public sealed record ParameterSheetDiagnostic(int Line, string Message);

/// <summary>The total, deterministic result of parsing one parameter-sheet text.</summary>
public sealed record ParameterSheetParseResult(
    IReadOnlyDictionary<int, ParameterEntry> Entries,
    IReadOnlyList<ParameterSheetDiagnostic> Diagnostics)
{
    /// <summary>Gets whether the text produced no diagnostic.</summary>
    public bool IsValid => Diagnostics.Count == 0;
}
```

- [ ] **Step 4: Write the parser**

```csharp
using System.Text.RegularExpressions;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;

namespace ScadaBuilderV2.Domain.QuickWindows;

/// <summary>Parses a parameter-sheet text into classified entries; total and pure.</summary>
/// <remarks>
/// A line is `#N := expression`, N from 1 to 999 without a leading zero. `//` starts a comment outside a
/// string literal. Blank lines are ignored and line order carries no meaning. A number defined twice is an
/// error rather than "last wins", which is what keeps the sheet idempotent.
///
/// Decisions: DEC-PARAMSHEETS.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §4.3.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetParserTests.cs.
/// </remarks>
public static partial class ParameterSheetParser
{
    /// <summary>Parses <paramref name="text"/>; never throws.</summary>
    public static ParameterSheetParseResult Parse(string? text)
    {
        var entries = new Dictionary<int, ParameterEntry>();
        var diagnostics = new List<ParameterSheetDiagnostic>();
        var lines = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var content = StripComment(lines[index]).Trim();
            if (content.Length == 0) continue;

            var match = LineRegex().Match(content);
            if (!match.Success)
            {
                diagnostics.Add(new(lineNumber, "Ligne invalide : attendu « #N := valeur », N de 1 à 999 sans zéro de tête."));
                continue;
            }

            var number = int.Parse(match.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var source = match.Groups["rhs"].Value.Trim();
            var parsed = ScadaExpressionParser.Parse(source);
            if (parsed.Root is null)
            {
                diagnostics.Add(new(lineNumber, $"#{number} : {string.Join(" ", parsed.Errors)}"));
                continue;
            }

            var nested = FindPlaceholderReference(parsed.Root);
            if (nested is not null)
            {
                diagnostics.Add(new(lineNumber, $"#{number} ne peut pas référencer le paramètre {nested}."));
                continue;
            }

            if (entries.TryGetValue(number, out var first))
            {
                diagnostics.Add(new(lineNumber, $"#{number} est déjà défini à la ligne {first.Line}."));
                continue;
            }

            entries[number] = new ParameterEntry(number, lineNumber, Classify(parsed.Root), source, parsed.Root);
        }

        return new ParameterSheetParseResult(entries, diagnostics);
    }

    /// <summary>Classifies one parsed right-hand side (ruling R1).</summary>
    public static ParameterValueKind Classify(ScadaExprNode node) => node switch
    {
        ScadaExprTagRef => ParameterValueKind.Tag,
        ScadaExprLiteralString => ParameterValueKind.Literal,
        _ => ParameterValueKind.Expression
    };

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inString = !inString;
            else if (!inString && line[i] == '/' && i + 1 < line.Length && line[i + 1] == '/') return line[..i];
        }

        return line;
    }

    private static string? FindPlaceholderReference(ScadaExprNode node) => node switch
    {
        ScadaExprTagRef tag when AnyPlaceholderRegex().IsMatch(tag.TagName) => tag.TagName,
        ScadaExprUnary unary => FindPlaceholderReference(unary.Operand),
        ScadaExprBinary binary => FindPlaceholderReference(binary.Left) ?? FindPlaceholderReference(binary.Right),
        ScadaExprFunc func => func.Args.Select(FindPlaceholderReference).FirstOrDefault(found => found is not null),
        _ => null
    };

    [GeneratedRegex(@"^#(?<n>[1-9][0-9]{0,2})\s*:=\s*(?<rhs>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex LineRegex();

    [GeneratedRegex(@"^#[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AnyPlaceholderRegex();
}
```

`AnyPlaceholderRegex` refuse toute référence `{#…}`, valide ou non : cette tâche ne dépend donc pas de `QuickWindowPlaceholder` (Task 3).

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ParameterSheetParserTests"`
Expected: PASS, 17 tests.

- [ ] **Step 6: Version, changelog, commit**

```bash
git add src/ScadaBuilderV2.Domain/QuickWindows/ tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetParserTests.cs VERSION docs/
git commit -m "feat: parse quick-window parameter sheets"
```

---

### Task 3: Jetons `#N` et modèle de projet

**Files:**
- Create: `src/ScadaBuilderV2.Domain/QuickWindows/QuickWindowPlaceholder.cs`
- Modify: `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs:16-22` (enum), `:81-111` (`ScadaProject`)
- Modify: `src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBinding.cs:29-46`
- Modify: `src/ScadaBuilderV2.Domain/QuickWindows/QuickWindowInvocationModels.cs:34-40`
- Test: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPlaceholderTests.cs`, `tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetProjectModelTests.cs`

**Interfaces:**
- Produces:
  - `static bool QuickWindowPlaceholder.TryParseName(string? value, out int number)` — vrai seulement pour une chaîne exactement `#N`, 1..999, sans zéro de tête.
  - `static IReadOnlyList<QuickWindowPlaceholderToken> QuickWindowPlaceholder.FindInText(string? text)` avec `record QuickWindowPlaceholderToken(string Raw, int Index, int? Number)` (`Number` nul si `#0`, `#01`…).
  - `static string QuickWindowPlaceholder.UnescapeText(string text)`
  - `ScadaPageType.QuickWindow` (dernière valeur de l'enum)
  - `ScadaProject.ParameterSheets : IReadOnlyList<ParameterSheet>?` et `EffectiveParameterSheets`
  - `ScadaCommandBinding.ParameterSheetKey : Guid?`, `ScadaCommandBinding.QuickWindowPageKey : Guid?`
  - `QuickWindowBinding.ExpressionAst : ScadaExprNode?`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text.Json;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.Tests.QuickWindows;

[TestClass]
public sealed class QuickWindowPlaceholderTests
{
    [DataTestMethod]
    [DataRow("#1", true, 1)]
    [DataRow("#999", true, 999)]
    [DataRow("#0", false, 0)]
    [DataRow("#01", false, 0)]
    [DataRow("#1000", false, 0)]
    [DataRow(" #1", false, 0)]
    [DataRow("FAN_03", false, 0)]
    public void TryParseNameAcceptsOnlyAnExactValidToken(string value, bool expected, int expectedNumber)
    {
        Assert.AreEqual(expected, QuickWindowPlaceholder.TryParseName(value, out var number));
        if (expected) Assert.AreEqual(expectedNumber, number);
    }

    [TestMethod]
    public void FindInTextSkipsEscapesAndLongerNumbers()
    {
        var tokens = QuickWindowPlaceholder.FindInText(@"Ventilateur #4 - pompe \#2 - #10 - #1000");

        CollectionAssert.AreEqual(new int?[] { 4, 10 }, tokens.Where(t => t.Number is not null).Select(t => t.Number).ToArray());
        Assert.IsFalse(tokens.Any(t => t.Raw == "#2"));
        Assert.IsFalse(tokens.Any(t => t.Raw == "#1"), "#1 inside #1000 is not a token");
    }

    [TestMethod]
    public void FindInTextReportsInvalidTokensWithoutANumber()
    {
        var token = QuickWindowPlaceholder.FindInText("Valeur #0").Single();

        Assert.AreEqual("#0", token.Raw);
        Assert.IsNull(token.Number);
    }

    [TestMethod]
    public void UnescapeTurnsBackslashHashIntoHash()
    {
        Assert.AreEqual("Pompe #2", QuickWindowPlaceholder.UnescapeText(@"Pompe \#2"));
    }
}

[TestClass]
public sealed class ParameterSheetProjectModelTests
{
    [TestMethod]
    public void QuickWindowIsAppendedLastSoNoPersistedValueMoves()
    {
        var values = Enum.GetValues<ScadaPageType>();

        Assert.AreEqual(ScadaPageType.QuickWindow, values[^1]);
        Assert.AreEqual(0, (int)ScadaPageType.Default);
        Assert.AreEqual(1, (int)ScadaPageType.Fragment);
    }

    [TestMethod]
    public void ACommandRoundTripsItsSheetAndPageKeys()
    {
        var sheet = Guid.NewGuid();
        var page = Guid.NewGuid();
        var command = new ScadaCommandBinding("c1", "Ouvrir", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
            ParameterSheetKey: sheet, QuickWindowPageKey: page);

        var back = JsonSerializer.Deserialize<ScadaCommandBinding>(JsonSerializer.Serialize(command))!;

        Assert.AreEqual(sheet, back.ParameterSheetKey);
        Assert.AreEqual(page, back.QuickWindowPageKey);
    }

    [TestMethod]
    public void AProjectWithoutSheetsHasAnEmptyEffectiveList()
    {
        Assert.AreEqual(0, ScadaProject.CreateDefault("p").EffectiveParameterSheets.Count);
    }
}
```


- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~QuickWindowPlaceholderTests|FullyQualifiedName~ParameterSheetProjectModelTests"`
Expected: FAIL — compilation, membres introuvables.

- [ ] **Step 3: Implement `QuickWindowPlaceholder`**

```csharp
using System.Text.RegularExpressions;

namespace ScadaBuilderV2.Domain.QuickWindows;

/// <summary>One `#…` token found in a displayed text; <see cref="Number"/> is null for an invalid token.</summary>
public sealed record QuickWindowPlaceholderToken(string Raw, int Index, int? Number);

/// <summary>Recognises `#N` placeholder tokens of a quick-window page.</summary>
/// <remarks>
/// A token is `#` followed by 1 to 3 digits and not by a further digit; `\#` escapes it. Tokens are only
/// searched in texts and binding fields, never in styles: `#0F2A30` is a colour.
///
/// Decisions: DEC-PARAMSHEETS.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §4.4.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPlaceholderTests.cs.
/// </remarks>
public static partial class QuickWindowPlaceholder
{
    /// <summary>Returns whether <paramref name="value"/> is exactly one valid token, `#1` to `#999`.</summary>
    public static bool TryParseName(string? value, out int number)
    {
        number = 0;
        if (value is null || !ExactRegex().IsMatch(value)) return false;
        number = int.Parse(value.AsSpan(1), System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Finds every unescaped token of <paramref name="text"/>, valid or not.</summary>
    public static IReadOnlyList<QuickWindowPlaceholderToken> FindInText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        return TokenRegex().Matches(text)
            .Select(match => new QuickWindowPlaceholderToken(
                match.Value,
                match.Index,
                TryParseName(match.Value, out var number) ? number : null))
            .ToArray();
    }

    /// <summary>Turns every `\#` into a literal `#`.</summary>
    public static string UnescapeText(string text) => text.Replace(@"\#", "#", StringComparison.Ordinal);

    [GeneratedRegex(@"^#[1-9][0-9]{0,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex ExactRegex();

    [GeneratedRegex(@"(?<!\\)#[0-9]{1,3}(?![0-9A-Fa-f])", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
```

La garde `(?![0-9A-Fa-f])` exclut aussi `#1B7A1B` et `#0F2A30` écrits dans un texte : une couleur n'est jamais un paramètre, même hors des styles.

- [ ] **Step 4: Extend the models**

Dans `ProjectModels.cs`, enum :

```csharp
public enum ScadaPageType
{
    Default,
    Fragment,
    Header,
    Footer,
    QuickWindow
}
```

`ScadaProject` : ajouter après `QuickWindowInvocations` le paramètre `IReadOnlyList<ParameterSheet>? ParameterSheets = null,` (avant `FormatVersion`) et la propriété :

```csharp
    [JsonIgnore]
    public IReadOnlyList<ParameterSheet> EffectiveParameterSheets => ParameterSheets ?? Array.Empty<ParameterSheet>();
```

`ScadaCommandBinding` : ajouter en fin de paramètres `Guid? ParameterSheetKey = null, Guid? QuickWindowPageKey = null`.

`QuickWindowBinding` : ajouter en fin de paramètres `ScadaExprNode? ExpressionAst = null` (using `ScadaBuilderV2.Domain.ElementEvents.Expressions`).

Rechercher par `grep -rn "new ScadaProject(" src tests` les constructions positionnelles de `ScadaProject` : un paramètre inséré avant `FormatVersion` décale les appels positionnels qui passent `FormatVersion` sans nom. Les corriger en arguments nommés.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~QuickWindowPlaceholderTests|FullyQualifiedName~ParameterSheetProjectModelTests|FullyQualifiedName~ParameterSheetParserTests"`
Expected: PASS.

- [ ] **Step 6: Full suite** — `dotnet test ScadaBuilderV2.sln` : 1020 + nouveaux, 0 échec.

- [ ] **Step 7: Version, changelog, commit**

```bash
git add src/ScadaBuilderV2.Domain/ tests/ScadaBuilderV2.Tests/QuickWindows/ VERSION docs/
git commit -m "feat: add quick-window page type, parameter sheets and placeholder tokens to the model"
```

---

### Task 4: Persistance des feuilles

**Files:**
- Create: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ParameterSheetStore.cs`
- Modify: `src/ScadaBuilderV2.Infrastructure/ModernProjects/ModernProjectStore.cs` (chargement ≈ l.186, écriture transactionnelle ≈ l.257-345, contrôle de chemin ≈ l.537, suppression ≈ l.616, résolution ≈ l.695)
- Test: `tests/ScadaBuilderV2.Tests/ParameterSheetStoreTests.cs`

**Interfaces:**
- Consumes: `ParameterSheet`, `ScadaProject.ParameterSheets`.
- Produces: `static string ParameterSheetStore.GetRelativePath(Guid sheetKey)` → `parameter-sheets/{key:N}.parameter-sheet.json` ; `ResolveContainedParameterSheetPath(string projectRoot, string relativePath)` ; `StageJsonAsync(...)` ; `LoadAllAsync(string projectRoot, CancellationToken)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ParameterSheetStoreTests
{
    [TestMethod]
    public void TheRelativePathIsUnderParameterSheets()
    {
        var key = Guid.Parse("11111111-2222-3333-4444-555555555555");

        Assert.AreEqual("parameter-sheets/11111111222233334444555555555555.parameter-sheet.json", ParameterSheetStore.GetRelativePath(key));
    }

    [TestMethod]
    public void APathOutsideTheFolderIsRefused()
    {
        Assert.ThrowsException<InvalidOperationException>(() =>
            ParameterSheetStore.ResolveContainedParameterSheetPath(Path.GetTempPath(), "../evil.parameter-sheet.json"));
    }

    [TestMethod]
    public async Task ASavedProjectReloadsItsSheetsUnchanged()
    {
        var root = Path.Combine(Path.GetTempPath(), "psheet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var sheet = new ParameterSheet(Guid.NewGuid(), "ventilateur_3", "Ventilateur 3", "#1 := {FAN_03_RUN}\r\n// fin\r\n");
            var project = ScadaProject.CreateDefault("p") with { ParameterSheets = [sheet] };
            var store = new ModernProjectStore();

            await store.SaveAsync(root, project, new Dictionary<Guid, ScadaScene>());
            var reloaded = await store.LoadAsync(root);

            Assert.AreEqual(sheet, reloaded.Project.EffectiveParameterSheets.Single());
            Assert.IsTrue(File.Exists(Path.Combine(root, "parameter-sheets", $"{sheet.SheetKey:N}.parameter-sheet.json")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task ARemovedSheetFileIsDeletedOnSave()
    {
        var root = Path.Combine(Path.GetTempPath(), "psheet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var sheet = new ParameterSheet(Guid.NewGuid(), "a", "A", "#1 := 1");
            var store = new ModernProjectStore();
            await store.SaveAsync(root, ScadaProject.CreateDefault("p") with { ParameterSheets = [sheet] }, new Dictionary<Guid, ScadaScene>());

            await store.SaveAsync(root, ScadaProject.CreateDefault("p"), new Dictionary<Guid, ScadaScene>());

            Assert.IsFalse(File.Exists(Path.Combine(root, "parameter-sheets", $"{sheet.SheetKey:N}.parameter-sheet.json")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
```

Avant d'écrire ces tests, lire `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs` et **reprendre exactement** la façon dont il appelle `SaveAsync`/`LoadAsync` et nomme le résultat du chargement ; ajuster les deux appels ci-dessus à ces signatures réelles.

- [ ] **Step 2: Run to verify failure** — `dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ParameterSheetStoreTests"` → FAIL (compilation).

- [ ] **Step 3: Implement `ParameterSheetStore`** — copier la structure de `QuickWindowStore.cs` (même `JsonSerializerOptions`, même `StageJsonAsync`, même contrôle de confinement) en remplaçant `quick-windows/` par `parameter-sheets/`, `.quick-window.json` par `.parameter-sheet.json`, `QuickWindowDefinition` par `ParameterSheet` et `DefinitionKey` par `SheetKey`. Message d'erreur de confinement : `$"ParameterSheet path must be under parameter-sheets/: {relativePath}"`.

- [ ] **Step 4: Integrate in `ModernProjectStore`** — à chaque endroit où `QuickWindowStore` intervient, ajouter le même traitement pour `ParameterSheetStore` : chargement dans `project.ParameterSheets` ; `project.json` écrit avec `ParameterSheets = null` (les fichiers dédiés font foi, comme pour les fenêtres rapides) ; mise en scène transactionnelle ; calcul et application des suppressions ; contrôle de confinement ; résolution du chemin.

- [ ] **Step 5: Run to verify pass** — le filtre de Step 2 passe ; `dotnet test --filter "FullyQualifiedName~ModernProjectStoreTests"` reste vert.

- [ ] **Step 6: Version, changelog (`docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md` : dossier `parameter-sheets/`), commit**

```bash
git add src/ScadaBuilderV2.Infrastructure/ tests/ScadaBuilderV2.Tests/ParameterSheetStoreTests.cs VERSION docs/
git commit -m "feat: persist parameter sheets under parameter-sheets/"
```

---

### Task 5: Abaissement et validation par couple

**Files:**
- Create: `src/ScadaBuilderV2.Application/QuickWindows/QuickWindowKeys.cs`
- Create: `src/ScadaBuilderV2.Application/QuickWindows/QuickWindowPlaceholderUsageCollector.cs`
- Create: `src/ScadaBuilderV2.Application/QuickWindows/ParameterSheetLowering.cs`
- Test: `tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetLoweringTests.cs`

**Interfaces:**
- Consumes: Task 2 et 3 ; `ScadaExpressionValidator.TryResolveTagReference(string value, ScadaTagCatalog? catalog) : TagResolveResult` (`Status`, `CanonicalId`) ; `VisualContent.FromScene(ScadaScene)`.
- Produces:
  - `enum PlaceholderUsageSite { ReadTag, WriteTag, StateExpression, ReadVariable, CommandWriteTag, CommandReadTag, Text }`
  - `record PlaceholderUsage(int Number, PlaceholderUsageSite Site, string ElementId, string? CommandId)`
  - `record InvalidPlaceholderToken(string Raw, string ElementId)`
  - `static (IReadOnlyList<PlaceholderUsage> Usages, IReadOnlyList<InvalidPlaceholderToken> Invalid) QuickWindowPlaceholderUsageCollector.Collect(ScadaScene scene)`
  - `static Guid QuickWindowKeys.Member(Guid pageKey, int number)`, `static Guid QuickWindowKeys.Invocation(Guid ownerPageKey, string elementId, string commandId)`
  - `record ParameterSheetLoweringResult(ScadaProject Project, IReadOnlyDictionary<Guid, ScadaScene> Scenes, IReadOnlyList<ScadaBuildValidationIssue> Diagnostics)`
  - `static ParameterSheetLoweringResult ParameterSheetLowering.Lower(ScadaProject project, IReadOnlyDictionary<Guid, ScadaScene> scenes)`

- [ ] **Step 1: Write the failing tests**

Construire les scènes avec les constructeurs réels : lire d'abord `tests/ScadaBuilderV2.Tests/QuickWindows/Win00054QuickWindowIntegrationTests.cs`, qui monte déjà scènes, éléments, catalogue et commandes, et reprendre ses fabriques. Les tests à écrire :

```csharp
[TestClass]
public sealed class ParameterSheetLoweringTests
{
    // Fabriques locales : Page(Guid key, ScadaPageType type, params ScadaElement[]),
    // TextElement(id, text), ReadElement(id, tagId), WriteCommandElement(id, writeTagId),
    // StateElement(id, expressionSource), OpenCommand(elementId, sheetKey, pageKey),
    // Catalog("FAN_03_RUN", "FAN_03_SPEED") — reprises de Win00054QuickWindowIntegrationTests.

    [TestMethod] public void AQuickWindowPageBecomesADefinitionKeyedByItsPageKeyWithOneMemberPerUsedPlaceholder() { }
    [TestMethod] public void AnOpenCommandBecomesAnInvocationWithOneBindingPerUsedPlaceholder() { }
    [TestMethod] public void BindingKindsFollowTheSheetClassification() { }            // Tag → TagId canonique, Literal → LiteralValue, Expression → Expression + ExpressionAst
    [TestMethod] public void TheCommandReceivesItsDeterministicInvocationKey() { }
    [TestMethod] public void LoweringTwiceIsByteIdentical() { }                          // JSON des deux résultats égal
    [TestMethod] public void AQuickWindowPageIsRemovedFromExportedPages() { }            // absente de Scenes et de Project.Scenes du résultat
    [TestMethod] public void APlaceholderUsedButUndefinedIsAnError() { }                 // code parameter-sheet.placeholder-undefined, nomme feuille, page, #N
    [TestMethod] public void AWriteToANonTagIsAnError() { }                              // parameter-sheet.write-not-tag
    [TestMethod] public void APlaceholderInATextBoundToATagIsAnError() { }               // R2 : parameter-sheet.text-not-literal
    [TestMethod] public void AnUnknownTagIsAnError() { }                                 // parameter-sheet.tag-unknown, nomme la ligne
    [TestMethod] public void ASheetSyntaxErrorIsAnErrorCarryingItsLine() { }             // parameter-sheet.syntax
    [TestMethod] public void ADefinedButUnusedPlaceholderIsOnlyAWarning() { }            // parameter-sheet.placeholder-unused, Severity Warning
    [TestMethod] public void AnInvalidTokenInATextIsAnError() { }                        // "#0" → parameter-sheet.token-invalid
    [TestMethod] public void HexColoursAreNeverPlaceholders() { }                        // Review Focus 1 : styles #0F2A30 et texte "#1B7A1B" → 0 usage
    [TestMethod] public void ACommandPointingAtADeletedSheetIsAnErrorNotAnException() { }    // Review Focus 2 : parameter-sheet.sheet-missing
    [TestMethod] public void ACommandPointingAtAPageThatIsNoLongerAQuickWindowIsAnError() { } // Review Focus 2 : parameter-sheet.page-not-quick-window
    [TestMethod] public void TheFragmentPageOfTheReferenceProjectHasNoPlaceholder() { }  // charge projects/TEST_CONVERSION/scenes/win00034.scene.json (ignorer si absent : Assert.Inconclusive), 0 usage, 0 invalide
}
```

Chaque méthode vide ci-dessus est à **écrire intégralement** : arranger une page, une feuille et une commande minimales, appeler `ParameterSheetLowering.Lower`, et asserter sur le résultat nommé en commentaire — code de diagnostic exact, sévérité exacte, et au moins une sous-chaîne du message (nom de feuille, `#N`, numéro de ligne). Aucun test ne doit se contenter de « un diagnostic existe ».

- [ ] **Step 2: Run to verify failure** — filtre `ParameterSheetLoweringTests` → FAIL (compilation).

- [ ] **Step 3: Implement `QuickWindowKeys`**

```csharp
using System.Security.Cryptography;
using System.Text;

namespace ScadaBuilderV2.Application.QuickWindows;

/// <summary>Deterministic keys of lowered quick-window structures (ruling R4).</summary>
/// <remarks>Same derivation as <c>PageIdentityModels.CreateDeterministic</c>. Decisions: DEC-PARAMSHEETS.</remarks>
public static class QuickWindowKeys
{
    /// <summary>Key of the interface member lowered from placeholder <paramref name="number"/> of a page.</summary>
    public static Guid Member(Guid pageKey, int number) =>
        Create($"scada-builder-v2-qw-member|{pageKey:N}|{number}");

    /// <summary>Key of the invocation lowered from one open-quick-window command.</summary>
    public static Guid Invocation(Guid ownerPageKey, string elementId, string commandId) =>
        Create($"scada-builder-v2-qw-invocation|{ownerPageKey:N}|{elementId}|{commandId}");

    private static Guid Create(string canonical)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
```

- [ ] **Step 4: Implement the collector** — parcours récursif de `scene.Elements` et `ChildElements`, et des cellules de `Table` (`ScadaTableCell.ValueBindings.ReadTagId/WriteTagId`). Emplacements lus, et **uniquement** eux :

| Champ | Site |
| --- | --- |
| `Data.ReadTagId`, `Data.TagBinding` valant exactement `#N` | `ReadTag` |
| `Data.WriteTagId` valant exactement `#N` | `WriteTag` |
| `StateConfig.States[].Expression.Ast` : tout `ScadaExprTagRef` dont `TagName` est `#N` | `StateExpression` |
| `StateConfig.ReadVariable.TagId` valant `#N` | `ReadVariable` |
| `CommandConfig.Commands[].WriteTagId` / `.ReadTagId` valant `#N` | `CommandWriteTag` / `CommandReadTag` |
| `Data.Text`, `LegacyPayload.Text` via `QuickWindowPlaceholder.FindInText` | `Text` ; tout jeton à `Number` nul va dans `Invalid` |

Jamais `Style`, jamais les couleurs.

- [ ] **Step 5: Implement `ParameterSheetLowering.Lower`**, dans cet ordre :
  1. Indexer les feuilles par `SheetKey`, les analyser une fois chacune (`ParameterSheetParser.Parse`) ; chaque diagnostic d'analyse devient `parameter-sheet.syntax` (Error) avec `$"Feuille '{sheet.DisplayName}', ligne {line} : {message}"`.
  2. Pour chaque page `QuickWindow` : collecter ses usages ; construire la définition `new QuickWindowDefinition(page.PageKey, page.EffectivePageCode, page.Title, InterfaceVersion: 1, VisualContent.FromScene(scene), members, new QuickWindowPresentationDefaults())`, un membre `new QuickWindowInterfaceMember(QuickWindowKeys.Member(page.PageKey, n), $"#{n}", Family, DataType, Access)` par numéro utilisé — `Family = WriteCommand`/`Access = Write` si un usage est `WriteTag` ou `CommandWriteTag`, sinon `ReadState`/`Read` ; `DataType` = `String` (spec §6, type informatif).
  3. Pour chaque élément de chaque page et chaque commande `OpenQuickWindow` portant `ParameterSheetKey` et `QuickWindowPageKey` : vérifier feuille existante (`parameter-sheet.sheet-missing`), page existante et de type `QuickWindow` (`parameter-sheet.page-not-quick-window`) ; puis pour chaque `#N` utilisé par la page : absent de la feuille → `parameter-sheet.placeholder-undefined` ; usage d'écriture et nature ≠ `Tag` → `parameter-sheet.write-not-tag` ; usage `Text` et nature ≠ `Literal` → `parameter-sheet.text-not-literal` ; nature `Tag` → `TryResolveTagReference(tagName, project.TagCatalog)` non résolu → `parameter-sheet.tag-unknown`. Chaque `#N` défini non utilisé → `parameter-sheet.placeholder-unused` (Warning).
  4. Construire la liaison : `Tag` → `QuickWindowBinding(memberKey, Tag, TagId: canonicalId)` ; `Literal` → `QuickWindowBinding(memberKey, Literal, LiteralValue: ((ScadaExprLiteralString)node).Value)` ; `Expression` → `QuickWindowBinding(memberKey, Expression, Expression: source, ExpressionAst: node)`. Invocation `new QuickWindowInvocation(QuickWindowKeys.Invocation(ownerPageKey, element.Id, command.Id), page.PageKey, bindings, InterfaceVersion: 1, OwnerElementId: element.Id, OwnerCommandId: command.Id)` — **sans** `OwnerPageKey` (la validation de paquet interdit les propriétés `*PageKey`, cf. `KNOWN_GAPS_V2.md` entrée 30).
  5. Réécrire la commande avec `QuickWindowInvocationKey` = la clé calculée, dans une copie de la scène propriétaire.
  6. Retirer les pages `QuickWindow` de `Project.Scenes` et du dictionnaire de scènes du résultat ; poser `QuickWindows` et `QuickWindowInvocations` du projet résultat. Les définitions et invocations déjà présentes dans le projet d'entrée sont **conservées** telles quelles.
  7. Chaque diagnostic est un `ScadaBuildValidationIssue` de sévérité `Error` sauf `placeholder-unused`, avec `PropertyPath` = `$"ParameterSheets[{sheet.Code}]"` ou `$"Scenes[{page.EffectivePageCode}].{elementId}"`.

- [ ] **Step 6: Run to verify pass** — filtre `ParameterSheetLoweringTests` → PASS ; suite complète verte.

- [ ] **Step 7: Version, changelog, commit**

```bash
git add src/ScadaBuilderV2.Application/QuickWindows/ tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetLoweringTests.cs VERSION docs/
git commit -m "feat: lower parameter sheets and quick-window pages onto DEC-0050 structures"
```

---

### Task 6: Manifeste et branchement de l'abaissement

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/QuickWindows/Ft100QuickWindowManifestModels.cs:60-66`
- Modify: `src/ScadaBuilderV2.Rendering/QuickWindows/QuickWindowCompiler.cs:137-143`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs` (export ≈ l.4484-4500 ; build/validation ≈ l.4555 ; toute autre capture `CaptureSnapshotAsync` suivie de `ScadaProjectBuildValidator.Validate`)
- Test: `tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetExportTests.cs`

**Interfaces:**
- Consumes: `ParameterSheetLowering.Lower` (Task 5).
- Produces: `Ft100QuickWindowBinding.ExpressionAst : JsonElement?` sérialisé en camelCase avec discriminant `type`.

- [ ] **Step 1: Write the failing test** — exporter par `Ft100SceneExporter.ExportProjectAsync` le résultat de `Lower` pour une page d'appel, une page `QuickWindow` utilisant `#1` (lecture), `#3` (condition d'état) et `#4` (texte), et une feuille `#1 := {FAN_03_RUN}`, `#3 := {FAN_03_SPEED} * 0.1`, `#4 := "Ventilateur 3"` ; lire `manifest.json` du dossier d'export ; asserter :
  - `QuickWindows[0].DefinitionKey` = `PageKey` de la page ;
  - la liaison de `#3` a `SourceKind` `Expression` et `ExpressionAst.type` = `"binary"` ;
  - la liaison de `#4` a `LiteralValue` `"Ventilateur 3"` et pas d'`ExpressionAst` ;
  - aucune page exportée n'a pour identifiant le code de la page `QuickWindow` ;
  - le HTML du fragment de la définition contient `#1`, `#3` et `#4` inchangés.

Reprendre le montage d'export de `Win00054QuickWindowIntegrationTests` (catalogue synthétique, dossier temporaire).

- [ ] **Step 2: Run to verify failure** — filtre `ParameterSheetExportTests` → FAIL.

- [ ] **Step 3: Implement** — ajouter `JsonElement? ExpressionAst = null` à `Ft100QuickWindowBinding`. L'AST doit sortir **exactement** comme celui des conditions d'état que l'exporteur écrit déjà dans `data-scada-state-config` (`Ft100SceneExporter.cs` ≈ l.1240) : même discriminant `type`, mêmes noms de propriétés, mêmes énumérations en chaînes. Passer donc `private static readonly JsonSerializerOptions StateCommandJsonOptions` (`Ft100SceneExporter.cs:45`) en `internal static`, et dans `QuickWindowCompiler.ToManifestBinding` : `binding.ExpressionAst is null ? null : JsonSerializer.SerializeToElement(binding.ExpressionAst, Ft100SceneExporter.StateCommandJsonOptions)`. Ne pas créer de seconde configuration : deux sérialisations divergentes produiraient des nœuds que l'évaluateur ne reconnaît pas. Ajouter au test de Step 1 une assertion comparant le `op` de l'`ExpressionAst` exporté à celui d'une condition d'état équivalente exportée dans la même page d'appel.

- [ ] **Step 4: Brancher l'abaissement dans l'application** — dans chaque chemin qui valide ou exporte un instantané (`MainWindow.xaml.cs` ≈ l.4484 et ≈ l.4555) :

```csharp
var workspaceSnapshot = await _pageWorkspaceController.CaptureSnapshotAsync();
var lowered = ParameterSheetLowering.Lower(workspaceSnapshot.Project, workspaceSnapshot.Scenes);
var projectSnapshot = lowered.Project;
var validationIssues = lowered.Diagnostics
    .Concat(ScadaProjectBuildValidator.Validate(projectSnapshot, lowered.Scenes.Values.ToArray()))
    .ToArray();
```

puis passer `lowered.Scenes` à `BuildFt100ProjectExportInputsAsync` (adapter sa signature pour recevoir les scènes au lieu de les relire). Ne pas réaffecter `_modernProject` à la projection abaissée : l'éditeur continue de travailler sur le modèle d'authoring.

- [ ] **Step 5: Run to verify pass** — filtre → PASS ; suite complète verte ; `Win00054QuickWindowIntegrationTests` inchangé et vert.

- [ ] **Step 6: Version, changelog (contrat §12.4 déjà modifié en Task 1 : ne pas le redocumenter, citer `DEC-PARAMSHEETS`), commit**

```bash
git add src/ tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetExportTests.cs VERSION docs/
git commit -m "feat: export lowered quick windows with their expression ASTs"
```

---

### Task 7: Substitution au montage dans le runtime partagé

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/quick-window-runtime.js` (`resolveInvocation` ≈ l.103-181, `readPort` ≈ l.317-337, `mountInstance` ≈ l.280-300)
- Test: `tests/runtime-js/quick-window-placeholder-substitution.test.mjs`

**Interfaces:**
- Consumes: manifeste de Task 6 (`Bindings[].ExpressionAst`).
- Produces: `ScadaRuntime.QuickWindow.substituteInstance(root, ports) : {ok, code, detail}` exporté pour les tests ; `mountInstance` l'appelle avant `initPage` et refuse le montage si `ok` est faux.

- [ ] **Step 1: Write the failing tests** — lire d'abord `tests/runtime-js/harness.mjs` et `tests/runtime-js/quick-window-runtime.test.mjs` et reprendre leur DOM simulé et leur chargement du runtime. Tests :

```js
import test from 'node:test';
import assert from 'node:assert/strict';
// + import du harness existant (même forme que quick-window-runtime.test.mjs)

const ports = {
  '#1': { sourceKind: 'Tag', available: true, tagId: 'tf100.mapping.196' },
  // L'évaluateur normalise l'opérateur (_canonicalOperator), la casse de `op` est donc indifférente.
  '#3': { sourceKind: 'Expression', available: true, expressionAst: { type: 'binary', op: 'Multiply',
          left: { type: 'tagRef', tagName: 'FAN_03_SPEED' }, right: { type: 'literalNumber', value: 0.1 } } },
  '#4': { sourceKind: 'Literal', available: true, literalValue: 'Ventilateur 3' },
};

test('read and write tag attributes equal to #N become the bound tag id', () => { /* data-scada-read-tag="#1" → tf100.mapping.196 */ });
test('a write tag bound to a non-tag refuses the mount', () => { /* code placeholder-write-not-tag */ });
test('a state expression tagRef #3 is replaced by the bound AST and the JSON stays valid', () => { /* JSON.parse du nouvel attribut, walk */ });
test('a command config writeTagId #1 becomes the tag id', () => { });
test('a text node #4 becomes the literal through textContent', () => { /* et \\#2 devient #2 */ });
test('a text node bound to a tag refuses the mount', () => { /* placeholder-text-not-literal */ });
test('an unbound placeholder refuses the mount instead of showing #N', () => { /* placeholder-unbound */ });
test('style attributes with hex colours are never touched', () => { /* style="color:#0F2A30" inchangé ; Review Focus 1 */ });
test('two successive instances of the same page show their own values', () => { /* Review Focus 3 */ });
test('999 placeholders are substituted in a single pass', () => { /* Review Focus 5 : compter les parcours via un espion sur querySelectorAll ou TreeWalker */ });
test('readPort evaluates an Expression port from its AST', () => { /* régression de l'écart KNOWN_GAPS 33 */ });
```

Chaque test est à écrire intégralement dans le style des tests voisins, avec des assertions sur les valeurs exactes.

- [ ] **Step 2: Run to verify failure**

Run: `cd tests/runtime-js && node --test quick-window-placeholder-substitution.test.mjs`
Expected: FAIL — `substituteInstance` n'existe pas.

- [ ] **Step 3: Porter l'AST dans les ports** — dans `resolveInvocation`, ajouter au port lié `expressionAst: bound.ExpressionAst || null`. Dans `readPort`, branche `Expression` : `evaluator.evaluate(port.expressionAst)` (l'évaluateur marche un AST ; la chaîne source n'est pas évaluable). Étendre `_isInjection` aux valeurs chaînes contenues dans `ExpressionAst` (`tagName`, `value` des `literalString`).

- [ ] **Step 4: Implement `substituteInstance`**

```js
  var PLACEHOLDER_EXACT = /^#([1-9][0-9]{0,2})$/;
  var PLACEHOLDER_IN_TEXT = /(\\?)#([0-9]{1,3})(?![0-9A-Fa-f])/g;

  function _refuse(code, detail) {
    _diagnose(code, detail);
    return { ok: false, code: code, detail: detail };
  }

  function _tagFor(ports, name) {
    var port = ports[name];
    if (!port || !port.available) return { error: 'placeholder-unbound' };
    if (port.sourceKind !== 'Tag' || !port.tagId) return { error: 'placeholder-write-not-tag' };
    return { tagId: port.tagId };
  }

  function _nodeFor(port) {
    if (port.sourceKind === 'Tag') return { type: 'tagRef', tagName: port.tagId };
    if (port.sourceKind === 'Literal') return { type: 'literalString', value: String(port.literalValue) };
    return port.expressionAst;
  }

  function _substituteAst(node, ports) {
    if (!node || typeof node !== 'object') return { node: node };
    if (node.type === 'tagRef' && PLACEHOLDER_EXACT.test(node.tagName || '')) {
      var port = ports[node.tagName];
      if (!port || !port.available) return { error: 'placeholder-unbound', detail: node.tagName };
      return { node: _nodeFor(port) };
    }
    var copy = {};
    for (var key in node) {
      if (!Object.prototype.hasOwnProperty.call(node, key)) continue;
      var value = node[key];
      if (value && typeof value === 'object') {
        if (Array.isArray(value)) {
          var items = [];
          for (var i = 0; i < value.length; i++) {
            var item = _substituteAst(value[i], ports);
            if (item.error) return item;
            items.push(item.node);
          }
          copy[key] = items;
        } else {
          var child = _substituteAst(value, ports);
          if (child.error) return child;
          copy[key] = child.node;
        }
      } else {
        copy[key] = value;
      }
    }
    return { node: copy };
  }
```

puis `substituteInstance(root, ports)` qui, en **un seul parcours** de `root.querySelectorAll('[data-scada-read-tag],[data-scada-write-tag],[data-scada-state-config],[data-scada-command-config]')` puis d'un `TreeWalker` sur les nœuds texte :
  - `data-scada-read-tag` / `data-scada-write-tag` valant exactement `#N` → `_tagFor` ; erreur → `_refuse`.
  - `data-scada-state-config` : `JSON.parse`, `_substituteAst` sur chaque `states[i].expression`, `readVariable.tagId` exactement `#N` → `_tagFor`, puis `setAttribute(…, JSON.stringify(config))`.
  - `data-scada-command-config` : `JSON.parse`, pour chaque `commands[i]` les champs `writeTagId` et `readTagId` exactement `#N` → `_tagFor`, puis re-sérialiser.
  - nœud texte : remplacer chaque correspondance de `PLACEHOLDER_IN_TEXT` ; si le groupe 1 vaut `\`, écrire `#` + chiffres ; sinon port `Literal` → sa valeur, port absent → refus `placeholder-unbound`, autre nature → refus `placeholder-text-not-literal`. Écrire par `node.textContent = …` uniquement.
  - ne jamais lire ni écrire d'attribut `style`.

Dans `mountInstance`, entre `context.root = root || null;` et `context.state = 'Active';` :

```js
    if (root) {
      var substituted = substituteInstance(root, context.ports || {});
      if (!substituted.ok) {
        disposeInstance(runtimeInstanceId);
        return { ok: false, code: substituted.code };
      }
    }
```

et exporter `substituteInstance` dans l'objet public en fin de fichier.

- [ ] **Step 5: Run to verify pass** — `cd tests/runtime-js && node --test "*.test.mjs"` : tous verts, y compris les suites existantes.

- [ ] **Step 6: Version, changelog, commit**

```bash
git add src/ScadaBuilderV2.Rendering/Runtime/quick-window-runtime.js tests/runtime-js/quick-window-placeholder-substitution.test.mjs VERSION docs/
git commit -m "feat: substitute #N placeholders when a quick-window instance mounts"
```

---

### Task 8: Type de page Fenêtre rapide dans l'éditeur

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml:1070-1080` (ComboBox du type de page)
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:3905-3935` (`GetSelectedPageType`, `SelectPageType`, `GetPageTypeLabel`)
- Modify: `src/ScadaBuilderV2.App/Pages/PagesPanelViewModel.cs:25`
- Create: `src/ScadaBuilderV2.App/Pages/PageTypeOptions.cs` (sans WPF, lié aux tests)
- Test: `tests/ScadaBuilderV2.Tests/PageTypeOptionsTests.cs`

**Interfaces:**
- Produces: `record PageTypeOption(ScadaPageType Type, string Label)` ; `static IReadOnlyList<PageTypeOption> PageTypeOptions.Selectable` = Défaut, Fenêtre rapide, En-tête, Pied de page (sans Fragment) ; `static string PageTypeOptions.Label(ScadaPageType type)` (Fragment → « Fragment (obsolète) »).

- [ ] **Step 1: Write the failing tests**

```csharp
using ScadaBuilderV2.App.Pages;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class PageTypeOptionsTests
{
    [TestMethod]
    public void FragmentIsNotSelectableAndQuickWindowIs()
    {
        var types = PageTypeOptions.Selectable.Select(option => option.Type).ToArray();

        CollectionAssert.DoesNotContain(types, ScadaPageType.Fragment);
        CollectionAssert.Contains(types, ScadaPageType.QuickWindow);
    }

    [TestMethod]
    public void EverySelectableTypeRoundTripsByValue()
    {
        foreach (var option in PageTypeOptions.Selectable)
        {
            Assert.AreEqual(option.Type, PageTypeOptions.Selectable.Single(candidate => candidate.Type == option.Type).Type);
        }
        Assert.AreEqual("Fenêtre rapide", PageTypeOptions.Label(ScadaPageType.QuickWindow));
        Assert.AreEqual("Fragment (obsolète)", PageTypeOptions.Label(ScadaPageType.Fragment));
    }
}
```

Ajouter `<Compile Include="..\..\src\ScadaBuilderV2.App\Pages\PageTypeOptions.cs" Link="AppSource\PageTypeOptions.cs" />` au `.csproj` de tests.

- [ ] **Step 2: Run to verify failure** — filtre `PageTypeOptionsTests` → FAIL.

- [ ] **Step 3: Implement `PageTypeOptions`** avec les libellés : Défaut → « Défaut », QuickWindow → « Fenêtre rapide », Header → « En-tête », Footer → « Pied de page », Fragment → « Fragment (obsolète) ».

- [ ] **Step 4: Mapper par valeur** — `PageTypeComboBox.ItemsSource = PageTypeOptions.Selectable`, `DisplayMemberPath="Label"`, et :

```csharp
private ScadaPageType GetSelectedPageType() =>
    (PageTypeComboBox.SelectedItem as PageTypeOption)?.Type ?? ScadaPageType.Default;

private void SelectPageType(ScadaPageType pageType) =>
    PageTypeComboBox.SelectedItem = PageTypeOptions.Selectable.FirstOrDefault(option => option.Type == pageType);

private static string GetPageTypeLabel(ScadaPageType pageType) => PageTypeOptions.Label(pageType);
```

Une page encore `Fragment` affiche le sélecteur vide ; la choisir « Fenêtre rapide » la convertit (spec D6). Retirer les `<ComboBoxItem>` codés en dur du XAML. `PagesPanelViewModel.TypeFilters` : `["Tous", "Default", "Fenêtre rapide", "Header", "Footer"]`, et adapter le filtre pour comparer au libellé de `PageTypeOptions.Label`.

- [ ] **Step 5: Sélecteurs de tag sur une page Fenêtre rapide** — là où l'éditeur fournit le catalogue aux sélecteurs (`ActiveSelectorTagCatalog`, `MainWindow.QuickWindows.cs:57`) : si la page active est de type `QuickWindow`, fournir un catalogue synthétique `#1`…`#999` (`new ScadaTagDefinition($"#{n}", $"#{n}", Device: "Paramètre", Writeable: true, Enabled: true)`), construit une fois et mis en cache.

- [ ] **Step 6: Run** — filtre vert, suite complète verte ; lancer l'application et vérifier à l'écran qu'un aller-retour Défaut → En-tête → Pied de page → Fenêtre rapide conserve chaque type.

- [ ] **Step 7: Version, changelog, commit**

```bash
git add src/ScadaBuilderV2.App/ tests/ VERSION docs/
git commit -m "feat: select the quick-window page type by value and retire Fragment"
```

---

### Task 9: Panneau et éditeur de feuilles

**Files:**
- Create: `src/ScadaBuilderV2.App/QuickWindows/ParameterSheetEditorViewModel.cs` (sans WPF, lié aux tests)
- Create: `src/ScadaBuilderV2.App/QuickWindows/ParameterSheetEditorDialog.xaml(.cs)`, `ParameterRuleDialog.xaml(.cs)`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml:642-700` (panneau « Fenêtres rapides »), `MainWindow.QuickWindows.cs` (commandes du panneau)
- Modify: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowWorkspaceController.cs` (création, renommage, suppression de feuilles)
- Test: `tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetEditorViewModelTests.cs`

**Interfaces:**
- Consumes: `ParameterSheetParser`, `PageCodePolicy.DeriveCodeFromDisplayName`, `ScadaProject.EffectiveParameterSheets`.
- Produces:
  - `ParameterSheetEditorViewModel(ParameterSheet sheet, ScadaTagCatalog? catalog)` : `string Text { get; set; }`, `IReadOnlyList<ParameterSheetDiagnostic> Diagnostics`, `int NextFreeNumber`, `void AppendRule(int number, ParameterValueKind kind, string value)`, `ParameterSheet ToSheet()`.
  - Mutations undoables `QuickWindowWorkspace.CreateSheetAsync`, `RenameSheetAsync`, `UpdateSheetTextAsync`, `DeleteSheetAsync`, sur le modèle de `CreateAsync`/`RenameAsync` existants.

- [ ] **Step 1: Write the failing tests**

```csharp
[TestClass]
public sealed class ParameterSheetEditorViewModelTests
{
    private static ParameterSheet Sheet(string text) => new(Guid.NewGuid(), "v3", "Ventilateur 3", text);

    [TestMethod]
    public void NextFreeNumberIsTheSmallestUnusedOne()
    {
        Assert.AreEqual(2, new ParameterSheetEditorViewModel(Sheet("#1 := 1\n#3 := 1"), null).NextFreeNumber);
    }

    [TestMethod]
    public void AppendRuleAddsOneLineInTheExistingGrammar()
    {
        var vm = new ParameterSheetEditorViewModel(Sheet("#1 := {A}"), null);

        vm.AppendRule(2, ParameterValueKind.Tag, "FAN_03_RUN");
        vm.AppendRule(3, ParameterValueKind.Literal, "Ventilateur 3");
        vm.AppendRule(4, ParameterValueKind.Expression, "{A} * 0.1");

        StringAssert.EndsWith(vm.Text.Replace("\r\n", "\n"), "#2 := {FAN_03_RUN}\n#3 := \"Ventilateur 3\"\n#4 := {A} * 0.1");
        Assert.AreEqual(0, vm.Diagnostics.Count);
    }

    [TestMethod]
    public void ALiteralWithAQuoteIsRefusedRatherThanProducingABrokenLine()
    {
        var vm = new ParameterSheetEditorViewModel(Sheet(""), null);

        Assert.ThrowsException<ArgumentException>(() => vm.AppendRule(1, ParameterValueKind.Literal, "dit \"x\""));
    }

    [TestMethod]
    public void EditingTheTextRefreshesDiagnostics()
    {
        var vm = new ParameterSheetEditorViewModel(Sheet(""), null);

        vm.Text = "#1 := {A}\n#1 := {B}";

        Assert.AreEqual(2, vm.Diagnostics.Single().Line);
    }

    [TestMethod]
    public void ToSheetKeepsTheKeyAndReturnsTheEditedText()
    {
        var sheet = Sheet("#1 := 1");
        var vm = new ParameterSheetEditorViewModel(sheet, null) { Text = "#1 := 2" };

        Assert.AreEqual(sheet.SheetKey, vm.ToSheet().SheetKey);
        Assert.AreEqual("#1 := 2", vm.ToSheet().Text);
    }
}
```

La grammaire existante n'a pas d'échappement de guillemet dans une chaîne : refuser le littéral plutôt que produire une ligne invalide.

- [ ] **Step 2: Run to verify failure**, puis **Step 3: implémenter** le view-model (Diagnostics recalculés par `ParameterSheetParser.Parse` à chaque affectation de `Text` ; `AppendRule` ajoute un saut de ligne si le texte n'en finit pas par un, puis `#{n} := {…}`, `"…"` ou l'expression telle quelle).

- [ ] **Step 4: Surfaces WPF**
  - Panneau « Fenêtres rapides » : `ItemsSource` = feuilles du projet (`DisplayName`, sous-titre `Code`), boutons « + », Renommer, Dupliquer, Supprimer. Le « + » demande un nom via `LibraryNameDialog` (code dérivé comme en `ef7d502`), crée la feuille puis ouvre `ParameterSheetEditorDialog`.
  - `ParameterSheetEditorDialog` : `TextBox` multi-ligne `FontFamily="Consolas"` `AcceptsReturn="True"` liée à `Text` ; sous lui une `ItemsControl` des diagnostics (« Ligne N : message ») ; en-tête avec « + » ouvrant `ParameterRuleDialog` ; Annuler / Enregistrer. Enregistrer applique `UpdateSheetTextAsync` en une transition undoable.
  - `ParameterRuleDialog` : `TextBox` numéro prérempli `NextFreeNumber` ; `ComboBox` nature (Tag, Littéral, Expression) ; selon la nature, `ComboBox` du catalogue (`DisplayMemberPath="AuthoringLabel"`) ou `TextBox` ; message de validation ; Annuler / Ajouter → `AppendRule`.
  - La double-clic sur une feuille ouvre son éditeur.

- [ ] **Step 5: Undo** — test dans `ParameterSheetEditorViewModelTests` ou un fichier voisin lié : `UpdateSheetTextAsync` puis `History.Undo` restaure le texte d'avant (reprendre le montage des tests d'historique de `QuickWindowFrontDoorNamingTests`).

- [ ] **Step 6: Run** — filtres verts, suite complète verte ; vérifier à l'écran : « + » → nom → éditeur → « + » d'en-tête → règle Tag → la ligne apparaît → Enregistrer → Ctrl+Z la retire.

- [ ] **Step 7: Version, changelog, commit**

```bash
git add src/ScadaBuilderV2.App/ tests/ VERSION docs/
git commit -m "feat: author parameter sheets from the quick-window panel"
```

---

### Task 10: Dialogue « Configuration de la fenêtre rapide »

**Files:**
- Create: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowConfigurationDialog.xaml(.cs)`
- Modify: `src/ScadaBuilderV2.App/ElementCommandDialog.xaml(.cs)` (le type `OpenQuickWindow` ouvre ce dialogue ; retrait de `QuickWindowDefinitionComboBox`)
- Modify: `src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml(.cs)` (retrait de l'onglet « Liaisons » pour les commandes `OpenQuickWindow` d'authoring)
- Modify: `src/ScadaBuilderV2.Application/Commands/ElementCommandKindCatalog.cs`
- Test: `tests/ScadaBuilderV2.Tests/Commands/ElementCommandKindCatalogTests.cs` (étendu)

**Interfaces:**
- Consumes: `ScadaCommandBinding.ParameterSheetKey`, `QuickWindowPageKey`.
- Produces: `ElementCommandKindCatalog.CreateOptions(usedKinds, editedKind, bool hasQuickWindowPages, bool hasParameterSheets, bool isQuickWindowContent)` ; constantes `NoQuickWindowPageReason`, `NoParameterSheetReason`.

- [ ] **Step 1: Write the failing tests** — dans `ElementCommandKindCatalogTests` :

```csharp
[TestMethod]
public void OpenQuickWindowNeedsBothAPageAndASheet()
{
    Option(pages: false, sheets: true).AssertDisabled(ElementCommandKindCatalog.NoQuickWindowPageReason);
    Option(pages: true, sheets: false).AssertDisabled(ElementCommandKindCatalog.NoParameterSheetReason);
    StringAssert.Contains(Option(pages: false, sheets: false).DisabledReason, ElementCommandKindCatalog.NoQuickWindowPageReason);
    StringAssert.Contains(Option(pages: false, sheets: false).DisabledReason, ElementCommandKindCatalog.NoParameterSheetReason);
    Assert.IsTrue(Option(pages: true, sheets: true).IsEnabled);
}
```

avec un helper local `Option(bool pages, bool sheets)` qui appelle `CreateOptions([], null, pages, sheets, false)` et renvoie l'entrée `OpenQuickWindow`, et `AssertDisabled(reason)` qui vérifie `IsEnabled == false` et `DisabledReason == reason`. Mettre à jour les tests existants au nouvel appel. Constantes exactes :
  - `NoQuickWindowPageReason = "Aucune page de type Fenêtre rapide dans ce projet."`
  - `NoParameterSheetReason = "Aucune feuille de paramètres. Créez-en une dans le panneau « Fenêtres rapides »."`
  - les deux manquants : `$"{NoQuickWindowPageReason} {NoParameterSheetReason}"`.
  - la règle « déjà utilisé » garde sa priorité.

- [ ] **Step 2: Run to verify failure**, **Step 3: implémenter** le catalogue.

- [ ] **Step 4: Dialogue** — `QuickWindowConfigurationDialog` titre « Configuration de la fenêtre rapide » : `ComboBox` « Feuille de paramètres » (feuilles du projet, `DisplayName`), `ComboBox` « Page Fenêtre rapide » (pages `QuickWindow`, `Title`), Annuler, Enregistrer (désactivé tant que les deux ne sont pas choisis). Enregistrer produit `ParameterSheetKey` et `QuickWindowPageKey` sur la commande. Choisir le type `OpenQuickWindow` dans `ElementCommandDialog` ouvre ce dialogue ; rééditer une commande existante le rouvre présélectionné. Les deux appelants de `ElementCommandDialog` (`MainWindow.xaml.cs` ≈ l.6040 et ≈ l.6081, et `ElementPropertiesDialog`) passent `hasQuickWindowPages` et `hasParameterSheets` calculés depuis le projet.

- [ ] **Step 5: Run** — filtres et suite verts ; à l'écran : commande → Ajouter → type grisé avec les deux raisons sur un projet vide ; après création d'une page Fenêtre rapide et d'une feuille, le type s'active et le dialogue enregistre la paire.

- [ ] **Step 6: Version, changelog, commit**

```bash
git add src/ tests/ VERSION docs/
git commit -m "feat: configure an open-quick-window command as a sheet and page pair"
```

---

### Task 11: Interface locale en lecture et banc d'essai

**Files:**
- Modify: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowInterfacePanel.xaml(.cs)` et `QuickWindowInterfaceViewModel.cs`
- Modify: `src/ScadaBuilderV2.App/QuickWindows/QuickWindowTestBench.xaml(.cs)` et `QuickWindowTestBenchViewModel.cs`
- Test: `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInterfaceViewModelTests.cs` (étendu ou créé)

**Interfaces:**
- Consumes: `QuickWindowPlaceholderUsageCollector.Collect`, `ParameterSheetLowering.Lower`.
- Produces: `QuickWindowInterfaceViewModel.LoadPlaceholders(ScadaScene page)` exposant des lignes `(#N, nombre d'usages, emplacements)` triées par N ; `QuickWindowTestBenchViewModel.SelectSheet(Guid sheetKey)`.

- [ ] **Step 1: Write the failing test** — `LoadPlaceholders` sur une page utilisant `#3` deux fois et `#1` une fois donne deux lignes dans l'ordre `#1`, `#3` avec les comptes 1 et 2.

- [ ] **Step 2: Run to verify failure**, **Step 3: implémenter** ; retirer les boutons Ajouter / Éditer / Supprimer membre du panneau (la liste est en lecture) ; le panneau s'affiche quand la page active est de type `QuickWindow`.

- [ ] **Step 4: Banc d'essai** — une `ComboBox` de feuille ; sa sélection abaisse le couple (feuille, page active) par `ParameterSheetLowering.Lower` sur un instantané et nourrit l'aperçu existant, qui passe par `mountInstance` et donc par la substitution de Task 7. Les diagnostics de l'abaissement s'affichent dans le banc.

- [ ] **Step 5: Run** — filtres et suite verts ; à l'écran : page Fenêtre rapide `#4` dans un texte, feuille `#4 := "Ventilateur 3"` → le banc affiche « Ventilateur 3 ».

- [ ] **Step 6: Version, changelog, commit**

```bash
git add src/ tests/ VERSION docs/
git commit -m "feat: show a quick-window page's placeholders and preview it with a sheet"
```

---

### Task 12: Bout en bout dans le Builder

**Files:**
- Test: `tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetEndToEndTests.cs`
- Test: `tests/runtime-js/quick-window-parameter-sheet-e2e.test.mjs`

- [ ] **Step 1: Test .NET** — un projet synthétique : une page d'appel avec deux boutons, une page `QuickWindow` utilisant `#1` (lecture), `#3` (état), `#4` (texte), deux feuilles (Ventilateur 3, Ventilateur 5). Abaisser, valider (0 erreur), exporter ; asserter deux invocations de la même définition avec des liaisons différentes, et écrire le paquet dans un dossier que le test JS relit (chemin passé par variable d'environnement `SB2_E2E_DIR`, sinon test JS `skip`).

- [ ] **Step 2: Test JS** — charger le manifeste et le fragment exportés, ouvrir la première invocation, monter, asserter : `data-scada-read-tag` = tag de Ventilateur 3, texte « Ventilateur 3 », expression d'état substituée ; fermer, ouvrir la seconde, asserter les valeurs de Ventilateur 5 sans reste de la première.

- [ ] **Step 3: Run both** — les deux passent ; `dotnet test ScadaBuilderV2.sln` et `node --test "*.test.mjs"` complets verts.

- [ ] **Step 4: Version, changelog, commit**

```bash
git add tests/ VERSION docs/
git commit -m "test: prove a parameter-sheet quick window end to end in the Builder"
```

---

### Task 13: Rejeu de conformance TF100Web

**Files (dépôt `F:\Projet\Git\TF100Web-qw`, branche `codex/quick-window-v1`):**
- Create: `frontend/test_fixtures/quick-window-parameter-sheet.sb2` et son `.sha256`
- Create: `frontend/tests_runtime_js/quick-window-parameter-sheet.test.mjs`
- Modify: documentation de conformance si elle liste les fixtures

**Interfaces:**
- Consumes: le paquet exporté par Task 12 ; `static/asset/js/quick-window-host.js` **non modifié**.

- [ ] **Step 1: Fixture** — copier le `.sb2` de Task 12 et calculer son SHA-256 dans le format des fixtures voisines (`quick-window-runtime-handshake.sb2/.sha256`).

- [ ] **Step 2: Sonde d'animation** — test sur le modèle de `quick-window-runtime-handshake.test.mjs` qui ouvre l'invocation **par l'hôte** `quick-window-host.js`, fournit une valeur au tag lié à `#1` par le pont de tags, et **asserte l'effet visible sur l'élément** (texte ou classe d'état), pas seulement `readPort`. C'est la preuve exigée par la spec §8.

- [ ] **Step 3: Rejouer** — `cd frontend/tests_runtime_js && node --test "quick-window-*.test.mjs"` ; puis sous WSL, en `root` (cf. `KNOWN_GAPS_V2.md` entrée 28) : les suites `frontend.tests_scada_quick_window_*`. Consigner tout échec lié au type des membres (`String`) : c'est le risque §12 de la spec, à traiter par une mise à jour du contrat §12.4 et un ticket, pas par un contournement.

- [ ] **Step 4: Commit dans TF100Web**

```bash
git -C "F:/Projet/Git/TF100Web-qw" add frontend/
git -C "F:/Projet/Git/TF100Web-qw" commit -m "test: prove a parameter-sheet quick window animates an element in the host"
```

- [ ] **Step 5: Côté Builder** — mettre à jour `KNOWN_GAPS_V2.md` entrée 33 (écart comblé, commit TF100Web cité), `VERSION`, changelog, commit.
