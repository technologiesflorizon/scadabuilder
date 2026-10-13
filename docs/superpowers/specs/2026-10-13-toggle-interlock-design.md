# Interverrouillage des commandes Toggle - Spécification

Date: 2026-10-13
Status: Approved - DEC-0054
Document version: `V2.1.6.0072`
Portée: SCADA Builder V2 (modèle, éditeur, validation, compilateur, runtime partagé embarqué dans le paquet) et TF100Web (liste des capacités runtime supportées)
Dépendances: `docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md` (`DEC-0036`), `docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md` (`DEC-0052`), `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`, `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-10-13 | `V2.1.6.0072` | `PENDING` | `DEC-0054` : interverrouillage optionnel des commandes Toggle (`InterlockTagId`, capacité `command.write.toggle-interlock`). Contrat mis à jour avant l'implémentation. |
| 2026-10-13 | `V2.1.6.0071` | — | Création, après cadrage avec l'utilisateur. |

## 1. Le problème

Sur le site AMR, les fenêtres rapides des condenseurs portent des boutons en mode Toggle sur des paires de commandes qui s'excluent : **Départ / Arrêt** (`BP_HMI_Depart_Manuel_CE_1B` / `BP_HMI_Arret_Manuel_CE_1B`) et **Auto / Man** (`BP_1B_AUTO` / `BP_1B_MAN`). Ce sont des tags distincts, pas un même tag dans deux états. Aujourd'hui, activer l'un ne désactive pas l'autre : les deux peuvent rester à 1 du fait du SCADA.

L'utilisateur demande un champ optionnel **Interverrouillage** sur les commandes Toggle : quand le bouton fait passer son tag d'écriture à vrai, le tag d'interverrouillage est mis à faux.

## 2. Constats vérifiés au 2026-10-13

- `ScadaCommandBinding` (`src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBinding.cs`) porte déjà `WriteTagId`, `ReadTagId` et `WriteMode`. Le toggle lit `ReadTagId ?? WriteTagId`.
- Le toggle est exécuté par `command-dispatcher.js` du runtime partagé, livré **dans le paquet**. Il écrit via `ScadaRuntime.TagBridge.writeTag`, que TF100Web implémente déjà (`POST /visualisation/mapping/write/`). Une écriture supplémentaire n'exige donc aucun nouveau service de l'hôte.
- Le dispatcher bloque une seconde exécution de la même commande tant que la première est en cours (`_pendingCommands`, diagnostic `write-in-flight`).
- `quick-window-runtime.js` (`substituteInstance`) remplace les `#N` de `writeTagId` et `readTagId` dans `data-scada-command-config` à l'ouverture d'une instance, et refuse l'instance si un `#N` n'est pas lié.
- Chaque mode d'écriture est une capacité runtime déclarée (`ScadaRuntimeCapabilityCatalog.WriteModes`, par exemple `command.write.toggle`). TF100Web refuse un paquet dont `RuntimeContract.RequiredCapabilities` contient une capacité absente de `SUPPORTED_SCADA_RUNTIME_CAPABILITIES` (`frontend/scada_package.py`).
- TF100Web ne valide pas côté serveur les champs individuels des commandes.

## 3. Décisions de cadrage (prises avec l'utilisateur le 2026-10-13)

1. **Déclencheur : le clic HMI seulement.** L'interverrouillage n'agit que lorsque *ce* bouton écrit 1. Si le tag d'écriture passe à 1 par une autre source (automate, Sysmac, autre poste), le SCADA ne fait rien : l'automate reste maître de sa logique.
2. **Ordre : interverrouillage d'abord.** Écrire 0 sur le tag d'interverrouillage, puis, seulement si c'est accepté, écrire 1 sur le tag d'écriture. Si la première écriture échoue, la seconde n'est pas faite.
3. **Périmètre : interverrouillage seul.** La lecture fraîche avant toggle et la relecture de confirmation, demandées par ailleurs, sont un chantier distinct. Le toggle continue de calculer sa cible à partir de la valeur en cache.
4. **Approche A : un champ sur la commande.** Rejetées : une liste générique d'écritures associées (sur-dimensionnée pour un seul tag) et une seconde commande SetFixed sur le même bouton (elle partirait à chaque clic, sans ordre ni abandon).
5. **Une capacité runtime dédiée est déclarée**, plutôt qu'un comportement non déclaré, par cohérence avec les autres modes d'écriture et pour qu'un hôte qui ne l'a pas validée refuse explicitement le paquet.

