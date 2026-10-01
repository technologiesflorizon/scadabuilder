# SCADA Builder V2 - Project Model Contract

Date: 2026-08-13
Status: Active project model contract
Document version: `V2.1.6.0065`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-10-01 | `V2.1.6.0065` | `PENDING` | Revue du cache de scènes (`V2.1.6.0064`), trois correctifs mineurs. (1) Course d'invalidation perdue : un lecteur qui avait lu les anciens octets pouvait stocker son entrée après qu'un enregistrement eut remplacé le fichier et invalidé l'entrée ; si le nouveau fichier gardait la même date et la même longueur (FAT/exFAT, partage réseau), l'ancienne scène aurait été servie. `SceneFileCache` tient désormais une génération par chemin (incrémentée par `Invalidate`/`Forget`) et une époque globale (incrémentée par `InvalidateUnder`/`ForgetUnder`/`RetainOnly`) ; le lecteur les capture avant de sonder le fichier et ne stocke rien si l'une a bougé — ce qui empêche aussi une entrée tardive du projet A après le passage au projet B. (2) Une ouverture de projet refusée après la lecture de son snapshot (diagnostic bloquant ou exception) oublie désormais les scènes de ce projet (`ModernProjectStore.ForgetSceneCache`, appelé par `ProjectWorkspaceRepository.OpenAsync`). (3) §2.4 précisée : ce sont les enregistrements imbriqués qui sont partagés entre cache et snapshots (l'appelant reçoit toujours une nouvelle scène de premier niveau), l'hypothèse d'écrivain unique et la fermeture de la course par la génération sont documentées. Trois tests ajoutés à `tests/ScadaBuilderV2.Tests/ModernProjectSceneCacheTests.cs`, vérifiés en échec avant correction. |
| 2026-10-01 | `V2.1.6.0064` | `PENDING` | Lecture des snapshots d'espace de travail : nouveau cache de scènes `SceneFileCache` (`src/ScadaBuilderV2.Infrastructure/ModernProjects/SceneFileCache.cs`) porté par `ModernProjectStore`. Chaque capture (`PageWorkspaceController.CaptureSnapshotAsync`, appelée par toutes les commandes de page, de Fenêtre rapide et de feuille de paramètres, par l'enregistrement, et par le rafraîchissement du panneau) relisait et redésérialisait toutes les scènes non ouvertes — sur le projet de référence (56 scènes, ~60 Mo) 3,1 s au premier appel puis 1,1 à 1,5 s à chaque appel. Une scène dont le fichier n'a pas changé (chemin résolu + date de dernière écriture UTC + longueur) n'est plus ni relue ni désérialisée ; un fichier réécrit est relu et comparé par SHA-256, le contenu identique garde la scène en cache ; un fichier disparu est oublié. Le cache ne contient que la scène brute (fonction pure des octets du fichier) : la projection sur le projet disque puis sur le projet en mémoire est réappliquée à chaque lecture, exactement comme avant, et le manifeste disque n'est plus chargé qu'une fois par capture au lieu d'une fois par scène. Les onglets ouverts restent prioritaires. Invalidation : enregistrement de l'espace de travail (chaque scène écrite est rétrogradée — relue et rehachée au prochain usage, ce qui couvre une écriture dans la même résolution d'horodatage — et chaque scène supprimée oubliée), `SaveSceneAsync`, recouvrement de transaction, conversion de format à l'ouverture, suppression de page en attente, changement/fermeture de projet. Mesure : 2,1 s puis 17 à 48 ms (premier/second appel), 137 à 154 ms pour la première capture après un enregistrement. Voir §2.4. Nouveaux tests : `tests/ScadaBuilderV2.Tests/ModernProjectSceneCacheTests.cs`. |
| 2026-09-30 | `V2.1.6.0038` | `332f633` | Tâche 4 du chantier `2026-09-30-quick-window-parameter-sheets` : `ParameterSheetStore` (nouveau) persiste chaque feuille de paramètres sous `parameter-sheets/<SheetKey>.parameter-sheet.json`, en miroir exact de `QuickWindowStore` (même `JsonSerializerOptions`, même mise en scène transactionnelle `new/`/`backup/`, même contrôle de confinement de chemin). `ModernProjectStore.LoadProjectFromRootAsync` charge ces fichiers autoritaires dans `ScadaProject.ParameterSheets` et **refuse** (§2.2, même règle que pour les Fenêtres rapides) un `project.json` qui porterait des `ParameterSheets` inline sans fichiers autoritaires correspondants — pas de migration implicite. `SaveProjectToRootAsync` et `SaveWorkspaceSnapshotToProjectRootAsync` écrivent les feuilles dans l'ordre déterministe (`Code` puis `SheetKey`), calculent et appliquent la suppression des fichiers `parameter-sheets/*.parameter-sheet.json` retirés du projet, et valident chaque feuille mise en scène. `project.json` ne porte jamais la clé `ParameterSheets` : le champ gagne `[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` (`ProjectModels.cs`), l'idiome déjà utilisé sur `FormatVersion`. Nouveaux tests : `tests/ScadaBuilderV2.Tests/ParameterSheetStoreTests.cs`. |
| 2026-09-30 | `V2.1.6.0037` | `dcb602b` | Revue round 1 de la Tâche 3 (chantier `2026-09-30-quick-window-parameter-sheets`) : **la ligne `V2.1.6.0036` ci-dessous affirmait à tort que `QuickWindowBinding.ExpressionAst` est « sérialisé `null` par défaut »**, ce que le contrat §12.4 (`docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`, tel qu'amendé par la Tâche 1) contredit explicitement : le champ doit être « présent seulement quand `SourceKind` vaut `Expression` ». Corrigé par `[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` sur `ExpressionAst`, le même idiome déjà utilisé sur `ScadaProject.FormatVersion`. Les fixtures `tests/conformance/quick-window-contract-handshake/{manifest,expectations}.json` régénérées par la Tâche 3 sont restaurées à leur état d'avant la Tâche 3 (`git diff fa7345f -- tests/conformance/quick-window-contract-handshake/` vide) : le champ absent à `null` ne change plus rien à la sortie sérialisée. Nouveau test `ExpressionAstIsOmittedFromJsonWhenNullAndPresentWhenSet` (`tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPlaceholderTests.cs`) épingle le comportement, vérifié en échec sans l'attribut puis au vert avec. |
| 2026-09-30 | `V2.1.6.0036` | `f2469c4` | Tâche 3 du chantier `2026-09-30-quick-window-parameter-sheets` : `ScadaPageType.QuickWindow` ajouté en dernière valeur (aucun décalage des valeurs persistées) ; `ScadaProject` porte `ParameterSheets : IReadOnlyList<ParameterSheet>?` (inséré avant `FormatVersion`, tous les appelants nomment déjà `FormatVersion`) et `EffectiveParameterSheets`. `ScadaCommandBinding` gagne `ParameterSheetKey`/`QuickWindowPageKey` (`Guid?`) et `QuickWindowBinding` gagne `ExpressionAst : ScadaExprNode?`, tous deux additifs et sérialisés `null` par défaut (fixtures de conformance regénérées : `tests/conformance/quick-window-contract-handshake/*.json`). Nouveau reconnaisseur `QuickWindowPlaceholder` (`Domain/QuickWindows/QuickWindowPlaceholder.cs`) : `TryParseName`, `FindInText`, `UnescapeText`, jeton `#1`..`#999` sans zéro de tête, `\#` échappé, garde hexadécimale excluant les couleurs (`#0F2A30`). `ScadaRuntimeCapabilityCatalog.PageTypes` gagne `page.quick-window` (`Blocked`, `Tf100WebHost`) pour garder la complétude de catalogue ; aucun comportement d'export changé (capacité non `Supported`, absente du profil strict). Détail complet : `docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md` §4.4. |
| 2026-09-10 | `V2.1.6.0022` | `240dcf6` | §5 corrigée : sa première phrase se lisait comme si les quatre modules persistés portaient un refus vers l'arrière en service. Seul `project.json` l'est effectivement; `Scene`, `TagCatalog` et `Component` portent le champ `FormatVersion` à l'aller-retour mais aucun code de production ne le lit pour refuser ou convertir. Trouvé au tour 2 de la revue de branche du chantier de versionnement de format, `KNOWN_GAPS_V2.md` entrée 35. |
| 2026-09-10 | `V2.1.6.0019` | `6e4f4b6` | Correction de suivi Tâche 8 (revue round 1) : §2.2 précise que le retrait de la comparaison `ManifestVersion` corrige un blocage total latent de la fonctionnalité Fenêtre rapide, pas une réduction de portée. Le trou résiduel sous `Compatibility21`/`Compatibility22` est consigné à l'entrée 34 de `KNOWN_GAPS_V2.md`. |
| 2026-09-10 | `V2.1.6.0018` | `02de468` | Tâche 8 du chantier de versionnement de format : `ManifestVersion` perd son rôle d'autorisation de contenu pour les Fenêtres rapides. `ValidateQuickWindows` ne compare plus contre `"2.3"`; le catalogue de capacités reste seul juge de ce qu'un projet peut contenir, quel que soit le profil négocié. **Corrige un défaut latent** : `ManifestVersion` valait `"2.0"` par défaut et n'était jamais mis à `"2.3"` nulle part dans `src/`, donc la comparaison retirée bloquait en `Error` tout projet réel portant une Fenêtre rapide, dès sa création — pas un simple rétrécissement de portée. |
| 2026-09-10 | `V2.1.6.0017` | `f5589e9` | `ProjectGeneration1Converter` (Tâche 7 du chantier C) écrit `PageKey` avec la même dérivation que `PageKeyFactory.CreateDeterministic` (GUID v5, bits de version/variante posés) plutôt qu'une dérivation SHA-256 brute incompatible. `ModernProjectMigration` reste le normaliseur d'identité du store, appelé aux écritures et à la construction en mémoire; le convertisseur ne fait que régler l'identité une fois, dans le fichier, à l'ouverture. |
| 2026-09-08 | `V2.1.6.0013` | `baf42e4` | Les trois modules (projet, scène, catalogue) portent les champs `FormatVersion` et `EffectiveFormatVersion`. Jamais sérialisés tant que nuls, donc les artefacts figés demeurent byte-identiques. |
| 2026-09-08 | `V2.1.6.0012` | `225b0b5` | `DEC-0049` D5 implémentée : `OpenAsync` refuse un `project.json` dont la génération de format dépasse `ScadaFormatGeneration.Project`, avant toute désérialisation. |
| 2026-09-08 | `V2.1.6.0004` | `d5f9ab1` | Le profil 2.3 est productible : les gates des Phases 3 à 6 sont franchis et le fail-closed ne porte plus que sur les deux capacités non promues. |
| 2026-08-21 | `V2.1.5.0026` | `1452849` | Versionnement de l'Interface locale : réalignement par invocation, statut `Outdated` dérivé et liaisons jamais réécrites. |
| 2026-08-13 | `V2.1.5.0022` | `436d38f` | Phase 2 : mutations Application coordonnent projet, scène appelante, commande et invocation; l’historique restaure le snapshot complet sans I/O. |
| 2026-08-13 | `V2.1.5.0021` | `b353e37` | Contrat de persistance QuickWindow Phase 1 : fichiers autoritaires séparés, manifest sans duplication et profils antérieurs fail-closed. |
| 2026-07-29 | `V2.1.5.0000` | `8fe1077` | La persistance accepte une racine projet exacte choisie par l’utilisateur; `.sb2` demeure un artefact runtime. |
| 2026-07-15 | `V2.1.4.0039` | `ce99ff9` | Les cellules ancres `InputNumeric` peuvent porter `DisplayFormat` et des bindings lecture/ecriture persistants, proteges par les operations structurelles et exclus du clipboard. |
| 2026-07-15 | `V2.1.4.0027` | `88e865a` | Validation end-to-end d'une table 16 x 10 avec contenus mixtes, deux en-têtes, fusion, styles par portée, pistes non uniformes, bordures physiques et `IsLocked`, sans modifier le schéma `.sb2`. |
| 2026-07-15 | `V2.1.4.0026` | `0874416` | Extension Tableau par retour a la ligne, hauteur typographique et bordures physiques; ajout de `ScadaElement.IsLocked` comme metadata d'authoring. |
| 2026-07-14 | `V2.1.4.0016` | `10cfa72` | Ajout du contrat persistant `ScadaElementKind.Table`, pistes, cellules, fusions, contenus input et styles heritables. |
| 2026-07-14 | `V2.1.4.0012` | `50b2ad9` | Contrat `PageKey`/`PageCode`, provenance Wonderware, pages natives et sauvegarde atomique désormais implémenté. |
| 2026-07-14 | `V2.1.4.0011` | `4def659` | Ajout de la cible approuvée `PageKey`/`PageCode`, provenance importée et migration compatible, avant implémentation. |
| 2026-06-17 | `V2.1.2.0024` | `49cedc7` | Clarification du role actif de `DisplayFormat` et de la deprecation authoring de `TagBinding`, `Decimals` et `Unit`. |
| 2026-06-17 | `V2.1.2.0010` | `5302022` | Ajout du modele `ScadaActionCondition` pour actions objet conditionnelles. |
| 2026-06-17 | `V2.1.2.0009` | `7e3610c` | Ajout des bindings Element+ `ReadTagId` et `WriteTagId` et des validations de build. |
| 2026-06-17 | `V2.1.2.0008` | `f78e8cd` | Ajout du catalogue tags TF100Web importe au modele projet. |
| 2026-06-16 | `V2.1.1.0039` | `2c5a0b4` | Creation du contrat actif du modele projet et scene. |

## 1. Contract

The V2 project model is the source of truth for preview, save/reload, Studio exchange, and FT100/TF100Web export.

Legacy source paths and source ids are trace metadata unless explicitly converted or approved by a sanitized-source decision.

Un projet éditable est identifié par sa racine exacte et son `project.json`. Les scènes, assets, bibliothèques, imports, exports et données `.studio` restent sous cette racine. La compatibilité des sources importées historiques est confinée à un adaptateur de résolution distinct.

## 2. Ownership

1. Project owns identity, scene inventory, home page, and build composition.
2. Project owns the optional imported TF100Web tag catalog used by Element+ authoring surfaces.
3. Scene owns canvas, page type, background, elements, actions, composition references, and removed source ids.
4. Elements own identity, kind, bounds, data, event bindings, and optional read/write tag bindings.
5. Actions may own one optional tag condition when the runtime function supports deterministic conditional execution.
6. Runtime manifests are generated outputs, not editable source models.

### 2.1 Element+ Tableau

Un tableau est un seul `ScadaElement` de kind `Table` dont `Table` contient les colonnes, rangees, ancres de cellules, spans, contenus et styles. Les pistes sont limitees a 1..64 par axe; les dimensions minimales sont 24 px par colonne et 20 px par rangee. Les anciennes scenes sans champ `Table` restent lisibles.

Une cellule contient du texte statique, un `InputText` ou un `InputNumeric`. Une cellule ancre `InputNumeric` peut porter `DisplayFormat` ainsi que `ScadaTableCellValueBindings.ReadTagId` et `WriteTagId`; les cellules texte et `InputText` ne sont pas eligibles dans cette tranche. Les styles nullable signifient `Heriter`; la resolution se fait propriete par propriete selon `cellule > rangee explicite > bande > colonne > tableau > defaut`.

L'identite d'une cible liee reste derivee de l'id Tableau et des coordonnees courantes de son ancre. Les insertions de pistes deplacent contenu et binding ensemble; une fusion qui absorberait une autre cellule liee est refusee; la suppression destructive exige confirmation. `ClearContent` conserve type et binding en vidant seulement la valeur initiale. Le clipboard n'embarque jamais les bindings et ne peut pas convertir atomiquement une cible liee vers un contenu non numerique.

`ScadaTableFormat` porte aussi `TextWrap` et `LineHeight`. `BorderOverrides` persiste des segments horizontaux/verticaux unitaires; les presets UI ne sont jamais serialises. Plusieurs rangees initiales consecutives peuvent porter `IsHeader`.

`ScadaElement.IsLocked` est une metadata persistante de position. Une scene historique sans cle charge `false`. Cette metadata n'est ni une propriete runtime, ni une geometrie `.sb2`/`.sep`.

Element numeric data keeps compatibility fields for older projects, but active authoring uses:

1. `DisplayFormat` as the single display-format signal exported to TF100Web.
2. `Minimum` and `Maximum` only as clamp constraints for non-read-only numeric inputs.
3. `ReadTagId` and `WriteTagId` for runtime value bindings.

`TagBinding`, `Decimals`, and `Unit` are legacy model fields. They may be preserved by save/reload, but they are not active Element+ authoring controls.

### 2.2 Fenêtres rapides — contrat persistant Phase 1

`ScadaProject.QuickWindows` est la projection en mémoire des définitions chargées. Chaque définition persistante est autoritaire dans `quick-windows/<QuickWindowDefinitionKey>.quick-window.json`; `project.json` ne duplique jamais le graphe complet des définitions. Le nom du fichier et la clé du contenu doivent correspondre exactement.

Une définition porte un `VisualContent` borné, sa version d’interface, ses membres locaux et ses defaults de présentation. Les invocations restent portées par le projet et les commandes appelantes au moyen d’un `InvocationKey`, d’un `QuickWindowDefinitionKey` et d’une `InterfaceVersion`; l’identité runtime n’est jamais persistée. Les membres privés ne sont pas bindables par une invocation.

Les fichiers sont ordonnés de façon déterministe et remplacés atomiquement après écriture temporaire, flush et validation. Un projet historique sans Fenêtre rapide se recharge sans migration et ne doit pas être réécrit. Une définition inline sans fichier autoritaire est refusée au lieu d’être migrée implicitement. Les gates des Phases 3 à 6 sont franchis : le profil 2.3 est productible et exporte les onze capacités `quick-window.*` promues `Supported`. Le gate reste fail-closed sur les deux capacités encore `Blocked` — `quick-window.binding.parent-port` et `quick-window.legacy-fragment-adapter` — : un projet qui en déclenche une est refusé avant qu’aucun répertoire d’export n’existe.

`ManifestVersion` ne porte plus qu'un rôle : le profil d'export négocié avec TF100Web. Ce n'est plus lui qui autorise la présence de contenu Fenêtre rapide dans un projet — le catalogue de capacités (`ScadaRuntimeCapabilityCatalog`) est la seule autorité sur ce qu'un projet peut contenir, via le même gate fail-closed ci-dessus. La comparaison de chaîne contre `"2.3"` qui refusait autrefois toute Fenêtre rapide sous un `ManifestVersion` différent a été retirée de `ValidateQuickWindows` (Tâche 8 du chantier de versionnement de format, 2026-09-08).

**Ceci corrige un défaut latent, pas seulement une réduction de portée.** `ScadaProject.ManifestVersion` vaut `"2.0"` par défaut (`ProjectModels.cs`) et rien dans `src/` ne lui assigne jamais `"2.3"` — `ModernProjectStore.cs:52` réaffirme `"2.0"` quand le champ est vide, et le projet de référence porte `"2.0"`. `"2.3"` n'est écrit nulle part ailleurs que dans le manifeste **exporté**, un artefact distinct (`Ft100SceneExporter.ManifestVersion(Ft100ManifestProfile)`). Avant cette tâche, la comparaison retirée se déclenchait donc en `Error` (`quick-window.profile-unsupported`) sur **tout** projet réel portant du contenu Fenêtre rapide, dès sa création — ce n'était pas un gate fonctionnel réduit par cette tâche, c'était un blocage total et latent de la fonctionnalité que `DEC-0050` a livrée, qui ne s'est jamais manifesté seulement parce qu'aucun projet réel n'a encore porté de Fenêtre rapide.

L'Interface locale est versionnée par clé stable. Renommer un membre ou modifier un membre privé n'exige aucun increment de `InterfaceVersion` et ne casse aucune invocation. Ajouter, retirer ou modifier le contrat typé d'un membre public l'exige. Lors de la mutation, chaque invocation encore compatible est réalignée sur la nouvelle version sans que ses liaisons soient réécrites; une invocation cassée conserve sa version et ses liaisons et devient `Outdated`. Ce statut est dérivé du couple de versions : aucun champ de statut n'est persisté et aucun quatrième identifiant n'est introduit.

Les mutations Phase 2 sont préparées en mémoire comme un snapshot cohérent du projet et des scènes appelantes. Ajouter, modifier ou retirer une invocation met à jour sa commande `OpenQuickWindow` dans la même transition. L’undo/redo restaure le projet, les scènes, la sélection et le dirty state sans effectuer d’I/O; Save demeure le seul commit persistant.

### 2.3 Feuilles de paramètres — persistance (Tâche 4)

`ScadaProject.ParameterSheets` est la projection en mémoire des feuilles chargées. Chaque feuille persistante est autoritaire dans `parameter-sheets/<SheetKey>.parameter-sheet.json`; `project.json` ne porte jamais la clé `ParameterSheets`, ni `null` ni un graphe inline (`[JsonIgnore(Condition = WhenWritingNull)]`). Le nom du fichier (le `SheetKey` canonique en format `N`) doit correspondre exactement à `ParameterSheet.SheetKey`.

`ParameterSheetStore` mire `QuickWindowStore` trait pour trait : mêmes options JSON, même écriture temporaire-puis-`File.Move`/`File.Replace` atomique, même confinement de chemin (`ResolveContainedParameterSheetPath` refuse tout chemin hors de `parameter-sheets/`). `ModernProjectStore` intègre `ParameterSheetStore` aux mêmes quatre points que `QuickWindowStore` : chargement (`LoadProjectFromRootAsync`, avec le même refus fail-closed d'une feuille inline sans fichier autoritaire), écriture non transactionnelle (`SaveProjectToRootAsync`), mise en scène et suppression transactionnelles (`SaveWorkspaceSnapshotToProjectRootAsync`), et résolution de secours dans `ApplyPendingDeletions`.

### 2.4 Lecture des snapshots d'espace de travail — cache de scènes

`ModernProjectStore.ReadWorkspaceSnapshotFromProjectRootAsync` et `LoadOrCreateSceneFromProjectRootAsync` lisent les fichiers de scène à travers `SceneFileCache`, propre à l'instance du store (l'application en partage une seule). Le résultat est, par construction, identique à une lecture sans cache :

1. Le cache ne mémorise que la scène brute telle que désérialisée du fichier (après le retrait des surcharges de texte legacy converties), c'est-à-dire une fonction pure des octets. La projection `ModernProjectMigration.MigrateScene` sur le projet disque, puis sur le projet du snapshot, est réappliquée à chaque lecture.
2. Une entrée n'est servie sans toucher au fichier que si la date de dernière écriture UTC et la longueur du fichier sont exactement celles observées à sa lecture. Sinon le fichier est relu ; si ses octets sont identiques (SHA-256) la scène en cache est conservée, sinon elle est redésérialisée. Un fichier absent fait oublier l'entrée et retombe sur la scène vide habituelle.
3. Les scènes des onglets ouverts (`PageWorkspaceReadContext.OpenOrDirtyScenes`) restent prioritaires sur le disque et sur le cache.
4. Toute écriture faite par le store rétrograde l'entrée concernée (relecture et rehachage obligatoires au prochain usage, ce qui couvre une réécriture dans la résolution d'horodatage du système de fichiers) : `SaveWorkspaceSnapshotToProjectRootAsync` (succès ou échec), `SaveSceneAsync`, recouvrement d'une transaction interrompue, conversion de format à l'ouverture (`ProjectWorkspaceRepository`). Les scènes supprimées par un enregistrement sont oubliées. `PageWorkspaceController` rétrograde les chemins des pages en suppression en attente, garde le cache du projet qu'il initialise et l'oublie entièrement à la fermeture. Une ouverture refusée après la lecture de son snapshot (diagnostic bloquant ou exception) oublie les scènes du projet refusé.
5. Une lecture concurrente d'une écriture ne peut pas stocker une entrée périmée : chaque invalidation ou oubli incrémente une génération par chemin, et chaque invalidation de racine, oubli de racine ou changement de portée incrémente une époque globale. Le lecteur capture les deux avant de sonder le fichier et ne stocke son résultat que si aucune n'a bougé ; il renvoie ce qu'il a lu à son appelant, sans le mettre en cache. Cela ferme la course interne à l'application même sur un système de fichiers à horodatage grossier, et empêche une entrée tardive d'un projet après le passage à un autre.

`ScadaScene` et tout son graphe (101 types parcourus par réflexion) sont des `record` à propriétés `init` exposant des collections en lecture seule, et aucun code de production ne convertit ces collections vers un type mutable. L'appelant ne reçoit jamais l'instance mise en cache elle-même (`MigrateScene` renvoie toujours une nouvelle scène et de nouveaux éléments), mais les enregistrements imbriqués non migrés (styles, données, liaisons, etc.) sont partagés entre le cache et les snapshots de l'historique d'annulation ; c'est sûr parce que le graphe du domaine est immuable. Le cache suppose que l'application est le seul écrivain du projet ouvert : un fichier réécrit par un autre processus en gardant à la fois la même date d'écriture et la même longueur ne serait pas détecté ; ce cas sort du contrat.

## 3. Tag Catalog

Imported tags are stored as `ScadaProject.TagCatalog` with schema `tf100web-scada-tags-v1`. The catalog is project-level data, not scene-level geometry.

The catalog preserves:

1. Stable tag id.
2. Display label for authoring UI.
3. TF100Web source metadata such as keyword label, device, protocol, address, datatype, unit, enabled state, and writeable state.

All enabled tags are exposed for `Lire valeur` authoring. `Ecrire valeur` may target only writeable tags and only editable input Element+ objects. Build/export validation rejects missing tag references, write bindings on non-input or read-only Element+ objects, and write bindings to read-only tags.

## 4. Implemented Modern Page Model

`DEC-0038` is implemented. Every page owns an immutable internal `PageKey`, a visible mutable `PageCode`, optional import provenance, and canonical internal home/composition/action references by key. Existing id fields remain readable during idempotent migration. The `.sb2` boundary resolves keys back to human page codes and never emits GUIDs.

`ScadaProject.Scenes` remains authoritative for page inventory and metadata. Native pages do not require imported HTML; imported Wonderware projections remain optional provenance-backed inputs. A new `Default` page starts with `IncludeInBuild = false`.

`ProjectGeneration1Converter` (project format generation 0 → 1) settles a keyless page's `PageKey` once, at open, using `PageKeyFactory.CreateDeterministic(projectName, pageCode)` — the exact derivation `PageWorkspaceController.CreateImportedPageReferences` already uses elsewhere, so a converted page carries the identity the rest of the product would have produced for it. `ModernProjectMigration.NormalizeIdentity`/`ResolveTargetKey` remain: they are the store's identity normalizer, exercised on every save and on in-memory construction from an import inventory, not only at load, so a once-at-open file converter cannot replace them (see `KNOWN_GAPS_V2.md`, entry on the re-scoped Task 7 deletion).

## 5. Backward Format Refusal

`DEC-0049` D5 is implemented **for `project.json` only**. Chaque module persisté (`project.json`, une scène, le catalogue de tags, un composant `.sep`) porte un champ `FormatVersion` optionnel dans son enregistrement de domaine, exposé par `ScadaFormatGeneration` (`Domain/Projects/ScadaFormatGeneration.cs`) pour la génération courante attendue de chacun, et une scène/un catalogue de tags sérialise et désérialise ce champ à l'aller-retour. Mais **le refus n'est câblé que pour `project.json`** : `ProjectWorkspaceRepository.OpenAsync` est le seul site de production qui lit une génération déclarée et refuse. `ArtifactModule.Scene`, `.TagCatalog` et `.Component` n'ont aucune référence de production (`KNOWN_GAPS_V2.md` entrée 35) — rien ne construit jamais un `ArtifactToConvert` pour eux, et `ScadaScene.FormatVersion`/`ScadaTagCatalog.FormatVersion` ne sont lus par aucun code décisionnel. Un lecteur de cette section ne doit pas en conclure que les quatre modules sont gardés : trois sur quatre ne le sont pas aujourd'hui. L'absence du champ dans un fichier vaut génération zéro : tout artefact écrit avant ce mécanisme reste lisible sans être réécrit.

`ProjectWorkspaceRepository.OpenAsync` lit la génération déclarée du fichier `project.json` avant toute désérialisation, via `ArtifactFormatVersionReader.ReadFormatVersion` (`Infrastructure/ModernProjects/ArtifactFormatVersionReader.cs`), qui parcourt le document JSON brut sans jamais matérialiser un `ScadaProject`. Si la génération déclarée dépasse `ScadaFormatGeneration.Project`, l'ouverture est refusée avant toute activation : le diagnostic `project.format-too-new` est retourné et aucun `ProjectLoadCandidate` n'est produit. Le fichier n'est ni lu au-delà de ce pré-scan, ni réécrit.

Ce refus ferme une perte de données réelle : un binaire antérieur à `DEC-0050` Phase 1 ignorait silencieusement `QuickWindows`/`QuickWindowInvocations` inconnus à la désérialisation et les réécrivait absents dès la première sauvegarde. Avec le refus, un binaire qui ne comprend pas un fichier ne peut plus l'altérer.

## 6. Related Tests

1. `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
2. `tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`
3. `tests/ScadaBuilderV2.Tests/PageIdentityTests.cs`
4. `tests/ScadaBuilderV2.Tests/ModernProjectAtomicSnapshotTests.cs`
5. `tests/ScadaBuilderV2.Tests/PageLifecycleIntegrationTests.cs`
6. `tests/ScadaBuilderV2.Tests/ModernProjectSceneCacheTests.cs`
6. `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowDomainTests.cs`
7. `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowBindingTests.cs`
8. `tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowStoreTests.cs`
9. `tests/ScadaBuilderV2.Tests/Formats/ArtifactFormatVersionReaderTests.cs`
10. `tests/ScadaBuilderV2.Tests/Formats/BackwardRefusalTests.cs`
11. `tests/ScadaBuilderV2.Tests/ParameterSheetStoreTests.cs`
