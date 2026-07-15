# Outils UI d'authoring des tableaux et verrouillage Element+ - Specification de conception

Date: 2026-07-15
Status: Draft - pending approval
Document version: `V2.1.4.0022`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0022` | `PENDING` | Precision complete du ruban Tableau, du verrouillage persistant de tous les Element+, des groupes, des surfaces d'etat et du decoupage cible en classes et methodes. |
| 2026-07-15 | `V2.1.4.0021` | `f77aedb` | Creation de la specification autonome des outils UI d'authoring des tableaux; le chantier approuve et implemente couvert par `DEC-0039` demeure immuable. |

Cette specification ouvre un nouveau chantier produit consacre aux outils UI d'authoring des tableaux. Le Tableau Element+ deja implemente, documente dans [Tableau moderne et ruban Inserer hierarchique](2026-07-14-modern-table-and-insert-ribbon-design.md), est une dependance existante et non une specification a etendre ou a modifier. Le document approuve et implemente demeure immuable.

## 1. Ecart reel confirme

Les outils independants `Texte`, `Entree texte` et `Entree numerique` existent deja et le texte expose police, taille, gras et italique. Ils restent des Element+ autonomes : ils ne permettent ni de definir une cellule structurelle ni de maintenir une grille lors d'un redimensionnement. Ils ne remplacent donc pas l'outillage Tableau.

Le noyau Tableau persiste deja les trois types de contenu et les proprietes d'input sans `ValueBindings` cellule par cellule. En revanche, les ecarts suivants sont observes dans l'interface actuelle :

1. la commande `Donnees > Tableau` ouvre un dialogue de creation avant placement;
2. la selection et le redimensionnement internes ne sont pas suffisamment appropries par le mode Tableau : un clic de cellule peut encore declencher le deplacement de l'Element+;
3. `Verrouiller` est affiche pour la selection globale, mais son etat n'est pas encore applique aux gestes de transformation;
4. le panneau Tableau, le dialogue de proprietes et `CellFormatDialog` n'exposent qu'une partie du contenu, du format et des dimensions disponibles dans le modele;
5. il n'existe pas de selection de portee explicite ni d'en-tetes de rangee/colonne cliquables visibles;
6. le modele ne distingue pas encore les quatre bordures d'une cellule, le retour a la ligne et la hauteur de ligne;
7. les actions de dimensions precises, de distribution proportionnelle, d'ajustement au contenu et de gestion complete des en-tetes ne sont pas accessibles;
8. `ScadaElement` ne possede aucun etat `IsLocked` persistant;
9. le bouton `object.lock` du ruban Selection est enregistre comme commande desactivee;
10. `ToggleSelectionLockCommand` ne verrouille que l'objet transitoire `SelectionState`, sans proteger les mouvements ni survivre a la sauvegarde;
11. le bouton `Lock` de la barre superieure est lie a un simple booleen de `MainWindow`, sans synchronisation avec les Element+ selectionnes.

## 2. Objectif

Completer le Tableau Element+ pour qu'un auteur puisse reconstruire une page equivalente visuellement a `win00012` avec des tableaux structurels, du texte et des inputs locaux, sans bindings de cellules et sans devoir dessiner manuellement des centaines de lignes et rectangles.

Cette specification ne prevoit pas la conversion automatique de `win00012`; elle rend sa reconstruction manuelle rapide, precise et maintenable.

Le chantier rend aussi le verrouillage de position reel pour tous les Element+ de scene. Cette capacite transversale est necessaire au Tableau, mais son contrat ne doit contenir aucune exception propre au type Tableau.

## 3. Ruban Tableau et creation sans dialogue

### 3.1 Point d'entree et bouton Ajouter

1. La commande canonique reste `insert.table`.
2. Dans `Inserer > Donnees`, son libelle devient `Ajouter un tableau`.
3. L'activation de `insert.table` ouvre le ruban contextuel `Tableau` sans afficher de dialogue et sans exiger qu'un tableau existe deja.
4. Le premier groupe du ruban `Tableau` s'appelle `Tableau` et contient toujours un bouton `Ajouter un tableau`.
5. Ce bouton demeure visible lorsqu'un tableau existant est selectionne afin de permettre la creation d'un second tableau sans revenir au ruban Inserer.
6. Un clic sur `Ajouter un tableau` arme le placement. Le prochain clic valide dans le canvas cree le tableau, le selectionne et active automatiquement le mode `Cellules`.
7. `Escape` annule uniquement le placement en cours et ne modifie pas la scene.

### 3.2 Configuration avant placement

Le ruban expose avant placement `Rangees`, `Colonnes`, `Rangees d'en-tete` et les presets. Les limites restent 1 a 64, avec le preset 6 x 8 et une rangee d'en-tete. Ces valeurs appartiennent a la session d'authoring, pas a `MainWindow` et pas au projet tant qu'aucun tableau n'est place.

