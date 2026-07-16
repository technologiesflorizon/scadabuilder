# Conformite runtime generale SCADA Builder V2 vers TF100Web - Specification

Date: 2026-07-16
Status: Implemented on active branches - remote promotion pending
Document version: `V2.1.4.0062`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-16 | `V2.1.4.0062` | `PENDING` | Architecture implementee et prouvee sur les branches; promotion TF100Web distante et smoke operateur restent gates. |
| 2026-07-16 | `V2.1.4.0046` | `b2e4f5f` | Extension de `DEC-0046` en architecture generale de capacites, runtime semantique unique et conformance end-to-end. |

## 1. Probleme

Les corrections TF100Web ont jusqu'ici ete validees par tranche et souvent par page. Cette approche peut rendre un cas concret fonctionnel tout en laissant une autre combinaison du meme contrat silencieusement inactive. Elle encourage aussi la reimplementation d'une meme semantique dans le runtime exporte, le host TF100Web et des branches historiques.

Le modele courant expose davantage de comportements que les quatre pages de reference : quatre types de page, actions objet, bindings, expressions, effets, cinq triggers de commande, sept familles de commande et quatre modes d'ecriture. Certaines capacites sont implementees par le runtime partage; d'autres sont exportees mais pas executees par l'intake fragment TF100Web. Le manifest ne declare pas les capacites dont un package depend et TF100Web peut donc accepter un package qu'il ne sait pas executer completement.

Une succession de tests page par page ne suffit pas. Le contrat doit rendre impossible l'ajout d'une nouvelle capacite Builder sans declaration, implementation runtime, test de conformance et negotiation avec TF100Web.

## 2. Objectif de conformite

Pour toute capacite que SCADA Builder V2 permet d'authorer et d'exporter comme active, une seule des situations suivantes est valide :

1. La capacite est supportee end-to-end et possede des preuves Builder, package, runtime partage et TF100Web.
2. La capacite est explicitement marquee non supportee par la cible et l'export/deploiement est bloque avant utilisation operateur.

Une capacite ne peut plus etre exportee, acceptee et ensuite ignoree silencieusement.

Les quatre pages `win00003`, `win00004`, `win00008` et `win00012_modern_no_legacy` deviennent les tests d'integration industriels d'un contrat general. Elles ne definissent plus la limite de couverture.

## 3. Decisions d'architecture

### 3.1 Registre canonique de capacites

SCADA Builder V2 possede un registre type et versionne de capacites runtime. Chaque capacite a :

1. un identifiant stable;
2. une version minimale;
3. un proprietaire d'execution unique;
4. les artefacts manifest/DOM qu'elle consomme;
5. une fixture de conformance;
6. un statut `Supported`, `Blocked` ou `Deprecated`;
7. les tests Builder, runtime partage et TF100Web qui la prouvent.

Le registre est du code de contrat, pas une liste Markdown maintenue manuellement. La documentation et la matrice de couverture sont generees ou verifiees contre ce registre.

### 3.2 Negotiation manifest 2.3

Le manifest 2.3 ajoute un bloc racine `RuntimeContract` :

```json
{
  "RuntimeContract": {
    "Version": "1.0",
    "RequiredCapabilities": [
      "page.navigate",
      "state.expression.binary",
      "command.write.toggle",
      "binding.numeric.table"
    ],
    "RuntimeSha256": "<sha256>"
  }
}
```

L'analyseur Builder derive `RequiredCapabilities` du projet exporte. Aucun appel WPF ne construit cette liste. Le validateur `.sb2` refuse une capacite emise mais non declaree.

TF100Web publie son ensemble `SupportedCapabilities`. Lors du deploiement d'un manifest 2.3, toute capacite requise absente bloque le deploiement avec un diagnostic actionnable. Les manifests 2.1 et 2.2 restent acceptes par un profil de compatibilite infere et regression-teste; ils ne beneficient pas de la garantie stricte 2.3.

### 3.3 Un seul proprietaire semantique

Le runtime partage du package possede les semantiques portables :

- evaluation des expressions;
- selection d'etat et fallback;
- application/restauration des effets;
- interpretation de `CommandConfig`;
- interpretation des actions objet;
- conditions simples/composees;
- normalisation des intents navigation, popup, URL, historique et ecriture.

TF100Web fournit uniquement les services host :

- composition et cycle de vie des pages;
- navigation/historique;
- montage des fragments popup;
- snapshot et qualite des tags;
- permissions, CSRF et ecriture PLC;
- politique d'URL externe;
- diagnostics et observabilite.

Les actions historiques sont adaptees une seule fois vers l'enveloppe canonique du runtime partage. Elles ne sont pas reimplementees famille par famille dans `visualisation_import.js`. Les scripts inline hors fragment ne sont pas une source semantique pour TF100Web.

