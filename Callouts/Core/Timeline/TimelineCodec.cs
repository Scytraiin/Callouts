using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Callouts.Core.Timeline;

/// <summary>Result of decoding a timeline import string.</summary>
public sealed record TimelineImportResult
{
    public bool Success { get; init; }

    public string? Error { get; init; }

    public TimelineDefinition? Timeline { get; init; }

    public static TimelineImportResult Ok(TimelineDefinition timeline) => new() { Success = true, Timeline = timeline };

    public static TimelineImportResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Import/export of a single timeline as a shareable clipboard string: JSON → gzip → base64 with a
/// <c>TL1|</c> version prefix. Mirrors <see cref="Rules.RuleCodec"/> (same bomb guard). Pure/testable.
/// </summary>
public static class TimelineCodec
{
    private const string Prefix = "TL1|";
    private const int MaxDecompressedBytes = 1_000_000;

    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = false };

    public static string Export(TimelineDefinition timeline)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(timeline, JsonOptions);

        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(json, 0, json.Length);
        }

        return Prefix + Convert.ToBase64String(buffer.ToArray());
    }

    public static TimelineImportResult Import(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || !payload.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return TimelineImportResult.Fail("Unrecognized code (wrong format or version).");
        }

        byte[] compressed;
        try
        {
            compressed = Convert.FromBase64String(payload[Prefix.Length..].Trim());
        }
        catch (FormatException)
        {
            return TimelineImportResult.Fail("Corrupt import code.");
        }

        byte[] json;
        try
        {
            json = Decompress(compressed);
        }
        catch (BombException)
        {
            return TimelineImportResult.Fail("Import code is too large and was rejected.");
        }
        catch (InvalidDataException)
        {
            return TimelineImportResult.Fail("Corrupt import code.");
        }

        try
        {
            var timeline = JsonSerializer.Deserialize<TimelineDefinition>(json, JsonOptions);
            return timeline is null ? TimelineImportResult.Fail("Corrupt import code.") : TimelineImportResult.Ok(timeline);
        }
        catch (JsonException)
        {
            return TimelineImportResult.Fail("Corrupt import code.");
        }
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