### 3.3 Etats de la surface

| Contexte | Ajouter un tableau | Configuration initiale | Outils cellule/structure/format |
| --- | --- | --- | --- |
| Ruban Tableau ouvert, aucun tableau selectionne | Active | Active | Desactives avec raison `Selectionnez un tableau`. |
| Tableau selectionne | Actif | Active pour le prochain tableau | Actifs selon la cellule, plage ou piste selectionnee. |
| Placement arme | Actif et visuellement selectionne | Active | Desactives jusqu'au placement. |
| Element+ non-Tableau selectionne hors placement | Accessible par `Inserer > Donnees` | Non affichee | Ruban contextuel Tableau masque. |

Le ruban Tableau contient au minimum les groupes `Tableau`, `Mode`, `Contenu`, `Structure`, `Format`, `Bordures` et `Dimensions`. `TablePropertiesDialog` reste une surface detaillee ouverte explicitement; il ne participe pas au flux obligatoire de creation.

## 4. Interaction Tableau fiable

### 4.1 Proprietaire exclusif des gestes

Le Tableau possede deux modes editor-only et non persistants :

| Mode | Proprietaire des gestes | Effet |
| --- | --- | --- |
| `Objet` | Interaction Studio Element+ | Selection et transformations externes permises selon le verrouillage de position. |
| `Cellules` | Editeur Tableau | Selection, edition, menus et redimensionnement des pistes internes; aucun geste ne deplace le tableau. |

1. Un double-clic, le bouton `Editer cellules` ou le placement d'un nouveau tableau entre en mode `Cellules`.
2. `Escape` termine d'abord l'edition en cours, puis quitte le mode `Cellules` vers `Objet`.
3. En mode `Cellules`, le bridge intercepte en phase capture `pointerdown`, `pointermove`, `pointerup`, clic droit et clavier avant le mecanisme Studio.
4. Un clic selectionne une cellule; `Shift+clic` et le glissement selectionnent une plage rectangulaire.
5. Des en-tetes editor-only de rangees et de colonnes selectionnent une piste complete et exposent son separateur.
6. Les separateurs internes ont priorite de hit-testing sur les cellules et sur le conteneur de transformation.
7. Le mode, les en-tetes, les surbrillances et les separateurs ne sont jamais persistants ni exportes.

### 4.2 Tableau verrouille

Un Tableau verrouille reste selectionnable. Son deplacement externe est refuse, mais le mode `Cellules` demeure disponible : contenu, format, fusion, bordures et dimensions de pistes internes restent authorables. Le verrouillage n'est donc jamais utilise comme substitut au routage correct des gestes de cellules.

## 5. Verrouillage de position de tous les Element+

### 5.1 Etat persistant

1. `ScadaElement` recoit `bool IsLocked = false` a la fin de son contrat serialise.
2. L'absence du champ dans un ancien projet signifie `false`; aucune migration destructive n'est requise.
3. Tout nouvel Element+ est deverrouille par defaut.
4. Le verrouillage est une propriete d'authoring persistante dans la scene. Il ne devient ni comportement runtime ni geometrie `.sb2` ou `.sep`.
5. La selection n'est jamais verrouillee : `SelectionState.IsSelectionLocked` et la commande transitoire `selection.toggle-lock` sont decommissionnes.

