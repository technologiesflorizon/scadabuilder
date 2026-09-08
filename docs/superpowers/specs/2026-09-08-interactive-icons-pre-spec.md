# Pré-spécification — Icônes interactives dans Studio Element+

Date: 2026-09-08
Status: Pré-spécification — décisions arrêtées et points non tranchés, avant design détaillé
Document version: `V2.1.6.0010`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-09-08 | `V2.1.6.0010` | `PENDING` | Création : décisions arrêtées pour les chantiers A (outils de dessin) et B (parties adressables et instances), propositions non confirmées et points non tranchés séparés. |

## 0. Ce que ce document est, et n'est pas

Ce document **fige ce qui a été décidé** au cours de la discussion de cadrage du 2026-09-08, pour qu'aucune de ces décisions ne soit reprise de mémoire ou réinventée plus tard.

Il distingue trois choses, et la distinction est l'essentiel de sa valeur :

- **§2 Décisions arrêtées** — choisies explicitement. Elles ne se rediscutent pas sans décision contraire.
- **§3 Propositions non confirmées** — recommandées et argumentées, mais jamais approuvées. Elles n'ont pas force de décision.
- **§4 Points non tranchés** — ni décidés ni proposés. Ce ne sont pas des décisions ouvertes au sens du registre `DEC-` : ce sont des inconnues de conception à lever avant d'écrire le design.

Ce n'est pas un design : il n'y a ici ni architecture cible, ni contrats de validation, ni plan de tests. Ceux-ci viendront dans les specs de A puis de B, écrites après le chantier C.

**Prérequis :** le chantier C, `2026-09-08-project-format-versioning-and-converters-design.md`, est livré avant A. Les deux chantiers ci-dessous produisent chacun leurs convertisseurs.

## 1. Le besoin, tel qu'énoncé

> « Il faut moderniser Studio Element+ afin de pouvoir dessiner afin de créer des icônes, importer des SVG et dessiner/modifier. Il faut les outils nécessaires pour pouvoir se créer des icônes modernes. Importer des fonts, outil de dessin. Actuellement aucun outil complet n'existe pour créer des éléments. Ex.: je veux dessiner un réservoir ou une vanne actuateur. Je dois pouvoir. »

### 1.1 Constat vérifié dans le code

**Le modèle est à moitié prêt; c'est l'outillage qui manque.** `ElementStudioComponentPartKind` déclare déjà `Line`, `Polyline`, `Rectangle`, `Polygon`, `Path`, `Text`, `Image`, `Group`, `Html`, `Custom`. Le format `.sep` sait donc décrire un dessin vectoriel par parties nommées, hiérarchisées, stylées, avec assets embarqués.

Mais `ElementStudioEditorState`, 493 lignes et cœur de l'éditeur, ne sait que **manipuler ce qui existe déjà** : sélectionner, déplacer, redimensionner, aligner, distribuer, grouper, verrouiller, annuler. Il n'existe **aucune primitive de création**. C'est un éditeur d'arrangement.

**Le vocabulaire d'effets est plus riche qu'attendu.** `ScadaEffectBlock` porte quatorze propriétés : couleurs de fond, bordure et texte, contenu texte, visibilité du texte et de l'élément, opacité, **rotation**, quatre animations (`Blink`, `Pulse`, `Halo`, `Spin`) et un filtre de couleur avec halo. Faire tourner un actionneur selon un tag ne demande aucun effet nouveau.

**Mais le ciblage est un drapeau, pas un nom.** Le runtime partagé résout `element.querySelectorAll('[data-scada-effect-background-target]')` et applique **le même effet à tous les nœuds marqués**. Un état, une couleur, tout ce qui est marqué change ensemble. Une vanne dont le corps reste gris, dont l'actionneur tourne et dont le témoin passe au vert selon trois sources distinctes est aujourd'hui inexprimable.

**Un élément posé depuis la bibliothèque ne garde aucune identité vers son `.sep`.** `ElementPlusLibraryItem` porte un `ComponentId`, mais rien sur l'élément de scène ne le retient — au mieux un `SourceTrace` informatif. Poser une icône, aujourd'hui, c'est coller des formes, pas instancier une définition.

**Un défaut d'import à corriger.** `ElementStudioSvgMarkupNormalizer.CalculateGeometryBounds` reconnaît `circle`, `ellipse`, `image`, `line`, `polygon`, `polyline`, `rect` et `text`, **mais pas `path`**. Tout SVG produit par Illustrator, Figma ou un générateur d'images est fait de `path` : pour ceux-là les bornes reviennent nulles et le markup est renvoyé sans normalisation de `viewBox`. L'import SVG est silencieusement dégradé pour l'entrée la plus courante. À corriger dans A, quel que soit le reste.

