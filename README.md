# Callouts

> **Draft README for the future plugin repository** — written at PRD stage.
> Sections marked *(placeholder)* are filled in during implementation.

Workspace for the `Callouts` Dalamud plugin: user-defined trigger rules that turn
in-game events into local alerts — Echo messages, sound effects, and toasts.

**When something you care about happens, Callouts tells you.** No scripting, no
per-fight modules: you define a rule in the UI ("when a chat line contains X",
"when an enemy starts casting Y", "when a marker appears over my head") and pick
what should happen (echo text with placeholders, one of the 16 alert sounds, a toast).

Everything is local and read-only: Callouts never sends chat visible to others,
never automates gameplay, and never transmits data anywhere.

## Features

- **Trigger sources**
  - *Stable tier (always available)*: chat/log lines (contains or regex, with capture
    groups), enemy cast starts, status effects gained/removed (on you, your party
    members, or your current target), duty wipe/recommence events.
  - *Advanced tier (opt-in toggle)*: VFX spawns on actors and head markers (e.g.
    spread/stack icons over players). These use game-function hooks and can break on
    game patches — see [Upgrade path](#upgrade-path).
- **Outputs, per rule**: Echo message (placeholders like `{sender}`, `{action}`, `$1`),
  game sound effect 1–16, on-screen toast (Normal/Quest/Error) — any combination.
- **Live events**: a live window showing candidate events with one-click
  **"create rule from this event"** — you never have to look up an action id or VFX path.
- **Safety rails**: rules can never trigger on the plugin's own Echo output; per-rule
  cooldowns and a global rate limit prevent spam; a broken regex disables only its rule
  (your on/off choices are never overwritten by the plugin).
- **Sharing**: export/import rules as clipboard strings, with duplicate-safe updates
  of shared rule packs. A starter-rule pack (ready check, countdown, food expiry) can
  be imported with one click.

**Known limitations (by design, v1):** the stable cast trigger only sees actions
*with a cast bar* — instant abilities are invisible to it (an advanced "action used"
trigger is on the roadmap). And because the plugin's own output lives in the Echo
channel (which is excluded from matching to prevent feedback loops), rules cannot
trigger on your own `/echo` macros.

## Commands

```text
/callouts          # rules window
/callouts config   # settings (incl. Advanced sources toggle)
/callouts events   # live events window (/callouts debug works as alias)
```

## Installation

1. Add the custom repository to Dalamud (Settings → Experimental → Custom Plugin
   Repositories):
   `https://raw.githubusercontent.com/Scytraiin/MyDalamudPlugins/main/pluginmaster.json`
2. Search for **Callouts** in the Plugin Installer and install.

### Release status

- Current plugin version: `0.1.0`
- the current release target is `v0.1.0-alpha`

## Upgrade path

### How updates reach you
Releases are published as GitHub Releases (`latest.zip`) and picked up automatically
through the custom repository. Your rules live in the plugin configuration and are
**migrated automatically** between versions: the config carries a version number, and
**before any migration your old config is backed up on disk** — an upgrade (or even a
downgrade, which is refused safely) can never silently lose rules.

### Staged feature roadmap
| Stage | Version | What you get |
|---|---|---|
| Alpha | v0.1.x | Chat triggers + Echo output, core UI |
| Alpha | v0.2.x | Cast, status & duty-event triggers, sounds, toasts, placeholders, Live events window |
| Beta | v0.3.x | Advanced tier: VFX & head-marker triggers behind the opt-in toggle |
| Stable | v1.0 | Import/export, zone scoping, bulk operations, starter-rule pack, polish |
| Later | v1.x+ | Tether triggers, "action used" trigger for instant casts (advanced), TTS output (candidate), localized UI (candidate) |

### Game-patch behavior (important for the Advanced tier)
- **Stable-tier sources** (chat, casts, status, duty events) use documented Dalamud
  APIs and keep working across game patches once the plugin is built for the current
  Dalamud API level.
- **Advanced-tier sources** rely on memory signatures that a game patch can invalidate.
  When that happens they **disable themselves automatically** — the plugin keeps
  running, and you get **one clear in-game notice at login** (an Echo line + error
  toast naming the failed source and how many rules are affected), so you find out
  before a pull, not during one. Affected rules are flagged ⚠ in the rule list; your
  enabled/disabled choices are untouched. A plugin update restores them automatically.
- If you want maximum patch-day robustness, simply leave the Advanced sources toggle
  off — the stable tier is unaffected by design.

## Local validation & build *(placeholder — mirrors sibling repos)*

The Docker workflow keeps validation reproducible without a host .NET install.
Unit tests gate the build: the image will not produce a plugin package unless the
full test suite passes.

```bash
docker build -t callouts-ci .
docker run --rm callouts-ci                            # tests only
docker run --rm \
  -v "/path/to/your/Hooks/dev:/dalamud:ro" \
  -v "$PWD/out:/out" callouts-ci                       # tests + plugin build
```

Releases are prepared with `scripts/prepare_release.py --workspace . --version vX.Y.Z
--dalamud-dev-path /path/to/Hooks/dev`, which syncs all version metadata, rebuilds,
and verifies the packaged artifacts.

## Manual in-game test checklist *(placeholder)*

- [ ] Chat rule fires on a party message; does not fire on its own Echo output
- [ ] Cast rule fires once per cast — and fires **again** when the boss recasts the
      same spell back-to-back
- [ ] Status rule fires on food expiry
- [ ] Sound preview and toast test buttons work in the editor; test output is
      `[test]`-prefixed
- [ ] Advanced toggle installs/removes hooks (health indicators change)
- [ ] Simulated hook failure blocks only that source and shows the login notice;
      rule checkboxes remain as the user set them

## Project layout

- `Callouts/` — the Dalamud plugin (Dalamud API 15, .NET 10)
- `Callouts.Tests/` — xUnit v3 tests for the Dalamud-free core
- `scyt.repo.json` — custom Dalamud repository metadata
- `release-notes/` — per-release notes used for GitHub releases
- `PRD.md`, `DESIGN.md` — product requirements and design proposal

## License

AGPL-3.0 — see [LICENSE.md](LICENSE.md).
