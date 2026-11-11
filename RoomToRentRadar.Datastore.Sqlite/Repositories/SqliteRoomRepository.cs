using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

public sealed class SqliteRoomRepository : IRoomRepository
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

    private static Room ReadRoom(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Title = reader.GetString(1),
        Description = reader.GetString(2),
        Price = Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
        Area = Convert.ToDouble(reader.GetValue(4), CultureInfo.InvariantCulture),
        Address = reader.GetString(5),
        Latitude = Convert.ToDouble(reader.GetValue(6), CultureInfo.InvariantCulture),
        Longitude = Convert.ToDouble(reader.GetValue(7), CultureInfo.InvariantCulture),
        LandlordId = reader.GetString(8),
        IsAvailable = Convert.ToInt64(reader.GetValue(9), CultureInfo.InvariantCulture) != 0,
        IsVerified = Convert.ToInt64(reader.GetValue(10), CultureInfo.InvariantCulture) != 0,
        CreatedAtUtc = ParseUtc(reader.GetString(11))
    };

    private static DateTime ParseUtc(string iso8601) =>
        DateTime.Parse(iso8601, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private void ReleaseConnection(SqliteConnection conn)
    {
        if (!_dbContext.OwnsSharedConnection)
        {
            conn.Dispose();
        }
    }
}
