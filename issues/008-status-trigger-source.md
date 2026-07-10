# 008 — Status-effect trigger source (gained / removed)

## Parent PRD
[PRD.md](../PRD.md) — FR-2.3 (incl. the scope definition), acceptance §10.3 (status part); DESIGN.md §3.3

## Type
AFK

## What to build
The third trigger source: "when status *X* is gained/removed on *me / a party member / my target*, alert me."

- **Source**: `StatusSource` polling `StatusList` for **self + party members (via `IPartyList`) + current target** — this *is* the definition of scope "anyone" (PRD FR-2.3 scope note; no all-nearby-actors scan). Reads each actor's `StatusList` once per sweep into a local (each property access allocates a wrapper — DESIGN.md §3.3/§9), diffing per-actor status-id sets to emit `StatusGained` / `StatusRemoved` events.
- **Core**: status source spec (`StatusId`, `Event` = Gained | Removed | Either, `Bearer` = Self | Party | Target | Anyone, optional `MinStacks`), `StatusTriggerMatcher`, placeholders `{status}`, `{bearer}`.
- **Lumina**: status-name resolution + searchable status picker (localized names, icons if cheap).
- **UI**: "When" section variant for Status.
- **Tests**: diff logic (gain, loss, simultaneous gain+loss across actors, stack-count threshold), bearer scoping, matcher + placeholders, party join/leave mid-session (diff sets reset per actor correctly).

## What happens (behavior & data flow)
1. Per framework update, the source reads the buff/debuff arrays of at most 10 actors (self + 7 party + target) from client memory — the same data the game renders as status icons.
2. Set-diffing turns state into discrete events; a food buff expiring produces exactly one `StatusRemoved` event.
3. Matching rules fire the configured outputs; e.g. US-4: status `Well Fed` removed from Self → toast "Food expired".
4. Edge behavior: on zone change or party reshuffle, per-actor baselines reset without emitting spurious events (tested).

## Network traffic
**None initiated.** Status effects are part of the actor state the client already holds; the plugin reads memory, sends nothing, requests nothing.

## Acceptance criteria
- US-4 works in-game: "Well Fed removed from self → toast + sound" fires when food expires (or is right-clicked off).
- Bearer scopes behave per the FR-2.3 definition; a debuff on a non-party bystander never fires an "anyone" rule (unit test on scope logic).
- `MinStacks` gates correctly (unit test).
- No spurious fires on zone change / party changes (unit tests on baseline reset).
- Status picker resolves localized names.

## Blocked by
- [issues/004-regex-placeholders-error-states.md](004-regex-placeholders-error-states.md)

## User stories addressed
- US-4 (buff watcher — complete, with outputs from issue 006)
