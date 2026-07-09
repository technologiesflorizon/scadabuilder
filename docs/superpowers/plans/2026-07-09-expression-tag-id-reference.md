# Expression canonique Tag Id â€” Plan d'implÃ©mentation

Date: 2026-07-09
Status: Draft plan â€” en attente d'approbation
Document version: `V2.1.4.0002`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Faire en sorte que les expressions d'états références les tags par leur Id canonique (`tf100.mapping.X`) et que l'export normalise les ASTs existants avant sÃ©rialisation runtime (HTML + manifest).

**Architecture:** Ajouter `TagId?` Ã  `ScadaExprTagRef`, crÃ©er un resolver `public` dans le domaine, faire en sorte que `ElementStateRuleDialog` conserve le libellÃ© humain dans la source UI mais injecte `TagId` dans l'AST, mettre Ã  jour `ScadaExpression` pour collecter les refs canoniques, intÃ©grer le resolver dans le validator avec dÃ©tection d'ambiguÃ¯tÃ©s, normaliser les ASTs Ã  l'export dans HTML ET manifest (bloquer sur ambiguÃ¯tÃ©, avertir sur non-rÃ©solu). Ajouter une validation de migration pour les commandes/bindings (D9).

**Tech Stack:** C# 12, .NET 8-windows, MSTest, JavaScript (Node test runner)

**Spec:** `docs/superpowers/specs/2026-07-09-expression-tag-id-reference.md`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-09 | `V2.1.4.0002` | PENDING | Correctif : resolver public, normalisation manifest + HTML, warnings non resolus dedupliques, D9 sur ScadaCommandBinding, casing AST coherent, warnings projet, regression archive .sb2. |
| 2026-07-09 | `V2.1.4.0001` | PENDING | Ajout Task 3 (ScadaExpression), Task 7 (D9) ; correction injection TagId individuelle, ambiguÃ¯tÃ©s export. |
| 2026-07-09 | `V2.1.4.0000` | PENDING | Plan initial (6 tÃ¢ches). |

## Global Constraints

- Ne pas modifier le parser `ScadaExpressionParser` (il continue d'extraire `{...}` â†’ `TagName`)
- Ne pas modifier `ScadaTagDefinition` (Id, DisplayName, KeywordLabel existent dÃ©jÃ )
- Ne pas modifier `Tf100WebTagCatalogImporter`
- Ne pas modifier le runtime JS dans cette tranche (l'export continue d'Ã©mettre `tagName`)
- `tagName` doit TOUJOURS Ãªtre prÃ©sent dans l'AST exportÃ© â€” ne jamais Ã©mettre `tagId` sans `tagName`
- L'expression source UI (`{PE_16} == true`) conserve le libellÃ© humain pour l'affichage/rÃ©Ã©dition
- Le resolver et ses types sont `public` dans le domaine (consommÃ©s par `.App` et `.Rendering`)
- Chaque commit doit compiler (`dotnet build ScadaBuilderV2.sln`)
- Les tests existants doivent rester verts
- APIs publiques : doc XML
- PowerShell depuis `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`

## Before You Start

- [ ] VÃ©rifier l'Ã©tat du worktree : `git status` doit Ãªtre clean
- [ ] Branche de travail : `debug-state-effect` (dÃ©jÃ  crÃ©Ã©e)
- [ ] Lire la spec complÃ¨te : `docs/superpowers/specs/2026-07-09-expression-tag-id-reference.md`
- [ ] Lire les fichiers clÃ©s :
  - `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExprNode.cs`
  - `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpression.cs`
  - `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpressionValidator.cs`
  - `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs`
  - `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`

---

### Task 1: Ajouter `TagId?` Ã  `ScadaExprTagRef`

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExprNode.cs:48-49`
- Modify: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExprNodeTests.cs`

**Interfaces:**
- Consumes: nothing (foundational)
- Produces: `public sealed record ScadaExprTagRef(string TagName, string? TagId = null)` â€” utilisÃ© par Tasks 2-8

- [ ] **Step 1: Ajouter `TagId?` au record avec `[JsonIgnore]` conditionnel**

```csharp
// src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExprNode.cs:48-49
// Remplacer :
// public sealed record ScadaExprTagRef(string TagName) : ScadaExprNode;
// Par :

/// <summary>
/// References one project tag. <see cref="TagName"/> holds the human-readable label
/// for UI display/re-editing; <see cref="TagId"/> holds the canonical runtime identifier
/// (e.g. <c>tf100.mapping.196</c>) for validation, export, and runtime resolution.
/// </summary>
public sealed record ScadaExprTagRef(
    string TagName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? TagId = null) : ScadaExprNode;
```

Le parser (`ScadaExpressionParser.cs:253`) appelle `new ScadaExprTagRef(token.Text)` â€” le paramÃ¨tre optionnel `TagId = null` rend cet appel inchangÃ©.

- [ ] **Step 2: Ajouter les tests**

```csharp
// Ajouter dans tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExprNodeTests.cs

[TestMethod]
public void TagRef_WithTagId_RoundTripsThroughJson()
{
    var tagRef = new ScadaExprTagRef("PE_16", "tf100.mapping.161");
    var json = JsonSerializer.Serialize<ScadaExprNode>(tagRef);

    StringAssert.Contains(json, "\"tagName\":\"PE_16\"");
    StringAssert.Contains(json, "\"tagId\":\"tf100.mapping.161\"");

    var deserialized = JsonSerializer.Deserialize<ScadaExprNode>(json);
    Assert.IsInstanceOfType(deserialized, typeof(ScadaExprTagRef));
    var roundTripped = (ScadaExprTagRef)deserialized;
    Assert.AreEqual("PE_16", roundTripped.TagName);
    Assert.AreEqual("tf100.mapping.161", roundTripped.TagId);
}

[TestMethod]
public void TagRef_WithoutTagId_OmitsTagIdFromJson()
{
    var tagRef = new ScadaExprTagRef("PE_16");
    var json = JsonSerializer.Serialize<ScadaExprNode>(tagRef);

    StringAssert.Contains(json, "\"tagName\":\"PE_16\"");
    Assert.IsFalse(json.Contains("\"tagId\""),
        "Null TagId must not appear in serialized JSON.");
}

[TestMethod]
public void TagRef_LegacyJsonWithoutTagId_DeserializesWithNullTagId()
{
    var legacyJson = "{\"type\":\"tagRef\",\"tagName\":\"PE_16\"}";
    var deserialized = JsonSerializer.Deserialize<ScadaExprNode>(legacyJson);

    Assert.IsInstanceOfType(deserialized, typeof(ScadaExprTagRef));
    var tagRef = (ScadaExprTagRef)deserialized;
    Assert.AreEqual("PE_16", tagRef.TagName);
    Assert.IsNull(tagRef.TagId);
}
```

- [ ] **Step 3: Build et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaExprNodeTests"
```
Expected: 6 tests verts (3 existants + 3 nouveaux).

- [ ] **Step 4: Commit**

```bash
git add src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExprNode.cs
git add tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExprNodeTests.cs
git commit -m "feat: add TagId to ScadaExprTagRef for canonical tag reference

Adds optional TagId field to ScadaExprTagRef to carry the canonical
runtime identifier (tf100.mapping.X) alongside the human-readable TagName.
Legacy JSON without tagId deserializes with TagId=null. Null TagId is
omitted from serialized JSON via JsonIgnoreCondition.WhenWritingNull.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: RÃ©solveur commun `TryResolveTagReference` (public)

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpressionValidator.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionValidatorTests.cs`

**Interfaces:**
- Consumes: `ScadaExprTagRef` avec `TagId?` (Task 1)
- Produces (tous `public`, dans le namespace `ScadaBuilderV2.Domain.ElementEvents.Expressions`) :
  - `public enum TagResolveStatus { Resolved, Unresolved, Ambiguous }`
  - `public sealed record TagResolveResult(TagResolveStatus Status, string? CanonicalId, IReadOnlyList<string> Matches)`
  - `public static TagResolveResult TryResolveTagReference(string value, ScadaTagCatalog? catalog)`

Pourquoi `public` : le resolver est consommÃ© par `ScadaBuilderV2.App` (ElementStateRuleDialog, Task 4) et `ScadaBuilderV2.Rendering` (Ft100SceneExporter, Task 6), qui rÃ©fÃ©rencent dÃ©jÃ  `ScadaBuilderV2.Domain`. Aucun `InternalsVisibleTo` requis.

- [ ] **Step 1: Ã‰crire les tests du resolver**

```csharp
// Ajouter dans tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionValidatorTests.cs

private static ScadaTagCatalog CreateResolverCatalog() => new(
    "tf100web-scada-tags-v1",
    new[]
    {
        new ScadaTagDefinition("tf100.mapping.196", "Noeud1_N15_04_Commande_MC_120C",
            KeywordLabel: "MC_120C", Datatype: "bool"),
        new ScadaTagDefinition("tf100.mapping.195", "Noeud1_N15_03_Commande_MC_120A",
            KeywordLabel: "MC_120A", Datatype: "bool"),
        new ScadaTagDefinition("tf100.mapping.200", "DuplicateLabel", Datatype: "float"),
        new ScadaTagDefinition("tf100.mapping.201", "DuplicateLabel", Datatype: "bool"),
    });

[TestMethod]
public void Resolve_ById_ReturnsResolved()
{
    var result = ScadaExpressionValidator.TryResolveTagReference(
        "tf100.mapping.196", CreateResolverCatalog());
    Assert.AreEqual(TagResolveStatus.Resolved, result.Status);
    Assert.AreEqual("tf100.mapping.196", result.CanonicalId);
}

[TestMethod]
public void Resolve_ByDisplayName_ReturnsResolved()
{
    var result = ScadaExpressionValidator.TryResolveTagReference(
        "Noeud1_N15_04_Commande_MC_120C", CreateResolverCatalog());
    Assert.AreEqual(TagResolveStatus.Resolved, result.Status);
    Assert.AreEqual("tf100.mapping.196", result.CanonicalId);
}

[TestMethod]
public void Resolve_ByKeywordLabel_ReturnsResolved()
{
    var result = ScadaExpressionValidator.TryResolveTagReference(
        "MC_120C", CreateResolverCatalog());
    Assert.AreEqual(TagResolveStatus.Resolved, result.Status);
    Assert.AreEqual("tf100.mapping.196", result.CanonicalId);
}

[TestMethod]
public void Resolve_IdHasPriorityOverLabel()
{
    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("MC_120C", "AutreNom", Datatype: "bool"),
        new ScadaTagDefinition("tf100.mapping.196", "Noeud1_N15_04_Commande_MC_120C",
            KeywordLabel: "MC_120C", Datatype: "bool"),
    });
    var result = ScadaExpressionValidator.TryResolveTagReference("MC_120C", catalog);
    Assert.AreEqual(TagResolveStatus.Resolved, result.Status);
    Assert.AreEqual("MC_120C", result.CanonicalId,
        "Id match must take priority over KeywordLabel match.");
}

