using System.Text.Json;

namespace Callouts.Core.Config;

/// <summary>
/// The plan produced from inspecting the on-disk config before it is loaded. The Dalamud layer
/// executes it (writes the backup, then either loads the typed config or defaults).
/// </summary>
public sealed record MigrationPlan
{
    /// <summary>Version found on disk, or null for a fresh install / unreadable file.</summary>
    public int? StoredVersion { get; init; }

    public bool NeedsBackup { get; init; }

    public string? BackupFileName { get; init; }

    /// <summary>True when the stored config is newer than this plugin: refuse, load defaults.</summary>
    public bool RefuseAsDowngrade { get; init; }

    /// <summary>User-facing notice to log/echo, or null when nothing happened.</summary>
    public string? Notice { get; init; }
}

/// <summary>
/// Decides what to do with an existing config file before loading it (DESIGN.md §4.6, PRD FR-9):
/// an <b>unconditional pre-migration backup</b> whenever the stored version differs from the
/// code version, forward-only migration, and a hard <b>downgrade refusal</b>. Pure and testable;
/// all file IO is done by the caller.
/// </summary>
public static class ConfigMigrator
{
    public static MigrationPlan Plan(string? rawJson, int codeVersion)
    {
        var stored = TryReadVersion(rawJson);

        if (stored is null || stored == codeVersion)
        {
            return new MigrationPlan { StoredVersion = stored };
        }

        // Versions differ → always back up the raw file first, before touching anything.
        var backupName = $"callouts-config.backup-v{stored}.json";

        if (stored > codeVersion)
        {
            return new MigrationPlan
            {
                StoredVersion = stored,
                NeedsBackup = true,
                BackupFileName = backupName,
                RefuseAsDowngrade = true,
                Notice = $"Callouts: your saved settings (v{stored}) are newer than this plugin version "
                    + $"(v{codeVersion}). Loaded defaults to avoid data loss; your file was backed up to {backupName}.",
            };
        }

        return new MigrationPlan
        {
            StoredVersion = stored,
            NeedsBackup = true,
            BackupFileName = backupName,
            Notice = $"Callouts: upgraded settings from v{stored} to v{codeVersion} (backup saved to {backupName}).",
        };
    }

    private static int? TryReadVersion(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("Version", out var versionElement)
                && versionElement.TryGetInt32(out var version))
            {
                return version;
            }
        }
        catch (JsonException)
        {
            // Corrupt/unreadable file: treat as fresh so we never crash on load.
        }

        return null;
    }
}
