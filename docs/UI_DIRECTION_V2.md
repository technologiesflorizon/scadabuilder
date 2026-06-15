# SCADA Builder V2 - Direction UI

Date: 2026-06-15
Statut: Draft de conception
Status: Draft de conception
Document version: `V2.1.1.0030`
Wireframe source: `docs/wireframes/wireframe_Scada_Builder_V2.png`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Ajout du header documentaire obligatoire et rattachement a l'arbre documentaire stable. |
| 2026-06-15 | `V2.0.0.0000` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

## 1. Intention generale

SCADA Builder V2 doit devenir un editeur desktop industriel, visuel et organise autour d'un workspace central.

Le layout cible separe clairement:

1. Commandes globales en haut.
2. Outils d'action et projet a gauche.
3. Scene SCADA active au centre.
4. Contexte/proprietes dynamiques a droite.
5. Etat, warnings et notifications en bas.

L'apparence doit rester en continuite avec le site Florizon Technologies:

1. Interface claire, moderne et professionnelle.
2. Palette derivee du site web Florizon.
3. Accent vert/turquoise utilise pour les actions primaires, selection et etats actifs.
4. Fond clair et panneaux blancs pour garder une lecture industrielle nette.
5. Typographie proche du site: titres type `Space Grotesk`, texte UI type `Source Sans 3` ou equivalent systeme si non embarque.

Palette de depart extraite du site:

1. Fond application: `#f7fbf5`
2. Surface/panneau: `#ffffff`
3. Texte principal: `#0f2a30`
4. Texte secondaire: `#4e6a71`
5. Accent primaire vert: `#90c030`
6. Accent secondaire turquoise: `#2090a0`
7. Accent doux: `#e0f2d0`
8. Bordures: `rgba(15,42,48,0.08-0.16)`
9. Ombres: `rgba(15,42,48,0.10-0.18)`

Adaptation V2:

1. Le SCADA Builder doit utiliser cette palette comme base, mais avec une densite plus elevee que le site public.
2. Les panneaux d'edition doivent rester plus compacts que des cartes marketing.
3. Les actions critiques et warnings doivent avoir des couleurs dediees distinctes du vert de marque.

Positionnement produit:

SCADA Builder V2 est un editeur industriel moderne.

Il doit combiner:

1. Ergonomie d'un outil de conception professionnel.
2. Robustesse d'un outil industriel.
3. Rendu web moderne.
4. Controle strict des sorties generees.
5. Architecture compartimentee pour eviter un monolithe UI.

Note de developpement:

Le developpement V2 ne doit pas viser uniquement le minimum technique. Lorsqu'un outil est ajoute, il doit etre suffisamment complet pour etre essaye dans un vrai flux de travail: commandes visibles, etats clairs, retour utilisateur, valeurs par defaut raisonnables et limites connues. Les prototypes sont acceptables pour derisquer une fonctionnalite, mais ils doivent evoluer rapidement vers des tranches verticales utilisables.

Contrainte ergonomique:

Toutes les boites contextuelles, menus flottants et inspecteurs ouverts dans la scene doivent etre deplacables par drag. Une zone de titre ou de poignee doit etre clairement disponible pour deplacer la boite sans declencher l'edition d'un champ. Ces boites doivent aussi rester fermables par un bouton visible de type `X` standard Windows et par `Escape`.

Objectif legacy majeur:

SCADA Builder V2 doit permettre de migrer progressivement des fichiers legacy Wonderware/ArchestrA vers notre format moderne.

L'extraction legacy est deja documentee et realisee dans le projet existant. V2 doit s'appuyer sur cette base au lieu de recommencer l'extraction, mais le modele extrait actuel ne devient pas le domaine officiel.

Decision:

1. SCADA Builder V2 doit avoir son propre domaine officiel.
2. `AMR_REF_SCADA` est une source legacy imparfaite et un corpus de regression, pas le modele cible.
3. Les pages legacy doivent etre ouvertes dans un outil integre de consultation/extraction.
4. Les elements utiles sont extraits et convertis en elements V2 modernes.
5. Le build moderne doit compiler depuis le modele V2, pas depuis le HTML legacy.

La valeur principale de V2 est de transformer les elements UI legacy en elements modernes:

1. Organiser les elements UI legacy.
2. Identifier les groupes logiques.
3. Convertir les elements legacy en UI Elements modernes.
4. Ameliorer visuellement les composants importes.
5. Remplacer progressivement les anciens assets par des composants propres.
6. Produire des elements modernes transportables entre scenes/projets.
7. Conserver la tracabilite avec la source legacy quand necessaire.
8. Generer un rendu web moderne deployable sur FT100.

La conversion doit etre progressive:

1. Import brut.
2. Inventaire et classification.
3. Groupement logique.
4. Nettoyage visuel.
5. Conversion en composant moderne.
6. Liaison aux mappings/actions.
7. Validation preview/build.
8. Export FT100.

Precision importante:

1. L'auto-detection doit detecter les elements atomiques.
2. L'auto-detection ne doit pas tenter de grouper automatiquement les composants logiques au depart.
3. Les groupements se font dans le domaine V2 apres conversion.
4. Les groupes valides peuvent ensuite etre ajoutes a la librairie V2.
5. Pour `win00008`, la source HTML non travaillee a privilegier est `03_web_legacy/html_pages/win00008_a0cf691217f4.html`.
6. `08_web_modernized/html_pages/win00008_updated.html` sert de reference de travail precedent/comparaison, pas de source brute.

Principe architectural:

1. L'UI ne doit pas contenir la logique metier.
2. Les actions utilisateur doivent passer par des commandes explicites.
3. Les panneaux sont des modules independants.
4. Le workspace, le projet, la selection, les proprietes, la librairie, le build et le scripting doivent etre des domaines separes.
5. Chaque domaine doit pouvoir etre teste sans lancer toute l'application.
6. Le rendu preview doit consommer le meme modele que le build.
7. Toute nouvelle fonctionnalite doit avoir une frontiere claire: UI, application, domaine, infrastructure.

Domaines legacy a isoler:

1. Import legacy.
2. Inventaire legacy.
3. Classification des elements.
4. Modernisation visuelle.
5. Conversion en composants modernes.
6. Mapping legacy vers modele V2.
7. Validation de fidelite visuelle.

Outils legacy integres:

1. `Legacy Viewer`: consultation read-only d'une scene legacy.
2. `Legacy Extraction Workspace`: selection/classification des elements a convertir.
3. `Compare View`: comparaison manuelle legacy vs scene V2.
4. `Modernization Pipeline`: conversion progressive vers elements V2.

## 2. Barre superieure

La barre superieure contient les familles de commandes:

1. `Fichier`
2. `Edition`
3. `Ecran`
4. `Selection`
5. Zone de commandes/ruban contextuel

Decision:

1. `Fichier` affiche le ruban des outils Fichier.
2. Pour `Edition`, `Ecran`, `Selection` et les autres menus ou une liste simple est plus adaptee, une liste deroulante standard peut etre utilisee.
3. Une approche type ruban AutoCAD est souhaitee pour les actions visuelles et frequentes.
4. Une icone `Outils` dans le ruban doit permettre d'acceder a tous les outils disponibles, incluant ceux presents dans la barre d'outils gauche.
5. Ce menu outil pourra servir plus tard a configurer certains outils.

## 3. Panneau gauche

Le panneau gauche est une zone verticale resizable/collapsible/fermable.

Il contient au minimum deux onglets:

1. `Outil`
2. `Projet`

## 3.1 Onglet Outil

Contient les outils d'action rapides:

1. Selection.
2. Deplacement.
3. Texte.
4. Image.
5. Groupe.
6. Lock.
7. Zoom.
8. Autres outils d'edition future.

Les outils doivent etre representes par icones avec tooltips.

## 3.2 Onglet Projet

Contient les parametres generaux du projet et la navigation projet.

Elements cibles:

1. Nom du projet.
2. Resolution/dimensions SCADA.
3. Liste des pages.
4. Actions page: nouvelle page, ouvrir, renommer, supprimer, dupliquer.
5. Strategie responsive du projet.

Decision:

1. Les pages doivent etre rapatriees dans la section `Projet`.
2. Chaque page ouverte doit apparaitre comme un onglet dans le workspace central.
3. La resolution SCADA doit etre globale au projet par defaut.
4. La resolution par page peut rester une option avancee plus tard, mais ne doit pas etre le comportement principal V2.

