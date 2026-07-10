# Callouts — Design Proposal

Companion to [PRD.md](PRD.md). Status: Draft v1.1 (post adversarial review), 2026-07-10.

API references below were verified against the Dalamud 15 source tree at
`FFYIV/Dalamud-master` (the framework version the sibling plugins build against)
and against the Splatoon/BossMod ecosystem as precedent.

---

## 1. Architecture overview

Same layering philosophy as Loot Distribution Info and VoiceDirectorV2, pushed further:
a **Dalamud-free core** (fully unit-testable), thin **adapter layers** for input and
output, and ImGui windows on top.

```
                    ┌─────────────────────────────────────────────┐
   game / Dalamud   │                 Plugin.cs                   │  composition root
                    └─────────────────────────────────────────────┘
                        │                                   │
              ┌─────────▼─────────┐               ┌─────────▼─────────┐
              │  TRIGGER SOURCES  │               │     UI WINDOWS    │
              │  (adapter layer)  │               │ Rules / Editor /  │
              │                   │               │ Settings / Events │
              │ ChatSource        │               └─────────┬─────────┘
              │ CastSource        │                         │ reads/writes
              │ StatusSource      │               ┌─────────▼─────────┐
              │ DutyEventSource   │  TriggerEvent │   Configuration   │
              │ VfxSource      ⚠  │──────┐        │  (rules, options) │
              │ HeadMarkerSource ⚠│      │        └───────────────────┘
              └───────────────────┘      │
                 ⚠ = advanced tier,      │
                 hook-based, gated       ▼
                          ┌────────────────────────────┐
                          │        CORE ENGINE         │  ← Dalamud-free,
                          │ RuleEngine (match+dispatch)│    compile-linked
                          │ RuleRuntimeState           │    into test project
                          │ TriggerMatcher per source  │
                          │ PlaceholderRenderer        │
                          │ CooldownGate / RateLimiter │
                          │ RuleCodec (import/export)  │
                          │ ConfigMigrator             │
                          └─────────────┬──────────────┘
                                        │ AlertAction(s)
                          ┌─────────────▼──────────────┐
                          │        OUTPUT SINKS        │
                          │ EchoSink   (IChatGui)      │
                          │ SoundSink  (UIGlobals/SE)  │
                          │ ToastSink  (IToastGui)     │
                          └────────────────────────────┘
```

**Data flow**: a source normalizes a game event into a `TriggerEvent` (plain record:
source kind + string/number fields). The `RuleEngine` matches it against all enabled
rules of that kind, applies cooldown/rate gates, renders placeholders, and returns
`AlertAction`s. Sinks execute them. Sources and sinks are the *only* code touching
Dalamud services; the engine in the middle is pure and synchronous.

## 2. Why memory-reading, not pixels (context)

Everything visible on screen exists as structured game state before it is rendered.
Dalamud exposes that state in-process; hooks cover the rest. Screen capture/CV would be
slower, fragile against resolution and camera, and is not used by any notable plugin.
Precedent: Splatoon's script API models exactly our advanced-tier events
(`OnVFXSpawn(target, vfxPath)`, `OnTetherCreate`, `OnObjectEffect`, `OnMessage`);
BossMod reconstructs whole fights from cast events + actor state.

## 3. Trigger sources — implementation detail

Verified API references are from the local Dalamud 15 source.

### 3.1 ChatSource (stable)
- Subscribes `IChatGui.ChatMessage` (same event Loot Distribution Info uses).
- Extract plain text via SeString `TextValue`; reuse the `SeStringDisplayText`
  utility pattern from LootDistributionInfo.
- Emits `TriggerEvent { Kind=Chat, Channel, Sender, Message }`.
- **Hard rule (anti-loop)**: events with `XivChatType.Echo` (56, `Dalamud/Game/Text/XivChatType.cs`)
  are dropped before matching — non-configurable. Verified sufficient: the plugin's own
  `Print` output re-enters `ChatMessage` via the `RaptureLogModule.PrintMessage` detour
  (`ChatGui.cs:372`) as Echo, and sound/toast outputs produce no event any source
  observes. Documented consequence: user `/echo` macros are unmatchable (PRD FR-6).

