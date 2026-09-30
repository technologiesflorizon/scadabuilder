# Conversion d'une page Fragment en fenêtre rapide - Spécification

Date: 2026-09-24
Status: Superseded by docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md (DEC-0052)
Document version: `V2.1.6.0034`
Portée: SCADA Builder V2, éditeur et modèle de projet. Aucune capacité runtime, aucun contrat de package modifié.
Dépendances: `docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md` (`DEC-0050`), `docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md`, `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md`, `docs/06_ui_ux/UI_ARCHITECTURE_V2.md`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-09-30 | `V2.1.6.0034` | `PENDING` | Statut passé à *Superseded* par `docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md` (`DEC-0052`) : la conversion vers une définition séparée que cette spec décrivait est rendue inutile par le modèle de feuilles de paramètres. |
| 2026-09-24 | `V2.1.6.0032` | `8c8e544` | Création. Ferme la lacune laissée par `DEC-0050` : `ScadaPageType.Fragment` et `QuickWindowDefinition` coexistaient sans passerelle, de sorte qu'aucun popup legacy ne pouvait devenir une fenêtre rapide et qu'une page Fragment n'avait ni interface locale ni banc d'essai. |

## 1. Le problème

`DEC-0050` a introduit `QuickWindowDefinition` comme entité typée et a retiré les kinds de commande popup. Il n'a jamais retiré `ScadaPageType.Fragment`, ni fourni de chemin de l'un vers l'autre. Le dépôt porte donc deux mécanismes parallèles :

- une page `Fragment` est une `ScadaScene` ordinaire. Elle n'a pas d'interface locale, pas de banc d'essai, pas de ports, et rien ne peut l'ouvrir depuis `DEC-0050` puisque les kinds popup ont disparu ;
- une `QuickWindowDefinition` a tout cela, mais naît vide et ne peut pas reprendre le contenu d'une page existante.

Constats vérifiés dans le code au 2026-09-24 :

- `VisualContent.FromScene(ScadaScene)` existe dans `src/ScadaBuilderV2.Domain/QuickWindows/QuickWindowModels.cs:45` et **n'a aucun appelant dans tout le dépôt**. C'est la matière première de la conversion, laissée inutilisée.
- Aucune commande de conversion n'existe, sous aucun nom.
- Le panneau « Interface locale » (`QuickWindowInterfaceAnchorable`) n'est monté que par `ActivateQuickWindowAsync` (`MainWindow.QuickWindows.cs:206`), qui exige un `_hostedQuickWindowDefinition`. Une page Fragment ne passe jamais par ce chemin : l'absence de table de variables locales sur un Fragment est structurelle, pas un défaut d'affichage.
- `QuickWindowDefinition.CreateEmpty` (`QuickWindowModels.cs:223`) produit un canvas vierge et zéro membre d'interface.

Effet pour l'utilisateur : il a converti `Popup_Fan_Condenseur` en Fragment, n'a obtenu ni interface locale ni moyen de lier la définition à la page, et le workflow n'a jamais pu être validé de bout en bout.

## 2. Ce que les données du projet imposent

Mesuré sur `projects/TEST_CONVERSION` (57 scènes) :

- **Aucune** occurrence de `MountFragment`, `ClosePopup` ni `TogglePopup`. La page Fragment `win00034` (`Popup_Fan_Condenseur`) n'a donc **aucun appelant** : la conversion n'a aucune référence à réécrire.
- `win00034` porte **91 éléments, tous de `Kind: LegacyStatic`**, et **zéro liaison** : `StateConfig`, `CommandConfig`, `ReadTagId`, `WriteTagId`, `TagBinding` sont nuls sur les 91, et `Actions` de la scène est vide.

Deux conséquences qui orientent la spec :

1. La promotion automatique des tags en ports produira **zéro port** pour cette page. La table de variables locales apparaîtra vide et devra être remplie à la main. C'est correct et attendu ; ce n'est pas un échec de conversion.
2. La conversion doit donc rester valide et utile pour une page sans aucune liaison. Une conversion qui exigerait au moins un tag serait inutilisable sur le cas réel qui motive ce travail.

