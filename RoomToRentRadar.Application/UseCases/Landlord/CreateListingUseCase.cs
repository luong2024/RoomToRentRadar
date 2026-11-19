using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Requests.Listings;
using RoomToRentRadar.Ports.Contracts.Responses.Listings;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Inbound.UseCases.Landlord;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Transactions;

namespace RoomToRentRadar.Application.UseCases.Landlord;

/// <summary>
/// <c>operationId: createListing</c> (UC-30, US08 AC1–AC2, BR-LST-01..04/11, BR-MED-01..03): enforce the
/// 50-listing cap, validate the 3–20 Standard (+0–3 Panorama) photos, build the aggregate in
/// <c>PendingReview</c> with its children and house-rules version 1, then insert the room and attach the
/// photos in one unit of work.
/// </summary>
public sealed class CreateListingUseCase : ICreateListingUseCase
{
    /// <summary>BR-LST-11: at most 50 non-archived listings per landlord.</summary>
    public const int MaxActiveListings = 50;

    private readonly IRoomRepository _roomRepository;
    private readonly IRoomPhotoRepository _photoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPhotoStoragePort _storage;

    public CreateListingUseCase(
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

    public async Task<MyListingDetailDto> ExecuteAsync(CreateListingCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.LandlordId))
        {
            throw new DomainValidationException("The landlord is required.");
        }

        var listing = command.Listing
            ?? throw new DomainValidationException("The listing payload is required.");
        ValidateRequiredSections(listing);

        var activeListings = await _roomRepository
            .CountNonArchivedByLandlordAsync(command.LandlordId, ct)
            .ConfigureAwait(false);
        if (activeListings >= MaxActiveListings)
        {
            throw new LimitExceededException(
                $"A landlord can have at most {MaxActiveListings} active listings (BR-LST-11).",
                ProblemCodes.LimitExceeded);
        }

        var utcNow = DateTime.UtcNow;
        var photos = await ListingPhotoValidator
            .ValidateAsync(listing.PhotoIds, command.LandlordId, roomId: null, _photoRepository, utcNow, ct)
            .ConfigureAwait(false);

        var room = BuildRoom(command.LandlordId, listing, utcNow);
        room.SetChildren(
            listing.Amenities,
            listing.SafetyFeatures?.Select(feature => new RoomSafetyFeatureEntry(feature, IsConfirmed: false)),
            BuildHouseRules(room.Id, command.LandlordId, listing.HouseRules, utcNow));

        await _unitOfWork.ExecuteAsync(
            innerCt => AddRoomAndAttachPhotosAsync(command.LandlordId, room, photos.OrderedIds, innerCt),
            ct).ConfigureAwait(false);

        var storedPhotos = await _photoRepository.ListByRoomAsync(room.Id, ct).ConfigureAwait(false);
        return LandlordListingMapper.ToDetail(room, storedPhotos, _storage);
    }

    /// <summary>Body of the unit of work: both writes commit or roll back together (US08 AC2).</summary>
    private async Task AddRoomAndAttachPhotosAsync(
        string landlordId,
        Room room,
        IReadOnlyList<string> photoIds,
        CancellationToken ct)
    {
        await _roomRepository.AddAsync(room, ct).ConfigureAwait(false);
        await _photoRepository.AttachAsync(landlordId, room.Id, photoIds, ct).ConfigureAwait(false);
    }

    private static void ValidateRequiredSections(UpsertListingRequest listing)
    {
        if (listing.Address is null)
        {
            throw new DomainValidationException("The listing address is required.", ProblemCodes.ValidationFailed);
        }

        if (listing.Location is null)
        {
            throw new DomainValidationException("The listing location is required.", ProblemCodes.ValidationFailed);
        }
    }

    private static Room BuildRoom(string landlordId, UpsertListingRequest listing, DateTime utcNow) =>
        Room.Create(
            landlordId: landlordId,
            title: listing.Title,
            roomType: listing.RoomType,
            monthlyRentVnd: listing.MonthlyRentVnd,
            depositVnd: listing.DepositVnd,
            areaM2: listing.AreaM2,
            addressLine: listing.Address.AddressLine,
            ward: listing.Address.Ward,
            district: listing.Address.District,
            latitude: listing.Location.Latitude,
            longitude: listing.Location.Longitude,
            description: listing.Description,
            maxOccupants: listing.MaxOccupants,
            furnishing: listing.Furnishing == default ? null : listing.Furnishing.ToString(),
            availableFrom: listing.AvailableFrom,
            electricityPriceVndPerKwh: listing.Utilities?.ElectricityPriceVndPerKwh,
            waterPriceVnd: listing.Utilities?.WaterPriceVnd,
            waterPriceUnit: listing.Utilities is null || listing.Utilities.WaterPriceUnit == default
                ? null
                : listing.Utilities.WaterPriceUnit.ToString(),
            internetFeeVnd: listing.Utilities?.InternetFeeVnd,
            parkingFeeVnd: listing.Utilities?.ParkingFeeVnd,
            otherFeesNote: listing.Utilities?.OtherFeesNote,
            utcNow: utcNow);

    private static HouseRuleVersion? BuildHouseRules(
        string roomId,
        string landlordId,
        HouseRulesFields? rules,
        DateTime utcNow) =>
        rules is null
            ? null
            : HouseRuleVersion.Create(
                roomId: roomId,
                versionNumber: 1, // BR-HR-02: create always starts at version 1
                guestPolicy: rules.GuestPolicy,
                cookingPolicy: rules.CookingPolicy,
                parkingAvailable: rules.ParkingAvailable,
                petsAllowed: rules.PetsAllowed,
                smokingAllowed: rules.SmokingAllowed,
                createdByUserId: landlordId,
                utcNow: utcNow,
                quietHoursStart: rules.QuietHoursStart,
                quietHoursEnd: rules.QuietHoursEnd,
                curfewTime: rules.CurfewTime,
                guestPolicyNote: rules.GuestPolicyNote,
                parkingRules: rules.ParkingRules,
                otherRules: rules.OtherRules);
}
