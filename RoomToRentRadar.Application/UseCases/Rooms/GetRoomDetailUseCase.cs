namespace RoomToRentRadar.Application.UseCases.Rooms;

using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

/// <summary>
/// Retrieves the full details of a room listing (US02, UC-03, getRoomDetail).
/// Enforces BR-LST-04a (public visibility), BR-AUTH-07 (landlord phone gating), and BR-CMP-01 (campus resolution).
/// </summary>
public sealed class GetRoomDetailUseCase : IGetRoomDetailUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICampusRepository _campusRepository;
    private readonly ICurrentUserContextPort _currentUserContext;

    public GetRoomDetailUseCase(
        IRoomRepository roomRepository,
        IUserRepository userRepository,
        ICampusRepository campusRepository,
        ICurrentUserContextPort currentUserContext)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _campusRepository = campusRepository ?? throw new ArgumentNullException(nameof(campusRepository));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    public async Task<RoomDetailDto> ExecuteAsync(
        string roomId,
        string? campusId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new EntityNotFoundException(ProblemCodes.RoomNotFound, "Room ID is required.");
        }

        var room = await _roomRepository.GetByIdAsync(roomId.Trim(), ct);
        if (room == null)
        {
            throw new EntityNotFoundException(ProblemCodes.RoomNotFound, $"Room with ID '{roomId}' was not found.");
        }

        // BR-LST-04a: Available, Rented, Unpublished are viewable by public.
        // PendingReview, Rejected, Suspended return 404 to non-owners/non-admins.
        if (room.Status != ListingStatus.Available &&
            room.Status != ListingStatus.Rented &&
            room.Status != ListingStatus.Unpublished)
        {
            var isOwner = _currentUserContext.IsAuthenticated &&
                          string.Equals(_currentUserContext.UserId, room.LandlordId, StringComparison.Ordinal);
            var isAdmin = _currentUserContext.IsAuthenticated &&
                          string.Equals(_currentUserContext.Role, "Admin", StringComparison.OrdinalIgnoreCase);

            if (!isOwner && !isAdmin)
            {
                throw new EntityNotFoundException(
                    ProblemCodes.RoomNotFound,
                    $"Room with ID '{roomId}' was not found or is not public.");
            }
        }

        // BR-CMP-01: Resolve campus for distance calculation
        Campus campus;
        if (string.IsNullOrWhiteSpace(campusId))
        {
            campus = await _campusRepository.GetDefaultCampusAsync(ct)
                ?? throw new InvalidOperationException("Default campus (ICTU) is not configured.");
        }
        else
        {
            campus = await _campusRepository.GetByIdAsync(campusId.Trim(), ct)
                ?? throw new DomainValidationException($"Campus with ID '{campusId}' was not found.", ProblemCodes.CampusNotFound);
        }

        // BR-AUTH-07 / AC4: Unmasked phone for authenticated users only
        var landlord = await _userRepository.GetByIdAsync(room.LandlordId, ct);
        var isPhoneVisible = _currentUserContext.IsAuthenticated;

        return RoomMapper.ToDetailDto(room, landlord, campus, isPhoneVisible);
    }
}
