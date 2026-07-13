# Element+ — Capacités de style avancé — Plan d'implémentation

Date: 2026-07-13
Status: In progress — implementation underway; TF100Web integration test awaiting database-enabled validation
Document version: `V2.1.3.0010`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-13 | `V2.1.4.0003` | `b954d46` | Exécution approuvée : modèle, export, preview WebView/WPF, surfaces Style, icônes et tests ciblés implémentés ; preuve TF100Web bloquée par MySQL indisponible. |
| 2026-07-13 | `V2.1.3.0010` | `PENDING` | Correction du plan : aperçu vivant obligatoire, preuve d’intake TF100Web ajoutée et gate inter-dépôts explicite. |

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ajouter 8 propriétés de style model-backed (FontWeight, FontStyle, TextDecoration, TextAlign, TextTransform, LetterSpacing, LineHeight, BorderRadius), rendre Foreground authorable, étendre les styles de bordure à 9 valeurs, et refondre l'onglet Style en sections avec icônes sémantiques — avec parité sur les 4 surfaces (dialogue modal, panneau docké, preview WebView, export FT100).

**Architecture:** Extension du record `ScadaElementStyle` avec champs optionnels rétrocompatibles → sérialisation JSON transparente → consommation par WebView et FT100 via un mapping CSS identique → UI WPF en sections avec `ToggleButton` à état et `ColorPickerField`. Correction du bug `AppendElementCss` qui omet `opacity`/`transform`.

**Tech Stack:** C# 12 / .NET 8, WPF, WebView2, MSTest, System.Text.Json

**Spec source:** `docs/superpowers/specs/2026-07-13-element-plus-style-capability-design.md` (D1–D20, §5.7, §6.1, §12)

## Global Constraints

- Contrat : `docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md` — règles 15, 16 existantes ; nouvelles règles pour typographie, bordure avancée et rayon
- UI : `docs/06_ui_ux/UI_SPECIFICATION_V2.md`
- Icônes : `docs/06_ui_ux/ICON_STRATEGY_V2.md` — famille `Icon.Property.*` à créer
- Les deux surfaces UI doivent rester synchronisées (mêmes contrôles, mêmes validations)
- Les overlays et aperçus éditeur ne doivent jamais être exportés
- Aucun changement du runtime TF100Web/Django n’est prévu ; le test d’intake requis est une modification de test uniquement dans le dépôt TF100Web et doit franchir le gate inter-dépôts ci-dessous.
- `AdvancedCss` reste le dernier override utilisateur (D14)
- Anciens projets sans nouveaux champs → rendu identique (D2)
- Undo/redo, sauvegarde/recharge, preview et export pour chaque mutation (D18)
- Q1 : l’aperçu vivant WPF est obligatoire dans cette itération et doit être implémenté avant la validation finale (D17, §12).

## Pré-vérifications

Avant toute modification, capturer l'état de référence :

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaSceneModelsTests|FullyQualifiedName~WebViewContextMenuScriptTests|FullyQualifiedName~Ft100SceneExporterTests|FullyQualifiedName~EditorHistoryServiceTests|FullyQualifiedName~ModernProjectStoreTests"
```

Avant toute modification du dépôt externe, capturer également :

```powershell
Set-Location "F:\Projet\Git\TF100Web"
git status --short --branch
```

Le dépôt TF100Web doit rester sur sa branche de travail courante et ses changements préexistants doivent être préservés.

---

### Task 1: Étendre le modèle de style Domain

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs:198-234`
- Test: `tests/ScadaBuilderV2.Tests/ScadaSceneModelsTests.cs`

**Interfaces:**
- Consumes: nothing (first task)
- Produces:
  - `ScadaElementStyle` — 8 nouveaux champs avec défauts (FontWeight="Normal", FontStyle="Normal", TextDecoration=null, TextAlign="Left", TextTransform="None", LetterSpacing=0, LineHeight=0, BorderRadius=null)
  - `ScadaBorderRadius(double TopLeft=0, double TopRight=0, double BottomRight=0, double BottomLeft=0)` — record avec `[JsonIgnore] bool IsUniform` et `ScadaBorderRadius Normalized()`

- [ ] **Step 1: Ajouter `ScadaBorderRadius` et les tests de défaut**

Ajouter le record après `ScadaElementStyle` (avant `ScadaElementData`, vers ligne 235) :

```csharp
/// <summary>
/// CSS border-radius values for an Element+ object, one value per corner in pixels.
/// </summary>
/// <remarks>
/// Decisions: D13.
/// Contracts: docs/superpowers/specs/2026-07-13-element-plus-style-capability-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/ScadaSceneModelsTests.cs.
/// </remarks>
public sealed record ScadaBorderRadius(
    double TopLeft = 0,
    double TopRight = 0,
    double BottomRight = 0,
    double BottomLeft = 0)
{
    /// <summary>
    /// True when all four corners share the same pixel value (uniform mode).
    /// </summary>
    [JsonIgnore]
    public bool IsUniform =>
        Math.Abs(TopLeft - TopRight) < 0.01 &&
        Math.Abs(TopRight - BottomRight) < 0.01 &&
        Math.Abs(BottomRight - BottomLeft) < 0.01;

    /// <summary>
    /// Returns a copy with negative values clamped to zero.
    /// </summary>
    public ScadaBorderRadius Normalized() => new(
        Math.Max(0, TopLeft),
        Math.Max(0, TopRight),
        Math.Max(0, BottomRight),
        Math.Max(0, BottomLeft));

    /// <summary>
    /// Zero-radius instance used as the effective default.
    /// </summary>
    public static ScadaBorderRadius None { get; } = new();
}
```

Ajouter les tests dans `ScadaSceneModelsTests.cs` :

```csharp
[TestMethod]
public void ScadaBorderRadius_Defaults_AllZero()
{
    var r = new ScadaBorderRadius();
    Assert.AreEqual(0, r.TopLeft);
    Assert.AreEqual(0, r.TopRight);
    Assert.AreEqual(0, r.BottomRight);
    Assert.AreEqual(0, r.BottomLeft);
    Assert.IsTrue(r.IsUniform);
}

[TestMethod]
public void ScadaBorderRadius_IsUniform_DetectsUniform()
{
    Assert.IsTrue(new ScadaBorderRadius(8, 8, 8, 8).IsUniform);
    Assert.IsFalse(new ScadaBorderRadius(8, 0, 8, 8).IsUniform);
    Assert.IsFalse(new ScadaBorderRadius(8, 8, 2, 2).IsUniform);
}

[TestMethod]
public void ScadaBorderRadius_Normalized_ClampsNegatives()
{
    var r = new ScadaBorderRadius(-5, 10, -2, 3).Normalized();
    Assert.AreEqual(0, r.TopLeft);
    Assert.AreEqual(10, r.TopRight);
    Assert.AreEqual(0, r.BottomRight);
    Assert.AreEqual(3, r.BottomLeft);
}

[TestMethod]
public void ScadaBorderRadius_Normalized_PreservesPositives()
{
    var r = new ScadaBorderRadius(4, 6, 8, 12).Normalized();
    Assert.AreEqual(4, r.TopLeft);
    Assert.AreEqual(6, r.TopRight);
    Assert.AreEqual(8, r.BottomRight);
    Assert.AreEqual(12, r.BottomLeft);
}

[TestMethod]
public void ScadaBorderRadius_None_IsZeroUniform()
{
    Assert.AreEqual(0, ScadaBorderRadius.None.TopLeft);
    Assert.IsTrue(ScadaBorderRadius.None.IsUniform);
}
```

- [ ] **Step 2: Exécuter les tests pour vérifier l'échec (record non défini)**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaBorderRadius"
```

Attendu : échec de compilation car `ScadaBorderRadius` n'existe pas encore.

- [ ] **Step 3: Ajouter `ScadaBorderRadius` dans ScadaSceneModels.cs**

Insérer le code du record (Step 1) dans `ScadaSceneModels.cs` après la ligne 234 (après `ScadaElementStyle`).

- [ ] **Step 4: Vérifier que les tests passent**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaBorderRadius"
```

Attendu : 5 tests passent.

- [ ] **Step 5: Étendre `ScadaElementStyle` avec les nouveaux champs**

Modifier le record `ScadaElementStyle` (lignes 198-211) :

```csharp
public sealed record ScadaElementStyle(
    string FontFamily,
    double FontSize,
    string Foreground,
    string Background,
    string BorderColor,
    double BorderWidth,
    string BorderStyle,
    string ShadowPreset,
    string? AdvancedCss,
    double Opacity = 1,
    double Rotation = 0,
    bool FlipHorizontally = false,
    bool FlipVertically = false,
    string FontWeight = "Normal",
    string FontStyle = "Normal",
    IReadOnlyList<string>? TextDecoration = null,
    string TextAlign = "Left",
    string TextTransform = "None",
    double LetterSpacing = 0,
    double LineHeight = 0,
    ScadaBorderRadius? BorderRadius = null)
```

- [ ] **Step 6: Ajouter les tests de défaut et sérialisation pour les nouveaux champs**

```csharp
[TestMethod]
public void ScadaElementStyle_NewFields_HaveBackwardCompatibleDefaults()
{
    var style = new ScadaElementStyle(
        "Segoe UI", 14, "#000", "#FFF", "#CCC", 1, "Solid", "None", null);

    Assert.AreEqual("Normal", style.FontWeight);
    Assert.AreEqual("Normal", style.FontStyle);
    Assert.IsNull(style.TextDecoration);
    Assert.AreEqual("Left", style.TextAlign);
    Assert.AreEqual("None", style.TextTransform);
    Assert.AreEqual(0, style.LetterSpacing);
    Assert.AreEqual(0, style.LineHeight);
    Assert.IsNull(style.BorderRadius);
}

[TestMethod]
public void ScadaElementStyle_ExistingConstructorStillCompiles()
{
    // The old 9-param constructor must still work with defaults
    var style = new ScadaElementStyle(
        "Arial", 12, "#111", "#222", "#333", 2, "Dashed", "Soft", ".custom{}");

    Assert.AreEqual("Arial", style.FontFamily);
    Assert.AreEqual(12, style.FontSize);
    Assert.AreEqual("Normal", style.FontWeight); // default
    Assert.AreEqual("Soft", style.ShadowPreset);
}

[TestMethod]
public void ScadaElementStyle_NewFields_RoundTripJson()
{
    var style = new ScadaElementStyle(
        "Segoe UI", 16, "#0F2A30", "Transparent", "Transparent", 0, "None", "None", null,
        FontWeight: "Bold", FontStyle: "Italic",
        TextDecoration: new[] { "Underline", "LineThrough" },
        TextAlign: "Center", TextTransform: "Uppercase",
        LetterSpacing: 1.5, LineHeight: 24,
        BorderRadius: new ScadaBorderRadius(8, 8, 2, 2));

    var json = JsonSerializer.Serialize(style, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    });
    var deserialized = JsonSerializer.Deserialize<ScadaElementStyle>(json, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    Assert.IsNotNull(deserialized);
    Assert.AreEqual("Bold", deserialized.FontWeight);
    Assert.AreEqual("Italic", deserialized.FontStyle);
    Assert.AreEqual(2, deserialized.TextDecoration!.Count);
    Assert.AreEqual("Underline", deserialized.TextDecoration[0]);
    Assert.AreEqual("LineThrough", deserialized.TextDecoration[1]);
    Assert.AreEqual("Center", deserialized.TextAlign);
    Assert.AreEqual("Uppercase", deserialized.TextTransform);
    Assert.AreEqual(1.5, deserialized.LetterSpacing);
    Assert.AreEqual(24, deserialized.LineHeight);
    Assert.IsNotNull(deserialized.BorderRadius);
    Assert.AreEqual(8, deserialized.BorderRadius.TopLeft);
    Assert.AreEqual(2, deserialized.BorderRadius.BottomLeft);
}

[TestMethod]
public void ScadaElementStyle_OldJson_DeserializesWithDefaults()
{
    var oldJson = """{"fontFamily":"Segoe UI","fontSize":16,"foreground":"#0F2A30","background":"Transparent","borderColor":"Transparent","borderWidth":0,"borderStyle":"None","shadowPreset":"None"}""";

    var style = JsonSerializer.Deserialize<ScadaElementStyle>(oldJson, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    Assert.IsNotNull(style);
    Assert.AreEqual("Segoe UI", style.FontFamily);
    Assert.AreEqual("Normal", style.FontWeight); // default from missing JSON
    Assert.AreEqual("Normal", style.FontStyle);
    Assert.IsNull(style.TextDecoration);
    Assert.AreEqual("Left", style.TextAlign);
    Assert.AreEqual(0, style.LineHeight);
    Assert.IsNull(style.BorderRadius);
}

[TestMethod]
public void ScadaElementStyle_LineHeight_ZeroMeansNormal()
{
    var style = new ScadaElementStyle(
        "Segoe UI", 14, "#000", "#FFF", "#CCC", 1, "Solid", "None", null,
        LineHeight: 0);
    Assert.AreEqual(0, style.LineHeight);
}

[TestMethod]
public void ScadaElementStyle_LineHeight_NegativeRejected()
{
    var style = new ScadaElementStyle(
        "Segoe UI", 14, "#000", "#FFF", "#CCC", 1, "Solid", "None", null,
        LineHeight: -5);
    // Le modèle accepte la valeur (record immuable), la validation est dans l'UI
    // Mais le rendu doit traiter les négatifs comme 0 (normal)
    Assert.AreEqual(-5, style.LineHeight);
}
```

