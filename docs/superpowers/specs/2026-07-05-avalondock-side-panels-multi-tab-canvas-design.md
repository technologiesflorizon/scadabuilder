# Design: panneaux lateraux AvalonDock + onglets multiples au canvas central

Date: 2026-07-05
Statut: Approuve (brainstorming)

## Contexte

L'utilisateur souhaite rapprocher SCADA_BUILDER_V2 du niveau fonctionnel de
FactoryTalk View Studio. Le gap identifie en priorite est le layout de
l'editeur : la coquille WPF actuelle (`MainWindow.xaml`) utilise une `Grid`
fixe avec des `GridSplitter` pour les colonnes laterales. Les colonnes
laterales sont deja des `TabControl` (gauche : `Outil`, `Projet`,
`Catalogue Tags` ; droite : `Page`, `Element`, `Propriete`, `Librairie`),
mais ces panneaux ne peuvent pas etre detaches, deplaces, ou reorganises,
et aucune disposition n'est persistee. Le canvas central est une seule
instance `PreviewWebView` (`WebView2`) liee a une seule page active
(`_activeScene` / `_activeReferencePage`) — une seule page peut etre editee
a la fois.

## Perimetre

**Dans le perimetre :**

1. Convertir les panneaux lateraux existants (gauche et droite) en panes
   ancrables via AvalonDock (`LayoutAnchorable`), avec support du
   drag-and-drop, du flottement, de la fermeture/reouverture, et de la
   persistance de la disposition entre sessions.
2. Ajouter une bande d'onglets au-dessus du canvas central permettant
   d'ouvrir plusieurs pages simultanement, chaque onglet possedant sa
   propre instance `WebView2` vivante (etat preserve, changement
   d'onglet instantane).

**Hors perimetre :**

1. Toute modification du comportement runtime TF100Web (piste separee,
   deja explicitement ecartee par l'utilisateur).
2. Le contenu des panneaux eux-memes (redesign de la palette d'outils, du
   catalogue de tags, de la librairie, etc.) — seul le conteneur/mecanisme
   d'ancrage change ; le contenu existant est deplace tel quel.
3. Toute reorganisation du canvas central lui-meme en zone dockable
   (pas de `LayoutDocumentPane` AvalonDock, pas de flottement de page) —
   uniquement une bande d'onglets classique au-dessus d'un canvas qui
   reste architecturalement une zone unique.
4. Polish multi-ecran au-dela de ce qu'AvalonDock fournit par defaut.

## Architecture

### Panneaux lateraux (AvalonDock)

- Ajouter le paquet NuGet `AvalonDock` a `ScadaBuilderV2.App`.
- Remplacer les deux colonnes `Grid` laterales fixes par un
  `DockingManager` AvalonDock contenant des `LayoutAnchorablePane` pour :
  `Outil`, `Projet`, `Catalogue Tags` (gauche) et `Page`, `Element`,
  `Propriete`, `Librairie` (droite). Chacun devient un `LayoutAnchorable`
  — deplacable, flottable, fermable/reouvrable via un menu `Fenetres`.
- Le contenu de chaque panneau (XAML/`UserControl` existant) est deplace
  sans modification dans son anchorable — aucune refonte du contenu.
- La disposition (positions, tailles, etat flottant, panneaux
  ouverts/fermes) est persistee via `XmlLayoutSerializer` d'AvalonDock,
  sauvegardee par utilisateur (ex. `%AppData%/ScadaBuilderV2/layout.xml`),
  avec une commande "Reinitialiser la disposition" pour restaurer le
  layout par defaut.

### Onglets du canvas central (WebView2 multiple)

- Introduire une session d'edition par page (`PageEditorSession`) :
  chaque page ouverte possede sa propre instance `WebView2` ainsi que
  l'etat actuellement porte par des champs comme `_activeScene` /
  `_activeReferencePage`, regroupes dans cet objet.
- Une bande d'onglets au-dessus du canvas liste les `PageEditorSession`
  ouvertes ; l'onglet actif determine quel `WebView2` est visible, les
  autres restent montes mais caches (vivants en memoire, conformement au
  choix "un WebView2 par onglet").
- Les panneaux Propriete/Element/Evenement a droite refletent toujours la
  selection de l'onglet **actif** — changer d'onglet change quelle
  session alimente ces panneaux.
- Ouvrir une page deja ouverte (depuis le panneau `Projet`) donne le focus
  a son onglet existant plutot que d'en creer un doublon. Fermer un onglet
  detruit son instance `WebView2` pour liberer la memoire.
- Aucun changement au fonctionnement interne de l'edition d'une page
  unique — cette conception ne fait qu'encapsuler la logique canvas
  existante par onglet.

### Risque explicite

`MainWindow.xaml.cs` fait deja environ 9000 lignes et suppose un canvas
unique. Cette conception exige d'extraire l'etat/comportement par page
hors de `MainWindow` vers une structure possedant des
`PageEditorSession` — c'est le plus grand risque d'implementation de ce
projet et necessitera son propre plan detaille, probablement incremental
(une session a la fois), en gardant le comportement mono-onglet
fonctionnel tout au long de la transition.

## Gestion des erreurs et cas limites

- **Limites de ressources WebView2** : trop d'onglets ouverts peut
  degrader memoire/CPU. Ajouter un seuil d'avertissement souple (au-dela
  de 8-10 onglets simultanes) plutot qu'une limite dure.
- **Corruption de la disposition** : si `layout.xml` echoue a se
  deserialiser (ex. apres une mise a jour qui renomme des panneaux),
  revenir a la disposition par defaut plutot que de planter, et logger un
  diagnostic.
- **Panneau ferme par accident** : tous les anchorables doivent rester
  accessibles via un menu `Fenetres` (liste a cocher) meme si l'utilisateur
  ferme tous les panneaux.
- **Onglet ferme avec modifications non enregistrees** : fermer un onglet
  de page avec des modifications en attente doit demander
  enregistrer/ignorer/annuler, coherent avec le comportement de
  confirmation de sauvegarde existant ailleurs dans l'application.
- **Liaison session-panneau au changement d'onglet** : changer l'onglet
  actif doit rafraichir completement les panneaux Propriete/Element a
  droite vers l'etat de selection du nouvel onglet — aucune liaison
  perimee de l'onglet precedent ne doit subsister.

## Tests

- La suite MSTest existante (253 tests) doit continuer a passer ; rien
  dans ce travail ne touche `Domain`/`Application`/`Rendering` — c'est
  uniquement la couche `App` (coquille WPF).
- Nouvelle couverture : aller-retour de persistance de la disposition
  (sauvegarde/rechargement de `layout.xml`), cycle de vie
  ouverture/focus/fermeture/destruction de `PageEditorSession`, et
  rafraichissement des panneaux de proprietes au changement d'onglet —
  probablement via des tests legers de logique WPF plutot que de
  l'automatisation UI complete, coherent avec la structure actuelle de
  `WebViewContextMenuScriptTests` etc.
- Passe de verification manuelle requise (barre de qualite production de
  `docs/AGENTS.md`) : glisser/flotter/re-ancrer chaque panneau, redemarrer
  l'application et confirmer que la disposition persiste, ouvrir/fermer
  plusieurs onglets de page, verifier l'absence de fuite d'etat entre
  onglets.
