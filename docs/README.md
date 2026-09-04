# SCADA Builder V2 - Documentation Index

Date: 2026-08-11
Status: Active enterprise documentation map
Document version: `V2.1.6.0003`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-09-04 | `V2.1.6.0003` | `58b6018` | Task 7.2 close : acceptation complete verte, ecriture PLC toujours non autorisee et consignee comme gate ouvert. |
| 2026-09-04 | `V2.1.6.0002` | `ac9003a` | Task 7.1 close : verticale moteur win00054 sur catalogue synthetique, projet de reference intact. |
| 2026-09-04 | `V2.1.6.0001` | `511621d` | Task 7.1 bloquee a l'audit : le projet de reference ne porte aucune commande d'ecriture de moteur. |
| 2026-09-04 | `V2.1.6.0000` | `078dbce` | Phase 6 : promotion de onze capacites Fenetre rapide et ouverture de l'export strict 2.3. Deux capacites restent bloquees, faute de preuve. |
| 2026-09-03 | `V2.1.5.0056` | `5716000` | Phase 5 close : mise en service en site industriel reportee a la fin du projet, le deploiement controle tient lieu de preuve de deploiement capable. |
| 2026-09-03 | `V2.1.5.0055` | `63605dc` | Critere d'erreurs console mesure par capture dediee; correctifs de la Task 5.4 publies au canary et conformance rejouee; correction de lecture sur les criteres de fuite (plats et plafonnes, non decroissants). |
| 2026-09-03 | `V2.1.5.0054` | `82ea206` | Soak de 18,37 h accepte sur decision explicite en lieu et place des 24 h; sous-item soak de la Task 5.3 clos, deploiement production toujours requis. La preuve mesuree n'est pas modifiee. |
| 2026-09-03 | `V2.1.5.0053` | `8e61306` | Verdict du soak : quatre criteres verts sur 18,37 h, duree insuffisante et critere d'erreurs non mesure; trois defauts d'instrumentation corriges. Le soak reste a refaire. |
| 2026-09-02 | `V2.1.5.0052` | `9aa93ac` | Preparation de la Phase 6 : inventaire des treize capacites par couche, arithmetique de version et outillage verifies; deux ecarts relevees dans l'enonce. Rien n'est promu. |
| 2026-09-02 | `V2.1.5.0051` | `ea54079` | Audit de Phase 5 ouvert pendant la phase; enregistreur de checkpoint rendu fail-closed sur la branche et la proprete des worktrees. |
| 2026-09-02 | `V2.1.5.0050` | `2c3f451` | Task 5.4 close : composition et isolation legacy prouvees; traversee popup legacy vers Fenetre rapide refusee fail-closed. |
| 2026-09-02 | `V2.1.5.0049` | `fbb1079` | Task 5.4 : chemin Fragment de substitution reel decouvert et corrige; `load_composed_page` composait un namespace `qw-*` comme page. |
| 2026-09-02 | `V2.1.5.0048` | `cdaf03f` | Task 5.3 outillee et soak 24 h lance : paquet de charge elargi, alimentateur Redis, harnais d'endurance et canary WSL reel. |
| 2026-08-25 | `V2.1.5.0047` | `dce0941` | Task 5.3 : conformance cross-runtime des Fenetres rapides, epreuves canary et rollback; soak et production restent a decider. |
| 2026-08-25 | `V2.1.5.0046` | `705077c` | Task 5.2 : adaptateur host TF100Web et gestionnaire SinglePerDefinition livres; capacites toujours `Blocked`. |
| 2026-08-25 | `V2.1.5.0045` | `40e300a` | Task 5.1 : TF100Web ingere et refuse fail-closed les registres Fenetre rapide du manifest 2.3 avant activation et deploiement. |
| 2026-08-25 | `V2.1.5.0044` | `c3cfce9` | MySQL disponible sous WSL : les suites Django adossees a la base passent (39/39); le dernier prerequis d'infrastructure de la Phase 5 est leve. |
| 2026-08-25 | `V2.1.5.0043` | `94f1a2b` | Verification sous WSL : la suite Django de TF100Web s'execute, le blocage `fcntl` est leve; le prerequis reel de la Phase 5 est un serveur MySQL local. |
| 2026-08-25 | `V2.1.5.0042` | `3e87e33` | Phase 4 Fenetres rapides cloturee : rapport d'audit publie et checkpoint versionne enregistre; capacites toujours `Blocked`. |
| 2026-08-25 | `V2.1.5.0041` | `5ae5ff4` | Task 4.4 : premier round-trip reel Builder -> package -> TF100Web execute et verrouille par hash; Phase 4 terminee, capacites toujours `Blocked`. |
| 2026-08-24 | `V2.1.5.0040` | `d98d753` | Task 4.3 : runtime partage Fenetre rapide livre inerte, sans overlay ni chrome; conformance et preuve industrielle regenerees deliberement. |
| 2026-08-24 | `V2.1.5.0039` | `4f690ea` | Task 4.2 : compilation deterministe des Fenetres rapides et gate structurel d'export sans bypass; aucun artefact produit tant qu'une capacite reste `Blocked`. |
| 2026-08-24 | `V2.1.5.0038` | `5953265` | Task 4.1 : 13 capacites granulaires Fenetre rapide enregistrees `Blocked` avec analyse par declencheur propre; matrice generee et index de conformance regeneres. |
| 2026-08-24 | `V2.1.5.0037` | `c4f7391` | Task 4.0 : contrat package et layout déployé des Fenêtres rapides figés avant toute compilation; capacités TF100Web encore inconnues côté runtime. |
| 2026-08-24 | `V2.1.5.0036` | `3560f48` | Phase 3 Fenetres rapides cloturee : rapport d'audit de phase publie et checkpoint versionne enregistre; capacites toujours `Blocked`. |
| 2026-08-24 | `V2.1.5.0035` | `2e86fd9` | Task 3.6 Fenetres rapides livree : surface de reparation des invocations `Outdated`, confirmation d'impact avant modification d'interface et gate de build ferme jusqu'a reparation complete. Phase 3 terminee. |
| 2026-08-24 | `V2.1.5.0034` | `4202a70` | Task 3.5 Fenetres rapides livree : frontiere presse-papier page/fenetre rapide validee fail-closed, refus par defaut et variante `Coller sans liaisons`. |
| 2026-08-24 | `V2.1.5.0033` | `85e088d` | Gate Phase 0 entièrement rejoué sur le moteur épinglé Node `24.15.x`; fixture vendorisée TF100Web réalignée sur le hash gelé. |
| 2026-08-24 | `V2.1.5.0032` | `1fd1d14` | Leg WebView2 réel du gate Phase 0 rejoué sur le moteur épinglé Node `24.15.0`; leg Edge/TF100Web encore ouvert. |
| 2026-08-24 | `V2.1.5.0031` | `cd61f0e` | `DEC-0051` : moteur Node ré-épinglé de `20.18.x` vers `24.15.x`; fixture Phase 0 et hash gelés inchangés, leg Node rejoué `PASS`. |
| 2026-08-23 | `V2.1.5.0030` | `6c55fdb` | Task 3.4 Fenetres rapides livree : module runtime hote adapte du prototype gele, apercu d'instance editor-only avec chrome minimal et banc d'essai dont les valeurs ne sont jamais exportees. |
| 2026-08-23 | `V2.1.5.0029` | `012135d` | Task 3.3 Fenetres rapides livree : commande `OpenQuickWindow` avec cible definition, `CloseQuickWindow(Self)` dans un contenu, onglet conditionnel `Liaisons` et grille de liaisons typees avec statut `Outdated`. |
| 2026-08-23 | `V2.1.5.0028` | `2fd6c72` | Task 3.2 Fenêtres rapides livrée : éditeur `Interface locale` substitué au catalogue de tags, tableau unique groupé et filtré, édition inline et dialogue commun, compteurs d'usages avec navigation et suppression référencée confirmée. |
| 2026-08-23 | `V2.1.5.0027` | `ec6e6f7` | Task 3.1 Fenêtres rapides livrée : groupe projet distinct, contexte d'éditeur borné, duplication, portée d'historique dédiée et projection canvas editor-only en lecture seule. |
| 2026-08-21 | `V2.1.5.0026` | `1452849` | Task 2.4 Fenêtres rapides livrée : versionnement d'Interface locale, réalignement par invocation, statut `Outdated` dérivé et réparation explicite. |
| 2026-08-21 | `V2.1.5.0025` | `abb81d2` | Gate documentaire ramene a zero erreur : 39 plans/specs anterieurs au standard retro-documentes par `tools/docs/backfill-doc-headers.py`. |
| 2026-08-21 | `V2.1.5.0024` | `f881bbe` | Dette documentaire fermee : 493 placeholders `PENDING` resolus vers leur commit introducteur, resolveur `tools/docs/resolve-pending-commits.py` ajoute et branche dans `verify-docs`. |
| 2026-08-21 | `V2.1.5.0023` | `b0159f9` | Audit de complétude Fenêtres rapides avant Phase 3 : spec étendue (`FR-030..036`, `FR-UI-23..26`) et plan complété par Task 2.4, Tasks 3.5/3.6, Task 4.0 prérequis de contrat package, Task 5.4 composition/legacy, blocs de vérification par tâche et checkpoints versionnés. |
| 2026-08-13 | `V2.1.5.0022` | `436d38f` | Phase 2 Fenêtres rapides livrée : orchestration Application, analyse référentielle cycle/profondeur, mutations atomiques, historique workspace et validation build/export fail-closed; capacités runtime toujours `Blocked`. |
| 2026-08-13 | `V2.1.5.0021` | `b353e37` | Audit correctif Fenêtres rapides : Phase 0 validée sur DOM/WebView2/Edge réels; contrats, persistance et handshake de Phase 1 alignés; capacités toujours `Blocked`. |
| 2026-08-11 | `V2.1.5.0020` | `fc5b333` | Phase 1 livrée : modèle QuickWindow (VisualContent, PresentationDefaults, SinglePerDefinition), invocations typées avec anti-injection, retrait fail-closed des kinds popup, persistance atomique quick-windows/ et handshake contractuel Builder→TF100Web. |
| 2026-08-10 | `V2.1.5.0019` | `fc5b333` | Revue renforcée du plan `DEC-0050` : audit popup mesurable, prototype itératif hashé, rollback inter-phase, export sans bypass, handshakes précoces, races/SLA, WebView2 qualifié, canary TF100Web et versioning explicite. |
| 2026-08-10 | `V2.1.5.0018` | `fc5b333` | Ajout du plan d’implémentation `DEC-0050`; sa phase 0 constitue le gate absolu du prototype d’isolation DOM/CSS dans WebView2 et TF100Web avant tout développement de production. |
| 2026-08-10 | `V2.1.5.0017` | `fc5b333` | `DEC-0050` approuve la spécification Fenêtre rapide et supersède les décisions popup Fragment historiques `DEC-0019`, `DEC-0020` et `DEC-0022`; le plan peut maintenant être rédigé avec un gate d’isolation en phase 0. |
| 2026-08-10 | `V2.1.5.0016` | `fc5b333` | La première tranche Fenêtre rapide est une verticale `win00054` complète, de l’authoring au runtime TF100Web, éprouvant deux invocations moteur aux mappings indépendants. |
| 2026-08-10 | `V2.1.5.0015` | `fc5b333` | Le modèle Fenêtre rapide n’ajoute aucun `InstanceKey` distinct : définition, invocation persistante et montage runtime possèdent chacun leur clé canonique. |
| 2026-08-10 | `V2.1.5.0014` | `fc5b333` | Le plan Fenêtre rapide devra être écrit avant le prototype; ce dernier devient sa phase 0 bloquante avant toute modification de production. |
| 2026-08-10 | `V2.1.5.0013` | `fc5b333` | Les kinds `OpenPopup`, `TogglePopup` et `ClosePopup`, jamais complétés bout en bout, seront retirés; les Fenêtres rapides introduisent seulement `OpenQuickWindow`, `CloseQuickWindow` et une référence d’invocation typée. |
| 2026-08-10 | `V2.1.5.0012` | `fc5b333` | Les actions popup legacy et `ScadaPopupOptions` sont explicitement phased-out et exclus de la migration Fenêtre rapide; le choix restant concerne uniquement les commandes modernes `ScadaCommandBinding`. |
| 2026-08-10 | `V2.1.5.0011` | `fc5b333` | La V1 des Fenêtres rapides retire le Toggle moderne : ouverture et fermeture sont explicites; `TogglePopup` reste limité à la compatibilité des Fragments legacy. |
| 2026-08-10 | `V2.1.5.0010` | `fc5b333` | La V1 des Fenêtres rapides conserve des liaisons explicites par invocation; leur copie est une aide d’authoring autonome et undoable, sans preset persistant ni contexte d’équipement. |
| 2026-08-10 | `V2.1.5.0009` | `fc5b333` | Les Fenêtres rapides étendent le manifest 2.3 par capacités granulaires, sans repli 2.1/2.2; TF100Web doit être capable et déployé avant l’activation de leur export Builder. |
| 2026-08-10 | `V2.1.5.0008` | `fc5b333` | L’isolation des Fenêtres rapides modernes utilise une racine DOM scoppée et un namespace stable par définition; l’`iframe` reste limité à l’adaptation legacy opaque et un prototype cross-runtime demeure requis. |
| 2026-08-10 | `V2.1.5.0007` | `fc5b333` | La composition visuelle des Fenêtres rapides est arrêtée : pages et Fenêtres rapides possèdent un `VisualContent` commun borné, sans héritage de domaine ni mélange de leurs responsabilités propres. |
| 2026-08-10 | `V2.1.5.0006` | `fc5b333` | Résolution du contrat de ports requis des Fenêtres rapides : optionnels par défaut, `Required` explicite et build/export bloqué seulement lorsque requis. |
| 2026-08-05 | `V2.1.5.0005` | `fc5b333` | Approbation et consignation des 22 décisions UI Fenêtre rapide couvrant le cadre runtime, le backdrop, la réouverture, l’arborescence, l’Interface locale, les Liaisons, le preview et les suppressions référentielles. |
| 2026-08-05 | `V2.1.5.0004` | `fc5b333` | Consolidation du registre de décisions Fenêtre rapide : entité distincte, Interface locale typée, liaisons par invocation, ports optionnels, instance unique, imbrication bornée, backdrop et choix visuels encore ouverts. |
| 2026-08-04 | `V2.1.5.0003` | `fc5b333` | Ajout du premier brouillon exploratoire sur les popups paramétrés et les mappings typés; l’hypothèse multi-instance a ensuite été écartée pour la première version par `V2.1.5.0004`. |
| 2026-07-30 | `V2.1.5.0002` | `0168f2f` | Les effets Etat de fond et de bordure ciblent désormais explicitement la géométrie SVG visible des formes exportées, avec repli compatible sur le wrapper HTML. |
| 2026-07-29 | `V2.1.5.0001` | `0a961d2` | À l’initialisation du registre, les projets existants sous le répertoire produit `projects/` sont inscrits dans les récents sans ouverture automatique; un retrait reste persistant. |
| 2026-07-29 | `V2.1.5.0000` | `8fe1077` | `DEC-0049` implémentée : accueil sans projet, création et ouverture à racine choisie, sauvegarde/fermeture sûres et projets récents. |
| 2026-07-29 | `V2.1.4.0068` | `9e1c4c1` | Ajout de la spécification approuvée `DEC-0049` et du plan d’implémentation du cycle de vie autonome des projets. |
| 2026-07-18 | `V2.1.4.0067` | `23daac2` | Correction Builder des bindings numeriques divergents : normalisation StateConfig/ValueBindings, validation fail-closed et audit de toutes les pages compilees. |
| 2026-07-17 | `V2.1.4.0066` | `41ccbae` | Ajout du contrat de direction artistique versionnee pour la modernisation des ecrans. |
| 2026-07-17 | `V2.1.4.0065` | `4bee5ab` | Correction du filtre Etat sur SVG opaque : geometrie visuelle sous l'overlay, texte/controles au-dessus et ordre auteur entre objets inchange. |
| 2026-07-17 | `V2.1.4.0064` | `f73b3e3` | `win00012_modern_no_legacy` ajoute les rangees Depart Manuel et Etat du degivrage, avec 14 boutons et 14 voyants rectangulaires sans mapping. |
| 2026-07-17 | `V2.1.4.0063` | Builder `6603992`, TF100Web `f9afcba` | Les 118 capabilities Supported possedent maintenant un probe TF100Web exact, independant et mutation-teste; les operateurs AST lower-camel exportes sont executes par le runtime partage. |
| 2026-07-16 | `V2.1.4.0062` | `370641d` | Contrats, matrice 162 capabilities et preuves Builder/runtime/TF100Web synchronises; promotion distante reservee a la livraison. |
| 2026-07-16 | `V2.1.4.0061` | Builder `c56c5af`/`3fc1fc8`, TF100Web `33c5846` | Package industriel 2.3 exporte et quatre pages gatees sans ecriture PLC. |
| 2026-07-16 | `V2.1.4.0060` | Builder `22c787f`, TF100Web `6fac468` | Parite modele/preview/export/manifest/runtime deploye verrouillee par tests. |
| 2026-07-16 | `V2.1.4.0059` | Builder `90b70eb`, TF100Web `2fb46e6` | Fixture Builder executee dans TF100Web : 118 capabilities supportees et 44 bloquees gatees. |
| 2026-07-16 | `V2.1.4.0058` | Builder `e4776c1`, TF100Web `9e85844` | Composition lineaire instrumentee, caches generationnels et publication/rollback atomiques. |
| 2026-07-16 | `V2.1.4.0057` | Builder `d1270dd`, TF100Web `c304af3` | Bindings numeriques generiques completes : read/write combos, controles edit, formatage partage et fallback mapping. |
| 2026-07-16 | `V2.1.4.0056` | Builder `2343bae`, TF100Web `1fc3ac4` | `DEC-0046` implemente : navigation latest-wins, hydration forcee coalescee et mutations stale rejetees. |
| 2026-07-16 | `V2.1.4.0055` | Builder `e3dd656`, TF100Web `cab2733` | HostAdapter Runtime 1.0 unique : navigation/history/popup/URL/ecriture, validation fail-closed et compatibilite 2.1/2.2. |
| 2026-07-16 | `V2.1.4.0054` | Builder `12a11c2`, TF100Web `7d60c63` | Intake TF100Web 2.3 negocie capabilities, contrat et hash avant remplacement actif; fixture Builder vendoree. |
| 2026-07-16 | `V2.1.4.0053` | `bcec075` | Actions objet portables unifiees : 9 kinds, conditions, propagation, cibles page-scope et bindings DOM canoniques. |
| 2026-07-16 | `V2.1.4.0052` | `a76e220` | CommandConfig partage complete : intents host 1.0, Momentary press/release, confirmations, concurrence et cleanup DOM. |
| 2026-07-16 | `V2.1.4.0051` | `9878fb1` | Runtime partage Etat/Expression/Effet complete : semantiques table-driven, transitions reversibles, animations, tokens et fallback qualite. |
| 2026-07-16 | `V2.1.4.0050` | `c626442` | Fixture de conformance `.sb2` deterministe : 162 capabilities indexees, 118 cas supportes, gaps bloques et SHA-256 partage. |
| 2026-07-16 | `V2.1.4.0049` | `f9659ae` | Builder manifest 2.3 strict par defaut : capabilities triees, SHA-256 runtime, rejet des gaps et profils 2.1/2.2 explicites. |
| 2026-07-16 | `V2.1.4.0048` | `684478e` | Matrice runtime generee depuis le registre et gate stale branche dans `verify-docs`; promotion `Supported` interdite sans trois couches de preuves. |
| 2026-07-16 | `V2.1.4.0047` | `9a58d0c` | Premiere tranche `DEC-0047` : registre type de plus de 100 capabilities, proprietaire/statut/version et analyseur pur avec exhaustivite enum/effet/AST. |
| 2026-07-16 | `V2.1.4.0046` | `b2e4f5f` | `DEC-0047` approuvee : registre exhaustif de capacites, manifest 2.3 negocie, runtime semantique unique et suite de conformance partagee Builder/TF100Web. |
| 2026-07-16 | `V2.1.4.0045` | `2f4010c` | `DEC-0046` approuvee : navigation TF100Web latest-wins, hydratation obligatoire et matrice exhaustive pour `win00003`, `win00004`, `win00008` et `win00012_modern_no_legacy`. |
| 2026-07-16 | `V2.1.4.0044` | `de37a35`, TF100Web `9d5d400` | `DEC-0045` implementee : effets Etat reversibles, filtre sous le contenu et bindings numeriques standards/Tableau reunis dans le cache et le bridge TF100Web partages. |
| 2026-07-16 | `V2.1.4.0043` | `8489dbd` | `DEC-0044` implementee : runtime Etat/Commande TF100Web partage, cible texte semantique et 56 boutons de degivrage relies au bit PLC confirme. |
| 2026-07-16 | `V2.1.4.0042` | `9fd2a30` | Correction du routage `page.properties` : la page cible est ouverte et activee avant le chargement du panneau Page, avec regression dediee. |
| 2026-07-16 | `V2.1.4.0041` | `090d388` | `DEC-0043` implementee et validee : surface InputNumeric unique, identite A1 fiable, fallback Lire/Ecrire explicite et smoke isole reussi. |
| 2026-07-15 | `V2.1.4.0040` | `75f5000` | Approbation de `DEC-0043` : commande unique pour les cellules InputNumeric, identite A1 fiable et fallback Ecrire vers Lire; specification et plan correctifs ajoutes. |
| 2026-07-15 | `V2.1.4.0039` | `ce99ff9` | `DEC-0042` implemente en code : inputs numeriques cellule, manifest 2.2 et intake TF100Web 2.1/2.2; gates industriels et livraison ordonnee encore ouverts. |
| 2026-07-15 | `V2.1.4.0038` | `0086bae` | Integration de la revue du plan `DEC-0042` : nouvelles valeurs `TableEditKind` explicites et justification de `data-scada-step` sur la cible cellule TF100Web. |
| 2026-07-15 | `V2.1.4.0037` | `0086bae` | Approbation de la specification des inputs numeriques lies dans les cellules Tableau, ajout de `DEC-0042` et creation du plan cross-repo ordonnant TF100Web avant l'export `.sb2` 2.2. |
| 2026-07-15 | `V2.1.4.0036` | `0086bae` | Ajout de la specification cross-repo pour les bindings lecture/ecriture des cellules InputNumeric de Tableau dans TF100Web, sans support InputText dans cette tranche. |
| 2026-07-15 | `V2.1.4.0035` | `740796e` | Correction du hit-testing Tableau : les reperes A/1 ne recouvrent plus les cellules, le drag de plage exige un pointeur gauche actif et les scopes d'en-tete partagent le rendu de selection normalise. |
| 2026-07-15 | `V2.1.4.0034` | `b75f1d7` | Implementation de `DEC-0041` : verrou immediat avant preview, modes Tableau deterministes, etat A/1 effectif, payload editor-only teste et smoke WPF/WebView2 isole reussi. |
| 2026-07-15 | `V2.1.4.0033` | `e811253` | Approbation de la specification corrective Tableau/verrou et autorisation de son plan d'implementation. |
| 2026-07-15 | `V2.1.4.0032` | `ff21e33` | Ajout d'une specification et d'un plan correctifs autonomes pour le drag verrouille, les modes Tableau, l'acces aux cellules/pistes et les reperes A/1. |
| 2026-07-15 | `V2.1.4.0031` | `e127190` | Correction du ruban secondaire : hauteur augmentee, barre horizontale native retiree et navigation d'overflow par chevrons. |
| 2026-07-15 | `V2.1.4.0030` | `5d762bb` | Correction des interactions verrou/Tableau et clarification des reperes, fusion contextuelle et origine du format. |
| 2026-07-15 | `V2.1.4.0029` | `bbca8fa` | Modernisation compacte du ruban secondaire : commandes horizontales sur deux rangees, icones et galerie reduites. |
| 2026-07-15 | `V2.1.4.0028` | `c873744` | Correction des surfaces de base validables : Tableau ouvre le ruban contextuel sans modale; verrouillage visible et synchronisé dans Propriété, ruban Sélection, indicateur supérieur et menu contextuel Element+. |
| 2026-07-15 | `V2.1.4.0027` | `32a3ef6` | Cloture automatisee des tranches Tableau manquantes : view models dedies, inspecteur herite/personnalise/mixte, distribution/en-tetes, bridge diagnostique, scenario `win00012`, rendu HTML semantique et mesures Release 64 x 64; smoke WebView2 interactif isole encore requis. |
| 2026-07-15 | `V2.1.4.0026` | `0874416` | Implementation de `DEC-0040` : sous-surface Tableau sans dialogue, modes Objet/Cellules, contenu/format/bordures/pistes/en-tetes avances et verrouillage persistant Element+; validation interactive Release encore requise. |
| 2026-07-15 | `V2.1.4.0025` | `0b1fbf4` | Integration de la revue du plan `DEC-0040` : extraction structurelle, cas conditionnels export, tests de decouplage/resize, retrait controle du dialogue, preuve performance et staging documentaire explicite. |
| 2026-07-15 | `V2.1.4.0024` | `3f6e6a5` | Approbation de la specification d'authoring Tableau/verrouillage Element+, ajout de `DEC-0040` et creation de son plan d'implementation autonome. |
| 2026-07-15 | `V2.1.4.0023` | `18a9e9d` | Revue de la specification Tableau/verrouillage contre le code : migrations explicites, contrat JSON, ruban secondaire, bindings WPF, bordures par segment, auto-fit WebView, performance, classes et tests localises. |
| 2026-07-15 | `V2.1.4.0022` | `3a99b99` | Specification Tableau detaillee : bouton Ajouter, verrouillage persistant de tous les Element+, groupes, multiselection, surfaces partagees et decoupage concret en classes/methodes. |
| 2026-07-15 | `V2.1.4.0021` | `f77aedb` | Creation d'une specification autonome pour les outils UI d'authoring des tableaux, sans modifier la specification approuvee et implementee du Tableau moderne. |
| 2026-07-15 | `V2.1.4.0020` | `42b3105` | Premiere separation du nouveau besoin Tableau dans un document distinct, ensuite remplace par une specification autonome au vocabulaire et au cycle de vie independants. |
| 2026-07-14 | `V2.1.4.0019` | `08affb4` | Premiere redaction de l'extension Tableau, ensuite relocalisee dans une specification distincte afin de respecter son nouveau cycle de vie. |
| 2026-07-14 | `V2.1.4.0018` | `858473c` | Correction du layout commun des dialogues Tableau afin d'afficher leurs champs WPF en plus des boutons d'action. |
| 2026-07-14 | `V2.1.4.0017` | `a94016a` | Compactage du niveau 1 du ruban Inserer afin de rendre le niveau 2 entierement visible dans la hauteur normalisee. |
| 2026-07-14 | `V2.1.4.0016` | `10cfa72` | Implementation du Tableau Element+ moderne, edition type tableur, export `.sb2` sans bindings cellule, et ruban Inserer hierarchique a huit familles. |
| 2026-07-14 | `V2.1.4.0015` | `95a57ac` | Specification Tableau approuvee, `DEC-0039` enregistree et plan d'implementation executable ajoute. |
| 2026-07-14 | `V2.1.4.0014` | `a95addd` | Specification Tableau precisee avec surfaces de proprietes dediees, menu contextuel type tableur, dimensions manuelles, limite validee contre `win00012` et garde-fou strict hors `MainWindow`; une precedence de style detaillee reste a confirmer. |
| 2026-07-14 | `V2.1.4.0013` | `766f8e2` | Specification Tableau precisee : cellules texte ou inputs natifs, sans `ValueBindings` cellule par cellule. |
| 2026-07-14 | `V2.1.4.0012` | `da244d9` | Ajout du routage vers la specification draft du tableau moderne et du ruban Inserer hierarchique. |
| 2026-07-14 | `V2.1.4.0011` | `50b2ad9` | Gestion moderne des pages implémentée; contrats, état, surfaces, diagnostics, couverture et limites synchronisés. |
| 2026-07-14 | `V2.1.4.0010` | `c5d6f0e` | Ajout du routage vers la spécification approuvée et le plan d’implémentation de la gestion moderne des pages. |
| 2026-07-05 | `V2.1.3.0004` | `a49ad78` | Ajout du champ `Component.Provenance` (Legacy/AiModernized) au contrat `.sep` (DEC-0034), avec badge "IA" dans la bibliotheque Element+ des deux applications. |
| 2026-07-05 | `V2.1.3.0003` | `0aa1251` | Ajout du guide de style d'icones SCADA 2026 et du workflow interactif de modernisation Element+ (DEC-0033), en remplacement du pipeline autonome sep-ai-modernizer. |
| 2026-06-19 | `V2.1.3.0002` | `a99b886` | Ajout du color picker moderne pour les couleurs arriere-plan/bordure Style et Bouton Element+. |
| 2026-06-19 | `V2.1.3.0001` | `620e914` | Ajustement de la galerie Formes a des icones 32x32 sans libelles visibles. |
| 2026-06-19 | `V2.1.3.0000` | `b195fe0` | Correction de la galerie Formes du ruban Inserer, ajout Cercle/Triangle/Etoile, et placement Ligne/Fleche en deux points. |
| 2026-06-19 | `V2.1.2.0044` | `c50cbcf` | Extraction de la palette laterale d'outils vers le catalogue semantique d'icones et commandes. |
| 2026-06-19 | `V2.1.2.0043` | `fde1b31` | Cloture de la refonte du ruban superieur par retrait du fallback XAML statique. |
| 2026-06-19 | `V2.1.2.0042` | `0825cfe` | Branchement des commandes de ruban `object.group` et `object.ungroup` sur les workflows Element+ existants. |
| 2026-06-19 | `V2.1.2.0041` | `88a3e8b` | Extraction du catalogue de commandes de ruban dans la couche Application avec couverture de contrat. |
| 2026-06-19 | `V2.1.2.0040` | `335adfb` | Ajout du registre de commandes actif pour le rendu du ruban superieur. |
| 2026-06-19 | `V2.1.2.0039` | `e5f8a82` | Refonte du ruban superieur et normalisation du registre d'icones visibles. |
| 2026-06-19 | `V2.1.2.0038` | `6f76dc8` | Cloture du bloc boutons HMI avec parite metadata preview/export. |
| 2026-06-19 | `V2.1.2.0037` | `2a540d6` | Ajout des evenements runtime explicites pour boutons HMI standards. |
| 2026-06-19 | `V2.1.2.0036` | `8cc4d33` | Ajout du runtime disabled reel pour les boutons Element+. |
| 2026-06-19 | `V2.1.2.0035` | `588d712` | Ajout du runtime d'etat actif pour les boutons Toggle Element+. |
| 2026-06-19 | `V2.1.2.0034` | `61eef34` | Ajout du style appui/actif avance pour les boutons HMI Element+. |
| 2026-06-19 | `V2.1.2.0033` | `89d7165` | Ajout des symboles HMI Element+ interrupteur, disjoncteur, transformateur et balise alarme. |
| 2026-06-18 | `V2.1.2.0032` | `d5ee1fd` | Ajout des proprietes avancees Element+ opacite et rotation. |
| 2026-06-18 | `V2.1.2.0031` | `f6a85ed` | Ajout des symboles HMI Element+ moteur, ventilateur, convoyeur et jauge. |
| 2026-06-18 | `V2.1.2.0030` | `cae57c9` | Ajout des presets de boutons HMI Element+ et du champ exporte `ButtonKind`. |
| 2026-06-18 | `V2.1.2.0029` | `b97ef16` | Ajout des formes process HMI Element+ reservoir, tuyaux, vanne et pompe. |
| 2026-06-18 | `V2.1.2.0028` | `44fbdae` | Ajout des formes HMI Element+ voyant et barres de valeur. |
| 2026-06-18 | `V2.1.2.0027` | `530907a` | Ajout de la tranche formes standards Element+ et insertion de boutons depuis le ruban. |
| 2026-06-17 | `V2.1.2.0026` | `876a6be` | Correction du contrat `DisplayFormat` manifest et alignement TF100Web sur les datatypes de mapping. |
| 2026-06-17 | `V2.1.2.0025` | `58567eb` | Synchronisation du contrat TF100Web apres support des masques `DisplayFormat` `#` dans le runtime `.sb2`. |
| 2026-06-17 | `V2.1.2.0024` | `49cedc7` | Refactor de l'onglet Donnees Element+: `Format affichage` devient le signal actif, `Mapping / Tag`, `Decimales` et `Unite` passent en legacy. |
| 2026-06-17 | `V2.1.2.0023` | `3b67c3a` | Ajout du statut de parite event SCADA Builder V2 / TF100Web et preparation de la prochaine tranche d'implementation. |
| 2026-06-17 | `V2.1.2.0022` | `3b67c3a` | Harmonisation du contrat `.sb2` pour les events de binding TF100Web `ValueBindings`. |
| 2026-06-17 | `V2.1.2.0021` | `1040889` | Correction du feedback `.sb2` pour qu'il soit applique au bon handler d'export. |
| 2026-06-17 | `V2.1.2.0020` | `c2f0b6f` | Correction du validateur CSS `.sb2` et ajout d'un indicateur de progression non bloquant pour l'export FT100. |
| 2026-06-17 | `V2.1.2.0019` | `bd6515e` | Ajout de l'export `.sb2` FT100 avec gate anti-collision DOM/CSS. |
| 2026-06-17 | `V2.1.2.0018` | `ad364a6` | Ajout du contrat d'intake FT100 audite dans TF100Web et de la reference source locale. |
| 2026-06-17 | `V2.1.2.0017` | `789a433` | Ajout des effets visuels runtime standards. |
| 2026-06-17 | `V2.1.2.0017` | `b465ba9` | Ajout du bridge lifecycle runtime global. |
| 2026-06-17 | `V2.1.2.0017` | `1b5df61` | Ajout des groupes de conditions runtime et politique degradee explicite. |
| 2026-06-17 | `V2.1.2.0017` | `95af4bb` | Ajout des options runtime avancees pour popup Fragment. |
| 2026-06-17 | `V2.1.2.0016` | `32d9227` | Ajout des actions runtime de bordure Element+ ciblee. |
| 2026-06-17 | `V2.1.2.0015` | `6ac2245` | Ajout des actions runtime `Fermer popup` et `Basculer popup`. |
| 2026-06-17 | `V2.1.2.0014` | `06652c6` | Ajout de l'action runtime `Ouvrir popup` pour fragments compiles. |
| 2026-06-17 | `V2.1.2.0013` | `4b01460` | Ajout des filtres et du resume de catalogue tags dans l'editeur. |
| 2026-06-17 | `V2.1.2.0012` | `a73be05` | Ajout de l'application runtime des valeurs de tags lues aux Element+ lies. |
| 2026-06-17 | `V2.1.2.0010` | `5302022` | Ajout des actions objet conditionnelles `Afficher`, `Masquer` et `Basculer visibilite`. |
| 2026-06-17 | `V2.1.2.0009` | `7e3610c` | Remplacement de l'authoring `WriteTag` par les bindings Element+ `Lire valeur` et `Ecrire valeur`. |
| 2026-06-17 | `V2.1.2.0008` | `f78e8cd` | Ajout du catalogue tags TF100Web importe au projet et de l'authoring `WriteTag` Element+. |
| 2026-06-16 | `V2.1.2.0007` | `5c7d617` | Ajout du curseur runtime par defaut pour boutons et cibles cliquables FT100/TF100Web. |
| 2026-06-16 | `V2.1.2.0006` | `5c7d617` | Correction de l'export FT100 des events `Clic -> Changer de page` portes par des groupes Element+. |
| 2026-06-16 | `V2.1.2.0005` | `5c7d617` | Ajout des metadonnees hover automatique des boutons Element+, de la tab Bouton et du CSS hover FT100. |
| 2026-06-16 | `V2.1.2.0004` | `5c7d617` | Ajout du registre Evenement Element+ et de la premiere tranche Clic -> Changer de page. |
| 2026-06-16 | `V2.1.2.0003` | `940af93` | Correction du groupement Element+: preservation de l'ordre visuel, hierarchie Element et mouvement solidaire. |
| 2026-06-16 | `V2.1.2.0002` | `2c5a0b4` | Ajout du contrat de groupement scene Element+ only et de l'avertissement conversion legacy. |
| 2026-06-16 | `V2.1.2.0001` | `2c5a0b4` | Correction du raccourci clavier WebView: Backspace ne supprime plus un Element+ selectionne et les champs editables ne declenchent pas les raccourcis scene. |
| 2026-06-16 | `V2.1.2.0000` | `2c5a0b4` | Bump feature pour la conversion dynamique Element+ des boutons legacy, le menu Propriete contextualise et le rendu/export du texte des boutons. |
| 2026-06-16 | `V2.1.1.0039` | `2c5a0b4` | Refonte de l'architecture documentaire en modules, ajout du registre decisionnel, des regles AGENTS, des contrats separes, des diagrammes Mermaid et du workflow de verification documentaire. |
| 2026-06-15 | `V2.1.1.0038` | `841d05a` | Ajout de la roadmap `On click -> open popup` et hover border sur element/groupe. |
| 2026-06-15 | `V2.1.1.0037` | `90c108b` | Ajout de la roadmap de developpement: events, tags TF100Web, Studio Element+, proprietes CSS, effets visuels et scripts globaux. |
| 2026-06-15 | `V2.1.1.0036` | `63c2475` | Generalisation du contrat de namespace CSS/DOM par page pour interdire les collisions de selecteurs en composition TF100Web. |
| 2026-06-15 | `V2.1.1.0035` | `63c2475` | Clarification du scoping CSS par page pour eviter les collisions header/body/footer sur les `data-id`. |
| 2026-06-15 | `V2.1.1.0034` | `63c2475` | Documentation du contrat selection polymorphe et suppression globale source/objet sans masquage durable. |
| 2026-06-15 | `V2.1.1.0033` | `63c2475` | Clarification du contrat de selection source `data-id`, incluant SVG, et du garde-fou inline limite aux couches HTML legacy. |
| 2026-06-15 | `V2.1.1.0032` | `63c2475` | Extension du garde-fou de geometrie inline aux objets source legacy persistants. |
| 2026-06-15 | `V2.1.1.0031` | `63c2475` | Documentation du contrat de composition header/body/footer TF100Web et du garde-fou de geometrie inline FT100. |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Creation de l'arbre documentaire stable, des regles de header et des decisions de deprecation `index.html`. |