[TestMethod]
public void Resolve_Unknown_ReturnsUnresolved()
{
    var result = ScadaExpressionValidator.TryResolveTagReference(
        "TagInexistant", CreateResolverCatalog());
    Assert.AreEqual(TagResolveStatus.Unresolved, result.Status);
    Assert.IsNull(result.CanonicalId);
}

[TestMethod]
public void Resolve_DuplicateLabel_ReturnsAmbiguous()
{
    var result = ScadaExpressionValidator.TryResolveTagReference(
        "DuplicateLabel", CreateResolverCatalog());
    Assert.AreEqual(TagResolveStatus.Ambiguous, result.Status);
    Assert.IsNull(result.CanonicalId);
    Assert.IsTrue(result.Matches.Count == 2);
}

[TestMethod]
public void Resolve_NullCatalog_ReturnsUnresolved()
{
    var result = ScadaExpressionValidator.TryResolveTagReference("anything", null);
    Assert.AreEqual(TagResolveStatus.Unresolved, result.Status);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Resolve_"
```
Expected: compilation errors â€” types et mÃ©thode inexistants.

- [ ] **Step 3: Ajouter les types `public` et la mÃ©thode**

```csharp
// Ajouter dans ScadaExpressionValidator.cs, dans le namespace, AVANT la classe :

/// <summary>Outcome of resolving a tag reference against the project catalog.</summary>
public enum TagResolveStatus
{
    /// <summary>Exactly one match found; <see cref="TagResolveResult.CanonicalId"/> is set.</summary>
    Resolved,
    /// <summary>No match found in the catalog.</summary>
    Unresolved,
    /// <summary>Multiple tags match the same label; resolution is ambiguous.</summary>
    Ambiguous
}

/// <summary>Result of <see cref="ScadaExpressionValidator.TryResolveTagReference"/>.</summary>
public sealed record TagResolveResult(
    TagResolveStatus Status,
    string? CanonicalId,
    IReadOnlyList<string> Matches)
{
    public static TagResolveResult ForResolved(string canonicalId) =>
        new(TagResolveStatus.Resolved, canonicalId, new[] { canonicalId });

    public static TagResolveResult ForUnresolved() =>
        new(TagResolveStatus.Unresolved, null, Array.Empty<string>());

    public static TagResolveResult ForAmbiguous(IReadOnlyList<string> matches) =>
        new(TagResolveStatus.Ambiguous, null, matches);
}
```

```csharp
// Ajouter dans la classe ScadaExpressionValidator :

/// <summary>
/// Resolves a tag reference value (from <c>{...}</c> in an expression) against the
/// project tag catalog. Resolution order: exact <see cref="ScadaTagDefinition.Id"/>,
/// then <see cref="ScadaTagDefinition.DisplayName"/>, then
/// <see cref="ScadaTagDefinition.KeywordLabel"/>.
/// </summary>
/// <param name="value">The tag reference text (content between { and }).</param>
/// <param name="catalog">The project tag catalog, or null to skip resolution.</param>
public static TagResolveResult TryResolveTagReference(string value, ScadaTagCatalog? catalog)
{
    if (string.IsNullOrWhiteSpace(value))
        return TagResolveResult.ForUnresolved();

    if (catalog?.Tags is null || catalog.Tags.Count == 0)
        return TagResolveResult.ForUnresolved();

    var tags = catalog.Tags;

    // 1. Exact match by Id (canonical â€” highest priority)
    var byId = tags.FirstOrDefault(t =>
        string.Equals(t.Id, value, StringComparison.Ordinal));
    if (byId is not null)
        return TagResolveResult.ForResolved(byId.Id);

    // 2. Match by DisplayName
    var byDisplayName = tags
        .Where(t => string.Equals(t.DisplayName, value, StringComparison.OrdinalIgnoreCase))
        .ToArray();
    if (byDisplayName.Length == 1)
        return TagResolveResult.ForResolved(byDisplayName[0].Id);
    if (byDisplayName.Length > 1)
        return TagResolveResult.ForAmbiguous(
            byDisplayName.Select(t => t.Id).ToArray());

    // 3. Match by KeywordLabel
    var byKeyword = tags
        .Where(t => t.KeywordLabel is not null &&
                    string.Equals(t.KeywordLabel, value, StringComparison.OrdinalIgnoreCase))
        .ToArray();
    if (byKeyword.Length == 1)
        return TagResolveResult.ForResolved(byKeyword[0].Id);
    if (byKeyword.Length > 1)
        return TagResolveResult.ForAmbiguous(
            byKeyword.Select(t => t.Id).ToArray());

    return TagResolveResult.ForUnresolved();
}
```

- [ ] **Step 4: Build et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaExpressionValidatorTests"
```
Expected: 7 nouveaux tests verts + anciens tests verts.

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpressionValidator.cs
git add tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionValidatorTests.cs
git commit -m "feat: add public TryResolveTagReference resolver

Resolution order: Id (exact) -> DisplayName -> KeywordLabel.
Types are public (consumed cross-assembly by .App and .Rendering).
Returns Resolved (with canonical Id), Ambiguous (multiple matches),
or Unresolved.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: `ScadaExpression` â€” collecter les refs canoniques

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpression.cs:32-54`
- Modify: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionTests.cs`

**Interfaces:**
- Consumes: `ScadaExprTagRef` avec `TagId?` (Task 1)
- Produces: `public static ScadaExpression FromAst(string source, ScadaExprNode? ast)` â€” collecte `TagId` dans `ReferencedTags`

- [ ] **Step 1: Ã‰crire le test**

```csharp
// Ajouter dans tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionTests.cs

[TestMethod]
public void FromAst_PrefersTagIdOverTagName()
{
    var tagRef = new ScadaExprTagRef("PE_16", "tf100.mapping.161");
    var expr = ScadaExpression.FromAst(
        "{PE_16} == true",
        new ScadaExprBinary(ScadaExprBinaryOp.Equal,
            tagRef,
            new ScadaExprLiteralBool(true)));

    CollectionAssert.Contains(expr.ReferencedTags.ToList(), "tf100.mapping.161");
}

[TestMethod]
public void FromAst_LegacyTagRefWithoutTagId_UsesTagName()
{
    var tagRef = new ScadaExprTagRef("LegacyName"); // TagId = null
    var expr = ScadaExpression.FromAst(
        "{LegacyName} == true",
        new ScadaExprBinary(ScadaExprBinaryOp.Equal,
            tagRef,
            new ScadaExprLiteralBool(true)));

    CollectionAssert.Contains(expr.ReferencedTags.ToList(), "LegacyName");
}

[TestMethod]
public void FromAst_MultipleTagRefs_MixedTagIds()
{
    var left = new ScadaExprTagRef("PE_16", "tf100.mapping.161");
    var right = new ScadaExprTagRef("LegacyName"); // TagId = null
    var expr = ScadaExpression.FromAst(
        "{PE_16} && {LegacyName}",
        new ScadaExprBinary(ScadaExprBinaryOp.And,
            left,
            right));

    var tags = expr.ReferencedTags.ToList();
    CollectionAssert.Contains(tags, "tf100.mapping.161");
    CollectionAssert.Contains(tags, "LegacyName");
}
```

- [ ] **Step 2: Run test to verify it fails**

```powershell
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~FromAst_"
```
Expected: FAIL â€” `FromAst` n'existe pas.

- [ ] **Step 3: Ajouter `FromAst` et modifier `CollectTagRefs`**

```csharp
// Ajouter dans ScadaExpression.cs, dans la classe ScadaExpression :

/// <summary>
/// Creates a <see cref="ScadaExpression"/> from a source text and AST,
/// collecting canonical tag references when <see cref="ScadaExprTagRef.TagId"/>
/// is available.
/// </summary>
public static ScadaExpression FromAst(string source, ScadaExprNode? ast)
{
    if (ast is null)
        return new ScadaExpression(source, null, Array.Empty<string>());

    var tags = new List<string>();
    CollectTagRefs(ast, tags);
    return new ScadaExpression(source, ast, tags);
}

// Remplacer la mÃ©thode CollectTagRefs existante (ligne 32-54) :

private static void CollectTagRefs(ScadaExprNode node, List<string> tags)
{
    switch (node)
    {
        case ScadaExprTagRef tagRef:
            tags.Add(!string.IsNullOrWhiteSpace(tagRef.TagId) ? tagRef.TagId : tagRef.TagName);
            break;
        case ScadaExprUnary unary:
            CollectTagRefs(unary.Operand, tags);
            break;
        case ScadaExprBinary binary:
            CollectTagRefs(binary.Left, tags);
            CollectTagRefs(binary.Right, tags);
            break;
        case ScadaExprFunc func:
            foreach (var arg in func.Args)
                CollectTagRefs(arg, tags);
            break;
    }
}
```

- [ ] **Step 4: Build et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaExpressionTests"
```
Expected: tous les tests verts.

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpression.cs
git add tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionTests.cs
git commit -m "feat: collect canonical tag Ids in ScadaExpression.FromAst

Adds FromAst factory that prefers TagId over TagName when collecting
ReferencedTags. Updates CollectTagRefs to use canonical Id when available,
falling back to TagName for legacy refs.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 4: `ElementStateRuleDialog` â€” TagName humain + TagId canonique

**Files:**
- Modify: `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ElementEvents/StateEditorEffectDialogContractTests.cs`

**Interfaces:**
- Consumes: `ScadaExprTagRef` avec `TagId?` (Task 1), `TryResolveTagReference` (Task 2), `ScadaExpression.FromAst` (Task 3)
- Produces: `BuildExpressionFromVariable` garde `tag.DisplayName` dans la source ; `OnSaveClick` rÃ©sout chaque TagRef individuellement

- [ ] **Step 1: Ã‰crire les tests de contrat UI**

```csharp
// Ajouter dans tests/ScadaBuilderV2.Tests/ElementEvents/StateEditorEffectDialogContractTests.cs

[TestMethod]
public void ElementStateRuleDialog_BuildExpression_KeepsDisplayNameInSource()
{
    var source = ReadAppFile("ElementStateRuleDialog.xaml.cs");
    var usesDisplayNameInSource = source.Contains("$\"{{{tag.DisplayName}}}");
    Assert.IsTrue(usesDisplayNameInSource,
        "BuildExpressionFromVariable must keep DisplayName in the expression source text.");
}

[TestMethod]
public void ElementStateRuleDialog_SelectTagByName_PrimaryById()
{
    var source = ReadAppFile("ElementStateRuleDialog.xaml.cs");
    var primaryById = source.IndexOf("string.Equals(item.TagId, tagName", StringComparison.Ordinal);
    Assert.IsTrue(primaryById >= 0,
        "SelectTagByName must match by tag Id first.");
}

[TestMethod]
public void ElementStateRuleDialog_OnSave_InjectsTagIdIntoAst()
{
    var source = ReadAppFile("ElementStateRuleDialog.xaml.cs");
    var usesFromAst = source.Contains("FromAst");
    Assert.IsTrue(usesFromAst,
        "OnSaveClick must use ScadaExpression.FromAst or inject TagId into the AST.");
}

[TestMethod]
public void ElementStateRuleDialog_ResolveTagIds_UsesTryResolvePerRef()
{
    var source = ReadAppFile("ElementStateRuleDialog.xaml.cs");
    // Ne doit PAS injecter le mÃªme TagId aveuglÃ©ment dans tous les TagRefs
    var hasBlindInject = source.Contains("InjectTagIds(node, tag.Id)");
    Assert.IsFalse(hasBlindInject,
        "Must resolve each TagRef individually, not inject the same TagId everywhere.");
}
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~StateEditorEffectDialogContractTests"
```
Expected: `ElementStateRuleDialog_OnSave_InjectsTagIdIntoAst` FAIL, `ElementStateRuleDialog_ResolveTagIds_UsesTryResolvePerRef` FAIL.

- [ ] **Step 3: `BuildExpressionFromVariable` â€” garder `DisplayName` (AUCUN CHANGEMENT)**

La source UI reste `{PE_16} == true`. Le `TagId` est injectÃ© dans l'AST uniquement au moment du `OnSaveClick`.

- [ ] **Step 4: `SelectTagByName` â€” inverser primaire/fallback**

```csharp
// Remplacer SelectTagByName dans ElementStateRuleDialog.xaml.cs:234-263

private void SelectTagByName(string tagName)
{
    // Primary: match by Id (canonical)
    for (int i = 0; i < TagComboBox.Items.Count; i++)
    {
        if (TagComboBox.Items[i] is TagItem item &&
            string.Equals(item.TagId, tagName, StringComparison.Ordinal))
        {
            TagComboBox.SelectedIndex = i;
            return;
        }
    }

    // Fallback 1: match by DisplayName (backward compat)
    for (int i = 0; i < TagComboBox.Items.Count; i++)
    {
        if (TagComboBox.Items[i] is TagItem item)
        {
            var tag = (_tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
                .FirstOrDefault(t => t.Id == item.TagId);
            if (tag is not null && string.Equals(tag.DisplayName, tagName, StringComparison.OrdinalIgnoreCase))
            {
                TagComboBox.SelectedIndex = i;
                return;
            }
        }
    }

    // Fallback 2: match by KeywordLabel
    for (int i = 0; i < TagComboBox.Items.Count; i++)
    {
        if (TagComboBox.Items[i] is TagItem item)
        {
            var tag = (_tagCatalog?.Tags ?? Array.Empty<ScadaTagDefinition>())
                .FirstOrDefault(t => t.Id == item.TagId);
            if (tag?.KeywordLabel is not null &&
                string.Equals(tag.KeywordLabel, tagName, StringComparison.OrdinalIgnoreCase))
            {
                TagComboBox.SelectedIndex = i;
                return;
            }
        }
    }

    // Tag non trouvÃ© : fallback Expression manuelle
    ExpressionModeRadio.IsChecked = true;
    ExpressionTextBox.Text = $"{{{tagName}}}";
}
```

- [ ] **Step 5: Ajouter `ResolveTagIds` â€” rÃ©solution individuelle par TagRef avec prioritÃ© `SelectedTag.Id`**

```csharp
// Ajouter dans ElementStateRuleDialog.xaml.cs

/// <summary>
/// Resolves <see cref="ScadaExprTagRef.TagId"/> for every tag reference in the AST.
/// When the expression was built from the dropdown (single tag selected),
/// <paramref name="selectedTagId"/> provides the canonical Id and bypasses catalog lookup.
/// For manual expressions or additional refs, falls back to catalog resolution.
/// </summary>
private static ScadaExprNode ResolveTagIds(
    ScadaExprNode node,
    ScadaTagCatalog? catalog,
    string? selectedTagId,
    string? selectedTagDisplayName)
{
    return node switch
    {
        ScadaExprTagRef tagRef => ResolveSingleTagRef(
            tagRef, catalog, selectedTagId, selectedTagDisplayName),
        ScadaExprUnary unary =>
            new ScadaExprUnary(unary.Op, ResolveTagIds(
                unary.Operand, catalog, selectedTagId, selectedTagDisplayName)),
        ScadaExprBinary binary =>
            new ScadaExprBinary(binary.Op,
                ResolveTagIds(binary.Left, catalog, selectedTagId, selectedTagDisplayName),
                ResolveTagIds(binary.Right, catalog, selectedTagId, selectedTagDisplayName)),
        ScadaExprFunc func =>
            new ScadaExprFunc(func.Name,
                func.Args.Select(a => ResolveTagIds(
                    a, catalog, selectedTagId, selectedTagDisplayName)).ToArray()),
        _ => node
    };
}

/// <summary>
/// Resolves a single TagRef:
/// 1. If TagId is already present (re-edition), keep it.
/// 2. If the selectedTagId matches this TagRef's TagName (dropdown-created expression),
///    use selectedTagId directly â€” avoids ambiguity when DisplayName is duplicated (D2).
/// 3. Otherwise, try to resolve via the catalog.
/// </summary>
private static ScadaExprTagRef ResolveSingleTagRef(
    ScadaExprTagRef tagRef,
    ScadaTagCatalog? catalog,
    string? selectedTagId,
    string? selectedTagDisplayName)
{
    if (!string.IsNullOrWhiteSpace(tagRef.TagId))
        return tagRef; // dÃ©jÃ  rÃ©solu (rÃ©-Ã©dition)

    // PrioritÃ© dropdown : le tag sÃ©lectionnÃ© explicitement par l'utilisateur
    if (!string.IsNullOrWhiteSpace(selectedTagId) &&
        string.Equals(tagRef.TagName, selectedTagDisplayName, StringComparison.OrdinalIgnoreCase))
    {
        return new ScadaExprTagRef(tagRef.TagName, selectedTagId);
    }

    // Fallback catalogue pour les expressions manuelles
    if (catalog is not null)
    {
        var result = ScadaExpressionValidator.TryResolveTagReference(
            tagRef.TagName, catalog);
        if (result.Status == TagResolveStatus.Resolved && result.CanonicalId is not null)
            return new ScadaExprTagRef(tagRef.TagName, result.CanonicalId);
    }

    return tagRef; // non rÃ©solu : garder TagName sans TagId
}
```

Dans `OnSaveClick`, remplacer la crÃ©ation de l'expression :

```csharp
// Avant :
// var expression = ScadaExpression.FromSource(source);

// AprÃ¨s :
var parsed = ScadaExpression.FromSource(source);
ScadaExprNode? ast = parsed.Ast;
if (ast is not null)
    ast = ResolveTagIds(ast, _tagCatalog, SelectedTag?.Id, SelectedTag?.DisplayName);
var expression = ScadaExpression.FromAst(source, ast);
```

- [ ] **Step 6: Build et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~StateEditorEffectDialogContractTests"
```
Expected: tous les tests verts.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs
git add tests/ScadaBuilderV2.Tests/ElementEvents/StateEditorEffectDialogContractTests.cs
git commit -m "feat: inject canonical TagId per TagRef in state expressions

BuildExpressionFromVariable keeps DisplayName in the UI source text.
SelectTagByName resolves by Id first, then DisplayName/KeywordLabel.
OnSaveClick resolves TagId individually for each TagRef via the catalog
resolver, supporting multi-tag manual expressions.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 5: `ScadaExpressionValidator` â€” resolver intÃ©grÃ© + dÃ©tection d'ambiguÃ¯tÃ©s

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpressionValidator.cs:33-115`
- Modify: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionValidatorTests.cs`

**Interfaces:**
- Consumes: `TryResolveTagReference` (Task 2)
- Produces: `Validate` utilise le resolver ; dÃ©tecte les rÃ©fÃ©rences inconnues ET ambiguÃ«s

- [ ] **Step 1: Ã‰crire les tests**

```csharp
// Ajouter dans tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionValidatorTests.cs

[TestMethod]
public void Validate_CanonicalId_Passes()
{
    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("tf100.mapping.196", "MC_120C", Datatype: "bool"),
    });
    var result = ScadaExpressionValidator.Validate("{tf100.mapping.196} == false", catalog);
    Assert.IsTrue(result.IsValid);
}

[TestMethod]
public void Validate_DisplayName_StillPassesForBackwardCompat()
{
    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("tf100.mapping.196", "MC_120C", Datatype: "bool"),
    });
    var result = ScadaExpressionValidator.Validate("{MC_120C} == false", catalog);
    Assert.IsTrue(result.IsValid);
}

[TestMethod]
public void Validate_KeywordLabel_StillPassesForBackwardCompat()
{
    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("tf100.mapping.196", "LongDisplayName",
            KeywordLabel: "MC_120C", Datatype: "bool"),
    });
    var result = ScadaExpressionValidator.Validate("{MC_120C} == false", catalog);
    Assert.IsTrue(result.IsValid);
}

[TestMethod]
public void Validate_UnknownTag_Fails()
{
    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("tf100.mapping.196", "MC_120C", Datatype: "bool"),
    });
    var result = ScadaExpressionValidator.Validate("{Inconnu} == true", catalog);
    Assert.IsFalse(result.IsValid);
    Assert.IsTrue(result.Errors.Any(e => e.Contains("Inconnu")));
}

[TestMethod]
public void Validate_AmbiguousTag_Fails()
{
    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("tf100.mapping.200", "DuplicateLabel", Datatype: "float"),
        new ScadaTagDefinition("tf100.mapping.201", "DuplicateLabel", Datatype: "bool"),
    });
    var result = ScadaExpressionValidator.Validate("{DuplicateLabel} == true", catalog);
    Assert.IsFalse(result.IsValid,
        "Ambiguous tag reference must fail validation.");
    Assert.IsTrue(result.Errors.Any(e =>
        e.Contains("DuplicateLabel") && e.Contains("ambigu", StringComparison.OrdinalIgnoreCase)));
}

[TestMethod]
public void Validate_AmbiguousById_StillPasses()
{
    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("tf100.mapping.200", "DuplicateLabel", Datatype: "float"),
        new ScadaTagDefinition("tf100.mapping.201", "DuplicateLabel", Datatype: "bool"),
    });
    var result = ScadaExpressionValidator.Validate("{tf100.mapping.200} == true", catalog);
    Assert.IsTrue(result.IsValid,
        "Direct Id reference must pass even when DisplayName is duplicated.");
}
```

- [ ] **Step 2: Run tests**

```powershell
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaExpressionValidatorTests"
```
Expected: `Validate_CanonicalId_Passes` FAIL, `Validate_KeywordLabel_StillPasses` FAIL, `Validate_AmbiguousTag_Fails` FAIL.

- [ ] **Step 3: Remplacer `Validate` et `Walk`**

```csharp
// Remplacer la mÃ©thode Validate et supprimer l'ancienne mÃ©thode Walk :

public static ScadaExprValidationResult Validate(string source, ScadaTagCatalog? tagCatalog)
{
    var parseResult = ScadaExpressionParser.Parse(source);
    if (parseResult.Root is null)
        return new ScadaExprValidationResult(false, parseResult.Errors, Array.Empty<string>());

    var errors = new List<string>();
    var referencedTags = new List<string>();

    WalkAndValidate(parseResult.Root, errors, referencedTags, tagCatalog);

    if (!IsBooleanNode(parseResult.Root))
        errors.Add("La condition doit s'evaluer en booleen (utilisez une comparaison ou un operateur logique a la racine).");

    return new ScadaExprValidationResult(errors.Count == 0, errors, referencedTags);
}

private static void WalkAndValidate(
    ScadaExprNode node, List<string> errors, List<string> referencedTags,
    ScadaTagCatalog? catalog)
{
    switch (node)
    {
        case ScadaExprTagRef tagRef:
            referencedTags.Add(tagRef.TagName);
            if (catalog is not null)
            {
                var resolveResult = TryResolveTagReference(tagRef.TagName, catalog);
                switch (resolveResult.Status)
                {
                    case TagResolveStatus.Unresolved:
                        errors.Add($"Le tag '{tagRef.TagName}' n'existe pas dans le catalogue du projet.");
                        break;
                    case TagResolveStatus.Ambiguous:
                        errors.Add($"Le tag '{tagRef.TagName}' est ambigu : plusieurs tags correspondent " +
                                   $"({string.Join(", ", resolveResult.Matches)}). Utilisez l'Id canonique.");
                        break;
                }
            }
            break;

        case ScadaExprUnary unary:
            WalkAndValidate(unary.Operand, errors, referencedTags, catalog);
            break;

        case ScadaExprBinary binary:
            WalkAndValidate(binary.Left, errors, referencedTags, catalog);
            WalkAndValidate(binary.Right, errors, referencedTags, catalog);
            if (binary.Op == ScadaExprBinaryOp.Divide && IsLiteralZero(binary.Right))
                errors.Add("Division par zero litterale detectee.");
            break;

        case ScadaExprFunc func:
            if (!FunctionArity.TryGetValue(func.Name, out var expectedArity))
            {
                errors.Add($"Fonction inconnue : '{func.Name}'. Fonctions supportees : ABS, MIN, MAX, BIT.");
            }
            else if (func.Args.Count != expectedArity)
            {
                errors.Add($"La fonction '{func.Name}' attend {expectedArity} argument(s), {func.Args.Count} fourni(s).");
            }
            foreach (var arg in func.Args)
                WalkAndValidate(arg, errors, referencedTags, catalog);
            break;
    }
}
```

- [ ] **Step 4: Build et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaExpressionValidatorTests"
```
Expected: tous les tests verts (anciens + nouveaux).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpressionValidator.cs
git add tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionValidatorTests.cs
git commit -m "feat: use TryResolveTagReference in validator, detect ambiguities

Validator now resolves tags via Id/DisplayName/KeywordLabel. Unknown tags
fail validation. Ambiguous references (multiple tags matching the same
label) are detected and reported with conflicting Ids.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 6: `Ft100SceneExporter` â€” normalisation HTML + manifest, warning non-rÃ©solu, blocage ambiguÃ¯tÃ©

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExportResult.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100ProjectExportResult.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

**Interfaces:**
- Consumes: `ScadaExprTagRef` avec `TagId?` (Task 1), `TryResolveTagReference` (Task 2), `ScadaExpression.FromAst` (Task 3)
- Produces: `NormalizeStateConfigForExport` (bloque sur ambigu, avertit sur non-rÃ©solu) ; appliquÃ© dans `BuildStateCommandAttributes` (HTML) et `BuildManifestPage` (manifest) ; `Ft100SceneExportResult.Warnings`

- [ ] **Step 1: Ã‰crire les tests d'export**

```csharp
// Ajouter dans tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs

private static string CreateTempExportDir()
{
    var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    Directory.CreateDirectory(Path.Combine(root, "source"));
    return root;
}

private static ScadaProject CreateProjectWithCatalog(
    ScadaTagCatalog catalog,
    params ScadaScene[] scenes) =>
    ScadaProject.CreateDefault("TestProject") with
    {
        TagCatalog = catalog,
        Scenes = scenes.Select(scene => new ScadaSceneReference(
            scene.Id,
            scene.Title,
            $"{scene.Id}.html",
            scene.PageType,
            scene.CanvasSize,
            scene.Background,
            scene.IncludeInBuild,
            scene.HeaderPageId,
            scene.FooterPageId)).ToArray()
    };

[TestMethod]
public async Task ExportAsync_TagRefWithTagId_ExportsCanonicalTagName()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "tagid_test.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var expr = new ScadaExpression(
        "{PE_16} == true",
        new ScadaExprBinary(ScadaExprBinaryOp.Equal,
            new ScadaExprTagRef("PE_16", "tf100.mapping.161"),
            new ScadaExprLiteralBool(true)),
        new[] { "PE_16" });

    var element = new ScadaElement(
        "el_canonical", "Canonical", ScadaElementKind.Text,
        new SceneBounds(10, 20, 100, 30), null, ScadaElementLayout.Absolute,
        ScadaElementStyle.DefaultText,
        new ScadaElementData("test", null, null, null, null, null, null, null, null, false),
        StateConfig: new ScadaElementStateConfig(
            ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
            ScadaEffectBlock.Empty,
            new[] { new ScadaStateRule("s1", "Running", true, expr,
                new ScadaEffectBlock(ColorFilterColor: "#12B729")) }));

    var scene = ScadaScene.CreateEmpty("win00008", "Test", new(1280, 873)).WithElement(element);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var html = await File.ReadAllTextAsync(result.HtmlPath);
        var decoded = html.Replace("&quot;", "\"");

        StringAssert.Contains(decoded, "\"tagName\":\"tf100.mapping.161\"",
            "Exported AST must use canonical Id as tagName.");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

[TestMethod]
public async Task ExportAsync_TagRefWithTagId_NormalizesEvenWithoutCatalog()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "nocatalog_test.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var expr = new ScadaExpression(
        "{PE_16} == true",
        new ScadaExprBinary(ScadaExprBinaryOp.Equal,
            new ScadaExprTagRef("PE_16", "tf100.mapping.161"),
            new ScadaExprLiteralBool(true)),
        new[] { "PE_16" });

    var element = new ScadaElement(
        "el_nocat", "NoCatalog", ScadaElementKind.Text,
        new SceneBounds(10, 20, 100, 30), null, ScadaElementLayout.Absolute,
        ScadaElementStyle.DefaultText,
        new ScadaElementData("test", null, null, null, null, null, null, null, null, false),
        StateConfig: new ScadaElementStateConfig(
            ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
            ScadaEffectBlock.Empty,
            new[] { new ScadaStateRule("s1", "R", true, expr,
                new ScadaEffectBlock(ColorFilterColor: "#12B729")) }));

    var scene = ScadaScene.CreateEmpty("win00008", "NoCat", new(1280, 873)).WithElement(element);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var html = await File.ReadAllTextAsync(result.HtmlPath);
        var decoded = html.Replace("&quot;", "\"");

        StringAssert.Contains(decoded, "\"tagName\":\"tf100.mapping.161\"",
            "TagRef with TagId must be normalized even without a catalog.");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