Le contrat de cette tranche est un verrouillage de position. Il interdit toute modification de X/Y par glissement, clavier, panneau de proprietes ou mouvement de groupe. Il n'interdit pas la selection, le contenu, le style, les evenements, les dimensions du Tableau, ni le redimensionnement interne de ses rangees et colonnes.

### 5.2 Groupes et descendants

1. Verrouiller un groupe applique `IsLocked = true` au groupe et a tous ses descendants, recursivement.
2. Deverrouiller un groupe applique `IsLocked = false` au groupe et a tous ses descendants.
3. La fermeture de selection d'un groupe comprend le groupe, ses enfants et tous les groupes imbriques.
4. Un groupe ne peut pas etre deplace si lui-meme ou au moins un descendant est verrouille.
5. Grouper conserve l'etat individuel des enfants; le nouveau groupe est deverrouille par defaut. La presence d'un enfant verrouille bloque neanmoins le mouvement du groupe.
6. Degrouper conserve l'etat de verrouillage de chaque enfant.
7. Copier ou dupliquer preserve `IsLocked`; une nouvelle insertion reste deverrouillee.

### 5.3 Semantique du toggle en multiselection

Pour la selection courante, le coordinateur calcule la fermeture recursive des cibles :

1. si au moins une cible est deverrouillee, l'etat visuel du toggle est `Deverrouille` et un clic verrouille toutes les cibles;
2. si toutes les cibles sont verrouillees, l'etat visuel est `Verrouille` et un clic deverrouille toutes les cibles;
3. sans selection Element+, la commande est desactivee et l'indicateur est neutre;
4. une seule mutation de scene et une seule entree undo/redo couvrent toute l'operation.

### 5.4 Surfaces partagees

1. Le ruban `Selection` active le bouton existant `Verrou` sous l'id stable `object.lock`. Il s'agit d'un toggle pilote par l'etat reel des objets.
2. Le panneau droit, onglet `Propriete` de l'Element+, ajoute une case `Verrouillage`. En multiselection elle est cochee si tous sont verrouilles, decochee si aucun ne l'est et indeterminee si l'etat est mixte. Cliquer l'etat mixte verrouille toute la selection.
3. Le bouton `Lock` de la barre superieure est deplace dans un conteneur aligne a droite, immediatement a gauche du texte `SCADA Builder V2`.
4. Ce bouton est a la fois avertisseur d'etat et raccourci vers `object.lock`. Il ne possede aucun etat local.
5. `Lock` affiche l'etat verrouille uniquement lorsque toute la fermeture de selection est verrouillee; un etat mixte s'affiche deverrouille, conformement au ruban Selection.
6. Les trois surfaces consomment le meme `ElementLockSelectionState` et la meme commande; aucune ne duplique la logique d'agregation.

### 5.5 Defense contre les mouvements

Le WebView bloque le debut du drag lorsque `data-editor-locked="true"`. Cette protection visuelle n'est pas suffisante : l'Application revalide chaque demande de translation avant toute mutation. Les mouvements pointeur, fleches clavier, edition X/Y et mouvements normalises vers un groupe utilisent tous le meme garde `CanTranslate`. Une demande refusee ne modifie ni la scene, ni le dirty state, ni l'historique.

## 6. Editeur de type et contenu de cellule

Le groupe `Contenu`, le panneau droit et le dialogue detaille doivent partager un meme editeur pour la portee active :

| Type | Champs exposes |
| --- | --- |
| Texte | Texte initial. |
| Input texte | Valeur initiale, placeholder, lecture seule. |
| Input numerique | Valeur initiale, placeholder, lecture seule, minimum, maximum et pas. |

