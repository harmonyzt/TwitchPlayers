using System.Text.Json.Serialization;

namespace TwitchPlayers.Models;

public record BotCallsignsNames()
{
    [JsonPropertyName("Names")]
    public List<string> Names { get; set; }
}

public record TtvNamesData
{
    [JsonPropertyName("GeneratedTwitchNames")]
    public List<string> GeneratedTwitchNames { get; set; }
}

public record SainPersonalityData
{
    [JsonPropertyName("NicknamePersonalityMatches")]
    public Dictionary<string, string> NicknamePersonalityMatches { get; set; }
}