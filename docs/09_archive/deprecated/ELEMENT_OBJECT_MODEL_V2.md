# SCADA Builder V2 - Element Object Model

Date: 2026-06-15
Status: Approved direction, first implementation slice
Document version: `V2.1.1.0030`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Normalisation du header documentaire et rattachement a l'arbre documentaire stable. |
| 2026-06-15 | `V2.0.3.0016` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

## 1. Objective

Element+ objects must be real runtime/domain objects.

The editor must not treat a modern object only as a generic record with a kind enum and loose data bag. A converted or inserted object must be instantiated as a concrete class, for example:

```csharp
Elements.Add(new NumericInput(...));
```

The current `ScadaElement` record remains temporarily as a persistence and rendering adapter. It is not the final object model.

## 2. Base Element

All Element+ objects derive from a shared `Element` base class.

The base class owns generic behavior:

1. Id and display name.
2. Bounds and layout.
3. Style.
4. Optional legacy meta reference.
5. Generated or editable `HtmlCode`.
6. Generated or editable `CssCode`.
7. Generated or editable `JsCode`.
8. Conversion to the current `ScadaElement` adapter while the old rendering pipeline still exists.

Target shape:

```csharp
public abstract class Element : EditorObject
{
    public string Id { get; }
    public string Name { get; set; }
    public SceneBounds Bounds { get; set; }
    public ScadaElementStyle Style { get; set; }
    public string HtmlCode { get; set; }
    public string CssCode { get; set; }
    public string JsCode { get; set; }
    public LegacySourceTrace? LegacySource { get; set; }

    public abstract ScadaElementKind ScadaKind { get; }
    public abstract ScadaElementData ToScadaData();
    public virtual ScadaElement ToScadaElement();
}
```

## 3. Numeric Input

`Affichage numerique` and editable numeric input are the same object class.

The difference is only:

```csharp
IsReadOnly = true  // display-only numeric value
IsReadOnly = false // editable numeric value
```

Class:

```csharp
public sealed class NumericInput : Element
{
    public double? Value { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public int? Decimals { get; set; }
    public string? Unit { get; set; }
    public string? DisplayFormat { get; set; }
    public string? TagBinding { get; set; }
    public bool IsReadOnly { get; set; }
}
```

## 4. Conversion Rule

Conversion Element+ must be a transaction:

```text
LegacyObjects.Remove(source)
Elements.Add(new NumericInput(...))
UndoStack.Push(source + createdElement)
```

The WebView is only a projection. It must not be the source of truth for whether an object exists.

Persistence rule:

1. The Element+ object is saved in the V2 scene.
2. Its `LegacySource.SourceElementId` is used only to trace and suppress the converted legacy projection on reload.
3. The V2 element id remains the primary identity of the object.
4. The full legacy source object is not saved as part of the V2 scene and survives only in the session undo cache.
5. Legacy text overrides matching a converted source id are removed from the saved scene.

## 5. Migration Rule

Until the scene renderer and persistence are fully migrated:

1. New Element+ classes are created in the domain.
2. Converters instantiate concrete Element+ classes.
3. The concrete Element+ object adapts itself to `ScadaElement` through `ToScadaElement()`.
4. UI and persistence continue to consume `ScadaElement` temporarily.
5. The next phase migrates `ScadaScene.Elements` from `IReadOnlyList<ScadaElement>` to `IReadOnlyList<Element>`.

This avoids a risky full rewrite while establishing the official domain model.

## 6. Element Groups

`ElementGroup` is an Element+ object.

It owns a list of child `Element` objects and may contain:

1. Simple elements.
2. Other groups.
3. A mix of simple elements and nested groups.

Rules:

1. A child has one parent at a time.
2. A group cannot contain itself.
3. A group cannot contain one of its ancestors.
4. A selection cannot group both a group and one of that group's descendants.
5. Group bounds are absolute when the group is at scene root.
6. Direct child bounds are relative to the parent group.
7. Moving a parent changes only the parent bounds.
8. Moving a child changes only the child relative bounds.
9. Ungrouping converts direct child bounds back to absolute coordinates while preserving their visible position.
10. Nested groups stay grouped when their parent is ungrouped.

Coordinate rule:

```text
child.AbsoluteX = parent.AbsoluteX + child.RelativeX
child.AbsoluteY = parent.AbsoluteY + child.RelativeY
```

The first implementation slice provides the domain model and unit tests. UI commands and WebView rendering overlays are implemented in the next slice.

