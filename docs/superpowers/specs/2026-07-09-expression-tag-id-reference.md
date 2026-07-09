# Expression state - Reference canonique de tag par Id (design)

Date: 2026-07-09
Status: Draft design - contrat TF100Web fixe + normalisation export
Document version: `V2.1.4.0002`
Portee: SCADA Builder V2 - `ElementStateRuleDialog`, `ScadaExpressionValidator`, `Ft100SceneExporter`
Reference: `docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-09 | `V2.1.4.0002` | PENDING | Fixe explicitement le contrat TF100Web obligatoire : les expressions d'etat exportees doivent livrer `tagName = tf100.mapping.<id>` au runtime JS. |
| 2026-07-09 | `V2.1.4.0001` | PENDING | Precision du modele cible `ScadaExprTagRef` avec `TagId` canonique + libelle humain, normalisation export obligatoire, et contrat TF100Web degrade en `qualityFallback` si tag non resolu. |
| 2026-07-09 | `V2.1.4.0000` | PENDING | Design initial : reference par Id canonique dans les expressions d'etat. |

## 1. Probleme

L'UI de creation de regle d'etat (`ElementStateRuleDialog`) permet de selectionner un tag
via un ComboBox qui affiche le `DisplayName` / `KeywordLabel` (ex. `PE_16`,
`Noeud1_N15_04_Commande_MC_120C`). Mais la methode `BuildExpressionFromVariable` ecrit ce
label directement dans l'expression :

```text
{PE_16} == true
{Noeud1_N15_04_Commande_MC_120C} == false
```

Au lieu de persister et d'exporter la reference canonique que le bridge TF100Web sait
resoudre :

```text
{tf100.mapping.161} == true
{tf100.mapping.196} == false
```

### 1.1 Consequence

Sur le runtime TF100Web, le bridge `tf100webScadaBuilder.getTagValue(tagId)` doit recevoir
des IDs canoniques au format `tf100.mapping.<id>`. Les `DisplayName` comme `PE_16` ou les
noms legacy comme `Noeud1_N15_03_Commande_MC_120A` ne sont pas des identifiants runtime
stables et ne doivent pas devenir un contrat TF100Web.

Lorsqu'un tag n'est pas resolu, le comportement attendu est :

1. `getTagValue(...)` retourne `null`.
2. `state-engine.js` detecte `anyNull = true`.
3. L'etat est saute.
4. Si aucun etat n'est evaluable, `qualityFallback` est applique.

Ce comportement degrade est correct. Le probleme est qu'une regle selectionnee depuis le
catalogue devrait transporter son Id canonique afin que le runtime evalue le bon mapping au
lieu de degrader ou de risquer une resolution non deterministe.

### 1.2 Portee

1. Les expressions d'etat sont affectees : elles utilisent aujourd'hui le libelle humain dans
   `{...}`.
2. Les commandes modernes sont moins exposees : `ElementCommandDialog` stocke deja
   `WriteTagId` / `ReadTagId` depuis `tag.Id`. Elles doivent quand meme etre validees par le
   meme resolver pour les anciens fichiers ou futures surfaces.
3. Les scenes existantes (`win00008`, etc.) ont des expressions avec `DisplayName` ou des
   noms legacy. Elles doivent etre normalisees a l'export quand le catalogue permet une
   resolution unique.

## 2. Objectif

Faire en sorte que les expressions d'etat portent une reference canonique (`tf100.mapping.X`)
tout en continuant d'afficher des labels humains dans l'UI.

Objectif non negociable : respecter le contrat TF100Web actuel, sans variante permissive et
sans nouveau format concurrent. TF100Web expose `tf100webScadaBuilder.getTagValue(tagId)` et
resout les valeurs au format `tf100.mapping.<id>` vers `RegisterMapping.id`. Le runtime
SCADA Builder JS actuel lit `ScadaExprTagRef.tagName`. Par consequent, le contrat exporte
obligatoire pour une expression d'etat resolue est :

```json
{
  "type": "tagRef",
  "tagName": "tf100.mapping.196"
}
```

Le HTML/manifest exporte doit rester compatible avec ce runtime actuel :

1. L'AST runtime livre a TF100Web doit exposer une valeur resolvable par `getTagValue()`.
2. Si une reference ne peut pas etre resolue, elle doit rester non resolvable et tomber en
   `qualityFallback`; l'export ne doit jamais inventer ou approximer un mapping.

## 3. Decisions

| # | Decision |
| --- | --- |
| D1 | Le modele cible de reference tag separe le libelle humain et l'identite runtime : `TagName` pour l'affichage, `TagId` pour la resolution canonique. |
| D2 | `BuildExpressionFromVariable` conserve l'affichage humain dans l'UI, mais cree une reference dont `TagId = tag.Id`. |
| D3 | `SelectTagByName` cherche d'abord par `Id`, puis par `DisplayName` et `KeywordLabel` pour retrocompatibilite. |
| D4 | `ScadaExpressionValidator` utilise un resolver commun `Id` / `DisplayName` / `KeywordLabel`, detecte les references inconnues et bloque les ambiguities. |
| D5 | Les expressions existantes avec `DisplayName` / `KeywordLabel` peuvent etre resolues contre le catalogue lorsque le match est unique. |
| D6 | L'export normalise obligatoirement les expressions d'etat avant serialisation HTML/manifest : si `TagId` existe, il est utilise; sinon `TagName` est resolu par `Id`, `DisplayName`, puis `KeywordLabel`. |
| D7 | Contrat fixe : l'AST runtime exporte pour TF100Web utilise la propriete `tagName`, et sa valeur doit etre l'Id canonique (`tf100.mapping.X`) quand la reference est resolue. `tagName = DisplayName` est invalide pour les exports runtime. |
| D8 | Si une reference ne peut pas etre resolue, l'export peut la laisser non resolue uniquement si cela garantit `qualityFallback`; il ne doit jamais choisir un mapping approximatif. Une reference ambigue doit etre une erreur de validation export. |
| D9 | Les commandes et read/write bindings doivent continuer de stocker `ReadTagId` / `WriteTagId` en IDs canoniques, avec validation de migration pour detecter les anciens libelles humains. |

## 3.0 Contrat TF100Web fixe

Cette section est normative pour les exports `.sb2` SCADA Builder V2.

TF100Web attend des references runtime de tag sous forme canonique :

```text
tf100.mapping.<RegisterMapping.id>
```

Pour les expressions d'etat, TF100Web ne parse pas lui-meme le modele domaine. Il charge le
runtime JS fourni dans le `.sb2`; ce runtime lit actuellement `expression.ast.*.tagName`,
puis appelle `window.tf100webScadaBuilder.getTagValue(tagName)`. Donc le payload runtime
obligatoire est :

```json
{
  "type": "tagRef",
  "tagName": "tf100.mapping.196"
}
```

Sont non conformes dans un export runtime :

```json
{ "type": "tagRef", "tagName": "Noeud1_N15_04_Commande_MC_120C" }
{ "type": "tagRef", "tagName": "PE_16" }
{ "type": "tagRef", "tagId": "tf100.mapping.196" }
```

Le dernier exemple peut etre accepte comme metadata supplementaire seulement si `tagName`
reste present et canonique. Le contrat runtime ne doit pas dependre de `tagId` tant que le
runtime JS TF100Web/SCADA Builder n'est pas explicitement migre.

## 3.1 Modele cible

Le modele actuel `ScadaExprTagRef` ne porte qu'une chaine (`TagName`), utilisee a la fois
comme libelle humain et comme identifiant runtime. Le modele cible doit porter deux
informations :

```csharp
public sealed record ScadaExprTagRef(
    string TagName,
    string? TagId = null);
