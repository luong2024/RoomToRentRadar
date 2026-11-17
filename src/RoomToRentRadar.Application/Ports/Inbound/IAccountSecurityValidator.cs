namespace RoomToRentRadar.Application.Ports.Inbound;

/// <summary>
/// Application port used during access token validation to verify that the account remains active
/// and its security stamp and role have not been invalidated since the token was issued.
/// </summary>
public interface IAccountSecurityValidator
{
    Task<bool> ValidateTokenSecurityStateAsync(
        Guid userId,
        string securityStamp,
        string? roleClaim,
        CancellationToken cancellationToken = default);
}
