# Callouts — Product Requirements Document

| | |
|---|---|
| **Product** | Callouts — a user-configurable trigger & alert plugin for FFXIV (Dalamud) |
| **Status** | Draft v1.1 (post adversarial review) |
| **Date** | 2026-07-10 |
| **Author** | ScytRaiin (with Claude) |
| **License** | AGPL-3.0 |
| **Repo (planned)** | `Scytraiin/Callouts`, distributed via `Scytraiin/MyDalamudPlugins` |

---

## 1. Summary

Callouts lets a player define **trigger rules** ("when X happens in the game, alert me with Y") without writing code. A rule watches one **trigger source** — a chat/log line, an enemy starting a cast, a status effect appearing, or (as an advanced option) a VFX/head-marker spawning on an actor — and fires one or more **outputs**: an Echo chat message, a game sound effect, and/or an on-screen toast. All detection is read-only and local; the plugin never sends anything to other players or servers and never automates gameplay.

It is the third plugin in the ScytRaiin ecosystem and reuses the established conventions of Loot Distribution Info and VoiceDirectorV2: Dalamud API 15 / .NET 10, a Dalamud-free unit-tested core, Docker test-gated builds, and distribution through GitHub Releases + the `pluginmaster.json` custom repo.

## 2. Background & motivation

- Loot Distribution Info already proves the chat-watching pattern (fixed loot verbs). Players repeatedly want the *general* version: "tell me when **my** pattern appears."
- Full raid-mechanic suites (BossMod, Splatoon) exist but are heavyweight, fight-specific, and require scripting for custom needs. There is a gap for a **simple, rule-based, user-friendly** alert tool.
- Research against the Dalamud 15 source (see DESIGN.md §3) confirms every required API exists: `IChatGui.ChatMessage`, `IObjectTable`/`IBattleChara` cast info, `StatusList`, `IToastGui`, `IChatGui.Print` with `XivChatType.Echo`, `UIGlobals.PlayChatSoundEffect`, and `IGameInteropProvider` for hook-based sources.

## 3. Goals

1. Let a non-programmer create a working alert rule in under a minute (see the specified golden path, FR-8.2 and §10.1).
2. Cover the trigger sources: chat lines, cast starts, status gain/loss in the stable tier; VFX spawns and head markers in an opt-in **Advanced sources** tier. (Tethers and duty events are explicit upgrade-path items, see §9/README.)
3. Per-rule, individually configurable outputs: Echo text (with placeholders), sound effect, toast.
4. Ship with the same quality bar as the existing plugins: unit-tested core, test-gated Docker build, CI, versioned releases with release notes, and a README documenting a clear upgrade path.

## 4. Non-goals