- [ ] **Step 7: Vérifier que les tests modèles passent**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaSceneModelsTests"
```

Attendu : tous les tests passent, y compris les nouveaux.

- [ ] **Step 8: Vérifier que les tests existants passent encore (rétrocompatibilité)**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ModernProjectStoreTests"
```

Attendu : aucun test cassé par les nouveaux champs optionnels.

- [ ] **Step 9: Commit**

```bash
git add src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs tests/ScadaBuilderV2.Tests/ScadaSceneModelsTests.cs
git commit -m "feat: add FontWeight, FontStyle, TextDecoration, TextAlign, TextTransform, LetterSpacing, LineHeight, BorderRadius to ScadaElementStyle

Add ScadaBorderRadius record with IsUniform, Normalized(), and None static.
All new fields have backward-compatible defaults. Old JSON deserializes correctly.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: Corriger le bug AppendElementCss + ajouter les nouvelles propriétés CSS à l'exporteur

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs:1686-1760`
- Test: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

**Interfaces:**
- Consumes: `ScadaElementStyle` avec les nouveaux champs (Task 1)
- Produces: `AppendElementCss` émet `opacity`, `transform`, `font-weight`, `font-style`, `text-decoration`, `text-align`, `text-transform`, `letter-spacing`, `line-height`, `border-radius` + tous les styles de bordure étendus

- [ ] **Step 1: Ajouter les tests d'export pour les nouvelles propriétés**

```csharp
[TestMethod]
public async Task ExportAsync_EmitsFontWeightAndFontStyle()
{
    var scene = CreateSceneWithElement("text-1", ScadaElementKind.Text, new ScadaElementStyle(
        "Segoe UI", 16, "#000", "#FFF", "#CCC", 1, "Solid", "None", null,
        FontWeight: "Bold", FontStyle: "Italic"));
    var html = await ExportSceneHtmlAsync(scene);
    StringAssert.Contains(html, "font-weight: Bold");
    StringAssert.Contains(html, "font-style: Italic");
}

[TestMethod]
public async Task ExportAsync_EmitsTextDecoration()
{
    var scene = CreateSceneWithElement("text-1", ScadaElementKind.Text, new ScadaElementStyle(
        "Segoe UI", 16, "#000", "#FFF", "#CCC", 1, "Solid", "None", null,
        TextDecoration: new[] { "Underline", "LineThrough" }));
    var html = await ExportSceneHtmlAsync(scene);
    StringAssert.Contains(html, "text-decoration: underline line-through");
}

[TestMethod]
public async Task ExportAsync_OmitsTextDecorationWhenEmpty()
{
    var scene = CreateSceneWithElement("text-1", ScadaElementKind.Text, new ScadaElementStyle(
        "Segoe UI", 16, "#000", "#FFF", "#CCC", 1, "Solid", "None", null,
        TextDecoration: new string[0]));
    var html = await ExportSceneHtmlAsync(scene);
    // Must NOT contain "text-decoration: none" (leave CSS inheritance alone)
    Assert.IsFalse(html.Contains("text-decoration:"), "text-decoration should be omitted when empty");
}

[TestMethod]
public async Task ExportAsync_EmitsBorderRadius()
{
    var scene = CreateSceneWithElement("text-1", ScadaElementKind.Text, new ScadaElementStyle(
        "Segoe UI", 16, "#000", "#FFF", "#CCC", 1, "Solid", "None", null,
        BorderRadius: new ScadaBorderRadius(4, 8, 12, 16)));
    var html = await ExportSceneHtmlAsync(scene);
    StringAssert.Contains(html, "border-radius: 4px 8px 12px 16px");
}

[TestMethod]
public async Task ExportAsync_BorderRadiusUniformEmitsFourIdentical()
{
    var scene = CreateSceneWithElement("text-1", ScadaElementKind.Text, new ScadaElementStyle(
        "Segoe UI", 16, "#000", "#FFF", "#CCC", 1, "Solid", "None", null,
        BorderRadius: new ScadaBorderRadius(8, 8, 8, 8)));
    var html = await ExportSceneHtmlAsync(scene);
    StringAssert.Contains(html, "border-radius: 8px 8px 8px 8px");
}

[TestMethod]
public async Task ExportAsync_EmitsAllBorderStyles()
{
    foreach (var style in new[] { "Solid", "Dashed", "Dotted", "Double", "Groove", "Ridge", "Inset", "Outset", "None" })
    {
        var scene = CreateSceneWithElement("text-1", ScadaElementKind.Text, new ScadaElementStyle(
            "Segoe UI", 16, "#000", "#FFF", "#CCC", 1, style, "None", null));
        var html = await ExportSceneHtmlAsync(scene);
        var expectedBorderStyle = style == "None" ? "none" : style.ToLowerInvariant();
        StringAssert.Contains(html, expectedBorderStyle, $"Border style '{style}' not found in export");
    }
}

[TestMethod]
public async Task ExportAsync_EmitsTextAlignAndTransform()
{
    var scene = CreateSceneWithElement("text-1", ScadaElementKind.Text, new ScadaElementStyle(
        "Segoe UI", 16, "#000", "#FFF", "#CCC", 1, "Solid", "None", null,
        TextAlign: "Center", TextTransform: "Uppercase"));
    var html = await ExportSceneHtmlAsync(scene);
    StringAssert.Contains(html, "text-align: Center");
    StringAssert.Contains(html, "text-transform: Uppercase");
}

[TestMethod]
public async Task ExportAsync_EmitsLetterSpacingAndLineHeight()
{
    var scene = CreateSceneWithElement("text-1", ScadaElementKind.Text, new ScadaElementStyle(
        "Segoe UI", 16, "#000", "#FFF", "#CCC", 1, "Solid", "None", null,
        LetterSpacing: 2.5, LineHeight: 28));
    var html = await ExportSceneHtmlAsync(scene);
    StringAssert.Contains(html, "letter-spacing: 2.5px");
    StringAssert.Contains(html, "line-height: 28px");
}

[TestMethod]
public async Task ExportAsync_LineHeightZeroEmitsNormal()
{
    var scene = CreateSceneWithElement("text-1", ScadaElementKind.Text, new ScadaElementStyle(
        "Segoe UI", 16, "#000", "#FFF", "#CCC", 1, "Solid", "None", null,
        LineHeight: 0));
    var html = await ExportSceneHtmlAsync(scene);
    StringAssert.Contains(html, "line-height: normal");
}

[TestMethod]
public async Task ExportAsync_AppendElementCssEmitsOpacityAndTransform()
{
    // Bug fix: AppendElementCss was missing opacity and transform
    var scene = CreateSceneWithElement("text-1", ScadaElementKind.Text, new ScadaElementStyle(
        "Segoe UI", 16, "#000", "#FFF", "#CCC", 1, "Solid", "None", null,
        Opacity: 0.5, Rotation: 45, FlipHorizontally: true));
    var html = await ExportSceneHtmlAsync(scene);
    StringAssert.Contains(html, "opacity: 0.5");
    StringAssert.Contains(html, "rotate(45deg)");
    StringAssert.Contains(html, "scaleX(-1)");
}

[TestMethod]
public async Task ExportAsync_AdvancedCssRemainsLastOverride()
{
    var scene = CreateSceneWithElement("text-1", ScadaElementKind.Text, new ScadaElementStyle(
        "Segoe UI", 16, "#000", "#FFF", "#CCC", 1, "Solid", "None", ".override{color:red}",
        FontWeight: "Bold", BorderRadius: new ScadaBorderRadius(4)));
    var html = await ExportSceneHtmlAsync(scene);
    var fontWeightIndex = html.IndexOf("font-weight: Bold", StringComparison.Ordinal);
    var overrideIndex = html.IndexOf(".override{color:red}", StringComparison.Ordinal);
    Assert.IsTrue(fontWeightIndex < overrideIndex,
        "AdvancedCss must appear after structured font-weight");
}
```

Helper method à ajouter dans la classe de test :

```csharp
private static ScadaScene CreateSceneWithElement(string id, ScadaElementKind kind, ScadaElementStyle style)
{
    return new ScadaScene(
        "page-1", "Test Page", new CanvasSize(1920, 1080),
        new[] { new ScadaElement(id, "Element", kind, new SceneBounds(10, 10, 200, 40),
            null, ScadaElementLayout.Absolute, style) },
        "#000000");
}

private static async Task<string> ExportSceneHtmlAsync(ScadaScene scene)
{
    var exporter = new Ft100SceneExporter();
    var stream = new MemoryStream();
    await exporter.ExportAsync(scene, "test-project", stream);
    stream.Position = 0;
    using var reader = new StreamReader(stream);
    return await reader.ReadToEndAsync();
}
```

