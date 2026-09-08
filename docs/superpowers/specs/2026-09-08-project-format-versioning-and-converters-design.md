# Spécification — Versionnement de format et convertisseurs

Date: 2026-09-08
Status: Design approuvé en portée, non implémenté
Document version: `V2.1.6.0011`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-09-08 | `V2.1.6.0011` | `PENDING` | Correction : le `.sep` porte déjà `SchemaVersion`. C1 le raccorde au registre au lieu de lui ajouter un champ parallèle; trois modules seulement reçoivent un `FormatVersion` neuf. |
| 2026-09-08 | `V2.1.6.0010` | `c7acf1e` | C5 ramenée à deux issues sur décision : convertir ou ne pas ouvrir. Le mode consultation en lecture seule sort du périmètre, les écarts entre générations étant trop nombreux pour qu'une session à moitié migrée soit fidèle. |
| 2026-09-08 | `V2.1.6.0009` | `06dd83e` | Création : versionnement de format par module, refus vers l'arrière, registre de convertisseurs chaînés et conversion consentie avec sauvegarde. Chantier C, prérequis des icônes interactives. |

## 1. Problème

Trois faits vérifiés dans le code le 2026-09-08 décrivent le même manque.

**Aucun fichier de projet ne porte sa version de format.** `ScadaProject` expose trois notions de version, et aucune n'est celle du format persisté :

| Champ | Ce que c'est | Ce que ce n'est pas |
| --- | --- | --- |
| `Version` | `ScadaVersion(Production, Feature, Iteration)`, la version produit qui a créé le projet | le format |
| `ManifestVersion` | le profil d'export `2.0`/`2.1`/`2.2`/`2.3` | le format |
| *(absent)* | | il n'existe aucune version de format |

**La migration est un reniflage de formes perpétuel.** Faute de version, `ModernProjectMigration.MigrateProject` déduit l'ancienneté par la forme des données — « si `origin` est nul et `provenance` nulle et `imported` non nul », « si l'élément est `InputNumeric` et que le binding diverge ». Elle s'exécute à chaque chargement de chaque projet, elle est idempotente par construction, et **rien n'enregistre qu'un projet a déjà été migré**. Ce code ne peut donc jamais être retiré : il ne peut que grossir. Il fait 200 lignes.

**Le refus vers l'arrière est spécifié mais absent.** `DEC-0049` D5 exige qu'« une version plus récente [soit] refusée avec diagnostics ». `ProjectWorkspaceRepository` ne refuse que `project.open-path-invalid` et `project.open-failed`. Aucun contrôle de version n'existe.

### 1.1 La perte de données que cela cause aujourd'hui

`ScadaProject.QuickWindows` et `QuickWindowInvocations` sont des propriétés nullables ajoutées en Phase 1 de `DEC-0050`. Un binaire antérieur qui ouvre un projet en contenant ignore ces propriétés inconnues à la désérialisation, puis les **réécrit absentes** à la première sauvegarde. Les définitions, leurs interfaces locales et toutes les invocations disparaissent sans message, sans refus et sans trace.

Ce binaire ne peut pas faire mieux : sans version de format, il n'a aucun moyen de savoir qu'il regarde quelque chose de plus récent que lui. Le défaut est actif dès maintenant, indépendamment de tout travail futur.

## 2. Audit architectural vérifié

### 2.1 Ce qui existe et sera réutilisé

- `ModernProjectMigration` applique déjà `MigrateProject` et `MigrateScene` à chaque chargement, depuis un point d'entrée unique : le pipeline de conversion s'y greffe sans nouveau chemin.
- `DEC-0049` D6 pose que la migration vit **en mémoire et n'est jamais persistée automatiquement**; seule une sauvegarde explicite l'écrit. C'est déjà la moitié de la garantie de sécurité.
- `ProjectWorkspaceRepository.OpenAsync` prépare et valide un candidat **avant** de remplacer la session active. Le refus de version s'y insère naturellement, avant toute activation.
- L'écriture est atomique : staging, flush, validation, renommage. Une conversion interrompue ne laisse pas de projet à moitié converti.
- `ScadaBuildValidationIssue` porte déjà `Severity`, `Code` et `Message`, et le shell sait présenter une liste de diagnostics.

### 2.2 Ce qui manque