Le normaliseur, en revanche, **ne filtre rien** : `<defs>`, `<linearGradient>`, `<radialGradient>`, `<mask>`, `<clipPath>` et `<filter>` traversent l'import intacts. Les fonctionnalités vectorielles avancées ne sont pas bloquées par la plomberie.

## 2. Décisions arrêtées

### A1 — L'icône est un composant typé à états, pas une illustration

Un dessin produit dans Studio Element+ porte des parties nommées qu'un état peut cibler indépendamment. Une vanne affiche sa position; elle n'est pas une image.

*Conséquence :* le chantier B n'est pas optionnel. Sans lui, A ne produit que de jolies images.

### A2 — A avant B

Les outils de dessin sont livrés d'abord, avec le nommage de parties et la déclaration d'interface **dès le départ**, mais inertes côté runtime. Les icônes dessinées pendant A s'animeront quand B arrivera, sans réauthoring.

*Conséquence :* le modèle `.sep` cible doit être figé au début de A, pas découvert à la fin.

### A3 — Les polices sont embarquées, pas vectorisées

Un texte conserve sa police dans le paquet exporté, en WOFF2 base64. Il reste donc du texte : modifiable, et pilotable par un tag via l'effet `TextContent` existant.

*Contreparties actées :* le paquet grossit de 20 à 300 ko par police, et la redistribution exige une licence qui l'autorise. La vérification de licence est une responsabilité d'exploitation, pas une fonctionnalité du produit.

### B1 — L'icône a une interface locale; le mapping se fait dans le Builder

Studio Element+ définit l'icône **globalement** : son dessin, ses parties nommées, son interface locale typée et ses liaisons internes. SCADA Builder câble **chaque instance** à des tags réels.

C'est le triplet définition / invocation / interface locale de `DEC-0050`, appliqué à un autre objet. Il apporte avec lui le versionnement d'interface, le statut `Outdated`, la surface de réparation, les registres déterministes du manifest et le gate fail-closed.

*Raison :* plusieurs instances de la même icône coexistent avec des mappings différents. Aucun autre modèle ne l'exprime.

### B2 — N instances simultanées, contrairement aux Fenêtres rapides

| | Fenêtre rapide | Icône interactive |
| --- | --- | --- |
| Présence | montée et démontée à la demande | inline, toujours présente |
| Instances simultanées | **une seule**, `SinglePerDefinition` | **N, obligatoirement** |
| Cycle de vie | possédé par le host TF100Web | aucun : c'est du DOM de page |
| Chrome, backdrop, focus, invalidation | oui | non |

C'est un **relâchement** du modèle Fenêtre rapide, pas une contrainte supplémentaire. Rien de la machinerie host n'est requis.

### B3 — L'élément de scène gagne une identité de définition

Un élément posé depuis la bibliothèque portera une clé de définition et une clé d'invocation, comme une commande porte son `QuickWindowInvocationKey`. C'est le premier pas de B.

### B4 — Le manifest passe en 2.4

Le paquet exporté gagne des registres de définitions et d'invocations de composants. Les profils 2.1, 2.2 et 2.3 restent lisibles. Le schéma `.sep` passe de 1 à 2. La `ContractVersion` des capacités `DEC-0047` **ne bouge pas** : ce sont de nouveaux identifiants `Blocked` jusqu'à preuve aux trois couches, pas un nouveau contrat.

### B5 — Rien ne se convertit sans convertisseur ni consentement

Les règles du chantier C s'appliquent intégralement : version de format par module, refus vers l'arrière, sauvegarde automatique, conversion consentie, **convertir ou ne pas ouvrir**. A livre le convertisseur `.sep 1 → 2`; B livre celui du module projet.

## 3. Propositions non confirmées

Ces points ont été recommandés et argumentés. **Aucun n'a été approuvé.** Ils sont consignés pour être tranchés au début du design de A, pas pour être appliqués tels quels.

### P1 — Bâtir sur Paper.js, exposer par paliers

Question posée : « Est-ce que l'éditeur vectoriel complet est réalisable ? » Réponse donnée : oui techniquement, avec trois raisons — les opérations booléennes sur courbes de Bézier sont fournies par Paper.js (MIT) qui tourne dans le WebView2 déjà hébergé; dégradés, masques, écrêtage et filtres sont du SVG natif que l'import ne filtre pas; l'architecture canevas + pont JS est celle de l'éditeur principal.

Nuance donnée : « complet » n'est pas une liste de fonctionnalités mais un niveau de finition. L'ergonomie d'édition de nœuds, le magnétisme, les guides, la sélection sur chemins superposés et la granularité d'annulation sont où passent les mois.

**Recommandation, non confirmée :** viser le niveau complet comme destination, bâtir le canevas sur Paper.js dès le départ, mais n'exposer d'abord que primitives, plume et édition de nœuds. Booléens, masques et dégradés radiaux deviennent alors de l'ajout d'interface, pas une réécriture.

**Statut : le niveau d'outillage de A n'est pas arrêté.**