- [ ] **Step 2: Vérifier que les tests échouent**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ExportAsync_EmitsFontWeight|ExportAsync_EmitsTextDecoration|ExportAsync_EmitsBorderRadius|ExportAsync_EmitsAllBorderStyles|ExportAsync_EmitsTextAlign|ExportAsync_EmitsLetterSpacing|ExportAsync_LineHeightZero|ExportAsync_AppendElementCssEmitsOpacity|ExportAsync_AdvancedCssRemainsLast"
```

Attendu : échec, propriétés non émises.

- [ ] **Step 3: Mettre à jour `AppendElementCss` pour émettre toutes les propriétés**

Remplacer le contenu de `AppendElementCss` (lignes 1734-1753) :

```csharp
var style = element.Style ?? ScadaElementStyle.DefaultText;
css.AppendLine();
css.AppendLine($"{scope.ElementSelector(element.Id)} {{");
css.AppendLine($"  left: {Format(absoluteX)}px;");
css.AppendLine($"  top: {Format(absoluteY)}px;");
css.AppendLine($"  width: {Format(element.Bounds.Width)}px;");
css.AppendLine($"  height: {Format(element.Bounds.Height)}px;");
css.AppendLine($"  color: {style.Foreground};");
css.AppendLine($"  background: {(element.Kind == ScadaElementKind.Shape ? "transparent" : style.Background)};");
css.AppendLine($"  font-family: {style.FontFamily};");
css.AppendLine($"  font-size: {Format(style.FontSize)}px;");
css.AppendLine($"  font-weight: {style.FontWeight};");
css.AppendLine($"  font-style: {style.FontStyle};");
AppendOptionalTextDecoration(css, style.TextDecoration);
AppendOptionalCss(css, "text-align", style.TextAlign, "Left");
AppendOptionalCss(css, "text-transform", style.TextTransform, "None");
if (Math.Abs(style.LetterSpacing) > 0.01)
    css.AppendLine($"  letter-spacing: {Format(style.LetterSpacing)}px;");
css.AppendLine(style.LineHeight <= 0
    ? "  line-height: normal;"
    : $"  line-height: {Format(style.LineHeight)}px;");
css.AppendLine(element.Kind == ScadaElementKind.Shape
    ? "  border: 0 none transparent;"
    : $"  border: {Format(style.BorderWidth)}px {NormalizeBorderStyle(style.BorderStyle)} {style.BorderColor};");
AppendOptionalBorderRadius(css, style.BorderRadius);
css.AppendLine($"  box-shadow: {ShadowCss(style.ShadowPreset)};");
css.AppendLine($"  opacity: {Format(Math.Clamp(style.Opacity, 0, 1))};");
css.AppendLine("  transform-origin: center center;");
var scaleX = style.FlipHorizontally ? -1 : 1;
var scaleY = style.FlipVertically ? -1 : 1;
css.AppendLine($"  transform: rotate({Format(style.Rotation)}deg) scaleX({scaleX}) scaleY({scaleY});");
if (!string.IsNullOrWhiteSpace(style.AdvancedCss))
{
    css.AppendLine($"  {style.AdvancedCss}");
}
css.AppendLine("}");
```

- [ ] **Step 4: Ajouter les méthodes helper dans `Ft100SceneExporter`**

Après `ShadowCss` (vers ligne 2096) :

```csharp
private static void AppendOptionalCss(StringBuilder css, string property, string value, string defaultWhenOmitted)
{
    if (!string.Equals(value, defaultWhenOmitted, StringComparison.OrdinalIgnoreCase))
    {
        css.AppendLine($"  {property}: {value};");
    }
}

private static void AppendOptionalTextDecoration(StringBuilder css, IReadOnlyList<string>? decorations)
{
    if (decorations is null || decorations.Count == 0)
        return;
    var cssValue = string.Join(" ", decorations.Select(d => d.ToLowerInvariant()));
    css.AppendLine($"  text-decoration: {cssValue};");
}

private static void AppendOptionalBorderRadius(StringBuilder css, ScadaBorderRadius? radius)
{
    if (radius is null || (radius.IsUniform && Math.Abs(radius.TopLeft) < 0.01))
        return;
    css.AppendLine($"  border-radius: {Format(radius.TopLeft)}px {Format(radius.TopRight)}px {Format(radius.BottomRight)}px {Format(radius.BottomLeft)}px;");
}
```

- [ ] **Step 5: Vérifier que les tests d'export passent**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~Ft100SceneExporterTests"
```

Attendu : tous les tests passent, y compris les 12 nouveaux.

- [ ] **Step 6: Vérifier que `BuildElementInlineStyle` émet aussi les nouvelles propriétés**

Ajouter le même bloc de propriétés dans `BuildElementInlineStyle` (lignes 1586-1617). Après `font-size`, ajouter :

```csharp
css.Append($"font-weight:{style.FontWeight};");
css.Append($"font-style:{style.FontStyle};");
if (style.TextDecoration is { Count: > 0 })
    css.Append($"text-decoration:{string.Join(" ", style.TextDecoration.Select(d => d.ToLowerInvariant()))};");
AppendOptionalCss(css, "text-align", style.TextAlign, "Left");
AppendOptionalCss(css, "text-transform", style.TextTransform, "None");
if (Math.Abs(style.LetterSpacing) > 0.01)
    css.Append($"letter-spacing:{Format(style.LetterSpacing)}px;");
css.Append(style.LineHeight <= 0 ? "line-height:normal;" : $"line-height:{Format(style.LineHeight)}px;");
```

Et après `border`, ajouter :

```csharp
if (style.BorderRadius is not null && !(style.BorderRadius.IsUniform && Math.Abs(style.BorderRadius.TopLeft) < 0.01))
    css.Append($"border-radius:{Format(style.BorderRadius.TopLeft)}px {Format(style.BorderRadius.TopRight)}px {Format(style.BorderRadius.BottomRight)}px {Format(style.BorderRadius.BottomLeft)}px;");
```

Note : `AppendOptionalCss` doit être utilisable ici. Si c'est une méthode statique privée, elle est accessible dans la même classe. Si le `StringBuilder` est passé différemment dans `BuildElementInlineStyle`, adapter — cette méthode utilise `css.Append()` directement, pas `css.AppendLine()`.

- [ ] **Step 7: Vérifier que tous les tests d'export passent après la mise à jour inline**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~Ft100SceneExporterTests"
```

Attendu : tous passent.

- [ ] **Step 8: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "feat: emit new style properties in FT100 exporter, fix AppendElementCss opacity/transform

Add font-weight, font-style, text-decoration, text-align, text-transform,
letter-spacing, line-height, border-radius to both AppendElementCss and
BuildElementInlineStyle. Support all 9 border styles.

Fix: AppendElementCss now emits opacity, transform-origin, transform
(rotate + scaleX/Y) matching BuildElementInlineStyle.

Helper methods: AppendOptionalCss, AppendOptionalTextDecoration,
AppendOptionalBorderRadius.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: Mettre à jour le script WebView pour les nouvelles propriétés

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:1884-1897`
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `ScadaElementStyle` avec les nouveaux champs (Task 1)
- Produces: `renderModernElements` émet `fontWeight`, `fontStyle`, `textDecoration`, `textAlign`, `textTransform`, `letterSpacing`, `lineHeight`, `borderRadius` sur les wrappers HTML

- [ ] **Step 1: Ajouter les tests de contrat WebView pour les nouvelles propriétés**

```csharp
[TestMethod]
public void WebViewScript_ContainsFontWeightApplication()
{
    var script = File.ReadAllText(GetWebViewScriptPath());
    StringAssert.Contains(script, "fontWeight", "WebView script must apply fontWeight from style");
    StringAssert.Contains(script, "FontWeight", "WebView script must read style.FontWeight");
}

[TestMethod]
public void WebViewScript_ContainsFontStyleApplication()
{
    var script = File.ReadAllText(GetWebViewScriptPath());
    StringAssert.Contains(script, "fontStyle", "WebView script must apply fontStyle from style");
}

[TestMethod]
public void WebViewScript_ContainsTextDecorationApplication()
{
    var script = File.ReadAllText(GetWebViewScriptPath());
    StringAssert.Contains(script, "TextDecoration", "WebView script must read style.TextDecoration");
    StringAssert.Contains(script, "textDecoration", "WebView script must apply text-decoration CSS");
}

[TestMethod]
public void WebViewScript_ContainsBorderRadiusApplication()
{
    var script = File.ReadAllText(GetWebViewScriptPath());
    StringAssert.Contains(script, "BorderRadius", "WebView script must read style.BorderRadius");
    StringAssert.Contains(script, "borderRadius", "WebView script must apply border-radius CSS");
}

[TestMethod]
public void WebViewScript_ContainsLineHeightApplication()
{
    var script = File.ReadAllText(GetWebViewScriptPath());
    StringAssert.Contains(script, "LineHeight", "WebView script must read style.LineHeight");
}

[TestMethod]
public void WebViewScript_ContainsLetterSpacingApplication()
{
    var script = File.ReadAllText(GetWebViewScriptPath());
    StringAssert.Contains(script, "LetterSpacing", "WebView script must read style.LetterSpacing");
}
```

- [ ] **Step 2: Vérifier que les tests échouent**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~WebViewScript_ContainsFontWeight|WebViewScript_ContainsFontStyle|WebViewScript_ContainsTextDecoration|WebViewScript_ContainsBorderRadius|WebViewScript_ContainsLineHeight|WebViewScript_ContainsLetterSpacing"
```

Attendu : échec.

- [ ] **Step 3: Ajouter les nouvelles propriétés dans `renderModernElements`**

Dans `MainWindow.WebViewScript.cs`, après la ligne 1885 (`wrapper.style.fontSize = ...`), insérer :

```javascript
wrapper.style.fontWeight = cssText(style.FontWeight, 'Normal');
wrapper.style.fontStyle = cssText(style.FontStyle, 'Normal').toLowerCase();
wrapper.style.textAlign = cssText(style.TextAlign, 'Left').toLowerCase();
wrapper.style.textTransform = cssText(style.TextTransform, 'None').toLowerCase();
if (Number(style.LetterSpacing) !== 0) {
  wrapper.style.letterSpacing = `${cssText(style.LetterSpacing, 0)}px`;
}
const lineHeight = Number(style.LineHeight ?? 0);
wrapper.style.lineHeight = lineHeight <= 0 ? 'normal' : `${lineHeight}px`;
if (style.TextDecoration && style.TextDecoration.length > 0) {
  wrapper.style.textDecoration = style.TextDecoration.map(function(d) { return d.toLowerCase(); }).join(' ');
}
```

Après la ligne 1890 (`wrapper.style.borderColor = ...`), insérer :

```javascript
if (style.BorderRadius) {
  var r = style.BorderRadius;
  wrapper.style.borderRadius = `${cssText(r.TopLeft, 0)}px ${cssText(r.TopRight, 0)}px ${cssText(r.BottomRight, 0)}px ${cssText(r.BottomLeft, 0)}px`;
}
```

- [ ] **Step 4: Vérifier que les tests de contrat passent**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~WebViewScript_Contains"
```

Attendu : tous passent.

- [ ] **Step 5: Vérifier qu'aucun test existant n'est cassé**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~WebViewContextMenuScriptTests"
```

Attendu : tous les tests passent.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: apply new style properties in WebView preview

Emit fontWeight, fontStyle, textDecoration, textAlign, textTransform,
letterSpacing, lineHeight, and borderRadius from ScadaElementStyle
on Element+ wrappers. TextDecoration is joined from array, LineHeight 0
maps to 'normal', LetterSpacing omitted when 0.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 4: Ajouter les icônes `Icon.Property.*` dans Icons.xaml

**Files:**
- Modify: `src/ScadaBuilderV2.App/Resources/Icons.xaml`
- Test: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` (extension)

**Interfaces:**
- Consumes: rien (icônes autonomes)
- Produces: 13 clés `Icon.Property.*` résolubles comme `DrawingImage` dans `Icons.xaml`

