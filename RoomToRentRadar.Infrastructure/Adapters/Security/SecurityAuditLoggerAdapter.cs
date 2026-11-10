using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Infrastructure.Adapters.Security;

public sealed class SecurityAuditLoggerAdapter : ISecurityAuditLoggerPort
{
    private readonly IAuditLogRepository? _auditLogRepository;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly Action<string>? _sink;

    public SecurityAuditLoggerAdapter(
        IAuditLogRepository? auditLogRepository = null,
        IServiceScopeFactory? scopeFactory = null,
        Action<string>? sink = null)
    {
        _auditLogRepository = auditLogRepository;
        _scopeFactory = scopeFactory;
        _sink = sink;
    }

    public SecurityAuditLoggerAdapter(Action<string>? sink)
        : this(null, null, sink)
    {
    }

    public void LogUserRegistered(string userId, string email, string role, string authProvider, bool isVerified)
    {
        WriteEntry("USER_REGISTERED", $"userId={userId} email={email} role={role} provider={authProvider} isVerified={isVerified}");

        PersistLogSafe(new AuditLogEntry(
            id: null,
            userId: userId,
            userEmail: email,
            role: role,
            action: "USER_REGISTERED",
            entityType: "User",
            entityId: userId,
            detailsJson: $"{{\"provider\":\"{authProvider}\",\"isVerified\":{isVerified.ToString().ToLowerInvariant()}}}",
            isSuccess: true));
    }

    public void LogLoginSucceeded(string userId, string email, string role, string authProvider)
    {
        WriteEntry("LOGIN_SUCCEEDED", $"userId={userId} email={email} role={role} provider={authProvider}");

        PersistLogSafe(new AuditLogEntry(
            id: null,
            userId: userId,
            userEmail: email,
            role: role,
            action: "LOGIN_SUCCEEDED",
            entityType: "User",
            entityId: userId,
            detailsJson: $"{{\"provider\":\"{authProvider}\"}}",
            isSuccess: true));
    }

    public void LogLoginFailed(string email, string reason)
    {
        WriteEntry("LOGIN_FAILED", $"email={email} reason={reason}");

        PersistLogSafe(new AuditLogEntry(
            id: null,
            userId: null,
            userEmail: email,
            role: "Anonymous",
            action: "LOGIN_FAILED",
            entityType: "User",
            entityId: null,
            detailsJson: $"{{\"reason\":\"{reason}\"}}",
            isSuccess: false,
            errorMessage: reason));
    }

    public void LogAccountLockedOut(string userId, string email, DateTime lockoutEndUtc)
    {
        WriteEntry("ACCOUNT_LOCKED_OUT", $"userId={userId} email={email} lockoutEndUtc={lockoutEndUtc:O}");

        PersistLogSafe(new AuditLogEntry(
            id: null,
            userId: userId,
            userEmail: email,
            role: "User",
            action: "ACCOUNT_LOCKED_OUT",
            entityType: "User",
            entityId: userId,
            detailsJson: $"{{\"lockoutEndUtc\":\"{lockoutEndUtc:O}\"}}",
            isSuccess: false,
            errorMessage: $"Locked out until {lockoutEndUtc:O}"));
    }

    public void LogTokenRefreshed(string userId, string newJwtId)
    {
        WriteEntry("TOKEN_REFRESHED", $"userId={userId} newJwtId={newJwtId}");

        PersistLogSafe(new AuditLogEntry(
            id: null,
            userId: userId,
            userEmail: null,
            role: "User",
            action: "TOKEN_REFRESHED",
            entityType: "Token",
            entityId: newJwtId,
            detailsJson: $"{{\"newJwtId\":\"{newJwtId}\"}}",
            isSuccess: true));
    }

    public void LogTokenRevoked(string userId, string reason)
    {
        WriteEntry("TOKEN_REVOKED", $"userId={userId} reason={reason}");

        PersistLogSafe(new AuditLogEntry(
            id: null,
            userId: userId,
            userEmail: null,
            role: "User",
            action: "TOKEN_REVOKED",
            entityType: "Token",
            entityId: null,
            detailsJson: $"{{\"reason\":\"{reason}\"}}",
            isSuccess: true,
            errorMessage: reason));
    }

    public void LogLandlordApproved(string adminUserId, string landlordUserId)
    {
        WriteEntry("LANDLORD_APPROVED", $"adminUserId={adminUserId} landlordUserId={landlordUserId}");

        PersistLogSafe(new AuditLogEntry(
            id: null,
            userId: adminUserId,
            userEmail: null,
            role: "Admin",
            action: "LANDLORD_APPROVED",
            entityType: "User",
            entityId: landlordUserId,
            detailsJson: $"{{\"adminUserId\":\"{adminUserId}\",\"landlordUserId\":\"{landlordUserId}\"}}",
            isSuccess: true));
    }

    private void PersistLogSafe(AuditLogEntry entry)
    {
        try
        {
            if (_auditLogRepository != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _auditLogRepository.InsertAsync(entry);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[SECURITY_AUDIT_ERROR] Failed to persist audit log: {ex.Message}");
                    }
                });
                return;
            }

            if (_scopeFactory != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var repo = scope.ServiceProvider.GetService<IAuditLogRepository>();
                        if (repo != null)
                        {
                            await repo.InsertAsync(entry);
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[SECURITY_AUDIT_ERROR] Failed to persist audit log: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[SECURITY_AUDIT_ERROR] Failed to initiate persist: {ex.Message}");
        }
    }

    private void WriteEntry(string eventType, string details)
    {
        var line = $"[SECURITY_AUDIT] [{DateTime.UtcNow:O}] [{eventType}] {details}";
        _sink?.Invoke(line);
        Trace.WriteLine(line);
    }
}
