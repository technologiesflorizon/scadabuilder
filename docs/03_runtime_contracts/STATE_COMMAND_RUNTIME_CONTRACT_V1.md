# Element+ State & Command Runtime Contract (V1)

Date: 2026-07-16
Status: Active implemented runtime contract
Document version: `V2.1.4.0043`
Owner: SCADA Builder V2 authoring team. Runtime implementation owner: TF100Web team (`F:\Projet\Git\TF100Web`).

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-16 | `V2.1.4.0043` | `PENDING` | Contrat Etat/Commande confirme dans TF100Web : runtime partage deploye, mappings Etat/Commande collectes, boutons avec cible texte semantique et 56 toggles de degivrage configures. |
| 2026-07-07 | `V2.1.2.0023` | `PENDING` | Creation du contrat runtime state/command V1 (specification, non implemente). |

## 1. Purpose

Defines the serialized format and evaluation semantics that TF100Web must implement to
render Element+ display-state rules and execute Element+ commands at runtime. SCADA
Builder V2 authors and validates this data; TF100Web is the deployed runtime consumer.

Design source: `docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md`.

## 2. Serialized shapes (JSON, per Element+)

```json
{
  "stateConfig": {
    "qualityFallback": { "opacity": 0.4, "borderColor": "#000000", "borderWidth": 2 },
    "defaultEffect": {},
    "states": [
      {
        "id": "state-1",
        "name": "Alarme haute",
        "enabled": true,
        "expression": { "source": "{Temp} > 80", "ast": { "...": "see §3" } },
        "effect": { "backgroundColor": "#E53935", "animation": "Blink" }
      }
    ]
  },
  "commandConfig": {
    "commands": [
      {
        "id": "cmd-1",
        "name": "Demarrer pompe",
        "enabled": true,
        "trigger": "OnClick",
        "kind": "WriteTag",
        "writeTagId": "tag-cmd-start",
        "readTagId": null,
        "writeMode": "Toggle",
        "confirmation": { "message": "Demarrer la pompe ?" }
      }
    ]
  }
}
```

Effect block properties (`backgroundColor`, `borderColor`, `borderWidth`, `textColor`,
`textContent`, `textVisible`, `elementVisible`, `opacity`, `rotation`, `animation`,
`colorFilterColor`, `colorFilterOpacity`, `colorFilterHalo`, `colorFilterHaloColor`) are all
optional; an absent/null property means "leave current appearance unchanged for this
property" — TF100Web must not default it, only skip applying it.

`textContent` targets a descendant marked `[data-scada-text]`. Text and button renderers
use this same semantic target; TF100Web does not maintain a button-specific text branch.

## 3. Expression AST format

The AST, not `expression.source`, is authoritative at runtime. Node shapes:

```json
{ "type": "literalNumber", "value": 80 }
{ "type": "literalBool", "value": true }
{ "type": "literalString", "value": "text" }
{ "type": "tagRef", "tagName": "Temp" }
{ "type": "unary", "op": "Not" | "Negate", "operand": { "...": "node" } }
{ "type": "binary", "op": "Add|Subtract|Multiply|Divide|Modulo|Equal|NotEqual|LessThan|LessThanOrEqual|GreaterThan|GreaterThanOrEqual|And|Or", "left": {}, "right": {} }
{ "type": "func", "name": "ABS|MIN|MAX|BIT", "args": [ { "...": "node" } ] }
```

`BIT(tag, n)` returns the boolean value of bit `n` (0-indexed, least significant bit) of
the integer value of `tag`.

## 4. Evaluation semantics (must match exactly)

```
1. LISTE  → iterate states top to bottom:
     • if any tag referenced by THIS state's expression is null (unavailable)
         → SKIP this state, continue to the next.
     • otherwise evaluate the expression:
         - true              → apply this state's effect. STOP. (first-match-wins)
         - false             → continue.
         - evaluation error (e.g. runtime division by zero)
                              → treat as false, raise the error flag (§5), continue.

2. ALL STATES UNEVALUABLE → if every state was skipped in step 1 (all had >= 1 null tag,
     or there are zero states) → apply qualityFallback. STOP.

3. DEFAULT → some states were evaluable but none matched → apply defaultEffect. STOP.

4. ERROR FLAG (cross-cutting, non-blocking):
     • if any expression raised an evaluation error during the pass
         → render a small error badge/overlay on the element
         → any textContent driven by the applied effect becomes "---" instead of its
           interpolated value.
     • does not prevent the match/default effect from being applied.
```

"Null" = the tag has never been read, or TF100Web's own quality/connectivity flag marks
it unavailable. There is no stale-timeout in V1 — a value refreshed at any point in the
past is not null.

## 5. Command execution semantics

- `WriteTag` + `Momentary`: write `onValue` on trigger-press, `offValue` on release.
- `WriteTag` + `Toggle`: read current value of `readTagId` (fallback: `writeTagId`), write
  its logical negation to `writeTagId`.
- `WriteTag` + `SetFixed`: write `fixedValue` verbatim on trigger.
- `WriteTag` + `SetFromInput`: write the operator-entered runtime value (no design-time
  value is stored).
- `Navigate`/`OpenPopup`/`TogglePopup`/`ClosePopup`/`OpenUrl`/`Back`: same semantics as the
  existing `ScadaActionKind.Navigate`/`MountFragment`/`TogglePopup`/`ClosePopup` runtime
  behavior described in `docs/04_editor/ACTIONS_EVENTS_CONTRACT_V2.md` §8, §12; `OpenUrl`
  and `Back` are new and have no prior runtime behavior to match.
- If `confirmation` is present, TF100Web must show `confirmation.message` and require
  operator acknowledgement before executing the command's effect.

## 6. Non-goals for this contract version

- No live simulator wire format — the Builder's static preview never calls this runtime.
- No stale-timeout quality detection.
- No functions beyond `ABS/MIN/MAX/BIT`.
- No cumulative multi-animation composition beyond the single `animation` field.

## 7. Implementation status

Implemented by the shared package runtime deployed by TF100Web. The active chain is
`ScadaTagCache -> TagBridge -> StateEngine / CommandDispatcher -> EffectApplier / writeTag`.
TF100Web loads `static/scada/js/scada-runtime.js`, initializes each composed fragment and
polls the mappings referenced by value bindings, `data-scada-state-config` and
`data-scada-command-config`. State, command and binding ids are normalized and deduplicated
before each snapshot request.

`DEC-0044` validates the contract on 56 `win00012_modern_no_legacy` defrost buttons. Their
true/false states drive green/red 70% filters and `ACTIF`/`ARRÊTÉ` labels from the confirmed
PLC bit. Toggle writes continue through the existing TF100Web bridge and are reflected only
after the shared snapshot reports the resulting value.