### 3.2 CastSource (stable)
- Polls `IObjectTable` on `IFramework.Update`; for each `IBattleChara`
  reads `IsCasting`, `CastActionId`, `CastTargetObjectId`, `TotalCastTime`
  (`Dalamud/Game/ClientState/Objects/Types/BattleChara.cs`).
- **Diff key is the cast *session*, not the action id** (review fix): an actor's map
  entry is cleared whenever `IsCasting == false` during a sweep; the source emits when
  `IsCasting == true` and the entry is empty *or* holds a different action id. This
  makes back-to-back recasts of the same action (extremely common for bosses) fire
  correctly: cast → idle → cast = two events. Unit test required for exactly this.
- **Map pruning**: entries whose `EntityId` was not seen in the current sweep are
  removed each sweep (spawn-unique EntityIds would otherwise grow the map unboundedly
  in long sessions); additionally cleared on territory change.
- Resolves action names once via Lumina `Action` sheet; the sheet's `CastType`
  / `EffectRange` columns are also how AoE shape info could be surfaced later
  (upgrade path: show "cone/circle" in the Live events feed).
- **Limitation (documented, PRD FR-2.2)**: only actions with a cast bar are visible;
  instant abilities never set `IsCasting`. An ActionEffect-hook "action used" source
  is the upgrade-path answer.
- Emits `TriggerEvent { Kind=Cast, ActionId, ActionName, CasterName, TargetIsSelf, TargetInParty }`.

### 3.3 StatusSource (stable)
- Polls `StatusList` (`Dalamud/Game/ClientState/Statuses/`) for **self + party members
  (via `IPartyList`) + current target** — this is the definition of scope "anyone"
  (PRD FR-2.3); no all-nearby-actors scan.
- Reads `StatusList` **once per actor per sweep** into a local (each property access
  allocates a fresh wrapper — `BattleChara.cs:76`), diffing status-id sets per actor;
  emits `StatusGained` / `StatusRemoved`.

### 3.4 DutyEventSource (stable, M2)
- Subscribes the duty wipe/recommence events VoiceDirectorV2 already consumes
  (`DutyWiped`, `DutyRecommenced`); emits `TriggerEvent { Kind=DutyEvent, Event }`.

### 3.5 VfxSource (advanced ⚠)
- `IGameInteropProvider.HookFromSignature<T>` (`Dalamud/Plugin/Services/IGameInteropProvider.cs`)
  on the game's actor-VFX-create function; extracts the VFX path string and the target
  actor. Hooks are **Reloaded-backed by default** (`HookBackend.Automatic`; MinHook
  exists only as a discouraged fallback — `IGameInteropProvider.cs:16,68`).
- Signature maintained as a single constant; on install failure → source blocked +
  active notification (PRD FR-3). This is the same event Splatoon exposes as `OnVFXSpawn`.
- Emits `TriggerEvent { Kind=Vfx, VfxPath, TargetName, TargetIsSelf, TargetInParty }`.

### 3.6 HeadMarkerSource (advanced ⚠)
User-facing contract is the **named-marker picker** (PRD FR-2.5).

> **OQ-2 decided (issue 015): Option A** — head markers are derived from VFX-path
> matching via `MarkerMapping` (a maintained named-marker ↔ `vfx/lockon/eff/*` table),
> reusing `VfxSource`'s single hook. No extra signature to maintain. ActorControl
> (Option B) is deferred; if tethers (post-1.0) later need ActorControl, revisiting is
> cheap because the source contract already hides the mechanism. The VFX signature
> itself is a documented patch-day/HITL item (empty in source control until verified
> in-game — see `VfxSource`).

The two options considered were:
- Option A: reuse VfxSource, match `vfx/lockon/eff/*` paths — zero extra hooks, but
  **requires a maintained mapping table** `named marker ↔ lockon VFX path(s)` shipped
  with the plugin (a deliverable of this option, kept in one data file).
