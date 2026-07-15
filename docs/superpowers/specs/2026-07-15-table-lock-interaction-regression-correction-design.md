# Correction des interactions Tableau et du verrou Element+ - Specification

Date: 2026-07-15
Status: Draft - approbation requise avant implementation
Document version: `V2.1.4.0032`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0032` | `ff21e33` | Specification corrective autonome pour le drag verrouille, le mode initial Tableau, l'acces aux cellules/pistes et l'etat effectif des reperes A/1. |

## 1. Objet et relation avec les specifications existantes

Cette specification corrige des regressions observees apres l'implementation de `DEC-0040`. Elle est volontairement distincte de `docs/superpowers/specs/2026-07-15-table-ui-authoring-and-element-lock-design.md`, qui demeure approuvee et implementee sans modification retrospective.

Elle precise ou remplace seulement les comportements d'interaction suivants :

1. la projection editor-only de `IsLocked` vers le WebView;
2. le refus visuel immediat d'un drag de position verrouille;
3. le mode actif apres creation ou reselection d'un Tableau;
4. l'arbitrage des pointeurs entre l'objet Tableau et ses cellules/separateurs;
5. la signification effective de `Afficher/Masquer A/1`;
6. les resizes d'un objet verrouille qui impliqueraient une translation.

Le modele persistant, le format `.sb2`, le contrat TF100Web, les contenus Tableau, les bordures, les en-tetes et les regles de groupes de `DEC-0040` restent inchanges.

## 2. Symptomes confirmes

1. Un Element+ ou un Tableau verrouille peut commencer un drag et se deplacer visuellement avant que l'Application refuse la mutation et rerende sa position initiale.
2. Un Tableau nouvellement cree entre directement en mode Cellules et ne peut donc pas etre positionne par drag alors qu'il est deverrouille.
3. Un resize depuis un bord nord ou ouest peut deplacer visuellement un objet verrouille avant le refus final.
4. Apres deselection/reselection, l'utilisateur ne sait pas quel mode possede le pointeur et peut perdre l'acces aux cellules ou aux separateurs de pistes.
5. Le libelle `Masquer A/1` peut etre affiche alors que les reperes sont deja caches par le mode Objet; `Afficher A/1` peut changer une preference sans rendre les reperes visibles.

## 3. Causes techniques etablies

### 3.1 Verrou absent du payload d'edition

`MainWindow.WebViewScript.cs` lit `element.IsLocked` pour produire `data-editor-locked`, mais le type `ModernElementRenderPayload` et `ToRenderPayload` ne projettent pas cette propriete dans la version implementee. Le JavaScript recoit donc `undefined`, traite l'objet comme deverrouille et demarre le drag. `ElementTransformGuard` bloque correctement la mutation C# plus tard, ce qui explique le retour a la position initiale au release.

### 3.2 Mode initial incompatible avec le positionnement

`TableAuthoringSession.CompletePlacement` force actuellement `TableInteractionMode.Cells`. Ce mode reserve les gestes internes aux cellules et separateurs; il rend le placement initial par drag impossible.

### 3.3 Etat A/1 stocke et etat A/1 visible confondus

`ShowEditorGuides` est une preference stockee, tandis que la feuille de style masque les guides en mode Objet. Le ruban utilise uniquement la preference pour son libelle et son etat toggle. Le controle peut donc annoncer l'inverse de l'etat reel.

### 3.4 Synchronisation partielle du WebView

Le mode et les guides sont envoyes par deux chemins independants. Les changements de selection, creation, verrou, rerender et commandes du ruban peuvent laisser le DOM avec un couple mode/guides different de la session Application.

## 4. Resultat utilisateur exige

### 4.1 Tableau nouvellement cree

1. `table.add` cree et selectionne le Tableau.
2. Le Tableau entre en mode Objet, jamais automatiquement en mode Cellules.
3. Tant qu'il est deverrouille, un drag de son corps le deplace immediatement.
4. Un double-clic sur une cellule ou la commande `Cellules` entre en mode Cellules.

Ce point remplace l'activation automatique du mode Cellules de la specification `DEC-0040`; la specification precedente n'est pas reecrite.

### 4.2 Reselection et modes

1. Une deselection ramene la session Tableau en mode Objet.
2. Une reselection simple d'un Tableau commence en mode Objet.
3. Le verrouillage ou deverrouillage ne change jamais le mode actif.
4. Le mode Objet donne le pointeur au cadre Element+.
5. Le mode Cellules donne priorite aux cellules, en-tetes et separateurs avant tout drag Element+.
6. `Escape` depuis le mode Cellules revient en mode Objet.
7. Le double-clic permettant d'entrer en mode Cellules fonctionne aussi lorsque le Tableau est verrouille.

### 4.3 Verrouillage de position

Pour tout Element+, Tableau compris :

1. un drag de position interdit ne demarre pas;
2. le wrapper ne bouge pas, aucun apercu de translation n'est affiche et aucun pointer capture de mouvement n'est conserve;
3. aucune mutation, entree d'historique ou dirty state n'est produite;
4. `ElementTransformGuard` reste la seconde defense obligatoire cote Application;
5. le clavier et l'edition numerique X/Y restent refuses selon `DEC-0040`;
6. la selection, les proprietes, le contenu, le style et les evenements restent accessibles.

Le verrou d'un groupe ou la presence d'un descendant verrouille bloque avant apercu tout drag ou resize de groupe qui deplacerait un descendant.

### 4.4 Resize et rotation d'un objet verrouille

1. Les dimensions peuvent changer seulement si X et Y restent strictement identiques pendant l'apercu et au commit.
2. Les poignees est, sud et sud-est restent disponibles pour un objet simple verrouille.
3. Les poignees nord, ouest, nord-ouest, nord-est et sud-ouest sont indisponibles lorsqu'elles exigeraient un changement de X ou Y.
4. La rotation reste disponible si le modele conserve X/Y.
5. Un resize de groupe contenant une cible verrouillee est bloque avant apercu si le calcul mettrait a l'echelle la position d'un descendant.
6. Le guard Application revalide toujours le resultat final, meme si le WebView a deja filtre le geste.

### 4.5 Cellules et pistes d'un Tableau verrouille

Le verrou porte sur la position de l'Element+, pas sur son contenu interne. En mode Cellules, un Tableau verrouille permet donc :

1. selection cellule, rangee, colonne, plage et tout le Tableau;
2. edition de contenu;
3. resize des colonnes et rangees;
4. fusion/defusion;
5. format, bordures, en-tetes et autres commandes Tableau existantes.

Les handlers Tableau doivent arreter la propagation avant les handlers Element+ des qu'une cible interne valide est reconnue.

### 4.6 Reperes A/1

Deux etats sont distingues :

- `ShowEditorGuides`: preference conservee dans la session;
- `EditorGuidesVisible`: etat effectif, defini par `Mode == Cells && ShowEditorGuides`.

Regles de surface :

1. le libelle et le toggle du ruban utilisent `EditorGuidesVisible`;
2. en mode Objet, la commande affiche `Afficher A/1`;
3. cliquer `Afficher A/1` depuis le mode Objet entre en mode Cellules et active les reperes;
4. cliquer `Masquer A/1` en mode Cellules masque les reperes sans quitter ce mode;
5. revenir en mode Objet masque toujours les reperes effectifs;
6. aucune gouttiere A/1 n'est exportee.

## 5. Contrats de synchronisation

### 5.1 Projection Element+ vers le document d'edition

La projection editor-only transporte explicitement `IsLocked` pour chaque element racine et descendant. Le WebView applique `data-editor-locked="true|false"` sur chaque wrapper.

Cette propriete appartient uniquement au payload de l'editeur. Elle ne doit pas etre ajoutee au HTML runtime, au `.sb2`, a la geometrie `.sep` ou aux overlays exportes.

### 5.2 Etat Tableau atomique

Le WebView recoit une seule synchronisation logique contenant :

```text
TableElementId, Mode, ShowEditorGuides, EditorGuidesVisible
```

Cette synchronisation est appliquee apres :

1. creation;
2. selection ou deselection;
3. changement Objet/Cellules;
4. bascule A/1;
5. rerender apres verrouillage;
6. fermeture de la surface Tableau.

Le DOM ne calcule pas le mode depuis le verrou et le verrou ne calcule pas le mode depuis le DOM.

## 6. Decoupage des classes et methodes

### 6.1 Application

| Classe | Changement | Methodes/proprietes |
| --- | --- | --- |
| `TableAuthoringSession` | Corriger les transitions | `CompletePlacement`, `SelectTable`, `SetMode`, `ToggleEditorGuides`, `EditorGuidesVisible` |
| `TableRibbonStateProvider` | Utiliser l'etat effectif | `Build` construit le libelle/toggle A/1 depuis `EditorGuidesVisible` |
| `ElementTransformGuard` | Conserver la defense finale | Aucun changement de contrat; ajouter seulement les regressions manquantes si necessaire |

### 6.2 App/WPF et bridge

| Classe/fichier | Statut | Responsabilite |
| --- | --- | --- |
| `EditorBridge/ModernElementRenderPayload.cs` | Nouveau type dedie | DTO editor-only, incluant `IsLocked` et les enfants |
| `EditorBridge/ModernElementRenderPayloadFactory.cs` | Nouvelle classe stateless | `Create(element, selectedIds, renderIndex)` projette recursivement le modele; aucune regle de scene |
| `TableEditor/TableEditorWebViewState.cs` | Nouveau record immuable | Snapshot mode/guides a envoyer au WebView |
| `TableEditor/TableEditorWebViewStateFactory.cs` | Nouvelle classe stateless | `Create(session)` calcule l'etat effectif testable |
| `MainWindow.TableIntegration.cs` | Integration haut niveau | `SyncTableEditorStateInWebViewAsync` transmet le snapshot; aucune duplication de regle |
| `MainWindow.xaml.cs` | Integration haut niveau | Appelle la factory de payload et la synchronisation apres creation/rerender |
| `MainWindow.WebViewScript.cs` | Gestes Element+ | Projette `data-editor-locked`, refuse move/resize interdit avant `modernDrag` et maintient X/Y invariants |
| `TableEditor/TableWebViewScript.cs` | Gestes Tableau | Applique atomiquement mode/guides et donne priorite aux cibles internes en mode Cellules |

Les types de payload sortent de `MainWindow.NestedTypes.cs`. Aucun calcul d'etat A/1, de politique de verrou ou de priorite Tableau n'est ajoute dans `MainWindow`.

## 7. Tests obligatoires

### 7.1 Tests automatises

1. `TableAuthoringSessionTests`: creation -> Objet; deselection/reselection -> Objet; lock sans effet sur le mode; bascule A/1 et etat effectif.
2. `ModernElementRenderPayloadFactoryTests`: `IsLocked` vrai/faux et projection recursive; serialisation contient la valeur attendue.
3. `TableEditorWebViewStateTests`: couples Objet/Cellules et preference/visibilite A/1.
4. `WebViewContextMenuScriptTests`: refus avant `modernDrag`/pointer capture, resize verrouille sans X/Y, priorite des cellules/separateurs et application atomique mode/guides.
5. `ElementTransformGuardTests`: aucune translation verrouillee; resize W/H a X/Y fixes accepte; resize de groupe avec descendant deplace refuse.
6. `Ft100SceneExporterTests` et `StudioElementPlusContractTests`: aucun `data-editor-locked`, `IsLocked` editor-only, header A/1 ou overlay dans les artefacts.

Les tests de source JavaScript ne suffisent pas pour le payload : au moins un test doit construire et serialiser le DTO reel.

### 7.2 Smoke interactif reproductible

Sur une copie isolee du projet :

1. creer un Tableau, le deplacer avant verrouillage;
2. verrouiller et tenter drag, fleches, X/Y, resize est/sud/sud-est, resize nord/ouest et rotation;
3. deselectionner/reselectionner, entrer en mode Cellules par commande et double-clic;
4. selectionner une cellule et une plage, puis resize une colonne et une rangee sur Tableau verrouille;
5. verifier les quatre etats A/1 : Objet, Cellules visible, Cellules masque, retour Objet;
6. repeter drag/resize sur Element+ simple, groupe verrouille et groupe mixte;
7. verifier undo/redo, sauvegarde/recharge et absence de mouvement visuel interdit.

## 8. Persistance et compatibilite

1. Aucun nouveau champ Domain ou JSON de scene.
2. `ScadaElement.IsLocked` conserve son contrat actuel.
3. Aucun changement au manifest, HTML runtime ou packaging `.sb2`.
4. Aucun changement au contrat TF100Web.
5. Aucun changement au contrat `.sep`.
6. Mode, selection interne et guides restent transitoires et editor-only.

## 9. Hors scope

1. verrouillage du contenu, du style, de la taille ou de la rotation;
2. nouveaux types de cellules, bindings ou formules;
3. refonte du rendu Tableau runtime;
4. modification generale du ruban;
5. migration de projet ou changement de format;
6. reecriture de la specification approuvee `DEC-0040`.

## 10. Criteres d'acceptation

La correction est complete uniquement si :

1. aucun Element+ verrouille ne bouge visuellement lors d'un drag refuse;
2. un Tableau neuf et deverrouille est deplacable immediatement;
3. un Tableau verrouille reste editable en mode Cellules, pistes comprises;
4. resize et rotation ne modifient jamais X/Y d'une cible verrouillee;
5. le libelle A/1 correspond toujours a la visibilite effective;
6. le payload reel transporte le verrou et sa couverture ne repose pas seulement sur une recherche de chaine;
7. aucun artefact editor-only n'entre dans `.sb2` ou `.sep`;
8. build, tests cibles, suite complete, verification documentaire et smoke interactif sont consignes.

L'approbation de cette specification devra etre enregistree comme une nouvelle decision succedant aux seuls comportements d'interaction concernes de `DEC-0040`.
