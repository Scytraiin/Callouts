using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

using Callouts.Core.Engine;
using Callouts.Core.Rules;

namespace Callouts.Windows;

/// <summary>
/// The main Rules window: filterable list + non-modal editor pane. Covers all stable sources,
/// placeholders, Echo/Sound/Toast outputs, scoping, bulk operations with undo, master enable,
/// status badges, and create-rule-from-event.
/// </summary>
public sealed class RulesWindow : Window, IDisposable
{
    private static readonly Vector4 ErrorColor = new(0.9f, 0.4f, 0.4f, 1f);
    private static readonly Vector4 OkColor = new(0.4f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 WarnColor = new(0.95f, 0.75f, 0.35f, 1f);

    private readonly Configuration configuration;
    private readonly RuleEngine engine;
    private readonly Action saveConfiguration;
    private readonly Action<AlertAction> previewAction;
    private readonly Action openEvents;
    private readonly Action openSettings;
    private readonly Func<(uint Id, string Name)> currentZone;

    private Rule? draft;
    private string? editingId;
    private bool focusPatternNextFrame;
    private string testerInput = string.Empty;
    private string? statusMessage;

    // Filters
    private string search = string.Empty;
    private TriggerKind? sourceFilter;
    private bool enabledOnly;

    // Undo of the last delete (bulk or single)
    private readonly List<(int Index, Rule Rule)> lastDeleted = new();

    public RulesWindow(
        Configuration configuration,
        RuleEngine engine,
        Action saveConfiguration,
        Action<AlertAction> previewAction,
        Action openEvents,
        Action openSettings,
        Func<(uint Id, string Name)> currentZone)
        : base("Callouts — Rules###CalloutsRules")
    {
        this.configuration = configuration;
        this.engine = engine;
        this.saveConfiguration = saveConfiguration;
        this.previewAction = previewAction;
        this.openEvents = openEvents;
        this.openSettings = openSettings;
        this.currentZone = currentZone;

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 480),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    /// <summary>Opens the editor pre-filled from a Live-events entry (issue 010).</summary>
    public void BeginCreateFromEvent(TriggerEvent evt)
    {
        var rule = new Rule
        {
            Name = SuggestName(evt),
            CooldownSeconds = this.configuration.Options.DefaultCooldownSeconds,
            Source = BuildSourceFromEvent(evt),
            Outputs = new OutputSpec { Echo = new EchoOutput { Enabled = true, Text = SuggestEchoText(evt) } },
        };

        this.draft = rule;
        this.editingId = null;
        this.focusPatternNextFrame = true;
        this.testerInput = evt.Kind == TriggerKind.Chat ? evt.Message : string.Empty;
        this.statusMessage = null;
        this.IsOpen = true;
    }

    public override void Draw()
    {
        this.DrawBanners();
        this.DrawToolbar();
        ImGui.Separator();
        this.DrawRuleList();

        if (this.draft is not null)
        {
            ImGui.Separator();
            this.DrawEditor();
        }
    }

    private void DrawBanners()
    {
        if (!this.engine.MasterEnabled)
        {
            ImGui.TextColored(WarnColor, "Callouts is globally disabled.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Turn on"))
            {
                this.engine.MasterEnabled = true;
                this.configuration.Options.MasterEnabled = true;
                this.saveConfiguration();
            }
        }

        var inactive = this.CountNeedsAttention();
        if (inactive > 0)
        {
            ImGui.TextColored(WarnColor, $"⚠ {inactive} rule(s) need attention.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Open Settings"))
            {
                this.openSettings();
            }
        }
    }

    private void DrawToolbar()
    {
        if (ImGui.Button("＋ New rule"))
        {
            this.BeginCreate();
        }

        ImGui.SameLine();
        if (ImGui.Button("👁 Watch events"))
        {
            this.openEvents();
        }

        ImGui.SameLine();
        if (ImGui.Button("⚙ Settings"))
        {
            this.openSettings();
        }

        // Filters
        var searchText = this.search;
        ImGui.SetNextItemWidth(180);
        if (ImGui.InputTextWithHint("##search", "Search name…", ref searchText, 128))
        {
            this.search = searchText;
        }

        ImGui.SameLine();
        this.DrawSourceFilter();

        ImGui.SameLine();
        var enabled = this.enabledOnly;
        if (ImGui.Checkbox("Enabled only", ref enabled))
        {
            this.enabledOnly = enabled;
        }
    }

    private void DrawSourceFilter()
    {
        var label = this.sourceFilter is null ? "All sources" : this.sourceFilter.Value.ToString();
        ImGui.SetNextItemWidth(140);
        if (!ImGui.BeginCombo("##sourcefilter", label))
        {
            return;
        }

        if (ImGui.Selectable("All sources", this.sourceFilter is null))
        {
            this.sourceFilter = null;
        }

        foreach (var kind in new[] { TriggerKind.Chat, TriggerKind.Cast, TriggerKind.Status, TriggerKind.DutyEvent, TriggerKind.Vfx, TriggerKind.HeadMarker })
        {
            if (ImGui.Selectable(kind.ToString(), this.sourceFilter == kind))
            {
                this.sourceFilter = kind;
            }
        }

        ImGui.EndCombo();
    }

    private void DrawRuleList()
    {
        var shown = RuleListView.Filter(this.configuration.Rules, this.search, this.sourceFilter, this.enabledOnly);

        if (this.configuration.Rules.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextWrapped("No rules yet. Click \"＋ New rule\", or open \"👁 Watch events\" and click ＋ on any line to build a rule from it.");
            ImGui.Spacing();
            return;
        }

        this.DrawBulkBar(shown);

        if (!ImGui.BeginTable("rules", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY))
        {
            return;
        }

        ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 32f);
        ImGui.TableSetupColumn("Name");
        ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 110f);
        ImGui.TableSetupColumn("Fires", ImGuiTableColumnFlags.WidthFixed, 48f);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 150f);
        ImGui.TableHeadersRow();

        Rule? ruleToDelete = null;

        foreach (var rule in shown)
        {
            ImGui.TableNextRow();
            ImGui.PushID(rule.Id);

            ImGui.TableNextColumn();
            var enabled = rule.Enabled;
            if (ImGui.Checkbox("##enabled", ref enabled))
            {
                rule.Enabled = enabled;
                this.saveConfiguration();
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(string.IsNullOrWhiteSpace(rule.Name) ? "(unnamed)" : rule.Name);
            this.DrawStateBadge(rule);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(DescribeSource(rule));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(this.engine.GetFireCount(rule.Id).ToString());

            ImGui.TableNextColumn();
            if (ImGui.SmallButton("Test"))
            {
                this.TestFire(rule);
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Edit"))
            {
                this.BeginEdit(rule);
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Del"))
            {
                ruleToDelete = rule;
            }

            ImGui.PopID();
        }

        ImGui.EndTable();

        if (ruleToDelete is not null)
        {
            this.DeleteRules(new[] { ruleToDelete });
        }
    }

    private void DrawBulkBar(IReadOnlyList<Rule> shown)
    {
        ImGui.TextDisabled($"Showing {shown.Count} of {this.configuration.Rules.Count}.");
        ImGui.SameLine();
        if (ImGui.SmallButton("Enable shown"))
        {
            foreach (var rule in shown)
            {
                rule.Enabled = true;
            }

            this.saveConfiguration();
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Disable shown"))
        {
            foreach (var rule in shown)
            {
                rule.Enabled = false;
            }

            this.saveConfiguration();
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Delete shown"))
        {
            this.DeleteRules(shown);
        }

        if (this.lastDeleted.Count > 0)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton($"Undo delete ({this.lastDeleted.Count})"))
            {
                this.UndoDelete();
            }
        }
    }

    private void DrawStateBadge(Rule rule)
    {
        var state = this.engine.GetRuntimeState(rule);
        switch (state)
        {
            case RuleRuntimeState.RuleError:
                ImGui.SameLine();
                ImGui.TextColored(ErrorColor, "⛔");
                if (ImGui.IsItemHovered() && this.engine.TryGetError(rule.Id, out var message))
                {
                    ImGui.SetTooltip(message);
                }

                break;

            case RuleRuntimeState.BlockedAdvancedOff:
                ImGui.SameLine();
                ImGui.TextColored(WarnColor, "⚠");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Blocked — advanced sources are off (see Settings).");
                }

                break;

            case RuleRuntimeState.SourceFailed:
                ImGui.SameLine();
                ImGui.TextColored(ErrorColor, "⚠");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Blocked — source failed (game patch?). See Settings.");
                }

                break;

            case RuleRuntimeState.DisabledByUser:
                ImGui.SameLine();
                ImGui.TextDisabled("(off)");
                break;
        }
    }

    private void DrawEditor()
    {
        var d = this.draft!;
        ImGui.TextUnformatted(this.editingId is null ? "New rule" : "Edit rule");

        var name = d.Name;
        if (ImGui.InputText("Name", ref name, 128))
        {
            d.Name = name;
        }

        var enabled = d.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            d.Enabled = enabled;
        }

        ImGui.Separator();
        ImGui.TextDisabled("WHEN");
        DrawSourceKindPicker(d.Source);

        switch (d.Source.Kind)
        {
            case TriggerKind.Chat:
                this.DrawChatWhen(d);
                break;
            case TriggerKind.Cast:
                DrawCastWhen(d.Source);
                break;
            case TriggerKind.Status:
                DrawStatusWhen(d.Source);
                break;
            case TriggerKind.DutyEvent:
                DrawDutyWhen(d.Source);
                break;
        }

        ImGui.Separator();
        ImGui.TextDisabled($"THEN  (placeholders: {PlaceholderHint(d.Source.Kind)})");
        this.DrawEchoOutput(d);
        this.DrawSoundOutput(d);
        this.DrawToastOutput(d);

        ImGui.Separator();
        ImGui.TextDisabled("OPTIONS");
        this.DrawOptions(d);

        ImGui.Separator();
        foreach (var error in RuleValidator.Validate(d))
        {
            ImGui.TextColored(ErrorColor, $"⚠ {error}");
        }

        if (ImGui.Button("Save rule"))
        {
            this.TrySave();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            this.CancelEdit();
        }

        if (!string.IsNullOrEmpty(this.statusMessage))
        {
            ImGui.SameLine();
            ImGui.TextColored(OkColor, this.statusMessage);
        }
    }

    private void DrawOptions(Rule d)
    {
        var cooldown = (float)d.CooldownSeconds;
        if (ImGui.InputFloat("Cooldown (sec)", ref cooldown))
        {
            d.CooldownSeconds = Math.Clamp(cooldown, 0f, 3600f);
        }

        var onlyCombat = d.Scope.OnlyInCombat;
        if (ImGui.Checkbox("Only in combat", ref onlyCombat))
        {
            d.Scope.OnlyInCombat = onlyCombat;
        }

        ImGui.SameLine();
        var onlyDuty = d.Scope.OnlyInDuty;
        if (ImGui.Checkbox("Only in duty", ref onlyDuty))
        {
            d.Scope.OnlyInDuty = onlyDuty;
        }

        // Zone restriction
        if (d.Scope.TerritoryIds.Count == 0)
        {
            ImGui.TextDisabled("Zones: everywhere");
        }
        else
        {
            ImGui.TextDisabled($"Zones: {d.Scope.TerritoryIds.Count} restricted");
        }

        var zone = this.currentZone();
        if (zone.Id != 0)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton($"Add current ({zone.Name})"))
            {
                if (!d.Scope.TerritoryIds.Contains(zone.Id))
                {
                    d.Scope.TerritoryIds.Add(zone.Id);
                }
            }
        }

        if (d.Scope.TerritoryIds.Count > 0)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear zones"))
            {
                d.Scope.TerritoryIds.Clear();
            }
        }
    }

    private static void DrawSourceKindPicker(SourceSpec source)
    {
        if (!ImGui.BeginCombo("Source", SourceKindLabel(source.Kind)))
        {
            return;
        }

        foreach (var kind in new[] { TriggerKind.Chat, TriggerKind.Cast, TriggerKind.Status, TriggerKind.DutyEvent })
        {
            if (ImGui.Selectable(SourceKindLabel(kind), source.Kind == kind))
            {
                source.Kind = kind;
            }
        }

        ImGui.EndCombo();
    }

    private void DrawChatWhen(Rule d)
    {
        this.DrawChannelPicker(d.Source);
        this.DrawMatchModePicker(d.Source);

        var caseSensitive = d.Source.CaseSensitive;
        if (ImGui.Checkbox("Case sensitive", ref caseSensitive))
        {
            d.Source.CaseSensitive = caseSensitive;
        }

        var patternLabel = d.Source.MatchMode == MatchMode.Regex ? "Pattern (regex)" : "Text contains";
        var pattern = d.Source.Pattern;
        if (this.focusPatternNextFrame)
        {
            ImGui.SetKeyboardFocusHere();
            this.focusPatternNextFrame = false;
        }

        if (ImGui.InputTextWithHint(patternLabel, "e.g. has initiated a ready check", ref pattern, 512))
        {
            d.Source.Pattern = pattern;
        }

        if (d.Source.MatchMode == MatchMode.Regex)
        {
            ImGui.TextDisabled("Capture groups are available as $1..$9 in output text.");
        }

        var sender = d.Source.SenderPattern ?? string.Empty;
        if (ImGui.InputTextWithHint("Sender contains (optional)", "any sender", ref sender, 128))
        {
            d.Source.SenderPattern = string.IsNullOrWhiteSpace(sender) ? null : sender;
        }

        this.DrawLiveTester(d);
    }

    private static void DrawCastWhen(SourceSpec s)
    {
        var actionId = s.ActionId;
        if (ImGui.InputInt("Action id (0 = any)", ref actionId))
        {
            s.ActionId = Math.Max(0, actionId);
        }

        var actionName = s.ActionNameContains ?? string.Empty;
        if (ImGui.InputTextWithHint("Action name contains", "e.g. Ultima", ref actionName, 128))
        {
            s.ActionNameContains = string.IsNullOrWhiteSpace(actionName) ? null : actionName;
        }

        var scope = s.CasterScope;
        EnumCombo("Caster", ref scope);
        s.CasterScope = scope;

        var caster = s.CasterNameContains ?? string.Empty;
        if (ImGui.InputTextWithHint("Caster name contains (optional)", "any caster", ref caster, 128))
        {
            s.CasterNameContains = string.IsNullOrWhiteSpace(caster) ? null : caster;
        }

        var onlyMe = s.OnlyTargetingMe;
        if (ImGui.Checkbox("Only when targeting me", ref onlyMe))
        {
            s.OnlyTargetingMe = onlyMe;
        }

        var onlyParty = s.OnlyTargetingParty;
        if (ImGui.Checkbox("Only when targeting a party member", ref onlyParty))
        {
            s.OnlyTargetingParty = onlyParty;
        }

        ImGui.TextDisabled("Tip: only actions with a cast bar are detected. Use Watch events to find ids.");
    }

    private static void DrawStatusWhen(SourceSpec s)
    {
        var statusId = s.StatusId;
        if (ImGui.InputInt("Status id (0 = any)", ref statusId))
        {
            s.StatusId = Math.Max(0, statusId);
        }

        var statusName = s.StatusNameContains ?? string.Empty;
        if (ImGui.InputTextWithHint("Status name contains", "e.g. Well Fed", ref statusName, 128))
        {
            s.StatusNameContains = string.IsNullOrWhiteSpace(statusName) ? null : statusName;
        }

        var change = s.StatusChange;
        EnumCombo("Change", ref change);
        s.StatusChange = change;

        var bearer = s.Bearer;
        EnumCombo("On", ref bearer);
        s.Bearer = bearer;

        var minStacks = s.MinStacks;
        if (ImGui.InputInt("Min stacks (0 = any)", ref minStacks))
        {
            s.MinStacks = Math.Max(0, minStacks);
        }
    }

    private static void DrawDutyWhen(SourceSpec s)
    {
        var options = new[] { DutyEventFilter.Any, DutyEventFilter.Wiped, DutyEventFilter.Recommenced };
        if (!ImGui.BeginCombo("Duty event", s.DutyEvent.ToString()))
        {
            return;
        }

        foreach (var option in options)
        {
            if (ImGui.Selectable(option.ToString(), s.DutyEvent == option))
            {
                s.DutyEvent = option;
            }
        }

        ImGui.EndCombo();
    }

    private static void EnumCombo<TEnum>(string label, ref TEnum value)
        where TEnum : struct, Enum
    {
        if (!ImGui.BeginCombo(label, value.ToString()))
        {
            return;
        }

        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (ImGui.Selectable(candidate.ToString(), candidate.Equals(value)))
            {
                value = candidate;
            }
        }

        ImGui.EndCombo();
    }

    private void DrawMatchModePicker(SourceSpec source)
    {
        var preview = source.MatchMode == MatchMode.Regex ? "Regex" : "Contains";
        if (!ImGui.BeginCombo("Match", preview))
        {
            return;
        }

        if (ImGui.Selectable("Contains", source.MatchMode == MatchMode.Contains))
        {
            source.MatchMode = MatchMode.Contains;
        }

        if (ImGui.Selectable("Regex", source.MatchMode == MatchMode.Regex))
        {
            source.MatchMode = MatchMode.Regex;
        }

        ImGui.EndCombo();
    }

    private void DrawLiveTester(Rule d)
    {
        var sample = this.testerInput;
        if (ImGui.InputTextWithHint("Try it", "paste a sample chat line", ref sample, 512))
        {
            this.testerInput = sample;
        }

        if (string.IsNullOrEmpty(this.testerInput) || string.IsNullOrEmpty(d.Source.Pattern))
        {
            return;
        }

        var channel = d.Source.Channels.Count > 0 ? d.Source.Channels[0] : 10;
        var evt = new TriggerEvent
        {
            Kind = TriggerKind.Chat,
            Channel = channel,
            Sender = "Tester",
            Message = this.testerInput,
        };

        Regex? compiled = null;
        if (d.Source.MatchMode == MatchMode.Regex)
        {
            if (!RegexFactory.TryCompile(d.Source.Pattern, d.Source.CaseSensitive, out compiled, out var regexError))
            {
                ImGui.TextColored(ErrorColor, $"✘ {regexError}");
                return;
            }
        }

        try
        {
            var result = ChatTriggerMatcher.Match(d, evt, compiled);
            ImGui.TextColored(result is null ? ErrorColor : OkColor, result is null
                ? "✘ no match"
                : result.Captures.Count > 0
                    ? $"✔ match — captures: {string.Join(", ", result.Captures)}"
                    : "✔ match");
        }
        catch (RegexMatchTimeoutException)
        {
            ImGui.TextColored(ErrorColor, "✘ regex timed out (>50 ms)");
        }
    }

    private void DrawEchoOutput(Rule d)
    {
        var echoEnabled = d.Outputs.Echo.Enabled;
        if (ImGui.Checkbox("Echo", ref echoEnabled))
        {
            d.Outputs.Echo.Enabled = echoEnabled;
        }

        var echoText = d.Outputs.Echo.Text;
        if (ImGui.InputTextWithHint("Echo text", "text shown in your Echo channel", ref echoText, 512))
        {
            d.Outputs.Echo.Text = echoText;
        }
    }

    private void DrawSoundOutput(Rule d)
    {
        var soundEnabled = d.Outputs.Sound.Enabled;
        if (ImGui.Checkbox("Sound", ref soundEnabled))
        {
            d.Outputs.Sound.Enabled = soundEnabled;
        }

        ImGui.SameLine();
        if (ImGui.BeginCombo("##soundId", $"Effect {d.Outputs.Sound.EffectId}"))
        {
            for (var id = SoundOutput.MinEffectId; id <= SoundOutput.MaxEffectId; id++)
            {
                if (ImGui.Selectable($"Effect {id}", d.Outputs.Sound.EffectId == id))
                {
                    d.Outputs.Sound.EffectId = id;
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Preview##sound"))
        {
            this.previewAction(new AlertAction { Kind = AlertOutputKind.Sound, SoundEffectId = d.Outputs.Sound.EffectId });
        }
    }

    private void DrawToastOutput(Rule d)
    {
        var toastEnabled = d.Outputs.Toast.Enabled;
        if (ImGui.Checkbox("Toast", ref toastEnabled))
        {
            d.Outputs.Toast.Enabled = toastEnabled;
        }

        var toastText = d.Outputs.Toast.Text;
        if (ImGui.InputTextWithHint("Toast text", "text shown on screen", ref toastText, 512))
        {
            d.Outputs.Toast.Text = toastText;
        }

        var style = d.Outputs.Toast.Style;
        EnumCombo("Style", ref style);
        d.Outputs.Toast.Style = style;

        ImGui.SameLine();
        if (ImGui.SmallButton("Test##toast"))
        {
            var text = string.IsNullOrEmpty(d.Outputs.Toast.Text) ? "Callouts test toast" : d.Outputs.Toast.Text;
            this.previewAction(new AlertAction { Kind = AlertOutputKind.Toast, Text = text, ToastStyle = d.Outputs.Toast.Style });
        }
    }

    private void DrawChannelPicker(SourceSpec source)
    {
        var preview = source.Channels.Count == 0
            ? "Any channel"
            : $"{source.Channels.Count} channel(s) selected";

        if (!ImGui.BeginCombo("Channels", preview))
        {
            return;
        }

        if (ImGui.Selectable("Any channel", source.Channels.Count == 0))
        {
            source.Channels.Clear();
        }

        ImGui.Separator();

        foreach (var entry in ChatChannelCatalog.Channels)
        {
            var selected = source.Channels.Contains(entry.Value);
            if (ImGui.Checkbox($"{entry.Label}##ch{entry.Value}", ref selected))
            {
                if (selected)
                {
                    if (!source.Channels.Contains(entry.Value))
                    {
                        source.Channels.Add(entry.Value);
                    }
                }
                else
                {
                    source.Channels.Remove(entry.Value);
                }
            }
        }

        ImGui.EndCombo();
    }

    private void TestFire(Rule rule)
    {
        var match = SyntheticMatch(rule.Source.Kind);
        foreach (var action in this.engine.BuildTestActions(rule, match))
        {
            this.previewAction(PrefixTest(action));
        }

        this.statusMessage = $"Test-fired \"{rule.Name}\".";
    }

    private static MatchResult SyntheticMatch(TriggerKind kind) => kind switch
    {
        TriggerKind.Cast => MatchResult.FromValues(("caster", "TestCaster"), ("action", "TestAction"), ("zone", "TestZone")),
        TriggerKind.Status => MatchResult.FromValues(("status", "TestStatus"), ("bearer", "You"), ("zone", "TestZone")),
        TriggerKind.DutyEvent => MatchResult.FromValues(("event", "Wiped"), ("zone", "TestZone")),
        _ => MatchResult.FromValues(("sender", "TestSender"), ("message", "test message"), ("zone", "TestZone")),
    };

    private static AlertAction PrefixTest(AlertAction action)
        => action.Kind switch
        {
            AlertOutputKind.Echo => action with { Text = $"[test] {action.Text}" },
            AlertOutputKind.Toast => action with { Text = $"[test] {action.Text}" },
            _ => action,
        };

    private void BeginCreate()
    {
        this.draft = new Rule
        {
            Name = "New rule",
            CooldownSeconds = this.configuration.Options.DefaultCooldownSeconds,
            Source = new SourceSpec { Kind = TriggerKind.Chat, MatchMode = MatchMode.Contains },
            Outputs = new OutputSpec { Echo = new EchoOutput { Enabled = true, Text = string.Empty } },
        };
        this.editingId = null;
        this.focusPatternNextFrame = true;
        this.testerInput = string.Empty;
        this.statusMessage = null;
    }

    private void BeginEdit(Rule rule)
    {
        this.draft = rule.Clone();
        this.editingId = rule.Id;
        this.focusPatternNextFrame = true;
        this.testerInput = string.Empty;
        this.statusMessage = null;
    }

    private void CancelEdit()
    {
        this.draft = null;
        this.editingId = null;
        this.focusPatternNextFrame = false;
    }

    private void TrySave()
    {
        var d = this.draft;
        if (d is null || !RuleValidator.IsValid(d))
        {
            return;
        }

        if (this.editingId is null)
        {
            this.configuration.Rules.Add(d);
        }
        else
        {
            var index = this.configuration.Rules.FindIndex(r => r.Id == this.editingId);
            if (index >= 0)
            {
                this.configuration.Rules[index] = d;
            }
            else
            {
                this.configuration.Rules.Add(d);
            }
        }

        this.saveConfiguration();
        var savedName = string.IsNullOrWhiteSpace(d.Name) ? "rule" : d.Name;
        this.CancelEdit();
        this.statusMessage = $"Saved \"{savedName}\".";
    }

    private void DeleteRules(IReadOnlyList<Rule> rules)
    {
        this.lastDeleted.Clear();
        foreach (var rule in rules)
        {
            var index = this.configuration.Rules.IndexOf(rule);
            if (index >= 0)
            {
                this.lastDeleted.Add((index, rule));
            }
        }

        // Remove from the highest index down so stored indices stay valid on undo.
        this.lastDeleted.Sort((a, b) => b.Index.CompareTo(a.Index));
        foreach (var (_, rule) in this.lastDeleted)
        {
            this.configuration.Rules.Remove(rule);
            if (this.editingId == rule.Id)
            {
                this.CancelEdit();
            }
        }

        this.saveConfiguration();
        this.statusMessage = $"Deleted {this.lastDeleted.Count} rule(s).";
    }

    private void UndoDelete()
    {
        // Restore in ascending index order so positions line up.
        var restore = new List<(int Index, Rule Rule)>(this.lastDeleted);
        restore.Sort((a, b) => a.Index.CompareTo(b.Index));
        foreach (var (index, rule) in restore)
        {
            var target = Math.Clamp(index, 0, this.configuration.Rules.Count);
            this.configuration.Rules.Insert(target, rule);
        }

        this.lastDeleted.Clear();
        this.saveConfiguration();
        this.statusMessage = "Restored deleted rule(s).";
    }

    private int CountNeedsAttention()
    {
        var count = 0;
        foreach (var rule in this.configuration.Rules)
        {
            var state = this.engine.GetRuntimeState(rule);
            if (state is RuleRuntimeState.RuleError or RuleRuntimeState.SourceFailed or RuleRuntimeState.BlockedAdvancedOff)
            {
                count++;
            }
        }

        return count;
    }

    private static SourceSpec BuildSourceFromEvent(TriggerEvent evt) => evt.Kind switch
    {
        TriggerKind.Chat => new SourceSpec
        {
            Kind = TriggerKind.Chat,
            Channels = evt.Channel != 0 ? [evt.Channel] : [],
            MatchMode = MatchMode.Contains,
            Pattern = evt.Message,
        },
        TriggerKind.Cast => new SourceSpec { Kind = TriggerKind.Cast, ActionId = evt.ActionId, CasterScope = evt.CasterIsEnemy ? CasterScope.Enemy : CasterScope.Anyone },
        TriggerKind.Status => new SourceSpec
        {
            Kind = TriggerKind.Status,
            StatusId = evt.StatusId,
            StatusChange = evt.StatusGained ? StatusChangeFilter.Gained : StatusChangeFilter.Removed,
            Bearer = evt.BearerIsSelf ? BearerScope.Self : BearerScope.Anyone,
        },
        TriggerKind.DutyEvent => new SourceSpec { Kind = TriggerKind.DutyEvent, DutyEvent = evt.DutyEvent },
        TriggerKind.Vfx => new SourceSpec { Kind = TriggerKind.Vfx, VfxPathPattern = evt.VfxPath, VfxMatchMode = MatchMode.Contains },
        TriggerKind.HeadMarker => new SourceSpec { Kind = TriggerKind.HeadMarker, MarkerKey = string.IsNullOrEmpty(evt.MarkerKey) ? evt.RawValue : evt.MarkerKey },
        _ => new SourceSpec { Kind = evt.Kind },
    };

    private static string SuggestName(TriggerEvent evt) => evt.Kind switch
    {
        TriggerKind.Chat => "Chat rule",
        TriggerKind.Cast => string.IsNullOrEmpty(evt.ActionName) ? $"Cast {evt.ActionId}" : evt.ActionName,
        TriggerKind.Status => string.IsNullOrEmpty(evt.StatusName) ? $"Status {evt.StatusId}" : evt.StatusName,
        TriggerKind.DutyEvent => $"Duty {evt.DutyEvent}",
        TriggerKind.Vfx => "VFX rule",
        TriggerKind.HeadMarker => "Marker rule",
        _ => "New rule",
    };

    private static string SuggestEchoText(TriggerEvent evt) => evt.Kind switch
    {
        TriggerKind.Cast => "{caster} casts {action}!",
        TriggerKind.Status => "{status} on {bearer}!",
        TriggerKind.DutyEvent => "{event}!",
        _ => string.Empty,
    };

    private static string SourceKindLabel(TriggerKind kind) => kind switch
    {
        TriggerKind.Chat => "Chat message",
        TriggerKind.Cast => "Enemy / actor cast",
        TriggerKind.Status => "Status effect",
        TriggerKind.DutyEvent => "Duty event",
        _ => kind.ToString(),
    };

    private static string PlaceholderHint(TriggerKind kind) => kind switch
    {
        TriggerKind.Chat => "{sender}, {message}, {zone}, $1..$9",
        TriggerKind.Cast => "{caster}, {action}, {zone}",
        TriggerKind.Status => "{status}, {bearer}, {zone}",
        TriggerKind.DutyEvent => "{event}, {zone}",
        _ => "{zone}",
    };

    private static string DescribeSource(Rule rule)
    {
        switch (rule.Source.Kind)
        {
            case TriggerKind.Chat:
                var mode = rule.Source.MatchMode == MatchMode.Regex ? "re" : "∋";
                return rule.Source.Channels.Count == 0 ? $"Chat {mode}·any" : $"Chat {mode}·{rule.Source.Channels.Count}ch";
            case TriggerKind.Cast:
                return "Cast";
            case TriggerKind.Status:
                return "Status";
            case TriggerKind.DutyEvent:
                return "Duty";
            default:
                return rule.Source.Kind.ToString();
        }
    }
}
