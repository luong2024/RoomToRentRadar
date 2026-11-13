using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

/// <summary>
/// SQLite storage of the <see cref="Room"/> aggregate root against the ERD §4.4 <c>Rooms</c> table
/// (ADR-002). Enums are stored as TEXT, date-times as ISO-8601 round-trip ("O") strings and
/// <c>ConcurrencyToken</c> as <c>Version.ToString()</c> (ERD §1: derived, never stored).
/// </summary>
public sealed class SqliteRoomRepository : IRoomRepository
{
    private const string SelectColumns = """
        Id, LandlordId, Title, Description, RoomType, MonthlyRentVnd, DepositVnd, AreaM2,
        MaxOccupants, Furnishing, AvailableFrom, AddressLine, Ward, District, Latitude, Longitude,
        ElectricityPriceVndPerKwh, WaterPriceVnd, WaterPriceUnit, InternetFeeVnd, ParkingFeeVnd,
        OtherFeesNote, Status, StatusReason, NeedsReReview, IsSafetyVerified, VerifiedAtUtc,
        VerifiedByUserId, PublishedAtUtc, LastUpdatedAtUtc, CreatedAtUtc, Version
        """;

    private readonly SqliteDbContext _dbContext;

    public SqliteRoomRepository(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<Room?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT {SelectColumns} FROM Rooms WHERE Id = @id LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", id);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            return MapRoom(reader);
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
                    Id, LandlordId, Title, Description, RoomType, MonthlyRentVnd, DepositVnd, AreaM2,
                    MaxOccupants, Furnishing, AvailableFrom, AddressLine, Ward, District, Latitude, Longitude,
                    ElectricityPriceVndPerKwh, WaterPriceVnd, WaterPriceUnit, InternetFeeVnd, ParkingFeeVnd,
                    OtherFeesNote, Status, StatusReason, NeedsReReview, IsSafetyVerified, VerifiedAtUtc,
                    VerifiedByUserId, PublishedAtUtc, LastUpdatedAtUtc, CreatedAtUtc, Version
                ) VALUES (
                    @id, @landlordId, @title, @description, @roomType, @monthlyRentVnd, @depositVnd, @areaM2,
                    @maxOccupants, @furnishing, @availableFrom, @addressLine, @ward, @district, @latitude, @longitude,
                    @electricityPriceVndPerKwh, @waterPriceVnd, @waterPriceUnit, @internetFeeVnd, @parkingFeeVnd,
                    @otherFeesNote, @status, @statusReason, @needsReReview, @isSafetyVerified, @verifiedAtUtc,
                    @verifiedByUserId, @publishedAtUtc, @lastUpdatedAtUtc, @createdAtUtc, @version
                );
                """;
            BindRoomParameters(cmd, room);

            try
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && ex.SqliteExtendedErrorCode == 1555)
            {
                // SQLITE_CONSTRAINT_PRIMARYKEY (19 | 1555 << 8) only: any other constraint violation
                // (e.g. a future UNIQUE/FK) must surface as-is, not be misreported as a duplicate room.
                throw new DomainValidationException(
                    $"A room with Id '{room.Id}' already exists.",
                    "DUPLICATE_ROOM");
            }

            room.MarkPersisted();
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    /// <summary>
    /// Optimistic concurrency (GUARDRAILS 1.5.2): the row must still hold the version this instance
    /// was loaded with (<see cref="Room.LoadedVersion"/>). Using <c>Version - 1</c> would break as
    /// soon as a caller performs more than one transition between load and save. Zero rows updated
    /// means someone else wrote first → <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public async Task UpdateAsync(Room room, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(room);

        var expectedVersion = room.LoadedVersion;

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE Rooms SET
                    LandlordId = @landlordId,
                    Title = @title,
                    Description = @description,
                    RoomType = @roomType,
                    MonthlyRentVnd = @monthlyRentVnd,
                    DepositVnd = @depositVnd,
                    AreaM2 = @areaM2,
                    MaxOccupants = @maxOccupants,
                    Furnishing = @furnishing,
                    AvailableFrom = @availableFrom,
                    AddressLine = @addressLine,
                    Ward = @ward,
                    District = @district,
                    Latitude = @latitude,
                    Longitude = @longitude,
                    ElectricityPriceVndPerKwh = @electricityPriceVndPerKwh,
                    WaterPriceVnd = @waterPriceVnd,
                    WaterPriceUnit = @waterPriceUnit,
                    InternetFeeVnd = @internetFeeVnd,
                    ParkingFeeVnd = @parkingFeeVnd,
                    OtherFeesNote = @otherFeesNote,
                    Status = @status,
                    StatusReason = @statusReason,
                    NeedsReReview = @needsReReview,
                    IsSafetyVerified = @isSafetyVerified,
                    VerifiedAtUtc = @verifiedAtUtc,
                    VerifiedByUserId = @verifiedByUserId,
                    PublishedAtUtc = @publishedAtUtc,
                    LastUpdatedAtUtc = @lastUpdatedAtUtc,
                    -- CreatedAtUtc is immutable: bound on INSERT only, never overwritten here.
                    Version = @version
                WHERE Id = @id AND Version = @expectedVersion;
                """;
            BindRoomParameters(cmd, room);
            cmd.Parameters.AddWithValue("@expectedVersion", expectedVersion);

            var rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
            if (rowsAffected == 0)
            {
                throw new ConcurrencyConflictException(
                    $"Optimistic concurrency conflict occurred updating room '{room.Id}' (expected version {expectedVersion}).");
            }

