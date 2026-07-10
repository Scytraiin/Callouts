# Callouts

Callouts is a Dalamud plugin for FFXIV that lets you build your own local alert
rules. When a game event you care about happens, the plugin can respond with an
Echo message, a sound effect, a toast, or any combination of the three.

The goal is simple: give players a flexible way to react to information already
available in the client without writing scripts or installing fight-specific
modules.

Everything stays local. Callouts does not automate gameplay, does not send chat
visible to other players, and does not transmit your data anywhere.

## What it does

- Watch for chat and log lines, including contains and regex matching.
- Watch for enemy cast starts, status gains and removals, and duty wipe or
  recommence events.
- Optionally watch advanced hook-based events such as actor VFX spawns and head
  markers.
- Turn matching events into Echo lines, sounds, and toasts.
- Let you create rules directly from a live event feed instead of manually
  hunting ids and paths.
- Import, export, and share rule packs safely.

## Typical uses

- Alert on a ready check, countdown, or wipe.
- Warn when a specific boss cast starts.
- Notify when food falls off.
- Highlight head markers or other visual mechanics for your own character or
  party.
- Build personal reminders from chat lines, macros, or combat events.

## Features

### Trigger sources

- Chat and log lines
- Enemy cast starts
- Status gained or removed on self, party, target, or anyone depending on rule
  scope
- Duty wipe and recommence events
- Advanced sources for VFX spawns and head markers

### Outputs

- Echo text with placeholders such as `{sender}`, `{action}`, `{status}`, and
  regex capture groups like `$1`
- Sound effects `1` through `16`
- Toast notifications in Normal, Quest, or Error style

### Rule authoring

- A rules window for creating, editing, duplicating, bulk-enabling, and
  disabling rules
- A live events window with one-click "create rule from this event"
- Per-rule cooldowns and a global rate limit
- Import and export support for sharing packs
- A starter pack for common quality-of-life alerts

## Commands

```text
/callouts          Open the rules window
/callouts config   Open settings
/callouts events   Open the live events window
```

## Installation

1. Open Dalamud settings and add this custom repository:
   `https://raw.githubusercontent.com/Scytraiin/MyDalamudPlugins/main/pluginmaster.json`
2. Open the Plugin Installer.
3. Search for `Callouts`.
4. Install or update normally through Dalamud.

## Notes and limitations

- The stable cast trigger only sees actions with a cast bar. Instant abilities
  are not visible to that source.
- The plugin's own Echo output is excluded from matching to prevent feedback
  loops, so rules cannot trigger on the plugin's own messages or your own
  `/echo` macros.
- Stable sources rely on documented Dalamud APIs and are expected to remain the
  most resilient across patches.
- Advanced sources use game-function hooks and can break on patch days. If an
  advanced source fails, it disables itself and the rest of the plugin keeps
  working.

## Configuration safety

Rules are stored in the plugin configuration and migrated forward when the
schema changes. When the stored config version differs from the running plugin
version, the original file is backed up before migration runs. Downgrades are
refused safely instead of risking silent data loss.

## Development

This repository contains the plugin, its test project, and release metadata.

- `Callouts/` - main plugin project
- `Callouts.Tests/` - xUnit tests for the Dalamud-free core
- `scyt.repo.json` - custom repository entry metadata
- `scripts/prepare_release.py` - release preparation helper

### Local validation

The Docker workflow runs the test suite first and only builds plugin artifacts
when the tests pass.

```bash
docker build -t callouts-ci .
docker run --rm callouts-ci
docker run --rm \
  -v "/path/to/your/Hooks/dev:/dalamud:ro" \
  -v "$PWD/out:/out" \
  callouts-ci
```

### Release workflow

Release prep is handled by `scripts/prepare_release.py`. It updates version
metadata, rebuilds through Docker, and refreshes the packaged output in `out/`.

```bash
python3 scripts/prepare_release.py \
  --workspace . \
  --version vX.Y.Z \
  --dalamud-dev-path /path/to/Hooks/dev
```

## License

AGPL-3.0. See [LICENSE.md](LICENSE.md).
