using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

using Callouts.Core.Engine;
using Callouts.Core.Rules;

namespace Callouts.Windows;

/// <summary>
/// Settings window (issue 013): master enable, the advanced-sources toggle (issue 014), rate
/// limit, default cooldown, and the event-buffer size. The starter-pack import lands in issue 016.
/// </summary>
public sealed class SettingsWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly RuleEngine engine;
    private readonly EventBuffer buffer;
    private readonly Action save;
    private readonly Action<bool> onAdvancedToggled;
    private Func<string>? advancedHealth;
    private string? starterMessage;

    public SettingsWindow(
        Configuration configuration,
        RuleEngine engine,
        EventBuffer buffer,
        Action save,
        Action<bool> onAdvancedToggled)
        : base("Callouts — Settings###CalloutsSettings")
    {
        this.configuration = configuration;
        this.engine = engine;
        this.buffer = buffer;
        this.save = save;
        this.onAdvancedToggled = onAdvancedToggled;

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(460, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    /// <summary>Optional provider of an advanced-tier health summary line (set by issue 014).</summary>
    public void SetAdvancedHealthProvider(Func<string> provider) => this.advancedHealth = provider;

    public void Dispose()
    {
    }

    private void ApplyLogLimits()
    {
        this.buffer.SetLimits(this.configuration.Options.EventLogDefaultLimit, this.configuration.Options.EventCategoryLimits);
        this.save();
    }

    public override void Draw()
    {
        var options = this.configuration.Options;

        ImGui.TextDisabled("GENERAL");

        var master = options.MasterEnabled;
        if (ImGui.Checkbox("Enable Callouts (master switch)", ref master))
        {
            options.MasterEnabled = master;
            this.engine.MasterEnabled = master;
            this.save();
        }

        ImGui.Separator();
        ImGui.TextDisabled("ADVANCED SOURCES");
        ImGui.TextWrapped("Advanced sources (VFX, head markers) use game hooks that can break on a game patch. Leave off for maximum patch-day robustness.");

        var advanced = options.AdvancedSourcesEnabled;
        if (ImGui.Checkbox("Enable advanced sources", ref advanced))
        {
            options.AdvancedSourcesEnabled = advanced;
            this.save();
            this.onAdvancedToggled(advanced);
        }

        if (this.advancedHealth is not null)
        {
            ImGui.TextDisabled(this.advancedHealth());
        }

        ImGui.Separator();
        ImGui.TextDisabled("LIMITS");

        var rate = (float)options.RateLimitPerSecond;
        if (ImGui.InputFloat("Rate limit (alerts/sec)", ref rate))
        {
            options.RateLimitPerSecond = Math.Clamp(rate, 1f, 100f);
            this.engine.RateLimiter.Reconfigure(options.RateLimitPerSecond, options.RateLimitPerSecond);
            this.save();
        }

        var cooldown = (float)options.DefaultCooldownSeconds;
        if (ImGui.InputFloat("Default cooldown for new rules (sec)", ref cooldown))
        {
            options.DefaultCooldownSeconds = Math.Clamp(cooldown, 0f, 600f);
            this.save();
        }

        var defaultLimit = options.EventLogDefaultLimit;
        if (ImGui.InputInt("Default per-category log limit", ref defaultLimit))
        {
            options.EventLogDefaultLimit = Math.Clamp(defaultLimit, 100, 100_000);
            this.ApplyLogLimits();
        }

        ImGui.TextDisabled("Each event category keeps its own most-recent entries (max 100,000 each), so a noisy category can't evict the ones you care about.");

        if (ImGui.CollapsingHeader("Per-category limits (override the default)"))
        {
            foreach (EventCategory category in Enum.GetValues<EventCategory>())
            {
                var effective = options.EventCategoryLimits.TryGetValue(category, out var ov) ? ov : options.EventLogDefaultLimit;
                var value = effective;
                if (ImGui.InputInt($"{EventCategorizer.Label(category)}##lim{category}", ref value))
                {
                    var clamped = Math.Clamp(value, 100, 100_000);
                    if (clamped == options.EventLogDefaultLimit)
                    {
                        options.EventCategoryLimits.Remove(category); // follow the default again
                    }
                    else
                    {
                        options.EventCategoryLimits[category] = clamped;
                    }

                    this.ApplyLogLimits();
                }
            }

            if (ImGui.Button("Reset all to default"))
            {
                options.EventCategoryLimits.Clear();
                this.ApplyLogLimits();
            }
        }

        ImGui.Separator();
        ImGui.TextDisabled("SUGGESTIONS");

        var autoOpen = options.AutoOpenSuggestions;
        if (ImGui.Checkbox("Open Suggestions automatically when combat ends", ref autoOpen))
        {
            options.AutoOpenSuggestions = autoOpen;
            this.save();
        }

        ImGui.Separator();
        ImGui.TextDisabled("STARTER RULES");
        ImGui.TextWrapped("Add a few ready-made example rules (ready check, countdown, food expiry, wipe).");
        if (ImGui.Button("Import starter rules"))
        {
            var report = RuleCodec.Merge(this.configuration.Rules, StarterPack.Create(), CollisionChoice.Replace);
            this.save();
            this.starterMessage = $"Starter rules: {report.Added} added, {report.Replaced} updated.";
        }

        if (!string.IsNullOrEmpty(this.starterMessage))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(this.starterMessage);
        }
    }
}
