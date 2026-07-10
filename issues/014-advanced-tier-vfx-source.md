# 014 — Advanced tier: VFX trigger source behind the master toggle

## Parent PRD
[PRD.md](../PRD.md) — FR-2.4, FR-3 (complete, incl. active failure notification), NFR-3, acceptance §10.4/§10.5; DESIGN.md §3.5, §7.0, §10

## Type
AFK

## What to build
The first hook-based source and the entire advanced-tier machinery around it, end-to-end:

- **Source**: `VfxSource` using `IGameInteropProvider.HookFromSignature<T>` (Reloaded-backed by default — DESIGN.md §3.5) on the game's actor-VFX-create function; extracts the VFX path string + target actor; emits `TriggerEvent { Kind=Vfx, VfxPath, TargetName, TargetIsSelf, TargetInParty }`. The signature lives as a single documented constant.
- **Gating (FR-3)**: "Enable advanced sources" toggle in Settings, **default off**; hook installed only while on, uninstalled on toggle-off; per-source health line (Active / Failed / Disabled).
- **Failure handling**: install failure → source status `Failed`, affected rules → runtime state `SourceFailed` (badges + banner from issue 013 light up), **one-time-per-session active notification** at load: Echo line + error toast naming the source and the count of inactive rules (PRD FR-3; the review's patch-day finding).
- **Core/UI**: VFX source spec (path contains/regex, actor scope Self/Party/Anyone), editor "When" variant, VFX events in the Live events feed (this is how users discover paths like `vfx/lockon/eff/…`).
- **Tests**: gating state machine (toggle × install success/failure → source status × rule runtime states), notification once-per-session logic, matcher (path modes, actor scopes). Hook internals are covered by the manual checklist (needs the live game).

## What happens (behavior & data flow)
1. When enabled, the hook intercepts the client function that spawns an actor-attached VFX (the visual the user described as "a circle above his head"); the plugin reads the path + target, **calls the original function unchanged**, and returns — observation only, no behavior modification (DESIGN.md §2; the same event Splatoon exposes as `OnVFXSpawn`).
2. Normalized events flow through the identical engine/gates/sinks pipeline as every stable source.
3. On a game patch that breaks the signature: install throws at load → the failure path above runs → the stable tier is completely unaffected (PRD NFR-3; acceptance §10.5). Recovery = plugin update (README patch-day section, DESIGN.md §10).

## Network traffic
**None initiated, none modified.** The hooked function is a *client-side rendering* routine operating on data the game already received; the plugin does not read, craft, inject, or suppress any network packets, and the hook never alters game behavior (original always invoked). Nothing is transmitted by the plugin.

## Acceptance criteria
- Acceptance §10.4: with the toggle on, a rule "path contains `vfx/lockon/eff/` on YOU" fires when a lock-on marker appears on the player (in-game, e.g. duty with a targeted marker mechanic).
- Toggle off → hook uninstalled (health shows Disabled), VFX rules show blocked badges, stable rules unaffected — then back on restores without reload (acceptance §10.5 first half).
- Simulated install failure (temporarily broken signature in a dev build): one Echo + one error toast at load with the correct rule count; source shows FAILED; user `Enabled` flags untouched on disk (acceptance §10.5).
- VFX events appear in the Live events feed with `[＋]` pre-fill producing a working rule.
- Gating/notification state machine fully unit-tested.

## Blocked by
- [issues/006-sound-toast-outputs.md](006-sound-toast-outputs.md)
- [issues/010-live-events-create-from-event.md](010-live-events-create-from-event.md)
- [issues/013-rules-list-scale-master-enable.md](013-rules-list-scale-master-enable.md) — badge/banner surfaces

## User stories addressed
- US-2 (progression raider — partial: raw VFX-path rules work; the friendly named-marker picker completes it in issue 015)
