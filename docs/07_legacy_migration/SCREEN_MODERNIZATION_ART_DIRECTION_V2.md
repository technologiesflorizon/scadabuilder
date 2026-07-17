# SCADA Builder V2 - Direction artistique de modernisation des ecrans

Date: 2026-07-17
Status: Active mandatory visual contract
Document version: `V2.1.4.0066`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-17 | `V2.1.4.0066` | `PENDING` | Premiere reference versionnee pour moderniser les controles de page a partir du pattern approuve de `win00008`. |

## 1. Objet et reference

Ce document est la regle visuelle obligatoire pour toute passe de modernisation d'ecran SCADA. Il evite les conversions purement structurelles qui conservent une apparence legacy, des zones de texte clippees ou des liaisons runtime perdues.

La reference approuvee est `projects/AMR_REF_SCADA_V2/scenes/win00008.scene.json` :

- cadre : `Rectangle004` (`shape_004`);
- lecture : `Element+ Text24` (`elementplus_numeric_display_789`);
- consigne : `Element+ Text27` (`elementplus_numeric_793`).

Cette page est une reference artistique locale. Elle ne remplace pas la politique de sources legacy : les identites, geometries utiles et liaisons viennent de la scene/source de la page modernisee.

## 2. Un controle de procede est un groupe visuel complet

Une lecture ou une consigne ne doit jamais etre modernisee comme un texte isole. Chaque controle comporte, de bas en haut :

1. un cadre Element+ `Shape / Rectangle`;
2. un champ `InputNumeric`, lisible et dimensionne pour la valeur complete;
3. une unite explicite, par exemple `°F`, `°C` ou `PSI`;
4. son libelle de mesure au-dessus du cadre;
5. un titre de section lorsque plusieurs controles forment une zone.

Les cadres legacy, leurs lignes de bordure et les textes legacy remplaces sont retires de la sortie par `RemovedSourceElementIds`. La trace `LegacySource` reste sur chaque objet Element+.

## 3. Tokens artistiques obligatoires

| Role | Police | Taille | Couleur | Cadre / fond |
| --- | --- | ---: | --- | --- |
| Cadre de controle | Segoe UI | 14 px | `#0F2A30` | fond `#BABABA`, bordure `#2090A0`, 1 px, `Inset`, ombre `Inset` |
| Valeur ou consigne | Arial, Arial, sans-serif | 16 px | `rgb(0, 0, 64)` | transparent, bordure `#8AA0A6`, 1 px, `Inset` |
| Unite | Arial, Arial, sans-serif | 14 px | `rgb(0, 0, 64)` | sans bordure, fond transparent |
| Libelle de mesure | Arial, Arial, sans-serif | 12 px gras | `rgb(0, 0, 64)` | sans bordure, fond transparent |
| Titre de section | Arial, Arial, sans-serif | 14 px gras | `rgb(0, 0, 64)` | sans bordure, fond transparent |

Les exceptions doivent etre visibles dans la revue et justifiees dans le commit.

## 4. Geometrie et lisibilite

Le cadre standard de la famille PT-16 mesure **120 x 34 px**.

- champ numerique : 66 x 24 px, avec une marge gauche de 12 px et une marge haute de 5 px;
- unite : 25 x 22 px, placee a 87 px du bord gauche et 6 px du bord haut;
- libelle : au-dessus du cadre, hauteur minimale 18 px;
- aucune valeur ne doit etre coupee; agrandir le champ avant de diminuer la police;
- aucune unite ne doit chevaucher le champ numerique.

Pour une valeur susceptible de depasser le format ou une unite plus longue, la largeur du cadre, du champ et la position de l'unite augmentent ensemble. Ne jamais reduire la police sous 14 px pour faire tenir une valeur industrielle.

## 5. Conservation fonctionnelle

La modernisation artistique ne modifie pas le comportement runtime.

- Preserver `Data.ReadTagId`, `Data.WriteTagId`, `DisplayFormat`, lecture seule, limites et decimales.
- Preserver `StateConfig` et verifier que chaque `ReadVariable.TagId` correspond au tag de lecture attendu.
- Preserver `CommandConfig`, y compris les commandes Lire+Ecrire de consigne.
- Ne pas inventer de mapping, de valeur, d'unite ou de regle Etat.
- Une unite affichee est un texte Element+ explicite; le champ `Data.Unit` reste une metadonnee legacy non active tant que le contrat runtime ne l'active pas.

## 6. Procedure obligatoire par page

1. Creer une copie de sauvegarde de la scene avant modification.
2. Inventorier les cadres, lectures, consignes, libelles, unites, lignes legacy et leurs `LegacySource.SourceElementId`.
3. Associer chaque lecture/consigne a son cadre et a son unite avant de modifier la geometrie.
4. Creer ou convertir les Element+ selon les tokens et les dimensions de ce document.
5. Retirer durablement les sources remplacees avec `RemovedSourceElementIds`; aucun doublon legacy ne doit rester visible derriere un Element+.
6. Comparer visuellement la page avec `win00008`, puis valider JSON, preview/build et export cible.

## 7. Criteres d'acceptation

Une page est acceptee seulement si :

- chaque controle modernise montre un cadre moderne, une valeur/consigne lisible et son unite;
- les libelles et titres utilisent la typographie definie;
- aucun texte, unite, cadre ou ligne legacy ne double visuellement un Element+;
- les liaisons PT-16, Etats et Commandes sont identiques avant et apres modernisation;
- les objets editor-only ne sont pas exportes;
- la comparaison visuelle humaine est faite avant de reproduire le pattern sur une autre page.

## 8. Portee initiale

La premiere application de cette regle est la zone de mesures de `win00007`. Elle doit etre reprise depuis sa sauvegarde selon ce document avant toute nouvelle propagation.
