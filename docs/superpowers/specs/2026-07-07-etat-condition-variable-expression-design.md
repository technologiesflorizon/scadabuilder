# État — Condition variable/expression (Tag Picker)

**Date:** 2026-07-07
**Status:** Draft design
**Scope:** `ElementStateRuleDialog` — choix Variable/Expression et assistant de construction d'expression pour les conditions d'état.

## Motivation

Le champ "Condition" dans la fenêtre d'édition d'état (`ElementStateRuleDialog`) est actuellement un `TextBox` libre. L'utilisateur doit taper manuellement une expression comme `{NomTag} >= 50`. Pour les cas simples (un seul tag), ce devrait être un dropdown de sélection de tag, avec opérateurs adaptés au type de donnée. L'expression libre reste disponible pour les conditions complexes.

## Design

### UI

Deux `RadioButton` en haut de la section condition :

- **Variable** — sélection d'un tag depuis le catalogue
- **Expression** — saisie libre d'une expression (comportement actuel)

#### Mode Variable

1. **ComboBox tags** — liste tous les tags `Enabled` du `ScadaTagCatalog`, triés par `DisplayName`, affichage via `AuthoringLabel` (même pattern que `ElementEventDialog`).

2. **Si le tag sélectionné est booléen** (`bool`, `boolean`, `digital`, case-insensitive) :
   - Deux `RadioButton` : **Si vrai** / **Si faux**
   - Expression générée : `{TagId} == true` ou `{TagId} == false`

3. **Sinon (float, int, ou autre)** :
   - Un `ComboBox` opérateur (affichage → expression) :
     | Affichage | Expression générée |
     |---|---|
     | `<>` | `!=` |
     | `>=` | `>=` |
     | `>` | `>` |
     | `=` | `==` |
     | `<` | `<` |
     | `<=` | `<=` |
   - Un `TextBox` valeur
   - Expression générée : `{TagId} <op-expression> <valeur>`

#### Mode Expression

- Le champ conserve l'expression source éditable et sa validation en direct via `ScadaExpressionValidator.Validate()`.
- Le champ est précédé du label explicite **Expression :**.
- Un bouton **Outil** est placé à droite du champ et ouvre `ExpressionCreationDialog`.

### Assistant `ExpressionCreationDialog`

L'assistant est une surface de composition non destructive : il ne remplace pas le champ d'expression de la règle et insère uniquement du texte à la position demandée.

1. Le dialogue affiche un bouton **Variable**.
2. Le bouton **Variable** ouvre un sélecteur de tags contenant tous les tags disponibles et activés du `ScadaTagCatalog`.
3. Le sélecteur permet de choisir un tag par double-clic ou avec un bouton **Sélectionner**.
4. La valeur insérée est toujours une référence d'expression entre accolades (`{TagDisplayName}` ou `{TagId}` selon le contrat de résolution); elle ne doit jamais être insérée comme un identifiant nu.
5. L'insertion cible la position courante du caret dans `ExpressionTextBox`. Si le caret n'est pas disponible, le fragment est inséré au début de l'expression.
6. Le dialogue propose des boutons d'opérateur avec tooltip explicatif :

   | Bouton | Opérateur inséré | Tooltip attendu |
   |---|---|---|
   | `>` | `>` | Supérieur à |
   | `<` | `<` | Inférieur à |
   | `>=` | `>=` | Supérieur ou égal à |
   | `<=` | `<=` | Inférieur ou égal à |
   | `==` | `==` | Égal à |
   | `!=` | `!=` | Différent de |
   | `&&` | `&&` | Toutes les conditions doivent être vraies |
   | `||` | `||` | Au moins une condition doit être vraie |

