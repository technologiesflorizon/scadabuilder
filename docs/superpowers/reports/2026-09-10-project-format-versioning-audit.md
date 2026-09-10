# Audit du chantier — versionnement de format de projet et convertisseurs

Date: 2026-09-10
Status: **Chantier C clos.** Les 8 tâches sont satisfaites. Le plan `2026-09-08-project-format-versioning-and-converters.md` livre le stamp de génération, le refus vers l'arrière, le pipeline de conversion avec consentement et sauvegarde, et le retrait du rôle d'autorisation de contenu de `ManifestVersion`. **Il ne livre pas la suppression du reniflage de forme qui le motivait** : cette promesse est re-cadrée et consignée en écart 33.
Document version: `V2.1.6.0020`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-09-10 | `V2.1.6.0020` | `d3be7da` | Clôture du chantier de versionnement de format : rapport d'audit, sort de chaque règle de `ModernProjectMigration`, état des générations par module. |

## 1. Portée et conclusion

Le chantier existe parce qu'aucun fichier de projet ne portait sa version de format. Trois champs de version existaient — `ScadaVersion`, `ManifestVersion`, `SchemaVersion` du `.sep` — et **aucun n'était le format du projet**. La conséquence était mesurable et vivante : `ScadaProject.QuickWindows` et `QuickWindowInvocations`, propriétés nullables ajoutées en Phase 1 de `DEC-0050`, disparaissaient silencieusement à la première sauvegarde d'un binaire antérieur, avec toutes les définitions, interfaces locales et invocations. `DEC-0049` D5 spécifiait le refus; le dépôt ne refusait que `project.open-path-invalid` et `project.open-failed`.

**Ce défaut est fermé.** Un projet déclarant une génération supérieure à celle du binaire est refusé au lieu d'être réécrit.

**25 commits**, `V2.1.6.0011` → `V2.1.6.0020`, suite de **912 à 968 tests sans skip**, `verify-docs` à `Errors: 0` et 121 avertissements du début à la fin, **zéro dérive de fixture gelée** (`git diff 3301329..HEAD -- '*.sep' '*.sb2' '*.sha256'` est vide).

## 2. Générations par module

| Module | Génération | Porteur | Convertisseur |
| --- | --- | --- | --- |
| `project.json` | 1 | `FormatVersion` (nouveau) | `ProjectGeneration1Converter` (0 → 1) |
| Scène | 0 | `FormatVersion` (nouveau) | aucun |
| Catalogue de tags | 0 | `FormatVersion` (nouveau) | aucun |
| Composant `.sep` | 1 | `SchemaVersion` (existant, raccordé) | aucun |

Le `.sep` n'a pas reçu de champ : il en portait déjà un, jamais consulté comme génération. Le raccorder valait mieux que d'en ajouter un second.

## 3. Sort de chaque règle de `ModernProjectMigration`

C'était le point de la Tâche 7 : chaque règle reprise devait être **retirée** dans le même commit. **Une seule l'a été partiellement, et cinq sont conservées.** Le tableau dit pourquoi, règle par règle.

| Règle | Sort | Raison |
| --- | --- | --- |
| Back-fill de `PageKey` sur une page sans clé | **Reprise dans le fichier**, conservée en mémoire | Le convertisseur la fixe une fois sur le disque; `MigrateProject` la garde pour les projets construits en mémoire qui ne viennent d'aucun fichier |
| Canonicalisation `Id`/`PageCode` | Conservée | Invariant maintenu à chaque sauvegarde, pas une question d'âge de fichier |
| Dérivation de `Origin` | Conservée | Idem |
| Synthèse et fusion d'`ImportProvenance` | Conservée | Dépend d'un **inventaire d'import en mémoire** qu'un convertisseur de fichier ne possède pas |
| Résolution `HeaderPageKey`/`FooterPageKey` | Conservée | Idem, recalculée après chaque édition de composition |
| Résolution `HomePageKey`/`HomePageId` | Conservée | Idem |

**La prémisse du plan était fausse.** `MigrateProject` n'est pas une migration au chargement : c'est le normaliseur d'identité du store, appelé depuis **11 sites**, dont deux écritures (`SaveProjectToRootAsync`) et une construction en mémoire sans fichier (`EnsureReferenceProjectAsync`, qui migre un `ScadaProject` bâti depuis un inventaire d'import Wonderware). Un convertisseur s'exécute une fois, sur un fichier, à l'ouverture; il ne peut pas tenir un invariant qui doit valoir après chaque édition en mémoire et avant chaque écriture.

