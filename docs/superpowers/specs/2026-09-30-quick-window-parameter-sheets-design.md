# Feuilles de paramètres des fenêtres rapides - Spécification

Date: 2026-09-30
Status: Approved - DEC-0052
Document version: `V2.1.6.0062`
Portée: SCADA Builder V2 (modèle, éditeur, compilateur, runtime partagé embarqué dans le paquet) et rejeu de conformance sur la branche TF100Web `codex/quick-window-v1`
Dépendances: `docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md` (`DEC-0050`), `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md` §11b et §12, `docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md`, `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`, `docs/08_implementation_status/KNOWN_GAPS_V2.md`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-09-30 | `V2.1.6.0062` | `9197796` | Revue finale du chantier `2026-09-30-quick-window-parameter-sheets` (`DEC-PARAMSHEETS` = `DEC-0052`), correctif M5 (documentation). Spec : §11(2) corrigé selon R1 (`"x"` → `Literal`, `1` → `Expression`, et non `Literal`) ; le tableau de validation par couple de §6 gagne les cas livrés par la revue finale (`read-not-tag`, texte non littéral, `placeholder-unsupported-site`, `literal-rejected-by-runtime`, `page-not-materialized`) et le paragraphe « Type des membres » précise qu'un désaccord de type donne `String` sans erreur et que le contrôle de type tag ne s'applique pas à un membre `#N` (I5). `KNOWN_GAPS_V2.md` entrée 36 : le défaut de canonicalisation est attribué à `ParameterSheetLowering.cs` (et non `QuickWindowCompiler.cs`), et l'entrée décrit exactement la couverture après C1/I2/M1/M4 — y compris ce qui reste non couvert : `Data.TagBinding` et les `ValueBindings` de cellule de tableau sont validés comme sites de lecture mais jamais émis dans le DOM d'une définition, donc n'animent rien dans une fenêtre rapide. Aucun code touché. |
| 2026-09-30 | `V2.1.6.0034` | `a32545a` | Approuvée sous `DEC-0052`. Statut passé à *Approved*; rulings R1 à R4 ajoutés en fin de §3. |
| 2026-09-30 | `V2.1.6.0033` | `9ba10d4` | Création. Remplace le modèle d'authoring des fenêtres rapides par des pages de type Fenêtre rapide paramétrées par `#N` et des feuilles de paramètres, résolues à l'ouverture de l'instance. Supersède à l'approbation `2026-09-24-fragment-to-quick-window-conversion-design.md`. |

## 1. Le problème

Retour de l'utilisateur après essai de la branche `feature/quick-window-front-door` : le « + » du panneau « Fenêtres rapides » crée une définition nommée, traitée comme une bibliothèque, et cette stratégie ne permet pas de travailler. Il demande le modèle qu'il connaît de FactoryTalk View :

- une **feuille de paramètres** idempotente, table de correspondance textuelle `#1 := …`, commentaires `//` sur une ligne ;
- une **page de type Fenêtre rapide** dont les états, commandes et textes utilisent `#1` à `#999` à la place des tags ;
- une commande **« Ouvrir une fenêtre rapide »** qui associe une feuille à une page ;
- la liaison entre chaque instance et sa feuille produite par la chaîne de compilation.

## 2. Constats vérifiés au 2026-09-30

**Le dernier maillon des ports n'existe dans aucun code de production.** `quick-window-runtime.js` expose `readPort` et `writePort`, qui résolvent correctement un tag, un littéral ou une expression. Mais ils ne sont appelés que par des tests : `tests/runtime-js/quick-window-runtime.test.mjs` côté Builder, `frontend/tests_runtime_js/quick-window-runtime-handshake.test.mjs` côté TF100Web. Le test `frontend/tests_scada_quick_window_host.py` interdit même à l'hôte TF100Web de les appeler. Le compilateur ne réécrit pas non plus les références des éléments d'une fenêtre rapide. Un élément de fenêtre rapide lié à un port n'est donc animé nulle part. Les 126 sondes de la Phase 6 prouvent l'API de résolution, pas l'animation des éléments ; la capacité `quick-window.port-binding` est promue sur cette seule base.

