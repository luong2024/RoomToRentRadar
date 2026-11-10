namespace RoomToRentRadar.Ports.Contracts.Responses.Audit;

public record AuditLogDto(
    string Id,
    string? UserId,
    string? UserEmail,
    string Role,
    string Action,
    string EntityType,
    string? EntityId,
    string? DetailsJson,
    string? IpAddress,
    string? UserAgent,
    int? StatusCode,
    long? DurationMs,
    bool IsSuccess,
    string? ErrorMessage,
    DateTime TimestampUtc);