## 1. Role

This file is the required entry point for SCADA Builder V2 documentation.

Use it to locate the owner document before editing a contract, plan, status note, or decision. The documentation is now organized by ownership:

1. Governance and decisions.
2. Product objectives.
3. Software architecture.
4. Runtime contracts.
5. Editor contracts.
6. Studio Element+ contracts.
7. UI/UX contracts.
8. Legacy migration policy.
9. Implementation status.
10. Generated code documentation and diagrams.

## 2. Required Reading

Before changing documentation or code that affects documented behavior:

1. Read `docs/AGENTS.md`.
2. Read `docs/00_governance/DECISION_REGISTER_V2.md`.
3. Read the owner document listed below for the touched area.
4. If Studio Element+ selection, hit-testing, movement, grouping, properties, `.sep` export, or regression tests are touched, read `docs/05_studio_element_plus/STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md`.

## 3. Active Documentation Tree

Governance:

1. `00_governance/DOCUMENTATION_STANDARD_V2.md` - mandatory document structure, ownership, Mermaid, code-doc, and verification rules.
2. `00_governance/DECISION_REGISTER_V2.md` - authoritative decision registry; decisions are never deleted when superseded.
3. `00_governance/VERSIONING_AND_CHANGELOG_POLICY_V2.md` - version and history policy.
4. `00_governance/TEAM_WORKFLOW_V2.md` - team workflow for code, docs, decisions, tests, and reviews.
5. `00_governance/DOC_SYNC_SKILL_SPEC_V2.md` - required behavior for the `scada-v2-doc-sync` skill and verification script.

