# SCADA Builder V2 - Known Gaps

Date: 2026-08-13
Status: Active known gaps register
Document version: `V2.1.5.0047`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-08-25 | `V2.1.5.0047` | `PENDING` | Task 5.3 partiellement close : conformance, canary et rollback verts; soak 24 h et deploiement production restent a decider. |
| 2026-08-25 | `V2.1.5.0046` | `705077c` | Task 5.2 livree : host TF100Web et SinglePerDefinition; restent la conformance cross-runtime et la composition header/pied. |
| 2026-08-25 | `V2.1.5.0045` | `40e300a` | Task 5.1 livree : ingestion et validation fail-closed des registres cote TF100Web; le chargeur de fragment et l'adaptateur host restent ouverts. |
| 2026-08-25 | `V2.1.5.0044` | `c3cfce9` | Prerequis MySQL leve : les suites Django adossees a la base passent sous WSL; il ne reste que le contenu fonctionnel de la Phase 5. |
| 2026-08-25 | `V2.1.5.0043` | `94f1a2b` | Requalification du blocage Phase 5 : Django s'execute sous WSL; restent un serveur MySQL local et six echecs preexistants de `tests_scada_package`. |
| 2026-08-25 | `V2.1.5.0042` | `3e87e33` | Phase 4 formellement close (audit + checkpoint); la Phase 5 est bloquee tant que la suite Django de TF100Web ne peut pas s'executer sur ce poste. |
| 2026-08-25 | `V2.1.5.0041` | `5ae5ff4` | Task 4.4 close et Phase 4 terminee; l'adaptateur host TF100Web, le service du contenu et la promotion de capacites restent ouverts (Phases 5 et 6). |
| 2026-08-24 | `V2.1.5.0040` | `d98d753` | Task 4.3 close; le runtime partage est livre inerte et l'adaptateur host reel reste a implementer en Phase 5. |
| 2026-08-24 | `V2.1.5.0039` | `4f690ea` | Task 4.2 close; la compilation reste inatteignable depuis le produit tant que les capacites sont `Blocked`, par conception. |
| 2026-08-24 | `V2.1.5.0038` | `5953265` | Task 4.1 close; les 13 capacites restent `Blocked` et aucune n'est promue avant la Phase 6. |
| 2026-08-24 | `V2.1.5.0037` | `c4f7391` | Task 4.0 close; `SUPPORTED_SCADA_RUNTIME_CAPABILITIES` de TF100Web ne connaît aucune capacité `quick-window.*`, prérequis explicite de la Phase 6. |
| 2026-08-24 | `V2.1.5.0036` | `3560f48` | Phase 3 formellement close (audit + checkpoint); la Phase 4 demarre par la Task 4.0, gate bloquant exigeant TF100Web. |
| 2026-08-24 | `V2.1.5.0035` | `2e86fd9` | Task 3.6 QuickWindow fermee : la Phase 3 authoring est complete cote code; restent le rapport d'audit de phase, l'entree de checkpoint, puis les Phases 4 a 7. |
| 2026-08-24 | `V2.1.5.0034` | `4202a70` | Task 3.5 QuickWindow fermee; seule la surface de reparation `FR-UI-24` (Task 3.6) reste ouverte en Phase 3. |
| 2026-08-24 | `V2.1.5.0033` | `85e088d` | Les trois legs du gate Phase 0 sont rejoués sur Node `24.15.x`; la lacune d'épinglage est fermée. |
| 2026-08-24 | `V2.1.5.0032` | `1fd1d14` | Leg WebView2 réel du gate Phase 0 rejoué sur Node `24.15.0`; seul le leg Edge/TF100Web reste à rejouer. |
| 2026-08-24 | `V2.1.5.0031` | `cd61f0e` | `DEC-0051` : legs WebView2 réel et TF100Web du gate Phase 0 à rejouer sur Node `24.15.x` avant l'entrée en Phase 4. |
| 2026-08-23 | `V2.1.5.0030` | `6c55fdb` | Task 3.4 QuickWindow fermee cote editeur; l'apercu reste editor-only et aucune capacite runtime n'est promue. Le presse-papier inter-contextes (Task 3.5) et la surface de reparation `FR-UI-24` (Task 3.6) restent ouverts. |
| 2026-08-23 | `V2.1.5.0029` | `012135d` | Task 3.3 QuickWindow fermee; le preview/banc d'essai (Task 3.4), le presse-papier inter-contextes (Task 3.5) et la surface de reparation `FR-UI-24` (Task 3.6) restent ouverts. L'authoring `CloseQuickWindow(Self)` reste inaccessible depuis le canvas tant que la projection est en lecture seule. |
| 2026-08-23 | `V2.1.5.0028` | `2fd6c72` | Task 3.2 QuickWindow fermée; l'onglet `Liaisons` des appelants (Task 3.3), le preview/banc d'essai (Task 3.4) et la surface de réparation `FR-UI-24` (Task 3.6) restent ouverts. |
| 2026-08-23 | `V2.1.5.0027` | `ec6e6f7` | Task 3.1 QuickWindow fermée; l'édition du contenu d'une définition sur le canvas reste ouverte (projection en lecture seule) et appartient aux Tasks 3.2 et 3.4. |
| 2026-08-21 | `V2.1.5.0026` | `1452849` | Task 2.4 QuickWindow fermée (versionnement d'Interface locale et statut `Outdated`); la surface WPF de réparation `FR-UI-24` reste ouverte en Task 3.6. |
| 2026-08-13 | `V2.1.5.0022` | `436d38f` | Phase 2 QuickWindow fermée; authoring WPF, preview/compiler/runtime, export/promotion et intake TF100Web des Phases 3 à 7 restent ouverts et `Blocked`. |
| 2026-08-13 | `V2.1.5.0021` | `b353e37` | Phases 0/1 QuickWindow fermées après audit; orchestration, authoring, preview/export et runtime des Phases 2 à 7 restent explicitement ouverts et `Blocked`. |
| 2026-07-17 | `V2.1.4.0063` | Builder `6603992`, TF100Web `f9afcba` | Faux positif des gates agreges ferme : 118 probes independants sont verts; les Blocked et la promotion distante demeurent les seuls gates de cette tranche. |
| 2026-07-16 | `V2.1.4.0062` | `370641d` | Gaps runtime reclasses depuis la matrice generee; seul le deploiement distant et les capabilities Blocked restent ouverts. |
| 2026-07-16 | `V2.1.4.0061` | Builder `c56c5af`/`3fc1fc8`, TF100Web `33c5846` | Gate industriel local ferme; promotion distante/restart et smoke operateur restent ouverts. |
| 2026-07-16 | `V2.1.4.0060` | Builder `22c787f`, TF100Web `6fac468` | Gap parite automatisee ferme; integrations industrielles et promotion restent gatees. |
| 2026-07-16 | `V2.1.4.0059` | TF100Web `2fb46e6` | Gate fixture general ferme; parity preview/export/host et integrations industrielles restent ouvertes. |
| 2026-07-16 | `V2.1.4.0058` | TF100Web `9e85844` | Gap algorithmique composition/cache ferme sur branche; mesure distante industrielle reste requise. |
| 2026-07-16 | `V2.1.4.0057` | TF100Web `c304af3` | Couverture binding generale et fallback mapping fermes sur branche; preuves PLC autorisees et performance restent ouvertes. |
| 2026-07-16 | `V2.1.4.0056` | TF100Web `1fc3ac4` | Gap `DEC-0046` ferme sur branche; bindings exhaustifs, performance, fixture executee et promotion industrielle restent ouverts. |
| 2026-07-16 | `V2.1.4.0055` | TF100Web `cab2733` | HostAdapter unique complete sur branche; lifecycle latest-wins, fixture executee et promotion industrielle restent gates. |
| 2026-07-16 | `V2.1.4.0054` | TF100Web `7d60c63` | Negotiation/hash/capabilities 2.3 completees sur branche; HostAdapter et deploiement restent gates. |
| 2026-07-16 | `V2.1.4.0053` | `bcec075` | Actions objet portables completees; negotiation/HostAdapter TF100Web et preuves de promotion restent ouvertes. |
| 2026-07-16 | `V2.1.4.0052` | `a76e220` | CommandConfig portable complete; host adapter TF100Web et promotion Momentary restent gates end-to-end. |
| 2026-07-16 | `V2.1.4.0051` | `9878fb1` | Trous unitaires Etat/Expression/Effet fermes; promotion animation et preuve TF100Web demeurent gates end-to-end. |
| 2026-07-16 | `V2.1.4.0050` | `c626442` | Fixture partageable Builder creee; execution par TF100Web, negotiation 2.3 et preuves end-to-end restent ouvertes. |
| 2026-07-16 | `V2.1.4.0049` | `f9659ae` | Builder 2.3 strict implemente; negotiation/rejet atomique TF100Web et fixture partagee restent gaps actifs. |
| 2026-07-16 | `V2.1.4.0048` | `684478e` | Matrice runtime generee et verifiee; gaps semantiques bloques et fixture end-to-end par capability encore pending. |
| 2026-07-16 | `V2.1.4.0047` | `9a58d0c` | Registre/analyseur `DEC-0047` implementes; negotiation 2.3, matrice generee, fixture partagee et preuves end-to-end restent gaps actifs. |
| 2026-07-16 | `V2.1.4.0046` | `b2e4f5f` | `DEC-0047` enregistre le gap systemique : absence actuelle de negotiation de capabilities et de preuve exhaustive; mapping absent reclasse fallback non bloquant. |
| 2026-07-16 | `V2.1.4.0045` | `2f4010c` | Ajout du gap confirme navigation/poll de TF100Web `9d5d400`, de la latence de composition et du mapping officiel manquant `YL_E12_HDEG4`. |
| 2026-07-16 | `V2.1.4.0044` | `de37a35`, TF100Web `9d5d400` | Retrait du gap de code polling/gestes des cellules : chemin partage implemente; validation mappings/permissions/feedback PLC reels demeure un gate industriel. |
| 2026-07-16 | `V2.1.4.0043` | `8489dbd` | Retrait du gap Etat/Commande TF100Web : runtime package partage deploye et initialise, mappings de commande collectes; les anciennes actions popup/lifecycle restent distinctes. |
| 2026-07-15 | `V2.1.4.0039` | `ce99ff9` | `DEC-0042` est implemente et valide localement; polling/ecriture/gestes et permissions sur mappings industriels reels restent un gate de livraison autorise. |
| 2026-07-15 | `V2.1.4.0034` | `b75f1d7` | Smoke correctif Tableau/verrou reussi sur copie isolee; le gate performance WebView2 64 x 64 plus large de `DEC-0040` demeure distinct. Baseline : 618 reussites et 5 echecs historiques non lies. |
| 2026-07-15 | `V2.1.4.0028` | `c873744` | Les quatre blocages de validation des surfaces fondamentales ont été corrigés; le smoke WPF/WebView2 complet demeure le seul gate Tableau/verrou restant. |
| 2026-07-15 | `V2.1.4.0027` | `32a3ef6` | Mesures automatisées modèle/rendu 64 x 64 consignées; le gap est réduit au smoke WPF/WebView2 interactif isolé. Baseline complète : 608/613, cinq échecs historiques non liés. |
| 2026-07-15 | `V2.1.4.0026` | `0874416` | Ajout du gate interactif Release 64 x 64 restant avant cloture produit de `DEC-0040`. |
| 2026-07-14 | `V2.1.2.0027` | `fd445ac` | Baseline de tests actualisee apres la correction du crash WPF au demarrage. |
| 2026-07-14 | `V2.1.2.0026` | `50b2ad9` | Ajout des validations manuelles et fonctions avancées de classement/modèles/droits restant hors de la tranche Pages. |
| 2026-06-17 | `V2.1.2.0025` | `58567eb` | Retrait du gap TF100Web pour les masques `DisplayFormat` `#` apres commit `3c795c2`. |
| 2026-06-17 | `V2.1.2.0024` | `49cedc7` | Ajout du gap TF100Web restant pour interpreter les masques `DisplayFormat` de type `##.#`. |
| 2026-06-17 | `V2.1.2.0023` | `3b67c3a` | Ajout du backlog de parite events TF100Web pour preparer la prochaine tranche d'implementation. |
| 2026-06-17 | `V2.1.2.0022` | `3b67c3a` | Retrait du gap TF100Web pour l'intake host-side des events de binding `ValueBindings`; maintien des gaps page-script hors fragment. |
| 2026-06-17 | `V2.1.2.0019` | `bd6515e` | Clarification que l'export `.sb2` ne ferme pas le gap runtime fragment TF100Web. |
| 2026-06-17 | `V2.1.2.0018` | `ad364a6` | Ajout du gap de parite entre runtime exporte SCADA Builder et intake fragment TF100Web. |
| 2026-06-17 | `V2.1.2.0017` | `789a433` | Retrait du gap effets visuels standards; le styling custom reste roadmap. |
| 2026-06-17 | `V2.1.2.0017` | `b465ba9` | Retrait du gap lifecycle runtime global; le chargement de scripts custom reste roadmap. |
| 2026-06-17 | `V2.1.2.0017` | `1b5df61` | Retrait du gap conditions composees et politique degradee simple. |
| 2026-06-17 | `V2.1.2.0017` | `95af4bb` | Retrait du gap politique popup avancee; le placement visuel authorable reste roadmap. |
| 2026-06-17 | `V2.1.2.0016` | `32d9227` | Retrait du gap hover group border; les effets visuels avances restent roadmap. |
| 2026-06-17 | `V2.1.2.0015` | `6ac2245` | Retrait du gap actions popup close/toggle; les politiques avancees restent roadmap. |
| 2026-06-17 | `V2.1.2.0014` | `06652c6` | Retrait du gap `On click -> open popup`; les options avancees de popup restent roadmap. |
| 2026-06-17 | `V2.1.2.0012` | `a73be05` | Retrait du gap d'application runtime des valeurs lues; les reponses degradees restent roadmap. |
| 2026-06-17 | `V2.1.2.0010` | `5302022` | Clarification que les conditions simples sont implementees pour actions objet, tandis que degrade/expressions restent roadmap. |
| 2026-06-17 | `V2.1.2.0009` | `7e3610c` | Retrait du gap binding valeur importe et ajout du gap import protocoles pour creation locale de tags. |
| 2026-06-17 | `V2.1.2.0008` | `f78e8cd` | Remplacement du gap schema tags global par les limites restantes apres import catalogue et `WriteTag`. |
| 2026-06-16 | `V2.1.2.0005` | `5c7d617` | Clarification que les metadonnees hover bouton sont implementees, tandis que l'application runtime appartient a FT100Web. |
| 2026-06-16 | `V2.1.2.0004` | `5c7d617` | Clarification des limites restantes apres la premiere tranche Evenement Element+. |
| 2026-06-16 | `V2.1.1.0039` | `2c5a0b4` | Creation du registre des ecarts connus. |

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
12. The full test suite currently reports 770 passed and 4 failures outside the QuickWindow slice. Three are historical WPF/runtime contract tests (`LegacyContextMenuExposesElementStudioCommand`, `ModernDoubleClickOpensWpfPropertiesDialog`, `ReadOnlyNumericElementsRenderDisplayFormatWhenValueIsMissing`); the fourth is `ReferenceProjectExportsStrict23AndLocksFourIndustrialIntegrations`, where `win00003` currently exports 7 navigation commands while the acceptance expects 8. The targeted QuickWindow, popup-residue and conformance suites are green.
13. Numeric Table cell and Element+ polling/editing now share one policy/controller/cache/formatter. Automated coverage includes read-only, write-only, split mappings, permissions, focus/pending polling, Enter/Escape/change, rejection/offline restore, readback and datatypes. Validation of real operator permissions and confirmed PLC readback still requires an explicitly authorized industrial TF100Web environment before delivery closure.
14. `DEC-0040` code and automated slices are complete. Release measurements on the current machine record model/HTML initial rendering at 367,556 ms, selection inspection p95 at 12,403 ms and Domain resize p95 at 0,023 ms over 100 samples. The focused `DEC-0041` WPF/WebView2 smoke passed on an isolated copy; the separate 64 x 64 browser-composition performance gate remains pending, and automated values must not be presented as WebView2 timings.
15. TF100Web `9d5d400` had the confirmed navigation/poll race observed on `win00008 -> win00012_modern_no_legacy -> win00008`. Branch commit `1fc3ac4` implements `DEC-0046` with abortable generations and mandatory coalesced hydration; an industrial browser smoke after deployment remains required before release closure.
16. Remote page composition previously measured approximately 6.7 s for `win00008` and 14.2 s for `win00012_modern_no_legacy`, while a 426-mapping snapshot measured approximately 0.2 s. TF100Web `9e85844` closes the known algorithmic gap with single-pass injection, bulk catalog resolution, generation/revision caches, phase instrumentation and atomic invalidation. Remote post-deployment measurements on the industrial server remain required; cellular latency remains a separate external factor.
17. The official `tf100web-scada-tags (3).json` audit contains 425 tags but no `YL_E12_HDEG4` or mapping 615. TF100Web `c304af3` now handles this as a non-blocking `data-scada-quality`/`---` diagnostic without fabricated mapping; industrial visual confirmation remains pending.
18. General fixture execution, parity and automated four-page industrial acceptance are complete on the branches. The strict 2.3 industrial artifact passes TF100Web production negotiation and carries machine-readable evidence. Remote deployment/restart, served capability/hash checks and read-only operator smoke remain pending, so this is not yet a remote operator promotion claim.
19. Portable state, expression, effect, command and object-action semantics now have one shared-runtime owner. TF100Web `cab2733` removed the parallel new-host message action switch: canonical direct intents and 2.1/2.2 aliases converge into one adapter. The mutually exclusive legacy per-request diagram loader remains a compatibility surface to retire only with explicit migration evidence. Exporter inline scripts are not a semantic fallback.
20. Shared State/Expression/Effect behavior is complete and table-tested locally, including animation execution. The six `effect.animation*` entries remain `Blocked` because the exact-SHA conformance suite currently proves their strict rejection, not active host rendering. Promotion requires a new three-layer active-rendering gate.
21. Shared CommandConfig behavior is complete locally, including real Momentary cleanup and the canonical host-intent envelope. TF100Web `cab2733` supplies the single protected write/intent adapter, but `command.write.momentary` remains `Blocked` because no authorized press/release plus permission/readback gate promotes it. Top-level aliases preserve 2.1/2.2 compatibility only.
22. Shared object-action behavior is complete locally for all nine current kinds, simple/compound conditions, ordering, propagation and page-scoped targets. TF100Web negotiates manifest 2.3, owns only navigation/popup/write/URL/history services and enforces latest-wins through `1fc3ac4`. Capabilities still listed `Blocked` are intentionally rejected by the exact-SHA suite; each requires an explicit active host-service proof before registry promotion.
23. The prior TF100Web conformance harness grouped 118 Supported ids behind five aggregate family booleans and could therefore pass several unexecuted variants. That validation gap is closed: every id now returns its own `probe:<capability-id>` result, concrete evidence and diagnostic, and an isolated fixture mutation proves independent failure. This does not promote any of the 44 intentionally Blocked capabilities.

24. `DEC-0049` est implémentée et validée par build et tests ciblés. Le parcours interactif WPF complet (créer, modifier, changer de projet avec les trois choix dirty, fermer et rouvrir un récent) reste à exécuter sur une copie isolée avant promotion opérateur.
27. La Phase 3 authoring est close : rapport `docs/superpowers/reports/2026-08-24-quick-window-phase-3-audit.md` et entrée `phase 3` dans `tools/quick-window/checkpoints.json`. Les Phases 4 à 7 restent entièrement ouvertes et toutes les capacités `quick-window.*` restent `Blocked`. Les deux dépôts portent des commits locaux non poussés.

28. La Phase 4 est close : rapport `docs/superpowers/reports/2026-08-25-quick-window-phase-4-audit.md` et entrée `phase 4` dans `tools/quick-window/checkpoints.json`. L'infrastructure de test est complète : la suite Django s'exécute sous WSL Ubuntu — le blocage `fcntl` ne valait que pour l'interpréteur Windows — et le serveur MySQL présent dans la distribution satisfait les suites adossées à la base. `frontend.tests_scada_deploy` et `frontend.tests_scada_page_composition` passent 39/39. Le compte `root@localhost` de ce serveur utilise `auth_socket`, donc les tests doivent être lancés sous l'utilisateur `root` de WSL (`wsl -u root`), qui s'authentifie par la socket Unix; le mot de passe de `tf100web/settings.py` est alors ignoré. `frontend.tests_scada_package` présente 5 échecs et 1 erreur **préexistants**, identiques avec ou sans base et identiques avec les changements Fenêtre rapide mis de côté; ils portent sur des attentes d'assets et de déploiement sans rapport avec ce chantier et sont à traiter pour eux-mêmes. Les Tasks 5.1 à 5.3 sont livrées : TF100Web ingère les registres `QuickWindows[]`/`QuickWindowInvocations[]` et les refuse fail-closed avant activation et déploiement (`quick_window_registry_errors`, contrat §12.6) et les monte derrière son propre adaptateur host, avec le gestionnaire SinglePerDefinition et le service du fragment par namespace (contrat §12.7). La conformance cross-runtime, l'épreuve canary et l'épreuve de rollback sont vertes (contrat §12.8). **Deux étapes de la Task 5.3 restent ouvertes et exigent une décision humaine :** le soak d'au moins 24 h sur le canary et le déploiement en production, tous deux décrits sans être exécutés dans `deploy/developpement/quick_window_canary_runbook.md` (dépôt TF100Web). La Phase 6 ne peut pas s'ouvrir avant. Reste le contenu restant de la Phase 5 : ingestion des registres, chargeur de fragment par namespace avec résolution du CSS aplati, `openQuickWindow`/`closeQuickWindow` ajoutés aux `acceptedKinds` de l'adaptateur host, services d'overlay et de cycle de vie, et appel de `loadQuickWindowRegistries` par la vue. `SUPPORTED_SCADA_RUNTIME_CAPABILITIES` ne doit pas être étendu avant la Phase 6.

26. `DEC-0051` a ré-épinglé le moteur de vérification sur Node `24.15.x`. Les trois legs du gate Phase 0 — Node headless, WebView2 réel et Edge/TF100Web — sont rejoués `PASS` sur `v24.15.0` avec le hash de fixture gelé inchangé, et la fixture vendorisée dans TF100Web est réalignée octet pour octet (LF épinglé, espaces de fin restaurés). La lacune d'épinglage est fermée. Le dépôt TF100Web porte ces corrections sur `codex/quick-window-v1` sans push.

25. `DEC-0050` Phases 0 à 2 sont validées pour l’isolation, les contrats persistants, l’orchestration Application, les dépendances, l’historique et le gate de build. L’authoring WPF (Phase 3), le preview/compilateur/runtime partagé (Phases 4/5), l’export et la promotion de capacités (Phase 6), puis l’intake/acceptance TF100Web (Phase 7) restent non implémentés. Aucun package QuickWindow productible ne doit être émis et toutes les capacités concernées restent `Blocked`.

## 2. Rule

Known gaps must not be documented as implemented behavior.

## 3. TF100Web Event Parity Backlog

The following items are the active correction backlog for TF100Web after the `.sb2` binding-event intake slice:

State/Command polling, AST evaluation, effects, Toggle reads and writes use the implemented shared runtime and are no longer part of this backlog.

1. Promote the TF100Web branch and manifest 2.3 package to the remote server in the documented order.
2. Verify served capability registry, stable/hashed runtime SHA and latest-wins navigation with the read-only operator account.
3. Add popup services for open, close, toggle, placement, focus, multi-instance and host-region behavior behind the existing adapter before promoting their blocked capabilities.
4. Add authorized active-rendering/permission/readback gates before promoting animation, Momentary or host-dependent action capabilities.
5. Preserve strict rejection and active-package rollback for every capability that remains `Blocked`.
