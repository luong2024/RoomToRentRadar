using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Security;

namespace RoomToRentRadar.Application.UseCases.Rooms;

public class GetRoomDetailUseCase : IGetRoomDetailUseCase
{
    public const string DefaultUnavailableMessage = "Phòng trọ này hiện không còn khả dụng. Dưới đây là gợi ý các phòng tương tự gần bạn.";

    private readonly IRoomRepository _roomRepository;
    private readonly ICurrentUserContext _currentUserContext;

    public GetRoomDetailUseCase(IRoomRepository roomRepository, ICurrentUserContext currentUserContext)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    public async Task<RoomDetailDto?> ExecuteAsync(string roomId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return null;
        }

        var room = await _roomRepository.GetByIdAsync(roomId, ct);
        if (room == null)
        {
            return null;
        }

        // AC4: Only signed-in users can see the landlord's phone number
        var landlordPhone = _currentUserContext.IsAuthenticated ? room.LandlordPhoneNumber : null;

        // AC3: If the listing is unavailable, provide a message and suggest similar rooms
        IReadOnlyList<RoomCardDto> similarRooms = Array.Empty<RoomCardDto>();
        string? unavailableMessage = null;

        if (!room.IsAvailable)
        {
            unavailableMessage = DefaultUnavailableMessage;
            var similarEntities = await _roomRepository.GetSimilarRoomsAsync(
                excludeRoomId: room.Id,
                targetPrice: room.MonthlyRent,
                targetArea: room.AreaSquareMeters,
                limit: 3,
                ct: ct);

            if (similarEntities != null && similarEntities.Count > 0)
            {
                similarRooms = similarEntities.Select(s => new RoomCardDto(
                    Id: s.Id,
                    Title: s.Title,
                    MonthlyRent: s.MonthlyRent,
                    AreaSquareMeters: s.AreaSquareMeters,
                    DistanceToCampusKm: s.DistanceToCampusKm,
                    IsVerified: s.IsVerified,
                    ThumbnailUrl: s.PhotoUrls.FirstOrDefault() ?? string.Empty,
                    AddressSummary: s.FullAddress
                )).ToList();
            }
        }

        // AC1 & AC2: Comprehensive room details with listing status and last-updated date
        return new RoomDetailDto(
            Id: room.Id,
            Title: room.Title,
            Description: room.Description,
            MonthlyRent: room.MonthlyRent,
            DepositAmount: room.DepositAmount,
            AreaSquareMeters: room.AreaSquareMeters,
            RoomType: room.RoomType,
            Amenities: room.Amenities ?? Array.Empty<string>(),
            FullAddress: room.FullAddress,
            LandlordName: room.LandlordName,
            LandlordPhoneNumber: landlordPhone,
            PhotoUrls: room.PhotoUrls ?? Array.Empty<string>(),
            Panorama360Url: room.Panorama360Url,
            HouseRules: room.HouseRules ?? Array.Empty<string>(),
            Status: room.Status.ToString(),
            LastUpdatedAt: room.LastUpdatedAt,
            IsAvailable: room.IsAvailable,
            UnavailableMessage: unavailableMessage,
            SimilarRooms: similarRooms
        );
    }
}
