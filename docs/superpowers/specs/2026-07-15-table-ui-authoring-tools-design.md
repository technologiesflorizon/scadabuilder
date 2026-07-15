# Outils UI d'authoring des tableaux - Specification de conception

Date: 2026-07-15
Status: Draft - pending approval
Document version: `V2.1.4.0021`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0021` | `PENDING` | Creation de la specification autonome des outils UI d'authoring des tableaux; le chantier approuve et implemente couvert par `DEC-0039` demeure immuable. |

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
7. les actions de dimensions precises, de distribution proportionnelle, d'ajustement au contenu et de gestion complete des en-tetes ne sont pas accessibles.

## 2. Objectif

Completer le Tableau Element+ pour qu'un auteur puisse reconstruire une page equivalente visuellement a `win00012` avec des tableaux structurels, du texte et des inputs locaux, sans bindings de cellules et sans devoir dessiner manuellement des centaines de lignes et rectangles.

Cette specification ne prevoit pas la conversion automatique de `win00012`; elle rend sa reconstruction manuelle rapide, precise et maintenable.

## 3. Ruban contextuel sans dialogue de creation

1. Choisir `Inserer > Donnees > Tableau` active l'outil de placement et affiche une section secondaire `Outils Tableau`; aucun dialogue ne s'ouvre automatiquement.
2. Avant le premier placement, cette section propose les valeurs rapides `Rangees`, `Colonnes`, `Rangees d'en-tete` et les presets. Les valeurs sont valides entre 1 et 64; le preset reste 6 x 8 avec une rangee d'en-tete.
3. Le clic canvas place directement le tableau avec la configuration courante. L'annulation de l'outil abandonne cette configuration temporaire sans modifier la scene.
4. Lorsqu'un tableau est selectionne, le meme ruban devient contextuel et contient au minimum les groupes `Mode`, `Contenu`, `Structure`, `Format`, `Bordures` et `Dimensions`.
5. `TablePropertiesDialog` demeure une surface detaillee ouverte explicitement par `Proprietes...`; il ne doit jamais etre la consequence obligatoire de la selection de l'outil Tableau.
6. Le descripteur d'insertion Tableau doit devenir un placement direct configure par l'etat du ruban, et non un flux `DialogThenPoint`.

## 4. Interaction fiable, selection et verrouillage

Le Tableau possede deux modes editor-only, non persistants :

| Mode | Proprietaire des gestes | Effet |
| --- | --- | --- |
| `Objet` | Studio Element+ | Selection, deplacement et poignees externes du tableau entier. |
| `Cellules` | Editeur Tableau | Selection, edition et redimensionnement de pistes internes; aucun geste ne deplace le tableau. |

1. Un double-clic ou le bouton `Editer cellules` entre en mode `Cellules`; `Escape` termine l'edition, puis un second `Escape` revient au mode `Objet`.
2. En mode `Cellules`, le pont WebView intercepte et consomme `pointerdown`, mouvement, clic droit et clavier avant le mecanisme de deplacement Studio. Il ne propage aucun geste de cellule au conteneur de transformation.
3. Un clic selectionne une cellule; `Shift+clic` et le glissement selectionnent une plage rectangulaire. Les en-tetes de rangee et de colonne selectionnent leur piste complete. Les selections de plages restent internes et ne changent pas les modificateurs canoniques Studio Element+ hors tableau.
4. Les separateurs internes sont des zones visibles, accessibles et prioritaires au-dessus des cellules; leur geste n'est jamais interprete comme un deplacement de l'objet.
5. Le verrou global devient effectif : sur un Element+ verrouille, deplacement et redimensionnement externes sont refuses, avec feedback. L'edition semantique interne autorisee en mode `Cellules` (contenu, format, fusions, pistes) reste possible afin de ne pas transformer le verrou de position en verrou d'authoring.
6. Les en-tetes, surbrillances, separateurs, focus, modes et apercus de redimensionnement sont editor-only et exclus du preview exporte, `.sep` et `.sb2`.

## 5. Editeur de type et contenu de cellule

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

## 6. Inspecteur de format et portees explicites

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

## 7. Bordures avancees