[TestMethod]
public async Task ExportAsync_LegacyDisplayName_IsNormalizedViaCatalog()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "legacy_test.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var legacyExpr = new ScadaExpression(
        "{Noeud1_N15_04_Commande_MC_120C} == false",
        new ScadaExprBinary(ScadaExprBinaryOp.Equal,
            new ScadaExprTagRef("Noeud1_N15_04_Commande_MC_120C"),
            new ScadaExprLiteralBool(false)),
        new[] { "Noeud1_N15_04_Commande_MC_120C" });

    var element = new ScadaElement(
        "el_legacy", "Legacy", ScadaElementKind.Text,
        new SceneBounds(10, 20, 100, 30), null, ScadaElementLayout.Absolute,
        ScadaElementStyle.DefaultText,
        new ScadaElementData("test", null, null, null, null, null, null, null, null, false),
        StateConfig: new ScadaElementStateConfig(
            ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
            ScadaEffectBlock.Empty,
            new[] { new ScadaStateRule("s1", "Arret", true, legacyExpr,
                new ScadaEffectBlock(ColorFilterColor: "#E53935")) }));

    var scene = ScadaScene.CreateEmpty("win00008", "Legacy", new(1280, 873)).WithElement(element);

    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("tf100.mapping.196", "Noeud1_N15_04_Commande_MC_120C",
            KeywordLabel: "MC_120C", Datatype: "bool"),
    });
    var project = CreateProjectWithCatalog(catalog, scene);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"), project);

        // VÃ©rifier HTML
        var html = await File.ReadAllTextAsync(result.HtmlPath);
        var decoded = html.Replace("&quot;", "\"");
        StringAssert.Contains(decoded, "\"tagName\":\"tf100.mapping.196\"");
        Assert.IsFalse(decoded.Contains("\"tagName\":\"Noeud1_N15_04_Commande_MC_120C\""),
            "Exported AST must not use DisplayName as tagName when tag is resolved.");

        // VÃ©rifier manifest
        var manifest = await File.ReadAllTextAsync(
            Path.Combine(result.ExportDirectory, "manifest.json"));
        StringAssert.Contains(manifest, "\"tagName\":\"tf100.mapping.196\"");
        Assert.IsFalse(manifest.Contains("\"tagName\":\"Noeud1_N15_04_Commande_MC_120C\""),
            "Manifest must also use canonical Id in AST.");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