### P2 — États par membres d'interface, actions par ancrages nommés

Question posée : « Les évents seraient dorénavant définis côté Studio Element+ de manière globale puis associés réellement au tag / page_id / autre dans SCADA Builder ? »

Réponse donnée, appuyée sur un fait : `ScadaCommandKind` compte six valeurs — `WriteTag`, `Navigate`, `OpenQuickWindow`, `CloseQuickWindow`, `OpenUrl`, `Back` — et **une seule vise un tag**. Un membre d'interface de famille `WriteCommand` se lie à un tag par construction. Tout exprimer en membres d'interface ferait perdre cinq kinds sur six, ou obligerait à redupliquer un modèle de commande complet avec ses conditions, sa confirmation et ses quatre modes d'écriture.

**Découpage proposé, non confirmé :**

| Studio Element+ définit globalement | SCADA Builder associe par instance |
| --- | --- |
| **États** — membres `ReadState` / `PublicParameter`, et liaisons internes `partie ← membre` | le **tag** réel |
| **Actions** — des **ancrages nommés** : « cette partie est cliquable, elle s'appelle `actionneur` » | la **commande complète** existante, avec conditions, confirmation et mode d'écriture |

Forme illustrative proposée :

```
vanne-actuateur.sep
  Interface locale
    IsOpen      ReadState        Boolean   requis
    Position    ReadState        Integer   requis
    Label       PublicParameter  String
  Liaisons internes
    temoin.fill      ← IsOpen
    tige.rotation    ← Position
    etiquette.text   ← Label
  Ancrages d'action
    "actionneur"   (partie tige, cliquable)
    "corps"        (partie corps, cliquable)
```

**Statut : proposé, jamais approuvé.** La question « ce découpage vous convient-il ? » n'a pas reçu de réponse.

### P3 — Les liaisons internes désignent un membre, jamais un tag

`ElementStudioComponentBinding(BindingId, Target, BindingType, TagName, Expression)` nomme aujourd'hui un **tag**. Sous B1, une liaison interne doit nommer un **membre d'interface**; le tag vient de l'invocation.

Si cette correction n'est pas faite pendant A, chaque icône dessinée devra être réauthorée quand B arrivera.

**Statut : proposé. Découle de B1 mais n'a pas été confirmé explicitement.**

### P4 — Un `.sep` ne transporte aucun code exécutable

`ElementStudioComponentEvent` porte aujourd'hui un champ `Script`. Dans un artefact qui traverse un runtime fail-closed et se négocie par capacités, du script libre est une surface qu'il vaut mieux fermer avant de bâtir dessus.

**Proposé :** un `.sep` ne porte que l'affordance et le nom, jamais du code.

**Statut : proposé, non confirmé. Retirer un champ existant est une rupture de format qui exige son propre convertisseur.**

### P5 — Les commandes deviennent adressables par ancrage

`ScadaElementCommandConfig` rattache aujourd'hui les commandes à l'élément entier. Une icône à trois ancrages exige que chaque commande dise à quel ancrage elle se rattache.

**Statut : conséquence mécanique de P2. Tombe si P2 tombe.**

## 4. Points non tranchés

1. **Représentation du dessin dans le `.sep`.** Le markup SVG fait-il foi, avec `Parts[]` comme index nommé vers ses nœuds ? Ou `Parts[]` fait-il foi, le SVG étant régénéré ? La première voie est sans perte et suit Paper.js naturellement; la seconde donne le contrôle total au prix de devoir réexprimer la sémantique SVG. **Non tranché.**
2. **Promotion d'un composant existant.** Comment un `.sep` v1 devient-il une icône interactive : action explicite par composant, ou conversion en lot ? Et que devient une icône promue par erreur, au-delà du `.bak` ?
3. **Portée de la bibliothèque.** Une définition d'icône vit-elle dans la bibliothèque du projet, dans une bibliothèque partagée entre projets, ou les deux ? Cela décide si l'invocation référence un chemin ou une clé globale.
4. **Ordre d'application des états.** Deux membres qui touchent la même partie — l'un la couleur, l'autre l'opacité — sont cumulatifs; deux qui touchent la même propriété ne le sont pas. Quelle règle ?
5. **Capacités à créer.** Quels identifiants `DEC-0047` couvrent les icônes interactives, et lesquels restent `Blocked` faute de preuve host ?
6. **Coût réel de P1.** Aucune estimation n'a été produite. « Plusieurs mois » a été avancé pour le niveau complet, sans décomposition.

## 5. Ce qui vient ensuite

1. Chantier **C** livré : versionnement de format et convertisseurs.
2. Trancher §3 et §4 pour A, puis écrire la spec de A et son plan.
3. A livré, avec le modèle `.sep` v2 figé et son convertisseur.
4. Trancher §4 pour B, puis écrire la spec de B et son plan.
