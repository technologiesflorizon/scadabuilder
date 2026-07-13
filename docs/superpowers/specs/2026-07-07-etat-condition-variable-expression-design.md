# État — Condition variable/expression (Tag Picker)

**Date:** 2026-07-07
**Status:** Spec
**Scope:** `ElementStateRuleDialog` — remplacer le champ Condition texte libre par un choix Variable/Expression avec dropdown tag.

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

- Le `TextBox` existant, avec validation en direct via `ScadaExpressionValidator.Validate()`.

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

## Pas de changements

- Aucun changement dans le Domain, l'Application, ou l'Infrastructure
- Aucun changement de contrat
- `ScadaExpression`, `ScadaExpressionValidator`, `ScadaStateRule`, `ScadaEffectBlock` : inchangés
- L'export `.sb2` et le rendu restent identiques — le `ScadaExpression` stocké est le même

## Texte français

Tous les labels UI sont en français :
- "Variable" / "Expression"
- "Si vrai" / "Si faux"
- Opérateurs dans la dropdown : `<>`, `>=`, `>`, `=`, `<`, `<=`
- "Valeur" pour le champ de comparaison numérique
- "Condition valide." pour la validation (existant)