1. La conversion conserve les champs communs lorsque possible; les champs incompatibles sont clairement remis a leur valeur par defaut et l'operation est annulable.
2. Une portee multiple applique le type choisi a toutes les cellules couvertes. Les valeurs distinctes sont affichees `Mixte` et ne sont ecrasees que si l'utilisateur renseigne le champ.
3. Le contenu reste porte par `ScadaTableCellContent`; aucun Element+ enfant, tag, `ReadTagId`, `WriteTagId` ou `ValueBindings` cellule par cellule n'est ajoute.
4. Preview et export continuent a rendre des `<input type="text">` et `<input type="number">` HTML natifs sous le namespace de page existant.

## 7. Inspecteur de format et portees explicites

Le panneau `Propriete` doit toujours afficher la portee active et permettre de la changer explicitement parmi : `Tableau`, `Rangees d'en-tete`, `Rangees alternees`, `Rangee(s)`, `Colonne(s)`, `Cellule` et `Plage`.

Le format complet comprend :

1. police, taille, gras et italique;
2. alignement horizontal et vertical;
3. padding;
4. couleur de texte et de fond;
5. style, couleur et epaisseur de grille;
6. retour a la ligne et hauteur de ligne;
7. `Heriter/Reinitialiser` par propriete et par portee.

`Heriter/Reinitialiser` supprime la surcharge nullable de la portee cible et revele la valeur issue de la precedence existante. Il ne remplace jamais explicitement une couleur par une transparence ou une largeur par zero.

Le contrat `ScadaTableFormat` est etendu de facon retrocompatible avec `TextWrap` et `LineHeight` nullables. Les valeurs absentes de projets existants conservent le rendu actuel.

## 8. Bordures avancees

La grille uniforme existante reste le raccourci de base. Le format ajoute une description nullable de bordures pour :

1. contour exterieur du tableau;
2. grille interieure horizontale et verticale;
3. haut, droite, bas et gauche d'une cellule ou d'une plage.

Chaque bordure definit independamment style, couleur et epaisseur. Les presets `Aucune`, `Toutes`, `Contour`, `Interieures`, `Haut`, `Droite`, `Bas` et `Gauche` sont accessibles depuis le ruban et le menu contextuel.

Pour eviter les conflits de rendu, une arrete physique n'a qu'une valeur effective : une bordure horizontale interne est portee par le bas de la cellule au-dessus et une bordure verticale interne par la droite de la cellule a gauche; le contour est porte par le tableau ou la cellule de bord. Le moteur normalise les selections multi-cellules vers ces aretes et preserve les bordures visibles lors d'une fusion ou d'une defusion.

Le modele introduira des valeurs dediees, nullable et serialisables (par exemple `ScadaTableBorder` et `ScadaTableBorders`) sans supprimer `GridColor`, `GridWidth` et `GridStyle`. Ces derniers restent le fallback pour les documents historiques et les actions rapides.

## 9. Dimensions precises et en-tetes

Le groupe `Dimensions` expose :

1. position X/Y et largeur/hauteur exactes du tableau;
2. largeur d'une ou plusieurs colonnes et hauteur d'une ou plusieurs rangees;
3. `Uniformiser`, qui repartit egalement la taille existante sur les pistes selectionnees;
4. `Distribuer proportionnellement`, qui applique une taille totale cible tout en conservant les ratios des pistes selectionnees;
5. `Ajuster au contenu`, base sur texte, placeholder et valeur initiale connus a l'authoring, jamais sur une valeur runtime inconnue.

Les separateurs internes et les dialogues numeriques utilisent les memes contraintes minimales. Chaque operation est atomique et produit une seule action undo/redo.

Les en-tetes permettent :

1. de marquer ou demarquer une ou plusieurs rangees;
2. plusieurs rangees d'en-tete consecutives;
3. la fusion de titres de sections dans ces rangees;
4. un style global d'en-tete et des bandes alternees configurables;
5. le rendu semantique `<th>` de toutes les rangees marquees, avec les spans valides.

## 10. Decoupage logiciel cible

### 10.1 Regles de dependance