La grille uniforme existante reste le raccourci de base. Le format ajoute une description nullable de bordures pour :

1. contour exterieur du tableau;
2. grille interieure horizontale et verticale;
3. haut, droite, bas et gauche d'une cellule ou d'une plage.

Chaque bordure definit independamment style, couleur et epaisseur. Les presets `Aucune`, `Toutes`, `Contour`, `Interieures`, `Haut`, `Droite`, `Bas` et `Gauche` sont accessibles depuis le ruban et le menu contextuel.

Pour eviter les conflits de rendu, une arrete physique n'a qu'une valeur effective : une bordure horizontale interne est portee par le bas de la cellule au-dessus et une bordure verticale interne par la droite de la cellule a gauche; le contour est porte par le tableau ou la cellule de bord. Le moteur normalise les selections multi-cellules vers ces aretes et preserve les bordures visibles lors d'une fusion ou d'une defusion.

Le modele introduira des valeurs dediees, nullable et serialisables (par exemple `ScadaTableBorder` et `ScadaTableBorders`) sans supprimer `GridColor`, `GridWidth` et `GridStyle`. Ces derniers restent le fallback pour les documents historiques et les actions rapides.

## 8. Dimensions precises et en-tetes

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

## 9. Limites d'architecture et compatibilite

1. Domain porte les valeurs de bordures, retour a la ligne, hauteur de ligne et invariants; Application porte les commandes de conversion, portee, dimensions et en-tetes; Rendering applique une seule projection preview/export.
2. App encapsule le mode cellule, le bridge WebView, les view models de ruban, le panneau droit et les dialogues dans des classes Tableau dediees et testables.
3. `MainWindow` ne recoit que l'activation de haut niveau, le routage du contexte actif et la delegation; aucune regle de selection, de bordure, de dimension, de conversion ou de format Tableau ne lui est ajoutee.
4. Aucun changement n'est apporte au contrat de racine `.sb2`, a la composition TF100Web ou a la persistance runtime des valeurs d'input.

## 10. Criteres d'acceptation

1. `Donnees > Tableau` n'ouvre aucun dialogue et le ruban secondaire permet de regler la structure avant placement.
2. En mode `Cellules`, clic, plage, clic droit et redimensionnement de piste ne deplacent jamais le tableau; en mode `Objet`, les transformations externes restent disponibles.
3. Le verrou empeche reellement les transformations externes sans bloquer l'authoring interne autorise.
4. Toute portee annoncee peut recevoir contenu et format, avec affichage `Mixte` et heritage par propriete.
5. Les trois types de cellule et tous leurs champs survivent sauvegarde/recharge et sont exportes sans bindings cellule par cellule.
6. Les bordures exterieures, interieures et par arete ont la meme geometrie et le meme style en preview et dans l'archive `.sb2`.
7. Les dimensions numeriques, uniformisation, distribution et ajustement au contenu respectent les contraintes et undo/redo.
8. Plusieurs en-tetes, titres fusionnes et bandes alternees sont visibles, persistants et rendus semantiquement.
9. Une reconstruction structurelle representative de `win00012` ne requiert pas la creation manuelle de centaines de lignes et rectangles.
10. La couverture automatisee inclut Domain, commandes/historique, persistance, rendu/export, interaction WebView et un test d'architecture interdisant les regles Tableau dans `MainWindow`; une verification interactive isolee valide les gestes pointeur et le verrou.

## 11. Decisions a approuver avant le prochain plan

1. Remplacer definitivement le dialogue automatique de creation par le ruban contextuel configure avant placement.
2. Adopter les modes explicites `Objet` et `Cellules` comme proprietaires exclusifs des gestes.
3. Definir le verrou comme un verrou de transformation externe, non comme un verrou de l'edition semantique interne.
4. Etendre le modele avec bordures par arete, retour a la ligne et hauteur de ligne, tout en maintenant la compatibilite des documents existants.
5. Maintenir hors scope les formules, bindings par cellule, synchronisation runtime, import Excel/CSV et conversion automatique de `win00012`.

Une fois ces decisions approuvees, cette nouvelle specification devra etre enregistree dans le registre de decisions, puis recevoir son propre plan d'implementation avec des tests dedies.