```

Semantique :

1. `TagName` : libelle humain conserve pour affichage/reedition (`DisplayName` ou
   `KeywordLabel` au moment du choix).
2. `TagId` : reference canonique stable (`tf100.mapping.196`) utilisee pour validation,
   export, runtime et comparaison d'identite.

Pour les scenes existantes, `TagId` sera absent. Le resolver commun doit alors tenter de
resoudre `TagName` contre le catalogue. Si la resolution est unique, l'export emet l'Id
canonique. Si elle echoue, TF100Web doit recevoir une reference non resolvable et tomber en
`qualityFallback`, ou l'export doit bloquer selon la politique de validation active.

## 3.2 Contrat export TF100Web

Le runtime JS actuel lit `expression.ast.left.tagName` et appelle
`tf100webScadaBuilder.getTagValue(tagName)`. Pour respecter ce contrat fixe :

1. Le domaine peut stocker `TagId`, mais le payload runtime exporte doit rester lisible par
   le JS existant.
2. Dans l'AST exporte, `tagName` doit contenir l'Id canonique lorsque la reference est
   resolue.
3. `tagId` peut etre present dans le JSON exporte a titre de metadata, mais `tagName` reste
   obligatoire et canonique. Le runtime ne doit pas dependre de `tagId` tant que
   `expression-evaluator.js` n'est pas migre.
4. Les references non resolues ne doivent jamais etre rapprochees approximativement par
   TF100Web. Elles doivent produire `null`, donc `anyNull = true`, donc `qualityFallback`.

## 4. Architecture & fichiers

| Fichier | Action |
| --- | --- |
| `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExprNode.cs` | Modifier - ajouter `TagId` optionnel a `ScadaExprTagRef` sans retirer `TagName`. |
| `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpression.cs` | Modifier - collecter les refs canoniques quand `TagId` est disponible, conserver `TagName` pour affichage. |
| `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs` | Modifier - `BuildExpressionFromVariable` cree une reference avec `TagName` humain + `TagId = tag.Id`; `SelectTagByName` resout par Id puis label. |
| `src/ScadaBuilderV2.Domain/ElementEvents/Expressions/ScadaExpressionValidator.cs` | Modifier - utiliser un resolver commun Id/DisplayName/KeywordLabel et detecter les ambiguities. Pour cette tranche, le resolver peut etre une methode statique interne au validator; extraction dans un `ScadaTagResolver` dedie seulement si une deuxieme surface non-expression en a besoin. |
| `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs` | Modifier - normaliser les `StateConfig` avant serialisation pour emettre un AST runtime avec `tagName = tf100.mapping.X`. La normalisation doit se faire juste avant `JsonSerializer.Serialize(stateConfig, ...)` via une methode du type `NormalizeStateConfigForExport(ScadaElementStateConfig, ScadaTagCatalog?)`. |
| `tests/ScadaBuilderV2.Tests/ElementEvents/ScadaExpressionValidatorTests.cs` | Modifier - couvrir Id canonique, fallback DisplayName/KeywordLabel, inconnu, ambigu. |
| `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs` | Modifier - couvrir l'export d'une regle existante `{DisplayName}` vers `tagName = tf100.mapping.X`, verifier que `tagName = DisplayName` n'est jamais emis pour une reference resolue, et couvrir le fallback non resolu. |
| `tests/runtime-js/state-engine.test.mjs` | Completer - verifier qu'une reference non resolue produit `qualityFallback` et qu'une reference canonique match correctement. |

## 4.1 Matrice de validation

Chaque etape de correction doit avoir une validation dediee avant de passer a l'etape
suivante.

| Etape | Validation obligatoire |
| --- | --- |
| Modele `ScadaExprTagRef(TagName, TagId)` | Test de deserialisation d'un ancien JSON sans `tagId` -> `TagId == null`; test de serialisation nouveau JSON -> `tagName` et `tagId` presents; test parser `{PE_16}` -> `TagName = PE_16`, `TagId = null`. |
| Resolver `Id` / `DisplayName` / `KeywordLabel` | Tests : `tf100.mapping.196` resout par Id; `Noeud1_N15_04_Commande_MC_120C` resout par label; inconnu -> unresolved; doublon label -> ambiguous; priorite Id avant label. |
| `ElementStateRuleDialog` | Tests UI/contrat : le dropdown affiche le label humain; la sauvegarde produit une reference avec `TagId = tf100.mapping.X`; la reedition par `TagId` preselectionne le tag; la reedition legacy par `TagName = DisplayName` preselectionne via fallback. |
| `ScadaExpressionValidator` | Tests : expression avec Id canonique valide; expression legacy avec label valide si resolution unique; expression ambigue invalide; expression inconnue signalee selon politique permissive/strict. |
| `Ft100SceneExporter` | Tests : `TagId` exporte `tagName = tf100.mapping.X`; legacy `{DisplayName}` exporte aussi `tagName = tf100.mapping.X`; un tag resolu ne laisse jamais `tagName = DisplayName`; reference non resolue produit warning + payload qui tombe en `qualityFallback`; ambiguite bloque l'export. |
| Runtime JS | Tests : `tagName = tf100.mapping.196` + valeur false applique l'effet attendu; tag inconnu/null applique `qualityFallback`; condition false avec tag disponible applique `defaultEffect` si ce contrat est confirme; le casing AST exporte correspond au JS (`op`, `left`, `tagName`). |
| Regression `.sb2` | Test zip/manifest/html sur candidat synthetique `win00008` : `MC_120C` resolu vers `tf100.mapping.196`; aucun effet vert attache au rectangle a une seule regle rouge; `data-scada-state-config` HTML et manifest coherents. |

## 4.2 Placement du resolver

Le resolver doit etre partage par le validator et l'export, mais il n'a pas besoin d'etre une
nouvelle abstraction publique dans la premiere tranche. Strategie recommandee :

1. Ajouter une methode statique interne/testee pres du validator, par exemple
   `TryResolveTagReference(string value, ScadaTagCatalog? catalog)`.
2. Resolution exacte, dans cet ordre : `Id`, `DisplayName`, `KeywordLabel`.
3. `0 match` : reference non resolue.
4. `>1 match` : reference ambigue.
5. Extraire plus tard vers `ScadaTagResolver` si les commandes, bindings ou autres surfaces
   doivent consommer la meme logique hors expressions.

## 4.3 Placement de la normalisation export

La normalisation ne doit pas muter le projet en memoire pendant l'export. Elle doit produire
une copie runtime du `ScadaElementStateConfig` :

```text
scene/project model -> StateConfig auteur
export pipeline     -> NormalizeStateConfigForExport(...)
HTML/manifest       -> StateConfig runtime normalise
```

Le `ScadaTagCatalog` doit provenir du projet actif transmis a l'exporteur. Si l'exporteur ne
dispose pas du catalogue dans la methode appelee pour une scene, la signature ou le contexte
d'export doit etre etendu plutot que d'aller relire un fichier d'import.

## 4.4 Point d'attention serialization

Le contrat runtime exige que `tagName` soit present meme si `tagId` existe. Une serialisation
globale avec `DefaultIgnoreCondition`, un changement de record, ou un converter incomplet ne
doit jamais produire un tag ref exporte de cette forme :

```json
{ "type": "tagRef", "tagId": "tf100.mapping.196" }
```

Le `ScadaExpressionConverter` actuel ecrit les proprietes explicitement. L'implementation doit
conserver ce controle : si `tagId` est ajoute, le converter ou la normalisation export doit
garantir que `tagName` reste present et canonique dans le payload runtime.

## 5. Comportement avant/apres

### 5.1 Avant (actuel)

```text
Tag selectionne : PE_16
Id              : tf100.mapping.161
DisplayName     : PE_16

