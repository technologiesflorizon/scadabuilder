# SCADA Builder V2 - Icon Strategy

Date: 2026-06-16
Status: Active icon strategy pointer
Document version: `V2.1.2.0039`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
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

Current icon families:

1. `Icon.Project.*` for project file commands.
2. `Icon.Import.*` and `Icon.Export.*` for intake and delivery commands.
3. `Icon.Edit.*` for edit/history/clipboard commands.
4. `Icon.View.*` for responsive preview and measurement commands.
5. `Icon.Selection.*`, `Icon.Object.*`, and `Icon.Layer.*` for scene selection and ordering.
6. `Icon.Tool.*` for general editor tools.
7. `Icon.Field.*`, `Icon.Shape.*`, `Icon.Hmi.*`, and `Icon.Button.*` for insert-ribbon authoring commands.

## 4. Standardization Rule

Every visible command in the top ribbon must resolve to a semantic icon key before the command is considered UI-complete.

Temporary text glyphs are not valid command icons in the top ribbon. Examples such as `BTN`, `LED`, `TNK`, `VLV`, `PMP`, `MTR`, `FAN`, `CVY`, `GAU`, `SW`, `CB`, `XFMR`, `ALM`, `123`, `[ ]`, `--`, `==`, and `I/O` may be useful during prototyping but must be replaced by normalized vector resources before delivery.

The current implementation uses internal original WPF vector primitives and does not introduce a third-party icon dependency. If the baseline later moves to Lucide or Fluent UI System Icons, the project must record the selected upstream version, license, converted files, and required third-party notices before distribution.