Product and architecture:

1. `01_product/APPLICATION_OBJECTIVES_V2.md` - product objectives and non-negotiable application goals.
2. `02_architecture/GLOBAL_ARCHITECTURE_V2.md` - global software architecture and module boundaries.
3. `02_architecture/APPLICATION_FLOW_V2.md` - end-to-end flow from input to preview, Studio Element+, export, and tests.
4. `02_architecture/MODULE_BOUNDARIES_V2.md` - ownership matrix for Domain, Application, Infrastructure, Rendering, App, and Studio.
5. `02_architecture/DATA_MODEL_OVERVIEW_V2.md` - high-level project, scene, element, event, and package model.

Runtime contracts:

1. `03_runtime_contracts/PREVIEW_BUILD_EXPORT_CONTRACT_V2.md` - preview/build/export parity.
2. `03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md` - normalized package contract.
3. `03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md` - project and scene model contract.
4. `03_runtime_contracts/VERSIONING_CONTRACT_V2.md` - runtime/product version contract.

Editor contracts:

1. `04_editor/COMMANDS_CONTRACT_V2.md` - command registry, ids, enablement, dispatch, and ownership.
2. `04_editor/STATE_MANAGEMENT_CONTRACT_V2.md` - project, scene, selection, dirty state, and undo/redo ownership.
3. `04_editor/ACTIONS_EVENTS_CONTRACT_V2.md` - runtime actions, object events, tags, popup, hover, and scripts.
4. `04_editor/SELECTION_CONTRACT_V2.md` - global SCADA Builder V2 source/object selection contract.
5. `04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md` - ribbon, context menu, panels, and command surfaces.
6. `04_editor/PROPERTIES_PANEL_CONTRACT_V2.md` - property inspector ownership and validation.