1. Domain porte les valeurs persistantes et operations pures.
2. Application porte les sessions d'authoring, commandes, enablement, verrouillage, diagnostics et mutations annulables.
3. App porte seulement les view models WPF, dialogues et adaptateurs WebView.
4. Rendering consomme le modele sans connaitre les modes, selections ou verrous d'editeur.
5. `MainWindow` branche le workspace actif et transmet les intentions; aucune regle de Tableau ou de verrouillage n'y est calculee.

### 10.2 Classes Domain

| Classe | Statut | Methodes structurantes | Responsabilite |
| --- | --- | --- | --- |
| `ScadaElement` | Etendue | propriete `IsLocked` | Persister le verrouillage de position de chaque Element+. |
| `ScadaScene` | Etendue | `WithElementLockStateRecursive(ids, isLocked)`, `IsMovementLocked(id)`, `ExpandSelectionClosure(ids)` | Appliquer le verrouillage aux groupes et descendants et resoudre les ancetres verrouilles. |
| `ScadaTableFormat` | Etendue | valeurs nullables `TextWrap`, `LineHeight` et bordures par arete | Porter les nouvelles valeurs sans casser les anciens JSON. |
| `ScadaTableBorder` / `ScadaTableBorders` | Nouvelles | valeurs style, couleur, epaisseur et aretes `Top/Right/Bottom/Left` | Serialiser les bordures avancees et conserver la grille uniforme comme fallback. |
| `ScadaTableOperations` | Etendue | `SetContentKind`, `ApplyFormat`, `ApplyBorders`, `EqualizeTracks`, `DistributeTracks`, `AutoFitTracks`, `SetHeaderRows` | Executer les mutations pures et valider les invariants de grille. |
| `ScadaTableBorderResolver` | Nouvelle | `ResolveEdge`, `NormalizeRangeEdges` | Garantir une valeur effective unique par arete physique. |

Aucune de ces classes ne reference WPF, WebView, presse-papiers ou historique.

### 10.3 Classes Application

| Classe | Statut | Methodes structurantes | Responsabilite |
| --- | --- | --- | --- |
| `TableAuthoringSession` | Nouvelle | `ActivateInsert`, `BeginPlacement`, `SelectTable`, `EnterCellMode`, `ExitCellMode`, `SetRange`, `Clear` | Posseder l'etat temporaire du ruban Tableau et du mode Cellules. |
| `TableEditCoordinator` | Etendue | `Apply(element, request)` | Rester le point unique de validation des editions Tableau. |
| `TableContextMenuProvider` | Etendue | `Build(table, selection, scope, canPaste)` | Produire commandes et enablement sans logique WPF. |
| `TableRibbonStateProvider` | Nouvelle | `Build(session, selectedElement)` | Construire les groupes, etats actifs et raisons de desactivation du ruban contextuel. |
| `ElementLockSelectionState` | Nouveau record | `HasSelection`, `AllLocked`, `IsMixed`, `TargetIds` | Transporter l'etat agrege sans dependance WPF. |
| `ElementLockMutation` | Nouveau record | `BeforeScene`, `AfterScene`, `ChangedElementIds`, `NextLockedState` | Transporter une mutation complete et annulable vers la commande et l'historique. |
| `ElementLockCoordinator` | Nouvelle | `ResolveSelectionState(scene, ids)`, `CreateToggleMutation(scene, ids)` | Agreger l'etat, determiner verrouiller/deverrouiller et produire la mutation recursive. |
| `ElementTransformGuard` | Nouvelle | `CanTranslate(scene, ids)` | Refuser uniformement les mouvements verrouilles, incluant groupes et ancetres. |
| `ToggleElementLockCommand` | Nouvelle | `CanExecute(context)`, `ExecuteAsync(context, token)` | Implementer l'id `object.lock` et publier le resultat structure. |
| `ElementLockChangedAction` | Nouvelle | `UndoAsync`, `RedoAsync` | Restaurer exactement tous les etats de verrouillage touches. |

`ToggleSelectionLockCommand` est retire; il ne doit pas etre adapte pour continuer a verrouiller `SelectionState`. `TableEditRequest` et `TableEditKind` sont etendus pour les nouvelles operations plutot que de multiplier des handlers UI.

