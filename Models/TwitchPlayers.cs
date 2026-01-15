using System.Text.Json.Serialization;
using TwitchPlayers.Enums;

namespace TwitchPlayers.Models;

public record BotCallsignsNames
{
    [JsonPropertyName("Names")]
    public List<string> Names { get; set; }
}

public record TtvNamesData
{
    [JsonPropertyName("GeneratedTwitchNames")]
    public Dictionary<string, EPersonality> GeneratedTwitchNames { get; set; }
}