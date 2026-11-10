using System.Diagnostics;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadar.Infrastructure.Adapters.Security;

public sealed class SecurityAuditLoggerAdapter : ISecurityAuditLoggerPort
{
    private readonly Action<string>? _sink;

    public SecurityAuditLoggerAdapter(Action<string>? sink = null)
    {
        _sink = sink;
    }

    public void LogUserRegistered(string userId, string email, string role, string authProvider, bool isVerified) =>
        WriteEntry("USER_REGISTERED", $"userId={userId} email={email} role={role} provider={authProvider} isVerified={isVerified}");

    public void LogLoginSucceeded(string userId, string email, string role, string authProvider) =>
        WriteEntry("LOGIN_SUCCEEDED", $"userId={userId} email={email} role={role} provider={authProvider}");

    public void LogLoginFailed(string email, string reason) =>
        WriteEntry("LOGIN_FAILED", $"email={email} reason={reason}");

    public void LogAccountLockedOut(string userId, string email, DateTime lockoutEndUtc) =>
        WriteEntry("ACCOUNT_LOCKED_OUT", $"userId={userId} email={email} lockoutEndUtc={lockoutEndUtc:O}");

    public void LogTokenRefreshed(string userId, string newJwtId) =>
        WriteEntry("TOKEN_REFRESHED", $"userId={userId} newJwtId={newJwtId}");

    public void LogTokenRevoked(string userId, string reason) =>
        WriteEntry("TOKEN_REVOKED", $"userId={userId} reason={reason}");

    public void LogLandlordApproved(string adminUserId, string landlordUserId) =>
        WriteEntry("LANDLORD_APPROVED", $"adminUserId={adminUserId} landlordUserId={landlordUserId}");

    private void WriteEntry(string eventType, string details)
    {
        var line = $"[SECURITY_AUDIT] [{DateTime.UtcNow:O}] [{eventType}] {details}";
        _sink?.Invoke(line);
        Trace.WriteLine(line);
    }
}

