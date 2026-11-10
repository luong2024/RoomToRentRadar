using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Entities;

public sealed class AuditLogEntry
{
    public string Id { get; private set; }
    public string? UserId { get; private set; }
    public string? UserEmail { get; private set; }
    public string Role { get; private set; }
    public string Action { get; private set; }
    public string EntityType { get; private set; }
    public string? EntityId { get; private set; }
    public string? DetailsJson { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public int? StatusCode { get; private set; }
    public long? DurationMs { get; private set; }
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime TimestampUtc { get; private set; }

    public AuditLogEntry(
        string? id,
        string? userId,
        string? userEmail,
        string? role,
        string action,
        string entityType,
        string? entityId = null,
        string? detailsJson = null,
        string? ipAddress = null,
        string? userAgent = null,
        int? statusCode = null,
        long? durationMs = null,
        bool isSuccess = true,
        string? errorMessage = null,
        DateTime? timestampUtc = null)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new DomainValidationException("Action cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new DomainValidationException("EntityType cannot be empty.");
        }

        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim();
        UserId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
        UserEmail = string.IsNullOrWhiteSpace(userEmail) ? null : userEmail.Trim().ToLowerInvariant();
        Role = string.IsNullOrWhiteSpace(role) ? "Anonymous" : role.Trim();
        Action = action.Trim().ToUpperInvariant();
        EntityType = entityType.Trim();
        EntityId = string.IsNullOrWhiteSpace(entityId) ? null : entityId.Trim();
        DetailsJson = string.IsNullOrWhiteSpace(detailsJson) ? null : detailsJson.Trim();
        IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
        UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim();
        StatusCode = statusCode;
        DurationMs = durationMs;
        IsSuccess = isSuccess;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim();

        var resolvedTimestamp = timestampUtc ?? DateTime.UtcNow;
        TimestampUtc = resolvedTimestamp.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(resolvedTimestamp, DateTimeKind.Utc)
            : resolvedTimestamp.ToUniversalTime();
    }

    public string GetEffectivePartitionKey()
    {
        return !string.IsNullOrWhiteSpace(UserId)
            ? UserId
            : $"anonymous_{TimestampUtc:yyyyMM}";
    }
}

