using System;

using Callouts.Core.Engine;

namespace Callouts.Sources;

/// <summary>Health of a trigger source, surfaced in Settings and used by the runtime-state model.</summary>
public enum SourceStatus
{
    Disabled,
    Active,
    Failed,
}

/// <summary>
/// Adapter that turns a Dalamud input into normalized <see cref="TriggerEvent"/>s. Stable
/// sources start always; advanced (hook-based) sources start only while the master toggle
/// is on (issue 014). Lifecycle per DESIGN.md §3.
/// </summary>
public interface ITriggerSource : IDisposable
{
    TriggerKind Kind { get; }

    SourceStatus Status { get; }

    event Action<TriggerEvent>? OnEvent;

    void Start();

    void Stop();
}