Expression generee : {PE_16} == true
Validation          : checke "PE_16" dans le set { "PE_16", ... } -> OK
Runtime TF100Web    : bridge.getTagValue("PE_16") -> null -> etat saute -> qualityFallback
```

### 5.2 Apres (nouvelle regle)

```text
Tag selectionne : PE_16
Id              : tf100.mapping.161
DisplayName     : PE_16

Expression UI/source : {PE_16} == true
AST domaine          : tagName = "PE_16", tagId = "tf100.mapping.161"
AST export runtime   : tagName = "tf100.mapping.161"
Runtime TF100Web     : bridge.getTagValue("tf100.mapping.161") -> valeur du tag -> etat evalue normalement
```

### 5.3 Scene existante resolue

```text
Expression existante : {Noeud1_N15_04_Commande_MC_120C} == false
Catalogue            : DisplayName/KeywordLabel = Noeud1_N15_04_Commande_MC_120C
                       Id = tf100.mapping.196

Edition : SelectTagByName(...) trouve le tag par fallback label.
Export  : resolver trouve Noeud1_N15_04_Commande_MC_120C -> tf100.mapping.196.
Runtime : AST exporte avec tagName = "tf100.mapping.196".
```

### 5.4 Scene existante non resolue

```text
Expression existante : {NomAbsentDuCatalogue} == true