**Le point d'accroche existe et est partagé.** L'aperçu du Builder (`src/ScadaBuilderV2.Rendering/Runtime/quick-window-host.js`) et l'hôte TF100Web (`static/asset/js/quick-window-host.js`) construisent le DOM de l'instance, puis appellent tous deux `mountInstance(runtimeInstanceId, root)` du runtime partagé livré dans le paquet. Celui-ci appelle ensuite `ScadaRuntime.initPage(root, runtimeInstanceId)`.

**Le moteur d'éléments lit ses liaisons dans quatre attributs** du DOM exporté : `data-scada-state-config`, `data-scada-command-config`, `data-scada-read-tag`, `data-scada-write-tag` (`scada-runtime.js`).

**Les trois natures de valeur existent déjà.** `QuickWindowBindingSourceKind` porte `Tag`, `Literal` et `Expression`. `ScadaExpressionParser` accepte littéraux, références `{tag}`, `! -`, arithmétique, comparaisons, `&& ||` et `ABS MIN MAX BIT` — la grammaire des conditions d'état.

**Le support TF100Web n'est pas en production.** Il vit sur la branche `codex/quick-window-v1`, non fusionnée dans `main` (32 commits d'avance), worktree `F:\Projet\Git\TF100Web-qw`. Le contrat n'a donc aucun consommateur déployé.

**L'hôte n'ouvre qu'une instance par définition** : capacité `quick-window.instance.single-per-definition`, promue.

**Il n'y a rien à migrer.** Les deux projets ne portent aucune invocation, et une seule définition, `fenetre`, vide, créée pendant les essais dans `TEST_CONVERSION`.

## 3. Décisions

| # | Décision | Alternatives écartées |
| --- | --- | --- |
| D1 | La partie droite d'une ligne est une **expression de la grammaire existante**. `{TAG}` désigne un tag, `"texte"` ou un nombre un littéral, le reste une expression. | Style Structured Text et syntaxe propre aux feuilles : deux syntaxes dans un même outil et un second analyseur. |
| D2 | Une ligne peut lier un **tag, un littéral ou une expression**. | Tag seul : ne permet ni titre d'équipement ni valeur calculée. |
| D3 | `#N` est utilisable dans les **états, les commandes et les textes** affichés. | États et commandes seulement : un littéral ne pourrait plus nommer l'équipement affiché. |
| D4 | Approche **C** : la page est compilée une seule fois avec ses `#N`, chaque feuille devient une table dans le paquet, et **le runtime substitue à l'ouverture de l'instance**. C'est le mécanisme de FactoryTalk et la structure de `DEC-0050`. | A, résolution à la compilation : une copie de page par feuille et une recompilation par équipement ajouté. B, ports lus en continu par les éléments : chantier runtime et hôte bien plus large. |
| D5 | La règle **une instance par définition** est conservée : ouvrir Ventilateur 5 remplace Ventilateur 3. | Instances simultanées : chantier TF100Web supplémentaire, à reprendre séparément. |
| D6 | `ScadaPageType.Fragment` quitte le sélecteur et reste lu. Une page Fragment se **convertit en la retypant** en Fenêtre rapide. | Conversion vers une définition séparée : c'est l'objet de la spec du 2026-09-24, rendue inutile par ce modèle. |
| D7 | Un même `#N` défini deux fois dans une feuille est une **erreur**, jamais « le dernier gagne ». | Dernier gagne : la feuille dépendrait de l'ordre de ses lignes et cesserait d'être idempotente. |

**Rulings R1 à R4 (`DEC-0052`) :**

