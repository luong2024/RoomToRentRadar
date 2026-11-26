using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Seed;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;

namespace RoomToRentRadarUnitTests.Search.Fixtures;

/// <summary>
/// US04 / T04.3: one deterministic room row used by the search fixtures. <see cref="VerifiedAtUtc"/> and
/// <see cref="LandlordIsVerified"/> are stored independently on purpose so BR-LST-06 can be tested with
/// adversarial combinations (Available but never verified, Available but landlord not verified).
/// </summary>
public sealed record SeedRoom(
    string Id,
    string LandlordId,
    string Title,
    RoomType RoomType,
    long MonthlyRentVnd,
    double AreaM2,
    double Latitude,
    double Longitude,
    ListingStatus Status,
    DateTime? PublishedAtUtc,
    DateTime? VerifiedAtUtc,
    bool LandlordIsVerified);

/// <summary>
/// Deterministic search datasets for US04/T04.3. Rows are inserted with raw parameterised SQL (not the
/// Room aggregate) because the adversarial cases — Available with <c>VerifiedAtUtc = NULL</c>, unverified
/// landlords, non-Available rows in filter range — are exactly the states the domain would normally reject.
/// </summary>
public static class SearchDatasetSeeder
{
    /// <summary>Kilometres per degree of latitude for the Haversine calculator (R = 6371 km).</summary>
    public const double KmPerDegreeLatitude = 111.19492664455873;

    private static readonly DateTime BasePublishedAtUtc = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DefaultVerifiedAtUtc = new(2026, 11, 1, 7, 30, 0, DateTimeKind.Utc);
    private const string CreatedAtUtc = "2026-11-01T07:00:00.0000000Z";

    public const string VerifiedLandlordId = "ld-verified";
    public const string UnverifiedLandlordId = "ld-unverified";

    // ---- Coordinates ------------------------------------------------------

    public static Campus Campus(string campusId) =>
        CampusSeedData.CreateCampuses().Single(campus => campus.Id == campusId);

    /// <summary>Latitude offset for a room exactly <paramref name="km"/> north of <paramref name="latitude"/>.</summary>
    public static double OffsetNorth(double latitude, double km) => latitude + km / KmPerDegreeLatitude;

    /// <summary>Longitude offset for a room exactly <paramref name="km"/> east of <paramref name="longitude"/>.</summary>
    public static double OffsetEast(double longitude, double latitude, double km) =>
        longitude + km / (KmPerDegreeLatitude * Math.Cos(latitude * Math.PI / 180.0));

    // ---- Datasets ---------------------------------------------------------

