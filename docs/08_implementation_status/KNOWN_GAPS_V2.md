# SCADA Builder V2 - Known Gaps

Date: 2026-06-17
Status: Active known gaps register
Document version: `V2.1.2.0008`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-17 | `V2.1.2.0008` | `PENDING` | Remplacement du gap schema tags global par les limites restantes apres import catalogue et `WriteTag`. |
| 2026-06-16 | `V2.1.2.0005` | `PENDING` | Clarification que les metadonnees hover bouton sont implementees, tandis que l'application runtime appartient a FT100Web. |
| 2026-06-16 | `V2.1.2.0004` | `PENDING` | Clarification des limites restantes apres la premiere tranche Evenement Element+. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du registre des ecarts connus. |

## 1. Known Gaps

1. Full migration from legacy top-level documentation into the new owner documents is not complete.
2. Public C# XML documentation coverage is not yet enforced as a failing build gate.
3. `On click -> open popup`, hover group border, conditional authoring, tag conditions, degraded semantics, SCADA Builder-side visual effects, and global lifecycle scripts remain roadmap unless tests prove otherwise.
4. Rich Element+ button hover runtime interpretation belongs to TF100Web. SCADA Builder V2 exports metadata and scoped CSS, but the editor preview does not apply the hover behavior locally.
5. Tag read/display bindings, Element+ input value binding, expression binding, and degraded tag semantics remain roadmap. The implemented tag slice covers project catalog import and literal `WriteTag` event actions only.
6. The final `FT100`, `TF100Web`, and `tf100-web` naming convention remains to be decided.
7. Sanitized-source approval for divergent pages such as `win00008` remains unresolved.

## 2. Rule

Known gaps must not be documented as implemented behavior.