[TestMethod]
public async Task ExportAsync_UnresolvedTagRef_KeepsOriginalTagName()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "unresolved_test.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var expr = new ScadaExpression(
        "{TagInconnu} == true",
        new ScadaExprBinary(ScadaExprBinaryOp.Equal,
            new ScadaExprTagRef("TagInconnu"),
            new ScadaExprLiteralBool(true)),
        new[] { "TagInconnu" });

    var element = new ScadaElement(
        "el_unresolved", "Unresolved", ScadaElementKind.Text,
        new SceneBounds(10, 20, 100, 30), null, ScadaElementLayout.Absolute,
        ScadaElementStyle.DefaultText,
        new ScadaElementData("test", null, null, null, null, null, null, null, null, false),
        StateConfig: new ScadaElementStateConfig(
            ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
            ScadaEffectBlock.Empty,
            new[] { new ScadaStateRule("s1", "Unk", true, expr,
                new ScadaEffectBlock(ColorFilterColor: "#E53935")) }));

    var scene = ScadaScene.CreateEmpty("win00008", "Unresolved", new(1280, 873)).WithElement(element);

    // Projet avec catalogue ne contenant PAS ce tag â€” pour que le warning soit Ã©mis
    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("tf100.mapping.196", "AutreTag", Datatype: "bool"),
    });
    var project = CreateProjectWithCatalog(catalog, scene);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"), project);

        var html = await File.ReadAllTextAsync(result.HtmlPath);
        var decoded = html.Replace("&quot;", "\"");

        // La rÃ©fÃ©rence non rÃ©solue doit rester telle quelle
        StringAssert.Contains(decoded, "\"tagName\":\"TagInconnu\"",
            "Unresolved tag ref must be left as-is for qualityFallback.");

        // Un warning doit Ãªtre prÃ©sent dans le rÃ©sultat d'export
        Assert.IsTrue(result.Warnings.Any(w => w.Contains("TagInconnu")),
            "Export must warn about unresolved tag references.");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