Studio Element+:

1. `05_studio_element_plus/STUDIO_ELEMENT_PLUS_ARCHITECTURE_V2.md` - SCADA Builder to Studio flow.
2. `05_studio_element_plus/STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md` - canonical Studio selection contract.
3. `05_studio_element_plus/STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md` - `.sep` package and export boundary.

UI/UX:

1. `06_ui_ux/UI_ARCHITECTURE_V2.md` - UI shell, surfaces, and interaction ownership.
2. `06_ui_ux/UI_SPECIFICATION_V2.md` - active UI specification.
3. `06_ui_ux/ICON_STRATEGY_V2.md` - icon strategy and licensing.
4. `06_ui_ux/RESPONSIVE_MODEL_V2.md` - responsive model.

Legacy migration:

1. `07_legacy_migration/LEGACY_SOURCE_POLICY_V2.md` - legacy source policy, sanitized-source decision, and `win00008`/`win00009` baseline.
2. `07_legacy_migration/REFERENCE_PROJECT_NOTES_V2.md` - reference project notes.
3. `07_legacy_migration/MODERNIZATION_WORKFLOW_V2.md` - modernization workflow.
4. `07_legacy_migration/SCADA_2026_ICON_STYLE_GUIDE_V2.md` - icon visual style guide and junction-point contract for Element+ modernization.
5. `07_legacy_migration/SCREEN_MODERNIZATION_ART_DIRECTION_V2.md` - mandatory versioned visual rules for modernized screen controls, based on the approved `win00008` pattern.

