# SCADA Builder V2 - Properties Panel Contract

Date: 2026-06-16
Status: Active properties panel contract
Document version: `V2.1.2.0000`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.2.0000` | `PENDING` | Clarification du contrat Propriete pour les objets Element+ et du blocage explicite pour les sources non converties. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du contrat actif du panneau proprietes. |

## 1. Contract

The properties panel edits model-backed properties through commands or application services. It must not write durable behavior through ad hoc WebView state.

## 2. Rules

1. Common geometry fields reflect current selection.
2. Mixed values are blank or explicitly represented as mixed.
3. Invalid values must be blocked or warning-only; they must not silently export invalid runtime output.
4. CSS/runtime effect properties require metadata, validation, serialization, preview, and export rules before becoming active.
5. Tag bindings remain placeholders until the binding schema is decided and implemented.
6. Context-menu `Propriete` edits only converted Element+ scene objects; source legacy objects must be converted before the property panel can edit durable model-backed properties.
7. Button Element+ text is a model-backed `Data.Text` property and must render in preview and FT100 export.

## 3. Related Tests

1. `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`
2. `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
