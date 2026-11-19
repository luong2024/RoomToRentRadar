namespace RoomToRentRadar.Ports.Contracts.Requests.Audit;

public record CreateAuditLogRequest(
    string? UserId,
    string? UserEmail,
    string Role,
    string Action,
    string EntityType,
    string? EntityId = null,
    string? DetailsJson = null,
    string? IpAddress = null,
    string? UserAgent = null,
    int? StatusCode = null,
    long? DurationMs = null,
    bool IsSuccess = true,
    string? ErrorMessage = null);

