using Callouts.Core.Engine;

namespace Callouts.Sinks;

/// <summary>
/// Executes one kind of <see cref="AlertAction"/>. Sinks are the only code that turns engine
/// output into Dalamud calls, and each is exception-isolated by the dispatcher (DESIGN.md §5).
/// </summary>
public interface IAlertSink
{
    AlertOutputKind Kind { get; }

    void Execute(AlertAction action);
}
