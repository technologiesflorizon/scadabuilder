# Design: pipeline de modernisation IA des composants .sep

Date: 2026-07-04
Statut: Approuve (brainstorming)

## Probleme

Certains composants `.sep` de la bibliotheque Element+ (`Condenseur.sep`,
`Ventilateur.sep`) sont composes d'un seul `Part` de type `Image`
encapsulant un raster PNG legacy basse resolution, importe depuis les
anciennes pages SCADA (`win00008` et similaires). Deux problemes :

- **Qualite visuelle** : le raster est flou/basse resolution, ne supporte
  pas le zoom, et son style ne correspond plus au niveau attendu en 2026.
- **Absence de decomposition** : contrairement a `Pipe_Elbow.sep`,
  `Vertical_Piping.sep` ou `pompeAmmoniac.sep` (deja decomposes en
  plusieurs `Parts` vectoriels), ces objets ne sont pas structures en
  sous-parties reutilisables/editables.

De nouveaux objets importes depuis des pages legacy continueront
d'arriver avec ce meme defaut (flux continu, pas un cas isole). Il faut
un pipeline reutilisable qui produit un `.sep` modernise en respectant
deux contraintes non negociables : les `Bounds` (dimensions) d'origine
ne changent jamais, et l'objet reste reconnaissable (un condenseur doit
rester un condenseur).

## Solution retenue

### Architecture generale

Un microservice Python separe de l'application .NET Studio Element+/SCADA
Builder V2, heberge sur un serveur Ubuntu disposant d'un GPU AMD (RX 9070
XT, 16 Go VRAM, ROCm) :

- **Django + DRF + Postgres** : API HTTP et etat des jobs (table `Job` :
  statut, payload, resultat, timestamps). `POST /modernize` recoit un
  `.sep` en entree, cree un `Job` en statut `queued`, publie un message
  Redis (pub/sub, canal `sep-jobs`) et repond immediatement avec le
  `job_id`. `GET /jobs/{id}` renvoie le statut et, une fois `done`, le
  `.sep` candidat + un rendu de comparaison avant/apres.
- **Redis** : utilise uniquement comme canal de notification pub/sub pour
  reveiller le worker instantanement (pas de polling), pas comme broker
  de queue complet.
- **Un seul service systemd** (`sep-modernize-worker.service`), process
  unique dedie au GPU : le modele reste charge en VRAM en permanence
  entre deux jobs. Au demarrage, il effectue un balayage initial des jobs
  `queued` en base (couvre le cas d'un message pub/sub perdu pendant un
  redemarrage), puis s'abonne au canal `sep-jobs` et traite les jobs au
  fil de l'eau, en mettant a jour leur statut en Postgres.
- Pas de Celery : un worker GPU est intrinsequement mono-concurrence
  (charger le modele plusieurs fois en VRAM sur une seule carte 16 Go
  causerait un OOM), donc la complexite d'un broker de taches complet
  n'apporte rien ici.

Perimetre V1 : seule l'equipe Florizon declenche le pipeline (pas de
declenchement client). L'API est neanmoins concue generique/sans etat
d'authentification fort pour qu'une future version "declenchement client
depuis Studio Element+" (service paye) puisse reutiliser le meme service
en ajoutant seulement une couche auth/quota/multi-tenant, sans reecrire
le pipeline.

Ce service vit dans un **repo Git separe** de `SCADA_BUILDER_V2`
(`sep-ai-modernizer`, `ssh://git@git.oroubotic-technologies.com:2224/knarfel01/scadaiconemakerpipeline.git`) :
stack (Python/Django) et cible de deploiement (serveur Ubuntu avec GPU,
secrets Postgres/Redis/cle Claude API) totalement differentes du produit
desktop .NET. Ce repo depend du contrat `.sep` documente dans
`SCADA_BUILDER_V2/docs/05_studio_element_plus/STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md`
sans couplage de code (cross-langage) ; toute evolution de ce contrat
doit etre repercutee manuellement dans `sep-ai-modernizer`.

### Detection des candidats

Regle deterministe : tout `.sep` contenant au moins un `Part` avec
`Kind == "Image"` (ou `Visual.Kind == "Image"` pour les cas les plus
anciens) est candidat a la modernisation. Cette regle permet de detecter
automatiquement les futurs objets importes depuis des pages legacy, sans
liste figee a maintenir a la main.

### Etapes du pipeline (execute par le worker)