- Option B: hook the `ActorControl` network handler for the head-marker opcode and
  expose numeric icon ids (more precise; what ACT/BossMod use) — mapping table then
  keys on icon ids instead.
Either way the source emits `TriggerEvent { Kind=HeadMarker, MarkerKey, RawValue,
TargetIsSelf, TargetInParty }` and the abstraction genuinely hides the mechanism.

### Source lifecycle
`ITriggerSource { Kind; Status (Active/Failed/Disabled); Start(); Stop(); event OnEvent; }`
Stable sources start always; advanced sources start only while the master toggle is on.
`Start()` failures are caught, logged, reflected in `Status`, surfaced via the active
notification (PRD FR-3), and shown in Settings.

## 4. Core engine

### 4.1 Rule model (persisted)
```jsonc
{
  "Id": "guid",                        // stable across export/import (see §4.5)
  "Name": "Ready check",
  "Enabled": true,                     // USER INTENT ONLY — never written by the engine
  "Source": {
    "Kind": "Chat",                    // Chat | Cast | Status | DutyEvent | Vfx | HeadMarker
    "Channels": [57, 58],              // chat only; empty = any
    "MatchMode": "Contains",           // Contains | Regex
    "Pattern": "has initiated a ready check",
    "CaseSensitive": false,
    "SenderPattern": null              // chat only, optional (FR-2.1)
    // cast:   ActionId / ActionNameContains / CasterFilter(AnyEnemy|NameContains) /
    //         OnlyTargetingMe / OnlyTargetingParty            (FR-2.2)
    // status: StatusId / Event(Gained|Removed|Either) /
    //         Bearer(Self|Party|Target|Anyone*) / MinStacks   (FR-2.3; *=self+party+target)
    // vfx:    PathPattern / MatchMode / ActorScope(Self|Party|Anyone)
    // marker: MarkerKey (named) or RawValue / ActorScope
    // duty:   Event(Wiped|Recommenced|Either)
  },
  "Outputs": {
    "Echo":  { "Enabled": true,  "Text": "Ready check from {sender}!" },
    "Sound": { "Enabled": true,  "EffectId": 6 },       // 1..16 (1-based, see §5)
    "Toast": { "Enabled": false, "Text": "", "Style": "Normal" }  // Normal|Quest|Error
  },
  "Scope": { "TerritoryIds": [], "OnlyInCombat": false, "OnlyInDuty": false },
  "CooldownSeconds": 2.0
}
```

**Runtime state (NOT persisted)** — review blocker fix: whether a rule actually runs is
computed per rule as
`RuleRuntimeState = Active | DisabledByUser | BlockedAdvancedOff | SourceFailed | RuleError(message)`
derived from (user `Enabled`, advanced toggle, source health, last engine error).
The engine **never mutates `Enabled`**: a regex timeout sets `RuleError` in a runtime
map keyed by rule id; fixing the pattern or reloading clears it. User intent therefore
always survives sessions, patches, and errors. The UI renders checkbox = intent,
badge = runtime state (§7.1).

### 4.2 Matching
- One `TriggerMatcher` per source kind; pure functions `(rule, event) → MatchResult?`
  where `MatchResult` carries capture groups and placeholder values.
- Regexes compiled on save with `RegexOptions.Compiled` and a **50 ms `MatchTimeout`**;
  a timeout or exception sets `RuleError` (never touches `Enabled`) with the message
  surfaced in the rule list (PRD §11).

### 4.3 Gates
- `CooldownGate`: per-rule monotonic timestamp check (injected clock for tests).
- `RateLimiter`: global token bucket (default 10/s); drops counted and shown in the
  Live events window. Test-fires bypass both gates (§7.5).

### 4.4 PlaceholderRenderer
Template → output string using `MatchResult` values; unknown placeholders → empty
string; `$1..$9` only for regex matches. Pure, heavily unit-tested.