## 4. Modèle

`ScadaCommandBinding` gagne un paramètre optionnel, placé en dernier pour ne casser aucun appel positionnel :

```csharp
string? InterlockTagId = null
```

Absent, il vaut `null` et le comportement est inchangé. Les projets existants se chargent sans migration.

Un seul tag par commande. La réciprocité (Départ remet Arrêt à 0 *et* Arrêt remet Départ à 0) s'obtient en configurant les deux boutons.

## 5. Éditeur

Dans `ElementCommandDialog`, une liste déroulante **« Interverrouillage (optionnel) »** est ajoutée sous « Tag lu », alimentée par la même liste de tags (y compris les `#N` dans une page de type Fenêtre rapide). Elle n'est active que si le mode d'écriture est **Toggle**. Changer de mode la vide.

## 6. Validation

`ScadaCommandBindingValidator` ajoute, quand `InterlockTagId` est renseigné :

| Condition | Résultat |
| --- | --- |
| `WriteMode` différent de `Toggle` | erreur : interverrouillage réservé au mode Toggle |
| tag absent du catalogue | erreur, même règle que `WriteTagId` |
| tag non accessible en écriture | erreur |
| tag non booléen | erreur |
| égal à `WriteTagId` ou à `ReadTagId` | erreur : la commande s'annulerait elle-même |

Pour un `#N`, les contrôles de catalogue, d'écriture et de type ne s'appliquent pas (règle existante des membres `#N`, `DEC-0052`). Le contrôle d'égalité s'applique au texte.

## 7. Fenêtres rapides

`QuickWindowPlaceholderUsageCollector` ajoute le site `PlaceholderUsageSite.CommandInterlockTag`, traité comme un **site d'écriture** partout où `CommandWriteTag` l'est (`ParameterSheetLowering`, messages de diagnostic). Un `#N` utilisé en interverrouillage doit donc être lié à un tag, comme un `#N` d'écriture.

Dans `quick-window-runtime.js`, `substituteInstance` remplace un `interlockTagId` de forme `#N` par le tag lié (site `write`), et refuse l'instance si le `#N` n'est pas lié.

## 8. Export et contrat runtime

- `Ft100SceneExporter.BuildRuntimeCommandConfig` émet `InterlockTagId` (sérialisé `interlockTagId`) **seulement s'il est renseigné**. Un projet sans interverrouillage produit un manifeste et un DOM identiques à aujourd'hui.
- Nouvelle capacité **`command.write.toggle-interlock`**, propriétaire `SharedRuntime`. L'analyseur de capacités l'ajoute à `RequiredCapabilities` dès qu'au moins une commande Toggle porte un `InterlockTagId`.
- Pas de changement de `ManifestVersion` ni de version de contrat runtime.
- TF100Web ajoute `command.write.toggle-interlock` à `SUPPORTED_SCADA_RUNTIME_CAPABILITIES`.
- La capacité n'est inscrite comme supportée dans le catalogue qu'avec les trois preuves exigées par la gouvernance : test Builder, test runtime JS, test TF100Web. La matrice `docs/10_generated/` est régénérée (`tools/docs/generate-runtime-capability-matrix.ps1`).
- `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md` et `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md` sont mis à jour **avant** l'implémentation (champ `interlockTagId`, capacité, séquence d'écriture), comme l'exige toute modification du contrat de paquet.

