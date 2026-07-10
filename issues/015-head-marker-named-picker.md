# 015 — Head-marker source with named-marker picker

## Parent PRD
[PRD.md](../PRD.md) — FR-2.5, OQ-2 (**decision required**), acceptance §10.4; DESIGN.md §3.6

## Type
**HITL** — requires the OQ-2 mechanism decision before implementation.

## What to build
The user-friendly head-marker trigger: pick "spread" or "stack" from a named list instead of knowing VFX paths or icon ids.

**The HITL decision (PRD OQ-2), to be made first with the maintainer:**
- **Option A — VFX-path matching**: reuse issue 014's hook, match `vfx/lockon/eff/*`; requires shipping and maintaining a **named-marker ↔ VFX-path mapping table** (one data file, a deliverable of this option).
- **Option B — ActorControl hook**: hook the network-message handler for the head-marker opcode; yields numeric icon ids (more precise, what ACT/BossMod use); mapping table keys on icon ids; one more signature to maintain.

Decision inputs: reliability of path matching across markers, signature maintenance budget, and whether tethers (post-1.0) would need ActorControl anyway — deciding B here amortizes that.

**Then, either way:**
- **Source**: `HeadMarkerSource` emitting `TriggerEvent { Kind=HeadMarker, MarkerKey, RawValue, TargetIsSelf, TargetInParty }`; advanced-tier gated with its own health line (machinery from issue 014).
- **Data**: the curated mapping table (starter set: spread, stack, enumeration, flare, defamation-style markers) + raw-value entry for unmapped markers.
- **UI**: named-marker picker in the editor; markers render human-readably in the Live events feed (`✨ Marker "spread" → YOU`).
- **Tests**: mapping lookup (named + raw fallback), matcher scopes, feed formatting; mechanism internals via manual checklist.

## What happens (behavior & data flow)
1. A raid mechanic assigns a marker: the server informs the client (ActorControl message), and the client attaches a lock-on VFX to the actor. Option A observes the second step, Option B the first — both are **passive observation of data the client already received**; the original handler always runs unchanged.
2. The source translates the raw observation into a stable `MarkerKey` via the mapping table, so user rules survive mechanism changes (the abstraction promised by FR-2.5).
3. Rules like "spread on **me** → toast SPREAD + sound 11" fire through the standard pipeline.

## Network traffic
**None initiated, none modified.** Option B hooks the client's *handler* for an already-received message type — read-only interception with pass-through; no packets are crafted, altered, dropped, or sent. Option A doesn't touch the network layer at all.

## Acceptance criteria
- OQ-2 decision documented (in this issue file and DESIGN.md §3.6/§11) with rationale.
- US-2 complete in-game (acceptance §10.4): "spread on me" rule created via the named picker fires toast + sound during a real marker mechanic.
- An unmapped marker still surfaces in the Live events feed with its raw value, and `[＋]` creates a working raw-value rule.
- Mapping table is a single data file with a documented update procedure (patch-day playbook reference).
- Source participates fully in advanced-tier gating/health/notification from issue 014.

## Blocked by
- [issues/014-advanced-tier-vfx-source.md](014-advanced-tier-vfx-source.md)

## User stories addressed
- US-2 (progression raider spread marker — complete)