### 10.4 Classes App/WPF

| Classe | Statut | Methodes structurantes | Responsabilite |
| --- | --- | --- | --- |
| `TableRibbonViewModel` | Nouvelle | `Refresh`, `AddTable`, `ExecuteTableCommand` | Exposer la session, le bouton Ajouter et les commandes contextuelles. |
| `TablePropertiesViewModel` | Nouvelle | `LoadSelection`, `ApplyContent`, `ApplyFormat`, `ResetOverride` | Alimenter le panneau droit et les valeurs mixtes. |
| `TableEditorController` | Etendue | `HandleCommand`, `OpenDetailedProperties`, `OpenCellFormat` | Adapter les dialogues et le presse-papiers; ne pas posseder les invariants. |
| `TableWebViewMessageAdapter` | Nouvelle | `TryParse(json, out request)` | Convertir les messages JS en requetes typees. |
| `TableWebViewScript` | Etendue | `render` et emission des messages contractuels | Hit-testing, overlays et feedback live editor-only. |
| `ElementLockStateViewModel` | Nouvelle | `Refresh(state)`, `ToggleCommand` | Fournir un etat commun au ruban, au panneau Propriete et au bouton Lock superieur. |

`TablePropertiesDialog` et `CellFormatDialog` consomment `TablePropertiesViewModel` ou les memes requetes typees. Ils ne construisent pas directement un nouveau `ScadaTableDefinition`.

### 10.5 Messages WebView types

Les ids de messages existants restent stables pendant la migration :

| Type bridge | DTO Application/App | Traitement |
| --- | --- | --- |
| `tableSelection` | `TableSelectionRequest` | Met a jour `TableAuthoringSession.SetRange`. |
| `tableCellEdit` | `TableContentEditRequest` | Devient un `TableEditRequest` valide. |
| `tableTrackResize` | `TableTrackResizeRequest` | Produit une seule mutation a la fin du geste. |
| `moveSelectionBy` / geometrie | `ElementTranslationRequest` | Passe obligatoirement par `ElementTransformGuard.CanTranslate`. |

Le JSON brut n'est traite que par `TableWebViewMessageAdapter`. Le switch general de `MainWindow` ne doit pas contenir les validations de plages, de bordures, de verrouillage ou de groupes.

### 10.6 Points d'integration autorises dans MainWindow

Les seules nouvelles methodes de delegation permises dans `MainWindow` sont :

1. `ActivateTableAuthoringSurface()`;
2. `ForwardTableWebViewMessage(request)`;
3. `CommitTableMutation(result)`;
4. `ExecuteObjectLockCommandAsync()`;
5. `RefreshElementLockIndicators()`.

Ces methodes peuvent resoudre la scene et l'onglet actifs, pousser une action deja preparee et demander un rerendu. Elles ne peuvent ni parcourir les cellules, ni calculer une fermeture de groupe, ni choisir le prochain etat du toggle, ni modifier directement `IsLocked`.

## 11. Flux contractuels

### 11.1 Ajouter un tableau

`insert.table` -> `TableAuthoringSession.ActivateInsert` -> `TableRibbonViewModel` -> `BeginPlacement` -> clic canvas -> creation Domain -> action d'historique -> selection du tableau -> `EnterCellMode`.

### 11.2 Verrouiller une selection

Ruban, panneau ou Lock superieur -> `object.lock` -> `ElementLockCoordinator.ResolveSelectionState` -> `CreateToggleMutation` -> `ElementLockChangedAction` -> scene dirty -> rafraichissement des trois surfaces -> rerendu editor-only.

### 11.3 Deplacer un objet

Pointeur, clavier ou X/Y -> `ElementTranslationRequest` -> `ElementTransformGuard.CanTranslate` -> refus sans effet ou mutation de geometrie existante -> historique.

## 12. Persistance et export

