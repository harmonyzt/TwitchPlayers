using System.Text.Json.Serialization;

namespace TwitchPlayers.Models;

public record TwitchPlayerNames()
{
    [JsonPropertyName("Names")]
    public List<string> Names { get; set; }
}