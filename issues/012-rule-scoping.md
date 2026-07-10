# 012 — Rule scoping: zones, duty, combat

## Parent PRD
[PRD.md](../PRD.md) — FR-7 (Must); DESIGN.md §4.1 (Scope block)

## Type
AFK

## What to build
Per-rule scoping conditions, end-to-end: a rule can be limited to specific zones/duties, to "only in duty", and/or "only in combat".

- **Core**: `Scope { TerritoryIds[], OnlyInCombat, OnlyInDuty }` evaluated as a gate before matching (cheap reject); a `GameContext` snapshot (current territory id, in-combat flag, in-duty flag) supplied per event by the adapter layer so the core stays Dalamud-free and testable.
- **Adapters**: context snapshot fed from `IClientState` (territory, combat condition flags) — updated on change, not per event.
- **UI**: "Options" row of the editor per DESIGN.md §7.2 — searchable territory picker (Lumina `TerritoryType` names, e.g. "AAC Heavyweight M1 (Savage)"), plus the two checkboxes. Rule list can group by zone (foundation for issue 013's group-by).
- **Tests**: scope gate combinations (zone match/mismatch × combat × duty), empty-scope = everywhere, context-snapshot injection.

## What happens (behavior & data flow)
1. Each `TriggerEvent` is paired with the current `GameContext`; scoped rules short-circuit before pattern matching when the context doesn't fit (performance win for users with many raid-specific rules — PRD NFR-1).
2. Territory changes update the snapshot once (event-driven), not per rule per event.
3. Example: a "spread marker" rule scoped to one savage tier is completely inert — zero matching cost — while the user runs roulettes.

## Network traffic
**None.** Territory/combat state is client state already held in memory.

## Acceptance criteria
- A zone-scoped rule fires in its zone and is inert elsewhere (in-game check both ways).
- "Only in combat" suppresses out-of-combat fires (in-game check with a `/say` pattern).
- Scope gates are unit-tested across the full combination matrix; empty scope preserves pre-issue behavior for all existing rules (config migration not required — absent scope defaults to everywhere).
- Territory picker searches by localized zone name.

## Blocked by
- [issues/004-regex-placeholders-error-states.md](004-regex-placeholders-error-states.md)

## User stories addressed
- US-2 and US-3 (raid rules stop firing in unrelated content — quality-of-life completion of those stories' real usage)
