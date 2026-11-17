using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Landlord;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Transactions;

namespace RoomToRentRadar.Application.UseCases.Landlord;

/// <summary>
/// Shared body of the five landlord state transitions (UC-33, BR-LST-04): <c>If-Match</c> (428/412),
/// ownership-scoped load (404 anti-IDOR), version check against the loaded aggregate, the explicit domain
/// transition method (409 <c>INVALID_STATE_TRANSITION</c> on a wrong state) and the optimistic-concurrency
/// save. Returns the listing with its new <c>ETag</c> version.
/// </summary>
internal static class LandlordListingTransition
{
    public static async Task<MyListingDetailDto> ExecuteAsync(
        TransitionMyListingCommand command,
        Action<Room> transition,
        IRoomRepository roomRepository,
        IRoomPhotoRepository photoRepository,
        IUnitOfWork unitOfWork,
        IPhotoStoragePort storage,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(roomRepository);
        ArgumentNullException.ThrowIfNull(photoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(storage);

        if (string.IsNullOrWhiteSpace(command.LandlordId))
        {
            throw new DomainValidationException("The landlord is required.");
        }

        if (string.IsNullOrWhiteSpace(command.RoomId))
        {
            throw new DomainValidationException("Room id is required.");
        }

        var expectedVersion = IfMatchParser.ParseRequiredVersion(command.IfMatch);

        var room = await roomRepository
            .GetOwnedByIdAsync(command.RoomId, command.LandlordId, ct)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(
                ProblemCodes.RoomNotFound,
                $"Room '{command.RoomId}' was not found.");

        if (room.Version != expectedVersion)
        {
            throw new EtagMismatchException(
                $"If-Match '\"{expectedVersion}\"' does not match the current listing version {room.Version}.");
        }

        transition(room);

        // The aggregate save also rewrites its child tables, so it runs in one transaction.
        await unitOfWork.ExecuteAsync(
            innerCt => roomRepository.UpdateAsync(room, innerCt),
            ct).ConfigureAwait(false);

        var photos = await photoRepository.ListByRoomAsync(room.Id, ct).ConfigureAwait(false);
        return LandlordListingMapper.ToDetail(room, photos, storage);
    }
}