| # | Ruling |
| --- | --- |
| R1 | Seul un littéral chaîne est de nature `Literal`; un nombre ou un booléen seul est une expression à un nœud (le runtime évalue des AST typés). |
| R2 | Un `#N` utilisé dans un texte affiché doit être lié à un littéral chaîne; lié à un tag ou à une expression, c'est une erreur de compilation (une substitution textuelle afficherait un identifiant technique comme `tf100.mapping.196`). Les valeurs vivantes continuent d'utiliser l'affichage de variable de lecture existant, qui accepte un `#N` lié à un tag. |
| R3 | Le runtime possède un évaluateur d'expressions mais aucun analyseur; une liaison `Expression` porte donc, en plus de sa source, un champ manifeste optionnel `ExpressionAst` : l'AST sérialisé au format runtime (discriminant `type`, mêmes options de sérialisation que celles déjà utilisées par l'exporteur pour `data-scada-state-config`). |
| R4 | `DefinitionKey` est la `PageKey` de la page fenêtre rapide; `MemberKey` et `InvocationKey` sont dérivés de façon déterministe par SHA-256 de chaînes canoniques, de sorte que deux compilations du même projet soient identiques. |

## 4. Modèle d'authoring

### 4.1 Page Fenêtre rapide

`ScadaPageType` gagne la valeur `QuickWindow`, ajoutée **en dernière position**. L'enum est persisté sous forme de nom (`"PageType": "Fragment"` dans les scènes), mais aucune valeur existante n'est renumérotée.

Une page `QuickWindow` :

- n'est jamais exportée comme page navigable, ni retenue comme page d'accueil, en-tête ou pied de page ;
- voit ses sélecteurs de tag proposer `#1` à `#999` au lieu du catalogue du projet ;
- accepte `#N` dans ses textes affichés.

Le sélecteur de type de page de `MainWindow.xaml` mappe aujourd'hui les types **par position** (`GetSelectedPageType`, `SelectPageType` dans `MainWindow.xaml.cs`, `1 => ScadaPageType.Fragment`). Il passe à un mappage **par valeur** avant tout retrait d'entrée, sans quoi retirer « Fragment » changerait silencieusement les en-têtes en pieds de page.

### 4.2 Feuille de paramètres

Entité de projet `ParameterSheet` :

| Champ | Rôle |
| --- | --- |
| `SheetKey` | GUID stable. |
| `Code` | Dérivé du nom par `PageCodePolicy.DeriveCodeFromDisplayName`, unique dans le projet. |
| `DisplayName` | Nom libre affiché dans les listes. |
| `Text` | **Source de vérité.** Tout le reste en est dérivé à la lecture. |

Persistance : `parameter-sheets/<SheetKey:N>.parameter-sheet.json`, sur le modèle de `quick-windows/` (`QuickWindowStore`), avec le même contrôle de chemin et la même écriture atomique que `ModernProjectStore`.

### 4.3 Syntaxe d'une feuille

```
// Ventilateur condenseur 3
#1 := {FAN_03_RUN}
#2 := {FAN_03_FAULT}
#3 := {FAN_03_SPEED} * 0.1
#4 := "Ventilateur 3"
#5 := 1
```

- Une ligne utile est `#N := <expression>`, `N` entier de 1 à 999, sans zéro de tête.
- `//` ouvre un commentaire jusqu'à la fin de la ligne, y compris après une ligne utile. Pas de commentaire multi-lignes.
- Les lignes vides ou de blancs sont ignorées. L'ordre des lignes n'a aucun sens.
- La partie droite est analysée par `ScadaExpressionParser` et **classée** : exactement une référence `{TAG}` donne `Tag`, exactement un littéral donne `Literal`, tout le reste donne `Expression`.
- Une partie droite ne peut pas référencer un autre `#N` : l'imbrication de fenêtres (`quick-window.binding.parent-port`) reste `Blocked`.

L'analyse est **pure et totale** : un texte produit toujours la même table et la même liste de diagnostics, chacun porteur de son numéro de ligne. C'est le sens d'« idempotente ».

### 4.4 `#N` dans une page Fenêtre rapide

- Un jeton est `#` suivi de 1 à 3 chiffres, non suivi d'un chiffre : `#1` dans `#10` n'est pas reconnu.
- Dans un texte, `\#` produit un `#` littéral, pour les libellés du type « Pompe \#2 » hérités d'un import.
- Hors de cet échappement, tout `#N` d'un texte de page Fenêtre rapide est un paramètre. Un `#N` hérité non voulu est donc signalé à la compilation plutôt qu'affiché tel quel.

## 5. Surfaces UI

**Panneau « Fenêtres rapides »** : il liste les **feuilles de paramètres**. Son « + » crée une feuille, sous un nom libre, et ouvre son éditeur.

**Éditeur de feuille** : un textarea monospace pour le texte, les diagnostics affichés ligne par ligne sous le texte, et dans l'en-tête un « + » qui ouvre le **dialogue de règle** :

- numéro `#N` proposé = premier numéro libre, modifiable ;
- nature : Tag, Littéral ou Expression ;
- pour Tag, une déroulante du catalogue de tags du projet ; pour les deux autres, une zone de saisie validée par l'analyseur ;
- Enregistrer ajoute **une ligne** au texte, qui reste la seule source de vérité.

**Commande « Ouvrir une fenêtre rapide »** : dans `ElementCommandDialog`, le type ouvre le dialogue **« Configuration de la fenêtre rapide »** — déroulante des feuilles de paramètres, déroulante des pages de type Fenêtre rapide, Annuler, Enregistrer. Il remplace la déroulante de définition et l'onglet « Liaisons ». Le grisage déjà livré s'adapte : l'entrée est grisée quand le projet n'a aucune page Fenêtre rapide **ou** aucune feuille de paramètres, avec en infobulle la raison qui s'applique — les deux si les deux manquent.

**« Interface locale »** : liste en lecture seule des `#N` utilisés par la page Fenêtre rapide active, avec leurs emplacements. Plus d'édition manuelle de membres typés.

**Banc d'essai** : une déroulante de feuille permet de prévisualiser la page avec ses valeurs.

**Type de page** : « Fenêtre rapide » apparaît dans le sélecteur et dans le filtre du panneau des pages ; « Fragment » disparaît des deux.

## 6. Compilation

La chaîne produit les structures existantes de `DEC-0050`, ce qui garde intact le manifeste 2.3 :

| Authoring | Compilé en |
| --- | --- |
| Page `QuickWindow` | Une `QuickWindowDefinition`. Son contenu est celui de la page, **`#N` conservés**. Un membre public par `#N` utilisé, nommé `#N`. |
| Commande « Ouvrir une fenêtre rapide » (feuille, page) | Une `QuickWindowInvocation` de cette définition, avec une liaison par `#N` utilisé par la page, de nature `Tag`, `Literal` ou `Expression` selon le classement de §4.3. |

Deux commandes qui associent la même feuille à la même page produisent deux invocations identiques en contenu ; aucune déduplication n'est exigée.

**Validation par couple (feuille, page)**, fail-closed avant toute écriture d'export :

| Cas | Sévérité |
| --- | --- |
| `#N` utilisé par la page, absent de la feuille | Erreur |
| `#N` en cible d'écriture (`data-scada-write-tag`, commande d'écriture) dont la partie droite n'est pas un `Tag` seul | Erreur |
| `#N` lu comme un tag (`data-scada-read-tag`, variable lue, tag relu par une commande, lecture de cellule de tableau) dont la partie droite n'est pas un `Tag` seul (`parameter-sheet.read-not-tag`) | Erreur |
| `#N` d'un texte affiché dont la partie droite n'est pas un littéral chaîne (R2) | Erreur |
| `#N` dans un champ que la substitution de §7 ne réécrit pas : texte d'un effet d'état, format d'une variable lue, message de confirmation, attributs d'une saisie (`parameter-sheet.placeholder-unsupported-site`) | Erreur |
| Valeur de liaison que `_isInjection` du runtime refuserait au montage (`parameter-sheet.literal-rejected-by-runtime`) | Erreur |
| Page Fenêtre rapide importée dont la couche HTML source n'est pas entièrement remplacée par des Element+ (`parameter-sheet.page-not-materialized`) | Erreur |
| Tag référencé absent du catalogue du projet | Erreur |
| Erreur de syntaxe ou `#N` défini deux fois | Erreur |
| `#N` défini dans la feuille, inutilisé par la page | Avertissement |