    /// <summary>
    /// ~50 visible rooms covering every filter boundary plus 7 noise rows that BR-LST-06 must always hide.
    /// All ids are prefixed so ordinal ordering is predictable (<c>c-…</c> visible, <c>n-…</c> noise,
    /// <c>t-…</c> TNUT/TNUE-relative).
    /// </summary>
    public static IReadOnlyList<SeedRoom> CreateCombinationDataset()
    {
        var ictu = Campus("ICTU");
        var tnut = Campus("TNUT");
        var tnue = Campus("TNUE");
        var rooms = new List<SeedRoom>(60);

        SeedRoom Ictu(
            string id,
            ListingStatus status,
            RoomType type,
            long rent,
            double area,
            double kmNorth,
            double kmEast,
            int? publishedMinute,
            bool verifiedAt = true,
            bool landlordVerified = true,
            string landlordId = VerifiedLandlordId)
        {
            var latitude = OffsetNorth(ictu.Latitude, kmNorth);
            var longitude = OffsetEast(ictu.Longitude, ictu.Latitude, kmEast);
            return new SeedRoom(
                id, landlordId, $"Fixture {id}", type, rent, area, latitude, longitude, status,
                publishedMinute is null ? null : BasePublishedAtUtc.AddMinutes(publishedMinute.Value),
                verifiedAt ? DefaultVerifiedAtUtc : null,
                landlordVerified);
        }

        // Distances 0 / 0.1 / 0.95 / 1.04 (rounds to 1.0) / 1.06 (rounds to 1.1) / 2 / 3 / 5 / 10 / 49.9 / 50.04 / 50.5 km.
        rooms.Add(Ictu("c-0000-zero", ListingStatus.Available, RoomType.BoardingRoom, 1_500_000, 18.0, 0.0, 0.0, 0));
        rooms.Add(Ictu("c-0100-tenth", ListingStatus.Available, RoomType.BoardingRoom, 1_200_000, 15.0, 0.1, 0.0, 1));
        rooms.Add(Ictu("c-0950", ListingStatus.Available, RoomType.MiniApartment, 2_000_000, 20.0, 0.95, 0.0, 2));
        rooms.Add(Ictu("c-1040", ListingStatus.Available, RoomType.MiniApartment, 2_500_000, 25.0, 1.04, 0.0, 3));
        rooms.Add(Ictu("c-1060", ListingStatus.Available, RoomType.WholeHouse, 2_500_000, 25.0, 1.06, 0.0, 4));
        rooms.Add(Ictu("c-1485-diag", ListingStatus.Available, RoomType.BoardingRoom, 1_500_000, 18.0, 0.99, 0.99, 5));
        rooms.Add(Ictu("c-2000", ListingStatus.Available, RoomType.Dormitory, 3_000_000, 35.0, 2.0, 0.0, 6));
        rooms.Add(Ictu("c-2000-east", ListingStatus.Available, RoomType.BoardingRoom, 2_500_000, 25.0, 0.0, 2.0, 7));
        rooms.Add(Ictu("c-2000-west", ListingStatus.Available, RoomType.BoardingRoom, 2_500_000, 25.0, 0.0, -2.0, 7));
        rooms.Add(Ictu("c-3000", ListingStatus.Available, RoomType.WholeHouse, 5_000_000, 50.0, 3.0, 0.0, 8));
        rooms.Add(Ictu("c-4000-nullpub", ListingStatus.Available, RoomType.MiniApartment, 4_000_000, 40.0, 4.0, 0.0, null));
        rooms.Add(Ictu("c-5000", ListingStatus.Available, RoomType.Dormitory, 10_000_000, 100.0, 5.0, 0.0, 9));
        rooms.Add(Ictu("c-10000", ListingStatus.Available, RoomType.WholeHouse, 50_000_000, 200.0, 10.0, 0.0, 10));
        rooms.Add(Ictu("c-49900", ListingStatus.Available, RoomType.BoardingRoom, 100_000, 5.0, 49.9, 0.0, 11));
        rooms.Add(Ictu("c-50500", ListingStatus.Available, RoomType.BoardingRoom, 1_500_000, 18.5, 50.5, 0.0, 12));
        // 50.04 km due East/West rounds to 50.0 km, so they are valid at maxDistanceKm=50 and must survive
        // the longitude half of the bounding box, which is wider than the exact cut only by the padding.
        rooms.Add(Ictu("c-50040-east", ListingStatus.Available, RoomType.BoardingRoom, 9_900_000, 44.0, 0.0, 50.04, 25));
        rooms.Add(Ictu("c-50040-west", ListingStatus.Available, RoomType.BoardingRoom, 9_900_000, 44.0, 0.0, -50.04, 26));

        // ±1 VND around common rent boundaries; ±1 m² around area boundaries.
        rooms.Add(Ictu("c-rent-1199999", ListingStatus.Available, RoomType.BoardingRoom, 1_199_999, 20.0, 0.0, 6.0, 13));
        rooms.Add(Ictu("c-rent-1200001", ListingStatus.Available, RoomType.BoardingRoom, 1_200_001, 20.0, 0.0, -6.0, 14));
        rooms.Add(Ictu("c-rent-1499999", ListingStatus.Available, RoomType.BoardingRoom, 1_499_999, 20.0, 0.0, 0.5, 15));
        rooms.Add(Ictu("c-rent-1500001", ListingStatus.Available, RoomType.BoardingRoom, 1_500_001, 20.0, 0.0, -0.5, 16));
        rooms.Add(Ictu("c-area-149", ListingStatus.Available, RoomType.BoardingRoom, 1_500_000, 14.9, 0.8, 0.0, 17));
        rooms.Add(Ictu("c-area-151", ListingStatus.Available, RoomType.BoardingRoom, 1_500_000, 15.1, 0.9, 0.0, 18));
        rooms.Add(Ictu("c-area-199", ListingStatus.Available, RoomType.BoardingRoom, 1_500_000, 19.9, 1.2, 0.0, 19));
        rooms.Add(Ictu("c-area-201", ListingStatus.Available, RoomType.BoardingRoom, 1_500_000, 20.1, 1.3, 0.0, 20));

        // Same rent, same distance band, same PublishedAtUtc -> exercises every tie-break (BR-SRCH-04).
        rooms.Add(Ictu("c-dup-newest-a", ListingStatus.Available, RoomType.Dormitory, 3_500_000, 30.0, 0.0, 3.0, 21));
        rooms.Add(Ictu("c-dup-newest-b", ListingStatus.Available, RoomType.Dormitory, 3_500_000, 30.0, 0.0, -3.0, 21));

        // Campus-relative rows: their distance/order changes with campusId (BR-CMP-03).
        rooms.Add(new SeedRoom(
            "t-near-tnut", VerifiedLandlordId, "Fixture t-near-tnut", RoomType.BoardingRoom, 1_500_000, 18.0,
            OffsetNorth(tnut.Latitude, 0.2), tnut.Longitude, ListingStatus.Available,
            BasePublishedAtUtc.AddMinutes(22), DefaultVerifiedAtUtc, true));
        rooms.Add(new SeedRoom(
            "t-far-tnut", VerifiedLandlordId, "Fixture t-far-tnut", RoomType.BoardingRoom, 1_500_000, 18.0,
            OffsetNorth(tnut.Latitude, 1.0), tnut.Longitude, ListingStatus.Available,
            BasePublishedAtUtc.AddMinutes(23), DefaultVerifiedAtUtc, true));
        rooms.Add(new SeedRoom(
            "t-near-tnue", VerifiedLandlordId, "Fixture t-near-tnue", RoomType.BoardingRoom, 1_500_000, 18.0,
            OffsetNorth(tnue.Latitude, 0.3), tnue.Longitude, ListingStatus.Available,
            BasePublishedAtUtc.AddMinutes(24), DefaultVerifiedAtUtc, true));

        // Filler rooms inside the common ranges so every filter combination has a non-trivial result set.
        for (var index = 0; index < 24; index++)
        {
            rooms.Add(Ictu(
                $"c-fill-{index:00}",
                ListingStatus.Available,
                (RoomType)(1 + index % 4),
                300_000 + index * 250_000,
                10.0 + index * 2,
                0.4 + index * 0.2,
                0.0,
                30 + index));
        }

        // ---- Noise rows (must never surface) ----
        rooms.Add(Ictu("n-pending", ListingStatus.PendingReview, RoomType.BoardingRoom, 1_500_000, 18.0, 0.5, 0.0, 40, verifiedAt: false));
        rooms.Add(Ictu("n-rejected", ListingStatus.Rejected, RoomType.BoardingRoom, 1_500_000, 18.0, 0.5, 0.0, 41, verifiedAt: false));
        rooms.Add(Ictu("n-rented", ListingStatus.Rented, RoomType.BoardingRoom, 1_500_000, 18.0, 0.5, 0.0, null));
        rooms.Add(Ictu("n-unpublished", ListingStatus.Unpublished, RoomType.BoardingRoom, 1_500_000, 18.0, 0.5, 0.0, null, verifiedAt: false));
        rooms.Add(Ictu("n-suspended", ListingStatus.Suspended, RoomType.BoardingRoom, 1_500_000, 18.0, 0.5, 0.0, null, verifiedAt: false));
        rooms.Add(Ictu("n-avail-unverified-landlord", ListingStatus.Available, RoomType.BoardingRoom, 1_500_000, 18.0, 0.5, 0.0, 42,
            landlordVerified: false, landlordId: UnverifiedLandlordId));
        rooms.Add(Ictu("n-avail-null-verified", ListingStatus.Available, RoomType.BoardingRoom, 1_500_000, 18.0, 0.5, 0.0, 43, verifiedAt: false));

        return rooms;
    }

