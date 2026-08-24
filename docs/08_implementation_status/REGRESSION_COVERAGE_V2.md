# SCADA Builder V2 - Regression Coverage

Date: 2026-08-13
Status: Active regression coverage map
Document version: `V2.1.5.0035`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-08-24 | `V2.1.5.0035` | `PENDING` | Couverture Task 3.6 : liste de reparation, navigation non mutante, reparation unitaire annulable, confirmation d'impact et gate de build ferme jusqu'a reparation complete. |
| 2026-08-24 | `V2.1.5.0034` | `4202a70` | Couverture Task 3.5 : frontiere presse-papier dans les deux sens, duplication, composant de bibliotheque, decision operateur et absence de reference orpheline. |
| 2026-08-24 | `V2.1.5.0033` | `85e088d` | Les trois legs du gate Phase 0 sont rejoués sur le moteur épinglé; épinglage LF de la fixture ajouté des deux côtés. |
| 2026-08-24 | `V2.1.5.0031` | `cd61f0e` | Épinglage du moteur de vérification aligné sur Node `24.15.x` (`DEC-0051`). |
| 2026-08-23 | `V2.1.5.0030` | `6c55fdb` | Couverture Task 3.4 : apercu d'instance namespace, chrome minimal, bundle d'apercu separe de l'export, generations monotones, confinement de la materialisation et semantique hote verifiee en Node. |
| 2026-08-23 | `V2.1.5.0029` | `012135d` | Couverture Task 3.3 : kinds offerts par surface, onglet conditionnel, colonnes et sources typees, statuts, `Outdated` bloquant et transitions d'invocation. |
| 2026-08-23 | `V2.1.5.0028` | `2fd6c72` | Couverture Task 3.2 : panneau `Interface locale`, filtres, édition inline versionnée, usages/navigation, suppression confirmée et catalogue de sélecteurs local. |
| 2026-08-23 | `V2.1.5.0027` | `ec6e6f7` | Couverture Task 3.1 : shell d'authoring, portée d'historique QuickWindow, politique de commandes et projection canvas editor-only. |
| 2026-08-21 | `V2.1.5.0026` | `1452849` | Couverture Task 2.4 : versionnement d'Interface locale, statut `Outdated` dérivé et réparation explicite. |
| 2026-08-13 | `V2.1.5.0022` | `436d38f` | Couverture Phase 2 QuickWindow : services Application, usages/cycles/profondeur, historique atomique et matrice build/export fail-closed. |
| 2026-08-13 | `V2.1.5.0021` | `b353e37` | Couverture Phases 0/1 QuickWindow : DOM réel, WebView2/Edge, validations Domain, persistance atomique, retrait popup et handshake muté dans les deux dépôts. |
| 2026-07-30 | `V2.1.5.0002` | `0168f2f` | Régression effets SVG : cibles preview/export, application fill/stroke, transition, reset de baseline, repli wrapper et fixture conformance régénérée. |
| 2026-07-29 | `V2.1.5.0001` | `0a961d2` | Régression de découverte : seuls les enfants immédiats contenant `project.json` sont inscrits; build solution et 9 tests cycle projet verts. |
| 2026-07-29 | `V2.1.5.0000` | `856398e` | Cycle projet couvert par 8 tests infrastructure/coordinator et contrats ciblés WPF/ruban; build solution vert. Après synchronisation des contrats de test, les cinq échecs historiques hors tranche demeurent. |
| 2026-07-18 | `V2.1.4.0067` | `23daac2` | Regressions de coherence numerique : authoring, migration idempotente, validation bloquante, audit des 26 pages compilees et export cible `win00017`; suite complete observee a 689/697 avec huit echecs hors tranche. |
| 2026-07-17 | `V2.1.4.0065` | `4bee5ab` | Regression `win00008` : filtre visible sur SVG opaque, texte/controles directs ou imbriques au-dessus, `pointer-events:none`, z-index du wrapper inchange et styles descendants restaures. |
| 2026-07-17 | `V2.1.4.0064` | `f73b3e3` | Regression `win00012` couvrant 18 rangees, l'espacement conserve, 14 boutons manuels, 14 voyants Rectangle et l'absence volontaire de mappings. |
| 2026-07-17 | `V2.1.4.0063` | Builder `6603992`, TF100Web `f9afcba` | Couverture 118/118 rendue point par point : resultat unique, evaluateur exact, mutation isolee et operateurs AST serialises executes. |
| 2026-07-16 | `V2.1.4.0062` | `370641d` | Carte synchronisee avec les gates exact-SHA, parite, rollback et acceptance industrielle sans ecriture PLC. |
| 2026-07-16 | `V2.1.4.0061` | Builder `c56c5af`/`3fc1fc8`, TF100Web `33c5846` | 1 export industriel long et 8 gates TF verts; 03/04/08/12, hashes et capabilities couverts. |
| 2026-07-16 | `V2.1.4.0060` | Builder `22c787f`, TF100Web `6fac468` | 12 tests Builder cibles et 6 gates TF fixture verts; parite et runtime deploye exact verrouilles. |
| 2026-07-16 | `V2.1.4.0059` | TF100Web `2fb46e6` | 5 gates fixture verts; 118 Supported executes, 44 Blocked rejetes, SHA/composition/runtime verifies. |
| 2026-07-16 | `V2.1.4.0058` | TF100Web `9e85844` | 17 tests composition/deploiement/performance verts; single-pass, revisions, generation et rollback couverts. |
| 2026-07-16 | `V2.1.4.0057` | TF100Web `c304af3` | 23 tests JS et 20 contrats Django verts; bindings read/write, edit, formats/datatype et mapping absent couverts. |
| 2026-07-16 | `V2.1.4.0056` | TF100Web `1fc3ac4` | 16 tests JS host/lifecycle/hydration et 19 tests contrat Django verts; ordre inverse, timeout, reprise et forced poll couverts. |
| 2026-07-16 | `V2.1.4.0055` | TF100Web `cab2733` | 7 tests JS HostAdapter et 16 tests contrat runtime Django verts : intents, origine, doublons, stale scope, URL/ecriture/permissions. |
| 2026-07-16 | `V2.1.4.0054` | TF100Web `7d60c63` | 11 tests negotiation/fixture cibles verts; check Django local vert; suite module expose des echecs historiques hors tranche. |
| 2026-07-16 | `V2.1.4.0053` | `bcec075` | 53 tests runtime JS et 98 tests cibles verts; suite 679/684 avec cinq echecs historiques; fixture `fb06431e...08404`. |
| 2026-07-16 | `V2.1.4.0052` | `a76e220` | 47 tests runtime JS et 113 tests .NET cibles verts; suite 678/683, cinq echecs historiques; fixture `4381347c...40a6`. |
| 2026-07-16 | `V2.1.4.0051` | `9878fb1` | 35 tests runtime JS et 113 tests .NET cibles verts; suite complete 678/683, cinq echecs historiques; hash fixture `6976e192...15ef`. |
| 2026-07-16 | `V2.1.4.0050` | `c626442` | 3 tests de conformance et 94 tests cibles verts; package byte-identique et SHA-256 canonique verifies; suite complete 678/683, cinq echecs historiques. |
| 2026-07-16 | `V2.1.4.0049` | `f9659ae` | 84 tests FT100 exporter/package verts : 2.3, requirements, hash, tamper, blocked et profils compatibles; suite complete 675/680, cinq echecs historiques. |
| 2026-07-16 | `V2.1.4.0048` | `684478e` | Matrice de 162 capabilities generee depuis le code; `verify-docs` rejette staleness et support sans preuves trilaterales. |
| 2026-07-16 | `V2.1.4.0047` | `9a58d0c` | 7 regressions `RuntimeContracts` couvrent catalogue, exhaustivite enum/effet/AST, gaps bloques, analyse et deduplication. |
| 2026-07-16 | `V2.1.4.0046` | `b2e4f5f` | Plan `DEC-0047` : matrice generee, tests d'exhaustivite, fixture `.sb2` partagee et preuves end-to-end requises pour chaque capability. |
| 2026-07-16 | `V2.1.4.0044` | `de37a35`, TF100Web `9d5d400` | Couverture `DEC-0045` : restauration fallback, overlay sous contenu, collecte mappings resolus, snapshot force et binding numerique commun idempotent. |
| 2026-07-16 | `V2.1.4.0043` | `8489dbd` | Ajout de la couverture `DEC-0044` pour les 56 toggles, le texte semantique exporte, les effets true/false et la collecte TF100Web des mappings de commande. |
| 2026-07-16 | `V2.1.4.0042` | `9fd2a30` | Regression `page.properties` : ouverture, activation et selection de la page cible sans mutation, dirty state ni historique; suite complete 659/664 avec cinq echecs historiques inchanges. |
| 2026-07-16 | `V2.1.4.0041` | `6afe427` | Couverture `DEC-0043` pour A1, provenance de selection, commande/dialogue unique, fallback Lire/Ecrire, double-clic, round-trip/export et smoke isole; suite complete 658/663 avec cinq echecs historiques inchanges. |
| 2026-07-15 | `V2.1.4.0039` | `ce99ff9` | Couverture `DEC-0042` Domain/Application/WPF/rendu/export/validation et intake TF100Web; suite SCADA 645/650 avec cinq echecs historiques inchanges. |
| 2026-07-15 | `V2.1.4.0035` | `740796e` | Regression du hit-testing cellule Tableau : guides A/1 externes, drag primaire explicite, annulation pointeur, normalisation des plages et rendu commun des scopes rangee/colonne. |
| 2026-07-15 | `V2.1.4.0034` | `b75f1d7` | Couverture `DEC-0041` du payload reel, etat Tableau atomique, refus avant preview, resize verrouille, guide A/1 et absence d'artefact export; smoke WPF/WebView2 isole reussi. |
| 2026-07-15 | `V2.1.4.0031` | `e127190` | Regression XAML du ruban secondaire : hauteur anti-clipping, scrollbar masquee et chevrons de pagination; 14 tests ruban reussis, suite complete inchangee a 614 reussites et 5 echecs historiques. |
| 2026-07-15 | `V2.1.4.0030` | `5d762bb` | Couverture verrou avant preview, gestes Tableau, reperes A/1, toggle fusion et format; suite complete observee a 614 reussites et 5 echecs historiques non lies. |
| 2026-07-15 | `V2.1.4.0029` | `bbca8fa` | Regression XAML du ruban secondaire compact : dimensions, disposition horizontale, troncature et groupes sur deux rangees. |
| 2026-07-15 | `V2.1.4.0028` | `c873744` | Régressions ciblées pour l'entrée Tableau sans `ShowDialog`, la visibilité des contrôles de verrou et l'action contextuelle Verrouiller/Déverrouiller. |
| 2026-07-15 | `V2.1.4.0027` | `32a3ef6` | Ajout des suites dédiées inspecteur, bridge, performance 64 x 64 et intégration `win00012`; suite complète observée à 608 réussites et 5 échecs historiques non liés. |
| 2026-07-15 | `V2.1.4.0026` | `0874416` | Couverture `DEC-0040` : lock/persistance/groupes/guard, session Tableau, conversions, formats, bordures, pistes/en-tetes, architecture WebView 64x64 et export sans artefact editeur. |
| 2026-07-14 | `V2.1.4.0018` | `858473c` | Couverture du layout type des dialogues Tableau et de l'affichage de leurs controles WPF concrets. |
| 2026-07-14 | `V2.1.4.0017` | `a94016a` | Ajout de la regression garantissant un niveau 1 Inserer compact, independant du style 58 px des commandes de niveau 2. |
| 2026-07-14 | `V2.1.4.0016` | `10cfa72` | Couverture Tableau : modele/limites/precedence, operations, coordinator, clipboard, menu, architecture hors MainWindow, persistance scene, rendu HTML et archive `.sb2`; couverture des huit familles Inserer. |
| 2026-07-14 | `V2.1.4.0008` | `34dfc82` | Ajout de la couverture du clic droit sur une page lorsque la cible est un contenu inline WPF `Run`. |
| 2026-07-14 | `V2.1.4.0007` | `fdcd11e` | Couverture des dimensions et de la marge interne de l'icone Nouvelle page. |
| 2026-07-14 | `V2.1.4.0006` | `cc670c1` | Couverture du libelle Recherche, des filtres initiaux Default/Tous et de l'icone partagee Nouvelle page. |
| 2026-07-14 | `V2.1.4.0005` | `fd445ac` | Ajout de la regression interdisant les liaisons `Run.Text` TwoWay implicites vers les proprietes Pages et Diagnostics en lecture seule. |
| 2026-07-14 | `V2.1.4.0004` | `50b2ad9` | Ajout de la couverture identité, commandes, historique, sauvegarde atomique, pages natives, surfaces Pages/Diagnostics et cycle `.sb2` complet. |
| 2026-07-13 | `V2.1.4.0003` | `b954d46` | Couverture des nouveaux champs de style, export CSS, preview WebView, icônes sémantiques et preuve d’intake TF100Web. |
| 2026-07-06 | `V2.1.3.0002` | `4dfe7fe` | Ajout de la couverture de la poignée de rotation Element+ et des presets/angle personnalisé du menu contextuel (7 tests WebViewContextMenuScriptTests). |
| 2026-06-19 | `V2.1.3.0001` | `620e914` | Ajout de la couverture icon-only 32x32 pour la galerie Formes. |
| 2026-06-19 | `V2.1.3.0000` | `b195fe0` | Ajout de la couverture galerie Formes, formes standard completes, et placement Ligne/Fleche en deux points. |
| 2026-06-19 | `V2.1.2.0044` | `c50cbcf` | Ajout de la couverture de la palette laterale d'outils issue du catalogue semantique. |
| 2026-06-19 | `V2.1.2.0043` | `fde1b31` | Ajout de la couverture interdisant le retour des anciens rubans XAML statiques. |
| 2026-06-19 | `V2.1.2.0042` | `0825cfe` | Ajout de la couverture du dispatch de ruban pour `object.group` et `object.ungroup`. |
| 2026-06-19 | `V2.1.2.0038` | `6f76dc8` | Ajout de la couverture de parite metadata preview/export pour boutons Element+. |
| 2026-06-19 | `V2.1.2.0037` | `2a540d6` | Ajout de la couverture des evenements runtime pour boutons HMI standards. |
| 2026-06-19 | `V2.1.2.0036` | `8cc4d33` | Ajout de la couverture du runtime disabled reel pour boutons Element+. |
| 2026-06-19 | `V2.1.2.0035` | `588d712` | Ajout de la couverture du runtime on/off pour boutons Toggle Element+. |
| 2026-06-19 | `V2.1.2.0034` | `61eef34` | Ajout de la couverture du style appui/actif pour les boutons Element+. |
| 2026-06-19 | `V2.1.2.0033` | `89d7165` | Ajout de la couverture des symboles HMI Element+ electriques et alarme. |
| 2026-06-18 | `V2.1.2.0032` | `d5ee1fd` | Ajout de la couverture des proprietes avancees Element+ opacite et rotation. |
| 2026-06-18 | `V2.1.2.0031` | `f6a85ed` | Ajout de la couverture des symboles HMI Element+ moteur, ventilateur, convoyeur et jauge. |
| 2026-06-18 | `V2.1.2.0030` | `cae57c9` | Ajout de la couverture des presets de boutons HMI Element+ et du champ exporte `ButtonKind`. |
| 2026-06-18 | `V2.1.2.0029` | `b97ef16` | Ajout de la couverture des primitives process HMI Element+ reservoir, tuyaux, vanne et pompe. |
| 2026-06-18 | `V2.1.2.0028` | `44fbdae` | Ajout de la couverture des primitives HMI Element+ voyant et barres de valeur. |
| 2026-06-18 | `V2.1.2.0027` | `530907a` | Ajout de la couverture des formes standards Element+ et de l'insertion manuelle des boutons Element+. |
| 2026-06-17 | `V2.1.2.0025` | `58567eb` | Ajout de la validation TF100Web du formatage runtime `DisplayFormat` hash mask. |
| 2026-06-17 | `V2.1.2.0024` | `49cedc7` | Ajout de la couverture du refactor Donnees Element+ et du masque numerique `DisplayFormat`. |
| 2026-06-17 | `V2.1.2.0022` | `3b67c3a` | Ajout de la couverture TF100Web des events de binding `ValueBindings` issus du `.sb2`. |
| 2026-06-17 | `V2.1.2.0020` | `c2f0b6f` | Ajout de la couverture du validateur CSS `.sb2` avec selecteurs page-scopes indentes. |
| 2026-06-17 | `V2.1.2.0019` | `bd6515e` | Ajout du validateur `.sb2` FT100 a la carte de couverture et test archive cible. |
| 2026-06-17 | `V2.1.2.0018` | `ad364a6` | Ajout de la reference aux tests d'intake TF100Web audites. |
| 2026-06-17 | `V2.1.2.0017` | `789a433` | Ajout de la couverture domaine, persistance et export pour effets visuels runtime. |
| 2026-06-17 | `V2.1.2.0017` | `b465ba9` | Ajout de la couverture export pour le bridge lifecycle runtime global. |
| 2026-06-17 | `V2.1.2.0017` | `1b5df61` | Ajout de la couverture domaine, validation, persistance et export pour conditions composees. |
| 2026-06-17 | `V2.1.2.0017` | `95af4bb` | Ajout de la couverture domaine, validation, persistance et export pour options popup avancees. |
| 2026-06-17 | `V2.1.2.0016` | `32d9227` | Ajout de la couverture domaine, validation, persistance et export pour les actions bordure Element+. |
| 2026-06-17 | `V2.1.2.0015` | `6ac2245` | Ajout de la couverture domaine, persistance et export pour `Fermer popup` et `Basculer popup`. |
| 2026-06-17 | `V2.1.2.0014` | `06652c6` | Ajout de la couverture domaine, persistance, validation et export pour `Ouvrir popup`. |
| 2026-06-17 | `V2.1.2.0013` | `4b01460` | Ajout de la couverture de contrat pour le panneau `Catalogue Tags` filtre. |
| 2026-06-17 | `V2.1.2.0012` | `a73be05` | Ajout de la couverture export runtime pour l'application des valeurs `Lire valeur`. |
| 2026-06-17 | `V2.1.2.0010` | `5302022` | Ajout de la couverture actions objet conditionnelles, validation build et export runtime. |
| 2026-06-17 | `V2.1.2.0009` | `7e3610c` | Ajout de la couverture bindings `Lire valeur` et `Ecrire valeur`, validation build et export runtime. |
| 2026-06-17 | `V2.1.2.0008` | `f78e8cd` | Ajout de la couverture import tags TF100Web, persistance catalogue et export `WriteTag`. |
| 2026-06-16 | `V2.1.2.0007` | `5c7d617` | Ajout de la couverture du curseur runtime par defaut des cibles cliquables FT100. |
| 2026-06-16 | `V2.1.2.0006` | `5c7d617` | Ajout de la couverture des wrappers runtime transparents pour events de groupe Element+. |
| 2026-06-16 | `V2.1.2.0005` | `5c7d617` | Ajout de la couverture metadonnees hover automatique, CSS FT100 et disabled des boutons Element+. |
| 2026-06-16 | `V2.1.2.0004` | `5c7d617` | Ajout de la couverture du registre evenements Element+ et du bouton Evenement de l'editeur double-clic. |
| 2026-06-16 | `V2.1.2.0003` | `940af93` | Ajout de la couverture pour ordre visuel, inventaire hierarchique et mouvement solidaire des groupes Element+. |
| 2026-06-16 | `V2.1.2.0002` | `2c5a0b4` | Ajout de la couverture regression pour le groupement de scene Element+ only. |
| 2026-06-16 | `V2.1.2.0001` | `2c5a0b4` | Ajout de la couverture regression du raccourci Backspace non destructif et du garde-fou clavier pour champs editables. |
| 2026-06-16 | `V2.1.2.0000` | `2c5a0b4` | Ajout de la couverture regression pour conversion Button, Propriete contextuelle et rendu/export du texte des boutons. |
| 2026-06-16 | `V2.1.1.0039` | `2c5a0b4` | Creation de la carte de couverture regression. |

