# Design: onglet Librairie (parcourir + gerer) dans Studio Element+

Date: 2026-07-03
Statut: Approuve (brainstorming)

## Contexte

Studio Element+ possede un bouton ruban "Librairie" place sous l'onglet
"Exporter" depuis la version initiale du projet, mais il est branche sur le
handler generique `OnUnavailableCommandClick` ("implementation metier a
venir") et n'a jamais fait quoi que ce soit. Ce n'est pas une regression du
travail recent sur la configuration multi-librairies : `git blame` confirme
que ce bouton existe depuis le commit de baseline initial.

Independamment de ce bouton dormant, l'utilisateur souhaite un vrai moyen de
parcourir et gerer les composants d'une librairie Element+ directement
depuis Studio Element+, sous forme d'un 5eme onglet dans le `TabControl`
lateral existant (aux cotes de "Element", "Structure", "Proprietes",
"Composant" — `MainWindow.xaml:265-401`).

## Decoupage en deux specs

L'idee complete inclut la capacite d'ouvrir un composant `.sep` existant
dans l'editeur pour le modifier puis le re-sauvegarder (Enregistrer /
Enregistrer sous pour creer un variant). Une investigation technique a
confirme qu'**aucun chemin de rechargement n'existe aujourd'hui** :
`ElementStudioPackageLoader` ne sait charger que des paquets d'import
(elements legacy selectionnes dans SCADA Builder V2), et
`ElementStudioWorkspaceViewModel` n'a qu'un seul constructeur qui attend ce
format. Reconstruire un etat editable a partir d'un `.sep` deja sauvegarde
(dont les `Parts` conservent geometrie/style/traces source, mais pas
visibilite/verrouillage/groupes/ordre-z) est un sous-systeme reel, pas une
correction mineure.

En consequence, ce document couvre uniquement la **premiere moitie** :
parcourir la librairie active et gerer les fichiers `.sep` (renommer,
copier, supprimer) sans capacite d'edition. La capacite "Editer" fera
l'objet d'une deuxieme spec separee, une fois les decisions difficiles
(etat non persistant a la reouverture) traitees explicitement.

## Solution retenue

### Nouvel onglet "Librairie"

- Nouveau `TabItem Header="Librairie"` ajoute apres l'onglet "Composant"
  dans le `TabControl` lateral de `MainWindow.xaml`
  (`ScadaBuilderV2.ElementStudio.App`), meme style visuel que les onglets
  existants.
- Contenu :
  - Un `ComboBox` de selection de librairie, alimente par la meme source
    que le split-button "Ajouter a la librairie" deja existant : l'entree
    par defaut synthetisee via `ResolveDefaultSepDirectory()` + les entrees
    externes lues via `LibraryRegistryStore.ReadExternalEntriesAsync()`.
    Aucune nouvelle source de donnees ; reutilisation directe du registre
    de librairies existant.
  - Une `ListBox` des composants de la librairie selectionnee, chargee via
    `ElementPlusLibraryReader.ReadAsync(libraryPath)` — l'infrastructure
    deja utilisee par le panneau "Librairie" de SCADA Builder V2 (meme
    modele `ElementPlusLibraryItem` : nom, categorie, type visuel, nombre
    de parties, chemin, apercu, tags).
- Changer la selection du `ComboBox` recharge la `ListBox` (meme pattern
  que `RefreshElementLibraryAsync` dans l'app principale).

### Menu contextuel (clic droit sur un composant de la liste)

Quatre entrees :

1. **Editer** — visible mais **desactivee**, avec un tooltip explicite
   ("Edition disponible dans une prochaine version"). Reservee a la 2e
   spec.
2. **Renommer** — ouvre une petite boite de dialogue de saisie de texte
   (nom pre-rempli avec le nom actuel du composant). A la confirmation :
   - Relit le paquet `.sep` via `ElementStudioComponentPackageStore.ReadFromPathAsync`.
   - Met a jour `Component.Name` (via `with` sur le record immuable).
   - Calcule le nouveau chemin de fichier via
     `ElementStudioComponentPackageStore.GetDefaultComponentPath` (meme
     convention de nom de fichier sur que la sauvegarde normale).
   - Ecrit le paquet renomme au nouveau chemin, puis supprime l'ancien
     fichier si le chemin a change.
   - Rejette un nom vide (meme validation que le reste du texte-input UX
     de ce projet).
3. **Copier** — a la confirmation :
   - Relit le paquet source.
   - Calcule un nouveau nom `"{nom}_copie{N}"`, ou `N` est le premier
     entier a partir de 1 qui ne collisionne avec aucun nom de composant
     deja present dans la librairie active.
   - Genere un nouvel identifiant de composant (evite toute collision
     d'`ComponentId` entre l'original et la copie).
   - Ecrit la copie via `WriteToLibraryAsync` dans la librairie active.
4. **Supprimer** — boite de confirmation Oui/Non (`MessageBox`), puis
   suppression du fichier `.sep` sur disque a la confirmation.

Chaque operation (Renommer/Copier/Supprimer) rafraichit la `ListBox` a la
fin pour refleter l'etat courant du dossier.

## Gestion d'erreurs

- Toute operation fichier (lecture, ecriture, suppression) est entouree
  d'un `try/catch` ; les echecs (fichier verrouille, permissions,
  paquet corrompu) affichent un message d'erreur via `MessageBox.Show`
  sans crasher l'application, coherent avec les patterns deja en place
  dans `ScadaBuilderV2.ElementStudio.App` (ex. `SaveComponentAsync`,
  `OnLibraryMenuItemClick`).
- Un nom vide en Renommer est rejete avant toute ecriture.
- La detection de collision de nom en Copier est purement locale (scan des
  noms de composants deja charges dans la `ListBox`), pas une nouvelle
  dependance.

## Tests

- Verifications textuelles XAML/code (meme contrainte que pour le reste de
  cette fonctionnalite : le projet de tests ne peut pas referencer les
  projets WPF `net8.0-windows`).
- Si le calcul du suffixe `_copieN` est extrait dans une fonction pure
  testable (ex. `static string GenerateCopyName(string baseName,
  IEnumerable<string> existingNames)`), elle recoit une couverture unitaire
  reelle (pas seulement textuelle) : collision, absence de collision,
  plusieurs copies successives.

## Hors perimetre (reporte a la 2e spec)

- Ouvrir un composant `.sep` existant dans l'editeur pour le modifier.
- Enregistrer / Enregistrer sous pour creer un variant edite.
- Toute decision sur l'etat non persistant (visibilite, verrouillage,
  groupes, ordre-z) lors d'une reouverture future.