[TestMethod]
public async Task ExportAsync_AmbiguousTagRef_ThrowsInvalidOperationException()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "ambiguous_test.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var expr = new ScadaExpression(
        "{DuplicateLabel} == true",
        new ScadaExprBinary(ScadaExprBinaryOp.Equal,
            new ScadaExprTagRef("DuplicateLabel"),
            new ScadaExprLiteralBool(true)),
        new[] { "DuplicateLabel" });

    var element = new ScadaElement(
        "el_ambig", "Ambiguous", ScadaElementKind.Text,
        new SceneBounds(10, 20, 100, 30), null, ScadaElementLayout.Absolute,
        ScadaElementStyle.DefaultText,
        new ScadaElementData("test", null, null, null, null, null, null, null, null, false),
        StateConfig: new ScadaElementStateConfig(
            ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
            ScadaEffectBlock.Empty,
            new[] { new ScadaStateRule("s1", "Amb", true, expr,
                new ScadaEffectBlock(ColorFilterColor: "#E53935")) }));

    var scene = ScadaScene.CreateEmpty("win00008", "Ambiguous", new(1280, 873)).WithElement(element);

    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("tf100.mapping.200", "DuplicateLabel", Datatype: "float"),
        new ScadaTagDefinition("tf100.mapping.201", "DuplicateLabel", Datatype: "bool"),
    });
    var project = CreateProjectWithCatalog(catalog, scene);

    try
    {
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => new Ft100SceneExporter().ExportAsync(
                scene, sourceHtmlPath, Path.Combine(root, "export"), project),
            "Ambiguous tag reference must block export (D8).");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ExportAsync_TagRefWithTagId_ExportsCanonicalTagName"
```
Expected: FAIL.

- [ ] **Step 3: ImplÃ©menter les mÃ©thodes de normalisation**

```csharp
// Ajouter dans src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs

/// <summary>
/// Normalizes a <see cref="ScadaElementStateConfig"/> for export by replacing
/// <see cref="ScadaExprTagRef.TagName"/> with the canonical identifier in all
/// expression ASTs. Returns the normalized config and a list of warnings for
/// unresolved references. Throws on ambiguous references.
/// </summary>
private static ScadaElementStateConfig NormalizeStateConfigForExport(
    ScadaElementStateConfig config,
    ScadaTagCatalog? catalog,
    List<string> warnings)
{
    if (config.States.Count == 0) return config;

    var hasAmbiguous = false;
    var ambiguousMessages = new List<string>();

    var normalizedStates = config.States.Select(state =>
    {
        if (state.Expression?.Ast is null) return state;

        var (normalizedAst, ambiguous) = NormalizeAstForExport(
            state.Expression.Ast, catalog, state.Expression.Source);
        if (ambiguous.Count > 0)
        {
            hasAmbiguous = true;
            ambiguousMessages.AddRange(ambiguous);
        }

        // Emettre un warning pour chaque TagRef non rÃ©solu
        EmitUnresolvedWarnings(normalizedAst, state.Expression.Source, warnings);

        var referencedTags = CollectCanonicalTags(normalizedAst);
        var normalizedExpression = new ScadaExpression(
            state.Expression.Source, normalizedAst, referencedTags);

        return new ScadaStateRule(state.Id, state.Name, state.Enabled,
            normalizedExpression, state.Effect);
    }).ToArray();

    if (hasAmbiguous)
    {
        throw new InvalidOperationException(
            "L'export est bloque car des references de tag dans les expressions d'etat " +
            "sont ambigues :\n" + string.Join("\n", ambiguousMessages));
    }

    return new ScadaElementStateConfig(
        config.QualityFallback, config.DefaultEffect, normalizedStates, config.ReadVariable);
}

/// <summary>
/// Walks the AST and adds a warning for each TagRef whose TagName does not
/// look like a canonical Id (i.e. will likely be unresolved at runtime,
/// causing qualityFallback).
/// </summary>
private static void EmitUnresolvedWarnings(
    ScadaExprNode node, string expressionSource, List<string> warnings)
{
    switch (node)
    {
        case ScadaExprTagRef tagRef:
            // Si le tagName n'est pas au format tf100.mapping.X, c'est probablement
            // un DisplayName legacy non rÃ©solu
            if (!tagRef.TagName.StartsWith("tf100.mapping.", StringComparison.OrdinalIgnoreCase))
            {
                AddWarningOnce(
                    warnings,
                    $"Expression \"{expressionSource}\" : la reference '{{{tagRef.TagName}}}' " +
                    "n'a pas pu etre resolue en Id canonique. Le runtime appliquera qualityFallback.");
            }
            break;
        case ScadaExprUnary unary:
            EmitUnresolvedWarnings(unary.Operand, expressionSource, warnings);
            break;
        case ScadaExprBinary binary:
            EmitUnresolvedWarnings(binary.Left, expressionSource, warnings);
            EmitUnresolvedWarnings(binary.Right, expressionSource, warnings);
            break;
        case ScadaExprFunc func:
            foreach (var arg in func.Args)
                EmitUnresolvedWarnings(arg, expressionSource, warnings);
            break;
    }
}

private static void AddWarningOnce(List<string> warnings, string warning)
{
    if (!warnings.Contains(warning, StringComparer.Ordinal))
    {
        warnings.Add(warning);
    }
}

private static (ScadaExprNode Node, List<string> Ambiguous) NormalizeAstForExport(
    ScadaExprNode node, ScadaTagCatalog? catalog, string expressionSource)
{
    return node switch
    {
        ScadaExprTagRef tagRef =>
            (NormalizeTagRefForExport(tagRef, catalog, expressionSource, out var amb), amb),

        ScadaExprUnary unary =>
            NormalizeUnaryForExport(unary, catalog, expressionSource),

        ScadaExprBinary binary =>
            NormalizeBinaryForExport(binary, catalog, expressionSource),

        ScadaExprFunc func =>
            NormalizeFuncForExport(func, catalog, expressionSource),

        _ => (node, new List<string>())
    };
}

private static ScadaExprTagRef NormalizeTagRefForExport(
    ScadaExprTagRef tagRef, ScadaTagCatalog? catalog, string source,
    out List<string> ambiguous)
{
    ambiguous = new List<string>();

    // PrioritÃ© 1 : TagId dÃ©jÃ  prÃ©sent â†’ normalisation directe
    if (!string.IsNullOrWhiteSpace(tagRef.TagId))
        return new ScadaExprTagRef(tagRef.TagId, tagRef.TagId);

    // PrioritÃ© 2 : rÃ©soudre TagName via le catalogue
    if (catalog is not null)
    {
        var result = ScadaExpressionValidator.TryResolveTagReference(
            tagRef.TagName, catalog);
        switch (result.Status)
        {
            case TagResolveStatus.Resolved when result.CanonicalId is not null:
                return new ScadaExprTagRef(result.CanonicalId, result.CanonicalId);

            case TagResolveStatus.Ambiguous:
                ambiguous.Add(
                    $"Expression \"{source}\" : le tag '{{{tagRef.TagName}}}' est ambigu " +
                    $"({string.Join(", ", result.Matches)}). Remplacez-le par l'Id canonique.");
                break;
        }
    }

    // Non rÃ©solu : laisser TagName tel quel â†’ qualityFallback au runtime
    return tagRef;
}

private static (ScadaExprNode Node, List<string> Ambiguous) NormalizeUnaryForExport(
    ScadaExprUnary unary, ScadaTagCatalog? catalog, string source)
{
    var (operand, amb) = NormalizeAstForExport(unary.Operand, catalog, source);
    return (new ScadaExprUnary(unary.Op, operand), amb);
}