## 3.3 Strategie responsive projet

Le projet doit pouvoir definir comment les scenes SCADA s'adaptent selon les appareils.

Decision:

1. La resolution de base est globale au projet.
2. Le projet peut etre configure en mode:
   - `Desktop first`
   - `Tablet first`
   - `Mobile first`
3. Le mode choisi influence les breakpoints, le preview et les exports.
4. Le workspace doit permettre de previsualiser au moins:
   - Desktop
   - Tablet
   - Mobile
5. L'objectif est un SCADA moderne, consultable sur differents formats, sans sacrifier l'ergonomie desktop d'edition.

Approche proposee:

1. `Canvas size`: dimension logique de reference du SCADA.
2. `Responsive mode`: fixe, scale-to-fit, adaptive layout.
3. `Authoring mode`: desktop first / tablet first / mobile first.
4. `Preview device`: desktop / tablet / mobile.
5. `Safe area`: zones visibles et marges a respecter par profil.

## 3.4 Adaptive layout par scene

En mode `Adaptive layout`, une meme scene logique peut avoir plusieurs variantes de layout.

Exemple:

1. Scene `win00008`
   - Variante `Desktop`
   - Variante `Tablet`
   - Variante `Mobile`

Chaque variante represente la meme scene fonctionnelle, mais avec une disposition adaptee aux proportions du device cible.

Decision:

1. `Desktop`, `Tablet` et `Mobile` sont des variantes d'une meme scene, pas des pages independantes.
2. Les elements, bindings, tags et actions doivent rester lies a la scene logique.
3. Chaque variante peut avoir ses propres positions, dimensions, visibilites et groupements visuels.
4. Les variantes mobile/tablet doivent pouvoir etre affichees en portrait ou paysage.
5. La rotation 90 degres doit etre disponible pour mobile et tablet.
6. Au build, l'adaptation doit etre generee principalement en CSS.
7. Le JavaScript runtime ne doit pas devenir responsable de la logique responsive si le CSS peut le faire.

Objectif:

1. Garder un seul modele SCADA logique.
2. Eviter de dupliquer les tags et bindings.
3. Permettre un rendu moderne sur desktop, tablette et mobile.
4. Produire un export web simple et robuste.

## 3.5 Gestion des mesures et presets devices

Un menu de gestion des mesures doit permettre de definir les dimensions de reference.

Presets initiaux recommandes:

1. `Desktop 16:9`
   - 1920 x 1080
   - 1600 x 900
   - 1366 x 768
   - 1280 x 720
2. `Desktop 4:3`
   - 1280 x 960
   - 1024 x 768
3. `Tablet`
   - iPad portrait
   - iPad landscape
   - Android tablet portrait
   - Android tablet landscape
4. `Mobile`
   - iPhone portrait
   - iPhone landscape
   - Android phone portrait
   - Android phone landscape
5. `Custom`
   - largeur
   - hauteur
   - orientation
   - pixel ratio optionnel
   - safe area optionnelle

Decision:

1. Les presets doivent etre raisonnables des la creation d'un projet.
2. L'utilisateur peut ajouter des presets custom.
3. Les mesures doivent etre stockees dans la configuration projet.
4. Le workspace doit permettre de previsualiser une scene dans un preset choisi.
5. Le build doit produire les media queries CSS necessaires.

## 3.6 Configuration projet et creation initiale

SCADA Builder V2 doit creer un projet complet a partir d'un chemin choisi par l'utilisateur.

Decision:

1. Lors de la creation d'un projet, l'utilisateur choisit le path racine.
2. L'application cree automatiquement les sous-dossiers necessaires.
3. L'application cree un fichier de configuration initiale en XAML.
4. Le XAML devient le fichier de configuration projet principal pour V2.
5. Les fichiers de scenes/layouts peuvent rester separes pour eviter un fichier monolithique trop lourd.

Structure initiale proposee:

```text
MyScadaProject/
  project.xaml
  scenes/
    desktop/
    tablet/
    mobile/
  assets/
    images/
    icons/
    symbols/
  libraries/
  exports/
  backups/
  logs/
```

Role de `project.xaml`:

