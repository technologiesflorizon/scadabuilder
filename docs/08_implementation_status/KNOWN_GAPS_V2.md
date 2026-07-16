# SCADA Builder V2 - Known Gaps

Date: 2026-07-16
Status: Active known gaps register
Document version: `V2.1.4.0049`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-16 | `V2.1.4.0049` | `PENDING` | Builder 2.3 strict implemente; negotiation/rejet atomique TF100Web et fixture partagee restent gaps actifs. |
| 2026-07-16 | `V2.1.4.0048` | `PENDING` | Matrice runtime generee et verifiee; gaps semantiques bloques et fixture end-to-end par capability encore pending. |
| 2026-07-16 | `V2.1.4.0047` | `PENDING` | Registre/analyseur `DEC-0047` implementes; negotiation 2.3, matrice generee, fixture partagee et preuves end-to-end restent gaps actifs. |
| 2026-07-16 | `V2.1.4.0046` | `PENDING` | `DEC-0047` enregistre le gap systemique : absence actuelle de negotiation de capabilities et de preuve exhaustive; mapping absent reclasse fallback non bloquant. |
| 2026-07-16 | `V2.1.4.0045` | `PENDING` | Ajout du gap confirme navigation/poll de TF100Web `9d5d400`, de la latence de composition et du mapping officiel manquant `YL_E12_HDEG4`. |
| 2026-07-16 | `V2.1.4.0044` | `de37a35`, TF100Web `9d5d400` | Retrait du gap de code polling/gestes des cellules : chemin partage implemente; validation mappings/permissions/feedback PLC reels demeure un gate industriel. |
| 2026-07-16 | `V2.1.4.0043` | `8489dbd` | Retrait du gap Etat/Commande TF100Web : runtime package partage deploye et initialise, mappings de commande collectes; les anciennes actions popup/lifecycle restent distinctes. |
| 2026-07-15 | `V2.1.4.0039` | `PENDING` | `DEC-0042` est implemente et valide localement; polling/ecriture/gestes et permissions sur mappings industriels reels restent un gate de livraison autorise. |
| 2026-07-15 | `V2.1.4.0034` | `b75f1d7` | Smoke correctif Tableau/verrou reussi sur copie isolee; le gate performance WebView2 64 x 64 plus large de `DEC-0040` demeure distinct. Baseline : 618 reussites et 5 echecs historiques non lies. |
| 2026-07-15 | `V2.1.4.0028` | `c873744` | Les quatre blocages de validation des surfaces fondamentales ont été corrigés; le smoke WPF/WebView2 complet demeure le seul gate Tableau/verrou restant. |
| 2026-07-15 | `V2.1.4.0027` | `32a3ef6` | Mesures automatisées modèle/rendu 64 x 64 consignées; le gap est réduit au smoke WPF/WebView2 interactif isolé. Baseline complète : 608/613, cinq échecs historiques non liés. |
| 2026-07-15 | `V2.1.4.0026` | `0874416` | Ajout du gate interactif Release 64 x 64 restant avant cloture produit de `DEC-0040`. |
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
8. TF100Web commit `29ebd35` still extracts only page root fragments, but it deploys and loads the package shared runtime independently and initializes `StateConfig`/`CommandConfig` on composed fragments. Inline exporter scripts remain excluded; lifecycle, popup and legacy non-navigation action families therefore remain parity gaps until their host handlers or a controlled inline-script contract exist.
9. `.sb2` archive export validates import/package compatibility and transports the shared runtime. It does not make TF100Web execute inline page scripts that remain outside the extracted root fragment.
10. Manual UI validation of the new Pages and Diagnostics surfaces on an isolated project copy remains pending; launching the current shell would open the protected real reference project. Automated WPF surface contracts and the temporary-project lifecycle test are green.
11. Page folders, drag-and-drop ordering, reusable page templates beyond `Blank`, and role-based page permissions remain later slices. The command gate and stable identity model are extension points, not claims that these features are complete.
12. The full test suite currently reports 661 passed and 5 pre-existing unrelated failures (`Ft100ExportPrefersReferenceHtmlSourceBeforeRawFallback`, `LegacyContextMenuExposesElementStudioCommand`, `ModernDoubleClickOpensWpfPropertiesDialog`, `ReadOnlyNumericElementsRenderDisplayFormatWhenValueIsMissing`, `ScadaBuilderLaunchesStudioFromProjectInDevelopmentToAvoidStaleBinaries`). The targeted `DEC-0044`/`DEC-0045` suites are green.
13. Numeric Table cell polling, POST feedback, focus/Enter/blur/Escape semantics and permission guards now share the Element+ runtime path and are covered locally. Validation of real mappings, operator permissions and confirmed PLC readback still requires an explicitly authorized industrial TF100Web environment before delivery closure.
14. `DEC-0040` code and automated slices are complete. Release measurements on the current machine record model/HTML initial rendering at 367,556 ms, selection inspection p95 at 12,403 ms and Domain resize p95 at 0,023 ms over 100 samples. The focused `DEC-0041` WPF/WebView2 smoke passed on an isolated copy; the separate 64 x 64 browser-composition performance gate remains pending, and automated values must not be presented as WebView2 timings.
15. TF100Web `9d5d400` has a confirmed navigation/poll race: `poll(true)` returns when `pollInFlight` is set, then unchanged cached values do not notify the newly rendered DOM. `win00008 -> win00012_modern_no_legacy -> win00008` can therefore return without state overlays or readings. `DEC-0046` is approved but pending implementation; latest-wins navigation and mandatory hydration must not be claimed active yet.
16. Remote page composition measured approximately 6.7 s for `win00008` and 14.2 s for `win00012_modern_no_legacy`, while a 426-mapping snapshot measured approximately 0.2 s. Binding injection currently rescans a full fragment per binding. These observations require server-side profiling, single-pass/indexed injection and safe cache invalidation; cellular latency remains a separate external factor.
17. The official `tf100web-scada-tags (3).json` audit contains 425 tags but no `YL_E12_HDEG4` or mapping 615. This is a non-blocking quality case: deterministic fallback and diagnostics are required while all other controls remain functional. A local fabricated mapping is forbidden.
18. Runtime coverage is not yet general. The typed capability registry, analyzer, generated matrix and Builder-side manifest 2.3/hash/strict validator now exist. Baseline capabilities cite existing three-layer suites while known semantic gaps remain fail-closed `Blocked`; TF100Web negotiation, shared conformance `.sb2` and per-capability CI evidence remain pending. Builder 2.3 output is therefore not yet deployable to current TF100Web `9d5d400`.
19. Some portable behavior remains split between shared package modules, exporter inline scripts and TF100Web host branches. Until `DEC-0047` establishes one semantic owner and blocks unsupported capabilities, new behavior risks duplicate implementation or silent fragment-intake gaps.

## 2. Rule

Known gaps must not be documented as implemented behavior.

## 3. TF100Web Event Parity Backlog

The following items are the active correction backlog for TF100Web after the `.sb2` binding-event intake slice:

State/Command polling, AST evaluation, effects, Toggle reads and writes use the implemented shared runtime and are no longer part of this backlog.

1. Validate `ReadTag` production behavior on `win00007 / Element+ Text20 / tf100.mapping.180`.
2. Identify a writeable production candidate for `WriteTag` / `Ecrire valeur` and verify `data-scada-write-mapping-id` when read and write mappings differ.
3. Add TF100Web host-side action dispatch for non-navigation `Actions`.
4. Add host-side condition evaluation using TF100Web mapping/tag snapshots before executing conditioned actions.
5. Add visibility action handlers for `Show`, `Hide`, and `ToggleVisibility`.
6. Add class action handlers for border and standard visual effects.
7. Add popup fragment handlers for open, close, toggle, placement, and host-region behavior.
8. Add lifecycle diagnostics equivalent to the exporter `window.scadaBuilderRuntime` contract.
9. Decide whether controlled execution of exporter-emitted page scripts is allowed, or whether all remaining runtime behavior must be reimplemented as TF100Web host handlers.