## 9. Comportement runtime

Dans `command-dispatcher.js`, cas `toggle` :

1. La cible est calculée comme aujourd'hui à partir de `getTagValue(readTagId || writeTagId)`. Valeur absente : `toggle-read-missing`, rien n'est écrit.
2. Cible `'0'`, ou pas d'`interlockTagId` : comportement actuel, une seule écriture.
3. Cible `'1'` avec `interlockTagId` :
   1. écrire `'0'` sur `interlockTagId` (métadonnées `mode: 'ToggleInterlock'`) ;
   2. si le résultat est un succès (promesse résolue à une valeur autre que `false`, ou retour synchrone autre que `false`), écrire `'1'` sur `writeTagId` (métadonnées `mode: 'Toggle'`) ;
   3. sinon, diagnostic `interlock-write-failed`, le `'1'` n'est **pas** écrit, la commande renvoie un échec.
4. Toute la séquence est une seule commande en cours : un nouveau clic pendant la séquence donne `write-in-flight` et n'écrit rien.

Chaque écriture passe par `TagBridge.writeTag` et produit donc côté TF100Web son propre `POST` et son propre événement `scada-tag-write-result`.

## 10. Hors périmètre

- Réagir à un passage à 1 du tag d'écriture d'une autre source que le clic.
- Lecture fraîche avant toggle et relecture de confirmation.
- Interverrouillage pour les modes Momentary, SetFixed et SetFromInput.
- Plusieurs tags d'interverrouillage par commande.

## 11. Tests

Écrits avant le code.

**Builder, C#** (`tests/ScadaBuilderV2.Tests`)
- validateur : chacune des cinq erreurs du §6, un cas valide sans erreur, un `#N` dispensé des contrôles de catalogue ;
- collecteur d'usages : un `#N` en interverrouillage produit un usage `CommandInterlockTag` traité comme écriture ;
- export : `interlockTagId` émis seulement s'il est renseigné ; capacité exigée seulement si utilisée ; un projet sans interverrouillage produit un manifeste inchangé.

**Builder, JS** (`tests/runtime-js`)
- cible 1 : l'écriture `0` sur l'interverrouillage précède l'écriture `1` ;
- interverrouillage refusé (`false`) ou en échec (promesse rejetée) : `1` jamais écrit, diagnostic `interlock-write-failed` ;
- cible 0 : aucune écriture sur l'interverrouillage ;
- clic pendant la séquence : `write-in-flight`, aucune écriture ;
- substitution : `#N` remplacé par le tag lié ; `#N` non lié refuse l'instance.

**TF100Web**
- `scada_package` accepte un paquet exigeant `command.write.toggle-interlock` ;
- la suite de conformité runtime couvre la capacité.

**Vérification finale** : suites complètes des deux dépôts ; rendu Edge du popup condenseur avec Départ et Arrêt interverrouillés, clic simulé, contrôle de l'ordre et du contenu des écritures envoyées au pont, sans automate ; essai sur site avec l'accord de l'utilisateur.

## 12. Branches et déploiement

- Builder : `feature/toggle-interlock`, partie de `feature/quick-window-front-door` (branche du `.sb2` déployé sur le site AMR, non fusionnée dans `master`).
- TF100Web : `feature/toggle-interlock-capability`, partie de `main` (`7543cd3`).
- Décision : une entrée `DEC-xxxx` est ouverte dans `docs/00_governance/DECISION_REGISTER_V2.md` à l'approbation de cette spec ; `VERSION` et les tables de changements des documents propriétaires sont mis à jour avec chaque changement livré.
- Ordre de déploiement : TF100Web d'abord, puis le `.sb2` régénéré, dans lequel l'interverrouillage est configuré sur Départ/Arrêt et Auto/Man. Un `.sb2` qui n'utilise pas l'interverrouillage reste accepté par un TF100Web antérieur.
