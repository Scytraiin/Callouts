# 007 — Cast-start trigger source

## Parent PRD
[PRD.md](../PRD.md) — FR-2.2, acceptance §10.3 (cast part, incl. recast); DESIGN.md §3.2

## Type
AFK

## What to build
The second trigger source, end-to-end: "when an enemy starts casting *X*, alert me."

- **Source**: `CastSource` polling `IObjectTable` on `IFramework.Update`, reading `IsCasting`, `CastActionId`, `CastTargetObjectId`, `TotalCastTime` per `IBattleChara`. **Cast-session diffing** per DESIGN.md §3.2 (review-hardened): an actor's map entry clears whenever `IsCasting == false`; emit when casting and (entry empty OR different action) — so back-to-back recasts of the same spell fire correctly. Map entries pruned for EntityIds absent from the current sweep; cleared on territory change.
- **Core**: cast source spec (`ActionId` or `ActionNameContains`, `CasterFilter` = AnyEnemy | NameContains, `OnlyTargetingMe`, `OnlyTargetingParty`), `CastTriggerMatcher`, new placeholders `{caster}`, `{action}`, `{target}`.
- **Lumina**: action-name resolution via the `Action` sheet (client language automatically); a searchable action picker in the editor (name → id), raw-id entry still possible.
- **UI**: the editor's "When" section re-renders for source kind Cast (first proof of the adaptive-editor design, DESIGN.md §7.2).
- **Docs**: the cast-bar limitation (instant abilities invisible — PRD FR-2.2 known limitation) noted in the editor's help text.
- **Tests**: session-diff logic (**cast → idle → same cast fires twice**; continuous same cast fires once; action change mid-bar fires), pruning behavior, matcher filters (caster name, targeting flags), placeholder values.

## What happens (behavior & data flow)
1. Every framework update (once per rendered frame), one sweep over the object table reads each battle character's cast fields — this is reading the client's already-synchronized actor state from process memory, the same data the game uses to draw cast bars.
2. The diff map converts continuous state into discrete "cast started" events; typical raid pulls produce a handful of events per second at most.
3. A matching rule renders placeholders (e.g. `"{caster} is casting {action} — mit now"`) and dispatches to the configured sinks (issues 002/006).
4. Performance envelope per PRD NFR-1: diff maps keyed by EntityId, no allocations beyond Dalamud's wrappers on the no-change path (DESIGN.md §9).

## Network traffic
**None initiated.** The source passively reads actor state that the game client already received through its normal server connection; the plugin adds no requests, no packet hooks (this is the polling-based *stable* tier), and transmits nothing.

## Acceptance criteria
- US-3 works in-game: rule "cast name contains `Ultima`" fires echo + sound when a boss starts the cast.
- A boss recasting the same spell back-to-back fires the rule twice (unit test on the session diff; in-game spot check per README checklist).
- `OnlyTargetingMe` suppresses fires when the cast targets someone else (unit test).
- Action picker finds actions by localized name; raw-id entry works.
- No measurable frame-time regression with the source active outside combat (informal check via Dalamud's plugin statistics).

## Blocked by
- [issues/004-regex-placeholders-error-states.md](004-regex-placeholders-error-states.md) — placeholder system and adaptive-editor/inline-validation infrastructure.

## User stories addressed
- US-3 (duty player — "Ultima incoming" — complete when combined with issue 006 outputs)
