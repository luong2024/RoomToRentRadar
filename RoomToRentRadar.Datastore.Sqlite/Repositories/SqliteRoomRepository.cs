using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Policies;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

public sealed class SqliteRoomRepository : IRoomRepository, IRoomSearchReadModel
{
    private readonly SqliteDbContext _dbContext;

    public SqliteRoomRepository(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Room>> GetAllAsync(CancellationToken ct = default)
    {
        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT Id, Title, Description, Price, Area, Address, Latitude, Longitude,
                       LandlordId, IsAvailable, IsVerified, CreatedAtUtc, Status, LandlordName, LandlordPhoneNumber
                FROM Rooms
                ORDER BY CreatedAtUtc ASC, Id ASC;
                """;

            var rooms = new List<Room>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                rooms.Add(ReadRoom(reader));
            }

            return rooms;
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task<Room?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT Id, Title, Description, Price, Area, Address, Latitude, Longitude,
                       LandlordId, IsAvailable, IsVerified, CreatedAtUtc, Status, LandlordName, LandlordPhoneNumber
                FROM Rooms
                WHERE Id = @id
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("@id", id);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? ReadRoom(reader) : null;
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task AddAsync(Room room, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(room);
        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Rooms (
                    Id, Title, Description, Price, Area, Address, Latitude, Longitude,
                    LandlordId, IsAvailable, IsVerified, CreatedAtUtc, Status, LandlordName, LandlordPhoneNumber
                ) VALUES (
                    @id, @title, @description, @price, @area, @address, @latitude, @longitude,
                    @landlordId, @isAvailable, @isVerified, @createdAtUtc, @status, @landlordName, @landlordPhoneNumber
                );
                """;
            cmd.Parameters.AddWithValue("@id", room.Id);
            cmd.Parameters.AddWithValue("@title", room.Title);
            cmd.Parameters.AddWithValue("@description", room.Description);
            cmd.Parameters.AddWithValue("@price", room.Price);
            cmd.Parameters.AddWithValue("@area", room.Area);
            cmd.Parameters.AddWithValue("@address", room.Address);
            cmd.Parameters.AddWithValue("@latitude", room.Latitude);
            cmd.Parameters.AddWithValue("@longitude", room.Longitude);
            cmd.Parameters.AddWithValue("@landlordId", room.LandlordId);
            cmd.Parameters.AddWithValue("@isAvailable", room.IsAvailable ? 1 : 0);
            cmd.Parameters.AddWithValue("@isVerified", room.IsVerified ? 1 : 0);
            cmd.Parameters.AddWithValue("@createdAtUtc", room.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("@status", room.Status.ToString());
            cmd.Parameters.AddWithValue("@landlordName", (object?)room.LandlordName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@landlordPhoneNumber", (object?)room.LandlordPhoneNumber ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task<IReadOnlyList<RoomCardDto>> GetSimilarRoomsAsync(
        string targetRoomId,
        decimal targetMonthlyRentVnd,
        double targetLatitude,
        double targetLongitude,
        RoomType roomType,
        int limit = 5,
        CancellationToken ct = default)
    {
        var allRooms = await GetAllAsync(ct);
        var safeLimit = Math.Max(1, Math.Min(limit, SimilarRoomsPolicy.MaxSimilarRooms));
        var minPrice = targetMonthlyRentVnd * (1.0m - SimilarRoomsPolicy.PriceToleranceRatio);
        var maxPrice = targetMonthlyRentVnd * (1.0m + SimilarRoomsPolicy.PriceToleranceRatio);

        // BR-LST-10: Available, not target room, within price +/-20%
        var candidates = allRooms
            .Where(r => !string.Equals(r.Id, targetRoomId, StringComparison.OrdinalIgnoreCase) &&
                        r.Status == ListingStatus.Available &&
                        r.MonthlyRentVnd >= minPrice &&
                        r.MonthlyRentVnd <= maxPrice)
            .Select(r => new
            {
                Room = r,
                DistanceKm = HaversineCalculator.DistanceKm(targetLatitude, targetLongitude, r.Latitude, r.Longitude)
            })
            .Where(x => x.DistanceKm <= SimilarRoomsPolicy.MaxDistanceKm)
            .ToList();

        // Prefer same room type
        var sameType = candidates.Where(x => x.Room.RoomType == roomType).ToList();
        var selected = sameType.Count >= SimilarRoomsPolicy.MinResultsBeforeRelaxingRoomType
            ? sameType
            : candidates;

        return selected
            .OrderBy(x => x.DistanceKm)
            .Take(safeLimit)
            .Select(x => MapToCardDto(x.Room, x.DistanceKm))
            .ToList();
    }

    private static Room ReadRoom(SqliteDataReader reader)
    {
        var id = reader.GetString(0);
        var title = reader.GetString(1);
        var description = reader.GetString(2);
        var price = Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture);
        var area = Convert.ToDouble(reader.GetValue(4), CultureInfo.InvariantCulture);
        var address = reader.GetString(5);
        var latitude = Convert.ToDouble(reader.GetValue(6), CultureInfo.InvariantCulture);
        var longitude = Convert.ToDouble(reader.GetValue(7), CultureInfo.InvariantCulture);
        var landlordId = reader.GetString(8);
        var isAvailable = Convert.ToInt64(reader.GetValue(9), CultureInfo.InvariantCulture) != 0;
        var isVerified = Convert.ToInt64(reader.GetValue(10), CultureInfo.InvariantCulture) != 0;
        var createdAtUtc = ParseUtc(reader.GetString(11));
        var statusStr = reader.FieldCount > 12 && !reader.IsDBNull(12) ? reader.GetString(12) : null;
        var landlordName = reader.FieldCount > 13 && !reader.IsDBNull(13) ? reader.GetString(13) : null;
        var landlordPhone = reader.FieldCount > 14 && !reader.IsDBNull(14) ? reader.GetString(14) : null;

        var status = Enum.TryParse<ListingStatus>(statusStr, true, out var parsedStatus)
            ? parsedStatus
            : (isAvailable ? ListingStatus.Available : ListingStatus.Rented);

        return new Room(
            id: id,
            title: title,
            description: description,
            monthlyRent: price,
            depositAmount: 0m,
            areaSquareMeters: area,
            roomType: "BoardingRoom",
            amenities: null,
            fullAddress: address,
            landlordName: landlordName,
            landlordPhoneNumber: landlordPhone,
            photoUrls: null,
            panorama360Url: null,
            houseRules: null,
            status: status,
            lastUpdatedAt: createdAtUtc,
            isVerified: isVerified,
            latitude: latitude,
            longitude: longitude,
            landlordId: landlordId
        );
    }

    private static DateTime ParseUtc(string iso8601) =>
        DateTime.Parse(iso8601, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    // Haversine distance is provided by HaversineCalculator (Domain.Services) — no inline duplication per M2 fix.

    private static RoomCardDto MapToCardDto(Room room, double distanceKm) => new(
        Id: room.Id,
        Title: room.Title,
        MonthlyRentVnd: (long)room.MonthlyRentVnd,
        AreaM2: room.AreaM2,
        RoomType: room.RoomType,
        DistanceKm: distanceKm,
        IsVerified: room.IsVerified,
        Status: room.Status,
        Latitude: room.Latitude,
        Longitude: room.Longitude,
        CoverThumbUrl: null,
        IsFavourite: false,
        PublishedAtUtc: room.PublishedAtUtc
    );

    private void ReleaseConnection(SqliteConnection conn)
    {
        if (!_dbContext.OwnsSharedConnection)
        {
            conn.Dispose();
        }
    }
}
