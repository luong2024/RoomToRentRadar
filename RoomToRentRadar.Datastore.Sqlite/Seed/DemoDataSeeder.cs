using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Datastore.Sqlite.Seed;

/// <summary>
/// T00.10: idempotently seeds the full 30-room demo catalog and child tables
/// (<c>RoomAmenities</c>, <c>RoomSafetyFeatures</c>, <c>HouseRuleVersions</c>).
/// Child rows are inserted only when a room currently has zero child rows across all three tables
/// so subsequent startups never resurrect amenities/rules edited or removed by a user.
/// </summary>
internal static class DemoDataSeeder
{
    public static void Seed(SqliteConnection conn)
    {
        ArgumentNullException.ThrowIfNull(conn);

        var entries = DemoRoomCatalog.CreateAllEntries();
        using var tx = conn.BeginTransaction();

        foreach (var entry in entries)
        {
            DemoRoomRowWriter.InsertOrIgnore(conn, tx, entry.Room);

            if (!HasAnyChildRows(conn, tx, entry.Room.Id))
            {
                InsertAmenities(conn, tx, entry.Room);
                InsertSafetyFeatures(conn, tx, entry.Room);
                InsertHouseRuleVersions(conn, tx, entry.HouseRuleVersions);
            }
        }

        tx.Commit();
    }

    private static bool HasAnyChildRows(SqliteConnection conn, SqliteTransaction tx, string roomId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT
                (SELECT COUNT(1) FROM RoomAmenities WHERE RoomId = @roomId) +
                (SELECT COUNT(1) FROM RoomSafetyFeatures WHERE RoomId = @roomId) +
                (SELECT COUNT(1) FROM HouseRuleVersions WHERE RoomId = @roomId);
            """;
        cmd.Parameters.AddWithValue("@roomId", roomId);

        var count = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        return count > 0;
    }

    private static void InsertAmenities(SqliteConnection conn, SqliteTransaction tx, Room room)
    {
        if (room.Amenities.Count == 0)
        {
            return;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO RoomAmenities (RoomId, AmenityCode) VALUES (@roomId, @amenityCode);";
        cmd.Parameters.AddWithValue("@roomId", room.Id);
        var codeParam = cmd.Parameters.Add("@amenityCode", SqliteType.Text);

        foreach (var amenity in room.Amenities)
        {
            codeParam.Value = amenity.ToString();
            cmd.ExecuteNonQuery();
        }
    }

    private static void InsertSafetyFeatures(SqliteConnection conn, SqliteTransaction tx, Room room)
    {
        if (room.SafetyFeatures.Count == 0)
        {
            return;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO RoomSafetyFeatures (RoomId, FeatureCode, IsConfirmed)
            VALUES (@roomId, @featureCode, @isConfirmed);
            """;
        cmd.Parameters.AddWithValue("@roomId", room.Id);
        var codeParam = cmd.Parameters.Add("@featureCode", SqliteType.Text);
        var confirmedParam = cmd.Parameters.Add("@isConfirmed", SqliteType.Integer);

        foreach (var entry in room.SafetyFeatures)
        {
            codeParam.Value = entry.Feature.ToString();
            confirmedParam.Value = entry.IsConfirmed ? 1 : 0;
            cmd.ExecuteNonQuery();
        }
    }

    private static void InsertHouseRuleVersions(
        SqliteConnection conn,
        SqliteTransaction tx,
        IReadOnlyList<HouseRuleVersion> versions)
    {
        if (versions.Count == 0)
        {
            return;
        }

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
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

        var idParam = cmd.Parameters.Add("@id", SqliteType.Text);
        var roomIdParam = cmd.Parameters.Add("@roomId", SqliteType.Text);
        var versionParam = cmd.Parameters.Add("@versionNumber", SqliteType.Integer);
        var quietStartParam = cmd.Parameters.Add("@quietHoursStart", SqliteType.Text);
        var quietEndParam = cmd.Parameters.Add("@quietHoursEnd", SqliteType.Text);
        var curfewParam = cmd.Parameters.Add("@curfewTime", SqliteType.Text);
        var guestPolicyParam = cmd.Parameters.Add("@guestPolicy", SqliteType.Text);
        var guestNoteParam = cmd.Parameters.Add("@guestPolicyNote", SqliteType.Text);
        var cookingPolicyParam = cmd.Parameters.Add("@cookingPolicy", SqliteType.Text);
        var parkingAvailableParam = cmd.Parameters.Add("@parkingAvailable", SqliteType.Integer);
        var parkingRulesParam = cmd.Parameters.Add("@parkingRules", SqliteType.Text);
        var petsParam = cmd.Parameters.Add("@petsAllowed", SqliteType.Integer);
        var smokingParam = cmd.Parameters.Add("@smokingAllowed", SqliteType.Integer);
        var otherRulesParam = cmd.Parameters.Add("@otherRules", SqliteType.Text);
        var createdByParam = cmd.Parameters.Add("@createdByUserId", SqliteType.Text);
        var createdAtParam = cmd.Parameters.Add("@createdAtUtc", SqliteType.Text);

        foreach (var rules in versions)
        {
            idParam.Value = rules.Id;
            roomIdParam.Value = rules.RoomId;
            versionParam.Value = rules.VersionNumber;
            quietStartParam.Value = (object?)rules.QuietHoursStart ?? DBNull.Value;
            quietEndParam.Value = (object?)rules.QuietHoursEnd ?? DBNull.Value;
            curfewParam.Value = (object?)rules.CurfewTime ?? DBNull.Value;
            guestPolicyParam.Value = rules.GuestPolicy.ToString();
            guestNoteParam.Value = (object?)rules.GuestPolicyNote ?? DBNull.Value;
            cookingPolicyParam.Value = rules.CookingPolicy.ToString();
            parkingAvailableParam.Value = rules.ParkingAvailable ? 1 : 0;
            parkingRulesParam.Value = (object?)rules.ParkingRules ?? DBNull.Value;
            petsParam.Value = rules.PetsAllowed ? 1 : 0;
            smokingParam.Value = rules.SmokingAllowed ? 1 : 0;
            otherRulesParam.Value = (object?)rules.OtherRules ?? DBNull.Value;
            createdByParam.Value = rules.CreatedByUserId;
            createdAtParam.Value = rules.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture);
            cmd.ExecuteNonQuery();
        }
    }
}
