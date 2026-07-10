# 010 — Live events window + create-rule-from-event

## Parent PRD
[PRD.md](../PRD.md) — FR-8.3, FR-8.2 ("pick from recent chat"), FR-10 (`/callouts events` + alias), acceptance §10.7; DESIGN.md §7.3

## Type
AFK

## What to build
The primary authoring surface (review-elevated from "debug window"): a live feed of everything the sources see, with one-click rule creation — so users never type an action id, status id, or exact chat string by hand.

- **Core**: session-only ring buffer of normalized `TriggerEvent`s (default 200, size configurable — mirrors LootDistributionInfo's `DebugEventBuffer` pattern; never persisted).
- **UI — Live events window** (`/callouts events`, `/callouts debug` as alias): rows per DESIGN.md §7.3 wireframe — timestamp, source icon, human-readable summary; per-kind show/hide checkboxes; Pause/Clear; the global rate-limit drop counter (moves here from Settings, issue 005).
- **`[＋]` create-from-event**: opens the rule editor **pre-filled with the exact captured values** — chat: channel + full line as pattern (contains); cast: action id + name; status: status id + gained/removed as observed; the user only names the rule and picks outputs.
- **Discoverability** (review fix): `[👁 Watch events…]` button in the Rules window toolbar; the editor's chat pattern field gets the **"pick from recent"** dropdown fed by this buffer.
- **Tests**: ring-buffer semantics (cap, ordering, clear), event-summary formatting, pre-fill mapping from each event kind to a valid source spec.

## What happens (behavior & data flow)
1. Every source (issues 002/007/008/009 now; advanced sources join in issue 014) additionally appends its normalized events to the ring buffer — one shared pipeline, no second detection path.
2. The window renders the buffer live; Pause freezes rendering without stopping capture.
3. Clicking `[＋]` copies the event's identifying fields into a fresh rule spec and opens the editor pane beside the feed (the pane is non-modal precisely so the feed stays visible — DESIGN.md §7.2 decision).
4. Buffer contents are in-memory only and vanish on plugin unload (privacy: no chat logging to disk beyond what the rules themselves need, which is nothing).

## Network traffic
**None.** The feed displays events already captured in-process; nothing is fetched, logged remotely, or transmitted. Buffer is RAM-only.

## Acceptance criteria
- Acceptance §10.7: `[＋]` on a cast event opens the editor pre-filled with that exact action id/name; same for chat (channel + line) and status (id + event type) — verified in-game for each kind.
- The Rules window toolbar button opens the feed; both commands work; alias documented.
- Pause stops the display but not capture (resuming shows what happened meanwhile); Clear empties the view; buffer never exceeds its cap (unit tests).
- Rate-limit drops are visible in the feed footer.
- "Pick from recent" in the chat editor lists recent chat lines and inserts the chosen one as the pattern.

## Blocked by
- [issues/007-cast-trigger-source.md](007-cast-trigger-source.md)
- [issues/008-status-trigger-source.md](008-status-trigger-source.md)
- [issues/009-duty-event-trigger-source.md](009-duty-event-trigger-source.md)

## User stories addressed
- Accelerates all of US-1…US-5 (it is the mechanism behind PRD Goal 1 / acceptance §10.1's 60-second path); no new story completed on its own.
