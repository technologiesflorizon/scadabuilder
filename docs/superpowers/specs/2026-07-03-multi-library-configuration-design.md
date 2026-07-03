# Design: configuration de librairies Element+ multiples

Date: 2026-07-03
Statut: Approuve (brainstorming)

## Probleme

SCADA Builder V2 et Studio Element+ ne connaissent qu'une seule librairie
Element+, dont le chemin est calcule en dur a partir du projet de reference
(`AMR_REF_SCADA_V2/library/elements`). Il n'existe aucun moyen de :

- declarer des librairies externes supplementaires (ex. une librairie
  partagee sur un autre disque ou un depot separe) ;
- choisir, dans SCADA Builder V2, quelle librairie parcourir pour inserer
  des Element+ existants ;
- choisir, dans Studio Element+, dans quelle librairie sauvegarder un
  composant nouvellement cree, plutot que systematiquement la librairie par
  defaut.

Le bouton "Configurer" (`tool.settings`) du ruban Tools existe deja mais est
desactive ("Configurer les outils a venir") et n'ouvre rien.

## Solution retenue

### Registre de librairies partage

- **`ScadaBuilderV2.Application.Libraries`** (nouveau namespace) :
  `LibraryEntry(string Name, string Path, bool IsDefault)` et
  `LibraryRegistry`, qui assemble en memoire une liste ordonnee :
  1. l'entree par defaut, toujours en premiere position, **synthetisee a
     chaque lecture** via la logique existante
     (`ResolveElementPlusLibraryRoot`/`ModernProjectStore.GetReferenceModernProjectRoot`)
     — son chemin n'est jamais ecrit dans un fichier de configuration, pour
     eviter qu'il devienne perime si le depot est deplace ou re-clone ;
  2. les entrees externes chargees depuis le fichier de settings.
  `LibraryRegistry` expose `Add`, `Rename`, `UpdatePath`, `Remove`.
  `Rename` s'applique aussi bien a l'entree `IsDefault` qu'aux entrees
  externes (seul le nom de la librairie par defaut est modifiable, conforme
  a la decision prise en brainstorming). `UpdatePath` et `Remove` refusent
  explicitement de s'appliquer a l'entree `IsDefault` (chemin et presence
  verrouilles). `Add` refuse un chemin deja present dans la liste
  (comparaison de chemins normalisee, insensible a la casse sous Windows).

- **`ScadaBuilderV2.Infrastructure.Libraries.LibraryRegistryStore`** :
  persiste uniquement les entrees externes dans
  `%AppData%/ScadaBuilderV2/libraries.json` (tableau JSON
  `[{ "Name": "...", "Path": "..." }]`). Fichier absent au premier lancement
  -> liste externe vide, aucune erreur. `ReadAsync()` / `WriteAsync(entries)`.

Les deux applications (`ScadaBuilderV2.App` et
`ScadaBuilderV2.ElementStudio.App`) sont des process WPF separes ; chacune
relit `libraries.json` au moment ou elle en a besoin (ouverture de la
fenetre Configuration, ouverture du menu du split-button). Il n'y a pas de
synchronisation live entre process — coherent avec l'absence actuelle de
tout mecanisme inter-process dans le code base.

### Fenetre Configuration (SCADA Builder V2)

- Nouvelle `ConfigurationWindow` (+ `.xaml`) dans `ScadaBuilderV2.App`,
  suivant le meme pattern que `ElementPropertiesDialog` (fenetre WPF modale,
  `Owner = MainWindow`).
- `TabControl` avec un seul `TabItem Header="Librairie"` pour l'instant
  (pas d'onglets vides speculatifs ; on en ajoutera si un besoin concret
  apparait).
- Contenu de l'onglet Librairie : liste des librairies (defaut en premier,
  ligne verrouillee — nom editable inline, chemin et suppression
  desactives pour cette ligne uniquement) ; pour les entrees externes :
  boutons Ajouter (ouvre un dialogue de selection de dossier existant),
  Renommer, Modifier le chemin (meme dialogue de selection de dossier),
  Supprimer.
- A la fermeture via OK, persiste les entrees externes via
  `LibraryRegistryStore.WriteAsync` et notifie `MainWindow` pour rafraichir
  le `ComboBox` de selection de librairie.
