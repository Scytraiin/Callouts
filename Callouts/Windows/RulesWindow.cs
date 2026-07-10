using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

using Callouts.Core.Engine;
using Callouts.Core.Rules;

namespace Callouts.Windows;

/// <summary>
/// The main Rules window: a list of rules plus a non-modal editor pane. Issue 002 covers
/// chat-contains rules with an Echo output; later issues extend the editor per source kind
/// and add sound/toast, scoping, bulk operations, and the create-from-event flow.
/// </summary>
public sealed class RulesWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Action saveConfiguration;

    // Editor working copy. Non-null while creating or editing; edits never touch the live
    // rule until Save (the engine only ever reads saved config).
    private Rule? draft;

    // Id of the existing rule being edited; null while creating a new rule.
    private string? editingId;

    private bool focusPatternNextFrame;
    private string? statusMessage;

    public RulesWindow(Configuration configuration, Action saveConfiguration)
        : base("Callouts — Rules###CalloutsRules")
    {
        this.configuration = configuration;
        this.saveConfiguration = saveConfiguration;

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 420),
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

        if (!ImGui.BeginTable("rules", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            return;
        }

        ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 32f);
        ImGui.TableSetupColumn("Name");
        ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 120f);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 130f);
        ImGui.TableHeadersRow();

        Rule? ruleToDelete = null;

        foreach (var rule in this.configuration.Rules)
        {
            ImGui.TableNextRow();
            ImGui.PushID(rule.Id);

            // Enabled = user intent; toggling saves immediately.
            ImGui.TableNextColumn();
            var enabled = rule.Enabled;
            if (ImGui.Checkbox("##enabled", ref enabled))
            {
                rule.Enabled = enabled;
                this.saveConfiguration();
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(string.IsNullOrWhiteSpace(rule.Name) ? "(unnamed)" : rule.Name);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(DescribeSource(rule));

            ImGui.TableNextColumn();
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

    private void DrawEditor()
    {
        var d = this.draft!;
        ImGui.TextUnformatted(this.editingId is null ? "New rule" : "Edit rule");

        // Name
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

        // Source is fixed to Chat in issue 002.
        ImGui.TextUnformatted("Source: Chat message");

        // Channel multi-select.
        this.DrawChannelPicker(d.Source);

        ImGui.TextDisabled("Match: contains (regex support arrives in a later release)");

        var caseSensitive = d.Source.CaseSensitive;
        if (ImGui.Checkbox("Case sensitive", ref caseSensitive))
        {
            d.Source.CaseSensitive = caseSensitive;
        }

        var pattern = d.Source.Pattern;
        if (this.focusPatternNextFrame)
        {
            ImGui.SetKeyboardFocusHere();
            this.focusPatternNextFrame = false;
        }

        if (ImGui.InputTextWithHint("Text contains", "e.g. has initiated a ready check", ref pattern, 512))
        {
            d.Source.Pattern = pattern;
        }

        var sender = d.Source.SenderPattern ?? string.Empty;
        if (ImGui.InputTextWithHint("Sender contains (optional)", "any sender", ref sender, 128))
        {
            d.Source.SenderPattern = string.IsNullOrWhiteSpace(sender) ? null : sender;
        }

        ImGui.Separator();
        ImGui.TextDisabled("THEN");

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

        ImGui.Separator();

        // Inline validation — reasons are always shown; Save never silently no-ops.
        var errors = RuleValidator.Validate(d);
        foreach (var error in errors)
        {
            ImGui.TextColored(new Vector4(0.9f, 0.4f, 0.4f, 1f), $"⚠ {error}");
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
            ImGui.TextColored(new Vector4(0.4f, 0.85f, 0.45f, 1f), this.statusMessage);
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

        var any = source.Channels.Count == 0;
        if (ImGui.Selectable("Any channel", any))
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
        this.statusMessage = null;
    }

    private void BeginEdit(Rule rule)
    {
        this.draft = rule.Clone();
        this.editingId = rule.Id;
        this.focusPatternNextFrame = true;
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

        return rule.Source.Channels.Count == 0
            ? "Chat · any"
            : $"Chat · {rule.Source.Channels.Count} ch";
    }
}
