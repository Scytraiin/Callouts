using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

using Callouts.Core.Rules;
using Callouts.Core.Suggestions;

namespace Callouts.Windows;

/// <summary>
/// The Suggestions tab: ranked, ready-to-adopt trigger proposals aggregated from combat. Each row
/// offers Create rule, Copy import code, and Ignore. Scope switches between the current fight and the
/// whole session; ignored suggestions are persisted (issues 018–020).
/// </summary>
public sealed class SuggestionsWindow : Window, IDisposable
{
    private static readonly Vector4 OkColor = new(0.4f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 DimColor = new(0.6f, 0.6f, 0.6f, 1f);

    private static readonly string[] CategoryOrder =
    [
        SuggestionCategory.DebuffsOnYou,
        SuggestionCategory.EnemyCasts,
        SuggestionCategory.PartyEffects,
        SuggestionCategory.Markers,
    ];

    private readonly SuggestionCollector collector;
    private readonly Configuration configuration;
    private readonly Action<Suggestion> createRule;
    private readonly Action save;

    private readonly Dictionary<string, bool> categoryShown = new();
    private EncounterScope scope = EncounterScope.ThisFight;
    private bool showIgnored;
    private string? statusMessage;

    public SuggestionsWindow(SuggestionCollector collector, Configuration configuration, Action<Suggestion> createRule, Action save)
        : base("Callouts — Suggestions###CalloutsSuggestions")
    {
        this.collector = collector;
        this.configuration = configuration;
        this.createRule = createRule;
        this.save = save;

        foreach (var category in CategoryOrder)
        {
            this.categoryShown[category] = true;
        }

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(620, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        this.DrawToolbar();

        var ignored = new HashSet<string>(this.configuration.Options.IgnoredSuggestionKeys);
        var suggestions = this.collector.GetSuggestions(this.configuration.Rules, ignored, this.scope);

        ImGui.Separator();

        if (this.showIgnored)
        {
            this.DrawIgnoredPanel();
            return;
        }

        if (suggestions.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextWrapped("Nothing yet. Pull an enemy or take a debuff and the interesting mechanics show up here, ranked. (Combat data only; nothing is saved.)");
            return;
        }

        foreach (var category in CategoryOrder)
        {
            if (!this.categoryShown[category])
            {
                continue;
            }

            var inCategory = suggestions.Where(s => s.Category == category).ToList();
            if (inCategory.Count == 0)
            {
                continue;
            }

            ImGui.TextDisabled(category.ToUpperInvariant());
            foreach (var suggestion in inCategory)
            {
                this.DrawRow(suggestion);
            }

            ImGui.Spacing();
        }

        ImGui.Separator();
        var covered = suggestions.Count(s => s.Covered);
        ImGui.TextDisabled($"{suggestions.Count} shown · {covered} already covered · {this.configuration.Options.IgnoredSuggestionKeys.Count} ignored");
        if (!string.IsNullOrEmpty(this.statusMessage))
        {
            ImGui.SameLine();
            ImGui.TextColored(OkColor, this.statusMessage);
        }
    }

    private void DrawToolbar()
    {
        if (ImGui.Button("Clear"))
        {
            this.collector.Clear();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(140);
        if (ImGui.BeginCombo("##scope", this.scope == EncounterScope.ThisFight ? "This fight" : "This session"))
        {
            if (ImGui.Selectable("This fight", this.scope == EncounterScope.ThisFight))
            {
                this.scope = EncounterScope.ThisFight;
            }

            if (ImGui.Selectable("This session", this.scope == EncounterScope.ThisSession))
            {
                this.scope = EncounterScope.ThisSession;
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.Button(this.showIgnored ? "Back to suggestions" : "Manage ignored"))
        {
            this.showIgnored = !this.showIgnored;
        }

        // Category filters
        foreach (var category in CategoryOrder)
        {
            var on = this.categoryShown[category];
            if (ImGui.Checkbox($"{category}##cat", ref on))
            {
                this.categoryShown[category] = on;
            }

            ImGui.SameLine();
        }

        ImGui.NewLine();
    }

    private void DrawRow(Suggestion suggestion)
    {
        ImGui.PushID(suggestion.Key);

        var stars = new string('★', suggestion.Stars) + new string('☆', 5 - suggestion.Stars);
        var rationale = string.IsNullOrEmpty(suggestion.Hint)
            ? suggestion.Rationale
            : $"{suggestion.Rationale} · {suggestion.Hint}";
        ImGui.TextUnformatted(suggestion.Title);
        ImGui.SameLine();
        ImGui.TextColored(DimColor, $"{stars}  {rationale}");

        if (suggestion.Covered)
        {
            ImGui.SameLine();
            ImGui.TextColored(OkColor, "already covered ✓");
        }

        ImGui.TextColored(DimColor, $"   → {DescribeProposal(suggestion)}");

        if (ImGui.SmallButton("＋ Create rule"))
        {
            this.createRule(suggestion);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Copy import code"))
        {
            ImGui.SetClipboardText(RuleCodec.Export([suggestion.ToRule()]));
            this.statusMessage = "Copied import code to clipboard.";
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Ignore"))
        {
            if (!this.configuration.Options.IgnoredSuggestionKeys.Contains(suggestion.Key))
            {
                this.configuration.Options.IgnoredSuggestionKeys.Add(suggestion.Key);
                this.save();
            }
        }

        ImGui.PopID();
    }

    private void DrawIgnoredPanel()
    {
        var ignored = this.configuration.Options.IgnoredSuggestionKeys;
        if (ignored.Count == 0)
        {
            ImGui.TextDisabled("No ignored suggestions.");
            return;
        }

        ImGui.TextDisabled("Ignored suggestion keys — un-ignore to let them appear again:");
        string? restore = null;
        foreach (var key in ignored)
        {
            ImGui.PushID(key);
            ImGui.TextUnformatted(key);
            ImGui.SameLine();
            if (ImGui.SmallButton("Un-ignore"))
            {
                restore = key;
            }

            ImGui.PopID();
        }

        if (restore is not null)
        {
            ignored.Remove(restore);
            this.save();
        }
    }

    private static string DescribeProposal(Suggestion s)
    {
        var src = s.ProposedSource;
        return src.Kind switch
        {
            Core.Engine.TriggerKind.Cast => $"Cast · action id {src.ActionId} · echo \"{s.ProposedOutputs.Echo.Text}\"",
            Core.Engine.TriggerKind.Status => $"Status gained · id {src.StatusId} · on {src.Bearer} · echo \"{s.ProposedOutputs.Echo.Text}\"",
            Core.Engine.TriggerKind.HeadMarker => $"Head marker · \"{src.MarkerKey}\" · echo \"{s.ProposedOutputs.Echo.Text}\"",
            _ => $"{src.Kind} · echo \"{s.ProposedOutputs.Echo.Text}\"",
        };
    }
}
