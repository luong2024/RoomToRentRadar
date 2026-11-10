namespace RoomToRentRadar.Ports.Outbound.ExternalServices;

public interface ISecurityAuditLoggerPort
{
    void LogUserRegistered(string userId, string email, string role, string authProvider, bool isVerified);
    void LogLoginSucceeded(string userId, string email, string role, string authProvider);
    void LogLoginFailed(string email, string reason);
    void LogAccountLockedOut(string userId, string email, DateTime lockoutEndUtc);
    void LogTokenRefreshed(string userId, string newJwtId);
    void LogTokenRevoked(string userId, string reason);
    void LogLandlordApproved(string adminUserId, string landlordUserId);
}