- [ ] **Step 1: Ajouter les tests de résolution d'icônes**

```csharp
[TestMethod]
public void IconsXaml_PropertyIcons_AllKeysResolve()
{
    var requiredKeys = new[]
    {
        "Icon.Property.Typography",
        "Icon.Property.FontFamily",
        "Icon.Property.FontWeight",
        "Icon.Property.FontStyle",
        "Icon.Property.TextDecoration",
        "Icon.Property.TextAlign",
        "Icon.Property.Colors",
        "Icon.Property.Border",
        "Icon.Property.BorderRadius",
        "Icon.Property.Shadow",
        "Icon.Property.Transform",
        "Icon.Property.AdvancedCss",
        "Icon.Property.Reset"
    };

    var iconsPath = Path.Combine(GetProjectRoot(), "src", "ScadaBuilderV2.App", "Resources", "Icons.xaml");
    var xaml = File.ReadAllText(iconsPath);

    foreach (var key in requiredKeys)
    {
        Assert.IsTrue(xaml.Contains($"x:Key=\"{key}\""),
            $"Icon key '{key}' must exist in Icons.xaml");
    }
}
```

- [ ] **Step 2: Vérifier que les tests échouent**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~IconsXaml_PropertyIcons"
```

Attendu : échec, clés absentes.

- [ ] **Step 3: Ajouter les 13 icônes dans `Icons.xaml`**

Insérer après la dernière icône existante (après `Icon.Button.EmergencyStop`, vers la ligne 573) :

```xml
    <!-- ============================================================
         Icon.Property.* — Property inspector Style tab section icons.
         All use the same Icon.OutlinePen and Icon.StrokeBrush.
         Target size: 16-20px readable.
    ============================================================ -->

    <DrawingImage x:Key="Icon.Property.Typography">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M5,4 L9,4 L12,7 L15,4 L19,4 L19,12 L5,12 Z"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M8,8 L16,8 M8,10 L14,10"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M5,15 L19,15 L19,20 L5,20 Z M5,15 L7,12 M19,15 L17,12"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>

    <DrawingImage x:Key="Icon.Property.FontFamily">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M4,5 L12,5 L12,19 L4,19 Z M16,5 L20,5 L20,19 L16,19 Z"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M8,9 L8,15 M6,12 L10,12"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M18,9 L16,15 M18,9 L20,15"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>

    <DrawingImage x:Key="Icon.Property.FontWeight">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M5,4 L7,4 L12,20 L10,20 Z M8,12 L15,12"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M15,4 L17,4 L18,20 L16,20 Z"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>

    <DrawingImage x:Key="Icon.Property.FontStyle">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M6,4 L8,4 L14,20 L12,20 Z"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M15,4 L19,4 L13,20 L11,20 Z"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>

    <DrawingImage x:Key="Icon.Property.TextDecoration">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M4,6 L20,6"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M4,12 L20,12"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M4,18 L20,18"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M4,4 L8,4 L10,6 L6,6 Z"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M8,12 L20,12 M14,10 L17,12 L14,14"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>

    <DrawingImage x:Key="Icon.Property.TextAlign">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M4,6 L20,6"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M6,9 L18,9"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M4,12 L20,12"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M8,15 L16,15"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M4,18 L20,18"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>

    <DrawingImage x:Key="Icon.Property.Colors">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M12,2 C6.477,2 2,6.477 2,12 C2,15.5 4.5,18.5 8,20.5 L12,13 L16,20.5 C19.5,18.5 22,15.5 22,12 C22,6.477 17.523,2 12,2 Z"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M8,20.5 C5.5,19 4,16 4,12 L12,13 Z"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M16,20.5 L12,13 L20,12 C20,16 18.5,19 16,20.5 Z"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>

    <DrawingImage x:Key="Icon.Property.Border">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M4,4 L20,4 L20,20 L4,20 Z"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M8,8 L16,8 L16,16 L8,16 Z"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>

    <DrawingImage x:Key="Icon.Property.BorderRadius">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M7,4 L17,4 C18.657,4 20,5.343 20,7 L20,17 C20,18.657 18.657,20 17,20 L7,20 C5.343,20 4,18.657 4,17 L4,7 C4,5.343 5.343,4 7,4 Z"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M8,4 L8,7 C8,8.105 7.105,9 6,9 L4,9"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>

    <DrawingImage x:Key="Icon.Property.Shadow">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M5,4 L15,4 L15,14 L5,14 Z"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M8,7 L18,7 L18,17 L8,17 Z" Opacity="0.4"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>

    <DrawingImage x:Key="Icon.Property.Transform">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M4,4 L20,4 L20,20 L4,20 Z"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M4,4 L20,20 M20,4 L14,4 L20,10 Z"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>

    <DrawingImage x:Key="Icon.Property.AdvancedCss">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M4,4 L8,4 L8,20 L4,20 Z"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M10,4 L14,4 L14,10 L10,10 Z M10,14 L14,14 L14,20 L10,20 Z"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M7,7 L13,10 M7,17 L13,14 M7,11 L10,12"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>

    <DrawingImage x:Key="Icon.Property.Reset">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M5,6 L10,2 L15,6"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M10,2 L10,10"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M19,12 C19,16.971 14.971,21 10,21 C6.5,21 3.5,19 1.5,16"/>
                <GeometryDrawing Pen="{StaticResource Icon.OutlinePen}" Geometry="M17,15 L20,18 L17,21"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>
```

- [ ] **Step 4: Vérifier que les tests d'icônes passent**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~IconsXaml_PropertyIcons"
```

Attendu : tous passent (13 clés résolues).

- [ ] **Step 5: Commit**

```bash
git add src/ScadaBuilderV2.App/Resources/Icons.xaml tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: add Icon.Property.* semantic icon family for Style tab

13 new DrawingImage keys: Typography, FontFamily, FontWeight, FontStyle,
TextDecoration, TextAlign, Colors, Border, BorderRadius, Shadow, Transform,
AdvancedCss, Reset. All vector primitives with Icon.OutlinePen, consistent
with existing families. Test verifies every key resolves in Icons.xaml.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 5: Refondre l'onglet Style du dialogue modal ElementPropertiesDialog

**Files:**
- Modify: `src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml:98-156`
- Modify: `src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml.cs:239-380,451-483`

**Interfaces:**
- Consumes: `ScadaElementStyle` nouveaux champs (Task 1), `Icon.Property.*` (Task 4)
- Produces: `ElementPropertiesDialogResult` étendu avec 8 champs Style + Foreground, dialogue modal avec onglet Style en sections

- [ ] **Step 1: Remplacer le XAML de l'onglet Style**

Remplacer les lignes 98-156 (`<TabItem Header="Style">...</TabItem>`) par :

```xml
<TabItem Header="Style">
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel Margin="0,8,8,0">
            <!-- Section Typographie -->
            <StackPanel Margin="0,0,0,12">
                <DockPanel Margin="0,0,0,6">
                    <Image Source="{DynamicResource Icon.Property.Typography}" Width="16" Height="16" Margin="0,0,6,0"/>
                    <TextBlock Text="Typographie" FontWeight="SemiBold" Foreground="{StaticResource InkBrush}"/>
                    <Button Content="Reset" DockPanel.Dock="Right" Click="OnResetTypographyClick"
                            ToolTip="Reinitialiser la typographie aux valeurs par defaut" Padding="6,2" MinHeight="24"/>
                </DockPanel>
                <!-- Ligne de formatage rapide -->
                <UniformGrid Columns="4" Margin="0,3,0,6">
                    <ToggleButton x:Name="BoldToggle" Content="B" FontWeight="Bold" ToolTip="Gras (Ctrl+G)"/>
                    <ToggleButton x:Name="ItalicToggle" Content="I" FontStyle="Italic" ToolTip="Italique (Ctrl+I)"/>
                    <ToggleButton x:Name="UnderlineToggle" ToolTip="Souligne (Ctrl+U)">
                        <TextBlock Text="U" TextDecorations="Underline"/>
                    </ToggleButton>
                    <ToggleButton x:Name="StrikethroughToggle" ToolTip="Barre">
                        <TextBlock Text="S" TextDecorations="Strikethrough"/>
                    </ToggleButton>
                </UniformGrid>
                <!-- Alignement -->
                <UniformGrid Columns="4" Margin="0,0,0,6">
                    <RadioButton x:Name="AlignLeftRadio" GroupName="TextAlign" ToolTip="Aligner a gauche">
                        <Image Source="{DynamicResource Icon.Property.TextAlign}" Width="14" Height="14"/>
                    </RadioButton>
                    <RadioButton x:Name="AlignCenterRadio" GroupName="TextAlign" ToolTip="Centrer"/>
                    <RadioButton x:Name="AlignRightRadio" GroupName="TextAlign" ToolTip="Aligner a droite"/>
                    <RadioButton x:Name="AlignJustifyRadio" GroupName="TextAlign" ToolTip="Justifier"/>
                </UniformGrid>
                <!-- Famille et taille -->
                <Grid Margin="0,3,0,0">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="2*"/>
                        <ColumnDefinition Width="8"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <ComboBox x:Name="FontFamilyComboBox" Grid.Column="0">
                        <ComboBoxItem Content="Segoe UI"/>
                        <ComboBoxItem Content="Arial"/>
                        <ComboBoxItem Content="Calibri"/>
                        <ComboBoxItem Content="Consolas"/>
                    </ComboBox>
                    <TextBox x:Name="FontSizeTextBox" Grid.Column="2" ToolTip="Taille de police (px)"/>
                </Grid>
                <!-- Ligne avancee typo -->
                <UniformGrid Columns="3" Margin="0,6,0,0">
                    <ComboBox x:Name="TextTransformComboBox" ToolTip="Transformation du texte">
                        <ComboBoxItem Content="Aucune" Tag="None"/>
                        <ComboBoxItem Content="MAJUSCULES" Tag="Uppercase"/>
                        <ComboBoxItem Content="minuscules" Tag="Lowercase"/>
                        <ComboBoxItem Content="Capitales" Tag="Capitalize"/>
                    </ComboBox>
                    <TextBox x:Name="LetterSpacingTextBox" ToolTip="Espacement des lettres (px)"/>
                    <TextBox x:Name="LineHeightTextBox" ToolTip="Interligne (px, 0 = normal)"/>
                </UniformGrid>
            </StackPanel>

            <!-- Section Couleurs -->
            <StackPanel Margin="0,0,0,12">
                <DockPanel Margin="0,0,0,6">
                    <Image Source="{DynamicResource Icon.Property.Colors}" Width="16" Height="16" Margin="0,0,6,0"/>
                    <TextBlock Text="Couleurs" FontWeight="SemiBold" Foreground="{StaticResource InkBrush}"/>
                    <Button Content="Reset" DockPanel.Dock="Right" Click="OnResetColorsClick"
                            ToolTip="Reinitialiser les couleurs aux valeurs par defaut" Padding="6,2" MinHeight="24"/>
                </DockPanel>
                <TextBlock Text="Couleur du texte"/>
                <local:ColorPickerField x:Name="ForegroundColorPicker"/>
                <TextBlock Text="Couleur d'arriere-plan"/>
                <local:ColorPickerField x:Name="BackgroundColorPicker"/>
            </StackPanel>

            <!-- Section Bordure -->
            <StackPanel Margin="0,0,0,12">
                <DockPanel Margin="0,0,0,6">
                    <Image Source="{DynamicResource Icon.Property.Border}" Width="16" Height="16" Margin="0,0,6,0"/>
                    <TextBlock Text="Bordure" FontWeight="SemiBold" Foreground="{StaticResource InkBrush}"/>
                    <Button Content="Reset" DockPanel.Dock="Right" Click="OnResetBorderClick"
                            ToolTip="Reinitialiser la bordure aux valeurs par defaut" Padding="6,2" MinHeight="24"/>
                </DockPanel>
                <DockPanel Margin="0,0,0,2">
                    <TextBlock Text="Couleur bordure" VerticalAlignment="Center"/>
                    <CheckBox x:Name="BorderTransparentCheckBox" Content="Transparent" VerticalAlignment="Center"
                              Margin="8,0,0,0" Checked="OnBorderTransparentChanged" Unchecked="OnBorderTransparentChanged"/>
                </DockPanel>
                <local:ColorPickerField x:Name="BorderColorPicker"/>
                <ComboBox x:Name="BorderStyleComboBox">
                    <ComboBoxItem Content="Solid"/>
                    <ComboBoxItem Content="Dashed"/>
                    <ComboBoxItem Content="Dotted"/>
                    <ComboBoxItem Content="Double"/>
                    <ComboBoxItem Content="Groove"/>
                    <ComboBoxItem Content="Ridge"/>
                    <ComboBoxItem Content="Inset"/>
                    <ComboBoxItem Content="Outset"/>
                    <ComboBoxItem Content="None"/>
                </ComboBox>
                <TextBlock Text="Largeur bordure (px)"/>
                <TextBox x:Name="BorderWidthTextBox"/>
                <!-- Rayon -->
                <DockPanel Margin="0,6,0,0">
                    <Image Source="{DynamicResource Icon.Property.BorderRadius}" Width="14" Height="14" Margin="0,0,6,0"/>
                    <TextBlock Text="Rayon uniforme (px)"/>
                    <ToggleButton x:Name="BorderRadiusPerCornerToggle" DockPanel.Dock="Right"
                                  Content="Par coin" ToolTip="Activer le mode rayon par coin"
                                  Checked="OnBorderRadiusPerCornerChanged" Unchecked="OnBorderRadiusPerCornerChanged"
                                  Padding="6,2" MinHeight="24"/>
                </DockPanel>
                <TextBox x:Name="BorderRadiusUniformTextBox" ToolTip="Rayon de bordure uniforme (px)"/>
                <Grid x:Name="BorderRadiusPerCornerGrid" Visibility="Collapsed" Margin="0,4,0,0">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="4"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <StackPanel>
                        <TextBlock Text="Haut gauche"/>
                        <TextBox x:Name="BorderRadiusTopLeftTextBox"/>
                        <TextBlock Text="Bas gauche"/>
                        <TextBox x:Name="BorderRadiusBottomLeftTextBox"/>
                    </StackPanel>
                    <StackPanel Grid.Column="2">
                        <TextBlock Text="Haut droit"/>
                        <TextBox x:Name="BorderRadiusTopRightTextBox"/>
                        <TextBlock Text="Bas droit"/>
                        <TextBox x:Name="BorderRadiusBottomRightTextBox"/>
                    </StackPanel>
                </Grid>
            </StackPanel>

            <!-- Section Ombre et effets -->
            <StackPanel Margin="0,0,0,12">
                <DockPanel Margin="0,0,0,6">
                    <Image Source="{DynamicResource Icon.Property.Shadow}" Width="16" Height="16" Margin="0,0,6,0"/>
                    <TextBlock Text="Ombre et effets" FontWeight="SemiBold" Foreground="{StaticResource InkBrush}"/>
                    <Button Content="Reset" DockPanel.Dock="Right" Click="OnResetShadowClick"
                            ToolTip="Reinitialiser l'ombre et les effets aux valeurs par defaut" Padding="6,2" MinHeight="24"/>
                </DockPanel>
                <UniformGrid Columns="4" Margin="0,3,0,8">
                    <RadioButton x:Name="ShadowNoneRadio" Content="Aucun" GroupName="ShadowPreset"/>
                    <RadioButton x:Name="ShadowSoftRadio" Content="Soft" GroupName="ShadowPreset"/>
                    <RadioButton x:Name="ShadowRaisedRadio" Content="Lift" GroupName="ShadowPreset"/>
                    <RadioButton x:Name="ShadowInsetRadio" Content="Inset" GroupName="ShadowPreset"/>
                </UniformGrid>
                <UniformGrid Columns="2" Margin="0,0,0,0">
                    <StackPanel Margin="0,0,4,0">
                        <TextBlock Text="Opacite (0-1)"/>
                        <TextBox x:Name="OpacityTextBox"/>
                    </StackPanel>
                    <StackPanel Margin="4,0,0,0">
                        <TextBlock Text="Rotation (deg)"/>
                        <TextBox x:Name="RotationTextBox"/>
                    </StackPanel>
                </UniformGrid>
            </StackPanel>

            <!-- Section CSS avance -->
            <StackPanel>
                <DockPanel Margin="0,0,0,6">
                    <Image Source="{DynamicResource Icon.Property.AdvancedCss}" Width="16" Height="16" Margin="0,0,6,0"/>
                    <TextBlock Text="CSS avance" FontWeight="SemiBold" Foreground="{StaticResource InkBrush}"/>
                </DockPanel>
                <TextBlock Text="Ce champ surcharge les valeurs structurees ci-dessus. Utilisez-le uniquement pour des proprietes non couvertes par les sections."
                           Foreground="{StaticResource MutedBrush}" TextWrapping="Wrap" Margin="0,0,0,4"/>
                <TextBox x:Name="AdvancedCssTextBox"
                         AcceptsReturn="True"
                         MinHeight="76"
                         TextWrapping="Wrap"/>
            </StackPanel>
        </StackPanel>
    </ScrollViewer>