- Un champ de version de format sur **trois** des quatre artefacts persistés : projet, scène, catalogue de tags. Le `.sep` en porte déjà un — `ElementStudioComponentMetadata.CurrentSchemaVersion`, avec son schéma nommé : il est **raccordé** au registre, jamais doublé par un champ parallèle qui créerait deux vérités sur le même fichier.
- Un registre de convertisseurs et la composition d'une chaîne.
- Une surface de consentement, et la sauvegarde qui l'accompagne.
- La séparation entre profil d'export et capacité déclarée du projet, aujourd'hui confondus dans `ManifestVersion`.

### 2.3 Contrainte de non-régression

Les preuves industrielles gelées et les fixtures de conformance dépendent d'octets stables. Toute écriture d'un champ nouveau dans un artefact existant doit rester **absente quand elle ne s'applique pas**, selon la règle déjà appliquée à `quickWindowInvocationKey` : la propriété n'est écrite que lorsqu'elle existe, de sorte qu'un paquet inchangé garde ses octets.

## 3. Objectifs

1. Rendre détectable la version de chaque artefact persisté.
2. Fermer la perte de données décrite au §1.1, sans attendre le reste.
3. Remplacer le reniflage de formes par des conversions explicites, ordonnées et **retirables**.
4. Convertir sous consentement, avec sauvegarde, et dire clairement que l'opération ne se défait pas.
5. Donner aux chantiers suivants — dessin, parties adressables — un mécanisme de contrat versionné déjà éprouvé.

Non-objectifs : convertir un projet vers une version **antérieure**; réparer un artefact corrompu; convertir automatiquement sans intervention humaine; ouvrir un projet non converti en lecture seule.

## 4. Décisions approuvées

### C1 — Une version de format par module, pas une pour tout

Chaque artefact persisté porte sa propre version de format. Un module évolue sans forcer les autres, et un convertisseur ne connaît que son module.

Trois modules reçoivent un entier `FormatVersion` nouveau : `project.json`, chaque scène, le catalogue de tags. Le quatrième, le composant `.sep`, en porte déjà un : `ElementStudioComponentMetadata.SchemaVersion`, accompagné de `Schema`. Ce champ existant devient la version de format du module `.sep` et le registre s'y adosse tel quel. Ajouter un `FormatVersion` à côté créerait deux numéros pour un seul fichier, et la question « lequel fait foi » n'a pas de bonne réponse.

L'absence du champ vaut **génération 0** : tout l'existant est en génération 0 sans être réécrit.

`FormatVersion` est un entier qui s'incrémente de un. Il n'est ni sémantique, ni corrélé à `ScadaVersion`, ni corrélé à `ManifestVersion`. Un numéro de format ne décrit qu'une forme de données.

### C2 — Le refus vers l'arrière précède tout le reste

Un artefact dont le `FormatVersion` dépasse celui que le binaire connaît est **refusé à l'ouverture**, avant toute activation, avec un diagnostic nommant les deux versions et la version de produit requise.

Cette règle est livrée **en premier**, indépendamment des convertisseurs : elle ferme à elle seule la perte de données du §1.1. Un binaire qui ne comprend pas ce qu'il lit ne doit jamais pouvoir le réécrire.

Le code de diagnostic est `project.format-too-new`, et ses analogues par module.

### C3 — Des convertisseurs chaînés, un pas à la fois

Un convertisseur déclare le module qu'il traite, une version d'entrée et une version de sortie. Le pipeline compose la chaîne : `0 → 1 → 2 → 3`. On écrit **une étape**, jamais une matrice.

Un convertisseur peut couvrir plusieurs pas d'un coup lorsque le changement est trivial — il déclare alors `De = 1, Vers = 3` — mais il ne peut jamais sauter par-dessus un pas qu'un autre convertisseur revendique.

Un convertisseur est une fonction pure sur l'arbre désérialisé : pas d'accès disque, pas d'accès réseau, pas d'horloge. C'est ce qui le rend testable par cas figé.

### C4 — La chaîne est validée avant d'être exécutée

Avant toute écriture, le pipeline vérifie que la chaîne est complète, sans trou ni chevauchement, du `FormatVersion` lu jusqu'à la version courante. Une chaîne incomplète est un refus, pas une conversion partielle.

