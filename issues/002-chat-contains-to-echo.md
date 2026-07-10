# 002 — First tracer: chat-contains rule → Echo output

## Parent PRD
[PRD.md](../PRD.md) — FR-1 (rule engine, enabled-vs-active model), FR-2.1 (chat source, contains mode), FR-4 (Echo output), FR-6 (anti-loop hard rule), FR-8.1/8.2 (minimal versions)

## Type
AFK

## What to build
The first complete user-visible feature, cutting through every runtime layer: a user creates a rule "when a chat line contains *X*, echo *Y*" in the UI, and it fires in-game.

- **Core (Dalamud-free, tested)**: `Rule` model (id, name, `Enabled` = user intent, chat source spec with channel list + contains-pattern + case sensitivity + optional sender filter, echo output spec, fixed default cooldown for now), `TriggerEvent` record, `ChatTriggerMatcher` (contains mode only), `RuleEngine.Process(event) → AlertAction[]`, `RuleRuntimeState` skeleton (`Active`/`DisabledByUser`; the other states arrive with issues 004/014).
- **Source**: `ChatSource` subscribing `IChatGui.ChatMessage`; SeString → plain text via a `SeStringDisplayText`-style utility (port the pattern from LootDistributionInfo); **hard drop of `XivChatType.Echo` (56) before matching** — non-configurable (DESIGN.md §3.1).
- **Sink**: `EchoSink` printing via `IChatGui.Print(new XivChatEntry { Type = XivChatType.Echo, ... })`.
- **UI**: first real Rules window — rule list (enable checkbox, name, edit/delete) and a minimal editor pane (name, channel multi-select or Any, pattern, echo text, Save/Cancel). Golden-path defaults per FR-8.2: new rule = Chat / Any / Contains, Echo enabled, focus in pattern field.
- **Persistence**: rules saved via `IPluginConfiguration` (`Version = 1`); reload restores them.
- **Tests**: matcher (contains, case sensitivity, channel filter, sender filter), the Echo-exclusion rule, engine dispatch, rule validation (non-empty pattern, echo text required when echo enabled).

## What happens (behavior & data flow)
1. The game client receives a chat line (already decoded by the game); Dalamud's `ChatGui` detour raises `ChatMessage` with type, sender, and SeString message.
2. `ChatSource` drops the event if type is Echo; otherwise it normalizes to `TriggerEvent { Kind=Chat, Channel, Sender, Message }` (plain text).
3. `RuleEngine` runs every *active* chat rule's matcher; a hit produces an `AlertAction(Echo, text)`.
4. `EchoSink` prints the text to the Echo channel — visible only to this client, exactly like typing `/e text`.
5. The printed line re-enters `ChatMessage` as Echo and is dropped by step 2 — the loop is structurally impossible (PRD acceptance §10.2).

## Network traffic
**None.** The plugin only observes chat events the game client has already received and decoded; it initiates no connections and sends no packets. The Echo output is a purely client-local UI print — nothing is transmitted to the game server or other players.

## Acceptance criteria
- Create "ready check" rule via UI → party member (or a second chat channel like `/say`) produces the line → Echo output appears; disabling the rule stops it; rules survive a plugin reload.
- A rule whose pattern matches its own echo text does **not** re-fire (unit test + in-game check).
- Sender filter and channel filter behave per FR-2.1 (unit tests).
- Editor opens with the FR-8.2 golden-path defaults; Save is blocked with an inline reason when the pattern is empty.
- All new core classes are compile-linked into the test project; suite passes in the Docker gate.

## Blocked by
- [issues/001-walking-skeleton-pipeline.md](001-walking-skeleton-pipeline.md)

## User stories addressed
- US-1 (raid lead ready-check — partial: echo only; sound completes it in issue 006)
