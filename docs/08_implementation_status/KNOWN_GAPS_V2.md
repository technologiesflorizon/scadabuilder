# SCADA Builder V2 - Known Gaps

Date: 2026-07-14
Status: Active known gaps register
Document version: `V2.1.2.0027`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-14 | `V2.1.2.0027` | `PENDING` | Baseline de tests actualisee apres la correction du crash WPF au demarrage. |
| 2026-07-14 | `V2.1.2.0026` | `PENDING` | Ajout des validations manuelles et fonctions avancées de classement/modèles/droits restant hors de la tranche Pages. |
| 2026-06-17 | `V2.1.2.0025` | `58567eb` | Retrait du gap TF100Web pour les masques `DisplayFormat` `#` apres commit `3c795c2`. |
| 2026-06-17 | `V2.1.2.0024` | `PENDING` | Ajout du gap TF100Web restant pour interpreter les masques `DisplayFormat` de type `##.#`. |
| 2026-06-17 | `V2.1.2.0023` | `PENDING` | Ajout du backlog de parite events TF100Web pour preparer la prochaine tranche d'implementation. |
| 2026-06-17 | `V2.1.2.0022` | `PENDING` | Retrait du gap TF100Web pour l'intake host-side des events de binding `ValueBindings`; maintien des gaps page-script hors fragment. |
| 2026-06-17 | `V2.1.2.0019` | `bd6515e` | Clarification que l'export `.sb2` ne ferme pas le gap runtime fragment TF100Web. |
| 2026-06-17 | `V2.1.2.0018` | `ad364a6` | Ajout du gap de parite entre runtime exporte SCADA Builder et intake fragment TF100Web. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Retrait du gap effets visuels standards; le styling custom reste roadmap. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Retrait du gap lifecycle runtime global; le chargement de scripts custom reste roadmap. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Retrait du gap conditions composees et politique degradee simple. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Retrait du gap politique popup avancee; le placement visuel authorable reste roadmap. |
| 2026-06-17 | `V2.1.2.0016` | `PENDING` | Retrait du gap hover group border; les effets visuels avances restent roadmap. |
| 2026-06-17 | `V2.1.2.0015` | `PENDING` | Retrait du gap actions popup close/toggle; les politiques avancees restent roadmap. |
| 2026-06-17 | `V2.1.2.0014` | `PENDING` | Retrait du gap `On click -> open popup`; les options avancees de popup restent roadmap. |
| 2026-06-17 | `V2.1.2.0012` | `PENDING` | Retrait du gap d'application runtime des valeurs lues; les reponses degradees restent roadmap. |
| 2026-06-17 | `V2.1.2.0010` | `PENDING` | Clarification que les conditions simples sont implementees pour actions objet, tandis que degrade/expressions restent roadmap. |
| 2026-06-17 | `V2.1.2.0009` | `PENDING` | Retrait du gap binding valeur importe et ajout du gap import protocoles pour creation locale de tags. |
| 2026-06-17 | `V2.1.2.0008` | `PENDING` | Remplacement du gap schema tags global par les limites restantes apres import catalogue et `WriteTag`. |
| 2026-06-16 | `V2.1.2.0005` | `PENDING` | Clarification que les metadonnees hover bouton sont implementees, tandis que l'application runtime appartient a FT100Web. |
| 2026-06-16 | `V2.1.2.0004` | `PENDING` | Clarification des limites restantes apres la premiere tranche Evenement Element+. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du registre des ecarts connus. |

## 1. Known Gaps

1. Full migration from legacy top-level documentation into the new owner documents is not complete.
2. Public C# XML documentation coverage is not yet enforced as a failing build gate.
3. Visual authoring for popup placement, expression/formula conditions, custom effect styling, local effect preview, and controlled custom script loading remain roadmap unless tests prove otherwise.
4. Rich Element+ button hover runtime interpretation belongs to TF100Web. SCADA Builder V2 exports metadata and scoped CSS, but the editor preview does not apply the hover behavior locally.
5. Expression binding and SCADA Builder-side tag creation remain roadmap. Local tag creation depends on a future project protocol import revision.
6. The final `FT100`, `TF100Web`, and `tf100-web` naming convention remains to be decided.
7. Sanitized-source approval for divergent pages such as `win00008` remains unresolved.
8. TF100Web commit `3c795c2` extracts only page root fragments and does not execute SCADA Builder exporter-emitted page scripts. TF100Web now handles `.sb2` `ValueBindings` as host-side binding events and interprets `DisplayFormat` hash masks, but exporter-side lifecycle, popup, condition page-script evaluation, border/effect, and other non-navigation action runtimes remain TF100Web parity gaps until host-side handlers or page-script execution exist.
9. `.sb2` archive export validates import/package compatibility only. It does not make TF100Web execute SCADA Builder page scripts that remain outside the extracted root fragment.
10. Manual UI validation of the new Pages and Diagnostics surfaces on an isolated project copy remains pending; launching the current shell would open the protected real reference project. Automated WPF surface contracts and the temporary-project lifecycle test are green.
11. Page folders, drag-and-drop ordering, reusable page templates beyond `Blank`, and role-based page permissions remain later slices. The command gate and stable identity model are extension points, not claims that these features are complete.
12. The full test suite currently reports 555 passed and 4 pre-existing unrelated failures (`Ft100ExportPrefersReferenceHtmlSourceBeforeRawFallback`, `LegacyContextMenuExposesElementStudioCommand`, `ModernDoubleClickOpensWpfPropertiesDialog`, `ScadaBuilderLaunchesStudioFromProjectInDevelopmentToAvoidStaleBinaries`). The page lifecycle and WPF startup-binding targeted suites are green.

## 2. Rule

Known gaps must not be documented as implemented behavior.

## 3. TF100Web Event Parity Backlog

The following items are the active correction backlog for TF100Web after the `.sb2` binding-event intake slice:

1. Validate `ReadTag` production behavior on `win00007 / Element+ Text20 / tf100.mapping.180`.
2. Identify a writeable production candidate for `WriteTag` / `Ecrire valeur` and verify `data-scada-write-mapping-id` when read and write mappings differ.
3. Add TF100Web host-side action dispatch for non-navigation `Actions`.
4. Add host-side condition evaluation using TF100Web mapping/tag snapshots before executing conditioned actions.
5. Add visibility action handlers for `Show`, `Hide`, and `ToggleVisibility`.
6. Add class action handlers for border and standard visual effects.
7. Add popup fragment handlers for open, close, toggle, placement, and host-region behavior.
8. Add lifecycle diagnostics equivalent to the exporter `window.scadaBuilderRuntime` contract.
9. Decide whether controlled execution of exporter-emitted page scripts is allowed, or whether all remaining runtime behavior must be reimplemented as TF100Web host handlers.