1. Identite projet.
2. Resolution globale.
3. Strategie responsive.
4. Presets de mesures.
5. Liste des scenes.
6. Chemins des librairies/assets.
7. Options de build.

Principe:

1. XAML est le contrat projet C#/.NET.
2. Le modele doit rester lisible et versionnable.
3. Les donnees volumineuses ou generees doivent rester dans des fichiers separes.

## 3.7 Modes responsive

Decision:

1. `Fixed`
   - Une seule scene.
   - CSS global simple.
   - Resolution de reference stricte.
   - Cible: panel industriel fixe.
2. `Scale-to-fit`
   - Une seule scene.
   - CSS global avec comportement flexible.
   - Le canvas conserve ses proportions et s'adapte a l'ecran.
   - Cible: desktop/tablet sans refaire le layout.
3. `Adaptive layout`
   - Une scene logique.
   - Trois variantes editables: desktop, tablet, mobile.
   - CSS responsive genere selon les presets.
   - Chaque variante peut etre ajustee manuellement.

Regle generale:

Meme en `Fixed` ou `Scale-to-fit`, les composants doivent etre optimises pour rester flexibles autant que possible:

1. Variables CSS.
2. Unites relatives quand pertinentes.
3. Contraintes min/max.
4. Safe areas.
5. Composants capables de se redimensionner sans briser le rendu.

## 3.8 Scripting et actions

SCADA Builder V2 devra permettre le scripting d'actions.

Objectif:

1. Permettre a un utilisateur avance de definir des comportements.
2. Offrir une syntaxe simple, de type basic ou pseudo-code structure.
3. Convertir ce scripting vers JavaScript au build.
4. Garder les scripts lisibles et maintenables dans le projet.
5. Eviter d'ecrire directement du JavaScript dans l'UI lorsque ce n'est pas necessaire.

Exemples d'usages:

1. Changer une classe CSS selon une valeur.
2. Afficher/masquer un element.
3. Changer une couleur selon un seuil.
4. Naviguer vers une scene.
5. Declencher une animation.
6. Appliquer une logique simple sur une valeur FT100.

Principe:

1. Le scripting est stocke dans le modele projet.
2. Le moteur C# valide le script.
3. Le build transpile le script vers JavaScript.
4. Le runtime web execute le JavaScript genere.
5. Les scripts ne doivent pas court-circuiter le modele de bindings et d'actions.

## 3.9 Import mappings FT100

SCADA Builder V2 devra importer les mappings existants du FT100.

Objectif:

1. Lire la liste des tags/mappings existants.
2. Creer ou proposer les relations d'action.
3. Associer les elements SCADA aux sources FT100.
4. Reduire la saisie manuelle.
5. Eviter les erreurs de nommage.

Fonctions cibles:

1. Importer un fichier ou export FT100.
2. Parser les mappings.
3. Afficher les tags disponibles dans une palette ou un panneau.
4. Associer un tag a une propriete d'objet.
5. Generer les bindings correspondants.
6. Valider les tags manquants ou invalides.

## 3.10 Proprietes CSS HTML5 par objet

Le panneau de proprietes doit exposer les principales proprietes CSS utiles selon le type de balise HTML.

Objectif:

1. Permettre un rendu SCADA moderne.
2. Exposer les proprietes pertinentes sans noyer l'utilisateur.
3. Garder un controle strict sur ce qui est genere.
4. Eviter les divergences entre preview et build.

Exemples de familles CSS:

1. Position et layout:
   - position
   - left/top/right/bottom
   - width/height
   - min/max width/height
   - display
   - flex
   - grid optionnel
2. Box model:
   - margin
   - padding
   - border
   - border-radius
   - box-sizing
3. Background:
   - background-color
   - background-image
   - gradients controles
   - opacity
4. Typography:
   - font-family
   - font-size
   - font-weight
   - color
   - text-align
   - line-height
5. Effects:
   - box-shadow
   - filter limite
   - transform
   - transition
6. Visibility and interaction:
   - visibility
   - display
   - pointer-events
   - cursor
7. Layering:
   - z-index
   - overflow

Decision:

1. Les proprietes disponibles dependent du type d'element.
2. Un element texte n'a pas exactement les memes proprietes visibles qu'une image, un bouton ou un groupe.
3. Les proprietes avancees doivent etre disponibles dans une section avancee.
4. Les valeurs doivent etre validees.
5. Les styles doivent etre serialises dans le modele projet, pas seulement dans du HTML genere.

## 3.11 Normalisation CSS preview/build

Probleme a eviter:

Les comportements CSS natifs des elements HTML peuvent diverger entre preview, build, navigateur, WebView2 et target finale.

Decision:

1. SCADA Builder V2 doit appliquer une couche de reset/normalisation CSS controlee.
2. Les styles natifs des elements doivent etre neutralises lorsque necessaire.
3. Le preview et le build doivent utiliser le meme runtime CSS.
4. Les composants doivent avoir une base CSS deterministe.
5. Le HTML genere ne doit pas dependre des styles implicites du navigateur.

Regles cibles:

1. `box-sizing: border-box` global.
2. Reset controle des marges/paddings par defaut.
3. Styles de bouton/input normalises si ces elements sont utilises comme composants SCADA.
4. Polices et tailles de base explicites.
5. Gestion explicite de overflow.
6. Couleurs et backgrounds explicites.
7. Aucun style critique ne doit dependre du user-agent stylesheet.

Objectif:

Le rendu du preview doit correspondre au rendu compile.

## 4. Workspace central

Le workspace central contient les scenes/pages ouvertes.

Decision:

1. Chaque page ouverte devient un onglet dans le workspace.
2. Une page active controle le contenu du panneau droit.
3. La scene doit rester la zone visuelle prioritaire.
4. Le changement de scene ne doit pas forcement detruire la selection si l'utilisateur a verrouille la selection.
5. Les onglets de page doivent avoir un bouton `x` pour fermer.
6. Les onglets de page doivent pouvoir etre reorganises par drag.
7. Les pages modifiees et non sauvegardees doivent afficher un indicateur `*`.

## 5. Panneau droit

Le panneau droit est contextuel, resizable/collapsible/fermable.

Il affiche les proprietes selon:

1. La page active.
2. L'element selectionne.
3. La librairie ou le contexte actif.
4. Le mode outil actif.

Onglets ou sections cibles:

1. `Page`
2. `Element`
3. `Propriete`
4. `Librairie`

Decision:

1. Le panneau droit doit etre dynamique selon la page active dans la scene.
2. Les libelles utilisateur doivent etre utilises partout.
3. Les cles internes comme `legacy:795` ne doivent pas etre affichees comme nom d'element.

## 6. Selection verrouillee

Besoin:

L'utilisateur doit pouvoir verrouiller une selection pour la garder active pendant certaines operations, par exemple:

1. Changer de scene/page.
2. Copier/coller un element entre deux scenes.
3. Modifier des proprietes sans perdre la selection.

Decision:

1. Ajouter une icone cadenas dans le panneau droit ou la zone de selection.
2. Etat `lock selection` distinct du lock objet.
3. `Lock selection` signifie: conserver la selection active.
4. `Lock objet` signifie: empecher le deplacement/modification de l'objet.
5. Les deux etats doivent etre visuellement differencies.
6. Le cadenas de selection est global dans le panneau droit.

## 7. Panneaux

Tous les panneaux principaux doivent etre:

1. Redimensionnables.
2. Repliables.
3. Fermables.
4. Restaurables.

Decision:

1. `Edition -> Panneau -> <Nom du panneau>` permet de reafficher un panneau ferme.
2. Les panneaux cibles initiaux sont:
   - Panneau gauche `Outil/Projet`
   - Panneau droit `Contexte/Proprietes`
   - Barre bas `Etat/Warning`
   - Ruban/toolbar superieur si applicable

## 8. Barre inferieure

La barre inferieure affiche:

1. Etat courant.
2. Warnings.
3. Erreurs de validation.
4. Etat de sauvegarde.
5. Etat de build.

Elle doit pouvoir etre repliee ou fermee, puis restauree depuis `Edition -> Panneau`.

## 9. Questions ouvertes

1. Confirmer si le ruban AutoCAD remplace completement une toolbar classique.
2. Definir la difference visuelle entre `lock selection` et `lock objet`.
3. Definir les onglets exacts du panneau droit pour la V2 initiale.
