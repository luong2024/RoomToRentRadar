using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Exceptions;
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

    public async Task<UserProfileDto> ExecuteAsync(string adminUserId, ApproveLandlordRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            throw new DomainValidationException("Admin user ID is required.");
        }

        if (string.IsNullOrWhiteSpace(request.LandlordUserId))
        {
            throw new DomainValidationException("Landlord user ID is required.");
        }

        var landlord = await _userRepository.GetByIdAsync(request.LandlordUserId, ct);
        if (landlord == null)
        {
            throw new UserNotFoundException(request.LandlordUserId);
        }

        landlord.ApproveLandlord(DateTime.UtcNow);
        await _userRepository.UpdateAsync(landlord, ct);
        _auditLogger.LogLandlordApproved(adminUserId, landlord.Id);

        return UserMapper.ToProfileDto(landlord);
    }
}
