# État — Assistant de création d'expressions

**Date:** 2026-07-13
**Status:** Draft design
**Scope:** Modernisation du mode `Expression` de `ElementStateRuleDialog` avec un assistant guidé pour insérer des tags et opérateurs.

## Problème

Le mode `Expression` expose actuellement un champ texte libre. L'utilisateur doit connaître la syntaxe `{Tag}` et saisir manuellement les opérateurs. Cela rend difficile la composition d'une expression multi-tag telle que `{tagA} == {tagB}`.

## Objectif

Permettre de composer une expression valide depuis une interface guidée, sans retirer l'édition libre et sans modifier le contrat AST, de validation ou d'exécution existant.

## Décisions

1. Le champ principal du mode Expression porte le label **Expression :**.
2. Un bouton **Outil**, placé à droite du champ, ouvre `ExpressionCreationDialog`.
3. `ExpressionCreationDialog` possède un bouton **Variable** qui ouvre un sélecteur contenant tous les tags activés du `ScadaTagCatalog`.
4. Le sélecteur permet la sélection par double-clic ou par bouton **Sélectionner**.
5. Le tag sélectionné est inséré au format `{TagDisplayName}` ou `{TagId}` compatible avec le résolveur actuel, à la position du caret dans le champ principal. Si aucune position n'est disponible, l'insertion se fait au début.
6. Les opérateurs sont proposés sous forme de boutons avec tooltips : `>`, `<`, `>=`, `<=`, `==`, `!=`, `&&`, `||`.
7. Les opérateurs binaires sont insérés avec un espace de chaque côté pour garder l'expression lisible.
8. L'assistant ne sauvegarde pas la règle. Il retourne le texte composé à `ElementStateRuleDialog`, qui conserve la validation finale, la construction de l'AST et la sauvegarde.
9. Un tag peut être inséré plusieurs fois afin de composer `{tagA} == {tagB}`.
10. Aucun opérateur `->` ni mécanisme d'affectation n'est introduit : une condition d'état produit une valeur booléenne et ne modifie pas un tag.

## Architecture et contrat

`ElementStateRuleDialog` reste propriétaire de l'expression éditée et du résultat final. `ExpressionCreationDialog` est une surface WPF d'assistance sans modèle persistant.

```text
ElementStateRuleDialog
  Expression : [expression éditable] [Outil]
                              |
                              v
                 ExpressionCreationDialog
                  [Variable] [opérateurs]
                              |
                              v
                    TagSelectionDialog
                              |
                              v
             fragment {Tag} inséré au caret
```

Le texte produit repasse par `ScadaExpressionValidator.Validate()`. Le `ScadaExpression`, son AST, la résolution `TagId`, le runtime `state-engine.js` et l'export `.sb2` restent inchangés.

## UI attendue

- `ExpressionCreationDialog` doit distinguer clairement la zone d'expression, les outils de variables et les opérateurs.
- Les boutons d'opérateur doivent avoir un libellé court et un tooltip explicatif :

  | Bouton | Tooltip |
  |---|---|
  | `>` | Supérieur à |
  | `<` | Inférieur à |
  | `>=` | Supérieur ou égal à |
  | `<=` | Inférieur ou égal à |
  | `==` | Égal à |
  | `!=` | Différent de |
  | `&&` | Toutes les conditions doivent être vraies |
  | `||` | Au moins une condition doit être vraie |

- Actions minimales : **Variable**, **Sélectionner**, **Insérer**, **Effacer**, **Fermer**.
- Le double-clic sur un tag doit avoir le même résultat que **Sélectionner**.
- La sélection d'un tag ne doit pas écraser l'expression existante.

## Hors scope

- Aucun nouvel opérateur d'affectation ou de flèche.
- Aucun changement au modèle `ScadaExpression` ou à l'AST.
- Aucun changement au runtime TF100Web ou à l'export `.sb2`.
- Aucun remplacement de l'édition libre par un éditeur visuel obligatoire.

## Fichiers pressentis

| Fichier | Rôle |
|---|---|
| `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml` | Ajouter le label et le bouton `Outil` |
| `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs` | Ouvrir l'assistant et appliquer son résultat au caret |
| `src/ScadaBuilderV2.App/ExpressionCreationDialog.xaml` | Nouvelle surface WPF de composition |
| `src/ScadaBuilderV2.App/ExpressionCreationDialog.xaml.cs` | Insertion, opérateurs, validation et sélection de tags |
| `tests/ScadaBuilderV2.Tests/` | Tests UI/contrat et insertion multi-tag |

## Validation

- Vérifier l'insertion d'un tag dans un champ vide, au début, au milieu et en fin d'expression.
- Vérifier le double-clic et le bouton **Sélectionner**.
- Vérifier la composition `{tagA} == {tagB}`.
- Vérifier chaque opérateur et son tooltip.
- Vérifier que l'expression invalide reste refusée à la sauvegarde.
- Vérifier que le JSON/AST produit reste compatible avec le runtime et l'export existants.
