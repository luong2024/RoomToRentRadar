using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;

namespace RoomToRentRadar.Application.UseCases;

public class GetRoomDetailsUseCase : IGetRoomDetailsUseCase
{
    private readonly IRoomRepository _roomRepository;

    public GetRoomDetailsUseCase(IRoomRepository roomRepository)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
    }

    public async Task<RoomDetailsDto> ExecuteAsync(string roomId, CancellationToken cancellationToken = default)
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

        return new RoomDetailsDto
        {
            Id = room.Id,
            Title = room.Title,
            Description = room.Description,
            Price = room.Price,
            FormattedPrice = $"{room.Price:N0} VNĐ",
            Area = room.Area,
            FormattedArea = $"{room.Area} m²",
            Address = room.Address,
            IsVerified = room.IsVerified,
            Status = room.Status.ToString()
        };
    }
}
