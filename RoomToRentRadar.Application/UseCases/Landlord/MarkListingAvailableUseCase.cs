using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Landlord;
using RoomToRentRadar.Ports.Inbound.UseCases.Landlord;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Transactions;

namespace RoomToRentRadar.Application.UseCases.Landlord;

/// <summary><c>operationId: markListingAvailable</c> (UC-33, BR-LST-04): Rented → Available.</summary>
public sealed class MarkListingAvailableUseCase : IMarkListingAvailableUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IRoomPhotoRepository _photoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPhotoStoragePort _storage;

    public MarkListingAvailableUseCase(
        IRoomRepository roomRepository,
        IRoomPhotoRepository photoRepository,
        IUnitOfWork unitOfWork,
        IPhotoStoragePort storage)
    {
        ArgumentNullException.ThrowIfNull(roomRepository);
        ArgumentNullException.ThrowIfNull(photoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(storage);

        _roomRepository = roomRepository;
        _photoRepository = photoRepository;
        _unitOfWork = unitOfWork;
        _storage = storage;
    }

    public Task<MyListingDetailDto> ExecuteAsync(
        TransitionMyListingCommand command,
        CancellationToken ct = default) =>
        LandlordListingTransition.ExecuteAsync(
            command,
            (Room room) => room.MarkAvailable(),
            _roomRepository,
            _photoRepository,
            _unitOfWork,
            _storage,
            ct);
}