Implementation status:

1. `08_implementation_status/IMPLEMENTED_FEATURES_V2.md` - current implemented features.
2. `08_implementation_status/REGRESSION_COVERAGE_V2.md` - regression coverage map.
3. `08_implementation_status/KNOWN_GAPS_V2.md` - gaps that must not be documented as implemented behavior.

Active specifications and implementation plans:

1. `superpowers/specs/2026-07-14-page-commands-design.md` - implemented architecture and product decisions for modern page management.
2. `superpowers/plans/2026-07-14-page-management-commands.md` - implementation record for page identity, commands, persistence, diagnostics, WPF surfaces, and `.sb2` compatibility; manual isolated-copy UI verification and real-project migration remain gated.
3. `superpowers/specs/2026-07-14-modern-table-and-insert-ribbon-design.md` - implemented `DEC-0039` core design for the model-backed modern table and hierarchical Insert ribbon.
4. `superpowers/specs/2026-07-15-table-ui-authoring-and-element-lock-design.md` - approved `DEC-0040` design for advanced Table UI authoring plus persistent Element+ position locking, group/multiselection semantics, shared lock surfaces, and explicit class/method boundaries.
5. `superpowers/plans/2026-07-15-table-ui-authoring-and-element-lock.md` - implementation record for advanced Table authoring and global Element+ position locking; automated validation is complete and the isolated interactive Release performance gate remains.
6. `superpowers/specs/2026-07-15-table-lock-interaction-regression-correction-design.md` - approved corrective specification for locked drag enforcement, deterministic Table modes, internal cell/track access, and effective A/1 guide state.
7. `superpowers/plans/2026-07-15-table-lock-interaction-regression-correction.md` - approved correction plan derived from the regression specification.
8. `superpowers/plans/2026-07-14-modern-table-and-insert-ribbon.md` - implementation record for the approved and implemented modern table core and hierarchical Insert ribbon.
9. `superpowers/specs/2026-07-15-table-cell-numeric-input-tf100web-design.md` - implemented-in-code `DEC-0042` cross-repository specification for functional numeric inputs inside Table cells; industrial delivery gates remain open.
10. `superpowers/plans/2026-07-15-table-cell-numeric-input-tf100web.md` - executed cross-repository implementation plan with local 2.1/2.2 evidence and TF100Web-first delivery gates still pending.
11. `superpowers/specs/2026-07-15-table-numeric-cell-authoring-correction-design.md` - implemented `DEC-0043` corrective specification for one configuration command, fresh cell identity, A1 display, double-click authoring, and explicit Write-to-Read defaulting.
12. `superpowers/plans/2026-07-15-table-numeric-cell-authoring-correction.md` - executed implementation record for the `DEC-0043` authoring correction, including automated coverage and an isolated `win00012_modern_no_legacy` smoke.
13. `superpowers/specs/2026-07-16-stateful-defrost-toggle-buttons-design.md` - implemented `DEC-0044` contract for confirmed PLC state, shared dynamic text and command mapping dependencies on the 56 defrost Toggle buttons.
14. `superpowers/plans/2026-07-16-stateful-defrost-toggle-buttons.md` - executed cross-repository implementation record for SCADA Builder V2 and TF100Web.
15. `superpowers/specs/2026-07-16-shared-runtime-visual-and-table-binding-correction-design.md` - implemented `DEC-0045` correction for reversible state effects and one TF100Web numeric binding runtime shared by Element+ and Table cells.
16. `superpowers/plans/2026-07-16-shared-runtime-visual-and-table-binding-correction.md` - executed cross-repository correction record and validation evidence.
17. `superpowers/specs/2026-07-16-tf100web-navigation-lifecycle-and-page-performance-design.md` - approved `DEC-0046` design for latest-navigation-wins, mandatory hydration, safe composition caching and exhaustive four-page acceptance.
18. `superpowers/plans/2026-07-16-tf100web-navigation-lifecycle-and-page-performance.md` - superseded execution plan; navigation and performance tasks are folded into the general conformance plan.
19. `superpowers/specs/2026-07-16-scada-v2-tf100web-runtime-conformance-design.md` - approved `DEC-0047` architecture for exhaustive capabilities, manifest 2.3 negotiation and one shared semantic runtime.
20. `superpowers/plans/2026-07-16-scada-v2-tf100web-runtime-conformance.md` - active pending implementation plan covering every currently authorable/exportable runtime family plus the four industrial integration pages.
21. `superpowers/specs/2026-07-29-project-lifecycle-design.md` - approved `DEC-0049` architecture for project creation, fail-closed opening, safe closing, empty-shell startup and recent projects.
22. `superpowers/plans/2026-07-29-project-lifecycle.md` - draft executable implementation plan for generalizing project roots, lifecycle commands, WPF session transitions and compatibility validation.
23. `superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md` - spécification approuvée `DEC-0050` des Fenêtres rapides : entité distincte, Interface locale typée, liaisons d’invocation, instance unique, présentation, cycle de vie, manifest 2.3 et hosting TF100Web.
24. `superpowers/plans/2026-08-10-parameterized-quick-window-management.md` - plan cross-repository dérivé de `DEC-0050`; Phases 0 à 2 validées, Phase 3 en attente et capacités toujours `Blocked`.
25. `superpowers/reports/2026-08-10-quick-window-dom-css-isolation-prototype.md` - preuve de gate Phase 0 corrigée, exécutée dans WebView2 et Edge réels avec fixture gelée `1.0.2`.
26. `superpowers/reports/2026-08-13-quick-window-phase-0-1-implementation-audit.md` - audit des écarts Phases 0/1, corrections appliquées et limites restantes.

