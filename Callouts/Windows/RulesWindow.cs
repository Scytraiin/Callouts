using System;
using System.Numerics;
using System.Text.RegularExpressions;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

using Callouts.Core.Engine;
using Callouts.Core.Rules;

namespace Callouts.Windows;

/// <summary>
/// The main Rules window: a list of rules plus a non-modal editor pane. Covers chat rules
/// (contains + regex), placeholders, and Echo/Sound/Toast outputs with a live tester and
/// output previews.
/// </summary>
public sealed class RulesWindow : Window, IDisposable
{
    private static readonly Vector4 ErrorColor = new(0.9f, 0.4f, 0.4f, 1f);
    private static readonly Vector4 OkColor = new(0.4f, 0.85f, 0.45f, 1f);

    private readonly Configuration configuration;
    private readonly RuleEngine engine;
    private readonly Action saveConfiguration;
    private readonly Action<AlertAction> previewAction;

    private Rule? draft;
    private string? editingId;
    private bool focusPatternNextFrame;
    private string testerInput = string.Empty;
    private string? statusMessage;

    public RulesWindow(Configuration configuration, RuleEngine engine, Action saveConfiguration, Action<AlertAction> previewAction)
        : base("Callouts — Rules###CalloutsRules")
    {
        this.configuration = configuration;
        this.engine = engine;
        this.saveConfiguration = saveConfiguration;
        this.previewAction = previewAction;

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(580, 460),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        this.DrawToolbar();
        ImGui.Separator();
        this.DrawRuleList();

        if (this.draft is not null)
        {
            ImGui.Separator();
            this.DrawEditor();
        }
    }

    private void DrawToolbar()
    {
        if (ImGui.Button("＋ New rule"))
        {
            this.BeginCreate();
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"{this.configuration.Rules.Count} rule(s)");
    }

    private void DrawRuleList()
    {
        if (this.configuration.Rules.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextWrapped("No rules yet. Click \"＋ New rule\" to create your first one — for example, echo a reminder when a party member starts a ready check.");
            ImGui.Spacing();
            return;
        }

        if (!ImGui.BeginTable("rules", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            return;
        }

        ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 32f);
        ImGui.TableSetupColumn("Name");
        ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 110f);
        ImGui.TableSetupColumn("Fires", ImGuiTableColumnFlags.WidthFixed, 48f);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 140f);
        ImGui.TableHeadersRow();

        Rule? ruleToDelete = null;

        foreach (var rule in this.configuration.Rules)
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
            if (ImGui.SmallButton("Delete"))
            {
                ruleToDelete = rule;
            }

            ImGui.PopID();
        }

        ImGui.EndTable();

        if (ruleToDelete is not null)
        {
            this.configuration.Rules.Remove(ruleToDelete);
            this.saveConfiguration();
            if (this.editingId == ruleToDelete.Id)
            {
                this.CancelEdit();
            }

            this.statusMessage = $"Deleted \"{ruleToDelete.Name}\".";
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
        ImGui.TextUnformatted("Source: Chat message");

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

        ImGui.Separator();
        ImGui.TextDisabled("THEN  (placeholders: {sender}, {message}, $1..$9)");

        this.DrawEchoOutput(d);
        this.DrawSoundOutput(d);
        this.DrawToastOutput(d);

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

        var channel = d.Source.Channels.Count > 0 ? d.Source.Channels[0] : 10; // Say
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
            if (result is null)
            {
                ImGui.TextColored(ErrorColor, "✘ no match");
            }
            else
            {
                ImGui.TextColored(OkColor, result.Captures.Count > 0
                    ? $"✔ match — captures: {string.Join(", ", result.Captures)}"
                    : "✔ match");
            }
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

        if (ImGui.BeginCombo("Style", d.Outputs.Toast.Style.ToString()))
        {
            foreach (ToastStyle style in Enum.GetValues<ToastStyle>())
            {
                if (ImGui.Selectable(style.ToString(), d.Outputs.Toast.Style == style))
                {
                    d.Outputs.Toast.Style = style;
                }
            }

            ImGui.EndCombo();
        }

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
        // Synthetic match with canned placeholder values; bypasses gates and fire counters.
        var match = MatchResult.FromValues(
            ("sender", "TestSender"),
            ("message", "test message"));

        foreach (var action in this.engine.BuildTestActions(rule, match))
        {
            this.previewAction(PrefixTest(action));
        }

        this.statusMessage = $"Test-fired \"{rule.Name}\".";
    }

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

    private static string DescribeSource(Rule rule)
    {
        if (rule.Source.Kind != TriggerKind.Chat)
        {
            return rule.Source.Kind.ToString();
        }

        var mode = rule.Source.MatchMode == MatchMode.Regex ? "re" : "∋";
        return rule.Source.Channels.Count == 0
            ? $"Chat {mode} · any"
            : $"Chat {mode} · {rule.Source.Channels.Count}ch";
    }
}