    /// <summary>
    /// 1,000 Available rooms with verified landlords spread around the eight campuses plus 200 noise rows
    /// (150 non-Available across the five hidden statuses, 50 Available but owned by unverified landlords).
    /// Fully deterministic: the same <paramref name="seed"/> always produces the same rows.
    /// </summary>
    public static IReadOnlyList<SeedRoom> CreatePerformanceDataset(int seed = 20261125)
    {
        var campuses = CampusSeedData.CreateCampuses();
        var random = new Random(seed);
        var rooms = new List<SeedRoom>(1200);

        for (var index = 0; index < 1000; index++)
        {
            var campus = campuses[index % campuses.Count];
            var latitude = campus.Latitude + (random.NextDouble() * 6.0 - 3.0) / KmPerDegreeLatitude;
            var longitude = campus.Longitude + (random.NextDouble() * 6.0 - 3.0) / KmPerDegreeLatitude;
            rooms.Add(new SeedRoom(
                $"perf-{index:0000}",
                "ld-perf-verified",
                $"Perf room {index}",
                (RoomType)random.Next(1, 5),
                random.Next(80, 5001) * 1000L,
                Math.Round(8 + random.NextDouble() * 72, 1),
                latitude,
                longitude,
                ListingStatus.Available,
                BasePublishedAtUtc.AddMinutes(index),
                DefaultVerifiedAtUtc,
                true));
        }

        var hiddenStatuses = new[]
        {
            ListingStatus.PendingReview, ListingStatus.Rejected, ListingStatus.Rented,
            ListingStatus.Unpublished, ListingStatus.Suspended
        };

        var ictu = Campus("ICTU");
        for (var index = 0; index < 150; index++)
        {
            var status = hiddenStatuses[index % hiddenStatuses.Length];
            rooms.Add(new SeedRoom(
                $"noise-status-{index:000}",
                "ld-perf-verified",
                $"Noise room {index}",
                RoomType.BoardingRoom,
                1_500_000,
                18.0,
                OffsetNorth(ictu.Latitude, 0.5 + (index % 10) * 0.15),
                OffsetEast(ictu.Longitude, ictu.Latitude, 0.2 + (index % 5) * 0.2),
                status,
                null,
                status == ListingStatus.Rented ? DefaultVerifiedAtUtc : null,
                true));
        }

        for (var index = 0; index < 50; index++)
        {
            rooms.Add(new SeedRoom(
                $"noise-unverified-landlord-{index:000}",
                $"ld-perf-unverified-{index % 5}",
                $"Noise unverified landlord {index}",
                RoomType.BoardingRoom,
                1_500_000,
                18.0,
                OffsetNorth(ictu.Latitude, 0.5 + (index % 10) * 0.15),
                OffsetEast(ictu.Longitude, ictu.Latitude, 0.2 + (index % 5) * 0.2),
                ListingStatus.Available,
                BasePublishedAtUtc,
                DefaultVerifiedAtUtc,
                false));
        }

        return rooms;
    }

