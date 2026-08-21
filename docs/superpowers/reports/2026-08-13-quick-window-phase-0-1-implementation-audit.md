# Audit d’implémentation Fenêtres rapides — Phases 0 et 1

Date: 2026-08-13
Status: PASS après corrections — Phases 0 et 1 conformes; Phase 2 non démarrée
Document version: `V2.1.5.0021`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-08-13 | `V2.1.5.0021` | `b353e37` | Audit du commit de Phase 1, correction des preuves Phase 0 et des écarts Domain/persistance/handshake de Phase 1. |

## 1. Portée et conclusion

L’audit compare l’état de la branche au contrat `DEC-0050`, à la spécification propriétaire et aux Tasks `0.1` à `1.5` du plan. Les écarts bloquants et majeurs trouvés ont été corrigés. La branche est maintenant conforme au périmètre suivant:

- Phase 0: fixture d’isolation réelle, gelée et exécutée dans WebView2 et TF100Web;
- Phase 1: contrats Domain, validation typée, refus legacy, persistance déterministe et handshake cross-repository;
- capacités QuickWindow: toujours `Blocked` et invisibles dans l’authoring courant;
- Phase 2 et suivantes: non implémentées.

## 2. Écarts constatés et corrections

| Sévérité | Écart audité | Correction appliquée | Preuve |
| --- | --- | --- | --- |
| Bloquant | Rapport Phase 0 fondé sur un runtime simulé, Node 24 et des assertions permissives/statistiques. | Fixture `1.0.2`, assertions DOM strictes, Node `20.18.1`, WebView2 réel, Edge headless réel, hash partagé. | Node Builder `5/5`; WebView2 `32/32`; Node TF `3/3`; pytest TF isolation `6/6`. |
| Bloquant | Le bootstrap WebView2 mélangeait modules et scripts classiques et expirait sans exécuter le scénario. | Chargement classique cohérent et appels publics corrects. | Harness WebView2 `PASS`, runtime `151.0.4129.78`. |
| Majeur | `InterfaceVersion` n’était pas transportée ni comparée par invocation. | Version ajoutée au contrat et validation d’égalité définition/invocation. | `QuickWindowBindingTests`, handshake généré. |
| Majeur | `CloseQuickWindow` pouvait être authoré hors contenu QuickWindow. | Rejet projet/page systématique et validation `Self` implicite dans une définition. La commande est masquée dans le dialogue de page. | `QuickWindowBindingTests`. |
| Majeur | Familles, accès, valeurs par défaut, expressions, tags et types pouvaient être ambigus ou permissifs. | Validation fail-closed par famille/type/accès, parseur d’expression réel, catalogue obligatoire et anti-injection structurée. | `QuickWindowDomainTests`, `QuickWindowBindingTests`. |
| Majeur | Les membres privés pouvaient apparaître dans les liaisons d’invocation. | Membres privés réservés à l’état interne et exclus du handshake. | `QuickWindowBindingTests`, fixture handshake. |
| Majeur | La persistance dupliquait les définitions dans `project.json` et n’assurait pas toutes les écritures atomiques. | `quick-windows/<DefinitionKey>.quick-window.json` devient autoritaire; manifest sans duplication; temp + flush + remplacement atomique. | `QuickWindowStoreTests`. |
| Majeur | Handshake écrit manuellement, hash SHA vide et mutations non exécutées côté TF100Web. | Fixture générée depuis les records C#, SHA canonique réel et validateur/mutations exécutables dans les deux dépôts. | Builder `4/4`; TF100Web handshake `5/5`. |
| Information | Le commit audité contient aussi des données de référence sans rapport avec QuickWindow. | Ces données utilisateur sont conservées hors portée; la correction n’ajoute ni définition ni invocation QuickWindow à un projet durable. | Tests byte-for-byte des projets sans QuickWindow. |

## 3. Conformité Phase 1

Les contrats corrigés assurent:

1. une `QuickWindowDefinition` distincte, un `VisualContent` borné et des defaults de présentation V1;
2. exactement trois identités: définition, invocation et instance runtime temporaire;
3. des invocations versionnées et propriétaires, des liaisons publiques typées et l’absence explicite;
4. le rejet des profils 2.1/2.2 et l’acceptation contractuelle 2.3 sans promotion runtime;
5. le refus diagnostique des anciens command kinds popup sans migration silencieuse;
6. des fichiers de définition autoritaires, ordonnés, atomiques et validés par leur nom de fichier;
7. un handshake Builder → TF100Web déterministe avec SHA `f17d150f8d9f428fbf2451fe08106380c656bc20fb5f74f294f20bcfc85c9131`.

## 4. Déviation de séquencement

La Phase 1 avait été commencée avant qu’une preuve Phase 0 valide existe. L’audit ne masque pas cette déviation historique: il invalide l’ancien PASS, corrige et rejoue Phase 0, puis réaudite Phase 1. Les données de référence sans rapport avec QuickWindow sont préservées et ne servent pas de preuve à cette phase. L’état final respecte les gates techniques, mais les commits antérieurs restent traçables dans l’historique Git.

## 5. Résiduel explicitement bloqué

Les services Application, dépendances, undo/redo, UI dédiée, preview produit, compilateur partagé, export manifest 2.3 QuickWindow, runtime partagé, host TF100Web et acceptance `win00054` appartiennent aux Phases 2 à 7. Aucun de ces comportements n’est revendiqué comme livré. Toutes les capacités QuickWindow demeurent `Blocked` jusqu’aux preuves trilatérales exigées par `DEC-0047`.
