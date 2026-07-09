# Element+ Studio — crash silencieux à l'import de lots legacy volumineux

- **Date** : 2026-07-08
- **Statut** : Approuvé
- **Scène de repro** : `win00059` (AMR_REF_SCADA_V2), 152 items sélectionnés
- **Commit du dernier fix (raté)** : `9751349` — "fix: add null guards and catch-all exception handling in ElementStudioPackageLoader"

## 1. Contexte

Depuis SCADA Builder V2, sélectionner un lot d'éléments legacy dans une scène puis
"Ouvrir dans Studio Element+" ouvre `ScadaBuilderV2.ElementStudio.App`, qui affiche sa
fenêtre puis se referme automatiquement quelques secondes plus tard, sans message
d'erreur. Le commit `9751349` a ajouté des garde-fous null et un `catch (Exception)`
dans `ElementStudioPackageLoader` et `App.OnStartup` en supposant une
`NullReferenceException` sur `SceneBounds` lors de la désérialisation JSON. Le crash
persiste : le patch protège un chemin de code qui n'est pas celui qui échoue.

## 2. Root cause (confirmée par reproduction réelle)

Reproduit via `dotnet run --project src/ScadaBuilderV2.ElementStudio.App -- <fichier .ft1 de win00059>` :

```
System.ArgumentException: Value does not fall within the expected range.
   at Microsoft.Web.WebView2.Core.Raw.ICoreWebView2.NavigateToString(String htmlContent)
   ...
   at ScadaBuilderV2.ElementStudio.App.MainWindow.OnLoaded(Object sender, RoutedEventArgs e) in MainWindow.xaml.cs:line 72
```

Chaîne causale :

1. **Capture d'extraction trop verbeuse.** `toElementMessage` dans
   `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs:543-559` sérialise
   `Array.from(window.getComputedStyle(el))` — la totalité des propriétés CSS
   exposées par le moteur WebView2 (~300, y compris `grid-*`, `scroll-*`, `ruby-*`,
   `hyphenate-*`, `contain-*`, `timeline-*`, `-webkit-*`, etc.), pas seulement les
   propriétés pertinentes au rendu de l'élément. Résultat mesuré sur le fichier réel :
   ~12 Ko de style par élément.
2. Pour `win00059` (152 items), ça donne **1,86 Mo** de markup brut rien que pour les
   styles inline, dans un package `.ft1` de 3,9 Mo au total.
3. `MainWindow.OnLoaded` (ElementStudio.App, ligne 72) concatène le markup de tous les
   items dans un seul document HTML (`BuildLegacySourceDocument`) et l'envoie via
   `WebView2.NavigateToString()`, qui a une **limite documentée de ~2 097 152
   caractères UTF-16 (2 Mo)**. Le document dépasse cette limite → `ArgumentException`.
4. `OnLoaded` est déclaré `async void`. Une exception qui s'y produit est postée sur
   le dispatcher WPF ; comme **aucun handler global n'existe** dans
   `ScadaBuilderV2.ElementStudio.App` (ni `Application.DispatcherUnhandledException`,
   ni `AppDomain.CurrentDomain.UnhandledException`, ni
   `TaskScheduler.UnobservedTaskException`), WPF termine le process silencieusement.
   Ça correspond exactement au symptôme observé : la fenêtre s'affiche (le
   constructeur synchrone de `MainWindow`, protégé par le `try/catch` de
   `App.OnStartup`, réussit), puis le crash survient dans la continuation
   asynchrone qui suit `Show()` — hors de portée de ce `try/catch`.

Le patch `9751349` n'avait aucune chance de corriger ce crash : il protège
`ElementStudioPackageLoader.Load()` (chargement JSON synchrone) et le constructeur de
`MainWindow`, deux chemins de code qui réussissent déjà. Le vrai point de défaillance
est postérieur à `window.Show()`.

## 3. Contrainte de non-régression `.sep` / `.sb2`