private static (ScadaExprNode Node, List<string> Ambiguous) NormalizeBinaryForExport(
    ScadaExprBinary binary, ScadaTagCatalog? catalog, string source)
{
    var (left, ambL) = NormalizeAstForExport(binary.Left, catalog, source);
    var (right, ambR) = NormalizeAstForExport(binary.Right, catalog, source);
    var allAmb = new List<string>();
    allAmb.AddRange(ambL);
    allAmb.AddRange(ambR);
    return (new ScadaExprBinary(binary.Op, left, right), allAmb);
}

private static (ScadaExprNode Node, List<string> Ambiguous) NormalizeFuncForExport(
    ScadaExprFunc func, ScadaTagCatalog? catalog, string source)
{
    var allAmb = new List<string>();
    var normalizedArgs = func.Args.Select(arg =>
    {
        var (a, amb) = NormalizeAstForExport(arg, catalog, source);
        allAmb.AddRange(amb);
        return a;
    }).ToArray();
    return (new ScadaExprFunc(func.Name, normalizedArgs), allAmb);
}

private static IReadOnlyList<string> CollectCanonicalTags(ScadaExprNode node)
{
    var tags = new List<string>();
    CollectTagsForExport(node, tags);
    return tags;
}

private static void CollectTagsForExport(ScadaExprNode node, List<string> tags)
{
    switch (node)
    {
        case ScadaExprTagRef tagRef:
            tags.Add(tagRef.TagName);
            break;
        case ScadaExprUnary unary:
            CollectTagsForExport(unary.Operand, tags);
            break;
        case ScadaExprBinary binary:
            CollectTagsForExport(binary.Left, tags);
            CollectTagsForExport(binary.Right, tags);
            break;
        case ScadaExprFunc func:
            foreach (var arg in func.Args)
                CollectTagsForExport(arg, tags);
            break;
    }
}
```

- [ ] **Step 4: Propager `tagCatalog` et `warnings` dans toute la chaÃ®ne d'appel (HTML + Manifest)**

```csharp
// === Dans ExportAsync, aprÃ¨s validation ===
var tagCatalog = project?.TagCatalog;
var warnings = new List<string>();

// === ChaÃ®ne HTML : BuildHtml â†’ BuildElementHtml â†’ BuildStateCommandAttributes ===

// BuildHtml â€” nouvelle signature :
private static string BuildHtml(
    ScadaScene scene, string cssFileName, string sourceContent, string runtimeHash,
    ScadaTagCatalog? tagCatalog, List<string> warnings)
{
    // ... inchangÃ© sauf l'appel rÃ©cursif :
    var modernElements = string.Concat(scene.Elements.Select(
        element => BuildElementHtml(element, 0, 0, scope, tagCatalog, warnings)));
    // ...
}

// BuildElementHtml â€” nouvelle signature :
private static string BuildElementHtml(
    ScadaElement element, double parentX, double parentY, Ft100ExportScope scope,
    ScadaTagCatalog? tagCatalog, List<string> warnings)
{
    // Ligne 825 : BuildStateCommandAttributes(element) â†’
    //   BuildStateCommandAttributes(element, tagCatalog, warnings)
    // Ligne 826 : BuildElementHtml(child, 0, 0, scope) â†’
    //   BuildElementHtml(child, 0, 0, scope, tagCatalog, warnings)
    // Ligne 835 : BuildElementHtml(child, absoluteX, absoluteY, scope) â†’
    //   BuildElementHtml(child, absoluteX, absoluteY, scope, tagCatalog, warnings)
    // Ligne 847 : BuildStateCommandAttributes(element) â†’
    //   BuildStateCommandAttributes(element, tagCatalog, warnings)
}

// BuildStateCommandAttributes â€” nouvelle signature :
private static string BuildStateCommandAttributes(
    ScadaElement element, ScadaTagCatalog? catalog, List<string> warnings)
{
    var stateConfig = element.EffectiveStateConfig;
    var commandConfig = element.EffectiveCommandConfig;
    var hasStateConfig = stateConfig.States.Count > 0 || HasNonDefaultFallback(stateConfig);
    var hasCommandConfig = commandConfig.Commands.Count > 0;

    if (!hasStateConfig && !hasCommandConfig) return "";

    var attributes = new StringBuilder();
    if (hasStateConfig)
    {
        var exportConfig = NormalizeStateConfigForExport(stateConfig, catalog, warnings);
        var json = JsonSerializer.Serialize(exportConfig, StateCommandJsonOptions);
        attributes.Append(" data-scada-state-config=\"");
        attributes.Append(HtmlEncoder.Default.Encode(json));
        attributes.Append('"');
    }

    if (hasCommandConfig)
    {
        var json = JsonSerializer.Serialize(commandConfig, StateCommandJsonOptions);
        attributes.Append(" data-scada-command-config=\"");
        attributes.Append(HtmlEncoder.Default.Encode(json));
        attributes.Append('"');
    }

    return attributes.ToString();
}

// === ChaÃ®ne Manifest : BuildManifest / BuildProjectManifest â†’ BuildManifestPage ===

// BuildManifest â€” nouvelle signature :
private static string BuildManifest(
    ScadaScene scene, ScadaProject? project, List<string> warnings)
{
    var tagCatalog = project?.TagCatalog;
    var homePageId = project?.EffectiveHomePageId;
    return JsonSerializer.Serialize(new
    {
        // ... inchangÃ© sauf :
        Pages = new[] { BuildManifestPage(scene, homePageId, false, tagCatalog, warnings) },
        // ...
    }, ManifestJsonOptions);
}

// BuildProjectManifest â€” nouvelle signature :
private static string BuildProjectManifest(
    ScadaProject project, IReadOnlyList<ScadaScene> scenes, List<string> warnings)
{
    var tagCatalog = project.TagCatalog;
    var homePageId = project.EffectiveHomePageId;
    return JsonSerializer.Serialize(new
    {
        // ... inchangÃ© sauf :
        Pages = exportedScenes
            .Select(scene => BuildManifestPage(scene, homePageId, true, tagCatalog, warnings))
            .ToArray(),
        // ...
    }, ManifestJsonOptions);
}

// BuildManifestPage â€” nouvelle signature :
private static object BuildManifestPage(
    ScadaScene scene, string? homePageId, bool projectRelativePath,
    ScadaTagCatalog? tagCatalog, List<string> warnings)
{
    // ... dans le corps, remplacer la ligne StateConfig par :
    StateConfig = element.EffectiveStateConfig.States.Count > 0
        || HasNonDefaultFallback(element.EffectiveStateConfig)
        ? NormalizeStateConfigForExport(element.EffectiveStateConfig, tagCatalog, warnings)
        : null,
}

// === Mettre Ã  jour les appels dans ExportAsync ===
// BuildHtml(scene, cssFileName, normalizedSourceContent, runtimeHash) â†’
//   BuildHtml(scene, cssFileName, normalizedSourceContent, runtimeHash, tagCatalog, warnings)
// BuildManifest(scene, project) â†’
//   BuildManifest(scene, project, warnings)
```

Dans `ExportProjectAsync`, agréger les warnings des pages avant de produire le manifest racine,
puis passer la même liste à `BuildProjectManifest` :

```csharp
var warnings = pageResults
    .SelectMany(page => page.Warnings)
    .Distinct(StringComparer.Ordinal)
    .ToList();

await File.WriteAllTextAsync(
    manifestPath,
    BuildProjectManifest(project, pageInputsById.Values.Select(input => input.Scene).ToArray(), warnings),
    Encoding.UTF8,
    cancellationToken);
```

- [ ] **Step 5: Ajouter `Warnings` Ã  `Ft100SceneExportResult`**

```csharp
// Dans src/ScadaBuilderV2.Rendering/Ft100SceneExportResult.cs :
public sealed record Ft100SceneExportResult(
    string ExportDirectory,
    string HtmlPath,
    string CssPath,
    string ImagesDirectory,
    int CopiedImageCount,
    IReadOnlyList<string> Warnings)
{
    public Ft100SceneExportResult(
        string exportDirectory, string htmlPath, string cssPath,
        string imagesDirectory, int copiedImageCount)
        : this(exportDirectory, htmlPath, cssPath, imagesDirectory,
               copiedImageCount, Array.Empty<string>())
    { }
}
```

Dans `ExportAsync`, Ã  la fin :

```csharp
return new Ft100SceneExportResult(
    sceneDirectory, htmlPath, cssPath, imagesDirectory, copiedImages, warnings);
```

Ajouter aussi les warnings agreges au resultat projet :

```csharp
// Dans src/ScadaBuilderV2.Rendering/Ft100ProjectExportResult.cs :
public sealed record Ft100ProjectExportResult(
    string ExportDirectory,
    string ManifestPath,
    IReadOnlyList<Ft100SceneExportResult> PageResults,
    int CopiedImageCount,
    IReadOnlyList<string> Warnings)
{
    public Ft100ProjectExportResult(
        string exportDirectory,
        string manifestPath,
        IReadOnlyList<Ft100SceneExportResult> pageResults,
        int copiedImageCount)
        : this(exportDirectory, manifestPath, pageResults, copiedImageCount,
               pageResults.SelectMany(page => page.Warnings).Distinct(StringComparer.Ordinal).ToArray())
    { }
}
```

Dans `ExportProjectAsync`, aprÃƒÂ¨s l'ÃƒÂ©criture du manifest projet :

```csharp
var projectWarnings = warnings
    .Distinct(StringComparer.Ordinal)
    .ToArray();

return new Ft100ProjectExportResult(
    packageDirectory,
    manifestPath,
    pageResults,
    pageResults.Sum(page => page.CopiedImageCount),
    projectWarnings);
```

- [ ] **Step 6: Build et tous les tests d'export**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Ft100SceneExporterTests"
```
Expected: tous les tests verts.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs
git add src/ScadaBuilderV2.Rendering/Ft100SceneExportResult.cs
git add tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "feat: normalize state expressions in HTML and manifest on export

NormalizeStateConfigForExport replaces tagName with canonical Id in all
expression ASTs. Applied in BuildStateCommandAttributes (HTML) and
BuildManifestPage (manifest). TagId is used when present (even without
catalog). Ambiguous refs throw. Unresolved refs emit warnings in
Ft100SceneExportResult.Warnings.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 7: Validation migration commandes/bindings (D9)

**Files:**
- Create: `src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBindingValidator.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementCommandConfigTests.cs`

**Interfaces:**
- Consumes: `TryResolveTagReference` (Task 2)
- Produces: `public static IReadOnlyList<string> ValidateCommandBinding(ScadaCommandBinding, ScadaTagCatalog?)` â€” dÃ©tecte les libellÃ©s humains dans `WriteTagId`/`ReadTagId`

