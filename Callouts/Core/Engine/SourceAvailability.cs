namespace Callouts.Core.Engine;

/// <summary>
/// Runtime availability of a source kind, supplied to the engine by the plugin. Stable sources
/// are always <see cref="Available"/>; advanced (hook-based) kinds report BlockedAdvancedOff when
/// the master toggle is off and Failed when their hook did not install (issue 014).
/// </summary>
public enum SourceAvailability
{
    Available,
    BlockedAdvancedOff,
    Failed,
}