</TabItem>
```

- [ ] **Step 2: Ajouter l’aperçu vivant local du dialogue**

Ajouter dans le même `TabItem` un aperçu WPF temporaire (`Border`/`TextBlock`) couvrant typographie, couleurs, bordure, rayon, ombre, opacité et rotation. Chaque modification des contrôles doit mettre à jour cette projection sans modifier la scène, `HtmlCode` ou `CssCode`. Le rendu doit être visible avant `OnApplyClick` et respecter les décisions D17 et §12 de la spécification.

- [ ] **Step 3: Vérifier l’aperçu avant application**

Attendu : les changements sont visibles immédiatement, le reset de section met aussi l’aperçu à jour, et l’annulation du dialogue ne laisse aucune mutation dans le projet.

- [ ] **Step 4: Mettre à jour le code-behind — `LoadElement` (chargement du style)**

Remplacer le bloc de chargement Style (lignes 255-273) par :

```csharp
// Style — Typographie
SelectComboBoxText(FontFamilyComboBox, style.FontFamily);
FontSizeTextBox.Text = style.FontSize.ToString("0.##");
BoldToggle.IsChecked = IsFontWeightBold(style.FontWeight);
ItalicToggle.IsChecked = !string.Equals(style.FontStyle, "Normal", StringComparison.OrdinalIgnoreCase);
UnderlineToggle.IsChecked = HasTextDecoration(style.TextDecoration, "Underline");
StrikethroughToggle.IsChecked = HasTextDecoration(style.TextDecoration, "LineThrough");
SelectAlignRadio(style.TextAlign);
SelectComboBoxByTag(TextTransformComboBox, style.TextTransform);
LetterSpacingTextBox.Text = style.LetterSpacing.ToString("0.##");
LineHeightTextBox.Text = style.LineHeight.ToString("0.##");

// Style — Couleurs
ForegroundColorPicker.SetColor(style.Foreground);
BackgroundColorPicker.SetColor(style.Background);

// Style — Bordure
var isBorderTransparent = string.Equals(style.BorderColor, "Transparent", StringComparison.OrdinalIgnoreCase);
BorderTransparentCheckBox.IsChecked = isBorderTransparent;
BorderColorPicker.IsEnabled = !isBorderTransparent;
if (!isBorderTransparent)
    BorderColorPicker.SetColor(style.BorderColor);
SelectComboBoxText(BorderStyleComboBox, style.BorderStyle);
BorderWidthTextBox.Text = style.BorderWidth.ToString("0.##");
PopulateBorderRadius(style.BorderRadius);

// Style — Ombre et effets (inchange)
ShadowNoneRadio.IsChecked = style.ShadowPreset == "None";
ShadowSoftRadio.IsChecked = style.ShadowPreset == "Soft";
ShadowRaisedRadio.IsChecked = style.ShadowPreset == "Raised";
ShadowInsetRadio.IsChecked = style.ShadowPreset == "Inset";
OpacityTextBox.Text = style.Opacity.ToString("0.##");
RotationTextBox.Text = style.Rotation.ToString("0.##");

// Style — CSS avance (inchange)
AdvancedCssTextBox.Text = style.AdvancedCss ?? "";
```

- [ ] **Step 5: Ajouter les méthodes helper dans le code-behind**

```csharp
private static bool IsFontWeightBold(string fontWeight)
{
    return fontWeight switch
    {
        "Bold" or "Bolder" => true,
        _ when int.TryParse(fontWeight, out var w) && w >= 600 => true,
        _ => false
    };
}

private static bool HasTextDecoration(IReadOnlyList<string>? decorations, string kind)
{
    return decorations?.Any(d => string.Equals(d, kind, StringComparison.OrdinalIgnoreCase)) == true;
}

private void SelectAlignRadio(string textAlign)
{
    AlignLeftRadio.IsChecked = string.Equals(textAlign, "Left", StringComparison.OrdinalIgnoreCase);
    AlignCenterRadio.IsChecked = string.Equals(textAlign, "Center", StringComparison.OrdinalIgnoreCase);
    AlignRightRadio.IsChecked = string.Equals(textAlign, "Right", StringComparison.OrdinalIgnoreCase);
    AlignJustifyRadio.IsChecked = string.Equals(textAlign, "Justify", StringComparison.OrdinalIgnoreCase);
}

private void PopulateBorderRadius(ScadaBorderRadius? radius)
{
    var r = radius?.Normalized() ?? ScadaBorderRadius.None;
    if (r.IsUniform)
    {
        BorderRadiusUniformTextBox.Text = r.TopLeft.ToString("0.##");
        BorderRadiusPerCornerToggle.IsChecked = false;
        BorderRadiusPerCornerGrid.Visibility = Visibility.Collapsed;
    }
    else
    {
        BorderRadiusUniformTextBox.Text = "";
        BorderRadiusPerCornerToggle.IsChecked = true;
        BorderRadiusPerCornerGrid.Visibility = Visibility.Visible;
        BorderRadiusTopLeftTextBox.Text = r.TopLeft.ToString("0.##");
        BorderRadiusTopRightTextBox.Text = r.TopRight.ToString("0.##");
        BorderRadiusBottomRightTextBox.Text = r.BottomRight.ToString("0.##");
        BorderRadiusBottomLeftTextBox.Text = r.BottomLeft.ToString("0.##");
    }
}

