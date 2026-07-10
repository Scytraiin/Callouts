using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

using Callouts.Core.Suggestions;

namespace Callouts.Windows;

/// <summary>
/// The Suggestions tab (issue 018): ranked, ready-to-adopt trigger proposals aggregated from the
/// current combat. Each row offers one-click "Create rule". Ignore/copy-code/encounter-scope arrive
/// in issue 020; advanced-tier candidates in issue 021.
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
    private readonly HashSet<string> noIgnored = [];

    public SuggestionsWindow(SuggestionCollector collector, Configuration configuration, Action<Suggestion> createRule)
        : base("Callouts — Suggestions###CalloutsSuggestions")
    {
        this.collector = collector;
        this.configuration = configuration;
        this.createRule = createRule;

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        if (ImGui.Button("Clear"))
        {
            this.collector.Clear();
        }

        ImGui.SameLine();
        ImGui.TextDisabled("Suggestions from the current combat. Click Create rule to adopt one.");

        var suggestions = this.collector.GetSuggestions(this.configuration.Rules, this.noIgnored);
        if (suggestions.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextWrapped("Nothing yet. Pull an enemy or take a debuff and the interesting mechanics will show up here, ranked.");
            return;
        }

        ImGui.Separator();

        foreach (var category in CategoryOrder)
        {
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
    }

    private void DrawRow(Suggestion suggestion)
    {
        ImGui.PushID(suggestion.Key);

        var stars = new string('★', suggestion.Stars) + new string('☆', 5 - suggestion.Stars);
        ImGui.TextUnformatted($"{suggestion.Title}");
        ImGui.SameLine();
        ImGui.TextColored(DimColor, $"{stars}  {suggestion.Rationale}");

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

        ImGui.PopID();
    }

    private static string DescribeProposal(Suggestion s)
    {
        var src = s.ProposedSource;
        return src.Kind switch
        {
            Core.Engine.TriggerKind.Cast => $"Cast · action id {src.ActionId} · echo \"{s.ProposedOutputs.Echo.Text}\"",
            Core.Engine.TriggerKind.Status => $"Status gained · id {src.StatusId} · on {src.Bearer} · echo \"{s.ProposedOutputs.Echo.Text}\"",
            _ => $"{src.Kind} · echo \"{s.ProposedOutputs.Echo.Text}\"",
        };
    }
}
