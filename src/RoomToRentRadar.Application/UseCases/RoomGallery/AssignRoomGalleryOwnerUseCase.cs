using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.UseCases.RoomGallery;

/// <summary>
/// Admin-only backfill/assignment workflow that binds an existing room to a verified Landlord account
/// from the authoritative user store so legacy rooms without LandlordId can be managed safely.
/// </summary>
public sealed class AssignRoomGalleryOwnerUseCase : IAssignRoomGalleryOwnerUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IUserRepository _userRepository;

    public AssignRoomGalleryOwnerUseCase(
        IRoomRepository roomRepository,
        IUserRepository userRepository)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task ExecuteAsync(
        string roomId,
        Guid landlordId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new InvalidRoomPhotoException("Room ID is required.");
        }

        if (landlordId == Guid.Empty)
        {
            throw new InvalidRoomPhotoException("A valid landlord user ID is required.");
        }

        var normalizedRoomId = roomId.Trim();
        var user = await _userRepository.GetByIdAsync(landlordId, cancellationToken);
        if (user is null || !user.IsActive || user.Role != UserRole.Landlord)
        {
            throw new InvalidRoomPhotoException(
                $"User '{landlordId}' is not an active landlord account.");
        }

        var updated = await _roomRepository.AssignLandlordAsync(normalizedRoomId, landlordId, cancellationToken);
        if (!updated)
        {
            throw new NotFoundException($"Room '{normalizedRoomId}' was not found.");
        }
    }
}
