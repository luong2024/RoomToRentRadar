using Newtonsoft.Json;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

/// <summary>
/// Cosmos DB document used as a unique reference index ensuring global uniqueness of email addresses across partitions.
/// Partition key is /id where id is the normalized email address.
/// </summary>
public sealed class EmailIndexDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
