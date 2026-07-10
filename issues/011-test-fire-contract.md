# 011 — Test-fire contract

## Parent PRD
[PRD.md](../PRD.md) — FR-8.4; DESIGN.md §7.5, §7.6

## Type
AFK

## What to build
The specified test-fire behavior (review-hardened contract), end-to-end from engine to every sink:

- **Core**: `RuleEngine.TestFire(rule)` injecting a synthetic `TriggerEvent` with canned placeholder values (`{sender}=TestSender`, `$1=example`, `{action}=Test Action`, …, per source kind); **bypasses** `CooldownGate` and `RateLimiter`; does **not** increment the session fire counter; runs regardless of enabled state, advanced-toggle state, or source health.
- **Sinks**: all outputs produced by a test-fire are visibly marked — Echo and toast text prefixed `[test]`; sound plays unmarked (inherently harmless).
- **UI**: ▶ button per rule row in the Rules window; the editor's existing Preview/Test buttons (issue 006) unify onto this same code path.
- **Tests**: gate bypass, counter non-increment, canned values per source kind, `[test]` prefixing, test-fire of a rule whose source is failed/blocked still produces outputs.

## What happens (behavior & data flow)
1. The user clicks ▶; the engine fabricates a match result without any source involvement — proving the *output* half of the pipeline in isolation.
2. This is the patch-day diagnostic (DESIGN.md §7.5): when an advanced source is down (issue 014), test-fire demonstrates "your outputs still work; only detection is broken", exactly the reassurance a worried raider needs.
3. The `[test]` prefix guarantees a mid-duty test toast/echo is never mistaken for a real alert (DESIGN.md §7.6).

## Network traffic
**None.** Synthetic events are fabricated in-process; outputs are the same client-local sinks as issues 002/006.

## Acceptance criteria
- ▶ on any rule produces its enabled outputs immediately, `[test]`-prefixed, with canned placeholder values filled in.
- Two rapid ▶ clicks both fire (cooldown bypass); the session fire counter is unchanged afterwards (unit tests + UI check).
- ▶ works on a disabled rule and (after issue 014) on a rule whose source hook is failed.
- The editor Preview/Test buttons and the row ▶ demonstrably share one code path (single engine entry point; asserted by tests).

## Blocked by
- [issues/006-sound-toast-outputs.md](006-sound-toast-outputs.md)

## User stories addressed
- Supports all stories' verifiability; directly serves the patch-day flow around US-2 (advanced-tier reassurance).
