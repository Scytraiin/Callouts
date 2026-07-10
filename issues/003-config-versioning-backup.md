# 003 — Config versioning, pre-migration backup & downgrade safety

## Parent PRD
[PRD.md](../PRD.md) — FR-9, acceptance §10.8; DESIGN.md §4.6

## Type
AFK

## What to build
The configuration safety net, end-to-end: versioned config schema, forward-only migrations, an **unconditional** pre-migration backup, and a safe downgrade refusal — plus the manual export/backup button in a first minimal Settings window (`/callouts config`).

- **Core**: `ConfigMigrator` with ordered steps `(int fromVersion) → apply(json)`; operates on raw JSON so it works regardless of current C# model shape.
- **Load path**: on startup read the raw config file via `IDalamudPluginInterface.ConfigFile` **before** typed deserialization (DESIGN.md §4.6 explains why the typed `GetPluginConfig` is unsuitable). If stored `Version` ≠ code version → copy the raw file to `callouts-config.backup-v<stored>.json` beside it, then migrate (forward) or refuse (downgrade: stored > code → keep backup, load defaults, show a notice toast/echo once).
- **Failure path**: any migration exception → backup is already on disk; load defaults; visible notice.
- **UI**: minimal Settings window with "Export config backup now" and the config file path displayed.
- **Tests**: per-step migration tests, version-mismatch-creates-backup, downgrade refusal, migration-failure-loads-defaults-and-keeps-backup, no-op when versions match (no backup spam).

## What happens (behavior & data flow)
1. Plugin load → raw JSON read from the Dalamud config directory (local disk).
2. Version comparison decides: no-op / backup+migrate / backup+refuse (downgrade).
3. Only after this does the typed configuration materialize and the engine start.
4. The user's rules therefore can never be destroyed by an upgrade: every version transition leaves the original file on disk first (PRD FR-9; README "Upgrade path" promise).

## Network traffic
**None.** All reads/writes are to the local Dalamud plugin-configuration directory on disk. Backups stay on the local machine.

## Acceptance criteria
- Simulated old-version config (fixture JSON) migrates to current, and the backup file exists with the original bytes (unit test).
- Simulated future-version config: migration refused, defaults loaded, backup present, notice shown (unit test for logic; in-game check for the notice).
- Migration step that throws: defaults load, no crash, backup intact (unit test).
- Equal versions: no backup file created (unit test).
- Settings window shows the export button; pressing it writes a timestamped copy.

## Blocked by
- [issues/002-chat-contains-to-echo.md](002-chat-contains-to-echo.md)

## User stories addressed
None directly — protects every user story's data (PRD FR-9, acceptance §10.8).
