# SCADA Builder V2 - Icon Strategy

Date: 2026-06-16
Status: Active icon strategy pointer
Document version: `V2.1.4.0004`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-14 | `V2.1.4.0004` | `PENDING` | Ajout de la famille vectorielle sémantique `Icon.Page.*` pour les commandes partagées de gestion des pages. |
| 2026-07-13 | `V2.1.4.0003` | `b954d46` | Création de la famille vectorielle sémantique `Icon.Property.*` pour l’inspecteur Style Element+. |
| 2026-06-19 | `V2.1.3.0001` | `620e914` | Ajustement de la galerie Formes vers des icones 32x32 sans libelles visibles. |
| 2026-06-19 | `V2.1.3.0000` | `b195fe0` | Ajout des icones semantiques Cercle, Triangle et Etoile et de la taille 64x64 pour la galerie Formes. |
| 2026-06-19 | `V2.1.2.0044` | `c50cbcf` | Extension du catalogue semantique a la palette laterale d'outils. |
| 2026-06-19 | `V2.1.2.0041` | `88a3e8b` | Ajout de la couverture de contrat pour les cles d'icones semantiques du catalogue de ruban. |
| 2026-06-19 | `V2.1.2.0039` | `e5f8a82` | Ajout du registre operationnel des icones de ruban et de la normalisation des glyphes visibles. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du nouveau document proprietaire de strategie d'icones. |

## 1. Active Rule

The icon system must remain centralized and mapped through semantic keys. Existing detailed candidate analysis is archived in `docs/09_archive/deprecated/ICON_STRATEGY_V2.md`.

## 2. Guardrails

1. Verify licenses before distribution.
2. Vendor only selected icons.
3. Keep SCADA-specific overlays internal and traceable.
4. State must not rely on color alone.

## 3. Operational Registry

The WPF shell currently owns its vector resources in:

```text
src/ScadaBuilderV2.App/Resources/Icons.xaml
src/ScadaBuilderV2.App/Resources/README.md
```

The semantic resource key is the public UI contract. A control should reference `Icon.Project.Save`, `Icon.Export.Package`, or `Icon.Hmi.Pump`, not a source library filename or temporary drawing name.

The default top-ribbon command catalog in `ScadaBuilderV2.Application.Commands.RibbonCommandCatalog` records the semantic `Icon.*` key for every visible command. The same catalog exposes the left tool palette through `CreateToolPalette()`. The WPF shell resolves those keys against `Icons.xaml`; command metadata tests block empty, non-semantic, or temporary icon references from entering the default catalog or tool palette.

Current icon families:

1. `Icon.Project.*` for project file commands.
2. `Icon.Import.*` and `Icon.Export.*` for intake and delivery commands.
3. `Icon.Edit.*` for edit/history/clipboard commands.
4. `Icon.View.*` for responsive preview and measurement commands.
5. `Icon.Selection.*`, `Icon.Object.*`, and `Icon.Layer.*` for scene selection and ordering.
6. `Icon.Tool.*` for general editor tools.
7. `Icon.Field.*`, `Icon.Shape.*`, `Icon.Hmi.*`, and `Icon.Button.*` for insert-ribbon authoring commands. The standard shape family includes `Icon.Shape.Rectangle`, `Icon.Shape.Ellipse`, `Icon.Shape.Circle`, `Icon.Shape.Triangle`, `Icon.Shape.Star`, `Icon.Shape.Line`, and `Icon.Shape.Arrow`.
8. `Icon.Page.*` for page creation, rename, duplication, deletion, properties, and validation commands shared by the ribbon and project panel.

## 4. Standardization Rule

Every visible command in the top ribbon or left tool palette must resolve to a semantic icon key before the command is considered UI-complete.

Temporary text glyphs are not valid command icons in the top ribbon. Examples such as `BTN`, `LED`, `TNK`, `VLV`, `PMP`, `MTR`, `FAN`, `CVY`, `GAU`, `SW`, `CB`, `XFMR`, `ALM`, `123`, `[ ]`, `--`, `==`, and `I/O` may be useful during prototyping but must be replaced by normalized vector resources before delivery.

The Insert `Formes` gallery scales its command icons to 32x32 and relies on the icon silhouette plus tooltip labels instead of visible shape-name text inside each button. Other ribbon command families continue using compact command icon sizing unless their contract explicitly defines a gallery treatment.

The current implementation uses internal original WPF vector primitives and does not introduce a third-party icon dependency. If the baseline later moves to Lucide or Fluent UI System Icons, the project must record the selected upstream version, license, converted files, and required third-party notices before distribution.

## 5. Element+ Property Icon Family

The Style inspector uses internal vector resources under the semantic `Icon.Property.*` namespace. The initial family includes `Typography`, `FontFamily`, `FontWeight`, `FontStyle`, `TextDecoration`, `TextAlign`, `Colors`, `Border`, `BorderRadius`, `Shadow`, `Transform`, `AdvancedCss`, and `Reset`. Each key resolves through `Resources/Icons.xaml`, uses `Icon.OutlinePen`, and remains independent of a third-party icon library.
