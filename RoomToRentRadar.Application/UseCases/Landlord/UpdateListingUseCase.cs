using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Application.UseCases.Photos;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Requests.Listings;
using RoomToRentRadar.Ports.Contracts.Responses.Listings;
using RoomToRentRadar.Ports.Inbound.UseCases.Landlord;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Transactions;

namespace RoomToRentRadar.Application.UseCases.Landlord;

/// <summary>
/// <c>operationId: updateListing</c> (UC-32, US08 AC3, BR-LST-05/09; BR-HR-02 for house rules):
/// mandatory <c>If-Match</c> (428/412, ADR-012), ownership-scoped load (404 anti-IDOR), photo mix
/// re-validation and a single unit of work that updates the room, removes dropped photos and re-attaches
/// the ordered list. <c>houseRules</c> is ignored — rules only change through <c>updateHouseRules</c> so
/// every change is versioned.
/// </summary>
public sealed class UpdateListingUseCase : IUpdateListingUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IRoomPhotoRepository _photoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPhotoStoragePort _storage;
    private readonly ITravelTimeCacheRepository _travelTimeCacheRepository;

    public UpdateListingUseCase(
        IRoomRepository roomRepository,
        IRoomPhotoRepository photoRepository,
        IUnitOfWork unitOfWork,
        IPhotoStoragePort storage,
        ITravelTimeCacheRepository travelTimeCacheRepository)
    {
        ArgumentNullException.ThrowIfNull(roomRepository);
        ArgumentNullException.ThrowIfNull(photoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(travelTimeCacheRepository);

        _roomRepository = roomRepository;
        _photoRepository = photoRepository;
        _unitOfWork = unitOfWork;
        _storage = storage;
        _travelTimeCacheRepository = travelTimeCacheRepository;
    }

    public async Task<MyListingDetailDto> ExecuteAsync(UpdateListingCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.LandlordId))
        {
            throw new DomainValidationException("The landlord is required.");
        }

        if (string.IsNullOrWhiteSpace(command.RoomId))
        {
            throw new DomainValidationException("Room id is required.");
        }

        var listing = command.Listing
            ?? throw new DomainValidationException("The listing payload is required.");
        if (listing.Address is null || listing.Location is null)
        {
            throw new DomainValidationException("The listing address and location are required.", ProblemCodes.ValidationFailed);
        }

        var expectedVersion = IfMatchParser.ParseRequiredVersion(command.IfMatch);

        var room = await _roomRepository
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

        var utcNow = DateTime.UtcNow;
        var photos = await ListingPhotoValidator
            .ValidateAsync(listing.PhotoIds, command.LandlordId, room.Id, _photoRepository, utcNow, ct)
            .ConfigureAwait(false);

        var pinMoved = !room.Latitude.Equals(listing.Location.Latitude)
            || !room.Longitude.Equals(listing.Location.Longitude);

        room.UpdateDetails(ToDetails(listing, photos.HasAddedNewPhotos));

        var removedPhotos = new List<Domain.Entities.RoomPhoto>();
        await _unitOfWork.ExecuteAsync(
            innerCt => UpdateRoomAndPhotosAsync(command.LandlordId, room, photos.OrderedIds, removedPhotos, pinMoved, innerCt),
            ct).ConfigureAwait(false);

        // After commit: the rows are gone, so a failed file delete must not fail the request.
        await PhotoFileCleanup.DeleteManyAsync(_storage, removedPhotos).ConfigureAwait(false);

        var storedPhotos = await _photoRepository.ListByRoomAsync(room.Id, ct).ConfigureAwait(false);
        return LandlordListingMapper.ToDetail(room, storedPhotos, _storage);
    }

    /// <summary>Body of the unit of work: room + children, photo removal, pin-cache invalidation and re-attach are one commit.</summary>
    private async Task UpdateRoomAndPhotosAsync(
        string landlordId,
        Domain.Aggregates.Room room,
        IReadOnlyList<string> photoIds,
        List<Domain.Entities.RoomPhoto> removedPhotos,
        bool pinMoved,
        CancellationToken ct)
    {
        await _roomRepository.UpdateAsync(room, ct).ConfigureAwait(false);

        if (pinMoved)
        {
            // BR-TRV-03: invalidate cached travel times whenever the room pin moves.
            await _travelTimeCacheRepository.InvalidateByRoomIdAsync(room.Id, ct).ConfigureAwait(false);
        }

        var removed = await _photoRepository
            .RemoveNotInListAsync(room.Id, photoIds, ct)
            .ConfigureAwait(false);
        removedPhotos.AddRange(removed);

        await _photoRepository.AttachAsync(landlordId, room.Id, photoIds, ct).ConfigureAwait(false);
    }

    private static RoomDetails ToDetails(UpsertListingRequest listing, bool hasAddedNewPhotos) =>
        new(
            Title: listing.Title,
            Description: listing.Description,
            RoomType: listing.RoomType,
            MonthlyRentVnd: listing.MonthlyRentVnd,
            DepositVnd: listing.DepositVnd,
            AreaM2: listing.AreaM2,
            MaxOccupants: listing.MaxOccupants,
            Furnishing: listing.Furnishing == default ? null : listing.Furnishing.ToString(),
            AvailableFrom: listing.AvailableFrom,
            AddressLine: listing.Address.AddressLine,
            Ward: listing.Address.Ward,
            District: listing.Address.District,
            Latitude: listing.Location.Latitude,
            Longitude: listing.Location.Longitude,
            ElectricityPriceVndPerKwh: listing.Utilities?.ElectricityPriceVndPerKwh,
            WaterPriceVnd: listing.Utilities?.WaterPriceVnd,
            WaterPriceUnit: listing.Utilities is null || listing.Utilities.WaterPriceUnit == default
                ? null
                : listing.Utilities.WaterPriceUnit.ToString(),
            InternetFeeVnd: listing.Utilities?.InternetFeeVnd,
            ParkingFeeVnd: listing.Utilities?.ParkingFeeVnd,
            OtherFeesNote: listing.Utilities?.OtherFeesNote,
            Amenities: listing.Amenities ?? Array.Empty<Amenity>(),
            SafetyFeatures: listing.SafetyFeatures ?? Array.Empty<SafetyFeature>(),
            HasAddedNewPhotos: hasAddedNewPhotos);
}