Generated documentation:

1. `10_generated/CODE_MAP_V2.md` - generated or verified code map.
2. `10_generated/MODULE_FUNCTION_INDEX_V2.md` - generated public function index and doc coverage.
3. `10_generated/COMMAND_FLOW_DIAGRAM_V2.md` - command flow diagram.
4. `10_generated/STATE_FLOW_DIAGRAM_V2.md` - state flow diagram.
5. `10_generated/EXPORT_FLOW_DIAGRAM_V2.md` - export flow diagram.
6. `10_generated/STUDIO_ELEMENT_PLUS_FLOW_DIAGRAM_V2.md` - Studio Element+ flow diagram.
7. `10_generated/RUNTIME_CAPABILITY_MATRIX_V2.md` - generated strict 2.3 capability, artifact, fixture, and executable-evidence matrix.

## 4. Current Contract Guardrails

These guardrails are active decisions in `00_governance/DECISION_REGISTER_V2.md`:

1. Current FT100/TF100Web exports use root `manifest.json` plus `<page-id>/<page-id>.html`; `index.html` is deprecated for current packages.
2. Preview, build, and export consume the same V2 project model.
3. Editor overlays, layout tools, diagnostics, selection handles, drag rectangles, and test panels must not become runtime/export geometry.
4. `08_web_modernized` is comparison/history material by default and is not raw source of truth without an explicit sanitized-source decision.
5. `win00009` is the known-good comparison baseline; `win00008` is a known divergence/regression candidate.
6. Selection is polymorphic: present source nodes and Element+ scene objects remain selectable according to their contract.
7. Durable source deletion uses scene state and `RemovedSourceElementIds`, not WebView masking or inventory omission.
8. Exported CSS, DOM ids, and runtime action targets are page-namespaced for TF100Web composition.
9. Scene grouping is Element+ only; legacy/source nodes must be converted to Element+ before they can be grouped.
10. Imported TF100Web tags are project-level catalog data; Element+ value bindings use all enabled tags for `Lire valeur`, require writeable tags for `Ecrire valeur`, and export through the FT100/TF100Web manifest/runtime bridge. The editor `Catalogue Tags` panel exposes search, device, datatype, access, and state filters plus a filtered summary.
11. Element+ object visibility actions may be conditioned by imported tag values with deterministic operators; boolean `Vrai/Faux` conditions require boolean tags.
12. Runtime TF100Web can push tag values into read-bound Element+ objects through `window.scadaBuilderSetTagValue(tagId, value, meta)` or the `scada-builder-tag-value` browser event.
13. `DEC-0050` remplace le modèle popup Fragment par des Fenêtres rapides distinctes et typées. Les actions legacy `MountFragment`, `ClosePopup`, `TogglePopup` et `ScadaPopupOptions` sont phased-out, ne prouvent aucune capacité Fenêtre rapide et ne sont pas migrées silencieusement. Les capacités `quick-window.*` restent bloquées jusqu’aux preuves Builder, runtime partagé et TF100Web exigées par `DEC-0047`.
14. Runtime border actions `Afficher bordure`, `Masquer bordure`, and `Basculer bordure` target Element+ objects through the standard page-scoped `scada-runtime-border-highlight` CSS class.
15. Runtime action conditions support optional compound groups with `All` or `Any` mode and explicit missing-tag policy.
16. Exported pages expose `window.scadaBuilderRuntime` and lifecycle events for page ready, action executed, and runtime errors.
17. Standard runtime visual effects include blink, glow, pulse, alarm highlight, and degraded treatment through page-scoped CSS classes.
18. Current TF100Web intake source is `F:\Projet\Git\TF100Web` on branch `codex/adding-table-cell-numeric-input`; as audited through commit `29ebd35`, TF100Web extracts `<div id="ft100-<page-id>">`, loads sibling CSS/assets, deploys and loads the package shared runtime, composes fragments, and initializes value bindings plus `StateConfig`/`CommandConfig`.
19. Inline SCADA Builder scripts emitted outside the page root are not executed by fragment intake. The separately deployed package runtime is executed and owns State/Command behavior; documentation must keep the remaining legacy action gaps distinct.
20. `.sb2` is the preferred FT100 transfer artifact. It is a ZIP archive whose top-level entry is `scada-builder-v2-ft100-package/`.
21. `.sb2` export rewrites legacy source ids under `ft100-<page-id>__legacy-*` before validation, then blocks packages that still contain duplicate DOM ids, unscoped DOM ids, unsafe paths, missing page roots, invalid header/footer references, or generated global CSS selectors that could collide in TF100Web composition.
22. FT100 `.sb2` export must keep the WPF shell responsive and show an indeterminate progress indicator in the bottom status bar while package generation and archive creation are running.
23. `ReadTag` and `WriteTag` are runtime binding events. Current TF100Web `.sb2` intake must consume SCADA Builder V2 `ValueBindings.ReadTagId` and `ValueBindings.WriteTagId`, resolve `tf100.mapping.<id>` to TF100Web mappings, and inject host runtime attributes onto page-scoped Element+ DOM ids.
24. Not every SCADA Builder V2 event family is currently functional in TF100Web. `03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md` owns the event parity matrix and next implementation tranche; `08_implementation_status/KNOWN_GAPS_V2.md` owns the active gap list.
25. Element+ `Donnees` authoring uses `Format affichage` as the active numeric display signal. Hash masks such as `##.#` and `###.#` are exported through `Objects[].Data.DisplayFormat` and interpreted by TF100Web against `RegisterMapping.DataType`: `FLOAT32` and `FLOAT64` round raw values directly, integer datatypes scale by mask decimals, and unknown datatypes fall back to direct rounding. `Mapping / Tag`, `Decimales`, and `Unite` are legacy model fields and are not active authoring controls. `Min` and `Max` are input constraints only for non-read-only numeric inputs.
26. Standard and HMI Element+ shapes created from SCADA Builder V2 persist `ShapeKind` and render/export as Element+-owned SVG content. Standard authoring includes rectangle, ellipse, circle, triangle, star, line, and arrow; line and arrow persist explicit start/end coordinates captured by a two-point Insert workflow. They remain real scene objects; editor-only placement previews, selection overlays, handles, drag rectangles, workzone state, zoom, and pan must not be exported.
27. Element+ state and command events share the deployed TF100Web tag cache and runtime. Button text effects use `[data-scada-text]`; command read/write mapping ids are collected and deduplicated with state and binding dependencies, and Toggle appearance follows the confirmed snapshot rather than an optimistic local state.
28. State effects are non-cumulative transitions: runtime-managed properties are restored to their element baseline before the next effect. Exported basic Shape geometry marks its visible SVG fill and stroke as explicit effect targets, while older/custom markup retains the wrapper-style fallback. Color-filter stacking is isolated inside each Element+ wrapper as visual geometry (`z=0`), runtime overlay (`z=1`), then semantic text and controls (`z=2`); the wrapper's authored sibling order is never changed. TF100Web collects resolved read/write mapping attributes, hydrates a new page with a forced snapshot, and applies one idempotent numeric ValueBinding handler to standard Element+ inputs and Table-cell inputs.
29. `DEC-0046` is the approved pending correction for a confirmed navigation/poll race in TF100Web commit `9d5d400`: body navigation is latest-wins, stale page/snapshot work is invalidated, and the accepted DOM must cross an awaitable hydration barrier even when cached values are unchanged. This target must not be documented as implemented until its behavioral tests and production round-trip smoke pass.
30. `DEC-0047` generalizes runtime delivery: every authorable/exportable capability is registered, declared by manifest 2.3 and either proven end-to-end or blocked before deployment. Portable semantics belong to the shared package runtime; TF100Web supplies host services and must not reimplement a second expression/effect/action engine.

