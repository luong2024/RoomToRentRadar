using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

/// <summary>
/// SQLite storage of the <see cref="Room"/> aggregate root against the ERD §4.4 <c>Rooms</c> table and
/// its children (§4.5 <c>RoomAmenities</c>, §4.6 <c>RoomSafetyFeatures</c>, §4.7 <c>HouseRuleVersions</c>).
/// Enums are stored as TEXT, date-times as ISO-8601 round-trip ("O") strings and <c>ConcurrencyToken</c>
/// as <c>Version.ToString()</c> (ERD §1: derived, never stored). Every command joins the ambient
/// <see cref="SqliteAmbientTransaction"/> when a unit of work is active.
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

    /// <summary>ERD §4.4 derived <c>CoverPhoto</c>: the thumbnail of the photo with the lowest SortOrder.</summary>
    private const string CoverThumbColumn = """
        (SELECT ThumbPath FROM RoomPhotos p WHERE p.RoomId = r.Id ORDER BY p.SortOrder ASC LIMIT 1)
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

        using var lease = SqliteConnectionLease.Acquire(_dbContext);
        return await LoadRoomAsync(lease, id, expectedLandlordId: null, ct);
    }

    /// <summary>Anti-IDOR (GUARDRAILS §1.5.6): a room of another landlord behaves exactly like a missing one.</summary>
    public async Task<Room?> GetOwnedByIdAsync(string id, string landlordId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(landlordId);

        using var lease = SqliteConnectionLease.Acquire(_dbContext);
        return await LoadRoomAsync(lease, id, landlordId, ct);
    }

    public async Task AddAsync(Room room, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(room);

        using var lease = SqliteConnectionLease.Acquire(_dbContext);
        await using (var cmd = lease.CreateCommand())
        {
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
        }

        await InsertAmenitiesAsync(lease, room, ct);
        await InsertSafetyFeaturesAsync(lease, room, ct);
        await InsertCurrentHouseRulesAsync(lease, room, ct);

        room.MarkPersisted();
    }

    /// <summary>
    /// Optimistic concurrency (GUARDRAILS 1.5.2): the row must still hold the version this instance
    /// was loaded with (<see cref="Room.LoadedVersion"/>). Using <c>Version - 1</c> would break as
    /// soon as a caller performs more than one transition between load and save. Zero rows updated
    /// means someone else wrote first → <see cref="ConcurrencyConflictException"/>.
    /// Children are replaced after the parent row wins the version check; <c>HouseRuleVersions</c> is
    /// append-only and deliberately untouched here (updateListing ignores house rules, openapi.yaml).
    /// </summary>
    public async Task UpdateAsync(Room room, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(room);

        var expectedVersion = room.LoadedVersion;

        using var lease = SqliteConnectionLease.Acquire(_dbContext);
        await using (var cmd = lease.CreateCommand())
        {
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
        }

        await DeleteAmenitiesAsync(lease, room.Id, ct);
        await InsertAmenitiesAsync(lease, room, ct);
        await DeleteSafetyFeaturesAsync(lease, room.Id, ct);
        await InsertSafetyFeaturesAsync(lease, room, ct);

        room.MarkPersisted();
    }

    /// <summary>BR-LST-11: <c>Unpublished</c> listings are archived and do not count toward the 50-listing cap.</summary>
    public async Task<int> CountNonArchivedByLandlordAsync(string landlordId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(landlordId);

        using var lease = SqliteConnectionLease.Acquire(_dbContext);
        await using var cmd = lease.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM Rooms WHERE LandlordId = @landlordId AND Status <> @archivedStatus;";
        cmd.Parameters.AddWithValue("@landlordId", landlordId);
        cmd.Parameters.AddWithValue("@archivedStatus", ListingStatus.Unpublished.ToString());

        var count = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(count, CultureInfo.InvariantCulture);
    }

    public async Task<PagedOwnedRooms> ListByLandlordAsync(
        string landlordId,
        ListingStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(landlordId);

        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            // GUARDRAILS 1.5.1 whitelist: a casted number must never reach a query.
            throw new DomainValidationException($"Listing status '{(int)status.Value}' is not a recognized status.");
        }

        var effectivePageNumber = Math.Max(pageNumber, 1);
        var effectivePageSize = Math.Max(pageSize, 0);
        var offset = ((long)effectivePageNumber - 1) * effectivePageSize;

        using var lease = SqliteConnectionLease.Acquire(_dbContext);

        var countsByStatus = await ReadCountsByStatusAsync(lease, landlordId, ct);
        var totalCount = await ReadTotalCountAsync(lease, landlordId, status, ct);
        var items = await ReadPageAsync(lease, landlordId, status, effectivePageSize, offset, ct);

        return new PagedOwnedRooms(items, totalCount, countsByStatus);
    }

    // ----------------------------------------------------------------- current row + children

    private static async Task<Room?> LoadRoomAsync(
        SqliteConnectionLease lease,
        string id,
        string? expectedLandlordId,
        CancellationToken ct)
    {
        Room? room;
        await using (var cmd = lease.CreateCommand())
        {
            cmd.CommandText = expectedLandlordId is null
                ? $"SELECT {SelectColumns} FROM Rooms WHERE Id = @id LIMIT 1;"
                : $"SELECT {SelectColumns} FROM Rooms WHERE Id = @id AND LandlordId = @landlordId LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", id);
            if (expectedLandlordId is not null)
            {
                cmd.Parameters.AddWithValue("@landlordId", expectedLandlordId);
            }

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            room = MapRoom(reader);
        }

        // Read after the row so a missing room costs one query only; SetChildren does not Touch().
        room.SetChildren(
            await LoadAmenitiesAsync(lease, id, ct),
            await LoadSafetyFeaturesAsync(lease, id, ct),
            await LoadCurrentHouseRulesAsync(lease, id, ct));

        return room;
    }

    private static async Task<IReadOnlyList<Amenity>> LoadAmenitiesAsync(
        SqliteConnectionLease lease,
        string roomId,
        CancellationToken ct)
    {
        await using var cmd = lease.CreateCommand();
        cmd.CommandText = "SELECT AmenityCode FROM RoomAmenities WHERE RoomId = @roomId ORDER BY AmenityCode;";
        cmd.Parameters.AddWithValue("@roomId", roomId);

        var amenities = new List<Amenity>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            amenities.Add(ParseEnum<Amenity>(reader.GetString(0)));
        }

        return amenities;
    }

    private static async Task<IReadOnlyList<RoomSafetyFeatureEntry>> LoadSafetyFeaturesAsync(
        SqliteConnectionLease lease,
        string roomId,
        CancellationToken ct)
    {
        await using var cmd = lease.CreateCommand();
        cmd.CommandText = """
            SELECT FeatureCode, IsConfirmed FROM RoomSafetyFeatures
            WHERE RoomId = @roomId ORDER BY FeatureCode;
            """;
        cmd.Parameters.AddWithValue("@roomId", roomId);

        var features = new List<RoomSafetyFeatureEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            features.Add(new RoomSafetyFeatureEntry(
                ParseEnum<SafetyFeature>(reader.GetString(0)),
                reader.GetInt64(1) != 0));
        }

        return features;
    }

    private static async Task<HouseRuleVersion?> LoadCurrentHouseRulesAsync(
        SqliteConnectionLease lease,
        string roomId,
        CancellationToken ct)
    {
        await using var cmd = lease.CreateCommand();
        cmd.CommandText = """
            SELECT Id, RoomId, VersionNumber, QuietHoursStart, QuietHoursEnd, CurfewTime, GuestPolicy,
                   GuestPolicyNote, CookingPolicy, ParkingAvailable, ParkingRules, PetsAllowed,
                   SmokingAllowed, OtherRules, CreatedByUserId, CreatedAtUtc
            FROM HouseRuleVersions
            WHERE RoomId = @roomId
            ORDER BY VersionNumber DESC
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@roomId", roomId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return HouseRuleVersion.Reconstitute(
            id: reader.GetString(0),
            roomId: reader.GetString(1),
            versionNumber: reader.GetInt32(2),
            guestPolicy: ParseEnum<GuestPolicy>(reader.GetString(6)),
            cookingPolicy: ParseEnum<CookingPolicy>(reader.GetString(8)),
            parkingAvailable: reader.GetInt64(9) != 0,
            petsAllowed: reader.GetInt64(11) != 0,
            smokingAllowed: reader.GetInt64(12) != 0,
            createdByUserId: reader.GetString(14),
            createdAtUtc: ParseUtc(reader.GetString(15)),
            quietHoursStart: GetNullableString(reader, 3),
            quietHoursEnd: GetNullableString(reader, 4),
            curfewTime: GetNullableString(reader, 5),
            guestPolicyNote: GetNullableString(reader, 7),
            parkingRules: GetNullableString(reader, 10),
            otherRules: GetNullableString(reader, 13));
    }

    private static async Task InsertAmenitiesAsync(SqliteConnectionLease lease, Room room, CancellationToken ct)
    {
        foreach (var amenity in room.Amenities)
        {
            await using var cmd = lease.CreateCommand();
            cmd.CommandText = "INSERT INTO RoomAmenities (RoomId, AmenityCode) VALUES (@roomId, @amenityCode);";
            cmd.Parameters.AddWithValue("@roomId", room.Id);
            cmd.Parameters.AddWithValue("@amenityCode", amenity.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task InsertSafetyFeaturesAsync(SqliteConnectionLease lease, Room room, CancellationToken ct)
    {
        foreach (var entry in room.SafetyFeatures)
        {
            await using var cmd = lease.CreateCommand();
            cmd.CommandText = """
                INSERT INTO RoomSafetyFeatures (RoomId, FeatureCode, IsConfirmed)
                VALUES (@roomId, @featureCode, @isConfirmed);
                """;
            cmd.Parameters.AddWithValue("@roomId", room.Id);
            cmd.Parameters.AddWithValue("@featureCode", entry.Feature.ToString());
            cmd.Parameters.AddWithValue("@isConfirmed", entry.IsConfirmed ? 1 : 0);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task InsertCurrentHouseRulesAsync(SqliteConnectionLease lease, Room room, CancellationToken ct)
    {
        if (room.CurrentHouseRules is not { } rules)
        {
            return;
        }

        await using var cmd = lease.CreateCommand();
        cmd.CommandText = """
            INSERT INTO HouseRuleVersions (
                Id, RoomId, VersionNumber, QuietHoursStart, QuietHoursEnd, CurfewTime, GuestPolicy,
                GuestPolicyNote, CookingPolicy, ParkingAvailable, ParkingRules, PetsAllowed,
                SmokingAllowed, OtherRules, CreatedByUserId, CreatedAtUtc
            ) VALUES (
                @id, @roomId, @versionNumber, @quietHoursStart, @quietHoursEnd, @curfewTime, @guestPolicy,
                @guestPolicyNote, @cookingPolicy, @parkingAvailable, @parkingRules, @petsAllowed,
                @smokingAllowed, @otherRules, @createdByUserId, @createdAtUtc
            );
            """;
        cmd.Parameters.AddWithValue("@id", rules.Id);
        cmd.Parameters.AddWithValue("@roomId", rules.RoomId);
        cmd.Parameters.AddWithValue("@versionNumber", rules.VersionNumber);
        cmd.Parameters.AddWithValue("@quietHoursStart", (object?)rules.QuietHoursStart ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@quietHoursEnd", (object?)rules.QuietHoursEnd ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@curfewTime", (object?)rules.CurfewTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@guestPolicy", rules.GuestPolicy.ToString());
        cmd.Parameters.AddWithValue("@guestPolicyNote", (object?)rules.GuestPolicyNote ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cookingPolicy", rules.CookingPolicy.ToString());
        cmd.Parameters.AddWithValue("@parkingAvailable", rules.ParkingAvailable ? 1 : 0);
        cmd.Parameters.AddWithValue("@parkingRules", (object?)rules.ParkingRules ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@petsAllowed", rules.PetsAllowed ? 1 : 0);
        cmd.Parameters.AddWithValue("@smokingAllowed", rules.SmokingAllowed ? 1 : 0);
        cmd.Parameters.AddWithValue("@otherRules", (object?)rules.OtherRules ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@createdByUserId", rules.CreatedByUserId);
        cmd.Parameters.AddWithValue("@createdAtUtc", rules.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task DeleteAmenitiesAsync(SqliteConnectionLease lease, string roomId, CancellationToken ct)
    {
        await using var cmd = lease.CreateCommand();
        cmd.CommandText = "DELETE FROM RoomAmenities WHERE RoomId = @roomId;";
        cmd.Parameters.AddWithValue("@roomId", roomId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task DeleteSafetyFeaturesAsync(SqliteConnectionLease lease, string roomId, CancellationToken ct)
    {
        await using var cmd = lease.CreateCommand();
        cmd.CommandText = "DELETE FROM RoomSafetyFeatures WHERE RoomId = @roomId;";
        cmd.Parameters.AddWithValue("@roomId", roomId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ----------------------------------------------------------------- own-listings page (UC-31)

    private static async Task<IReadOnlyDictionary<ListingStatus, int>> ReadCountsByStatusAsync(
        SqliteConnectionLease lease,
        string landlordId,
        CancellationToken ct)
    {
        var counts = Enum.GetValues<ListingStatus>().ToDictionary(status => status, _ => 0);

        await using var cmd = lease.CreateCommand();
        cmd.CommandText = "SELECT Status, COUNT(1) FROM Rooms WHERE LandlordId = @landlordId GROUP BY Status;";
        cmd.Parameters.AddWithValue("@landlordId", landlordId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var status = ParseEnum<ListingStatus>(reader.GetString(0));
            counts[status] = reader.GetInt32(1);
        }

        return counts;
    }

    private static async Task<int> ReadTotalCountAsync(
        SqliteConnectionLease lease,
        string landlordId,
        ListingStatus? status,
        CancellationToken ct)
    {
        await using var cmd = lease.CreateCommand();
        cmd.CommandText = status.HasValue
            ? "SELECT COUNT(1) FROM Rooms WHERE LandlordId = @landlordId AND Status = @status;"
            : "SELECT COUNT(1) FROM Rooms WHERE LandlordId = @landlordId;";
        cmd.Parameters.AddWithValue("@landlordId", landlordId);
        if (status.HasValue)
        {
            cmd.Parameters.AddWithValue("@status", status.Value.ToString());
        }

        var count = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(count, CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<OwnedRoomListing>> ReadPageAsync(
        SqliteConnectionLease lease,
        string landlordId,
        ListingStatus? status,
        int pageSize,
        long offset,
        CancellationToken ct)
    {
        await using var cmd = lease.CreateCommand();
        cmd.CommandText = $"""
            SELECT {SelectColumns}, {CoverThumbColumn} AS CoverThumbPath
            FROM Rooms r
            WHERE r.LandlordId = @landlordId{(status.HasValue ? " AND r.Status = @status" : string.Empty)}
            ORDER BY r.LastUpdatedAtUtc DESC, r.Id ASC
            LIMIT @pageSize OFFSET @offset;
            """;
        cmd.Parameters.AddWithValue("@landlordId", landlordId);
        if (status.HasValue)
        {
            cmd.Parameters.AddWithValue("@status", status.Value.ToString());
        }

        cmd.Parameters.AddWithValue("@pageSize", pageSize);
        cmd.Parameters.AddWithValue("@offset", offset);

        var items = new List<OwnedRoomListing>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            // Items intentionally carry no children: the own-listings page needs summary rows only.
            var coverThumbPath = GetNullableString(reader, 32);
            items.Add(new OwnedRoomListing(MapRoom(reader), coverThumbPath));
        }

        return items;
    }

    // ----------------------------------------------------------------- mapping

    private static Room MapRoom(SqliteDataReader reader)
    {
        // Column order matches SelectColumns / BindRoomParameters exactly (CoverThumbPath, when
        // selected, is appended after them and is not part of the aggregate).
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
}
