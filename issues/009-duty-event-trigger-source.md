# 009 — Duty-event trigger source (wipe / recommence)

## Parent PRD
[PRD.md](../PRD.md) — FR-2 upgrade-path item resolved into M2 (OQ-4 resolution, §12.4), §9 M2; DESIGN.md §3.4

## Type
AFK

## What to build
A small but complete fourth stable source: "when the duty wipes / restarts, alert me" (e.g. wipe → echo "reset CDs, B1 to the left").

- **Source**: `DutyEventSource` subscribing the same Dalamud duty-state events VoiceDirectorV2 already consumes (`DutyWiped`, `DutyRecommenced`); emits `TriggerEvent { Kind=DutyEvent, Event }`.
- **Core**: duty source spec (`Event` = Wiped | Recommenced | Either), trivial matcher, placeholder `{zone}` reused.
- **UI**: "When" section variant for Duty event (a single dropdown — deliberately the simplest source, good contrast case for the adaptive editor).
- **Tests**: matcher (event filter), spec round-trip. (The Dalamud event subscription itself is integration-layer, covered by the manual checklist.)

## What happens (behavior & data flow)
1. Dalamud raises `DutyWiped`/`DutyRecommenced` when the duty director signals those state changes to the client (the same events VoiceDirectorV2 uses for its wipe randomizer — proven reliable in your own ecosystem).
2. The source forwards a normalized event; matching rules fire the standard output pipeline (issues 002/006), respecting cooldowns (issue 005).

## Network traffic
**None initiated.** The duty director state arrives via the game's normal server connection; the plugin merely subscribes to Dalamud's already-decoded event and transmits nothing.

## Acceptance criteria
- In a duty, a wipe fires a "Wiped" rule (echo appears after the party is defeated); recommencing (barrier drops on retry) fires a "Recommenced" rule.
- `Either` fires on both (unit test on matcher).
- Rules with duty source respect cooldown and outputs like all others (engine-level test reuse).

## Blocked by
- [issues/004-regex-placeholders-error-states.md](004-regex-placeholders-error-states.md)

## User stories addressed
None of the five §5 rows directly — implements the PRD OQ-4 decision (raid-QoL: wipe/reset callouts) and completes the M2 stable-tier source set.
