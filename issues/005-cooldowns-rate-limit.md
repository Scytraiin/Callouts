# 005 — Per-rule cooldowns & global rate limit

## Parent PRD
[PRD.md](../PRD.md) — FR-6 (cooldown, global rate limit — both Must), §11 spam risk; DESIGN.md §4.3

## Type
AFK

## What to build
The spam-control layer, end-to-end from engine gates to UI fields:

- **Core**: `CooldownGate` (per-rule monotonic timestamp, injected clock abstraction for tests; default 2 s, range 0–600 s) and `RateLimiter` (global token bucket, default 10 fires/second, refill per second; dropped fires counted per session). Both sit between match and dispatch in `RuleEngine`.
- **UI**: cooldown field in the rule editor (with range validation + inline hint); global rate limit setting in the Settings window; a session drop counter displayed in Settings (it moves to the Live events window in issue 010).
- **Tests**: boundary timing (fire at exactly cooldown expiry; not 1 ms before) with the injected clock, token refill behavior, drop counting, zero-cooldown rules, config round-trip of both settings.

## What happens (behavior & data flow)
1. A matched rule first passes `CooldownGate`: if the rule fired less than `CooldownSeconds` ago, the fire is swallowed silently (by design — a repeating raid mechanic shouldn't produce 10 echoes).
2. Surviving fires pass `RateLimiter`: if the global bucket is empty (alert storm — e.g. an over-broad pattern matching every chat line in a busy hunt train), the fire is dropped and the session drop counter increments, giving the user visible evidence instead of a frozen chat log.
3. Gates never affect matching state or runtime status — a gated rule is still `Active`.

## Network traffic
**None.** Both gates are in-memory counters; nothing leaves the process.

## Acceptance criteria
- A rule matching a rapidly repeating line fires at most once per cooldown window (in-game check with a spammy `/say` macro; unit tests for exact boundaries).
- With the rate limit set to a low test value, an alert storm drops excess fires and the counter reflects the exact number dropped (unit test; in-game sanity check).
- Cooldown edits take effect on Save without reload; values outside 0–600 s are rejected inline.
- Injected-clock tests prove no reliance on wall-clock sleeps (suite stays fast).

## Blocked by
- [issues/002-chat-contains-to-echo.md](002-chat-contains-to-echo.md)

## User stories addressed
None new — hardens all stories against spam (PRD FR-6, risk §11).
