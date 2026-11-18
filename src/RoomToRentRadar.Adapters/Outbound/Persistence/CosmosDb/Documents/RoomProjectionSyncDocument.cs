namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

using Newtonsoft.Json;

public sealed class RoomProjectionSyncDocument
{
    public const string PendingState = "pending";
    public const string CompletedState = "completed";
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [JsonProperty("campusId")]
    public string CampusId { get; set; } = string.Empty;

    private string? _previousCampusId;

    [JsonProperty("previousCampusIds")]
    public List<string> PreviousCampusIds { get; set; } = [];

    [JsonProperty("previousCampusId")]
    public string? PreviousCampusId
    {
        get => PreviousCampusIds.Count > 0 ? PreviousCampusIds[0] : _previousCampusId;
        set
        {
            _previousCampusId = value;
            if (!string.IsNullOrWhiteSpace(value) && !PreviousCampusIds.Contains(value, StringComparer.Ordinal))
            {
                PreviousCampusIds.Add(value);
            }
        }
    }

    [JsonProperty("operation")]
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Durable listing snapshot for an upsert. The reconciler must use this
    /// payload rather than infer the operation from a concurrently changing
    /// listings container.
    /// </summary>
    [JsonProperty("listing")]
    public RoomListingDocument? Listing { get; set; }

    [JsonProperty("sequence")]
    public long Sequence { get; set; }

    [JsonProperty("state")]
    public string State { get; set; } = PendingState;

    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [JsonProperty("_etag")]
    public string? ETag { get; set; }
}