- `RibbonCommandCatalog.cs` : `tool.settings` passe de `Disabled` a
  `Enabled("tool.settings", "Configurer", "Icon.Tool.Settings", "Ouvrir la
  configuration (librairies)")`. `ExecuteRibbonCommand` recoit un nouveau
  `case "tool.settings"` qui ouvre `ConfigurationWindow`.

### Selection de librairie active (SCADA Builder V2)

- Dans l'onglet "Librairie" du panneau lateral de `MainWindow`, le
  `TextBlock Text="Element+ disponibles"` (actuellement en dur) est
  remplace par un `ComboBox x:Name="LibrarySelectorComboBox"` liste les noms
  des `LibraryEntry` (defaut + externes), defaut selectionne par defaut.
- `SelectionChanged` recalcule le chemin racine a partir de l'entree
  choisie (au lieu d'appeler systematiquement
  `ResolveElementPlusLibraryRoot`), puis relance
  `RefreshElementLibraryAsync()` et redemarre le `FileSystemWatcher`
  (`StartElementLibraryWatcher`) sur ce nouveau chemin.
- La selection n'est **pas persistee** : au prochain lancement de
  l'application, le `ComboBox` retombe sur la librairie par defaut. C'est un
  etat de session, pas une preference durable.

### Split-button "Ajouter a la librairie" (Studio Element+)

- Le bouton "Save as .sep" devient "Ajouter a la librairie", rendu comme un
  split-button : bouton principal + fleche separee a droite.
- Clic sur le bouton principal : comportement identique a aujourd'hui
  (`OnSaveComponentClick` -> sauvegarde vers `ResolveDefaultSepDirectory()`,
  qui pointe vers la librairie par defaut). Aucun changement de ce chemin de
  code.
- Clic sur la fleche : ouvre un menu (`ContextMenu`/`Popup`) peuple depuis
  `LibraryRegistryStore.ReadAsync()` + l'entree par defaut synthetisee.
  Selectionner une entree appelle directement
  `componentPackageStore.WriteToLibraryAsync(package, entry.Path)` (chemin
  cible = celui de la librairie choisie), sans passer par la boite de
  dialogue `SaveFileDialog` existante.

## Gestion d'erreurs

- Chemin externe devenu invalide/inaccessible au moment de charger la
  librairie dans SCADA Builder V2 : le `ComboBox` affiche quand meme
  l'entree ; la tentative de chargement echoue avec le meme message
  d'erreur deja gere par `RefreshElementLibraryAsync`
  (`"Chargement librairie impossible: {ex.Message}"`).
- Ajout d'un chemin deja enregistre dans la fenetre Configuration : refuse
  avec un message explicite, pas de doublon silencieux dans la liste.
- Suppression/renommage/modification de chemin tentee sur l'entree par
  defaut : les boutons correspondants sont desactives pour cette ligne (pas
  seulement un refus silencieux en code).

## Tests

- `LibraryRegistryStoreTests` : round-trip JSON, fichier absent -> liste
  vide, ne persiste jamais l'entree par defaut meme si elle est passee par
  erreur a `WriteAsync`.
- `LibraryRegistryTests` : `Add`/`Rename`/`UpdatePath`/`Remove` refusent
  l'entree `IsDefault` ; `Add` refuse les doublons de chemin.
- `RibbonCommandCatalogTests` : `tool.settings` est maintenant `Enabled`
  avec la nouvelle description ; pas de duplication d'id.
- Tests XAML/structure (style `RibbonCommandCatalogTests`/
  `WebViewContextMenuScriptTests`) verifiant la presence du `ComboBox`
  `LibrarySelectorComboBox` dans `MainWindow.xaml` et du split-button dans
  `ScadaBuilderV2.ElementStudio.App`'s `MainWindow.xaml`.

## Hors perimetre

- Pas de synchronisation live entre `ScadaBuilderV2.App` et
  `ScadaBuilderV2.ElementStudio.App` pendant qu'ils tournent simultanement.
- Pas de persistance de la derniere librairie active selectionnee dans
  SCADA Builder V2 entre deux lancements.
- Pas d'onglets supplementaires dans `ConfigurationWindow` au-dela de
  "Librairie".
- Pas de creation automatique de dossier si le chemin choisi via le
  dialogue de selection n'existe pas encore (le dialogue pointe vers un
  dossier existant).
