# 001 — Walking skeleton: loadable plugin + test-gated pipeline

## Parent PRD
[PRD.md](../PRD.md) — §8 (Distribution, build & release), FR-10 (partial), NFR-5 (partial)

## Type
AFK

## What to build
The complete repository scaffold with a do-almost-nothing plugin that proves every layer of the delivery pipeline end-to-end: source tree → unit test → Docker gate → CI → packaged artifact → loadable in-game plugin with one working command.

Concretely:
- `Callouts/Callouts.csproj` on `Dalamud.NET.Sdk/15.0.0`, `net10.0-windows`, nullable + implicit usings, version `0.1.0`.
- `Callouts/Plugin.cs` implementing `IDalamudPlugin`: registers `/callouts`, which toggles a stub ImGui window ("Callouts — coming soon"). Window system + disposal wired correctly.
- `Callouts/Callouts.json` plugin manifest (InternalName `Callouts`, punchline, AGPL note, repo URL placeholder).
- `Callouts.Tests/Callouts.Tests.csproj` (xUnit v3, same package set as the siblings), compile-linking `Core/` files (starts with one placeholder core class + test so the link mechanism is proven).
- `scyt.repo.json` + the two metadata tests ported from the siblings (JSON validity; repo↔manifest alignment).
- `Dockerfile`: identical test-gated pattern to the siblings — restore & run tests in Release (hard gate) → require `/dalamud` mount or exit 2 → build with `EnableWindowsTargeting` → export to `/out/plugin`.
- `.github/workflows/ci.yml`: `test` job (setup .NET 10, `dotnet test` Release) + `docker-smoke` job, path filters adapted; copied from `ffxiv-loot-distribution`.
- `scripts/prepare_release.py` ported from VoiceDirectorV2 with names/paths adapted (not exercised for a real release yet — that's issue 017).
- `LICENSE.md` (AGPL-3.0), `.gitignore`, `release-notes/` folder with `v0.1.0-alpha.md` stub.

See DESIGN.md §6 for the exact layout.

## What happens (behavior & data flow)
- In-game: the user types `/callouts`; Dalamud's `ICommandManager` invokes the handler; the stub window opens/closes via the Dalamud `WindowSystem`. No game state is read, nothing is persisted yet.
- Build: `docker run` executes `dotnet test` first; any test failure aborts before the plugin is ever built. With `/dalamud` mounted, the plugin DLL + manifest + deps.json land in `/out/plugin/`.
- CI: every push/PR runs the test suite on ubuntu and smoke-builds the Docker image (no Dalamud mount in CI — validation-only mode, exit 2 path is the expected success signal for the smoke job's build step, matching the sibling setup).

## Network traffic
- **Runtime (plugin): none.** The plugin opens no sockets, performs no HTTP calls, and sends nothing to game servers or third parties.
- **Build-time only**: `docker build`/CI restore pulls the .NET SDK base image from `mcr.microsoft.com` and NuGet packages (Dalamud SDK, xUnit) from `nuget.org` / the Dalamud package feed. This is standard toolchain traffic, identical to the sibling repos.

## Acceptance criteria
- `docker build && docker run` (no mount) runs the placeholder + metadata tests and exits 2 with the "mount DALAMUD_HOME" message; with a mounted dev folder it exports `Callouts.dll`, `Callouts.json`, `Callouts.deps.json` to `out/plugin/`.
- CI workflow file present with both jobs; `dotnet test` passes locally.
- Plugin loads in-game via dev-plugin location; `/callouts` opens and closes the stub window without errors in `/xllog`.
- Metadata tests fail if `scyt.repo.json` and `Callouts.json` disagree on InternalName/Author/RepoUrl (prove by intentional temporary mismatch during development).
- Repo contains LICENSE.md (AGPL-3.0) and the DESIGN.md §6 folder layout.

## Blocked by
None — can start immediately.

## User stories addressed
None directly — this slice is the tracer through the *pipeline* layers that every other slice rides on (PRD §8, NFR-5).
