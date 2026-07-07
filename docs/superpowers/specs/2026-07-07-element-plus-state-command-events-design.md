# Element+ — Événements d'affichage d'état & de commande (design)

Date: 2026-07-07
Status: Approved design — prêt pour planification d'implémentation
Portée: SCADA Builder V2 (implémentation complète) + contrat runtime pour TF100Web (spec seulement)

## 1. Problème

Le système d'events actuel des Element+ n'est pas aligné avec les besoins d'un SCADA
industriel. Trois manques structurels (voir audit et `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`) :

1. **Aucune action ne pilote une couleur/apparence arbitraire.** Seules des classes CSS
   figées prédéfinies existent (`SetClass`/`RemoveClass`/`ToggleClass`) — pas de
   `background-color`/`border`/`opacity` libres.
2. **Les conditions ne sont câblées que sur les 3 actions de visibilité**
   (`Show`/`Hide`/`ToggleVisibility`), pas sur le style.
3. **Tout est déclenché par la souris** (clic/hover). Il n'existe aucun mécanisme
   d'automatisation continu « la valeur du tag change → l'apparence change ».

Conséquence : impossible de faire un box vert si `tag >= x`, rouge selon une autre
condition, semi-transparent si aucune donnée, apparence de repos sinon.

Ce design **remplace** l'ancien modèle (aucun projet en production → pas de migration).

## 2. Objectif

Découpler deux familles d'événements, chacune dans son propre onglet du panneau
Propriétés d'un Element+ :

- **Événement d'affichage d'état** — automatisation. Piloté en continu par la valeur des
  tags. Change l'apparence. Ne réagit jamais à la souris.
- **Événement de commande** — interaction. Piloté par l'opérateur (souris). Écrit vers
  les tags ou navigue. Ne change jamais l'apparence directement.

Principe directeur : **État = ce que l'élément *montre* (imposé par l'automate).
Commande = ce que l'élément *fait* (déclenché par l'opérateur).**

## 3. Décisions verrouillées (résumé)

