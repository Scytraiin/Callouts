using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace Callouts.Core.Rules;

public enum CollisionChoice
{
    Skip,
    Replace,
    KeepBoth,
}

/// <summary>Result of decoding an import string.</summary>
public sealed record ImportResult
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public IReadOnlyList<Rule> Rules { get; init; } = [];

    public static ImportResult Ok(IReadOnlyList<Rule> rules) => new() { Success = true, Rules = rules };

    public static ImportResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>Summary of what a merge did.</summary>
public sealed record MergeReport(int Added, int Replaced, int Skipped);

/// <summary>
/// Import/export of rule packs as shareable clipboard strings: JSON → gzip → base64 with a
/// <c>CO1|</c> version prefix. Ids are preserved so re-importing an updated pack matches existing
/// rules. Decompression is capped (gzip-bomb guard). Pure/testable; no clipboard or IO here.
/// (DESIGN.md §4.5, PRD §11.)
/// </summary>
public static class RuleCodec
{
    private const string Prefix = "CO1|";
    private const int MaxDecompressedBytes = 1_000_000;

    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = false };

    public static string Export(IEnumerable<Rule> rules)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(rules.ToList(), JsonOptions);

        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(json, 0, json.Length);
        }

        return Prefix + Convert.ToBase64String(buffer.ToArray());
    }

    public static ImportResult Import(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || !payload.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return ImportResult.Fail("Unrecognized code (wrong format or version).");
        }

        byte[] compressed;
        try
        {
            compressed = Convert.FromBase64String(payload[Prefix.Length..].Trim());
        }
        catch (FormatException)
        {
            return ImportResult.Fail("Corrupt import code.");
        }

        byte[] json;
        try
        {
            json = Decompress(compressed);
        }
        catch (BombException)
        {
            return ImportResult.Fail("Import code is too large and was rejected.");
        }
        catch (InvalidDataException)
        {
            return ImportResult.Fail("Corrupt import code.");
        }

        try
        {
            var rules = JsonSerializer.Deserialize<List<Rule>>(json, JsonOptions);
            return rules is null ? ImportResult.Fail("Corrupt import code.") : ImportResult.Ok(rules);
        }
        catch (JsonException)
        {
            return ImportResult.Fail("Corrupt import code.");
        }
    }

    /// <summary>Merges incoming rules into the existing list by id, applying one collision choice.</summary>
    public static MergeReport Merge(List<Rule> existing, IReadOnlyList<Rule> incoming, CollisionChoice choice)
    {
        int added = 0, replaced = 0, skipped = 0;

        foreach (var rule in incoming)
        {
            var index = existing.FindIndex(r => r.Id == rule.Id);
            if (index < 0)
            {
                existing.Add(rule.Clone());
                added++;
                continue;
            }

            switch (choice)
            {
                case CollisionChoice.Replace:
                    existing[index] = rule.Clone();
                    replaced++;
                    break;

                case CollisionChoice.KeepBoth:
                    var copy = rule.Clone();
                    copy.Id = Guid.NewGuid().ToString();
                    existing.Add(copy);
                    added++;
                    break;

                default:
                    skipped++;
                    break;
            }
        }

        return new MergeReport(added, replaced, skipped);
    }

    public static int CountCollisions(IReadOnlyList<Rule> existing, IReadOnlyList<Rule> incoming)
    {
        var ids = new HashSet<string>(existing.Select(r => r.Id));
        return incoming.Count(r => ids.Contains(r.Id));
    }

    private static byte[] Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        var buffer = new byte[8192];
        var total = 0;
        int read;
        while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > MaxDecompressedBytes)
            {
                throw new BombException();
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    private sealed class BombException : Exception
    {
    }
}