### 3.4 Cycle host latest-wins

`DEC-0046` demeure un invariant du contrat general : une generation de navigation obsolete ne peut jamais modifier DOM, historique, dimensions, popups, loading ou runtime. Chaque DOM accepte traverse une hydratation awaitable, meme si les valeurs cachees sont identiques ou si un poll etait en vol.

### 3.5 Definition durable de « 100 % »

La couverture generale est atteinte lorsque :

1. chaque valeur d'enum et chaque variante persistante exportable appartient au registre;
2. chaque capacite `Supported` a un test end-to-end;
3. chaque capacite non supportee est bloquee avant deploiement;
4. chaque package 2.3 negocie ses besoins avec TF100Web;
5. aucune logique de meme capacite n'existe dans deux executeurs concurrents;
6. les tests industriels des quatre pages passent sans exception non documentee.

Une absence de mapping projet n'est pas un echec du contrat general. Elle doit produire le fallback qualite et un diagnostic deterministe, sans tag fabrique. `YL_E12_HDEG4` est donc une donnee indisponible connue, non un gate architectural.

## 4. Matrice generale obligatoire

### 4.1 Package, deploiement et composition

| Famille | Cas a couvrir |
| --- | --- |
| Manifest | 2.1/2.2 compatibilite; 2.3 strict; version inconnue; champ manquant; capacite inconnue; hash runtime invalide. |
| Pages | `Default`, `Header`, `Footer`, `Fragment`; page exclue; home valide/invalide; reference absente; mauvais type; cycle; page autonome. |
| Composition | sans chrome, header seul, footer seul, header+body+footer, fragment sans chrome, dimensions, backgrounds, CSS/assets. |
| Namespace | DOM/CSS/action targets page-scopes, ids dupliques, chemins dangereux, assets relatifs, composition multi-page. |
| Deploiement | package froid/chaud, remplacement atomique, rollback, invalidation cache, runtime hash, catalogue revise. |

### 4.2 Bindings et controles

| Famille | Cas a couvrir |
| --- | --- |
| Combinaisons | lecture seule, ecriture seule explicite, lecture/ecriture meme mapping, mappings distincts, mapping absent, mapping non ecrivable. |
| Cibles | affichage numerique, `InputNumeric` Element+, `InputNumeric` cellule Tableau, plusieurs cibles sur un mapping, header/footer/body/popup. |
| Format | `fixed:n`, masques `#`, entier signe/non signe, float, booleen, valeur nulle/invalide, min/max/step/decimales. |
| Edition | focus, polling concurrent, Enter, Escape, blur/change, deduplication, readback, rejet serveur, session/CSRF expiree. |
| Qualite | jamais lu, mauvais statut, deconnexion, snapshot partiel, reprise, valeur identique sur nouveau DOM. |

### 4.3 Etat et expressions

Tous les noeuds AST exportables sont couverts : `literalNumber`, `literalBool`, `literalString`, `tagRef`, `unary`, `binary` et `func`.

Tous les operateurs/fonctions sont couverts : `Not`, `Negate`, arithmetique, comparaisons, `And`, `Or`, `ABS`, `MIN`, `MAX`, `BIT`, division/modulo par zero, tag absent et coercitions autorisees.

La selection d'etat couvre ordre, `Enabled`, first-match, default, quality fallback, zero etat, plusieurs tags, plusieurs elements, plusieurs slots, reinitialisation et reprise apres edition.

Chaque champ de `ScadaEffectBlock` est teste seul et en transition avec chaque categorie voisine : arriere-plan, bordure, texte/couleur/visibilite, element visible, opacite, rotation, animations `None/Blink/Pulse/Halo/Spin`, filtre couleur, opacite/halo du filtre, restauration baseline et tokens texte.

### 4.4 Commandes Element+

| Axe | Ensemble exhaustif |
| --- | --- |
| Trigger | `OnClick`, `OnRelease`, `OnHover`, `OnHoverEnter`, `OnHoverExit`. |
| Kind | `WriteTag`, `Navigate`, `OpenPopup`, `TogglePopup`, `ClosePopup`, `OpenUrl`, `Back`. |
| WriteMode | `Momentary`, `Toggle`, `SetFixed`, `SetFromInput`. |
| Garde | enabled/disabled, confirmation acceptee/refusee, permissions, champs requis manquants, handler idempotent. |
| Concurrence | double clic, commande pendant navigation, write pending, readback divergent, DOM remplace, reponse tardive. |

`Momentary` est valide sur press et release reels; il ne peut pas rester une approximation « press cycle » sur un clic unique si le Builder l'annonce comme supporte.

### 4.5 Actions objet et conditions

