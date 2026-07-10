# 016 — Import/export + starter-rule pack

## Parent PRD
[PRD.md](../PRD.md) — FR-8.5, FR-8.6 (starter-pack homes), OQ-3 (resolved: yes), §11 gzip-bomb risk; DESIGN.md §4.5

## Type
AFK

## What to build
Rule sharing and the guided first-run content, end-to-end:

- **Core — `RuleCodec`**: export = JSON → UTF-8 → gzip → base64 with `CO1|` version prefix; **rule ids preserved** in the payload so pack updates can match existing rules. Import = reverse, with schema-version validation and a **gzip-bomb guard**: decompression through a counting stream, hard cap 1 MB decompressed, over-limit → clean user-facing rejection (review fix; PRD §11).
- **Import flow (UI)**: paste string → preview (rule names + source kinds + outputs) → collision detection by id (and name) → per-rule **Skip / Replace / Keep both**, default Replace for id-identical (so re-importing a friend's updated 10-rule pack yields 10 rules, not 30 — the review's duplicate finding).
- **Export flow (UI)**: export all, or export the current filter selection from the Rules window (`Export shown`).
- **Starter pack**: 3–5 curated rules (ready check, countdown detection, food-expiry toast, wipe callout) shipped as an embedded export string; one-click import via the empty-state button (issue 013) and its permanent Settings home.
- **Tests**: round-trip fidelity (ids, every source kind incl. scope + outputs), version-prefix rejection of unknown versions, corrupt-input rejection, **bomb rejection at the cap boundary**, collision-resolution semantics for all three choices, starter pack imports cleanly into an empty config.

## What happens (behavior & data flow)
1. Export serializes selected rules and places the string on the **local clipboard** — sharing happens wherever the user pastes it (Discord etc.), entirely outside the plugin.
2. Import parses clipboard text defensively: size cap before full decompression, schema validation before anything touches the config, preview before anything is saved. A malicious string can at worst produce an error message (PRD §11).
3. Replace keeps the existing rule's position/tags where sensible; Keep-both mints a fresh id.
4. The starter pack is data, not code — it exercises the exact same import path users see (one code path, tested once).

## Network traffic
**None.** Export/import are clipboard-only; the plugin performs no uploads/downloads. Distribution of shared strings is manual, external, and out of scope by design (PRD Non-goals: no network transmission).

## Acceptance criteria
- Round trip: export 10 mixed-source rules, wipe config, import → identical rules incl. ids (unit + manual).
- Re-importing a modified pack with the same ids and Replace default updates in place — rule count unchanged.
- A crafted gzip bomb and a truncated/corrupt string are both rejected with a clear message and zero config changes (unit tests).
- Starter pack: one click from the empty state yields working example rules (ready-check rule fires immediately when tested via issue 011's ▶).
- Import preview always shows before any write; Cancel leaves config untouched.

## Blocked by
- [issues/003-config-versioning-backup.md](003-config-versioning-backup.md)
- [issues/013-rules-list-scale-master-enable.md](013-rules-list-scale-master-enable.md)

## User stories addressed
- US-1 (the starter pack ships the ready-check rule — zero-effort path)
- Enables sharing for all of US-1…US-5 (raid groups distributing rule packs)
