# 004 — Regex mode, placeholders, live "Try it" tester & rule error states

## Parent PRD
[PRD.md](../PRD.md) — FR-2.1 (complete), FR-5, FR-1 (runtime state: `RuleError`), NFR-2, FR-8.2 (live tester, inline validation); DESIGN.md §4.1/§4.2/§4.4/§7.2

## Type
AFK

## What to build
The full expressiveness of chat rules, end-to-end: regex matching with capture groups, the placeholder system, a live in-editor tester, and the runtime error-state model that keeps a bad regex from ever damaging user configuration.

- **Core**: `MatchMode.Regex` in `ChatTriggerMatcher` — compiled on save with `RegexOptions.Compiled` and **50 ms `MatchTimeout`**; `MatchResult` carrying `$1`–`$9` + named values; `PlaceholderRenderer` (`{sender}`, `{message}`, `{zone}`, `{time}`, `$n`; unknown → empty string); `RuleRuntimeState` extended with `RuleError(message)` — set in a runtime map on regex timeout/exception, **never** written to the persisted `Enabled` flag (PRD FR-1).
- **UI**: editor gains match-mode selector, "Try it" sample-input box running the *real* core matcher live (match/no-match + captured groups shown); inline validation messages replace any silently disabled Save ("Add a pattern", "Pattern does not compile: <error>"); rule list shows a distinct ⛔ error badge with tooltip for `RuleError` rules.
- **Tests**: regex matching incl. groups and case flags, timeout → `RuleError` (crafted catastrophic pattern with injected short timeout), renderer coverage for every placeholder + `$n` bounds + missing-value behavior, runtime-state matrix (Enabled × error), invalid-pattern rejection at save time.

## What happens (behavior & data flow)
1. On save, the pattern is compiled once; failures surface immediately in the editor (the rule cannot be saved in a known-broken state).
2. At match time, a regex that exceeds 50 ms (catastrophic backtracking) throws; the engine catches it, marks the rule `RuleError` in the runtime map, and continues with other rules — the game never stutters, the plugin never crashes, the user's checkbox stays checked (PRD §11 risk table).
3. On a match, capture groups flow through `MatchResult` into `PlaceholderRenderer`, producing the final echo text (e.g. `"$1 is online"` → `"Krile Baldesion is online"`).
4. Fixing the pattern (or reloading) clears the error state — nothing persisted needed cleanup.

## Network traffic
**None.** Regex evaluation and placeholder rendering are pure in-process computations over already-received local chat data.

## Acceptance criteria
- US-5 works in-game: regex rule `(\w+ \w+) has logged in` with echo `"$1 is online"` fires with the captured name substituted.
- A deliberately catastrophic regex triggers `RuleError`: error badge + message in the list, other rules keep firing, `Enabled` remains true in the saved config file (verify the JSON on disk).
- "Try it" box shows match/no-match and group values live while typing, using the same matcher code the unit tests cover.
- Save is never silently disabled — every blocked state shows its reason inline.
- Placeholder unit tests cover all FR-5 chat placeholders; unsupported placeholders render empty.

## Blocked by
- [issues/002-chat-contains-to-echo.md](002-chat-contains-to-echo.md)

## User stories addressed
- US-5 (overworld regex login alert — complete)
- US-1 (raid lead — improved via `{sender}` in echo text)
