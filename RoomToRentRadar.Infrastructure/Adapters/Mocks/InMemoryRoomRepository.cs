using System.Collections.Concurrent;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Infrastructure.Adapters.Mocks;

/// <summary>
/// Test-only <see cref="IRoomRepository"/>. Starts empty unless a seed is supplied by the caller
/// (M1/M5: demo data lives only in the SQLite seed, never in Infrastructure). Copies rooms on the
/// way in and out so the store cannot be mutated through an alias.
/// </summary>
public sealed class InMemoryRoomRepository : IRoomRepository
{
    private readonly ConcurrentDictionary<string, Room> _roomsById = new(StringComparer.Ordinal);

    public InMemoryRoomRepository(IEnumerable<Room>? seed = null)
    {
        foreach (var room in seed ?? Array.Empty<Room>())
        {
            _roomsById[room.Id] = Copy(room);
        }
    }

    public Task<Room?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_roomsById.TryGetValue(id, out var room) ? Copy(room) : null);
    }

    public Task AddAsync(Room room, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(room);
        ct.ThrowIfCancellationRequested();

        if (!_roomsById.TryAdd(room.Id, Copy(room)))
        {
            // LSP parity with SqliteRoomRepository (GUARDRAILS §1.6.3): a duplicate id is a validation error.
            throw new DomainValidationException(
                $"A room with Id '{room.Id}' already exists.",
                "DUPLICATE_ROOM");
        }

        room.MarkPersisted();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Same optimistic-concurrency contract as <c>SqliteRoomRepository</c> (GUARDRAILS §1.5.2, §1.6.3):
    /// the stored version must equal <see cref="Room.LoadedVersion"/>; otherwise — including an unknown
    /// id — the operation fails with <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public Task UpdateAsync(Room room, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(room);
        ct.ThrowIfCancellationRequested();

        if (!_roomsById.TryGetValue(room.Id, out var stored)
            || stored.Version != room.LoadedVersion)
        {
            throw new ConcurrencyConflictException(
                $"Optimistic concurrency conflict occurred updating room '{room.Id}' (expected version {room.LoadedVersion}).");
        }

        _roomsById[room.Id] = Copy(room);
        room.MarkPersisted();
        return Task.CompletedTask;
    }

    /// <summary>Anti-IDOR parity with SQLite (GUARDRAILS §1.5.6): another landlord's room looks missing.</summary>
    public Task<Room?> GetOwnedByIdAsync(string id, string landlordId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(landlordId);
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(
            _roomsById.TryGetValue(id, out var room)
            && string.Equals(room.LandlordId, landlordId, StringComparison.Ordinal)
                ? Copy(room)
                : null);
    }

    /// <summary>BR-LST-11 parity with SQLite: <c>Unpublished</c> rooms are archived and not counted.</summary>
    public Task<int> CountNonArchivedByLandlordAsync(string landlordId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(landlordId);
        ct.ThrowIfCancellationRequested();

        var count = _roomsById.Values.Count(room =>
            string.Equals(room.LandlordId, landlordId, StringComparison.Ordinal)
            && room.Status != ListingStatus.Unpublished);

        return Task.FromResult(count);
    }

    public Task<PagedOwnedRooms> ListByLandlordAsync(
        string landlordId,
        ListingStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(landlordId);

        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            throw new DomainValidationException($"Listing status '{(int)status.Value}' is not a recognized status.");
        }

        ct.ThrowIfCancellationRequested();

        var owned = _roomsById.Values
            .Where(room => string.Equals(room.LandlordId, landlordId, StringComparison.Ordinal))
            .ToList();

        var countsByStatus = Enum.GetValues<ListingStatus>()
            .ToDictionary(listingStatus => listingStatus, listingStatus => owned.Count(room => room.Status == listingStatus));

        var filtered = owned
            .Where(room => !status.HasValue || room.Status == status.Value)
            .OrderByDescending(room => room.LastUpdatedAtUtc)
            .ThenBy(room => room.Id, StringComparer.Ordinal)
            .ToList();

        var effectivePageSize = Math.Max(pageSize, 0);
        var offset = ((long)Math.Max(pageNumber, 1) - 1) * effectivePageSize;
        var items = offset >= filtered.Count
            ? new List<OwnedRoomListing>()
            : filtered.Skip((int)offset).Take(effectivePageSize)
                .Select(room => new OwnedRoomListing(Copy(room), CoverThumbPath: null))
                .ToList();

        return Task.FromResult(new PagedOwnedRooms(items, filtered.Count, countsByStatus));
    }

    private static Room Copy(Room source) => Room.Reconstitute(
        id: source.Id,
        landlordId: source.LandlordId,
        title: source.Title,
        roomType: source.RoomType,
        monthlyRentVnd: source.MonthlyRentVnd,
        depositVnd: source.DepositVnd,
        areaM2: source.AreaM2,
        addressLine: source.AddressLine,
        ward: source.Ward,
        district: source.District,
        latitude: source.Latitude,
        longitude: source.Longitude,
        status: source.Status,
        createdAtUtc: source.CreatedAtUtc,
        lastUpdatedAtUtc: source.LastUpdatedAtUtc,
        version: source.Version,
        concurrencyToken: source.ConcurrencyToken,
        description: source.Description,
        maxOccupants: source.MaxOccupants,
        furnishing: source.Furnishing,
        availableFrom: source.AvailableFrom,
        electricityPriceVndPerKwh: source.ElectricityPriceVndPerKwh,
        waterPriceVnd: source.WaterPriceVnd,
        waterPriceUnit: source.WaterPriceUnit,
        internetFeeVnd: source.InternetFeeVnd,
        parkingFeeVnd: source.ParkingFeeVnd,
        otherFeesNote: source.OtherFeesNote,
        statusReason: source.StatusReason,
        needsReReview: source.NeedsReReview,
        isSafetyVerified: source.IsSafetyVerified,
        verifiedAtUtc: source.VerifiedAtUtc,
        verifiedByUserId: source.VerifiedByUserId,
        publishedAtUtc: source.PublishedAtUtc,
        amenities: source.Amenities,
        safetyFeatures: source.SafetyFeatures,
        currentHouseRules: source.CurrentHouseRules,
        houseRuleHistory: source.HouseRuleHistory);
}
