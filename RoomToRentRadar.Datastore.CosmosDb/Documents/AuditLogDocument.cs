using Newtonsoft.Json;

namespace RoomToRentRadar.Datastore.CosmosDb.Documents;

public sealed class AuditLogDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("actualUserId")]
    public string? ActualUserId { get; set; }

    [JsonProperty("userEmail")]
    public string? UserEmail { get; set; }

    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("action")]
    public string Action { get; set; } = string.Empty;

    [JsonProperty("entityType")]
    public string EntityType { get; set; } = string.Empty;

    [JsonProperty("entityId")]
    public string? EntityId { get; set; }

    [JsonProperty("detailsJson")]
    public string? DetailsJson { get; set; }

    [JsonProperty("ipAddress")]
    public string? IpAddress { get; set; }

    [JsonProperty("userAgent")]
    public string? UserAgent { get; set; }

    [JsonProperty("statusCode")]
    public int? StatusCode { get; set; }

    [JsonProperty("durationMs")]
    public long? DurationMs { get; set; }

    [JsonProperty("isSuccess")]
    public bool IsSuccess { get; set; }

    [JsonProperty("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonProperty("timestampUtc")]
    public DateTime TimestampUtc { get; set; }
}