1. `IsLocked` survit sauvegarde/recharge et undo/redo.
2. Les anciennes scenes sans champ restent deverrouillees.
3. Preview d'edition peut projeter `data-editor-locked` pour le hit-testing.
4. Rendering `.sb2` et `.sep` n'emet ni attribut runtime de verrouillage, ni CSS, ni handle, ni overlay associe.
5. Les nouvelles valeurs Tableau restent produites par le meme modele pour preview et export.
6. Aucun `ValueBindings` cellule par cellule n'est ajoute.

## 13. Tests et validation

### 13.1 Tableau

1. `insert.table` ouvre le ruban Tableau sans dialogue et le bouton `Ajouter un tableau` fonctionne sans selection preexistante.
2. Le bouton reste disponible avec un tableau selectionne.
3. Les commandes contextuelles sont desactivees avec raison sans tableau.
4. Le mode Cellules capture les gestes sans mouvement de l'objet.
5. Contenu, format, bordures, pistes et en-tetes couvrent sauvegarde, historique, preview et export.

### 13.2 Verrouillage

1. ancien JSON sans `IsLocked` -> faux; round-trip vrai/faux;
2. verrouillage et deverrouillage recursifs d'un groupe imbrique;
3. conservation des verrous au groupement, degroupement et duplication;
4. agregation simple, totale et mixte de la multiselection;
5. etat mixte affiche deverrouille dans les toggles et indetermine dans le panneau;
6. un clic mixte verrouille toutes les cibles; un clic totalement verrouille les deverrouille;
7. drag, fleches, X/Y et mouvement de groupe sont refuses si une cible effective est verrouillee;
8. refus sans dirty state ni historique;
9. undo/redo atomique de toute la fermeture de selection;
10. selection, proprietes et edition interne d'un Tableau verrouille restent actives.

### 13.3 Architecture et surfaces

1. test du catalogue : `object.lock` executable et `insert.table` libelle `Ajouter un tableau`;
2. test XAML : `Lock` est immediatement a gauche de `SCADA Builder V2` et les trois surfaces partagent le meme view model;
3. test de bridge : les messages Tableau passent par l'adaptateur type;
4. test d'architecture : aucune operation `ScadaTableOperations`, fermeture recursive, aggregation de lock ou decision de toggle dans `MainWindow`;
5. test export : aucun artefact de verrouillage ou d'edition dans `.sb2`/`.sep`;
6. verification interactive isolee des gestes pointeur, groupes, multiselection et tableaux.

## 14. Hors scope

1. formules, tri, filtre et remplissage Excel;
2. import/export CSV ou Excel;
3. bindings par cellule ou persistance runtime des inputs;
4. conversion automatique de `win00012`;
5. verrouillage des objets source/legacy non convertis;
6. permissions utilisateur ou verrouillage collaboratif;
7. protection par mot de passe;
8. verrouillage du redimensionnement, de la rotation ou des edits de contenu : cette tranche verrouille le deplacement X/Y.

## 15. Decisions produit consignees dans ce draft

1. Le ruban Tableau possede un bouton `Ajouter un tableau` utilisable sans tableau selectionne et toujours visible lorsqu'un tableau est actif.
2. La creation n'ouvre aucun dialogue.
3. Les modes `Objet` et `Cellules` possedent exclusivement leurs gestes.
4. Le verrouillage appartient a chaque `ScadaElement`, jamais a `SelectionState`.
5. Le verrouillage s'applique a tous les Element+ de scene et se propage recursivement lors du toggle d'un groupe.
6. Un etat mixte s'affiche deverrouille dans les toggles; cliquer verrouille toute la selection.
7. `object.lock` est la commande canonique partagee par le ruban Selection, le panneau Propriete et le bouton Lock superieur.
8. Le bouton Lock superieur est place immediatement a gauche de `SCADA Builder V2`.
9. Le verrouillage empeche le deplacement X/Y tout en permettant l'edition interne du Tableau.
10. `MainWindow` reste limite aux branchements de haut niveau enumeres en section 10.6.

Cette specification reste Draft jusqu'a approbation. Son plan d'implementation sera un nouveau document et ne modifiera pas le plan du Tableau moderne deja implemente.