- **No automation.** Callouts never presses buttons, moves the character, or sends chat visible to others. Outputs are strictly local (Echo is local-only by design).
- **No bundled fight modules.** Callouts is a generic engine; it does not ship per-boss logic (that is BossMod's job). Sharing rules via export strings (FR-8.5, Must) is supported instead.
- **No network transmission, telemetry, or external APIs.** Fully offline, like the sibling plugins.
- **No pixel/screen-capture analysis.** All detection reads game state from memory via Dalamud (see DESIGN.md §2).
- **No TTS in v1** (listed as a possible future output in the upgrade path).

## 5. Target users & core use cases

| User | Example rule |
|---|---|
| Raid lead | Chat line `has initiated a ready check` → Echo "Ready check!" + sound 6 |
| Progression raider | Head marker (spread) on **me** → toast "SPREAD" + sound 11 |
| Any duty player | Enemy cast whose name contains `Ultima` → Echo "Big raidwide — mit now" + sound 8 |
| Buff watcher | Status `Well Fed` **removed** from self → toast "Food expired" |
| Overworld / casual | Chat line matching regex `(\w+ \w+) has logged in` → Echo "$1 is online" |

## 6. Functional requirements

Priorities: **Must** (blocks v1.0), **Should** (target v1.x), **Could** (backlog). Everything the v1.0 milestone (M4, §9) contains is Must.

### FR-1 Rule engine (Must)
A rule consists of: unique id, display name, **user-intent enabled flag**, **one trigger source** with source-specific filter fields, **one or more outputs**, scoping conditions, and a per-rule cooldown. Rules are evaluated independently; multiple rules may fire from one event.

**Enabled vs. active (Must):** the persisted `Enabled` flag stores *user intent only* and is **never written by the engine**. Whether a rule actually runs is a separately computed **runtime status**: `Active`, `DisabledByUser`, `BlockedAdvancedOff` (advanced toggle off), `SourceFailed` (hook broken), or `RuleError` (e.g. regex timeout — carries the error message). Runtime blocks never survive as changes to user configuration; fixing the cause reactivates the rule without user action. UI representation in FR-8.1.

### FR-2 Trigger sources

| # | Source | Tier | Priority | Filter fields |
|---|---|---|---|---|
| FR-2.1 | **Chat message** | Stable | Must | Channel(s) (`XivChatType` multi-select or "any"), match mode (contains / regex), pattern, case sensitivity, optional sender filter |
| FR-2.2 | **Cast started** | Stable | Must | Action (by name via searchable Lumina picker, or raw id), caster filter (any enemy / name contains), "only when targeting me / a party member" option |
| FR-2.3 | **Status gained / removed** | Stable | Must | Status (searchable picker or id), event (gained / removed / either), bearer (self / party member / current target — see scope note), optional min stacks |
| FR-2.4 | **VFX spawn on actor** | Advanced | Must | VFX path (contains / regex, e.g. `vfx/lockon/eff/`), actor filter (self / party / anyone) |
| FR-2.5 | **Head marker** | Advanced | Must | **Named-marker picker** (curated list of common markers, e.g. "spread", "stack", plus raw-value entry), target (self / party / anyone). The picker abstracts the detection mechanism (icon id vs. lock-on VFX path — DESIGN.md §3.5); users never see the difference. |

*Stable* sources use documented Dalamud events and object-table polling and survive game patches. *Advanced* sources require signature-based game-function hooks and may break on patch days (see FR-3, NFR-3).

**Known limitation (FR-2.2, Must-document):** the stable cast source only sees actions **with a cast bar** (`IsCasting`). Instant abilities (many tankbusters, auto-attack specials) are invisible to it. This is documented in the README; an advanced-tier "action used" source (ActionEffect hook) is an upgrade-path candidate.

**Scope note (FR-2.3):** bearer scope "anyone" is defined as *self + party members + current target* (bounded polling cost). The UI uses exactly this wording; there is no implied "all nearby actors" scan.

**Upgrade-path sources (not v1.0):** tether events (advanced tier, v1.x), duty wipe/recommence events (stable tier, targeted for M2 — see OQ-4 resolution §12), "action used" for instant casts (advanced, backlog).

### FR-3 Advanced sources master switch (Must)
- A single **"Enable advanced sources (VFX, head markers)"** toggle in settings, **default off**.
- Hooks are only installed while the toggle is on; turning it off removes them at runtime.
- Each advanced source shows a health indicator (Active / Failed / Disabled). If a hook fails to install (e.g. signature broke after a game patch), the plugin **blocks that source only** (runtime status, FR-1), shows its state in the settings UI, and everything else keeps working. Rules bound to a failed source are flagged in the rule list, never silently dropped and never mutated.
- **Active failure notification (Must):** on hook-install failure the plugin prints **one Echo line and one error toast per source per session** at load time, e.g. `Callouts: head-marker detection failed to start (game patch?). 3 rules are inactive — /callouts config.` Users must not have to open a window to learn their raid alerts are dead. The Rules window title bar additionally shows an inactive count (FR-8.1).

### FR-4 Outputs — per rule, individually toggleable (Must)
Each rule enables any combination (at least one required):
- **Echo message**: text with placeholder substitution (FR-5), printed to the Echo channel (local-only).
- **Sound effect**: one of the 16 game alert sounds (ids 1–16, the `<se.1>`–`<se.16>` set), previewable in the editor.
- **Toast**: on-screen popup; style selectable (Normal / Quest / Error) with the text also placeholder-substituted.

### FR-5 Placeholders (Must)
Substituted into Echo and toast text where the source provides them: `$1`–`$9` (regex capture groups, chat source), `{sender}`, `{message}`, `{caster}`, `{action}`, `{target}`, `{status}`, `{bearer}`, `{vfxpath}`, `{zone}`, `{time}`. Unavailable placeholders render as empty; the rule editor shows which placeholders the selected source supports.

### FR-6 Anti-loop & spam control (Must)
- Messages in the **Echo channel are never matched** (prevents a rule triggering on its own output — a hard, non-configurable rule). *Verified sufficient*: plugin-printed messages re-enter `ChatMessage`, and sound/toast outputs produce no observable event (DESIGN.md §3.1).
- **Documented consequence:** user macros that print via `/echo` also land in the Echo channel, so rules cannot trigger on your own `/echo` macros. This is stated in the README; a v1.x candidate is whitelisting Echo lines the plugin did not itself print.
- Per-rule **cooldown**, default 2 s, range 0–600 s.
- **Global rate limit** (default max 10 fires/second across all rules; excess dropped and counted in the Live events window). Promoted to Must: it is part of the spam-risk mitigation (§11) and advertised in the README.
- Suppress-identical window: identical rule+text within 1 s fires once. *(Could)*

### FR-7 Rule scoping (Must)
Per rule: restrict to specific zones/duties (searchable territory picker), "only in combat", "only in duty". Default: everywhere. (Promoted to Must: committed for v1.0 in §9.)

### FR-8 User interface (Must) — detailed design in DESIGN.md §7

**FR-8.1 Rules window** (`/callouts`):
- Searchable/filterable/sortable rule list (sort by name, source, fire count, status), enable checkboxes, source-type icons, session fire counters; add / edit / duplicate / delete; per-rule test-fire (FR-8.4).
- **State display**: checkbox = user intent; a separate computed status badge (with tooltip) shows runtime state; `RuleError` shows a distinct error badge with the message. A rule blocked by the advanced toggle shows as *checked + blocked*, never as unchecked.
- **Banners, one place for "why is nothing firing"**: master-off banner ("Callouts is globally disabled — [Turn on]", list dimmed), and a single collapsed banner for blocked rules ("3 rules inactive — advanced sources are off / a source failed [Open Settings]") instead of per-row explainer lines. Window title shows the inactive count.
- **Bulk operations** on the current filter result: enable all shown / disable all shown / delete shown (undoable, NFR-6).
- **Empty state (first run)**: short pitch, primary CTA "[＋ Create your first rule]", secondary "[Import starter rules]", pointer to Live events ("Don't know the exact text? Watch events and click ＋ on the one you mean.").

**FR-8.2 Rule editor** — non-modal right-hand pane (decision: DESIGN.md §7.2): form adapts to the chosen source type; live regex/sample tester; per-output toggles with sound preview and toast test; placeholder help scoped to the source.
- **Golden-path defaults (Must, testable)**: a new rule opens with Source = Chat, Channels = Any, Match = Contains, Echo output pre-enabled, keyboard focus in the Pattern field.
- **Inline validation instead of silent disable**: when Save is unavailable, the editor shows why ("Add a pattern", "Enable at least one output") next to the button.
- Chat pattern field offers **"pick from recent chat"** (fed by the event buffer) so exact game strings never need to be typed from memory.
- Unsaved changes prompt on close/cancel; edits apply atomically on Save (the live engine never sees a half-edited rule).

**FR-8.3 Live events window** (`/callouts events`; alias `/callouts debug`): live feed of candidate events (chat lines, casts, status changes, and — when advanced is on — VFX/markers) with **"create rule from this event"** pre-filling the editor. Reached prominently via a **[👁 Watch events…] toolbar button** in the Rules window (not only via command). This is the primary authoring UX: users never type an id or VFX path by hand.

**FR-8.4 Test-fire contract (Must)**: test-fire injects a synthetic event with canned placeholder values ("TestSender", `$1=example`, …), **bypasses** cooldown/rate-limit gates, does **not** increment the fire counter, works regardless of enabled state or source health (it proves outputs work even when detection is down), and prefixes Echo/toast output with `[test]` so a mid-duty test is never mistaken for a real alert.

**FR-8.5 Import/export (Must)**: rules serializable to a clipboard-friendly string for sharing. Export **preserves rule ids**; import shows a preview and detects id/name collisions with per-rule *Skip / Replace / Keep both* (default Replace for id-identical rules, so updated rule packs don't duplicate). Never overwrites silently.

**FR-8.6 Settings window** (`/callouts config`): master enable, advanced-sources toggle + per-source health, global rate limit, default cooldown, event-feed buffer size, config export/backup, import rules, **import starter rules** (permanent home, not only the empty state).

### FR-9 Configuration persistence & migration (Must)
Versioned config (integer `Version`, forward migrations only — same pattern as Loot Distribution Info's config v11). **Unconditional pre-migration backup**: whenever the on-disk version differs from the code version, the raw config JSON is copied to a backup file *before* any migration runs (not only on failure). On migration failure, the backup is kept and defaults load. On **downgrade** (stored version > code version): refuse to migrate, keep backup, load defaults with a visible notice. Config export/backup also available manually. Implementation notes in DESIGN.md §4.6.

### FR-10 Commands (Must)
- `/callouts` — rules window
- `/callouts config` — settings
- `/callouts events` — live events window (`/callouts debug` kept as alias)
- `/callouts on|off` — master enable/disable *(Should)*

### FR-11 README with upgrade path (Must)
The repo README must document: what the plugin does, install via the custom repo, usage, the stable-vs-advanced source distinction **including the documented limitations** (instant casts invisible to the stable cast source; `/echo` macros unmatchable), **an explicit upgrade-path section** (staged feature roadmap, config-migration guarantees, patch-day behavior of advanced sources incl. the active failure notice), and the standard local Docker validation commands. Draft: `README.md` in this folder.

### FR-12 Localization stance (Must, documentation-level)
Chat patterns are user-authored, so the engine is client-language-agnostic by construction; users on DE/FR/JP clients write patterns in their language. Cast/status pickers resolve names through Lumina in the client language automatically. UI text is English-only in v1 (upgrade path item).

## 7. Non-functional requirements

- **NFR-1 Performance**: steady-state overhead < 0.1 ms per frame with 50 rules; object-table polling is diff-based (react on change, not every frame per rule); regexes compiled once on save. (Allocation realities: DESIGN.md §9.)
- **NFR-2 Robustness**: every source callback and output sink is exception-isolated; a throwing/timing-out rule is **runtime-blocked** with status `RuleError` and a visible error badge + message (FR-1/FR-8.1) — never a plugin crash, and never a mutation of the user's `Enabled` flag.
- **NFR-3 Patch resilience**: stable-tier features must keep working after a game patch with at most a Dalamud API-level bump. Advanced tier degrades per FR-3 (incl. active notification) and recovery is a plugin update, documented in the README patch-day section.
- **NFR-4 Privacy**: no data leaves the machine; no telemetry.
- **NFR-5 Testability**: all pure logic (matching, placeholder rendering, cooldown/rate gating, runtime-status computation, config migration, import/export codec) lives in a Dalamud-free core covered by xUnit v3, compile-linked into the test project — same approach as the sibling plugins. Target: every FR-1/5/6/9 behavior has at least one test.
- **NFR-6 Usability**: no raw ids required anywhere a searchable name picker is possible; destructive actions are **undoable in preference to confirm dialogs** (e.g. "Rule deleted — [Undo]" inline for 5 s, or Ctrl-click to delete instantly), avoiding dialog fatigue at 50+ rules.

## 8. Distribution, build & release (mirrors existing conventions)

- Dalamud API level 15, `Dalamud.NET.Sdk/15.0.0`, `net10.0-windows`, C# latest, nullable enabled.
- **Build**: Docker test-gated flow — identical pattern to the sibling Dockerfiles (tests run first and gate the build; plugin build requires a mounted Dalamud dev folder at `/dalamud`; artifacts exported to `/out/plugin`).
- **CI**: GitHub Actions, two jobs (test on ubuntu + docker smoke build), copied from `ffxiv-loot-distribution/.github/workflows/ci.yml`.
- **Release**: `prepare_release.py` ported from VoiceDirectorV2 (version normalization, csproj + `scyt.repo.json` + README sync, Docker rebuild, artifact verification, `out/release/latest.zip`).
- **Distribution**: GitHub Releases (`latest.zip` per tag) + `scyt.repo.json` in-repo + third entry in `MyDalamudPlugins/pluginmaster.json`.
- **Metadata tests**: repo-json validity and repo↔manifest alignment tests, same as both siblings.

## 9. Milestones / staged delivery

| Milestone | Version | Scope |
|---|---|---|
| M1 — Engine core | `v0.1.0-alpha` | Rule engine incl. runtime-status model, chat source, Echo output, minimal rules window, config persistence + backup, anti-loop, unit tests, Docker/CI skeleton |
| M2 — Stable tier complete | `v0.2.0-alpha` | Cast + status sources, duty wipe/recommence source, sound + toast outputs, placeholders, rule editor (golden-path defaults, live tester, inline validation), Live events window with create-from-event |
| M3 — Advanced tier | `v0.3.0-beta` | VFX + head-marker sources behind the advanced toggle, named-marker mapping, hook health UI + active failure notification |
| M4 — 1.0 | `v1.0.0` | Import/export with collision handling, zone scoping, bulk operations, starter-rule pack, empty state, polish pass, README/docs complete, full test pass, pluginmaster listing |

## 10. Acceptance criteria (v1.0)

1. **Golden path**: following the scripted flow (new rule → type pattern → save), a first-time user creates "ready check → echo + sound" in under 60 seconds; alternatively one click imports it from the starter pack. Editor opens with the FR-8.2 defaults; Save unavailability always shows a reason inline.
2. **Anti-loop**: a rule's own Echo output can never re-trigger any rule (test-covered).
3. **Stable sources fire end-to-end**: for each of chat / cast / status, a scripted event produces the configured echo, sound, and toast (matcher-level test-covered; in-game via the manual checklist). Cast rules fire again on back-to-back recasts of the same action (test-covered, DESIGN.md §3.2).
4. **Advanced tier positive path**: with advanced sources enabled, a VFX/head-marker rule fires on a real lock-on marker in-game (manual checklist).
5. **Advanced tier failure path**: disabling advanced sources removes all hooks (verified via debug indicators); a simulated hook failure blocks only that source, surfaces the one-time Echo+toast notice, flags affected rules, and leaves user `Enabled` flags untouched.
6. **Placeholders** render correctly in both Echo and toast text for every source-supported placeholder (test-covered).
7. **Create-from-event** opens the editor pre-filled with the exact channel/action-id/status-id/VFX-path of the clicked event.
8. **Config safety**: config survives an upgrade across a config-version bump without losing rules; a pre-migration backup file exists; downgrade refuses migration and preserves the backup (test-covered).
9. **Pipeline**: all unit tests pass in the Docker gate; CI green; release artifact contains the expected version (script-verified).

## 11. Risks & mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Game patch breaks hook signatures (advanced tier) | High (each major patch) | Medium | FR-3 gating + runtime block + active notification; stable tier unaffected; patch-day playbook in DESIGN.md §10 |
| User regex catastrophically backtracks | Medium | Medium | 50 ms `Regex.MatchTimeout`; offending rule runtime-blocked (`RuleError`) with surfaced message |
| Alert spam / feedback loops | Medium | High (chat flood) | FR-6 hard Echo exclusion + cooldowns + global rate limit (all Must) |
| Malicious/corrupt import strings (gzip bomb) | Low | Medium | Decompression size cap + schema validation in RuleCodec (DESIGN.md §4.5, test-covered) |
| Scope creep toward BossMod | Medium | Medium | Non-goals §4; generic engine only |
| Client-language differences in chat patterns | Certain | Low | FR-12: user-authored patterns; document clearly |

## 12. Open questions — status after review

1. **Sound playback API — largely resolved.** `UIGlobals.PlayChatSoundEffect(id)` is used by Dalamud's own source at this API level (`Dalamud/Interface/Internal/Windows/Data/Widgets/SeStringCreatorWidget.cs:1054`, note the **1-based id**), so candidate A is real; remaining work is a runtime smoke test at M2. Constraint recorded: fallback B (`<se.N>` payload on the Echo line) cannot serve **sound-only** rules without printing an unrequested chat line — if A ever fails, sound-only rules degrade with a UI notice (DESIGN.md §5).
2. **Head-marker mechanism** (decision at M3): ActorControl hook (icon ids — precise) vs. reusing the VFX hook on `vfx/lockon/` paths (one hook, but requires a **maintained named-marker ↔ VFX-path mapping table**, which is a deliverable of that option — DESIGN.md §3.5). Either way FR-2.5's named-marker picker is the user-facing contract.
3. **Starter rules — resolved: yes.** 3–5 example rules (ready check, countdown, food expiry) ship as an importable pack with permanent UI homes in Settings and the empty state (FR-8.1/8.6).
4. **Duty wipe/recommence source — resolved: yes, M2.** Stable tier, same Dalamud events VoiceDirectorV2 already consumes; reflected in §9 and the README roadmap.
