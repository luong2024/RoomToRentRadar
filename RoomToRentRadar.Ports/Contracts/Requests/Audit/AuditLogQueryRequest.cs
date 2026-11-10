namespace RoomToRentRadar.Ports.Contracts.Requests.Audit;

public record AuditLogQueryRequest(
    string? UserId = null,
    string? Action = null,
    string? EntityType = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int PageNumber = 1,
    int PageSize = 50);