Les actions `Navigate`, `Show`, `Hide`, `ToggleVisibility`, `MountFragment`, `ClosePopup`, `TogglePopup`, `ReadValue` et `WriteValue` doivent etre adaptees au runtime canonique ou bloquees.

Les conditions couvrent chaque operateur, `All`/`Any`, politiques de valeur manquante, target introuvable, groupe Element+, propagation/prevention et ordre de plusieurs actions sur le meme trigger.

### 4.6 Pages, navigation et popups

Couverture obligatoire : chargement frais, navigation repetee, clics rapides, ordre de reponse inverse, back/forward, meme page, erreur HTTP, timeout, annulation, offline/reprise, session expiree, popup unique/multiple selon politique, open/close/toggle, fermeture lors d'un changement de corps, fragment sans header/footer et restauration du focus.

### 4.7 Styles et boutons

Le transport opaque des styles Element+ couvre typographie, couleurs, bordures, rayon, opacite, rotation, SVG, groupes, hover/pressed, disabled, types de bouton, curseur, texte semantique et absence d'artefacts editeur.

### 4.8 Securite et diagnostics

Les tests couvrent echappement HTML/JSON/attributs, URLs autorisees/interdites, paths, ids, ciblage hors scope, permission write, CSRF, absence de valeurs PLC dans les logs, diagnostics sans fuite et rejet d'une capacite inconnue.

## 5. Suite de conformance partagee

Builder genere un petit package `.sb2` de conformance deterministe contenant une page/fixture par capacite et un index des attentes. Le meme artefact, identifie par SHA-256, est consomme par les tests TF100Web. Le SHA attendu est versionne des deux cotes; une divergence bloque la CI.

La pyramide de tests est :

1. tests unitaires Domain/Application pour registre et analyse;
2. tests JavaScript table-driven du runtime partage;
3. tests exporter/validator pour manifest, DOM et capabilities;
4. tests Django intake/composition/deploiement;
5. harness navigateur host avec concurrence et erreurs;
6. quatre pages de reference comme integration industrielle;
7. smoke d'ecriture PLC seulement dans une fenetre autorisee.

Les tests de symboles ou de chaines source peuvent rester des garde-fous, mais ne constituent jamais la preuve principale d'un comportement asynchrone.

## 6. Performance et observabilite

La performance s'applique au contrat general, pas seulement aux pages lourdes :

- injection mapping indexee ou en passage unique;
- cache structurel par generation de deploiement;
- revision de catalogue dans les cles pertinentes;
- cancellation client et rejet de toutes les reponses stale;
- `Server-Timing`/logs par phase;
- budgets froids/chauds et tests de non-regression par taille de fragment et nombre de bindings.

Un budget depasse ne doit pas changer la correction fonctionnelle. Il produit un echec de performance distinct et actionnable.

## 7. Migration et compatibilite

1. Les packages 2.1/2.2 restent lisibles avec le profil historique teste.
2. Le premier export 2.3 n'est autorise qu'apres deploiement TF100Web compatible.
3. Aucun projet n'est migre a l'ouverture uniquement pour ajouter des capabilities; celles-ci sont derivees a l'export.
4. Les actions anciennes sont normalisees par un adaptateur, sans duplication de dispatcher.
5. Les capacites depreciees restent nommees dans le registre jusqu'a la fin de leur fenetre de compatibilite.

## 8. Tests industriels de reference

- `win00003` prouve les huit navigations et l'historique.
- `win00004` prouve la composition statique header/body/footer.
- `win00008` prouve etats, lectures et input numerique apres aller-retour.
- `win00012_modern_no_legacy` prouve les Toggles et les 126 cellules liees.

Le mapping `YL_E12_HDEG4` absent est attendu en fallback qualite et consigne dans les diagnostics; il ne bloque pas la conformance des autres capacites.

## 9. Criteres de sortie

1. Registre complet contre les enums/modeles/exporteurs actuels.
2. Manifest 2.3 negocie et strict.
3. Un proprietaire semantique par capacite.
4. Toutes les capacites `Supported` vertes dans la suite partagee.
5. Toute capacite restante `Blocked` refusee avant deploiement avec raison.
6. Aucun test industriel ne depend d'un cache chaud ou d'un ordre reseau favorable.
7. Matrice documentaire generee/synchronisee et aucun gap presente comme implemente.

## 10. Hors scope

1. Inventer de nouvelles fonctions produit qui ne sont pas authorables aujourd'hui.
2. Garantir la latence du reseau cellulaire.
3. Modifier le PLC ou fabriquer des mappings.
4. Executer arbitrairement des scripts utilisateur dans TF100Web.
5. Maintenir deux moteurs semantiques pour une meme capacite.
