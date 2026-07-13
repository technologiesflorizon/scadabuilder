# État — Assistant de création d'expressions — Spécification de conception

Date: 2026-07-13
Status: Draft design
Portée: SCADA Builder V2 — App WPF uniquement (nouveau dialogue + modification d'un dialogue existant)
Dépendances: `docs/superpowers/specs/2026-07-07-etat-condition-variable-expression-design.md`, `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`
Version du document: `V2.1.4.0003`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-13 | `V2.1.4.0003` | `PENDING` | Corrections de décision : caret nul ou position zéro au début, assistant avec copie locale et bouton Appliquer, vocabulaire d'actions harmonisé, suite de tests dédiée et version alignée. |
| 2026-07-13 | `V2.1.4.0000` | `PENDING` | Standardisation : hypothèses vérifiées contre le code, contrat AST/parser/validateur documenté, décisions de format d'insertion explicitées, parenthèses ajoutées aux opérateurs, flux de résolution TagId documenté. |
| 2026-07-13 | `V2.1.3.0000` | `PENDING` | Première ébauche : assistant de composition guidée pour le mode Expression. |

---

## 1. Problème

### 1.1 État actuel (vérifié contre le code)

Le dialogue `ElementStateRuleDialog` (`src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml:68-72`) propose deux modes pour définir une condition d'état :

- **Mode Variable** : structuré — un `ComboBox` de tags, un sélecteur booléen (Vrai/Faux) ou un comparateur numérique (opérateur + valeur).
- **Mode Expression** : libre — un `TextBox` nu, sans label, sans assistant, sans aide syntaxique.

Le mode Expression est puissant mais inaccessible : l'utilisateur doit connaître la syntaxe `{TagName}`, les opérateurs disponibles (`==`, `!=`, `<`, `<=`, `>`, `>=`, `&&`, `||`), et les fonctions (`ABS()`, `MIN()`, `MAX()`, `BIT()`). Aucun mécanisme ne guide l'insertion d'un tag ou d'un opérateur.

Le parser (`ScadaExpressionParser.Parse`, `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpressionParser.cs:34`) accepte la syntaxe `{tag}` pour les références et produit un AST polymorphique (`ScadaExprNode` : `ScadaExprTagRef`, `ScadaExprBinary`, `ScadaExprUnary`, `ScadaExprFunc`, `ScadaExprLiteralNumber`, `ScadaExprLiteralBool`, `ScadaExprLiteralString`).

Le validateur (`ScadaExpressionValidator.Validate`, `ScadaExpressionValidator.cs:109`) vérifie la syntaxe, l'existence des tags via `TryResolveTagReference` (Id → DisplayName → KeywordLabel), l'arité des fonctions, la division par zéro littérale et le type booléen racine.

Le dialogue reçoit déjà un `ScadaTagCatalog?` (`ElementStateRuleDialog.xaml.cs:13,30`) et expose les tags activés (`tag.Enabled == true`) dans le mode Variable.

### 1.2 Lacune

Le mode Expression est un champ texte brut. L'utilisateur doit composer manuellement `{NomTag} >= 80 && {AutreTag}` sans aucune assistance, rendant la composition d'expressions multi-tags difficile et sujette aux erreurs de syntaxe.

---

## 2. Objectif

Permettre de composer une expression valide via une interface guidée (`ExpressionCreationDialog`), accessible depuis un bouton **Outil** dans le mode Expression, sans retirer l'édition libre et sans modifier le contrat AST, de validation ou d'exécution existant.

```text
ElementStateRuleDialog
  Expression : [________expression éditable________] [Outil]
                              |
                              v
                 ExpressionCreationDialog
                  [Variable] [>] [<] [==] [!=] [&&] [||] [(] [)] [Effacer]
                              |
                              v
                    TagSelectionDialog
                              |
                              v
             fragment {TagDisplayName} inséré au caret
```

---

## 3. Décisions

| # | Décision |
|---|---|
| D1 | Le champ Expression porte le label **Expression :** et un bouton **Outil** à sa droite. |
| D2 | Le bouton **Outil** ouvre `ExpressionCreationDialog` en tant que boîte de dialogue modale. |
| D3 | `ExpressionCreationDialog` possède un bouton **Variable** qui ouvre un sélecteur de tags (`TagSelectionDialog`). |
| D4 | Le sélecteur affiche tous les tags activés (`tag.Enabled == true`) du `ScadaTagCatalog`, triés par `DisplayName`. |
| D5 | Le tag sélectionné est inséré au format `{DisplayName}` dans la copie locale de l'assistant. Si la position de caret transmise est `null` ou égale à `0`, l'insertion se fait au début; sinon elle se fait à la position du caret, bornée à la longueur du texte. |
| D6 | Le format `{DisplayName}` est cohérent avec le mode Variable existant (`BuildExpressionFromVariable`, `ElementStateRuleDialog.xaml.cs:507`) qui utilise déjà `tag.DisplayName`. La résolution canonique vers `TagId` est effectuée par `OnSaveClick` (`ResolveTagIds`, ligne 562) et ne concerne pas l'assistant. |
| D7 | Les opérateurs sont proposés sous forme de boutons avec tooltips explicites en français. |
| D8 | Les opérateurs sont insérés avec un espace de chaque côté pour la lisibilité (` == `, ` && `). Les parenthèses sont insérées sans espace supplémentaire. |
| D9 | L'assistant travaille sur une copie locale de l'expression reçue à l'ouverture. Le bouton **Appliquer** retourne le texte composé à `ElementStateRuleDialog`; l'assistant ne sauvegarde jamais la règle. Le dialogue hôte conserve la validation finale (`ScadaExpressionValidator.Validate`), la construction AST (`ScadaExpression.FromSource`) et la sauvegarde. |
| D10 | Un tag peut être inséré plusieurs fois afin de composer des comparaisons multi-tags comme `{TagA} == {TagB}`. |
| D11 | Aucun opérateur `->` ni mécanisme d'affectation n'est introduit : une condition d'état produit une valeur booléenne et ne modifie pas un tag. |
| D12 | Les fonctions (`ABS`, `MIN`, `MAX`, `BIT`) ne sont pas exposées dans cette première itération. Elles restent utilisables en édition libre. Leur ajout à l'assistant est réservé pour une évolution future. |

---

## 4. Architecture et contrat

### 4.1 Responsabilités

`ElementStateRuleDialog` reste propriétaire de l'expression éditée et du résultat final (`ScadaStateRule? Result`). `ExpressionCreationDialog` est une surface WPF d'assistance sans modèle persistant. `TagSelectionDialog` est un sélecteur de tag simple.

```
ElementStateRuleDialog (propriétaire)
  ├── ExpressionTextBox (édition libre)
  ├── Bouton Outil → ouvre ExpressionCreationDialog
  │
  └── ExpressionCreationDialog (assistant, sans modèle persistant)
        ├── Copie locale de l'expression éditable
        ├── Bouton Variable → ouvre TagSelectionDialog
        ├── Boutons d'opérateurs
        └── Bouton Appliquer → retourne le texte modifié à ElementStateRuleDialog
              │
              └── TagSelectionDialog (sélecteur)
                    └── Retourne le tag sélectionné (Id + DisplayName)
```

### 4.2 Contrat avec le modèle existant

Le texte produit par l'assistant repasse par le flux de validation et sauvegarde existant :

1. `ExpressionTextBox.Text` reçoit le texte retourné par l'assistant.
2. `OnExpressionTextChanged` → `ValidateExpression()` → `ScadaExpressionValidator.Validate(source, _tagCatalog)`.
3. `OnSaveClick` → `ScadaExpression.FromSource(source)` → `ResolveTagIds(ast, selectedTagId)` → `ScadaStateRule`.
4. Le `ScadaExpression`, son AST, la résolution `TagId`, le runtime `state-engine.js` et l'export `.sb2` restent inchangés.

Aucun nouveau champ dans le modèle Domain. Aucun changement au parser, au validateur, à l'AST ou au sérialiseur JSON.

---

## 5. Conception UI

### 5.1 ExpressionCreationDialog

Le dialogue est une fenêtre WPF modale légère avec :

- **Zone d'expression** : une copie locale dans un `TextBox` multiligne (`AcceptsReturn="True"`, `TextWrapping="Wrap"`, min-height 80px) affichant l'expression reçue à l'ouverture.
- **Barre d'outils** : une rangée de boutons organisés en groupes logiques.
- **Validation inline** : un `TextBlock` sous la zone d'expression affiche le résultat de `ScadaExpressionValidator.Validate` (vert si valide, rouge avec les erreurs sinon), mis à jour à chaque modification.

### 5.2 Boutons d'opérateurs

Deux groupes de boutons :

**Groupe 1 — Comparateurs :**

| Bouton | Tooltip |
|---|---|
| `>` | Supérieur à |
| `<` | Inférieur à |
| `>=` | Supérieur ou égal à |
| `<=` | Inférieur ou égal à |
| `==` | Égal à |
| `!=` | Différent de |

**Groupe 2 — Logique et structure :**

| Bouton | Tooltip |
|---|---|
| `&&` | Toutes les conditions doivent être vraies (ET) |
| `\|\|` | Au moins une condition doit être vraie (OU) |
| `( )` | Parenthèses (grouper des conditions) |

Les opérateurs binaires (`==`, `!=`, `<`, `<=`, `>`, `>=`, `&&`, `||`) sont insérés avec un espace de chaque côté. Les parenthèses sont insérées sans espace supplémentaire. Exemples :
- Clic sur `==` → insère ` == `
- Clic sur `( )` → insère `()` avec le caret positionné entre les parenthèses

### 5.3 Actions

| Action | Bouton | Comportement |
|---|---|---|
| **Variable** | Bouton "Variable" | Ouvre `TagSelectionDialog`. Au retour, insère `{DisplayName}` au caret. |
| **Effacer** | Bouton "Effacer" | Vide le contenu de la zone d'expression. |
| **Appliquer** | Bouton "Appliquer" | Retourne la copie locale à `ElementStateRuleDialog` et ferme l'assistant. |
| **Annuler** | Bouton "Annuler" | Ferme l'assistant sans modifier l'expression du dialogue hôte. |

La sélection d'un tag ne doit pas écraser l'expression existante — elle insère au caret.

### 5.4 TagSelectionDialog

Une liste (`ListBox`) affichant les tags activés du catalogue, avec les colonnes ou le format : `DisplayName` | `Id` (si différent) | `Datatype`.

- **Tri** : par `DisplayName`, insensible à la casse.
- **Double-clic** sur un tag → sélectionne et ferme (même comportement que le bouton **Sélectionner**).
- **Bouton Sélectionner** → retourne le tag sélectionné et ferme.
- **Bouton Annuler** → ferme sans sélection.

---

## 6. Flux d'édition

### 6.1 Ouverture de l'assistant

1. L'utilisateur clique sur **Outil** dans `ElementStateRuleDialog`.
2. `ExpressionCreationDialog` s'ouvre avec une copie du contenu actuel de `ExpressionTextBox.Text` et la position actuelle du caret.
3. La validation est exécutée immédiatement sur le texte existant.

### 6.2 Insertion d'un tag

1. L'utilisateur positionne le caret dans la copie locale de l'expression.
2. L'utilisateur clique sur **Variable**.
3. `TagSelectionDialog` s'ouvre, affichant tous les tags activés.
4. L'utilisateur sélectionne un tag (double-clic ou bouton **Sélectionner**).
5. Le dialogue ferme et retourne le `DisplayName` du tag.
6. `ExpressionCreationDialog` insère `{DisplayName}` au début si le caret reçu est `null` ou `0`; sinon à la position du caret local.
7. La validation est réexécutée.

### 6.3 Insertion d'un opérateur

1. L'utilisateur positionne le caret.
2. L'utilisateur clique sur un bouton d'opérateur (ex: `==`).
3. Le texte ` == ` est inséré au caret.
4. La validation est réexécutée.

### 6.4 Fermeture et application

1. L'utilisateur clique sur **Appliquer**.
2. Le texte courant est retourné à `ElementStateRuleDialog`.
3. `ExpressionTextBox.Text` est mis à jour.
4. `ValidateExpression()` est exécutée dans `ElementStateRuleDialog`.
5. L'utilisateur peut continuer à éditer librement ou sauvegarder.

---

## 7. Validation et tests

### 7.1 Tests UI/contrat

| Test | Fichier |
|---|---|
| Insertion d'un tag dans un champ vide | `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs` |
| Insertion d'un tag au début, au milieu et en fin d'expression | `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs` |
| Caret `null` ou `0` : insertion au début | `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs` |
| Insertion d'un tag via double-clic et via bouton **Sélectionner** | `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs` |
| Composition `{TagA} == {TagB}` | `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs` |
| Vérification du tooltip de chaque opérateur | `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs` |
| Le bouton **Effacer** vide la copie locale | `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs` |
| Insertion de parenthèses positionne le caret entre `()` | `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs` |
| Le bouton **Annuler** ne retourne pas la copie locale | `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs` |
| Le bouton **Appliquer** retourne la copie locale sans sauvegarder la règle | `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs` |

### 7.2 Tests de non-régression

| Test | Fichier |
|---|---|
| Expression invalide reste refusée à la sauvegarde | `ScadaExpressionValidatorTests.cs` |
| Le JSON/AST produit reste compatible avec le runtime existant | `ScadaExpressionTests.cs`, `ScadaExprNodeTests.cs` |
| La résolution `TagId` canonique fonctionne après insertion par l'assistant | `ScadaExpressionTests.cs` |
| Le mode Variable n'est pas affecté par l'assistant | `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs` |

---

## 8. Fichiers et responsabilités

| Surface | Fichier | Modification |
|---|---|---|
| **Dialog hôte XAML** | `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml` | Ajout du bouton **Outil** à droite du `ExpressionTextBox` |
| **Dialog hôte code** | `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs` | Ouverture de l'assistant, réception du résultat |
| **Assistant XAML** | `src/ScadaBuilderV2.App/ExpressionCreationDialog.xaml` | **Nouveau** — surface WPF de composition |
| **Assistant code** | `src/ScadaBuilderV2.App/ExpressionCreationDialog.xaml.cs` | **Nouveau** — logique d'insertion, opérateurs, validation inline |
| **Sélecteur XAML** | `src/ScadaBuilderV2.App/TagSelectionDialog.xaml` | **Nouveau** — liste des tags activés |
| **Sélecteur code** | `src/ScadaBuilderV2.App/TagSelectionDialog.xaml.cs` | **Nouveau** — filtrage, tri, sélection |
| **Tests assistant** | `tests/ScadaBuilderV2.Tests/ElementEvents/ExpressionCreationDialogContractTests.cs` | Tests de contrat WPF dédiés à l'assistant |

---

## 9. Hors scope

- Aucun nouvel opérateur d'affectation ou de flèche.
- Aucun changement au modèle `ScadaExpression`, à l'AST ou au parser.
- Aucun changement au runtime TF100Web, à l'export `.sb2` ou au `state-engine.js`.
- Aucun remplacement de l'édition libre par un éditeur visuel obligatoire.
- Aucun bouton pour les fonctions `ABS`, `MIN`, `MAX`, `BIT` dans cette itération (D12).
- Aucune coloration syntaxique dans la zone d'expression.
- Aucun auto-complétion des noms de tags pendant la frappe.

---

## 10. Critères d'acceptation

1. Le mode Expression affiche un label **Expression :** et un bouton **Outil**.
2. Le bouton **Outil** ouvre `ExpressionCreationDialog` avec le contenu existant pré-rempli.
3. Le bouton **Variable** ouvre un sélecteur listant tous les tags activés, triés par `DisplayName`.
4. La sélection d'un tag (double-clic ou bouton) insère `{DisplayName}` au caret sans écraser l'expression.
5. Les 8 opérateurs (`>`, `<`, `>=`, `<=`, `==`, `!=`, `&&`, `||`) et les parenthèses sont disponibles comme boutons avec tooltips en français.
6. Les opérateurs binaires sont insérés avec un espace de chaque côté.
7. L'insertion de parenthèses positionne le caret entre `(` et `)`.
8. Le bouton **Effacer** vide la zone d'expression.
9. La validation inline affiche les erreurs en temps réel (vert si valide, rouge sinon).
10. Le bouton **Appliquer** retourne la copie locale à `ElementStateRuleDialog` sans sauvegarder; **Annuler** ne la retourne pas.
11. `ElementStateRuleDialog.OnSaveClick` continue de valider, parser et résoudre les `TagId` canoniques comme avant.
12. Le mode Variable n'est pas modifié par l'ajout de l'assistant.
13. Les tests existants (parser, validateur, AST, export) passent sans modification.
14. Aucun nouveau champ dans le modèle Domain ni changement au contrat `.sb2`.