- [ ] **Step 1: Ã‰crire les tests**

```csharp
// Ajouter dans tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementCommandConfigTests.cs

[TestMethod]
public void ValidateCommandBinding_CanonicalId_Passes()
{
    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("tf100.mapping.196", "MC_120C", Datatype: "bool"),
    });
    var cmd = new ScadaCommandBinding("cmd1", "Write", true,
        ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
        WriteTagId: "tf100.mapping.196", WriteMode: ScadaWriteMode.SetFixed,
        FixedValue: "true");

    var issues = ScadaCommandBindingValidator.ValidateCommandBinding(cmd, catalog);
    Assert.AreEqual(0, issues.Count);
}

[TestMethod]
public void ValidateCommandBinding_DisplayNameAsWriteTagId_ReturnsIssue()
{
    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("tf100.mapping.196", "MC_120C", Datatype: "bool"),
    });
    var cmd = new ScadaCommandBinding("cmd1", "Write", true,
        ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
        WriteTagId: "MC_120C", // DisplayName, pas Id
        WriteMode: ScadaWriteMode.SetFixed, FixedValue: "true");

    var issues = ScadaCommandBindingValidator.ValidateCommandBinding(cmd, catalog);
    Assert.IsTrue(issues.Count > 0);
    Assert.IsTrue(issues[0].Contains("MC_120C"));
    Assert.IsTrue(issues[0].Contains("tf100.mapping.196"));
}

[TestMethod]
public void ValidateCommandBinding_NullCatalog_Skips()
{
    var cmd = new ScadaCommandBinding("cmd1", "Write", true,
        ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
        WriteTagId: "nimporte", WriteMode: ScadaWriteMode.SetFixed,
        FixedValue: "true");

    var issues = ScadaCommandBindingValidator.ValidateCommandBinding(cmd, null);
    Assert.AreEqual(0, issues.Count,
        "Null catalog must skip validation (no false positives).");
}
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ValidateCommandBinding_"
```
Expected: compilation errors â€” `ScadaCommandBindingValidator` n'existe pas.

- [ ] **Step 3: CrÃ©er le validateur**

```csharp
// CrÃ©er src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBindingValidator.cs

using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Domain.ElementEvents.Command;

/// <summary>
/// Validates that <see cref="ScadaCommandBinding"/> tag references use canonical Ids,
/// not human-readable labels.
/// </summary>
/// <remarks>
/// Decisions: DEC-0036, D9.
/// Contracts: docs/superpowers/specs/2026-07-09-expression-tag-id-reference.md Â§3.0.
/// </remarks>
public static class ScadaCommandBindingValidator
{
    /// <summary>
    /// Validates a single command binding's tag references against the catalog.
    /// Returns a list of issues (empty if valid).
    /// </summary>
    public static IReadOnlyList<string> ValidateCommandBinding(
        ScadaCommandBinding command, ScadaTagCatalog? catalog)
    {
        if (catalog?.Tags is null || catalog.Tags.Count == 0)
            return Array.Empty<string>();

        var issues = new List<string>();
        var tagIds = catalog.Tags
            .Select(t => t.Id)
            .ToHashSet(StringComparer.Ordinal);

        CheckTagId(command.WriteTagId, "WriteTagId", command.Name, catalog, tagIds, issues);
        CheckTagId(command.ReadTagId, "ReadTagId", command.Name, catalog, tagIds, issues);

        return issues;
    }

    private static void CheckTagId(
        string? tagValue, string field, string commandName,
        ScadaTagCatalog catalog, HashSet<string> canonicalIds,
        List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(tagValue)) return;
        if (canonicalIds.Contains(tagValue)) return; // dÃ©jÃ  canonique

        var result = ScadaExpressionValidator.TryResolveTagReference(tagValue, catalog);
        if (result.Status == TagResolveStatus.Resolved && result.CanonicalId is not null)
        {
            issues.Add(
                $"La commande '{commandName}' utilise un libelle humain comme {field} " +
                $"('{tagValue}'). Remplacez-le par l'Id canonique '{result.CanonicalId}'.");
        }
    }
}
```

- [ ] **Step 4: Build et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ValidateCommandBinding_"
```
Expected: 3 tests verts.

- [ ] **Step 5: IntÃ©grer dans `ScadaProjectBuildValidator`**

```csharp
// Dans src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs,
// mÃ©thode Validate(ScadaProject project, IReadOnlyList<ScadaScene> scenes, ...) :

// Ajouter en haut du fichier si absent :
// using ScadaBuilderV2.Domain.ElementEvents.Command;

// Ajouter apres AuditOrphanedEventBindings :
ValidateSceneCommandBindings(issues, scene, project.TagCatalog);
```

Ajouter la methode privee suivante dans `ScadaProjectBuildValidator`.
Elle reutilise le `FlattenElements` deja present dans `ProjectModels.cs` :

```csharp
private static void ValidateSceneCommandBindings(
    List<ScadaBuildValidationIssue> issues,
    ScadaScene scene,
    ScadaTagCatalog? tagCatalog)
{
    foreach (var element in FlattenElements(scene.Elements))
    {
        foreach (var cmd in element.EffectiveCommandConfig.Commands)
        {
            var cmdIssues = ScadaCommandBindingValidator.ValidateCommandBinding(
                cmd, tagCatalog);
            foreach (var issue in cmdIssues)
            {
                issues.Add(new ScadaBuildValidationIssue(
                    ScadaBuildValidationSeverity.Warning,
                    "command.tag-human-label",
                    $"Scene '{scene.Id}', element '{element.Id}': {issue}",
                    scene.Id));
            }
        }
    }
}
```


- [ ] **Step 6: Build et tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ValidateCommandBinding_"
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaProjectBuildValidator"
```
Expected: tous les tests verts.

- [ ] **Step 7: Commit**

```bash
git add src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBindingValidator.cs
git add src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs
git add tests/ScadaBuilderV2.Tests/ElementEvents/ScadaElementCommandConfigTests.cs
git commit -m "feat: add command binding validation for canonical tag Ids (D9)

Detects human-readable labels used as WriteTagId/ReadTagId in command
bindings and suggests the canonical replacement. Integrated into
ScadaProjectBuildValidator for build/export validation.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 8: Tests runtime JS

**Files:**
- Modify: `tests/runtime-js/state-engine.test.mjs`

**Interfaces:**
- Consumes: none (indÃ©pendant)
- Produces: couverture de test pour rÃ©solution canonique, qualityFallback, defaultEffect

- [ ] **Step 1: Ajouter les tests avec le casing rÃ©el**

Le runtime JS reÃ§oit l'AST exportÃ© en camelCase. L'export utilise `JsonNamingPolicy.CamelCase` + `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`, donc les valeurs d'enum sont `"equal"`, `"add"`, etc. Les tests doivent utiliser ce casing.

```javascript
// Ajouter dans tests/runtime-js/state-engine.test.mjs

describe('canonical tag reference', () => {
    it('resolves canonical tagName and applies matching effect', () => {
        const element = createTestElement('el1', {
            qualityFallback: { opacity: 0.4, borderColor: '#000000', borderWidth: 2 },
            defaultEffect: {},
            states: [{
                id: 's1', name: 'Running', enabled: true,
                expression: {
                    source: '{tf100.mapping.196} == true',
                    referencedTags: ['tf100.mapping.196'],
                    ast: {
                        type: 'binary', op: 'equal',
                        left: { type: 'tagRef', tagName: 'tf100.mapping.196' },
                        right: { type: 'literalBool', value: true }
                    }
                },
                effect: { colorFilterColor: '#12B729', colorFilterOpacity: 0.8 }
            }]
        });

        window.ScadaRuntime.TagBridge.setTagValue('tf100.mapping.196', true);
        window.ScadaRuntime.StateEngine.evaluate(element, { 'tf100.mapping.196': true });

        const overlay = element.querySelector('[data-scada-color-filter-overlay]');
        assert.ok(overlay, 'color filter overlay must be present');
        assert.equal(overlay.style.backgroundColor, '#12b729');
    });

    it('applies qualityFallback when canonical tag is unavailable', () => {
        const element = createTestElement('el2', {
            qualityFallback: { opacity: 0.4, borderColor: '#000000', borderWidth: 2 },
            defaultEffect: {},
            states: [{
                id: 's1', name: 'Running', enabled: true,
                expression: {
                    source: '{tf100.mapping.196} == true',
                    ast: {
                        type: 'binary', op: 'equal',
                        left: { type: 'tagRef', tagName: 'tf100.mapping.196' },
                        right: { type: 'literalBool', value: true }
                    }
                },
                effect: { colorFilterColor: '#12B729' }
            }]
        });

        window.ScadaRuntime.TagBridge.setTagValue('tf100.mapping.196', null);
        window.ScadaRuntime.StateEngine.evaluate(element, {});

        assert.equal(element.style.opacity, '0.4');
        const overlay = element.querySelector('[data-scada-color-filter-overlay]');
        assert.ok(!overlay, 'color filter overlay must NOT be present when tag is null');
    });

    it('applies qualityFallback for unresolved legacy tag name', () => {
        const element = createTestElement('el3', {
            qualityFallback: { opacity: 0.4 },
            defaultEffect: {},
            states: [{
                id: 's1', name: 'Unknown', enabled: true,
                expression: {
                    source: '{NomAbsentDuCatalogue} == true',
                    ast: {
                        type: 'binary', op: 'equal',
                        left: { type: 'tagRef', tagName: 'NomAbsentDuCatalogue' },
                        right: { type: 'literalBool', value: true }
                    }
                },
                effect: { colorFilterColor: '#12B729' }
            }]
        });

        window.ScadaRuntime.StateEngine.evaluate(element, {});
        assert.equal(element.style.opacity, '0.4');
    });

    // NOTE: ce test Ã©chouera tant que state-engine.js ne distingue pas
    // "tag indisponible" de "condition fausse". Il est inclus comme
    // test de fermeture pour le correctif Ã  venir (Â§6.1 de la spec).
    it.skip('applies defaultEffect when condition is false with available tag', () => {
        const element = createTestElement('el4', {
            qualityFallback: { opacity: 0.4 },
            defaultEffect: { opacity: 1.0 },
            states: [{
                id: 's1', name: 'Running', enabled: true,
                expression: {
                    source: '{tf100.mapping.196} == true',
                    ast: {
                        type: 'binary', op: 'equal',
                        left: { type: 'tagRef', tagName: 'tf100.mapping.196' },
                        right: { type: 'literalBool', value: true }
                    }
                },
                effect: { colorFilterColor: '#12B729' }
            }]
        });

        window.ScadaRuntime.TagBridge.setTagValue('tf100.mapping.196', false);
        window.ScadaRuntime.StateEngine.evaluate(element, { 'tf100.mapping.196': false });

        assert.equal(element.style.opacity, '1.0',
            'false condition with available tag should apply defaultEffect, not qualityFallback');
    });
});
```

- [ ] **Step 2: ExÃ©cuter les tests**

```powershell
npm test -- tests/runtime-js/state-engine.test.mjs
```
Expected: 3 tests passent, 1 skip (defaultEffect).

- [ ] **Step 3: Commit**

```bash
git add tests/runtime-js/state-engine.test.mjs
git commit -m "test: add runtime JS tests for canonical tag resolution