`QuickWindowValidation.ValidateVisualContent` (`QuickWindowValidation.cs:98`) ne restreint pas les types d'éléments : elle ne valide que `CanvasSize` et les listes de références. Les 91 éléments `LegacyStatic` sont donc acceptés sans adaptation.

## 3. Décisions

| # | Décision | Alternative écartée |
| --- | --- | --- |
| D1 | `ScadaPageType.Fragment` est **retiré du sélecteur de type de page**. Le chargeur continue de lire la valeur pour que les projets existants s'ouvrent, mais on ne peut plus en créer. | Migration automatique au chargement : modifie les données de l'utilisateur sans demande explicite, et un projet relu par une version antérieure ne se comporte plus pareil. Coexistence durable : c'est l'état actuel, celui qui a produit le problème. |
| D2 | La conversion **promeut automatiquement en ports publics** chaque tag distinct référencé par les éléments, sans question posée. | Dialogue de sélection : plus fin, mais une étape de plus; l'utilisateur a tranché pour l'automatique, les ports en trop se suppriment ensuite dans le panneau « Interface locale », qui porte déjà Ajouter / Éditer / Supprimer. |
| D3 | La commande vit **sur la page** (menu contextuel et ruban) et **retire la page** au profit de la définition, en **une seule transition annulable**. | Conserver la page : laisse deux copies du même écran qui divergent. Importer dans une définition existante : deux étapes, et rien ne retire la page d'origine. |

## 4. Le modèle de conversion

Une commande applicative unique, `ConvertPageToQuickWindow(pageKey)`, produisant une `QuickWindowWorkspaceMutation` comme les autres opérations de `DEC-0050`, donc annulable par l'historique partagé pages/fenêtres rapides.

Sa transition, dans cet ordre :

1. **Refuser tôt** si la page est introuvable, si elle est `Header`, `Footer` ou la page d'accueil, ou si un appelant subsiste. Le refus porte le motif ; il n'est jamais silencieux.
2. **Composer le contenu** avec `VisualContent.FromScene(page)`. La `CanvasSize` de la définition est celle de la page.
3. **Dériver les ports** (section 5). Zéro tag référencé donne zéro port, ce qui est valide.
4. **Réécrire les éléments** pour viser les ports dérivés au lieu des tags du projet.
5. **Créer la définition** : `Code` dérivé du titre de la page par `PageCodePolicy`, `DisplayName` égal au titre, `InterfaceVersion` à 1, défauts de présentation de `CreateEmpty`.
6. **Retirer la page** du projet et de ses index de navigation.
7. **Valider l'état d'arrivée** avec `QuickWindowValidation.ValidateDefinition`. Toute anomalie bloque la mutation entière : rien n'est appliqué à moitié.

Aucune `QuickWindowInvocation` n'est créée. Il n'y a aucun appelant à préserver (section 2), et une invocation n'existe que rattachée à une commande appelante. La liaison des ports se fait quand l'utilisateur pose une commande « Ouvrir une fenêtre rapide » sur un élément, surface déjà livrée.

## 5. Dérivation des ports

Pour chaque identifiant de tag distinct trouvé dans les éléments de la page, un `QuickWindowInterfaceMember` :

| Champ | Règle |
| --- | --- |
| `MemberKey` | Nouveau GUID. |
| `Name` | **L'identifiant du tag, tel quel.** `QuickWindowValidation` n'impose aucun format de nom de membre : seulement non vide et unique sans égard à la casse (`QuickWindowValidation.cs:159-161`). Reprendre l'identifiant garde la provenance lisible sans invention de règle. Deux tags ne différant que par la casse produiraient une collision : dans ce cas seulement, suffixer `_2`, `_3`… |
| `Family` | `WriteCommand` si le tag est écrit par au moins un élément ; sinon `ReadState`. Un tag à la fois lu et écrit donne `WriteCommand`. |
| `DataType` | Reprise de la définition du tag dans le catalogue du projet. Tag absent du catalogue : `String`, et un diagnostic non bloquant le signale. |
| `Access` | `Write` pour `WriteCommand`, `Read` pour `ReadState`. |
| `Required` | `false`. Un port requis non lié bloquerait build et export dès la conversion, avant que l'utilisateur ait pu lier quoi que ce soit. |
| `DefaultValue` | Nul. |
| `Description` | Nul dans le cas normal, puisque `Name` porte déjà l'identifiant. Renseigné avec l'identifiant d'origine dans le seul cas d'une collision de casse, où `Name` a dû être suffixé. |