### C5 — Conversion consentie, jamais implicite

L'ouverture d'un artefact de génération inférieure produit un **plan de conversion** présenté à l'opérateur : les modules touchés, la chaîne de versions, ce que chaque étape change en une phrase, et le chemin de la sauvegarde.

**Deux issues seulement : `Convertir` ou `Annuler`.** Annuler n'ouvre pas le projet.

Il n'y a pas de mode consultation en lecture seule. La tentation existe — regarder un vieux projet sans le convertir — mais les écarts entre générations sont trop nombreux pour qu'une session à moitié migrée soit fidèle à ce que l'opérateur croit voir. Un troisième état signifierait aussi que chaque chemin d'écriture du produit — sauvegarde de projet, sauvegarde de scène, export, publication de composant — doit connaître et respecter une interdiction, ce qui multiplie les endroits où l'oubli d'un seul recrée précisément la perte de données du §1.1.

Le mode lecture seule reste explicitement hors périmètre; il pourra être rouvert quand les générations auront cessé de diverger.

### C6 — La sauvegarde est automatique et précède l'écriture

Avant la première écriture convertie, chaque fichier touché est copié en `<nom>.<extension>.bak`. La copie précède l'écriture; son échec annule la conversion.

Cette règle reprend la pratique déjà en vigueur pour les `.sep` lors des modernisations d'icônes, et l'étend à tous les modules.

Une sauvegarde existante n'est jamais écrasée silencieusement : le suffixe est numéroté si nécessaire.

### C7 — L'irréversibilité est dite, pas devinée

Le dialogue de conversion énonce que l'opération ne se défait pas et que la sauvegarde est le seul retour possible. Aucun convertisseur inverse n'est écrit, aujourd'hui ni plus tard : un convertisseur descendant devrait inventer les données qu'il ne possède pas.

### C8 — `ManifestVersion` est scindé

`ScadaProject.ManifestVersion` sert aujourd'hui deux rôles : le profil d'export négocié avec TF100Web, et la déclaration de ce que le projet a le droit de contenir — les Fenêtres rapides exigent `2.3` sur le projet lui-même.

Ces deux rôles se séparent **sans renommer le champ** : `ManifestVersion` reste le profil d'export visé, ce qu'il a toujours décrit et ce que le manifeste publie. Ce qui change est le second rôle, qui lui est retiré.

La validation de contenu cesse de s'adosser à `ManifestVersion` et s'adosse aux **capacités** du catalogue `DEC-0047`. Celui-ci est déjà l'autorité sur ce qu'un projet a le droit de contenir : il porte le statut, les preuves aux trois couches et le gate fail-closed. Un projet contenant des Fenêtres rapides est donc autorisé parce que `quick-window.definition` est `Supported`, pas parce qu'une chaîne vaut `"2.3"`.

Aucun nouveau champ n'est introduit : la séparation retire une responsabilité, elle n'en ajoute pas. La conversion `0 → 1` du module projet ne touche pas la valeur de `ManifestVersion`; elle marque seulement que le projet a été relu sous la nouvelle règle de validation.

### C9 — Le reniflage remplacé devient supprimable

Chaque règle de `ModernProjectMigration` reprise par un convertisseur est **retirée** dans le même commit. C'est le gain qui justifie le chantier, et il ne se réalise que si le retrait est fait, pas reporté.

Une règle non reprise reste en place et est nommée dans le rapport de phase, avec la raison.

### C10 — Aucun artefact gelé ne bouge sans décision

`FormatVersion` n'est écrit dans un artefact que lorsqu'il est converti ou créé. Les fixtures gelées, les paquets de conformance et les preuves industrielles ne sont pas régénérés par ce chantier. Toute dérive d'octets est un échec de test, pas un effet attendu.

## 5. Architecture cible

### 5.1 Domain

`FormatVersion` comme propriété optionnelle des enregistrements persistés, absente par défaut, jamais sérialisée quand elle vaut zéro.

`ScadaFormatGeneration` : les constantes de version courante par module, en un seul endroit.

### 5.2 Application

`IArtifactConverter` : `Module`, `FromVersion`, `ToVersion`, et une conversion pure sur le document désérialisé.

`ArtifactConverterRegistry` : enregistrement, résolution de chaîne, détection de trou et de chevauchement.

