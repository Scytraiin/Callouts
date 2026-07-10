using System;
using System.Runtime.InteropServices;

using Dalamud.Hooking;
using Dalamud.Plugin.Services;

using Callouts.Core.Engine;
using Callouts.Core.Rules;

namespace Callouts.Sources;

/// <summary>
/// Advanced (hook-based) source for actor VFX spawns. Emits a <see cref="TriggerKind.Vfx"/> event
/// for every spawn and, when the path looks like a lock-on head marker, an additional
/// <see cref="TriggerKind.HeadMarker"/> event via <see cref="MarkerMapping"/> (OQ-2 Option A).
///
/// PATCH-FRAGILE: <see cref="VfxCreateSignature"/> is the single game-memory signature this
/// source depends on. It is intentionally empty in source control — it must be filled and
/// verified against the live client during release bring-up (issue 017, patch-day playbook).
/// Until then the source reports <see cref="SourceStatus.Failed"/> and the advanced-tier failure
/// notification fires — exactly the designed game-patch behavior (DESIGN.md §3.5, §10).
/// </summary>
public sealed class VfxSource : ITriggerSource
{
    // See the class remarks. Empty = "not configured for this build".
    private const string VfxCreateSignature = "";

    private readonly IGameInteropProvider interop;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IPluginLog log;

    private Hook<VfxCreateDelegate>? hook;
    private bool started;

    public VfxSource(IGameInteropProvider interop, IObjectTable objectTable, IPartyList partyList, IPluginLog log)
    {
        this.interop = interop;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.log = log;
    }

    private delegate IntPtr VfxCreateDelegate(IntPtr pathPtr, IntPtr targetObject, float a3, IntPtr a4, byte a5, IntPtr a6, byte a7);

    public TriggerKind Kind => TriggerKind.Vfx;

    public SourceStatus Status { get; private set; } = SourceStatus.Disabled;

    public string? FailureReason { get; private set; }

    public event Action<TriggerEvent>? OnEvent;

    public void Start()
    {
        if (this.started)
        {
            return;
        }

        this.started = true;

        if (string.IsNullOrEmpty(VfxCreateSignature))
        {
            this.Status = SourceStatus.Failed;
            this.FailureReason = "VFX signature is not configured in this build (verify in-game — see README).";
            this.log.Warning("Callouts: {Reason}", this.FailureReason);
            return;
        }

        try
        {
            this.hook = this.interop.HookFromSignature<VfxCreateDelegate>(VfxCreateSignature, this.Detour);
            this.hook.Enable();
            this.Status = SourceStatus.Active;
            this.FailureReason = null;
        }
        catch (Exception ex)
        {
            this.Status = SourceStatus.Failed;
            this.FailureReason = "VFX hook failed to install (game patch?).";
            this.log.Error(ex, "Callouts: {Reason}", this.FailureReason);
        }
    }

    public void Stop()
    {
        if (!this.started)
        {
            return;
        }

        this.hook?.Dispose();
        this.hook = null;
        this.started = false;
        this.Status = SourceStatus.Disabled;
    }

    public void Dispose() => this.Stop();

    private IntPtr Detour(IntPtr pathPtr, IntPtr targetObject, float a3, IntPtr a4, byte a5, IntPtr a6, byte a7)
    {
        // Read-only observation; the original is always invoked unchanged.
        try
        {
            var path = Marshal.PtrToStringUTF8(pathPtr) ?? string.Empty;
            if (!string.IsNullOrEmpty(path))
            {
                this.Emit(path, targetObject);
            }
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Callouts: VFX detour observation failed.");
        }

        return this.hook!.Original(pathPtr, targetObject, a3, a4, a5, a6, a7);
    }

    private void Emit(string path, IntPtr targetObject)
    {
        var (targetIsSelf, targetInParty) = this.ResolveTarget(targetObject);

        this.OnEvent?.Invoke(new TriggerEvent
        {
            Kind = TriggerKind.Vfx,
            VfxPath = path,
            TargetIsSelf = targetIsSelf,
            TargetInParty = targetInParty,
        });

        if (MarkerMapping.IsHeadMarkerPath(path))
        {
            this.OnEvent?.Invoke(new TriggerEvent
            {
                Kind = TriggerKind.HeadMarker,
                MarkerKey = MarkerMapping.Lookup(path) ?? string.Empty,
                RawValue = path,
                TargetIsSelf = targetIsSelf,
                TargetInParty = targetInParty,
            });
        }
    }

    private (bool Self, bool Party) ResolveTarget(IntPtr targetObject)
    {
        if (targetObject == IntPtr.Zero)
        {
            return (false, false);
        }

        var localId = this.objectTable.LocalPlayer?.GameObjectId ?? 0;
        for (var i = 0; i < this.objectTable.Length; i++)
        {
            var obj = this.objectTable[i];
            if (obj is not null && obj.Address == targetObject)
            {
                var self = obj.GameObjectId == localId;
                var inParty = this.BuildPartyNameSet().Contains(obj.Name.TextValue);
                return (self, inParty);
            }
        }

        return (false, false);
    }

    private System.Collections.Generic.HashSet<string> BuildPartyNameSet()
    {
        var names = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < this.partyList.Length; i++)
        {
            var member = this.partyList[i];
            if (member is not null)
            {
                names.Add(member.Name.TextValue);
            }
        }

        return names;
    }
}