`ReadState` et `WriteCommand` sont tous deux publics au sens de `QuickWindowInterfaceMember.IsPublic` (`QuickWindowModels.cs:175`), donc liables par une invocation.

## 6. Le sort du type Fragment

- Retiré du sélecteur de type de page dans l'éditeur.
- Conservé dans l'enum `ScadaPageType` et accepté à la lecture : aucun projet existant ne devient illisible.
- Une page encore en `Fragment` produit un **diagnostic non bloquant** proposant la conversion. Elle n'empêche ni build ni export : la migration reste au choix de l'utilisateur, comme D1 l'exige.
- Aucune valeur d'enum n'est renumérotée. Le format persisté ne change pas, donc `FormatVersion` n'évolue pas.

## 7. Surfaces UI

- **Menu contextuel de la page** et **ruban** : « Convertir en fenêtre rapide », active seulement sur une page convertible.
- Après succès, la fenêtre rapide créée est **activée** : le canvas la montre, « Interface locale » et « Banc d'essai » s'ouvrent, par le chemin existant `ActivateQuickWindowAsync`.
- Le message de statut nomme la définition créée et le nombre de ports dérivés, y compris quand ce nombre est zéro — l'utilisateur doit savoir qu'il n'y avait rien à promouvoir plutôt que de croire à un échec.
- Les libellés restent en français ; le vocabulaire « Fragment » disparaît des surfaces utilisateur.

## 8. Hors périmètre

- Toute capacité runtime : aucune promotion dans le registre `DEC-0047`.
- Le contrat de package et l'export : inchangés.
- La conversion inverse, d'une fenêtre rapide vers une page.
- Le câblage des tags de `Popup_Fan_Condenseur`. La conversion livre une définition et une table vide ; remplir cette table est un travail d'édition distinct, à faire sur site.
- La performance de première ouverture d'un panneau ancrable (restructuration du `RootPanel` par `AddToLayout`), suivie séparément.

## 9. Validation

Le projet de tests référence Domain, Application, Rendering et Infrastructure, et lie certains fichiers de `ScadaBuilderV2.App` un par un. La commande de conversion doit donc vivre du côté **Application**, où elle reçoit de vrais tests.

À couvrir :

1. Une page sans aucune liaison se convertit : définition créée, page retirée, zéro port, mutation `Succeeded`. **C'est le cas réel de `win00034`.**
2. Une page portant des tags lus et écrits produit un port par tag distinct, avec la bonne `Family`, et ses éléments visent les ports.
3. Un tag à la fois lu et écrit produit un seul port `WriteCommand`.
4. Un tag absent du catalogue produit un port `String` et un diagnostic non bloquant.
5. La conversion d'une page `Header`, `Footer` ou d'accueil est refusée avec motif.
6. `Ctrl+Z` après conversion restaure la page et retire la définition ; l'état est identique à l'avant.
7. Une page restée en `Fragment` produit le diagnostic, et celui-ci ne bloque ni build ni export.
8. Collision de casse : deux tags ne différant que par la casse produisent deux ports distincts, le second suffixé, et le premier identifiant conservé en `Description`.

Chaque test doit échouer avant l'implémentation ; la preuve est exigée au rapport.

## 10. Risques

| Risque | Traitement |
| --- | --- |
| La conversion produit une définition invalide sur une page inattendue. | Validation de l'état d'arrivée à l'étape 7, échec bloquant la mutation entière. |
| Les ports dérivés sont trop nombreux pour être utilisables. | Accepté par D2 ; réversible par Supprimer dans « Interface locale ». |
| Un élément `LegacyStatic` ne se rend pas pareil dans une fenêtre rapide. | À vérifier à l'exécution sur `win00034` après implémentation. La validation ne l'interdit pas, mais rien ne le prouve encore. |
| Retirer `Fragment` du sélecteur casse un flux non identifié. | Le type reste lu et l'enum inchangé ; seule la création est retirée. |