Chaque diagnostic nomme la feuille, la page, le `#N` et la ligne de la feuille.

**Type des membres.** Le manifeste exige un type par membre, alors qu'une même page peut être liée à un tag booléen par une feuille et à un réel par une autre. La substitution de §7 étant textuelle, le runtime ne s'appuie pas sur ce type. Il est déduit quand toutes les invocations concordent et vaut `String` sinon, **sans erreur** : le contrôle de compatibilité de type tag ne s'applique pas à un membre `#N`, si bien qu'un tag booléen et un tag réel liés au même `#N` par deux feuilles valident tous deux. Voir §12.

## 7. Runtime : substitution à l'ouverture

Dans `quick-window-runtime.js`, `mountInstance` réécrit les `#N` de l'instance **après** `context.root = root` et **avant** `ScadaRuntime.initPage`. La substitution se fait une seule fois par instance.

| Emplacement | Règle |
| --- | --- |
| `data-scada-read-tag`, `data-scada-write-tag` | L'attribut valant exactement `#N` est remplacé par l'identifiant du tag. Une nature autre que `Tag` est un refus au montage. |
| `data-scada-state-config`, `data-scada-command-config` | Le JSON est **analysé**, les champs portant un identifiant de tag ou une expression sont substitués, puis le JSON est **re-sérialisé**. Jamais de remplacement de chaîne sur le JSON brut. Dans une expression, un `Tag` devient `{TAG}`, une `Expression` devient `(…)` et un `Literal` devient un littéral correctement échappé. |
| Nœuds texte | Remplacement de `#N` par la valeur d'affichage, via `textContent` uniquement, jamais `innerHTML`. `\#` devient `#`. |