private void OnBorderRadiusPerCornerChanged(object sender, RoutedEventArgs e)
{
    if (BorderRadiusPerCornerToggle.IsChecked == true)
    {
        BorderRadiusPerCornerGrid.Visibility = Visibility.Visible;
        // Initialiser les 4 coins a la valeur uniforme actuelle
        if (TryReadDouble(BorderRadiusUniformTextBox.Text, "Rayon", out var uniform))
        {
            BorderRadiusTopLeftTextBox.Text = uniform.ToString("0.##");
            BorderRadiusTopRightTextBox.Text = uniform.ToString("0.##");
            BorderRadiusBottomRightTextBox.Text = uniform.ToString("0.##");
            BorderRadiusBottomLeftTextBox.Text = uniform.ToString("0.##");
        }
    }
    else
    {
        BorderRadiusPerCornerGrid.Visibility = Visibility.Collapsed;
    }
}

private string GetSelectedTextAlign()
{
    if (AlignLeftRadio.IsChecked == true) return "Left";
    if (AlignCenterRadio.IsChecked == true) return "Center";
    if (AlignRightRadio.IsChecked == true) return "Right";
    if (AlignJustifyRadio.IsChecked == true) return "Justify";
    return "Left";
}

private IReadOnlyList<string>? GetTextDecoration()
{
    var decorations = new List<string>();
    if (UnderlineToggle.IsChecked == true) decorations.Add("Underline");
    if (StrikethroughToggle.IsChecked == true) decorations.Add("LineThrough");
    // Overline is not exposed as a toggle in v1; reserved for future
    return decorations.Count > 0 ? decorations : null;
}

private string GetFontWeight()
{
    return BoldToggle.IsChecked == true ? "Bold" : "Normal";
}

private ScadaBorderRadius? GetBorderRadius()
{
    if (BorderRadiusPerCornerToggle.IsChecked == true)
    {
        var tl = ParseDoubleOrDefault(BorderRadiusTopLeftTextBox.Text, 0);
        var tr = ParseDoubleOrDefault(BorderRadiusTopRightTextBox.Text, 0);
        var br = ParseDoubleOrDefault(BorderRadiusBottomRightTextBox.Text, 0);
        var bl = ParseDoubleOrDefault(BorderRadiusBottomLeftTextBox.Text, 0);
        var r = new ScadaBorderRadius(tl, tr, br, bl).Normalized();
        return r.IsUniform && Math.Abs(r.TopLeft) < 0.01 ? null : r;
    }
    else
    {
        var uniform = ParseDoubleOrDefault(BorderRadiusUniformTextBox.Text, 0);
        if (uniform <= 0) return null;
        return new ScadaBorderRadius(uniform, uniform, uniform, uniform);
    }
}

private string GetTextTransform()
{
    var selected = TextTransformComboBox.SelectedItem as ComboBoxItem;
    return selected?.Tag?.ToString() ?? "None";
}
```

- [ ] **Step 6: Mettre à jour `OnApplyClick` pour collecter les nouvelles propriétés**

Étendre le `ElementPropertiesDialogResult` (remplacer lignes 451-483) :

```csharp
public sealed record ElementPropertiesDialogResult(
    string DisplayName,
    SceneBounds Bounds,
    ElementPositionMode PositionMode,
    string FontFamily,
    double FontSize,
    string Foreground,
    string Background,
    string BorderColor,
    string BorderStyle,
    double BorderWidth,
    string ShadowPreset,
    double Opacity,
    double Rotation,
    string? AdvancedCss,
    string FontWeight,
    string FontStyle,
    IReadOnlyList<string>? TextDecoration,
    string TextAlign,
    string TextTransform,
    double LetterSpacing,
    double LineHeight,
    ScadaBorderRadius? BorderRadius,
    bool ButtonDisabled,
    bool ButtonHoverEnabled,
    string ButtonHoverBackground,
    string ButtonHoverForeground,
    string ButtonHoverBorderColor,
    bool ButtonPressedEnabled,
    string ButtonPressedBackground,
    string ButtonPressedForeground,
    string ButtonPressedBorderColor,
    string? Placeholder,
    string? Text,
    double? Value,
    double? Minimum,
    double? Maximum,
    int? Decimals,
    string? Unit,
    string? DisplayFormat,
    string? TagBinding,
    bool IsReadOnly);
```

Mettre à jour la construction du résultat dans `OnApplyClick` (après AdvancedCss, avant ButtonDisabled) :

```csharp
Foreground: GetColorPickerValue(ForegroundColorPicker, "#0F2A30"),
FontWeight: GetFontWeight(),
FontStyle: ItalicToggle.IsChecked == true ? "Italic" : "Normal",
TextDecoration: GetTextDecoration(),
TextAlign: GetSelectedTextAlign(),
TextTransform: GetTextTransform(),
LetterSpacing: Math.Clamp(ParseDoubleOrDefault(LetterSpacingTextBox.Text, 0), -10, 50),
LineHeight: Math.Max(0, ParseDoubleOrDefault(LineHeightTextBox.Text, 0)),
BorderRadius: GetBorderRadius(),
```

- [ ] **Step 7: Ajouter les handlers de reset par section**

```csharp
private void OnResetTypographyClick(object sender, RoutedEventArgs e)
{
    BoldToggle.IsChecked = false;
    ItalicToggle.IsChecked = false;
    UnderlineToggle.IsChecked = false;
    StrikethroughToggle.IsChecked = false;
    AlignLeftRadio.IsChecked = true;
    SelectComboBoxText(FontFamilyComboBox, "Segoe UI");
    FontSizeTextBox.Text = "16";
    SelectComboBoxByTag(TextTransformComboBox, "None");
    LetterSpacingTextBox.Text = "0";
    LineHeightTextBox.Text = "0";
}

private void OnResetColorsClick(object sender, RoutedEventArgs e)
{
    ForegroundColorPicker.SetColor("#0F2A30");
    BackgroundColorPicker.SetColor("Transparent");
}

private void OnResetBorderClick(object sender, RoutedEventArgs e)
{
    BorderTransparentCheckBox.IsChecked = true;
    SelectComboBoxText(BorderStyleComboBox, "None");
    BorderWidthTextBox.Text = "0";
    BorderRadiusUniformTextBox.Text = "0";
    BorderRadiusPerCornerToggle.IsChecked = false;
    BorderRadiusPerCornerGrid.Visibility = Visibility.Collapsed;
}

private void OnResetShadowClick(object sender, RoutedEventArgs e)
{
    ShadowNoneRadio.IsChecked = true;
    OpacityTextBox.Text = "1";
    RotationTextBox.Text = "0";
}
```

- [ ] **Step 8: Ajouter `SelectComboBoxByTag` helper**

```csharp
private static void SelectComboBoxByTag(ComboBox comboBox, string tag)
{
    foreach (ComboBoxItem item in comboBox.Items)
    {
        if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
        {
            item.IsSelected = true;
            return;
        }
    }
}
```

- [ ] **Step 9: Build et vérification**

```powershell
dotnet build ScadaBuilderV2.sln
```

Corriger les erreurs de compilation éventuelles (noms de contrôles, références manquantes).

- [ ] **Step 10: Vérifier les tests**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~WebViewContextMenuScriptTests"
```

- [ ] **Step 11: Commit**

```bash
git add src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml.cs
git commit -m "feat: redesign ElementPropertiesDialog Style tab with sections and new properties

Reorganize Style tab into Typography, Colors, Border, Shadow & Effects,
Advanced CSS sections with section icons (Icon.Property.*) and per-section
reset buttons.

Add controls: Bold/Italic/Underline/Strikethrough toggles, text alignment
radio buttons, text transform combo, letter spacing, line height,
Foreground color picker, extended border style combo (9 values),
border radius (uniform + per-corner mode).

Extend ElementPropertiesDialogResult with 8 new Style fields + Foreground.
Add toggle helpers: GetFontWeight, GetTextDecoration, GetBorderRadius, etc.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 6: Refondre l'onglet Style du panneau docké MainWindow

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml:910-970`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs:6215-6284`

**Interfaces:**
- Consumes: `ScadaElementStyle` nouveaux champs (Task 1), `ElementPropertiesDialogResult` étendu (Task 5), helpers de conversion
- Produces: Panneau docké avec onglet Style identique au dialogue modal (mêmes sections, contrôles, validations)

- [ ] **Step 1: Remplacer le XAML de l'onglet Style docké**

Remplacer les lignes 910-970 par le même XAML que Task 5 Step 1, en renommant les `x:Name` avec le préfixe `Element` (convention existante du panneau docké) :

| Dialogue modal | Panneau docké |
|---|---|
| `FontFamilyComboBox` | `ElementFontFamilyComboBox` |
| `FontSizeTextBox` | `ElementFontSizeTextBox` |
| `BoldToggle` | `ElementBoldToggle` |
| `ItalicToggle` | `ElementItalicToggle` |
| `UnderlineToggle` | `ElementUnderlineToggle` |
| `StrikethroughToggle` | `ElementStrikethroughToggle` |
| `AlignLeftRadio` | `ElementAlignLeftRadio` |
| `AlignCenterRadio` | `ElementAlignCenterRadio` |
| `AlignRightRadio` | `ElementAlignRightRadio` |
| `AlignJustifyRadio` | `ElementAlignJustifyRadio` |
| `TextTransformComboBox` | `ElementTextTransformComboBox` |
| `LetterSpacingTextBox` | `ElementLetterSpacingTextBox` |
| `LineHeightTextBox` | `ElementLineHeightTextBox` |
| `ForegroundColorPicker` | `ElementForegroundColorPicker` |
| `BackgroundColorPicker` | `ElementBackgroundColorPicker` |
| `BorderColorPicker` | `ElementBorderColorPicker` |
| `BorderTransparentCheckBox` | `ElementBorderTransparentCheckBox` |
| `BorderStyleComboBox` | `ElementBorderStyleComboBox` |
| `BorderWidthTextBox` | `ElementBorderWidthTextBox` |
| `BorderRadiusUniformTextBox` | `ElementBorderRadiusUniformTextBox` |
| `BorderRadiusPerCornerToggle` | `ElementBorderRadiusPerCornerToggle` |
| `BorderRadiusPerCornerGrid` | `ElementBorderRadiusPerCornerGrid` |
| `BorderRadiusTopLeftTextBox` | `ElementBorderRadiusTopLeftTextBox` |
| `BorderRadiusTopRightTextBox` | `ElementBorderRadiusTopRightTextBox` |
| `BorderRadiusBottomRightTextBox` | `ElementBorderRadiusBottomRightTextBox` |
| `BorderRadiusBottomLeftTextBox` | `ElementBorderRadiusBottomLeftTextBox` |
| `ShadowNoneRadio` | (garde le même nom — inchangé) |
| `ShadowSoftRadio` | (garde le même nom — inchangé) |
| `ShadowRaisedRadio` | (garde le même nom — inchangé) |
| `ShadowInsetRadio` | (garde le même nom — inchangé) |
| `OpacityTextBox` | `ElementOpacityTextBox` |
| `RotationTextBox` | `ElementRotationTextBox` |
| `AdvancedCssTextBox` | `ElementAdvancedCssTextBox` |

Note : `ElementFontFamilyComboBox`, `ElementFontSizeTextBox`, `ElementBackgroundColorPicker`, `ElementBorderColorPicker`, `ElementBorderStyleComboBox`, `ElementBorderWidthTextBox`, `ElementOpacityTextBox`, `ElementRotationTextBox`, `ElementAdvancedCssTextBox` existent déjà dans le panneau docké. Les nouveaux contrôles utilisent le préfixe `Element`.

Ajouter les handlers d'événement `OnElementPropertyChanged` sur chaque contrôle interactif (TextBox, ComboBox, ToggleButton, RadioButton, ColorPickerField).

- [ ] **Step 2: Ajouter l’aperçu vivant local du panneau docké**

Ajouter la même projection WPF temporaire que dans le dialogue modal, avec les mêmes valeurs, validations et mises à jour immédiates. L’aperçu ne doit pas écrire dans la scène avant l’application de la mutation prévue par le panneau et ne doit jamais être exporté.

- [ ] **Step 3: Mettre à jour `RefreshPropertiesUi` (chargement docké)**

Dans la méthode qui peuple le panneau docké (rechercher `ElementFontFamilyComboBox` dans `MainWindow.xaml.cs`), ajouter le même chargement que Task 5 Step 2, avec les noms préfixés `Element` :

```csharp
// Style — Typographie
ElementBoldToggle.IsChecked = IsFontWeightBold(style.FontWeight);
ElementItalicToggle.IsChecked = !string.Equals(style.FontStyle, "Normal", StringComparison.OrdinalIgnoreCase);
ElementUnderlineToggle.IsChecked = HasTextDecoration(style.TextDecoration, "Underline");
ElementStrikethroughToggle.IsChecked = HasTextDecoration(style.TextDecoration, "LineThrough");
SelectAlignRadioDock(style.TextAlign);
SelectComboBoxByTag(ElementTextTransformComboBox, style.TextTransform);
ElementLetterSpacingTextBox.Text = style.LetterSpacing.ToString("0.##");
ElementLineHeightTextBox.Text = style.LineHeight.ToString("0.##");

