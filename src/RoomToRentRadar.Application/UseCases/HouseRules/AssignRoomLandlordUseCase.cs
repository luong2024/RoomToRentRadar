using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Application.UseCases.HouseRules;

/// <summary>
/// Assigns a verified landlord account as the owner of an existing room (including legacy rooms without LandlordId).
/// </summary>
public sealed class AssignRoomLandlordUseCase : IAssignRoomLandlordUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IUserRepository _userRepository;

    public AssignRoomLandlordUseCase(
        IRoomRepository roomRepository,
        IUserRepository userRepository)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task ExecuteAsync(AssignRoomLandlordCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RoomId))
        {
            throw new RequestValidationException("roomId", "Room ID is required.");
        }

        if (command.LandlordId == Guid.Empty)
        {
            throw new RequestValidationException("landlordId", "A valid landlord user identifier is required.");
        }

        var user = await _userRepository.GetByIdAsync(command.LandlordId, cancellationToken)
            ?? throw new NotFoundException($"Landlord user '{command.LandlordId}' was not found.");

        if (user.Role != UserRole.Landlord)
        {
            throw new RequestValidationException("landlordId", "Assigned owner must have the Landlord role.");
        }

        var trimmedRoomId = command.RoomId.Trim();
        var updated = await _roomRepository.AssignLandlordAsync(trimmedRoomId, command.LandlordId, cancellationToken);
        if (!updated)
        {
            throw new NotFoundException($"Room '{trimmedRoomId}' was not found.");
        }
    }
}
