using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Domain.Aggregates;

namespace RoomToRentRadar.Datastore.Sqlite.Seed;

/// <summary>
/// Writes <see cref="Room"/> seed rows idempotently using <c>INSERT OR IGNORE</c>.
/// Shared by core room seeding and extended demo seeding.
/// </summary>
internal static class DemoRoomRowWriter
{
    private const string InsertSql = """
        INSERT OR IGNORE INTO Rooms (
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

    public static bool InsertOrIgnore(SqliteConnection conn, SqliteTransaction tx, Room room)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(tx);
        ArgumentNullException.ThrowIfNull(room);

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = InsertSql;

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

        return cmd.ExecuteNonQuery() > 0;
    }
}