La suppression forcée a été tentée et a rendu **7 tests rouges** — `ModernProjectStoreTests` ×3, `PageIdentityTests` ×3, `PageDependencyAnalyzerTests` ×1. La couverture existante a arrêté la régression. Le fichier a été restauré et la promesse re-cadrée plutôt que forcée.

Retirer `ModernProjectMigration` reste possible, mais exige d'abord de déplacer l'établissement d'identité de page à l'import vers l'importeur lui-même. C'est un chantier distinct, non commencé, consigné en écart 33.

## 4. Ce que le chantier corrige au-delà de son objet

**Un blocage latent total sur les Fenêtres rapides.** `ValidateQuickWindows` comparait `project.ManifestVersion` à `"2.3"` pour autoriser le contenu. Or ce champ **persisté** vaut `"2.0"` par défaut, le projet de référence porte `"2.0"`, et **aucune ligne de `src/` ne lui assigne jamais `"2.3"` ** — cette chaîne n'est écrite que dans le manifeste **exporté**, un artefact différent. Le contrôle aurait donc levé `quick-window.profile-unsupported` en **Error** sur tout projet réel dès qu'il aurait porté une Fenêtre rapide. Il n'a jamais sauté seulement parce qu'aucun projet réel n'en porte encore.

La Tâche 8 retire ce contrôle. Ce n'est pas une réduction de portée d'une garde en état de marche : c'est la correction d'une mine sous la fonctionnalité que `DEC-0050` a livrée. L'autorisation de contenu revient au catalogue de capacités, qui porte le statut, la preuve à trois couches et le gate fail-closed qu'une comparaison de chaîne ne pouvait pas porter.

## 5. Défauts trouvés et corrigés en cours de chantier

- **`ResolveChain` sur-convertissait.** Un convertisseur 0 → 3 interrogé pour 0 → 2 rendait une chaîne marquée complète qui dépassait la cible. Non atteignable par l'appelant actuel, mais une conversion ne se défait pas : sur-convertir est irréversible. Le dépassement est désormais traité comme une chaîne incomplète — refuser vaut mieux que convertir trop loin.
- **Le convertisseur inventait une seconde dérivation d'identité.** Un SHA-256 brut du seul code de page, sans le nom du projet, sans repli de casse et sans les bits de version et de variante — là où `PageKeyFactory.CreateDeterministic` produit un GUID v5 sur `nom|code`. Deux pages sans code auraient reçu la **même** clé. Aligné, puis épinglé par un littéral (`e6ce2175-1047-5dbc-ac17-fdaa0840b615` pour `("P","win00001")`) recalculé indépendamment.
- **Une `ArgumentException` s'échappait jusqu'à l'UI.** Le `catch` d'`OpenAsync` est filtré sur cinq types; `CreateDeterministic` lève `ArgumentException`, absente du filtre. Un nom de projet ou un code de page vide traversait `Apply`, `OpenAsync` et le coordinateur — qui n'a qu'un `finally` — sans produire aucun diagnostic. Le convertisseur valide désormais lui-même et lève `InvalidDataException`, type déjà couvert.
- **La sauvegarde laissait des débris.** Écrite avant la conversion, elle survivait à une conversion qui échoue, et chaque nouvelle tentative en ajoutait une puisque le suffixe est numéroté sans réemploi. Déplacée juste avant l'écriture : convertir en mémoire ne modifie rien sur disque, donc C6 reste satisfait à la lettre.

## 6. Écarts ouverts

- **Écart 33** — la suppression du reniflage de forme n'est pas livrée. Voir §3.
- **Écart 34** — le contenu Fenêtre rapide n'est gardé sous aucun profil `Compatibility21`/`Compatibility22`. Le correctif est nommé : un contrôle structurel dans `Ft100PackageValidation.ValidateQuickWindows`, sur le modèle exact de celui de `RuntimeContract`. Non urgent : aucun chemin produit ne passe ces profils.
- **Préexistant, non imputable au chantier** — `blockedQuickWindowCapabilities` dans `ProjectModels.cs` omet `QuickWindowLegacyFragmentAdapter`, que la liste agrégée de l'analyseur porte. La capacité est refusée à l'export mais pas à la validation de build : plus tard et plus cher, jamais silencieusement.

## 7. Ce qui reste à valider humainement

Aucun test humain n'a été exécuté sur ce chantier. Le parcours qui compte est celui-ci : **ouvrir un projet antérieur, lire le plan de conversion, refuser, vérifier que rien n'a bougé; puis rouvrir, accepter, vérifier la sauvegarde et le projet converti.** Les tests couvrent chaque moitié; seul un opérateur peut dire si le dialogue explique ce qu'il fait à quelqu'un qui ne l'a pas écrit.
