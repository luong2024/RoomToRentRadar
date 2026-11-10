using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Inbound.UseCases.Auth;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Auth;

public sealed class ApproveLandlordUseCase : IApproveLandlordUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly ISecurityAuditLoggerPort _auditLogger;

    public ApproveLandlordUseCase(
        IUserRepository userRepository,
        ISecurityAuditLoggerPort auditLogger)
    {
        _userRepository = userRepository;
        _auditLogger = auditLogger;
    }

    public Task<UserProfileDto> ExecuteAsync(string adminUserId, ApproveLandlordRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();
}

