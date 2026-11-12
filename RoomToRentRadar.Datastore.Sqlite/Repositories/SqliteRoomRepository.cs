using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Policies;
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
                       LandlordId, IsAvailable, IsVerified, CreatedAtUtc
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
                       LandlordId, IsAvailable, IsVerified, CreatedAtUtc
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
                    LandlordId, IsAvailable, IsVerified, CreatedAtUtc
                ) VALUES (
                    @id, @title, @description, @price, @area, @address, @latitude, @longitude,
                    @landlordId, @isAvailable, @isVerified, @createdAtUtc
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
                DistanceKm = CalculateDistance(targetLatitude, targetLongitude, r.Latitude, r.Longitude)
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

    private static Room ReadRoom(SqliteDataReader reader) => Room.Reconstitute(
        id: reader.GetString(0),
        title: reader.GetString(1),
        description: reader.GetString(2),
        price: Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
        area: Convert.ToDouble(reader.GetValue(4), CultureInfo.InvariantCulture),
        address: reader.GetString(5),
        latitude: Convert.ToDouble(reader.GetValue(6), CultureInfo.InvariantCulture),
        longitude: Convert.ToDouble(reader.GetValue(7), CultureInfo.InvariantCulture),
        landlordId: reader.GetString(8),
        isAvailable: Convert.ToInt64(reader.GetValue(9), CultureInfo.InvariantCulture) != 0,
        isVerified: Convert.ToInt64(reader.GetValue(10), CultureInfo.InvariantCulture) != 0,
        createdAtUtc: ParseUtc(reader.GetString(11)));

    private static DateTime ParseUtc(string iso8601) =>
        DateTime.Parse(iso8601, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(earthRadiusKm * c, 2);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static RoomCardDto MapToCardDto(Room room, double distanceKm) => new(
        Id: room.Id,
        Title: room.Title,
        Description: room.Description,
        Price: room.Price,
        Area: room.Area,
        Address: room.FullAddress,
        Latitude: room.Latitude,
        Longitude: room.Longitude,
        DistanceKm: distanceKm,
        IsAvailable: room.IsAvailable,
        IsVerified: room.IsVerified,
        CreatedAtUtc: room.CreatedAtUtc
    );

    private void ReleaseConnection(SqliteConnection conn)
    {
        if (!_dbContext.OwnsSharedConnection)
        {
            conn.Dispose();
        }
    }
}
