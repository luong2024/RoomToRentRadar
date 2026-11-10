using Newtonsoft.Json;

namespace RoomToRentRadar.Datastore.CosmosDb.Documents;

public sealed class RefreshTokenDocument
{
    [JsonProperty("tokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    [JsonProperty("jwtId")]
    public string JwtId { get; set; } = string.Empty;

    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [JsonProperty("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; set; }

    [JsonProperty("revokedAtUtc")]
    public DateTime? RevokedAtUtc { get; set; }

    [JsonProperty("replacedByTokenHash")]
    public string? ReplacedByTokenHash { get; set; }
}