Les contrôles existants s'appliquent aux valeurs substituées : `_isInjection` refuse toute valeur de liaison suspecte avant le montage, et un refus est diagnostiqué et non monté. Un `#N` sans liaison au montage — impossible après la validation de §6, mais défendu quand même — est un refus diagnostiqué, jamais un affichage de `#N`.

Écrit **une seule fois**, dans le runtime partagé livré dans le paquet : l'aperçu du Builder et l'hôte TF100Web l'exécutent tous deux par `mountInstance`. **Aucun changement de code de l'hôte TF100Web n'est attendu.**

## 8. Contrat de paquet et TF100Web

- `FT100_TF100WEB_PACKAGE_CONTRACT_V2.md` §11b et §12 est mis à jour **avant** toute implémentation : présence de `#N` dans le contenu d'une définition, sémantique de substitution au montage, règles de §7.
- Sur `codex/quick-window-v1` : un nouveau fixture exporté, et le rejeu des suites `quick-window-*` et `tests_scada_quick_window_*`. Si l'hôte valide le type des membres, §12 le traite.
- `quick-window.port-binding` n'est considérée réellement couverte que lorsqu'une sonde prouve qu'**un élément** d'une instance ouverte dans l'hôte est animé par la valeur substituée — pas seulement que `readPort` répond.
- `KNOWN_GAPS_V2.md` consigne l'écart de §2 **dès la première tâche**, indépendamment de la suite.

## 9. Ce qui est remplacé

- La spec `2026-09-24-fragment-to-quick-window-conversion-design.md` (`8c8e544`) passe *Superseded* à l'approbation de celle-ci.
- La création d'une définition vide par le « + », l'onglet « Liaisons » et l'édition manuelle des membres typés disparaissent des surfaces. Les services de domaine sous-jacents restent, puisque la compilation les produit.
- La définition vide `fenetre` de `TEST_CONVERSION` peut être supprimée.

## 10. Hors périmètre

