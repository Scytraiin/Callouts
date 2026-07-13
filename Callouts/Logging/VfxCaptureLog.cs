using System;
using System.IO;

using Dalamud.Plugin.Services;

using Callouts.Core.Engine;
using Callouts.Core.Logging;

namespace Callouts.Logging;

/// <summary>
/// Append-only VFX capture log (opt-in). While enabled, appends one tab-separated line per observed
/// VFX spawn to a file in the plugin config directory — <b>no size or line cap</b>; the file grows
/// until the user turns capture off or clears it. Intended for offline analysis, e.g. finding the
/// exact VFX paths that distinguish "real" vs "fake" mechanics.
///
/// Runs on the game's main thread (same as the event pipeline), so no locking is needed. Writes are
/// auto-flushed so nothing is lost if the game crashes mid-fight; any IO error disables capture
/// rather than throwing into the event pipeline.
/// </summary>
public sealed class VfxCaptureLog : IDisposable
{
    private readonly string filePath;
    private readonly IPluginLog log;
    private StreamWriter? writer;

    public VfxCaptureLog(string filePath, IPluginLog log)
    {
        this.filePath = filePath;
        this.log = log;
    }

    public bool Enabled { get; private set; }

    public string FilePath => this.filePath;

    /// <summary>Turns capture on/off. Enabling appends to any existing file and writes a session banner.</summary>
    public void SetEnabled(bool enabled)
    {
        if (enabled == this.Enabled)
        {
            return;
        }

        if (enabled)
        {
            this.Open();
        }
        else
        {
            this.Close();
        }
    }

    /// <summary>Appends one line for a VFX event. Non-VFX events and the disabled state are ignored.</summary>
    public void Write(TriggerEvent evt)
    {
        if (!this.Enabled || this.writer is null || evt.Kind != TriggerKind.Vfx)
        {
            return;
        }

        try
        {
            this.writer.WriteLine(VfxCaptureFormatter.FormatLine(evt, DateTime.Now));
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Callouts: failed to write VFX capture line; disabling capture.");
            this.Close();
        }
    }

    /// <summary>Deletes the capture file, reopening a fresh one if capture is currently on.</summary>
    public void ClearFile()
    {
        var wasEnabled = this.Enabled;
        this.Close();

        try
        {
            if (File.Exists(this.filePath))
            {
                File.Delete(this.filePath);
            }
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Callouts: failed to delete VFX capture file.");
        }

        if (wasEnabled)
        {
            this.Open();
        }
    }

    public void Dispose() => this.Close();

    private void Open()
    {
        try
        {
            var freshFile = !File.Exists(this.filePath) || new FileInfo(this.filePath).Length == 0;
            this.writer = new StreamWriter(this.filePath, append: true) { AutoFlush = true };
            this.writer.WriteLine(VfxCaptureFormatter.SessionBanner(DateTime.Now));
            if (freshFile)
            {
                this.writer.WriteLine("# " + VfxCaptureFormatter.Header);
            }

            this.Enabled = true;
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Callouts: failed to open VFX capture file.");
            this.writer = null;
            this.Enabled = false;
        }
    }

    private void Close()
    {
        try
        {
            this.writer?.Flush();
            this.writer?.Dispose();
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Callouts: failed to close VFX capture file.");
        }
        finally
        {
            this.writer = null;
            this.Enabled = false;
        }
    }
}