            room.MarkPersisted();
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    private static Room MapRoom(SqliteDataReader reader)
    {
        // Column order matches SelectColumns / BindRoomParameters exactly.
        return Room.Reconstitute(
            id: reader.GetString(0),
            landlordId: reader.GetString(1),
            title: reader.GetString(2),
            roomType: ParseEnum<RoomType>(reader.GetString(4)),
            monthlyRentVnd: reader.GetInt64(5),
            depositVnd: reader.GetInt64(6),
            areaM2: reader.GetDouble(7),
            addressLine: reader.GetString(11),
            ward: reader.GetString(12),
            district: reader.GetString(13),
            latitude: reader.GetDouble(14),
            longitude: reader.GetDouble(15),
            status: ParseEnum<ListingStatus>(reader.GetString(22)),
            createdAtUtc: ParseUtc(reader.GetString(30)),
            lastUpdatedAtUtc: ParseUtc(reader.GetString(29)),
            version: reader.GetInt32(31),
            concurrencyToken: reader.GetInt32(31).ToString(CultureInfo.InvariantCulture),
            description: GetNullableString(reader, 3),
            maxOccupants: reader.IsDBNull(8) ? null : reader.GetInt32(8),
            furnishing: GetNullableString(reader, 9),
            availableFrom: reader.IsDBNull(10) ? null : ParseDateOnly(reader.GetString(10)),
            electricityPriceVndPerKwh: GetNullableInt64(reader, 16),
            waterPriceVnd: GetNullableInt64(reader, 17),
            waterPriceUnit: GetNullableString(reader, 18),
            internetFeeVnd: GetNullableInt64(reader, 19),
            parkingFeeVnd: GetNullableInt64(reader, 20),
            otherFeesNote: GetNullableString(reader, 21),
            statusReason: GetNullableString(reader, 23),
            needsReReview: reader.GetInt64(24) != 0,
            isSafetyVerified: reader.GetInt64(25) != 0,
            verifiedAtUtc: GetNullableUtc(reader, 26),
            verifiedByUserId: GetNullableString(reader, 27),
            publishedAtUtc: GetNullableUtc(reader, 28));
    }

    private static void BindRoomParameters(SqliteCommand cmd, Room room)
    {
        cmd.Parameters.AddWithValue("@id", room.Id);
        cmd.Parameters.AddWithValue("@landlordId", room.LandlordId);
        cmd.Parameters.AddWithValue("@title", room.Title);
        cmd.Parameters.AddWithValue("@description", (object?)room.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@roomType", room.RoomType.ToString());
        cmd.Parameters.AddWithValue("@monthlyRentVnd", room.MonthlyRentVnd);
        cmd.Parameters.AddWithValue("@depositVnd", room.DepositVnd);
        cmd.Parameters.AddWithValue("@areaM2", room.AreaM2);
        cmd.Parameters.AddWithValue("@maxOccupants", (object?)room.MaxOccupants ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@furnishing", (object?)room.Furnishing ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@availableFrom", room.AvailableFrom.HasValue
            ? room.AvailableFrom.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : DBNull.Value);
        cmd.Parameters.AddWithValue("@addressLine", room.AddressLine);
        cmd.Parameters.AddWithValue("@ward", room.Ward);
        cmd.Parameters.AddWithValue("@district", room.District);
        cmd.Parameters.AddWithValue("@latitude", room.Latitude);
        cmd.Parameters.AddWithValue("@longitude", room.Longitude);
        cmd.Parameters.AddWithValue("@electricityPriceVndPerKwh", (object?)room.ElectricityPriceVndPerKwh ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@waterPriceVnd", (object?)room.WaterPriceVnd ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@waterPriceUnit", (object?)room.WaterPriceUnit ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@internetFeeVnd", (object?)room.InternetFeeVnd ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@parkingFeeVnd", (object?)room.ParkingFeeVnd ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@otherFeesNote", (object?)room.OtherFeesNote ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", room.Status.ToString());
        cmd.Parameters.AddWithValue("@statusReason", (object?)room.StatusReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@needsReReview", room.NeedsReReview ? 1 : 0);
        cmd.Parameters.AddWithValue("@isSafetyVerified", room.IsSafetyVerified ? 1 : 0);
        cmd.Parameters.AddWithValue("@verifiedAtUtc", room.VerifiedAtUtc.HasValue
            ? room.VerifiedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture)
            : DBNull.Value);
        cmd.Parameters.AddWithValue("@verifiedByUserId", (object?)room.VerifiedByUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@publishedAtUtc", room.PublishedAtUtc.HasValue
            ? room.PublishedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture)
            : DBNull.Value);
        cmd.Parameters.AddWithValue("@lastUpdatedAtUtc", room.LastUpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@createdAtUtc", room.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@version", room.Version);
    }

    private static TEnum ParseEnum<TEnum>(string value)
        where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(value, ignoreCase: false);

    private static DateTime ParseUtc(string iso8601) =>
        DateTime.Parse(iso8601, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static DateOnly ParseDateOnly(string value) =>
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? GetNullableString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static long? GetNullableInt64(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);

    private static DateTime? GetNullableUtc(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : ParseUtc(reader.GetString(ordinal));

    private void ReleaseConnection(SqliteConnection conn)
    {
        if (!_dbContext.OwnsSharedConnection)
        {
            conn.Dispose();
        }
    }
}