    // ---- Insertion --------------------------------------------------------

    public static string ExpectedCoverThumbPath(string roomId) => $"rooms/{roomId}/cover-t.jpg";

    public static void Seed(SqliteConnection connection, IReadOnlyList<SeedRoom> rooms)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(rooms);

        using var transaction = connection.BeginTransaction();
        InsertLandlords(connection, transaction, rooms);
        InsertRooms(connection, transaction, rooms);
        transaction.Commit();
    }

    /// <summary>
    /// Seeds three <c>RoomPhotos</c> rows per room (one <c>Panorama</c> at <c>SortOrder = 0</c> plus two
    /// <c>Standard</c> photos at <c>SortOrder = 1, 2</c>) so query-plan and 1,000-room benchmarks exercise
    /// the correlated <c>CoverThumbPath</c> subquery (BR-MED-05) against realistic photo density.
    /// </summary>
    public static void SeedPhotos(SqliteConnection connection, IReadOnlyList<SeedRoom> rooms)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(rooms);

        using var transaction = connection.BeginTransaction();
        InsertRoomPhotos(connection, transaction, rooms);
        transaction.Commit();
    }

    private static void InsertLandlords(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<SeedRoom> rooms)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO Users (
                Id, Email, NormalizedEmail, FullName, PasswordHash, Role, AuthProvider,
                SsoSubjectId, PhoneNumber, StudentCode, IsVerified, FailedLoginCount,
                LockoutEndUtc, CreatedAtUtc, UpdatedAtUtc
            ) VALUES (
                @id, @email, @normalizedEmail, @fullName, NULL, 'Landlord', 'Local',
                NULL, NULL, NULL, @isVerified, 0,
                NULL, @createdAtUtc, @createdAtUtc
            );
            """;

        var id = command.Parameters.Add("@id", SqliteType.Text);
        var email = command.Parameters.Add("@email", SqliteType.Text);
        var normalizedEmail = command.Parameters.Add("@normalizedEmail", SqliteType.Text);
        var fullName = command.Parameters.Add("@fullName", SqliteType.Text);
        var isVerified = command.Parameters.Add("@isVerified", SqliteType.Integer);
        command.Parameters.AddWithValue("@createdAtUtc", CreatedAtUtc);

        foreach (var landlord in rooms
                     .Select(room => (room.LandlordId, room.LandlordIsVerified))
                     .Distinct())
        {
            id.Value = landlord.LandlordId;
            email.Value = $"{landlord.LandlordId}@fixture.local";
            normalizedEmail.Value = $"{landlord.LandlordId}@fixture.local";
            fullName.Value = $"Fixture landlord {landlord.LandlordId}";
            isVerified.Value = landlord.LandlordIsVerified ? 1 : 0;
            command.ExecuteNonQuery();
        }
    }

    private static void InsertRooms(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<SeedRoom> rooms)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO Rooms (
                Id, LandlordId, Title, Description, RoomType, MonthlyRentVnd, DepositVnd, AreaM2,
                MaxOccupants, Furnishing, AvailableFrom, AddressLine, Ward, District, Latitude, Longitude,
                ElectricityPriceVndPerKwh, WaterPriceVnd, WaterPriceUnit, InternetFeeVnd, ParkingFeeVnd,
                OtherFeesNote, Status, StatusReason, NeedsReReview, IsSafetyVerified, VerifiedAtUtc,
                VerifiedByUserId, PublishedAtUtc, LastUpdatedAtUtc, CreatedAtUtc, Version
            ) VALUES (
                @id, @landlordId, @title, NULL, @roomType, @monthlyRentVnd, @depositVnd, @areaM2,
                2, 'Basic', '2026-11-01', @addressLine, 'Fixture Ward', 'TP. Thái Nguyên', @latitude, @longitude,
                3500, 50000, 'PerPerson', 100000, 0,
                NULL, @status, NULL, 0, 1, @verifiedAtUtc,
                NULL, @publishedAtUtc, @lastUpdatedAtUtc, @createdAtUtc, 1
            );
            """;

        var id = command.Parameters.Add("@id", SqliteType.Text);
        var landlordId = command.Parameters.Add("@landlordId", SqliteType.Text);
        var title = command.Parameters.Add("@title", SqliteType.Text);
        var roomType = command.Parameters.Add("@roomType", SqliteType.Text);
        var monthlyRentVnd = command.Parameters.Add("@monthlyRentVnd", SqliteType.Integer);
        var depositVnd = command.Parameters.Add("@depositVnd", SqliteType.Integer);
        var areaM2 = command.Parameters.Add("@areaM2", SqliteType.Real);
        var addressLine = command.Parameters.Add("@addressLine", SqliteType.Text);
        var latitude = command.Parameters.Add("@latitude", SqliteType.Real);
        var longitude = command.Parameters.Add("@longitude", SqliteType.Real);
        var status = command.Parameters.Add("@status", SqliteType.Text);
        var verifiedAtUtc = command.Parameters.Add("@verifiedAtUtc", SqliteType.Text);
        var publishedAtUtc = command.Parameters.Add("@publishedAtUtc", SqliteType.Text);
        var lastUpdatedAtUtc = command.Parameters.Add("@lastUpdatedAtUtc", SqliteType.Text);
        command.Parameters.AddWithValue("@createdAtUtc", CreatedAtUtc);

        foreach (var room in rooms)
        {
            id.Value = room.Id;
            landlordId.Value = room.LandlordId;
            title.Value = room.Title;
            roomType.Value = room.RoomType.ToString();
            monthlyRentVnd.Value = room.MonthlyRentVnd;
            depositVnd.Value = room.MonthlyRentVnd;
            areaM2.Value = room.AreaM2;
            addressLine.Value = $"{room.Id} street";
            latitude.Value = room.Latitude;
            longitude.Value = room.Longitude;
            status.Value = room.Status.ToString();
            verifiedAtUtc.Value = room.VerifiedAtUtc.HasValue
                ? room.VerifiedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture)
                : DBNull.Value;
            publishedAtUtc.Value = room.PublishedAtUtc.HasValue
                ? room.PublishedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture)
                : DBNull.Value;
            lastUpdatedAtUtc.Value = room.PublishedAtUtc.HasValue
                ? room.PublishedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture)
                : CreatedAtUtc;
            command.ExecuteNonQuery();
        }
    }

    private static void InsertRoomPhotos(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<SeedRoom> rooms)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO RoomPhotos (
                Id, RoomId, UploadedByUserId, Kind, ContentType, SizeBytes, Width, Height,
                OriginalPath, MediumPath, ThumbPath, SortOrder, CreatedAtUtc, AttachedAtUtc
            ) VALUES (
                @id, @roomId, @uploadedByUserId, @kind, 'image/jpeg', 65536, 1280, 720,
                @path, @path, @thumbPath, @sortOrder, @createdAtUtc, @createdAtUtc
            );
            """;

        var id = command.Parameters.Add("@id", SqliteType.Text);
        var roomId = command.Parameters.Add("@roomId", SqliteType.Text);
        var uploadedByUserId = command.Parameters.Add("@uploadedByUserId", SqliteType.Text);
        var kind = command.Parameters.Add("@kind", SqliteType.Text);
        var path = command.Parameters.Add("@path", SqliteType.Text);
        var thumbPath = command.Parameters.Add("@thumbPath", SqliteType.Text);
        var sortOrder = command.Parameters.Add("@sortOrder", SqliteType.Integer);
        command.Parameters.AddWithValue("@createdAtUtc", CreatedAtUtc);

        foreach (var room in rooms)
        {
            roomId.Value = room.Id;
            uploadedByUserId.Value = room.LandlordId;

            id.Value = $"{room.Id}-p0-pano";
            kind.Value = "Panorama";
            path.Value = $"rooms/{room.Id}/pano-m.jpg";
            thumbPath.Value = $"rooms/{room.Id}/pano-t.jpg";
            sortOrder.Value = 0;
            command.ExecuteNonQuery();

            id.Value = $"{room.Id}-p1-cover";
            kind.Value = "Standard";
            path.Value = $"rooms/{room.Id}/cover-m.jpg";
            thumbPath.Value = ExpectedCoverThumbPath(room.Id);
            sortOrder.Value = 1;
            command.ExecuteNonQuery();

            id.Value = $"{room.Id}-p2-extra";
            kind.Value = "Standard";
            path.Value = $"rooms/{room.Id}/extra-m.jpg";
            thumbPath.Value = $"rooms/{room.Id}/extra-t.jpg";
            sortOrder.Value = 2;
            command.ExecuteNonQuery();
        }
    }
}

/// <summary>One match the API is expected to return, computed independently of the production use case.</summary>
public sealed record OracleMatch(
    string Id,
    long MonthlyRentVnd,
    double AreaM2,
    RoomType RoomType,
    double DistanceKm,
    DateTime? PublishedAtUtc);

/// <summary>
/// Independent reference implementation of UC-05 / BR-SRCH-01..06 / BR-LST-06 over a <see cref="SeedRoom"/>
/// list. It intentionally does NOT model the SQL bounding box: that is an optimisation detail, and the
/// returned set must equal the exact Haversine cut.
/// </summary>
public sealed class SearchExpectation
{
    public SearchExpectation(IReadOnlyList<OracleMatch> ordered)
    {
        Ordered = ordered;
    }

    public IReadOnlyList<OracleMatch> Ordered { get; }

    public int TotalCount => Ordered.Count;

    public int TotalPages(int pageSize) => pageSize > 0 ? (int)Math.Ceiling((double)TotalCount / pageSize) : 0;

    public IReadOnlyList<OracleMatch> Page(int pageNumber, int pageSize)
    {
        var skip = (long)(pageNumber - 1) * pageSize;
        if (skip >= TotalCount || pageSize <= 0)
        {
            return Array.Empty<OracleMatch>();
        }

        return Ordered.Skip((int)skip).Take(pageSize).ToList();
    }
}

public static class SearchOracle
{
    public static SearchExpectation Search(
        IReadOnlyList<SeedRoom> rooms,
        RoomSearchRequest request,
        string? campusId = null)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(request);

        var campus = SearchDatasetSeeder.Campus(campusId ?? "ICTU");
        var sort = request.Sort ?? RoomSearchRequest.DefaultSort;
        var matches = new List<OracleMatch>();

        foreach (var room in rooms)
        {
            // BR-LST-06: public search shows only Available, verified listings owned by verified landlords.
            if (room.Status != ListingStatus.Available || !room.LandlordIsVerified || room.VerifiedAtUtc is null)
            {
                continue;
            }

            if (request.MinRentVnd is { } minRent && room.MonthlyRentVnd < minRent)
            {
                continue;
            }

            if (request.MaxRentVnd is { } maxRent && room.MonthlyRentVnd > maxRent)
            {
                continue;
            }

            if (request.MinAreaM2 is { } minArea && room.AreaM2 < minArea)
            {
                continue;
            }

            if (request.MaxAreaM2 is { } maxArea && room.AreaM2 > maxArea)
            {
                continue;
            }

            if (request.RoomTypes is { Count: > 0 } roomTypes && !roomTypes.Contains(room.RoomType))
            {
                continue;
            }

            var distanceKm = HaversineDistanceCalculator.CalculateDistanceKm(
                campus.Latitude, campus.Longitude, room.Latitude, room.Longitude);
            if (!double.IsFinite(distanceKm))
            {
                continue;
            }

            if (request.MaxDistanceKm is { } maxDistance && distanceKm > maxDistance)
            {
                continue;
            }

            matches.Add(new OracleMatch(
                room.Id, room.MonthlyRentVnd, room.AreaM2, room.RoomType, distanceKm, room.PublishedAtUtc));
        }

        IOrderedEnumerable<OracleMatch> ordered = sort switch
        {
            "price_asc" => matches.OrderBy(match => match.MonthlyRentVnd),
            "price_desc" => matches.OrderByDescending(match => match.MonthlyRentVnd),
            "newest" => matches.OrderByDescending(match => match.PublishedAtUtc ?? DateTime.MinValue),
            _ => matches.OrderBy(match => match.DistanceKm)
        };

        return new SearchExpectation(ordered.ThenBy(match => match.Id, StringComparer.Ordinal).ToList());
    }
}

/// <summary>Parsed <c>searchRooms</c> 200 response plus the JSON items for field-level assertions.</summary>
public sealed record SearchApiPage(
    IReadOnlyList<JsonElement> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage)
{
    public IReadOnlyList<string> Ids =>
        Items.Select(item => item.GetProperty("id").GetString()!).ToList();

    public IReadOnlyList<double> Distances =>
        Items.Select(item => item.GetProperty("distanceKm").GetDouble()).ToList();

    public static SearchApiPage Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var items = root.GetProperty("items").EnumerateArray().Select(item => item.Clone()).ToList();
        return new SearchApiPage(
            items,
            root.GetProperty("totalCount").GetInt32(),
            root.GetProperty("pageNumber").GetInt32(),
            root.GetProperty("pageSize").GetInt32(),
            root.GetProperty("totalPages").GetInt32(),
            root.GetProperty("hasNextPage").GetBoolean(),
            root.GetProperty("hasPreviousPage").GetBoolean());
    }
}

/// <summary>Small HTTP helpers shared by the T04.3 integration suites.</summary>
public static class SearchApiClient
{
    public static async Task<SearchApiPage> GetSearchPageAsync(this HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Unexpected {response.StatusCode} for '{url}': {body}");
        }

        return SearchApiPage.Parse(body);
    }

    public static async Task<ApiProblem> GetProblemAsync(this HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        string? errorCode = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("errorCode", out var code) && code.ValueKind == JsonValueKind.String)
            {
                errorCode = code.GetString();
            }
        }
        catch (JsonException)
        {
            // Non-JSON bodies are surfaced through StatusCode/ContentType assertions.
        }

        return new ApiProblem(response.StatusCode, response.Content.Headers.ContentType?.MediaType, errorCode, body);
    }

    /// <summary>Mirrors <c>HttpRoomGateway.BuildSearchUrl</c> so tests can drive the controller directly.</summary>
    public static string SearchUrl(RoomSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = new List<string>(10);
        if (request.CampusId is not null)
        {
            query.Add($"campusId={Uri.EscapeDataString(request.CampusId)}");
        }

        if (request.MinRentVnd is { } minRent)
        {
            query.Add($"minRentVnd={minRent.ToString(CultureInfo.InvariantCulture)}");
        }

        if (request.MaxRentVnd is { } maxRent)
        {
            query.Add($"maxRentVnd={maxRent.ToString(CultureInfo.InvariantCulture)}");
        }

        if (request.MinAreaM2 is { } minArea)
        {
            query.Add($"minAreaM2={minArea.ToString(CultureInfo.InvariantCulture)}");
        }

        if (request.MaxAreaM2 is { } maxArea)
        {
            query.Add($"maxAreaM2={maxArea.ToString(CultureInfo.InvariantCulture)}");
        }

        if (request.RoomTypes is not null)
        {
            query.Add($"roomTypes={Uri.EscapeDataString(string.Join(",", request.RoomTypes.Select(type => type.ToString())))}");
        }

        if (request.MaxDistanceKm is { } maxDistance)
        {
            query.Add($"maxDistanceKm={maxDistance.ToString(CultureInfo.InvariantCulture)}");
        }

        if (request.Sort is not null)
        {
            query.Add($"sort={Uri.EscapeDataString(request.Sort)}");
        }

        query.Add($"pageNumber={request.PageNumber.ToString(CultureInfo.InvariantCulture)}");
        query.Add($"pageSize={request.PageSize.ToString(CultureInfo.InvariantCulture)}");
        return $"/api/rooms?{string.Join("&", query)}";
    }
}

public sealed record ApiProblem(HttpStatusCode StatusCode, string? ContentType, string? ErrorCode, string Body);

/// <summary>
/// Boots the real API (<see cref="WebApplicationFactory{TEntryPoint}"/>) on an isolated SQLite database with
/// demo seeding disabled, so every test owns 100% of the data. Dispose removes the temp file (and any WAL
/// side files) to keep the working tree and temp folder clean.
/// </summary>
public sealed class SearchTestFactory : IDisposable
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";

    private readonly WebApplicationFactory<Program> _factory;
    private bool _disposed;

    public SearchTestFactory(bool useFileDatabase = false)
    {
        DatabasePath = useFileDatabase
            ? Path.Combine(Path.GetTempPath(), $"rtrr-search-{Guid.NewGuid():N}.db")
            : null;

        var connectionString = DatabasePath is null ? "Data Source=:memory:" : $"Data Source={DatabasePath}";
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", connectionString);
            webHost.UseSetting("Sqlite:SeedDemoRooms", "false");
            webHost.UseSetting("Sqlite:SeedExtendedDemoData", "false");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
            webHost.UseSetting("Routing:OpenRouteServiceApiKey", "test-ors-api-key");
        });
        Client = _factory.CreateClient();
    }

    public HttpClient Client { get; }

    public string? DatabasePath { get; }

    public WebApplicationFactory<Program> WebApp => _factory;

    public void Seed(IReadOnlyList<SeedRoom> rooms)
    {
        using var lease = OpenConnection();
        SearchDatasetSeeder.Seed(lease.Connection, rooms);
    }

    public void SeedPhotos(IReadOnlyList<SeedRoom> rooms)
    {
        using var lease = OpenConnection();
        SearchDatasetSeeder.SeedPhotos(lease.Connection, rooms);
    }

    public long QueryScalar(string sql)
    {
        using var lease = OpenConnection();
        using var command = lease.Connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public void Execute(string sql)
    {
        using var lease = OpenConnection();
        using var command = lease.Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public SqliteConnectionLease OpenConnection()
    {
        var dbContext = _factory.Services.GetRequiredService<SqliteDbContext>();
        var connection = dbContext.CreateOpenConnection();
        return new SqliteConnectionLease(connection, ownsConnection: !dbContext.OwnsSharedConnection);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Client.Dispose();
        _factory.Dispose();
        SqliteConnection.ClearAllPools();

        if (DatabasePath is null)
        {
            return;
        }

        foreach (var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" })
        {
            var path = DatabasePath + suffix;
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                    // Best effort: a locked side file in temp must never fail a test run.
                }
            }
        }
    }

    /// <summary>Never disposes the shared <c>:memory:</c> keep-alive connection owned by <see cref="SqliteDbContext"/>.</summary>
    public sealed class SqliteConnectionLease : IDisposable
    {
        private readonly bool _ownsConnection;

        public SqliteConnectionLease(SqliteConnection connection, bool ownsConnection)
        {
            Connection = connection;
            _ownsConnection = ownsConnection;
        }

        public SqliteConnection Connection { get; }

        public void Dispose()
        {
            if (_ownsConnection)
            {
                Connection.Dispose();
            }
        }
    }
}