## 5. Decommissioned Legacy Documents

The original top-level Markdown files have been decommissioned as active documentation and moved to:

```text
docs/09_archive/deprecated/
```

They are historical/source material only. They must not receive new active contracts.

Examples:

1. `docs/09_archive/deprecated/ARCHITECTURE_V2.md` -> active content lives in `02_architecture/*`.
2. `docs/09_archive/deprecated/COMMANDS_AND_STATE.md` -> active content lives in `04_editor/COMMANDS_CONTRACT_V2.md` and `04_editor/STATE_MANAGEMENT_CONTRACT_V2.md`.
3. `docs/09_archive/deprecated/PAGE_MANIFEST_OBJECT_ACTIONS_PLAN_V2.md` -> active content lives in `04_editor/ACTIONS_EVENTS_CONTRACT_V2.md` and `08_implementation_status/*`.
4. `docs/09_archive/deprecated/STUDIO_ELEMENT_PLUS_SELECTION_DECISIONS_V2.md` -> active content lives in `05_studio_element_plus/STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md`.

The decommission map is `docs/09_archive/DECOMMISSION_REPORT_V2.md`.

## 6. Validation Commands

Run documentation validation after documentation changes:

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
rg -n "index\.html|08_web_modernized|source_html|Open[ ]Decisions|Document version|Historique des changements|PENDING" docs
```

Run tests when documentation claims implemented behavior changed:

```powershell
dotnet test ScadaBuilderV2.sln --no-restore
```
