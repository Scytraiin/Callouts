# 006 — Sound & toast outputs, per-rule output configuration

## Parent PRD
[PRD.md](../PRD.md) — FR-4 (complete), OQ-1 (resolution), acceptance §10.6 (toast part); DESIGN.md §5

## Type
AFK

## What to build
The remaining two output channels and the full per-rule output configuration, completing FR-4's "any combination" contract:

- **Sinks**: `SoundSink` calling FFXIVClientStructs `UIGlobals.PlayChatSoundEffect(id)` with **1-based ids 1–16** (verified used by Dalamud's own source — `SeStringCreatorWidget.cs:1054`; PRD OQ-1). `ToastSink` via `IToastGui.ShowNormal/ShowQuest/ShowError` with placeholder-rendered text.
- **Core**: `OutputSpec` grows sound (EffectId 1–16) and toast (text + style) alongside echo; validation enforces ≥1 output enabled; `PlaceholderRenderer` applies to toast text identically to echo text.
- **UI**: the editor's "Then" section per DESIGN.md §7.2 — three toggle rows (Echo / Sound with ♪ dropdown + **Preview** button / Toast with style dropdown + **Test** button); rule list output-summary column (e.g. `echo+♪6+toast`).
- **Fallback documentation**: if in-game smoke testing shows `PlayChatSoundEffect` unusable, implement the documented degraded mode (DESIGN.md §5): `<se.N>` payload for rules with Echo enabled; sound-only rules show a UI notice. (Not expected to be needed.)
- **Tests**: output validation (≥1 enabled, EffectId bounds, style enum), placeholder rendering into toast text, output-spec config round-trip.

## What happens (behavior & data flow)
1. A fired rule now produces up to three `AlertAction`s, dispatched to their sinks in order (echo → sound → toast); each sink is exception-isolated so one failing output never blocks the others (DESIGN.md §5).
2. Sound: a direct in-process call into the game's own UI sound player — the same routine the game uses for `<se.N>` chat macros. Nothing is added to chat.
3. Toast: the game's native popup UI (same visual as quest/system toasts), client-local.
4. The Preview/Test buttons in the editor call the sinks directly with sample text, so users can hear/see an output before saving (part of the 60-second golden path, PRD §10.1).

## Network traffic
**None.** Sound playback and toasts are client-local API calls into the game's UI module; no packets are generated. (The game never transmits Echo-channel/UI-local activity to servers.)

## Acceptance criteria
- US-1 complete in-game: ready-check rule with echo + sound 6 produces both simultaneously; adding a toast shows all three.
- Sound preview plays each of ids 1–16 correctly (manual pass; confirms the 1-based mapping).
- A rule with only sound enabled (no echo) works — proving OQ-1's primary API path (or the documented degraded-mode notice appears, if fallback was required).
- Placeholders render in toast text identically to echo text (unit test, acceptance §10.6).
- Saving a rule with all outputs disabled is blocked with an inline reason (unit + UI check).

## Blocked by
- [issues/002-chat-contains-to-echo.md](002-chat-contains-to-echo.md)

## User stories addressed
- US-1 (raid lead ready-check — complete: echo + sound)
- US-4 (buff watcher — toast output now exists; the status source arrives in issue 008)
