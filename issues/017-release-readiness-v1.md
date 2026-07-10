# 017 — Release readiness: v1.0 verification, docs & distribution

## Parent PRD
[PRD.md](../PRD.md) — FR-11, FR-12, §8, §9 (M4), §10 (all acceptance criteria); DESIGN.md §10; README.md (this folder — the draft to finalize)

## Type
**HITL** — requires in-game manual verification and the publish decision by the maintainer.

## What to build / do
The final slice turns the finished feature set into a distributed 1.0:

- **Full acceptance pass (PRD §10)**: execute all nine criteria — the scripted 60-second golden path with a stopwatch, anti-loop, stable sources end-to-end (incl. the back-to-back recast check), advanced positive + failure paths, placeholders, create-from-event, config-safety, pipeline. Record results in this issue file.
- **Manual in-game checklist**: run the README checklist top to bottom on the live game; fix or file follow-ups for any failure.
- **Docs (FR-11/FR-12)**: finalize the repo README from the draft in this folder — remove *(placeholder)* markers, fill real screenshots, verify the upgrade-path section matches shipped behavior (incl. the known-limitations wording: instant casts, `/echo` macros, status scope definition); write `release-notes/v1.0.0.md`.
- **Release mechanics (§8)**: run `scripts/prepare_release.py --version v1.0.0` (syncs csproj/`scyt.repo.json`/README versions, Docker rebuild with mounted Dalamud dev folder, artifact verification); tag `v1.0.0`; create the GitHub release with `out/release/latest.zip`.
- **Distribution**: add the third entry to `MyDalamudPlugins/pluginmaster.json` (AssemblyVersion, download links to the `v1.0.0` release, category tags, `DalamudApiLevel: 15`, LastUpdate timestamp); verify install through the custom repo on a clean Dalamud.

## What happens (behavior & data flow)
1. The release script is the same test-gated flow as the siblings: the zip cannot be produced unless the full suite passes inside Docker; the script then verifies the packaged version strings.
2. After the pluginmaster update, any Dalamud client subscribed to the custom repo sees the new plugin and installs it from the GitHub release asset.

## Network traffic
- **Plugin runtime: none** — final confirmation of the PRD privacy stance (NFR-4) is part of the acceptance pass (no sockets/HTTP anywhere in the shipped code; verify by code review grep).
- **Release-time (maintainer actions)**: pushing the tag/release uploads `latest.zip` to GitHub; users' Dalamud clients later fetch `pluginmaster.json` from raw.githubusercontent.com and the zip from GitHub Releases. Identical distribution traffic to the two sibling plugins.

## Acceptance criteria
- All PRD §10 criteria pass and are checked off with notes in this file.
- README finalized (no placeholders), matching shipped behavior; `release-notes/v1.0.0.md` written.
- `v1.0.0` GitHub release exists with a script-verified `latest.zip`; CI green on the release commit.
- Plugin installs and loads on a clean Dalamud via the custom repository; `pluginmaster.json` entry validated by the metadata tests.
- Maintainer sign-off recorded (this is the HITL gate).

## Blocked by
- [issues/014-advanced-tier-vfx-source.md](014-advanced-tier-vfx-source.md)
- [issues/015-head-marker-named-picker.md](015-head-marker-named-picker.md)
- [issues/016-import-export-starter-pack.md](016-import-export-starter-pack.md)
- [issues/011-test-fire-contract.md](011-test-fire-contract.md)
- [issues/005-cooldowns-rate-limit.md](005-cooldowns-rate-limit.md)
- (transitively: all other issues)

## User stories addressed
- Ships US-1…US-5 to end users; completes PRD Goal 4 and §9 M4.
