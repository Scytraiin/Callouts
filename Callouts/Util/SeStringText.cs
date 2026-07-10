using System.Text;

using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace Callouts.Util;

/// <summary>
/// Flattens a Dalamud <see cref="SeString"/> to plain matchable text. Ports the pattern
/// from LootDistributionInfo's SeStringDisplayText: walk text and player payloads, then
/// fall back to <see cref="SeString.TextValue"/> if the walk produced nothing. Kept in the
/// Dalamud-facing layer because SeString is a Dalamud type (the core stays Dalamud-free).
/// </summary>
internal static class SeStringText
{
    public static string Flatten(SeString message)
    {
        var builder = new StringBuilder();

        foreach (var payload in message.Payloads)
        {
            switch (payload)
            {
                case ITextProvider textProvider:
                    builder.Append(textProvider.Text);
                    break;

                case PlayerPayload playerPayload:
                    builder.Append(playerPayload.PlayerName);
                    break;
            }
        }

        var flattened = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(flattened) ? message.TextValue.Trim() : flattened;
    }
}