UI slice:

1. A selected legacy set can be grouped into an Element+ group.
2. The group is rendered as a blue parent outline.
3. A selected child is rendered as a yellow child outline while the group context remains visible.
4. The Element list can select groups and nested children.
5. The selected group can be ungrouped from the property panel or modern context menu.
6. Child movement is persisted through recursive scene replacement.
7. Multiple Element+ objects can be selected at the same time; the last selected object remains the primary object for the properties panel.
8. Groups are transparent containers only.
9. Grouping legacy elements before conversion must create only a frame/selection object; it must not hide, repaint, or replace the legacy rendering.
10. Shape children are rendered only for true Element+ shape objects, not as a substitute for unconverted legacy geometry.

## 7. Studio Element+ Boundary

Legacy polygon, line, and composed graphic selections are not simple one-to-one conversions.

They are source material for a new Element+ component and should be opened in Studio Element+.

Rules:

1. SCADA Builder V2 may frame or select legacy source elements.
2. SCADA Builder V2 must not convert complex geometry implicitly.
3. The command is `Ouvrir dans Studio Element+`.
4. Studio Element+ receives a `.ft1` import package.
5. Studio Element+ creates the final modern Element+ component.
6. SCADA Builder V2 inserts or replaces scene content only after explicit user action.
7. Studio Element+ must first render the imported legacy source markup faithfully before conversion tooling modifies or modernizes the content.
8. Full legacy markup and computed CSS are export payload, not normal selection payload; capture them only when creating the Studio import package.

## 8. Graphic Element+ Components

Element+ must support reusable graphic components, not only form controls and simple typed primitives.

Graphic components are the official path for legacy piping, composed symbols, and industrial visual assemblies made from lines, polylines, polygons, paths, text, and images.

Supported visual payload kinds:

1. `Svg`: cleaned and normalized SVG markup.
2. `Image`: raster asset plus metadata.
3. `Html`: structured HTML/CSS payload.
4. `Composite`: a tree of child Element+ objects.

Rules:

1. A graphic Element+ component is still one object at the scene level.
2. Its internal SVG may contain many primitives.
3. Internal primitives may later be named and targeted by events.
4. The first implementation may support component-level events only.
5. The component must be reusable from the project library.
6. The editable Studio source must be serializable to `.sep`.
7. Legacy selection UI artifacts must not become part of the component payload.
8. SVG coordinates must be normalized to the component coordinate system before save.
9. Image-backed components embed image data in `.sep` for portability.
10. Source legacy names can be preserved as metadata while Studio creates cleaner internal names for editable variants.
11. The Studio workzone is editor state and must not become exported Element+ geometry.
12. Drawing tools create real component primitives, not temporary editor overlays.
13. One `.sep` file contains exactly one Element+ component.
14. Legacy imports are transformed into component source content and must not remain as permanent non-destructive legacy layers in the final component.
15. `.ft1` is the SCADA Builder V2 -> Studio Element+ transfer/export format.
16. `.sep` is the shared library format consumed by Studio Element+ and SCADA Builder V2.

Example:

```text
Legacy selected piping:
  Polygon100
  Polygon397
  Polygon398
  Line014
  Polyline003

Studio Element+ output:
  PipingAssembly_001.sep
  VisualKind = Svg
  Bounds = component-local bounds
  SvgMarkup = cleaned normalized SVG
```

This allows a complex legacy graphic to become a modern reusable object without flattening every primitive into a top-level scene element.

The Element+ object name is the component name. Internal SVG part names are editing metadata, not the scene-level identity.

First implementation slice:

1. Studio Element+ can select imported source geometry from the WebView source layer.
2. Studio Element+ can create an in-memory SVG Element+ component draft from imported sources.
3. Studio Element+ must persist editable component work as `.sep`.
4. Library publication/export and source replacement remain later workflow steps.

Regression contract:

1. `.ft1` is only the transfer package from SCADA Builder V2 to Studio Element+.
2. `.sep` is the shared library/editing source package consumed by Studio Element+ and SCADA Builder V2.
3. Each `.sep` file contains exactly one Element+ component.
4. The Studio `Element` tab lists imported source items by generated Element+ names while preserving source traceability.
5. List selection and WebView source selection must converge on one Studio selection model.
6. Drawing tools create concrete component primitives or embedded assets in the Element+ component model.
7. The workzone, zoom/pan state, selection handles, diagnostic overlays, and other editor-only state are never exported as Element+ geometry.