`ConversionPlan` : les modules touchés, les chaînes, les libellés d'étape, les chemins de sauvegarde.

`ConversionCoordinator` : refus vers l'arrière, calcul du plan, demande de consentement, exécution, écriture. Il ne connaît ni le disque ni l'interface.

### 5.3 Infrastructure

Lecture du `FormatVersion` avant désérialisation complète, pour pouvoir refuser sans instancier un modèle qu'on ne comprend pas.

Écriture des sauvegardes, et application des convertisseurs enregistrés.

### 5.4 App/WPF

`ConversionPlanDialog` : le plan, les trois issues, l'irréversibilité, le chemin de sauvegarde.

Désactivation de la sauvegarde en mode consultation, avec la raison portée par le mécanisme `DisabledReason` existant.

## 6. Contrats de validation

### 6.1 Refus vers l'arrière

Un `FormatVersion` supérieur au courant est refusé avant activation, avec les deux versions nommées. Aucune écriture n'est possible dans cette session pour cet artefact.

### 6.2 Chaîne

Une chaîne complète convertit. Une chaîne trouée refuse. Deux convertisseurs qui revendiquent le même pas sont une erreur de démarrage, pas d'exécution.

### 6.3 Idempotence et détermination

Convertir deux fois un même artefact produit des octets identiques. Convertir sur deux machines produit des octets identiques.

### 6.4 Sauvegarde

Aucune écriture convertie n'a lieu sans sauvegarde préalable réussie. Une sauvegarde existante n'est pas écrasée.

### 6.5 Refus de conversion

`Annuler` n'ouvre pas le projet : la session précédente reste exactement dans son état, et aucun artefact n'est touché. Il n'existe aucun chemin par lequel un projet de génération inférieure devienne actif sans avoir été converti.

## 7. Tests et validation

- Refus d'un `FormatVersion` supérieur, par module, avant activation.
- **Régression de la perte de données du §1.1** : un projet portant des Fenêtres rapides, ouvert par un binaire déclarant une génération inférieure, est refusé au lieu d'être réécrit sans elles.
- Chaîne complète, chaîne trouée, chevauchement.
- Idempotence et détermination octet à octet.
- Sauvegarde créée avant écriture; échec de sauvegarde annulant la conversion; sauvegarde existante non écrasée.
- Refuser la conversion n'ouvre pas le projet et ne modifie aucun fichier.
- Aucun chemin d'activation n'accepte un artefact de génération inférieure non converti.
- Chaque convertisseur : cas figé d'entrée, sortie attendue.
- Les fixtures gelées et les paquets de conformance restent identiques.

## 8. Hors périmètre, et ce qui suit

Ce chantier ne dessine rien et n'anime rien. Il est le prérequis de deux chantiers déjà cadrés :

**A — Outils de dessin dans Studio Element+.** Canevas vectoriel bâti sur Paper.js dans le WebView2 existant, primitives, plume de Bézier et édition de nœuds, import SVG, polices embarquées. Chaque icône produit un dessin, des parties nommées, une interface locale typée et des liaisons internes `partie ← membre`. Livre le convertisseur `.sep 1 → 2`.

**B — Parties adressables et instances.** L'icône devient une définition à interface locale, la page en pose des invocations, chacune avec ses propres liaisons — le triplet de `DEC-0050`, sans host, sans cycle de vie, et avec N instances simultanées là où une Fenêtre rapide en interdit plus d'une. Les états passent par les membres d'interface; les actions passent par des ancrages nommés auxquels le Builder attache ses commandes existantes, parce que cinq des six `ScadaCommandKind` ne visent pas un tag. Livre le convertisseur de projet et le profil manifest `2.4`.

Un défaut à corriger dans A, trouvé pendant cet audit : `ElementStudioSvgMarkupNormalizer.CalculateGeometryBounds` reconnaît `circle`, `ellipse`, `image`, `line`, `polygon`, `polyline`, `rect` et `text`, **mais pas `path`**. Tout SVG produit par Illustrator, Figma ou un générateur d'images est fait de `path` : pour ceux-là les bornes reviennent nulles et le markup est renvoyé sans normalisation de `viewBox`. L'import SVG est donc silencieusement dégradé pour l'entrée la plus courante.