Investigation du flux de consommation du markup capturé (voir rapport d'exploration) :

- Le dump de style calculé n'est pas limité à l'aperçu de Studio Element+. Il est
  embarqué tel quel dans `Visual.SvgMarkup` / `Parts[].HtmlMarkup` à l'écriture du
  `.sep` (`ElementStudioComponentPackageStore`, aucune normalisation).
- À l'ouverture d'un composant `.sep` dans SCADA Builder V2,
  `CreateElementPlusLibraryInstanceAsync` (`MainWindow.xaml.cs:4803-4863`) relit ce
  markup et le stocke comme `ScadaElementData.Text` sur un véritable élément de scène
  `ScadaElementKind.Custom` — ce n'est pas qu'un aperçu, ça devient le contenu réel de
  l'élément.
- À l'export, `Ft100SceneExporter.cs:948` réémet `data.Text` tel quel (seuls les ids
  sont rescopés et le ratio d'aspect SVG forcé — aucun filtrage de style) dans le HTML
  du `.sb2` consommé par TF100Web.

**Conclusion** : réduire la capture à uniquement les propriétés *explicitement
définies* sur l'élément (authored-only) romprait potentiellement la fidélité visuelle
pour tout style aujourd'hui correct uniquement parce qu'il est hérité ou calculé par
défaut (couleur héritée d'un parent, police par défaut, etc.) — régression visuelle
silencieuse possible dans les futurs `.sep`/`.sb2`.

**Décision** : garder `window.getComputedStyle()` (l'héritage reste résolu
correctement) mais filtrer la sérialisation avec une **liste blanche** de propriétés
pertinentes au rendu, au lieu de la liste noire actuelle de 7 propriétés
(`outline*`, `box-shadow`, `cursor`).

Aucun `.sep` ni `.sb2` existant n'est affecté rétroactivement — seules les
**nouvelles extractions** après le fix produiront un markup plus léger.

## 4. Design de la correction

### 4.1 Fix A — Liste blanche de propriétés CSS à la capture

**Fichier** : `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`, fonction
`toElementMessage` (lignes 543-559).

Remplacer :

```js
computedStyleText = Array.from(computed)
  .filter(name => !['outline', 'outline-color', 'outline-style', 'outline-width', 'outline-offset', 'box-shadow', 'cursor'].includes(name))
  .map(name => `${name}: ${computed.getPropertyValue(name)};`)
  .join(' ');
```

par un filtre par **inclusion** sur une liste de propriétés pertinentes au rendu
visuel des éléments legacy (formes SVG, texte/label, image, boîte) :

- Remplissage/trait : `fill`, `fill-opacity`, `fill-rule`, `stroke`, `stroke-width`,
  `stroke-dasharray`, `stroke-linecap`, `stroke-linejoin`, `stroke-opacity`
- Opacité/visibilité/affichage : `opacity`, `visibility`, `display`
- Texte : `font-family`, `font-size`, `font-weight`, `font-style`, `color`,
  `text-align`, `text-decoration`, `text-transform`, `letter-spacing`
- Boîte/fond/bordure : `background-color`, `background-image`, `border-color`,
  `border-width`, `border-style`, `border-radius`
- Effets : `filter` (confirmé utilisé pour des `drop-shadow` dans les données réelles
  de win00059)

La liste exacte sera finalisée pendant l'implémentation par comparaison de rendu
(§4.3) — celle-ci est un point de départ, pas une liste figée.

`transform`, `opacity`, `display`, `position`, `left`, `top`, `fill`, `stroke`,
`strokeWidth`, `zIndex` restent capturés séparément dans `rawMetadata` sans
changement.

### 4.2 Fix B — Filet de sécurité anti-crash (scope strict)

**Fichiers** : `src/ScadaBuilderV2.ElementStudio.App/App.xaml.cs`,
`MainWindow.xaml.cs` (méthode `OnLoaded`).

1. Ajouter un handler `Application.DispatcherUnhandledException` dans
   `App.xaml.cs` qui affiche le même message d'erreur que le `catch` existant dans
   `OnStartup` (au lieu de laisser WPF terminer le process silencieusement), puis
   marque `e.Handled = true` pour permettre à l'app de rester ouverte quand c'est
   sûr de le faire, ou de fermer proprement sinon.
2. Dans `OnLoaded`, avant l'appel à `LegacySourceWebView.NavigateToString(...)`,
   vérifier la taille du document HTML généré contre la limite connue de WebView2
   (2 097 152 caractères). Si dépassée :
   - Écrire le document dans un fichier temporaire.
   - Utiliser `LegacySourceWebView.CoreWebView2.Navigate(uri)` à la place (pas de
     limite de taille par ce chemin).
   - Ajouter un diagnostic visible à l'utilisateur (`workspace.Diagnostics`) signalant
     que le document a été chargé en mode "fichier" à cause de sa taille.

Ce fix B reste utile même après le fix A : il élimine la classe de bug (crash
silencieux sur exception non gérée dans un `async void`) pour toute scène future
encore plus volumineuse ou tout autre point de défaillance dans `OnLoaded`.

Hors scope (confirmé) : l'audit systématique des autres méthodes `async void` de
`MainWindow.xaml.cs` pour le même risque. Seul le point de crash confirmé est traité.

### 4.3 Validation de non-régression

1. Ré-extraire `win00059` après le fix A, ouvrir dans Element+ Studio → doit réussir
   sans crash avec les 152 items.
2. Convertir en `.sep`, exporter en `.sb2`, ouvrir le rendu HTML exporté dans un
   navigateur et comparer visuellement à l'état actuel (avant fix) du même lot
   d'éléments.
3. Refaire la même passe (extraction → `.sep` → `.sb2` → comparaison visuelle) sur
   **win00009** (scène de référence connue-bonne) pour confirmer l'absence de
   régression sur un cas déjà validé.
4. Tests automatisés :
   - `WebViewContextMenuScriptTests`-style : assertion sur la présence de la liste
     blanche dans le source JS et l'absence de l'ancien filtre par exclusion.
   - Nouveau test unitaire sur la logique de garde de taille avant
     `NavigateToString` (extraite dans une méthode testable indépendamment du
     contrôle WebView2).

## 5. Hors scope

- Revert du commit `9751349` : inutile, ses garde-fous null restent inoffensifs et
  ne nuisent pas au fix.
- Migration de `HtmlPreviewControl` (WebBrowser/IE legacy) vers WebView2 : problème
  distinct, non lié à ce crash, non traité ici.
- Audit exhaustif de tous les `async void` de l'application (voir §4.2).