- Plusieurs instances simultanées d'une même page (D5).
- Paramètres nommés et liaison indirecte par instance de type (UDT), à la manière d'Ignition. Le modèle interne nomme déjà ses membres ; l'ajout reste possible sans rien casser.
- Fenêtre rapide ouverte depuis une fenêtre rapide avec transmission de `#N` (`parent-port`, `Blocked`).
- Modification d'une feuille prise en compte par une instance déjà ouverte : la substitution se fait à l'ouverture.
- Fusion de `codex/quick-window-v1` dans `main` de TF100Web et déploiement en production.

## 11. Validation

**Domaine et application (MSTest)** :

1. Analyse de feuille : les six lignes de l'exemple de §4.3 donnent la bonne table et la bonne nature ; commentaire en fin de ligne ; lignes vides ; `#0`, `#1000` et `#01` refusés ; `#N` en double refusé avec les deux numéros de ligne ; deux analyses du même texte identiques.
2. Classement (R1) : `{A}` → `Tag`, `"x"` → `Literal`, `1`, `{A} * 0.1` et `{A} && {B}` → `Expression`.
3. Jetons de page : `#1` reconnu, `#1` dans `#10` non reconnu, `\#2` non reconnu.
4. Compilation : définition avec un membre par `#N` utilisé ; invocation avec les bonnes natures ; chaque cas de validation de §6, chacun avec sa sévérité.
5. Sélecteur de type de page : un aller-retour de chaque type conserve sa valeur, avec « Fragment » retiré.
6. Persistance : une feuille relue est identique à la feuille écrite.

**Runtime (tests JS)** : substitution dans chacun des trois emplacements de §7 ; JSON re-sérialisé valide ; `textContent` et jamais `innerHTML` ; refus sur écriture vers un non-tag ; refus sur `#N` non lié ; `_isInjection` appliqué aux valeurs substituées ; deux instances successives d'une même page avec deux feuilles distinctes montrent chacune ses valeurs.

**Bout en bout** : une page Fenêtre rapide et deux feuilles exportées, ouvertes dans l'aperçu du Builder, affichent les valeurs de leur feuille. Puis la même épreuve sur l'hôte TF100Web, prouvant l'animation d'un élément.

Chaque test doit échouer avant l'implémentation de ce qu'il couvre ; la preuve est exigée dans chaque rapport de tâche.

## 12. Risques

| Risque | Traitement |
| --- | --- |
| L'hôte TF100Web valide le type des membres et refuse `String` pour un port lu comme booléen. | Détecté par le rejeu de §8 ; le type devient alors une valeur neutre du contrat, à ajouter par une mise à jour de §12 du contrat. |
| Un littéral mal échappé casse une expression ou ouvre une injection. | Substitution sur JSON analysé, échappement des littéraux, `_isInjection` sur les valeurs, tests dédiés. |
| Des textes importés contiennent des `#` sans être des paramètres. | Validation fail-closed et échappement `\#` ; le diagnostic nomme l'élément. |
| Le parcours du DOM à chaque ouverture pèse sur une page lourde. | Parcours unique borné à la racine de l'instance ; mesure sur `Popup_Fan_Condenseur` (91 éléments). |
| La branche TF100Web reste non fusionnée : le workflow n'est pas déployable en production. | Hors périmètre (§10), mais à dire clairement avant toute visite sur site. |

## 13. Séquencement

1. Consigner l'écart de §2 dans `KNOWN_GAPS_V2.md` ; mettre à jour le contrat de paquet ; enregistrer la décision au registre ; passer la spec du 2026-09-24 en *Superseded*.
2. Domaine : `ScadaPageType.QuickWindow`, `ParameterSheet`, analyseur et classement de feuille, reconnaissance des jetons `#N`.
3. Persistance des feuilles.
4. Compilation et validation par couple.
5. Runtime : substitution dans `mountInstance` et ses tests JS.
6. Surfaces : panneau des feuilles, éditeur et dialogue de règle, dialogue « Configuration de la fenêtre rapide », sélecteur de type par valeur, Interface locale en lecture, banc d'essai.
7. Bout en bout dans l'aperçu du Builder.
8. Rejeu de conformance sur `codex/quick-window-v1`.
