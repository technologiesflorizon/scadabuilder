# Element+ State & Command Runtime Contract (V1)

Date: 2026-07-17
Status: Active implemented runtime contract
Document version: `V2.1.4.0063`
Owner: SCADA Builder V2 authoring team and shared package runtime. TF100Web owns host services only (`F:\Projet\Git\TF100Web`).

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-17 | `V2.1.4.0063` | Builder `6603992`, TF100Web `f9afcba` | Casing AST ferme : lower-camel canonique exporte execute directement, PascalCase historique accepte, probes exacts pour chaque operateur. |
| 2026-07-16 | `V2.1.4.0062` | `370641d` | Contrat synchronise avec le runtime partage, le HostAdapter unique, la fixture exacte et les statuts Supported/Blocked stricts. |
| 2026-07-16 | `V2.1.4.0052` | `a76e220` | CommandConfig complete : 5 triggers, 7 kinds, 4 modes, intent 1.0, Momentary reel, confirmations et cleanup. |
| 2026-07-16 | `V2.1.4.0051` | `9878fb1` | Semantiques Etat/Expression/Effet partagees completees et table-driven : fallback, erreurs, coercions, transitions, tokens, animations et re-init. |
| 2026-07-16 | `V2.1.4.0046` | `b2e4f5f` | `DEC-0047` approuvee : couverture exhaustive de chaque trigger/kind/mode/expression/effet et migration des actions vers l'executeur partage unique. |
| 2026-07-16 | `V2.1.4.0043` | `8489dbd` | Contrat Etat/Commande confirme dans TF100Web : runtime partage deploye, mappings Etat/Commande collectes, boutons avec cible texte semantique et 56 toggles de degivrage configures. |
| 2026-07-07 | `V2.1.2.0023` | `PENDING` | Creation du contrat runtime state/command V1 (specification, non implemente). |

## 1. Purpose

Defines the serialized format and evaluation semantics implemented once by the shared
package runtime. SCADA Builder V2 authors and validates this data; TF100Web negotiates the
declared capabilities and supplies host services without reimplementing expressions,
effects, commands or object actions.

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
{ "type": "unary", "op": "not" | "negate", "operand": { "...": "node" } }
{ "type": "binary", "op": "add|subtract|multiply|divide|modulo|equal|notEqual|lessThan|lessThanOrEqual|greaterThan|greaterThanOrEqual|and|or", "left": {}, "right": {} }
{ "type": "func", "name": "ABS|MIN|MAX|BIT", "args": [ { "...": "node" } ] }
```

The package serializer's canonical operator spelling is lower camel case. The runtime
normalizes the first character so historical Pascal-case ASTs remain compatible without
creating a second evaluation path.

`BIT(tag, n)` returns the boolean value of bit `n` (0-indexed, least significant bit) of
the integer value of `tag`; `n` must be an integer from 0 through 31. Arithmetic and
ordered comparisons accept finite numbers, booleans and non-empty numeric strings.
Empty strings, non-finite values, invalid arity, unknown nodes/operators/functions and
division/modulo by zero return `null` and raise the evaluation error flag. Equality first
uses exact value/type equality, then finite numeric equivalence; two non-numeric strings
match only when their exact values match. `And`/`Or` use boolean coercion with normal
short-circuiting, while an unavailable operand propagates `null` when it is required.

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
- A command with `enabled = false` binds no trigger and cannot execute through the public
  dispatcher. Unknown triggers, kinds/modes, missing write values and an unavailable Toggle
  read value fail closed with diagnostics; they never infer a potentially unsafe write.
- `Momentary` binds real pointer/keyboard press and release phases. Pointer cancel, lost
  capture, window blur, explicit unbind, page disposal and detected DOM removal all release
  an energized command. An asynchronous confirmation accepted after release never writes
  `onValue`; an asynchronous accepted press is settled before `offValue` is sent.
- `TagBridge.writeTag` returns the host permission/write result. A command object permits one
  in-flight write, propagates rejection and leaves display state to the next confirmed
  snapshot rather than optimistically changing the host value.
- Navigation, popup, URL and history commands produce the versioned `scada-runtime-intent`
  1.0 envelope. Host behavior is provided by one adapter and is not duplicated in this
  module. Writes remain on the single TagBridge path.

## 6. Strict capability status

Manifest 2.3 exports are fail-closed. The generated capability matrix is authoritative:

1. `Supported` means Builder/export evidence, shared-runtime execution evidence and TF100Web conformance evidence are all present.
2. `Blocked` means the strict Builder exporter and TF100Web deployment validator reject the capability before the active package is replaced.
3. Implemented portable code alone does not promote a capability. In particular, animations, Momentary commands and host-dependent action/popup variants remain blocked until their complete end-to-end evidence is promoted in the registry.
4. Compatibility profiles 2.1/2.2 remain explicit legacy paths; they do not weaken the 2.3 gate.

## 7. Non-goals for this contract version

- No live simulator wire format — the Builder's static preview never calls this runtime.
- No stale-timeout quality detection.
- No functions beyond `ABS/MIN/MAX/BIT`.
- No cumulative multi-animation composition beyond the single `animation` field.

## 8. Implementation status

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

The shared State/Expression/Effect, CommandConfig and object-action paths have executable table-driven evidence. Command coverage includes every trigger/kind/write mode, enabled/disabled, missing inputs, confirmation timing, asynchronous rejection, duplicate suppression, real Momentary release and page/input cleanup. Input locks are keyed by DOM identity, refresh inactivity on edits and restore from the declared read tag.

Builder commits `9878fb1`, `a76e220` and `bcec075` own the portable semantics. TF100Web commits `7d60c63`, `cab2733`, `1fc3ac4` and `2fb46e6` negotiate manifest 2.3, expose one HostAdapter, enforce latest-wins lifecycle and execute the exact Builder fixture. The fixture gate proves all 118 `Supported` capabilities and rejects all 44 `Blocked` capabilities. The V2.1.4.0063 industrial package SHA-256 `4bdaa0338746be1ac440f2adeae6a5c3e6f8c80946dc208167b9edfbeec7dc88` validates the deployed contract shape without performing PLC writes. Remote server promotion and operator smoke remain release operations, not missing runtime semantics.