7. Les boutons d'opérateur insèrent le texte à la position du caret, avec des espaces autour des opérateurs binaires afin de maintenir une expression lisible.
8. Le dialogue expose un champ d'expression visible, une zone d'outils structurée et des actions **Insérer**, **Effacer** et **Fermer**. La validation utilise le même `ScadaExpressionValidator` que la règle principale.
9. La création d'expression ne sauvegarde pas directement la règle. Elle retourne uniquement le texte composé à `ElementStateRuleDialog`, qui reste responsable de la validation finale, de la construction AST et de la sauvegarde.

### Règles d'insertion et de sécurité

- Un tag peut être inséré plusieurs fois dans une même expression; cela permet notamment de composer `{tagA} == {tagB}`.
- Le mode Variable reste limité à la condition simple sur un seul tag; le mode Expression est le chemin officiel pour comparer plusieurs tags ou composer `&&`/`||`.
- Aucun opérateur fléché `->` ou mécanisme d'affectation n'est ajouté : une condition d'état doit produire une valeur booléenne et ne modifie pas un tag.
- Les références sont résolues vers `TagId` dans l'AST à la sauvegarde, comme dans le flux actuel, sans changer le contrat runtime/export.

### Comportement à l'ouverture (édition d'une règle existante)

Détection automatique du mode :
- Si l'expression source est un simple `{TagId}` → Variable, tag présélectionné, pas d'opérateur (condition "vrai si tag non-zéro" implicite)
- Si `{TagId} == true` ou `{TagId} == false` → Variable, tag présélectionné, radio Si vrai/Si faux
- Si `{TagId} <op> <valeur>` avec opérateur reconnu → Variable, tag présélectionné, opérateur et valeur restaurés
- Si le tag référencé n'existe plus dans le catalogue → fallback en mode Expression avec le texte source restauré
- Si le tag existe mais avec un datatype différent de celui attendu → Variable quand même, le datatype actuel du tag détermine l'UI (booléen ou numérique)
- Tout autre format → Expression, texte restauré

### Validation

Les deux modes passent par `ScadaExpressionValidator.Validate()` avant sauvegarde. Le résultat (`ScadaExpression`) est stocké de manière identique quel que soit le mode utilisé pour le construire.

## Fichiers modifiés

| Fichier | Changement |
|---|---|
| `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml` | Remplacer le TextBox Condition par : RadioButtons Variable/Expression, ComboBox tags, RadioButtons booléens Si vrai/Si faux, ComboBox opérateurs, TextBox valeur, TextBox expression |
| `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs` | Peupler le ComboBox tags depuis `_tagCatalog`, basculer Variable/Expression, détecter le type booléen, construire l'expression, restaurer le mode à l'ouverture |
| `src/ScadaBuilderV2.App/ExpressionCreationDialog.xaml` | Nouveau dialogue structuré pour composer une expression avec outils Variable et opérateurs |
| `src/ScadaBuilderV2.App/ExpressionCreationDialog.xaml.cs` | Gestion du caret, insertion de fragments, sélection de tags, tooltips et validation de l'expression composée |
| `tests/ScadaBuilderV2.Tests/` | Couverture du contrat d'insertion, des deux tags dans une comparaison et des opérateurs proposés |

## Pas de changements

- Aucun changement dans le Domain, l'Application, ou l'Infrastructure
- Aucun changement de contrat
- `ScadaExpression`, `ScadaExpressionValidator`, `ScadaStateRule`, `ScadaEffectBlock` : inchangés
- L'export `.sb2` et le rendu restent identiques — le `ScadaExpression` stocké est le même
- Le dialogue outil ne crée aucun nouvel opérateur ni champ persistant; il consomme le contrat d'expression existant.

## Texte français

Tous les labels UI sont en français :
- "Variable" / "Expression"
- "Si vrai" / "Si faux"
- Opérateurs dans la dropdown : `<>`, `>=`, `>`, `=`, `<`, `<=`
- "Valeur" pour le champ de comparaison numérique
- "Condition valide." pour la validation (existant)
- "Expression :" pour le champ manuel
- "Outil", "Variable", "Sélectionner", "Insérer", "Effacer" et "Fermer" pour l'assistant