## 1. Current Test Baseline

```text
dotnet test ScadaBuilderV2.sln --no-restore
770 passed, 4 failed, 0 skipped
```

Les 67 tests QuickWindow/legacy-popup ciblés, les 4 tests de fixture conformance et les gates Phase 0 cross-repository sont verts. Les quatre échecs de suite complète restent hors tranche: trois contrats historiques WPF/runtime déjà recensés et l’acceptance industrielle `win00003` dont la fixture de référence expose 7 navigations au lieu des 8 attendues.

## 2. Coverage Map

| Contract area | Primary tests |
| --- | --- |
| Fenêtres rapides Phase 0 — isolation (`DEC-0050`) | `tests/runtime-js/quick-window-dom-css-isolation.test.mjs` verrouille Node `24.15.x` (`20.18.x` avant `DEC-0051`), révision/hash et 100 cycles; `QuickWindowIsolationPrototypeContractTests` exige une capture WebView2 réelle. TF100Web exécute la même fixture/hash avec son test Node et `tests_scada_quick_window_isolation_prototype.py` dans Edge headless. |
| Fenêtres rapides Phase 1 — contrats et handshake (`DEC-0050`) | `QuickWindowDomainTests`, `QuickWindowBindingTests`, `QuickWindowStoreTests` et `QuickWindowContractHandshakeTests` couvrent familles/type/accès, anti-injection, version/profil, fermeture contextuelle, persistance déterministe/atomique et SHA canonique. TF100Web `tests_scada_quick_window_contract_handshake.py` parse le manifest et exécute les mutations clés/type/ordre/profil/required/legacy. |
| Fenêtres rapides Phase 3.1 — shell d'authoring (`DEC-0050`, `FR-033`, `FR-035`) | `QuickWindowShellContractTests` couvre la portée d'historique `QuickWindow`, la pile unique sur une alternance page/fenêtre, l'activation du contexte cible, le refus non destructif d'une action non résoluble, le masquage des commandes page-only, le contrat XAML du groupe projet, la garde d'architecture de `MainWindow.xaml.cs`, la projection canvas editor-only non exportable et la fermeture du handler de canvas en lecture seule. |
| Fenêtres rapides Phase 3.2 — Interface locale (`DEC-0050`, `FR-UI-15`, `FR-UI-16`, `FR-UI-17`, `FR-UI-20`, `FR-031`) | `QuickWindowInterfaceAuthoringTests` couvre le tableau unique groupé public/privé, les filtres famille/texte, la répartition inline/avancée et le contrat XAML du panneau et du dialogue, la cohérence des règles typées à l'édition inline, l'incrément d'`InterfaceVersion` limité aux changements de contrat public, le refus d'une édition invalide, les compteurs d'usages et la navigation non mutante, la suppression référencée confirmée ou annulée, les statuts gris/rouge des ports non liés et le catalogue de sélecteurs restreint aux membres locaux. |
| Fenêtres rapides Phase 3.3 — appelants et liaisons (`DEC-0050`, `FR-UI-18`, `FR-UI-19`, `FR-UI-20`, `FR-UI-24`) | `QuickWindowBindingAuthoringTests` couvre les kinds offerts par une page et par un contenu de Fenetre rapide, l'absence de cible page et de kind Toggle, l'onglet `Liaisons` conditionnel, les colonnes typees, le choix de source suivi de son selecteur contextuel, l'effacement de la valeur au changement de source, les statuts gris/rouge, la validation des ports requis, l'affichage `Outdated` avec blocage build/export, l'enregistrement atomique invocation + commande appelante, l'independance de deux invocations et la suppression de l'appelant. |
| Fenêtres rapides Phase 3.4 — apercu d'instance (`DEC-0050`, `FR-020`, `FR-021`, `FR-UI-07`, `FR-UI-08`, `FR-UI-21`) | `QuickWindowPreviewTests` couvre le rendu namespace d'une instance, l'isolation DOM/CSS entre deux definitions partageant les memes ids d'auteur, le chrome minimal, l'absence de donnees de banc d'essai dans un document exportable, le bundle d'apercu porteur du gestionnaire hote alors que le bundle exporte ne l'est pas, les generations monotones de l'adaptateur, le confinement de la materialisation et la nature transitoire du banc. `tests/runtime-js/quick-window-host.test.mjs` verifie la semantique hote elle-meme : meme invocation remise au premier plan, autre invocation fermee/disposee/recreee, absence de fuite de mapping, `X`/`Escape`/`Self`, backdrop partage non fermant, rejet des cycles et de la profondeur, cascade de fermeture. |
| Fenêtres rapides Phase 3.5 — frontière presse-papier (`DEC-0050`, `FR-031`, `FR-034`, `FR-036`, `FR-UI-23`) | `QuickWindowClipboardTests` couvre le tag projet collé dans une fenêtre rapide, le membre d'Interface locale collé sur une page, le port d'une autre définition, la référence non résoluble, l'invocation dont la cible a disparu, l'invocation déjà possédée par un autre appelant, les enfants d'un groupe collé, le composant de bibliothèque instancié sur un canvas de fenêtre rapide, les deux seules issues offertes à l'opérateur, l'absence de dialogue quand la frontière est respectée, le retrait des références sans promotion ni référence orpheline, et l'origine portée par le presse-papier partagé. |
| Fenêtres rapides Phase 3.6 — réparation des invocations `Outdated` (`DEC-0050`, `FR-032`, `FR-UI-24`) | `QuickWindowInterfaceVersioningTests` couvre la liste de réparation avec page, appelant, commande, couple de versions et motif, la navigation non mutante vers l'appelant, la réparation unitaire qui ne répare jamais les autres en masse, le gate de build qui ne se referme qu'une fois toutes les invocations réparées, la réparation comme transition unique réversible, la confirmation d'impact affichant le nombre d'invocations avant application et son refus qui laisse la définition inchangée, l'absence d'interruption quand rien ne casse, et le contrat XAML de la surface sans réparation en masse. |
| Fenêtres rapides Phase 2.4 — versionnement d'Interface locale (`DEC-0050`, `FR-032`) | `QuickWindowInterfaceVersioningTests` couvre le renommage sans increment, la neutralité des membres privés, le réalignement sur ajout optionnel, le statut `Outdated` sur ajout `Required`, retrait de port lié et changement de type, la préservation des liaisons, le blocage build/export et la réparation explicite (réussie et refusée). |
| Fenêtres rapides Phase 2 — Application, historique et build (`DEC-0050`) | `QuickWindowApplicationTests` couvre création/édition/suppression fail-closed, navigation vers usages, définitions manquantes, versions, ports retirés, cycle et profondeur; `QuickWindowHistoryTests` couvre appelant/commande/invocation/liaisons, sélection, dirty state et deux invocations indépendantes; `QuickWindowBuildValidationTests` couvre required, mapping/type/accès, injection, profil, présentation, capacité bloquée et absence de binding fabriqué. `EditorHistoryServiceTests` verrouille aussi la conservation de la pile lorsqu’un restore échoue. |
| Numeric `StateConfig.ReadVariable` / `ValueBindings.ReadTagId` coherence | `ScadaSceneElementEventsTests.WithElementStateConfigSynchronizesNumericReadVariableWithCanonicalValueBinding`, `ModernProjectStoreTests.SceneMigrationRepairsPersistedNumericReadBindingMismatch`, `OfficialSceneDomainTests.BuildValidationRejectsNumericReadVariableValueBindingMismatch`, and `IndustrialRuntimeIntegrationTests.ReferenceProjectNormalizesEveryCompiledNumericReadBindingAndExportsWin00017Mappings` cover authoring, migration, fail-closed validation, all compiled reference pages and exact `win00017` export mappings. |
| Runtime capability completeness (`DEC-0047`) | `RuntimeContracts/ScadaRuntimeCapabilityCatalogTests.cs` and `ScadaRuntimeCapabilityAnalyzerTests.cs` cover typed inventory, artifacts, fixture ids, three-layer evidence requirements and model analysis. `RuntimeConformancePackageTests.cs` proves exact 118-capability factory coverage, 118 unique `probe:<capability-id>` results, byte-identical package regeneration, canonical SHA `b5e4ea7fe32a928fd27b4ac1531d6940b887804662585892628340e2d6b1cf48`, archive/manifest/DOM/CSS/runtime integrity, sanitization and an exhaustive 162-entry expectation index. Runtime JS suites add table-driven AST/state/effect/command/action semantics, including the lower-camel operators actually emitted. TF100Web `frontend.tests_runtime_conformance` executes and reports every exact Supported probe, mutation-tests independent failure, and rejects every Blocked id. `tools/docs/generate-runtime-capability-matrix.ps1` plus `verify-docs` enforce code/matrix parity. |
| Shared command and input semantics (`DEC-0047`, partial) | Builder `tests/runtime-js/command-dispatcher.test.mjs` covers all five triggers, seven kinds, Toggle/SetFixed/SetFromInput and real Momentary phases, confirmation ordering, disabled/missing values, canonical intents, HostAdapter precedence, async rejection and duplicate suppression. TF100Web `frontend/tests_runtime_js/host-adapter.test.mjs` covers canonical service mapping, invalid input, duplicate delivery, origin, stale declared page and protected writes. End-to-end Momentary/readback promotion remains pending. |
| Shared object-action semantics (`DEC-0047`) | Builder `tests/runtime-js/action-dispatcher.test.mjs` covers all nine kinds, every condition operator, All/Any, both missing policies, binding order, prevent/stop propagation, disabled sources, disposal and duplicate ids across composed page roots. `Ft100SceneExporterTests.cs` locks canonical registries/bindings and scope. TF100Web `cab2733` removes the parallel message switch and routes action-owned host intents into one adapter; the exact-SHA suite enforces Supported execution and Blocked rejection. |
| Latest-wins navigation and hydration (`DEC-0046`) | TF100Web `frontend/tests_runtime_js/navigation-lifecycle.test.mjs` covers supersession, inverse completion, timeout ownership, stale settle, offline/retry and generation-gated mutation symbols. `tag-cache-hydration.test.mjs` covers forced-during-in-flight follow-up, force coalescing, unchanged-value notification, dependency recollection and stale snapshot rejection. `ScadaRuntimeInitContractTests` locks the deployed integration; 16 JS and 19 Django contract tests are green. |
| Generic numeric bindings (`DEC-0047`) | TF100Web `frontend/tests_runtime_js/binding-runtime.test.mjs` table-tests read-only/write-only/read-write/denied/unbound policies, fixed/hash formatting across FLOAT32/FLOAT64 and eight integer datatypes, valid/invalid/denied/rejected/offline commits, pending duplicate suppression and Enter/Escape behavior. Source integration locks one Element+/Table path, all composed slots, poll focus/pending protection and missing-mapping quality fallback. Combined JS runtime count is 23; 20 focused Django contracts are green. |
| Linear composition and atomic cache lifecycle (`DEC-0047`) | TF100Web `frontend/tests_scada_performance.py` parameterizes 1/3 pages and 64/256/1,024 bindings per fragment, proves one tag pass and constant catalog resolver calls, and verifies generation-keyed structural invalidation. `DeployScadaBuilderManifestTests` covers generation rotation, timing metadata and failed-swap rollback. Seventeen focused composition/deployment/performance tests are green. |
| End-to-end runtime conformance fixture (`DEC-0047`) | TF100Web `frontend.tests_runtime_conformance` verifies the exact Builder fixture SHA, production 2.3 negotiation, header/body/footer composition and one machine-readable result per capability. All 118 `Supported` ids execute their own static/runtime assertion with non-empty evidence; a `shape.rectangle` mutation fails only that probe; all 44 `Blocked` ids fail production validation. Nine tests are defined: eight local gates pass and the external industrial artifact gate remains environment-controlled. |
| Preview/export/deployed-host parity (`DEC-0047`) | Builder `PreviewDocumentTests`, `Ft100SceneExporterTests` and `RuntimeConformancePackageTests` compare model objects, native preview/export markup, namespaces, editor-only exclusion, analyzer/manifest capabilities, evidence and runtime SHA. TF100Web executes the byte-identical stable deployed runtime and checks its hashed twin while re-running all 118 exact probes. Twelve focused Builder tests and the local TF fixture gates are green. |
| Industrial pages `03/04/08/12` (`DEC-0047`) | `IndustrialRuntimeIntegrationTests` exports the real reference project under strict 2.3, validates eight navigation commands, composition/assets, 8 state + 2 read-only + 1 writable numeric controls, 56 toggles, 126 table cells, readback structure and mapping-615 fallback, then records SHA/timing/version diagnostics with `LiveWritesExecuted=false`. TF100Web verifies the same package SHA, runtime and 46 required capabilities through production validation. One long Builder integration and eight TF conformance gates are green. |
| TF100Web manifest 2.3 negotiation (`DEC-0047`) | `frontend.tests_scada_deploy.DeployScadaBuilderManifestTests` and targeted package tests cover 2.1/2.2 compatibility, valid 2.3, missing/unknown versions, unsupported capability ids, contract version, runtime tampering, missing contract, pre-replacement preservation and exact vendored fixture SHA. Eleven focused tests and the local Django check pass under SQLite verification settings. |
| FT100/TF100Web export | `Ft100SceneExporterTests.cs`: manifest 2.3 default, sorted/deduplicated requirements, packaged runtime SHA-256, pre-staging blocked-capability rejection, and explicit 2.1/2.2 profiles. |
| FT100 `.sb2` archive and namespace validation | `Ft100PackageValidator`, `Ft100PackageValidatorTests`: unknown/duplicate/unsorted/blocked capabilities, runtime contract version, missing/invalid/mismatched SHA-256, tampering and runtime filename; plus archive and page-scope regressions in `Ft100SceneExporterTests`. |
| TF100Web package intake audit | `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py` |
| TF100Web `.sb2` binding event intake | `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py` (`ValueBindings.ReadTagId` / `WriteTagId` -> host mapping attributes) |
| TF100Web `DisplayFormat` hash-mask runtime | `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`, `node --check static\asset\js\station\visualisation_import.js`, targeted Node validation of `formatValue(999, "##.#") -> "99.9"` |
| Project save/reload | `ModernProjectStoreTests.cs` |
| Modern page identity and Wonderware migration | `PageIdentityTests.cs`, `ModernProjectStoreTests.cs` |
| Page command coordinator and shared application commands | `PageCommandCoordinatorTests.cs` (`ShowPropertiesOpensAndActivatesSelectedPageWithoutDirtyingWorkspace`), `PageApplicationCommandTests.cs` |
| Project-scoped page undo/redo | `ProjectWorkspaceHistoryTests.cs`, `PageLifecycleIntegrationTests.cs` |
| Atomic project/scenes/deletions persistence | `ModernProjectAtomicSnapshotTests.cs`, `ModernProjectStoreTests.cs` |
| Native page preview/export and `.sb2` identity projection | `NativePageDocumentTests.cs`, `Ft100SceneExporterTests.cs`, `PageLifecycleIntegrationTests.cs` |
| Pages ribbon/project/context surfaces | `RibbonCommandCatalogTests.cs`, `PageManagementSurfaceContractTests.cs` (search label, initial filters, shared `Icon.Page.New` and inline-content right-click traversal) |
| Shared error dialog and Diagnostics panel | `DiagnosticsSurfaceContractTests.cs`, including explicit `OneWay` bindings for read-only WPF `Run.Text` targets |
| Scene/domain rules | `OfficialSceneDomainTests.cs`, `ScadaSceneGroupTests.cs` |
| Undo/redo/history | `EditorHistoryServiceTests.cs` |
| WebView bridge/context menu | `WebViewContextMenuScriptTests.cs` |
| Top ribbon dynamic command surface | `RibbonCommandCatalogTests.MainRibbonUsesOnlyDynamicCommandSurface`, `RibbonCommandCatalogTests.DefaultCatalogDefinesExpectedTopRibbonTabs`, `RibbonCommandCatalogTests.DefaultCatalogRequiresSemanticIconKeys`, `RibbonCommandCatalogTests.InsertFamilyRibbonKeepsFirstLevelCompact`, `RibbonCommandCatalogTests.SecondLevelRibbonUsesCompactTwoRowCommands` |
| Left tool palette semantic command surface | `RibbonCommandCatalogTests.ToolPaletteUsesSemanticCommandCatalog`, `RibbonCommandCatalogTests.DefaultCatalogRequiresSemanticIconKeys`, `RibbonCommandCatalogTests.DisabledCommandsExposeReason` |
| Element inventory hierarchy | `LegacyElementSelectionModelTests.cs` |
| Element+ legacy conversion | `ElementPlusLegacyConverterTests.cs` |
| Element+ events/actions | `OfficialSceneDomainTests.cs`, `WebViewContextMenuScriptTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| Element+ conditional object visibility actions | `OfficialSceneDomainTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| Element+ compound condition groups | `OfficialSceneDomainTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| FT100 runtime lifecycle bridge | `Ft100SceneExporterTests.cs` |
| Element+ runtime border actions | `OfficialSceneDomainTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| Element+ runtime visual effects | `OfficialSceneDomainTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| Element+ popup fragment actions | `OfficialSceneDomainTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| Element+ advanced popup options | `OfficialSceneDomainTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| TF100Web tag catalog import and Element+ value bindings | `ModernProjectStoreTests.cs`, `OfficialSceneDomainTests.cs`, `Ft100SceneExporterTests.cs` |
| Tag catalog editor panel filters | `StudioElementPlusContractTests.cs` |
| FT100 read tag value application runtime | `Ft100SceneExporterTests.cs` |
| Element+ group click navigation export | `Ft100SceneExporterTests.cs` |
| FT100 clickable target pointer cursor | `Ft100SceneExporterTests.cs` |
| Element+ button hover metadata and FT100 CSS | `OfficialSceneDomainTests.cs`, `WebViewContextMenuScriptTests.cs`, `ModernProjectStoreTests.cs`, `Ft100SceneExporterTests.cs` |
| Element+ button pressed/active metadata and FT100 CSS | `OfficialSceneDomainTests.ButtonElementHasDefaultHoverUnlessExplicitlyDisabled`, `WebViewContextMenuScriptTests.ElementPropertiesExposeAdvancedButtonPressedFields`, `ModernProjectStoreTests.SaveAndReloadPreservesPageManifestBackgroundAndObjectEvents`, `Ft100SceneExporterTests.ExportWritesDjangoManifestAndObjectOwnedClickNavigateAction` |
| Element+ Toggle button on/off runtime state | `Ft100SceneExporterTests.ExportWritesToggleButtonRuntimeStateOnWrapper` |
| Element+ disabled button runtime state | `Ft100SceneExporterTests.ExportWritesDisabledButtonRuntimeStateAndOmitsHoverCss` |
| Element+ standard button activation runtime events | `Ft100SceneExporterTests.ExportWritesStandardButtonActivationRuntimeEvents` |
| Element+ button preview/export metadata parity | `WebViewContextMenuScriptTests.ModernButtonRendersTextAndUsesPropertyText`, `Ft100SceneExporterTests.ExportWritesToggleButtonRuntimeStateOnWrapper`, `Ft100SceneExporterTests.ExportWritesDisabledButtonRuntimeStateAndOmitsHoverCss`, `Ft100SceneExporterTests.ExportWritesStandardButtonActivationRuntimeEvents` |
| Stateful defrost Toggle buttons (`DEC-0044`) | `Win00012DefrostToggleConfigurationTests.ReferenceScene_ConfiguresAllDefrostTogglesFromTheirConfirmedCommandBit`, `Ft100SceneExporterTests.ExportAsync_WrapsButtonLabelInDataScadaTextSpan`, `tests/runtime-js/state-engine.test.mjs`, `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py::ScadaRuntimeInitContractTests` |
| Reversible state visuals and shared numeric mappings (`DEC-0045`) | `tests/runtime-js/effect-applier.test.mjs` covers explicit SVG fill/stroke targeting, transitions, reset and wrapper fallback; `tests/runtime-js/state-engine.test.mjs`, `RuntimeJsModulesTests`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg`, `WebViewContextMenuScriptTests.ModernShapePreviewUsesSvgShapeKind`, and `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py::ScadaRuntimeInitContractTests` cover the remaining shared contract. |
| Element+ HMI button presets and `ButtonKind` export | `OfficialSceneDomainTests.ButtonElementHasDefaultHoverUnlessExplicitlyDisabled`, `WebViewContextMenuScriptTests.InsertRibbonExposesStandardShapesAndButtons`, `WebViewContextMenuScriptTests.ModernButtonRendersTextAndUsesPropertyText`, `ModernProjectStoreTests.SaveAndReloadPreservesPageManifestBackgroundAndObjectEvents`, `Ft100SceneExporterTests.ExportWritesDjangoManifestAndObjectOwnedClickNavigateAction` |
| Element+ advanced style opacity and rotation | `OfficialSceneDomainTests.InputTextElementHasEditableStyleAndDataDefaults`, `WebViewContextMenuScriptTests.ElementPropertiesExposeAdvancedShapeStyleFields`, `ModernProjectStoreTests.SaveAndReloadPreservesPageManifestBackgroundAndObjectEvents`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg` |
| Element+ rotation handle and context menu presets | `WebViewContextMenuScriptTests.LegacyViewerMessageExposesRotationField`, `WebViewContextMenuScriptTests.MainWindowHandlesRotationMessageAndNormalizesAngle`, `WebViewContextMenuScriptTests.NeHandleIsRepurposedForRotationDrag`, `WebViewContextMenuScriptTests.RotationDragShowsLiveAngleBadge`, `WebViewContextMenuScriptTests.ContextMenuOffersRotationPresetsForSingleElementPlusSelection`, `WebViewContextMenuScriptTests.ContextMenuCustomRotationOpensValidatedInlineInput`, `WebViewContextMenuScriptTests.CustomRotationCleanupDetachesBlurListenerBeforeHidingInput` |
| Element+ machine and measurement HMI symbols with manual insertion | `OfficialSceneDomainTests.ShapeElementDefaultsAndFactoriesPreserveShapeKind`, `WebViewContextMenuScriptTests.InsertRibbonExposesStandardShapesAndButtons`, `WebViewContextMenuScriptTests.ModernShapePreviewUsesSvgShapeKind`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg` |
| Element+ electrical and alarm HMI symbols with manual insertion | `OfficialSceneDomainTests.ShapeElementDefaultsAndFactoriesPreserveShapeKind`, `WebViewContextMenuScriptTests.InsertRibbonExposesStandardShapesAndButtons`, `WebViewContextMenuScriptTests.ModernShapePreviewUsesSvgShapeKind`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg` |
| Element+ standard, HMI, and process shapes with manual insertion | `OfficialSceneDomainTests.ShapeElementDefaultsAndFactoriesPreserveShapeKind`, `ModernProjectStoreTests.SaveAndReloadPreservesPageManifestBackgroundAndObjectEvents`, `WebViewContextMenuScriptTests.InsertRibbonExposesStandardShapesAndButtons`, `WebViewContextMenuScriptTests.ModernShapePreviewUsesSvgShapeKind`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg` |
| Insert Formes gallery and two-point line/arrow authoring | `RibbonCommandCatalogTests.MainRibbonUsesClippingSafeOverflowHeight`, `RibbonCommandCatalogTests.DefaultCatalogUsesStableUniqueCommandIds`, `WebViewContextMenuScriptTests.InsertRibbonExposesStandardShapesAndButtons`, `WebViewContextMenuScriptTests.LineAndArrowPlacementUseTwoPointMode`, `OfficialSceneDomainTests.ShapeElementDefaultsAndFactoriesPreserveShapeKind`, `Ft100SceneExporterTests.ExportRendersStandardShapeElementAsScopedSvg` |
| Modern table dialog fields | `TableUiArchitectureTests.TableDialogLayoutKeepsConcreteWpfControlsVisible` |
| Table contextual entry and shared Element+ lock surfaces | `TableUiArchitectureTests`, `TableAuthoringSessionTests.RibbonTogglesEditorGuidesAndMergeActionFromSelectionState`, `TableEditCoordinatorTests.ToggleMergeUsesCurrentSelectionState`, `WebViewContextMenuScriptTests.ContextMenuOffersStatefulElementLockAction`, `WebViewContextMenuScriptTests.LockedElementMovementIsRejectedBeforeVisualDragStarts`, `WebViewContextMenuScriptTests.TableCellModeOwnsPointerInputBeforeElementDrag`, `ElementLockCoordinatorTests`, `ApplicationCommandTests`, `RibbonCommandCatalogTests` |
| Advanced table format inspection and reset | `TablePropertiesInspectorTests`, `TableEditCoordinatorTests` |
| Typed table WebView bridge diagnostics | `TableWebViewMessageAdapterTests` |
| 64 x 64 batching and Release measurements | `TableWebViewPerformanceContractTests` |
| Representative `win00012` 16 x 10 round-trip/preview/`.sb2` | `AdvancedTableAuthoringIntegrationTests` |
| Element+ `Donnees` authoring and `DisplayFormat` masks | `ElementGroupTests.NumericDisplayFormatMaskControlsScalePrecisionAndInputStep`, `ElementGroupTests.NumericDisplayFormatMaskClampsToVisibleDigitBudget`, `WebViewContextMenuScriptTests.ElementDataTabDeprecatesLegacyTagDecimalsAndUnitFields` |
| Table cell numeric binding domain and edit safety | `TableCellBindingOperationsTests`, `TableContentOperationsTests`, `ScadaTableOperationsTests`, `TableClipboardTests`, `TableEditCoordinatorTests`, `OfficialSceneDomainTests` |
| Table cell numeric authoring surfaces | `TableNumericInputPropertiesViewModelTests`, `TableNumericInputPropertiesDialogTests`, `TableUiArchitectureTests`, `TablePropertiesViewModelTests` |
| Table cell numeric preview/export/validation | `ModernTableHtmlRendererTests`, `ModernProjectStoreTests`, `Ft100SceneExporterTests`, `Ft100PackageValidatorTests` |
| Reliable single-surface numeric cell authoring (`DEC-0043`) | `TableCellAddressTests`, `TableNumericBindingAuthoringPolicyTests`, `TablePropertiesInspectorTests`, `TableEditCoordinatorTests`, `TableUiArchitectureTests`, `TableWebViewMessageAdapterTests`, `ModernProjectStoreTests`, `Ft100SceneExporterTests`; isolated `win00012_modern_no_legacy` WPF/WebView2 smoke |
| TF100Web manifest 2.1/2.2 and cell runtime intake | `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py`, `frontend/tests_scada_table_bindings.py`, `frontend/tests_scada_runtime.py`, `frontend/tests_deploy_scada_builder.py` |
| Studio Element+ editor state | `ElementStudioEditorStateTests.cs` |
| Studio Element+ contract | `StudioElementPlusContractTests.cs` |
| Studio Element+ re-edit from scene | `WebViewContextMenuScriptTests.cs`, `ElementStudioComponentToImportPackageMapperTests.cs`, `ElementStudioComponentNamingTests.cs` |
| Studio source rendering | `ElementStudioSourceRenderingTests.cs` |
| Legacy extraction | `LegacyElementDetectorTests.cs`, `LegacyAtomicElementDetectorTests.cs` |

## 3. Rule

When a contract-sensitive behavior changes, update this map or document why no test exists in `KNOWN_GAPS_V2.md`.