Validation export : warning ou erreur selon politique active, mais aucun mapping invente
HTML/manifest     : reference non resolvable si export autorise
Runtime TF100Web  : bridge.getTagValue("NomAbsentDuCatalogue") -> null
StateEngine       : anyNull = true -> etat saute -> qualityFallback applique
```

Pour la premiere tranche, la politique recommandee est permissive : exporter la reference non
resolue et emettre un warning de build/export. Cela conserve le comportement degrade attendu
sur les scenes existantes. Le blocage strict peut etre ajoute plus tard lorsque l'UI de
repointage est suffisamment mature.

## 6. Risques

| Risque | Mitigation |
| --- | --- |
| Expressions existantes avec noms legacy comme `Noeud1_N15_...` | Le resolver tente `DisplayName` et `KeywordLabel`. Si le tag existe dans le catalogue (ex. `Noeud1_N15_04_Commande_MC_120C` -> `tf100.mapping.196`), l'export normalise. Sinon le runtime tombe en `qualityFallback` ou l'export bloque selon politique active. |
| Le `DisplayName` peut etre identique a un `Id` | Recherche primaire par `Id`; fallback label seulement apres. |
| Deux tags partagent le meme `DisplayName` / `KeywordLabel` | Erreur de validation export : la resolution est ambigue et ne doit pas choisir arbitrairement. |
| Impact sur le parser (`{...}` avec des points) | Le parser extrait tout entre `{` et `}` comme `TagRef`; `tf100.mapping.161` est deja compatible. |
| Contrat TF100Web brise par ajout de `TagId` | Interdit pour cette tranche. L'export conserve obligatoirement `tagName` comme propriete runtime et y place l'Id canonique. `tagId` reste metadata optionnelle seulement. |
| Divergence PascalCase/camelCase dans les AST exportes | Risque deja observe hors de cette spec. L'export runtime doit emettre le casing que `expression-evaluator.js` consomme reellement, ou le runtime doit accepter les deux formes. Sans cette correction, un tag resolu peut quand meme ne jamais matcher. |
| Confusion `states skipped` vs `states evaluated false` dans `state-engine.js` | Risque orthogonal. La normalisation des tags corrige l'identite du signal, mais le moteur doit aussi distinguer "tag indisponible" (`qualityFallback`) de "aucune condition vraie" (`defaultEffect`) si ce contrat est attendu. |

## 6.1 Risques hors portee immediate

Deux anomalies runtime sont liees au meme theme de contrat implicite et doivent etre suivies
dans une tranche dediee ou dans les tests de fermeture :

1. Casing AST : verifier que l'AST exporte (`op`, `left`, `tagName`, etc.) correspond au
   casing lu par `expression-evaluator.js`. Si le JS attend `Equal` mais l'export emet
   `equal`, l'evaluation echoue independamment de la reference tag.
2. Semantique fallback : verifier que `qualityFallback` est reserve aux tags indisponibles /
   qualite absente. Si tous les etats sont evaluables mais faux, l'effet attendu devrait etre
   `defaultEffect`, pas necessairement `qualityFallback`.

## 7. Non modifie

1. `ScadaExpressionParser` - aucun changement requis pour parser `{...}`.
2. `ScadaTagDefinition` - aucun changement, `Id`, `DisplayName` et `KeywordLabel` existent deja.
3. `Tf100WebTagCatalogImporter` - aucun changement, le mapping `keyword_label` -> `DisplayName` et `id` -> `Id` est correct.
4. `EffectEditorDialog` - aucun changement.
5. Runtime JavaScript SCADA Builder - aucun changement requis dans la premiere tranche, car l'export continue d'emettre `tagName` avec une valeur compatible TF100Web.
