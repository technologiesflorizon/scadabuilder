# SCADA Builder V2 - Known Gaps

Date: 2026-06-17
Status: Active known gaps register
Document version: `V2.1.2.0019`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-17 | `V2.1.2.0019` | `bd6515e` | Clarification que l'export `.sb2` ne ferme pas le gap runtime fragment TF100Web. |
| 2026-06-17 | `V2.1.2.0018` | `ad364a6` | Ajout du gap de parite entre runtime exporte SCADA Builder et intake fragment TF100Web. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Retrait du gap effets visuels standards; le styling custom reste roadmap. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Retrait du gap lifecycle runtime global; le chargement de scripts custom reste roadmap. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Retrait du gap conditions composees et politique degradee simple. |
| 2026-06-17 | `V2.1.2.0017` | `PENDING` | Retrait du gap politique popup avancee; le placement visuel authorable reste roadmap. |
| 2026-06-17 | `V2.1.2.0016` | `PENDING` | Retrait du gap hover group border; les effets visuels avances restent roadmap. |
| 2026-06-17 | `V2.1.2.0015` | `PENDING` | Retrait du gap actions popup close/toggle; les politiques avancees restent roadmap. |
| 2026-06-17 | `V2.1.2.0014` | `PENDING` | Retrait du gap `On click -> open popup`; les options avancees de popup restent roadmap. |
| 2026-06-17 | `V2.1.2.0012` | `PENDING` | Retrait du gap d'application runtime des valeurs lues; les reponses degradees restent roadmap. |
| 2026-06-17 | `V2.1.2.0010` | `PENDING` | Clarification que les conditions simples sont implementees pour actions objet, tandis que degrade/expressions restent roadmap. |
| 2026-06-17 | `V2.1.2.0009` | `PENDING` | Retrait du gap binding valeur importe et ajout du gap import protocoles pour creation locale de tags. |
| 2026-06-17 | `V2.1.2.0008` | `PENDING` | Remplacement du gap schema tags global par les limites restantes apres import catalogue et `WriteTag`. |
| 2026-06-16 | `V2.1.2.0005` | `PENDING` | Clarification que les metadonnees hover bouton sont implementees, tandis que l'application runtime appartient a FT100Web. |
| 2026-06-16 | `V2.1.2.0004` | `PENDING` | Clarification des limites restantes apres la premiere tranche Evenement Element+. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du registre des ecarts connus. |

## 1. Known Gaps

1. Full migration from legacy top-level documentation into the new owner documents is not complete.
2. Public C# XML documentation coverage is not yet enforced as a failing build gate.
3. Visual authoring for popup placement, expression/formula conditions, custom effect styling, local effect preview, and controlled custom script loading remain roadmap unless tests prove otherwise.
4. Rich Element+ button hover runtime interpretation belongs to TF100Web. SCADA Builder V2 exports metadata and scoped CSS, but the editor preview does not apply the hover behavior locally.
5. Expression binding and SCADA Builder-side tag creation remain roadmap. Local tag creation depends on a future project protocol import revision.
6. The final `FT100`, `TF100Web`, and `tf100-web` naming convention remains to be decided.
7. Sanitized-source approval for divergent pages such as `win00008` remains unresolved.
8. TF100Web commit `7d57600` extracts only page root fragments and does not execute SCADA Builder exporter-emitted page scripts. Exporter-side lifecycle, popup, condition, read/write tag page hooks, border/effect, and non-navigation action runtimes must be treated as TF100Web parity gaps until host-side intake support exists.
9. `.sb2` archive export validates import/package compatibility only. It does not make TF100Web execute SCADA Builder page scripts that remain outside the extracted root fragment.

## 2. Rule

Known gaps must not be documented as implemented behavior.