Uses camelCase AST values (op: 'equal') matching the actual exported JSON.
Covers: canonical tf100.mapping.X resolves and applies effect, unavailable
tag triggers qualityFallback, unresolved legacy tagName falls through.
Includes skipped test for defaultEffect (pending state-engine fix).

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### VÃ©rification finale

- [ ] **Build + tests complets**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln
```

Tous les tests doivent passer, y compris les tests existants non modifiÃ©s.

- [ ] **Test de rÃ©gression `.sb2` complet (zip/manifest/html)**

```csharp
// Ajouter dans tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
// Ajouter les using si absents : System.IO.Compression, System.Text

[TestMethod]
public async Task ExportProjectArchiveAsync_Sb2Package_CoherentStateConfigInZipManifestAndHtml()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "sb2_test.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    // ScÃ¨ne simulÃ©e : 3 rectangles comme win00008, avec tags rÃ©solubles
    var greenExpr = new ScadaExpression(
        "{Noeud1_N15_03_Commande_MC_120A} == true",
        new ScadaExprBinary(ScadaExprBinaryOp.Equal,
            new ScadaExprTagRef("Noeud1_N15_03_Commande_MC_120A"), // legacy DisplayName
            new ScadaExprLiteralBool(true)),
        new[] { "Noeud1_N15_03_Commande_MC_120A" });

    var redExpr = new ScadaExpression(
        "{Noeud1_N15_04_Commande_MC_120C} == false",
        new ScadaExprBinary(ScadaExprBinaryOp.Equal,
            new ScadaExprTagRef("Noeud1_N15_04_Commande_MC_120C"),
            new ScadaExprLiteralBool(false)),
        new[] { "Noeud1_N15_04_Commande_MC_120C" });

    var scene = ScadaScene.CreateEmpty("win00008", "SB2 Regression", new(1280, 873))
        .WithElement(new ScadaElement(
            "shape_001", "Rectangle001", ScadaElementKind.Shape,
            new SceneBounds(65, 697, 175, 68), null, ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData(null, null, null, null, null, null, null, null, null, false),
            ShapeKind: ScadaShapeKind.Rectangle,
            StateConfig: new ScadaElementStateConfig(
                ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
                ScadaEffectBlock.Empty,
                new[] {
                    new ScadaStateRule("s1", "Running", true, greenExpr,
                        new ScadaEffectBlock(ColorFilterColor: "#12B729")),
                    new ScadaStateRule("s2", "arret", true,
                        new ScadaExpression(
                            "{Noeud1_N15_03_Commande_MC_120A} == false",
                            new ScadaExprBinary(ScadaExprBinaryOp.Equal,
                                new ScadaExprTagRef("Noeud1_N15_03_Commande_MC_120A"),
                                new ScadaExprLiteralBool(false)),
                            new[] { "Noeud1_N15_03_Commande_MC_120A" }),
                        new ScadaEffectBlock(ColorFilterColor: "#E53935"))
                })))
        .WithElement(new ScadaElement(
            "el_red_only", "Rectangle001", ScadaElementKind.Shape,
            new SceneBounds(267, 698, 175, 68), null, ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText,
            new ScadaElementData(null, null, null, null, null, null, null, null, null, false),
            ShapeKind: ScadaShapeKind.Rectangle,
            StateConfig: new ScadaElementStateConfig(
                ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
                ScadaEffectBlock.Empty,
                new[] { new ScadaStateRule("s1", "Arret", true, redExpr,
                    new ScadaEffectBlock(ColorFilterColor: "#E53935")) })));

    // Catalogue : les deux tags existent
    var catalog = new ScadaTagCatalog("v1", new[]
    {
        new ScadaTagDefinition("tf100.mapping.195", "Noeud1_N15_03_Commande_MC_120A",
            KeywordLabel: "MC_120A", Datatype: "bool"),
        new ScadaTagDefinition("tf100.mapping.196", "Noeud1_N15_04_Commande_MC_120C",
            KeywordLabel: "MC_120C", Datatype: "bool"),
    });
    var project = CreateProjectWithCatalog(catalog, scene);

    try
    {
        var archivePath = Path.Combine(root, "export", "regression.sb2");
        var archiveResult = await new Ft100SceneExporter().ExportProjectArchiveAsync(
            project,
            new[] { new Ft100ProjectPageExportInput(scene, sourceHtmlPath) },
            archivePath);

        Assert.IsTrue(File.Exists(archiveResult.ArchivePath), "The .sb2 archive must be created.");

        using var archive = ZipFile.OpenRead(archiveResult.ArchivePath);
        var manifestEntry = archive.GetEntry("scada-builder-v2-ft100-package/manifest.json");
        var htmlEntry = archive.GetEntry("scada-builder-v2-ft100-package/win00008/win00008.html");
        Assert.IsNotNull(manifestEntry, "Root manifest must exist in the .sb2 archive.");
        Assert.IsNotNull(htmlEntry, "Page HTML must exist in the .sb2 archive.");

        static string ReadZipEntry(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        // 1. Manifest : doit contenir tagName canonique, pas DisplayName
        var manifest = ReadZipEntry(manifestEntry);
        StringAssert.Contains(manifest, "\"tagName\":\"tf100.mapping.195\"");
        StringAssert.Contains(manifest, "\"tagName\":\"tf100.mapping.196\"");
        Assert.IsFalse(manifest.Contains("\"tagName\":\"Noeud1_N15_03_Commande_MC_120A\""),
            "Manifest must not contain legacy DisplayName as tagName.");
        Assert.IsFalse(manifest.Contains("\"tagName\":\"Noeud1_N15_04_Commande_MC_120C\""),
            "Manifest must not contain legacy DisplayName as tagName.");

        // 2. HTML : data-scada-state-config contient les Ids canoniques
        var html = ReadZipEntry(htmlEntry);
        var decoded = html.Replace("&quot;", "\"");
        StringAssert.Contains(decoded, "\"tagName\":\"tf100.mapping.195\"");
        StringAssert.Contains(decoded, "\"tagName\":\"tf100.mapping.196\"");

        // 3. CohÃ©rence manifest/HTML : le mÃªme nombre d'Ã©lÃ©ments avec StateConfig
        var manifestStateConfigCount = CountOccurrences(manifest, "\"StateConfig\":");
        // Chaque Ã©lÃ©ment dans le HTML a un data-scada-state-config
        var htmlStateConfigCount = CountOccurrences(decoded, "data-scada-state-config=\"");
        Assert.AreEqual(manifestStateConfigCount, htmlStateConfigCount,
            "Manifest and HTML must have the same number of StateConfig entries.");

        // 4. Package valide, aucun warning bloquant attendu
        Assert.IsTrue(archiveResult.Validation.IsValid);
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

private static int CountOccurrences(string text, string pattern)
{
    int count = 0, i = 0;
    while ((i = text.IndexOf(pattern, i, StringComparison.Ordinal)) != -1)
    {
        count++;
        i += pattern.Length;
    }
    return count;
}
```

- [ ] **VÃ©rifier le casing AST exportÃ©**

```csharp
// Ajouter dans un test d'export (Ft100SceneExporterTests.cs) :

[TestMethod]
public async Task ExportAsync_ExportedAst_UsesLowercaseOpValues()
{
    var root = CreateTempExportDir();
    var sourceHtmlPath = Path.Combine(root, "source", "casing_test.html");
    await File.WriteAllTextAsync(sourceHtmlPath,
        "<!doctype html><html><body><div class=\"page\"></div></body></html>");

    var expr = new ScadaExpression(
        "{tf100.mapping.196} == true",
        new ScadaExprBinary(ScadaExprBinaryOp.Equal,
            new ScadaExprTagRef("tf100.mapping.196", "tf100.mapping.196"),
            new ScadaExprLiteralBool(true)),
        new[] { "tf100.mapping.196" });

    var element = new ScadaElement(
        "el_casing", "Casing", ScadaElementKind.Text,
        new SceneBounds(10, 20, 100, 30), null, ScadaElementLayout.Absolute,
        ScadaElementStyle.DefaultText,
        new ScadaElementData("test", null, null, null, null, null, null, null, null, false),
        StateConfig: new ScadaElementStateConfig(
            ScadaEffectBlock.Empty with { Opacity = 0.4, BorderColor = "#000000", BorderWidth = 2 },
            ScadaEffectBlock.Empty,
            new[] { new ScadaStateRule("s1", "R", true, expr,
                new ScadaEffectBlock()) }));

    var scene = ScadaScene.CreateEmpty("win00008", "Casing", new(1280, 873)).WithElement(element);

    try
    {
        var result = await new Ft100SceneExporter().ExportAsync(
            scene, sourceHtmlPath, Path.Combine(root, "export"));
        var html = await File.ReadAllTextAsync(result.HtmlPath);
        var decoded = html.Replace("&quot;", "\"");

        // VÃ©rifier le casing camelCase que le JS consomme rÃ©ellement
        StringAssert.Contains(decoded, "\"op\":\"equal\"",
            "AST must use camelCase enum values (e.g. 'equal', not 'Equal').");
        StringAssert.Contains(decoded, "\"type\":\"tagRef\"");
        StringAssert.Contains(decoded, "\"type\":\"literalBool\"");
        StringAssert.Contains(decoded, "\"tagName\":\"tf100.mapping.196\"");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
```

- [ ] **Commit final**

```bash
git add docs/superpowers/specs/2026-07-09-expression-tag-id-reference.md
git add docs/superpowers/plans/2026-07-09-expression-tag-id-reference.md
git commit -m "docs: finalize expression tag Id reference spec and plan"
```
