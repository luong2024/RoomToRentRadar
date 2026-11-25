namespace RoomToRentRadar.Application.UseCases.Rooms;

using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
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
    private readonly IRoomPhotoRepository _photoRepository;
    private readonly IPhotoStoragePort _storage;

    public GetRoomDetailUseCase(
        IRoomRepository roomRepository,
        IUserRepository userRepository,
        ICampusRepository campusRepository,
        ICurrentUserContextPort currentUserContext,
        IRoomPhotoRepository photoRepository,
        IPhotoStoragePort storage)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _campusRepository = campusRepository ?? throw new ArgumentNullException(nameof(campusRepository));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _photoRepository = photoRepository ?? throw new ArgumentNullException(nameof(photoRepository));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
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
        RoomQueryHelper.EnsureViewable(room, roomId, _currentUserContext);

        // BR-CMP-01..03: Resolve campus (explicit -> authenticated profile -> default ICTU)
        var campus = await RoomQueryHelper.ResolveCampusAsync(
            campusId, _campusRepository, _userRepository, _currentUserContext, ct);

        // BR-AUTH-07 / AC4: Unmasked phone for authenticated users only
        var landlord = await _userRepository.GetByIdAsync(room.LandlordId, ct);
        var isPhoneVisible = _currentUserContext.IsAuthenticated;

        // Load and map room photos ordered by SortOrder per BR-MED-05
        var photos = await _photoRepository.ListByRoomAsync(room.Id, ct);
        var photoDtos = photos
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .Select(p => LandlordListingMapper.ToPhotoDto(p, _storage))
            .ToList();

        return RoomMapper.ToDetailDto(room, landlord, campus, isPhoneVisible, photoDtos);
    }
}
