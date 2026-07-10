using System;

using Dalamud.Game.DutyState;
using Dalamud.Plugin.Services;

using Callouts.Core.Engine;
using Callouts.Core.Rules;

namespace Callouts.Sources;

/// <summary>
/// Event-driven source for duty lifecycle events (wipe / recommence), the same events
/// VoiceDirectorV2 consumes. No polling. (DESIGN.md §3.4, OQ-4.)
/// </summary>
public sealed class DutyEventSource : ITriggerSource
{
    private readonly IDutyState dutyState;
    private readonly IPluginLog log;
    private bool started;

    public DutyEventSource(IDutyState dutyState, IPluginLog log)
    {
        this.dutyState = dutyState;
        this.log = log;
    }

    public TriggerKind Kind => TriggerKind.DutyEvent;

    public SourceStatus Status { get; private set; } = SourceStatus.Disabled;

    public event Action<TriggerEvent>? OnEvent;

    public void Start()
    {
        if (this.started)
        {
            return;
        }

        this.dutyState.DutyWiped += this.OnDutyWiped;
        this.dutyState.DutyRecommenced += this.OnDutyRecommenced;
        this.started = true;
        this.Status = SourceStatus.Active;
    }

    public void Stop()
    {
        if (!this.started)
        {
            return;
        }

        this.dutyState.DutyRecommenced -= this.OnDutyRecommenced;
        this.dutyState.DutyWiped -= this.OnDutyWiped;
        this.started = false;
        this.Status = SourceStatus.Disabled;
    }

    public void Dispose() => this.Stop();

    private void OnDutyWiped(IDutyStateEventArgs args) => this.Emit(DutyEventFilter.Wiped);

    private void OnDutyRecommenced(IDutyStateEventArgs args) => this.Emit(DutyEventFilter.Recommenced);

    private void Emit(DutyEventFilter dutyEvent)
    {
        try
        {
            this.OnEvent?.Invoke(new TriggerEvent { Kind = TriggerKind.DutyEvent, DutyEvent = dutyEvent });
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Callouts: duty event dispatch failed.");
        }
    }
}
