using System.Collections.Concurrent;
using RoomToRentRadar.Domain.Aggregates;
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
        _roomsById[room.Id] = Copy(room);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Blind upsert of a test double: this adapter performs NO optimistic-concurrency check.
    /// Ownership of `UPDATE … WHERE Id = @Id AND Version = @Version` (GUARDRAILS 1.5.2) belongs to
    /// <c>SqliteRoomRepository</c> (Task 3).
    /// </summary>
    public Task UpdateAsync(Room room, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(room);
        ct.ThrowIfCancellationRequested();

        _roomsById[room.Id] = Copy(room);
        return Task.CompletedTask;
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
        publishedAtUtc: source.PublishedAtUtc);
}
