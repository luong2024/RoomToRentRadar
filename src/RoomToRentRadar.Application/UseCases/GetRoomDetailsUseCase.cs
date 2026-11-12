using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.UseCases;

public class GetRoomDetailsUseCase : IGetRoomDetailsUseCase
{
    private readonly IRoomRepository _roomRepository;

    public GetRoomDetailsUseCase(IRoomRepository roomRepository)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
    }

    public async Task<RoomDetailsDto> ExecuteAsync(string roomId, bool isUserVerified, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new ArgumentException("Mã phòng không được để trống.", nameof(roomId));
        }

        var room = await _roomRepository.GetByIdAsync(roomId, cancellationToken);
        if (room is null)
        {
            throw new NotFoundException($"Không tìm thấy phòng với ID '{roomId}'.");
        }

        var isAvailable = room.Status == RoomStatus.Available;

        var dto = new RoomDetailsDto
        {
            Id = room.Id,
            Title = room.Title,
            Description = room.Description,
            Price = room.Price,
            FormattedPrice = $"{room.Price:N0} VNĐ",
            Deposit = room.Deposit,
            FormattedDeposit = $"{room.Deposit:N0} VNĐ",
            Area = room.Area,
            FormattedArea = $"{room.Area} m²",
            Address = isAvailable ? room.Address : string.Empty,
            RoomType = room.RoomType,
            Amenities = room.Amenities ?? [],
            HouseRules = room.HouseRules ?? [],
            LandlordPhone = isAvailable ? room.LandlordPhone : string.Empty,
            LastUpdatedDate = room.LastUpdatedDate,
            IsVerified = room.IsVerified,
            Status = room.Status.ToString(),
            IsAvailable = isAvailable,
            UnavailableMessage = isAvailable ? null : "Phòng này hiện không khả dụng."
        };

        if (isAvailable && !isUserVerified)
        {
            dto.LandlordPhone = "*** (Yêu cầu xác thực để xem)";
        }

        if (!isAvailable)
        {
            var similar = await _roomRepository.GetSimilarRoomsAsync(room.Id, room.Price, room.Area, cancellationToken);
            dto.SimilarRooms = similar
                .Where(r => r.Status == RoomStatus.Available && r.Id != room.Id)
                .Select(r => new SimilarRoomDto
                {
                    Id = r.Id,
                    Title = r.Title,
                    Price = r.Price,
                    FormattedPrice = $"{r.Price:N0} VNĐ",
                    Area = r.Area,
                    FormattedArea = $"{r.Area} m²",
                    Address = r.Address,
                    RoomType = r.RoomType,
                    IsVerified = r.IsVerified
                })
                .ToList();
        }

        return dto;
    }
}
