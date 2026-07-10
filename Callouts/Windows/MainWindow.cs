using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Callouts.Windows;

public sealed class MainWindow : Window, IDisposable
{
    public MainWindow()
        : base("Callouts##MainWindow")
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 180),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        ImGui.TextWrapped("Callouts — coming soon.");
        ImGui.Spacing();
        ImGui.BulletText("Rules that turn in-game events into Echo messages, sounds, and toasts.");
        ImGui.BulletText("This is the walking-skeleton build — the rule engine arrives in the next release.");
    }
}