### 4.5 RuleCodec (import/export)
- JSON → UTF-8 → gzip → base64 with a `CO1|` prefix for versioning.
- **Ids are preserved in the export payload** so re-importing an updated rule pack can
  match existing rules. Import validates schema version, shows a preview (names +
  source kinds), detects id/name collisions, and offers per-rule
  *Skip / Replace / Keep both* (default Replace for id-identical). Never silent.
- **Gzip-bomb guard** (review fix): decompression runs through a counting stream with a
  hard cap (1 MB decompressed); over-limit input is rejected with a user-facing error.
  Test: a crafted bomb string must be rejected, alongside the corrupt-input test.

### 4.6 ConfigMigrator
`Configuration { int Version; List<Rule> Rules; GlobalOptions Options; }`.
- **Unconditional pre-migration backup** (review fix): the raw config JSON is read
  directly from disk via `IDalamudPluginInterface.ConfigFile` (the typed
  `GetPluginConfig` would deserialize before plugin code can intervene) and copied to
  `callouts-config.backup-v<oldVersion>.json` **whenever the stored version differs
  from the code version**, before any migration step runs — not only on failure.
- Forward-only migration steps `(int from) → apply`; on failure the backup is kept and
  defaults load with a visible notice.
- **Downgrade policy**: stored version > code version → refuse migration, keep backup,
  load defaults, show notice (PRD FR-9).

## 5. Output sinks

- **EchoSink**: `IChatGui.Print(new XivChatEntry { Type = XivChatType.Echo, Message = seString })`
  (`Dalamud/Plugin/Services/IChatGui.cs`, lines 123–171). Local-only by game design.
- **ToastSink**: `IToastGui.ShowNormal / ShowQuest / ShowError`
  (`Dalamud/Plugin/Services/IToastGui.cs`, lines 54–87). Quest style supports options
  (`QuestToastOptions`) — exposed later if useful.
- **SoundSink**: FFXIVClientStructs `UIGlobals.PlayChatSoundEffect(id)` with **1-based
  ids (1..16)**. Verified available at this API level: Dalamud's own SeString tooling
  calls it as the preview for the `<se.N>` macro
  (`Dalamud/Interface/Internal/Windows/Data/Widgets/SeStringCreatorWidget.cs:1054`;
  `UIGlobals.PlaySoundEffect` similarly at `Dalamud/Interface/Windowing/Window.cs:403`).
  Runtime smoke test at M2 confirms behavior in-game.
  **Fallback constraint** (review fix): the alternative — appending an `<se.N>` payload
  to the Echo line — cannot serve *sound-only* rules without printing an unrequested
  chat line. If the primary API ever fails, sound-only rules degrade with a UI notice;
  rules with Echo enabled keep sound via the payload (PRD OQ-1).

All sinks are exception-isolated; a sink failure logs and never breaks dispatch.

## 6. Project layout (mirrors siblings)

```
Callouts/
├── Callouts/                        # plugin project (Dalamud.NET.Sdk/15.0.0, net10.0-windows)
│   ├── Plugin.cs                    # composition root, commands, source/sink wiring
│   ├── Configuration.cs
│   ├── Core/                        # ← Dalamud-free (compile-linked into tests)
│   │   ├── Rules/ (Rule, SourceSpec, OutputSpec, Scope)
│   │   ├── Engine/ (RuleEngine, RuleRuntimeState, TriggerMatcher*, CooldownGate,
│   │   │            RateLimiter, PlaceholderRenderer, RuleCodec, ConfigMigrator,
│   │   │            TriggerEvent)
│   ├── Sources/ (ITriggerSource, ChatSource, CastSource, StatusSource,
│   │             DutyEventSource, VfxSource, HeadMarkerSource, marker-mapping data)
│   ├── Sinks/   (IAlertSink, EchoSink, SoundSink, ToastSink)
│   ├── Windows/ (RulesWindow, RuleEditorPane, SettingsWindow, LiveEventsWindow)
│   └── Callouts.json                # plugin manifest
├── Callouts.Tests/                  # xUnit v3, <Compile Include> of Core/ files
├── Dockerfile                       # test-gated, same pattern as siblings
├── .github/workflows/ci.yml         # test + docker-smoke (from ffxiv-loot-distribution)
├── scripts/prepare_release.py       # ported from VoiceDirectorV2
├── scyt.repo.json
├── release-notes/
├── README.md                        # see draft in this folder
└── LICENSE.md                       # AGPL-3.0
```