Chaque etape produit une sortie validee avant de passer a la suivante
(voir Gestion d'erreurs) :

1. **Analyse** : parse le `Component` JSON, extrait les `Bounds` de
   reference (a preserver strictement) et l'asset raster embarque
   (`Base64Data`), qui sert d'ancre visuelle pour la reconnaissance de
   l'objet.
2. **Decomposition semantique (Claude API)** : appel structure avec le
   nom/categorie de l'objet, les `Bounds`, et le raster legacy en image.
   Sortie validee par schema JSON strict : liste de sous-parties nommees
   (ex. `capot`, `serpentin`, `grille_ventilation`, `cadre`), chacune avec
   une bounding box relative (0..Width/Height) et un prompt visuel court.
3. **Generation de tuiles (SDXL/ComfyUI local, ROCm)** : une tuile
   raster haute resolution par sous-partie, sur-echantillonnee, avec un
   template de style fixe et partage entre tous les objets (icone
   technique plate, sans ombre, fond transparent, palette coherente) pour
   garantir l'uniformite visuelle de toute la bibliotheque.
4. **Vectorisation (vtracer/potrace, sans IA)** : chaque tuile isolee est
   convertie en SVG propre (une forme isolee se vectorise nettement mieux
   qu'une scene complete).
5. **Assemblage** : repositionnement/mise a l'echelle de chaque
   sous-partie vectorisee dans le repere de coordonnees du `Bounds`
   d'origine, selon sa bounding box relative issue de l'etape 2. Garantit
   que les dimensions externes ne bougent jamais.
6. **Export `.sep`** : construction d'un `Part` de type `Group` contenant
   les sous-parties vectorisees comme `Children` (`Path`/`Polygon`),
   regeneration de `Visual.SvgMarkup` a partir de cet arbre de `Parts`
   (source de verite unique = les `Parts`), ecriture du `.sep` candidat +
   un rendu PNG/HTML de comparaison avant/apres.

### Revue humaine et integration

Le `.sep` candidat n'est jamais ecrit directement dans
`library/elements/`. Un humain (Florizon) consulte le rendu de
comparaison avant/apres pour chaque job `done`, puis remplace
manuellement le fichier officiel (avec sauvegarde prealable, suivant la
convention existante `references/backups/...`).

## Point de vigilance a verifier avant implementation

Le modele C# (`ElementStudioComponentModels.cs`) definit bien
`PartKind.Group` et `Part.Children` (liste recursive), mais **aucun**
`.sep` existant n'utilise cette hierarchie aujourd'hui, et il n'est pas
confirme que le rendu/import de Studio Element+ (WPF) gere reellement un
`Part` de type `Group` avec des `Children` a l'ouverture. A verifier tot
dans l'implementation ; si ce n'est pas le cas, le pipeline doit pouvoir
basculer en repli sur une liste plate de `Parts` (sans imbrication
`Group`/`Children`) au sein du meme `Component` — le regroupement reste
visuellement assure (memes `Parts` sous un seul `Component`), meme sans
la hierarchie `Group`.

## Gestion d'erreurs

Principe : aucun resultat douteux n'atteint la revue humaine sans signal
clair. Un job `failed` explicite est prefere a un `.sep` degrade produit
silencieusement.

- **Decomposition (Claude)** : JSON invalide/incomplet -> 1 retry
  automatique avec le meme prompt, puis `failed` (reponse brute jointe au
  job).
- **Generation de tuiles (SDXL)** : image vide/aberrante ou timeout GPU
  -> retry de cette tuile seule, puis `failed` si persistant.
- **Vectorisation** : SVG vide ou nombre de chemins aberrant (0 ou > 500
  pour une petite tuile) -> `failed`, pas d'assemblage a partir d'un
  resultat casse.
- **Assemblage/export** : le `Bounds` final doit etre identique bit-a-bit
  a l'original ; toute deviation -> `failed`, jamais de recadrage
  silencieux.
- **Timeout global** : plafond de duree par job (5 min), au-dela duquel
  le job passe `failed` pour ne pas monopoliser le seul GPU disponible.

Chaque `failed` conserve l'etape en cause, la raison, et les artefacts
intermediaires disponibles (diagnostic, amelioration des prompts/gabarits
au fil du temps).

## Tests

- **Automatises (pytest, Django)** :
  - Validation du schema de sortie de decomposition Claude (mock API),
    y compris les cas invalides de la section Gestion d'erreurs.
  - Preservation stricte des `Bounds` : a partir d'un `.sep` d'entree
    connu, le `.sep` de sortie a exactement les memes `Bounds`.
  - Conformite du `.sep` produit au contrat existant
    (`STUDIO_ELEMENT_PLUS_SEP_CONTRACT_V2.md` et modele C#
    `ElementStudioComponentModels.cs`).
  - Cycle de vie des jobs via l'API (creation, transitions de statut,
    retries) avec pipeline mocke, sans GPU reel.
- **Verification manuelle (non automatisable)** :
  - Qualite visuelle reelle et coherence de style : jugee uniquement par
    revue humaine sur l'artefact de comparaison avant/apres.
  - Avant mise en production du pipeline : test de bout en bout reel sur
    `Condenseur.sep` et `Ventilateur.sep` (les deux seuls objets `Image`
    existants aujourd'hui) avant d'ouvrir a d'autres objets.

## Hors perimetre

- Declenchement du pipeline par un client final (bouton dans Studio
  Element+, facturation, quotas, authentification multi-tenant) — prevu
  comme evolution future, l'API est concue pour le permettre sans
  reecriture, mais rien de tout cela n'est implemente dans cette version.
- Interface de revue dediee : la revue humaine s'appuie sur un artefact
  de comparaison simple (rendu PNG/HTML), pas sur une UI de validation
  integree.
- Integration dans l'application .NET (Studio Element+/SCADA Builder V2)
  pour declencher ou afficher l'avancement d'un job depuis l'editeur.
- Support/verification du rendu `PartKind.Group` par le reste de
  l'application au-dela du point de vigilance signale ci-dessus.
