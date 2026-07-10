# 013 — Rules list at scale + master enable

## Parent PRD
[PRD.md](../PRD.md) — FR-8.1 (complete), FR-10 (`/callouts on|off`), NFR-6; DESIGN.md §7.0/§7.1

## Type
AFK

## What to build
Everything that keeps the Rules window honest and usable at 50+ rules, plus the global kill switch:

- **List UX**: sortable columns (name / source / fire count / status), optional user tags with tag filter, group-by (source or zone — zone data from issue 012), search + "Enabled only" filter.
- **Bulk operations on the current filter result**: Enable shown / Disable shown / Delete shown — deletion **undoable** per NFR-6 ("N rules deleted — [Undo]" inline for 5 s) instead of confirm-dialog chains; single-row delete via undo toast or Ctrl-click instant.
- **State surfacing (DESIGN.md §7.0)**: status badges + tooltips for every `RuleRuntimeState`; the single collapsed blocked-rules banner ("N rules inactive — … [Open Settings]"); inactive count in the window title. (Badge states for advanced sources activate fully with issue 014; the model and rendering land here.)
- **Master enable**: global on/off in Settings + `/callouts on|off` command; when off, a full-width banner "Callouts is globally disabled — [Turn on]" and a dimmed list — the "why is nothing firing" answer is always on screen.
- **Empty state**: first-run panel with `[＋ Create your first rule]`, `[Import starter rules]` (button present; wired fully by issue 016), and the Live-events pointer.
- **Tests**: sort/filter/group logic, bulk-op set computation ("shown" = exactly the filtered set), undo restore round-trip, master-enable gate in the engine.

## What happens (behavior & data flow)
1. The engine consults the master flag before any matching; `/callouts off` silences everything instantly (one boolean, persisted).
2. Sorting/filtering/grouping are pure functions over the rule collection (same pattern as LootDistributionInfo's `LootHistoryBrowser`, which is unit-tested the same way).
3. Deleting keeps a short-lived in-memory undo snapshot; Undo restores the exact rules (ids preserved); after expiry the snapshot drops.
4. Every "inert rule" cause is visible in one place: unchecked box (user), badge (system), banner (aggregate), title count, or the master banner.

## Network traffic
**None.** Pure UI and in-memory/config operations.

## Acceptance criteria
- With 50+ generated rules, the list stays navigable: sort by each column, filter by tag/source, group-by renders correctly (seeded test config).
- "Disable shown" with an active search affects exactly the visible rules (unit test).
- Bulk delete then Undo restores all rules with identical ids (unit test + UI check).
- `/callouts off` stops all firing; the banner appears; `/callouts on` restores; state survives reload.
- A rule blocked by the system renders **checked + badged**, never unchecked (DESIGN.md §7.0 — verified visually and by state-model unit tests).

## Blocked by
- [issues/004-regex-placeholders-error-states.md](004-regex-placeholders-error-states.md)
- [issues/012-rule-scoping.md](012-rule-scoping.md) — zone group-by (soft dependency; can land with group-by-source only if 012 is delayed)

## User stories addressed
- Scales all of US-1…US-5 to real collections; no single story completed by itself.
