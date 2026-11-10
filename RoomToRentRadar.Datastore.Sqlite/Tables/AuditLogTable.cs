namespace RoomToRentRadar.Datastore.Sqlite.Tables;

public sealed class AuditLogTable
{
    public string Id { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? DetailsJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public int? StatusCode { get; set; }
    public long? DurationMs { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime TimestampUtc { get; set; }
}