| # | Décision |
|---|---|
| D1 | Deux onglets : « Événement d'affichage d'état » + « Événement de commande ». L'ancien onglet Événements est scindé/supprimé, rien d'orphelin. |
| D2 | Résolution d'état : **first-match-wins** sur une liste ordonnée (ordre = priorité, haut gagne). |
| D3 | Deux replis éditables : **Repos** (aucun match) et **Qualité** (aucun état évaluable). Défaut Qualité = semi-transparent + bordure noire. Aucune couleur imposée pour Repos. |
| D4 | Qualité **par-état** : un état dont un tag référencé est `null` est **sauté**. Si tous les états sont non évaluables → repli Qualité. |
| D5 | Erreur d'évaluation (div/0, etc.) → la condition vaut **false**, non bloquant, lève un **flag d'erreur** (badge + `TextContent` piloté = `---`). |
| D6 | Conditions = **expressions libres**, mini-parser maison → **AST sérialisé** (source de vérité runtime). |
| D7 | Effets **cumulables** de natures différentes, **une seule animation** par état. |
| D8 | Commandes : 4 modes d'écriture + navigation/popup/URL/retour + confirmation optionnelle. |
| D9 | Namespace neuf : `ScadaBuilderV2.Domain.ElementEvents`. |
| D10 | UI hybride : liste réordonnable au panneau + **fenêtre** d'édition d'item ; **aperçu statique** (pas d'évaluateur C#) ; **bouton Test** toggle (1 actif, reset à la fermeture de page). |
| D11 | Scope itération : implémentation **complète Builder V2** (modèle + UI + parser/validateur). **TF100Web** = contrat seulement. **Simulateur live** = itération future. |

## 4. Sémantique d'évaluation (runtime)

Ordre de résolution d'un Element+ à chaque cycle d'évaluation runtime :

```
1. LISTE  → parcourir States (haut → bas) :
     • si un tag référencé par CET état est null (mauvaise qualité)
         → SKIP cet état, passer au suivant.
     • sinon évaluer Expression :
         - true             → appliquer son EffectBlock. STOP. (first-match-wins)
         - false            → continuer.
         - ERREUR (div/0…)  → traité comme false + lever le flag d'erreur, continuer.

2. TOUS NON ÉVALUABLES → si aucun état n'a pu être évalué (chacun a >= 1 tag null)
     → appliquer QualityFallback (défaut : semi-transparent + bordure noire). STOP.

3. DÉFAUT → des états étaient évaluables mais aucun n'a matché
     → appliquer DefaultEffect (état de repos). STOP.

4. FLAG ERREUR (transversal, non bloquant) :
     • si une expression a erré pendant le parcours
         → badge d'erreur discret sur l'élément
         → tout TextContent piloté par un effet appliqué devient "---".
     • n'empêche pas l'application du match/repli.
```

Notes :
- « Mauvaise qualité » = valeur `null` (aligné sur le comportement runtime actuel).
  Le stale-timeout (valeur non rafraîchie depuis un délai) est une **extension future**.
- La qualité est évaluée **par état** au moment où on l'examine, pas globalement en tête.
- Application d'un `EffectBlock` : chaque propriété non-nulle écrase l'apparence de
  conception ; une propriété nulle laisse la valeur de conception inchangée.
- `Rotation` (statique) et `Animation` (dont `Spin`) **composent une seule chaîne
  `transform`** au rendu (elles ne s'écrasent pas).

## 5. Modèle de données

Namespace : `ScadaBuilderV2.Domain.ElementEvents`. `Domain` n'a aucune référence de
projet ; `Scenes`/`Projects` référencent `ElementEvents`, jamais l'inverse. Un Element+
porte `ScadaElementStateConfig?` + `ScadaElementCommandConfig?` (les deux optionnels et
indépendants).

Organisation des fichiers :

```
src/ScadaBuilderV2.Domain/ElementEvents/
├─ State/
│   ├─ ScadaElementStateConfig.cs
│   ├─ ScadaStateRule.cs
│   ├─ ScadaEffectBlock.cs           (+ ScadaAnimation enum)
├─ Command/
│   ├─ ScadaElementCommandConfig.cs
│   ├─ ScadaCommandBinding.cs        (+ ScadaCommandKind / ScadaWriteMode / ScadaCommandTrigger enums)
│   └─ ScadaConfirmation.cs
├─ Expressions/
│   ├─ ScadaExpression.cs
│   ├─ ScadaExprNode.cs              (AST : Literal / TagRef / Unary / Binary / Func)
│   ├─ ScadaExpressionParser.cs      (texte → AST)
│   └─ ScadaExpressionValidator.cs   (tags existent, résultat booléen, /0 littéral, arité)
└─ ScadaElementEventRegistry.cs      (remplace ScadaEventRegistry : triggers, kinds, effets, fonctions)
```

### 5.1 Événement d'affichage d'état

```
ScadaElementStateConfig
├─ QualityFallback : ScadaEffectBlock      // repli « aucun état évaluable » (D4). Défaut: opacité 0.4 + bordure noire.
├─ DefaultEffect   : ScadaEffectBlock      // repli « aucun match » (état de repos, D3). Aucun défaut de couleur imposé.
└─ States          : List<ScadaStateRule>  // ORDONNÉE = priorité (first-match-wins)

ScadaStateRule
├─ Id          : Guid
├─ Name        : string                    // libellé utilisateur (ex: « Alarme haute »)
├─ Enabled     : bool                       // désactivable sans suppression
├─ Expression  : ScadaExpression           // doit s'évaluer en booléen
└─ Effect      : ScadaEffectBlock

ScadaEffectBlock   // toutes les propriétés OPTIONNELLES (null = ne touche pas) → cumulables
├─ BackgroundColor : string?               // hex
├─ BorderColor     : string?
├─ BorderWidth     : double?               // px
├─ TextColor       : string?
├─ TextContent     : string?               // statique OU interpolé « Débit: {Flow} »
├─ TextVisible     : bool?                  // show/hide du texte
├─ ElementVisible  : bool?                  // show/hide de l'élément (remplace ex-Show/Hide)
├─ Opacity         : double?               // 0.0 – 1.0
├─ Rotation        : double?               // degrés (statique)
└─ Animation       : ScadaAnimation?       // None/Blink/Pulse/Halo/Spin — une seule à la fois

enum ScadaAnimation { None, Blink, Pulse, Halo, Spin }   // extensible en révision future
```

### 5.2 Événement de commande

```
ScadaElementCommandConfig
└─ Commands : List<ScadaCommandBinding>     // indépendantes (pas de priorité), chacune sur son trigger

ScadaCommandBinding
├─ Id            : Guid
├─ Name          : string
├─ Enabled       : bool
├─ Trigger       : ScadaCommandTrigger      // OnClick / OnRelease / OnHover / OnHoverEnter / OnHoverExit
├─ Kind          : ScadaCommandKind         // WriteTag | Navigate | OpenPopup | TogglePopup | ClosePopup | OpenUrl | Back
├─ Confirmation  : ScadaConfirmation?       // null = pas de confirmation
└─ Payload       : (selon Kind, voir ci-dessous)

Payload par Kind :
• WriteTag      → WriteTagId (obligatoire),
                  ReadTagId (optionnel ; défaut = WriteTagId),
                  WriteMode (ScadaWriteMode),
                  OnValue / OffValue        (Momentary : press=On, release=Off),
                  FixedValue                (SetFixed)
                  // SetFromInput : la valeur vient de la saisie runtime opérateur.
• Navigate      → TargetPageId
• OpenPopup /
  TogglePopup /
  ClosePopup    → TargetPageId (Fragment) + PopupOptions (réutilise ScadaPopupOptions existant)
• OpenUrl       → Url, NewTab : bool
• Back          → (aucun argument)

enum ScadaCommandKind    { WriteTag, Navigate, OpenPopup, TogglePopup, ClosePopup, OpenUrl, Back }
enum ScadaWriteMode      { Momentary, Toggle, SetFixed, SetFromInput }
enum ScadaCommandTrigger { OnClick, OnRelease, OnHover, OnHoverEnter, OnHoverExit }

ScadaConfirmation
└─ Message : string        // message custom affiché avant l'écriture (« Démarrer la pompe ? »)
```

Règle Toggle : écrit l'inverse de la valeur lue via `ReadTagId` (défaut = `WriteTagId`).

### 5.3 Grammaire d'expression

Source texte saisie par l'utilisateur → AST (`ScadaExprNode`). L'**AST sérialisé** est la
source de vérité pour le runtime, pas la chaîne texte.

Grammaire (priorité des opérateurs standard) :

- **Littéraux** : nombre (`80`, `1.8`), booléen (`true`/`false`), chaîne (`"..."`).
- **Référence tag** : `{NomDuTag}` — résolue contre le catalogue de tags du projet.
- **Unaire** : `!` (non logique), `-` (négation).
- **Binaire** : `+ - * / %`, comparaisons `== != < <= > >=`, logiques `&& ||`.
- **Fonctions** (registre extensible, arité vérifiée) :
  - `ABS(x)`
  - `MIN(a, b)`
  - `MAX(a, b)`
  - `BIT(tag, n)` — bit `n` d'une valeur entière (accès bit natif, sans masquage manuel).

`ScadaExprNode` (hiérarchie sérialisable) : `Literal` (number/bool/string), `TagRef`,
`Unary(op, operand)`, `Binary(op, left, right)`, `Func(name, args[])`.

Validation à l'authoring (`ScadaExpressionValidator`) :
- Syntaxe correcte.
- Tous les `{tags}` existent dans le catalogue.
- Le résultat racine est **booléen** (une condition d'état doit l'être).
- Arité correcte des fonctions.
- Division par zéro **littérale** détectée (`x / 0`) → erreur d'authoring. La division par
  un tag ne peut pas être détectée statiquement → gérée au runtime (D5).

## 6. UI (panneau Propriétés Element+)

Approche **hybride** pour les deux onglets : la **liste vit dans le panneau**
(réorganisation = action fréquente), l'**édition d'un item ouvre une fenêtre** dédiée
(configuration détaillée = action ponctuelle). Réutilise le pattern `ElementEventDialog`
existant.

### 6.1 Onglet « Événement d'affichage d'état »

- Deux boutons de repli en haut : **Repos** et **Qualité/pas de données** → ouvrent la
  fenêtre d'édition d'effets (sans champ condition).
- **Liste ordonnée d'états** (l'ordre EST la priorité) :
  - drag-handle + flèches ▲▼ pour réordonner ;
  - case ☑ activer/désactiver ;
  - ✎ ouvre la fenêtre d'édition ; 🗑 supprime ;
  - **bouton Test** (▶) — voir §6.3 ;
  - `[+ Ajouter]` ouvre la fenêtre pour un nouvel état.
- **Fenêtre « Éditeur d'état »** (ouverte par + ou ✎) :
  - Nom.
  - Condition : champ expression + **validation live** (coche verte / message d'erreur) +
    auto-complétion des `{tags}` depuis le catalogue.
  - Éditeur d'effets : chaque effet est une ligne activable ; color pickers avec
    **raccourcis sémantiques** suggérés (vert/rouge/jaune/gris) + hex libre.
  - **Aperçu statique** (grand) : rend l'élément avec les effets de l'état édité, **sans
    évaluer l'expression**.

### 6.2 Onglet « Événement de commande »

- Liste de commandes (pas d'ordre de priorité — indépendantes).
- **Fenêtre « Éditeur de commande »** contextuelle selon `Kind` :
  - Nom, Déclencheur (trigger).
  - Si `WriteTag` : Tag écriture (obligatoire), Tag lecture (défaut = écriture), Mode
    (Momentané/Toggle/Set fixe/Set depuis saisie), valeurs selon le mode.
  - Si Navigation/Popup/URL/Retour : cible correspondante.
  - Confirmation : case + message custom.

### 6.3 Bouton Test (par état)

- **Toggle on/off** : force les effets de l'état sur l'élément du canvas ; re-clic → off.
- **Un seul test actif à la fois** (activer un autre Test désactive le précédent).
- **Aperçu statique pur** : applique l'`EffectBlock`, **n'évalue jamais** l'expression.
- **Auto-reset à la fermeture de la page/scène** → retour au `DefaultEffect`. Aucun état
  de test n'est persisté ni exporté.
- Indicateur visuel discret (item surligné + pastille « TEST » sur l'élément).

## 7. Migration de l'ancien onglet Événements

Tri des 13 actions / 5 triggers actuels (aucun orphelin) :

| Ancien | Destination | Sort |
|---|---|---|
| `OnClick`, `OnRelease`, `OnHover`, `OnHoverEnter`, `OnHoverExit` | Commande | conservés (`ScadaCommandTrigger`) |
| `Navigate` | Commande | `ScadaCommandKind.Navigate` |
| `MountFragment` / `ClosePopup` / `TogglePopup` | Commande | `OpenPopup` / `ClosePopup` / `TogglePopup` |
| `WriteValue` | Commande | absorbé par `WriteTag` + 4 modes |
| `WriteTag` (legacy) | — | **supprimé** |
| `ReadValue` | État | absorbé (lecture = moteur d'état, interpolation `{tag}`) |
| `Show` / `Hide` / `ToggleVisibility` | État | deviennent l'effet `ElementVisible` |
| `SetClass` / `RemoveClass` / `ToggleClass` | — | **supprimés**, remplacés par `ScadaEffectBlock` |
| *(nouveau)* Ouvrir URL externe, Retour | Commande | `OpenUrl`, `Back` |

`ScadaEventRegistry` (dans `Scenes/`) est **déprécié puis retiré** une fois
`ScadaElementEventRegistry` en place.

## 8. Livrables

1. **Cette spec** — conception complète pour l'implémentation Builder V2.
2. **Contrat runtime** `docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md` —
   format sérialisé état+commande, grammaire formelle + AST JSON, sémantique d'évaluation
   (§4) que TF100Web devra respecter. Rédigé dans cette itération, **implémenté plus tard**
   par un brainstorm côté TF100Web (`F:\Projet\Git\TF100Web`).
3. **Note de dépréciation** ajoutée à `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`
   (remplacé par le nouveau modèle).

## 9. Hors scope (itérations futures)

- **Simulateur live** dans le Builder : saisir des valeurs de tag, évaluer réellement les
  expressions (nécessite l'évaluateur C#), afficher MATCH/NO-MATCH et le first-match en
  direct. À documenter comme itération ultérieure.
- **Implémentation runtime TF100Web** (consomme le contrat du §8.2).
- **Stale-timeout** de qualité (au-delà de `null`).
- **Extensions de grammaire** : fonctions supplémentaires (`ROUND/SQRT/POW`…), cumul de
  plusieurs animations.
- **Set depuis saisie** avancé (widgets de consigne dédiés).

## 10. Contraintes projet à respecter (rappel)

- Les artefacts d'éditeur (aperçu, Test, surlignage) **ne doivent jamais fuiter** dans la
  géométrie exportée `.sb2`/`.sep`.
- Preview / build / export consomment **un seul** modèle de projet.
- Séparer « ce que l'exporteur émet » de « ce que TF100Web exécute » — ne pas documenter
  un gap comme implémenté (`docs/08_implementation_status/KNOWN_GAPS_V2.md`).
- APIs publiques : XML docs ; code sensible au contrat cite `Decisions:`/`Contracts:`/`Tests:`.