## 7. UI design

Design language: standard Dalamud ImGui idioms (same widgets as the siblings' windows),
keyboard-searchable pickers everywhere, no raw ids required.

### 7.0 State model surfaced consistently (review blocker fix)
Checkbox = **user intent** (the persisted `Enabled`). Everything else is a computed
badge with a tooltip: `⚠ blocked — advanced sources are off`, `⚠ blocked — source
failed after game patch`, `⛔ error — regex timed out (…)`. A blocked rule renders
**checked + badged**, never unchecked — the user can always tell what *they* chose
apart from what the system did. All "why is nothing firing" answers appear in one
place: banners at the top of the Rules window (master-off banner; one collapsed
blocked-rules banner) — not per-row explainer lines.

### 7.1 Rules window (`/callouts`)
```
┌ Callouts — Rules (1 inactive ⚠) ─────────────────────────────────┐
│ [＋ New rule] [👁 Watch events…] [Import ▾] [Export all]          │
│ Search: [__________] 🔍  Filter: [All sources ▾] [Enabled only ☐] │
│ ⚠ 1 rule inactive — advanced sources are off  [Open Settings]    │
├───┬────┬─ Name ▴ ─────────────┬─ Outputs ────┬─ Fires ─┬─────────┤
│ ☑ │ 💬 │ Ready check          │ echo+♪6      │ 12      │ ▶ ✎ ⧉ 🗑 │
│ ☑ │ ⚔  │ Ultima incoming      │ echo+♪8+toast│  3      │ ▶ ✎ ⧉ 🗑 │
│ ☑ │ ✨ │ Spread marker on me ⚠│ toast+♪11    │  0      │ ▶ ✎ ⧉ 🗑 │
├───┴────┴──────────────────────┴──────────────┴─────────┴─────────┤
│ Bulk (filtered): [Enable shown] [Disable shown] [Delete shown…]  │
└──────────────────────────────────────────────────────────────────┘
```
- Sortable columns (name / source / fire count / status); optional user tags and a
  group-by (source or zone) for large collections — 50+ rules stay navigable.
- Row actions: ▶ test-fire (§7.5), ✎ edit, ⧉ duplicate, 🗑 delete — deletion is
  **undoable** ("Rule deleted — [Undo]" for 5 s; Ctrl-click deletes instantly),
  no confirm-dialog fatigue (PRD NFR-6).
- **Master-off state**: full-width banner "Callouts is globally disabled — [Turn on]",
  list dimmed.
- **Empty state (first run)**: pitch line, `[＋ Create your first rule]`,
  `[Import starter rules]`, and "Don't know the exact text? [👁 Watch events] and
  click ＋ on the one you mean."

### 7.2 Rule editor — non-modal right-hand pane (decided)
A pane (not a modal) so the Live events feed stays visible and clickable while
authoring — essential for the create-from-event flow. Unsaved changes prompt on
close/cancel ("Discard changes?"); the live engine only ever reads saved config.
```
┌ Edit rule ───────────────────────────────────────────────────────┐
│ Name [Ready check______________________]  Enabled ☑              │
│ ── When ─────────────────────────────────────────────────────────│
│ Source [Chat message ▾]                                          │
│ Channels [Any ▾]  Match [Contains ▾]  Case sensitive ☐           │
│ Pattern [has initiated a ready check_______] [Pick from recent ▾]│
│ Try it: [Zenos yae Galvus has initiated a ready check.] ✔ match  │
│ ── Then ─────────────────────────────────────────────────────────│
│ ☑ Echo   [Ready check from {sender}!_______]  (placeholders ⓘ)   │
│ ☑ Sound  [ ♪ 6 ▾ ] [Preview]                                     │
│ ☐ Toast  [____________________] Style [Normal ▾] [Test]          │
│ ── Options ──────────────────────────────────────────────────────│
│ Cooldown [2.0]s   Zones [Everywhere ▾]  Only in combat ☐         │
│ ⚠ Add a pattern to enable Save         [Cancel]  [Save rule]     │
└──────────────────────────────────────────────────────────────────┘
```
- **Golden-path defaults**: new rule = Source Chat, Channels Any, Match Contains,
  Echo pre-enabled, focus in Pattern (PRD FR-8.2 — makes acceptance §10.1 testable).
- **Inline validation, never a silently disabled button**: the reason ("Add a pattern",
  "Enable at least one output") is shown next to Save.
- "Pick from recent" dropdown on the chat pattern field, fed by the event buffer.
- The **"When" section re-renders per source kind** (cast → action picker + caster
  filter; status → status picker + gained/removed + bearer; vfx → path pattern +
  actor scope; marker → named-marker picker).
- Live tester runs the real core matcher (the same code under unit test) and shows
  match/no-match + captured groups instantly.
- Placeholder ⓘ popup lists exactly the placeholders valid for the chosen source.

### 7.3 Live events window (`/callouts events`, alias `/callouts debug`)
Renamed from "debug" (review fix — it is the primary authoring surface, PRD FR-8.3)
and reachable via the `[👁 Watch events…]` toolbar button.
```
┌ Callouts — Live events ─────────────────────────────  [Pause ⏸] ─┐
│ Show: ☑ Chat ☑ Casts ☑ Status ☑ Duty ☑ VFX ☑ Markers   [Clear]   │
├──────────────────────────────────────────────────────────────────┤
│ 21:14:02 ⚔ Cast   Zenos casts "Storm of Swords" (id 12345)  [＋] │
│ 21:14:01 ✨ VFX   vfx/lockon/eff/m0244trg_a4t.avfx → YOU     [＋] │
│ 21:13:58 💬 Chat  [Party] Krile: pull in 5                   [＋] │
│ 21:13:55 ⭐ Status "Well Fed" removed from YOU               [＋] │
├──────────────────────────────────────────────────────────────────┤
│ Dropped by rate limit this session: 0                            │
└──────────────────────────────────────────────────────────────────┘
```
- `[＋]` = "create rule from this event": opens the editor pane pre-filled with the
  exact channel/action-id/status-id/vfx-path — the user never types an id by hand.
- Ring buffer (default 200, configurable); session-only, never persisted (mirrors
  LootDistributionInfo's DebugEventBuffer pattern).
- Consideration: auto-open this window docked beside the editor when the user manually
  picks an id-based source (cast/status/vfx).

### 7.4 Settings (`/callouts config`)
- Master enable; **Advanced sources** toggle with per-source health lines
  (`VFX hook: Active` / `Head markers: FAILED — blocked after game patch, see README`);
  global rate limit; default cooldown; feed buffer size; config export/backup;
  import rules; **import starter rules** (permanent home).

### 7.5 Test-fire contract (review fix; PRD FR-8.4)
Test-fire (row ▶ and editor buttons) injects a **synthetic TriggerEvent** with canned
placeholder values (`{sender}=TestSender`, `$1=example`, …), **bypasses** cooldown and
rate-limit gates, does **not** increment the session fire counter, and works regardless
of enabled state or source health — on a patch day it proves "your outputs still work,
only detection is down." All test output is prefixed `[test]` (Echo and toast alike)
so a mid-duty test is never mistaken for a real alert.

### 7.6 Combat & liveness semantics (explicit, not an omission)
No combat lockout: editing is allowed anytime. Edits apply **atomically on Save**; the
engine never evaluates a half-edited rule. Test outputs are always `[test]`-marked, so
a test Error-toast during prog is distinguishable from a real one.

## 8. Testing strategy

Same philosophy as the siblings: no mocking framework, no Dalamud types in tests.

| Area | Tests (examples) |
|---|---|
| TriggerMatcher (chat) | contains/regex, case sensitivity, channel filter, sender filter, capture groups, Echo-channel hard exclusion |
| TriggerMatcher (cast/status/vfx/marker/duty) | id & name matching, caster filter, scope filters (self/party/target), gained vs removed, named-marker mapping |
| Cast session diffing | **same actor + same action: cast → idle → cast fires twice**; map pruned for despawned EntityIds |
| RuleRuntimeState | every (Enabled × toggle × source health × error) combination → correct status; engine never mutates Enabled |
| PlaceholderRenderer | every placeholder, missing values → empty, `$n` bounds |
| CooldownGate / RateLimiter | boundary timing with injected clock, token refill, drop counting, test-fire bypass |
| RuleCodec | round-trip, id preservation, version prefix, corrupt-input rejection, **gzip-bomb rejection (size cap)**, collision detection (Skip/Replace/Keep both) |
| ConfigMigrator | each migration step, unconditional pre-migration backup, failure → backup+defaults, **downgrade refusal** |
| Rule validation | invalid regex rejected with message, ≥1 output enforced, cooldown bounds |
| Metadata | `scyt.repo.json` validity + alignment with `Callouts.json` (port from siblings) |

Estimated initial suite: ~55–70 facts. Runs three ways: `dotnet test`, the Docker
gate, and CI — identical to the sibling repos. In-game checks (hook install, sounds,
toasts, advanced positive path) follow the manual checklist in the README because they
need the live game.

## 9. Performance notes

- Chat: event-driven, zero polling cost.
- Cast/status polling: one object-table sweep per framework update, diff maps keyed by
  `EntityId` with per-sweep pruning; `StatusList` read once per actor per sweep into a
  local (each access allocates a wrapper — `BattleChara.cs:76`). Goal: no allocations
  on the no-change path **beyond Dalamud's own wrappers**.
- Rules indexed by source kind so an event only touches its kind's rules.
- Regex: compiled + 50 ms timeout; count of active regex rules surfaced in Settings.

## 10. Patch-day playbook (feeds README "upgrade path")

1. Game patch lands → Dalamud updates → stable tier keeps working once the plugin
   builds against the new API level (usually just an SDK bump + release).
2. Advanced hooks may fail signature resolution → affected sources blocked, **one-time
   Echo line + error toast at load** ("Callouts: head-marker detection failed to start
   (game patch?). N rules inactive — /callouts config."), Settings shows FAILED, rules
   flagged ⚠ in the list with the count in the window title; everything else runs and
   user `Enabled` flags are untouched.
3. Maintainer updates signatures, bumps patch version, releases via
   `prepare_release.py`; users update through the custom repo as usual — blocked rules
   reactivate automatically.

## 11. Decisions taken / deferred

| Decision | Status |
|---|---|
| Name: **Callouts**, InternalName `Callouts`, commands `/callouts` | ✅ decided |
| v1 includes advanced tier (VFX, head markers) behind default-off toggle | ✅ decided |
| Outputs echo+sound+toast, per-rule configurable | ✅ decided |
| License AGPL-3.0 | ✅ decided |
| `Enabled` = user intent; separate non-persisted runtime status | ✅ decided (review) |
| Rule editor = non-modal right-hand pane | ✅ decided (review) |
| Sound API: `UIGlobals.PlayChatSoundEffect`, 1-based ids | ✅ verified locally; runtime smoke test at M2 |
| Starter rules: importable pack (Settings + empty state) | ✅ decided (OQ-3) |
| Duty wipe/recommence source in M2 | ✅ decided (OQ-4) |
| Tethers | ⏳ post-1.0 (v1.x upgrade path) |
| Head markers: **Option A** (VFX paths + `MarkerMapping`) | ✅ decided (issue 015); ActorControl (Option B) deferred |
| VFX-create memory signature | ⏳ HITL: empty in source, verified in-game at release (issue 017) |
