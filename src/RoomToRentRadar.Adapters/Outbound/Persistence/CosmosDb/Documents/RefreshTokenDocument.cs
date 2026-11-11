using Newtonsoft.Json;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

public sealed class RefreshTokenDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("tokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    [JsonProperty("tokenFamily")]
    public string TokenFamily { get; set; } = string.Empty;

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("expiresAt")]
    public DateTimeOffset ExpiresAt { get; set; }

    [JsonProperty("revokedAt")]
    public DateTimeOffset? RevokedAt { get; set; }

    [JsonProperty("replacedByTokenId")]
    public string? ReplacedByTokenId { get; set; }

    [JsonProperty("revocationReason")]
    public string? RevocationReason { get; set; }
}
