# Design: modernisation visuelle 2026 des pages SCADA (Element+)

Date: 2026-07-04
Statut: Approuve (brainstorming)

## Contexte et probleme

`sep-ai-modernizer` (pipeline Python/Django/GPU decrit dans
`2026-07-04-sep-ai-modernization-pipeline-design.md`) a ete entierement
implemente (13 taches, revues approuvees) mais ne produit pas des
resultats utilisables en pratique. Deux familles de defauts constatees :

- **Qualite de style** : comparaison `Ventilateur.sep` (original, PNG
  raster legacy encapsule dans une balise `<image>`) vs `Ventilateur2.sep`
  (regenere par ChatGPT) - le resultat est un icone "flat design" grand
  public (dégradés pastel gris/bleu, look generique), pas un langage
  visuel industriel SCADA. De plus, meme modernise, le contenu reste une
  image opaque encapsulee (pas de `<path>`/`<circle>` natifs dans le DOM
  du composant), ce qui empeche tout pilotage d'etat par couleur/CSS.
- **Rupture geometrique fine** : dans `08_web_modernized/html_pages/win00008_updated.html`,
  les images de tuyauterie regenerees (`assets/modernized/win00008_auto_group_0001/*.svg`)
  conservent un `Bounds` (left/top/width/height) correct, mais leur trace
  interne ne touche plus les bords de leur boite englobante aux memes
  positions relatives que l'original. Resultat : le point de jonction
  visuel avec la valve/le tank voisin (fichier SVG independant) ne tombe
  plus au bon endroit, meme si chaque piece individuellement "rentre"
  dans sa boite. La preservation de `Bounds` seule (garantie deja
  demontree par `pipeline/export.py::export_component` dans
  `sep-ai-modernizer`) est **necessaire mais pas suffisante**.

Le systeme de conversion Legacy -> Element+ (`.sep`) de SCADA Builder V2
fonctionne deja et n'est pas remis en cause ici. Le present design couvre
uniquement **comment produire un artwork moderne** pour un `.sep` donne,
sans casser son integration dans la scene.

Hors scope explicite : modernisation du systeme d'evenements/etats
(`ScadaEventRegistry`, comportements idle/active/warning/fault/critical
actuellement geres par des classes JS page-locales comme `Condenser`
dans `win00008_updated.html`). Ce chantier est note comme suite logique
mais traite separement, une fois l'aspect visuel stabilise.

## Solution retenue

### 1. Guide de style SCADA 2026

Document court (`SCADA_BUILDER_V2/docs/design/SCADA_2026_STYLE_GUIDE.md`)
fixant, une fois pour toutes :

- palette (fond, couleurs d'etat reprenant celles deja en usage dans le
  pattern `Condenser` : IsActive/IsFaulty/IsWarning/IsCritAlarm/IsOffline/IsUnknown)
- langage de trait : epaisseur de contour constante, remplissages plats
  a fort contraste, style schematique technique - explicitement **pas**
  de dégradés/ombres "flat icon" grand public comme `Ventilateur2.sep`
- grille/`viewBox` standard commune a toutes les icones
- 2-3 icones de reference validees manuellement par l'utilisateur (ex:
  le ventilateur) servant d'exemple impose a toute regeneration future

Ce guide est cree/valide au premier passage sur une icone reelle
(section "Boucle de travail" ci-dessous), pas redige a priori dans le
vide.

### 2. Contrat geometrique : Bounds + points de jonction

Pour tout element source destine a etre modernise, on extrait de sa
geometrie vectorielle legacy (pas d'une capture image) :

- `Bounds` : deja produit par la conversion Legacy -> Element+ existante
  de SCADA Builder V2 (aucun changement requis ici)
- **points de jonction** : positions ou le trace original traverse le
  bord de sa boite englobante, exprimees en fraction de largeur/hauteur
  (ex: "entre par le bord gauche a 62% de la hauteur, sort par le bord
  droit a 20%"). Calcule geometriquement a partir des paths SVG source
  (intersection trace/bbox), pas par inspection visuelle.

Contrainte de sortie : le SVG modernise doit traverser ses propres bords
aux memes positions relatives, avec une tolerance fixee a **2 pixels**
(a l'echelle du rendu final dans la page, pas de l'espace de travail
interne). Cette verification est un calcul geometrique automatique
(comparaison de coordonnees), pas un jugement visuel - elle peut donc
bloquer une regeneration avant meme la revue humaine.

### 3. Boucle de travail (interactive, Claude Code)

Le declenchement reste manuel et interactif, session par session, plutot
qu'un service autonome (rejet explicite de l'approche "pipeline
autonome" pour ne pas reproduire l'echec de `sep-ai-modernizer`) :

1. L'utilisateur cree/identifie le `.sep` de l'element dans l'editeur
   SCADA Builder V2 (flux deja fonctionnel, inchange).
2. Dans une session Claude Code, l'utilisateur pointe le `.sep` (et sa
   source legacy). Claude :
   - extrait `Bounds` + points de jonction de la geometrie source,
   - redessine le SVG en respectant le guide de style + les contraintes
     de jonction,
   - verifie geometriquement le resultat (tolerance 2px) avant de le
     presenter.
3. L'utilisateur valide visuellement (coherence de style avec les icones
   deja approuvees, lisibilite fonctionnelle) ou demande un ajustement.
4. Le `.sep` modernise remplace l'ancien dans la bibliotheque Element
   Studio - reutilisable partout ou cet archetype apparait, donc le
   travail ne se refait pas a chaque nouvelle page qui utilise le meme
   composant.
5. Repetition element par element, page par page, en commencant par
   win00008 comme pilote, extensible a toute page `win*`.

### 4. Non-regression avant acceptation d'un `.sep` modernise

- `Bounds` identiques a l'original (contrat deja garanti par l'editeur
  SCADA Builder V2, non renegocie ici)
- points de jonction dans la tolerance de 2px
- SVG en vecteur natif inline (`<path>`/`<circle>`/...), jamais une
  image raster ou SVG encapsulee en base64 dans une balise `<image>` -
  condition necessaire pour un futur pilotage d'etat par couleur/CSS
- coherence de style avec les icones deja approuvees (comparaison cote a
  cote, sur le modele de `preview_compare.html`)

## Alternatives envisagees et ecartees

- **Automatisation IA en boucle fermee avec garde-fou qualite**
  (resurrection ajustee de `sep-ai-modernizer`) : ecartee pour l'instant
  - reinvestir dans une automatisation avant d'avoir un guide de style et
  un contrat de jonction valides manuellement risquerait de reproduire
  l'echec deja constate. Peut redevenir pertinent plus tard, une fois le
  guide de style et le contrat geometrique elabores et stabilises sur
  plusieurs icones via la boucle interactive.
- **Perimetre reduit aux ~8-10 icones les plus frequentes** : ecarte
  comme structure de decision explicite - dans la pratique, la boucle
  interactive traite naturellement les icones les plus utilisees en
  premier puisqu'elles apparaissent le plus souvent sur win00008, sans
  qu'il soit necessaire de figer une liste a l'avance.

## Suite

Chantier separe, non planifie ici : modernisation de
`ScadaEventRegistry` et des comportements d'etat (actuellement des
classes JS page-locales comme `Condenser`), pour piloter les nouvelles
icones vectorielles nativement (recoloration CSS par etat) plutot que
par swap de fichier SVG entier comme c'est le cas aujourd'hui pour les
ventilateurs.