// Style — Couleurs
ElementForegroundColorPicker.SetColor(style.Foreground);

// Style — Bordure (BorderRadius)
PopulateBorderRadiusDock(style.BorderRadius);
```

- [ ] **Step 4: Mettre à jour `OnElementPropertyChanged` pour les nouveaux champs**

Étendre le bloc `Style = style with { ... }` (lignes 6236-6248) :

```csharp
Style = style with
{
    FontFamily = GetComboBoxText(ElementFontFamilyComboBox, style.FontFamily),
    FontSize = Math.Max(6, ParseDoubleOrDefault(ElementFontSizeTextBox.Text, style.FontSize)),
    Foreground = GetColorPickerValue(ElementForegroundColorPicker, style.Foreground),
    Background = GetColorPickerValue(ElementBackgroundColorPicker, style.Background),
    BorderColor = ElementBorderTransparentCheckBox.IsChecked == true
        ? "Transparent"
        : GetColorPickerValue(ElementBorderColorPicker, style.BorderColor),
    BorderStyle = GetComboBoxText(ElementBorderStyleComboBox, style.BorderStyle),
    BorderWidth = Math.Max(0, ParseDoubleOrDefault(ElementBorderWidthTextBox.Text, style.BorderWidth)),
    ShadowPreset = GetSelectedShadowPresetDock(),
    Opacity = Math.Clamp(ParseDoubleOrDefault(ElementOpacityTextBox.Text, style.Opacity), 0, 1),
    Rotation = ParseDoubleOrDefault(ElementRotationTextBox.Text, style.Rotation),
    AdvancedCss = string.IsNullOrWhiteSpace(ElementAdvancedCssTextBox.Text) ? null : ElementAdvancedCssTextBox.Text,
    FontWeight = ElementBoldToggle.IsChecked == true ? "Bold" : "Normal",
    FontStyle = ElementItalicToggle.IsChecked == true ? "Italic" : "Normal",
    TextDecoration = GetTextDecorationDock(),
    TextAlign = GetSelectedTextAlignDock(),
    TextTransform = (ElementTextTransformComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "None",
    LetterSpacing = Math.Clamp(ParseDoubleOrDefault(ElementLetterSpacingTextBox.Text, 0), -10, 50),
    LineHeight = Math.Max(0, ParseDoubleOrDefault(ElementLineHeightTextBox.Text, 0)),
    BorderRadius = GetBorderRadiusDock()
},
```

- [ ] **Step 5: Ajouter les méthodes helper dans MainWindow.xaml.cs**

Reprendre les helpers de Task 5 Step 3 avec les noms suffixés `Dock` et les noms de contrôles préfixés `Element`. Exemple :

```csharp
private void SelectAlignRadioDock(string textAlign)
{
    ElementAlignLeftRadio.IsChecked = string.Equals(textAlign, "Left", StringComparison.OrdinalIgnoreCase);
    ElementAlignCenterRadio.IsChecked = string.Equals(textAlign, "Center", StringComparison.OrdinalIgnoreCase);
    ElementAlignRightRadio.IsChecked = string.Equals(textAlign, "Right", StringComparison.OrdinalIgnoreCase);
    ElementAlignJustifyRadio.IsChecked = string.Equals(textAlign, "Justify", StringComparison.OrdinalIgnoreCase);
}

private string GetSelectedTextAlignDock()
{
    if (ElementAlignLeftRadio.IsChecked == true) return "Left";
    if (ElementAlignCenterRadio.IsChecked == true) return "Center";
    if (ElementAlignRightRadio.IsChecked == true) return "Right";
    if (ElementAlignJustifyRadio.IsChecked == true) return "Justify";
    return "Left";
}

private IReadOnlyList<string>? GetTextDecorationDock()
{
    var decorations = new List<string>();
    if (ElementUnderlineToggle.IsChecked == true) decorations.Add("Underline");
    if (ElementStrikethroughToggle.IsChecked == true) decorations.Add("LineThrough");
    return decorations.Count > 0 ? decorations : null;
}

private ScadaBorderRadius? GetBorderRadiusDock()
{
    if (ElementBorderRadiusPerCornerToggle.IsChecked == true)
    {
        var tl = ParseDoubleOrDefault(ElementBorderRadiusTopLeftTextBox.Text, 0);
        var tr = ParseDoubleOrDefault(ElementBorderRadiusTopRightTextBox.Text, 0);
        var br = ParseDoubleOrDefault(ElementBorderRadiusBottomRightTextBox.Text, 0);
        var bl = ParseDoubleOrDefault(ElementBorderRadiusBottomLeftTextBox.Text, 0);
        var r = new ScadaBorderRadius(tl, tr, br, bl).Normalized();
        return r.IsUniform && Math.Abs(r.TopLeft) < 0.01 ? null : r;
    }

    var uniform = ParseDoubleOrDefault(ElementBorderRadiusUniformTextBox.Text, 0);
    if (uniform <= 0) return null;
    return new ScadaBorderRadius(uniform, uniform, uniform, uniform);
}

private void PopulateBorderRadiusDock(ScadaBorderRadius? radius)
{
    var r = radius?.Normalized() ?? ScadaBorderRadius.None;
    if (r.IsUniform)
    {
        ElementBorderRadiusUniformTextBox.Text = r.TopLeft.ToString("0.##");
        ElementBorderRadiusPerCornerToggle.IsChecked = false;
        ElementBorderRadiusPerCornerGrid.Visibility = Visibility.Collapsed;
    }
    else
    {
        ElementBorderRadiusUniformTextBox.Text = "";
        ElementBorderRadiusPerCornerToggle.IsChecked = true;
        ElementBorderRadiusPerCornerGrid.Visibility = Visibility.Visible;
        ElementBorderRadiusTopLeftTextBox.Text = r.TopLeft.ToString("0.##");
        ElementBorderRadiusTopRightTextBox.Text = r.TopRight.ToString("0.##");
        ElementBorderRadiusBottomRightTextBox.Text = r.BottomRight.ToString("0.##");
        ElementBorderRadiusBottomLeftTextBox.Text = r.BottomLeft.ToString("0.##");
    }
}
```

Ajouter les handlers de reset par section pour le panneau docké (`OnResetTypographyDock`, `OnResetColorsDock`, `OnResetBorderDock`, `OnResetShadowDock`) et `OnBorderRadiusPerCornerChangedDock`.

- [ ] **Step 6: Build et vérification**

```powershell
dotnet build ScadaBuilderV2.sln
```

- [ ] **Step 7: Vérifier les tests de contrat UI**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~WebViewContextMenuScriptTests"
```

- [ ] **Step 8: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs
git commit -m "feat: redesign MainWindow docked Style tab with sections matching dialog

Mirror the ElementPropertiesDialog Style tab layout: Typography, Colors,
Border, Shadow & Effects, Advanced CSS sections with identical controls
and validations. All new controls prefixed 'Element' per existing convention.

Docked panel now supports: Bold/Italic/Underline/Strikethrough toggles,
text alignment, text transform, letter spacing, line height, Foreground
color picker, 9 border styles, border radius (uniform + per-corner),
and per-section reset buttons.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 7: Mettre à jour le contrat PROPERTIES_PANEL_CONTRACT_V2.md

**Files:**
- Modify: `docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md`
- Modify: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`
- Modify: `CLAUDE.md`
- Modify: `codex.md`

- [ ] **Step 1: Ajouter les règles de contrat pour les nouvelles propriétés**

Après la règle 19, ajouter :

```markdown
20. The Element+ `Style` tab exposes model-backed typography properties: `FontWeight` (Normal/Bold with toggle), `FontStyle` (Normal/Italic with toggle), `TextDecoration` (combinable Underline/LineThrough toggles), `TextAlign` (Left/Center/Right/Justify as radio buttons), `TextTransform` (None/Uppercase/Lowercase/Capitalize as combo), `LetterSpacing` in px (-10 to 50), and `LineHeight` in px (0 = normal, negative rejected). All have backward-compatible defaults that produce the historical rendering.
21. `Foreground` (text color) is authorable through the same `ColorPickerField` used for `Background` and `BorderColor`. The default is `#0F2A30` matching `DefaultText.Foreground`.
22. The `BorderStyle` combo exposes all 9 CSS border styles: `Solid`, `Dashed`, `Dotted`, `Double`, `Groove`, `Ridge`, `Inset`, `Outset`, and `None`. The exporter lowercases the value; `None` becomes `none`.
23. `BorderRadius` is a four-corner pixel record. The UI offers a uniform mode (single text field, four corners equal) and a per-corner mode (toggle, four text fields). The model persists four values; uniform mode is a UI convenience. Negative values are normalized to zero before persistence.
24. Structured style properties are applied before `AdvancedCss`. `AdvancedCss` remains the final user override except for export geometry, namespace, and security invariants.
25. Both `ElementPropertiesDialog` and the docked `MainWindow` panel use the same section layout (Typography, Colors, Border, Shadow & Effects, Advanced CSS), the same control types, and the same validation rules.
26. Per-section reset buttons restore factory defaults for that section only, without affecting other sections or other tabs.
```

- [ ] **Step 2: Synchroniser le contrat TF100Web et les guidances protégées**

Documenter dans le contrat actif, `CLAUDE.md` et `codex.md` que le chemin moderne TF100Web traite l’HTML/CSS Element+ comme opaque, conserve le manifest PascalCase et les attributs JSON runtime camelCase, et n’interprète pas les nouvelles propriétés de style. Ne modifier aucun runtime TF100Web dans cette tâche.

- [ ] **Step 3: Mettre à jour la table des tests liés**

Ajouter à la section 3 "Related Tests" :

```markdown
5. `tests/ScadaBuilderV2.Tests/ScadaSceneModelsTests.cs` — tests de défaut, validation, bornes, sérialisation JSON
6. `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs` — tests undo/redo des nouvelles proprietes
```

- [ ] **Step 4: Mettre à jour l'historique des changements**

```markdown
| 2026-07-13 | `V2.1.3.0010` | `PENDING` | Ajout des règles 20-26 et synchronisation du contrat TF100Web pour les propriétés de style avancées. |
```

- [ ] **Step 5: Commit**

```bash
git add docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md CLAUDE.md codex.md
git commit -m "docs: add contract rules 20-26 for advanced Element+ style properties

Document typography fields, 9 border styles, BorderRadius record,
Foreground authoring, per-section reset, and structured-before-AdvancedCss
override order.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 8: Preuve d’intake TF100Web (Authorization Gate)

> **Authorization required before modifying `F:\Projet\Git\TF100Web`.** This task may modify only the named test file; no TF100Web runtime or Django production code may be changed.

**Files:**
- Inspect: `F:\Projet\Git\TF100Web\frontend\views.py`
- Inspect: `F:\Projet\Git\TF100Web\frontend\scada_builder_composition.py`
- Inspect: `F:\Projet\Git\TF100Web\core\management\commands\deploy_scada_builder.py`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_deploy.py`

**Interfaces:**
- Consumes: a Builder `.sb2` fixture containing `font-weight`, `font-style`, `text-decoration`, `border-style: inset`, and `border-radius`.
- Produces: proof that deployment and `frontend.views.scada_package_page` preserve Element+ HTML/CSS as opaque content.

- [ ] **Step 1: Vérifier l’état et le contrat du dépôt externe**

```powershell
Set-Location "F:\Projet\Git\TF100Web"
git status --short --branch
```

Confirm that pre-existing changes are preserved and that the test targets `scada_package_page` → `load_composed_page`.

- [ ] **Step 2: Ajouter le test d’intake sans modifier le runtime**

Construire ou utiliser un fixture `.sb2` minimal avec manifest PascalCase et HTML/CSS contenant les nouvelles propriétés. Déployer avec `deploy_scada_builder`, appeler `scada_package_page`, puis vérifier que le fragment retourné conserve les déclarations CSS et les attributs attendus. Vérifier séparément que les attributs JSON runtime restent camelCase. Le test ne doit pas exiger que TF100Web comprenne sémantiquement `ScadaElementStyle`.

- [ ] **Step 3: Exécuter la preuve d’intégration**

```powershell
Set-Location "F:\Projet\Git\TF100Web"
python manage.py test frontend.tests_scada_deploy -v 2
```

Attendu : le test de conservation passe ; toute limitation d’environnement est documentée et aucun code runtime TF100Web n’est modifié.

- [ ] **Step 4: Commit du test externe**

Créer un commit séparé dans TF100Web contenant uniquement le test d’intake, après validation du dépôt et sans embarquer les changements de SCADA Builder V2.

---

### Task 9: Tests undo/redo et validation finale

**Files:**
- Test: `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs`

**Interfaces:**
- Consumes: Toutes les tâches précédentes
- Produces: Couverture undo/redo pour les nouvelles propriétés

- [ ] **Step 1: Ajouter les tests undo/redo**

```csharp
[TestMethod]
public async Task UndoRedo_StyleFontWeight_RestoresPreviousValue()
{
    var service = CreateHistoryService();
    var scene = CreateSampleScene();
    var element = scene.Elements[0];
    var originalWeight = element.Style?.FontWeight ?? "Normal";

    var updatedStyle = (element.Style ?? ScadaElementStyle.DefaultText) with { FontWeight = "Bold" };
    var updated = element with { Style = updatedStyle };
    var sceneAfter = scene.WithReplacedElementRecursive(updated);

    service.Push($"Modifier graisse de {element.Id}", scene, sceneAfter);
    var undone = await service.UndoAsync();

    var restoredElement = undone!.FindElementRecursive(element.Id);
    Assert.IsNotNull(restoredElement);
    Assert.AreEqual(originalWeight, restoredElement!.Style?.FontWeight ?? "Normal");
}

[TestMethod]
public async Task UndoRedo_StyleBorderRadius_RestoresPreviousValue()
{
    var service = CreateHistoryService();
    var scene = CreateSampleScene();
    var element = scene.Elements[0];

    var updatedStyle = (element.Style ?? ScadaElementStyle.DefaultText) with
    {
        BorderRadius = new ScadaBorderRadius(8, 8, 4, 4)
    };
    var updated = element with { Style = updatedStyle };
    var sceneAfter = scene.WithReplacedElementRecursive(updated);

    service.Push($"Modifier rayon de {element.Id}", scene, sceneAfter);
    var undone = await service.UndoAsync();

    var restoredElement = undone!.FindElementRecursive(element.Id);
    Assert.IsNotNull(restoredElement);
    Assert.IsNull(restoredElement!.Style?.BorderRadius); // original had no radius
}

[TestMethod]
public async Task UndoRedo_StyleInsetBorder_PreservesAfterRedo()
{
    var service = CreateHistoryService();
    var scene = CreateSampleScene();
    var element = scene.Elements[0];

    var updatedStyle = (element.Style ?? ScadaElementStyle.DefaultText) with { BorderStyle = "Inset" };
    var updated = element with { Style = updatedStyle };
    var sceneAfter = scene.WithReplacedElementRecursive(updated);

    service.Push($"Modifier bordure de {element.Id}", scene, sceneAfter);

    var undone = await service.UndoAsync();
    var redone = await service.RedoAsync();

    var redoneElement = redone!.FindElementRecursive(element.Id);
    Assert.IsNotNull(redoneElement);
    Assert.AreEqual("Inset", redoneElement!.Style?.BorderStyle);
}

[TestMethod]
public async Task UndoRedo_StyleTextDecoration_TogglesCorrectly()
{
    var service = CreateHistoryService();
    var scene = CreateSampleScene();
    var element = scene.Elements[0];

    var updatedStyle = (element.Style ?? ScadaElementStyle.DefaultText) with
    {
        TextDecoration = new[] { "Underline", "LineThrough" }
    };
    var updated = element with { Style = updatedStyle };
    var sceneAfter = scene.WithReplacedElementRecursive(updated);

    service.Push($"Modifier decoration de {element.Id}", scene, sceneAfter);
    var undone = await service.UndoAsync();

    var restoredElement = undone!.FindElementRecursive(element.Id);
    Assert.IsNotNull(restoredElement);
    Assert.IsNull(restoredElement!.Style?.TextDecoration);
}
```

- [ ] **Step 2: Exécuter les tests undo/redo**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~UndoRedo_Style"
```

Attendu : tous passent.

- [ ] **Step 3: Exécuter la suite de tests complète**

```powershell
dotnet test ScadaBuilderV2.sln --no-restore
```

Attendu : 100% des tests passent.

- [ ] **Step 4: Commit**

```bash
git add tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs
git commit -m "test: add undo/redo coverage for new Element+ style properties

Cover FontWeight, BorderRadius, BorderStyle (Inset), TextDecoration
toggles through the full undo → redo cycle.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 10: Vérification manuelle et documentation de statut

**Files:**
- Modify: `docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md` (si applicable)
- Modify: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`
- Modify: `docs/06_ui_ux/ICON_STRATEGY_V2.md`
- Modify: `docs/06_ui_ux/UI_SPECIFICATION_V2.md`

- [ ] **Step 1: Checklist de vérification manuelle**

Exécuter les scénarios suivants avec l'application :

1. **Typographie** : ouvrir un élément Text → appliquer Bold + Italic + Underline → vérifier la preview → sauvegarder → recharger → vérifier que les toggles sont actifs
2. **Couleurs** : changer Foreground → vérifier la preview live → appliquer → vérifier dans `.sb2`
3. **Bordure** : sélectionner `Inset` → vérifier que `box-shadow: inset ...` est émis dans l'export
4. **Rayon uniforme** : entrer 8px → vérifier les 4 coins arrondis dans la preview et l'export
5. **Rayon par coin** : basculer en mode par coin → entrer TL=12, TR=0, BR=12, BL=0 → vérifier l'export CSS `border-radius: 12px 0px 12px 0px`
6. **Parité surfaces** : ouvrir le dialogue modal et le panneau docké sur le même élément → vérifier que les contrôles affichent les mêmes valeurs
7. **Undo/redo** : modifier Bold → Ctrl+Z → vérifier que le toggle est revenu → Ctrl+Y → vérifier que le toggle est réactivé
8. **Rétrocompatibilité** : ouvrir `AMR_REF_SCADA_V2` (ancien projet) → vérifier qu'aucune erreur n'apparaît → les nouveaux champs sont aux défauts
9. **Icônes** : vérifier visuellement les 13 icônes `Icon.Property.*` dans le dialogue et le panneau docké
10. **`.sb2` inspection** : exporter → ouvrir le `.html` → vérifier que `font-weight`, `font-style`, `text-decoration`, `border-radius` sont présents → vérifier l'absence d'overlays éditeur
11. **AdvancedCss** : définir `FontWeight=Bold` + `AdvancedCss: "font-weight: 300 !important"` → vérifier que l'AdvancedCss écrase bien le structured dans l'export (ordre d'application D14)

- [ ] **Step 2: Mettre à jour IMPLEMENTED_FEATURES_V2.md**

Ajouter une entrée :

```markdown
| 2026-07-13 | `V2.1.3.0010` | `PENDING` | Style Element+ avancé : typographie, 9 styles de bordure, BorderRadius, Foreground authorable, aperçu vivant et refonte UI en sections avec icônes `Icon.Property.*`. |
```

- [ ] **Step 3: Mettre à jour REGRESSION_COVERAGE_V2.md**

Ajouter la couverture des nouveaux tests :

```markdown
| Style Element+ avancé | `ScadaSceneModelsTests`, `Ft100SceneExporterTests`, `WebViewContextMenuScriptTests`, `EditorHistoryServiceTests`, `TF100Web/frontend/tests_scada_deploy.py` | Défauts, bornes, sérialisation, aperçu/export, undo/redo, contrat UI et conservation après intake TF100Web |
```

- [ ] **Step 4: Mettre à jour ICON_STRATEGY_V2.md**

Ajouter à la liste des familles d'icônes (section 3) :

```markdown
8. `Icon.Property.*` pour les sections et controles de l'inspecteur de proprietes Style (Typography, FontFamily, FontWeight, FontStyle, TextDecoration, TextAlign, Colors, Border, BorderRadius, Shadow, Transform, AdvancedCss, Reset).
```

- [ ] **Step 5: Exécuter la validation de documentation**

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
```

Attendu : pas d'erreurs.

- [ ] **Step 6: Commit final**

```bash
git add docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md docs/08_implementation_status/REGRESSION_COVERAGE_V2.md docs/06_ui_ux/ICON_STRATEGY_V2.md docs/06_ui_ux/UI_SPECIFICATION_V2.md
git commit -m "docs: update status, coverage, and icon strategy for advanced Element+ style

Declare V2.1.3.0010 feature, add regression coverage entries, register
Icon.Property.* family in icon strategy.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Validation finale

- [ ] `dotnet build ScadaBuilderV2.sln`
- [ ] `dotnet test ScadaBuilderV2.sln --no-restore`
- [ ] Tests ciblés modèle, export, WebView, historique
- [ ] `python manage.py test frontend.tests_scada_deploy -v 2` dans `F:\Projet\Git\TF100Web`
- [ ] Vérification manuelle des 11 scénarios (Task 10 Step 1)
- [ ] Compatibilité projet ancien (AMR_REF_SCADA_V2)
- [ ] `powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1`
- [ ] `git status --short --branch` clean
